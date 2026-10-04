# BITE 싱크 조정 UI

`game/calibration/Calibration.tscn`의 Presentation/MainLayout에 제목 → 픽셀 링 → 입력 안내 → 타이밍 레일 → 진행/결과 → 청록 버튼 순으로 기본 UI를 직접 작성한다. 1920×1080 기준이며 기존 canvas_items stretch를 사용한다. C#은 상태/값/애니메이션만 갱신한다.

## 표현

- Title의 title.png, underwater_light.gdshader, bubble.png, 청록 버튼 배경을 재사용한다. 배경은 어둡게 낮추고 광선 .035/caustic .006, 가장자리 기포 총10개만 표시한다. 효과/장식은 mouse input을 가로채지 않는다.
- RhythmIndicator.tscn: 낮은 해상도 픽셀 링/다이아몬드 TextureRect, nearest filtering. 기존 PresentationBeatPhase로 링 확장/감쇠를 표시하고 수락 입력 시 코어를 잠깐 압축한다. 별도 beat timer는 없다.
- BiteTheme의 neodgm, 기본 글자 #F4F8FF/테두리 #031436·5px를 상속한다. 작은 안내는 4px/3px 변형이다. 제목64px. 버튼은 CalibrationMenuText 32px/outline8px와 ±4px 8방향 #001A3D 픽셀 백킹을 사용한다. 버튼의 한 상태가 배경/글자/그룹을 함께 제어하며 테두리는 고정된다.
- CalibrationButton.tscn은 scene-authored 배경/Caption/HitButton 한 개다. hover1.025, press.98, tween.12초. 기존 Title UI는 변경하지 않는다.

## 타이밍 레일

- 표시 범위 −200~+200ms. 음수는 왼쪽(빠름), 양수는 오른쪽(늦음), 중앙은0. 범위 밖은 가장자리 안쪽으로 제한한다.
- Perfect/Good/Bad 참고 구간은 기존 RhythmJudgmentWindows 설정을 읽는다. 현재 ±70/140/200ms, 1120px rail에서 전체 폭392/784/1120px다. 점수/표본 수락 조건에는 영향을 주지 않는다.
- RhythmCalibration.SampleAccepted는 이미 계산한 signed 오차를 보낸다. UI는 입력 오차를 재계산하지 않는다. 확인 판정은 UI 이벤트보다 먼저 수행한다.
- TimingMarker.tscn 인스턴스로 최대8개 최근 입력을 표시한다. 작은 pop 후 같은 Song Clock 기준3초에 걸쳐 감쇠하며 X 위치는 바꾸지 않는다.
- 결과에서는12개 표본을 작은 희미한 마커로 표시하고 MAD 처리 후 median을 큰 청록 마커로 표시한다. 이 median의 음수를 입력 보정값으로 제안한다. 측정 중 작은 signed ms 안내는 참고용이며 등급을 표시하지 않는다.

## 보존된 흐름

- 120 BPM 클릭/준비4박/유효12개 표본. 중복/범위 밖 입력 제외, 누락 박은 다음 구간에서 추가 수집한다. 중앙값/MAD/수락 범위 및 Song Clock 계산은 유지한다.
- 다시 측정은 표본/마커를 초기화한다. 적용은 입력 offset을 저장하고 기존 controller에 전달하며 화면 offset을 유지한다. 기본값 시작은 두 offset을0으로 초기화한다.
- 화면 offset은 SongTime − VisualOffset의 표현 위상에만 적용한다. 판정 목표/오디오/BPM은 바꾸지 않는다.
- 최초 설정 완료 → Lobby. Settings 재보정 완료 → 요청한 Title/Lobby와 기존 Settings popup. SceneRouter/SettingsService/저장 구조는 변경하지 않는다. Gameplay 재보정은 동일한 일시 정지 씬과 Settings 문맥으로 복귀한다.

## 검증

- Godot4.7.2 C# build 오류/경고0. 실제 GUI 에디터의 동일 프로젝트 복사본에서 실행 전 배경/제목/안내/픽셀 링/레일/영역/버튼 표시와 노드 선택 편집, 폰트/테두리 확인.
- 실제 GUI/자동 InputEvent: −100/−30/0/+30/+100ms 위치, 가장자리 clamp, backend가 수락한 동일 signed 오차, 최대8개/감쇠/고정 X, beat 확장·감쇠, 12개 결과와 median 마커, 재측정 초기화 확인.
- 창/캡처 크기 1920×1080, 1600×900, 1366×768, 1280×720, 2560×1440에서 제목/레일/결과/버튼이 화면 안에 유지됨을 확인했다.
- 최초 진입 → 12입력/적용 → Lobby 및 완료/offset 저장, Settings 재측정/적용 → Lobby+popup, Settings 기본값 → Title+popup 및 두 offset0 확인. 화면 offset110ms를 유지한 재측정도 같은 입력 편향을 계산한다.
- 입력은 자동 생성했다. 사람의 청음/장치 지연 보정 품질, 실제 컨트롤러, 새 Gameplay는 미검증이다. 원본 에디터의 이전 Lab/검증 탭 캐시와 종료 시 GodotTools 오류는 별도 기존 문제이며 이 UI 작업에서 사용자 에디터 상태를 수정하지 않았다.
- 검증 로그/캡처: /private/tmp/bite-sync-ui-check.log, bite-sync-*.png, bite-sync-editor.log. 새 플레이어 Lab/테스트 씬은 추가하지 않았다.

## 현재 집중형 UI / 4박 준비

- 한 focus 영역: 제목/바로 아래 안내 → 준비 숫자와 작은 timing core/확장 링 → 빠름/기준/늦음 레일 → latest signed 오차 → 진행/보정값 → 주/보조 버튼. 입력 힌트는 버튼 아래, 무선 지연 안내는 맨 아래다.
- View 흐름 Idle(Entry) → CountIn → Measuring(AudioSampling) → Result(AudioResult). 기존 enum 값/확인 API를 유지하고 CountIn만 추가했다.
- 준비 클릭은 SongTime 0/.5/1/1.5초의 정확히4박이다. CountIn 중 입력/마커는 무시하고 진행0/12를 낮게 표시한다. 4번째 박 후.5초, SongTime2초에서 재생/시계 재시작 없이 측정으로 전환한다. "이제 눌러주세요"와 "측정 시작"은 실제 측정 시작부터.75초 표시한다. 첫 목표2초의 count-in 중 Early 입력은 수집하지 않는다.
- 다시 측정은 항상 같은 전체 준비4박을 거친다. 기존 누락 표본 추가수집도 같은 track 재생 경로를 사용하며 수집된 표본은 backend에서 유지한다.
- 기존 bubble형 BaseRing은 제거했다. 하이라이트 없는 픽셀 timing_pulse.png 확장 링과20px core만 사용한다. 위상/입력 반응은 같은 controller clock과 기존 VisualOffset을 읽는다.
- 배경 overlay62%, 가장자리 정적 어둠 최대22%, 희미한 광선.012/caustic.002, 기포총4개. 중앙은 반투명 navy pixel panel로 묶는다. 추가 움직이는 효과는 없다.
- 레일 준비 opacity.4 → 측정1. 참고 ±70/140/200ms와±200ms 범위를 사용한다. rail960px에서 전체 구간 폭336/672/960px. 최근 마커5개/1.8초; 결과는 전체 표본+대표 중앙값을 유지한다.
- primary Start/Repeat/Apply는 선명하게, skip은 opacity.5, result Repeat는.6이며 Apply가 먼저 나온다. 두 버튼은 같은 영역에 둔다. Caption/8방향 navy backing의 텍스트를 함께 갱신한다.
- BITE/neodgm AA off 유지. 제목64px/outline10, count54px/outline10, timing labels/버튼32px/outline8, 작은 힌트outline4/3. 결과/offset/Song Clock/math/first-time 및 Settings return context는 보존한다.
- 실제 Godot GUI 검사: 1/2/3/4 숫자, 준비 입력 무시/마커0/진행0, 연속 clock 전환,12개 수집, full4박 재측정, 결과/median/offset 저장, Lobby 및 Settings popup 복귀,5개 요구 해상도 통과. 검증은 자동 InputEvent/화면 캡처이며 사람의 시선 흐름/청음 평가는 아직 없다.
