> 과거 작업 기록입니다. 현재 구조와 요구사항은 ../architecture.md를 참조하세요.


# 실제 게임 기반 구축

## 생성
- game/startup/Startup.tscn, SceneRouter.cs
- game/title/Title.tscn, TitleScreen.cs
- game/calibration/Calibration.tscn, CalibrationScreen.cs
- game/gameplay/Gameplay.tscn, GameplayScreen.cs
- game/settings/SettingsPopup.tscn, SettingsPopup.cs, SettingsService.cs
- game/ui/GameUi.cs 및 Godot 생성 UID

## 제거
참조 확인 후 game/main, game/player, game/target 전체와 game/ui/GameplayHud.cs(.uid)/gameplay_hud.tscn, data/charts/test_chart.tres를 제거했다. 새 구조가 이 파일들을 참조하지 않는다. 실험실/예측 표현/검사 씬은 이전 정리에서 이미 제거되어 있었다.

## 보존/수정
리듬 core 및 RhythmCalibration 측정/CalibrationSession/PCM helper는 원본 그대로다. CalibrationFlow에는 기존 세션 선택을 무시하는 재보정 옵션만 추가했다. CalibrationHud는 단색 표현으로 바꿨다. Settings는 같은 세션 오프셋을 사용하며 bus 음량만 제어한다. SceneRouter는 전환만 하며 Gameplay는 World와 메뉴 버튼만 있는 빈 shell이다.

## 실제 검증
- C# 전체 빌드: 오류/경고 0.
- Godot 4.7.2 .NET editor import, 씬/외부 스크립트 경로 및 casing 확인.
- 씬 인자를 주지 않은 기본 프로젝트 실행: Startup→Title, 오류 없이 종료.
- 실제 프로젝트/GUI에서 임시 외부 GDScript 검사 36개 통과: 모든 화면 연결, 설정 popup, Master mute/volume, Music volume, SFX/Cue volume, 별도 offset/재열기 상태, 강제 재보정, 보정 metronome 재생, Gameplay 복귀/Title 복귀.
- 실제 마우스 입력으로 게임 시작 버튼 클릭: Calibration 도달 및 Gameplay 전환 통과.
- 별도 새 세션: 게임 시작→Calibration→기본값 선택→Gameplay, 종료 버튼으로 정상 종료.
- Title/Settings/Calibration/Gameplay 렌더링 캡처를 확인했다. 회색 실험 화면/원 placeholder 없음.
- 자동 UI 검사는 실제 Button signal과 Range value 변경으로 작동 경로를 검사했다. 사람의 청음/보정 품질이나 향후 Gameplay 재미는 검사하지 않았다.
- 최종 backend 파일 비교: game/rhythm 전체 및 RhythmCalibration의 측정 로직은 변경 전과 동일하다.

## 작업 경계
1920×1080 viewport이며 기본 창만 1280×720이다. 저장은 현재 세션만 유지한다. Gameplay는 의도적으로 비어 있다. 이번 작업은 foundation에서 멈추며 먹이/감각/허기/돌진은 구현하지 않는다.
