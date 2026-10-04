# BITE 스토리 튜토리얼 · CatchBurst

**최신 UX/판정/혈흔 설정은 [튜토리얼 UX · 판정 접근성 개선](tutorial-ux-accessibility.md)을 기준으로 한다.** 아래는 최초 구현 보고이며 재시도 방식·TAP·파편 수·판정 창은 후속 작업에서 변경되었다.

일반 Gameplay의 Song Clock·차트·밸런스를 유지하고, 상속 장면에 학습/스토리 정책을 추가했다. 정상 곡 데이터와 튜토리얼 차트는 분리되어 있다. 공통 패턴·판정·포만감 설정과 월드 프로필만 재사용한다.

## 요청 항목별 구현 보고

| # | 항목 | 구현/설정 |
|---|---|---|
| 1 | 모드 경로 | `res://game/tutorial/TutorialGameplay.tscn`, `TutorialGameplayScreen.cs`. `Gameplay.tscn`/`GameplayScreen` 상속. |
| 2 | 음원 | `res://assets/MUSIC/1-2.mp3`, 실제 디코딩 길이 **101.825310초**. 다른 곡으로 대체하지 않음. |
| 3 | BPM / beat offset | **125 BPM / +0.070초**. `TutorialProfile.tres`에서 수정. 10ms 에너지 블록의 onset 자기상관으로 125 BPM 확인; 도입 강박 후보 1.51/5.35/7.75초에 맞춘 초기 오프셋. 정밀 청취 조정 가능. |
| 4 | 저장 키 | 기존 `user://player_settings.cfg`의 `[progress] tutorial_completed` bool. 완주 시에만 true/Flush. |
| 5 | 최초 진입 | Title → 필요한 Calibration → 미완료 Tutorial → Lobby. 이후 완료된 Tutorial 자동 생략. |
| 6 | 설정 재실행 | `SettingsPopup/Drawer/Margins/Layout/ScrollContainer/SettingsContent/RhythmSection/Tutorial/ReplayButton`. 리듬 → 튜토리얼 → 다시 하기. 미보정 상태는 먼저 Calibration. |
| 7 | 숙련도 판정 | 기존 `Rhythm.JudgmentResolved` 이후 실제 플레이 GOOD/PERFECT만 +1. 자동 시범, BAD, MISS, 빈 Bite는 +0. `● ○ ○` 표시. |
| 8 | 필요 성공 | 각 14단계 **GOOD+ 최소 3회**, 연속일 필요 없음. 다음 단계에서 0으로 초기화. |
| 9 | 재시도 | `lessons.csv`의 지역 음원 구간과 절대 Song Clock 차트를 `StartSong(checkpointStart)`로 다시 시작. 성공 수 보존, 실패 3회 후 안전한 다음 반복에서 재시범. 이전 수업으로 돌아가지 않음. MP3는 모드 내 캐시 재사용. |
| 10 | 오른쪽 눈 | `VisionMask`는 월드 레이어의 오른쪽 반시야 마스크. 공격의 암전 뒤 RightEyeVisibility=0. 상어·파동·HUD는 마스크 위. 오른쪽 물고기 alpha=0, 놓친 물고기는 왼쪽으로 빠져나오며 보임. 이후 짧은 힌트. |
| 11 | 왼쪽 눈 | 1 → 0.9(50–58) → 0.65(58–65) → 0.35(65–75) → 0.15(75–82) → 0(82–90). 각 구간 선형 진행. 물고기 alpha는 눈 visibility × silhouette opacity. |
| 12 | 파동 감각 | 0.25 → 0.28 → 0.32 → 0.35 → 0.40 → 0.45 → 0.55 → 0.65 → 0.70 → 0.80 → 0.85 → 1.0. 고정 파동 lane opacity만 변경; 목표 시각 불변. |
| 13 | 검목상어 공격 | 약 **1.7초**. 위/아래 계단형 픽셀 이빨이 닫히며 횡방향 sweep → 0.6초 Bite SFX → world/상어 shake → 0.75초 암전 → 오른쪽 시야 손실. 충돌 중 설명 숨김. 시각 사건은 Song Clock으로 진행/일시 정지. |
| 14 | 최종 시험 | 90–101.8 구간, 기존 **ex1** 패턴. 실루엣 0, wave 1, Horn + Whale + Float 0.8 강도. GOOD+ 3회까지 최종 구간 반복. 완료 문구 1.5초 후 Lobby; Result/Game Over 없음. |
| 15 | Sardine | 튜토리얼에는 사용하지 않음. 정상 게임의 기존 band/각도/무화살표 구현 보존. |
| 16 | 물고기 인식 대기 | **0.12초**, 실제 애니메이션의 MouthImpact를 따라간 뒤 같은 위치에서 burst. |
| 17 | burst 프레임 | **0.05초 × 3 = 0.15초**. 기존 제공 GIF에서 추출한 `catch_burst_frame_0/1/2.png`. |
| 18 | PERFECT 파편 | **8개**. |
| 19 | GOOD 파편 | **5개**. |
| 20 | BAD 파편 | **3개**. MISS **0개**. |
| 21 | 혈흔 | 제공 `피 펑펑.png`, PERFECT **2/8**, GOOD **1/5**, BAD **0/3**. 청록색 재색칠 제거; 원본 적색을 유지하고 밝기만 강조. |
| 22 | 크기 | small 0.45–0.70(50%), medium 0.75–1.00(35%), large 1.10–1.40(15%). 원본 픽셀 확대 3배, 4px 혈흔은 추가 2배. |
| 23 | 수명 / fade | **0.35–0.50초**. alpha 1 → 0.75(0.15s) → 0.4(0.30s) → 0. 초기 420–720px/s, drag=4, 수평 drift ±18px/s, 회전/약한 부유. 아래 방향은 8개 중 5, 5개 중 3, 3개 중 2. 중력 없음. |
| 24 | burst 기준점 | `Composition/SharkAnchor/Shark/Art/MouthImpact`. 인식 대기 동안 추적, burst 시작 후 고정. 성공 ring/bubble도 같은 위치와 0.12초 시작 시점을 사용. 전체 효과는 WORLD z=3 / water z=4 아래; HUD 별도. |
| 25 | 남은 확인 | 아래 검증/제약 참고. |

## 수업 데이터와 재실행

`res://data/tutorial/lessons.csv`가 단계 구간, 패턴, 방향, 좌/우 시야, 실루엣, 감각, 간섭 강도, 짧은 문구를 소유한다. 수치는 초기화 시 캐시한다. 물고기와 TAP 시각도 파동과 동일한 Visual Offset을 사용한다. 한 먹이당 한 번의 시도, 5박 목표, Input/Visual Offset, 빈 Bite MISS, anti-mash는 기존 코드 경로를 사용한다. 기본/최종 학습 모두 포만감 drain/실패 손실로 Game Over가 발생하지 않는다. 콤보/판정/성공 피드백은 유지한다.

자동 시범은 입력을 차단하고 실제 판정 시계의 Input Offset을 보정해 Bite한다. 시범은 숙련도에 포함하지 않는다. 첫 오른쪽 실명 수업은 먼저 자연스럽게 시도할 기회를 주고, 놓친 물고기가 보이는 쪽으로 빠져나온 뒤 힌트를 보여준다. 최종 시험에는 최초 자동 시범/TAP 안내가 없다.

설정 재실행은 현재 게임을 안전하게 정지·해제하고 별도 Tutorial로 진입한다. 완료/중도 종료는 Lobby로 돌아간다. Lobby에서 시작했다면 선택 위치를 복원한다. 해금, FC, AP를 초기화하지 않는다. 최초 필수 학습에는 메뉴 종료 버튼을 숨기고, 재실행에는 ESC → 설정 → 메뉴로 나가기/확인을 허용한다. 재보정은 같은 일시 정지한 Tutorial로 복귀한다.

개발 체크포인트: 디버그 빌드에서 `--tutorial-debug`를 지정한 경우에만 `DebugJump("NormalVision" | "InterferenceIntro" | "RightEyeLoss" | "WaveDiscovery" | "LeftEyeFade" | "FullBlindness" | "FinalExam")` 호출 가능. 일반 UI에 노출하지 않는다.

## 검증

- C# build: 오류/경고 0.
- `game/debug/tutorial_checks.tscn`: 실제 음원/시계/물리 입력, 14단계 완주·실패 반복·최종 시험·영구 저장·나중 실행 생략·설정 재실행·중도 종료·해금/FC/AP 보존 **33개 검사 통과**.
- `game/debug/tutorial_state_checks.tscn -- --tutorial-debug`: +80ms Input / -40ms Visual Offset, 시범 제외, BAD +0 / GOOD +1, 3회 제한, MP3 재사용, pause/3-2-1/resume, 50ms 이내 music/clock drift, 약한 간섭 gain, 빈 Bite/anti-mash, 최종 무실루엣, 조기 종료 저장 방지, 720p 탈출 물고기/최초 힌트, 오른쪽 수업 BAD 3회 재시범, orphan 기준점 복귀 **28개 검사 통과**.
- GPU 화면 확인: `artifacts/tutorial/attack-final.png`, `right-eye-final.png`, `miss-escape-final.png`, `left-fade-final.png`, `blind-exam-final.png`.
- CatchBurst GPU 화면: `artifacts/tutorial/catch-0-0.07.png`(물고기 인식), `catch-0-0.25.png`(폭발/혈흔), `catch-0-0.38.png`(확산/잔상).
- CatchBurst: `polish_checks` **232개**, `catch_audio_checks` **54개** 통과. PERFECT/GOOD/BAD/MISS, 원본 혈흔 수, 아래 확산, dense chain 정리, 기존 상어 baseline/호흡·Sardine·Game Over 검증.
- Settings: 새 행 포함 1080p 무스크롤, 일반 pause/resume/recalibration/exit **33개 검사 통과**.
- 정상 게임 회귀: rhythm 75, anti-mash 20, 실제 flow/EX, song-chart, presentation, water lifecycle, Result 검증 통과. `artifacts/tutorial/verification-results.json`와 각 로그 참조. Water lifecycle은 GUI focus에 의한 입력 간섭 이후 `--skip-title-watch`로 재검사했다; Title 장시간 관찰은 수동 확인 항목이다.
- 무거운 실제 렌더링 990프레임: P95 **8.993ms**, P99 **9.331ms**, max **12.343ms**, SFX underrun **0**. 3회 생성/플레이/해제 후 노드 baseline **4**, orphan **0**, UI audio binding IDs baseline 복귀. 새로운 풀은 도입하지 않았다.
- 재보정으로 같은 Gameplay/Tutorial을 detach/reattach한 뒤 SFX timeline 구독이 끊기던 기존 문제를 `GameplayAudio._EnterTree` 재연결로 수정하고 재개 검사에 포함했다.

## 제약 / 기존 충돌

- 원곡 길이는 101.825초지만 수업마다 3회 성공/시범이 필요하므로 체크포인트 반복에 따라 실제 학습 시간은 더 길어진다. 제시된 창은 원곡의 지역 구간이며 숙련도를 시간으로 건너뛰지 않는다.
- BPM/offset은 실제 MP3 분석에 맞춘 현재 설정이다. 청취 기반 미세 조정은 `TutorialProfile.tres`에서 가능하다.
- 별도 `물고기 펑펑.png`는 현재 프로젝트에 없다. 제공된 3프레임 GIF와 동일한 추출 PNG를 사용한다. 원본 GIF/1–14/혈흔 파일을 보존했다.
- 기존 미사용 `game/gameplay/SharkResultGauge.tscn`은 없는 `shark_result_fill.gdshader`를 참조한다. 실제 Result/Game Over는 `ResultSharkGauge.tscn`을 사용한다. 이번 튜토리얼/CatchBurst 범위 밖이라 삭제하지 않았다.
