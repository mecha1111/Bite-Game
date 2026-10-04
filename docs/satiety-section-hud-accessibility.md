# 포만감 HUD · 구간별 허기 · 판정 접근성

현재 파일과 네 최종 차트를 기준으로 수정했다. 차트·곡·prey 수·Song Clock·종료 라우팅은 바꾸지 않았다. 이전 전곡 보고의 drain/판정 수치는 이 보고가 대체한다.

## 게이지 수정

- 오류 원인: 1100을 UI 100%로 정규화해 500이 45.45%, 800이 72.73% 위치였다. 숫자도 퍼센트 대신 값/최대값이었다. 기존 사각형 fill은 원본의 투명 내부 영역과 높이/끝점이 어긋났다.
- 표시: `clamp(Satiety,0,1100)/10%`. 몸통: `clamp(VisualSatiety/1000,0,1)`. 1000 초과는 `clamp((VisualSatiety-1000)/100,0,1)` 밝기/완만한 pulse로 표현한다. 외곽으로 늘어나지 않는다.
- 재생성 도구: `tools/build_satiety_mask.gd`(개발용으로만 실행, art bounds 변화 검증 및 lossless resource 압축).
- 원본 2048×683의 닫힌 투명 내부를 추출한 `game/gameplay/satiety_interior.res`를 shader mask로 사용. 내부 bounds는 x352..1780/y353..425. 원본 art와 동일 크기여서 resize에 함께 맞는다.
- 800 notch는 몸통 x80%, 폭4px/높이10px. 현재 값 기반 threshold 상태는 즉시 반영한다.
- 회복 cubic 보간 .22초, 감소 .18초, 지속 drain은 지수 보간(20/s). 실제 상태·숫자는 즉시 반영. pause 때 Song Clock 시각이 같아 보간도 멈춘다.
- PERFECT/GOOD/BAD pulse 강도는 기존 judgment_effects 데이터 사용. MISS는 양의 pulse를 취소한다. 잔물결/약한 shimmer, 1000 초과 cyan-white accent, 300 미만 따뜻한 tone, 150 미만 약4.5초 주기의 작은 pulse. HUD 전체 flash 없음.
- UI는 `game/gameplay/SatietyGauge.tscn` scene-authored. 효과용 material은 게이지당 한 번 생성되며 per-frame 재생성하지 않는다.

## 구간별 drain

- `data/balance/satiety_sections.csv`: `song_id,start_time_sec,end_time_sec,drain_multiplier,section_type,section_id`. full song의 연속 구간, active=1 / low_activity=.5 / break=.2. 기존 musical_sections를 기반으로 intro/outro를 별도 break 처리했다.
- `data/balance/satiety_drain_policy.csv`: `no_food_gap_seconds=3`, `no_food_multiplier_cap=.30`.
- 차트의 예정된 prey Start→Target+late capture(.25초) 구간을 계산한다. 그 사이 비어 있는 예정 구간이 3초 이상이면 전체 해당 구간의 drain을 min(authored,.30)으로 제한한다. 실제 CurrentPrey/MISS/먹은 여부와 무관하므로 조기 Bite로 보호를 만들 수 없다.
- CSV는 곡 로드시 한 번 파싱. SatietyDrainProfile이 authored+safety 경계를 컴파일하고 prefix 적분과 binary search로 Song Clock interval을 정확히 차감한다. long frame, 구간 경계, 최종 소수초, depletion 시각도 적분한다. Timer 없음.
- actual AudioStream 길이를 적용할 때 메모리의 profile을 rebase한다. 음악이 끝나기 전에는 Result를 열지 않는다. Satiety<=0이면 GameOver 우선. Tutorial은 drain 보호를 유지한다.
- 네 chart CSV에 prey를 추가/삭제하지 않았다. 음악을 위한 기존 쉼 구간을 유지했다.

## 전곡 경제 계산

시작500 / 상한1100 / clear800 / 회복150·80·40·0. 아래 AP/혼합 값은 각 target에서 회복·1100 cap·시간 drain을 순차 적용한 실제 시뮬레이션이다. 단순 총합은 cap 손실을 무시하므로 최종값으로 쓰지 않는다. 혼합은 약20%P/60%G/15%B/5%M을 정수 배분하고 순서대로 분산한다. MISS 벌점0/5/10도 계산한다.

|곡|길이(s)|prey|ACTIVE(s)|LOW(s)|BREAK(s)|이전→새 base/s|유효 총 drain|AP 회복 총량|AP 최종|혼합 최종|
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
|Hear the Tide|140.435944|43|88.595|0.000|51.841|23.106620 → 32.954641|3220.000|6450|1072.924|950.000|
|Hidden Current|138.500000|32|62.300|64.917|11.283|16.209386 → 25.025110|2220.000|4800|1068.487|900.000|
|Predator's Pulse|188.342850|49|103.500|62.249|22.594|13.000000 → 28.563482|3750.000|7350|1048.949|850.000|
|Deep Current|131.683258|46|94.608|13.396|23.679|26.920658 → 34.114149|3550.000|6900|1054.800|820.000|

base rate가 커졌지만 모든 초에 그 값으로 drain하지 않는다. 유효시간(Σ구간길이×multiplier)에 적용하도록 재계산했으며 이전 rate와 단순 비교하면 오해가 생긴다.

|곡|최장 catch-target 간격(s)|최장 예정 음식 없는 구간(s)|해당 gap 유효 배율|AP 중 2 MISS의 최악 최종|
|---|---:|---:|---:|---:|
|Hear the Tide|12.023|10.573|0.2 (safety cap .30)|911.909|
|Hidden Current|8.940|7.018|0.3 (safety cap .30)|953.121|
|Predator's Pulse|16.128|14.282|0.2 (safety cap .30)|905.349|
|Deep Current|7.767|6.604|0.2 (safety cap .30)|886.083|

catch 간격에는 아직 예정된 prey가 보이는 시간도 포함된다. 안전 fallback은 그 시간을 보호하지 않고 예정 encounter가 실제 없는 시간만 검사한다. 모든 3초 이상 gap 및 정확한 구간/혼합 배분/벌점/cap 손실은 `artifacts/satiety-sections/satiety-report.json`에 기록했다.

## 판정·연타 방지

- `data/balance/judgment.csv`: PERFECT≤70ms, GOOD≤140ms, BAD≤200ms, 그 밖 MISS. abs(error)의 nested inclusive 대칭 판정. Tutorial도 동일한 설정이며 별도 쉬운 profile 없음.
- ±250ms input capture는 유지. 첫 input이 target을 소비하고 나중 입력으로 고칠 수 없다. empty Bite=MISS와 MissStreak 유지.
- 0,±60,±70,±90,±130,±140,±170,±195,±200,±210ms를 통제 검사한다.

## 검증

- C# build: 경고0/오류0.
- 구간/HUD/대칭 판정: 147 checks 통과. 9개 값(0/250/500/799/800/999/1000/1050/1100) 실제 렌더 및 mask/숫자/threshold/overfill 검증. `artifacts/satiety-sections/gauge-gallery.png` 직접 확인.
- frame-independent 적분, pause/backwards time, 정확한 starvation 시각, authored 누락 safety, 조기 MISS의 보호 악용 방지 통과.
- stage-completion 231 / rhythm 75 / anti-mash 20 / tutorial-state 30 / tutorial-ux 32 / flow 21 / catch-audio 54 / polish 232 checks 통과(정확한 결과 요약은 regression-results.json). 자동 GUI 실행 중 tutorial focus loss로 일시정지된 검사 두 개는 headless+CoreAudio로 재실행했다. runtime focus pause 기능은 유지한다. 연타 fixture는 ±200ms에서 첫 입력이 BAD인 경우도 허용하되 첫 결과 고정/PERFECT 재획득 금지를 검사하도록 갱신했다.

- 실제 main scene→Lobby→Gameplay에서 4곡을 seek 없이 전곡 재생: 73 checks 통과. 모두 Result 1회/오디오 underrun 0. 실제 catch의 프레임 오차 때문에 계산값과 최종값이 소폭 다르다.

|곡|실제 AP 최종|최대 music/clock 차이(ms)|
|---|---:|---:|
|song_1|1073.171|19.75|
|song_2|1068.508|18.94|
|song_3|1049.015|17.94|
|song_4|1054.993|18.13|

- 전체 915 regression checks 통과. 결과: `artifacts/satiety-sections/regression-results.json`, 실제 전곡: `full-song-results.json`/`full-songs.log`. GameOver가 Result로 잘못 라우팅되지 않는 강제 고갈 검사는 stage-completion/flow에서 통과했다.

## 남은 사항

- 자동 시뮬레이션은 사람의 리듬 읽기/음악 청취 난이도를 대신하지 않는다. Stage 3/EX의 실제 초심자 플레이와 dense catch 중 HUD 가독성은 사람의 playtest가 필요하다.
- 혼합 최종값은 입력 순서에 의존한다. 실패 집중 구간/상한으로 버려지는 회복량은 JSON 시나리오를 참고해야 하며 모든 20/60/15/5 순열의 clear를 보장하지 않는다.
- Google Sheet 원격 값은 수정하지 않았다. 런타임 기준은 로컬 CSV이며 Sheet 재-export 시 새 drain/judgment/section 값을 유지해야 한다.
