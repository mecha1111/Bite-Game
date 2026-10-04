using System;
using Godot;
using Gamejam2.Rhythm;
using Gamejam2.Settings;
namespace Gamejam2.Calibration;
/// <summary>보정 UI/입력만 조합한다. 호출자가 제공하는 동일 controller/music을 사용한다.</summary>
public partial class CalibrationFlow : Node
{
    [Export] public RhythmCalibration? Calibration { get; set; }
    [Export] public CalibrationHud? Hud { get; set; }
    public event Action? Completed;
    private bool _initialized;
    /// <summary>composition root가 자신의 rhythm과 music을 전달하며 완료 후 화면 전환을 결정한다.</summary>
    public void Initialize(RhythmController rhythm, AudioStreamPlayer music, bool force = false)
    {
        if (Calibration == null || Hud == null)
        throw new InvalidOperationException("Calibration/Hud missing: res://game/calibration/Calibration.tscn / res://game/calibration/calibration_flow.tscn");
        Calibration.Initialize(rhythm, music);
        Calibration.Completed += OnCompleted;
        Hud.Initialize(Calibration);
        _initialized = true;
        if (CalibrationSession.HasChoice && !force) Calibration.Finish();
        else Calibration.ShowEntry();
    }
    private void OnCompleted() => Completed?.Invoke();
    public override void _UnhandledInput(InputEvent @event)
    {
        if (_initialized && Calibration!.Stage != CalibrationStage.Complete
            && !@event.IsEcho() && @event.IsActionPressed(SettingsService.RhythmInputAction))
        { Calibration.Tap(); GetViewport().SetInputAsHandled(); }
    }
    public override void _ExitTree()
    { if (_initialized) Calibration!.Completed -= OnCompleted; }
}
