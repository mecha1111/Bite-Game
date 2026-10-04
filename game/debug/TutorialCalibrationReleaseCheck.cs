using System;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Startup;
using Gamejam2.Title;
using Gamejam2.Calibration;
using Gamejam2.Tutorial;
using Gamejam2.Gameplay;
namespace Gamejam2.Debugging;
public partial class TutorialCalibrationReleaseCheck:Node
{
 public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
 private async Task Wait(double time=.15)=>await ToSignal(GetTree().CreateTimer(time),SceneTreeTimer.SignalName.Timeout);
 private void Check(bool ok,string name){if(!ok)throw new Exception(name);GD.Print("PASS "+name);}
 private async Task Run()
 {
  try{
   var router=GD.Load<PackedScene>("res://game/startup/Startup.tscn").Instantiate<SceneRouter>();router.SavePath="/private/tmp/bite-tutorial-calibration-"+OS.GetProcessId()+".cfg";AddChild(router);
   GetWindow().GrabFocus();router.ScreenHost!.GetChild<TitleScreen>(0).StartButton.EmitSignal(Button.SignalName.Pressed);await Wait();
   Check(router.ScreenHost.GetChild(0) is CalibrationScreen,"A fresh Start -> Calibration");
   router.GoToTitle();router.Settings!.Open();await Wait(.35);router.Settings.RecalibrateButton.EmitSignal(Button.SignalName.Pressed);await Wait();
   Check(router.ScreenHost.GetChild(0) is CalibrationScreen,"B Settings Sync -> Calibration");
   var screen=router.ScreenHost.GetChild<CalibrationScreen>(0);var calibration=screen.Flow!.Calibration!;
   screen.Flow.Hud!.StartButton.EmitSignal(Button.SignalName.Pressed);
   Check(router.Music!.Playing&&router.Music.Stream is AudioStreamWav wav&&wav.Data.Length>0,"Calibration generated metronome plays");
   ulong deadline=Time.GetTicksMsec()+4000;while(router.Rhythm!.SongTimeSeconds<2&&Time.GetTicksMsec()<deadline)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
   Input.ParseInputEvent(new InputEventKey{PhysicalKeycode=Gamejam2.Settings.SettingsService.RhythmKey==Key.None?Key.Space:Gamejam2.Settings.SettingsService.RhythmKey,Pressed=true});Input.FlushBufferedEvents();await Wait(.02);
   Check(calibration.Samples.Count>0,"C calibration Bite input accepted");
   router.GoToTitle();router.ScreenHost.GetChild<TitleScreen>(0).StartButton.EmitSignal(Button.SignalName.Pressed);await Wait();router.ScreenHost.GetChild<CalibrationScreen>(0).Flow!.Calibration!.SkipWithDefaults();await Wait(.4);
   Check(router.ScreenHost.GetChild(0) is TutorialGameplayScreen,"D Calibration -> Tutorial");
   var tutorial=router.ScreenHost.GetChild<TutorialGameplayScreen>(0);Check(tutorial.RunState==GameplayScreen.GameplayRunState.Playing,"Tutorial initialized successfully");
   Check(tutorial.Environment.Background.Texture!=null&&tutorial.Environment.Background.SelfModulate!=Colors.Black,"E existing Tutorial world texture active: "+tutorial.Environment.Background.Texture?.ResourcePath);
   foreach(InterferenceBubble bubble in tutorial.GetNode("InterferenceIndicators/SafeArea").GetChildren())Check(bubble.Initialized&&bubble.SelectedAnimation=="active","indicator initialized: "+bubble.EffectType);
   Check(tutorial.Vision.LeftVisibility==1&&tutorial.Vision.RightVisibility==1&&tutorial.Attack.Blackout==0,"F initial vision transparent");
   tutorial.SuspendForSettings();tutorial.SetProcess(false);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("res://artifacts/release-config/tutorial-world-initial.png");
   tutorial.Vision.Present(0,0);tutorial.Attack.Blackout=1;tutorial.Attack.QueueRedraw();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("res://artifacts/release-config/tutorial-world-triggered-blackout.png");
   tutorial.Vision.Present(1,1);tutorial.Attack.Blackout=0;tutorial.Attack.QueueRedraw();GetTree().Quit();
  }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
 }
}
