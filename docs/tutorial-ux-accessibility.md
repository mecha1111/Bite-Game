# 튜토리얼 UX · 판정 접근성 개선

> 이전 검증 기록입니다. 최신 판정(70/140/200ms), 구간별 drain 및 포만감 HUD 수치는 [현재 수정 보고](satiety-section-hud-accessibility.md)를 따릅니다.


후속 시야/포만감/음량 변경은 [song-end-satiety-vision-audio.md](song-end-satiety-vision-audio.md)를 따른다. 아래 UX·판정·음악 정책은 유지한다.

현재 실행 장면은 `res://game/tutorial/TutorialGameplay.tscn`이다. 일반 Gameplay의 Song Clock, 입력 소유권, WavePulse, 상어 애니메이션, 설정/보정, 스토리와 GOOD+ 숙련도 정책을 재사용한다.

## 요청 항목별 보고

| # | 항목 | 현재 구현 |
|---|---|---|
| 1 | 음악 반복/끊김 원인 | 이전 `TutorialGameplayScreen._Process/Finish → ScheduleRestart → GameplayScreen.Restart`가 각 단계/실패 구간마다 `Rhythm.StopSong/StartSong`을 호출했다. MP3 `Play(startSeconds)`가 8/15/22초 등 비마디 체크포인트에서 재개되었으며 presenter/SFX/CSV도 재초기화됐다. 곡 끝에는 구간 종료와 SongFinished 콜백이 중복 전환을 예약할 수도 있었다. 단독 프로파일에서 재시작 0→2/102.7ms 프레임을 확인해 중복 전환 guard를 추가했다. 음악 중복 플레이어가 원인은 아니었다. |
| 2 | 이전 재시도 | 성공이 모자라면 동일한 짧은 지역 구간으로 즉시 되돌아가고, 다음 수업도 음악을 재시작했다. |
| 3 | 새 재시도 | 실패 즉시 seek하지 않는다. 같은 음악의 다음 **6마디(11.52초)** 연습 블록을 사용한다. 3회 성공이 부족하면 같은 수업, 3회 실패면 다음 블록에 시범을 다시 제공한다. 미래 차트만 설치하고 기존 음악/시계는 유지한다. 새 SFX generator의 출력 지연을 보정해 음악이 계속 재생되는 동안에도 파동 음원 타임스탬프를 맞춘다. |
| 4 | 연속 재생 여부 | 원곡 **101.825초 전체는 연속 재생**한다. 숙련도가 남으면 원곡 끝에서만 0.18초 fade-out → 수업에 맞는 정확한 마디 시작으로 재개 → 0.18초 fade-in. 동일 MusicPlayer, 동일 Song Clock을 사용한다. 실제 MP3를 진입 전에 16bit stereo PCM으로 디코딩해 캐시한다. 원본 파일/곡/BPM/offset은 그대로이며, 디코딩 리소스는 메모리에서만 사용한다. 전체 학습을 반드시 한 곡 길이 안에 끝내지는 않는다. |
| 5 | UI 구조 | `TutorialHud/MessageArea/Content`: 시범/연습 안내 → 짧은 지시 → `● ○ ○`. 하나의 상단 카드, 필드는 주 시각 영역. 건너뛰기는 우하단, 확인 창은 별도 모달. 정적 구조는 `.tscn`에 작성. |
| 6 | 글자 크기 | neodgm **지시 40px / 상태 28px / 숙련도 30px**, 1920×1080 기준. |
| 7 | 배경 스타일 | navy `(.015,.04,.08,.86)`, 좌우 32px/상하 18px padding, 얇은 cyan 하단선. 지시/상태 outline=0. 버튼도 navy/cyan, 계속하기 포커스는 밝은 cyan. |
| 8 | TAP 제거 | `TutorialHud/Tap`와 C# Tap export/표시 로직 삭제. 대체 떠다니는 TAP 없음. |
| 9 | 시범 색 | 실제 진행 파동 **gold `(1,.79,.33)`**, 실제 목표 슬롯 8 파동 **green `(.3,1,.45)`**. gold=관찰, green=물기 목표. 별도 가짜 타이밍 애니메이션 없음. |
| 10 | 연습 색 | 첫 성공 전 주요 파동은 green accent 65%, 목표는 100%. 성공 1회 이후 일반 진행색 + 목표 green 90%, 성공 2회 이후 목표 green 40%. |
| 11 | 안내 제거 | 3회 숙련 후 정상 색. 처음에는 lane opacity 최소 .85(시범 .9), 이후 .7/.55로 줄이고 학습 감각 값으로 복귀. 목표 타임스탬프/반지름/확장 속도는 바꾸지 않는다. |
| 12 | 물고기 경로 | 초기 시력 수업만 표시. 실제 `CueSlots/Cues`의 고정 원점을 시간별로 연결하고 마지막 구간은 실제 `BiteTargetAnchor/TargetSeconds`로 끝난다. 별도 ease/path를 제거했다. 첫 오른쪽 실명 수업의 놓친 물고기 탈출은 유지하고, 이후 wave-only 수업에는 이동 물고기를 그리지 않는다. |
| 13 | Skip 경로 | `TutorialHud/SkipButton`. 모달: `TutorialHud/SkipConfirmation/Panel/Content/Buttons/{ContinueButton,ConfirmSkipButton}`. 기본 포커스 계속하기, ESC 취소, 기존 3-2-1 재개 사용. |
| 14 | Skip 저장 | 확인 시 `[progress] tutorial_completed=true` 저장 → Lobby. Result/Game Over 없음. 해금/FC/AP 보존, Settings 재실행 가능. 재실행의 일반 중도 종료는 완료 키를 새로 쓰지 않는다. |
| 15 | 일반 판정 | `data/balance/judgment.csv`: **PERFECT ±50 / GOOD ±100 / BAD ±150ms**, 나머지 MISS. `abs(error)` 중첩 inclusive 범위. 기존 CSV 열은 초 단위를 유지한다. |
| 16 | 튜토리얼 판정 | 일반과 동일한 **50/100/150ms**. 추가 완화 프로필 없음. 자동 시범은 실제 차트 목표를 사용하며 플레이어 Input Offset을 성공 여부에 적용하지 않는다. 시범의 시각 Bite/피드백은 목표 파동과 같은 Visual Offset을 따른다. 플레이어 판정에는 기존 Input Offset이 적용된다. |
| 17 | anti-mash | 빈 Bite MISS, 첫 입력의 활성 먹이 확정, 이후 입력 복구 불가, 한 먹이 한 시도 보존. BAD는 잡기 피드백만 제공하고 숙련도 +0. 보호 포만감 유지. |
| 18 | 남은 UX 확인 | 처음 보는 사람의 무설명 플레이테스트는 아직 필요하다. 현재 음원의 125 BPM/+70ms는 기존 분석 기반 설정이며 청취 미세 조정 가능. 14수업×3회 성공과 12회 초기 시범을 유지하면 101.8초만으로 모든 수업을 마칠 수 없어 곡 끝 반복이 발생한다. 공격은 실제 학습 진도에 따라 발생하며 원래 40초에 고정되지 않는다. |

## 혈흔/파편

| 판정 | 전체 파편 | 혈흔 | 일반 fish/flesh |
|---|---:|---:|---:|
| PERFECT | 10 | 4 | 6 |
| GOOD | 7 | 3 | 4 |
| BAD | 4 | 1 | 3 |
| MISS | 0 | 0 | 0 |

제공 혈흔 원본과 1–14 파편을 사용한다. WORLD z=3에서 기존 수중 처리와 함께 렌더링하며 HUD로 옮기지 않았다. 적색 밝기 gain은 기존 1.6을 유지했다. 혈흔 첫 조각은 medium을 보장하며 small .45–.70, medium .75–1.00, large 1.10–1.35를 사용한다. 대부분 아래/대각선으로 퍼지고 원형 적색 구름을 만들지 않는다.

혈흔 수명 **.40–.55초**, alpha **1 → .7(.20s) → .4(.35s) → 0**. 일반 파편은 .35–.50초/기존 빠른 fade. 물고기 인식 .12초와 burst 3×.05초는 유지했다. 실제 입을 따라간 뒤 같은 위치에서 burst하고, 효과 종료는 가장 긴 .55초 혈흔까지 기다린다.

## 실행 비용과 검증

패턴/블록 계획은 시작 전에 일반 C# 배열로 캐시하고, 활성 차트만 Godot Resource로 생성한다. 모든 블록을 Godot Resource로 미리 만들던 중간 구현은 대량 finalizer/재실행 오류가 발생해 폐기했다. 현재 반복 재실행 + 강제 GC 검사를 포함한다. MP3의 80.71초 위치 Play 요청은 별도 측정에서 32.5–32.7ms였다. 이를 피하려고 원본을 시작 전에 PCM으로 디코딩한다(44.1kHz에서 약 18MB). SFX/WavePulse/효과 장면은 시작 전 로드하며, SFX mix와 간섭 설정은 모드 내 재사용한다. UI/Gameplay 장면을 수업마다 재생성하지 않는다. 실제 52ms 렌더 프레임에서 64ms SFX 버퍼 underrun을 확인해 튜토리얼만 96ms headroom으로 늘렸다(일반 Gameplay 64ms 유지). 샘플 시간은 그대로이며 음악/입력 시계를 지연시키지 않는다.

검증 로그와 화면은 `artifacts/tutorial-ux/`에 저장한다. 정확한 완료 검사 수와 성능 측정은 `verification-results.json`을 참고한다. 프레임 시간은 이 Mac의 실제 렌더링 측정이며 다른 기기 성능을 보장하는 수치가 아니다.

최종 빌드: 오류 0 / 경고 0. 자동·런타임 회귀 검사 **481개 통과**: 튜토리얼 전체 33, 상태 30, UX 32, 고부하 5, 시각/효과 232, catch/audio 54, 리듬 기반 75, anti-mash 20. 런타임 오류는 없었다. anti-mash 단위 장면의 7개 경고는 음원 없이 의도적으로 실행하는 무음 시계 fixture 안내다.

고부하 실제 렌더링 **2,914프레임**: P50 **8.338ms**, P95 **9.260ms**, P99 **16.379ms**, 최대 **22.157ms**, 수업 전환 최대 **7.520ms**. 자연스러운 곡 끝 재개 1회에서 Play 요청 **0.021ms**, 시계와 음악 차이 최대 **16.373ms**, SFX underrun **0회**. EX1 파동·간섭·수중 shader와 0.3초 간격 PERFECT 효과를 함께 실행했고 wave/debris/audio 노드가 기준 수로 복귀했다.

실제 렌더 화면 `demo.png`, `target.png`, `practice.png`, `skip-confirm.png`에서 단일 안내 카드, 입의 초록 목표 파동과 시범 Bite, 기본 계속하기 포커스를 확인했다. `catch-perfect.png`/`catch-dense.png`에서 적색·살점 혼합 파편의 확산과 빠른 소멸을 확인했다. 무설명 초보자 플레이테스트와 실제 청취 기반 BPM/offset 미세 조정은 남아 있다.
