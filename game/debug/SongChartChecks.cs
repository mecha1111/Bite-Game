using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Gameplay;
using Gamejam2.Lobby;
using Gamejam2.Rhythm;
using Gamejam2.Settings;
using Gamejam2.Data;
namespace Gamejam2.Debugging;
/// <summary>Developer-only seek fixtures. Production clock architecture remains untouched.</summary>
public partial class SongChartChecks : Node
{
    private int _checks;
    private void Check(bool ok,string name){if(!ok)throw new Exception(name);GD.Print("PASS "+name);_checks++;}
    private async Task Capture(string name)
    {
        RenderingServer.ForceDraw();await Task.CompletedTask;
        GetViewport().GetTexture().GetImage().SavePng("/private/tmp/bite-song-"+name+".png");
    }
    private async Task Wait(double sec){await ToSignal(GetTree().CreateTimer(sec),SceneTreeTimer.SignalName.Timeout);}
    private void Seek(GameplayScreen game,double time)
    {
        var r=game.Rhythm;var f=BindingFlags.NonPublic|BindingFlags.Instance;
        typeof(RhythmController).GetField("_clockBaseSeconds",f)!.SetValue(r,time);
        typeof(RhythmController).GetField("_startTimeUsec",f)!.SetValue(r,Time.GetTicksUsec());
        typeof(RhythmController).GetField("_audioStartDelaySeconds",f)!.SetValue(r,0d);
        game.Music.Seek((float)time);
    }
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Run()
    {
        try
        {
            Gamejam2.Save.SaveStore.Initialize("/private/tmp/bite-song-chart-settings.cfg");Gamejam2.Save.ProgressService.Initialize();SettingsService.Initialize();SettingsService.InputOffsetSeconds=0;SettingsService.VisualOffsetSeconds=0;SettingsService.SetResolution(2);
            
            for(int i=1;i<=4;i++)
            {
                string song=$"song_{i}";var data=GameplayData.Load(song);var stream=GD.Load<AudioStream>(data.AudioPath);
                GD.Print($"AUDIO {song} actual={stream.GetLength():F6}s bpm={data.Chart.Bpm} phase={data.BeatOffsetSeconds:F6}");
                Check(data.Phrases.Count==LocalCsv.Read(data.ChartPath).Count,song+" exact authored prey count");
                Check(data.Phrases.All(p=>Math.Abs(p.TargetSeconds-p.StartSeconds-240/data.Chart.Bpm)<1e-9),song+" beat5 derived from start+4beats");
                Check(data.Phrases.Skip(1).Select((p,j)=>p.StartSeconds>data.Phrases[j].TargetSeconds+.25).All(x=>x),song+" no prey overlap including late capture");
                Check(stream.GetLength()>data.Phrases[^1].TargetSeconds+.25,song+" targets fit actual audio");
                var game=GD.Load<PackedScene>("res://game/gameplay/Gameplay.tscn").Instantiate<GameplayScreen>();game.SongId=song;AddChild(game);await Wait(.1);
                Check(game.RunState==GameplayScreen.GameplayRunState.Playing,song+" current selected MP3 starts");
                Check(game.Rhythm.AppliedSongEndTimeSeconds>=stream.GetLength(),song+" authoritative end covers audio");
                var first=data.Phrases[0];Seek(game,first.StartSeconds-.2);await Wait(3.8);
                Check(!game.Result.Visible,song+" first encounter never opens result");
                game.Signals.LeftLane.Activate(4,WaveKind.Medium,1,game.Rhythm.SongTimeSeconds-.2);
                await Wait(.02);await Capture(song);
                game.SuspendForSettings();double frozen=game.Rhythm.SongTimeSeconds;
                await Wait(.1);Check(game.Rhythm.SongTimeSeconds==frozen,song+" audio/world clock pause together");
                game.WorldWaterTreatment.Visible=false;await Wait(.03);
                var untreated=GetViewport().GetTexture().GetImage();
                game.WorldWaterTreatment.Visible=true;await Wait(.03);var treated=GetViewport().GetTexture().GetImage();
                Check(untreated.GetPixel(800,137).IsEqualApprox(treated.GetPixel(800,137)),song+" HUD unchanged by refraction");
                Check(!untreated.GetPixel(960,850).IsEqualApprox(treated.GetPixel(960,850)),song+" shark receives shared refraction/grade");
                game.BeginResumeCountdown();await Wait(3.4);
                Check(!game.InputBlocked&&game.Rhythm.IsRunning,song+" countdown resumes real audio clock");
                foreach(var entry in LocalCsv.Read("res://data/balance/interference.csv").Where(row=>row["song_id"]==song))
                {
                    string kind=entry["type"];double t=LocalCsv.Number(entry["time_seconds"]);
                    game.Interference.Initialize(song);game.Interference.Present(t);
                    var effect=game.Interference.GetEffect(kind);
                    Check(effect.EmissionCount>=1,song+" scheduled interference starts "+kind+" at "+t);
                    if(kind=="ship_horn")
                    {game.Interference.Present(t+1.26);Check(effect.EmissionCount==3,song+" independent horn warning+burst begins");
                     Check(effect.ActivePulses.Select(w=>w.Origin).Distinct().Count()>=2,song+" horn overlaps fixed independent origins");
                     game.Interference.Present(t+2.26);Check(effect.EmissionCount==7,song+" horn warning+six emitted");}
                }
                game.Interference.Initialize(song);
                // Complete remaining authored encounter timings via developer seeks, then actual run deadline.
                foreach(var phrase in data.Phrases.Skip(1))
                {Seek(game,phrase.TargetSeconds);await Wait(.01);game._Input(new InputEventAction{Action="rhythm_input",Pressed=true});await Wait(.01);Check(!game.Result.Visible,song+" section/encounter remains gameplay #"+phrase.Index);}
                Seek(game,game.Rhythm.AppliedSongEndTimeSeconds+.05);await Wait(.1);
                Check(game.Result.Visible&&game.ResultPresentationCount==1,song+" only song end opens one result");
                game.QueueFree();await Wait(.05);
            }
            bool rejected=false;try{GameplayData.ValidateEncounterGap("fixture",0,0,2,2.1,.25);}catch(ArgumentException e){rejected=e.Message.Contains("fixture")&&e.Message.Contains("overlap");}
            Check(rejected,"invalid overlapping chart rejected with event context");
            var lobby=GD.Load<PackedScene>("res://game/lobby/Lobby.tscn").Instantiate<LobbyScreen>();AddChild(lobby);await Wait(.85);
            await Capture("lobby");
            var preview=lobby.GetNode<SongPreview>("SongPreview");var a=preview.PlayerA;var b=preview.PlayerB;
            Check(preview.SongId=="song_1"&&a.Playing,"Lobby opens selected preview");
            Check(a.Bus=="Music"&&a.VolumeDb< -12,"quiet preview uses Music bus");
            lobby.Navigate(1);await Wait(.1);Check(a.Playing&&b.Playing,"crossfade has two independent decks");
            await Wait(.75);Check(preview.SongId=="song_2"&&!a.Playing&&b.Playing,"crossfade settles without outgoing playback");
            lobby.Navigate(1);lobby.Navigate(1);await Wait(1.7);Check(preview.SongId=="song_4","rapid selection coalesces without hard audio cut");
            bool selected=false;preview.FadeOut(()=>selected=true);await Wait(.25);Check(selected&&!a.Playing&&!b.Playing,"preview stops before gameplay activation");
            lobby.QueueFree();await Wait(.05);
            GD.Print($"SONG CHART VERIFIED: {_checks} checks");GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
