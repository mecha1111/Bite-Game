using Godot;
namespace Gamejam2.Rhythm;
/// <summary>차트 사건의 역할. 저장된 InputTarget의 enum 값 0을 보존한다.</summary>
public enum RhythmEventType
{
    /// <summary>판정과 독립적인 힌트/표시 사건.</summary>
    Cue = 1,
    /// <summary>플레이어 입력을 판정하는 시점.</summary>
    InputTarget = 0,
    /// <summary>현재 곡 구간을 갱신하는 표시 사건.</summary>
    Section = 2
}
/// <summary>재사용 가능한 판정 시점 데이터. 런타임 상태는 저장하지 않는다.</summary>
[GlobalClass]
public partial class RhythmEventData : Resource
{
    [Export] public string WaveEventId {get;set;}="";
    [Export] public int TargetOrdinal {get;set;}
    /// <summary>Optional sequential encounter handoff; never changes TargetTime or Song Clock.</summary>
    public double ResolveDeadlineSeconds { get; set; } = double.PositiveInfinity;
    /// <summary>곡 시작 기준 이벤트 시점(초).</summary>
    [Export] public double TimeSeconds { get; set; }
    /// <summary>이벤트의 역할. 입력 action과 별개의 개념이다.</summary>
    [Export] public RhythmEventType EventType { get; set; } = RhythmEventType.InputTarget;
    /// <summary>향후 cue asset 연결용 식별자.</summary>
    [Export] public string CueId { get; set; } = "";
    /// <summary>Section 이벤트의 구간 이름. 특정 게임의 구간 구조를 강제하지 않는다.</summary>
    [Export] public string SectionId { get; set; } = "";
    /// <summary>InputTarget에서만 유효하다. Cue/Section은 true여도 판정하지 않는다.</summary>
    [Export] public bool IsHittable { get; set; } = true;
}
