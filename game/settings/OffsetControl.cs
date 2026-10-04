using System;
using Godot;
namespace Gamejam2.Settings;
/// <summary>Scene-authored +/- control, expressed in milliseconds. Persistence belongs to SettingsService.</summary>
public partial class OffsetControl : HBoxContainer
{
    [Export] public Button Minus { get; set; } = null!;
    [Export] public Button Plus { get; set; } = null!;
    [Export] public Label Amount { get; set; } = null!;
    [Export] public double Minimum { get; set; } = -500;
    [Export] public double Maximum { get; set; } = 500;
    [Export] public double Step { get; set; } = 1;
    private double _value;
    public event Action<double>? ValueChanged;
    public double Value
    {
        get => _value;
        set
        {
            double next = double.IsFinite(value) ? Math.Clamp(value, Minimum, Maximum) : 0;
            bool changed = next != _value; _value = next;
            Refresh();
            if (changed) ValueChanged?.Invoke(_value);
        }
    }
    public override void _Ready()
    {
        Minus.Pressed += () => Value -= Step;
        Plus.Pressed += () => Value += Step;
        Refresh();
    }
    private void Refresh()
    {
        if (Amount == null || Minus == null || Plus == null) return;
        Amount.Text = $"{_value:+0;-0;0} ms";
        Minus.Disabled = _value <= Minimum; Plus.Disabled = _value >= Maximum;
    }
}
