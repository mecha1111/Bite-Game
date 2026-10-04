using Godot;
namespace Gamejam2.UI;
/// <summary>Scene-authored custom focus indicator without a default focus rectangle.</summary>
public partial class UiFocusFeedback : Node
{
    [Export] public Color Highlight { get; set; } = new(1.12f, 1.22f, 1.25f);
    private Control _control = null!;
    private Color _base;
    private bool _hover, _pressed;
    public override void _Ready()
    {
        _control = GetParent<Control>(); _base = _control.SelfModulate;
        _control.FocusEntered += Update; _control.FocusExited += Update;
        _control.MouseEntered += () => { _hover = true; Update(); };
        _control.MouseExited += () => { _hover = false; Update(); };
        if (_control is BaseButton button)
        {
            button.ButtonDown += () => { _pressed = true; Update(); };
            button.ButtonUp += () => { _pressed = false; Update(); };
        }
        Update();
    }
    private void Update() => _control.SelfModulate = _base * (_pressed ? new Color(.85f,.92f,.94f) : _hover || _control.HasFocus() ? Highlight : Colors.White);
}
