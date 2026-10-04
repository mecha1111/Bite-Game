using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Gameplay;
using Gamejam2.Startup;
using Gamejam2.Rhythm;
using Gamejam2.Settings;
using Gamejam2.Data;
using Gamejam2.Save;
using Gamejam2.Calibration;
using Gamejam2.Title;
using Gamejam2.Lobby;
namespace Gamejam2.Debugging;
/// <summary>Actual router/input regression with isolated save; chart-driven fixtures, no production seek API.</summary>
public partial class GameplayChecks : Node
{
    private int _checks;
    private GameplayScreen _game=null!;
    private SceneRouter _router=null!;
    private async Task Wait(double seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private async Task Until(Func<bool> condition,double seconds=12)
    {
        ulong end=Time.GetTicksMsec()+(ulong)(seconds*1000);
        while(!condition()) {if(Time.GetTicksMsec()>end)throw new TimeoutException("Gameplay regression deadline");GetWindow().GrabFocus();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    }
    private void Check(bool ok,string name){if(!ok)throw new Exception(name);_checks++;GD.Print("PASS "+name);}
    private void Press(){Input.ParseInputEvent(new InputEventKey{PhysicalKeycode=Key.Space,Pressed=true});Input.FlushBufferedEvents();Input.ParseInputEvent(new InputEventKey{PhysicalKeycode=Key.Space,Pressed=false});Input.FlushBufferedEvents();}
    private void SeekFixture(double time)
    {
        _game.Rhythm.PauseSong();
        typeof(RhythmController).GetField("_stoppedTimeSeconds",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(_game.Rhythm,time);
        _game.Rhythm.ResumeSong();
    }
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Run()
    {
        try
        {
            _router=GD.Load<PackedScene>("res://game/startup/Startup.tscn").Instantiate<SceneRouter>();
            _router.SavePath="/private/tmp/bite-cleanup-flow-"+OS.GetProcessId()+".cfg";AddChild(_router);GetWindow().GrabFocus();
            Check(_router.ScreenHost!.GetChild(0) is TitleScreen,"Startup opens actual Title");
            ((TitleScreen)_router.ScreenHost.GetChild(0)).StartButton.EmitSignal(BaseButton.SignalName.Pressed);await Wait(.1);
            var calibration=_router.ScreenHost.GetChild<CalibrationScreen>(0);
            Check(calibration.Flow!.Calibration!.Stage==CalibrationStage.Entry,"first launch requires Calibration");
            SaveStore.Write("progress","tutorial_completed",true);SaveStore.Flush();
            calibration.Flow.Calibration.SkipWithDefaults();await Wait(.2);
            Check(_router.ScreenHost.GetChild(0) is LobbyScreen,"Calibration completion opens Song Select");
            SettingsService.InputOffsetSeconds=SettingsService.VisualOffsetSeconds=0;
            _router.GoToGameplay("stage_1");await Wait(.1);_game=_router.ScreenHost.GetChild<GameplayScreen>(0);
            Check(_game.Data.Phrases.Count==LocalCsv.Read(_game.Data.ChartPath).Count,"actual selected chart count");
            Check(_game.Data.Phrases.All(p=>Math.Abs(p.TargetSeconds-p.StartSeconds-240/_game.Data.Chart.Bpm)<1e-9),"every Bite target stays on beat five");
            Check(_game.Signals.LeftLane.Slots.Length==10&&_game.Signals.RightLane.Slots.Length==10,"fixed mirrored rhythm origins");
            var positions=_game.Signals.LeftLane.Slots.Select(s=>s.Position).ToArray();
            await Until(()=>_game.Rhythm.IsClockAdvancing);Press();
            Check(_game.Satiety.EmptyBites==1&&_game.Satiety.CreateResult().Miss==1,"empty physical Bite is MISS");
            var prey=_game.Data.Phrases[0];await Until(()=>_game.Rhythm.SongTimeSeconds>=prey.TargetSeconds);
            Press();Check(_game.Rhythm.LastResult?.Judgment==RhythmJudgment.Perfect&&_game.Satiety.Combo==1,"actual first authored target PERFECT");
            Press();Check(_game.Satiety.Combo==1,"hardware debounce does not resolve target twice");
            _router.Settings!.Open();await Wait(.35);double frozen=_game.Rhythm.SongTimeSeconds,satiety=_game.Satiety.Value;int bites=_game.Shark.BiteCount;
            Press();await Wait(.1);Check(_game.Rhythm.SongTimeSeconds==frozen&&_game.Satiety.Value==satiety&&!_game.Music.Playing&&_game.Shark.BiteCount==bites,"Settings freezes clock/music/satiety/input");
            _router.Settings.Close();await Until(()=>_game.IsCountingDown);
            Check(_game.Countdown.CurrentNumber=="3","resume countdown 3");Press();
            await Until(()=>_game.Countdown.CurrentNumber=="2");Check(_game.Rhythm.SongTimeSeconds==frozen,"countdown 2 keeps clock frozen");
            await Until(()=>_game.Countdown.CurrentNumber=="1");Check(!_game.Music.Playing&&_game.Shark.BiteCount==bites,"countdown 1 blocks music and Bite");
            await Until(()=>!_game.IsCountingDown);await Until(()=>_game.Rhythm.SongTimeSeconds>frozen+.1);
            double heard=_game.Music.GetPlaybackPosition()+AudioServer.GetTimeSinceLastMix()-AudioServer.GetOutputLatency();
            Check(Math.Abs(heard-_game.Rhythm.SongTimeSeconds)<.05,"resumed music/Song Clock within 50ms");
            Check(_game.Signals.LeftLane.Slots.Select(s=>s.Position).SequenceEqual(positions),"origins never travel");
            typeof(SatietyState).GetProperty("Value")!.SetValue(_game.Satiety,0d);await Wait(.1);
            Check(_game.GameOver.Visible&&!_game.Result.Visible,"zero satiety opens only Game Over");
            _game.GameOver.Reveal();_game.GameOver.Actions.RetryButton.EmitSignal(BaseButton.SignalName.Pressed);await Wait(.1);
            Check(_game.RunState==GameplayScreen.GameplayRunState.Playing&&_game.Satiety.Value>0,"Game Over retry resets current song");
            _game.SetProcessInput(false);
            // Full-song drain cannot be skipped without food: resolve the authored targets
            // before the EOF routing fixture instead of creating an impossible starving run.
            foreach(var p in _game.Data.Phrases)
            {SeekFixture(p.TargetSeconds-.03);await Until(()=>_game.Rhythm.IsClockAdvancing&&_game.Rhythm.SongTimeSeconds>=p.TargetSeconds);_game._Input(new InputEventKey{PhysicalKeycode=Key.Space,Pressed=true});}
            SeekFixture(_game.Rhythm.AppliedSongEndTimeSeconds+.05);await Wait(.2);
            Check(_game.Result.Visible&&_game.ResultPresentationCount==1&&!_game.GameOver.Visible,"song-end fixture opens one Result");
            _game.Result.Reveal();_game.Result.RetryButton.EmitSignal(BaseButton.SignalName.Pressed);await Wait(.1);
            Check(_game.RunState==GameplayScreen.GameplayRunState.Playing&&!_game.Result.Visible,"Result retry resets current song");
            _router.GoToLobby();await Wait(.1);Check(_router.ScreenHost.GetChild(0) is LobbyScreen,"return to Song Select");
            ProgressService.UnlockStage("stage_4");_router.GoToGameplay("stage_4");await Wait(.1);_game=_router.ScreenHost.GetChild<GameplayScreen>(0);
            Check(_game.SongId=="song_4"&&_game.RunState==GameplayScreen.GameplayRunState.Playing,"EX selection loads actual current song");
            _game.StopGameplay();_router.QueueFree();await Wait(.1);GD.Print($"GAMEPLAY FLOW VERIFIED: {_checks} checks");GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
