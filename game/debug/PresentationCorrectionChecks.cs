using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Gameplay;
using Gamejam2.Startup;
using Gamejam2.Data;
namespace Gamejam2.Debugging;
public partial class PresentationCorrectionChecks : Node
{
    private int _checks;
    private void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);_checks++;GD.Print("PASS "+name);}
    private async Task Wait(double seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private async Task Capture(string name){await Wait(.06);GetViewport().GetTexture().GetImage().SavePng("/private/tmp/bite-corrections-"+name+".png");}
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Run()
    {
        try
        {
            var router=GD.Load<PackedScene>("res://game/startup/Startup.tscn").Instantiate<SceneRouter>();router.SavePath="/private/tmp/bite-corrections-check.cfg";AddChild(router);router.GoToLobby();router.GoToGameplay("stage_1");GetWindow().GrabFocus();
            var game=router.ScreenHost!.GetChild<GameplayScreen>(0);game.SuspendForSettings();
            var art=game.Shark.Art;
            Check(art.SpriteFrames.GetFrameCount("bite")==20,"20 consistent bite/idle frames");
            for(int i=0;i<20;i++){var frame=(AtlasTexture)art.SpriteFrames.GetFrameTexture("bite",i);Check(frame.Region.Size==new Vector2(256,256),"frame dimensions/pivot "+i);}
            var host=new Node2D{ZIndex=5};AddChild(host);
            WavePulse Spawn(WaveKind kind,Vector2 origin){var wave=GD.Load<PackedScene>("res://game/gameplay/WavePulse.tscn").Instantiate<WavePulse>();host.AddChild(wave);wave.Position=origin;wave.Begin(kind,0);return wave;}
            var small=Spawn(WaveKind.Small,new Vector2(350,440));var medium=Spawn(WaveKind.Medium,new Vector2(700,440));
            small.PresentAge(0);medium.PresentAge(0);Check(small.CurrentRadius==0&&medium.CurrentRadius==0,"Small/Medium start as point");await Capture("point");
            small.PresentAge(.225);medium.PresentAge(.225);Check(small.CurrentRadius>0&&medium.CurrentRadius>small.CurrentRadius,"rings grow from point");await Capture("growing");
            small.PresentAge(.45);medium.PresentAge(.45);Check(Math.Abs(small.CurrentRadius/medium.CurrentRadius-.25)<.001,"Small diameter is exactly one quarter Medium");Check(medium.CurrentRadius*2==260,"Medium head reference diameter260");await Capture("small-medium");
            small.PresentAge(.61);medium.PresentAge(.61);Check(small.Finished&&medium.Finished&&!small.Visible&&!medium.Visible,"localized waves fade and finish");
            foreach(var size in new[]{new Vector2I(1920,1080),new Vector2I(1600,900),new Vector2I(1366,768),new Vector2I(1280,720),new Vector2I(2560,1440),new Vector2I(1440,900)})
            {
                GetWindow().Size=size;await Wait(.15);
                var bottom = art.GlobalTransform * (art.Offset + new Vector2(0,128));
                var visible = GetViewport().GetVisibleRect();
                Check(Math.Abs(bottom.Y-visible.End.Y)<.01 && Math.Abs(bottom.X-visible.GetCenter().X)<.01,"first-frame centered and bottom-aligned / "+size);
                Check(art.Frame==0 && art.Offset==new Vector2(0,-128),"resting first frame uses full 256px bounds / "+size);
                foreach(var origin in new[]{new Vector2(120,420),new Vector2(1800,420),new Vector2(960,260)})
                {
                    var wave=Spawn(WaveKind.Large,origin);wave.PresentAge(0);Check(wave.CurrentRadius==0,"Large starts at point "+origin+" / "+size);
                    wave.PresentAge(.7);Check(wave.GlobalPosition==origin&&wave.CurrentRadius==630&&!wave.Finished,"fixed origin grows continuously "+origin);
                    float radius=wave.MaxRadius;var rect=GetViewport().GetVisibleRect();float far=new[]{rect.Position,new Vector2(rect.End.X,rect.Position.Y),rect.End,new Vector2(rect.Position.X,rect.End.Y)}.Max(c=>c.DistanceTo(origin));
                    Check(radius>far,"Large termination beyond furthest corner / thickness margin");
                    wave.PresentAge((radius-1)/wave.ExpansionSpeed);Check(wave.Visible&&!wave.Finished,"Large not removed before complete viewport exit");
                    wave.PresentAge((radius+1)/wave.ExpansionSpeed);Check(wave.Finished&&!wave.Visible,"Large removed only after exit");wave.QueueFree();
                }
                await Capture("shark-"+size.X+"x"+size.Y);
            }
            var nonWide=new SubViewport{Size=new Vector2I(1280,1024),Disable3D=true};AddChild(nonWide);
            var viewportWave=GD.Load<PackedScene>("res://game/gameplay/WavePulse.tscn").Instantiate<WavePulse>();nonWide.AddChild(viewportWave);viewportWave.Position=new Vector2(80,500);viewportWave.Begin(WaveKind.Large,0);viewportWave.PresentAge(.6);
            float nonWideFar=new[]{Vector2.Zero,new Vector2(1280,0),new Vector2(1280,1024),new Vector2(0,1024)}.Max(c=>c.DistanceTo(viewportWave.Position));
            Check(viewportWave.MaxRadius>nonWideFar&&viewportWave.MaxRadius<nonWideFar+20,"actual 5:4 viewport corner calculation, not a1920 constant");nonWide.QueueFree();
            GetWindow().Size=new Vector2I(1920,1080);await Wait(.1);
            var a=Spawn(WaveKind.Large,new Vector2(300,440));var b=Spawn(WaveKind.Large,new Vector2(1100,440));var c=Spawn(WaveKind.Medium,new Vector2(1500,440));
            a.PresentAge(.6);b.PresentAge(.3);c.PresentAge(0);Check(a.Visible&&b.Visible&&c.Visible&&a.CurrentRadius>b.CurrentRadius&&c.CurrentRadius==0,"independent waves overlap with different centers/radii");await Capture("overlap");a.QueueFree();b.QueueFree();c.QueueFree();
            var slot=game.Signals.LeftLane.Slots[0];slot.Reset();slot.Activate(WaveKind.Large,1,100);slot.Activate(WaveKind.Large,1,100.2);slot.Present(100.3);
            Check(slot.ActivePulses.Count==2&&slot.ActivePulses.All(w=>w.Origin==slot.GlobalPosition),"same slot retrigger preserves independent waves at slot origin");slot.Reset();
            Check(game.Signals.LeftLane.Slots.Length==10&&game.Signals.RightLane.Slots.Length==10,"10 fixed origins per mirrored lane");
            var ship=GD.Load<PackedScene>("res://game/gameplay/InterferenceShipHorn.tscn").Instantiate<InterferenceEffect>();host.AddChild(ship);ship.Position=new Vector2(960,460);
            var row=LocalCsv.Read("res://data/balance/interference_effects.csv").Single(r=>r["type"]=="ship_horn");var style=new InterferenceStyle(row["type"],LocalCsv.Number(row["delay_seconds"]),LocalCsv.Number(row["interval_seconds"]),int.Parse(row["emissions"]),LocalCsv.Number(row["duration_seconds"]));
            ship.Begin(style,0);ship.Present(0);Check(ship.EmissionCount==1&&ship.ActivePulses[0].CurrentRadius==0,"Ship Horn point warning");ship.Present(.99);Check(ship.EmissionCount==1,"one second warning delay from CSV");
            ship.Present(1.5);Check(ship.EmissionCount==4&&ship.ActivePulses.Select(w=>w.Origin).Distinct().Count()==4,"Ship Horn independent distinct origin events overlap");await Capture("ship-overlap");
            ship.Present(2.3);Check(ship.EmissionCount==7,"warning plus six CSV timed large events");ship.Present(2.7);Check(!ship.IsActive&&ship.ActivePulses.Count>0,"completed schedule does not truncate expanding rings");ship.Present(6);Check(ship.ActivePulses.Count==0,"all horn rings clear viewport and release");ship.QueueFree();host.QueueFree();
            var result=game.Result;result.ShowResult(new GameplayResult("Hear the Tide",880,800,20,7,6,0,21,33));await Wait(2.1);
            Check(result.Gauge.SharkImage.Texture.ResourcePath=="res://assets/result_shark_gauge.png","exact Result shark, no Gameplay HUD gauge");
            var panel=(AtlasTexture)result.Panel.GetNode<NinePatchRect>("Frame").Texture;Check(panel.Atlas.ResourcePath=="res://assets/result_panel_empty.png","exact empty Result panel");
            Check(result.Rows.GlobalPosition.X<result.Gauge.GlobalPosition.X&&result.MaxCombo.GlobalPosition.Y>result.Rows.GetGlobalRect().End.Y,"whiteboard left judgment/combo, right dominant satiety");
            Check(result.Perfect.Text=="20"&&result.Good.Text=="7"&&result.Bad.Text=="6"&&result.Miss.Text=="0"&&result.MaxCombo.Text=="21","fixture counts and max combo");Check(result.Gauge.Percentage.Text=="88%"&&result.Headline.Text=="CLEAR","880=88%, CLEAR");
            var material=(ShaderMaterial)result.Gauge.SharkImage.Material;var mask=((Texture2D)material.GetShaderParameter("interior_mask")).GetImage();Check(mask.GetPixel(0,0).A==0&&mask.GetPixel(700,550).A==1,"shark interior mask excludes outside pixels");await Capture("result-880");
            result.Gauge.Present(1100);Check(material.GetShaderParameter("fill_ratio").AsDouble()==1&&material.GetShaderParameter("overfill_strength").AsDouble()==1&&result.Gauge.Percentage.Text=="110%","overfill capped silhouette with extra tint");await Capture("result-1100");
            GD.Print($"PRESENTATION CORRECTIONS VERIFIED: {_checks} checks");GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
}
