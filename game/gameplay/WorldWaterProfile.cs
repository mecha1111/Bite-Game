using Godot;
namespace Gamejam2.Gameplay;
/// <summary>Foreground water integration; background/art values stay in environments.csv.</summary>
[GlobalClass]
public partial class WorldWaterProfile : Resource
{
    [Export] public float RefractionStrength { get; set; }
    [Export] public float RefractionSpeed { get; set; }
    [Export] public float CausticSpeed { get; set; } = .3f;
    [Export] public float CausticScale { get; set; }
    [Export] public float FogStrength { get; set; }
    [Export] public float Saturation { get; set; }
    [Export] public float SharkTintStrength { get; set; }
    [Export] public float WhiteReduction { get; set; }
}
