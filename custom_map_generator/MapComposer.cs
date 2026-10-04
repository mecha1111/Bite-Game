using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Gamejam2.Data;
using Gamejam2.Gameplay;
using Gamejam2.Rhythm;
using Godot;
namespace Gamejam2.CustomGenerator;
public sealed record GeneratedTarget(double Time,double Start,string Pattern,bool Left,double Strength)
{
    public string Evidence {get;init;}="";
    public string AccentClass {get;init;}="normal";
}
public sealed record MapFiles(string Chart,string Camera,string Interference,string Satiety,double Drain,double PerfectFinal,double MixedFinal);
public sealed record PatternModel(string Id,string Family,int[] CueSlots)
{
    public static PatternModel[] Load()=>LocalCsv.Read("res://data/balance/patterns.csv").Where(r=>r["prey_class"] is "small" or "medium" or "large").Select(r=>new PatternModel(r["pattern_id"],r["prey_class"],r["cue_slots"].Split(';').Select(int.Parse).ToArray())).ToArray();
}
public static class ChartGenerator
{
    private sealed record Candidate(Attack Attack,MusicSection Section,double Score,string Accent);
    public static GeneratedTarget[] Compose(Analysis music,IReadOnlyList<PatternModel> patterns)
    {
        double lead=240/music.PulseBpm;
        var candidates=new List<Candidate>();
        foreach(var section in music.Sections)
        {
            var attacks=music.Attacks.Where(a=>a.Time>=section.Start&&a.Time<section.End).ToArray();
            if(attacks.Length==0)continue;
            double median=attacks.Select(a=>a.Strength).Order().ElementAt(attacks.Length/2);
            foreach(var a in attacks)
            {
                if(a.Time<lead*.65||a.Time>=music.Duration-.5)continue;
                bool boundary=music.Changes.Any(c=>Math.Abs(c.Time-a.Time)<.65);
                bool phraseEnd=!attacks.Any(n=>n.Time>a.Time&&n.Time<a.Time+60/music.Bpm*.9);
                double band=Math.Max(a.Bass,Math.Max(a.Mid,a.Percussion));
                if(section.Kind=="break"&&!boundary&&a.Strength<median*1.7)continue;
                if(a.Strength<median*(section.Kind=="low_activity"?1.15:.85)&&band<1.6&&!boundary&&!phraseEnd)continue;
                double salience=.55*a.Strength+.45*band+(boundary?.8:0)+(phraseEnd?.35:0);
                candidates.Add(new(a,section,Math.Pow(salience,1.7),boundary?"section_impact":phraseEnd?"phrase_end":a.Strength>median*1.5?"strong":"normal"));
            }
        }
        candidates=candidates.OrderBy(c=>c.Attack.Time).ToList();
        if(candidates.Count==0)throw new InvalidOperationException("음악에서 연주할 만한 타점을 찾지 못했습니다.");
        // Weighted interval selection keeps the musically strongest payoff, not the first threshold crossing.
        var best=new double[candidates.Count+1];var previous=new int[candidates.Count];var take=new bool[candidates.Count];
        for(int i=0;i<candidates.Count;i++)
        {
            double start=Math.Max(0,candidates[i].Attack.Time-lead);int lo=0,hi=i;
            while(lo<hi){int mid=(lo+hi)/2;if(candidates[mid].Attack.Time<=start-.30)lo=mid+1;else hi=mid;}
            previous[i]=lo;double value=best[lo]+candidates[i].Score;
            take[i]=value>best[i];best[i+1]=Math.Max(value,best[i]);
        }
        var selected=new List<Candidate>();
        for(int i=candidates.Count;i>0;)if(take[i-1]){selected.Add(candidates[i-1]);i=previous[i-1];}else i--;
        selected.Reverse();var result=new List<GeneratedTarget>();
        foreach(var c in selected)
        {
            double density=music.Attacks.Count(a=>Math.Abs(a.Time-c.Attack.Time)<1.5)/3.0;
            string family=c.Section.Kind!="active"||c.Attack.Bass>Math.Max(c.Attack.Percussion,c.Attack.Mid)*1.35&&c.Accent!="normal"?"large":density>=2.3&&c.Attack.Evidence=="percussion_attack"?"small":"medium";
            double start=Math.Max(0,c.Attack.Time-lead),slot=30/music.PulseBpm;
            double Fit(PatternModel p)
            {
                double score=0;
                foreach(int cue in p.CueSlots)
                {
                    double time=start+cue*slot;if(time>=c.Attack.Time)continue;
                    double hit=music.Attacks.Where(a=>Math.Abs(a.Time-time)<Math.Max(.085,slot*.32)).Select(a=>a.Strength*(1-Math.Abs(a.Time-time)/Math.Max(.085,slot*.32))).DefaultIfEmpty(0).Max();
                    score+=Math.Min(3,hit);
                }
                return score/Math.Sqrt(p.CueSlots.Length);
            }
            var pattern=patterns.Where(p=>p.Family==family).OrderByDescending(Fit).First();
            result.Add(new(c.Attack.Time,start,pattern.Id,result.Count%2==0,c.Attack.Strength){Evidence=c.Attack.Evidence,AccentClass=c.Accent});
        }
        return result.ToArray();
    }
    public static string Csv(string id,Analysis music,GeneratedTarget[] targets)
    {
        var b=new StringBuilder("song_id,phrase_id,phrase_side,phrase_start_time_sec,pattern_id,target_index,target_count,target_time_sec,target_gap_from_prev_ms,wave_event_id,separate_wave_event,is_multi_target,accent_strength,accent_class,source_bpm,gameplay_bpm\n");
        for(int i=0;i<targets.Length;i++){var t=targets[i];b.AppendLine(FormattableString.Invariant($"{id},{id}_P{i:D4},{(t.Left?"L":"R")},{t.Start:F6},{t.Pattern},1,1,{t.Time:F6},{(i==0?0:(t.Time-targets[i-1].Time)*1000):F3},{id}_W{i:D4},True,False,{t.Strength:F4},{t.AccentClass},{music.Bpm:F6},{music.PulseBpm:F6}"));}return b.ToString();
    }
}
public sealed record GeneratedInterference(double Time,string Type,double Duration,string Reason);
public static class InterferenceGenerator
{
    public static GeneratedInterference[] Compose(Analysis music,GeneratedTarget[] targets)
    {
        var durations=LocalCsv.Read("res://data/balance/interference_effects.csv").ToDictionary(r=>r["type"],r=>LocalCsv.Number(r["duration_seconds"]));
        double accentFloor=targets.Select(t=>t.Strength).Order().ElementAt(targets.Length/2)*1.25;
        var cues=music.Changes.Select(c=>(Time:c.Time,Kind:c.Kind,Strength:c.Strength))
            .Concat(targets.Where(t=>t.Strength>=accentFloor&&t.AccentClass!="normal").Select(t=>(Time:t.Time,Kind:"accent",Strength:t.Strength)))
            .OrderBy(c=>c.Time).ToArray();
        var result=new List<GeneratedInterference>();
        foreach(var cue in cues)
        {
            double phase=cue.Time/music.Duration;if(phase<.28||phase>.94)continue;
            var section=music.Sections.First(s=>cue.Time>=s.Start&&cue.Time<s.End);
            if(section.Kind!="active")continue;
            var target=targets.Where(t=>Math.Abs(t.Time-cue.Time)<2).OrderBy(t=>Math.Abs(t.Time-cue.Time)).FirstOrDefault();
            if(target==null)continue;
            var attack=music.Attacks.OrderBy(a=>Math.Abs(a.Time-target.Time)).First();
            string type=cue.Kind=="buildup"?"whale":cue.Kind=="section"?"sardine":attack.Bass>attack.Percussion*1.15?"ship_horn":"fishing_float";
            double at=target.Start;
            // Middle: single mild role. Later: stronger roles. Only a late structural climax can combine two.
            if(phase<.55)type=attack.Percussion>attack.Bass?"fishing_float":"ship_horn";
            double cooldown=phase<.55?22:phase<.8?16:12;
            if(result.Any(e=>at<e.Time+e.Duration+2)||result.Count>0&&at-result[^1].Time<cooldown)continue;
            if(at<music.Duration*.28||at+durations[type]>=music.Duration)continue;
            result.Add(new(at,type,durations[type],cue.Kind));
            bool climax=phase>=.80&&(cue.Kind is "drop" or "section")&&cue.Strength>=1.4;
            if(climax&&(type is "ship_horn" or "fishing_float")&&at+durations["sardine"]<music.Duration)
                result.Add(new(at,"sardine",durations["sardine"],"limited_climax_pair"));
        }
        return result.ToArray();
    }
    public static string Csv(string id,IReadOnlyList<GeneratedInterference> events)
    {
        var b=new StringBuilder("song_id,type,time_seconds\n");
        foreach(var e in events)b.AppendLine(FormattableString.Invariant($"{id},{e.Type},{e.Time:F6}"));return b.ToString();
    }
}
public sealed record GeneratedCamera(double Time,string Type,string Side,double Duration,double Hold,double Zoom,double Pan,string Reason);
public static class CameraGenerator
{
    public static GeneratedCamera[] Compose(Analysis music,GeneratedTarget[] targets,IReadOnlyList<GeneratedInterference> interference)
    {
        double accentFloor=targets.Select(t=>t.Strength).Order().ElementAt(targets.Length/2)*1.20;
        var cues=music.Changes.Select(c=>(Time:c.Time,Kind:c.Kind,Strength:c.Strength))
            .Concat(targets.Where(t=>t.AccentClass!="normal"&&t.Strength>=accentFloor).Select(t=>(Time:t.Time,Kind:t.AccentClass,Strength:t.Strength)))
            .OrderByDescending(c=>c.Strength).ToArray();
        var shots=new List<GeneratedCamera>();
        foreach(var cue in cues)
        {
            var target=targets.Where(t=>Math.Abs(t.Time-cue.Time)<3).OrderBy(t=>Math.Abs(t.Time-cue.Time)).FirstOrDefault();
            if(target==null)continue;
            double at=cue.Kind=="release"?cue.Time:Math.Max(0,target.Time-1.25);
            double duration=5.0,hold=2.0;
            if(at+duration>=music.Duration||shots.Any(s=>Math.Abs(s.Time-at)<duration+7))continue;
            if(interference.Any(e=>(e.Type is "whale" or "sardine")&&at<e.Time+e.Duration&&at+duration>e.Time))continue;
            var section=music.Sections.First(s=>cue.Time>=s.Start&&cue.Time<s.End);
            string type=section.Kind!="active"||cue.Kind=="release"?"PULLBACK":cue.Kind=="section"?"SECTION_TRANSITION":cue.Kind=="buildup"?"SIDE_PAN":(cue.Kind is "drop" or "section_impact")||target.Pattern=="b1"?"SHARK_CLOSEUP":"WAVE_CLOSEUP";
            // Camera direction is selected only for sparse wave shots, never copied from every L/R phrase.
            string side=type=="WAVE_CLOSEUP"?(target.Left?"L":"R"):"CENTER";
            double pan=type=="SIDE_PAN"?.10:side=="L"?-.12:side=="R"?.12:0;
            shots.Add(new(at,type,side,duration,hold,type=="PULLBACK"?.94:type=="SIDE_PAN"?1:type=="SECTION_TRANSITION"?1.04:type=="SHARK_CLOSEUP"?1.08:1.10,pan,cue.Kind));
        }
        return shots.OrderBy(s=>s.Time).ToArray();
    }
    public static string Csv(IReadOnlyList<GeneratedCamera> shots)
    {
        var b=new StringBuilder("time_sec,event_type,target_side,duration_sec,hold_sec,zoom_from,zoom_to,pan_percent,ease\n");
        foreach(var s in shots)b.AppendLine(FormattableString.Invariant($"{s.Time:F6},{s.Type},{s.Side},{s.Duration:F6},{s.Hold:F6},1.0,{s.Zoom:F6},{s.Pan:F6},SINE_IN_OUT"));return b.ToString();
    }
}
public static class SatietyAutoBalance
{
    public static MapFiles Compose(string id,Analysis music,GeneratedTarget[] targets,GeneratedInterference[] interference)
    {
        var b=new StringBuilder("song_id,section_id,start_time_sec,end_time_sec,section_type,drain_multiplier\n");
        var sections=music.Sections.Select((s,i)=>new SatietyDrainSection(s.Start,s.End,s.Kind=="break"?.25:s.Kind=="low_activity"?.5:1,s.Kind,"local_"+i)).ToArray();
        foreach(var s in sections)b.AppendLine(FormattableString.Invariant($"{id},{s.Id},{s.Start:F6},{s.End:F6},{s.Type},{s.Multiplier:F2}"));
        var balance=LocalCsv.Read("res://data/balance/satiety.csv").Single();var judgment=LocalCsv.Read("res://data/balance/judgment.csv").Single();
        double start=LocalCsv.Number(balance["starting_satiety"]),max=LocalCsv.Number(balance["max_satiety"]),threshold=LocalCsv.Number(balance["clear_threshold"]),recovery=LocalCsv.Number(balance["base_fish_recovery"]);
        double perfect=LocalCsv.Number(judgment["perfect_multiplier"])*recovery,good=LocalCsv.Number(judgment["good_multiplier"])*recovery,bad=LocalCsv.Number(judgment["bad_multiplier"])*recovery;
        var patterns=PatternModel.Load().ToDictionary(p=>p.Id);
        var prey=targets.Select((t,i)=>new PreyPhrase(i,t.Start,t.Time,t.Left,WaveKind.Large,1,patterns[t.Pattern].CueSlots,8,Array.Empty<double>(),t.Pattern,"generated",t.Time)).ToArray();
        var drainProfile=SatietyDrainProfile.Create(music.Duration,sections,prey);
        var policy=BitePolicy.Load();
        double Simulate(double rate,bool mixed)
        {
            double value=start,previous=0;int missStreak=0;
            for(int i=0;i<targets.Length;i++)
            {
                var t=targets[i];value-=drainProfile.EffectiveSecondsBetween(previous,t.Time)*rate;if(value<=0)return value;
                // Representative 60% PERFECT / 25% GOOD / 10% BAD / 5% MISS, including runtime penalties.
                int choice=i%20;bool miss=mixed&&choice==19;
                double gain=!mixed?perfect:choice<12?perfect:choice<17?good:choice<19?bad:0;
                value=Math.Min(max,value+gain);if(miss){missStreak++;value-=policy.Penalty(missStreak);}else missStreak=0;
                if(value<=0)return value;previous=t.Time;
            }
            return value;
        }
        double low=0,high=Math.Max(1,(start+targets.Length*perfect)/Math.Max(1,drainProfile.TotalEffectiveSeconds));
        if(Simulate(0,false)<threshold||Simulate(0,true)<threshold)throw new InvalidOperationException("음악의 타점만으로 클리어 가능한 밸런스를 만들지 못했습니다.");
        for(int i=0;i<64;i++){double rate=(low+high)/2;if(Simulate(rate,false)>=Math.Min(max,threshold+100)&&Simulate(rate,true)>=threshold)low=rate;else high=rate;}
        double drain=low*.95,perfectFinal=Simulate(drain,false),mixedFinal=Simulate(drain,true);
        if(perfectFinal<threshold||mixedFinal<threshold)throw new InvalidOperationException("생성된 포만감 시뮬레이션이 클리어 기준에 미달합니다.");
        // Last: cinematography consumes the immutable chart and interference plan, never moves a target.
        var camera=CameraGenerator.Compose(music,targets,interference);
        return new(ChartGenerator.Csv(id,music,targets),CameraGenerator.Csv(camera),InterferenceGenerator.Csv(id,interference),b.ToString(),drain,perfectFinal,mixedFinal);
    }
}
