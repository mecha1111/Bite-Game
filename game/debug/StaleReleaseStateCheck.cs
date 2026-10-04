using System;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Startup;
using Gamejam2.Title;
using Gamejam2.Calibration;
using Gamejam2.Tutorial;
using Gamejam2.Gameplay;
namespace Gamejam2.Debugging;
public partial class StaleReleaseStateCheck:Node
{
 public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
 private async Task Frames(){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
 private void Check(bool ok,string text){if(!ok)throw new Exception(text);GD.Print("PASS "+text);}
 private async Task Run()
 {
  try{
   var router=GD.Load<PackedScene>("res://game/startup/Startup.tscn").Instantiate<SceneRouter>();router.SavePath="/private/tmp/bite-stale-release-state-"+OS.GetProcessId()+".cfg";AddChild(router);
   router.ScreenHost!.GetChild<TitleScreen>(0).StartButton.EmitSignal(Button.SignalName.Pressed);await Frames();
   Check(router.ScreenHost.GetChild(0) is CalibrationScreen,"fresh Start -> Calibration");
   router.ScreenHost.GetChild<CalibrationScreen>(0).Flow!.Calibration!.SkipWithDefaults();await Frames();
   Check(router.ScreenHost.GetChild(0) is TutorialGameplayScreen,"Calibration -> Tutorial");
   var tutorial=router.ScreenHost.GetChild<TutorialGameplayScreen>(0);
   Check(tutorial.RunState==GameplayScreen.GameplayRunState.Playing&&tutorial.Environment.Background.Texture!=null&&tutorial.Environment.Background.SelfModulate!=Colors.Black,"Tutorial existing background visible");
   Check(tutorial.LessonIndex==0&&tutorial.LeftEyeVisibility==1&&tutorial.RightEyeVisibility==1&&tutorial.Attack.Blackout==0,"Tutorial initial state reset");
   await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("res://artifacts/release-config/tutorial-stale-reset.png");
   for(int i=0;i<5;i++)
   {
    router.Settings!.Open();if(i%2==1)router.Settings.Close();
    router.Settings.RecalibrateButton.EmitSignal(Button.SignalName.Pressed);
    router.Settings.RecalibrateButton.EmitSignal(Button.SignalName.Pressed); // Reject duplicate requests in the same frame.
    await Frames();
    Check(router.ScreenHost.GetChild(0) is CalibrationScreen,"manual Calibration "+(i+1)+"/5");
    Check(!router.Settings.Visible&&!router.Settings.RecalibrateButton.Disabled,"transition cleared "+(i+1));
    router.ScreenHost.GetChild<CalibrationScreen>(0).Flow!.Calibration!.SkipWithDefaults();await Frames();
   }
   GD.Print("SYNC SUCCESS 5/5");GetTree().Quit();
  }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
 }
}
