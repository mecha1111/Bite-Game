using System;
using System.Linq;
using Godot;
namespace Gamejam2.Gameplay;
/// <summary>A few authored pixel bubbles, aged by the presentation clock, including pause.</summary>
public partial class JuiceBurst : Node2D
{
    private Sprite2D[] _bubbles=Array.Empty<Sprite2D>();
    private double _started;
    private int _count;
    private Color _color;
    public bool Finished { get; private set; }
    public override void _Ready()=>_bubbles=GetChildren().OfType<Sprite2D>().ToArray();
    public void Begin(double time,int count,Color color) { _started=time;_count=Math.Clamp(count,0,_bubbles.Length);_color=color;Present(time); }
    public void Present(double time)
    {
        float age=(float)Math.Max(0,time-_started);Finished=age>=.45f;
        for(int i=0;i<_bubbles.Length;i++)
        {
            var bubble=_bubbles[i];bubble.Visible=time>=_started&&i<_count&&!Finished;
            float direction=i%2==0?-1:1;
            bubble.Position=new Vector2(Mathf.Round(direction*(10+i*7)*age),Mathf.Round(-age*(45+i*10)));
            bubble.Modulate=new Color(_color.R,_color.G,_color.B,Math.Clamp(1-age/.45f,0,1)*.65f);
        }
    }
}
