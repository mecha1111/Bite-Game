using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Gamejam2.Data;
namespace Gamejam2.Gameplay;
public sealed record StageFxEvent(double Time,string Type,double Strength,double Duration,string Parameter,string Target)
{
    public bool AuthoredV5 {get;init;}
    public double ZoomFrom {get;init;}=1;
    public double ZoomTo {get;init;}=1;
    public double PanPercent {get;init;}
    public double EntrySeconds {get;init;}=1;
    public double HoldSeconds {get;init;}=2;
    public double ExitSeconds {get;init;}=1.5;
    public Vector2 PositionOffset {get;init;}
    public Vector2? TargetWorldPosition {get;init;}
    public double ZoomDelta {get;init;}
    public string Ease {get;init;}="smooth";
    public PresentationEnergy Level {get;init;}=PresentationEnergy.MEDIUM;
    public int Priority {get;init;}=50;
    public string BudgetGroup {get;init;}="";
}
/// <summary>Visual-only authored landmarks; never writes clock, targets or gameplay transforms.</summary>
public static class StagePresentationChart
{
    private static readonly Dictionary<string,StageFxEvent[]> Cache=new();
    public static StageFxEvent[] Load(string songId,double duration)
    {
        if(Cache.TryGetValue(songId,out var cached))return cached;
        string path=MainStagePresentationProfile.IsMain(songId)?MainStagePresentationProfile.FxPath(songId):CustomMapRegistry.Find(songId)?.FxPath??throw new ArgumentException("Missing camera registry: "+songId);
        if(!path.EndsWith("_v5.csv",StringComparison.Ordinal))throw new ArgumentException("Current BITE stages require v5 camera data: "+path);
        var events=new List<StageFxEvent>();
        foreach(var row in LocalCsv.Read(path))
        {
            double N(string key)=>LocalCsv.Number(row[key]);
            double time=N("time_sec"),total=N("duration_sec"),hold=N("hold_sec"),move=(total-hold)/2;
            string type=row["event_type"],ease=row["ease"],side=row["target_side"];
            if(time<0||time>=duration||move<=0||hold<0||type is not ("WAVE_CLOSEUP" or "SHARK_CLOSEUP" or "PULLBACK")||ease is not ("CUBIC_IN_OUT" or "SINE_IN_OUT")||side is not ("L" or "R" or "CENTER"))throw new ArgumentException("Invalid v5 camera event: "+path);
            double from=N("zoom_from"),to=N("zoom_to"),pan=N("pan_percent");
            if(from<=0||to<.85||to>1.25||Math.Abs(pan)>.35)throw new ArgumentException("Invalid v5 camera framing: "+path);
            events.Add(new(time,type,1,total,"cyan",side=="L"?"left":side=="R"?"right":"center")
                {AuthoredV5=true,ZoomFrom=from,ZoomTo=to,PanPercent=pan,EntrySeconds=move,HoldSeconds=hold,ExitSeconds=move,Ease=ease,Level=PresentationEnergy.HIGH});
        }
        var result=events.OrderBy(e=>e.Time).ToArray();
        for(int i=1;i<result.Length;i++)if(result[i].Time<result[i-1].Time+result[i-1].Duration)throw new ArgumentException("Overlapping v5 camera shots: "+path);
        Cache.Add(songId,result);return result;
    }
    private static StageFxEvent[] SpaceCameraShots(IEnumerable<StageFxEvent> source)
    {
        string[] shots={"FOCUS_WAVE","FOLLOW_WAVE","ZOOM_TO_SHARK","PULL_BACK","SIDE_PAN","SECTION_TRANSITION","BUILDUP_ZOOM","DROP_ZOOM","PRESSURE_PUSH","INTERFERENCE_FOCUS","camera_zoom","camera_pan","camera_follow_wave","camera_drop"};
        string[] removed={"KICK_PULSE","BASS_PUSH","IMPACT_ZOOM","camera_pulse","camera_kick"};
        var result=new List<StageFxEvent>();var accepted=new List<StageFxEvent>();
        foreach(var e in source.OrderByDescending(e=>e.Priority).ThenBy(e=>e.Time))
        {
            if(removed.Contains(e.Type))continue;
            if(!shots.Contains(e.Type)){result.Add(e);continue;}
            var shot=e with {Duration=Math.Max(4.5,e.EntrySeconds+e.HoldSeconds+e.ExitSeconds)};
            if(accepted.Any(a=>shot.Time<a.Time+a.Duration+5&&a.Time<shot.Time+shot.Duration+5))continue;
            accepted.Add(shot);result.Add(shot);
        }
        return result.OrderBy(e=>e.Time).ToArray();
    }
    /// <summary>Keep coordinated effect bundles, preferring stronger authored musical landmarks.</summary>
    public static StageFxEvent[] ApplyCooldowns(IEnumerable<StageFxEvent> source)
    {
        var groups=source.GroupBy(e=>e.BudgetGroup.Length>0?e.BudgetGroup:e.Time.ToString("R",System.Globalization.CultureInfo.InvariantCulture))
            .Select(g=>(Events:g.ToArray(),Time:g.Min(e=>e.Time),Priority:g.Max(e=>e.Priority),Level:g.Max(e=>e.Level)))
            .OrderByDescending(g=>g.Priority).ThenByDescending(g=>g.Level).ThenBy(g=>g.Time).ToArray();
        var accepted=new List<(StageFxEvent[] Events,double Time,int Priority,PresentationEnergy Level)>();
        foreach(var group in groups)
            if(accepted.All(other=>Math.Abs(group.Time-other.Time)+1e-6>=Math.Max(MainStagePresentationProfile.Cooldown(group.Level),MainStagePresentationProfile.Cooldown(other.Level))))accepted.Add(group);
        return accepted.SelectMany(g=>g.Events).OrderBy(e=>e.Time).ToArray();
    }
}
