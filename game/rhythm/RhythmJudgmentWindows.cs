using System;
using Godot;
using System.Linq;
using Gamejam2.Data;
namespace Gamejam2.Rhythm;
/// <summary>판정 범위의 유일한 설정 소스. 단위는 초, 실행 시작 시 불변 snapshot을 만든다.</summary>
[GlobalClass]
public partial class RhythmJudgmentWindows : Resource
{
    public RhythmJudgmentWindows()
    {
        var row = LocalCsv.Read("res://data/balance/judgment.csv").Single();
        PerfectSeconds=LocalCsv.Number(row["perfect_seconds"]);
        GoodSeconds=LocalCsv.Number(row["good_seconds"]);
        BadSeconds=LocalCsv.Number(row["bad_seconds"]);
    }
    /// <summary>Perfect 절대 오차 상한, 경계 포함.</summary>
    [Export] public double PerfectSeconds { get; set; }
    /// <summary>Good 절대 오차 상한, 경계 포함.</summary>
    [Export] public double GoodSeconds { get; set; }
    /// <summary>Bad 절대 오차 상한 및 자동 Miss 경계, 경계 포함.</summary>
    [Export] public double BadSeconds { get; set; }
    /// <summary>유효성을 검증하고 현재 값의 불변 복사본을 반환한다.</summary>
    public RhythmJudgmentWindowSnapshot CreateSnapshot() => new(PerfectSeconds, GoodSeconds, BadSeconds);
}
/// <summary>실행에 적용된 범위. 시간/offset을 계산하지 않고 signed 오차를 분류한다.</summary>
public readonly struct RhythmJudgmentWindowSnapshot
{
    public double PerfectSeconds { get; }
    public double GoodSeconds { get; }
    public double BadSeconds { get; }
    /// <summary>유한하고 중첩된 범위만 허용한다.</summary>
    public RhythmJudgmentWindowSnapshot(double perfect, double good, double bad)
    {
        if (!double.IsFinite(perfect) || !double.IsFinite(good) || !double.IsFinite(bad)
            || perfect < 0 || good < perfect || bad < good || bad <= 0)
            throw new ArgumentException("판정 범위는 0 ≤ Perfect ≤ Good ≤ Bad이며 Bad > 0인 유한값이어야 합니다.");
        PerfectSeconds = perfect; GoodSeconds = good; BadSeconds = bad;
    }
    /// <summary>절대 오차의 중첩 inclusive window. 원본 오차의 음수 Early/양수 Late는 변경하지 않는다.</summary>
    public RhythmJudgment Classify(double timingErrorSeconds)
    {
        if (!double.IsFinite(timingErrorSeconds)) throw new ArgumentException("타이밍 오차는 유한값이어야 합니다.");
        double error = Math.Abs(timingErrorSeconds);
        if (error <= PerfectSeconds) return RhythmJudgment.Perfect;
        if (error <= GoodSeconds) return RhythmJudgment.Good;
        if (error <= BadSeconds) return RhythmJudgment.Bad;
        return RhythmJudgment.Miss;
    }
}
