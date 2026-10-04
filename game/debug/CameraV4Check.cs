using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Gameplay;
namespace Gamejam2.Debugging;
public partial class CameraV4Check:Node
{
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private void Check(bool ok,string message){if(!ok)throw new Exception(message);GD.Print("PASS "+message);}
    private async Task Capture(string name){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("res://artifacts/release-config/camera-v4-"+name+".png");}
    private async Task Run()
    {
        try
        {
            var game=GD.Load<PackedScene>("res://game/gameplay/Gameplay.tscn").Instantiate<GameplayScreen>();AddChild(game);GetWindow().GrabFocus();
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            Check(game.Rhythm.IsRunning&&game.Data.ChartPath.EndsWith("stage1_hear_the_tide_chart_v4.csv"),"Stage 1 starts with v4");
            game.SuspendForSettings();game.SetProcess(false);
            var p=game.Data.Phrases.First(p=>p.TargetEvents.Length>1);
            game.Signals.Initialize(game.Data);
            foreach(var target in p.TargetEvents)
            {
                game.Signals.Present(target.TargetTime);
                var lane=p.FromLeft?game.Signals.LeftLane:game.Signals.RightLane;
                Check(lane.Slots[8].ActivePulses.Any(w=>w.WaveEventId==target.WaveEventId),"separate target wave "+target.WaveEventId);
            }
            game.Signals.Initialize(game.Data);
            var camera=game.Presentation.WorldCamera!;
            var hud=new CanvasItem[]{game.Gauge,game.Combo,game.Feedback,game.SettingsButton};
            var transforms=hud.Select(c=>c.GetGlobalTransformWithCanvas()).ToArray();
            game.Presentation.Present(0);await Capture("base");
            var wave=game.Presentation.Events.First(e=>e.Type=="FOCUS_WAVE");
            game.Signals.Present(wave.Time+.23);game.Presentation.Present(wave.Time+.23);
            Check(camera.IsCurrent()&&Math.Abs(camera.Position.X-camera.BasePosition.X)>150&&camera.Zoom.X>1.09,"real camera wave pan/zoom "+camera.Position+" / "+camera.Zoom);await Capture("wave");
            game.Presentation.Present(wave.Time+1.1);Check(camera.Position.DistanceTo(camera.BasePosition)<.1&&Math.Abs(camera.Zoom.X-1)<.001,"camera returns to base");
            var shark=game.Presentation.Events.First(e=>e.Type=="ZOOM_TO_SHARK");game.Presentation.Present(shark.Time+.23);Check(camera.Zoom.X>1.15,"shark close-up "+camera.Zoom);await Capture("shark");
            var back=game.Presentation.Events.First(e=>e.Type=="PULL_BACK");game.Presentation.Present(back.Time+.3);Check(camera.Zoom.X<.94,"pullback "+camera.Zoom);await Capture("pullback");
            Check(hud.Select((c,i)=>c.GetGlobalTransformWithCanvas()==transforms[i]).All(v=>v),"fixed HUD CanvasLayer");
            game.Presentation.Present(back.Time+1.1);await Capture("return");
            GD.Print("CAMERA V4 CHECK COMPLETE");GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
}
