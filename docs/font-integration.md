# BITE 공통 UI 글꼴

- 기존 assets/fonts/neodgm.ttf 재사용. 추가 폰트 복사/외부 라이브러리 없음.
- 기존 game_theme.tres를 game/ui/BiteTheme.tres로 이동; 색상/여백/스타일 유지.
- Theme default_font에서 FontFile 참조, 기본 28px. Label 변형: GameTitle 64, ScreenHeading 44, SecondaryText 22, SmallText 20.
- Startup, Title, SettingsPopup, VolumeSliderRow, Calibration의 Presentation/Center, Lobby, StageCard에 scene-authored Theme 참조. 기타 기존 계층별 크기 override 유지. C# 변경 없음.
- 새 플레이어 UI는 BiteTheme를 scene에서 지정하거나 부모 테마를 상속한다. 개발자 전용 UI에는 적용하지 않는다.

## 실제 확인

- C# build: 오류/경고 0.
- Godot 4.7.2 Mono 실제 GUI 에디터의 격리된 프로젝트 복사본에서 Title/SettingsPopup/Calibration/Lobby/StageCard를 열고 폰트 상속, 선택/편집, 렌더링 확인.
- NeoDunggeunmo FontFile import, 한글/BITE 글리프 포함 및 제목 64px 변형 확인.
- 원본 프로젝트 Startup 실행 후 Title, 설정 4개 탭/SpinBox, Calibration, Lobby의 3개 StageCard를 순회하고 텍스트 Control의 neodgm 상속 확인. 최종 로그에 리소스 참조 오류 없음.
- 스크린샷에서 한글/영문 렌더링 확인. 검증 저장 파일은 /private/tmp에 격리.
- 기존 Title 이미지가 제목/메뉴를 가림. 검증 시에만 이미지 노드를 숨겨 BITE 텍스트 확인, 원본 씬 배치/이미지는 변경하지 않음. 이미지에 포함된 글자는 Theme로 변경할 수 없음.

## 공통 픽셀 테두리

- Label/Button/LineEdit/TabBar/TabContainer: 글자 #F4F8FF, 테두리 #031436, constants/outline_size=5. 작은 글자 SecondaryText=4px, SmallText=3px. 그림자/blur/glow 없음.
- TitleMenuText: 기존 54px 유지, 글자 #F7FBFF, 테두리 #02132F/5px. hover는 내부 글자 색만 변경하고 테두리는 고정한다.
- 실제 Godot 4.7.2 에디터에서 다섯 UI 씬을 열고, 원본 Startup 실행에서 Title/Settings/Calibration/Lobby 한글 렌더링과 테두리 상속/잘림 없음 확인.

## 현재 메뉴 픽셀 스타일

TitleMenuText=54px/outline10px, CalibrationMenuText=32px/outline8px. #001A3D 백킹을 8방향으로 scene-authored하며 각각 ±6px/±4px이다. neodgm import는 AA/subpixel off, oversampling1. 작은 본문용 theme 크기는 유지한다.
