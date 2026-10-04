using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Tutorial;
using Gamejam2.Gameplay;
using Gamejam2.Rhythm;
using Gamejam2.Settings;
using Gamejam2.Save;
namespace Gamejam2.Debugging;
public partial class TutorialStateChecks : Node
{
    private TutorialGameplayScreen _game=null!;
    private int _checks;
    private void Check(bool ok,string label){if(!ok)throw new Exception(label);_checks++;GD.Print("PASS "+label);}
    private async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    private async Task Until(Func<bool> condition,double seconds=20)
    {ulong end=Time.GetTicksMsec()+(ulong)(seconds*1000);while(!condition()){if(Time.GetTicksMsec()>end)throw new TimeoutException("tutorial state test");await Frame();}}
    private void Press(){foreach(bool down in new[]{true,false}){Input.ParseInputEvent(new InputEventKey{PhysicalKeycode=Key.Space,Pressed=down});Input.FlushBufferedEvents();}}
    private async Task Hit(int index,double error)
    {double when=_game.Data.Phrases[index].TargetSeconds+error-_game.Rhythm.AppliedUserOffsetSeconds;await Until(()=>_game.Rhythm.IsClockAdvancing&&_game.Rhythm.SongTimeSeconds>=when);Press();GD.Print($"STATE HIT #{index}: {_game.Rhythm.LastResult?.Judgment} error={_game.Rhythm.LastResult?.TimingErrorSeconds} successes={_game.SuccessCount} state={_game.LessonState} input={_game.PracticeInputEnabled}");}
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Run()
    {
        try
        {
            SaveStore.Initialize("/private/tmp/bite-tutorial-state-"+OS.GetProcessId()+".cfg");ProgressService.Initialize();SettingsService.Initialize();SettingsService.InputOffsetSeconds=.08;SettingsService.VisualOffsetSeconds=-.04;
            double baseline=Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
            _game=GD.Load<PackedScene>("res://game/tutorial/TutorialGameplay.tscn").Instantiate<TutorialGameplayScreen>();_game.IsReplay=true;AddChild(_game);
            var originalMusic=_game.Music.Stream;
            Check(_game.Rhythm.AppliedUserOffsetSeconds==.08&&SettingsService.VisualOffsetSeconds==-.04,"tutorial retains independent calibrated offsets");
            await Until(()=>!_game.IsDemonstrating);
            Check(_game.Rhythm.LastResult?.Judgment==RhythmJudgment.Perfect&&_game.SuccessCount==0,"offset-compensated demonstration earns no player mastery");
            await Until(()=>_game.PracticeInputEnabled);Check(Math.Abs(_game.LastDemonstrationVisualSeconds-_game.LastDemonstrationChartSeconds-SettingsService.VisualOffsetSeconds)<.035,"demonstration Bite and real green pulse share Visual Offset timing");
            await Hit(1,.175);Check(_game.Rhythm.LastResult?.Judgment==RhythmJudgment.Bad&&_game.SuccessCount==0,"BAD catches without increasing mastery");
            await Hit(2,.100);Check(_game.Rhythm.LastResult?.Judgment==RhythmJudgment.Good&&_game.SuccessCount==1,"GOOD advances mastery by one");
            await Hit(3,0);Check(_game.SuccessCount==2,"non-consecutive successful catches retained");
            await Until(()=>_game.CheckpointLoops==1&&_game.Rhythm.SongTimeSeconds<_game.Data.Phrases[0].StartSeconds);
            Check(_game.Music.Stream==originalMusic&&_game.MusicCheckpointRestarts==0,"failed lesson moves forward without restarting music");
            Check(_game.LessonIndex==0,"two successes cannot advance lesson");
            await Hit(0,0);Check(_game.SuccessCount==3,"third GOOD+ satisfies mastery");
            await Until(()=>_game.LessonIndex==1);Check(_game.SuccessCount==0,"next lesson resets mastery");
            _game.StandaloneSettings.SetSongTitle(_game.Data.SongName);_game.StandaloneSettings.Open();await Until(()=>_game.InputBlocked);
            double frozen=_game.Rhythm.SongTimeSeconds;await Frame();Check(_game.Rhythm.SongTimeSeconds==frozen&&!_game.Music.Playing,"tutorial settings freezes clock/audio");
            _game.StandaloneSettings.CloseImmediately();Check(_game.IsCountingDown,"tutorial uses existing 3-2-1 resume countdown");
            await Until(()=>!_game.IsCountingDown&&_game.Rhythm.IsClockAdvancing,6);
            await Until(()=>_game.Rhythm.SongTimeSeconds>frozen+.05);
            double heard=_game.Music.GetPlaybackPosition()+AudioServer.GetTimeSinceLastMix()-AudioServer.GetOutputLatency();
            Check(Math.Abs(heard-_game.Rhythm.SongTimeSeconds)<.05,"tutorial resume music/clock drift below 50ms");
            _game.SuspendForSettings();RemoveChild(_game);AddChild(_game);_game.BeginResumeCountdown();
            await Until(()=>!_game.IsCountingDown&&_game.Rhythm.IsClockAdvancing,6);
            Check(_game.AudioCues.IsCuePlaying,"calibration detach/reattach reconnects shared SFX timeline");
            Check(_game.DebugJump("InterferenceIntro"),"developer checkpoint gated flag available");
            Check(_game.AudioCues.Events.Where(e=>e.Prey<0).All(e=>e.Gain==.25f),"intro interference audio uses controlled gain");
            GetWindow().Size=new Vector2I(1280,720);
            Check(_game.DebugJump("RightEyeLoss"),"right-eye discovery checkpoint");
            await Until(()=>_game.Message.Text.Contains("보이지"),10);
            Check(_game.Fish.Visible&&_game.Fish.GlobalPosition.X<_game.GetGlobalRect().GetCenter().X,"escaping unseen prey and hint align with visible half at 720p");
            _game.DebugJump("RightEyeLoss");
            for(int retry=0;retry<3;retry++)
            {
                await Hit(retry,.175);
                Check(_game.Rhythm.LastResult?.Judgment==RhythmJudgment.Bad&&_game.SuccessCount==0,"right-eye BAD does not earn mastery "+retry);
            }
            await Until(()=>_game.IsDemonstrating);
            Check(!_game.PracticeInputEnabled,"three right-eye BAD attempts repeat demonstration without an initial hint");
            Check(_game.DebugJump("FinalExam"),"final EX1 developer checkpoint");await Until(()=>_game.Rhythm.IsClockAdvancing);
            var opening=_game.Data.Phrases[0];await Until(()=>_game.Rhythm.SongTimeSeconds>=opening.TargetSeconds+_game.Rhythm.AppliedLateCaptureSeconds-_game.Rhythm.AppliedUserOffsetSeconds+.05);
            Press();Check(_game.Satiety.EmptyBites==1&&_game.SuccessCount==0&&_game.Juice.CatchBursts.Count==0,"empty tutorial Bite remains MISS without successful debris");
            var prey=_game.Data.Phrases[1];await Until(()=>_game.Rhythm.SongTimeSeconds>=prey.StartSeconds+.3);Press();
            Check(_game.Rhythm.LastResult?.Judgment==RhythmJudgment.Miss&&_game.SuccessCount==0,"premature prey commitment remains irrevocable MISS");
            await Until(()=>_game.Rhythm.SongTimeSeconds>=prey.TargetSeconds-.08);Press();
            Check(_game.SuccessCount==0&&_game.Rhythm.LastResult?.Judgment==RhythmJudgment.Miss,"mashing cannot recover a committed prey into PERFECT");
            Check(!_game.Fish.Visible&&_game.WaveSenseStrength==1,"EX1 has no pre-bite fish guidance");
            SettingsService.VisualOffsetSeconds=.08;_game.DebugJump("NormalVision");await Until(()=>_game.PracticeInputEnabled);
            Check(Math.Abs(_game.LastDemonstrationVisualSeconds-_game.LastDemonstrationChartSeconds-.08)<.035,"positive Visual Offset keeps demo Bite and target pulse together");
            _game.RequestExit();Check(!TutorialGameplayScreen.HasCompleted&&_game.RunState==GameplayScreen.GameplayRunState.Abandoned,"early replay exit stops mode without persisting completion");
            _game.QueueFree();await Frame();await Frame();await Frame();
            Check(Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount)==baseline,"tutorial scene and checkpoint nodes leave no orphans");
            GD.Print("TUTORIAL STATE VERIFIED "+_checks);GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
