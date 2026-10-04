using System;
using Godot;
using Gamejam2.Rhythm;
namespace Gamejam2.Calibration;
/// <summary>실제 보정 화면. Startup이 소유한 기존 clock/music을 전달받는다.</summary>
public partial class CalibrationScreen : Control
{
    [Export] public CalibrationFlow? Flow { get; set; }
    public event Action? Completed;
    public void Initialize(RhythmController rhythm, AudioStreamPlayer music, bool force)
    {
        if (Flow == null) throw new InvalidOperationException("Calibration Flow missing: res://game/calibration/Calibration.tscn");
        Flow.Completed += OnCompleted; Flow.Initialize(rhythm, music, force);
    }
    private void OnCompleted() => Completed?.Invoke();
    public override void _ExitTree() { if (Flow != null) Flow.Completed -= OnCompleted; }
}
