using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Gamejam2.Data;
using Gamejam2.Gameplay;
namespace Gamejam2.CustomGenerator;
public sealed record GeneratedTarget(double Time,double Start,string Pattern,bool Left,double Strength);
public sealed record MapFiles(string Chart,string Camera,string Interference,string Satiety,double Drain,double PerfectFinal,double MixedFinal);
public static class ChartGenerator
{
    public static GeneratedTarget[] Compose(Analysis music)
    {
        double lead=240/music.PulseBpm;var chosen=new List<GeneratedTarget>();double previous=-lead-1;
        foreach(var attack in music.Attacks)
        {
            var section=music.Sections.First(s=>attack.Time>=s.Start&&attack.Time<s.End);
            double threshold=music.Attacks.Where(a=>a.Time>=section.Start&&a.Time<section.End).Select(a=>a.Strength+a.Bass*2).DefaultIfEmpty(1).Average();
            double value=attack.Strength+attack.Bass*2;
            if(value<threshold*(music.Confidence<.25?1.4:section.Kind=="break"?1.8:.95)||attack.Time-previous<lead+.45||attack.Time>music.Duration-.5)continue;
            string pattern=section.Kind!="active"?"b1":attack.Percussion>attack.Bass*2?"s1":attack.Bass>2?"m2":"m1";
            chosen.Add(new(attack.Time,Math.Max(0,attack.Time-lead),pattern,chosen.Count%2==0,value));previous=attack.Time;
        }
        // Low confidence uses only real strongest attacks, never manufactured evenly spaced notes.
        if(chosen.Count==0)foreach(var a in music.Attacks.OrderByDescending(a=>a.Strength).Take(8).OrderBy(a=>a.Time))
            if(a.Time-previous>=lead+.45&&a.Time<music.Duration-.5){chosen.Add(new(a.Time,Math.Max(0,a.Time-lead),"m1",chosen.Count%2==0,a.Strength));previous=a.Time;}
        if(chosen.Count==0)throw new InvalidOperationException("음악에서 명확한 리듬을 찾지 못했습니다. 다른 음원을 선택해 주세요.");
        return chosen.ToArray();
    }
    public static string Csv(string id,Analysis music,GeneratedTarget[] targets)
    {
        var b=new StringBuilder("song_id,phrase_id,phrase_side,phrase_start_time_sec,pattern_id,target_index,target_count,target_time_sec,target_gap_from_prev_ms,wave_event_id,separate_wave_event,is_multi_target,accent_strength,accent_class,source_bpm,gameplay_bpm\n");
        for(int i=0;i<targets.Length;i++){var t=targets[i];b.AppendLine(FormattableString.Invariant($"{id},{id}_P{i:D4},{(t.Left?"L":"R")},{t.Start:F6},{t.Pattern},1,1,{t.Time:F6},{(i==0?0:(t.Time-targets[i-1].Time)*1000):F3},{id}_W{i:D4},True,False,{t.Strength:F4},normal,{music.Bpm:F6},{music.PulseBpm:F6}"));}return b.ToString();
    }
}
public static class CameraGenerator
{
    public static string Csv(Analysis music,GeneratedTarget[] targets)
    {
        var b=new StringBuilder("time_sec,event_type,target_side,duration_sec,hold_sec,zoom_from,zoom_to,pan_percent,ease\n");double previous=-20;
        foreach(var change in music.Changes)
        {
            var target=targets.FirstOrDefault(t=>t.Time>=change.Time);if(target==null||target.Start-previous<12||music.Confidence<.25)continue;
            bool quiet=change.Kind=="drop"||music.Sections.First(s=>target.Time>=s.Start&&target.Time<s.End).Kind!="active";b.AppendLine(FormattableString.Invariant($"{target.Start:F6},{(quiet?"PULLBACK":"WAVE_CLOSEUP")},{(quiet?"CENTER":target.Left?"L":"R")},4.0,2.0,1.0,{(quiet?.92:1.10):F2},{(quiet?0:target.Left?-.18:.18):F2},CUBIC_IN_OUT"));previous=target.Start;
        }return b.ToString();
    }
}
public static class InterferenceGenerator
{
    public static string Csv(string id,Analysis music,GeneratedTarget[] targets)
    {
        var b=new StringBuilder("song_id,type,time_seconds\n");string[] roles={"fishing_float","sardine","whale","ship_horn"};double previous=-30;int index=0;
        foreach(var section in music.Sections.Skip(1))
        {
            if(section.Start<music.Duration*.3||section.Kind!="active"||section.Start-previous<16||music.Confidence<.25)continue;
            var at=targets.FirstOrDefault(t=>t.Time>=section.Start)?.Start;if(at==null)continue;
            b.AppendLine(FormattableString.Invariant($"{id},{roles[index++%4]},{at.Value:F6}"));previous=at.Value;
        }return b.ToString();
    }
}
public static class SatietyAutoBalance
{
    public static MapFiles Compose(string id,Analysis music,GeneratedTarget[] targets)
    {
        var b=new StringBuilder("song_id,section_id,start_time_sec,end_time_sec,section_type,drain_multiplier\n");
        for(int i=0;i<music.Sections.Length;i++){var s=music.Sections[i];b.AppendLine(FormattableString.Invariant($"{id},local_{i},{s.Start:F6},{s.End:F6},{s.Kind},{(s.Kind=="break"?.25:s.Kind=="low_activity"?.5:1):F2}"));}
        var balance=LocalCsv.Read("res://data/balance/satiety.csv").Single();var judgment=LocalCsv.Read("res://data/balance/judgment.csv").Single();
        double start=LocalCsv.Number(balance["starting_satiety"]),max=LocalCsv.Number(balance["max_satiety"]),recovery=LocalCsv.Number(balance["base_fish_recovery"]);
        double perfect=LocalCsv.Number(judgment["perfect_multiplier"])*recovery,good=LocalCsv.Number(judgment["good_multiplier"])*recovery;
        // Use the same immutable section/gap/outro integration as runtime, including long no-food safety.
        double Effective(double a,double z)
        {
            double result=0;foreach(var s in music.Sections)result+=Math.Max(0,Math.Min(z,s.End)-Math.Max(a,s.Start))*(s.Kind=="break"?.25:s.Kind=="low_activity"?.5:1);return result;
        }
        double Simulate(double rate,bool mixed)
        {
            double value=start,previous=0;
            for(int i=0;i<targets.Length;i++)
            {
                var t=targets[i];value-=Effective(previous,t.Time)*rate;if(value<=0)return value;
                value=Math.Min(max,value+(mixed?(i%10==9?-35:i%3==0?good:perfect):perfect));previous=t.Time;
            }return value;
        }
        double drain=Math.Max(.1,(start+targets.Length*good-850)/Math.Max(1,Effective(0,targets[^1].Time)));
        for(int i=0;i<100&&(Simulate(drain,false)<850||Simulate(drain,true)<550);i++)drain*=.9;
        if(Simulate(drain,false)<500||Simulate(drain,true)<500)throw new InvalidOperationException("플레이 가능한 밸런스를 만들지 못했습니다.");
        return new(ChartGenerator.Csv(id,music,targets),CameraGenerator.Csv(music,targets),InterferenceGenerator.Csv(id,music,targets),b.ToString(),drain,Simulate(drain,false),Simulate(drain,true));
    }
}
