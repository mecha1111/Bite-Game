using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Audio;
using Gamejam2.Gameplay;
using Gamejam2.Lobby;
using Gamejam2.Rhythm;
using Gamejam2.Save;
using Gamejam2.Settings;
using Gamejam2.Tutorial;
namespace Gamejam2.Debugging;
/// <summary>Developer-only time injection, current audio/chart sources and real input/results.</summary>
public partial class StageCompletionChecks : Node
{
    private int _checks;
    private void Check(bool ok,string label){if(!ok)throw new Exception(label);GD.Print("PASS "+label);_checks++;}
    private async Task Wait(double seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private static void Seek(RhythmController rhythm,double time)
    {
        var f=BindingFlags.Instance|BindingFlags.NonPublic;
        typeof(RhythmController).GetField("_clockBaseSeconds",f)!.SetValue(rhythm,time);
        typeof(RhythmController).GetField("_startTimeUsec",f)!.SetValue(rhythm,Time.GetTicksUsec());
        typeof(RhythmController).GetField("_audioStartDelaySeconds",f)!.SetValue(rhythm,0d);
    }
    private static int[] Mixed(int n)
    {
        double[] weights={.2,.6,.15,.05};int[] counts=weights.Select(w=>(int)(n*w)).ToArray();
        foreach(int k in Enumerable.Range(0,4).OrderByDescending(k=>n*weights[k]-counts[k]).Take(n-counts.Sum()))counts[k]++;
        int[] used=new int[4],result=new int[n];
        for(int i=0;i<n;i++){int k=Enumerable.Range(0,4).Where(k=>used[k]<counts[k]).OrderByDescending(k=>(i+1)*counts[k]/(double)n-used[k]).First();result[i]=k;used[k]++;}
        return result;
    }
    private static SatietyState Simulate(GameplayData data,int[] grades)
    {
        var state=new SatietyState(data);
        foreach(var p in data.Phrases){state.Advance(p.TargetSeconds);if(state.Value<=0)break;state.Resolve(new(new[]{RhythmJudgment.Perfect,RhythmJudgment.Good,RhythmJudgment.Bad,RhythmJudgment.Miss}[grades[p.Index]],0,new()));}
        state.Advance(data.DurationSeconds);return state;
    }
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Run()
    {
        try
        {
            GetWindow().GrabFocus();string save="/private/tmp/bite-full-song-"+Time.GetTicksUsec()+".cfg";
            SaveStore.Initialize(save);ProgressService.Initialize();SettingsService.Initialize();
            Check(SettingsService.MasterVolume==.5&&SettingsService.MusicVolume==.5&&SettingsService.EffectsVolume==1,"fresh profile audio 50/50/100");
            foreach(var item in new[]{("Master",.5),("Music",.5),("SFX",1d)})
                Check(Math.Abs(Mathf.DbToLinear(AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex(item.Item1)))-item.Item2)<1e-5,item.Item1+" actual audio bus matches defaults");
            var settings=GD.Load<PackedScene>("res://game/settings/SettingsPopup.tscn").Instantiate<SettingsPopup>();AddChild(settings);settings.Open();
            Check(settings.MasterSlider.Value==50&&settings.MusicSlider.Value==50&&settings.EffectsSlider.Value==100,"default sliders match bus levels");
            Check(settings.MasterAmount.Text=="50%"&&settings.MusicAmount.Text=="50%"&&settings.EffectsAmount.Text=="100%","default slider labels match values");
            settings.QueueFree();await Wait(.05);
            SettingsService.SetMasterVolume(.27);SettingsService.SetMusicVolume(.31);SettingsService.SetEffectsVolume(.62);
            SaveStore.Initialize(save);SettingsService.Initialize();
            Check(SettingsService.MasterVolume==.27&&SettingsService.MusicVolume==.31&&SettingsService.EffectsVolume==.62,"saved user preferences survive reinitialization");
            SaveStore.Initialize(save+"-fresh");ProgressService.Initialize();SettingsService.Initialize();SettingsService.InputOffsetSeconds=SettingsService.VisualOffsetSeconds=0;
            Check(SettingsService.MusicVolume==.5,"second new profile does not inherit previous session volume");
            Check(GetNode<UiAudio>("/root/UiAudio").Player.Bus=="SFX","UI click uses SFX bus");
            var lobby=GD.Load<PackedScene>("res://game/lobby/Lobby.tscn").Instantiate<LobbyScreen>();AddChild(lobby);await Wait(.05);
            var preview=lobby.GetNode<SongPreview>("SongPreview");Check(preview.PlayerA.Bus=="Music"&&preview.PlayerB.Bus=="Music"&&SettingsService.MusicVolume==.5,"preview decks use default Music bus");preview.PlayerA.Stop();preview.PlayerB.Stop();lobby.QueueFree();await Wait(.05);
            foreach(string song in new[]{"song_1","song_2","song_3","song_4"})
            {
                var data=GameplayData.Load(song);using var source=SongAudio.Load(data.AudioPath);data.UseAudioDuration(source.GetLength());
                Check(Math.Abs(data.DurationSeconds-source.GetLength())<1e-9,song+" real MP3 length is data deadline");
                Check(data.BaseRecovery==150&&Math.Abs(data.BaseRecovery*data.Multipliers[RhythmJudgment.Good]-80)<1e-8&&Math.Abs(data.BaseRecovery*data.Multipliers[RhythmJudgment.Bad]-40)<1e-8,song+" fish rewards 150/80/40");
                var ap=Simulate(data,new int[data.Phrases.Count]);var mixed=Simulate(data,Mixed(data.Phrases.Count));
                Check(ap.Value>=800&&ap.DepletedAtSongSeconds==null,song+" timeline all PERFECT clears without starvation");
                Check(mixed.Value>=800&&mixed.DepletedAtSongSeconds==null,song+" GOOD-heavy timeline clears");
                GD.Print($"ECONOMY {song} seconds={data.DurationSeconds:F9} prey={data.Phrases.Count} drain/s={data.DrainPerSecond:F6} AP={ap.Value:F6} mixed={mixed.Value:F6}");
                var game=GD.Load<PackedScene>("res://game/gameplay/Gameplay.tscn").Instantiate<GameplayScreen>();game.SongId=song;AddChild(game);game.SetProcess(false);game.Rhythm.SetProcess(false);
                Check(game.Music.Bus=="Music"&&game.AudioCues.CuePlayer.Bus=="SFX"&&game.AudioCues.FeedbackVoices.GetChildren().OfType<AudioStreamPlayer>().All(p=>p.Bus=="SFX"),song+" music / wave-interference / catch-whiff bus mapping");
                Check(game.Rhythm.AppliedSongEndTimeSeconds==game.Music.Stream.GetLength(),song+" clock deadline exactly equals audio, not chart/capture exhaustion");
                // Artificially generous local chart deadline cannot hold an audio-backed stage open.
                foreach(var p in game.Data.Phrases)
                {
                    Seek(game.Rhythm,p.TargetSeconds);game._Process(0);game._Input(new InputEventAction{Action="rhythm_input",Pressed=true});
                    Check(game.RunState==GameplayScreen.GameplayRunState.Playing&&!game.Result.Visible,song+" encounter never ends stage #"+p.Index);
                }
                Check(game.Satiety.CreateResult().Perfect==data.Phrases.Count,song+" every authored target committed once through real Bite");
                double end=game.Music.Stream.GetLength();Seek(game.Rhythm,end-.015);game.Rhythm._Process(0);game._Process(0);
                Check(!game.Result.Visible&&game.Music.Playing,song+" final empty chart tail retains music until EOF");
                Seek(game.Rhythm,end+.01);game.Rhythm._Process(0);
                Check(game.Result.Visible&&game.ResultPresentationCount==1&&game.RunState==GameplayScreen.GameplayRunState.Result,song+" actual EOF opens Result once");
                Check(game.Rhythm.SongTimeSeconds==end&&game.InputBlocked&&!game.Music.Playing,song+" EOF freezes exact full-song clock and input");
                game.Rhythm._Process(0);game._Input(new InputEventAction{Action="rhythm_input",Pressed=true});
                Check(game.ResultPresentationCount==1,song+" repeated end/input never duplicates Result");
                game.QueueFree();await Wait(.03);
            }
            // A valid final target within late capture is finalized at EOF, not hundreds of ms later.
            var chart=new RhythmChart{SongEndTimeSeconds=5};chart.Events.Add(new(){TimeSeconds=.59,EventType=RhythmEventType.InputTarget,IsHittable=true});
            var music=new AudioStreamPlayer{Stream=MetronomeTrack.CreateTrack(Array.Empty<double>(),.6),Bus="Music"};AddChild(music);
            var rhythm=new RhythmController{MusicPlayer=music,Chart=chart,EnableDebugLogging=false};AddChild(rhythm);rhythm.SetProcess(false);
            int finishes=0,misses=0;rhythm.SongFinished+=()=>finishes++;rhythm.JudgmentResolved+=r=>{if(r.Judgment==RhythmJudgment.Miss)misses++;};
            Check(rhythm.StartSong()&&Math.Abs(rhythm.AppliedSongEndTimeSeconds-.6)<1e-5,"short audio overrides stale longer chart end");
            Seek(rhythm,.585);rhythm._Process(0);Check(finishes==0&&misses==0,"final target is not prematurely missed before EOF");
            Seek(rhythm,.61);rhythm._Process(0);Check(finishes==1&&misses==1,"EOF resolves pending final target before one SongFinished");
            rhythm._Process(0);Check(finishes==1&&!rhythm.TryJudgeInput(),"finished clock rejects late input and duplicate completion");
            rhythm.QueueFree();music.QueueFree();await Wait(.03);
            foreach(bool skipFrame in new[]{false,true})
            {
                var game=GD.Load<PackedScene>("res://game/gameplay/Gameplay.tscn").Instantiate<GameplayScreen>();AddChild(game);game.SetProcess(false);game.Rhythm.SetProcess(false);
                typeof(SatietyState).GetProperty("Value")!.SetValue(game.Satiety,1d);
                Seek(game.Rhythm,skipFrame?game.Data.DurationSeconds+.01:1);
                if(skipFrame)game.Rhythm._Process(0);else game._Process(0);
                Check(game.RunState==GameplayScreen.GameplayRunState.GameOver&&game.GameOver.Visible&&!game.Result.Visible&&game.ResultPresentationCount==0,"starvation before EOF wins even when render frame skips EOF: "+skipFrame);
                game.QueueFree();await Wait(.03);
            }
            var tutorial=GD.Load<PackedScene>("res://game/tutorial/TutorialGameplay.tscn").Instantiate<TutorialGameplayScreen>();AddChild(tutorial);
            Check(tutorial.RunState==GameplayScreen.GameplayRunState.Playing&&tutorial.Music.Bus=="Music"&&tutorial.AttackSound.Bus=="SFX","tutorial starts with correct music and attack bus mapping");
            tutorial.StopGameplay();tutorial.QueueFree();await Wait(.05);
            GD.Print($"STAGE COMPLETION VERIFIED {_checks}");GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
