using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
namespace Gamejam2.Rhythm;
/// <summary>유일한 곡 시계와 차트 사건, 입력/자동 Miss 판정을 소유하는 gameplay Node.</summary>
public partial class RhythmController : Node
{
    /// <summary>Main Scene Inspector에서 연결할 음악 Node. 재생은 controller만 변경한다.</summary>
    [Export] public AudioStreamPlayer? MusicPlayer { get; set; }
    /// <summary>이번 실행에서 사용할 디자인 차트.</summary>
    [Export] public RhythmChart? Chart { get; set; }
    /// <summary>판정 범위의 유일한 Resource 설정. 변경은 다음 StartSong에 적용한다.</summary>
    [Export] public RhythmJudgmentWindows JudgmentWindows { get; set; } = new();
    /// <summary>이번 실행에 실제 적용된 불변 판정 범위.</summary>
    public RhythmJudgmentWindowSnapshot AppliedJudgmentWindows { get; private set; }
    [Export] public InputCaptureWindows? CaptureWindows { get; set; }
    public double AppliedEarlyCaptureSeconds { get; private set; }
    public double AppliedLateCaptureSeconds { get; private set; }
    private enum TargetState { Waiting, Attempted, Resolved, SkippedByMastery }
    private TargetState[] _targetStates = Array.Empty<TargetState>();
    /// <summary>시작 시 snapshot하여 판정 시계에 더한다. 양수는 같은 입력을 더 Late로 평가한다.</summary>
    [Export] public double UserOffsetSeconds { get; set; }
    /// <summary>시작/구간/판정/포커스/완료만 기록한다.</summary>
    [Export] public bool EnableDebugLogging { get; set; } = true;
    /// <summary>Stream이 없을 때 오디오 지연 없는 테스트 시계를 허용한다.</summary>
    [Export] public bool AllowSilentPreview { get; set; } = true;
    /// <summary>InputTarget을 한 번 resolve한 직후 발생한다.</summary>
    public event Action<RhythmJudgmentResult>? JudgmentResolved;
    /// <summary>Cue 시점 도달 시 한 번 발생한다. 수신자는 snapshot을 수정하지 않는다.</summary>
    public event Action<RhythmEventData>? CueTriggered;
    /// <summary>Section 시점 도달로 CurrentSection이 갱신된 직후 발생한다.</summary>
    public event Action<RhythmEventData>? SectionChanged;
    /// <summary>선택 음원의 실제 끝에서 실행당 한 번 발생한다. Stop은 완료가 아니다.</summary>
    public event Action? SongFinished;
    // Optional SFX consumers follow these existing music lifecycle operations; no second clock.
    public event Action<double>? AudioPlaybackStarted;
    public event Action? AudioPlaybackSuspended;
    /// <summary>곡 시간(초). 포커스 pause 동안 고정되며 resume 출력 대기 동안에도 고정된다.</summary>
    public double SongTimeSeconds => IsRunning && !IsFocusPaused && !IsManuallyPaused
        ? _clockBaseSeconds + Math.Max(0, (Time.GetTicksUsec() - _startTimeUsec) / 1_000_000.0 - _audioStartDelaySeconds)
        : _stoppedTimeSeconds;
    /// <summary>현재 실행에 실제 적용한 offset. Export 변경은 다음 StartSong에서 적용한다.</summary>
    public double AppliedUserOffsetSeconds { get; private set; }
    public double AppliedSongEndTimeSeconds { get; private set; }
    /// <summary>offset을 적용하는 유일한 위치. Cue/Section은 이 시계가 아닌 SongTime을 사용한다.</summary>
    public double JudgmentTimeSeconds => JudgmentTimeAt(SongTimeSeconds);
    /// <summary>차트에서 가장 앞에 남은 미처리 이벤트 index. 입력 전용 index와 다르다.</summary>
    public int CurrentEventIndex { get; private set; }
    /// <summary>가장 앞의 미처리 이벤트의 곡 기준 시점(초), 완료하면 null.</summary>
    public double? NextEventTimeSeconds => CurrentEventIndex < _events.Count
        ? EventTimeSeconds(CurrentEventIndex) : null;
    /// <summary>최근 판정. 재시작하면 null.</summary>
    public RhythmJudgmentResult? LastResult { get; private set; }
    /// <summary>최근 도달한 SectionId. 시작 전/재시작 시에는 비어 있다.</summary>
    public string CurrentSection { get; private set; } = "";
    /// <summary>시작된 실행인지 여부. focus pause 동안에도 true다.</summary>
    public bool IsRunning { get; private set; }
    /// <summary>focus loss 때문에 음악/clock/이벤트가 일시정지되어 있는지 여부.</summary>
    public bool IsFocusPaused { get; private set; }
    /// <summary>수동 일시정지. 포커스 복귀가 이를 해제하지 않는다.</summary>
    public bool IsManuallyPaused { get; private set; }
    /// <summary>읽기 전용 재생 상태.</summary>
    public string PlaybackState { get; private set; } = "Idle";
    private readonly List<RhythmEventData> _events = new();
    private readonly List<int> _inputTargetIndices = new();
    private bool[] _processed = Array.Empty<bool>();
    private int _timelineEventIndex;
    private int _nextInputTargetIndex;
    private int _runVersion;
    private ulong _startTimeUsec;
    private double _clockBaseSeconds;
    private double _audioStartDelaySeconds;
    private double _stoppedTimeSeconds;
    private double _songOffsetSeconds;
    private bool _hasFocus = true;
    private Window? _focusWindow;
    private bool _silentPreview;
    private bool _resumeAudio;
    /// <summary>필수 의존성을 최초 실행 시 확인한다.</summary>
    public override void _Ready()
    {
        if (MusicPlayer == null) GD.PushError("RhythmController: Main Inspector에서 MusicPlayer를 연결하세요.");
        if (Chart == null) GD.PushError("RhythmController: Main Inspector에서 Chart Resource를 연결하세요.");
        // Headless에는 OS focus가 없으므로 notification 테스트와 무음 실행을 허용한다.
        if (DisplayServer.GetName() != "headless")
        {
            _focusWindow = GetWindow();
            _hasFocus = _focusWindow.HasFocus();
            _focusWindow.FocusEntered += OnWindowFocusEntered;
            _focusWindow.FocusExited += OnWindowFocusExited;
        }
    }
    /// <summary>처음부터 재시작한다. Resource는 수정하지 않고 설정/데이터를 snapshot한다.</summary>
    public bool StartSong(double startSeconds = 0)
    {
        StopSong();
        ResetRuntimeState();
        if (!double.IsFinite(startSeconds) || startSeconds < 0) return Fail("Invalid checkpoint start.");
        if (MusicPlayer == null || Chart == null) return Fail("MusicPlayer와 Chart를 Main Inspector에서 연결하세요.");
        if (JudgmentWindows == null) return Fail("JudgmentWindows 설정을 연결하세요.");
        try { AppliedJudgmentWindows = JudgmentWindows.CreateSnapshot(); }
        catch (ArgumentException exception) { return Fail(exception.Message); }
        CaptureWindows ??= GD.Load<InputCaptureWindows>("res://game/rhythm/InputCaptureWindows.tres");
        if (CaptureWindows == null || !double.IsFinite(CaptureWindows.EarlySeconds) || !double.IsFinite(CaptureWindows.LateSeconds)
            || CaptureWindows.EarlySeconds < AppliedJudgmentWindows.BadSeconds || CaptureWindows.LateSeconds < AppliedJudgmentWindows.BadSeconds)
            return Fail("Capture windows must include every successful judgment window.");
        AppliedEarlyCaptureSeconds = CaptureWindows.EarlySeconds;
        AppliedLateCaptureSeconds = CaptureWindows.LateSeconds;
        if (!double.IsFinite(Chart.SongEndTimeSeconds) || Chart.SongEndTimeSeconds < 0) return Fail("SongEndTimeSeconds must be finite and non-negative.");
        if (!double.IsFinite(UserOffsetSeconds) || !double.IsFinite(Chart.SongOffsetSeconds))
            return Fail("offset은 유한값이어야 합니다.");
        _silentPreview = MusicPlayer.Stream == null;
        if (_silentPreview && !AllowSilentPreview) return Fail("MusicPlayer Stream을 연결하거나 AllowSilentPreview를 켜세요.");
        if (!_silentPreview && (!IsSupportedMusicStream(MusicPlayer.Stream!) || MusicPlayer.PitchScale != 1))
            return Fail("Focus resume에는 loop 없는 PCM WAV/Ogg Vorbis/MP3와 MusicPlayer PitchScale=1이 필요합니다.");
        foreach (var source in Chart.Events)
        {
            if (source == null || !double.IsFinite(source.TimeSeconds) || source.TimeSeconds < 0
                || !double.IsFinite(source.TimeSeconds + Chart.SongOffsetSeconds)
                || !Enum.IsDefined(source.EventType))
                return Fail("Chart Events의 TimeSeconds/EventType을 확인하세요. null/비유한 시점은 지원하지 않습니다.");
            _events.Add(new RhythmEventData { TimeSeconds = source.TimeSeconds,
                EventType = source.EventType, CueId = source.CueId, SectionId = source.SectionId,
                IsHittable = source.IsHittable, ResolveDeadlineSeconds=source.ResolveDeadlineSeconds,TargetOrdinal=source.TargetOrdinal,WaveEventId=source.WaveEventId });
            if (IsInputTarget(source)) _inputTargetIndices.Add(_events.Count - 1);
        }
        for (int index = 1; index < _events.Count; index++)
            if (_events[index].TimeSeconds < _events[index - 1].TimeSeconds)
                return Fail("Chart Events를 TimeSeconds 오름차순으로 작성하세요.");
        _processed = new bool[_events.Count];
        _targetStates = new TargetState[_events.Count];
        _songOffsetSeconds = Chart.SongOffsetSeconds;
        AppliedUserOffsetSeconds = UserOffsetSeconds;
        double lastEvent = _events.Count > 0 ? EventTimeSeconds(_events.Count-1) : 0;
        double lastCapture = _inputTargetIndices.Count > 0 ? EventTimeSeconds(_inputTargetIndices[^1]) + AppliedLateCaptureSeconds - AppliedUserOffsetSeconds : 0;
        // The audible Song Clock reaches the actual selected stream length. Chart gaps and
        // late capture windows never shorten or extend an audio-backed stage.
        AppliedSongEndTimeSeconds = _silentPreview
            ? (Chart.SongEndTimeSeconds>0?Chart.SongEndTimeSeconds+_songOffsetSeconds:Math.Max(lastEvent,lastCapture))
            : MusicPlayer.Stream!.GetLength();
        if(AppliedSongEndTimeSeconds<=0||lastEvent>AppliedSongEndTimeSeconds+1e-6)
            return Fail("Chart events must fit the authoritative song duration.");
        if (_silentPreview) GD.PushWarning("RhythmController: MusicPlayer Stream 없음. 무음 timing preview를 시작합니다.");
        IsRunning = true;
        _stoppedTimeSeconds = startSeconds;
        RefreshWindowFocus();
        if (_hasFocus) BeginPlaybackAt(startSeconds, !_silentPreview);
        else { IsFocusPaused = true; _resumeAudio = !_silentPreview; PlaybackState = "Focus Paused"; }
        Log("Song Started: " + PlaybackState);
        return true;
    }
    /// <summary>Install a future practice phrase without touching the authoritative clock or music playback.</summary>
    internal void InstallFutureChart(RhythmChart chart)
    {
        double now=SongTimeSeconds;
        if(!IsRunning||chart.Events.Count==0||chart.SongOffsetSeconds!=0||chart.Events.Any(e=>IsInputTarget(e)&&e.TimeSeconds<now))
            throw new ArgumentException("Practice targets must be future-only on the current song timeline.");
        double previous=-1;
        foreach(var e in chart.Events){if(!double.IsFinite(e.TimeSeconds)||e.TimeSeconds<previous)throw new ArgumentException("Unordered practice chart.");previous=e.TimeSeconds;}
        _runVersion++;_events.Clear();_inputTargetIndices.Clear();
        foreach(var e in chart.Events){_events.Add(new RhythmEventData{TimeSeconds=e.TimeSeconds,EventType=e.EventType,CueId=e.CueId,IsHittable=e.IsHittable,SectionId=e.SectionId,TargetOrdinal=e.TargetOrdinal,WaveEventId=e.WaveEventId});if(IsInputTarget(e))_inputTargetIndices.Add(_events.Count-1);}
        _processed=new bool[_events.Count];_targetStates=new TargetState[_events.Count];
        _timelineEventIndex=_nextInputTargetIndex=CurrentEventIndex=0;LastResult=null;Chart=chart;
    }
    /// <summary>Automated tutorial demonstration uses the real target timestamp, never player offset.</summary>
    internal bool DemonstratePrey(string cueId)
    {
        int position=_inputTargetIndices.FindIndex(i=>_events[i].CueId==cueId);
        if(!CanAdvance||position<0)return false;
        int index=_inputTargetIndices[position];if(_targetStates[index]!=TargetState.Waiting)return false;
        if(SongTimeSeconds<EventTimeSeconds(index))return false;
        _targetStates[index]=TargetState.Attempted;Resolve(index,RhythmJudgment.Perfect,0);return true;
    }
    /// <summary>Tutorial-only cancellation: consume pending cues/targets without a judgment callback.</summary>
    internal void SkipPracticePrey(string cueId)
    {
        for(int i=0;i<_events.Count;i++)
        {
            if(_events[i].CueId!=cueId||_processed[i])continue;
            MarkProcessed(i);
            if(IsInputTarget(_events[i]))_targetStates[i]=TargetState.SkippedByMastery;
        }
        while(_nextInputTargetIndex<_inputTargetIndices.Count&&_processed[_inputTargetIndices[_nextInputTargetIndex]])_nextInputTargetIndex++;
    }
    private void ResetRuntimeState()
    {
        _events.Clear();
        _inputTargetIndices.Clear();
        _processed = Array.Empty<bool>();
        _targetStates = Array.Empty<TargetState>();
        _timelineEventIndex = 0;
        _nextInputTargetIndex = 0;
        CurrentEventIndex = 0;
        LastResult = null;
        CurrentSection = "";
        AppliedUserOffsetSeconds = 0;
        _clockBaseSeconds = 0;
        _stoppedTimeSeconds = 0;
        _audioStartDelaySeconds = 0;
        _resumeAudio = false;
    }
    /// <summary>음악과 시계를 정지한다. 이후 focus in으로 다시 시작하지 않는다.</summary>
    public void StopSong()
    {
        _stoppedTimeSeconds = SongTimeSeconds;
        IsRunning = false;
        IsFocusPaused = false;
        IsManuallyPaused = false;
        _resumeAudio = false;
        _runVersion++;
        MusicPlayer?.Stop();
        AudioPlaybackSuspended?.Invoke();
        PlaybackState = "Stopped";
    }
    // 수동/포커스 정지는 동일한 audible 위치와 출력 지연 보정 경로를 공유한다.
    private void FreezePlayback()
    {
        _stoppedTimeSeconds = SongTimeSeconds;
        _resumeAudio = !_silentPreview && MusicPlayer!.Playing;
        MusicPlayer?.Stop();
        AudioPlaybackSuspended?.Invoke();
    }
    /// <summary>오디오/시계/이벤트를 함께 멈춘다. 진행 상태는 유지한다.</summary>
    public void PauseSong()
    {
        if (!IsRunning || IsManuallyPaused) return;
        if (!IsFocusPaused) FreezePlayback();
        IsManuallyPaused = true;
        PlaybackState = "Paused";
    }
    /// <summary>포커스가 있을 때 저장 위치에서 출력 지연을 보정하여 재개한다.</summary>
    public void ResumeSong()
    {
        if (!IsRunning || !IsManuallyPaused) return;
        IsManuallyPaused = false;
        RefreshWindowFocus();
        if (_hasFocus) BeginPlaybackAt(_stoppedTimeSeconds, _resumeAudio);
        else { IsFocusPaused = true; PlaybackState = "Focus Paused"; }
    }
    private void RefreshWindowFocus()
    {
        if (_focusWindow != null && GodotObject.IsInstanceValid(_focusWindow))
            _hasFocus = _focusWindow.HasFocus();
    }
    private void OnWindowFocusEntered() => _Notification((int)NotificationApplicationFocusIn);
    private void OnWindowFocusExited() => _Notification((int)NotificationApplicationFocusOut);
    public override void _ExitTree()
    {
        if (_focusWindow == null || !GodotObject.IsInstanceValid(_focusWindow)) return;
        _focusWindow.FocusEntered -= OnWindowFocusEntered;
        _focusWindow.FocusExited -= OnWindowFocusExited;
        _focusWindow = null;
    }
    /// <summary>포커스 복귀는 수동 일시정지를 해제하지 않는다.</summary>
    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut)
        {
            _hasFocus = false;
            if (!IsRunning || IsFocusPaused) return;
            if (!IsManuallyPaused) FreezePlayback();
            IsFocusPaused = true;
            PlaybackState = IsManuallyPaused ? "Paused" : "Focus Paused";
            Log("Focus Paused");
        }
        else if (what == NotificationApplicationFocusIn)
        {
            _hasFocus = true;
            if (!IsRunning || !IsFocusPaused) return;
            IsFocusPaused = false;
            if (!IsManuallyPaused) BeginPlaybackAt(_stoppedTimeSeconds, _resumeAudio);
            Log("Focus Resumed");
        }
    }
    private static bool IsSupportedMusicStream(AudioStream stream) => stream switch
    {
        AudioStreamWav wav => wav.LoopMode == AudioStreamWav.LoopModeEnum.Disabled
            && (wav.Format == AudioStreamWav.FormatEnum.Format8Bits || wav.Format == AudioStreamWav.FormatEnum.Format16Bits),
        AudioStreamOggVorbis ogg => !ogg.Loop,
        AudioStreamMP3 mp3 => !mp3.Loop,
        _ => false
    };
    public double LastPlaybackRequestMilliseconds { get; private set; }
    private void BeginPlaybackAt(double songTimeSeconds, bool playAudio)
    {
        _clockBaseSeconds = songTimeSeconds;
        // 재개도 새로운 출력 요청이다. freeze 위치에서 delay 동안 기다린 뒤 clock이 진행한다.
        // 일반 프레임에서는 output latency를 다시 조회하지 않는다.
        _startTimeUsec = Time.GetTicksUsec();
        _audioStartDelaySeconds = playAudio ? AudioServer.GetTimeToNextMix() + AudioServer.GetOutputLatency() : 0;
        if (playAudio){ulong began=Time.GetTicksUsec();MusicPlayer!.Play((float)songTimeSeconds);LastPlaybackRequestMilliseconds=(Time.GetTicksUsec()-began)/1000d;}
        AudioPlaybackStarted?.Invoke(songTimeSeconds);
        IsFocusPaused = false;
        PlaybackState = _silentPreview ? "Silent Preview" : "Playing";
    }
    /// <summary>물리 tick과 무관하게 Cue/Section 및 미입력 timeout을 진행한다.</summary>
    public override void _Process(double delta)
    {
        if (!CanAdvance) return;
        int runVersion = _runVersion;
        double songTimeSeconds = SongTimeSeconds;
        double through=Math.Min(songTimeSeconds,AppliedSongEndTimeSeconds);
        AdvanceEvents(through, JudgmentTimeAt(through));
        if (runVersion != _runVersion || !CanAdvance) return;
        if (songTimeSeconds >= AppliedSongEndTimeSeconds)
        {
            // A target just before EOF still contributes a result; never delay the stage by
            // its late-input window or discard it when the player did not bite.
            while(_nextInputTargetIndex<_inputTargetIndices.Count)
            {
                int index=_inputTargetIndices[_nextInputTargetIndex];
                Resolve(index,RhythmJudgment.Miss,JudgmentTimeAt(AppliedSongEndTimeSeconds)-EventTimeSeconds(index));
                if(runVersion!=_runVersion||!CanAdvance)return;
            }
            StopSong();
            _stoppedTimeSeconds=AppliedSongEndTimeSeconds;
            PlaybackState = "Completed";
            Log("Song Finished");
            SongFinished?.Invoke();
        }
    }
    /// <summary>_Input에서 즉시 호출한다. 한 요청은 hittable InputTarget 하나만 소비한다(MISS 포함).</summary>
    public bool TryJudgeInput()
    {
        if (!CanAdvance || SongTimeSeconds>=AppliedSongEndTimeSeconds) return false;
        // 다른 event callback이나 physics tick보다 먼저 입력 시점의 authoritative clock을 읽는다.
        int runVersion = _runVersion;
        double songTimeSeconds = SongTimeSeconds;
        double judgmentTimeSeconds = JudgmentTimeAt(songTimeSeconds);
        AdvanceEvents(songTimeSeconds, judgmentTimeSeconds);
        if (runVersion != _runVersion || !CanAdvance || _nextInputTargetIndex >= _inputTargetIndices.Count) return false;
        // Closest ownership includes consumed targets: repeated presses cannot spill into a
        // neighbouring target while the consumed target still owns this moment. Ties prefer earlier.
        int eventIndex = -1;
        double distance = double.PositiveInfinity;
        foreach (int index in _inputTargetIndices)
        {
            double error = judgmentTimeSeconds - EventTimeSeconds(index);
            if (error < -AppliedEarlyCaptureSeconds) break;
            if (error > AppliedLateCaptureSeconds) continue;
            if (Math.Abs(error) < distance) { distance = Math.Abs(error); eventIndex = index; }
        }
        if (eventIndex < 0 || _targetStates[eventIndex] != TargetState.Waiting) return false;
        _targetStates[eventIndex] = TargetState.Attempted;
        double timingErrorSeconds = judgmentTimeSeconds - EventTimeSeconds(eventIndex);
        Resolve(eventIndex, AppliedJudgmentWindows.Classify(timingErrorSeconds), timingErrorSeconds);
        return true;
    }
    /// <summary>Gameplay-only commitment. A presenting prey owns its first input even before capture.
    /// Calibration retains TryJudgeInput's existing capture semantics and judgment windows.</summary>
    public bool TryCommitPrey(string cueId)
    {
        if (!CanAdvance || SongTimeSeconds>=AppliedSongEndTimeSeconds) return false;
        // Midpoints partition ownership, including consumed targets: spam cannot repair
        // a committed result or steal a later rapid hit until that hit owns the input time.
        double now=JudgmentTimeSeconds;
        int index=-1;double closest=double.PositiveInfinity;
        foreach(int candidate in _inputTargetIndices)
        {
            if(_events[candidate].CueId!=cueId||_targetStates[candidate]!=TargetState.Waiting)continue;
            if(_events[candidate].TargetOrdinal>0&&now<EventTimeSeconds(candidate)-AppliedEarlyCaptureSeconds)continue;
            double distance=Math.Abs(now-EventTimeSeconds(candidate));
            if(distance<closest){closest=distance;index=candidate;}
        }
        if(index<0)return false;
        bool waiting=_targetStates[index]==TargetState.Waiting;
        int version=_runVersion;
        double songTime=SongTimeSeconds,judgmentTime=JudgmentTimeAt(songTime);
        AdvanceEvents(songTime,judgmentTime);
        if(version!=_runVersion)return waiting;
        // A timeout resolved during this very input already accounts for that late attempt.
        if(_targetStates[index]==TargetState.Resolved)return waiting;
        if(!CanAdvance||!waiting)return false;
        _targetStates[index]=TargetState.Attempted;
        double error=judgmentTime-EventTimeSeconds(index);
        Resolve(index,AppliedJudgmentWindows.Classify(error),error);
        return true;
    }
    /// <summary>False until paused/resumed audio is audible; gameplay input shares the clock gate.</summary>
    public bool IsClockAdvancing => CanAdvance;
    private bool CanAdvance => IsRunning && !IsFocusPaused && !IsManuallyPaused
        && (Time.GetTicksUsec() - _startTimeUsec) / 1_000_000.0 >= _audioStartDelaySeconds;
    private double JudgmentTimeAt(double songTimeSeconds) => songTimeSeconds + AppliedUserOffsetSeconds;
    private static bool IsInputTarget(RhythmEventData target) =>
        target.EventType == RhythmEventType.InputTarget && target.IsHittable;
    private double EventTimeSeconds(int index) => _events[index].TimeSeconds + _songOffsetSeconds;
    private void AdvanceEvents(double songTimeSeconds, double judgmentTimeSeconds)
    {
        int runVersion = _runVersion;
        // 독립 입력 cursor 덕분에 아직 resolve되지 않은 target이 뒤의 Cue/Section을 막지 않는다.
        // Cue/Section에는 user offset을 적용하지 않아 음악 timeline에서 이동하지 않는다.
        while (_timelineEventIndex < _events.Count && EventTimeSeconds(_timelineEventIndex) <= songTimeSeconds)
        {
            int index = _timelineEventIndex++;
            var target = _events[index];
            if (!IsInputTarget(target) && !_processed[index])
            {
                MarkProcessed(index);
                if (target.EventType == RhythmEventType.Cue) CueTriggered?.Invoke(target);
                else if (target.EventType == RhythmEventType.Section)
                {
                    CurrentSection = target.SectionId;
                    Log("Section: " + CurrentSection);
                    SectionChanged?.Invoke(target);
                }
                if (runVersion != _runVersion || !CanAdvance) return;
            }
        }
        while (_nextInputTargetIndex < _inputTargetIndices.Count)
        {
            int index = _inputTargetIndices[_nextInputTargetIndex];
            // Late capture 경계는 inclusive. 오차는 judgment - target이므로 음수 Early, 양수 Late.
            // 입력/timeout은 같은 offset 기준이고 처리 상태를 callback 전에 기록한다.
            if (judgmentTimeSeconds - EventTimeSeconds(index) <= AppliedLateCaptureSeconds
                && songTimeSeconds < _events[index].ResolveDeadlineSeconds) break;
            Resolve(index, RhythmJudgment.Miss, judgmentTimeSeconds - EventTimeSeconds(index));
            if (runVersion != _runVersion || !CanAdvance) return;
        }
    }
    private void MarkProcessed(int index)
    {
        _processed[index] = true;
        while (CurrentEventIndex < _processed.Length && _processed[CurrentEventIndex]) CurrentEventIndex++;
    }
    private void Resolve(int eventIndex, RhythmJudgment judgment, double timingErrorSeconds)
    {
        if (_processed[eventIndex] || !IsInputTarget(_events[eventIndex])) return;
        var result = new RhythmJudgmentResult(judgment, timingErrorSeconds, _events[eventIndex]);
        MarkProcessed(eventIndex);
        _targetStates[eventIndex] = TargetState.Resolved;
        while (_nextInputTargetIndex < _inputTargetIndices.Count && _processed[_inputTargetIndices[_nextInputTargetIndex]]) _nextInputTargetIndex++;
        LastResult = result;
        Log($"{judgment} {timingErrorSeconds * 1000:+0.0;-0.0;0} ms");
        JudgmentResolved?.Invoke(result);
    }
    private void Log(string message) { if (Gamejam2.Startup.BuildFeatures.DevelopmentEnabled && EnableDebugLogging) GD.Print("[Rhythm] " + message); }
    private bool Fail(string reason)
    { PlaybackState = "Error"; GD.PushError("RhythmController: " + reason); return false; }
}
