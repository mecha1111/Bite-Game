# 전곡 종료 · 포만감 · 튜토리얼 시야 · 기본 음량

> 이전 검증 기록입니다. 최신 판정(70/140/200ms), 구간별 drain 및 포만감 HUD 수치는 [현재 수정 보고](satiety-section-hud-accessibility.md)를 따릅니다.


## 종료 책임

`RhythmController.StartSong`은 선택한 non-loop MP3/PCM의 **실제 `GetLength()`**를 `AppliedSongEndTimeSeconds`에 적용한다. 기존 audible Song Clock의 출력 지연·pause/resume·offset 계산은 유지한다. `_Process`가 이 정확한 시점에 도달하면 아직 미정산인 마지막 타깃을 MISS로 정산하고, 시계/음악을 정확한 종료 시점에서 멈춘 다음 **SongFinished 한 번**을 발생시킨다. 정상 `GameplayScreen.Finish`만 Result를 연다. 음악 길이 밖 차트는 명시적으로 오류 처리한다.

감사 결과 기존 Result 진입은 이미 SongFinished 단일 경로였다. 먹이 수/콤보/구간 끝에서 Result를 여는 별도 구현은 없었다. 수정한 문제는 `max(audio length, chart end, last event, last capture)`가 실제 음원보다 종료를 연장할 수 있다는 점이다. 마지막 입력 유예를 기다려 음악 종료 후 250ms 이상 더 진행하는 동작을 제거했다. `GameplayData.UseAudioDuration`은 오래된 길이를 무조건 늘리는 방식 대신 실제 음원 길이를 양방향으로 적용하며 종료 Section도 맞춘다.

포만감 0은 이전과 같이 Game Over다. drain 중 0에 도달한 정확한 곡 시각을 `SatietyState.DepletedAtSongSeconds`로 기록한다. 프레임이 EOF를 건너뛰더라도 **곡 끝 이전의 starvation이 Result보다 우선**하며, 같은 프레임의 catch가 이미 0이 된 포만감을 되살리지 않는다. 곡 끝 이후에는 입력과 신규 사건을 받지 않는다. EX 환경 전환은 종료에 관여하지 않는다.

## 현재 곡별 계산

원본 음원은 `song_timing.csv`, 최종 prey는 `songs.csv.chart_path`의 네 현재 CSV에서 읽었다. 실제 길이는 Godot의 직접 MP3 loader로 검증했다. 차트·음원·패턴·간섭 시각은 변경하지 않았다.

| 곡 | 실제 길이(s) | prey | PERFECT 총 보상 | 이전 drain/s | 새 drain/s | 전곡 drain | AP 실제 최종 | GOOD 중심 실제 최종 | clear |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| Hear the Tide | 140.435944 | 43 | 6450 | 2.858157 | **23.106620** | 3245.000 | **1028.183** | **925.000** | 가능 |
| Hidden Current | 138.500000 | 32 | 4800 | 1.500000 | **16.209386** | 2245.000 | **1004.019** | **875.000** | 가능 |
| Predator's Pulse | 188.342850 | 49 | 7350 | 2.109290 | **13.000000** | 2448.457 | **988.701** | **988.701** | 가능 |
| Deep Current EX | 131.683258 | 46 | 6900 | 3.166667 | **26.920658** | 3545.000 | **948.578** | **825.000** | 가능 |

시작/최대/clear는 **500/1100/800**. 기존 runtime 보상 17·GOOD 80%·BAD 50%를 요청된 **150/80/40/0**으로 바꿨다. `satiety.csv.base_fish_recovery=150`, `judgment.csv` 회복 배율은 **1 / 8⁄15 / 4⁄15 / 0**. 판정 시간 50/100/150ms는 그대로다. `songs.csv`의 기존 3초 단위 필드는 유지하므로 새 값은 **69.31986 / 48.628158 / 39 / 80.761974**다. MISS 연속 추가 손실은 1회 0, 2회 5, 3회 이후 10이며 기존 `bite_policy.csv`를 사용한다.

상한이 없는 요청 공식은 `500 + 총 보상 − 전곡 drain − MISS 추가 손실`이다. AP의 공식상 잔량은 **3705.000 / 3055.000 / 5401.543 / 3855.000**으로 모두 800 이상이다. 실제 게임에서는 매 포획마다 1100 상한을 적용하므로 **상한으로 소실된 회복량도 빼야 한다**. 소실량은 각각 **2676.817 / 2050.981 / 4412.842 / 2906.422**다. 위 표는 상한·최종 무먹이 구간까지 반영한 실제 `SatietyState` 결과다.

AP 목표 1000의 단순 역산은 첫 추정으로만 사용했다. 먹이 밀집 구간에서는 상한으로 버려지고, 긴 도입/후주에서는 보상 없이 drain되어 해당 역산으로는 실제 생존이 보장되지 않았다. 1/2/EX는 분산된 GOOD 중심 플레이의 목표 잔량 925/875/825를 실제 타임라인 시뮬레이션으로 역산했다. 3번은 후반 마지막 두 prey를 놓치면 약 21.94초 무보상 시간이 생긴다. mixed 825를 목표로 계산한 18.808088/s에서는 두 실수만으로 클리어 불가능했으므로 **13/s**까지 낮췄다. 이 곡의 대표 mixed와 AP는 후반 상한에 함께 도달해 같은 최종 값이 된다. 더 긴장감 있게 조정하려면 사람 플레이테스트가 필요하며 이번 작업에서 차트/상한/포만감 구조를 바꾸지 않았다.

## 시나리오 검증

대표 분포는 20% PERFECT / 60% GOOD / 15% BAD / 5% MISS를 prey 수에 맞춰 최대 나머지 방식으로 정수화하고, 일정하게 분산한다. 곡별 실제 개수(P/G/B/M)는 **9/26/6/2**, **6/19/5/2**, **10/29/7/3**, **9/28/7/2**다. 비연속 MISS의 추가 패널티는 0이며, 별도로 마지막에 MISS를 몰아 연속 패널티도 검증했다.

| 곡 | 대표 mixed 최소 잔량 | mixed MISS를 끝에 몰았을 때 최종 | 연속 추가 손실 | AP에서 임의의 두 MISS: 최악 최종 |
|---|---:|---:|---:|---:|
| 1 | 298.082 | 899.000 | 5 | 913.791 |
| 2 | 346.627 | 826.241 | 5 | 827.104 |
| 3 | 290.332 | 687.896 — 실패 | 15 | 809.766 |
| EX | 241.569 | 814.383 | 5 | 814.383 |

두 MISS의 **모든 위치 조합**은 네 곡 모두 생존·클리어 가능했다. BAD 70% / MISS 30%의 나쁜 플레이는 각각 약 **43.49 / 47.19 / 97.69 / 28.97초**에 Game Over가 된다. 좋은 플레이의 가능성과 잘못된 플레이의 위험을 함께 검증했다. 같은 등급 분포라도 어느 구간에서 실패하는지에 따라 결과는 다르다.

재현: `python3 tools/audit_satiety.py`. 상세 수치와 상한 손실/사망 시각은 `artifacts/song-end-satiety/satiety-report.json`. runtime 검사에서 실제 `SatietyState`의 AP/mixed 결과를 별도로 확인한다.

## 튜토리얼 시야와 방향

`TutorialGameplay.tscn/VisionMask`의 scene-authored `ShaderMaterial`이 `game/tutorial/tutorial_vision.gdshader`를 사용한다. 별도 화면 샘플이나 noise texture 없이 단일 opacity pass다. material은 모드 인스턴스별로 공유하고, visibility 값이 달라질 때만 shader uniform을 갱신한다. UI/마스크를 매 프레임 다시 생성하지 않는다.

- 중앙 feather: 중심 **0.5**, 양쪽 **0.18** UV 범위, `smoothstep`. Y에 따른 경계 곡률 **±0.025**. 완벽히 수직인 검은 경계 없음.
- 오른쪽 최대 darkness **0.96**, 왼쪽 **0.90**. 어두운 navy `(0.003,0.012,0.025)`를 섞는다.
- 손상 쪽 코너 vignette strength **0.12**, 수직 edge factor 0.25→0.85, 옆 edge factor 0.55→1.0. 전체 alpha 상한 **0.97**로 순수 검정 차단을 피한다.
- 왼쪽 기존 학습 진행 **1 → .9 → .65 → .35 → .15 → 0**과 silhouette **1 → .9 → .7 → .5 → .3 → 0** 유지. 진행값이 같은 feather/vignette opacity를 점진적으로 올린다.
- 마스크 z=0은 배경 위/상어·파동 아래다. fish는 별도 좌우 visibility로 억제한다. 공격 z=4도 tutorial/HUD z=5보다 낮으며, 최종 시험에 추가하던 전체 화면 강제 blackout을 제거했다. 스토리 공격의 짧은 world blackout은 유지한다.
- early fish는 기존 texture 한 개와 기존 chart-timestamp 경로를 그대로 사용한다. **현재 segment의 `to.X−from.X`**가 음수면 `Fish.FlipH=true`, 양수면 false, 수평 이동이 없는 구간은 기존 방향 유지. 입 이후 놓친 먹이의 탈출도 같은 규칙을 사용한다. spawn side만으로 방향을 정하지 않는다. 현재 node는 Sprite2D이므로 추가 frame/좌우 asset이 없다. catch sprite와 일반 blind Gameplay는 바꾸지 않았다.

## 기본 음량

새 설정이 없을 때 **Master .5 / Music .5 / SFX 1**. `SettingsService.Initialize`의 missing-key fallback만 변경했으며 저장된 기존 설정은 그대로 읽는다. 새 프로필을 같은 실행에서 열어도 이전 static Music 값을 상속하지 않는다. UI Open은 실제 서비스/버스 값으로 slider와 percent를 갱신한다. 기존 Settings에는 전체 음량 reset 버튼이 없어 새 버튼을 만들지 않았다.

- Stage/Tutorial/미리듣기 모두 **Music bus → Master**.
- wave pings + Ship Horn/Whale/Float/Sardine 합성 generator, Bite/Whiff/Catch feedback voices, UI click, Result sounds, tutorial attack 모두 **SFX bus → Master**.
- SFX 100%는 SFX bus의 gain이며 Master 50%를 함께 거친다. 프리뷰의 기존 곡별 낮은 trim은 유지한다.

## 검증과 남은 확인

빌드/실행 결과는 `artifacts/song-end-satiety/`에 저장한다. 자동 종료 fixture는 실제 전체 audio/chart/입력/Result를 사용하되 개발 검사 안에서만 시각을 주입한다. 별도로 네 곡 실제 무탐색 완주를 실행한다. 화면 검사는 실제 렌더 픽셀로 중앙 seam, 코너 vignette, full blindness 잔존 밝기와 HUD 보호를 확인했고 공격 이후/왼쪽 fade/최종 시험 화면을 저장했다.

사람의 초보자 플레이테스트, 새로운 150/80/40 보상에 대한 체감 난이도 및 여러 기기에서의 시야 밝기 확인은 남아 있다. 3번 곡은 cap/긴 후주 때문에 대표 GOOD 중심 플레이도 AP와 최종 잔량이 같다는 한계가 있다. 스토리·GOOD+ 세 번 숙련·연속 튜토리얼 음악·Skip/재실행 정책은 유지했다.

최종 빌드 **오류 0 / 경고 0**, 현재 회귀 검사 **501개 통과**. 전곡 종료/경제/설정 231, 시야/방향 실제 렌더 18, 리듬 75, 메인 흐름 21, 튜토리얼 완주 33, 튜토리얼 상태 30, anti-mash 20, 실제 네 곡 완주 73. 런타임 오류 없음. anti-mash fixture의 7개 경고는 의도적인 무음 preview 안내다.

| 실제 무탐색 완주 | PERFECT | 실제 최종 포만감 | 최대 clock/music 차이(ms) | SFX 끊김 | Result 횟수 |
|---|---:|---:|---:|---:|---:|
| song_1 | 43 | 1028.320 | 28.72 | 0 | 1 |
| song_2 | 32 | 1004.147 | 19.00 | 0 | 1 |
| song_3 | 49 | 988.814 | 18.41 | 0 | 1 |
| song_4 | 46 | 948.792 | 17.39 | 0 | 1 |

입력은 실제 target 직후 몇 ms에 처리되어, 상한 소실량이 이론적인 정확한 target 입력과 조금 다르다. 첫 동시 실행 비교 로그에서는 SFX buffer resync로 완주 검사가 중단됐다. 다른 검사를 모두 종료하고 단독으로 다시 실행한 최종 로그에서는 네 곡 모두 resync 0으로 통과했다. 비교 로그를 삭제하거나 검증 조건을 완화하지 않았다. 이 수치는 현재 Mac 실행 결과이며 다른 기기의 성능을 보장하지 않는다.
