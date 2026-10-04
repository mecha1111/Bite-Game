using System;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Startup;
using Gamejam2.Title;
using Gamejam2.Gameplay;
namespace Gamejam2.Debugging;
public partial class ResolutionLayoutCheck:Node
{
 public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
 private async Task Wait(){await ToSignal(GetTree().CreateTimer(.2),SceneTreeTimer.SignalName.Timeout);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);}
 private void Check(bool ok,string what){if(!ok)throw new Exception(what);}
 private async Task Run()
 {
  try{
   var router=GD.Load<PackedScene>("res://game/startup/Startup.tscn").Instantiate<SceneRouter>();router.SavePath="/private/tmp/bite-layout-"+OS.GetProcessId()+".cfg";AddChild(router);
   Rect2? gauge=null;
   foreach(var size in new[]{new Vector2I(1920,1080),new Vector2I(1600,900),new Vector2I(1280,720)})
   {
    var window=GetWindow();window.Size=size;router.GoToTitle();await Wait();
    var title=router.ScreenHost!.GetChild<TitleScreen>(0);
    Check(window.ContentScaleSize==new Vector2I(1920,1080)&&window.ContentScaleAspect==Window.ContentScaleAspectEnum.Keep,"1920 reference");
    Check(title.Size.IsEqualApprox(new Vector2(1920,1080)),"Title logical size "+title.Size);
    Check(Math.Abs(title.Logo.Position.X-960)<1,"Title centered");
    GetViewport().GetTexture().GetImage().SavePng("res://artifacts/release-config/layout-title-"+size.X+".png");
    router.GoToLobby();router.GoToGameplay("stage_1");await Wait();
    var game=router.ScreenHost.GetChild<GameplayScreen>(0);Check(game.RunState==GameplayScreen.GameplayRunState.Playing,"Gameplay starts");
    game.SuspendForSettings();game.SetProcess(false);game.Presentation.Present(0);await Wait();
    Check(game.Size.IsEqualApprox(new Vector2(1920,1080)),"Gameplay reference size "+game.Size);
    var shark=game.GetNode<Control>("Composition/SharkAnchor").GlobalPosition;
    Check(shark.IsEqualApprox(new Vector2(960,1080)),"Shark bottom center "+shark);
    var rect=game.Gauge.GetGlobalRect();if(gauge.HasValue)Check(rect==gauge.Value,"HUD unchanged");gauge=rect;
    Check(game.Gauge.GetParent().GetParent() is CanvasLayer,"HUD fixed CanvasLayer");
    var bg=game.Environment.Background.GetGlobalRect();Check(bg.Position.X<=0&&bg.Position.Y<=0&&bg.End.X>=1920&&bg.End.Y>=1080,"Background covers");
    GetViewport().GetTexture().GetImage().SavePng("res://artifacts/release-config/layout-gameplay-"+size.X+".png");
    GD.Print("PASS "+size+": centered Title, covered background, bottom-center shark, fixed HUD, 1920x1080 logical composition");
   }
   GetTree().Quit();
  }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
 }
}
