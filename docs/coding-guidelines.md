# Coding Guidelines

- namespace: Gamejam2.Feature; 깊게 중첩하지 않는다.
- Scene/Resource 파일: snake_case. 요청된 진입 화면은 Startup.tscn, Title.tscn, Calibration.tscn, SettingsPopup.tscn 명칭을 사용한다. 재사용 UI 컴포넌트는 VolumeSliderRow.tscn/RhythmIndicator.tscn처럼 PascalCase를 사용한다. C# 파일/클래스/메서드/public property: PascalCase.
- private field: _camelCase; local: camelCase; constant: PascalCase.
- Node/Resource 상속은 partial class. Inspector authoring Resource는 GlobalClass.
- Export property는 데이터 또는 명시적인 Node reference. nullable Node는 Ready에서 검증하며 Inspector 연결 위치를 오류 메시지에 적는다.
- 중요한 public API에는 XML 문서. 핵심 timing 주석에는 구현 이유를 설명한다. 명백한 코드 번역/TODO 남발 금지.
- 의존성은 Inspector reference 또는 Main에서 전달. sibling 탐색, /root 접근, ServiceLocator 금지.
- C# 내부 결과는 C# event. Inspector 연결이 필요할 때 Godot Signal. 중복 발행 금지. ExitTree에서 구독 해제.
- 단위는 seconds. 이름에 Seconds/Usec 명시. 표시에서만 ms로 변환.
- timing error = judgment time - event time; 음수 Early, 양수 Late.
- UserOffset는 판정 시계에 한 번 더하며 양수는 Late 방향. SongOffset 양수는 이벤트를 뒤로 민다.
- Resource는 재사용 디자인 데이터. index/hit/score 같은 runtime 상태 저장 금지. runtime snapshot 결과도 소비자는 수정하지 않는다.
- 입력은 action 기반 _Input; physics tick을 기다리지 않는다. timeout은 _Process. 별도 song clock을 만들지 않는다.
- 설정/차트는 실행 시작에 snapshot. 편집한 값을 적용하려면 재시작한다.

## 필수 UI 제작 규칙
플레이어용 정적 UI는 .tscn의 실제 노드로 작성한다. C#은 버튼 연결/상태/문자/visibility/값/애니메이션만 제어하며 전체 화면의 정적 Label/Button/Container를 생성하지 않는다. 동적 목록/효과는 예외이나 재사용 PackedScene을 우선한다. 공통 look은 game/ui/BiteTheme.tres로 편집한다. UI 완료 전 실제 에디터에서 씬 표시와 선택/레이아웃 편집 가능 여부를 확인한다.

플레이어 UI 공통 글꼴은 BiteTheme의 assets/fonts/neodgm.ttf다. 기본 28px, Label 변형 GameTitle(64), ScreenHeading(44), SecondaryText(22), SmallText(20)을 재사용한다. 화면별 기존 계층용 크기 override는 유지하며 C#에서 기본 글꼴을 설정하지 않는다. 새 플레이어 Control 씬도 BiteTheme를 지정하거나 부모 테마를 상속한다. 개발자 전용 화면에는 적용하지 않는다.

## 밸런스 값

디자이너 Sheet/CSV에 표현된 gameplay 값은 C# 숫자 상수나 fallback으로 중복하지 않는다. 런타임 데이터는 res://data/balance/의 로컬 CSV를 사용하며 네트워크/Google 인증에 의존하지 않는다. 실제 export schema에 맞춰 기존 설정/실행 snapshot에 전달한다. 현재 연결 상태 및 기준 링크는 [balance.md](balance.md)를 따른다.
