using System;
using System.Linq;
using System.Collections.Generic;
using Gamejam2.Data;
namespace Gamejam2.Gameplay;
public enum PresentationEnergy { LOW, MEDIUM, HIGH, CLIMAX }
public sealed record PresentationSection(double Start,double End,PresentationEnergy Level);
public sealed record InterferenceCameraProfile(double Duration,float Zoom,float Offset,PresentationEnergy Level);
/// <summary>Cached, presentation-only policy. Main-stage sections do not touch prey/economy data.</summary>
public static class MainStagePresentationProfile
{
    public static bool IsMain(string song)=>song is "song_1" or "song_2" or "song_3" or "song_4";
    public static string FxPath(string song)=>"res://data/camera_fx/v5/"+(song switch
    {"song_1"=>"stage1_hear_the_tide", "song_2"=>"stage2_hidden_current", "song_3"=>"stage3_predators_pulse", "song_4"=>"stage4_deep_current_ex", _=>throw new ArgumentException("Unknown main song: "+song)})+"_camera_fx_v5.csv";
    private static readonly Dictionary<string,PresentationSection[]> Sections=new();
    private static Dictionary<string,InterferenceCameraProfile>? _interference;
    private static Dictionary<string,string>? _policy;
    public static double Policy(string key)=>LocalCsv.Number((_policy??=LocalCsv.Read("res://data/presentation/main/policy.csv").Single())[key]);
    public static double Cooldown(PresentationEnergy level)=>level switch{PresentationEnergy.LOW=>Policy("small_cooldown"),PresentationEnergy.MEDIUM=>Policy("medium_cooldown"),_=>Policy("strong_cooldown")};
    public static PresentationSection[] LoadSections(string song)
    {
        if(!IsMain(song))return Array.Empty<PresentationSection>();
        if(Sections.TryGetValue(song,out var result))return result;
        result=LocalCsv.Read("res://data/presentation/main/sections.csv").Where(r=>r["song_id"]==song).Select(r=>new PresentationSection(LocalCsv.Number(r["start_time_sec"]),LocalCsv.Number(r["end_time_sec"]),Enum.Parse<PresentationEnergy>(r["level"]))).ToArray();
        if(result.Length==0||result[0].Start!=0||result.Where((s,i)=>s.End<=s.Start||(i>0&&Math.Abs(s.Start-result[i-1].End)>1e-6)).Any())throw new ArgumentException("Invalid main presentation sections: "+song);
        Sections.Add(song,result);return result;
    }
    public static InterferenceCameraProfile Interference(string type)
    {
        _interference??=LocalCsv.Read("res://data/presentation/main/interference_profiles.csv").ToDictionary(r=>r["type"],r=>new InterferenceCameraProfile(LocalCsv.Number(r["duration"]),(float)LocalCsv.Number(r["zoom_delta"]),(float)LocalCsv.Number(r["offset"]),Enum.Parse<PresentationEnergy>(r["level"])));
        return _interference[type];
    }
    public static float Gain(PresentationEnergy level)=>level switch{PresentationEnergy.LOW=>.15f,PresentationEnergy.MEDIUM=>.65f,PresentationEnergy.HIGH=>.85f,_=>1};
}
