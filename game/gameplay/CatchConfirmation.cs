using System;
using Godot;
using Gamejam2.Rhythm;
namespace Gamejam2.Gameplay;
/// <summary>Fixed mouth-centered capture seal, distinct from expanding rhythm waves.</summary>
public partial class CatchConfirmation : Node2D
{
    [Export] public Sprite2D Art { get; set; } = null!;
    [Export] public double Duration { get; set; } = .18;
    private double _at=double.NegativeInfinity;
    public int CatchCount { get; private set; }
    public void Clear(){_at=double.NegativeInfinity;Visible=false;CatchCount=0;}
    public void ShowCatch(RhythmJudgment judgment,double time)
    {
        CatchCount++;_at=time;Visible=true;
        var shader=(ShaderMaterial)Art.Material;
        shader.SetShaderParameter("strength",judgment==RhythmJudgment.Perfect?1f:judgment==RhythmJudgment.Good?.65f:.35f);
        shader.SetShaderParameter("catch_tint",judgment==RhythmJudgment.Perfect?new Vector3(.85f,1,.65f):new Vector3(.55f,1,.9f));
        Present(time);
    }
    public void Present(double time){double age=time-_at;Visible=age>=0&&age<Duration;((ShaderMaterial)Art.Material).SetShaderParameter("phase",Math.Clamp(age/Duration,0,1));}
}
