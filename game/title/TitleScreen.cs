using System;
using Godot;
namespace Gamejam2.Title;
/// <summary>씬에서 작성한 Title 입력과 Logo 효과만 담당한다. 시작/설정 라우팅은 기존 SceneRouter에 위임한다.</summary>
public partial class TitleScreen : Control
{
    [Export] public Button StartButton { get; set; } = null!;
    [Export] public Button SettingsButton { get; set; } = null!;
    [Export] public Button ExitButton { get; set; } = null!;
    [Export] public Sprite2D Logo { get; set; } = null!;
    [Export] public Control LogoHitArea { get; set; } = null!;
    [Export] public float LogoHoverScale { get; set; } = 1.05f;
    [Export] public float LogoHoverRisePixels { get; set; } = 5f;
    [Export] public double LogoTransitionSeconds { get; set; } = 0.2;
    public event Action? StartRequested;
    public event Action? SettingsRequested;
    private Vector2 _logoPosition, _logoScale;
    [Export] public float LogoIdleAmplitudePixels { get; set; } = 2.5f;
    [Export] public double LogoIdleCycleSeconds { get; set; } = 3.2;
    [Export] public float LogoIdleBreathing { get; set; } = 0.005f;
    [Export] public float LogoHoverNudgePixels { get; set; } = 2f;
    private double _idleElapsed;
    private float _hoverBlend;
    private Tween? _logoTween;
    public override void _Ready()
    {
        if(Gamejam2.Startup.BuildIdentity.ShowTeamTestId)
        {
            var build=new Label{Name="TeamBuildIdentity",Text="BUILD "+Gamejam2.Startup.BuildIdentity.Id,MouseFilter=MouseFilterEnum.Ignore};
            AddChild(build);build.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
            build.OffsetLeft=-420;build.OffsetRight=-16;build.OffsetTop=-30;build.OffsetBottom=-10;
            build.HorizontalAlignment=HorizontalAlignment.Right;
            build.AddThemeFontSizeOverride("font_size",14);build.Modulate=new Color(.5f,.75f,.8f,.7f);
        }
        _logoPosition = Logo.Position; _logoScale = Logo.Scale;
        StartButton.Pressed += Start;
        SettingsButton.Pressed += Settings;
        ExitButton.Pressed += Quit;
        LogoHitArea.MouseEntered += LogoEnter;
        LogoHitArea.MouseExited += LogoExit;
        Callable.From(FocusStart).CallDeferred();
    }
    private void FocusStart()
    {
        if (IsInsideTree() && !StartButton.Disabled) StartButton.GrabFocus();
    }
    public void SetMenuInputEnabled(bool enabled)
    {
        StartButton.Disabled = SettingsButton.Disabled = ExitButton.Disabled = !enabled;
        if (enabled) Callable.From(RestoreSettingsFocus).CallDeferred();
    }
    private void RestoreSettingsFocus()
    {
        if (IsInsideTree() && !SettingsButton.Disabled) SettingsButton.GrabFocus();
    }
    private void Start()
    {
        GD.Print("[TITLE] Start pressed");
        if(StartRequested==null){GD.PushError("[TITLE] StartRequested has no SceneRouter subscriber: "+GetPath());return;}
        StartRequested.Invoke();
    }
    private void Settings() => SettingsRequested?.Invoke();
    private void Quit() => GetTree().Quit();
    private void LogoEnter() => AnimateLogo(true);
    private void LogoExit() => AnimateLogo(false);
    private void AnimateLogo(bool hover)
    {
        _logoTween?.Kill();
        _logoTween = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _logoTween.TweenMethod(Callable.From<float>(value => _hoverBlend = value), _hoverBlend, hover ? 1f : 0f, LogoTransitionSeconds);
    }
    public override void _Process(double delta)
    {
        // Presentation only. Always compose from the captured authored transform,
        // never from last frame's animated Position/Scale or the rhythm clock.
        _idleElapsed += delta;
        float wave = (float)Math.Sin(Math.Tau * _idleElapsed / Math.Max(0.1, LogoIdleCycleSeconds));
        float idle = 1f - _hoverBlend;
        Logo.Position = _logoPosition + new Vector2(LogoHoverNudgePixels * _hoverBlend,
            wave * LogoIdleAmplitudePixels * idle - LogoHoverRisePixels * _hoverBlend);
        Logo.Scale = _logoScale * (1f + LogoIdleBreathing * wave * idle + (LogoHoverScale - 1f) * _hoverBlend);
    }

    public override void _ExitTree()
    {
        _logoTween?.Kill();
        StartButton.Pressed -= Start; SettingsButton.Pressed -= Settings; ExitButton.Pressed -= Quit;
        LogoHitArea.MouseEntered -= LogoEnter; LogoHitArea.MouseExited -= LogoExit;
    }
}
