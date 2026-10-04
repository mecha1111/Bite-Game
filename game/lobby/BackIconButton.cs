using Godot;
namespace Gamejam2.Lobby;
/// <summary>출구 아이콘의 시각 반응만 담당한다. 뒤로 이동은 Lobby의 기존 event가 처리한다.</summary>
public partial class BackIconButton : Control
{
    [Export] public TextureRect Icon { get; set; } = null!;
    [Export] public Button HitButton { get; set; } = null!;
    [Export] public float HoverScale { get; set; } = 1.08f;
    [Export] public float PressScale { get; set; } = .96f;
    [Export] public Color HoverTint { get; set; } = new(.95f, 1.12f, 1.15f);
    private bool _hovered, _pressed;
    private Vector2 _baseScale;
    private Color _baseColor;
    private Tween? _tween;
    public override void _Ready()
    {
        _baseScale = Scale; _baseColor = Icon.SelfModulate;
        HitButton.MouseEntered += () => { _hovered = true; Refresh(); };
        HitButton.MouseExited += () => { _hovered = false; Refresh(); };
        HitButton.FocusEntered += Refresh; HitButton.FocusExited += Refresh;
        HitButton.ButtonDown += () => { _pressed = true; Refresh(); };
        HitButton.ButtonUp += () => { _pressed = false; Refresh(); };
    }
    private void Refresh()
    {
        bool active = !HitButton.Disabled && (_hovered || HitButton.HasFocus());
        bool pressed = !HitButton.Disabled && _pressed;
        _tween?.Kill();
        _tween = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _tween.TweenProperty(this, "scale", _baseScale * (pressed ? PressScale : active ? HoverScale : 1), .12);
        _tween.TweenProperty(Icon, "self_modulate", _baseColor * (pressed ? new Color(.8f,.9f,.95f) : active ? HoverTint : Colors.White), .12);
    }
    public override void _ExitTree() => _tween?.Kill();
}
