using Godot;
namespace Gamejam2.Rhythm;
/// <summary>Input ownership, independent of successful judgment thresholds.</summary>
[GlobalClass]
public partial class InputCaptureWindows : Resource
{
    [Export] public double EarlySeconds { get; set; }
    [Export] public double LateSeconds { get; set; }
}
