using Godot;
using Gamejam2.Rhythm;
namespace Gamejam2.Gameplay;
/// <summary>Bite animation begins immediately after input timestamp capture; fixed authored position.</summary>
public partial class SharkPresenter : Node2D
{
    [Export] public AnimatedSprite2D Art { get; set; } = null!;
    [Export] public Sprite2D Skeleton { get; set; } = null!;
    [Export] public Marker2D MouthImpact { get; set; } = null!;
    [Export] public Vector2[] FrameMouthCenters { get; set; } = System.Array.Empty<Vector2>();
    [Export(PropertyHint.Range,"0.15,0.6,0.01")] public double BiteDurationSeconds { get; set; } = .36;
    private Vector2 _basePosition;
    private Vector2 _artBase;
    private Vector2 _artScale;
    [Export] public float IdleBreathPixels { get; set; } = 3;
    [Export] public double IdleCycleSeconds { get; set; } = 3.2;
    private Tween? _failure;
    public override void _Ready(){_basePosition=Position;_artBase=Art.Position;_artScale=Art.Scale;}
    private double _biteTime=double.NegativeInfinity;
    public int BiteCount { get; private set; }
    public void Reset() { _failure?.Kill();Position=_basePosition;Skeleton.Visible=false;Skeleton.Position=new Vector2(0,-132);Skeleton.Modulate=Colors.White;Art.SelfModulate=Colors.White;Art.Position=_artBase; _biteTime=double.NegativeInfinity; BiteCount=0; Art.Animation="idle";Art.Frame=0;Present(0); }
    public void Bite(double songTime) { _biteTime=songTime; BiteCount++; Art.Animation="bite";Art.Frame=0;Present(songTime); }
    public void Present(double songTime)
    {
        double age=System.Math.Max(0,songTime-_biteTime);
        // Frames use the existing visual Song Clock so Settings freezes the attack in place.
        int frames=Art.SpriteFrames.GetFrameCount("bite");
        if(age<BiteDurationSeconds){Art.Animation="bite";Art.Frame=System.Math.Min(frames-1,(int)(age/BiteDurationSeconds*frames));}
        else {Art.Animation="idle";Art.Frame=0;}
        // Every 256x256 source frame has opaque pixels on row 255. Source art supplies
        // the upward attack; translating the entire sprite detached this shared baseline.
        Art.Position=_artBase;
        // Offset -128 anchors the 256px canvas at its bottom: breathing changes
        // the upper body height while the bottom edge remains exactly zero.
        float breath=age>=BiteDurationSeconds?(float)((1+System.Math.Sin(songTime/IdleCycleSeconds*System.Math.Tau))*.5)*IdleBreathPixels:0;
        Art.Scale=new Vector2(_artScale.X,_artScale.Y+breath/256f);
        // This visual-only marker follows the sprite mouth, never the fixed chart target.
        if(Art.Frame<FrameMouthCenters.Length)
            MouthImpact.Position=Art.Offset+FrameMouthCenters[Art.Frame]-Art.SpriteFrames.GetFrameTexture(Art.Animation,Art.Frame).GetSize()*.5f;
    }
    public void LoseEnergy()
    {
        _biteTime=double.NegativeInfinity;Art.Position=_artBase;Art.Animation="idle";Art.Frame=0;
        _failure?.Kill();Position=_basePosition;
        Skeleton.Visible=true;Skeleton.Position=new Vector2(0,-132);Skeleton.Modulate=new Color(.88f,.95f,1,0);
        _failure=CreateTween().SetParallel();
        _failure.TweenProperty(Art,"self_modulate",new Color(.55f,.65f,.7f,0),.22);
        _failure.TweenProperty(Skeleton,"modulate:a",1f,.32).SetDelay(.18);
        _failure.TweenProperty(Skeleton,"position:y",-120f,.35).SetDelay(.18);
    }
    public override void _ExitTree()=>_failure?.Kill();
}
