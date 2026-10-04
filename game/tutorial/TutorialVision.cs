using Godot;
namespace Gamejam2.Tutorial;
/// <summary>World-space sensory injury overlay; waves, shark and HUD render above it.</summary>
public partial class TutorialVision : Control
{
    public float LeftVisibility { get; set; }=1;
    public float RightVisibility { get; set; }=1;
    public float AttackAge { get; set; }=-1;
    public float Blackout { get; set; }
    [Export] public bool AttackLayer { get; set; }
    private float _shownLeft=-1,_shownRight=-1;
    public void Present(float left,float right)
    {
        LeftVisibility=Mathf.Clamp(left,0,1);RightVisibility=Mathf.Clamp(right,0,1);
        if(Material is not ShaderMaterial material)return;
        if(_shownLeft!=LeftVisibility){material.SetShaderParameter("left_visibility",LeftVisibility);_shownLeft=LeftVisibility;}
        if(_shownRight!=RightVisibility){material.SetShaderParameter("right_visibility",RightVisibility);_shownRight=RightVisibility;}
    }
    public override void _Draw()
    {
        if(!AttackLayer)
        {
            DrawRect(new Rect2(Vector2.Zero,Size),Colors.White);
            return;
        }
        if(AttackAge>=0&&AttackAge<1.3f)
        {
            float close=AttackAge<.65f?Mathf.SmoothStep(0,1,AttackAge/.65f):1-Mathf.SmoothStep(0,1,(AttackAge-.9f)/.4f);
            float depth=Size.Y*.44f*close;
            DrawRect(new Rect2(0,0,Size.X,Mathf.Max(0,depth-80)),new Color(.025f,.07f,.09f));
            DrawRect(new Rect2(0,Size.Y-depth+80,Size.X,Mathf.Max(0,depth-80)),new Color(.025f,.07f,.09f));
            for(int x=-120;x<Size.X+120;x+=100)
            {
                float sweep=Mathf.Round(AttackAge*110/4)*4;
                float tip=depth+Mathf.Sin(x*.03f)*22;
                Vector2[] top={new(x+sweep,depth-100),new(x+92+sweep,depth-100),new(x+72+sweep,tip-28),new(x+60+sweep,tip-28),new(x+52+sweep,tip),new(x+44+sweep,tip),new(x+32+sweep,tip-28),new(x+24+sweep,tip-28)};
                DrawColoredPolygon(top,new Color(.8f,.91f,.88f));
                var bottom=new Vector2[top.Length];for(int i=0;i<top.Length;i++)bottom[i]=new Vector2(top[i].X-50,Size.Y-top[i].Y);
                DrawColoredPolygon(bottom,new Color(.65f,.83f,.82f));
            }
        }
        if(Blackout>0)DrawRect(new Rect2(Vector2.Zero,Size),new Color(0,0,0,Blackout));
    }
}
