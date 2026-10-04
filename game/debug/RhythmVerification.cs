using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Audio;
using Gamejam2.Calibration;
using Gamejam2.Rhythm;
using Gamejam2.Startup;
using Gamejam2.Title;
using Gamejam2.Settings;
using Gamejam2.Lobby;
using Gamejam2.Save;
namespace Gamejam2.Debugging;
/// <summary>정상 화면과 분리된 최소 검증. 실제 controller/input 경로를 사용하며 타임스탬프를 주입하지 않는다.</summary>
public partial class RhythmVerification : Node
{
    private RhythmController _rhythm = null!;
    private AudioStreamPlayer _music = null!;
    private Label _status = null!;
    private int _checks, _results, _inputs;
    private bool _automated, _testingFocusLoss;
    public override void _Ready()
    {
        _rhythm = GetNode<RhythmController>("RhythmController");
        _music = GetNode<AudioStreamPlayer>("MusicPlayer");
        _status = GetNode<Label>("Display/Status");
        GetNode<RhythmDebugOverlay>("RhythmDebugOverlay").Initialize(_rhythm);
        _rhythm.JudgmentResolved += _ => _results++;
        _automated = OS.GetCmdlineUserArgs().Contains("--verify");
        if (_automated) Callable.From(() => { _ = RunChecks(); }).CallDeferred();
        else StartChart(0, Target(2), Target(3), Target(4));
    }
    public override void _Input(InputEvent input)
    {
        if (!input.IsEcho() && input.IsActionPressed("rhythm_input"))
        { _inputs++; _rhythm.TryJudgeInput(); GetViewport().SetInputAsHandled(); }
        if (!_automated && input is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.R })
            StartChart(0, Target(2), Target(3), Target(4));
    }
    public override void _Process(double delta)
    { if(_automated&&!_testingFocusLoss&&_rhythm.IsFocusPaused)GetWindow().GrabFocus();
        _status.Text = _automated ? $"개발 전용 자동 검증 · 통과 {_checks}개" : "개발 전용 리듬 검증 · Space 입력 · R 재시작\n목표: 2초 / 3초 / 4초"; }
    private static RhythmEventData Target(double time) => new() { TimeSeconds = time, EventType = RhythmEventType.InputTarget };
    private void StartChart(double offset, params RhythmEventData[] events)
    {
        _rhythm.StopSong();
        var chart = new RhythmChart(); foreach (var item in events) chart.Events.Add(item);
        _rhythm.Chart = chart; _rhythm.UserOffsetSeconds = offset;
        _music.Stream = MetronomeTrack.CreateTrack(new double[] { 0, .5, 1, 1.5, 2, 2.5, 3, 3.5, 4 }, 5);
        Check(_rhythm.StartSong(), "PCM 차트 시작");
    }
    private void Check(bool passed, string name)
    { if (!passed) throw new InvalidOperationException(name); _checks++; GD.Print($"PASS {name}"); }
    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private async Task Until(Func<bool> condition, double timeout = 15)
    {
        ulong deadline = Time.GetTicksMsec() + (ulong)(timeout * 1000);
        while (!condition()) { if (Time.GetTicksMsec() > deadline) throw new TimeoutException("검증 대기 시간 초과"); await Frame(); }
    }
    private Task At(double songTime) => Until(() => _rhythm.SongTimeSeconds >= songTime);
    private void Press()
    {
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Space, Pressed = true });
        Input.FlushBufferedEvents();
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Space, Pressed = false });
        Input.FlushBufferedEvents();
    }
    private async Task RunChecks()
    {
        try
        {
            GetWindow().GrabFocus();
            var windows = _rhythm.JudgmentWindows.CreateSnapshot();
            foreach (double sign in new double[] { -1, 1 })
            {
                Check(windows.Classify(sign * windows.PerfectSeconds) == RhythmJudgment.Perfect, "Perfect 포함 경계");
                Check(windows.Classify(sign * (windows.PerfectSeconds + .000001)) == RhythmJudgment.Good, "Perfect 밖 Good");
                Check(windows.Classify(sign * windows.GoodSeconds) == RhythmJudgment.Good, "Good 포함 경계");
                Check(windows.Classify(sign * (windows.GoodSeconds + .000001)) == RhythmJudgment.Bad, "Good 밖 Bad");
                Check(windows.Classify(sign * windows.BadSeconds) == RhythmJudgment.Bad, "Bad 포함 경계");
                Check(windows.Classify(sign * (windows.BadSeconds + .000001)) == RhythmJudgment.Miss, "Bad 밖 Miss");
            }
            Check(windows.Classify(0) == RhythmJudgment.Perfect, "0ms Perfect");
            int cues = 0, sections = 0;
            _rhythm.CueTriggered += _ => cues++; _rhythm.SectionChanged += _ => sections++;
            _results = 0;
            StartChart(0, new() { TimeSeconds = .2, EventType = RhythmEventType.Cue },
                new() { TimeSeconds = .4, EventType = RhythmEventType.Section, SectionId = "검증" }, Target(1), Target(1));
            await At(.2); Press(); Check(_results == 0, "Cue 입력으로 판정되지 않음");
            await At(.4); Press(); Check(_results == 0 && _rhythm.CurrentSection == "검증", "Section 입력으로 판정되지 않음");
            await At(.99); int inputs = _inputs; Press();
            Check(_inputs == inputs + 1 && _results == 1 && _rhythm.LastResult?.Judgment == RhythmJudgment.Perfect, "입력 timestamp 및 한 press 한 판정");
            Check(cues == 1 && sections == 1, "시간 이벤트 각각 한 번");
            await At(1.27); Check(_results == 2 && _rhythm.LastResult?.Judgment == RhythmJudgment.Miss, "자동 Miss 한 번");
            await At(1.45); Check(_results == 2, "중복 resolve 없음");
            Check(_rhythm.StartSong() && _rhythm.LastResult == null && _rhythm.CurrentEventIndex == 0 && _rhythm.CurrentSection == "", "재시작 상태 초기화");
            await At(.3);
            double audible = _music.GetPlaybackPosition() + AudioServer.GetTimeSinceLastMix() - AudioServer.GetOutputLatency();
            double difference = Math.Abs(audible - _rhythm.SongTimeSeconds);
            GD.Print($"AUDIO clock/cursor difference {difference * 1000:F2}ms");
            Check(difference < .05 && _music.Playing, "PCM 오디오와 Song Clock (50ms 이내)");
            _testingFocusLoss=true;_rhythm.Notification((int)NotificationApplicationFocusOut);
            double paused = _rhythm.SongTimeSeconds; int index = _rhythm.CurrentEventIndex;
            for (int frame = 0; frame < 15; frame++) await Frame();
            Check(_rhythm.SongTimeSeconds == paused && !_music.Playing && index == _rhythm.CurrentEventIndex, "focus loss 동시 정지");
            _rhythm.Notification((int)NotificationApplicationFocusIn);_testingFocusLoss=false; await At(paused + .1);
            Check(_music.Playing && !_rhythm.IsFocusPaused, "focus resume 동일 위치 재개");
            StartChart(0, Target(.5)); await At(.4); Press();
            Check(_rhythm.LastResult is { TimingErrorSeconds: < 0, Judgment: RhythmJudgment.Good }, "Early 음수");
            StartChart(0, Target(.5)); await At(.6); Press();
            Check(_rhythm.LastResult is { TimingErrorSeconds: > 0, Judgment: RhythmJudgment.Good }, "Late 양수");
            CalibrationSession.UserInputOffsetSeconds = .1;
            StartChart(CalibrationSession.UserInputOffsetSeconds, Target(.5)); await At(.5); Press();
            Check(_rhythm.LastResult is { TimingErrorSeconds: > .09, Judgment: RhythmJudgment.Good }
                && _rhythm.AppliedUserOffsetSeconds == .1, "세션 양수 입력 offset Late 방향 적용");
            _rhythm.StopSong();
            var calibration = new RhythmCalibration(); AddChild(calibration); calibration.Initialize(_rhythm, _music);
            calibration.BeginAudioCalibration();
            for (int tap = 0; tap < RhythmCalibration.AudioTapCount; tap++)
            { await At(RhythmCalibration.FirstTapSeconds + tap * RhythmCalibration.BeatSeconds + .042); Check(calibration.Tap(), "Song Clock 보정 표본 수집"); Check(!calibration.Tap(), "동일 박 중복 제외"); }
            await Until(() => calibration.Stage == CalibrationStage.AudioResult);
            Check(calibration.Summary.Count == 12 && calibration.SuggestedInputOffsetSeconds < -.04 && calibration.SuggestedInputOffsetSeconds > -.08, "12표본 median 음수 보정 제안");
            GD.Print($"CALIBRATION raw median {calibration.Summary.MedianSeconds * 1000:F2}ms");
            double suggestion = calibration.SuggestedInputOffsetSeconds;
            calibration.ApplySuggestionAndFinish();
            Check(CalibrationSession.UserInputOffsetSeconds == suggestion && _rhythm.UserOffsetSeconds == suggestion
                && calibration.Stage == CalibrationStage.Complete, "적용하고 시작은 세션/판정 설정에 전달");
            Check(_rhythm.StartSong() && _rhythm.AppliedUserOffsetSeconds == suggestion, "다음 판정 실행에 보정 snapshot 적용");
            _rhythm.StopSong();
            calibration.ApplySuggestionAndBeginVisual();
            calibration.SetVisualOffset(.15); await At(.15);
            Check(calibration.IsVisualFlashOn && _rhythm.AppliedUserOffsetSeconds == 0, "화면 offset은 표시만 이동");
            calibration.BeginConfirmation();
            double applied = _rhythm.AppliedUserOffsetSeconds;
            for (int tap = 0; tap < RhythmCalibration.ConfirmationTapCount; tap++)
            { await At(RhythmCalibration.FirstTapSeconds + tap * RhythmCalibration.BeatSeconds - applied); Check(calibration.Tap(), "확인 입력 수집"); }
            await Until(() => calibration.Stage == CalibrationStage.ConfirmationResult);
            Check(calibration.Summary.Count == 8 && calibration.Summary.Early + calibration.Summary.Late <= 8
                && Math.Abs(calibration.Summary.MedianSeconds) < .025 && calibration.Summary.MeanAbsoluteSeconds < .025, "보정 확인 통계와 판정 방향");
            GD.Print($"CONFIRM median={calibration.Summary.MedianSeconds * 1000:F2}ms MAE={calibration.Summary.MeanAbsoluteSeconds * 1000:F2}ms Early={calibration.Summary.Early} Late={calibration.Summary.Late}");
            calibration.ResetOffsets(); Check(CalibrationSession.UserInputOffsetSeconds == 0 && CalibrationSession.VisualOffsetSeconds == 0, "두 offset 초기화");
            calibration.SkipWithDefaults(); Check(calibration.Stage == CalibrationStage.Complete && CalibrationSession.HasChoice, "보정 건너뛰기/완료");
            calibration.QueueFree(); await Frame();
            var startup = GD.Load<PackedScene>("res://game/startup/Startup.tscn").Instantiate<SceneRouter>(); startup.SavePath = "/private/tmp/rhythm_verification_player_shell.cfg"; if (FileAccess.FileExists(startup.SavePath)) DirAccess.RemoveAbsolute(startup.SavePath); AddChild(startup); await Frame();
            Check(startup.ScreenHost!.GetChild(0) is TitleScreen && !startup.Settings!.Visible, "정상 시작 Title / debug 미노출");
            startup.Settings!.Open(); Check(startup.Settings.Visible, "설정 열기"); startup.Settings.Close();
            startup.GoToCalibration(CalibrationReason.Settings); await Frame();
            var screen = startup.ScreenHost.GetChild<CalibrationScreen>(0);
            Check(screen.Flow!.Calibration!.Stage == CalibrationStage.Entry, "Title에서 보정 진입");
            screen.Flow.Calibration.SkipWithDefaults(); await Frame(); await Frame();
            Check(startup.ScreenHost.GetChild(0) is TitleScreen && startup.Settings.Visible, "설정 문맥 보정 후 Title/설정 복귀");
            startup.GoToLobby(); await Frame();
            var lobby = startup.ScreenHost.GetChild<LobbyScreen>(0);
            var cards = lobby.Cards.GetChildren().OfType<StageCard>().ToArray();
            Check(!cards[0].LockOverlay.Visible && cards[1].LockOverlay.Visible, "스테이지 데이터 공통 해금 바인딩");
            ProgressService.UnlockStage("stage_2");
            Check(!cards[1].LockOverlay.Visible && cards[2].LockOverlay.Visible, "향후 해금 API와 카드 갱신");
            startup.QueueFree(); await Frame();
            GD.Print($"RHYTHM FOUNDATION VERIFIED: {_checks} checks"); GetTree().Quit(0);
        }
        catch (Exception exception) { GD.PushError($"FOUNDATION CHECK FAILED: {exception}"); GetTree().Quit(1); }
    }
}
