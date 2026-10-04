using System;
using System.Reflection;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Gameplay;
using Gamejam2.Rhythm;
using Gamejam2.Audio;
using Gamejam2.Startup;
namespace Gamejam2.Debugging;
public partial class WaterLifecycleChecks : Node
{
    private int _checks;
    private GameplayScreen _game=null!;
    private void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);_checks++;GD.Print("PASS "+name);}
    private async Task Wait(double seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private async Task Until(Func<bool> predicate,double seconds=30)
    {
        ulong end=Time.GetTicksMsec()+(ulong)(seconds*1000);
        while(!predicate()) {if(Time.GetTicksMsec()>end)throw new TimeoutException("water/lifecycle test");GetWindow().GrabFocus();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    }
    private async Task<Image> Capture(string name){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);var image=GetViewport().GetTexture().GetImage();image.SavePng("/private/tmp/bite-water-"+name+".png");return image;}
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Run()
    {
        try
        {
            var router=GD.Load<PackedScene>("res://game/startup/Startup.tscn").Instantiate<SceneRouter>();router.SavePath="/private/tmp/bite-water-check.cfg";AddChild(router);router.GoToTitle();GetWindow().GrabFocus();GetWindow().Size=new Vector2I(1920,1080);
            await Wait(.2);await Capture("title-start");if(!OS.GetCmdlineUserArgs().Contains("--skip-title-watch"))await Wait(20);await Capture("title-20-seconds");
            Check(router.ScreenHost!.GetChild(0) is Gamejam2.Title.TitleScreen,"Title runs20 seconds with existing composition");
            router.GoToLobby();router.GoToGameplay("stage_1");_game=router.ScreenHost.GetChild<GameplayScreen>(0);
            var anchor=_game.GetNode<Control>("Composition/SharkAnchor/BiteTargetAnchor");
            Check(anchor.GlobalPosition==new Vector2(960,984),"mouth anchor authored at960,984");
            foreach(var lane in new[]{_game.Signals.LeftLane,_game.Signals.RightLane})
            {
                Check(lane.GlobalPosition.Y==520,"lane shifted down60px");
                Check(lane.Slots[8].OriginAnchor==anchor,"both5th-beat origins use same mouth anchor");
                lane.Slots[8].Activate(WaveKind.Medium,1,_game.Rhythm.SongTimeSeconds);
                Check(lane.Slots[8].ActivePulses[0].Origin==anchor.GlobalPosition,"final wave starts inside mouth without moving center");
            }
            await Until(()=>_game.Rhythm.SongTimeSeconds>=2.35);
            Check(!_game.Result.Visible&&_game.RunState==GameplayScreen.GameplayRunState.Playing,"encounter ends without Result");
            typeof(GameplayScreen).GetMethod("Finish",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(_game,null);
            Check(!_game.Result.Visible&&_game.ResultPresentationCount==0,"obsolete mid-song Finish call rejected");
            _game.SuspendForSettings();double time=_game.Rhythm.SongTimeSeconds;
            foreach(var lane in new[]{_game.Signals.LeftLane,_game.Signals.RightLane})lane.Reset();
            _game.Signals.LeftLane.Activate(8,WaveKind.Medium,1,time-.15);await Wait(.05);await Capture("mouth-left");_game.Signals.LeftLane.Reset();
            _game.Signals.RightLane.Activate(8,WaveKind.Medium,1,time-.15);await Wait(.05);await Capture("mouth-right");_game.Signals.RightLane.Reset();
            _game.WorldWaterTreatment.Visible=false;await Wait(.05);var plain=await Capture("world-without-treatment");
            _game.WorldWaterTreatment.Visible=true;await Wait(.05);var treated=await Capture("world-treatment");
            Check(plain.GetPixel(800,137).IsEqualApprox(treated.GetPixel(800,137)),"HUD gauge untouched by water pass");
            Check(new Vector3(plain.GetPixel(960,850).R,plain.GetPixel(960,850).G,plain.GetPixel(960,850).B).DistanceTo(new Vector3(treated.GetPixel(960,850).R,treated.GetPixel(960,850).G,treated.GetPixel(960,850).B))>.001,"shark receives world color treatment");
            Check(_game.Rhythm.SongTimeSeconds==time&&((ShaderMaterial)_game.WorldWaterTreatment.Material).GetShaderParameter("time_seconds").AsDouble()==time,"world lighting freezes with Song Clock");
            foreach(string song in new[]{"song_1","song_2","song_3","song_4"})
            {_game.Environment.Apply(song);_game.Environment.Present(time,true);Check(_game.Environment.WorldProfile!=null,"stage-specific world profile "+song);await Capture(song);}
            _game.Environment.Apply("song_1");
            foreach(var size in new[]{new Vector2I(1920,1080),new Vector2I(1600,900),new Vector2I(1366,768),new Vector2I(1280,720),new Vector2I(2560,1440)})
            {
                GetWindow().Size=size;await Wait(.1);
                Check(_game.Signals.LeftLane.Slots[8].OriginAnchor!.GlobalPosition==anchor.GlobalPosition && _game.Signals.RightLane.Slots[8].OriginAnchor!.GlobalPosition==anchor.GlobalPosition,"shared mouth alignment at "+size);
                await Capture("mouth-"+size.X+"x"+size.Y);
            }
            GetWindow().Size=new Vector2I(1920,1080);
            _game.BeginResumeCountdown();await Until(()=>!_game.IsCountingDown);
            await Until(()=>_game.Rhythm.SongTimeSeconds>=10);
            Check(!_game.Result.Visible&&!_game.GameOver.Visible,"normal middle of song never displays Result");
            // Controlled terminal fixture; a 24-second prototype deadline is no longer valid.
            // Unskipped current-song completion is covered by V2RuntimeChecks.
            _game.Rhythm.PauseSong();
            typeof(RhythmController).GetField("_stoppedTimeSeconds",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(_game.Rhythm,_game.Rhythm.AppliedSongEndTimeSeconds+.05);
            _game.Rhythm.ResumeSong();
            await Until(()=>_game.Result.Visible);
            Check(_game.Rhythm.PlaybackState=="Completed"&&_game.RunState==GameplayScreen.GameplayRunState.Result&&_game.ResultPresentationCount==1,"current declared song-end fixture enters Result exactly once");
            typeof(GameplayScreen).GetMethod("Finish",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(_game,null);await Wait(.1);
            Check(_game.ResultPresentationCount==1,"duplicate finish ignored");
            _game.Restart();await Until(()=>_game.Rhythm.SongTimeSeconds>=.2);
            typeof(SatietyState).GetProperty(nameof(SatietyState.Value))!.SetValue(_game.Satiety,0.0);await Until(()=>_game.GameOver.Visible);
            Check(_game.RunState==GameplayScreen.GameplayRunState.GameOver&&!_game.Result.Visible&&_game.ResultPresentationCount==0,"starvation uses GameOver without receipt");
            typeof(GameplayScreen).GetMethod("Finish",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(_game,null);Check(!_game.Result.Visible,"GameOver cannot transition to Result");
            _game.StopGameplay();router.GoToTitle();
            var music=new AudioStreamPlayer{Stream=MetronomeTrack.CreateTrack(new[]{0.0},2)};AddChild(music);
            var chart=new RhythmChart{SongEndTimeSeconds=4};chart.Events.Add(new RhythmEventData{TimeSeconds=.1,EventType=RhythmEventType.Section,IsHittable=false,SectionId="encounter end"});
            var rhythm=new RhythmController{MusicPlayer=music,Chart=chart,EnableDebugLogging=false};AddChild(rhythm);int finished=0;rhythm.SongFinished+=()=>finished++;Check(rhythm.StartSong(),"controlled end lifecycle starts");
            await Until(()=>rhythm.SongTimeSeconds>=.4);music.Stop();Check(finished==0&&rhythm.CurrentEventIndex==1,"section/event exhaustion and stopped audio are not completion");
            await Until(()=>rhythm.SongTimeSeconds>=3);Check(finished==0,"no premature Result before declared song end");
            await Until(()=>finished==1);Check(rhythm.SongTimeSeconds>=4,"declared end emits once");await Wait(.1);Check(finished==1,"one-shot SongFinished");
            music.Stream=MetronomeTrack.CreateTrack(new[]{0.0},6);chart.SongEndTimeSeconds=2;Check(rhythm.StartSong()&&rhythm.AppliedSongEndTimeSeconds==6,"longer real audio extends short chart deadline");rhythm.StopSong();
            rhythm.QueueFree();music.QueueFree();router.QueueFree();await Wait(.2);
            GD.Print($"WATER LIFECYCLE VERIFIED: {_checks} checks");GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
}
