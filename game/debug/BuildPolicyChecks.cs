using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Startup;
using Gamejam2.Save;
using Gamejam2.Settings;
using Gamejam2.Lobby;
using Gamejam2.Data;
using Gamejam2.Title;
using Gamejam2.Calibration;
using Gamejam2.Tutorial;
namespace Gamejam2.Debugging;
/// <summary>Export-policy fixture excluded from all player exports; isolated profiles only.</summary>
public partial class BuildPolicyChecks : Node
{
    private int _checks;
    private void Check(bool ok,string name){if(!ok)throw new Exception(name);_checks++;GD.Print("PASS "+name);}
    private async Task Wait(double sec)=>await ToSignal(GetTree().CreateTimer(sec),SceneTreeTimer.SignalName.Timeout);
    private void Key(Key key){foreach(bool pressed in new[]{true,false}){Input.ParseInputEvent(new InputEventKey{PhysicalKeycode=key,Keycode=key,Pressed=pressed});Input.FlushBufferedEvents();}}
    private SceneRouter Router(string profile){var r=GD.Load<PackedScene>("res://game/startup/Startup.tscn").Instantiate<SceneRouter>();r.SavePath="/private/tmp/bite-build-policy-"+OS.GetProcessId()+"-"+profile+".cfg";AddChild(r);return r;}
    private async Task Capture(string name){await Wait(.05);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var image=GetViewport().GetTexture().GetImage();image.SavePng("res://artifacts/release-config/"+name+".png");}
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Run()
    {
        try
        {
            GetWindow().GrabFocus();if(BuildFeatures.TutorialEnabled)
            {
            Check(BuildFeatures.TutorialEnabled,"development tutorial enabled");var dev=Router("development");await Wait(.1);
            Check(dev.Settings!.TutorialButton.GetParent<Control>().Visible,"development replay row visible");dev.ScreenHost!.GetChild<TitleScreen>(0).StartButton.EmitSignal(Button.SignalName.Pressed);await Wait(.1);
            Check(dev.ScreenHost.GetChild(0) is CalibrationScreen,"fresh development profile calibrates first");dev.ScreenHost.GetChild<CalibrationScreen>(0).Flow!.Calibration!.SkipWithDefaults();await Wait(.2);
            Check(dev.ScreenHost.GetChild(0) is TutorialGameplayScreen,"development first-launch Tutorial remains intact");dev.QueueFree();await Wait(.15);GD.Print("DEVELOPMENT BUILD POLICY VERIFIED "+_checks);GetTree().Quit();return;
            }
            // Native export custom-feature path; no production test override/cheat exists.
            Check(OS.HasFeature("bite_release")&&!BuildFeatures.TutorialEnabled&&!BuildFeatures.DevelopmentEnabled,"export feature disables tutorial and all developer capabilities");
            var router=Router("release");await Wait(.1);var s=router.Settings!;
            Check(!s.TutorialButton.GetParent<Control>().Visible,"release Settings removes whole replay row");Key(Godot.Key.T);Check(!ProgressService.DebugUnlockAllStages&&!ProgressService.IsUnlocked("stage_2"),"release Title T ignored without fake unlock writes");
            router.ScreenHost!.GetChild<TitleScreen>(0).StartButton.EmitSignal(Button.SignalName.Pressed);await Wait(.1);Check(router.ScreenHost.GetChild(0) is CalibrationScreen,"fresh release profile still calibrates");
            router.ScreenHost.GetChild<CalibrationScreen>(0).Flow!.Calibration!.SkipWithDefaults();await Wait(.15);
            Check(router.ScreenHost.GetChild(0) is LobbyScreen&&!TutorialGameplayScreen.HasCompleted,"release Calibration routes directly to Lobby without faking tutorial completion");
            var lobby=router.ScreenHost.GetChild<LobbyScreen>(0);Check(!ProgressService.IsUnlocked("stage_2")&&!ProgressService.IsUnlocked("stage_3")&&!ProgressService.IsUnlocked("stage_4"),"fresh main-stage locks preserved");Key(Godot.Key.T);Check(!ProgressService.DebugUnlockAllStages,"release Lobby T ignored");
            SaveStore.Write("progress","CustomMapLocked",true);SaveStore.Write("progress","CustomMapUnlocked",false);SaveStore.Flush();
            lobby.GetNode<Button>("Categories/Custom").EmitSignal(Button.SignalName.Pressed);await Wait(.15);
            Check(lobby.CustomCategory&&lobby.Catalog.Stages.Count==CustomMapRegistry.Maps.Count&&CustomMapRegistry.Maps.All(m=>CustomMapRegistry.IsUnlocked(m.StageId)),"all registered customs unlocked without main progression despite legacy lock flags");
            Check(lobby.Cards.GetChildren().OfType<StageCard>().Where(c=>c.Visible).All(c=>!c.LockOverlay.Visible),"fresh custom category has no lock overlays");
            foreach(var map in CustomMapRegistry.Maps)GD.Print($"CUSTOM READINESS {map.StageId}: unlocked={CustomMapRegistry.IsUnlocked(map.StageId)} chart={map.ChartPath} playable={map.Available}");
            await Capture("release-custom-category");s.Open();await Wait(.4);await Capture("release-settings");
            var fixtureMap=await GeneratedMapTestFixture.Ensure(this);ProgressService.RecordStageResult("song_1",true,true,true);CustomMapRegistry.Record(fixtureMap.SongId,true,true,true,1100);SaveStore.Write("progress","tutorial_completed",true);SaveStore.Flush();
            s.ResetProgressButton.EmitSignal(Button.SignalName.Pressed);s.ConfirmResetButton.EmitSignal(Button.SignalName.Pressed);await Wait(.05);
            Check(CustomMapRegistry.BestRank(fixtureMap.StageId)=="none"&&CustomMapRegistry.Maps.All(m=>CustomMapRegistry.IsUnlocked(m.StageId))&&!TutorialGameplayScreen.HasCompleted,"progress reset clears custom achievements without relocking customs");
            s.CloseImmediately();router.GoToTitle();await Wait(.1);router.ScreenHost.GetChild<TitleScreen>(0).StartButton.EmitSignal(Button.SignalName.Pressed);await Wait(.1);
            Check(router.ScreenHost.GetChild(0) is LobbyScreen,"release next Start after reset skips Tutorial with saved calibration");
            router.GoToTutorial(true);Check(router.ScreenHost.GetChild(0) is LobbyScreen,"direct Tutorial route centrally disabled in exports");
            Check(ProjectSettings.GetSetting("application/config/icon").AsString()=="res://assets/아이콘.png","official uploaded icon assigned");
            router.QueueFree();await Wait(.1);GD.Print("BUILD POLICY VERIFIED "+_checks);GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
