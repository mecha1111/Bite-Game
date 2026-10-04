using System;
using Godot;
namespace Gamejam2.Lobby;
[Tool]
public partial class SwimmingMedalShark : Node2D
{
    [Export] public Sprite2D Art { get; set; }=null!;
    private MedalSharkDefinition? _definition;
    private Control _bounds=null!;
    private Vector2 _size;
    private double _elapsed;
    public string Rank { get; private set; }="none";
    public override void _Ready(){_bounds=GetParent<Control>();_bounds.Resized+=Resize;VisibilityChanged+=RefreshProcessing;Resize();SetProcess(false);}
    private void Resize(){_size=_bounds.Size;if(_definition!=null)Art.Scale=Vector2.One*(_size.X*_definition.WidthFraction/_definition.Region.Size.X);}
    private void RefreshProcessing()=>SetProcess(!Engine.IsEditorHint()&&_definition!=null&&IsVisibleInTree());
    public void SetRank(string rank,bool unlocked)
    {
        rank=unlocked?rank:"none";
        if(Rank!=rank){Rank=rank;_elapsed=0;}
        _definition=rank=="none"?null:MedalSharkCatalog.Find(rank);Visible=_definition!=null;
        if(_definition!=null){Art.Texture=_definition.Texture;Art.RegionEnabled=true;Art.RegionRect=_definition.Region;Resize();Present(_elapsed);}
        RefreshProcessing();
    }
    public override void _Process(double delta){_elapsed+=delta;Present(_elapsed);}
    public void Present(double seconds)
    {
        if(_definition==null)return;
        double phase=seconds/_definition.SwimSeconds%2;bool right=phase<1;float progress=(float)(right?phase:2-phase);
        Position=new Vector2(_size.X*(.12f+.76f*progress),_size.Y*.57f+Mathf.Sin((float)seconds*.85f)*8);
        Art.FlipH=_definition.NaturalFacesLeft?right:!right;Art.Rotation=Mathf.Sin((float)seconds*.85f)*.015f;Art.Modulate=new Color(.92f,.97f,1,.9f);
    }
    public override void _ExitTree(){if(_bounds!=null)_bounds.Resized-=Resize;VisibilityChanged-=RefreshProcessing;}
}
