using System;
using Godot;
namespace Gamejam2.Gameplay;
/// <summary>Presentation scale: 1000 satiety =100%; 1100 =110%. No balance authority.</summary>
public partial class SharkResultGauge : Control
{
    [Export] public TextureRect SharkImage { get; set; } = null!;
    [Export] public Label Percentage { get; set; } = null!;
    [Export] public bool SupportsOverfill { get; set; }
    [Export] public string LayoutScenePath {get;set;}="";
    public override void _Ready()
    {
        if(LayoutScenePath.Length>0)Gamejam2.UI.AuthoredControlLayout.Restore(this,
            LayoutScenePath,"SharkImage","Percentage");
    }
    public double DisplayedSatiety { get; private set; }
    public void Present(double value)
    {
        DisplayedSatiety=Math.Max(0,value);
        ((ShaderMaterial)SharkImage.Material).SetShaderParameter("fill_ratio",(float)Math.Clamp(DisplayedSatiety/1000,0,1));
        if(SupportsOverfill)((ShaderMaterial)SharkImage.Material).SetShaderParameter("overfill_strength",(float)Math.Clamp((DisplayedSatiety-1000)/100,0,1));
        Percentage.Text=$"{DisplayedSatiety/10:0.#}%";
    }
}
