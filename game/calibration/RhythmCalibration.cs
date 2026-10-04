using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Gamejam2.Rhythm;
using Gamejam2.Audio;
namespace Gamejam2.Calibration;
public enum CalibrationStage { Entry, AudioSampling, AudioResult, Visual, Confirmation, ConfirmationResult, Complete, CountIn }
/// <summary>입력 통계. 누락 입력은 인위적인 타이밍 오차로 통계에 넣지 않는다.</summary>
public readonly record struct CalibrationSummary(int Count, int Early, int Late, double MedianSeconds,
    double MeanSeconds, double MeanAbsoluteSeconds);
/// <summary>보정 시퀀스만 관리한다. 입력 시점/flash는 주입된 동일 RhythmController를 읽는다.</summary>
public partial class RhythmCalibration : Node
{
    public const double CalibrationBpm = 120;
    public const double BeatSeconds = 60 / CalibrationBpm;
    public const double FirstTapSeconds = 2;
    public const int AudioTapCount = 12;
    public const int ConfirmationTapCount = 8;
    private const double TapCaptureSeconds = 0.225;
    private RhythmController _rhythm = null!;
    private AudioStreamPlayer _music = null!;
    private readonly Dictionary<int, double> _samples = new();
    private bool _initialized;
    private int _audioSampleBeatBase;
    public CalibrationStage Stage { get; private set; } = CalibrationStage.Entry;
    public IReadOnlyCollection<double> Samples => _samples.Values;
    public CalibrationSummary Summary { get; private set; }
    public double SuggestedInputOffsetSeconds { get; private set; }
    public bool HasReliableEstimate => Summary.Count >= 8;
    /// <summary>이미 수락된 signed 오차를 표현 UI로 전달한다. UI는 시각에서 오차를 재계산하지 않는다.</summary>
    public event Action<double>? SampleAccepted;
    /// <summary>표현의 수명/피드백도 같은 Song Clock을 읽는다.</summary>
    public double SampleTimelineSeconds => _rhythm.SongTimeSeconds;
    public RhythmJudgmentWindows ReferenceWindows => _rhythm.JudgmentWindows;
    public event Action? StageChanged;
    public event Action? Completed;
    /// <summary>composition root에서 의존성을 전달한다. 별도 Song Clock은 만들지 않는다.</summary>
    public void Initialize(RhythmController rhythm, AudioStreamPlayer music)
    {
        _rhythm = rhythm; _music = music; _initialized = true;
        rhythm.SongFinished += OnSongFinished;
    }
    public void ShowEntry()
    {
        _rhythm.StopSong(); _samples.Clear(); SetStage(CalibrationStage.Entry);
    }
    public void BeginAudioCalibration()
    {
        _samples.Clear(); Summary = default; _audioSampleBeatBase = 0;
        StartTrack(CalibrationStage.CountIn, AudioTapCount, false);
    }
    /// <summary>준비와 측정은 같은 실행의 Song Clock에서 이어지며 재생을 다시 시작하지 않는다.</summary>
    private void AdvanceCountIn()
    {
        if (Stage == CalibrationStage.CountIn && _rhythm.IsRunning
            && !_rhythm.IsFocusPaused && !_rhythm.IsManuallyPaused
            && _rhythm.SongTimeSeconds >= FirstTapSeconds)
            SetStage(CalibrationStage.AudioSampling);
    }
    public override void _Process(double delta) { if (_initialized) AdvanceCountIn(); }
    /// <summary>1~4 준비 표시. 같은 clock의 마지막 준비 박부터 측정까지 0.5초.</summary>
    public int CountInBeatNumber => Math.Clamp((int)(_rhythm.SongTimeSeconds / BeatSeconds) + 1, 1, 4);
    public void ApplySuggestionAndBeginVisual()
    {
        if (!HasReliableEstimate) return;
        CalibrationSession.UserInputOffsetSeconds = SuggestedInputOffsetSeconds;
        BeginVisualCalibration();
    }
    /// <summary>제안값을 세션 및 다음 판정 실행에 전달한다. 화면 offset은 유지한다.</summary>
    public void ApplySuggestionAndFinish()
    {
        if (!HasReliableEstimate || _samples.Count != AudioTapCount) return;
        CalibrationSession.UserInputOffsetSeconds = SuggestedInputOffsetSeconds;
        _rhythm.UserOffsetSeconds = SuggestedInputOffsetSeconds;
        Finish();
    }
    /// <summary>읽기 전용 표시 위상. 시각 offset만 사용하고 판정 시계를 변경하지 않는다.</summary>
    public double? PresentationBeatPhase => Stage is CalibrationStage.CountIn or CalibrationStage.AudioSampling or CalibrationStage.Visual or CalibrationStage.Confirmation
        && _rhythm.IsRunning ? Math.Max(0, _rhythm.SongTimeSeconds - CalibrationSession.VisualOffsetSeconds) / BeatSeconds % 1 : null;
    public void SkipAudioWithDefaults(){CalibrationSession.UserInputOffsetSeconds=0;BeginVisualCalibration();}
    public void SkipVisualWithDefaults()
    {
        if (Stage != CalibrationStage.Visual) return;
        CalibrationSession.VisualOffsetSeconds = 0;
        Finish();
    }
    public void BeginVisualCalibration() => StartTrack(CalibrationStage.Visual, 60, false);
    public void BeginConfirmation()
    {
        _samples.Clear(); Summary = default;
        StartTrack(CalibrationStage.Confirmation, ConfirmationTapCount, true);
    }
    public void SetVisualOffset(double seconds)
    {
        if (!double.IsFinite(seconds)) return;
        CalibrationSession.VisualOffsetSeconds = Math.Clamp(seconds, -0.2, 0.2);
    }
    public void ResetOffsets()
    {
        CalibrationSession.ResetOffsets();
        if (Stage == CalibrationStage.Confirmation) BeginConfirmation();
        StageChanged?.Invoke();
    }
    /// <summary>기본값 선택도 세션 선택으로 기록하므로 Scene 재진입 때 강제 보정하지 않는다.</summary>
    public void SkipWithDefaults() { ResetOffsets(); Finish(); }
    public void Finish()
    {
        _rhythm.StopSong(); CalibrationSession.HasChoice = true;
        SetStage(CalibrationStage.Complete); Completed?.Invoke();
    }
    private void StartTrack(CalibrationStage stage, int tapCount, bool targets)
    {
        _rhythm.StopSong();
        var chart = new RhythmChart { Bpm = CalibrationBpm };
        var beats = new List<double>();
        int totalBeats = 4 + tapCount;
        for (int beat = 0; beat < totalBeats; beat++) {
            double time = beat * BeatSeconds; beats.Add(time);
            chart.Events.Add(new RhythmEventData { TimeSeconds = time, EventType = RhythmEventType.Cue, CueId = "calibration-click" });
            if (targets && beat >= 4)
                chart.Events.Add(new RhythmEventData { TimeSeconds = time, EventType = RhythmEventType.InputTarget, IsHittable = true });
        }
        double duration = totalBeats * BeatSeconds + _rhythm.JudgmentWindows.BadSeconds + 0.25;
        _music.Stream = MetronomeTrack.CreateTrack(beats, duration);
        _rhythm.Chart = chart;
        _rhythm.UserOffsetSeconds = targets ? CalibrationSession.UserInputOffsetSeconds : 0;
        SetStage(stage);
        if (!_rhythm.StartSong()) throw new InvalidOperationException("보정용 metronome 시작 실패");
        GD.Print($"[CALIBRATION] Stage={stage} Playback={_rhythm.PlaybackState} WindowFocus={GetWindow().HasFocus()} FocusPaused={_rhythm.IsFocusPaused} ManualPaused={_rhythm.IsManuallyPaused} AudioPlaying={_music.Playing}");
    }
    /// <summary>예상 beat와 raw SongTime을 대응시킨다. 한 beat당 한 표본, echo는 상위 입력에서 제외한다.</summary>
    public bool Tap()
    {
        AdvanceCountIn();
        if (Stage is not (CalibrationStage.AudioSampling or CalibrationStage.Confirmation)
            || !_rhythm.IsRunning || _rhythm.IsFocusPaused || _rhythm.IsManuallyPaused) return false;
        double songTime = _rhythm.SongTimeSeconds;
        int index = (int)Math.Round((songTime - FirstTapSeconds) / BeatSeconds);
        int limit = Stage == CalibrationStage.AudioSampling ? AudioTapCount : ConfirmationTapCount;
        double expected = FirstTapSeconds + index * BeatSeconds;
        int sampleKey = index + (Stage == CalibrationStage.AudioSampling ? _audioSampleBeatBase : 0);
        if (index < 0 || index >= limit || _samples.ContainsKey(sampleKey) || Math.Abs(songTime - expected) > TapCaptureSeconds) return false;
        // audio 측정은 보정 전 raw 오차. 확인은 코어가 한 번 보정한 judgment time을 읽는다.
        double error = Stage == CalibrationStage.AudioSampling ? songTime - expected : _rhythm.JudgmentTimeSeconds - expected;
        _samples.Add(sampleKey, error);
        if (Stage == CalibrationStage.Confirmation) _rhythm.TryJudgeInput();
        // Presentation callbacks must not delay confirmation input judgment.
        SampleAccepted?.Invoke(error);
        if (Stage == CalibrationStage.AudioSampling && _samples.Count == AudioTapCount)
        {
            _rhythm.StopSong(); CompleteAudioMeasurement();
        }
        else StageChanged?.Invoke();
        return true;
    }
    /// <summary>positive visual offset은 flash를 늦춘다. 판단/타겟/오디오에는 전달하지 않는다.</summary>
    public bool IsVisualFlashOn
    {
        get {
            if (Stage != CalibrationStage.Visual || !_rhythm.IsRunning) return false;
            double presentationTime = _rhythm.SongTimeSeconds - CalibrationSession.VisualOffsetSeconds;
            if (presentationTime < 0) return false;
            double distance = presentationTime - Math.Round(presentationTime / BeatSeconds) * BeatSeconds;
            return Math.Abs(distance) < 0.055;
        }
    }
    private void OnSongFinished()
    {
        if (Stage is CalibrationStage.AudioSampling or CalibrationStage.CountIn) {
            // 누락 입력을 실패로 끝내지 않는다. 새 오디오 구간에서도 기존 유효 표본을 유지한다.
            _audioSampleBeatBase += AudioTapCount;
            StartTrack(CalibrationStage.CountIn, AudioTapCount, false);
        } else if (Stage == CalibrationStage.Visual) BeginVisualCalibration();
        else if (Stage == CalibrationStage.Confirmation) {
            Summary = Summarize(_samples.Values, false); SetStage(CalibrationStage.ConfirmationResult);
        }
    }
    private void CompleteAudioMeasurement()
    {
        Summary = Summarize(_samples.Values, true);
        SuggestedInputOffsetSeconds = -Summary.MedianSeconds;
        SetStage(CalibrationStage.AudioResult);
    }
    public bool HasStrongBias => Summary.Count >= 4 && Math.Abs(Summary.MedianSeconds) > _rhythm.JudgmentWindows.PerfectSeconds
        && Math.Max(Summary.Early, Summary.Late) >= Math.Ceiling(Summary.Count * 0.8);
    /// <summary>median/MAD로 audio 이상값을 제외한다. 확인 통계는 실제 입력값을 모두 사용한다.</summary>
    public static CalibrationSummary Summarize(IEnumerable<double> samples, bool rejectOutliers)
    {
        var values = samples.Where(double.IsFinite).OrderBy(value => value).ToList();
        if (values.Count == 0) return default;
        if (rejectOutliers && values.Count >= 8) {
            double median = Median(values);
            var deviations = values.Select(value => Math.Abs(value - median)).OrderBy(value => value).ToList();
            double threshold = Math.Max(0.040, 3 * Median(deviations));
            values = values.Where(value => Math.Abs(value - median) <= threshold).ToList();
        }
        return new(values.Count, values.Count(value => value < 0), values.Count(value => value > 0),
            Median(values), values.Average(), values.Average(value => Math.Abs(value)));
    }
    private static double Median(List<double> sorted) => sorted.Count % 2 == 1 ? sorted[sorted.Count / 2]
        : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2;
    private void SetStage(CalibrationStage stage) { Stage = stage; StageChanged?.Invoke(); }
    public override void _ExitTree() { if (_initialized) _rhythm.SongFinished -= OnSongFinished; }
}
