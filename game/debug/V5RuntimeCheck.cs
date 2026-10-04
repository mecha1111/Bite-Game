using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Gameplay;
namespace Gamejam2.Debugging;
public partial class V5RuntimeCheck:Node
{
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private void Check(bool ok,string text){if(!ok)throw new Exception(text);GD.Print("PASS "+text);}
    private async Task Capture(string name){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("res://artifacts/release-config/v5-"+name+".png");}
    private async Task Run()
    {
        try
        {
            GetWindow().GrabFocus();
            var game=GD.Load<PackedScene>("res://game/gameplay/Gameplay.tscn").Instantiate<GameplayScreen>();AddChild(game);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            Check(game.Rhythm.IsRunning&&game.Data.ChartPath.EndsWith("stage1_hear_the_tide_chart_v5.csv"),"Stage 1 v5 starts");
            Check(game.Data.TargetCount==64,"Stage 1 target count 64");
            var map=await GeneratedMapTestFixture.Ensure(this);var custom=GameplayData.Load(map.SongId);Check(custom.TargetCount>0,"generated map has targets");
            Check(game.Data.Phrases.Concat(custom.Phrases).All(p=>p.TargetEvents.Length==1),"single target only");
            Check(game.Data.Phrases.Zip(game.Data.Phrases.Skip(1),(a,b)=>a.FromLeft!=b.FromLeft).All(v=>v),"phrase side alternation");
            var first=game.Data.Phrases[0];game.Signals.Present(first.TargetSeconds);
            Check(game.Signals.LeftLane.Slots[8].ActivePulses.Any(w=>w.WaveEventId==first.TargetEvents[0].WaveEventId)&&game.AudioCues.Events.Any(e=>e.WaveEventId==first.TargetEvents[0].WaveEventId),"single target has actual wave and SFX");
            game.SuspendForSettings();game.SetProcess(false);
            var hud=game.Gauge.GetGlobalTransformWithCanvas();
            var wave=game.Presentation.Events.First(e=>e.Type=="WAVE_CLOSEUP");
            game.Presentation.Present(wave.Time);float start=game.Presentation.CameraZoom;
            game.Presentation.Present(wave.Time+wave.EntrySeconds*.5);float moving=game.Presentation.CameraZoom;
            Check(start==1&&moving>start&&moving<wave.ZoomTo,"WAVE_CLOSEUP smooth entry");
            game.Presentation.Present(wave.Time+wave.EntrySeconds+.02);var pos=game.Presentation.WorldCamera!.Position;
            Check(Math.Abs(game.Presentation.CameraZoom-wave.ZoomTo)<.0001&&Math.Abs(game.Presentation.CameraOffset.X-game.Size.X*wave.PanPercent)<.1,"CSV camera pan/zoom applied");await Capture("wave-hold");
            game.Presentation.Present(wave.Time+wave.EntrySeconds+wave.HoldSeconds-.02);Check(game.Presentation.WorldCamera.Position.DistanceTo(pos)<.001,"WAVE_CLOSEUP holds");
            game.Presentation.Present(wave.Time+wave.Duration+.02);Check(game.Presentation.CameraZoom==1&&game.Presentation.WorldCamera.Position==game.Presentation.WorldCamera.BasePosition,"WAVE_CLOSEUP returns");
            // Stage 1's supplied CSV has no PULLBACK. Exercise the supplied Stage 3 shot on the same world rig.
            var events=StagePresentationChart.Load("song_3",188.34285);
            typeof(StagePresentation).GetProperty("Events")!.SetValue(game.Presentation,events);
            typeof(StagePresentation).GetField("_next",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(game.Presentation,0);
            var pull=events.First(e=>e.Type=="PULLBACK");
            game.Presentation.Present(pull.Time+pull.EntrySeconds*.5);Check(game.Presentation.CameraZoom<1&&game.Presentation.CameraZoom>pull.ZoomTo,"PULLBACK smooth entry");
            game.Presentation.Present(pull.Time+pull.EntrySeconds+.02);float wide=game.Presentation.CameraZoom;await Capture("pullback-hold");
            game.Presentation.Present(pull.Time+pull.EntrySeconds+pull.HoldSeconds-.02);Check(Math.Abs(wide-pull.ZoomTo)<.0001&&game.Presentation.CameraZoom==wide,"PULLBACK holds");
            game.Presentation.Present(pull.Time+pull.Duration+.02);Check(game.Presentation.CameraZoom==1,"PULLBACK returns");
            Check(game.Gauge.GetGlobalTransformWithCanvas()==hud,"HUD fixed");
            GD.Print("V5 MINIMAL CHECK COMPLETE");GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
