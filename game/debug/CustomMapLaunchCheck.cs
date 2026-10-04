using System;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Data;
using Gamejam2.Gameplay;
using Gamejam2.Lobby;
using Gamejam2.Startup;
namespace Gamejam2.Debugging;
public partial class CustomMapLaunchCheck : Node
{
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Run()
    {
        try
        {
            foreach(var map in CustomMapRegistry.Maps)
                if(!map.Available||!FileAccess.FileExists(map.ChartPath)||!FileAccess.FileExists(map.FxPath))throw new Exception("Missing map data: "+map.SongId);
            var router=GD.Load<PackedScene>("res://game/startup/Startup.tscn").Instantiate<SceneRouter>();
            router.SavePath="/private/tmp/bite-custom-launch-check.cfg";AddChild(router);
            router.GoToLobby();((LobbyScreen)router.ScreenHost!.GetChild(0)).SelectCategory(true);
            router.GoToGameplay("custom_shark_pool");
            var game=router.ScreenHost.GetChild<GameplayScreen>(router.ScreenHost.GetChildCount()-1);
            if(game.RunState!=GameplayScreen.GameplayRunState.Playing||game.Data.Phrases.Count!=131)throw new Exception("Custom map did not start.");
            double deadline=Time.GetTicksMsec()+12000;
            while(game.ActivePreyIndex<0&&Time.GetTicksMsec()<deadline)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            if(game.ActivePreyIndex!=0||!game.Music.Playing)throw new Exception("First prey/audio did not begin.");
            GD.Print("PASS custom_shark_pool launched from Custom Maps; v3 131-prey chart loaded; first prey began; three registry chart/FX paths exist.");
            game.StopGameplay();GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
}
