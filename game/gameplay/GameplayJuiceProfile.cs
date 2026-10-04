using Godot;
namespace Gamejam2.Gameplay;
/// <summary>Presentation only. No judgment windows, chart offsets or gameplay rewards.</summary>
[GlobalClass]
public partial class GameplayJuiceProfile : Resource
{
    // Kept at zero: spatial rhythm origins and HUD stay completely stable.
    [Export] public float HitCameraImpulse { get; set; } = 0;
    [Export] public float MissDimStrength { get; set; } = .07f;
    [Export] public float SatietyGaugePulseStrength { get; set; } = .16f;
    [Export] public float InterferenceRefractionStrength { get; set; } = .0008f;
    [Export] public double ExTransitionDuration { get; set; } = .6;
    [Export] public double DirectionCueDuration { get; set; } = .24;
    [Export] public float LowSatietyRatio { get; set; } = .23f;
    [Export] public double PauseEaseSeconds { get; set; } = .25;
}
