using System;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Startup;
using Gamejam2.Settings;
using Gamejam2.Tutorial;
using Gamejam2.Gameplay;
using Gamejam2.Calibration;
namespace Gamejam2.Debugging;
public partial class WindowsUiCompatibilityFixCheck:Node
{
 public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
 private async Task Wait(double t=.12)=>await ToSignal(GetTree().CreateTimer(t),SceneTreeTimer.SignalName.Timeout);
 private void Check(bool ok,string text){if(!ok)throw new Exception(text);GD.Print("PASS "+text);}
 private async Task Click(Control c){var p=c.GetGlobalTransformWithCanvas()*(c.Size*.5f);GetViewport().PushInput(new InputEventMouseMotion{Position=p,GlobalPosition=p},true);GetViewport().PushInput(new InputEventMouseButton{Position=p,GlobalPosition=p,ButtonIndex=MouseButton.Left,Pressed=true},true);await Wait(.03);GetViewport().PushInput(new InputEventMouseButton{Position=p,GlobalPosition=p,ButtonIndex=MouseButton.Left,Pressed=false},true);await Wait();}
 private async Task Sync(SceneRouter router,string name){for(int i=0;i<5;i++){router.Settings!.Scroll.EnsureControlVisible(router.Settings.RecalibrateButton);await Wait(.1);await Click(router.Settings.RecalibrateButton);await Wait(.1);Check(router.ScreenHost!.GetChild(0) is CalibrationScreen,name+" Sync "+(i+1)+"/5");router.ScreenHost.GetChild<CalibrationScreen>(0).Flow!.Calibration!.SkipWithDefaults();await Wait(.35);Check(router.Settings.Visible,name+" return Settings");}}
 private async Task Run(){try{
 var router=GD.Load<PackedScene>("res://game/startup/Startup.tscn").Instantiate<SceneRouter>();router.SavePath="/private/tmp/bite-ui-fix-"+OS.GetProcessId()+".cfg";AddChild(router);GetWindow().GrabFocus();
 SettingsService.CompleteCalibration();router.GoToTutorial(true);await Wait(.3);var t=router.ScreenHost!.GetChild<TutorialGameplayScreen>(0);
 Check(t.Size==new Vector2(1920,1080)&&t.GetNode<Control>("Composition").Size==t.Size,"Tutorial full reference composition");
 Check(t.GetNode<Control>("Composition/SharkAnchor").GlobalPosition==new Vector2(960,1080),"Tutorial shark bottom center");
 Check(t.Environment.Size==t.Size&&t.Environment.Background.Size==t.Size&&t.Environment.Background.Texture!=null&&t.Environment.Background.SelfModulate!=Colors.Black,"Tutorial background visible");
 t.SuspendForSettings();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("/private/tmp/bite-tutorial-runtime-layout-fix.png");
 t.OpenSkipConfirmation();GetViewport().PushInput(new InputEventKey{PhysicalKeycode=Key.Escape,Keycode=Key.Escape,Pressed=true},true);await Wait();Check(!t.SkipConfirmation.Visible&&!router.Settings!.Visible,"Tutorial ESC cancels skip only");
 t.SettingsButton.EmitSignal(Button.SignalName.Pressed);await Wait(.4);var slider=router.Settings!.MasterSlider;double previous=slider.Value;
 var p=slider.GetGlobalTransformWithCanvas()*(new Vector2(slider.Size.X*.3f,slider.Size.Y*.5f));
 GetViewport().PushInput(new InputEventMouseMotion{Position=p,GlobalPosition=p},true);
 GetViewport().PushInput(new InputEventMouseButton{Position=p,GlobalPosition=p,ButtonIndex=MouseButton.Left,Pressed=true},true);
 await Wait(.03);GetViewport().PushInput(new InputEventMouseButton{Position=p,GlobalPosition=p,ButtonIndex=MouseButton.Left,Pressed=false},true);await Wait();
 Check(slider.Value!=previous&&Math.Abs(SettingsService.MasterVolume-slider.Value/100)<.001,"Tutorial volume slider input and binding");
 await Sync(router,"Tutorial");
 router.GoToLobby();router.GoToGameplay("stage_1");await Wait(.3);var g=router.ScreenHost.GetChild<GameplayScreen>(0);g.SettingsButton.EmitSignal(Button.SignalName.Pressed);await Wait(.4);g.SetProcess(false);
 foreach(var shot in new[]{new Vector2(-450,0),new Vector2(300,0)}){
 g.Presentation.WorldCamera!.ApplyShot(shot,1.12f,g.Juice.MouthAnchor.GlobalPosition);
 Check(router.Settings!.GetGlobalTransformWithCanvas().IsEqualApprox(Transform2D.Identity),"Settings unaffected by camera "+shot);
 Check(router.Settings.GetCanvasLayerNode().Layer>g.GetNode<CanvasLayer>("FixedHudCanvas").Layer,"Settings above HUD");
 }
 await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("/private/tmp/bite-settings-fixed-runtime-layer.png");
 await Sync(router,"Main");GetTree().Quit();
 }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}}
}
