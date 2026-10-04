using Godot;
namespace Gamejam2.Gameplay;
/// <summary>Render-only Camera2D. WORLD coordinates and the Song Clock are never written.</summary>
public partial class WorldCameraRig : Camera2D
{
    private GameplayScreen _screen=null!;
    private Vector2 _lastSize;
    public Vector2 BasePosition {get;private set;}
    public Vector2 BaseZoom=>Vector2.One;
    public void ApplyShot(Vector2 offset,float zoom,Vector2 mouth)
    {
        BasePosition=GetViewportRect().Size*.5f;
        var size=GetViewportRect().Size;
        if(size!=_lastSize)
        {
            _lastSize=size;
            foreach(var art in new Control[]{_screen.Environment.Background,_screen.Environment.PreviousBackground,_screen.Environment.ReadabilityShade})
            {art.OffsetLeft=-size.X*.4f;art.OffsetTop=-size.Y*.4f;art.OffsetRight=size.X*.4f;art.OffsetBottom=size.Y*.4f;}
        }
        foreach(var art in new TextureRect[]{_screen.Environment.Background,_screen.Environment.PreviousBackground})
            if(art.Material is ShaderMaterial material)material.SetShaderParameter("camera_art_extent",1.8f);
        // Keep the bottom-aligned shark readable while framing incoming sources horizontally.
        Position=mouth+(BasePosition-mouth)/zoom+offset;
        Zoom=Vector2.One*zoom;
        ForceUpdateScroll();
    }
    public void Install(GameplayScreen screen)
    {
        _screen=screen;
        PositionSmoothingEnabled=false;RotationSmoothingEnabled=false;
        IgnoreRotation=true;Enabled=true;MakeCurrent();
        CanvasLayer Layer(string name,int order)
        {var layer=new CanvasLayer{Name=name,Layer=order};screen.AddChild(layer);return layer;}
        var treatment=Layer("WorldTreatmentCanvas",1);
        screen.GetNode("WorldSnapshot").Reparent(treatment,false);
        screen.WorldWaterTreatment.Reparent(treatment,false);
        var hud=Layer("FixedHudCanvas",3);
        var root=new Control{Name="Root",MouseFilter=Control.MouseFilterEnum.Ignore};hud.AddChild(root);root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach(var node in new Node[]{screen.Gauge,screen.Feedback.GetParent(),screen.Combo.GetParent(),screen.SettingsButton.GetParent(),screen.Countdown})node.Reparent(root,false);
        var modals=Layer("FixedModalCanvas",4);
        var modalRoot=new Control{Name="Root",MouseFilter=Control.MouseFilterEnum.Ignore};modals.AddChild(modalRoot);modalRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach(var node in new Node[]{screen.Result,screen.GameOver,screen.StandaloneSettings,screen.UnavailableLayer})node.Reparent(modalRoot,false);
        screen.GetNode<CanvasLayer>("InterferenceIndicators").Layer=2;
        ApplyShot(Vector2.Zero,1,screen.Juice.MouthAnchor.GlobalPosition);
    }
}
