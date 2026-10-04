using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Gameplay;
using Gamejam2.Rhythm;
using Gamejam2.Settings;
namespace Gamejam2.Debugging;
/// <summary>Developer-only strict input, catch and real SFX checks. Seeks are fixtures, never player logic.</summary>
public partial class CatchAudioChecks : Node
{
    private readonly InputEventAction _bite=new(){Action="rhythm_input",Pressed=true};
    private GameplayScreen _game=null!;
    private int _checks,_targets;
    public override void _Process(double delta){if(_game!=null&&_game.Rhythm.IsFocusPaused)GetWindow().GrabFocus();}
    private void Check(bool condition,string label){if(!condition)throw new Exception(label);_checks++;GD.Print("PASS "+label);}
    private async Task Wait(double seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private async Task At(double time){while(_game.Rhythm.SongTimeSeconds<time){if(_game.Rhythm.IsFocusPaused)GetWindow().GrabFocus();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}}
    private void Seek(double time)
    {
        _game.Rhythm.PauseSong();
        typeof(RhythmController).GetField("_stoppedTimeSeconds",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(_game.Rhythm,time);
        _game.Rhythm.ResumeSong();
    }
    private async Task Fresh(double time){_game.Restart();_targets=0;Seek(time);await Wait(.09);while(!_game.Rhythm.IsClockAdvancing)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private void Capture(string name){RenderingServer.ForceDraw();GetViewport().GetTexture().GetImage().SavePng("/private/tmp/bite-catch-"+name+".png");}
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Run()
    {
        try
        {
            Gamejam2.Save.SaveStore.Initialize("/private/tmp/bite-catch-check-settings.cfg");Gamejam2.Save.ProgressService.Initialize();SettingsService.Initialize();
            SettingsService.InputOffsetSeconds=SettingsService.VisualOffsetSeconds=0;SettingsService.SetResolution(2);GetWindow().GrabFocus();
            _game=GD.Load<PackedScene>("res://game/gameplay/Gameplay.tscn").Instantiate<GameplayScreen>();_game.SongId="song_1";AddChild(_game);_game.SetProcessInput(false);await Wait(.15);
            Check(_game.RunState==GameplayScreen.GameplayRunState.Playing,"game starts with imported PCM SFX");
            _game.Rhythm.JudgmentResolved+=_=>_targets++;
            var data=_game.Data;double start=data.Phrases[0].StartSeconds,target=data.Phrases[0].TargetSeconds;
            Check(_game.AudioCues.Events.Where(e=>e.Prey>=0).Select(e=>e.Time).SequenceEqual(data.Chart.Events.Where(e=>e.EventType==RhythmEventType.Cue).Select(e=>e.TimeSeconds)),"audio uses every authored Cue timestamp, no target answer cue");
            Check(_game.AudioCues.Events.Any(e=>e.Prey>=0&&e.Pan==-1)&&_game.AudioCues.Events.Any(e=>e.Prey>=0&&e.Pan==1),"left and right cues keep directional stereo source");
            foreach(var pair in new[]{("ship_horn",2),("fishing_float",0),("whale",0),("sardine",0)})Check(_game.AudioCues.Events.Count(e=>e.Key==pair.Item1)==pair.Item2,pair.Item1+" processed audio matches song schedule");
            while(!_game.Rhythm.IsClockAdvancing)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            _game._Input(_bite);Check(_game.Satiety.CreateResult().Miss==1&&_game.Shark.BiteCount==1,"active gameplay before first prey is an animated empty MISS");
            var state=new SatietyState(data);
            state.EmptyBite();Check(state.MissStreak==1&&state.LastMissPenalty==0&&state.Value==500,"first empty MISS costs zero extra satiety");
            state.EmptyBite();Check(state.MissStreak==2&&state.LastMissPenalty==5&&state.Value==495,"second consecutive MISS costs five");
            state.EmptyBite();state.EmptyBite();Check(state.MissStreak==4&&state.Value==475&&state.LastMissPenalty==10,"third and later MISS cost ten each");
            foreach(var grade in new[]{RhythmJudgment.Perfect,RhythmJudgment.Good,RhythmJudgment.Bad})
            {state.Resolve(new(grade,0,new()));Check(state.MissStreak==0&&state.Combo>0,"successful "+grade+" resets MISS streak");state.EmptyBite();}
            Check(state.CreateResult().Miss==7&&!state.CreateResult().FullCombo,"empty whiffs are final MISS stats and disqualify full combo");
            await Fresh(start+.25);_game._Input(_bite);
            Check(_targets==1&&_game.Rhythm.LastResult!.Value.Judgment==RhythmJudgment.Miss&&_game.PreyState==GameplayScreen.PreyEncounterState.Resolved,"beat-two input immediately consumes prey as MISS");
            _game._Input(_bite);Check(_game.Satiety.MissStreak==1,"duplicate hardware event is debounced");
            for(int i=0;i<3;i++){await Wait(.16);_game._Input(_bite);}
            GD.Print($"REPEAT targets={_targets}, empty={_game.Satiety.EmptyBites}, streak={_game.Satiety.MissStreak}, state={_game.PreyState}, clock={_game.Rhythm.SongTimeSeconds:F3}, focus={_game.Rhythm.IsFocusPaused}");
            Check(_targets==1&&_game.Satiety.EmptyBites==3&&_game.Satiety.MissStreak==4,"resolved prey receives no second attempt; later presses are empty MISS");
            await At(target+.005);_game._Input(_bite);
            Check(_targets==1&&_game.Satiety.CreateResult().Perfect==0&&_game.Satiety.EmptyBites==4,"accurate late press cannot repair early prey MISS");
            Capture("whiff");
            await Fresh(start+.04);double mashStart=_game.Rhythm.SongTimeSeconds;
            for(int tap=0;tap<12;tap++){await At(mashStart+tap/12d);_game._Input(_bite);}
            GD.Print($"MASH targets={_targets} empty={_game.Satiety.EmptyBites} streak={_game.Satiety.MissStreak} time={_game.Rhythm.SongTimeSeconds:F3} state={_game.RunState}");
            Check(_targets==1&&_game.Satiety.EmptyBites==11&&_game.Satiety.MissStreak==12,"12 presses/sec consumes one prey and penalizes eleven empty whiffs");
            await At(target+.005);_game._Input(_bite);Check(_game.Satiety.CreateResult().Perfect==0&&_targets==1,"12 Hz mashing cannot recover target at beat five");
            // Three encounters tapped at a constant quarter-note rhythm; no accurate rescue is possible.
            foreach(int index in new[]{0,1,2})
            {
                var phrase=data.Phrases[index];await Fresh(phrase.StartSeconds+.06);int initial=_targets;
                _game._Input(_bite);int resolved=_targets;
                for(int beat=1;beat<=4;beat++){await At(phrase.StartSeconds+beat*60/data.Chart.Bpm+.06);_game._Input(_bite);}
                Check(resolved==initial+1&&_targets==resolved&&_game.Rhythm.LastResult!.Value.Judgment==RhythmJudgment.Miss&&_game.Satiety.CreateResult().Perfect==0,"constant 4/4 cannot fish for PERFECT, encounter "+index);
            }
            foreach(var item in new[]{(RhythmJudgment.Perfect,0d),(RhythmJudgment.Good,.100),(RhythmJudgment.Bad,.175)})
            {
                await Fresh(target-.38);_game.Feedback.Visible=false;await At(target+item.Item2);double clock=_game.Rhythm.SongTimeSeconds;
                _game._Input(_bite);
                Check(_game.Rhythm.LastResult!.Value.Judgment==item.Item1&&_targets==1,"single accurate input yields "+item.Item1+" at accessible windows");
                Check(_game.AudioCues.CatchStacks==1&&_game.AudioCues.GulpLayers==1&&_game.AudioCues.WhiffSounds==0,"caught "+item.Item1+" plays snap plus gulp, no whiff");
                Check(_game.Juice.CatchSeal.Visible&&_game.Juice.CatchSeal.CatchCount==1&&_game.Satiety.Combo==1&&_game.Satiety.MissStreak==0,"caught "+item.Item1+" remains explicit with judgment images hidden");
                var impact=_game.Juice.LastCatchImpact!;var expected=_game.Data.JudgmentEffects[item.Item1];
                Check(impact!=null&&impact.MediumDiameter==expected.Diameter&&Math.Abs(impact.PeakOpacity-expected.Opacity)<.0001&&impact.ActivationBoost==0,"judgment impact size/opacity matches current four-grade table");
                await Wait(.045);Capture(item.Item1.ToString());
                Check(_game.Juice.CatchSeal.GlobalPosition.DistanceTo(_game.Shark.MouthImpact.GlobalPosition)<1,"catch seal follows animated mouth without moving chart origin");
                Check(_game.Rhythm.SongTimeSeconds>clock&&_game.Music.Playing&&!_game.Rhythm.IsManuallyPaused,"hit emphasis never interrupts music or Song Clock");
            }
            await Fresh(target-.3);await At(target+.28);Check(_targets==1&&_game.Rhythm.LastResult!.Value.Judgment==RhythmJudgment.Miss,"no input auto misses once at existing late-capture deadline");
            _game._Input(_bite);Check(_targets==1&&_game.Satiety.EmptyBites==1&&_game.AudioCues.GulpLayers==0,"post-timeout bite is empty whiff, never a second target result");
            // Record a real audio-only prey phrase for mix/listening review.
            await Fresh(start-.15);int bus=AudioServer.GetBusIndex("SFX");var recorder=new AudioEffectRecord();AudioServer.AddBusEffect(bus,recorder);
            recorder.SetRecordingActive(true);await Wait(2.6);recorder.SetRecordingActive(false);
            var recording=recorder.GetRecording();Check(recording!=null&&recording.Data.Length>100000,"SFX bus records actual audible pattern PCM");recording!.SaveToWav("/private/tmp/bite-pattern-audio.wav");
            AudioServer.RemoveBusEffect(bus,AudioServer.GetBusEffectCount(bus)-1);
            Check(_game.AudioCues.WaveEventsRendered>=5&&_game.AudioCues.UnderrunResyncs==0,"all five s1 pings render without underrun");
            _game.SuspendForSettings();double frozen=_game.Rhythm.SongTimeSeconds;_game._Input(_bite);int misses=_game.Satiety.CreateResult().Miss;await Wait(.2);
            Check(_game.Rhythm.SongTimeSeconds==frozen&&!_game.AudioCues.IsCuePlaying&&_game.Satiety.CreateResult().Miss==misses,"pause freezes music, cue renderer and bite penalties together");
            _game.BeginResumeCountdown();_game._Input(_bite);await Wait(.15);Check(_game.IsCountingDown&&!_game.AudioCues.IsCuePlaying&&_game.Satiety.CreateResult().Miss==misses,"countdown cannot cause whiff or restart cue audio early");
            await Wait(3.2);Check(!_game.InputBlocked&&_game.AudioCues.IsCuePlaying&&_game.Music.Playing&&_game.Rhythm.SongTimeSeconds>frozen,"resume restarts SFX from same authoritative music position");
            SettingsService.SetEffectsVolume(0);Check(AudioServer.IsBusMute(bus)&&!AudioServer.IsBusMute(AudioServer.GetBusIndex("Music")),"SFX setting mutes all cues/catch but not music");SettingsService.SetEffectsVolume(1);
            await Fresh(target+.35);_game.Satiety.EmptyBite();_game.Satiety.EmptyBite();
            typeof(SatietyState).GetProperty("Value")!.SetValue(_game.Satiety,5d);_game._Input(_bite);
            Check(_game.RunState==GameplayScreen.GameplayRunState.GameOver&&!_game.Music.Playing&&!_game.AudioCues.IsCuePlaying&&_game.AudioCues.IsFeedbackPlaying,"fatal MISS freezes run but preserves final short whiff sound");
            foreach(string id in new[]{"song_2","song_3","song_4"})
            {var other=GD.Load<PackedScene>("res://game/gameplay/Gameplay.tscn").Instantiate<GameplayScreen>();other.SongId=id;AddChild(other);await Wait(.03);Check(other.RunState==GameplayScreen.GameplayRunState.Playing&&other.AudioCues.Events.Any(e=>e.Prey<0),id+" loads cue family and authored interference");other.StopGameplay();other.QueueFree();await Wait(.03);}
            _game.StopGameplay();GD.Print($"CATCH AUDIO VERIFIED: {_checks} checks");GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
}
