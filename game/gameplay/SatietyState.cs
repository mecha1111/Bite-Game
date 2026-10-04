using System;
using Gamejam2.Rhythm;
namespace Gamejam2.Gameplay;
/// <summary>Gameplay consequences only. Drain integrates Song Clock differences, including the final fractional interval.</summary>
public sealed class SatietyState
{
    private readonly GameplayData _data;
    private double _drainedThrough;
    private readonly bool _protected;
    public double Value { get; private set; }
    public double? DepletedAtSongSeconds { get; private set; }
    public int MaxCombo { get; private set; }
    private readonly int[] _counts = new int[4];
    public int Combo { get; private set; }
    public long Score { get; private set; }
    public int MissStreak { get; private set; }
    public int EmptyBites { get; private set; }
    public double LastMissPenalty { get; private set; }
    public BitePolicy Policy { get; }
    private double _outroStarts=double.PositiveInfinity;
    /// <summary>The final resolved target ends hunger, not the authoritative song.</summary>
    public void StopDrainAfterFinalTarget(double songSeconds)=>_outroStarts=Math.Min(_outroStarts,songSeconds);
    public SatietyState(GameplayData data, bool protect=false) { _protected=protect;_data=data; Value=data.StartingSatiety;Policy=BitePolicy.Load(); }
    public void Advance(double songSeconds)
    {
        double time=Math.Clamp(songSeconds,0,_data.DurationSeconds);
        double rate=_protected?0:_data.DrainPerSecond;
        double drainEnd=Math.Min(time,_outroStarts);
        double elapsed=drainEnd<=_drainedThrough?0:_data.DrainProfile?.EffectiveSecondsBetween(_drainedThrough,drainEnd)??Math.Max(0,drainEnd-_drainedThrough);
        if(Value>0&&rate>0&&rate*elapsed>=Value)
            DepletedAtSongSeconds??=_data.DrainProfile?.TimeAfterEffectiveDrain(_drainedThrough,time,Value/rate)??(_drainedThrough+Value/rate);
        Value=Math.Max(0,Value-rate*elapsed); _drainedThrough=Math.Max(time,_drainedThrough);
    }
    public void Resolve(RhythmJudgmentResult result)
    {
        Score+=_data.ScoreRewards[result.Judgment];
        Value=Math.Min(_data.MaxSatiety,Value+_data.BaseRecovery*_data.Multipliers[result.Judgment]);
        if(result.Judgment==RhythmJudgment.Miss)ApplyMiss();
        else {Combo++;MissStreak=0;LastMissPenalty=0;}
        MaxCombo=Math.Max(MaxCombo,Combo);_counts[(int)result.Judgment]++;
    }
    public void EmptyBite(){EmptyBites++;_counts[(int)RhythmJudgment.Miss]++;ApplyMiss();}
    private void ApplyMiss(){Combo=0;MissStreak++;LastMissPenalty=Policy.Penalty(MissStreak);if(!_protected){Value=Math.Max(0,Value-LastMissPenalty);if(Value<=0)DepletedAtSongSeconds??=_drainedThrough;}}
    public GameplayResult CreateResult() => new(_data.SongName,Value,_data.ClearThreshold,
        _counts[(int)RhythmJudgment.Perfect],_counts[(int)RhythmJudgment.Good],_counts[(int)RhythmJudgment.Bad],_counts[(int)RhythmJudgment.Miss],MaxCombo,_data.TargetCount){Score=Score};
}
