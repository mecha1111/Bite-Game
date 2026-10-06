using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Lobby;
using Gamejam2.Data;
using Gamejam2.Startup;
using Gamejam2.Gameplay;
using Gamejam2.Save;
using Gamejam2.Settings;
using Gamejam2.Rhythm;
namespace Gamejam2.Debugging;
public partial class ExStageChecks : Node
{
    private int _checks;
    private void Check(bool ok,string name){if(!ok)throw new Exception(name);GD.Print("PASS "+name);_checks++;}
    private async Task Wait(double sec)
    {
        // GUI fixtures must own focus: production correctly freezes Song Clock when unfocused.
        GetWindow().GrabFocus();
        await ToSignal(GetTree().CreateTimer(sec),SceneTreeTimer.SignalName.Timeout);
    }
    private void Capture(string name){RenderingServer.ForceDraw();GetViewport().GetTexture().GetImage().SavePng("/private/tmp/bite-ex-"+name+".png");}
    private void Key(LobbyScreen lobby,string action)=>lobby._Input(new InputEventAction{Action=action,Pressed=true});
    private void Seek(GameplayScreen game,double time)
    {
        var f=BindingFlags.NonPublic|BindingFlags.Instance;var type=typeof(RhythmController);
        type.GetField("_clockBaseSeconds",f)!.SetValue(game.Rhythm,time);
        // Keep the fixture's paused snapshot coherent if a GUI focus notification arrives after Seek.
        type.GetField("_stoppedTimeSeconds",f)!.SetValue(game.Rhythm,time);
        type.GetField("_startTimeUsec",f)!.SetValue(game.Rhythm,Time.GetTicksUsec());
        type.GetField("_audioStartDelaySeconds",f)!.SetValue(game.Rhythm,0d);
        game.Music.Seek((float)time);
    }
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Run()
    {
        try
        {
            var authored=GD.Load<PackedScene>("res://game/lobby/Lobby.tscn").Instantiate<LobbyScreen>();
            Check(authored.GetNode<Control>("Composition/Cards").GetChildCount()==4,"exactly four cards authored before running");authored.Free();
            var router=GD.Load<PackedScene>("res://game/startup/Startup.tscn").Instantiate<SceneRouter>();router.SavePath="/private/tmp/bite-ex-stage-settings.cfg";AddChild(router);
            SettingsService.SetResolution(2);router.GoToLobby();await Wait(.9);
            var lobby=router.ScreenHost!.GetChild<LobbyScreen>(0);var cards=lobby.Cards.GetChildren().OfType<StageCard>().ToArray();
            Check(cards.Length==4&&lobby.Catalog.Stages.Count==4,"exactly four runtime entries");
            Check(cards.Select(c=>c.Data!.SongId).SequenceEqual(new[]{"song_1","song_2","song_3","song_4"}),"requested song order");
            Key(lobby,"stage_previous");Check(lobby.SelectedIndex==0&&lobby.LeftArrow.Disabled,"first stage cannot wrap left");
            // A side card navigates first; keyboard next then reaches fourth entry.
            cards[1].SelectButton.EmitSignal(Button.SignalName.Pressed);await Wait(.4);Check(lobby.SelectedIndex==1,"mouse side-card navigation");
            lobby.TitleButton.GrabFocus();Key(lobby,"stage_next");await Wait(.4);Check(!lobby.TitleButton.HasFocus(),"song navigation transfers focus away from Back");
            Key(lobby,"stage_next");await Wait(.85);Check(lobby.SelectedIndex==3&&lobby.RightArrow.Disabled,"Right/D reaches EX and blocks right edge");
            Key(lobby,"stage_next");Check(lobby.SelectedIndex==3,"EX cannot wrap right");
            var ex=cards[3];Check(ex.Data!.IsEx&&ex.Data.BackgroundTexture==null,"EX data uses composite without normal texture");
            Check(ex.CompositeCover.Visible&&!ex.Background.Visible&&ex.ExBadge.Text=="EX"&&ex.NameLabel.Text=="Deep Current","data-driven EX badge and separate title");
            var crops=ex.CompositeCover.GetChildren().OfType<Control>().Where(c=>c.ClipContents).ToArray();
            Check(crops.Length==3,"three scene-authored crop regions");
            Check(crops.All(c=>Math.Abs(c.Size.X-ex.Size.X/3)<.01),"exact vertical thirds");
            var textures=crops.Select(c=>c.GetChild<TextureRect>(0)).ToArray();
            Check(textures.All(t=>t.StretchMode==TextureRect.StretchModeEnum.KeepAspectCovered),"aspect-preserving cover crop for all three sources");
            Check(textures.Select(t=>t.Texture.ResourcePath).Distinct().Count()==3,"three distinct original environment resources");
            var preview=lobby.GetNode<SongPreview>("SongPreview");
            Check(preview.SongId=="song_4"&&new[]{preview.PlayerA,preview.PlayerB}.Any(p=>p.Playing&&p.Stream.ResourcePath=="res://assets/music/4.mp3"),"EX previews own current MP3 through shared crossfade");
            Capture("locked-cover");
            ProgressService.UnlockStage("stage_4");await Wait(.1);Check(!ex.SelectButton.Disabled&&!ex.LockOverlay.Visible,"configurable EX unlock enables selection");
            foreach(var size in new[]{new Vector2I(1920,1080),new Vector2I(1280,720),new Vector2I(1440,900)})
            {GetWindow().Size=size;await Wait(.15);Capture("cover-"+size.X+"x"+size.Y);Check(crops.All(c=>Math.Abs(c.Size.X-ex.Size.X/3)<.01),"thirds stay coherent at "+size);}
            Key(lobby,"stage_previous");await Wait(.4);Check(lobby.SelectedIndex==2,"Left/A navigates back from EX");
            lobby.Navigate(1);await Wait(.85);bool silentAtSelection=false;lobby.StageSelected+=_=>silentAtSelection=!preview.PlayerA.Playing&&!preview.PlayerB.Playing;Key(lobby,"stage_select");await Wait(.3);
            Check(router.ScreenHost.GetChild(0) is GameplayScreen,"Enter/Space activation launches gameplay");
            var game=router.ScreenHost.GetChild<GameplayScreen>(0);Check(game.SongId=="song_4"&&game.Data.SongName=="Deep Current","EX routes to its own song");
            Check(silentAtSelection,"menu preview stopped before gameplay starts");
            Check(game.Environment.ActiveEnvironmentSongId=="song_1","EX starts with full shallow environment, not composite");
            Check(game.FindChildren("ExCover","",true,false).Count==0,"EX gameplay contains no split cover");
            // Visual seek fixture skips food; replenish only the fixture so authored full-song drain cannot starve it.
            foreach(var entry in LocalCsv.Read("res://data/balance/environment_sequence.csv").Where(r=>r["song_id"]=="song_4"&&LocalCsv.Number(r["start_time_sec"])>0).Select(r=>(LocalCsv.Number(r["start_time_sec"]),r["environment_song_id"])))
            {typeof(SatietyState).GetProperty("Value")!.SetValue(game.Satiety,1100d);Seek(game,entry.Item1+.03);await Wait(.08);Check(game.Environment.ActiveEnvironmentSongId==entry.Item2,"Song Clock drives non-sequential section "+entry.Item1+" actual="+game.Environment.ActiveEnvironmentSongId+" clock="+game.Rhythm.SongTimeSeconds+" focusPaused="+game.Rhythm.IsFocusPaused+" manuallyPaused="+game.Rhythm.IsManuallyPaused);Capture("world-"+entry.Item1);}
            game.SuspendForSettings();var frozen=game.Environment.ActiveEnvironmentSongId;await Wait(.1);Check(game.Environment.ActiveEnvironmentSongId==frozen,"paused clock freezes EX section state");
            game.StopGameplay();router.GoToLobby();await Wait(.9);lobby=router.ScreenHost.GetChild<LobbyScreen>(0);lobby.Navigate(3);await Wait(.9);
            ex=lobby.Cards.GetChildren().OfType<StageCard>().Last();ex.SelectButton.EmitSignal(Button.SignalName.Pressed);await Wait(.3);
            Check(router.ScreenHost.GetChild<GameplayScreen>(0).SongId=="song_4","selected EX mouse click uses same activation path");
            router.QueueFree();await Wait(.2);GC.Collect();GC.WaitForPendingFinalizers();await Wait(.1);
            GD.Print($"EX STAGE VERIFIED: {_checks} checks");GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
