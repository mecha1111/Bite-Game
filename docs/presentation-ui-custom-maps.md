# BITE Result / UI / current / optional custom maps

## 작업 상태

현재 실행 파일을 기준으로 수정했다. 이 문서는 **전체 작업 완료 보고가 아니다**. `BITE_CustomMaps_v1` 및 6개 first-pass prey/camera CSV는 프로젝트·Downloads·Desktop에서 발견되지 않았다. 음원/아트만 확인됐다. 패키지 경로를 요청했으며, 차트 타점을 임의 생성하거나 다른 곡으로 fallback하지 않았다. 커스텀 3곡은 preview/cover를 사용할 수 있지만 Gameplay 시작은 chart 연결 전 차단한다. 자동 실행/음원 파형 검사를 사람의 청취 승인으로 표현하지 않는다.

## 1–3. Result 복구

- 애니메이션은 삭제된 상태가 아니었다. `ResultScreen.cs`의 Tween 및 두 열/판정 PNG/정산 로직이 현재에도 남아 있었다. Git repository는 없어 과거 파일과 비교했다고 주장하지 않는다.
- 실제 캡처의 상어 이미지가 사라졌고 percentage가 왼쪽 위에 붙었다. 원인: `ResultScreen.tscn`의 inherited SharkImage/Percentage 자식에 **layout_mode=0 override**가 있어 base scene의 앵커 배치를 무효화했다. 두 자식에 layout_mode=1과 앵커를 명시했고 base gauge도 같은 모드로 고정했다. 숫자/Shader uniform만 보는 기존 테스트에 실제 nonzero 이미지/label 크기 검사도 추가했다.
- 순서: world/dim 정착 .08초 → panel/dim .16 → header .12 → 4 rows/counts 각 .10 → max combo .08 → empty shark .08 → fill .80 → percent/state .12 → actions .12. 총 **1.96초**. Accept로 reveal을 끝내는 기존 기능/Retry focus/라우팅/FC/AP/최고 메달 저장은 유지.
- 실제 계층: `ResultScreen → BackgroundDim + ResultPanel/Content(Header, Body/LeftColumn(JudgmentRows,ComboSection), Body/RightColumn(SatietySection/SharkGauge,StateSection)) + Actions + ResultAudio`.
- 전용 `ResultSharkGauge.tscn` / `assets/투명한 게이지 픽셀 상어.png` / interior mask를 사용한다. PERFECT/GOOD/BAD/MISS PNG 그대로; GREAT 없음. 게임플레이 긴 게이지로 대체하지 않았다.

## 4–5. 픽셀 / 가독성

- Result atlas source 밖의 패딩/stray 영역은 기존 crop을 유지하고 **filter_clip=true**로 경계 샘플을 차단했다. 큰 opaque backing을 추가하지 않았다. nearest texture_filter=1을 유지.
- `snap_2d_vertices_to_pixel=true`: 렌더 정점만 픽셀 정렬한다. 차트/입력/Node 좌표를 반올림하지 않는다. 비정수 viewport stretch에서 맞닿는 패널 정점의 fractional seam을 줄인다.
- Satiety percent neodgm **42px**, near-white, 2px dark shadow, outline 0. fill/overfill/percentage/logic은 유지했다.
- Result/GameOver/Settings/Tutorial/Gameplay HUD/Combo/Main & Custom menu를 1920×1080,1600×900,1366×768,1280×720,2560×1440에서 실제 캡처. `artifacts/presentation-update/*-{width}.png`. 확인한 Result/패널/게이지에 배경이 새는 seam 또는 stray UI pixel은 보이지 않았다. 비정수 해상도에서는 원본 pixel 폭이 물리 1/2px로 번갈아 표시되는 nearest 확대 특성은 남는다.
- Editor의 Tool StageCard가 non-Tool SwimmingMedalShark를 타입 바인딩하지 못했던 문제도 해당 자식에 `[Tool]`을 적용하고 editor processing을 끄는 기존 조건을 유지했다.

## 6–10. 새 Sardine current

삭제: `SardineBand.cs(.uid)`, `presentation/SardineBand.tscn`, `sardine_band.gdshader(.uid)`. `InterferenceSardine.tscn`의 20개 old wave template/band를 1개 audio origin과 current로 교체. band 각도/밀집 texture/layer refresh runtime 없음. 제공 정어리 말풍선 아트는 별도 역할이므로 보존.

- Scene `game/gameplay/presentation/SardineVortex.tscn`, script `SardineVortex.cs`, shader `sardine_vortex.gdshader`.
- **5.0초**. 0–.5초 smoothstep formation, .5–4.5 full, 4.5–5 smoothstep fade. 중심 viewport (.5,.30), 낮은 opacity .22의 deterministic spiral.
- 최대 **95px** Art-only pull. `WavePulse.Art.Position`만 중심 방향으로 offset, local ring shader의 .22 curvature/rotation과 radius×.30 draw margin. 랜덤 jitter 없음. 종료 시 Art offset/current strength 0 복귀.
- prey와 interference의 살아 있는 ring 모두 적용. **WavePulse.Origin/CurrentRadius/StartedAt, chart TargetTime, Song Clock, input windows/offset/resolve**는 변경하지 않는다. 실제 inspector/test에서 origin/radius/target/clock 불변을 비교했다.
- Sardine SFX 구조 유지, configuration은 `interference_effects.csv`의 duration=5, interval=.5, emissions=10. 종료 후에도 다른 Large ring을 잘라내지 않는다. 기존 band 검사 대신 current curve/5초 종료 검사로 갱신했다.

## 11–14. removable module

- Root **`res://custom_maps/`**: `audio/`, `assets/`, `registry/catalog.json`, 준비된 `charts/`, `camera_fx/`, `data/`.
- Core의 유일한 optional-content entry **`game/data/CustomMapRegistry.cs`**. JSON manifest가 없으면 empty registry. Core tscn/tres가 custom map resource를 직접 preload하지 않는다. 공유 GameplayData/Environment/StagePresentationChart/SongPreview/StageCard가 registry definition만 조회.
- 기존 Lobby 카드/arrow/preview를 재사용. scene-authored `Lobby.tscn/Categories/Main`, `/Custom`로 **메인 4곡과 커스텀 3곡을 별도 목록**으로 표시. category가 기존 Catalog를 변경하지 않고 다른 Resource 목록을 선택한다. missing module이면 Custom button 숨김. 키보드/마우스 모두 연결.
- gameplay→Lobby는 custom category로 복귀할 수 있고 Settings calibration/tutorial replay의 category context도 유지.
- 저장은 **`custom_map_progress` section, custom StageId별 최고 rank string**. 메인 progress/unlock/FC/AP 배열에 쓰지 않는다. 더 낮은 rank로 downgrade하지 않는다.
- 테스트에서 manifest를 임시 비활성화→registry empty→main 4곡 chart load 성공→원본 복원. 실제 custom root를 삭제하지 않고 removal 경로를 검증했다.

## 15–18. custom songs / 차트 상태

| Song | actual AudioStream duration | first-pass Music BPM (미확정) | 실제 prey count / 조정 |
|---|---:|---:|---|
| 냉탕에 상어 | 219.103485s | 117.19 | supplied chart 미연결; 후보 52는 user 정보 |
| 저곳으로 | 216.201004s | 133.93 | supplied chart 미연결; 후보 48는 user 정보 |
| Under the Sea | 195.163712s | 133.93 | supplied chart 미연결; 후보 64는 user 정보 |

아직 final BPM/Gameplay BPM/TargetTime/StartTime/기아 균형을 확정하지 않았다. **청취 후 수동 이동한 타깃 없음**. 최종 chart 경로도 없는 상태이며 registry `Available=false, ChartPath="", FxPath=""`로 명확하게 차단한다.

음원은 각각 `custom_maps/audio/shark_pool.mp3`, `part_of_your_world.mp3`, `under_the_sea.mp3` (제공 원본 MP3 바이트를 이동, 재인코딩 없음). Godot의 실제 decode length 및 Music bus preview를 사용.

아트 inspection 결과:
- 냉탕: `shark_pool_cover.png`는 OCEAN VIBES/SUPER BITE 종이 포스터. `shark_pool_background.png`는 빈 coral environment. 각각 cover/gameplay 역할.
- 저곳: `part_of_your_world_cover.png`는 검정 바탕 인물 menu art. Gameplay **black rule** (`BackgroundPath=""`), custom black flag만 world fog/ray/ambient particles를 끄고 preserve_black uniform으로 color/light pulse가 빈 배경을 채우지 않게 한다. shark/waves는 읽히도록 유지. 다른 바다 art 삽입 없음.
- Under the Sea: `under_the_sea_cover.png`는 인어 인물/바다, `_background.png`는 인물이 없는 ocean. menu/gameplay로 각각 분리.

## 19–21. custom FX 상태

제공 camera CSV가 없으므로 final custom FX timeline을 발명하지 않았다. registry의 FxPath가 연결되면 기존 cached presentation loader가 같은 Song Clock으로 읽는다. First-pass CSV schema 및 실제 타점/강도/slow push 등 type conversion은 패키지를 받은 뒤 확인해야 한다. current code에 지원되지 않는 type은 명확한 data error이며 silently 무시하지 않는다. .45 important reading / .35 Whale-current intensity reduction과 fixed HUD는 shared presentation 그대로 사용한다.

## 22–27. interference bubbles

`game/gameplay/presentation/InterferenceIndicators.tscn` **CanvasLayer layer=1**. critical modal 열림/paused/result/gameover 때 숨긴다. static UI는 한 번 scene instantiate; time만 전달하고 rebuild하지 않는다.

| Type | static pop PNG | GIF에서 제공된 동일 frame-strip resource | slot (viewport fraction) |
|---|---|---|---|
| Whale | assets/고래 움1.png | assets/고래 움.gif → 고래 움.png → indicators/whale.tres | right (.925,.57) |
| Float | assets/낚시함1.png | 낚시함.gif → 낚시함.png → fishing_float.tres | top-right (.825,.13) |
| Horn | assets/배 옴1.png | 배 옴.gif → 배 옴.png → ship_horn.tres | top-left (.145,.13) |
| Current | assets/정어리 옴1.png | 정어리 옴.gif → 정어리 옴.png → sardine.tres | left (.075,.57) |

- 제공 strip은 128px tile, 12/14/10/14 frames; 원본 GIF duration을 SpriteFrames에 반영. 런타임 GIF decode/별도 애니메이션 timer 없이 presentation Song time으로 frame 선택.
- static PNG pop **.35 → 1.12 (.14초) → 1.00 (.10초)**. 첫 .24초 static, 이후 animated strip. 종료 **.18초 shrink .9 + alpha fade**.
- 190×190 reference bounds, 위치 Round. 4개 distinct slots; Gauge/Combo/Settings/Judgment/Tutorial message rectangle 예약. 충돌하면 숨겨 critical text를 침범하지 않는다. viewport size event로 layout만 재계산. Float Emitted마다 pop 재시작. Whale/current는 실제 IsActive 전체 기간 유지.

## 28–31. main FX pass

기존 authored CSV/camera/world compositor를 재사용하며 차트/곡을 바꾸지 않는다. Stage 1/2/3 각각 기존 prey phrase payoff 3개에 **camera_follow_wave** (.65초, 3/4/4.5px)을 추가했다. 전체 normal visual events **45/46/57**. Existing kick/bass/light/vent/drop/current language 및 sparse camera cap은 유지. EX 1→3→1→2→3→2→1, 각 환경의 authored crossfade/pressure/caustic + camera + profile blend를 실제 실행했다.

세부 추가 이벤트는 `data/presentation/stage_fx_chart.csv` notes=Selected existing phrase payoff. 실제 음악적 타점의 인간 감상 검토는 여전히 pending이며 '청취 완료'로 표현하지 않는다.

HUD는 기존 WORLD z≤3 → shader composite z4 → HUD z≥5가 유지된다. 카메라는 WORLD sampling UV만 변환해 좌표계를 바꾸지 않는다. Speech만 별도 fixed CanvasLayer이며 modal에서 숨겨진다. 실제 HUD 픽셀 비교 통과.

## 32–33. 검증 / 남은 작업

- build 경고/오류 0.
- UI-update **64**(5개 해상도/실제 Result 이미지 크기/custom 배경 적용 포함), Result 기존 **47**(실제 전체 Stage 1 종료/Retry 포함), Polish **231**, anti-mash **20**, rhythm **75**, 새 Stage FX **273**, Tutorial mastery 최종 **22**, EX **33**, Satiety sections **147**, processed audio **43**, catch audio **54**, Settings **33** 확인. Result의 추가 nonzero anchor assertion은 UI-update에서도 실행했다.
- 고부하 EX 3,538 frames: p50 **8.328ms**, p95 **8.912ms**, p99 **10.591ms**, max **22.903ms**, bubble peak85, SFX underrun0, Clock drift14.165ms. current + speech + rings + shader + CatchBurst workload / retry cleanup 포함. custom 실제 chart 부하는 미검증.
- 첫 전곡 Stage 1 검사에서는 108.886초에 93.726ms frame gap과 SFX underrun 2회를 관측했다. `GameplayAudio.Pump`의 매-frame PCM 배열을 3,072-frame 재사용 버퍼 + `ReadOnlySpan<Vector2>`로 교체하고 SatietyGauge/GameplayJuice의 per-frame shader StringName 변환을 cache했다. 기존 48kHz/64ms lookahead, PCM sample count, music/clock/offset/버스는 그대로다. 후속 Stage 1 전곡은 underrun 0, max frame gap45.67ms, drift37.65ms로 통과. GC와 동시에 발생한 정지를 관측했지만 모든 외부 stutter가 제거됐다고 주장하지 않는다.
- 오디오 기능 54개는 통과. 첫 실행의 종료 resource warning은 verbose 재실행에서 재현되지 않았다. 두 로그 모두 보관한다. 새 최종 고부하 검사도 273개/underrun0 통과했으며 max47.758ms의 단발 frame gap은 남았다.
- 초기 retry에서 C# profile wrapper/cache lifetime 실패가 한 번 관측되어 immutable background/profile cache를 shared static으로 유지하도록 했다. 최종 4-stage repeated FX test 통과. 초기 실패 로그를 green 결과로 숨기지 않는다.
- Tutorial의 production logic/음악/프로필은 해시 불변. 초기 시간 assertion 실패가 있어 실제 transition/clock/success count를 테스트 출력에 넣고 재실행했고 22개 최종 통과. tutorial architecture를 재작성하지 않았다.
- Main 4곡은 CoreAudio 전곡 실행했다. 각 곡의 모든 authored presentation event가 실행되고 실제 곡 끝에 Result 1회/Gold 저장을 검증했다. 첫 EX 실행은 시작 .101초의 SFX underrun 1회로 엄격한 audio check 실패; 독립 재실행은 0회/전곡 통과. 초기 실패를 삭제하지 않았다. 시작 경고의 재현 조건은 미확정이다.

| Final full-song run | Actual length | Perfect / MISS | SFX underrun | Max frame gap | Max clock drift |
|---|---:|---:|---:|---:|---:|
| Hear the Tide | 140.435944s | 43 / 0 | 0 | 45.67ms | 37.65ms |
| Hidden Current | 138.500000s | 32 / 0 | 0 | 37.99ms | 40.79ms |
| Predator's Pulse | 188.342850s | 49 / 0 | 0 | 38.03ms | 48.33ms |
| EX Deep Current (rerun) | 131.683258s | 46 / 0 | 0 | 38.32ms | 42.99ms |

Logs: `artifacts/presentation-update/full-song-1.log`–`full-song-4.log` (initial EX warning retained), `ex-rerun/full-song-4.log` (final run). Headless CoreAudio full-song checks verify runtime timing/flow, not human listening/art-direction judgment. Separate rendered UI/current/heavy scene captures are retained. Custom environment-only fixtures use the shared environment on a frozen main-scene test; they are explicitly **not custom-song Gameplay passes**.
- **Required missing input:** BITE_CustomMaps_v1 package path / 6 CSV. 전달되면 root 안에 보관하고 schema 변환→no-overlap/target-first 검증→곡별 Satiety budget→실제 3곡 실행→전후 audition record / 인간 청취 tuning. 그 전까지 Custom Gameplay 미완료이며 final timing/art direction 승인도 pending.
