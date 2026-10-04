using Godot;
namespace Gamejam2.Title;

/// <summary>하나의 입력 영역과 상태가 배경/아이콘/문자 전체를 함께 제어한다.</summary>
public partial class TitleMenuGroup : Control
{
    public enum InteractionState { Normal, Hover, Pressed }
    [Export] public Sprite2D Background { get; set; } = null!;
    [Export] public Sprite2D Icon { get; set; } = null!;
    [Export] public Label Caption { get; set; } = null!;
    [Export] public Button HitButton { get; set; } = null!;
    // 어두운 원본 텍스처에 색상을 곱하기 때문에 G/B gain으로 실제 밝기를 높인다.
    [Export] public Color HoverBackground { get; set; } = new(0.90f, 1.25f, 1.28f, 1f);
    [Export] public Color HoverIcon { get; set; } = new("dfffff");
    [Export] public Color HoverText { get; set; } = new("dfffff");
    [Export] public Color PressedTint { get; set; } = new("28afc5");
    [Export] public float HoverScale { get; set; } = 1.035f;
    [Export] public float PressedScale { get; set; } = 0.975f;
    [Export] public double TransitionSeconds { get; set; } = 0.12;
    public InteractionState State { get; private set; }
    [Export] public float IconHoverRisePixels { get; set; } = 3f;
    private Vector2 _baseScale, _iconPosition;
    private Color _backgroundColor, _iconColor, _fontColor;
    private bool _held;
    private Tween? _tween;

    public override void _Ready()
    {
        _baseScale = Scale; _iconPosition = Icon.Position;
        _backgroundColor = Background.SelfModulate;
        _iconColor = Icon.SelfModulate;
        _fontColor = Caption.GetThemeColor("font_color");
        HitButton.MouseEntered += Enter;
        HitButton.MouseExited += Exit;
        HitButton.ButtonDown += Down;
        HitButton.ButtonUp += Up;
        HitButton.FocusEntered += Refresh;
        HitButton.FocusExited += FocusExit;
    }
    // Hover transfers the single selection to the real Button. Mouse exit must
    // preserve focus so keyboard navigation can continue from that item.
    private void Enter() { if (!HitButton.Disabled) HitButton.GrabFocus(); }
    private void Exit() => Refresh();
    private void FocusExit() { _held = false; Refresh(); }
    private void Down() { _held = true; Refresh(); }
    private void Up() { _held = false; Refresh(); }
    private void Refresh()
    {
        var active = !HitButton.Disabled && HitButton.HasFocus();
        State = active ? (_held ? InteractionState.Pressed : InteractionState.Hover) : InteractionState.Normal;
        var multiplier = State == InteractionState.Pressed ? PressedScale : active ? HoverScale : 1f;
        var background = State == InteractionState.Pressed ? PressedTint : active ? HoverBackground : Colors.White;
        var icon = State == InteractionState.Pressed ? PressedTint : active ? HoverIcon : Colors.White;
        var text = State == InteractionState.Pressed ? PressedTint : active ? HoverText : _fontColor;
        _tween?.Kill();
        _tween = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _tween.TweenProperty(Icon, "position", _iconPosition + (active ? Vector2.Up * IconHoverRisePixels : Vector2.Zero), TransitionSeconds);
        _tween.TweenProperty(this, "scale", _baseScale * multiplier, TransitionSeconds);
        _tween.TweenProperty(Background, "self_modulate", _backgroundColor * background, TransitionSeconds);
        _tween.TweenProperty(Icon, "self_modulate", _iconColor * icon, TransitionSeconds);
        // Tint the glyph fill only; the scene/theme-authored navy outline stays unchanged.
        _tween.TweenMethod(Callable.From<Color>(color => Caption.AddThemeColorOverride("font_color", color)),
            Caption.GetThemeColor("font_color"), text, TransitionSeconds);
        _tween.Chain().TweenCallback(Callable.From(() =>
        {
            if (State == InteractionState.Normal) Caption.RemoveThemeColorOverride("font_color");
        }));
    }
    public override void _ExitTree()
    {
        _tween?.Kill();
        HitButton.MouseEntered -= Enter; HitButton.MouseExited -= Exit;
        HitButton.ButtonDown -= Down; HitButton.ButtonUp -= Up;
        HitButton.FocusEntered -= Refresh; HitButton.FocusExited -= FocusExit;
    }
}
