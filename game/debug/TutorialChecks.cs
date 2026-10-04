using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Startup;
using Gamejam2.Tutorial;
using Gamejam2.Gameplay;
using Gamejam2.Rhythm;
using Gamejam2.Save;
using Gamejam2.Settings;
using Gamejam2.Title;
using Gamejam2.Calibration;
using Gamejam2.Lobby;
namespace Gamejam2.Debugging;
public partial class TutorialChecks : Node
{
    private int _checks;
    private TutorialGameplayScreen? _active;
    private void Check(bool ok,string label){if(!ok)throw new Exception(label);_checks++;GD.Print("PASS "+label);}
    private async Task Frame(){GetWindow().GrabFocus();if(_active!=null&&GodotObject.IsInstanceValid(_active)&&_active.IsInsideTree()&&_active.Rhythm.IsFocusPaused)_active.Rhythm.Notification((int)NotificationApplicationFocusIn);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private async Task Wait(double s){ulong until=Time.GetTicksMsec()+(ulong)(s*1000);while(Time.GetTicksMsec()<until)await Frame();}
    private void Press(){foreach(bool down in new[]{true,false}){Input.ParseInputEvent(new InputEventKey{PhysicalKeycode=Key.Space,Pressed=down});Input.FlushBufferedEvents();}}
    private void Capture(string name){if(DisplayServer.GetName()=="headless")return;RenderingServer.ForceDraw();GetViewport().GetTexture().GetImage().SavePng("res://artifacts/tutorial/"+name+".png");}
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Run()
    {
        try
        {
            var router=GD.Load<PackedScene>("res://game/startup/Startup.tscn").Instantiate<SceneRouter>();router.SavePath="/private/tmp/bite-tutorial-check-"+OS.GetProcessId()+".cfg";AddChild(router);
            SettingsService.InputOffsetSeconds=SettingsService.VisualOffsetSeconds=0;SettingsService.SetResolution(2);
            ((TitleScreen)router.ScreenHost!.GetChild(0)).StartButton.EmitSignal(BaseButton.SignalName.Pressed);await Wait(.1);
            Check(router.ScreenHost.GetChild(0) is CalibrationScreen,"first launch calibrates before tutorial");
            router.ScreenHost.GetChild<CalibrationScreen>(0).Flow!.Calibration!.SkipWithDefaults();await Wait(.2);
            var game=router.ScreenHost.GetChild<TutorialGameplayScreen>(0);_active=game;
            Check(!game.IsReplay&&game.IsDemonstrating&&!TutorialGameplayScreen.HasCompleted,"fresh save opens required demonstration");
            Check(game.Data.AudioPath.EndsWith("1-2.mp3")&&game.Data.DurationSeconds>101.7&&game.Data.DurationSeconds<101.9,"tutorial real soundtrack and actual length");
            Check(game.Profile.RequiredSuccesses==3&&game.Data.Phrases.All(p=>p.TargetSlot==8),"three successes and unchanged beat-five pattern semantics");
            ProgressService.UnlockStage("stage_4");ProgressService.RecordSongAchievements("song_1",true,true);
            int lesson=-1,failures=0,failedPhases=0;bool tapped=false,attackShot=false,escapeShot=false;double lastClock=-1;ulong deadline=Time.GetTicksMsec()+900000;
            while(!game.IsComplete)
            {
                if(Time.GetTicksMsec()>deadline)throw new TimeoutException("tutorial traversal");
                if(game.LessonIndex!=lesson)
                {
                    if(lesson>=0)Check(game.SuccessCount==0,"mastery resets for lesson "+game.LessonIndex);
                    lesson=game.LessonIndex;tapped=false;lastClock=-1;GD.Print("LESSON "+game.LessonId);Capture("lesson-"+lesson);
                }
                double time=game.Rhythm.SongTimeSeconds;
                if(lesson==6&&game.Rhythm.LastResult?.Judgment==RhythmJudgment.Miss)failedPhases=1;
                if(time<lastClock-.3){tapped=false;GD.Print($"CHECKPOINT {game.LessonId} count={game.SuccessCount} demo={game.IsDemonstrating}");}lastClock=time;
                if(game.IsDemonstrating&&!tapped){Capture("tap-"+lesson);tapped=true;}
                if(lesson==6&&time>=game.SegmentStart+.65&&time<game.SegmentStart+.9&&!attackShot){Capture("attack");attackShot=true;}
                if(lesson==6&&game.Fish.Visible&&time>game.Data.Phrases[0].TargetSeconds+.35&&!escapeShot){Capture("escape-visible");escapeShot=true;}
                if(game.IsBiteInteractive&&game.PracticeInputEnabled)
                {
                    foreach(var p in game.Data.Phrases)
                    {
                        if(game.Rhythm.LastResult?.TargetEvent.CueId==p.Index.ToString())continue;
                        // Three early commitments verify anti-mash and forced demonstration replay.
                        if(lesson==0&&failures<3&&time>=p.StartSeconds+.35&&time<p.TargetSeconds-.4)
                        {int count=game.SuccessCount;Press();Check(game.SuccessCount==count,"failed commitment earns no mastery");failures++;break;}
                        if(lesson==6&&failedPhases==0&&time<p.TargetSeconds+.4)continue;
                        if(lesson==6&&failedPhases==0&&time>=p.TargetSeconds+.4){failedPhases++;break;}
                        if(time>=p.TargetSeconds&&time<p.TargetSeconds+.07){Press();GD.Print($"PRACTICE {game.LessonId} at={time:F3} result={game.Rhythm.LastResult?.Judgment} count={game.SuccessCount}");break;}
                    }
                }
                CheckQuiet(game.Satiety.Value>0&&!game.Result.Visible&&!game.GameOver.Visible,"protected tutorial cannot show Game Over/Result");
                await Frame();
            }
            Check(game.SuccessCount>=3&&lesson==13,"final EX1 mastered with three GOOD+");
            Check(game.RightEyeVisibility==0&&game.LeftEyeVisibility==0&&game.WaveSenseStrength==1&&!game.Fish.Visible,"final exam fully blind with readable waves");
            Check(game.CheckpointLoops>0&&failures==3,"local synchronized retry and failure redemonstration path exercised");
            Check(escapeShot&&attackShot,"attack and unseen prey escape rendered");Capture("complete");
            Check(TutorialGameplayScreen.HasCompleted,"completion persisted only after final mastery");
            await Wait(1.8);Check(router.ScreenHost.GetChild(0) is LobbyScreen,"tutorial completion returns Lobby without Result");
            router.GoToTitle();((TitleScreen)router.ScreenHost.GetChild(0)).StartButton.EmitSignal(BaseButton.SignalName.Pressed);await Wait(.1);
            Check(router.ScreenHost.GetChild(0) is LobbyScreen,"later start bypasses completed tutorial");
            router.Settings!.Open();await Wait(.4);Capture("settings-replay");router.Settings.TutorialButton.EmitSignal(BaseButton.SignalName.Pressed);await Wait(.1);
            game=router.ScreenHost.GetChild<TutorialGameplayScreen>(0);_active=game;Check(game.IsReplay&&router.Settings.LeaveButton.Visible,"Settings replay has safe exit");
            Check(ProgressService.IsUnlocked("stage_4")&&ProgressService.HasAllPerfect("song_1"),"replay preserves stage unlock/FC/AP");
            router.Settings.Open();await Wait(.3);Check(game.Rhythm.IsManuallyPaused&&!game.Music.Playing,"tutorial settings pauses shared clock/audio");
            router.Settings.LeaveButton.EmitSignal(BaseButton.SignalName.Pressed);router.Settings.ConfirmExitButton.EmitSignal(BaseButton.SignalName.Pressed);await Wait(.1);
            Check(router.ScreenHost.GetChild(0) is LobbyScreen&&TutorialGameplayScreen.HasCompleted,"manual replay exit returns Lobby and preserves completion");
            SaveStore.Write("progress","tutorial_completed",false);SaveStore.Flush();router.Settings.Open();router.Settings.TutorialButton.EmitSignal(BaseButton.SignalName.Pressed);await Wait(.1);
            game=router.ScreenHost.GetChild<TutorialGameplayScreen>(0);_active=game;game.RequestExit();await Wait(.1);Check(!TutorialGameplayScreen.HasCompleted,"early replay exit never marks incomplete tutorial completed");
            SaveStore.Initialize(router.SavePath);ProgressService.Initialize();Check(!TutorialGameplayScreen.HasCompleted&&ProgressService.IsUnlocked("stage_4"),"save reload verifies tutorial key independent of progression");
            router.QueueFree();await Wait(.1);GD.Print("TUTORIAL VERIFIED "+_checks);GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
    private static void CheckQuiet(bool ok,string label){if(!ok)throw new Exception(label);}
}
