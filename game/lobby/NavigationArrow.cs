using Godot;
namespace Gamejam2.Lobby;
/// <summary>픽셀 화살표의 한 상태를 hover/focus/press로 표현한다.</summary>
public partial class NavigationArrow : Button
{
    [Export] public TextureRect ArrowVisual { get; set; } = null!;
    [Export] public int Direction { get; set; } = -1;
    [Export] public float HoverScale { get; set; } = 1.1f;
    [Export] public float NudgePixels { get; set; } = 4;
    private Vector2 _basePosition;
    private bool _hovered, _pressed;
    private Tween? _tween;
    public override void _Ready()
    {
        _basePosition = ArrowVisual.Position; ArrowVisual.FlipH = Direction > 0;
        MouseEntered += () => { _hovered = true; Refresh(); };
        MouseExited += () => { _hovered = false; Refresh(); };
        FocusEntered += Refresh; FocusExited += Refresh;
        ButtonDown += () => { _pressed = true; Refresh(); };
        ButtonUp += () => { _pressed = false; Refresh(); };
        Refresh();
    }
    public void Refresh()
    {
        bool active = !Disabled && (_hovered || HasFocus());
        _tween?.Kill(); _tween = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _tween.TweenProperty(this, "scale", Vector2.One * (Disabled ? 1 : _pressed ? .97f : active ? HoverScale : 1), .12);
        _tween.TweenProperty(ArrowVisual, "position", _basePosition + new Vector2(!Disabled && _pressed ? Direction * (NudgePixels + 3) : active ? Direction * NudgePixels : 0, 0), .12);
        _tween.TweenProperty(ArrowVisual, "self_modulate", Disabled ? new Color(1, 1, 1, .22f) : active ? new Color("dfffff") : new Color(.85f, .95f, 1, .85f), .12);
    }
    public override void _ExitTree() => _tween?.Kill();
}
