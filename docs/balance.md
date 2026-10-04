# BITE 밸런스 데이터

## 기준과 배포 데이터

디자이너 기준: [BITE 밸런스 Google Sheet](https://docs.google.com/spreadsheets/d/1MOmwmQY77OfrMrbWScq_1iCSykTUbp3_V_yQS7adknc/edit?usp=sharing). 자주 조정하는 gameplay 값은 이 표에서 관리한다.

실행 기준: `res://data/balance/`의 로컬 CSV exports. 런타임은 포함된 파일만 읽으며 live Sheet 요청·Google 인증·네트워크 동기화를 하지 않는다. 표가 오프라인이거나 접근 불가여도 이미 배포된 게임은 로컬 데이터로 실행되어야 한다.

## 수정 절차

1. Sheet에서 값을 조정한다.
2. 필요한 탭을 UTF-8 CSV로 export해 `data/balance/`에 반영한다. 실제 표의 header/ID/단위를 확인하고 유지한다. 현재 로컬 스키마는 포함된 CSV header와 loader를 따른다.
3. 로컬 CSV의 필수 항목, 중복 ID, 유한 숫자/범위/단위를 검증한 뒤 runtime 설정으로 전달한다.
4. 실행과 export build에서 변경값 및 CSV 포함 여부를 확인한다. Godot export preset이 생기면 비리소스 CSV가 PCK에 포함되도록 `data/balance/*.csv` 필터를 확인한다.

Sheet/CSV 항목에 대응하는 숫자를 C# fallback·default·scene/resource override로 따로 유지하지 않는다. 필수 데이터가 빠졌거나 잘못됐을 때 다른 숫자로 조용히 대체하지 말고 항목/파일을 명시한 오류로 처리한다. 기존 runtime Resource/snapshot은 CSV에서 받은 값을 전달하는 구조로 재사용할 수 있다.

판정/이벤트는 기존 authoritative Song Clock 및 실행 snapshot을 유지한다. CSV 적용을 이유로 시계, signed error, input/visual offset 계약을 바꾸지 않는다. 사용자 저장 offset/음량/해상도 및 UI 표현 설정과 디자이너 gameplay 밸런스는 구분한다.

## 현재 연결 상태

현재 음악은 `assets/MUSIC/1_edited.mp3`, `2-2.mp3`, `3.mp3`, `4.mp3`이며 Gameplay/preview가 같은 song_timing 경로를 직접 읽는다. 교체된 runtime 음원은 정리됐다. 실제 경로 감사는 [musical-chart-review.md](musical-chart-review.md).

- `songs.csv`: 표시명, 길이, 곡별 3초 drain, 차트 경로. BPM/오디오 경로는 중복하지 않는다.
- `song_timing.csv`: BPM, 박자 기준 phase, 실제 음원 경로, preview 구간/음량/crossfade.
- `songs.csv.chart_path`가 곡별 CSV를 선택한다: `data/charts/`의 네 `*_musical_chart.csv`. target을 먼저 작성하고 start = target − 240/BPM을 검증/계산한다. phase를 절대 target에 다시 더하지 않는다. 이전 차트 감사 기록은 개발 자료이며 runtime에서 로드하지 않는다.
- `patterns.csv`: fixed slot indices와 wave kind/count. s2/m3/ex1의 doubled notation은 원문과 `mapping_status`를 함께 보존한다. 현재 8분음표 슬롯 매핑은 provisional이며 ●● 의미 확인 필요.
- `interference.csv`: 사용자 제공 musically aligned 시작값 그대로 유지. 추가 snap 없음.
- `interference_effects.csv`: Ship warning+1초 후6개, Float 3초 간격4개, Whale 2초, Sardine 3초. 독립 fixed-origin WavePulse를 사용한다.
- `judgment.csv`: PERFECT/GOOD/BAD/MISS 70/140/200ms; 회복 배율 1/(8/15)/(4/15)/0 → 실제 보상 150/80/40/0.
- `satiety_sections.csv`: 곡별 연속 구간(start/end/multiplier/type/id). active 1 / low_activity .5 / break .2. `satiety_drain_policy.csv`: 예정된 음식 없는 구간 3초 이상일 때 cap .30. 판정 결과와 무관한 차트 시간표로 보호한다.
- `satiety.csv`: 1100/500/800/150. Song Clock 차분을 구간별 multiplier로 적분해 최종 소수 시간까지 차감.
- `environment_sequence.csv`: EX의 non-sequential 환경 전환. song_id/start_time_sec/environment_song_id/transition_style로 기존 art+water profile을 Song Clock에 맞춰 선택한다. transition_style은 crossfade/pressure/caustic_blend이며 시간·배경·월드 프로필을 함께 보간한다. 커버 collage와 별도이며 초기 시각은 authored prey phrase 경계다.
- `environments.csv`: artwork/WaterTint(ambient_tint)/BackgroundDim/CausticStrength/LightRayStrength/ParticleDensity/Vignette 등.
- `game/gameplay/water_profiles/*.tres`: 굴절 강도·속도, caustic scale, fog, saturation, shark tint. CSV 값은 중복하지 않는다.

Prey 시작 전 이전 target+late capture 뒤인지 검증한다. 겹침은 song/previous/next/overlap seconds를 포함한 오류이며 실행하지 않는다. 입력 캡처 창은 `InputCaptureWindows.tres`의 ±250ms이며 첫 포획 시 타깃을 소비한다. Input/Visual Offset은 독립 저장한다.

Song2 beat phase는 사용자 미제공이므로 첫 시작20.155를 half-time 박자 주기로 나눈 나머지 `0.394701`을 초기 기준으로 사용했다. 실제 beat detection으로 검증된 수치라는 의미는 아니다.

현재 네 차트는 음악 구간별 후보이며 고정 encounter 수 제한은 없다. 상한/시작/clear threshold는 1100/500/800이며, 보상·drain은 실제 전곡과 최종 차트에 맞춰 재계산했다([현재 구간별 계산 보고](satiety-section-hud-accessibility.md)). 음악 강세에 대한 사람의 청취 검증은 아직 필요하다.

PCK export 시 로컬 CSV 포함 필터를 확인해야 한다. Google Sheet 실제 schema/export와의 대조는 아직 미실시.
