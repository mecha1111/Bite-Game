using System;
using Godot;
namespace Gamejam2.Gameplay;
/// <summary>Five-second visual current. Fixed origins, ring age and targets stay authoritative.</summary>
public partial class SardineVortex : Node2D
{
    [Export] public Sprite2D Art {get;set;}=null!;
    [Export] public double DurationSeconds {get;set;}=5;
    [Export] public float PullPixels {get;set;}=95;
    public float Strength {get;private set;}
    public Vector2 Center {get;private set;}
    private double _at=double.NegativeInfinity;
    private static readonly StringName StrengthKey="strength",TimeKey="age";
    public void Begin(double time){_at=time;Present(time);}
    public void Clear(){_at=double.NegativeInfinity;Strength=0;Visible=false;}
    private static float Smooth(float x){x=Math.Clamp(x,0,1);return x*x*(3-2*x);}
    public void Present(double time)
    {
        double age=time-_at;Visible=age>=0&&age<DurationSeconds;
        Strength=Visible?Smooth((float)(age/.5))*Smooth((float)((DurationSeconds-age)/.5)):0;
        if(!Visible)return;
        Center=GetViewportRect().Size*new Vector2(.5f,.30f);GlobalPosition=Center;
        var shader=(ShaderMaterial)Art.Material;shader.SetShaderParameter(StrengthKey,Strength);shader.SetShaderParameter(TimeKey,(float)age);
    }
    public void Apply(WavePulse wave)=>wave.ApplyCurrent(Center,Strength,PullPixels);
}
