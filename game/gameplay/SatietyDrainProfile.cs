using System;
using System.Collections.Generic;
using System.Linq;
using Gamejam2.Data;
using Godot;
using Gamejam2.Rhythm;
namespace Gamejam2.Gameplay;
public sealed record SatietyDrainSection(double Start,double End,double Multiplier,string Type,string Id);
public sealed record ScheduledFoodlessGap(double Start,double End);
public sealed record SatietyDrainSpan(double Start,double End,double Multiplier,string Type,bool SafetyApplied);
/// <summary>Compiled once from authored music sections and scheduled encounter windows.
/// Consumed/MISS prey state never changes this immutable drain timeline.</summary>
public sealed class SatietyDrainProfile
{
    public IReadOnlyList<SatietyDrainSection> AuthoredSections { get; }
    public IReadOnlyList<ScheduledFoodlessGap> FoodlessGaps { get; }
    public IReadOnlyList<SatietyDrainSpan> Spans { get; }
    public double GapThresholdSeconds { get; }
    public double SafetyMultiplierCap { get; }
    public double Duration { get; }
    public double TotalEffectiveSeconds=>_prefix[^1];
    private readonly double[] _ends,_prefix;
    private readonly double _lateCapture;
    private SatietyDrainProfile(double duration,SatietyDrainSection[] sections,IReadOnlyList<PreyPhrase> prey,double lateCapture,double threshold,double cap)
    {
        Duration=duration;AuthoredSections=sections;_lateCapture=lateCapture;GapThresholdSeconds=threshold;SafetyMultiplierCap=cap;
        var gaps=new List<ScheduledFoodlessGap>();double previous=0;
        foreach(var p in prey)
        {
            if(p.StartSeconds>previous)gaps.Add(new(previous,p.StartSeconds));
            previous=Math.Max(previous,p.LastTargetSeconds+lateCapture);
        }
        if(previous<duration)gaps.Add(new(previous,duration));FoodlessGaps=gaps;
        var safe=gaps.Where(g=>g.End-g.Start>=threshold-1e-9).ToArray();
        double outro=Math.Min(duration,prey.Count==0?0:prey.Max(p=>p.LastTargetSeconds)+lateCapture);
        var boundaries=sections.SelectMany(s=>new[]{s.Start,s.End}).Concat(safe.SelectMany(g=>new[]{g.Start,g.End})).Append(0).Append(outro).Append(duration)
            .Select(t=>Math.Clamp(t,0,duration)).Distinct().Order().ToArray();
        var spans=new List<SatietyDrainSpan>();int section=0,gap=0;
        for(int i=0;i<boundaries.Length-1;i++)
        {
            double a=boundaries[i],b=boundaries[i+1],middle=(a+b)/2;
            while(section+1<sections.Length&&sections[section].End<=middle)section++;
            while(gap<safe.Length&&safe[gap].End<=middle)gap++;
            var authored=sections.Length==0?new SatietyDrainSection(0,duration,1,"active","unmarked"):sections[section];
            bool safety=gap<safe.Length&&safe[gap].Start<=middle&&safe[gap].End>middle;
            spans.Add(new(a,b,middle>=outro?0:safety?Math.Min(authored.Multiplier,cap):authored.Multiplier,middle>=outro?"outro":authored.Type,safety));
        }
        Spans=spans;_ends=spans.Select(s=>s.End).ToArray();_prefix=new double[spans.Count+1];
        for(int i=0;i<spans.Count;i++)_prefix[i+1]=_prefix[i]+(spans[i].End-spans[i].Start)*spans[i].Multiplier;
    }
    public static SatietyDrainProfile Load(string song,double duration,IReadOnlyList<PreyPhrase> prey)
    {
        var custom=CustomMapRegistry.Find(song);
        var rows=LocalCsv.Read(custom!=null&&!string.IsNullOrEmpty(custom.SatietySectionsPath)?custom.SatietySectionsPath:"res://data/balance/satiety_sections.csv").Where(r=>r["song_id"]==song).ToArray();
        var sections=rows.Select(r=>new SatietyDrainSection(LocalCsv.Number(r["start_time_sec"]),LocalCsv.Number(r["end_time_sec"]),LocalCsv.Number(r["drain_multiplier"]),r["section_type"],r["section_id"])).ToArray();
        double through=0;
        foreach(var s in sections)
        {
            if(!double.IsFinite(s.Start)||!double.IsFinite(s.End)||!double.IsFinite(s.Multiplier)||s.Multiplier<0||s.Multiplier>1||s.End<=s.Start||Math.Abs(s.Start-through)>1e-7||s.Type is not ("active" or "low_activity" or "break"))
                throw new ArgumentException("Invalid/gapped/overlapping satiety sections: "+song);
            through=s.End;
        }
        if(sections.Length>0&&Math.Abs(through-duration)>1e-5)throw new ArgumentException("Satiety sections must cover the full song: "+song);
        var policy=LocalCsv.Read("res://data/balance/satiety_drain_policy.csv").Single();
        double threshold=LocalCsv.Number(policy["no_food_gap_seconds"]),cap=LocalCsv.Number(policy["no_food_multiplier_cap"]);
        if(!double.IsFinite(threshold)||threshold<=0||!double.IsFinite(cap)||cap<0||cap>1)throw new ArgumentException("Invalid no-food safety policy.");
        double late=GD.Load<InputCaptureWindows>("res://game/rhythm/InputCaptureWindows.tres").LateSeconds;
        return new(duration,sections,prey,late,threshold,cap);
    }
    public SatietyDrainProfile Rebuild(double duration,IReadOnlyList<PreyPhrase> prey)=>new(duration,AuthoredSections.ToArray(),prey,_lateCapture,GapThresholdSeconds,SafetyMultiplierCap);
    private int SpanAt(double time)
    {
        int index=Array.BinarySearch(_ends,Math.Clamp(time,0,Duration));
        return Math.Min(index>=0?index+1:~index,Spans.Count-1);
    }
    public double MultiplierAt(double time)=>Spans[SpanAt(time)].Multiplier;
    public double EffectiveSecondsAt(double time)
    {
        double t=Math.Clamp(time,0,Duration);int i=SpanAt(t);
        return _prefix[i]+(t-Spans[i].Start)*Spans[i].Multiplier;
    }
    public double EffectiveSecondsBetween(double start,double end)=>Math.Max(0,EffectiveSecondsAt(end)-EffectiveSecondsAt(start));
    public double TimeAfterEffectiveDrain(double start,double end,double effectiveSeconds)
    {
        for(int i=SpanAt(start);i<Spans.Count;i++)
        {
            var s=Spans[i];double a=Math.Max(start,s.Start),b=Math.Min(end,s.End),amount=Math.Max(0,b-a)*s.Multiplier;
            if(s.Multiplier>0&&effectiveSeconds<=amount)return a+effectiveSeconds/s.Multiplier;
            effectiveSeconds-=amount;if(b>=end)break;
        }
        return end;
    }
}
