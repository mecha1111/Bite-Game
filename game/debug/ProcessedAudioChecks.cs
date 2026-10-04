using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Audio;
using Gamejam2.Data;
using Gamejam2.Gameplay;
using Gamejam2.Rhythm;
using Gamejam2.Settings;
namespace Gamejam2.Debugging;
/// <summary>Developer-only data/mix/result assertions and full unskipped Songs 2–4. No runtime timing edits.</summary>
public partial class ProcessedAudioChecks : Node
{
    private GameplayScreen? _game;
    private int _next,_checks;
    private double _maxDrift;
    private readonly InputEventAction _bite=new(){Action="rhythm_input",Pressed=true};
    private async Task Wait(double seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private void Check(bool pass,string label){if(!pass)throw new Exception(label);GD.Print("PASS "+label);_checks++;}
    public override void _Process(double delta)
    {
        if(_game==null)return;
        if(_game.Rhythm.IsFocusPaused)GetWindow().GrabFocus();
        double now=_game.Rhythm.SongTimeSeconds;
        if(_game.IsBiteInteractive&&_next<_game.Data.Phrases.Count&&now>=_game.Data.Phrases[_next].TargetSeconds)
        {_game._Input(_bite);GD.Print($"FULL {_game.SongId} target {_next}: {_game.Rhythm.LastResult?.Judgment} at {now:F6}");_next++;}
        if(_game.Rhythm.IsClockAdvancing&&now>1&&_game.Music.Playing)
        {
            double heard=_game.Music.GetPlaybackPosition()+AudioServer.GetTimeSinceLastMix()-AudioServer.GetOutputLatency();
            _maxDrift=Math.Max(_maxDrift,Math.Abs(now-heard));
        }
        if(_game.Result.Visible&&now<_game.Rhythm.AppliedSongEndTimeSeconds)throw new Exception("Premature result");
    }
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Run()
    {
        try
        {
            Gamejam2.Save.SaveStore.Initialize("/private/tmp/bite-processed-settings.cfg");Gamejam2.Save.ProgressService.Initialize();SettingsService.Initialize();
            SettingsService.InputOffsetSeconds=SettingsService.VisualOffsetSeconds=0;SettingsService.SetResolution(2);GetWindow().GrabFocus();
            
            for(int i=1;i<=4;i++)
            {
                var data=GameplayData.Load("song_"+i);var music=GD.Load<AudioStream>(data.AudioPath);
                Check(music is AudioStreamMP3&&data.Phrases.Count==LocalCsv.Read(data.ChartPath).Count,"current MP3 and authored CSV event count song "+i);
                Check(data.Phrases.All(p=>Math.Abs(p.TargetSeconds-p.StartSeconds-240/data.Chart.Bpm)<1e-9),"target-first four-beat relation song "+i);
                Check(data.Phrases.Skip(1).Select((p,j)=>p.StartSeconds>data.Phrases[j].TargetSeconds+.25).All(x=>x),"no prey overlap including late capture song "+i);
                Check(data.Phrases[^1].TargetSeconds+.25<music.GetLength(),"last target fits current audio song "+i);
            }
            var first=GameplayData.Load("song_1");
            var supplied=LocalCsv.Read(first.ChartPath);
            Check(supplied.Select(r=>LocalCsv.Number(r["target_time_sec"])).SequenceEqual(first.Phrases.Select(p=>p.TargetSeconds)),"Song 1 runtime target list matches actual authored CSV exactly");
            bool rejected=false;try{GameplayData.ValidateEncounterGap("fixture",0,0,2,2.1,.25);}catch(ArgumentException e){rejected=e.Message.Contains("overlap");}
            Check(rejected,"overlapping prey explicitly rejected");
            var floatGame=GD.Load<PackedScene>("res://game/gameplay/Gameplay.tscn").Instantiate<GameplayScreen>();floatGame.SongId="song_2";AddChild(floatGame);floatGame.SetProcessInput(false);floatGame.SuspendForSettings();floatGame.SetProcess(false);
            double floatStart=LocalCsv.Number(LocalCsv.Read("res://data/balance/interference.csv").Single(r=>r["song_id"]=="song_2"&&r["type"]=="fishing_float")["time_seconds"]);
            var floatSounds=floatGame.AudioCues.Events.Where(e=>e.Key=="fishing_float").ToArray();
            Check(floatSounds.Length==4&&floatSounds.Select((e,i)=>Math.Abs(e.Time-floatStart-i*3)<1e-8).All(x=>x),"actual Float audio emits four times at 0/3/6/9 from shared schedule");
            floatGame.Interference.Initialize("song_2");
            for(int i=0;i<4;i++)
            {
                floatGame.Interference.Present(floatStart+i*3);
                var effect=floatGame.Interference.GetEffect("fishing_float");
                Check(effect.EmissionCount==i+1&&effect.ActivePulses.Count>0&&effect.ActivePulses.All(w=>w.WaveType==WaveKind.Large),"actual Float visual emission "+(i+1)+" is an independent Large wave");
            }
            floatGame.StopGameplay();floatGame.QueueFree();await Wait(.05);
            var clips=SfxCatalog.Load();
            foreach(var item in new[]{("small",-7f),("medium",-7f),("large",-7f),("bite_miss",-4f),("ship_horn",-1f),("whale",-.5f),("fishing_float",-3f),("sardine",-3.5f),("ui_click",-10f),("result_jingle",-6f),("result_gauge_fill",-7f)})
                Check(Math.Abs(clips[item.Item1].VolumeDb-item.Item2)<.001,"processed recommendation gain "+item.Item1);
            Check(clips["small"].Pitch==1.08f&&clips["medium"].Pitch==1&&clips["large"].Pitch==.92f,"mild 1.08/1/.92 wave pitch family");
            Check(clips["small"].Stereo==.3f&&clips["large"].Stereo==.3f,"30% wave stereo bias");
            Check(clips["whale"].Duration==2&&clips["sardine"].Duration==3&&clips["sardine"].Fade(2.99)<.1,"bounded Whale/Sardine textures fade at event end");
            Check(Enum.GetValues<RhythmJudgment>().Length==4&&JudgmentPresentation.Load().Count==4,"four runtime judgment grades and effects");
            var result=GD.Load<PackedScene>("res://game/gameplay/ResultScreen.tscn").Instantiate<ResultScreen>();AddChild(result);
            result.ShowResult(new("Fixture",880,800,20,7,6,0,21,33));await Wait(.05);
            Check(result.Audio.Entrance.Playing&&result.Audio.Entrance.Bus=="SFX"&&result.Audio.Entrance.VolumeDb==-6,"result entrance processed jingle on SFX");
            await Wait(.9);Check(result.Audio.Fill.Playing&&result.Audio.Fills==1,"result gauge processed fill starts with actual reveal");
            await Wait(1.2);Check(!result.Audio.Fill.Playing&&result.IsRevealed&&result.Gauge.Percentage.Text=="88%","88% result fill stops at animation end");
            var ui=GetNode<UiAudio>("/root/UiAudio");int clicks=ui.ClickCount;result.RetryButton.EmitSignal(BaseButton.SignalName.Pressed);
            Check(ui.ClickCount==clicks+1&&ui.Player.VolumeDb==-10&&ui.Player.Bus=="SFX","real result button quiet processed UI click");
            result.HideResult();Check(!result.Audio.Entrance.Playing&&!result.Audio.Fill.Playing,"result hide stops its sounds");result.QueueFree();await Wait(.05);
            foreach(string id in OS.GetCmdlineUserArgs().Contains("--data-only")?Array.Empty<string>():new[]{"song_2","song_3","song_4"})
            {
                _next=0;_maxDrift=0;_game=GD.Load<PackedScene>("res://game/gameplay/Gameplay.tscn").Instantiate<GameplayScreen>();_game.SongId=id;AddChild(_game);_game.SetProcessInput(false);
                await Wait(.1);Check(_game.RunState==GameplayScreen.GameplayRunState.Playing,id+" starts synchronized processed music");
                var sequences=_game.Interference.AudioSequences().ToArray();
                foreach(var sequence in sequences)
                {
                    var plan=_game.AudioCues.Events.Where(e=>e.Prey<0&&e.Key==sequence.Type&&e.Time>=sequence.Time&&e.Time<sequence.Time+9.1).ToArray();
                    if(sequence.Type=="fishing_float")Check(plan.Any(e=>Math.Abs(e.Time-sequence.Time)<1e-8)&&plan.Any(e=>Math.Abs(e.Time-sequence.Time-3)<1e-8)&&plan.Any(e=>Math.Abs(e.Time-sequence.Time-6)<1e-8)&&plan.Any(e=>Math.Abs(e.Time-sequence.Time-9)<1e-8),id+" Float independent 0/3/6/9 emissions");
                }
                foreach(string key in new[]{"ship_horn","whale","sardine"})Check(_game.AudioCues.Events.Count(e=>e.Key==key)==sequences.Count(e=>e.Type==key),id+" one processed "+key+" sound per authored start");
                while(!_game.Result.Visible)
                {await Wait(15);GD.Print($"PROGRESS {id} {_game.Rhythm.SongTimeSeconds:F1}s/{_game.Data.DurationSeconds:F1}s");if(_game.GameOver.Visible)throw new Exception("Unexpected starvation fixture");}
                await Wait(2);var total=_game.Result.Result!;
                Check(_next==_game.Data.Phrases.Count&&total.Perfect==_next&&total.Miss==0,id+" every target resolves once without chart seek");
                Check(_game.ResultPresentationCount==1&&_game.Rhythm.SongTimeSeconds>=_game.Rhythm.AppliedSongEndTimeSeconds,id+" only actual song end opens one result");
                Check(_game.AudioCues.UnderrunResyncs==0,id+" all wave/interference audio renders without underruns");
                Check(_maxDrift<.05,id+" music/clock drift <50ms (max "+(_maxDrift*1000).ToString("F2")+"ms)");
                RenderingServer.ForceDraw();GetViewport().GetTexture().GetImage().SavePng("/private/tmp/bite-processed-"+id+"-result.png");
                _game.StopGameplay();_game.QueueFree();_game=null;await Wait(.1);
            }
            GD.Print($"PROCESSED AUDIO VERIFIED: {_checks} checks; "+(OS.GetCmdlineUserArgs().Contains("--data-only")?"data/mix/result audio checks only.":"Songs 2–4 complete unskipped."));GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
}
