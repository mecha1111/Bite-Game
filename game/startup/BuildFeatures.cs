using Godot;
namespace Gamejam2.Startup;
/// <summary>One build policy. Desktop export presets carry bite_release, including debug exports.</summary>
public static class BuildFeatures
{
    public static bool DevelopmentEnabled => OS.IsDebugBuild() && !OS.HasFeature("bite_release");
    public static bool TutorialEnabled => true;
}
