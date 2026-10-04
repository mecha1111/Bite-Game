using Godot;
namespace Gamejam2.Settings;
public partial class SectionDivider:HBoxContainer
{
    [Export] public string CaptionText {get;set;}="";
    public override void _Ready()
    {
        Gamejam2.UI.AuthoredControlLayout.Restore(this,
            "res://game/settings/SectionDivider.tscn","Lead/Edge","Line/Edge");
        if(CaptionText.Length>0)GetNode<Label>("Title").Text=CaptionText;
    }
}
