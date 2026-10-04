> 과거 작업 기록입니다. 현재 구조와 요구사항은 ../architecture.md를 참조하세요.

> 초기 기반 구축 당시의 역사 기록입니다. 현재 구조/설정의 기준은 docs/architecture.md와 최신 사용자 지시입니다.

# Setup Report

## Created / Modified Files
수정: `project.godot`, `.gitignore`. 기존 파일과 설정 보존.
생성: `Gamejam2.csproj`, `default_bus_layout.tres`, `docs/setup-report.md` 및 다음 파일:

- `data/charts/test_chart.tres`
- `debug/RhythmDebugOverlay.cs`
- `debug/RhythmDebugOverlay.cs.uid`
- `debug/rhythm_debug_overlay.tscn`
- `docs/.gdignore`
- `docs/architecture.md`
- `docs/coding-guidelines.md`
- `game/main/Main.cs`
- `game/main/Main.cs.uid`
- `game/main/main.tscn`
- `game/player/Player.cs`
- `game/player/Player.cs.uid`
- `game/player/player.tscn`
- `game/rhythm/RhythmChart.cs`
- `game/rhythm/RhythmChart.cs.uid`
- `game/rhythm/RhythmController.cs`
- `game/rhythm/RhythmController.cs.uid`
- `game/rhythm/RhythmEventData.cs`
- `game/rhythm/RhythmEventData.cs.uid`
- `game/rhythm/RhythmJudgment.cs`
- `game/rhythm/RhythmJudgment.cs.uid`
- `game/rhythm/RhythmJudgmentResult.cs`
- `game/rhythm/RhythmJudgmentResult.cs.uid`
- `game/target/Target.cs`
- `game/target/Target.cs.uid`
- `game/target/target.tscn`
- `game/ui/GameplayHud.cs`
- `game/ui/GameplayHud.cs.uid`
- `game/ui/gameplay_hud.tscn`

## Folder Structure
`game/{main,rhythm,player,target,ui}`, `data/charts`, `debug`, `docs`.

## Architecture
Main은 구성/연결, RhythmController는 단일 clock/판정, Resource는 디자인 데이터, Player/Target/HUD는 표시, DebugOverlay는 읽기 전용 관찰.

## Project Settings
추가: run/main_scene, config/features의 C#, dotnet/project/assembly_name, input/bite. 기존 stretch(canvas_items/expand), renderer(Forward Plus/d3d12), physics(Jolt), window 설정 유지. V-Sync, interpolation, audio buffer 기본값 유지.

## Input
bite: 물리 Space, 마우스 왼쪽, Joypad button 0(A/Cross). _Input에서 key echo 없이 즉시 처리.

## Audio
Master로 Music/Cue/SFX 전송. Stream 비어 있으면 warning과 무음 preview. 실제 음악은 Main/MusicPlayer의 Stream에 연결.

## Rhythm Timing
Godot monotonic ticks - 다음 믹스 대기 - 출력 latency. 무음 delay=0. UserOffset은 판정 시계에 한 번만 더하며 양수는 Late 방향.

## Judgment
Perfect ±80ms, Good ±150ms, 초 단위 Export 설정. enum + readonly struct 결과, C# event 전달. 순차 index로 단일 resolve와 자동 Miss 처리. 너무 이른 입력은 무시. Chart Resource를 수정하지 않는다.

## Debugging
Song Time, Next Event Time, Delta, 최근 오차/Early/Late, Judgment, User Offset, Playback State, Index. 시작/판정 로그 Export toggle.

## Documentation
architecture.md: 구조, flow, 설정, 실행, 제한 및 검증. coding-guidelines.md: namespace, 주석, 참조, event, null, 단위/offset 규칙.

## Verification
C# build 0 warnings/errors. 공식 Godot 4.7.2 mono headless Scene loading, Main 실행, clock/입력/HUD/overlay, Perfect/Early Good/Late Good/Miss, 중복 방지, 원본 보존 검증 성공. 실제 시간 7개 timeout 확인, 정상 종료.

## Not Verified
GUI 시각 품질, 실제 마우스/게임패드 하드웨어, 실음악 동기화, 장치별 latency.

## Required Assets
실제 음악 AudioStream과 해당 음악에 맞는 chart. 테스트 placeholder에는 추가 art가 필요 없다.

## Known Issues / Open Questions
현재 설치 엔진은 일반 버전이므로 .NET 버전 필요. 공식 mono 에디터 즉시 --quit 종료에서 내부 crash; --quit-after 120 정상. Git 저장소가 없어 상태 추적/commit은 미수행. pause/seek/loop와 장시간 drift 보상은 범위 밖. Target은 현재 결과를 보여주는 placeholder이며 실제 spawn/content 연결은 후속 작업.
