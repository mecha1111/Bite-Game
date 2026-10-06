# Cleanup + Optimization

현재 Godot 4.7.2 Mono / .NET 10 파일을 기준으로 정리했다. gameplay 기능·판정·차트·밸런스·현재 음악은 변경하지 않았다.

## A. 삭제

30개 실제 파일 + 연결된 `.uid`/`.import`. 전체 목록/개별 이유: `artifacts/cleanup/deleted.json`.

- 씬 4개: 빈 루트 씬, 이미 없어진 개발 스크립트를 가리킨 고아 씬 2개, 현재 ResultSharkGauge로 교체된 이전 gauge 씬.
- shader 1개: 삭제한 gauge에서만 사용한 이전 fill shader. 같은 gauge C#은 현재 Result/Game Over에서 사용하므로 보존.
- chart 4개: songs.csv가 선택하지 않는 이전 balance 차트. 현재 musical 차트 4개 보존.
- audio 12개: MP3로 교체된 OGG 4개, 현행 SFX catalog에서 교체된 generated WAV 8개. 실제 snap/gulp/thump 보존.
- 도구 3개: 이전 차트 아키텍처를 재생성하는 superseded generator. 현재 phrase authoring/derive/audition 도구 보존.
- calibration 이미지 3개: 현재 indicator에서 참조하지 않는 이전 dot/ring/sync_ring.
- translation 3개: localization에서 쓰지 않는 자동 생성 자료. environment_sequence CSV importer를 `keep`으로 변경해 재생성을 차단했다.
- `.gdignore`: 개발 보고서·스크린샷·도구·제공 차트 원본을 Godot import에서 제외했다. 빈 obsolete 폴더와 Python cache를 정리했다.

삭제 백업: `/private/tmp/bite-cleanup-backup-1791016296`. 이전 문서 백업: `/private/tmp/bite-cleanup-docs-before`. `/private/tmp` 백업은 임시 보관이다.

## B. 보존

Startup/Title/Calibration/Lobby/SettingsPopup/Gameplay/Result/Game Over/EX의 실제 경로, Song Clock/audio sync/Input·Visual Offset, preview/CSV metadata, fixed origins/expanding waves, PERFECT/GOOD/BAD/MISS, empty MISS/one attempt/anti-mash, Satiety/Combo/interference, pause/3-2-1, water shader/profiles/EX transitions/CatchBurst를 유지했다. 기본 UI는 scene-authored 상태다.

현재 music/balance/chart 파일 해시 검증: `artifacts/cleanup/preserved-data-check.json`. 유용한 테스트는 삭제하지 않고 오래된 8-target/24-second/default-animation/OGG 가정을 현재 데이터·씬에 맞췄다.

## C. 중복

동일 파일 바이트의 UI/font/audio 중복은 발견하지 않았다. 설정 기어/뒤로 아이콘/font/theme는 이미 공유한다. 사용되지 않는 중복 `bite` InputMap action을 제거하고 실제 `rhythm_input` 매핑은 보존했다. 이전 Lobby 준비 중 안내를 제거하고 유효 stage의 ScenePath 누락은 개발 오류로 표시한다. WaveLarge의 동일한 override material을 제거해 기본 WavePulse 재질을 사용하고, `_Ready`에서 scene-local 재질을 또 복제하지 않도록 했다. 각 파동의 독립적인 반지름/opacity는 유지된다.

## D. 최적화

- WavePulse: static StringName으로 반복 문자열/native 이름 변환을 제거. viewport/transform/margin이 바뀔 때만 Large 종료 반경 계산. corner 배열 생성 제거. 재사용 PackedScene 참조.
- Audio: voice의 불변 gain/pan 제곱근 계산을 생성 시 한 번으로 이동. sample addressing/겹침/음량/clock은 유지. cue player가 정지·pause되면 Process 비활성화, 재시작 시 활성화.
- CSV: environment rows를 Apply lifecycle에서 한 번 파싱하고 EX profile 전환에서 재사용. 차트 편집/재시작은 현재 파일을 새로 읽는다.
- UI audio: 해제된 Control ID가 영구 binding set에 누적되지 않도록 제거. Calibration에서 잠시 detach한 Gameplay는 binding을 유지한다.
- pooling은 추가하지 않았다. 현행 debris 8/5/3, .25–.45초 수명과 단일 shader Sardine band, 작은 particle 수를 유지했다. shader/look/texture import 압축은 변경하지 않았다.

## E. 검증

Godot Metal Forward+ / Apple M3 Pro / 1920×1080, 120 FPS cap. 최종 회귀 결과는 `artifacts/cleanup/regression-results.json` 및 재검증 기록을 따른다. 초기 stale 테스트 실패도 별도 로그로 보존한다. pause/focus가 멈춘 Song Clock을 무시하고 벽시계로 입력/애니메이션을 기다리던 검증은 실제 Song Clock/상태를 기다리도록 수정했다.

최종 C# 빌드: 오류/경고 0. 충분한 초기화 시간을 준 최종 headless editor import도 오류/경고 0, 필수 참조 누락 0. 현행 검증 씬/driver 17개 모두 통과(1117개 PASS assertion). Title→첫 Calibration→Lobby→Gameplay→Settings→3-2-1→Game Over/Result→retry→Song Select와 EX/재보정 문맥, rhythm/offset/anti-mash/네 간섭/CatchBurst/shark baseline/world·HUD separation을 확인했다. 네 곡은 seek 없이 완주, 170개 target이 각 한 번 판정됐고 SFX underrun=0. 곡별 drift/result는 `artifacts/cleanup/full-song-summary.json`.

동일 10,000회 Large WavePulse 갱신: 53.282ms / 11,589,424 managed bytes → 7.341ms / 40 bytes. 40 bytes는 측정용 Stopwatch다. synthetic heavy workload 990 frames: 최초 최적화 후 P95 9.001ms / P99 10.441ms, 최종 재검증 P95 9.303ms / P99 17.315ms / max 29.991ms (baseline P95 9.023ms / P99 10.477ms / max 24.364ms). GPU FPS 개선을 의미하는 유의한 차이로 해석하지 않는다. frame 관리 할당은 약 23.2MB → 7.9MB. 세 차례 씬 반복 후 Node=4, orphan=0 기준 복귀, SFX underrun=0.

측정 중 GPU 동기 screenshot readback을 포함한 초기 실행에서 관측된 1회 SFX underrun은 측정 코드에서 분리했다. 실제 렌더링 부하 측정 중 캡처하지 않는다. 테스트 및 gameplay timing을 숨기거나 음악 clock을 조정하지 않았다.

전체 검증: `python3 tools/run_regressions.py --godot <Godot Mono executable> --full-songs`. 최초 목록 검사: `python3 tools/audit_project.py`.

## F. 불확실성/범위

`assets/music/1.mp3`, `2.mp3`, 미참조 GIF와 일부 artwork는 source/master 여부가 불명확해 보존했다. 제공 v2 chart와 과거 분석/스크린샷은 provenance로 보존하며 runtime fallback이 아니다. 분류/참조/해시 전체 목록은 `artifacts/cleanup/reviewed-inventory.json`이다. category E는 삭제하지 않았다.

프레임 tail은 반복 측정에서 변동하며 이 기기에서도 드물게 약 30ms까지 관측됐다. 안정적인 GPU frame-time 개선으로 주장하지 않는다. 모든 측정에서 SFX underrun=0이며 실제 네 곡의 최대 clock drift는 15.66–26.87ms였다.

ChartAudition의 listening_reviews.csv는 승인 시 생성되는 optional 개발 출력이며 누락된 필수 리소스가 아니다. 자동 검증은 사람의 청음·장치별 latency 평가를 대신하지 않는다. 별도 플랫폼/GPU 및 PCK export 패키지 검증은 이번 데스크톱 실행 검증 범위 밖이다.
