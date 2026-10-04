# BITE 작업 시작 규칙

현재 파일이 유일한 구현 기준이다. 작업 전에 `project.godot`, `docs/architecture.md`, 실제 씬·CSV·스크립트를 읽는다.

기존 Godot 4.7.2 C# 구현을 유지하고 요청 범위만 최소 수정한다. 기본 UI는 `.tscn`에 작성한다. Song Clock, Input/Visual Offset, 5박 Bite, 한 먹이 한 시도, 빈 Bite MISS, anti-mash, 포만감, 간섭, Calibration/Settings/Result/Game Over를 보존한다.

현재 실행은 Startup → Title → 최초 Calibration(필요한 경우) → Lobby → Gameplay → Game Over 또는 Result다. MP3/차트 경로는 `song_timing.csv` / `songs.csv`를 따른다. 누락 파일에 이전 소스를 대체 사용하지 않는다.

변경 후 `dotnet build --no-restore`와 관련 `game/debug/` 회귀 씬을 실행한다. 전체 검증은 `tools/run_regressions.py --godot <Godot Mono executable> --full-songs`다. 개발 파일 정리는 `tools/audit_project.py`로 참조를 확인한 후 수행한다. 자동 참조 분석만으로 파일을 삭제하지 않는다.
