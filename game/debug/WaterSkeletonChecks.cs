using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Gameplay;
using Gamejam2.Settings;
namespace Gamejam2.Debugging;
public partial class WaterSkeletonChecks : Node
{
    private int _checks;
    private async Task Wait(double seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private void Check(bool pass,string label){if(!pass)throw new Exception(label);GD.Print("PASS "+label);_checks++;}
    private async Task Capture(string name){await Wait(.08);GetViewport().GetTexture().GetImage().SavePng("/private/tmp/bite-subtle-"+name+".png");}
    private void KeyPress(Key key){foreach(bool pressed in new[]{true,false}){Input.ParseInputEvent(new InputEventKey{Keycode=key,PhysicalKeycode=key,Pressed=pressed});Input.FlushBufferedEvents();}}
    private void Click(Button button){var pos=button.GetGlobalRect().GetCenter();GetViewport().PushInput(new InputEventMouseMotion{Position=pos,GlobalPosition=pos},true);foreach(bool down in new[]{true,false})GetViewport().PushInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Position=pos,GlobalPosition=pos,Pressed=down},true);}
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Run()
    {
        try
        {
            Gamejam2.Save.SaveStore.Initialize("/private/tmp/bite-subtle-water-check.cfg");Gamejam2.Save.ProgressService.Initialize();SettingsService.Initialize();SettingsService.SetResolution(2);SettingsService.InputOffsetSeconds=SettingsService.VisualOffsetSeconds=0;GetWindow().GrabFocus();
            var title=GD.Load<PackedScene>("res://game/title/Title.tscn").Instantiate();AddChild(title);await Wait(5);await Capture("title");title.QueueFree();await Wait(.1);
            for(int song=1;song<=4;song++)
            {
                var game=GD.Load<PackedScene>("res://game/gameplay/Gameplay.tscn").Instantiate<GameplayScreen>();game.SongId="song_"+song;AddChild(game);game.SetProcessInput(false);await Wait(.3);game.SuspendForSettings();game.SetProcess(false);
                game.Feedback.Visible=false;game.Combo.Visible=false;game.Environment.LeftParticles.SpeedScale=game.Environment.RightParticles.SpeedScale=game.Environment.FarParticles.SpeedScale=0;
                game.Environment.Present(song==4?31.6:2,true,0);game.Shark.Present(2);
                game.Signals.Present(game.Data.Phrases[0].StartSeconds+.16);
                var water=(ShaderMaterial)game.WorldWaterTreatment.Material;
                Check(water.GetShaderParameter("refraction_strength").AsSingle()<=.001,"tiny refraction profile "+song);
                Check(water.GetShaderParameter("caustic_strength").AsSingle()<=.0081,"barely visible broad light profile "+song);
                Check(game.Shark.Skeleton.GetParent()==game.Shark&&game.WorldWaterTreatment.ZIndex>game.Shark.GetParent<Control>().ZIndex,"skeleton shares shark world below water shader "+song);
                game.WorldWaterTreatment.Visible=false;await Capture("song-"+song+"-off");game.WorldWaterTreatment.Visible=true;await Capture("song-"+song+"-on");
                if(song==1)
                {
                    game.Environment.Present(7,true,0);await Capture("song-1-moving-light");
                    // Force the actual guarded starvation route, not the overlay method alone.
                    typeof(SatietyState).GetProperty("Value")!.SetValue(game.Satiety,0d);game._Process(.016);
                    Check(game.GameOver.Visible&&!game.Result.Visible&&!game.Rhythm.IsRunning&&!game.Music.Playing&&game.InputBlocked,"starvation stops real run without receipt");
                    await Wait(.3);await Capture("skeleton-transition");await Wait(.85);
                    Check(game.Shark.Art.SelfModulate.A==0&&game.Shark.Skeleton.Visible&&game.Shark.Skeleton.Modulate.A>.99,"normal shark replaced by skeleton");
                    Check(game.Shark.Skeleton.Texture is AtlasTexture atlas&&atlas.Atlas.ResourcePath.Normalize().Contains("상어 뼈"),"exact supplied skeleton resource");
                    Check(game.Shark.Skeleton.Position==new Vector2(0,-120)&&game.Shark.Position.Y==0,"skeleton settles at same bottom world anchor");
                    Check(game.GameOver.IsRevealed&&game.GameOver.Gauge.Percentage.Text=="0%"&&game.GameOver.Actions.RetryButton.HasFocus(),"one second starvation UI zero/default retry");await Capture("game-over");
                    KeyPress(Key.Enter);await Wait(.12);
                    Check(!game.GameOver.Visible&&!game.Shark.Skeleton.Visible&&game.Shark.Art.SelfModulate.A==1&&game.Rhythm.IsRunning,"retry restores living shark and real run");
                    bool exited=false;game.LobbyRequested+=()=>exited=true;
                    typeof(SatietyState).GetProperty("Value")!.SetValue(game.Satiety,0d);game._Process(.016);await Wait(1.1);
                    KeyPress(Key.Right);Check(game.GameOver.Actions.SongSelectButton.HasFocus(),"keyboard Right focuses song select");
                    Click(game.GameOver.Actions.SongSelectButton);Check(exited,"mouse song-select retains route");
                }
                if(song>1){typeof(SatietyState).GetProperty("Value")!.SetValue(game.Satiety,0d);game._Process(.016);await Wait(1.1);Check(game.Shark.Skeleton.Visible&&game.Shark.Art.SelfModulate.A==0,"skeleton transition in stage "+song);await Capture("game-over-song-"+song);}
                game.StopGameplay();game.QueueFree();await Wait(.1);
            }
            GD.Print($"WATER SKELETON VERIFIED {_checks} checks");GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
