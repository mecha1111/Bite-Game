using Godot;
namespace Gamejam2.Calibration;
/// <summary>scene-authored pixel button의 한 상태가 배경/글자를 함께 움직인다.</summary>
public partial class CalibrationButton : Control
{
    [Export] public TextureRect Background { get; set; } = null!;
    [Export] public Label Caption { get; set; } = null!;
    [Export] public Button HitButton { get; set; } = null!;
    [Export] public string CaptionText {get;set;}="";
    [Export] public float HoverScale { get; set; } = 1.025f;
    [Export] public float PressScale { get; set; } = .98f;
    private bool _hover, _pressed;
    private Tween? _tween;
    public override void _Ready()
    {
        if(CaptionText.Length>0)
            foreach(var child in GetChildren())if(child is Label label)label.Text=CaptionText;
        HitButton.MouseEntered += () => { _hover = true; Refresh(); };
        HitButton.MouseExited += () => { _hover = false; Refresh(); };
        HitButton.ButtonDown += () => { _pressed = true; Refresh(); };
        HitButton.ButtonUp += () => { _pressed = false; Refresh(); };
        VisibilityChanged += () => { if (!Visible) { _hover = _pressed = false; _tween?.Kill(); Scale = Vector2.One; Background.SelfModulate = Colors.White; Caption.RemoveThemeColorOverride("font_color"); } };
    }
    private void Refresh()
    {
        _tween?.Kill(); _tween = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        var tint = _pressed ? new Color("28afc5") : _hover ? new Color(.9f, 1.25f, 1.28f) : Colors.White;
        _tween.TweenProperty(this, "scale", Vector2.One * (_pressed ? PressScale : _hover ? HoverScale : 1), .12);
        _tween.TweenProperty(Background, "self_modulate", tint, .12);
        Caption.AddThemeColorOverride("font_color", _pressed ? new Color("b4f3fa") : _hover ? new Color("dfffff") : new Color("f7fbff"));
    }
    public override void _ExitTree() => _tween?.Kill();
}
