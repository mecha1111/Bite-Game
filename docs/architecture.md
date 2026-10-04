# BITE 현재 구조

Godot 4.7.2 C# / .NET 10. 실행 진입점은 `game/startup/Startup.tscn`이다.

## 화면 흐름

Title → 최초 Calibration(필요한 경우) → 미완료 Tutorial → Lobby → Gameplay → Game Over 또는 Result.
Settings 재보정은 Title/Lobby 문맥 또는 일시 정지한 동일 Gameplay로 복귀한다. 닫으면 Gameplay는 3-2-1 이후 같은 Song Clock에서 재개한다.

## 책임과 데이터

- TutorialGameplayScreen: Gameplay 상속 모드. 연속 음악·미래 연습 구간/GOOD+ 3회/시야 손실·학습 정책. 일반 stage chart/성과 저장과 분리.
- SceneRouter: 화면 교체, 보정 복귀, 영구 SettingsPopup.
- RhythmController: 유일한 Song Clock, 입력 offset snapshot, 판정/이벤트/곡 종료. 선택 음원의 실제 길이에 도달하면 남은 최종 타깃을 정산하고 SongFinished를 한 번 발생시킨다. delta와 효과가 판정 시간을 소유하지 않는다.
- GameplayScreen: 한 먹이의 한 시도, 빈 Bite MISS, 포만감/콤보, pause, Game Over/Result 연결.
- GameplayData/LocalCsv: songs/song_timing과 명시적인 곡별 chart_path 로드. 누락/잘못된 데이터는 오류이며 대체 차트가 없다.
- SongAudio: 현재 MP3 바이트 직접 로드. Lobby SongPreview의 두 crossfade 플레이어는 Gameplay 진입 전에 멈춘다.
- GameplayAudio: chart cue와 간섭을 샘플 기준으로 합성하는 단일 generator + 재사용 feedback 플레이어. Song Clock에 종속된다.
- SignalPresenter/WavePulse: 고정 좌우 슬롯에서 점 → 확장 원. 먹이와 간섭은 독립적으로 겹칠 수 있다.
- SardineVortex: 5초 시각 current. 파동 원점/판정 시각을 보존하며 WORLD의 파동 그림만 upper-middle로 당긴다.
- GameplayEnvironment: world_water shader와 곡별 water profile, EX 전환. World 후처리는 배경/상어/효과에 적용하고 HUD는 상위 z에 유지한다.
- CatchBurstEffect: PERFECT/GOOD/BAD 10/7/4 debris, 혈흔 4/3/1, 인식 .12초 + 3프레임 .15초, 일반 파편 .35–.50초/혈흔 .40–.55초 수명. MISS에는 성공 효과가 없다.
- StagePresentation: 캐시된 곡별/EX FX CSV를 Song Clock에서 읽는 시각 전용 observer. WORLD 합성 shader만 이동하며 z5+ HUD와 모든 게임 좌표는 고정.
- SharkJumpBubbleEffect: 제공 물방울 texture, bounded batch; 물방울별 Node 없음.
- SwimmingMedalShark: scene-authored 카드 환경의 최고 Bronze/Silver/Gold 수영 장식.
- SaveStore/SettingsService/ProgressService: 보정·음량·화면·해금·곡 FC/AP 및 단조 증가 스테이지 메달 저장.

## 실제 곡 소스

| 곡 | MP3 (`assets/MUSIC/`) | 차트 (`data/charts/`) |
|---|---|---|
| Hear the Tide | 1_edited.mp3 | hear_the_tide_musical_chart.csv |
| Hidden Current | 2-2.mp3 | hidden_current_musical_chart.csv |
| Predator's Pulse | 3.mp3 | predators_pulse_musical_chart.csv |
| Deep Current EX | 4.mp3 | deep_current_ex_musical_chart.csv |

`data/balance/songs.csv`와 `song_timing.csv`가 실제 선택을 결정한다. 차트·밸런스는 정리 작업에서 변경하지 않는다. 패턴의 목표 슬롯 8은 반박 슬롯이므로 5박 목표이며 start = target − 240/gameplay BPM이다.

## UI와 개발 파일

기본 UI는 `.tscn` 작성, C#은 동작/값/애니메이션만 갱신한다. 공통 폰트/테마/설정 기어/뒤로 아이콘은 기존 공유 리소스를 사용한다.
`game/debug/`의 현행 검증과 ChartAudition 도구를 보존한다. `artifacts/`, `docs/`, `tools/`, 제공 차트 원본 폴더는 `.gdignore`로 Godot import에서 제외한다. 원본/감사 기록은 runtime fallback이 아니다.

검증·정리 결과: [cleanup-optimization.md](cleanup-optimization.md). 차트 청취 기록: [musical-chart-review.md](musical-chart-review.md).

튜토리얼 설정/재실행/25항목 보고: [story-tutorial-catch-feedback.md](story-tutorial-catch-feedback.md).

최신 튜토리얼 UX/판정 창/Skip 정책: [tutorial-ux-accessibility.md](tutorial-ux-accessibility.md).

현재 포만감·시야·기본 음량: [song-end-satiety-vision-audio.md](song-end-satiety-vision-audio.md).

음악 동기화 연출/EX 타임라인/메달 저장: [music-synchronized-presentation.md](music-synchronized-presentation.md).
