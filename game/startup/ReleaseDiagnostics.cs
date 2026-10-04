using System.Collections.Generic;
using Godot;
namespace Gamejam2.Startup;
/// <summary>Small Windows player log; no frame-by-frame tracing or user settings.</summary>
public static class ReleaseDiagnostics
{
    private static bool Enabled=>OS.HasFeature("windows")&&!BuildFeatures.DevelopmentEnabled;
    private static readonly HashSet<string> Seen=new();
    public static void Scene(string action,string path)
    {if(Enabled)GD.Print("[RELEASE] Scene "+action+": "+path);}
    public static void Loaded(string kind,string path)
    {if(Enabled&&Seen.Add(kind+":"+path))GD.Print("[RELEASE] "+kind+" loaded: "+path);}
}
