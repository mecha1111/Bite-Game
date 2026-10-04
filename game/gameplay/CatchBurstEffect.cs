using Godot;
using System;
using System.Collections.Generic;
using Gamejam2.Rhythm;
namespace Gamejam2.Gameplay;
public partial class CatchBurstEffect : Node2D
{
    [Export] public PackedScene DebrisScene { get; set; }=null!;
    [Export] public AnimatedSprite2D FishBurst { get; set; }=null!;
    [Export] public Node2D DebrisRoot { get; set; }=null!;
    [Export] public Texture2D[] DebrisTextures { get; set; }=Array.Empty<Texture2D>();
    [Export] public Texture2D BloodTexture { get; set; }=null!;
    [Export] public int CatchParticleCountPerfect { get; set; }=10;
    [Export] public int CatchParticleCountGood { get; set; }=7;
    [Export] public int CatchParticleCountBad { get; set; }=4;
    [Export] public float BloodParticleRatio { get; set; }=.4f;
    [Export] public Vector2 ParticleScaleSmall { get; set; }=new(.45f,.7f);
    [Export] public Vector2 ParticleScaleMedium { get; set; }=new(.75f,1);
    [Export] public Vector2 ParticleScaleLarge { get; set; }=new(1.1f,1.4f);
    [Export] public float LargeParticleChance { get; set; }=.15f;
    [Export] public float DownwardBias { get; set; }=.7f;
    [Export] public Vector2 ParticleSpeed { get; set; }=new(420,720);
    [Export] public float ParticleDrag { get; set; }=4;
    [Export] public Vector2 ParticleLifetime { get; set; }=new(.35f,.5f);
    [Export] public double BurstAnimationDuration { get; set; }=.15;
    [Export] public float BurstScale { get; set; }=2;
    [Export] public double FishRecognitionHold { get; set; }=.12;
    [Export] public float DebrisPixelScale { get; set; }=3;
    public WavePulse? ImpactRing { get; set; }
    public JuiceBurst? ImpactBubbles { get; set; }
    public int BloodPieceCount => _blood;
    public bool IsRecognitionHold(double time) => time-_at<FishRecognitionHold;
    private readonly List<CatchDebrisPiece> _pieces=new();
    private double _at;
    private bool _emitted;
    private int _count,_blood;
    private readonly RandomNumberGenerator _rng=new();
    public bool Finished { get; private set; }
    public int PieceCount=>_pieces.Count;
    public void Begin(RhythmJudgment judgment,double time)
    {
        _rng.Randomize();_at=time;_emitted=false;Finished=false;
        _count=judgment==RhythmJudgment.Perfect?CatchParticleCountPerfect:judgment==RhythmJudgment.Good?CatchParticleCountGood:judgment==RhythmJudgment.Bad?CatchParticleCountBad:0;
        _blood=Math.Min(judgment==RhythmJudgment.Perfect?4:judgment==RhythmJudgment.Good?3:judgment==RhythmJudgment.Bad?1:0,(int)Math.Round(_count*BloodParticleRatio));
        FishBurst.Scale=Vector2.One*BurstScale*(judgment==RhythmJudgment.Bad?.7f:1);
        FishBurst.Frame=0;Present(time);
    }
    public void Present(double time)
    {
        double age=Math.Max(0,time-_at-FishRecognitionHold);FishBurst.Visible=_count>0&&age<BurstAnimationDuration;
        FishBurst.Frame=Math.Min(2,(int)(age/BurstAnimationDuration*3));
        if(time>=_at+FishRecognitionHold&&!_emitted){_emitted=true;Emit(_at+FishRecognitionHold);}
        foreach(var piece in _pieces)piece.Present(time);
        Finished=age>=Math.Max(BurstAnimationDuration,Math.Max(.55,ParticleLifetime.Y));
    }
    private void Emit(double time)
    {
        var scene=DebrisScene;
        for(int i=0;i<_count;i++)
        {
            var piece=scene.Instantiate<CatchDebrisPiece>();DebrisRoot.AddChild(piece);_pieces.Add(piece);
            bool blood=i<_blood;piece.Texture=blood?BloodTexture:DebrisTextures[_rng.RandiRange(0,DebrisTextures.Length-1)];
            float group=_rng.Randf();Vector2 range=group<.5?ParticleScaleSmall:group<1-LargeParticleChance?ParticleScaleMedium:ParticleScaleLarge;
            if(blood&&i==0)range=ParticleScaleMedium;
            if(blood)range.Y=Math.Min(range.Y,1.35f);
            float scale=_rng.RandfRange(range.X,range.Y);piece.Scale=Vector2.One*scale*DebrisPixelScale*(blood?2:1);
            piece.Modulate=blood?new Color(1.6f,1,1):new Color(.9f,1,1);
            float angle=i<(int)Math.Floor(_count*DownwardBias)?_rng.RandfRange(.35f,Mathf.Pi-.35f):(i%2==0?_rng.RandfRange(-.5f,-.05f):_rng.RandfRange(Mathf.Pi+.05f,Mathf.Pi+.5f));
            piece.Velocity=Vector2.FromAngle(angle)*_rng.RandfRange(ParticleSpeed.X,ParticleSpeed.Y)/MathF.Sqrt(scale);
            piece.Drift=new Vector2(_rng.RandfRange(-18,18),-_rng.RandfRange(0,8));
            piece.AngularVelocity=_rng.RandfRange(-7,7)/scale;piece.Drag=ParticleDrag;
            piece.IsBlood=blood;piece.Lifetime=blood?_rng.RandfRange(.4f,.55f):_rng.RandfRange(ParticleLifetime.X,ParticleLifetime.Y);piece.Begin(time);
        }
    }
}
