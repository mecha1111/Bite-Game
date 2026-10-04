> 현행 Lobby는 네 카드(일반 3곡 + EX)이며 EX cover는 세 환경 이미지의 세로 crop이다. 이전 검증 기록의 세 환경 수와 카드 수를 구분한다.

# 곡 선택 Lobby

## 씬과 데이터
- `res://game/lobby/Lobby.tscn`: 1920×1080 기준 수평 선택 화면. 기본 카드/화살표/뒤로/선택/설정은 씬에서 편집한다.
- `StageCard.tscn`: 환경 이미지, 별도 곡 제목 Label, 잠금 overlay, 선택 frame. Tool 바인딩으로 에디터에서도 데이터를 미리 본다.
- `NavigationArrow.tscn`: 픽셀 이미지 기반 공통 화살표. hover/focus 1.1배·방향으로 4px, press 0.97배·방향으로 7px, 0.12초 반응.
- `stages.tres`는 `stage_1.tres`, `stage_2.tres`, `stage_3.tres`를 참조한다. 제공된 산호초/심해/열수구 이미지 원본을 그대로 사용한다.

곡 이름은 해당 StageData의 `DisplayName`을 바꾸면 된다. 현재 이름은 `곡 이름 1/2/3`이며 최종 곡명을 정하지 않았다. 배경이나 씬 Label 텍스트를 수정할 필요가 없다. StageId/SongId/BackgroundTexture/ScenePath/UnlockRequirement/IsAvailable/Difficulty/OptionalDescription도 데이터다. ScenePath와 Difficulty는 미정이다.

현재 CSV 로더는 없으므로 Resource를 사용한다. 향후 CSV 연결 시 동일 필드를 매핑할 수 있지만, 이번 작업에 새 CSV 시스템이나 live Google Sheets 의존성은 추가하지 않았다.

## 표시와 입력
선택 카드 1120×630, 양옆 86% 크기. 선택 이동은 0.34초 position/scale/overlay·채도 tween이다. nearest filtering과 기존 neodgm/BiteTheme를 유지한다. 곡 제목은 54px, navy 10px outline 및 8방향 6px backing을 사용한다.

InputMap: `stage_previous` Left/A, `stage_next` Right/D, `stage_select` Enter/Space, `stage_back` Escape. 끝에서 순환하지 않는다. 화살표 클릭도 같은 Navigate를 호출한다. 잠긴 카드도 옆 카드 클릭으로 중앙에 가져올 수 있으나 시작은 불가능하다. 중앙 카드 클릭과 Enter/Space가 같은 활성화를 수행한다. 별도 선택 버튼은 없다. 상단은 좌측 뒤로 아이콘만 유지하고 우측은 비워 둔다. Lobby의 기어/control/signal/component는 제거했다. ESC/ui_cancel은 뒤로 아이콘과 동일한 Back 함수를 통해 Title로 복귀한다. Settings를 열지 않는다. 카드/화살표/데이터는 유지한다.

일반 Lobby에는 Settings 접근 경로가 없다. Title의 Settings 버튼은 기존 popup을 연다. MenuInputEnabled는 기존 modal 입력 차단용으로 남겨 두며, Lobby SettingsRequested/SettingsButton은 없다.

환경 배경 TextureRect 3개는 StageData.BackgroundTexture에 바인딩되어 연속 0.34초 crossfade한다. 연타 시 현재 alpha/transform에서 다음 목표로 이어진다. TransitionEffects는 픽셀 단위 cyan ripple과 한 번에 6개, 0.9초 bubble burst다. 모든 효과는 mouse_filter=Ignore이며 UI 아래에 둔다. AmbientEffects의 CoralBubbles(4), DeepSpecks(5), VentEmbers(6)는 선택 환경에 따라 emitting을 전환한다. 환경별 빛/입자 값은 씬의 Inspector에서 편집하는 현재 3환경 presentation preset이며 데이터 모델이나 gameplay 규칙은 추가하지 않았다. 공통 UiAudio가 버튼/유효 키보드 이동의 SFX를 처리한다.

해금은 기존 ProgressService가 소유한다. 새 저장 파일은 1번만 해금, 2/3번은 잠김. `UnlockStage(id)`는 향후 완료 처리 연결 지점이며 자동 해금 규칙을 만들지 않았다. 기존 저장된 해금은 그대로 존중한다.

Gameplay는 구현하지 않았다. 현재 선택은 SceneRouter의 기존 준비 중 안내로 연결된다. 뒤로는 Title, 설정은 기존 popup이다.

## 검증
Godot 4.7.2 Mono에서 실제 Startup → Title → Lobby를 실행했다. 3개 환경 표시, 중앙/양옆 강조, DisplayName 변경과 backing 동기화, 실제 마우스 화살표 클릭, hover/focus/press, Left/A·Right/D·Enter/Space·Escape, 끝에서 비순환, 잠긴 곡 선택 차단, Settings popup 입력 분리, 기존 저장 해금 반영을 확인했다. C# 전체 빌드는 오류/경고 0개다. Lobby scene/resource 외부 경로 검사도 통과했다.

에디터에서 Lobby.tscn과 StageCard.tscn을 열어 실행 전 이미지와 데이터 제목 표시, 화살표 방향, 주요 visual control 선택 가능 여부를 확인했다. 1920×1080, 1600×900, 1366×768, 1280×720, 2560×1440 창 크기에서 중앙 카드/양옆 미리보기/버튼 영역을 검증하고 화면을 캡처했다.

검증 중 뒤로 이동 후 viewport 접근 오류와 화살표 instance의 mouse_filter 무시 설정을 발견해 수정 후 전체 실행 검증을 다시 통과했다. 개발 fixture는 임시 별도 저장 파일을 사용해 실제 사용자 저장을 덮어쓰지 않았다. 인간 플레이테스트/최종 곡 데이터/실제 Gameplay 전환은 범위 밖이다.


## Lobby presentation polish 검증
상단 선택/설정 텍스트 그룹을 제거하고 기존 기어 원본을 재사용했다. 0.34초 카드/배경/채도 전환, 6개 단발 버블 및 픽셀 물결, 환경별 sparse ambience를 씬 노드/Inspector 값으로 작성했다.

Godot 4.7.2 Mono 실제 GUI 실행에서 기어·선택 카드·잠긴 옆 카드의 클릭, Enter/Space, 방향키/A/D, Escape, 끝에서 비순환, 팝업 중 마우스/키 입력 차단 및 닫은 뒤 focus 복귀를 확인했다. 전환 중 alpha/position이 연속적이고 빠른 좌우 반전 후 기준 pose로 복구됨을 확인했다. 3환경별 emitter 활성 상태와 5개 해상도를 검증했다. 에디터에서 Lobby/StageCard의 실제 이미지/제목/기어/전환 노드를 확인하고 선택했다. 전체 C# build는 오류/경고 0개, Lobby 외부 리소스 경로도 통과했다.

실제 Gameplay는 미구현이며 활성화는 기존 준비 중 안내다. UI 효과음은 추가하지 않았다. 환경 ambience는 현재 세 entry 순서에 맞춘 씬 프리셋이다. 새 환경을 추가할 때 같은 방식의 배경 layer/ambience를 씬에서 준비해야 한다. 인간 감성 평가와 낮은 사양 하드웨어 FPS 비교는 하지 않았다.

기존 개발 검증의 잠금 assertions도 현재 LockOverlay 상태를 검사하도록 갱신했다. Headless 가상 오디오에서는 PCM/Clock 50ms 검사가 실패했으나, 실제 CoreAudio GUI로 다시 실행해 전체 리듬 기반 75개 검증(보정/오프셋/흐름/해금 포함)이 통과했다. 리듬 backend는 수정하지 않았다.

Lobby BackGroup는 BackButtonGroup.tscn의 아이콘 전용 컨트롤이다. Title 출구 아이콘 PNG를 AtlasTexture로 직접 참조한다. 배경/Label/backing 노드는 제거했다. 좌측 상단 (100,60)에 96×96 hit area, 64×64 아이콘을 유지하며 normal 원본 색, hover/focus 1.08배, press 0.96배를 사용한다. 동작은 기존 TitleRequested이며 포커스 후 Enter/Space, 클릭, Escape 모두 Title 복귀다. Title의 종료 behavior는 참조하지 않는다.

아이콘 전용 수정 검증: Godot GUI에서 배경/Label 없음, 원본 atlas 참조, 원본 normal tint 복귀, 아이콘 영역 hover 및 keyboard focus 1.08배, press 압축, 아이콘 클릭/포커스 Enter/Escape 후 종료 없이 Title 복귀를 확인했다. 에디터 표시/선택 가능 및 C# build 오류·경고 0개를 확인했다.

최신 navigation 수정 검증: Godot GUI에서 Left/Right, Enter/Space, ESC→Title, 실제 마우스 Back→Title, Back focus+Enter→Title을 확인했다. 삭제된 upper-right 위치 클릭은 아무 동작도 하지 않는다. 기존 gear component/script는 참조 확인 후 제거했고 Title 공용 gear texture는 보존했다. 이전 Settings popup 관련 검증 기록은 수정 전 이력이다.
