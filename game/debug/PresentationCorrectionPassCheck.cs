using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Gameplay;
using Gamejam2.Rhythm;
namespace Gamejam2.Debugging;
public partial class PresentationCorrectionPassCheck:Node
{
    private void Check(bool ok,string text){if(!ok)throw new Exception(text);GD.Print("PASS "+text);}
    private async Task Wait(double sec)=>await ToSignal(GetTree().CreateTimer(sec),SceneTreeTimer.SignalName.Timeout);
    private async Task Capture(string name){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("res://artifacts/release-config/presentation-pass-"+name+".png");}
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private static void Complete(GameplayScreen game,double value)
    {
        typeof(GameplayScreen).GetProperty("RunState")!.SetValue(game,GameplayScreen.GameplayRunState.Playing);
        game.Rhythm.StopSong();game.Satiety.StopDrainAfterFinalTarget(0);
        typeof(SatietyState).GetProperty("Value")!.SetValue(game.Satiety,value);
        typeof(RhythmController).GetField("_stoppedTimeSeconds",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(game.Rhythm,game.Rhythm.AppliedSongEndTimeSeconds);
        typeof(RhythmController).GetProperty("PlaybackState")!.SetValue(game.Rhythm,"Completed");
        typeof(GameplayScreen).GetMethod("Finish",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(game,null);
    }
    private async Task Run()
    {
        try
        {
            GetWindow().GrabFocus();
            var game=GD.Load<PackedScene>("res://game/gameplay/Gameplay.tscn").Instantiate<GameplayScreen>();AddChild(game);await Wait(.05);
            Check(game.Rhythm.IsRunning,"stage starts");game.SetProcess(false);game.SuspendForSettings();
            if(OS.GetCmdlineUserArgs().Contains("--end-only"))
            {
                game.Restart();game.SetProcess(false);game.Satiety.StopDrainAfterFinalTarget(0);
                typeof(SatietyState).GetProperty("Value")!.SetValue(game.Satiety,499d);
                var target=game.Data.Chart.Events.First(e=>e.EventType==RhythmEventType.InputTarget);
                typeof(GameplayScreen).GetMethod("OnJudgment",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(game,new object[]{new RhythmJudgmentResult(RhythmJudgment.Miss,0,target)});
                Check(game.RunState==GameplayScreen.GameplayRunState.Playing&&!game.GameOver.Visible&&game.Satiety.Value==499,"during song 499 plus judgment stays Playing");
                typeof(SatietyState).GetProperty("Value")!.SetValue(game.Satiety,0d);
                typeof(GameplayScreen).GetMethod("OnJudgment",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(game,new object[]{new RhythmJudgmentResult(RhythmJudgment.Miss,0,target)});
                Check(game.RunState==GameplayScreen.GameplayRunState.GameOver,"during song zero still GameOver");
                game.Restart();game.SetProcess(false);
                Complete(game,499);Check(game.RunState==GameplayScreen.GameplayRunState.GameOver&&game.GameOver.Visible&&!game.Result.Visible&&game.ResultPresentationCount==0,"SongFinished 499 directly GameOver");
                game.Restart();game.SetProcess(false);Complete(game,500);Check(game.RunState==GameplayScreen.GameplayRunState.Result&&game.ResultPresentationCount==1&&!game.GameOver.Visible,"SongFinished 500 normal Result");
                GD.Print("END THRESHOLD CHECK COMPLETE");GetTree().Quit();return;
            }
            var hud=game.Gauge.GetGlobalTransformWithCanvas();
            var shot=game.Presentation.Events.First(e=>e.Type=="PULL_BACK");
            game.Presentation.Present(shot.Time);float initial=game.Presentation.CameraZoom;
            game.Presentation.Present(shot.Time+.5);float middle=game.Presentation.CameraZoom;
            Check(initial==1&&middle<1&&middle>.90,"wide shot slow sine entry");
            game.Presentation.Present(shot.Time+1.2);float hold=game.Presentation.CameraZoom;await Capture("wide");
            game.Presentation.Present(shot.Time+2.8);Check(Math.Abs(hold-game.Presentation.CameraZoom)<.0001,"wide shot holds two seconds");
            game.Presentation.Present(shot.Time+3.75);Check(game.Presentation.CameraZoom>hold&&game.Presentation.CameraZoom<1,"slow shot exit");
            game.Presentation.Present(shot.Time+4.6);Check(game.Presentation.CameraZoom==1&&game.Gauge.GetGlobalTransformWithCanvas()==hud,"base return and fixed HUD");
            game.Signals.Present(7);game.Presentation.Present(7);var pos=game.Presentation.WorldCamera!.Position;
            game.Signals.Present(9);game.Presentation.Present(9);Check(game.Presentation.WorldCamera.Position==pos,"ordinary alternate prey do not steer camera");
            game.Juice.Bite(10);game.Juice.Judgment(RhythmJudgment.Perfect,10,1);
            Check(game.Juice.LastCatchImpact==null&&game.Juice.CatchImpactCount==0&&game.Juice.CatchBursts.All(c=>c.ImpactRing==null),"success has no shark-centered WavePulse");
            // One authored three-hit phrase, no full-song playback or chart reauthoring.
            var data=GameplayData.Load("song_3");var phrase=data.Phrases.First(p=>p.TargetEvents.Length==3);
            var chart=new RhythmChart{Bpm=data.Chart.Bpm,SongEndTimeSeconds=phrase.LastTargetSeconds+1};
            foreach(var e in data.Chart.Events.Where(e=>e.CueId==phrase.Index.ToString()&&e.WaveEventId.Length>0))chart.Events.Add(e);
            var previewMusic=new AudioStreamPlayer();AddChild(previewMusic);
            var rhythm=new RhythmController{MusicPlayer=previewMusic,Chart=chart,EnableDebugLogging=false,CaptureWindows=GD.Load<InputCaptureWindows>("res://game/rhythm/InputCaptureWindows.tres")};AddChild(rhythm);
            int judged=0,miss=0;
            rhythm.JudgmentResolved+=r=>{judged++;if(r.Judgment==RhythmJudgment.Miss)miss++;phrase.TargetEvents[r.TargetEvent.TargetOrdinal].GetType().GetProperty("ResolvedState")!.SetValue(phrase.TargetEvents[r.TargetEvent.TargetOrdinal],true);};
            game.Signals.Initialize(data);game.AudioCues.Initialize(data,game.Interference,rhythm);
            Check(phrase.TargetEvents.All(t=>game.AudioCues.Events.Any(e=>e.WaveEventId==t.WaveEventId+":cue")&&game.AudioCues.Events.Any(e=>e.WaveEventId==t.WaveEventId)),"three independent cue/target SFX schedules");
            Check(rhythm.StartSong(Math.Max(0,phrase.TargetEvents[0].CueTime-.15)),"three-hit sample starts");
            foreach(var target in phrase.TargetEvents)
            {
                while(rhythm.SongTimeSeconds<target.CueTime+.05){game.Signals.Present(rhythm.SongTimeSeconds);await Wait(.005);}
                game.Signals.Present(rhythm.SongTimeSeconds);
                var lane=phrase.FromLeft?game.Signals.LeftLane:game.Signals.RightLane;
                Check(lane.Slots[0].ActivePulses.Any(w=>w.WaveEventId==target.WaveEventId+":cue"&&w.Visible&&w.CurrentRadius>0),"visible animated cue "+target.WaveEventId);
                while(rhythm.SongTimeSeconds<target.TargetTime){game.Signals.Present(rhythm.SongTimeSeconds);await Wait(.005);}
                game.Signals.Present(rhythm.SongTimeSeconds);
                Check(rhythm.TryJudgeInput(),"one input judges "+target.WaveEventId);
            }
            Check(judged==3&&miss==0,"three individual judgments; no invisible-target MISS");
            game.AudioCues.Stop();rhythm.StopSong();rhythm.QueueFree();
            Complete(game,499);Check(game.RunState==GameplayScreen.GameplayRunState.GameOver&&game.GameOver.Visible&&!game.Result.Visible&&game.ResultPresentationCount==0,"SongFinished 499 routes directly to GameOver");
            game.Restart();game.SetProcess(false);Complete(game,500);Check(game.RunState==GameplayScreen.GameplayRunState.Result&&game.ResultPresentationCount==1&&!game.GameOver.Visible,"SongFinished 500 enters normal Result");
            GD.Print("PRESENTATION CORRECTION PASS COMPLETE");GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
