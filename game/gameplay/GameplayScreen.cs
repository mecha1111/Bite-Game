using System;
using System.Linq;
using Godot;
using Gamejam2.Audio;
using Gamejam2.Rhythm;
using Gamejam2.Settings;
using Gamejam2.Save;
namespace Gamejam2.Gameplay;
/// <summary>Gameplay composition root; delegates timing, signals, consequences and visual feedback.</summary>
public partial class GameplayScreen : Control
{
    private InterferenceIndicators _indicators=null!;
    [Export] public StagePresentation Presentation { get; set; }=null!;
    [Export] public GameplayAudio AudioCues { get; set; } = null!;
    [Export] public GameplayJuice Juice { get; set; } = null!;
    [Export] public string SongId { get; set; } = "song_1";
    [Export] public RhythmController Rhythm { get; set; } = null!;
    [Export] public AudioStreamPlayer Music { get; set; } = null!;
    [Export] public SharkPresenter Shark { get; set; } = null!;
    [Export] public SignalPresenter Signals { get; set; } = null!;
    [Export] public SatietyGauge Gauge { get; set; } = null!;
    [Export] public JudgmentFeedback Feedback { get; set; } = null!;
    [Export] public Button SettingsButton { get; set; } = null!;
    [Export] public ComboFeedback Combo { get; set; } = null!;
    [Export] public ResumeCountdown Countdown { get; set; } = null!;
    [Export] public ResultScreen Result { get; set; } = null!;
    [Export] public GameOverScreen GameOver { get; set; } = null!;
    [Export] public InterferencePresenter Interference { get; set; } = null!;
    [Export] public GameplayEnvironment Environment { get; set; } = null!;
    [Export] public SettingsPopup StandaloneSettings { get; set; } = null!;
    [Export] public ColorRect WorldWaterTreatment { get; set; } = null!;
    [Export] public Control UnavailableLayer { get; set; } = null!;
    [Export] public Button UnavailableBack { get; set; } = null!;
    public event Action? SettingsRequested;
    public event Action? LobbyRequested;
    public GameplayData Data { get; private set; } = null!;
    public SatietyState Satiety { get; private set; } = null!;
    public enum PreyEncounterState { None, Presenting, AwaitingTarget, Attempted, Resolved }
    public PreyEncounterState PreyState { get; private set; }
    public int ActivePreyIndex { get; private set; } = -1;
    private readonly PreySideSequence _preySides=new();
    public bool? LastPreySide=>_preySides.LastPreySide;
    protected void ApplyPreySideSequence(GameplayData data,bool? scriptedFirstSide=null)=>_preySides.Prepare(data,scriptedFirstSide);
    protected virtual void PreparePreySides(GameplayData data)=>ApplyPreySideSequence(data);
    private int _nextPrey;
    private readonly System.Collections.Generic.HashSet<int> _skippedPhrases=new();
    private double _lastBite=double.NegativeInfinity;
    public bool IsBiteInteractive=>AllowBite&&Data!=null&&RunState==GameplayRunState.Playing&&!InputBlocked&&!_ended&&Rhythm.IsClockAdvancing;
    public bool InputBlocked { get; private set; }
    public bool IsCountingDown => Countdown.IsActive;
    public enum GameplayRunState { Loading, Playing, Paused, Finishing, GameOver, Result, Abandoned, Unavailable }
    public GameplayRunState RunState { get; private set; } = GameplayRunState.Loading;
    public int ResultPresentationCount { get; private set; }
    private bool _ended;
    private bool _auditLogging,_chartLogging;
    private double _terminalEffectElapsed;
    private bool _leftAmbient,_rightAmbient,_farAmbient;
    private bool _pauseOwned;
    public override void _Ready()
    {
        try
        {
            var debugArgs=OS.GetCmdlineUserArgs();
            _auditLogging=Gamejam2.Startup.BuildFeatures.DevelopmentEnabled&&debugArgs.Contains("--runtime-audit");
            _chartLogging=Gamejam2.Startup.BuildFeatures.DevelopmentEnabled&&debugArgs.Contains("--chart-debug");
            _indicators=GetNode<InterferenceIndicators>("InterferenceIndicators");
            _leftAmbient=Environment.LeftParticles.Emitting;_rightAmbient=Environment.RightParticles.Emitting;_farAmbient=Environment.FarParticles.Emitting;
            Environment.WorldWaterOverlay=WorldWaterTreatment;Environment.JuiceProfile=Juice.Profile;Juice.Water=WorldWaterTreatment;Juice.MouthAnchor=GetNode<Control>("Composition/SharkAnchor/BiteTargetAnchor");Juice.Shark=Shark;Gauge.JuiceProfile=Juice.Profile;
            Interference.Started+=Juice.InterferenceStarted;Interference.Emitted+=Juice.InterferenceEmission;UnavailableBack.Pressed+=()=>LobbyRequested?.Invoke();
            Rhythm.JudgmentResolved+=OnJudgment;
            Rhythm.SongFinished+=Finish;
            SettingsButton.Pressed+=OpenSettings; Result.RetryRequested+=Restart; Result.SongSelectRequested+=()=>LobbyRequested?.Invoke();
            GameOver.RetryRequested+=Restart;GameOver.SongSelectRequested+=()=>LobbyRequested?.Invoke();
            StandaloneSettings.VisibilityChanged+=()=>{ if(StandaloneSettings.Visible) SuspendForSettings(); else if(InputBlocked&&!_ended) BeginResumeCountdown(); };
            Restart();
            Presentation.InstallWorldCamera();
        }
        catch(Exception error){ShowLoadFailure(error);}
    }
    protected virtual GameplayData LoadData() => GameplayData.Load(SongId);
    protected virtual AudioStream LoadMusic() => SongAudio.Load(Data.AudioPath);
    protected virtual double StartSeconds => 0;
    protected virtual bool ProtectSatiety => false;
    protected virtual bool AllowBite => true;
    protected virtual float InterferenceAudioGain => 1;
    protected virtual void ConfigureInterference() => Interference.Initialize(SongId);
    protected virtual bool DeferJudgmentPresentation=>false;
    protected virtual void JudgmentObserved(RhythmJudgmentResult result) {}
    protected virtual void RunStarted() {}
    public void Restart()
    {
        _terminalEffectElapsed=0;SetProcess(true);
        Environment.LeftParticles.Emitting=_leftAmbient;Environment.RightParticles.Emitting=_rightAmbient;Environment.FarParticles.Emitting=_farAmbient;
        if(ProtectSatiety)_preySides.RebaseCheckpoint();else _preySides.Reset();
        _skippedPhrases.Clear();_lastBite=double.NegativeInfinity;RunState=GameplayRunState.Loading;ResultPresentationCount=0;_nextPrey=0;ActivePreyIndex=-1;PreyState=PreyEncounterState.None;
        try
        {
            _failure?.Kill();UnavailableLayer.Visible=false;Rhythm.StopSong(); Environment.Apply(SongId); Data=LoadData();PreparePreySides(Data); Satiety=new(Data,ProtectSatiety);Juice.Reset(Data);Gauge.ResetDisplay(Satiety.Value); Shark.Reset();Feedback.Clear(); Signals.Initialize(Data);ConfigureInterference();AudioCues.Initialize(Data,Interference,Rhythm,InterferenceAudioGain);
            Rhythm.Chart=Data.Chart; Rhythm.JudgmentWindows=new(); Rhythm.UserOffsetSeconds=SettingsService.InputOffsetSeconds;
            Music.Stream=LoadMusic();
            Data.UseAudioDuration(Music.Stream.GetLength());Gauge.JudgmentEffects=Combo.JudgmentEffects=Data.JudgmentEffects;
            Juice.JumpBubbles.Enabled=!ProtectSatiety;Presentation.Initialize(this,Data,!ProtectSatiety);
            
            _ended=false; _pauseOwned=false; InputBlocked=false; Result.HideResult(); GameOver.HideScreen();Gauge.Visible=true;Signals.LeftLane.Modulate=Signals.RightLane.Modulate=Interference.Effects.Modulate=Colors.White;Countdown.Cancel();SettingsButton.Disabled=false;SettingsButton.GetParent<Control>().Visible=true;
            if(!Rhythm.StartSong(StartSeconds)) throw new InvalidOperationException("Gameplay 곡 시작 실패");
            RunState=GameplayRunState.Playing;
            if(_auditLogging)
            {
                GD.Print($"[RUNTIME] Scene={SceneFilePath} CurrentSongId={SongId} Audio={Music.Stream.ResourcePath} length={Music.Stream.GetLength():F6} playback={Music.GetPlaybackPosition():F6} Chart={Data.ChartPath} MusicBpm={Data.MusicalBpm:F6} GameplayPulseBpm={Data.Chart.Bpm:F6} BeatOffset={Data.BeatOffsetSeconds:F6} LoadedPrey={Data.Phrases.Count}");
                GD.Print($"[RUNTIME] WorldOwner={WorldWaterTreatment.GetPath()} Shader={((ShaderMaterial)WorldWaterTreatment.Material).Shader.ResourcePath} Shark={Shark.GetPath()} Animations={string.Join(',',Shark.Art.SpriteFrames.GetAnimationNames())}");
                foreach(var p in Data.Phrases.Take(5))GD.Print($"[CHART] #{p.Index} Pattern={p.PatternId} Side={(p.FromLeft?"L":"R")} Start={p.StartSeconds:F6} Target={p.TargetSeconds:F6} NearestBeat={p.NearestBeatSeconds:F6} TimingGrid={p.TimingGrid}");
            }
            Gauge.Present(Satiety.Value,Data.MaxSatiety,Data.ClearThreshold); Combo.Reset();_indicators.Initialize(this);RunStarted();
        }
        catch(Exception error)
        {
            ShowLoadFailure(error);
        }
    }
    private void ShowLoadFailure(Exception error)
    {
        GD.PushError($"Stage load failed [{SongId}]: {error}");Rhythm.StopSong();
        _ended=true;InputBlocked=true;RunState=GameplayRunState.Unavailable;SettingsButton.Disabled=true;SettingsButton.GetParent<Control>().Visible=false;Result.HideResult();GameOver.HideScreen();
        var message=UnavailableLayer.GetNode<Label>("Message");message.Text="곡 불러오기 실패\n"+error.Message;message.TooltipText=error.ToString();
        message.AutowrapMode=TextServer.AutowrapMode.WordSmart;message.AddThemeFontSizeOverride("font_size",24);message.OffsetTop=-120;
        UnavailableLayer.Visible=true;UnavailableBack.GrabFocus();
    }
    /// <summary>Tutorial phrase change: same music/clock, future chart and scene-authored presenters.</summary>
    protected void InstallPracticeData(GameplayData data)
    {
        PreparePreySides(data);
        _skippedPhrases.Clear();Rhythm.InstallFutureChart(data.Chart);Data=data;_nextPrey=0;ActivePreyIndex=-1;PreyState=PreyEncounterState.None;
        Signals.Initialize(data);Juice.Reset(data);ConfigureInterference();AudioCues.Initialize(data,Interference,Rhythm,InterferenceAudioGain);
        AudioCues.ContinueTimeline(Rhythm.SongTimeSeconds);
    }
    /// <summary>Only tutorial mastery calls this; keep the resolved catch alive while cancelling future practice.</summary>
    protected int[] SkipFuturePracticeEncounters()
    {
        var ids=Data.Phrases.Where(p=>p.StartSeconds>Rhythm.SongTimeSeconds).Select(p=>p.Index).ToArray();
        foreach(int id in ids){_skippedPhrases.Add(id);Rhythm.SkipPracticePrey(id.ToString());Signals.SkipEncounter(id);AudioCues.ConsumePrey(id);}
        _nextPrey=Data.Phrases.Count;Juice.SkipFutureEncounterCues();return ids;
    }
    private void OpenSettings()
    {
        if(SettingsRequested!=null) SettingsRequested.Invoke();
        else {StandaloneSettings.SetSongTitle(Data.SongName);StandaloneSettings.Open();}
    }
    public override void _Input(InputEvent input)
    {
        if(input.IsEcho() || InputBlocked || _ended) return;
        if(input.IsActionPressed("ui_cancel")) {OpenSettings();GetViewport().SetInputAsHandled();return;}
        if(!AllowBite || !input.IsActionPressed("rhythm_input") || Rhythm.IsFocusPaused || !Rhythm.IsRunning) return;
        // A click on the settings control is UI, never a bite. Other bite inputs are captured immediately.
        if(input is InputEventMouseButton && SettingsButton.GetGlobalRect().HasPoint(GetViewport().GetMousePosition())) return;
        if(RunState!=GameplayRunState.Playing||!Rhythm.IsClockAdvancing)return;
        double timestamp=Rhythm.SongTimeSeconds;
        if(timestamp>=Rhythm.AppliedSongEndTimeSeconds)return;
        if(timestamp-_lastBite<Satiety.Policy.DebounceSeconds){GetViewport().SetInputAsHandled();return;}
        _lastBite=timestamp;
        // Every accepted active-gameplay attempt reacts before target/judgment logic, including empty water.
        Shark.Bite(timestamp);Juice.Bite(timestamp);
        if(_auditLogging)GD.Print($"[BITE INPUT] received time={timestamp:F6} shark={Shark.GetPath()} animation={Shark.Art.Animation}");
        GetViewport().SetInputAsHandled();
        UpdatePreyState(timestamp);
        bool attempted=false;
        if(ActivePreyIndex>=0&&PreyState!=PreyEncounterState.Resolved)
        {PreyState=PreyEncounterState.Attempted;attempted=Rhythm.TryCommitPrey(ActivePreyIndex.ToString());}
        if(!attempted)
        {Satiety.Advance(timestamp);Satiety.EmptyBite();PresentBiteResult(RhythmJudgment.Miss,timestamp);}
        GetViewport().SetInputAsHandled();if(Satiety.Value<=0)Starve();
    }
    private void OnJudgment(RhythmJudgmentResult result)
    {
        double time=Rhythm.SongTimeSeconds;
        if(_chartLogging&&int.TryParse(result.TargetEvent.CueId,out int loggedIndex))
        {var p=Data.Phrases[loggedIndex];GD.Print($"[JUDGMENT] Song={SongId} Pattern={p.PatternId} Start={p.StartSeconds:F6} Target={p.TargetSeconds:F6} TimingErrorMs={result.TimingErrorSeconds*1000:F3} Grade={result.Judgment} GameplayPulseBpm={Data.Chart.Bpm:F6} NearestMusicalBeat={p.NearestBeatSeconds:F6}");}
        if(int.TryParse(result.TargetEvent.CueId,out int index))
        {
            var phrase=Data.Phrases[index];var target=phrase.TargetEvents[result.TargetEvent.TargetOrdinal];
            target.JudgmentState=result.Judgment;target.ResolvedState=true;
            if(phrase.TargetEvents.All(t=>t.ResolvedState))
            {_preySides.Complete(phrase);if(index==ActivePreyIndex)PreyState=PreyEncounterState.Resolved;Signals.ResolveEncounter(index,time-SettingsService.VisualOffsetSeconds);AudioCues.ConsumePrey(index);}
        }
        Satiety.Advance(time);
        // During playback only actual depletion kills; the 50% rule belongs to Finish.
        if(Satiety.Value<=0&&time<Rhythm.AppliedSongEndTimeSeconds){Starve();return;}
        Satiety.Resolve(result);if(Data.Phrases.All(p=>p.TargetEvents.All(t=>t.ResolvedState)))Satiety.StopDrainAfterFinalTarget(time);if(!DeferJudgmentPresentation)PresentBiteResult(result.Judgment,time);JudgmentObserved(result);
    }
    protected void PresentBiteResult(RhythmJudgment judgment,double time)
    {
        Presentation.ObserveJudgment(judgment,time);
        Juice.Judgment(judgment,time,Satiety.Combo);
        if(judgment!=RhythmJudgment.Miss){Gauge.NotifyGain(time,judgment);AudioCues.Catch(judgment);}
        else {Gauge.NotifyMiss();AudioCues.Whiff();if(Satiety.LastMissPenalty>0)Gauge.NotifyPenalty(time);}
        Feedback.ShowResult(judgment,time);Combo.UpdateCombo(Satiety.Combo,time,judgment);
    }
    public void SuspendForSettings()
    { if(_ended)return;RunState=GameplayRunState.Paused; InputBlocked=true; Countdown.Cancel(); _pauseOwned|=Rhythm.IsRunning&&!Rhythm.IsManuallyPaused; Rhythm.PauseSong();if(Data!=null)PresentClockState(); }
    /// <summary>Countdown is UI time while Song Clock/audio remain frozen. Only ResumeSong starts playback again.</summary>
    public void BeginResumeCountdown()
    { if(_ended || !Rhythm.IsRunning) {InputBlocked=_ended;return;} InputBlocked=true; Countdown.Begin(); }
    public override void _Process(double delta)
    {
        if(Data==null||RunState==GameplayRunState.Unavailable) return;
        if(_ended&&RunState is GameplayRunState.Result or GameplayRunState.GameOver)
        {
            // Preserve the short final catch/starvation fade, then retire WORLD work.
            _terminalEffectElapsed+=delta;
            if(_terminalEffectElapsed>=.75){StopUnusedWorld();return;}
        }
        if(Countdown.IsActive && !Rhythm.IsFocusPaused && Countdown.Advance(delta))
        {if(_pauseOwned)Rhythm.ResumeSong();_pauseOwned=false;InputBlocked=Rhythm.IsManuallyPaused;RunState=InputBlocked?GameplayRunState.Paused:GameplayRunState.Playing;}
        PresentClockState(delta);
    }
    // Flush the last clock interval before freezing UI, so no due pulse appears a frame after pause.
    private void PresentClockState(double delta=0)
    {
        double time=Rhythm.SongTimeSeconds;
        if(!_ended) {Satiety.Advance(time); if(Satiety.Value<=0 && time<Rhythm.AppliedSongEndTimeSeconds) Starve();}
        UpdatePreyState(time);
        double visual=time-SettingsService.VisualOffsetSeconds;
        bool paused=InputBlocked||Rhythm.IsManuallyPaused||Rhythm.IsFocusPaused||_ended;
        Environment.Present(time,paused,delta); Signals.Present(visual);Interference.Present(visual); Shark.Present(time); Combo.Present(time); Feedback.Present(time); Gauge.Present(Satiety.Value,Data.MaxSatiety,Data.ClearThreshold,time);Juice.Present(visual,time,paused&&!_ended,Countdown.IsActive,delta);
        Presentation.Present(time);
        Interference.ApplyCurrent(Signals);
        _indicators.Present(visual,paused);
    }
    private void UpdatePreyState(double time)
    {
        // Presentation lead-ins may coexist; input has one nearest, stable owner.
        // Nearest unresolved valid target owns one press; ties follow chart order.
        double judgmentTime=time+Rhythm.AppliedUserOffsetSeconds;
        double distance=double.PositiveInfinity;PreyPhrase? owner=null;PhraseTargetEvent? target=null;
        foreach(var phrase in Data.Phrases)
        {
            if(_skippedPhrases.Contains(phrase.Index)||time<phrase.StartSeconds||judgmentTime>phrase.LastTargetSeconds+Rhythm.AppliedLateCaptureSeconds)continue;
            foreach(var candidate in phrase.TargetEvents)
            {
                if(candidate.ResolvedState)continue;
                if(candidate.Ordinal>0&&judgmentTime<candidate.TargetTime-Rhythm.AppliedEarlyCaptureSeconds)continue;
                double error=Math.Abs(judgmentTime-candidate.TargetTime);
                if(error<distance){distance=error;owner=phrase;target=candidate;}
            }
        }
        ActivePreyIndex=owner?.Index??-1;
        PreyState=target==null?PreyEncounterState.None:target.ResolvedState?PreyEncounterState.Resolved:
            judgmentTime>=target.TargetTime-Rhythm.AppliedEarlyCaptureSeconds?PreyEncounterState.AwaitingTarget:PreyEncounterState.Presenting;
    }

    protected virtual void Finish()
    {
        if(_ended || RunState!=GameplayRunState.Playing || Rhythm.PlaybackState!="Completed"
            || Rhythm.SongTimeSeconds < Rhythm.AppliedSongEndTimeSeconds)return;
        Satiety.Advance(Rhythm.AppliedSongEndTimeSeconds);
        if(Satiety.Value<500){Starve();return;}
        RunState=GameplayRunState.Finishing;
        if(Rhythm.IsRunning)Rhythm.StopSong();
        _ended=true;InputBlocked=true;SettingsButton.Disabled=true;SettingsButton.GetParent<Control>().Visible=false;Countdown.Cancel();
        var result=Satiety.CreateResult();if(Gamejam2.Data.CustomMapRegistry.IsCustom(SongId))Gamejam2.Data.CustomMapRegistry.Record(SongId,result.Cleared,result.FullCombo,result.AllPerfect,result.FinalSatiety);else ProgressService.RecordStageResult(SongId,result.Cleared,result.FullCombo,result.AllPerfect,result.FinalSatiety);RunState=GameplayRunState.Result;ResultPresentationCount++;Result.ShowResult(result);
    }
    private Tween? _failure;
    private void Starve()
    {
        if(_ended || RunState is not (GameplayRunState.Playing or GameplayRunState.Paused))return;
        RunState=GameplayRunState.GameOver;_ended=true;InputBlocked=true;Rhythm.StopSong();Countdown.Cancel();
        SettingsButton.Disabled=true;SettingsButton.GetParent<Control>().Visible=false;
        Result.HideResult();Feedback.Clear();Combo.Reset();Gauge.ResetDisplay(0);Gauge.Visible=false;Environment.SetStarved();Shark.LoseEnergy();
        _failure?.Kill();_failure=CreateTween().SetParallel();
        _failure.TweenProperty(Signals.LeftLane,"modulate:a",0,.3);
        _failure.TweenProperty(Signals.RightLane,"modulate:a",0,.3);
        _failure.TweenProperty(Interference.Effects,"modulate:a",0,.3);
        _failure.TweenProperty(Juice,"modulate:a",0,.3);
        GameOver.ShowStarvation();
    }
    private void StopUnusedWorld()
    {
        SetProcess(false);Juice?.ClearTransientEffects();Interference?.Stop();_indicators?.Stop();
        if(Signals!=null){Signals.LeftLane.Reset();Signals.RightLane.Reset();}
        Presentation?.AccentParticles.Reset();
        if(Environment!=null)Environment.LeftParticles.Emitting=Environment.RightParticles.Emitting=Environment.FarParticles.Emitting=false;
    }
    public void StopGameplay() {_ended=true;InputBlocked=true;RunState=GameplayRunState.Abandoned;Countdown.Cancel();_failure?.Kill();Rhythm.StopSong();StopUnusedWorld();AudioCues?.Stop();}
    public override void _ExitTree()=>_failure?.Kill();
}
