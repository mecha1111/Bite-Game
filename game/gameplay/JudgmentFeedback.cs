using Godot;
using Gamejam2.Rhythm;
namespace Gamejam2.Gameplay;
/// <summary>Provided judgment textures, pop/hold/fade driven only by presentation time.</summary>
public partial class JudgmentFeedback : Node2D
{
    [Export] public Sprite2D Art { get; set; } = null!;
    [Export] public Texture2D PerfectTexture { get; set; } = null!;
    [Export] public Texture2D GoodTexture { get; set; } = null!;
    [Export] public Texture2D BadTexture { get; set; } = null!;
    [Export] public Texture2D MissTexture { get; set; } = null!;
    private float _pop=.3f;
    private double _shownAt=double.NegativeInfinity;
    public void Clear() { _shownAt=double.NegativeInfinity; Art.Visible=false; }
    public void ShowResult(RhythmJudgment judgment,double time)
    { Art.Texture=judgment switch {RhythmJudgment.Perfect=>PerfectTexture,RhythmJudgment.Good=>GoodTexture,RhythmJudgment.Bad=>BadTexture,_=>MissTexture}; _shownAt=time;_pop=judgment switch{RhythmJudgment.Perfect=>.48f,RhythmJudgment.Good=>.3f,RhythmJudgment.Bad=>.18f,_=>.12f};Present(time); }
    public void Present(double time)
    { double age=System.Math.Max(0,time-_shownAt); Art.Visible=age<.85; Art.Scale=Vector2.One*(float)(3+System.Math.Max(0,1-age/.12)*_pop); Art.Modulate=new Color(1,1,1,(float)System.Math.Clamp((.85-age)/.3,0,1)); }
}
