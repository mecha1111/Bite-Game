> 현재 Title particle 총량은 10개(3/3/2/2)다. 현재 subtle shader/실행 확인은 [cleanup-optimization](cleanup-optimization.md)을 따른다. 아래 수치 중 이전 작업 검증은 역사 기록이다.

# Title 커스텀 UI

Title.tscn의 수동 배치가 기준이다. Title 배경, Logo, 메뉴 배경/아이콘/Label 9개를 재사용했으며 전역 위치/스케일을 유지했다. 이전 Background/Center/Menu/Heading/Description/Spacer/StartButton/SettingsButton은 제거했다.

- StartGroup: start-button / start-icon / start-text / HitButton
- SettingGroup: setting-button / setting-icon / setting-text / HitButton
- ExitGroup: exit-button / exit-icon / exit-text / HitButton
- 이름: start/setting/exit-botten → *-button, strat-icon → start-icon.
- HitButton은 StyleBoxEmpty를 쓰는 투명 Button이다. 각 그룹의 배경/아이콘 alpha 영역과 Label 범위를 모두 덮는다. Label은 mouse_filter=Ignore, 배경 Sprite2D는 UI 입력을 가로채지 않는다. LogoHitArea는 기존 Logo 범위를 덮는 scene-authored Control이다.
- TitleMenuGroup의 한 Normal/Hover/Pressed 상태가 배경/아이콘의 self_modulate, 글자 내부 색과 그룹 scale을 함께 제어한다. 글자 전체를 tint하지 않아 테두리 색은 유지한다. 정상 색과 원래 scale을 저장해 복원한다.
- Hover 배경 RGB 곱셈: (0.90, 1.25, 1.28), icon/text #DFFFFF. 밝기 gain으로 기존 파란 텍스처를 밝힌다. Pressed 전체 #28AFC5. Hover scale=1.035, Pressed=0.975, tween=0.12초. Inspector에서 조절 가능.
- BiteTheme TitleMenuText: neodgm 54px, 글자 #F7FBFF, outline #001A3D / 10px. 같은 글자의 ±6px 8방향 네이비 백킹 Label을 씬에 두어 각진 픽셀 실루엣을 만든다. outline_size는 font_sizes가 아닌 constants에 지정한다. 정적 스타일은 Theme에 있으며 C#에서 생성하지 않는다.
- nearest texture filtering. Logo: 원래 위치 (960,279.24), scale=(.529,.529). Idle은 기준 좌표에서 ±2.5px/3.2초, scale ±0.5%로 움직인다. Hover는 idle을 감쇠해 1.05배, 위로5px/오른쪽2px, 0.2초; 이탈 시 원본을 기준으로 유영을 재개한다. 이전 frame Position/Scale을 누적하지 않는다.
- TitleScreen은 기존 StartRequested/SettingsRequested 이벤트를 유지한다. SceneRouter/설정/보정/저장은 수정하지 않았다. EXIT는 SceneTree.Quit(). Settings는 기존 overlay다.

## 실제 검증

- C# build 오류/경고 0.
- Godot 4.7.2 GUI 에디터의 격리된 동일 프로젝트 복사본에서 Title을 열어 background/logo/세 버튼 테두리 확인 및 Label 선택 편집 가능 확인.
- 원본 프로젝트의 Main Scene을 실행해 이전 11개 visual transform과 비교: 오차 <.001px. 중복 visual/기본 메뉴 없음, Button 세 개.
- 실제 GUI 마우스 이벤트: 세 그룹 각각 배경/아이콘/텍스트 위치 hover, 전체 색/scale 변화, 하나만 선택, 이탈 시 정상 복원. press scale/color 확인 및 스크린샷.
- Logo hover/exit 4회: hover transform 도달 후 기준 좌표의 ±2.5px idle 범위 복귀.
- 설정 세 구성 영역에서 각각 클릭: 동일 Title 위 기존 SettingsPopup 표시.
- START 아이콘 클릭 → Calibration → Space 12샘플 → 적용 → Lobby. 이후 START 배경/텍스트 클릭 → 저장한 CalibrationCompleted를 사용해 Lobby 직행.
- EXIT 아이콘 클릭으로 process exit code 0 확인. 모든 EXIT 구성 영역이 같은 HitButton에 포함됨을 hover로 확인.
- 검증 저장은 /private/tmp/title-custom-check.cfg로 격리했으며 사용자 저장은 변경하지 않았다. 최종 실행 로그에 오류 없음.
- 자동화 입력/스크린샷 검증이며 사용자 직접 조작 감성 평가는 아직 하지 않았다.

## 텍스트 테두리 검증

- 이전 font_sizes/outline_size 지정은 실제 테두리 상수가 아니었다. constants/outline_size로 수정하고 실제 Godot 에디터/런타임에서 검증했다.
- 세 Title caption은 54px/5px이며 hover/press는 글자 내부 색만 바꾼다. 테두리 색/두께 유지, 이탈 시 테마 색 복귀, 한글 렌더링 및 clipping 없음 확인.
- Settings 네 탭/SpinBox, Calibration, Lobby/StageCard의 neodgm 상속과 3/4/5px 테두리를 확인했다. C# build 오류/경고 0.

## 수중 분위기 효과 (현재)

- 기존 artwork Sprite의 ShaderMaterial 하나에서 수면 광선 3개, 느린 RGB 명암, 약한 caustic 패턴을 처리한다. 추가 fullscreen pass/UV 변형/blur/bloom 없음. 광선은 상단에서 강하고 아래로 감쇠하며 서로 다른 폭/위상으로 흐른다.
- Shader Inspector 값: strength=.028, speed=.22, ray_opacity=.075, ray_speed=.09, ray_drift=.03, ray_width_scale=1, caustic_strength=.014, caustic_speed=.16.
- AmbientEffects는 artwork 위/Logo·메뉴 아래다. Far(좌우8개씩)/Mid(좌우6개씩)는 이 층에 있고, Near(좌우3개씩)는 z_index=5인 전경이지만 x=60/1860 화면 가장자리에 한정된다. 총34개, 중앙 메뉴 위 방출 없음.
- Far: 8px texture, scale .9~1.4, 35~50px/s, lifetime7초, modulate opacity .45.
- Mid: 8px texture, scale1.6~2.4, 50~65px/s, lifetime6초, opacity .65.
- Near: 12px texture, scale1.8~2.8, 65~85px/s, lifetime5.5초, opacity .8. 모든 bubble은 transparent interior와 alpha Gradient fade를 사용하므로 불투명한 흰 공이 아니다.
- lifetime_randomness=.2로 수명은 기본값의80~100%; 별도 emission randomness=.45~.5, spread5~9도, random velocity/scale, bubble_wobble.tres의 ± tangential acceleration으로 소량의 횡방향 흔들림을 만든다.
- bubble.png/bubble_near.png는 단순 픽셀 테두리·하이라이트·어두운 cyan arc다. nearest filtering 유지. 새 물고기/외부 아트 없음.
- 해초/산호는 title.png에 구워져 있고 별도 식생 Sprite가 없어 정적으로 유지한다. 추출/분할/지역 UV 변형/장식 식생 생성은 하지 않았다.
- 배경/Logo/menu의 scene-authored 배치와 이미지, TitleScreen/TitleMenuGroup/라우팅/설정/보정 코드는 이번 ambient 작업에서 변경하지 않았다. optional burst는 없음.

### 실제 검증

- C# build 오류/경고0. 실제 Godot 4.7.2 GUI editor의 동일 프로젝트 복사본에서 기존 위치/scale, outline, 재질, 6개 emitter/34개 particle, 선택/편집 확인.
- Main Scene 실제 GUI 실행: 31초 관찰에서 세 opacity/depth, emit 상태/Curve, background pixel 변화, Logo idle5px 범위 확인. 기존 visual transform 오차 <.001px.
- 실제 mouse event로 배경/아이콘/텍스트 hover, 전체 group scale/color/이탈 복원, 4회 Logo hover/idle 복귀, 기존 Settings overlay, 12tap/apply → Lobby, 이후 Start → Lobby, Exit code0을 확인했다. 관찰 구간은 데스크톱 클릭 간섭을 배제하도록 hit button을 잠시 비활성화하고 이후 복구했다. 검증 저장은 /private/tmp로 격리.
- Ray on/off 렌더 차이를24초 비교: 첫 광선의 가중 중심188.82→220.18→221.77 viewport px, 약32.95px 이동. 광선 shader의 실제 표면 drift 확인.
- M3 Pro/Metal, VSync 해제/60FPS 제한의 독립 Title에서 각8초 비교: 기본60.005FPS/평균process1.357ms, 효과60.005FPS/1.786ms. 프로젝트 VSync/renderer는 변경하지 않았다. 에디터를 함께 실행한 Main flow 관찰 중 FPS sample은43이었으며 이를60FPS로 보고하지 않는다.
- 스크린샷에서 더 큰 가장자리 기포와 중/원경 기포의 대비, 메뉴 읽기, 원래 구도 유지 확인. 자동화/스크린샷 검증이며 사람의 감성 평가/다른 hardware 성능은 미검증이다.
- 원본 headless import 종료 시 기존 GodotTools debugger/timer 오류가 출력된다. Title resource와 실제 실행에는 오류가 없으며 이 editor 종료 경로는 작업 범위 밖으로 유지했다.

## Chunky 픽셀 글자 (현재)

- neodgm은 유지한다. import에서 antialiasing=None, subpixel positioning=Disabled, oversampling=1로 반투명 halo/서브픽셀 smoothing을 끈다.
- Title 세 caption: 54px/10px 테두리 + ±6px 8방향 백킹. Calibration 버튼: CalibrationMenuText 32px/8px + ±4px 백킹. 색은 #001A3D, 글자는 #F7FBFF. 원래 글자 위치/그룹/HitButton/라우팅은 유지한다.
- 백킹은 입력 없는 scene-authored Label이며 그룹 scale만 따른다. hover는 기존 Caption의 내부 색만 바꾸므로 navy silhouette은 고정된다. 글자 변경 시 같은 그룹의 TextBacking0~7 텍스트도 함께 변경한다.
- 실제 Godot 에디터/런타임에서 동일 폰트, 각진 두꺼운 테두리, 한글, clipping 없음, hover 테두리 유지 확인. build 오류/경고0.
