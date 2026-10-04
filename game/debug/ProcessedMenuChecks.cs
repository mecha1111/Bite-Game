using System;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Audio;
using Gamejam2.Lobby;
using Gamejam2.Settings;
namespace Gamejam2.Debugging;
public partial class ProcessedMenuChecks : Node
{
    private int _checks;
    private void Check(bool ok,string label){if(!ok)throw new Exception(label);GD.Print("PASS "+label);_checks++;}
    private async Task Wait(double seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Run()
    {
        try
        {
            Gamejam2.Save.SaveStore.Initialize("/private/tmp/bite-processed-menu.cfg");Gamejam2.Save.ProgressService.Initialize();SettingsService.Initialize();GetWindow().GrabFocus();
            var lobby=GD.Load<PackedScene>("res://game/lobby/Lobby.tscn").Instantiate<LobbyScreen>();AddChild(lobby);await Wait(.85);
            var preview=lobby.GetNode<SongPreview>("SongPreview");var ui=GetNode<UiAudio>("/root/UiAudio");
            Check(lobby.Catalog.Stages.Count==4&&preview.SongId=="song_1"&&preview.PlayerA.Stream is AudioStreamMP3,"four cards and current Song 1 preview");
            int clicks=ui.ClickCount;lobby._Input(new InputEventAction{Action="stage_previous",Pressed=true});
            Check(lobby.SelectedIndex==0&&ui.ClickCount==clicks,"blocked first-card left creates no sound/action");
            lobby._Input(new InputEventAction{Action="stage_next",Pressed=true});await Wait(.1);
            Check(lobby.SelectedIndex==1&&ui.ClickCount==clicks+1&&preview.PlayerA.Playing&&preview.PlayerB.Playing,"keyboard Right emits one quiet click and crossfades two decks");
            await Wait(.75);Check(preview.SongId=="song_2"&&!preview.PlayerA.Playing&&preview.PlayerB.Stream is AudioStreamMP3&&Math.Abs(preview.PlayerB.VolumeDb+15)<.01,"current Song 2 preview settles at -15dB");
            lobby.Navigate(1);lobby.Navigate(1);await Wait(1.7);
            Check(preview.SongId=="song_4"&&preview.PlayerB.Stream.ResourcePath.EndsWith("4.mp3"),"EX previews its own current source");
            clicks=ui.ClickCount;lobby._Input(new InputEventAction{Action="stage_next",Pressed=true});
            Check(lobby.SelectedIndex==3&&ui.ClickCount==clicks,"fourth-card right cannot wrap or play invalid click");
            SettingsService.SetMusicVolume(0);Check(AudioServer.IsBusMute(AudioServer.GetBusIndex("Music"))&&!AudioServer.IsBusMute(AudioServer.GetBusIndex("SFX")),"Music setting controls processed preview independently");SettingsService.SetMusicVolume(1);
            bool back=false;lobby.TitleRequested+=()=>back=true;lobby._Input(new InputEventAction{Action="ui_cancel",Pressed=true});await Wait(.25);
            Check(back&&ui.ClickCount==clicks+1&&!preview.PlayerA.Playing&&!preview.PlayerB.Playing,"ESC plays click, fades preview, returns Title");
            GD.Print($"PROCESSED MENU VERIFIED: {_checks} checks");GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
