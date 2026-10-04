using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Gameplay;
using Gamejam2.Rhythm;
using Gamejam2.Tutorial;
using Gamejam2.Save;
using Gamejam2.Settings;
using Gamejam2.Startup;
using Gamejam2.Title;
using Gamejam2.Calibration;
using Gamejam2.Lobby;
namespace Gamejam2.Debugging;
public partial class TutorialUxChecks : Node
{
    private int _checks;
    private void Check(bool ok,string name){if(!ok)throw new Exception(name);_checks++;GD.Print("PASS "+name);}
    private async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    private async Task Until(Func<bool> condition,double seconds=25){ulong end=Time.GetTicksMsec()+(ulong)(seconds*1000);while(!condition()){if(Time.GetTicksMsec()>end)throw new TimeoutException("UX test");await Frame();}}
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Run()
    {
        try
        {
            var window=new RhythmJudgmentWindows().CreateSnapshot();
            foreach(var c in new[]{(0d,RhythmJudgment.Perfect),(.060,RhythmJudgment.Perfect),(-.060,RhythmJudgment.Perfect),(.090,RhythmJudgment.Good),(-.130,RhythmJudgment.Good),(.170,RhythmJudgment.Bad),(-.195,RhythmJudgment.Bad),(.210,RhythmJudgment.Miss),(-.210,RhythmJudgment.Miss)})Check(window.Classify(c.Item1)==c.Item2,"accessible judgment "+c.Item1);
            foreach(var c in new[]{(.07,RhythmJudgment.Perfect),(.14,RhythmJudgment.Good),(.2,RhythmJudgment.Bad)})foreach(double sign in new[]{-1d,1d})Check(window.Classify(sign*c.Item1)==c.Item2,"symmetric inclusive boundary "+sign*c.Item1);
            var router=GD.Load<PackedScene>("res://game/startup/Startup.tscn").Instantiate<SceneRouter>();router.SavePath="/private/tmp/bite-ux-"+OS.GetProcessId()+".cfg";AddChild(router);
            ((TitleScreen)router.ScreenHost!.GetChild(0)).StartButton.EmitSignal(BaseButton.SignalName.Pressed);
            await Until(()=>router.ScreenHost.GetChild(0) is CalibrationScreen);router.ScreenHost.GetChild<CalibrationScreen>(0).Flow!.Calibration!.SkipWithDefaults();
            await Until(()=>router.ScreenHost.GetChild(0) is TutorialGameplayScreen);
            var game=router.ScreenHost.GetChild<TutorialGameplayScreen>(0);
            Check(game.Message.GetThemeFontSize("font_size")==40&&game.Message.GetThemeConstant("outline_size")==0,"readable scene-authored instruction typography");
            Check(!game.HasNode("TutorialHud/Tap")&&game.Mastery.GetParent()==game.Message.GetParent(),"TAP removed and mastery grouped with instruction");
            Check(game.SkipButton.Visible&&!game.IsReplay,"skip available on first-time tutorial");
            int starts=0;game.Rhythm.AudioPlaybackStarted+=_=>starts++;
            await Until(()=>game.Signals.LeftLane.Slots.Any(s=>s.ActivePulses.Any(w=>w.Modulate.R>.9&&w.Modulate.G<.9)));
            Check(game.IsDemonstrating&&game.ModeLabel.Text.StartsWith("시범"),"gold real chart pulses identify demonstration");
            await Until(()=>!game.IsDemonstrating);
            await Until(()=>game.Signals.LeftLane.Slots[8].ActivePulses.Any(w=>w.Modulate.G>.95&&w.Modulate.R<.5));
            Check(game.Rhythm.LastResult?.Judgment==RhythmJudgment.Perfect&&game.SuccessCount==0,"green actual target and automated Bite share chart timing");
            await Until(()=>game.CheckpointLoops>0);
            Check(game.MusicCheckpointRestarts==0&&starts==0&&game.Rhythm.SongTimeSeconds>11,"failed practice advances continuously with no music Play/Seek");
            Check(game.SuccessCount==0&&game.IsDemonstrating,"three failed practice phrases repeat a clear demonstration");
            Check(game.LastPracticeTransitionMilliseconds<20,"cached phrase transition below 20ms");
            GD.Print("PROFILE tutorial transition ms="+game.LastPracticeTransitionMilliseconds+" SFX underruns="+game.AudioCues.UnderrunResyncs);
            game.SkipButton.EmitSignal(BaseButton.SignalName.Pressed);
            Check(game.SkipDialogVisible&&game.ContinueButton.HasFocus()&&game.Rhythm.IsManuallyPaused,"skip confirmation defaults Continue and pauses music");
            game.ContinueButton.EmitSignal(BaseButton.SignalName.Pressed);Check(!game.SkipDialogVisible&&game.IsCountingDown&&!TutorialGameplayScreen.HasCompleted,"cancel skip preserves incomplete tutorial and resumes countdown");
            await Until(()=>!game.IsCountingDown);
            ProgressService.UnlockStage("stage_4");ProgressService.RecordSongAchievements("song_1",true,true);
            game.OpenSkipConfirmation();game.ConfirmSkipButton.EmitSignal(BaseButton.SignalName.Pressed);
            await Until(()=>router.ScreenHost.GetChild(0) is LobbyScreen);
            Check(TutorialGameplayScreen.HasCompleted&&ProgressService.IsUnlocked("stage_4")&&ProgressService.HasAllPerfect("song_1"),"confirmed skip persists onboarding without resetting progression");
            router.GoToTitle();((TitleScreen)router.ScreenHost.GetChild(0)).StartButton.EmitSignal(BaseButton.SignalName.Pressed);
            await Until(()=>router.ScreenHost.GetChild(0) is LobbyScreen);Check(true,"confirmed skip prevents forced tutorial on later start");
            router.Settings!.Open();router.Settings.TutorialButton.EmitSignal(BaseButton.SignalName.Pressed);
            await Until(()=>router.ScreenHost.GetChild(0) is TutorialGameplayScreen);Check(router.ScreenHost.GetChild<TutorialGameplayScreen>(0).IsReplay,"Settings replay remains available after skip");
            for(int retry=0;retry<4;retry++)
            {
                game=router.ScreenHost.GetChild<TutorialGameplayScreen>(0);game.RequestExit();await Until(()=>router.ScreenHost.GetChild(0) is LobbyScreen);
                await Frame();await Frame();GC.Collect();GC.WaitForPendingFinalizers();await Frame();
                Check(router.ScreenHost.GetChild<LobbyScreen>(0).Catalog.Stages.Count==4,"tutorial replay resources survive collection "+retry);
                if(retry<3)router.GoToTutorial(true);
            }
            router.QueueFree();await Frame();await Frame();GD.Print("TUTORIAL UX VERIFIED "+_checks);GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
