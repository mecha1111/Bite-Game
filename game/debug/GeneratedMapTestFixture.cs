using System.Linq;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Data;
using Gamejam2.CustomGenerator;
namespace Gamejam2.Debugging;
/// <summary>Developer checks use real generated content, never a shipped music fixture.</summary>
public static class GeneratedMapTestFixture
{
    public static string Source=>OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--test-mp3="))?.Substring("--test-mp3=".Length)??"res://assets/music/1_edited.mp3";
    public static async Task<CustomMapDefinition> Ensure(Node owner)
    {
        var existing=CustomMapRegistry.Maps.FirstOrDefault(m=>m.Generated&&m.Available);
        return existing??await LocalMapGenerator.Generate(owner,Source,text=>GD.Print(text));
    }
}
