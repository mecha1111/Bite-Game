using Godot;
namespace Gamejam2.Lobby;
/// <summary>작은 편집용 스테이지 목록. 순서/표시/요구사항은 Resource로 작성한다.</summary>
[Tool, GlobalClass]
public partial class StageCatalog : Resource
{
	[Export] public Godot.Collections.Array<StageData> Stages { get; set; } = new();
}
