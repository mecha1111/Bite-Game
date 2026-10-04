using System;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Startup;
using Gamejam2.Tutorial;
using Gamejam2.Settings;
namespace Gamejam2.Debugging;
public partial class TutorialLayoutSmokeCheck:Node
{
 public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
 private async Task Run(){try{
 var router=GD.Load<PackedScene>("res://game/startup/Startup.tscn").Instantiate<SceneRouter>();router.SavePath="/private/tmp/bite-tutorial-layout-"+OS.GetProcessId()+".cfg";AddChild(router);
 SettingsService.CompleteCalibration();router.GoToTutorial(true);
 await ToSignal(GetTree().CreateTimer(.4),SceneTreeTimer.SignalName.Timeout);
 var tutorial=router.ScreenHost!.GetChild<TutorialGameplayScreen>(0);tutorial.SuspendForSettings();
 foreach(var path in new[]{".","Composition","EnvironmentBackground","EnvironmentBackground/Art","TutorialHud"}){
 var c=tutorial.GetNode<Control>(path);GD.Print(path+" size="+c.Size);
 if(!c.Size.IsEqualApprox(new Vector2(1920,1080)))throw new Exception("Incorrect Tutorial size: "+path+" "+c.Size);
 }
 if(tutorial.Environment.Background.Texture==null||tutorial.Environment.Background.SelfModulate==Colors.Black)throw new Exception("Background missing/black");
 await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
 GetViewport().GetTexture().GetImage().SavePng("res://artifacts/release-config/tutorial-layout-fixed.png");
 GD.Print("PASS Tutorial full viewport, centered UI, background texture visible");GetTree().Quit();
 }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}}
}
