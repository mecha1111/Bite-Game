using Godot;
namespace Gamejam2.Startup;
/// <summary>Generated once per Windows export by tools/stamp_windows_build.py.</summary>
public static class BuildIdentity
{
    public const string Id = "BITE-WIN-FINAL-20261005-R3";
    public const string BuiltAtUtc = "2026-10-04T15:56:18+00:00";
    public const bool ShowTeamTestId = true;
    private static bool _logged;
    public static void LogBoot()
    {
        if(_logged)return;_logged=true;
        GD.Print("[BUILD] "+Id);
        string executable=OS.GetExecutablePath().Replace('\\','/').Split('/')[^1];
        GD.Print("[BUILD] BuiltAtUtc = "+BuiltAtUtc);
        GD.Print("[BUILD] Godot = "+Engine.GetVersionInfo()["string"].AsString());
        GD.Print("[BUILD] Configuration = "+(OS.IsDebugBuild()?"debug":"release"));
        GD.Print("[BUILD] Executable = "+executable);
    }
}
