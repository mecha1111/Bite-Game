# BITE 음악 동기화 연출 보고

## 런타임 연결과 보존

- 실행 경로: `Startup.tscn → Gameplay.tscn → StagePresentation.tscn`. `GameplayScreen`이 기존 Song Clock의 현재 초를 전달한다. 연출에는 Clock 쓰기/seek/판정/포만감/타깃 생성 권한이 없다.
- 일반 연출: `data/presentation/stage_fx_chart.csv` (`song_id,time_sec,event_type,strength,duration,parameter,optional_target,notes`). EX: `data/presentation/ex_fx_chart.csv` (`time_sec,environment_id,transition_type,camera_event,color_event,strength,duration,optional_target,notes`). 시작 전에 파싱하고 곡별 캐시를 재사용한다.
- EX 환경 선택은 기존 `data/balance/environment_sequence.csv`가 소유한다. EX FX의 환경 시각/ID/전환 종류/길이와 정확히 일치하는지 검증하며 불일치는 오류다.
- `RhythmController.cs`, `SatietyState.cs`, 판정/곡/먹이/포만감 데이터와 Tutorial C# 15개 파일의 SHA-256은 작업 전과 동일하다. 튜토리얼은 새 무대 FX/추진 물방울을 활성화하지 않는다.

## 1–3. 일반 스테이지 카메라

시각은 선택 MP3의 실제 Song Clock 초다. 타깃은 수정하지 않고 현재 음악/차트의 선택된 구절 악센트에 반응한다. 매 박 카메라는 없다. 조용한 구절에는 약한 빛/색만 사용한다. 상세 빛·입자 이벤트는 CSV 원문이 기준이다.

### Stage 1 — Hear the Tide

총 42개 시각 이벤트.

| 초 | 카메라 | 강도 | 초 길이 | 방향 |
|---:|---|---:|---:|---|
| 2.983214 | camera_pulse | 0.017 | 0.25 | world |
| 11.982761 | camera_pulse | 0.017 | 0.25 | world |
| 21.585935 | camera_pulse | 0.017 | 0.25 | world |
| 31.184121 | camera_pulse | 0.017 | 0.25 | world |
| 37.175505 | camera_pulse | 0.017 | 0.25 | world |
| 79.783668 | camera_pulse | 0.017 | 0.25 | world |
| 89.376865 | camera_pulse | 0.017 | 0.25 | world |
| 98.386389 | camera_pulse | 0.017 | 0.25 | world |
| 107.984575 | camera_pulse | 0.017 | 0.25 | world |
| 128.318362 | camera_pulse | 0.017 | 0.25 | world |
| 134.918362 | camera_pulse | 0.017 | 0.25 | world |

### Stage 2 — Hidden Current

총 43개 시각 이벤트.

| 초 | 카메라 | 강도 | 초 길이 | 방향 |
|---:|---|---:|---:|---|
| 7.283441 | camera_pan | 5 | 0.8 | left |
| 28.555096 | camera_pan | 5 | 0.8 | right |
| 31.054416 | camera_pulse | 0.021 | 0.25 | world |
| 43.266661 | camera_pulse | 0.021 | 0.25 | world |
| 55.483895 | camera_pulse | 0.021 | 0.25 | world |
| 63.505663 | camera_pan | 5 | 0.8 | right |
| 84.088883 | camera_pan | 5 | 0.8 | left |
| 92.340130 | camera_pulse | 0.021 | 0.25 | world |
| 104.123350 | camera_pulse | 0.021 | 0.25 | world |
| 116.984121 | camera_pulse | 0.021 | 0.25 | world |
| 116.984121 | camera_pan | 5 | 0.8 | right |
| 126.193192 | camera_pulse | 0.021 | 0.25 | world |

### Stage 3 — Predator’s Pulse

총 54개 시각 이벤트.

| 초 | 카메라 | 강도 | 초 길이 | 방향 |
|---:|---|---:|---:|---|
| 16.128339 | camera_pulse | 0.026 | 0.25 | world |
| 16.128339 | camera_kick | 6 | 0.2 | left |
| 37.723895 | camera_drop | 0.034 | 0.7 | world |
| 37.883895 | camera_pulse | 0.026 | 0.25 | world |
| 37.883895 | camera_kick | 6 | 0.2 | left |
| 48.335142 | camera_pulse | 0.026 | 0.25 | world |
| 48.335142 | camera_kick | 6 | 0.2 | left |
| 58.641718 | camera_pulse | 0.026 | 0.25 | world |
| 58.641718 | camera_kick | 6 | 0.2 | left |
| 72.490244 | camera_pulse | 0.026 | 0.25 | world |
| 72.490244 | camera_kick | 6 | 0.2 | left |
| 82.642171 | camera_pulse | 0.026 | 0.25 | world |
| 82.642171 | camera_kick | 6 | 0.2 | left |
| 112.483985 | camera_drop | 0.034 | 0.7 | world |
| 120.645799 | camera_pulse | 0.026 | 0.25 | world |
| 120.645799 | camera_kick | 6 | 0.2 | left |
| 133.641264 | camera_pulse | 0.026 | 0.25 | world |
| 133.641264 | camera_kick | 6 | 0.2 | right |
| 144.257137 | camera_pulse | 0.026 | 0.25 | world |
| 144.257137 | camera_kick | 6 | 0.2 | right |
| 155.112466 | camera_pulse | 0.026 | 0.25 | world |
| 155.112466 | camera_kick | 6 | 0.2 | right |

Stage 1: 얕은 바다의 cyan 광선(.012 이하)·caustic(.009 이하)과 제한된 물방울 악센트. Stage 2: cyan/teal(.030 이하), 5px 좌우 current pan와 배경 ±2px 수준 parallax/입자 방향 변화. Stage 3: 6px kick, .026 zoom pulse, 두 .034 drop과 약한 vent 색/방출. Stage 3 카드에 남아 있던 중층 그림도 실제 분화구 환경 그림과 일치시켰다.

## 4–6. EX 환경 / 카메라 / 색 타임라인

환경 순서 **1 → 3 → 1 → 2 → 3 → 2 → 1**. EX 카드의 기존 3분할 cover는 유지한다.

| 초 | 환경 | 전환 | 카메라 | 색 | 강도 | 길이 | 방향 |
|---:|---|---|---|---|---:|---:|---|
| 0.000000 | song_1 | crossfade | camera_pan | cyan | 4 | 0.8 | left |
| 12.571423 | song_3 | pressure | camera_drop | vent | 0.038 | 0.45 | center |
| 17.849427 | 유지 | accent | camera_pulse | teal | 0.025 | 0.28 | right |
| 27.203169 | 유지 | accent | camera_follow_wave | teal | 5 | 0.6 | right |
| 37.260312 | song_1 | caustic_blend | camera_zoom | cyan | 0.025 | 0.75 | center |
| 37.260312 | 유지 | accent | camera_pulse | teal | 0.025 | 0.28 | left |
| 46.135142 | 유지 | accent | camera_pulse | teal | 0.025 | 0.28 | right |
| 55.169609 | 유지 | accent | camera_pulse | teal | 0.025 | 0.28 | left |
| 62.153736 | song_2 | crossfade | camera_pan | teal | 6 | 0.85 | right |
| 69.028112 | song_3 | pressure | camera_drop | vent | 0.04 | 0.45 | center |
| 71.317908 | 유지 | accent | camera_follow_wave | cyan | 5 | 0.6 | left |
| 79.798634 | 유지 | accent | camera_pulse | cyan | 0.025 | 0.28 | left |
| 87.730607 | 유지 | accent | camera_pulse | cyan | 0.025 | 0.28 | right |
| 92.145573 | song_2 | crossfade | camera_pan | teal | 4 | 0.9 | left |
| 104.687069 | song_1 | caustic_blend | camera_drop | cyan | 0.034 | 0.65 | center |
| 106.612693 | 유지 | accent | camera_pulse | cyan | 0.025 | 0.28 | right |
| 114.908838 | 유지 | accent | camera_follow_wave | cyan | 5 | 0.6 | left |
| 121.259405 | 유지 | accent | camera_pulse | teal | 0.025 | 0.28 | left |
| 126.058498 | 유지 | accent | camera_pulse | teal | 0.025 | 0.28 | left |

환경 crossfade는 두 배경과 수중 프로필을 같은 Clock에서 보간한다. pressure는 짧은 zoom-out → zoom-in, 작은 압력 굴절(.00015 UV), caustic/입자 악센트. caustic_blend는 밝기 wash와 crossfade. 이전 배경은 전환이 끝나면 숨긴다. 모든 변경에 전환이 있으며 즉시 texture만 바꾸는 경로는 없다.

EX 색 강도는 `min(.065, strength × 1.3)`이며 표의 강도는 카메라 기준이다. vent는 낮은 주황색(.7,.27,.11), cyan(.24,.7,.86), teal(.14,.65,.55). WORLD의 밝은 파동은 색/어두움 영향이 적도록 보호한다. 흰 화면 flash·무작위 색·지속 shake는 없다.

## 7–10. 카메라 한계 / HUD

- 일반 offset 축별 최대 **8px**, zoom 최대 **1.030**. 강한 drop 최대 **12px / 1.040**, pullback 최저 **.988**. 원점 (.5,.65), 회전 없음. 실제 sparse 차트 값은 위 표에 있다.
- PERFECT/GOOD/BAD Bite의 작은 위쪽 impulse는 **2.5/1.5/.6px**, MISS 추가 impulse 0. 모두 .17초 이내 복귀.
- 어려운 ex1/m3/s2 읽기 또는 타깃 ±.4초에서 강도 **.45배**, Whale/Sardine 활성 시 **.35배**. scene `Intensity` 0–1을 곱한다. 기존 reduced-FX 옵션은 없어서 새 Settings 범주를 만들지 않았다.
- 실제 Camera2D나 노드 transform을 바꾸지 않는다. 기존 **WORLD z≤3 → world_water 합성 z4 → HUD z≥5**라는 CanvasLayer 동등 분리를 유지하고 합성 shader의 WORLD 샘플 UV에만 zoom/offset을 적용한다. 따라서 HUD/입력 좌표/고정 슬롯/물기 기준점이 이동하지 않는다. Tutorial/설정/일시정지도 기존 위 레이어에 남는다.
- 렌더 픽셀 비교: WORLD 표식이 움직이는 동안 z5 HUD 표식은 픽셀까지 동일함을 테스트한다. 실제 Gauge/Judgment/Combo/Settings rect와 mouth 좌표도 비교한다.

## 11–14. 추진 물방울

- 재사용 scene `game/gameplay/presentation/SharkJumpBubbleEffect.tscn`; 원본 **`res://assets/물방울.png`**, transparent padding만 AtlasTexture (190,190,260,260)로 잘라 사용한다.
- Bite당 **24–36개**, 동시에 최대 **256개**. 따로 stage accent batch 최대 96개. 한 Node2D의 사전 할당 struct 배열과 texture draw를 사용하며 물방울별 노드/타이머는 없다.
- 원점 `Shark.GlobalPosition + (0,-28)`; lower-body x ±140px, y -10…12px에서 시작. WORLD z2로 상어에 전부 가려지지 않게 한다. HUD/새 전체 화면 pass 없음.
- scale 작은 .35–.60(50%), 중간 .65–1.00(40%), 큰 1.05–1.35(10%), 기본 표시 크기 9px. 원본의 640px 빈 여백 때문에 scale은 잘라낸 표시 크기에 상대적이다.
- 수명 **.35–.80초**, alpha .70–1.00 → `(1-age/life)^1.25`로 0. 초기 속도 y -85…-170px/s, x ±110px/s; drag 2.5와 완만한 부력/좌우 drift. 수중 WORLD 셰이더 적용.
- 모든 Bite는 운동 물방울을 낸다. MISS도 운동 물방울/빈 ripple만 있으며 성공 CatchBurst/피/살점은 생성하지 않는다. 기존 CatchBurst 데이터는 변경하지 않았다.

## 15–20. 메달 상어 / 저장 / 판정 기준

| 최고 기록 | 원본 resource | 기준 |
|---|---|---|
| Bronze | `res://assets/상어 동.png` | 기존 `Result.Cleared` (포만감≥clear threshold) |
| Silver | `res://assets/상어 은.png` | 기존 `Result.FullCombo` |
| Gold | `res://assets/상어 금.png` | 기존 `Result.AllPerfect` |

- `data/presentation/medal_sharks.csv`: priority/criterion/texture/natural facing/width/swim seconds. Gold > Silver > Bronze > None. 최고 하나만 표시. 포만감 110%는 Gold 조건이 아니다.
- 원본은 모두 **왼쪽 방향**. 오른쪽으로 수영할 때 `FlipH=true`, 왼쪽으로 돌아올 때 false. 폭은 opaque region 기준 카드 폭 **16%**, 한 방향 **18초**, 작은 8px bob. `StageCard.tscn/MedalWater/SwimmingMedalShark`는 환경 영역에 clip되며 제목 위를 가리지 않는다. 잠긴 카드에서는 숨기고 processing을 끈다.
- `progress.stage_medal_ranks`: `{stage_id: "bronze"|"silver"|"gold"}` ConfigFile Dictionary. None은 생략. 기존 `full_combo_songs` / `all_perfect_songs`도 보존하고 Silver/Gold로 이관한다. 기존에는 clear 기록이 없었으므로 과거 Bronze는 추정하지 않는다.
- 정상 곡 종료에 이미 정산된 `Result`의 세 flag로 저장한다. 더 높은 priority만 갱신한다. Game Over/튜토리얼은 메달을 쓰지 않는다. Lobby 재바인딩은 저장된 최고 기록을 바로 읽는다. 해금 규칙/사용자 설정은 바꾸지 않았다.
- **Full Combo**: ExpectedTargets>0, `PERFECT+GOOD+BAD == ExpectedTargets`, MISS=0. 기존 규칙상 BAD도 연결된 Combo다. **All Perfect**: FullCombo이고 `Perfect == ExpectedTargets`. 기준은 기존 Result/Satiety 구현을 그대로 사용한다.

## 21. 성능 / 가독성 / 검증

배경·water profile은 음악 시작 전에 캐시, EX 이전 배경 material도 음악 시작 전에 한 번만 복제·재사용. 같은 단일 WORLD post-process에 연출 uniform만 더했고 별도의 전체 화면 pass는 없다. FX chart는 캐시하며 UI를 재생성하지 않는다. 물방울은 bounded analytic batch로 pause와 동일 Song Clock에서 정지/소멸한다.

Shader uniform 이름도 StringName으로 캐시했다. 실제 1,000회 동일 호출 측정: 문자열 변환 96,000 bytes → 캐시 0 bytes (관리 메모리). 연출 값/시각은 동일하며 오디오 버퍼나 Song Clock을 변경하지 않았다.

C# build: 경고 0 / 오류 0. 새 연출 264개 + 기존 회귀 769개 = **1,033개 검사 통과** (13개 test scene). 회귀 로그: `artifacts/stage-presentation/verified-regressions.json`. EX 시점 이동 fixture는 GUI focus 알림으로 paused clock snapshot이 이전 시점에 남는 문제가 있어 focus 확보와 paused snapshot 일치만 보정했다. 실제 런타임 Clock은 변경하지 않았고 최종 EX fixture 33개가 통과했다.

실제 1920×1080 / Metal / Apple M3 Pro 고부하 EX 반복: 3,538 frames, p50 **8.360ms**, p95 **9.173ms**, p99 **12.040ms**, 최대 **33.208ms**. 추진 물방울 동시 peak 87개, SFX underrun **0**, Music/Song Clock 최대 오차 **14.280ms**. 2회 scene 종료/재시작 후 nodes/orphans 기준치 복귀. Scene/choreography export Intensity=0 및 sensing-denial .35배 적용도 검증했다. 이 수치는 현재 기기 측정이며 다른 플랫폼의 성능을 보증하지 않는다.

HUD 픽셀/각 메달/물방울/고부하 스크린샷은 `artifacts/stage-presentation/`에 보관한다. 전체 곡 검증은 실제 main scene의 `--musical-audit-driver` (개발 전용)로 실행한다. `--audit-song=1..4`를 붙이면 해당 곡만 독립 실행하며 타임라인을 seek하지 않는다. 첫 일괄 실행은 Stage 2 종료 직전에 완료 표시 없이 종료됐으므로 완주로 계산하지 않고 `full-songs-first-run.log`로 보관한다. 그 실행의 Stage 1만 완주·Result·Gold 저장이 확인됐다. 첫 창 기반 Stage 2 실행의 위치 비교에는 최대 **63.19ms** 오차도 관측됐다. 새 연출과의 인과관계는 확인되지 않았으며 짧은 렌더 부하 검증만으로 이 장시간 관측을 해결됐다고 주장하지 않는다. Stage 2의 두 번째 창 기반 실행도 49초 이후 완료 표시 없이 종료됐다. 두 창 실행의 종료 원인은 확인되지 않았으며 통과로 계산하지 않는다. headless screenshot guard를 수정한 뒤 실제 CoreAudio Stage 2/3 독립 전곡 검증은 모두 통과했다 (각 32/49 PERFECT, MISS 0, Clock 최대 오차 36.44/41.92ms, SFX underrun 0, Result 1회·모든 FX 실행·Gold 저장). EX 전곡은 46 PERFECT/0 MISS로 끝까지 재생됐으나 SFX underrun 검사에서 실패했다. 마지막 수정 전 기록은 시작 0.097초에서 1회, 37.537초에서 67.132ms frame gap과 함께 추가 1회 (GC collection count 9/6/4); 최대 Clock 오차 42.49ms였다. 시작/GC 정지의 정확한 원인과 새 연출과의 인과관계는 확정하지 않았다. StringName 캐시·EX material 사전 준비 후 최종 EX 전곡 재검증은 **통과**: 46 PERFECT, MISS 0, SFX underrun 0, 최대 Clock 오차 47.33ms, 최대 frame gap 56.53ms, 17개 검사. 모든 FX와 마지막 얕은 바다 복귀·Result 1회·Gold 저장을 확인했다. 한 번의 재검증 통과로 모든 장기 재생/다른 기기의 오디오 정지를 해결했다고 일반화하지 않는다. 스크린샷 읽기/PNG 저장은 음악을 일시정지한 상태에서 수행하여 인위적인 디스크/GPU readback 정지가 오디오 부하 측정에 섞이지 않도록 했다.

청취 제한: 현재 실제 음원/차트 구절 악센트와 기존 분석을 기준으로 작성했지만 사람의 전곡 청취 감상은 완료했다고 주장하지 않는다. 시각적 취향/최종 악센트 선택은 실제 플레이 청취에서 추가 조정할 수 있다. 그 조정도 이 presentation CSV만 변경하면 된다.

재현 명령: `python3 tools/run_regressions.py --godot <Godot Mono executable> --presentation-songs --only full-song-1,full-song-2,full-song-3,full-song-4 --output artifacts/stage-presentation` (전곡 실제 CoreAudio, 창 없음). GUI 연출 회귀는 `--only stage-FX,EX`로 별도 실행한다.
