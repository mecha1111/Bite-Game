using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Data;
using Gamejam2.Gameplay;
using Gamejam2.Lobby;
using Gamejam2.Startup;
using Gamejam2.CustomGenerator;
namespace Gamejam2.Debugging;
/// <summary>Short Windows check. Isolates saves and preserves the player's map library.</summary>
public partial class CustomMapLaunchCheck : Node
{
    public SceneRouter? Router {get;set;}
    private void Check(bool value,string name){if(!value)throw new Exception(name);GD.Print("PASS "+name);}
    private async Task Frames(){GetWindow().GrabFocus();for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Run()
    {
        try
        {
            var router=Router;
            if(router==null){router=GD.Load<PackedScene>("res://game/startup/Startup.tscn").Instantiate<SceneRouter>();router.SavePath="user://logs/custom-cleanup-"+OS.GetProcessId()+".cfg";AddChild(router);}
            Check(CustomMapRegistry.Maps.All(m=>m.Generated),"registry contains only player-created maps");
            // Test an empty library in memory, without moving or deleting any user files.
            var field=typeof(CustomMapRegistry).GetField("_maps",BindingFlags.Static|BindingFlags.NonPublic)!;
            var saved=CustomMapRegistry.Maps.ToArray();field.SetValue(null,Array.Empty<CustomMapDefinition>());
            try
            {
                router.GoToLobby();await Frames();var lobby=router.ScreenHost!.GetChild<LobbyScreen>(0);lobby.SelectCategory(true);await Frames();
                Check(lobby.CustomCategory&&lobby.Catalog.Stages.Count==0&&lobby.GetNode<Button>("Categories/Custom").Visible,"Custom category remains available with no cards");
                Check(lobby.Information.Text=="아직 만든 커스텀 맵이 없습니다.","clean empty state");
                var add=lobby.GetNode<HBoxContainer>("LocalMusicActions").GetChild<Button>(0);Check(add.Visible&&!add.Disabled,"add music is available");add.EmitSignal(Button.SignalName.Pressed);
                var panel=lobby.GetChildren().OfType<GeneratorPanel>().Single();Check(panel.IsOpen(),"add music opens generator rights modal");panel.GetNode<Control>("LocalGenerator").Hide();
            }
            finally{field.SetValue(null,saved);}
            var map=await LocalMapGenerator.Generate(this,GeneratedMapTestFixture.Source,text=>GD.Print(text));
            Check(map.Generated&&map.Available&&map.Title.Length>0&&CustomMapRegistry.Find(map.SongId)!=null,"MP3 analysis registers generated metadata");
            foreach(string path in new[]{map.AudioPath,map.ChartPath,map.FxPath,map.InterferencePath,map.SatietySectionsPath})Check(FileAccess.FileExists(path),"generated resource exists "+path.GetFile());
            router.GoToLobby();await Frames();var populated=router.ScreenHost!.GetChild<LobbyScreen>(0);populated.SelectCategory(true);await Frames();
            Check(populated.Catalog.Stages.All(s=>s.StageId.StartsWith("custom_local_"))&&populated.Catalog.Stages.Any(s=>s.StageId==map.StageId),"generated card appears; no bundled cards");
            router.GoToGameplay(map.StageId);await Frames();var game=router.ScreenHost.GetChild<GameplayScreen>(0);
            Check(game.SongId==map.SongId&&game.RunState==GameplayScreen.GameplayRunState.Playing&&game.Data.TargetCount>0&&game.Music.Playing,"generated MP3 gameplay launches");
            Check(game.Environment.Background.Texture!=null&&game.Environment.ActiveEnvironmentSongId==map.EnvironmentProfile&&game.Presentation.Events.SequenceEqual(StagePresentationChart.Load(map.SongId,map.Duration)),"generated environment and camera load");game.StopGameplay();
            router.GoToLobby();await Frames();router.ScreenHost.GetChild<LobbyScreen>(0).SelectCategory(false);router.GoToGameplay("stage_1");await Frames();game=router.ScreenHost.GetChild<GameplayScreen>(0);
            Check(game.SongId=="song_1"&&game.Data.TargetCount==64&&game.RunState==GameplayScreen.GameplayRunState.Playing&&game.Music.Playing,"Main Stage 1 launches unchanged");game.StopGameplay();
            foreach(string path in new[]{"res://custom_maps/audio","res://custom_maps/assets","res://custom_maps/charts","res://custom_maps/camera_fx","res://custom_maps/data"})Check(!DirAccess.DirExistsAbsolute(path),"retired resource directory absent "+path);
            GD.Print("CUSTOM MAP CLEANUP VERIFIED");router.QueueFree();await Frames();GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
}
