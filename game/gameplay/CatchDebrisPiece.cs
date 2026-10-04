using Godot;
using System;
namespace Gamejam2.Gameplay;
public partial class CatchDebrisPiece : Sprite2D
{
    public bool IsBlood { get; set; }
    public Vector2 Velocity { get; set; }
    public Vector2 Drift { get; set; }
    public float AngularVelocity { get; set; }
    public double Lifetime { get; set; }
    public float Drag { get; set; }
    private double _at,_last;
    public bool Finished { get; private set; }
    public void Begin(double time){_at=_last=time;}
    public void Present(double time)
    {
        double age=time-_at;float dt=(float)Math.Max(0,time-_last);_last=time;
        float decay=MathF.Exp(-Drag*dt);Position+=Velocity*(Drag>0?(1-decay)/Drag:dt)+Drift*dt;Velocity*=decay;Rotation+=AngularVelocity*dt;
        Modulate=new Color(Modulate.R,Modulate.G,Modulate.B,IsBlood?(float)(age<.2?1-age/.2*.3:age<.35?.7-(age-.2)/.15*.3:.4*Math.Clamp((Lifetime-age)/Math.Max(.001,Lifetime-.35),0,1)):(float)(age<.15?1-age/.15*.25:age<.30?.75-(age-.15)/.15*.35:.4*Math.Clamp((Lifetime-age)/Math.Max(.001,Lifetime-.30),0,1)));
        Finished=age>=Lifetime;
    }
}
