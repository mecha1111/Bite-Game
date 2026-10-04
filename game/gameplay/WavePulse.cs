using System;
using Godot;
namespace Gamejam2.Gameplay;
/// <summary>Independent fixed-origin ring. Visual age is supplied by the authoritative presentation clock.</summary>
public partial class WavePulse : Node2D
{
	[Export] public Sprite2D Art { get; set; } = null!;
	[Export] public WaveKind WaveType { get; set; } = WaveKind.Medium;
	[Export] public float MediumDiameter { get; set; } = 260;
	[Export] public double DurationSeconds { get; set; } = .6;
	[Export] public float ExpansionSpeed { get; set; } = 900;
	[Export] public float RingThickness { get; set; } = 4;
	[Export] public float PixelSize { get; set; } = 2;
	[Export] public float PeakOpacity { get; set; } = .9f;
	[Export] public bool FullyOpaque { get; set; }
	[Export] public float CoreFlashSeconds { get; set; } = .055f;
	[Export] public float CoreFlashOpacity { get; set; } = 1;
	[Export] public float ActivationBoost { get; set; } = .1f;
	[Export] public Curve? FadeCurve { get; set; }
	public float CurrentRadius { get; private set; }
	public float MaxRadius { get; private set; }
	public bool Finished { get; private set; }
	public double StartedAt { get; private set; }
	public Vector2 Origin => GlobalPosition;
	public string WaveEventId {get;set;}="";
	public int EncounterId { get; set; } = -1;
	private double _fadeAt=double.PositiveInfinity;
	public void FadeOut(double visualTime)=>_fadeAt=Math.Min(_fadeAt,visualTime);
	private ShaderMaterial _material=null!;
	private float _currentStrength;
	private double _lastAge=-1;
	private static readonly StringName CurrentKey="current_strength";
	/// <summary>Artwork-only deformation. Origin and logical radius/age never move.</summary>
	public void ApplyCurrent(Vector2 center,float strength,float pullPixels)
	{
		if(strength==0&&_currentStrength==0)return;
		_currentStrength=strength;
		Vector2 direction=(center-Origin).Normalized();
		Art.Position=GlobalTransform.BasisXformInv(direction*pullPixels*strength);
		_material.SetShaderParameter(CurrentKey,strength);
		if(Visible)PresentAge(_lastAge);
	}
	private static readonly StringName BoundRadiusKey="bound_radius", RadiusKey="radius", ThicknessKey="thickness", PixelSizeKey="pixel_size", OpacityKey="opacity", CoreFlashKey="core_flash";
	private static PackedScene? _scene;
	private Rect2 _exitRect;
	private Transform2D _exitTransform;
	private float _exitMargin,_exitRadius;
	private bool _exitCached;
	public override void _Ready(){_material=(ShaderMaterial)Art.Material;Visible=false;}
	public void Begin(WaveKind kind,double timestamp){WaveType=kind;StartedAt=timestamp;Finished=false;PresentAge(-1);}
	public void Present(double time){PresentAge(time-StartedAt);if(time>=_fadeAt){float fade=(float)Math.Clamp(1-(time-_fadeAt)/.18,0,1);Art.Modulate=new Color(1,1,1,fade);if(fade<=0){Finished=true;Visible=false;}}}
	public float ViewportExitRadius()
	{
		var rect=GetViewportRect();var transform=GetGlobalTransformWithCanvas();
		float margin=RingThickness+PixelSize*2;
		if(_exitCached&&rect==_exitRect&&transform==_exitTransform&&margin==_exitMargin)return _exitRadius;
		var inverse=transform.AffineInverse();
		float radius=Math.Max((inverse*rect.Position).Length(),(inverse*new Vector2(rect.End.X,rect.Position.Y)).Length());
		radius=Math.Max(radius,(inverse*rect.End).Length());
		radius=Math.Max(radius,(inverse*new Vector2(rect.Position.X,rect.End.Y)).Length());
		_exitRect=rect;_exitTransform=transform;_exitMargin=margin;_exitRadius=radius+margin;_exitCached=true;
		return _exitRadius;
	}
	public void PresentAge(double age)
	{
		_lastAge=age;
		if(age<0){Visible=false;return;}
		MaxRadius=WaveType==WaveKind.Large?ViewportExitRadius():MediumDiameter*.5f*(WaveType==WaveKind.Small?.25f:1);
		CurrentRadius=WaveType==WaveKind.Large?(float)age*ExpansionSpeed:MaxRadius*(float)Math.Clamp(age/(DurationSeconds*.75),0,1);
		Finished=WaveType==WaveKind.Large?CurrentRadius>MaxRadius:age>=DurationSeconds;
		Visible=!Finished;if(Finished)return;
		float phase=WaveType==WaveKind.Large?CurrentRadius/MaxRadius:(float)(age/DurationSeconds);
		float opacity=FullyOpaque?1:FadeCurve?.Sample(Math.Clamp(phase,0,1)) ?? (WaveType==WaveKind.Large?1:Math.Clamp((1-phase)/.25f,0,1));
		float bound=CurrentRadius+RingThickness+PixelSize*2+CurrentRadius*.3f*_currentStrength;
		Art.Scale=Vector2.One*bound; // 2x2 white quad; only the shader's annulus is visible.
		_material.SetShaderParameter(BoundRadiusKey,bound);
		// CurrentRadius is the outer envelope, so final visible diameter obeys the 1:4 rule.
		_material.SetShaderParameter(RadiusKey,Math.Max(0,CurrentRadius-RingThickness*.5f-PixelSize));
		_material.SetShaderParameter(ThicknessKey,RingThickness);
		_material.SetShaderParameter(PixelSizeKey,PixelSize);
		_material.SetShaderParameter(OpacityKey,FullyOpaque?1:Math.Min(1,PeakOpacity*opacity*(1+ActivationBoost*(float)Math.Clamp(1-age/.08,0,1))));
		_material.SetShaderParameter(CoreFlashKey,CoreFlashOpacity*(float)Math.Clamp(1-age/Math.Max(.001,CoreFlashSeconds),0,1));
	}
	public static void Preload()=>_scene??=GD.Load<PackedScene>("res://game/gameplay/WavePulse.tscn");
	public static WavePulse Spawn(Node parent,WavePulse template,double timestamp,Vector2? fixedOrigin=null)
	{
		var wave=(_scene??=GD.Load<PackedScene>("res://game/gameplay/WavePulse.tscn")).Instantiate<WavePulse>();
		wave.WaveType=template.WaveType;wave.MediumDiameter=template.MediumDiameter;wave.DurationSeconds=template.DurationSeconds;
		wave.ExpansionSpeed=template.ExpansionSpeed;wave.RingThickness=template.RingThickness;wave.PixelSize=template.PixelSize;
		wave.CoreFlashSeconds=template.CoreFlashSeconds;wave.CoreFlashOpacity=template.CoreFlashOpacity;wave.ActivationBoost=template.ActivationBoost;wave.PeakOpacity=template.PeakOpacity;wave.FadeCurve=template.FadeCurve;wave.FullyOpaque=template.FullyOpaque;
		parent.AddChild(wave);wave.GlobalPosition=fixedOrigin??template.GlobalPosition;wave.Begin(template.WaveType,timestamp);return wave;
	}
}
