namespace Gamejam2.Rhythm;
/// <summary>한 번 resolve된 이벤트의 결과. Godot Variant 대신 C# event로 전달한다.</summary>
public readonly struct RhythmJudgmentResult
{
    /// <summary>판정 등급.</summary>
    public RhythmJudgment Judgment { get; }
    /// <summary>입력 시간 - 이벤트 시간(초). 음수 Early, 양수 Late.</summary>
    public double TimingErrorSeconds { get; }
    /// <summary>대상 디자인 데이터. 수신자는 수정하지 않는다.</summary>
    public RhythmEventData TargetEvent { get; }
    /// <summary>판정 결과를 생성한다.</summary>
    public RhythmJudgmentResult(RhythmJudgment judgment, double timingErrorSeconds, RhythmEventData targetEvent)
    { Judgment = judgment; TimingErrorSeconds = timingErrorSeconds; TargetEvent = targetEvent; }
}
