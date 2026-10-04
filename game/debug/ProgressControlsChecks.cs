using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Startup;
using Gamejam2.Save;
using Gamejam2.Settings;
using Gamejam2.Lobby;
using Gamejam2.Data;
using Gamejam2.Tutorial;
namespace Gamejam2.Debugging;
/// <summary>Isolated developer fixture; never routed from player screens.</summary>
public partial class ProgressControlsChecks : Node
{
    private int _checks;
    private void Check(bool ok,string name){if(!ok)throw new Exception(name);_checks++;GD.Print("PASS "+name);}
    private async Task Wait(double sec)=>await ToSignal(GetTree().CreateTimer(sec),SceneTreeTimer.SignalName.Timeout);
    private void Key(Key key){foreach(bool pressed in new[]{true,false}){Input.ParseInputEvent(new InputEventKey{PhysicalKeycode=key,Keycode=key,Pressed=pressed});Input.FlushBufferedEvents();}}
    private void Click(Button button){var pos=button.GetGlobalRect().GetCenter();GetViewport().PushInput(new InputEventMouseMotion{Position=pos,GlobalPosition=pos},true);foreach(bool down in new[]{true,false})GetViewport().PushInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Position=pos,GlobalPosition=pos,Pressed=down},true);}
    private async Task Capture(string name){await Wait(.05);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var image=GetViewport().GetTexture().GetImage();image.SavePng("res://artifacts/gameover-bubble-fix/"+name+".png");}
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Run()
    {
        try
        {
            var reload=OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--reload-save="));
            if(reload!=null)
            {
                SaveStore.Initialize(reload.Substring("--reload-save=".Length));ProgressService.Initialize();SettingsService.Initialize();
                string reloadCustomId=SaveStore.Read("fixture","custom_map_id","").AsString();Check(reloadCustomId.StartsWith("custom_local_"),"generated fixture id persisted");
                Check(!ProgressService.DebugUnlockAllStages&&ProgressService.IsUnlocked("stage_1")&&!ProgressService.IsUnlocked("stage_2")&&!ProgressService.IsUnlocked("stage_3")&&!ProgressService.IsUnlocked("stage_4"),"new process restores saved lock states without debug override");
                Check(!TutorialGameplayScreen.HasCompleted&&ProgressService.BestStageRank("stage_1")=="none"&&!ProgressService.HasFullCombo("song_1")&&!ProgressService.HasAllPerfect("song_1")&&CustomMapRegistry.BestRank(reloadCustomId)=="none","new process retains reset core/custom/tutorial records");
                Check(SettingsService.CalibrationCompleted&&Math.Abs(SettingsService.MasterVolume-.61)<.001&&Math.Abs(SettingsService.MusicVolume-.37)<.001&&Math.Abs(SettingsService.EffectsVolume-.82)<.001&&Math.Abs(SettingsService.InputOffsetSeconds-.083)<.00001&&Math.Abs(SettingsService.VisualOffsetSeconds-.037)<.00001,"new process preserves audio/calibration/offsets");
                GD.Print("PROGRESS RESTART VERIFIED "+_checks);GetTree().Quit();return;
            }
            var router=GD.Load<PackedScene>("res://game/startup/Startup.tscn").Instantiate<SceneRouter>();router.SavePath="/private/tmp/bite-progress-controls-"+OS.GetProcessId()+".cfg";AddChild(router);GetWindow().GrabFocus();router.GoToLobby();await Wait(.2);
            var map=await GeneratedMapTestFixture.Ensure(this);string customId=map.StageId;SaveStore.Write("fixture","custom_map_id",customId);
            var lobby=router.ScreenHost!.GetChild<LobbyScreen>(0);var s=router.Settings!;
            Check(!ProgressService.IsUnlocked("stage_2"),"fresh saved progression locked");SaveStore.Flush();string before=FileAccess.GetFileAsString(router.SavePath);
            Key(Godot.Key.T);await Wait(.05);
            Check(new[]{"stage_1","stage_2","stage_3","stage_4"}.All(ProgressService.IsUnlocked),"actual T input enables runtime debug override");
            Check(lobby.Cards.GetChildren().OfType<StageCard>().All(c=>!c.LockOverlay.Visible),"stage cards refresh immediately on Changed");
            Check(FileAccess.GetFileAsString(router.SavePath)==before&&!ProgressService.HasFullCombo("song_1")&&ProgressService.BestStageRank("stage_1")=="none","debug unlock writes no progression or achievements");
            Check(router.GetNode<Label>("DeveloperToast").Visible,"developer toast shown");
            ProgressService.DisableDebugUnlock();s.Open();await Wait(.4);Key(Godot.Key.T);Check(!ProgressService.DebugUnlockAllStages,"T ignored while Settings open");s.CloseImmediately();
            router.GoToCalibration(CalibrationReason.Settings);await Wait(.1);Key(Godot.Key.T);Check(!ProgressService.DebugUnlockAllStages,"T ignored during Calibration");router.GoToLobby();await Wait(.1);lobby=router.ScreenHost.GetChild<LobbyScreen>(0);
            SettingsService.SetMasterVolume(.61);SettingsService.SetMusicVolume(.37);SettingsService.SetEffectsVolume(.82);SettingsService.InputOffsetSeconds=.083;SettingsService.VisualOffsetSeconds=.037;SettingsService.CompleteCalibration();
            SaveStore.Write("progress","tutorial_completed",true);ProgressService.UnlockStage("stage_2");ProgressService.RecordStageResult("song_1",true,true,true);CustomMapRegistry.Record(customId,true,true,true,1100);SaveStore.Flush();
            var snapshot=new ConfigFile();snapshot.Load(router.SavePath);
            Check(TutorialGameplayScreen.HasCompleted&&ProgressService.BestStageRank("stage_1")=="gold"&&CustomMapRegistry.BestRank(customId)=="gold","test core and custom achievements established");
            s.Open();await Wait(.4);s.ResetProgressButton.EmitSignal(Button.SignalName.Pressed);Check(s.ResetConfirmation.Visible&&s.CancelResetButton.HasFocus(),"reset confirmation defaults to Cancel");
            Key(Godot.Key.Escape);Check(!s.ResetConfirmation.Visible&&s.Visible&&TutorialGameplayScreen.HasCompleted,"ESC cancels reset without erasing data");
            s.ResetProgressButton.EmitSignal(Button.SignalName.Pressed);await Wait(.08);Click(s.CancelResetButton);await Wait(.02);Check(!s.ResetConfirmation.Visible&&TutorialGameplayScreen.HasCompleted,"mouse Cancel preserves progression");
            foreach(var size in new[]{new Vector2I(1920,1080),new(1600,900),new(1366,768),new(1280,720),new(2560,1440)})
            {
                GetWindow().Size=size;await Wait(.08);s.Scroll.ScrollVertical=9999;await Wait(.08);await Capture("progress-data-"+size.X);
                s.ResetProgressButton.EmitSignal(Button.SignalName.Pressed);await Wait(.08);await Capture("progress-confirm-"+size.X);
                Check(s.ResetConfirmation.GetNode<Control>("Panel").GetGlobalRect().Encloses(s.ConfirmResetButton.GetGlobalRect()),"confirmation buttons contained "+size.X);Key(Godot.Key.Escape);
            }
            ProgressService.EnableDebugUnlock();s.ResetProgressButton.EmitSignal(Button.SignalName.Pressed);Key(Godot.Key.Tab);Check(s.ConfirmResetButton.HasFocus(),"Tab selects reset within modal");Key(Godot.Key.Enter);await Wait(.1);
            Check(!s.ResetConfirmation.Visible&&s.Visible&&router.ScreenHost.GetChild(0) is LobbyScreen,"confirmed reset stays in Settings/menu");
            Check(!TutorialGameplayScreen.HasCompleted&&!ProgressService.HasFullCombo("song_1")&&!ProgressService.HasAllPerfect("song_1")&&ProgressService.BestStageRank("stage_1")=="none"&&CustomMapRegistry.BestRank(customId)=="none","all core/custom records and TutorialCompleted cleared");
            Check(ProgressService.DebugUnlockAllStages&&ProgressService.IsUnlocked("stage_2"),"debug view remains unlocked after underlying progress reset");
            ProgressService.DisableDebugUnlock();Check(ProgressService.IsUnlocked("stage_1")&&!ProgressService.IsUnlocked("stage_2")&&lobby.Cards.GetChildren().OfType<StageCard>().Any(c=>c.LockOverlay.Visible),"real lock states immediately restored without override");
            var after=new ConfigFile();after.Load(router.SavePath);foreach(var key in snapshot.GetSectionKeys("settings"))Check(snapshot.GetValue("settings",key).Equals(after.GetValue("settings",key)),"device setting preserved: "+key);
            s.ResetProgressButton.EmitSignal(Button.SignalName.Pressed);await Wait(.08);Click(s.ConfirmResetButton);await Wait(.02);Check(!s.ResetConfirmation.Visible,"mouse Confirm also completes reset");
            SaveStore.Initialize(router.SavePath);ProgressService.Initialize();SettingsService.Initialize();Check(!ProgressService.IsUnlocked("stage_2")&&!TutorialGameplayScreen.HasCompleted&&ProgressService.BestStageRank("stage_1")=="none"&&CustomMapRegistry.BestRank(customId)=="none","reset persists through save/service reload");
            Check(SettingsService.CalibrationCompleted&&Math.Abs(SettingsService.InputOffsetSeconds-.083)<.00001&&Math.Abs(SettingsService.VisualOffsetSeconds-.037)<.00001,"calibration and offsets retained on reload");
            s.CloseImmediately();router.GoToTutorial();await Wait(.1);Key(Godot.Key.T);Check(!ProgressService.DebugUnlockAllStages,"T ignored during Tutorial");router.GoToGameplay("stage_1");await Wait(.1);Key(Godot.Key.T);Check(!ProgressService.DebugUnlockAllStages,"T ignored during Gameplay");router.GoToTitle();await Wait(.1);Key(Godot.Key.T);Check(ProgressService.DebugUnlockAllStages,"T accepted on Title");
            using(var file=FileAccess.Open("res://artifacts/gameover-bubble-fix/reload-save-path.txt",FileAccess.ModeFlags.Write))file.StoreString(router.SavePath);
            router.QueueFree();await Wait(.1);GD.Print("PROGRESS CONTROLS VERIFIED "+_checks);GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
