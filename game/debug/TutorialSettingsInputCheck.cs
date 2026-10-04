using System;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Startup;
using Gamejam2.Tutorial;
using Gamejam2.Calibration;
using Gamejam2.Settings;
namespace Gamejam2.Debugging;
public partial class TutorialSettingsInputCheck:Node
{
 public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
 private async Task Wait(double sec=.1)=>await ToSignal(GetTree().CreateTimer(sec),SceneTreeTimer.SignalName.Timeout);
 private void Check(bool ok,string name){if(!ok)throw new Exception(name);GD.Print("PASS "+name);}
 private async Task Click(Control control)
 {
  var pos=control.GetGlobalRect().GetCenter();
  GetViewport().PushInput(new InputEventMouseMotion{Position=pos,GlobalPosition=pos},true);
  GetViewport().PushInput(new InputEventMouseButton{Position=pos,GlobalPosition=pos,ButtonIndex=MouseButton.Left,Pressed=true},true);
  await Wait(.02);
  GetViewport().PushInput(new InputEventMouseButton{Position=pos,GlobalPosition=pos,ButtonIndex=MouseButton.Left,Pressed=false},true);
  await Wait();
 }
 private async Task Run()
 {
  try{
   var router=GD.Load<PackedScene>("res://game/startup/Startup.tscn").Instantiate<SceneRouter>();router.SavePath="/private/tmp/bite-settings-input-"+OS.GetProcessId()+".cfg";AddChild(router);
   SettingsService.CompleteCalibration();router.GoToTutorial(true);await Wait(.3);
   var tutorial=router.ScreenHost!.GetChild<TutorialGameplayScreen>(0);
   Check(tutorial.IsDemonstrating,"Tutorial demonstration active");
   tutorial.SettingsButton.EmitSignal(Button.SignalName.Pressed);await Wait(.4);
   var settings=router.Settings!;Check(settings.Visible&&tutorial.InputBlocked,"Settings opens and only gameplay pauses");
   var slider=settings.MasterSlider;double before=slider.Value;
   var point=slider.GetGlobalRect().Position+new Vector2(slider.Size.X*.28f,slider.Size.Y*.5f);
   GetViewport().PushInput(new InputEventMouseMotion{Position=point,GlobalPosition=point},true);
   GetViewport().PushInput(new InputEventMouseButton{Position=point,GlobalPosition=point,ButtonIndex=MouseButton.Left,Pressed=true},true);
   await Wait(.03);
   point.X+=slider.Size.X*.1f;
   GetViewport().PushInput(new InputEventMouseMotion{Position=point,GlobalPosition=point,ButtonMask=MouseButtonMask.Left,Relative=new Vector2(slider.Size.X*.1f,0)},true);
   GetViewport().PushInput(new InputEventMouseButton{Position=point,GlobalPosition=point,ButtonIndex=MouseButton.Left,Pressed=false},true);
   await Wait();Check(slider.Value!=before&&Math.Abs(SettingsService.MasterVolume-slider.Value/100)<.001,"Master slider mouse interaction and binding");
   settings.Scroll.EnsureControlVisible(settings.RecalibrateButton);await Wait(.15);await Click(settings.RecalibrateButton);await Wait(.2);
   Check(router.ScreenHost.GetChild(0) is CalibrationScreen,"Tutorial Settings Sync mouse click -> Calibration");
   router.ScreenHost.GetChild<CalibrationScreen>(0).Flow!.Calibration!.SkipWithDefaults();await Wait(.45);
   Check(router.ScreenHost.GetChild(0)==tutorial&&settings.Visible,"Return from Calibration to Tutorial Settings");
   GetViewport().PushInput(new InputEventKey{Keycode=Key.Escape,PhysicalKeycode=Key.Escape,Pressed=true},true);await Wait(.3);
   Check(!settings.Visible,"ESC closes Tutorial Settings");GetTree().Quit();
  }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
 }
}
