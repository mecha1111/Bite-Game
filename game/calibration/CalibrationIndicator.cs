using System;
using Godot;
namespace Gamejam2.Calibration;
/// <summary>RhythmIndicator.tscn의 texture 상태만 갱신한다. 정적 링/점은 editor에서도 보인다.</summary>
public partial class CalibrationIndicator : Control
{
    [Export] public TextureRect PulseRing { get; set; } = null!;
    [Export] public TextureRect CoreDot { get; set; } = null!;
    public void ShowPhase(double? phase, double inputAgeSeconds = double.PositiveInfinity, bool measuring = true)
    {
        PulseRing.Visible = phase.HasValue;
        if (phase is not {} beat) { CoreDot.Scale = Vector2.One; CoreDot.Modulate = new Color(1, 1, 1, .65f); return; }
        float glow = (float)Math.Exp(-beat * 12);
        float accepted = inputAgeSeconds >= 0 ? (float)Math.Exp(-inputAgeSeconds * 20) : 0;
        CoreDot.Scale = Vector2.One * (1 + glow * .18f - accepted * .12f);
        CoreDot.Modulate = new Color(1, 1, 1, .6f + .4f * Math.Max(glow, accepted));
        PulseRing.Scale = Vector2.One * (0.5f + (float)beat * 1.15f);
        PulseRing.Modulate = new Color(1, 1, 1, (float)Math.Pow(1 - beat, 3) * (measuring ? .9f : .65f));
    }
}
