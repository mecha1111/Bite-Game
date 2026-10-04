using System;
using Godot;
namespace Gamejam2.Gameplay;
/// <summary>Bounded textured sprite batch. Analytic underwater motion follows supplied presentation time.</summary>
public partial class SharkJumpBubbleEffect : Node2D
{
    [Export] public Texture2D BubbleTexture { get; set; }=null!;
    [Export] public int MinimumCount { get; set; }=24;
    [Export] public int MaximumCount { get; set; }=36;
    [Export] public int MaximumLive { get; set; }=256;
    private struct Bubble { public bool Alive;public double Start,Life;public Vector2 Origin,Velocity;public float Size,Drift,Alpha;public Color Tint; }
    private Bubble[] _bubbles=Array.Empty<Bubble>();
    private readonly RandomNumberGenerator _rng=new();
    private double _time;
    public bool Enabled { get; set; }=true;
    public int LiveCount { get; private set; }
    public int LastEmittedCount { get; private set; }
    public int EmissionCount { get; private set; }
    public Vector2 LastOrigin { get; private set; }
    public override void _Ready(){_bubbles=new Bubble[MaximumLive];SetProcess(false);Visible=false;}
    public void Reset(){Array.Clear(_bubbles);LiveCount=LastEmittedCount=EmissionCount=0;Visible=false;QueueRedraw();}
    public void Emit(double time,Vector2 globalOrigin,int? count=null,Color? tint=null,int seed=0)
    {
        if(!Enabled)return;
        _rng.Seed=unchecked((ulong)(Math.Round(time*1000000)+seed*7919));
        int requested=count??_rng.RandiRange(MinimumCount,MaximumCount);LastEmittedCount=0;LastOrigin=globalOrigin;EmissionCount++;
        for(int i=0;i<_bubbles.Length&&LastEmittedCount<requested;i++)
        {
            if(_bubbles[i].Alive&&time<_bubbles[i].Start+_bubbles[i].Life)continue;
            float sizePick=_rng.Randf();float scale=sizePick<.5f?_rng.RandfRange(.35f,.60f):sizePick<.9f?_rng.RandfRange(.65f,1):_rng.RandfRange(1.05f,1.35f);
            _bubbles[i]=new Bubble{Alive=true,Start=time,Life=_rng.RandfRange(.35f,.8f),Origin=ToLocal(globalOrigin)+new Vector2(_rng.RandfRange(-140,140),_rng.RandfRange(-10,12)),Velocity=new Vector2(_rng.RandfRange(-110,110),-_rng.RandfRange(85,170)),Size=9*scale,Drift=_rng.RandfRange(-8,8),Alpha=_rng.RandfRange(.7f,1),Tint=tint??new Color(.78f,.94f,1)};LastEmittedCount++;
        }
        if(LastEmittedCount>0)LiveCount=Math.Max(1,LiveCount);
        Present(time);
    }
    public void Present(double time)
    {
        _time=time;if(LiveCount==0&&!Visible)return;LiveCount=0;
        for(int i=0;i<_bubbles.Length;i++){if(_bubbles[i].Alive&&time>=_bubbles[i].Start+_bubbles[i].Life)_bubbles[i].Alive=false;if(_bubbles[i].Alive)LiveCount++;}
        if(LiveCount>0||Visible){Visible=LiveCount>0;QueueRedraw();}
    }
    public override void _Draw()
    {
        foreach(var b in _bubbles)
        {
            if(!b.Alive)continue;float age=(float)Math.Max(0,_time-b.Start),phase=(float)Math.Clamp(age/b.Life,0,1);
            var position=b.Origin+b.Velocity*((1-Mathf.Exp(-2.5f*age))/2.5f)+new Vector2(b.Drift*age,-18*age);
            var color=b.Tint;color.A=b.Alpha*Mathf.Pow(1-phase,1.25f);
            DrawTextureRect(BubbleTexture,new Rect2(position-Vector2.One*b.Size*.5f,Vector2.One*b.Size),false,color);
        }
    }
}
