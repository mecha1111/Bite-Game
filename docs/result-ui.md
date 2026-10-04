# BITE Final Result / Game Over

## Scene-authored presentation

- `game/gameplay/ResultScreen.tscn`: horizontal hunting statement, not a vertical receipt/modal. ResultPanel → Content(Header + Body). Body has LeftColumn(JudgmentRows + MaxCombo), vertical divider, and a45:55 split with the wider RightColumn(SatietySection + StateSection). Shared Actions is a sibling below the board. Board1320×800, centered at(960,505), width68.75% of1920. Navy overlay52% preserves the frozen selected environment.
- Judgment rows use `assets/PERFECT.png`, `GOOD.png`, `BAD.png`, `MISS.png` directly through AtlasTexture crops. Each TextureRect's visible height48, aspect retained, left edge aligned. Counts are actual right-aligned40px Labels. No second text judgment name.
- General text uses neodgm Labels: title56, song36, normal32, counts40, combo64, percentage72, state52, achievement28, action32. All result text has outline0.
- Result entrance: panel/dim .16s → header .12s → four rows/counts .10s each → combo .08s → satiety .08s reveal +.8s fill → state .12s → actions .12s. Percentage reveals after the fill; state appears in parallel. Total1.96s (world settle .08s before panel). Accept skips reveal; another accept activates focused action. CLEAR has small1.04→1 scale accent.
- Exact board frame: `assets/빈 남청색 픽셀 결과 패널.png`, transparent source padding cropped with AtlasTexture(125,91,1421,760), NinePatch corners64px. No Title-button fallback. Header centered; left performance and right shark summary remain separate. Compact actions are below the board.


## Shark gauge

`ResultSharkGauge.tscn` uses the exact `assets/투명한 게이지 픽셀 상어.png` directly. The old long Gameplay HUD gauge is disconnected from ResultScreen (GameOver's existing separate gauge is unchanged). The right-side shark image is dominant;72px percentage appears below it.

`result_shark_body_fill.gdshader` fills only `result_shark_interior.res`, a39KB compressed Godot ImageTexture. Developer utility `game/debug/build_result_shark_mask.gd` flood-fills transparent pixels reachable from image borders;219805 enclosed transparent pixels become the body mask. Source frame/eye/fins remain untouched. Source PNG is never modified. Left→right fill maps to enclosed-body X range; exterior alpha remains transparent. Rebuild mask when source artwork changes with Godot `--headless --path . --script res://game/debug/build_result_shark_mask.gd`. This preprocessing tool is outside shipped player flow.

Presentation scale is value/10 percent:0/500/800/1000/1100 →0/50/80/100/110%. The silhouette saturates at100%;101–110% adds a subtle brighter cyan tint via overfill_strength while keeping the same silhouette and explicit numeric reserve. Fractional percent uses at most one decimal so799 does not misleadingly round to80%. This changes no internal max/rewards/drain/clear rule. Gauge animation is UI-only while gameplay is frozen.

## Starvation

`GameOverScreen.tscn`: BackgroundDim → Content(Headline/hint, empty SharkGauge, shared Actions). No judgment receipt. Zero satiety before song completion uses a separate starvation path: block input, StopSong, fade lanes/interference .3s, desaturate/depth-tint environment, sink gameplay shark36px and darken .7s. Game Over sequence takes1.0s; overlay56%, heading80, hint32, empty gauge0%. Retry restores base shark transform/color, lanes, environment and existing song state.

## Shared actions and persistence

`ResultActions.tscn`: two260×68 native Buttons, clean32px BITE text. Retry receives initial focus after reveal; Left/Right changes focus without wrap. Enter/Space activates, mouse works, Escape uses the same song-selection callback.

Only completed-song results record achievements in existing ProgressService/SaveStore: `progress/full_combo_songs`, `progress/all_perfect_songs`, keyed by SongId. Best flags are monotonic; AP also stores FC. `HasFullCombo`/`HasAllPerfect` are available to Lobby. Result shows silver/gold shark accent; AP supersedes FC. Lobby already displays the highest saved swimming medal shark; custom ranks use their own namespace. Starvation does not award achievements or auto-unlock stages.

## Verification

Developer-only scene `game/debug/result_presentation_checks.tscn` uses an isolated /private/tmp save. It tests20/7/6/0, maxcombo21,880→88%/CLEAR, below800→FAILED,0/500/800/1000/1100 scales, image resource identity, gauge fill uniform, two-column separation, max combo under rows, buttons below board, percentage under gauge, panel fitting, FC/AP reload, forced starvation, frozen clock/audio/input,36px sink, lane fade, Retry and song-select routing. Actual window sizes:1920×1080,1600×900,1366×768,1280×720,2560×1440. Runtime/editor screenshots are inspected separately. No human fun/playtest claim.

실제 GUI 검증 결과: Result/Game Over47 checks, 기존 Gameplay57 checks 통과. 실제 결과 진입의 성과 저장, Space skip/Retry, Enter, 마우스 native Button 클릭, FC/AP 저장·재로드 포함. Build 경고/오류0.

Godot editor 복사본에서 ResultScreen/GameOverScreen/ResultActions/SharkResultGauge를 실행 전 열고 주요 이미지·Labels·버튼·mask material 선택 및0% preview 확인. 정상 player flow 밖의 검사 씬만 추가했으며 active scene/resource 경로와 sub-resource ID 검사는 모두 통과했다.

최신 전용 자산 검증: GUI presentation88 checks + Result/GameOver47 + Gameplay57 통과. Editor에서 ResultScreen/Gameplay/WavePulse를 실행 전 열어 exact shark texture와 편집 가능한 앵커/원형 shader preview를 확인했다.

종료 진입은 GameplayScreen의 guarded Finish 한 곳만 사용한다. RhythmController의 명시적 SongEndTime/audio length 기반 SongFinished 외에는 Result를 열지 않는다. data load error는 독립 UnavailableLayer. starvation은 GameOver 상태로 고정되어 Result 전환이 불가능하다. 기존 developer fixture의 강제 private Finish 호출을 실제8회 입력/곡 완료 경로로 바꾸었다.

2026-10-04 current inspection: inherited child layout_mode=0 overrides hid Result shark image and misplaced percentage despite numeric tests. Explicit anchored mode=1 restored the actual image; nonzero image/label geometry assertions and five-resolution screenshots now cover it. See `presentation-ui-custom-maps.md`.
