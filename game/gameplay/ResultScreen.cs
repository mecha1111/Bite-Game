using System;
using System.Linq;
using Godot;
using Gamejam2.Audio;
namespace Gamejam2.Gameplay;
/// <summary>End-of-song feeding statement. Images name judgments; Labels show all data.</summary>
public partial class ResultScreen : Control
{
    [Export] public ResultAudio Audio { get; set; } = null!;
    [Export] public Control Panel { get; set; } = null!;
    [Export] public ColorRect BackgroundDim { get; set; } = null!;
    [Export] public Label SongTitle { get; set; } = null!;
    [Export] public Label Headline { get; set; } = null!;
    [Export] public Label Perfect { get; set; } = null!;
    [Export] public Label Good { get; set; } = null!;
    [Export] public Label Bad { get; set; } = null!;
    [Export] public Label Miss { get; set; } = null!;
    [Export] public Label MaxCombo { get; set; } = null!;
    [Export] public TextureRect AchievementShark { get; set; } = null!;
    [Export] public Label Achievement { get; set; } = null!;
    [Export] public VBoxContainer Rows { get; set; } = null!;
    [Export] public Control Header { get; set; } = null!;
    [Export] public Control ComboSection { get; set; } = null!;
    [Export] public Control SatietySection { get; set; } = null!;
    [Export] public Control StateSection { get; set; } = null!;
    [Export] public SharkResultGauge Gauge { get; set; } = null!;
    [Export] public ResultActions Actions { get; set; } = null!;
    [Export] public double GaugeFillSeconds { get; set; } = .8;
    public Button RetryButton=>Actions.RetryButton;
    public Button SongSelectButton=>Actions.SongSelectButton;
    public event Action? RetryRequested;
    public event Action? SongSelectRequested;
    public GameplayResult? Result { get; private set; }
    public bool IsRevealed { get; private set; }
    private Tween? _entrance;
    private Vector2 _panelBase;
    private Control[] Sections=>new[]{Header,ComboSection,SatietySection,StateSection,Actions}.Concat(Rows.GetChildren().OfType<Control>()).ToArray();
    public override void _Ready()
    {
        _panelBase=Panel.Position;
        Actions.RetryRequested+=()=>RetryRequested?.Invoke();
        Actions.SongSelectRequested+=()=>SongSelectRequested?.Invoke();
    }
    public void ShowResult(GameplayResult result)
    {
        _entrance?.Kill();Result=result;Visible=true;IsRevealed=false;Audio.Begin();
        SongTitle.Text=result.SongTitle;Headline.Text=result.Cleared?"CLEAR":"FAILED";
        Headline.PivotOffset=Headline.Size/2;Headline.Scale=Vector2.One*(result.Cleared?1.04f:1);
        Headline.SelfModulate=result.Cleared?new Color(.7f,1,1):new Color(.78f,.58f,.58f);
        MaxCombo.Text="0";Achievement.Text=result.AllPerfect?"ALL PERFECT":result.FullCombo?"FULL COMBO":"";
        Achievement.SelfModulate=result.AllPerfect?new Color(1,.85f,.38f):new Color(.8f,.9f,1);
        var medal=Gamejam2.Lobby.MedalSharkCatalog.RankFor(result.FinalSatiety);
        AchievementShark.Visible=medal!="none";
        var accent=medal=="gold"?new Vector3(1,.82f,.26f):medal=="silver"?new Vector3(.78f,.88f,.96f):new Vector3(.72f,.43f,.23f);
        ((ShaderMaterial)AchievementShark.Material).SetShaderParameter("fill_tint",accent);
        foreach(var section in Sections)section.Modulate=new Color(1,1,1,0);
        foreach(var count in new[]{Perfect,Good,Bad,Miss})count.Text="0";
        Gauge.Present(0);Gauge.Percentage.Modulate=new Color(1,1,1,0);Actions.SetEnabled(false);Panel.Modulate=new Color(1,1,1,0);
        Panel.Position=_panelBase+new Vector2(0,-18);BackgroundDim.Color=new Color(.005f,.03f,.075f,0);
        _entrance=CreateTween();
        // Let the frozen world settle before the receipt enters; settlement stays presentation-only.
        _entrance.TweenProperty(BackgroundDim,"color:a",.25,.08);
        _entrance.TweenProperty(Panel,"modulate:a",1,.16);
        _entrance.Parallel().TweenProperty(Panel,"position",_panelBase,.16).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        _entrance.Parallel().TweenProperty(BackgroundDim,"color:a",.52,.16);
        _entrance.TweenProperty(Header,"modulate:a",1,.12);
        var counts=new[]{Perfect,Good,Bad,Miss};var values=new[]{result.Perfect,result.Good,result.Bad,result.Miss};
        var rows=Rows.GetChildren().OfType<Control>().ToArray();
        for(int i=0;i<rows.Length;i++)
        {
            var count=counts[i];int value=values[i];
            _entrance.TweenProperty(rows[i],"modulate:a",1,.10);
            _entrance.Parallel().TweenMethod(Callable.From<double>(n=>count.Text=Math.Round(n).ToString()),0.0,(double)value,.10);
        }
        _entrance.TweenProperty(ComboSection,"modulate:a",1,.08);
        _entrance.Parallel().TweenMethod(Callable.From<double>(n=>MaxCombo.Text=Math.Round(n).ToString()),0.0,(double)result.MaxCombo,.08);
        _entrance.TweenProperty(SatietySection,"modulate:a",1,.08);
        _entrance.TweenCallback(Callable.From(Audio.BeginFill));
        _entrance.TweenMethod(Callable.From<double>(phase=>{Gauge.Present(phase*result.FinalSatiety);Audio.FillProgress(phase);}),0.0,1.0,GaugeFillSeconds);
        _entrance.TweenProperty(Gauge.Percentage,"modulate:a",1,.08);
        _entrance.Parallel().TweenProperty(StateSection,"modulate:a",1,.12);
        _entrance.Parallel().TweenProperty(Headline,"scale",Vector2.One,.12);
        _entrance.TweenProperty(Actions,"modulate:a",1,.12);
        _entrance.TweenCallback(Callable.From(Reveal));
    }
    public void Reveal()
    {
        _entrance?.Kill();Audio.StopFill();IsRevealed=true;Headline.Scale=Vector2.One;Panel.Position=_panelBase;Panel.Modulate=Colors.White;
        BackgroundDim.Color=new Color(.005f,.03f,.075f,.52f);
        foreach(var section in Sections)section.Modulate=Colors.White;
        Gauge.Percentage.Modulate=Colors.White;
        if(Result!=null)
        {
            Perfect.Text=Result.Perfect.ToString();Good.Text=Result.Good.ToString();Bad.Text=Result.Bad.ToString();Miss.Text=Result.Miss.ToString();
            MaxCombo.Text=Result.MaxCombo.ToString();Gauge.Present(Result.FinalSatiety);
        }
        Actions.SetEnabled(true);
    }
    public void HideResult() {_entrance?.Kill();Audio.Stop();Visible=false;Actions.SetEnabled(false);}
    public override void _Input(InputEvent input)
    {
        if(!Visible||input.IsEcho())return;
        if(input.IsActionPressed("ui_cancel")){GetViewport().SetInputAsHandled();SongSelectRequested?.Invoke();}
        else if(!IsRevealed && input.IsActionPressed("ui_accept")){Reveal();GetViewport().SetInputAsHandled();}
    }
    public override void _ExitTree()=>_entrance?.Kill();
}
