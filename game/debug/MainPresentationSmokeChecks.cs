using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Gameplay;
using Gamejam2.Save;
using Gamejam2.Settings;
namespace Gamejam2.Debugging;
/// <summary>The requested A-H presentation samples only; no full-song or chart regression pass.</summary>
public partial class MainPresentationSmokeChecks : Node
{
    private void Check(bool ok,string label){if(!ok)throw new Exception(label);GD.Print("PASS "+label);}
    private async Task Frames(){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private GameplayScreen Spawn(string id)
    {var g=GD.Load<PackedScene>("res://game/gameplay/Gameplay.tscn").Instantiate<GameplayScreen>();g.SongId=id;AddChild(g);g.SetProcess(false);return g;}
    private async Task Remove(GameplayScreen g){g.StopGameplay();g.QueueFree();await Frames();}
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Run()
    {
        try
        {
            GetWindow().GrabFocus();SaveStore.Initialize("/private/tmp/bite-main-presentation-smoke.cfg");ProgressService.Initialize();SettingsService.Initialize();
            var one=Spawn("song_1");await Frames();one.Rhythm.PauseSong();
            Check(one.RunState==GameplayScreen.GameplayRunState.Playing,"main presentation scene starts");
            var hud=new Control[]{one.Gauge,one.Combo,one.Feedback.GetParent<Control>(),one.SettingsButton};var rects=hud.Select(c=>c.GetGlobalRect()).ToArray();
            one.Presentation.Present(5);
            Check(one.Presentation.CurrentEnergy==PresentationEnergy.LOW&&one.Presentation.CameraZoom==1&&one.Presentation.CameraOffset==Vector2.Zero,"A Stage 1 LOW remains calm");
            var kick=one.Presentation.Events.First(e=>e.Type=="KICK_PULSE");one.Presentation.Present(kick.Time+.04);
            Check(one.Presentation.CameraZoom>1,"B selected Stage 1 kick / light event works");
            Check(hud.Select((c,i)=>c.GetGlobalRect().IsEqualApprox(rects[i])).All(x=>x)&&one.Gauge.ZIndex>one.WorldWaterTreatment.ZIndex&&one.Combo.GetParent<Control>().ZIndex>one.WorldWaterTreatment.ZIndex&&one.SettingsButton.GetParent<Control>().ZIndex>one.WorldWaterTreatment.ZIndex,"G HUD keeps fixed coordinates and draws after WORLD-only treatment");
            one.Presentation.Present(kick.Time+3);Check(one.Presentation.CameraZoom==1&&one.Presentation.CameraOffset==Vector2.Zero,"temporary camera returns to base framing");
            await Remove(one);
            var two=Spawn("song_2");await Frames();two.Rhythm.PauseSong();
            var pan=two.Presentation.Events.First(e=>e.Type=="SIDE_PAN");two.Presentation.Present(pan.Time+pan.Duration*.5);
            Check(Math.Abs(two.Presentation.CameraOffset.X)>0,"C Hidden Current lateral camera works");await Remove(two);
            var three=Spawn("song_3");await Frames();three.Rhythm.PauseSong();
            var vent=three.Presentation.Events.First(e=>e.Type=="particle_burst");three.Presentation.Present(vent.Time+.04);
            Check(three.Presentation.CameraZoom!=1&&three.Presentation.AccentParticles.LastEmittedCount>0,"D Predator impact and vent particle bundle works");await Remove(three);
            var ex=Spawn("song_4");await Frames();ex.Rhythm.PauseSong();
            var transition=ex.Presentation.Events.First(e=>e.Type=="SECTION_TRANSITION");double at=transition.Time+transition.Duration*.5;
            ex.Environment.Present(at,false,.016);ex.Presentation.Present(at);
            Check(ex.Environment.ActiveEnvironmentSongId=="song_3"&&ex.Environment.TransitionProgress>0&&ex.Environment.TransitionProgress<1&&ex.Presentation.CameraZoom!=1,"E EX environment crossfade/profile blend includes camera and visual bundle");
            ex.Interference.Trigger("whale",at+2);ex.Presentation.Present(at+3);
            Check(ex.Presentation.CameraOffset.Length()>0,"F massive interference affects camera");await Remove(ex);
            var candidates=new[]{new StageFxEvent(5,"IMPACT_ZOOM",1,.3,"cyan","world"){Level=PresentationEnergy.HIGH,Priority=50},new StageFxEvent(5.1,"DROP_ZOOM",1,.5,"cyan","world"){Level=PresentationEnergy.HIGH,Priority=90},new StageFxEvent(6.7,"IMPACT_ZOOM",1,.3,"cyan","world"){Level=PresentationEnergy.HIGH,Priority=50}};
            var accepted=StagePresentationChart.ApplyCooldowns(candidates);
            Check(accepted.Length==2&&accepted[0].Time==5.1&&accepted[1].Time-accepted[0].Time>=1.5,"H strong cooldown suppresses weaker overlap and keeps the important accent");
            GD.Print("MAIN PRESENTATION A-H PASSED. Stopping; no full-song playback.");GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
