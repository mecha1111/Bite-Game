using System;
using Godot;
namespace Gamejam2.Gameplay;
/// <summary>Short starvation presentation; contains no final judgment receipt.</summary>
public partial class GameOverScreen : Control
{
    [Export] public Control Content { get; set; } = null!;
    [Export] public ColorRect BackgroundDim { get; set; } = null!;
    [Export] public Control Headline { get; set; } = null!;
    [Export] public SharkResultGauge Gauge { get; set; } = null!;
    [Export] public ResultActions Actions { get; set; } = null!;
    public bool IsRevealed { get; private set; }
    public event Action? RetryRequested;
    public event Action? SongSelectRequested;
    private Tween? _entrance;
    public override void _Ready()
    {Actions.RetryRequested+=()=>RetryRequested?.Invoke();Actions.SongSelectRequested+=()=>SongSelectRequested?.Invoke();}
    public void ShowStarvation()
    {
        _entrance?.Kill();Visible=true;IsRevealed=false;Gauge.Present(0);Actions.SetEnabled(false);
        Headline.Modulate=Gauge.Modulate=Actions.Modulate=new Color(1,1,1,0);
        BackgroundDim.Color=new Color(.005f,.03f,.085f,0);
        _entrance=CreateTween();
        _entrance.TweenProperty(BackgroundDim,"color:a",.56,.3);
        _entrance.TweenInterval(.15);
        _entrance.TweenProperty(Gauge,"modulate:a",1,.2);
        _entrance.TweenProperty(Headline,"modulate:a",1,.2);
        _entrance.TweenProperty(Actions,"modulate:a",1,.2);
        _entrance.TweenCallback(Callable.From(Reveal));
    }
    public void Reveal()
    {_entrance?.Kill();IsRevealed=true;Headline.Modulate=Gauge.Modulate=Actions.Modulate=Colors.White;BackgroundDim.Color=new Color(.005f,.03f,.085f,.56f);Actions.SetEnabled(true);}
    public void HideScreen(){_entrance?.Kill();Visible=false;Actions.SetEnabled(false);}
    public override void _Input(InputEvent input)
    {
        if(!Visible||input.IsEcho())return;
        if(input.IsActionPressed("ui_cancel")){GetViewport().SetInputAsHandled();SongSelectRequested?.Invoke();}
        else if(!IsRevealed && input.IsActionPressed("ui_accept")){Reveal();GetViewport().SetInputAsHandled();}
    }
    public override void _ExitTree()=>_entrance?.Kill();
}
