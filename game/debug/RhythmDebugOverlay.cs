using Godot;
using Gamejam2.Rhythm;
namespace Gamejam2.Debugging;
/// <summary>controller의 읽기 전용 데이터를 표시한다. 별도 시계를 만들지 않는다.</summary>
public partial class RhythmDebugOverlay : CanvasLayer
{
    /// <summary>자체 Scene에서 연결할 Label.</summary>
    [Export] public Label? DetailsLabel { get; set; }
    private RhythmController? _controller;
    /// <summary>Main에서 명시적으로 전달하는 읽기 대상.</summary>
    public void Initialize(RhythmController controller) => _controller = controller;
    /// <summary>표시 의존성을 검증한다.</summary>
    public override void _Ready()
    { if(!Gamejam2.Startup.BuildFeatures.DevelopmentEnabled){Hide();SetProcess(false);return;}
      if (DetailsLabel == null) GD.PushError("RhythmDebugOverlay: 자체 Scene에서 DetailsLabel을 연결하세요."); }
    /// <summary>초 단위 데이터를 표시할 때만 ms로 변환한다.</summary>
    public override void _Process(double delta)
    {
        if (_controller == null || DetailsLabel == null) return;
        var next = _controller.NextEventTimeSeconds;
        var last = _controller.LastResult;
        string error = last is {} result
            ? $"{(result.TimingErrorSeconds < 0 ? "Early" : "Late")} {System.Math.Abs(result.TimingErrorSeconds) * 1000:F1} ms" : "—";
        var windows = _controller.AppliedJudgmentWindows;
        DetailsLabel.Text = $"Song Time: {_controller.SongTimeSeconds:F3} s\n"
            + $"Next Event Time: {next?.ToString("F3") ?? "—"} s\n"
            + $"Delta To Event: {(next - _controller.SongTimeSeconds)?.ToString("F3") ?? "—"} s\n"
            + $"Last Timing Error: {error}\nLast Judgment: {last?.Judgment.ToString() ?? "—"}\n"
            + $"User Offset: {_controller.AppliedUserOffsetSeconds:F3} s (applied)\n"
            + $"Playback State: {_controller.PlaybackState}\nCurrent Event Index: {_controller.CurrentEventIndex}\n"
            + $"Current Section: {(string.IsNullOrEmpty(_controller.CurrentSection) ? "—" : _controller.CurrentSection)}\n"
            + $"적용 판정 범위: Perfect ±{windows.PerfectSeconds * 1000:0.#} / Good ±{windows.GoodSeconds * 1000:0.#} / Bad ±{windows.BadSeconds * 1000:0.#} ms";
    }
}
