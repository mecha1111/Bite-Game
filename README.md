# BITE

Godot 4.7 C# 리듬 게임 기반 프로젝트. 정상 실행은 Startup → Title → 필요한 Calibration → 최초 Tutorial → Lobby → Gameplay이며 네 곡을 플레이할 수 있다.

## 밸런스 기준

[디자이너 밸런스 Google Sheet](https://docs.google.com/spreadsheets/d/1MOmwmQY77OfrMrbWScq_1iCSykTUbp3_V_yQS7adknc/edit?usp=sharing)가 자주 조정하는 gameplay 값의 기준이다.

- 런타임은 `res://data/balance/`에 포함된 로컬 CSV export를 사용한다.
- Sheet/CSV에 정의한 값은 C# 상수·기본값으로 중복 관리하지 않는다.
- 출시 게임은 Google Sheets, 인터넷, Google 인증에 의존하지 않는다. 다운로드/동기화는 개발 과정에서만 수행한다.
- 로컬 CSV loader가 곡, 차트, 판정, 포만감, SFX 믹스를 읽는다.

상세 규칙: [밸런스 데이터](docs/balance.md). 구조: [architecture](docs/architecture.md). 리듬 계약: [RHYTHM_SYSTEM_RULES](RHYTHM_SYSTEM_RULES.md).

UI 해상도/창 크기: 1920×1080 기준 `canvas_items + keep`. 검증 결과 및 에디터 미리보기 주의점은 [docs/responsive-ui.md](docs/responsive-ui.md)를 참고하세요.

현재 Gameplay: 네 MP3(`assets/MUSIC/`)와 `data/charts/*_musical_chart.csv` 연결. 씬 `game/gameplay/Gameplay.tscn`. Lobby 미리듣기는 별도 Music-bus 플레이어이며 gameplay clock과 분리된다. 편집/CSV/검증: [현재 구조](docs/architecture.md), [밸런스](docs/balance.md), [처리 음원·타깃 검토](docs/processed-audio-target-first.md).

정리·성능·회귀 검증: [cleanup-optimization](docs/cleanup-optimization.md).

스토리 튜토리얼·성공 피드백: [구현·설정·검증 보고](docs/story-tutorial-catch-feedback.md).

튜토리얼 UX/건너뛰기/혈흔 설정(판정은 아래 최신 보고 참조): [구현 보고](docs/tutorial-ux-accessibility.md).

전곡 종료·포만감·시야·음량 기본값: [현재 계산/검증 보고](docs/song-end-satiety-vision-audio.md).

최신 포만감 HUD·구간별 허기·70/140/200ms 판정: [수정 및 계산 보고](docs/satiety-section-hud-accessibility.md).

튜토리얼 방향/세 번째 성공 즉시 완료: [수정·검증 보고](docs/tutorial-facing-mastery.md).

음악 동기화 월드 연출·Bite 물방울·메달 상어: [구현 및 검증 보고](docs/music-synchronized-presentation.md).

- [Result / pixel UI / 5-second current / optional custom maps status](docs/presentation-ui-custom-maps.md)
