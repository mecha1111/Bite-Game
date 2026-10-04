# Rhythm System Rules — Godot 4.7 / C#

이 문서는 프로젝트의 **리듬 시스템 구현 규칙**을 정의한다.

게임 콘셉트, 캐릭터, 세계관, 콘텐츠 규칙은 포함하지 않는다.

목표:
- 음악, 입력, 판정, 연출이 서로 다른 시간 기준을 사용하지 않게 한다.
- 프레임률이나 물리 업데이트 주기에 판정 결과가 종속되지 않게 한다.
- 협업자가 리듬 시스템의 기준을 빠르게 이해할 수 있게 한다.
- 오디오 지연과 장치별 오프셋을 추후 보정할 수 있는 구조를 유지한다.
- 초기 프로토타입에서 불필요하게 범용 리듬 엔진을 만들지 않는다.

---

## 1. 최상위 원칙

### 1.1 Single Authoritative Song Clock

프로젝트에는 리듬 판정을 위한 **하나의 신뢰 가능한 Song Clock**만 존재해야 한다.

다음 시스템은 모두 같은 Song Clock을 기준으로 동작한다.

- 리듬 이벤트
- 플레이어 입력 판정
- 자동 Miss
- Cue 발생
- 연출 동기화
- 디버그 정보

각 시스템이 별도의 누적 타이머를 만들지 않는다.

금지 예:

```csharp
_songTime += delta;
```

각 시스템이 독립적으로 위와 같은 시간을 누적하는 구조를 만들지 않는다.

Song Clock의 소유자는 현재 `RhythmController`다.

---

## 2. Godot 오디오 동기화 기준

리듬게임에서는 다음 값만 판정 시계로 사용하지 않는다.

```csharp
AudioStreamPlayer.GetPlaybackPosition()
```

오디오는 연속적으로 출력되는 것이 아니라 버퍼 단위로 믹싱되므로,
playback position은 정밀 판정에 충분하지 않을 수 있다.

몇 분 이내의 일반적인 곡에서는 시스템 시계 기반 Song Clock을 우선 사용한다.

개념:

```csharp
_startTimeUsec = Time.GetTicksUsec();

_audioStartDelaySeconds =
    AudioServer.GetTimeToNextMix()
    + AudioServer.GetOutputLatency();

_musicPlayer.Play();
```

현재 Song Time:

```csharp
double elapsedSeconds =
    (Time.GetTicksUsec() - _startTimeUsec)
    / 1_000_000.0;

double songTimeSeconds =
    Math.Max(
        0.0,
        elapsedSeconds - _audioStartDelaySeconds
    );
```

주의:
- 실제 Godot 4.7 C# API 이름을 확인한 뒤 사용한다.
- API 이름을 추측해서 구현하지 않는다.
- 오디오 출력 지연값을 매 프레임 불필요하게 다시 계산하지 않는다.

---

## 3. 시간 단위 규칙

리듬 시스템 내부의 기본 시간 단위는 **seconds**로 통일한다.

예:

```text
SongTimeSeconds
EventTimeSeconds
TimingErrorSeconds
UserOffsetSeconds
PerfectWindowSeconds
BadWindowSeconds
```

UI와 Debug Overlay에서만 millisecond로 변환한다.

예:

```text
-0.042 sec
→ 42 ms Early
```

프레임 수를 판정 단위로 사용하지 않는다.

금지 예:

```text
3 frames 이내 = Perfect
```

권장:

```text
abs(timingErrorSeconds) <= perfectWindowSeconds
```

---

## 4. Input Timing

리듬 입력은 polling보다 **InputEvent 기반 처리**를 우선한다.

Godot C#에서는 필요에 따라:

```csharp
public override void _Input(InputEvent @event)
```

에서 action 입력을 받고 즉시 판정 요청을 한다.

예:

```csharp
if (@event.IsActionPressed("rhythm_input"))
{
    _rhythmController.TryJudgeInput();
}
```

실제 프로젝트의 action 이름은 현재 Input Map 정의를 따른다.

중요:
- 입력 판정을 다음 Physics Tick까지 불필요하게 지연시키지 않는다.
- 특정 키 코드를 gameplay 코드에 직접 박지 않는다.
- InputMap action을 사용한다.

---

## 5. Input Repeat / Echo

키 입력의 OS repeat 때문에 한 번의 press가 여러 판정으로 처리되지 않게 한다.

키보드 이벤트를 직접 처리하는 경우:
- `IsEcho()` 여부를 확인한다.
- press와 release를 명확히 구분한다.

Input Action을 사용할 때도
한 입력이 동일 Rhythm Event를 두 번 resolve하지 않도록
Rhythm Event의 runtime 상태로 보호한다.

---

## 6. Rhythm Event와 Cue를 분리한다

`Cue`와 `Input Target`은 다른 개념이다.

### Cue

플레이어에게 리듬 정보를 전달하는 이벤트.

예:
- 소리
- 애니메이션
- 진동 표현
- 시각적 힌트

### Input Target

플레이어 입력을 판정해야 하는 시간 지점.

예:

```text
1.000 sec  Cue
1.500 sec  Cue
2.000 sec  Cue
2.500 sec  InputTarget
```

Cue가 발생했다고 즉시 입력해야 한다는 구조로 고정하지 않는다.

이 분리를 통해 다음을 지원할 수 있어야 한다.

- Cue 이후 일정 박자 뒤 Input
- 일부 Cue 생략
- 동일 Cue 패턴 반복 후 테스트
- 시각적 방해
- Call & Response
- Pattern Prediction

---

## 7. 권장 Event 타입

초기 시스템은 최소한의 타입만 가진다.

권장:

```csharp
public enum RhythmEventType
{
    Cue,
    InputTarget,
    Section
}
```

### Cue
힌트 또는 음악적 사건.

### InputTarget
판정 가능한 입력 이벤트.

### Section
스테이지/곡의 구조적인 구간 표시.

예:

```text
Intro
Teach
Practice
Test
Climax
```

현재 필요하지 않은 다음 타입을 미리 구현하지 않는다.

- Hold
- Slide
- Flick
- Chord
- LongNote

실제 디자인 요구가 생겼을 때 추가한다.

---

## 8. Prediction 중심 구조를 지원한다

리듬 시스템은 단순 반응게임만 가정하지 않는다.

다음과 같은 구조를 지원해야 한다.

```text
Cue
→ Cue
→ Cue
→ 예상
→ InputTarget
```

즉 플레이어가 앞선 신호를 듣고 다음 입력 시점을 예측할 수 있어야 한다.

시스템 관점에서는:
- Cue Event
- Input Target Event

를 독립적으로 배치할 수 있으면 된다.

---

## 9. Rhythm Chart

Chart 데이터는 Godot `Resource` 기반으로 관리한다.

초기에는 Chart Editor를 만들지 않는다.

권장 구조:

```text
RhythmChart
- Bpm
- SongOffsetSeconds
- Events
```

```text
RhythmEventData
- TimeSeconds
- EventType
- CueId
- IsHittable
```

현재 Section 이벤트는 `SectionId` 문자열을 사용한다. `IsHittable`은 InputTarget에서만 유효하다. Cue/Section은 이 값이 true여도 판정하지 않는다. Cue/Section dispatch는 offset 없는 SongTime, 입력/자동 Miss는 SongTime + 실제 적용 user offset을 사용한다.

현재 저장 enum 값은 기존 차트 보존을 위해 InputTarget=0, Cue=1, Section=2다. 이것은 시간 처리 우선순위가 아니다.

초기 판정은 `TimeSeconds`를 authoritative data로 사용한다.

BPM은:
- 제작 편의
- beat 계산
- debug
- authoring 보조

용도로 사용한다.

---

## 10. Design Data와 Runtime State 분리

Resource에는 runtime 상태를 기록하지 않는다.

금지 예:

```text
RhythmEventData.IsHit
RhythmEventData.IsMissed
```

Resource asset은 디자인 데이터로 취급한다.

runtime 상태 예:

```text
Pending
Hit
Missed
```

는 `RhythmController` 또는 runtime 전용 객체가 관리한다.

같은 Resource를 여러 번 재생해도 이전 플레이 상태가 남지 않아야 한다.

---

## 11. Event Resolve 규칙

하나의 Input Target은 정확히 한 번만 resolve된다.

가능한 결과:

```text
Pending
→ Hit

또는

Pending
→ Missed
```

이미 resolve된 event는 다시 판정하지 않는다.

입력 Miss와 Timeout Miss가 중복 발생하지 않게 한다.

---

## 12. Judgment 계산

기본 계산:

```text
TimingErrorSeconds =
    PlayerJudgmentTimeSeconds
    - TargetEventTimeSeconds
```

Sign convention:

```text
negative = Early
positive = Late
```

예:

```text
-0.042 = 42 ms Early
+0.063 = 63 ms Late
```

이 규칙을 프로젝트 전체에서 통일한다.

---

## 13. Judgment 결과 타입

판정 결과는 string이 아니라 enum과 명시적 결과 타입을 사용한다.

예:

```csharp
public enum RhythmJudgment
{
    Perfect,
    Good,
    Bad,
    Miss
}
```

결과에는 최소 다음 정보가 있어야 한다.

```text
Judgment
TimingErrorSeconds
TargetEvent
```

향후 score나 gameplay consequence는
판정 결과를 소비하는 별도 시스템에서 계산한다.

---

## 14. Judgment와 Score를 분리한다

Timing 판정과 Score 계산을 같은 로직으로 만들지 않는다.

권장 흐름:

```text
Player Input
→ Timing Error
→ Judgment
→ Gameplay Reaction
→ Score / Combo
```

이렇게 분리해야 이후:
- Perfect와 Bad의 점수 차이
- 성공/실패 연출
- gameplay 결과
를 독립적으로 조정할 수 있다.

---

## 15. Judgment Window

현재 기본값 (사용자 지정):

```text
Perfect: abs(error) ≤ 30 ms
Good:   30 ms < abs(error) ≤ 70 ms
Bad:    70 ms < abs(error) ≤ 130 ms
Miss:    abs(error) > 130 ms
```

`data/balance/judgment.csv`가 기본 판정 창의 유일한 소스다. `RhythmJudgmentWindows` Resource는 CSV 기본값을 읽고 실행 snapshot을 만든다. Controller의 `JudgmentWindows`를 Inspector에서 조절한다. 변경은 다음 StartSong에 적용하며 자동 저장하지 않는다.

StartSong에서 유효성을 검증한 불변 `AppliedJudgmentWindows` snapshot을 만들며 실행 도중 설정 편집은 기존 snapshot을 변경하지 않는다. 유한값, 0 ≤ Perfect ≤ Good ≤ Bad, Bad > 0만 허용한다. 코어 입력 판정과 경계 검증은 동일한 `Classify` 함수를 사용하며 UI가 Good를 재판정하지 않는다. 자동 Miss는 같은 적용 Bad 경계를 사용한다. 경계는 모두 포함하며 임의 epsilon을 추가하지 않는다.

타이밍 오차는 signed seconds 그대로 유지한다: 음수 Early(빠름), 양수 Late(늦음). 절대값은 등급 분류에만 사용한다. 너무 이른 범위 밖 입력은 기존 규칙대로 목표를 소비하지 않는다. 늦게 Bad 경계를 지난 목표는 한 번만 자동 Miss된다. Song Clock와 offset 적용 위치/방향은 그대로다.

---

## 16. Auto Miss

입력하지 않고 판정 가능 시간이 지나면 자동 Miss 처리한다.

기본 조건 예:

```text
SongTimeSeconds >
TargetTimeSeconds + BadWindowSeconds
```

이고 아직 event가 Pending이라면 Miss 처리.

Auto Miss는 `_Process()` 등에서 Song Clock을 기준으로 확인한다.

Physics frame을 기준으로 판정하지 않는다.

---

## 17. Event 탐색

입력마다 Chart 전체를 처음부터 순회하지 않는다.

현재 event index를 추적한다.

예:

```csharp
_currentEventIndex
```

입력 시 현재 또는 다음 hittable event 중심으로 판정한다.

현재 게임 규모에서는:
- binary search
- complex indexing
- generic scheduler framework

가 필요하지 않다.

단순하고 읽기 쉬운 순차 index 구조를 우선한다.

---

## 18. User Offset

장치별 latency 보정을 위해 판정 offset 구조를 유지한다.

예:

```csharp
[Export]
public double UserOffsetSeconds { get; set; } = 0.0;
```

판정용 시간 계산은 한 위치에서만 수행한다.

예:

```csharp
double judgmentTimeSeconds =
    SongTimeSeconds + UserOffsetSeconds;
```

Offset의 sign convention을 코드와 문서에 명시한다.

Calibration은 `game/calibration`의 재사용 흐름이다. Startup의 SceneRouter가 자신이 소유한 기존 controller/music을 보정 화면에 전달한다. 화면 전환 시 보정 트랙을 정지한다. 첫 진입 보정 완료는 Lobby로, Settings 재보정 완료는 요청한 화면 및 설정 popup으로 돌아간다. Gameplay는 자체 scene-owned RhythmController/MusicPlayer를 사용한다. 정상 Gameplay 중 Startup 보정 controller는 정지한다. Settings 재보정 동안 Gameplay 인스턴스와 실행 상태는 정지한 채 보존하고 Startup controller로 보정한다. 복귀 후 Settings를 닫으면 3-2-1 표시 뒤 Gameplay의 기존 ResumeSong 경로로 재개한다.

---

## 19. 게임 진입 Calibration

최초 게임 시작에서 보정을 선택하거나 기본값으로 건너뛴다. 완료 여부와 입력/화면 offset은 SettingsService가 user://player_settings.cfg에 저장하며 이후 실행에서 복원한다. 재보정은 Settings에서 열 수 있다.

- 입력/오디오: 같은 Song Clock의 120 BPM 예상 박과 입력 시각 차이를 12회 수집한다. 한 박의 중복 입력은 제외하며 median/MAD로 명백한 이상치를 제외한다. 유효 입력 12개가 모이면 결과로 전환한다. 누락된 박은 다음 오디오 측정 구간에서 추가 수집하며 기존 표본을 유지한다. 이상치 제외 후 8개 이상일 때 제안을 적용할 수 있다.
- 원시 오차 = SongTime − ExpectedBeatTime. 늦은 입력의 양수 median을 상쇄하는 `UserInputOffsetSeconds = −median`을 기존 `UserOffsetSeconds`로 전달한다. 코어는 여전히 SongTime + 적용 offset을 한 번만 계산한다.
- 화면: `VisualOffsetSeconds`는 ±200ms. 표시만 SongTime − VisualOffset으로 읽는다. 양수는 화면을 늦추며 음수는 앞당긴다. 차트 목표/입력 판정/오디오 속도는 바뀌지 않는다.
- 내부 확인 API: 8박 입력의 보정된 Early/Late, median, 평균, 평균 절대 오차를 표시한다. 일방적 편향은 재조정을 권장하지만 강제하지 않는다.
- 설정 화면에서 두 값을 별도로 조절한다. 보정 기본값 선택은 두 값을 초기화한다. 게임 진입 때 세션 입력 offset을 코어에 전달한다. 화면 offset은 표현 소비자가 별도로 읽어야 하며 판정에 사용하지 않는다.

이 측정은 사람이 소리에 맞춰 누른 총 편향이다. 실제 장치별 오디오/디스플레이/입력 지연을 독립적으로 측정하거나 추정하지 않는다. 기존 오디오 mix/output latency 보정과 구별한다. 별도 Song Clock, BPM/재생 속도 변경은 하지 않는다. 저장은 작은 설정 ConfigFile로만 유지한다.

---

## 20. Latency 종류를 혼동하지 않는다

다음 지연은 서로 다른 문제다.

### Audio Output Latency
게임에서 재생 요청 후 실제 귀에 들릴 때까지.

### Input Latency
플레이어가 버튼을 누른 뒤 게임이 이벤트를 받을 때까지.

### Display Latency
렌더링한 화면이 실제 모니터/TV에 표시될 때까지.

하나의 "리듬게임 딜레이" 값으로 모두 설명하지 않는다.

Debugging 시 어떤 지연을 측정하고 있는지 명시한다.

---

## 21. Audio First

리듬 판정의 기준은 음악 timeline이다.

캐릭터 animation frame이나 visual position을 판정 기준으로 사용하지 않는다.

금지 예:

```text
캐릭터의 입이 닫혔으므로 Hit
```

권장:

```text
Target Time = 12.500 sec
Player Input = 12.462 sec
Timing Error = -38 ms
```

그 후 animation이 판정 결과를 표현한다.

---

## 22. Visual은 Rhythm을 표현한다

시각 연출은 독립적인 timing truth가 아니다.

가능하면:

```text
Rhythm Event
├── Audio 표현
└── Visual 표현
```

구조로 생각한다.

같은 event를:
- 소리
- 애니메이션
- 화면 효과

가 각각 표현하도록 한다.

---

## 23. 애니메이션 동기화

판정 시간과 animation timeline을 분리한다.

입력 결과 연출이 길더라도
Song Clock은 멈추지 않는다.

금지 구조:

```text
Miss
→ Animation 완료 대기
→ 다음 Rhythm Event
```

권장 구조:

```text
Song Clock 계속 진행

Miss Animation은 독립적으로 재생
```

플레이어가 다음 박자로 복귀할 수 있어야 한다.

---

## 24. 음악과 Gameplay Timeline

가능하면 다음은 같은 음악 Grid를 기준으로 제작한다.

- Backing Track
- Cue
- Expected Input
- Visual beat event

고정 BPM 스테이지에서는:

```text
BeatDurationSeconds = 60 / BPM
```

을 authoring 보조용으로 사용할 수 있다.

초기 프로토타입에서는 고정 BPM을 우선한다.

---

## 25. Tempo Change

현재 초기 시스템에서는 한 스테이지/곡을
**고정 BPM**으로 가정한다.

BPM 변화가 실제 게임 디자인에 필요해지기 전까지
Tempo Map을 구현하지 않는다.

향후 필요하면:

```text
0.0 sec   120 BPM
16.0 sec  160 BPM
```

같은 Tempo Map을 별도 설계한다.

현재 범위에서는 Non-Goal이다.

---

## 26. Cue Audio 재생 주의

정확한 박자에 반드시 들려야 하는 Cue를:

```csharp
if (SongTimeSeconds >= cueTime)
{
    cuePlayer.Play();
}
```

처럼 런타임 `Play()`만으로 맞추는 것은 주의한다.

Play 요청 이후 실제 출력까지 audio buffer latency가 존재할 수 있다.

타이밍이 중요한 Cue는 가능하면 다음 중 하나를 우선한다.

### A. 음악 파일 자체에 Cue bake

가장 단순하고 안전한 프로토타입 방식.

### B. 동기화된 Audio Stem

Backing / Cue / 기타 stem을 동일 시점에 시작한다.

Godot의 동기화 가능한 AudioStream 기능을 사용할 경우
Godot 4.7에서 실제 지원되는 API인지 먼저 확인한다.

---

## 27. Audio Asset 품질

리듬 판정에 사용하는 SFX/Cue는 다음을 확인한다.

- 파일 앞부분의 불필요한 silence
- 지나치게 느린 attack
- 과도한 reverb
- sample start 위치

정확히 눌렀는데 성공음이 늦게 들리는 상황을 피한다.

Gameplay feedback용 핵심 SFX는
가능하면 빠른 transient를 가진다.

---

## 28. Audio Bus

기본 bus 구조:

```text
Master
├── Music
├── Cue
└── SFX
```

### Music
배경 음악.

### Cue
gameplay rhythm cue.

### SFX
입력, 판정, 기타 효과음.

Cue bus와 Music bus를 나누어두면
playtest 중 cue 가독성을 독립적으로 조정할 수 있다.

---

## 29. Difficulty 규칙

난이도를 BPM 증가만으로 설계하지 않는다.

시스템은 다음 형태의 변형을 지원할 수 있어야 한다.

- regular beat
- rest
- offbeat
- syncopation
- subdivision
- cue omission
- distractor
- call and response
- overlapping pattern
- pattern transformation

그러나 현재 시스템이 각각의 음악 이론 개념을
코드 타입으로 미리 정의할 필요는 없다.

대부분은 Event 배치 데이터로 표현한다.

---

## 30. Teach Before Test 구조 지원

Chart는 다음 흐름을 만들 수 있어야 한다.

```text
Teach
→ Repeat
→ Practice
→ Test
→ Variation
```

Section Event를 사용하면
이 구조를 debug 및 authoring 관점에서 구분할 수 있다.

시스템이 "처음 등장한 패턴은 반드시 설명해야 한다" 같은
게임 디자인 규칙을 코드로 강제하지는 않는다.

---

## 31. Restart

곡을 재시작할 때 다음 값은 반드시 reset되어야 한다.

- Song Clock start time
- current event index
- runtime event states
- last judgment
- last timing error
- section state
- song completion state

Resource 디자인 데이터 자체는 변경하지 않는다.

Restart 후 이전 플레이 상태가 남지 않아야 한다.

---

## 32. Pause 정책

application focus loss 자동 pause와 명시적인 PauseSong/ResumeSong을 지원한다. 수동 pause도 같은 audible 위치 저장 및 출력 지연 보정 경로를 사용한다. 포커스 복귀는 수동 pause를 해제하지 않는다. 수동 seek와 SceneTree 전역 pause는 지원하지 않는다.

Focus out에서 audible SongTime을 저장하고 audio playback을 Stop하며 clock/input/event progression을 freeze한다. Focus in에서만 저장한 위치로 Play(fromPosition), clock 기준점 및 다음 mix + output delay를 다시 초기화한다. 출력 대기 동안 clock과 event/input도 freeze한다. 휴지 시간은 곡 시간에 포함하지 않는다.

위치 재생을 안전하게 지원하는 loop 없는 PCM WAV(8/16-bit), Ogg Vorbis, MP3와 PitchScale=1을 현재 범위로 한다. 그 밖의 stream은 명확한 오류로 거부한다. 장치에 이미 전송된 audio tail은 회수할 수 없으며 실제 귀로 듣는 동기화는 별도 검증 대상이다.

---

## 33. Focus Loss / Alt+Tab

- application focus out: rhythm playback 자동 pause.
- application focus in: 저장 위치에서만 resume.
- pause 동안 song clock/input/events/section state 진행 없음.
- 중복 notification은 무시한다.
- unfocused restart는 0초에서 대기하며 focus 복귀 때 시작한다.
- StopSong 뒤 focus in은 재생하지 않는다.

---

## 34. Debug Overlay

리듬 시스템에는 최소 다음 debug 정보가 있어야 한다.

```text
Song Time
Next Event Time
Delta To Next Event
Current Event Index
Last Judgment
Last Timing Error
Actual Applied User Offset
Playback State
Current Section
```

Timing Error는 ms 단위로 표시한다.

예:

```text
GOOD
Early 72 ms
```

Debug Overlay는 시스템 상태를 읽기만 한다.

판정값을 직접 변경하지 않는다.

---

## 35. Debug Logging

중요 이벤트만 logging한다.

예:

```text
[Rhythm] Song Started
[Rhythm] Section: Practice
[Rhythm] Perfect -42 ms
[Rhythm] Miss +183 ms
[Rhythm] Song Finished
```

매 프레임 Song Time을 console에 출력하지 않는다.

필요하면:

```csharp
[Export]
public bool EnableDebugLogging { get; set; }
```

등으로 제어한다.

---

## 36. 최소 테스트 케이스

리듬 시스템 구현 후 가능한 범위에서 다음을 확인한다.

### Timing
- Song Clock이 정상 증가하는가?
- frame rate 변화에도 timing 기준이 변하지 않는가?
- physics FPS와 무관한가?

### Judgment
- Perfect가 정확히 한 번 발생하는가?
- Bad이 정확히 한 번 발생하는가?
- Miss가 정확히 한 번 발생하는가?
- Early/Late sign이 맞는가?

### Input
- 한 press가 두 event를 resolve하지 않는가?
- key echo가 중복 판정을 만들지 않는가?

### Timeout
- 아무것도 누르지 않으면 자동 Miss가 한 번 발생하는가?

### Restart
- 재시작 후 event index와 runtime state가 초기화되는가?

### Offset
- User Offset 변경 시 판정 기준이 예상 방향으로 이동하는가?

---

## 37. Frame Rate 검증

가능하면 다음 환경을 비교한다.

- 60 FPS
- 120 FPS
- 낮은 FPS 또는 인위적 frame drop

동일한 실제 input timing이
frame rate 차이 때문에 다른 판정이 되어서는 안 된다.

프레임 기반 판정이 발견되면 수정한다.

---

## 38. 실제 Audio 검증

실제 음악 asset이 없으면:

- Song Clock 코드
- 판정 로직
- runtime event 처리

까지만 검증했다고 기록한다.

실제 귀로 듣는 audio sync까지 검증했다고 주장하지 않는다.

실제 audio asset을 연결한 뒤 별도로 playtest한다.

---

## 39. Bluetooth / 외부 장치

Bluetooth audio와 TV/HDMI 환경에서는
추가 output latency가 생길 수 있음을 전제로 한다.

코드에서 특정 장치 latency를 추측하여 하드코딩하지 않는다.

`UserOffsetSeconds` 같은 사용자 보정값을 통해
향후 대응 가능한 구조를 유지한다.

---

## 40. 외부 라이브러리

현재 rhythm timing을 위해 외부 framework를 자동 설치하지 않는다.

Godot 기본 기능으로 우선 구현한다.

외부 addon이 필요하다고 판단한 경우
먼저 다음을 보고한다.

- addon 이름
- 목적
- Godot 4.7 호환 여부
- 기본 기능으로 해결하기 어려운 이유
- 유지보수 위험

사용자 확인 전 임의 설치하지 않는다.

---

## 41. 현재 Non-Goals

초기 리듬 시스템에서 다음은 구현하지 않는다.

- Chart Editor
- 범용 Rhythm Engine
- Tempo Map
- Hold / Slide / Flick
- 복잡한 Calibration UI
- Multiplayer synchronization
- Online leaderboard
- Replay system
- Generic Event Bus
- 별도의 audio middleware

실제 필요가 확인되면 추가한다.

---

## 42. 코드 주석 규칙

특히 다음 코드에는 "왜"를 설명하는 주석을 작성한다.

- Song Clock
- Audio latency compensation
- Input timing
- Judgment window
- Early/Late sign convention
- Auto Miss
- User Offset
- Runtime Event State

명백한 코드 동작을 그대로 설명하는 주석은 피한다.

---

## 43. 협업 규칙

리듬 timing 관련 핵심 계산을 수정할 경우
다음 중 영향을 받는 문서를 함께 확인한다.

- `docs/architecture.md`
- `docs/coding-guidelines.md`
- 이 문서

다음 변경은 특히 팀에 명확히 알려야 한다.

- Song Clock 계산 변경
- Judgment sign convention 변경
- Window 정의 변경
- User Offset 적용 방향 변경
- Event 타입 변경
- Pause/Restart 정책 변경

---

## 44. 작업 완료 보고

리듬 시스템 관련 작업 후 다음을 보고한다.

### Implemented
무엇을 구현했는가.

### Changed
기존 timing 또는 event 규칙 중 무엇을 변경했는가.

### Verified
실제로 확인한 동작.

### Not Verified
확인하지 못한 동작.

### Timing Data
필요하면 측정한 Early/Late, latency 등의 값.

### Open Questions
추가 결정이 필요한 항목.

실행하지 않은 테스트를 완료했다고 기록하지 않는다.

---

# 핵심 요약

리듬 시스템에서 가장 중요한 기준은 다음이다.

1. **하나의 Song Clock만 사용한다.**
2. **판정은 seconds 기반이며 frame rate에 의존하지 않는다.**
3. **Cue와 Input Target을 분리한다.**
4. **Audio timeline이 판정의 기준이다.**
5. **Visual과 Animation은 판정을 표현할 뿐 판정 기준이 아니다.**
6. **Timing Error는 signed value로 남긴다.**
7. **Judgment와 Score를 분리한다.**
8. **한 Event는 한 번만 resolve한다.**
9. **User Offset을 통해 장치별 latency 보정 가능성을 남긴다.**
10. **복잡한 기능은 실제 필요가 생기기 전까지 추가하지 않는다.**


## 현재 Gameplay pause
Settings가 열리면 Song Clock/오디오/이벤트/포만감 진행을 함께 정지한다. 닫힌 뒤 3-2-1은 정지 중 표시용 시간이며 별도 song clock이 아니다. countdown 중 focus loss는 countdown도 정지한다. 기존 freeze 위치에서 출력 지연을 보정해 재개하며 countdown 동안 목표를 이동하거나 오디오만 재생하지 않는다.
