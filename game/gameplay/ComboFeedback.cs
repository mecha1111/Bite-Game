using System;
using Godot;
using Gamejam2.Rhythm;
namespace Gamejam2.Gameplay;
/// <summary>Scene-authored secondary combo feedback, animated relative to the Song Clock.</summary>
public partial class ComboFeedback : Control
{
    public System.Collections.Generic.Dictionary<RhythmJudgment,JudgmentEffect> JudgmentEffects { get; set; } = JudgmentPresentation.Load();
    [Export] public WavePulse Accent { get; set; } = null!;
    [Export] public Label Number { get; set; } = null!;
    [Export] public float MilestoneScale { get; set; } = 1.25f;
    [Export] public double PopDuration { get; set; } = .18;
    private double _hitTime=double.NegativeInfinity;
    private bool _milestone;
    private bool _broken;
    private RhythmJudgment _judgment=RhythmJudgment.Good;
    public override void _Ready()=>Gamejam2.UI.AuthoredControlLayout.Restore(this,
        "res://game/gameplay/ComboFeedback.tscn",".");
    public void Reset() {_broken=false;Accent.Visible=false;Visible=false;Scale=Vector2.One;Modulate=Colors.White;Number.Text="0";_hitTime=double.NegativeInfinity;}
    public void UpdateCombo(int count,double songTime,RhythmJudgment judgment=RhythmJudgment.Good)
    {
        _judgment=judgment;_broken=count==0&&Visible;Visible=count>0||_broken;if(count>0)Number.Text=count.ToString();_hitTime=songTime;
        _milestone=count is 10 or 20 or 30 or 50 or 100;
        Present(songTime);
    }
    public void Present(double songTime)
    {
        if(_broken){double age=songTime-_hitTime;Visible=age<.18;Scale=Vector2.One*(1-(float)Math.Clamp(age/.18,0,1)*.08f);Modulate=new Color(.7f,.68f,.68f,(float)Math.Clamp(1-age/.18,0,1));Accent.Visible=false;return;}
        float amount=(float)Math.Clamp(1-(songTime-_hitTime)/PopDuration,0,1);
        float pop=_milestone?MilestoneScale:_judgment==RhythmJudgment.Perfect?1.22f:_judgment==RhythmJudgment.Good?1.18f:1.16f;
        Scale=Vector2.One*(1+(pop-1)*amount);
        Accent.PresentAge(_milestone||_judgment==RhythmJudgment.Perfect?songTime-_hitTime:1);
        Modulate=Colors.White.Lerp(_judgment==RhythmJudgment.Bad?new Color(.86f,.76f,.72f):new Color(.65f,1,1),amount*(_milestone?1:.4f));
    }
}
