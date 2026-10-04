using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Gameplay;
using Gamejam2.Data;
using Gamejam2.Rhythm;
using Gamejam2.Settings;
namespace Gamejam2.Debugging;
/// <summary>Developer scene: full real-time current MP3 run; seeks only in the subsequent EX visual fixture.</summary>
public partial class GameplayJuiceChecks : Node
{
    private readonly InputEventAction _bite=new(){Action="rhythm_input",Pressed=true};
    private GameplayScreen? _game;
    private int _press,_checks;
    private bool _auto;
    private double _captureAt=double.PositiveInfinity;
    private string _captureName="";
    private readonly double[] _errors={0,.1,.175,-.22};
    private void Check(bool condition,string label){if(!condition)throw new Exception(label);_checks++;GD.Print("PASS "+label);}
    private async Task Wait(double seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private void Capture(string label){RenderingServer.ForceDraw();GetViewport().GetTexture().GetImage().SavePng("/private/tmp/bite-juice-"+label+".png");}
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    public override void _Process(double delta)
    {
        if(_game==null)return;
        if(_game.Rhythm.IsFocusPaused)GetWindow().GrabFocus();
        double time=_game.Rhythm.SongTimeSeconds;
        if(_auto&&!_game.InputBlocked&&_press<_game.Data.Phrases.Count)
        {
            double error=_press<_errors.Length?_errors[_press]:0;
            if(time>=_game.Data.Phrases[_press].TargetSeconds+error)
            {
                _game._Input(_bite);
                GD.Print($"FULL RUN bite #{_press} at {time:F3}: {_game.Rhythm.LastResult?.Judgment}, error={_game.Rhythm.LastResult?.TimingErrorSeconds*1000:F2}ms, cue={_game.Rhythm.LastResult?.TargetEvent.CueId}");
                if(_press<4||(_press==6&&_game.Rhythm.LastResult?.Judgment==RhythmJudgment.Perfect)){_captureAt=time+.07;_captureName=_game.Rhythm.LastResult?.Judgment.ToString()??"unknown";}
                _press++;
            }
        }
        if(time>=_captureAt){Capture(_captureName);_captureAt=double.PositiveInfinity;}
    }
    private void Seek(GameplayScreen game,double time)
    {
        var flags=BindingFlags.NonPublic|BindingFlags.Instance;var type=typeof(RhythmController);
        type.GetField("_clockBaseSeconds",flags)!.SetValue(game.Rhythm,time);
        type.GetField("_startTimeUsec",flags)!.SetValue(game.Rhythm,Time.GetTicksUsec());
        type.GetField("_audioStartDelaySeconds",flags)!.SetValue(game.Rhythm,0d);game.Music.Seek((float)time);
    }
    private GameplayScreen Spawn(string id)
    {var game=GD.Load<PackedScene>("res://game/gameplay/Gameplay.tscn").Instantiate<GameplayScreen>();game.SongId=id;AddChild(game);game.SetProcessInput(false);return game;}
    private async Task Run()
    {
        try
        {
            Gamejam2.Save.SaveStore.Initialize("/private/tmp/bite-juice-settings.cfg");Gamejam2.Save.ProgressService.Initialize();SettingsService.Initialize();
            SettingsService.InputOffsetSeconds=SettingsService.VisualOffsetSeconds=0;SettingsService.SetResolution(2);GetWindow().GrabFocus();
            _game=Spawn("song_1");_auto=true;
            await Wait(.15);Check(_game.RunState==GameplayScreen.GameplayRunState.Playing,"real Hear the Tide playback starts");
            var mouth=_game.Juice.MouthAnchor.GlobalPosition;
            // Allow the complete song to run without skips, with deliberate four judgment qualities.
            while(_press<6){await Wait(.2);CheckEarlyResult();}
            _game._Input(new InputEventAction{Action="ui_cancel",Pressed=true});await Wait(.35);
            double frozen=_game.Rhythm.SongTimeSeconds;await Wait(.2);
            Check(_game.InputBlocked&&_game.Rhythm.SongTimeSeconds==frozen,"settings freezes authoritative clock and inputs");
            Check(_game.Juice.PauseBlend>.9,"pause presentation eases in without advancing clock");Capture("pause");
            _game.StandaloneSettings.Close();await Wait(.4);
            Check(_game.Countdown.IsActive,"normal settings close starts sonar countdown");Capture("countdown");
            for(int i=0;i<100&&_game.InputBlocked;i++)await Wait(.1);
            GD.Print($"RESUME state={_game.RunState} focus={_game.Rhythm.IsFocusPaused} manual={_game.Rhythm.IsManuallyPaused} countdown={_game.Countdown.IsActive} time={_game.Rhythm.SongTimeSeconds:F3} frozen={frozen:F3}");
            await Wait(.1);Check(!_game.InputBlocked&&_game.Rhythm.SongTimeSeconds>frozen,"countdown resumes same clock/audio");
            while(!_game.Result.Visible){await Wait(.5);CheckEarlyResult();}
            _auto=false;await Wait(2);
            var result=_game.Result.Result!;
            GD.Print($"FULL RUN totals P={result.Perfect} G={result.Good} B={result.Bad} M={result.Miss}");
            Check(_press==_game.Data.Phrases.Count&&result.Perfect+result.Good+result.Bad+result.Miss==_game.Data.Phrases.Count&&result.Perfect>0&&result.Good>0&&result.Bad>0&&result.Miss>0,"full real-time revised song has all four judgments and every consumed target");
            Check(_game.ResultPresentationCount==1,"result appears once at real song end");
            Check(_game.Juice.EncounterCueCount==_game.Data.Phrases.Count&&_game.Juice.JudgmentEffectCount==_game.Data.Phrases.Count,"every encounter and judgment drives presentation once");
            Check(_game.Juice.BiteEffectCount>=_game.Data.Phrases.Count,"each input immediately creates bite response");
            Check(_game.Juice.InterferencePulseCount>0,"authored interference emits presentation pulses");
            Check(_game.Juice.MouthAnchor.GlobalPosition==mouth,"juice never shifts the fixed bite origin");
            Check(Math.Abs(_game.Gauge.DisplayedValue-_game.Satiety.Value)<.01,"gauge settles to authoritative satiety without altering rewards");Capture("full-result");
            _game.StopGameplay();_game.QueueFree();_game=null;await Wait(.05);
            var ex=Spawn("song_4");_game=ex;await Wait(.15);
            double now=ex.Rhythm.SongTimeSeconds;
            ex.Combo.UpdateCombo(1,now,RhythmJudgment.Perfect);float perfectPop=ex.Combo.Scale.X;
            ex.Combo.UpdateCombo(1,now,RhythmJudgment.Good);float goodPop=ex.Combo.Scale.X;
            ex.Combo.UpdateCombo(1,now,RhythmJudgment.Bad);float badPop=ex.Combo.Scale.X;
            Check(perfectPop>goodPop&&goodPop>badPop,"combo pop strength follows judgment quality");
            ex.Combo.UpdateCombo(10,now,RhythmJudgment.Perfect);ex.Combo.Present(now+.1);
            Check(ex.Combo.Accent.Visible,"milestone adds compact HUD sonar ring");ex.Combo.Reset();
            var pressure=LocalCsv.Read("res://data/balance/environment_sequence.csv").First(r=>r["song_id"]=="song_4"&&r["transition_style"]=="pressure");
            Seek(ex,LocalCsv.Number(pressure["start_time_sec"]));await Wait(.15);
            Check(ex.Environment.ActiveEnvironmentSongId==pressure["environment_song_id"]&&ex.Environment.TransitionProgress>0&&ex.Environment.TransitionProgress<1,"EX pressure transition blends instead of hard swap");
            Check(ex.Environment.PreviousBackground.Visible&&ex.Environment.Background.SelfModulate.A<1,"EX two authored backgrounds crossfade");Capture("ex-midblend");
            ex.SuspendForSettings();float blend=ex.Environment.TransitionProgress;await Wait(.2);
            Check(ex.Environment.TransitionProgress==blend,"EX transition freezes with the existing song clock");
            ex.BeginResumeCountdown();await Wait(3.4);Check(!ex.Environment.PreviousBackground.Visible,"EX transition settles after synchronized resume");
            foreach(string type in new[]{"whale","fishing_float","sardine"})
            {ex.Interference.Trigger(type,ex.Rhythm.SongTimeSeconds);await Wait(.12);Capture(type);Check(ex.Interference.GetEffect(type).EmissionCount>=1,type+" keeps existing emissions and layered feedback");}
            typeof(SatietyState).GetProperty("Value")!.SetValue(ex.Satiety,200d);await Wait(.1);Capture("low-satiety");
            Check(Math.Abs(ex.Gauge.DisplayedPercent-ex.Satiety.Value/10)<.1,"low-satiety warning remains a presentation-only tint");
            typeof(SatietyState).GetProperty("Value")!.SetValue(ex.Satiety,0d);await Wait(1.2);
            Check(ex.GameOver.Visible&&!ex.Result.Visible,"starvation retains separate short Game Over");Capture("game-over");
            GD.Print($"GAMEPLAY JUICE VERIFIED: {_checks} checks; full current-source MP3 run, no chart seeks during full song.");GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
    private void CheckEarlyResult()
    {if(_game!.Result.Visible&&_game.Rhythm.SongTimeSeconds<_game.Rhythm.AppliedSongEndTimeSeconds)throw new Exception("premature result");}
}
