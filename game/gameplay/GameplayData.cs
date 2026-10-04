using System;
using System.Linq;
using System.Collections.Generic;
using Godot;
using Gamejam2.Data;
using Gamejam2.Rhythm;
namespace Gamejam2.Gameplay;
public sealed class PhraseTargetEvent(double targetTime,int ordinal)
{
    public double TargetTime {get;}=targetTime;
    public int Ordinal {get;}=ordinal;
    public string WaveEventId {get;init;}="";
    public double CueTime {get;init;}=double.NaN;
    public RhythmJudgment? JudgmentState {get;internal set;}
    public bool ResolvedState {get;internal set;}
}
public sealed record PreyPhrase(int Index, double StartSeconds, double TargetSeconds, bool FromLeft, WaveKind WaveKind, int PulseCount, int[] CueSlots, int TargetSlot, double[] Cues,string PatternId,string TimingGrid,double NearestBeatSeconds)
{
    public string PhraseId {get;init;}="";
    public PhraseTargetEvent[] TargetEvents {get;init;}={new(TargetSeconds,0)};
    public double LastTargetSeconds=>TargetEvents[^1].TargetTime;
    public string SectionId { get; init; } = "";
    public string Notes { get; init; } = "";
    public double ResolveDeadlineSeconds { get; init; } = double.PositiveInfinity;
}
/// <summary>Local composition data: local CSV -> immutable phrases + existing RhythmChart timestamps.</summary>
public sealed class GameplayData
{
    public double BeatOffsetSeconds { get; private set; }
    public double MusicalBpm { get; private set; }
    public string SongName { get; private set; } = "";
    public double DurationSeconds { get; private set; }
    public double DrainPerSecond { get; private set; }
    public SatietyDrainProfile? DrainProfile { get; private set; }
    public double MaxSatiety { get; private set; }
    public double StartingSatiety { get; private set; }
    public double ClearThreshold { get; private set; }
    public double BaseRecovery { get; private set; }
    public string AudioPath { get; private set; } = "";
    public string ChartPath { get; private set; } = "";
    public RhythmChart Chart { get; } = new();
    public List<PreyPhrase> Phrases { get; } = new();
    public int TargetCount=>Phrases.Sum(p=>p.TargetEvents.Length);
    public double LastCatchOpportunityTime=>Phrases.Count==0?0:Phrases.Max(p=>p.LastTargetSeconds);
    public Dictionary<RhythmJudgment,double> Multipliers { get; } = new();
    public Dictionary<RhythmJudgment,long> ScoreRewards { get; } = new();
    public Dictionary<RhythmJudgment,JudgmentEffect> JudgmentEffects { get; }
    public GameplayData():this(JudgmentPresentation.Load()){}
    private GameplayData(Dictionary<RhythmJudgment,JudgmentEffect> effects){JudgmentEffects=effects;}
    public void UseAudioDuration(double audioSeconds)
    {
        if(!double.IsFinite(audioSeconds)||audioSeconds<=0)throw new ArgumentException("Invalid selected song duration.");
        if(Phrases.Any(p=>p.LastTargetSeconds>=audioSeconds))throw new ArgumentException("Prey target lies outside the selected song.");
        DurationSeconds=audioSeconds;Chart.SongEndTimeSeconds=audioSeconds;
        if(DrainProfile!=null)DrainProfile=DrainProfile.Rebuild(audioSeconds,Phrases);
        foreach(var e in Chart.Events.Where(e=>e.EventType==RhythmEventType.Section&&e.SectionId=="종료"))e.TimeSeconds=audioSeconds;
    }
    public static void ValidateEncounterGap(string songId, int previousIndex, double previousStart, double previousTarget, double nextStart, double lateCapture, string previousPattern="",string nextPattern="")
    {
        double resolve=previousTarget+lateCapture;
        if(nextStart<resolve-1e-9)throw new ArgumentException($"Prey overlap: SongId={songId}, previous #{previousIndex} pattern={previousPattern} {previousStart:F6}->{resolve:F6}, next #{previousIndex+1} pattern={nextPattern} start={nextStart:F6}, overlap={resolve-nextStart:F6}s");
    }
    private void LoadBalance()
    {
        var balance=LocalCsv.Read("res://data/balance/satiety.csv").Single();
        MaxSatiety=LocalCsv.Number(balance["max_satiety"]); StartingSatiety=LocalCsv.Number(balance["starting_satiety"]);
        ClearThreshold=LocalCsv.Number(balance["clear_threshold"]); BaseRecovery=LocalCsv.Number(balance["base_fish_recovery"]);
        var judgment=LocalCsv.Read("res://data/balance/judgment.csv").Single();
        foreach(var (kind,key) in new[]{(RhythmJudgment.Perfect,"perfect"),(RhythmJudgment.Good,"good"),(RhythmJudgment.Bad,"bad"),(RhythmJudgment.Miss,"miss")})
        { var value=LocalCsv.Number(judgment[key+"_multiplier"]); if(value<0||value>1) throw new ArgumentException("판정 배율 범위 오류"); Multipliers.Add(kind,value); ScoreRewards.Add(kind,long.Parse(judgment[key+"_score_reward"])); }
    }
    /// <summary>Tutorial checkpoints use the same authored pattern slots and judgment resources.</summary>
    public static GameplayData Tutorial(string audioPath,double duration,double bpm,double offset,double start,double end,string[] patternIds,string side,double leadSeconds)
    {
        // Shared economy/presentation configuration, with tutorial-only rhythm events.
        var data=new GameplayData();data.LoadBalance();
        data.AudioPath=audioPath;data.ChartPath="res://data/tutorial/lessons.csv";data.SongName="파동의 시작";
        data.DurationSeconds=duration;data.Chart.SongEndTimeSeconds=duration;data.MusicalBpm=data.Chart.Bpm=bpm;data.BeatOffsetSeconds=offset;data.DrainPerSecond=0;
        if(bpm<=0||!double.IsFinite(bpm)||!double.IsFinite(offset))throw new ArgumentException("Tutorial timing configuration invalid.");
        var patterns=LocalCsv.Read("res://data/balance/patterns.csv").ToDictionary(r=>r["pattern_id"]);
        double beat=60/bpm;
        double first=offset+Math.Ceiling((start+leadSeconds-offset)/beat-1e-9)*beat;
        for(double at=first;at+4*beat+.4<end;at+=Math.Ceiling((4*beat+.65)/beat)*beat)
        {
            int index=data.Phrases.Count;string id=patternIds[index%patternIds.Length];var row=patterns[id];
            int[] slots=row["cue_slots"].Split(';').Select(int.Parse).ToArray();double target=at+4*beat;
            double[] cues=slots.Select(slot=>at+slot*.5*beat).ToArray();
            bool left=side=="left"||(side=="alternate"&&index%2==0);
            var phrase=new PreyPhrase(index,at,target,left,Enum.Parse<WaveKind>(row["wave_kind"],true),int.Parse(row["pulse_count"]),slots,8,cues,id,"tutorial_checkpoint",target);
            if(index>0)ValidateEncounterGap("tutorial",index-1,data.Phrases[^1].StartSeconds,data.Phrases[^1].TargetSeconds,at,.4);
            data.Phrases.Add(phrase);
            foreach(double cue in cues)data.Chart.Events.Add(new(){TimeSeconds=cue,EventType=RhythmEventType.Cue,CueId=index.ToString(),IsHittable=false});
            data.Chart.Events.Add(new(){TimeSeconds=target,EventType=RhythmEventType.InputTarget,CueId=index.ToString(),IsHittable=true});
        }
        if(data.Phrases.Count==0)throw new ArgumentException("Tutorial checkpoint cannot fit a beat-5 phrase.");
        return data;
    }
    /// <summary>Precompiled plain C# plans: no Godot resources or I/O during lesson changes.</summary>
    public static PreyPhrase[] TutorialPlan(GameplayData template,double start,double end,double lead,int patternCount)
    {
        double beat=60/template.Chart.Bpm,offset=template.BeatOffsetSeconds;
        double first=offset+Math.Ceiling((start+lead-offset)/beat-1e-9)*beat;
        var phrases=new List<PreyPhrase>();
        for(double at=first;at+4*beat+.4<end;at+=6*beat)
        {
            int index=phrases.Count;var p=template.Phrases[index%patternCount];double target=at+4*beat;
            bool left=template.Phrases.All(p=>p.FromLeft)||(!template.Phrases.All(p=>!p.FromLeft)&&index%2==0);
            phrases.Add(new(index,at,target,left,p.WaveKind,p.PulseCount,p.CueSlots,8,p.CueSlots.Select(slot=>at+slot*.5*beat).ToArray(),p.PatternId,"tutorial_continuous",target));
        }
        if(phrases.Count==0)throw new ArgumentException("Practice segment cannot fit a target.");return phrases.ToArray();
    }
    public static GameplayData TutorialFromPlan(GameplayData template,PreyPhrase[] plan)
    {
        var data=new GameplayData(template.JudgmentEffects){AudioPath=template.AudioPath,ChartPath=template.ChartPath,SongName=template.SongName,
            DurationSeconds=template.DurationSeconds,MusicalBpm=template.MusicalBpm,BeatOffsetSeconds=template.BeatOffsetSeconds,
            MaxSatiety=template.MaxSatiety,StartingSatiety=template.StartingSatiety,ClearThreshold=template.ClearThreshold,BaseRecovery=template.BaseRecovery};
        foreach(var pair in template.Multipliers)data.Multipliers.Add(pair.Key,pair.Value);
        foreach(var pair in template.ScoreRewards)data.ScoreRewards.Add(pair.Key,pair.Value);
        data.Chart.Bpm=template.Chart.Bpm;data.Chart.SongEndTimeSeconds=template.DurationSeconds;data.Phrases.AddRange(plan.Select(p=>p with {TargetEvents=p.TargetEvents.Select(t=>new PhraseTargetEvent(t.TargetTime,t.Ordinal)).ToArray()}));
        foreach(var p in plan){foreach(double cue in p.Cues)data.Chart.Events.Add(new(){TimeSeconds=cue,EventType=RhythmEventType.Cue,CueId=p.Index.ToString(),IsHittable=false});data.Chart.Events.Add(new(){TimeSeconds=p.TargetSeconds,EventType=RhythmEventType.InputTarget,CueId=p.Index.ToString(),IsHittable=true});}
        return data;
    }
    private static void LoadSingleTargetV5(GameplayData data,Dictionary<string,Dictionary<string,string>> patterns)
    {
        var rows=LocalCsv.Read(data.ChartPath);
        var ids=new HashSet<string>();double last=-1;
        var events=new List<RhythmEventData>();
        foreach(var group in rows.GroupBy(r=>r["phrase_id"]).OrderBy(g=>LocalCsv.Number(g.First()["target_time_sec"])))
        {
            var targets=group.OrderBy(r=>int.Parse(r["target_index"])).ToArray();var row=targets[0];
            var pattern=patterns[row["pattern_id"]];int index=data.Phrases.Count;
            double start=LocalCsv.Number(row["phrase_start_time_sec"]),first=LocalCsv.Number(row["target_time_sec"]);
            if(start<0||start>first||targets.Length!=1||int.Parse(row["target_count"])!=1||int.Parse(row["target_index"])!=1||bool.Parse(row["is_multi_target"]))throw new ArgumentException("Invalid v5 phrase: "+group.Key);
            var targetEvents=new List<PhraseTargetEvent>();
            foreach(var r in targets)
            {
                double t=LocalCsv.Number(r["target_time_sec"]);
                if(t<=last||t>=data.DurationSeconds||r["phrase_side"]!=row["phrase_side"]||!bool.Parse(r["separate_wave_event"])||!ids.Add(r["wave_event_id"]))throw new ArgumentException("Invalid v5 target/wave: "+r["wave_event_id"]);
                if(Math.Abs(LocalCsv.Number(r["gameplay_bpm"])-data.Chart.Bpm)>1e-6)throw new ArgumentException("v5 BPM mismatch: "+data.ChartPath);
                last=t;targetEvents.Add(new(t,targetEvents.Count){WaveEventId=r["wave_event_id"]});
            }
            bool left=row["phrase_side"]=="L";
            if(row["phrase_side"] is not ("L" or "R")||(index>0&&left==data.Phrases[^1].FromLeft))throw new ArgumentException("v5 phrase sides must alternate: "+group.Key);
            int[] slots=pattern["cue_slots"].Split(';').Select(int.Parse).ToArray();
            // v5's authored start can be clipped to zero for an immediate opening hit.
            var cues=slots.Select(slot=>(Time:start+slot*.5*(60/data.Chart.Bpm),Slot:slot)).Where(c=>c.Time<first).ToArray();
            var phrase=new PreyPhrase(index,start,first,left,Enum.Parse<WaveKind>(pattern["wave_kind"],true),int.Parse(pattern["pulse_count"]),cues.Select(c=>c.Slot).ToArray(),8,cues.Select(c=>c.Time).ToArray(),row["pattern_id"],"authored_v5",first)
                {PhraseId=group.Key,TargetEvents=targetEvents.ToArray()};
            data.Phrases.Add(phrase);
            foreach(var cue in cues)events.Add(new(){TimeSeconds=cue.Time,EventType=RhythmEventType.Cue,CueId=index.ToString(),IsHittable=false});
            foreach(var t in targetEvents)
            {
                events.Add(new(){TimeSeconds=t.TargetTime,EventType=RhythmEventType.InputTarget,CueId=index.ToString(),TargetOrdinal=t.Ordinal,WaveEventId=t.WaveEventId,IsHittable=true});
            }
        }
        if(data.Phrases.Count==0)throw new ArgumentException("Empty v5 chart: "+data.ChartPath);
        foreach(var e in events.OrderBy(e=>e.TimeSeconds))data.Chart.Events.Add(e);
    }
    public static GameplayData Load(string songId)
    {
        var data = new GameplayData();
        var custom=CustomMapRegistry.Find(songId);
        if(CustomMapRegistry.IsCustom(songId)&&(custom==null||!custom.Available))throw new ArgumentException("Custom map unavailable or missing authored charts: "+songId);
        var song = custom?.SongRow() ?? LocalCsv.Read("res://data/balance/songs.csv").Single(row=>row["song_id"]==songId);
        var timing = custom?.TimingRow() ?? LocalCsv.Read("res://data/balance/song_timing.csv").Single(row=>row["song_id"]==songId);
        data.BeatOffsetSeconds=LocalCsv.Number(timing["beat_offset_sec"]);
        data.MusicalBpm=LocalCsv.Number(timing["music_bpm"]);
        data.SongName=song["display_name"]; data.DurationSeconds=LocalCsv.Number(song["duration_seconds"]);
        data.DrainPerSecond=LocalCsv.Number(song["drain_per_3_seconds"])/3;
        data.AudioPath=timing["audio_path"];data.ChartPath=song["chart_path"];
        if(string.IsNullOrWhiteSpace(data.AudioPath)||string.IsNullOrWhiteSpace(data.ChartPath))throw new ArgumentException("Song audio/chart path is required: "+songId); data.Chart.SongEndTimeSeconds=data.DurationSeconds; data.Chart.Bpm=LocalCsv.Number(timing["bpm"]);
        data.LoadBalance();
        if (data.DurationSeconds<=0 || data.Chart.Bpm<=0 || data.MusicalBpm<=0 || data.MaxSatiety<=0 || data.StartingSatiety<0 || data.StartingSatiety>data.MaxSatiety
            || data.ClearThreshold<0 || data.ClearThreshold>data.MaxSatiety || data.DrainPerSecond<0 || data.BaseRecovery<0) throw new ArgumentException("곡/포만감 CSV 범위 오류");
        var patterns=LocalCsv.Read("res://data/balance/patterns.csv").ToDictionary(row=>row["pattern_id"]);
        if(!data.ChartPath.EndsWith("_v5.csv",StringComparison.Ordinal))
            throw new ArgumentException("Current BITE stages require a v5 chart: "+data.ChartPath);
        LoadSingleTargetV5(data,patterns);
        data.DrainProfile=SatietyDrainProfile.Load(songId,data.DurationSeconds,data.Phrases);
        data.Chart.Events.Add(new(){TimeSeconds=data.DurationSeconds,EventType=RhythmEventType.Section,IsHittable=false,SectionId="종료"});
        return data;
    }
}
