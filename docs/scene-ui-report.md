# Scene 기반 UI 전환

플레이어용 정적 UI를 C# runtime 생성에서 .tscn 작성으로 전환했다. 리듬/보정 측정/offset 수식은 변경하지 않았다.

- Title.tscn: 배경, 제목, 설명, menu 및 3개 버튼. TitleScreen은 버튼 연결만 담당.
- SettingsPopup.tscn: shade/panel/음량 3행/offset 2행/재보정 및 닫기 버튼. 단독 scene은 editor에서 표시되며 Startup instance 및 runtime에서만 숨긴다.
- Calibration.tscn: 직접 작성한 Presentation/labels/indicator/각 상태 버튼/장치 안내. CalibrationHud는 Export 참조의 text/visibility만 갱신한다.
- calibration_flow.tscn: 측정 backend만 포함. 외부 Presentation 주입.
- VolumeSliderRow.tscn / RhythmIndicator.tscn: 재사용 scene 구성요소. 링/점 SVG texture도 실제 scene 노드에 연결.
- game/ui/BiteTheme.tres: editor에서도 적용되는 공통 단색 Theme. 사용처 없는 GameUi.cs(.uid) 생성 helper 제거.
- Startup에 공통 Theme와 Settings visibility override 적용. SceneRouter는 scene 전환만 담당하며 화면 anchors를 runtime에 다시 작성하지 않는다.
- SETUP_PROMPT, coding-guidelines, architecture에 필수 scene 작성 규칙 기록.

## 실제 검증
- 전체 C# build: 오류 0, 경고 0.
- 리소스 경로/NodePath/새 scene 연결: Godot 4.7.2에서 로드 및 runtime 확인.
- CoreAudio headless 120 FPS 리듬/보정 회귀 검사 73개 통과.
- 최종 창 실행에서 scene anchors, 음량 3행/실제 audio bus, 입력/화면 offset 분리/재열기, 보정 상태 버튼 visibility 및 건너뛰기 smoke 검사 통과.
- 실제 창 60 FPS에서 보정 UI 진행/누락 박 복구/12표본/재측정/적용/Title 전환 통과. 실제 mouse 및 Space InputEvent 사용.
- 검증용 복사본의 실제 Godot editor에서 Title/Settings/Calibration 각각 열기, static 버튼 존재 및 root 소유권, EditorSelection 선택, Container offset 변경/복원, editor 2D viewport 렌더링을 확인했다. 플레이어 C# scripts는 Tool가 아니므로 Ready를 실행하지 않아도 기본 화면이 표시된다. 3개 editor 캡처를 확인했다. 복사본 editor 로그에 오류/경고 없음.
- 임시 editor 검증 plugin은 /private/tmp/scene-ui-editor-verification에만 있으며 production 프로젝트에 설치하지 않았다. 원본에 이미 열려 있는 editor는 이전 삭제 Lab 탭 cache를 가진 상태라 복사본을 사용했다. 원본 headless editor 로그의 폐기된 탭/HotReload timer 오류는 현재 scene/runtime 오류와 구분했다.

기존 Blueprint/게임 콘텐츠를 새로 추가하지 않았다. Gameplay가 없어 완료 후 Title로 돌아오는 흐름은 유지했다. 실제 인간 청음/gamepad/새 Gameplay 진입은 미검증이다.
