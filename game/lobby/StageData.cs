using Godot;
namespace Gamejam2.Lobby;
/// <summary>곡/환경 선택용 디자인 데이터. runtime 해금은 ProgressService가 소유한다.</summary>
[Tool, GlobalClass]
public partial class StageData : Resource
{
	[Export] public string StageId { get; set; } = "";
	[Export] public string SongId { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "";
	[Export(PropertyHint.Enum,"normal,ex_composite")] public string CoverType { get; set; } = "normal";
	[Export] public string SecondaryTag { get; set; } = "";
	public bool IsEx => CoverType == "ex_composite";
	[Export] public Texture2D? BackgroundTexture { get; set; }
	[Export] public string ScenePath { get; set; } = "";
	[Export] public string UnlockRequirement { get; set; } = "";
	[Export] public bool IsAvailable { get; set; } = true;
	[Export] public string Difficulty { get; set; } = "";
	[Export] public string OptionalDescription { get; set; } = "";
}
