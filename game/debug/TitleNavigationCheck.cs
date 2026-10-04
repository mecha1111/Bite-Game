using System;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Startup;
using Gamejam2.Title;
using Gamejam2.Settings;
using Gamejam2.Save;
using Gamejam2.Tutorial;
using Gamejam2.Calibration;
using Gamejam2.Lobby;
namespace Gamejam2.Debugging;
public partial class TitleNavigationCheck:Node
{
 public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
 private async Task Wait()=>await ToSignal(GetTree().CreateTimer(.35),SceneTreeTimer.SignalName.Timeout);
 private async Task Run()
 {
  try
  {
   var router=GD.Load<PackedScene>("res://game/startup/Startup.tscn").Instantiate<SceneRouter>();
   router.SavePath="/private/tmp/bite-title-navigation-"+OS.GetProcessId()+".cfg";AddChild(router);
   var title=router.ScreenHost!.GetChild<TitleScreen>(0);
   title.StartButton.EmitSignal(Button.SignalName.Pressed);await Wait();
   if(router.ScreenHost.GetChild(0) is not CalibrationScreen)throw new Exception("A Start to Calibration");
   GD.Print("PASS A Title Start -> Calibration");
   router.GoToTitle();router.Settings!.Open();await Wait();router.Settings.RecalibrateButton.EmitSignal(Button.SignalName.Pressed);await Wait();
   if(router.ScreenHost.GetChild(0) is not CalibrationScreen)throw new Exception("B manual Calibration");
   GD.Print("PASS B manual Calibration -> Calibration");
   SettingsService.CompleteCalibration();router.GoToTitle();
   router.ScreenHost.GetChild<TitleScreen>(0).StartButton.EmitSignal(Button.SignalName.Pressed);await Wait();
   if(router.ScreenHost.GetChild(0) is not TutorialGameplayScreen)throw new Exception("C completed Calibration -> Tutorial");
   GD.Print("PASS C calibrated incomplete Tutorial -> Tutorial");
   SaveStore.Write("progress","tutorial_completed",true);SaveStore.Flush();router.GoToTitle();
   router.ScreenHost.GetChild<TitleScreen>(0).StartButton.EmitSignal(Button.SignalName.Pressed);await Wait();
   if(router.ScreenHost.GetChild(0) is not LobbyScreen)throw new Exception("D completed Tutorial -> Song Select");
   GD.Print("PASS D completed onboarding -> Song Select");GetTree().Quit();
  }catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
 }
}
