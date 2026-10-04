using Godot;
namespace Gamejam2.Rhythm;
/// <summary>Inspector에서 작성하는 차트. 실행 시 controller가 검증·복사한다.</summary>
[GlobalClass]
public partial class RhythmChart : Resource
{
    /// <summary>편집 참고 정보. 런타임 판정 시점을 계산하지 않는다.</summary>
    [Export] public double Bpm { get; set; } = 120;
    /// <summary>양수이면 모든 이벤트가 곡에서 더 늦게 발생한다.</summary>
    [Export] public double SongOffsetSeconds { get; set; }
    /// <summary>곡 완료 시점. 0은 오디오 길이/마지막 이벤트에서 안전하게 추론한다.</summary>
    [Export] public double SongEndTimeSeconds { get; set; }
    /// <summary>타임스탬프 순으로 작성할 이벤트 목록.</summary>
    [Export] public Godot.Collections.Array<RhythmEventData> Events { get; set; } = new();
}
