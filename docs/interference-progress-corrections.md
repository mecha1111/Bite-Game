# Interference / progression correction report

## Game Over — exact visual restoration unavailable

**No Game Over visual was substituted.** The current rejected route is `res://game/gameplay/Gameplay.tscn` → `GameOverLayer` → `res://game/gameplay/GameOverScreen.tscn`.

Candidates found:

| Candidate | Distinguishing evidence |
|---|---|
| Current `game/gameplay/GameOverScreen.tscn` | 64px heading, 720px VBox, horizontal rules, shark/0% gauge, starvation shader; current gameplay route |
| Task-entry snapshot from the previous audit | Same route/composition, hidden shark-art override; retained baseline hash |
| `/private/tmp/scene-ui-editor-verification/game/gameplay/GameOverScreen.tscn` + script | Temporary editor fixture, 80px heading, 960px VBox, no rules, older long gauge and reveal sequence |
| External editor-save snapshot `artifacts/gameover-bubble-fix/candidate-external-save-GameOverScreen.tscn.txt` | Current serialized scene; hidden-art override removed, local gauge material added; byte-identical to current scene |

Available starvation screenshots show the rules/0% composition with world/skeleton variations. Recent overlay-only fixtures do not identify a previously approved starvation presentation. No Git repository or additional GameOver/Starvation/FailScreen backup/legacy/prototype scene was found. Nothing establishes which candidate was the **last correct player-facing version**, or why a prior rollback selected the current one. Full evidence is in `gameover-history-bubble-fix.md`.

Restored version: **none**. Removed GameOver visual nodes: **none**. An approved reference/backup is still needed. GameOver script, starvation routing and shared retry/select resources remain hash-identical to the earlier audit; current scene matches the preserved external-save candidate. Actual Satiety=0 still opens GameOver alone, stops gameplay, supports keyboard retry and mouse Song Select. This verifies routing, not restoration of the intended visual.

## One graphic-only interference family

Shared component: `game/gameplay/presentation/InterferenceIndicator.tscn`, script `game/gameplay/InterferenceBubble.cs`. Definitions, cropped PNG atlases, converted GIF SpriteFrames and anchored regions are in `InterferenceIndicators.tscn` and `presentation/indicators/*.tres`. No event-name Labels, captions or subtitles remain. Original image files/import scale are unchanged.

Sizing uses the union of visible artwork across the animation, excluding empty canvas. All supplied sequences have a 121×107 native content region. Shared scene-authored **TargetVisualHeight = 192px** at the 1920×1080 reference viewport. Width is derived from aspect ratio: **217.12px**; integer-aligned Control bounds **218×192px**. Static and animated versions preserve aspect and use nearest filtering. The animation starts only after its configured SpriteFrames is available.

| Type | Previous task-entry Control / animated visible envelope | New graphic envelope at 1920×1080 | Mirror | Final region / typical top-left position |
|---|---|---|---|---|
| Ship Horn | 384×384 / 363×321px | 217.12×192px | false | TopLeft Primary, (45,44) |
| Fishing Float | 384×384 / 363×321px | 217.12×192px | **true** | TopLeft Primary alone (45,44), Secondary with Horn (45,314) |
| Whale | 384×384 / 363×321px | 217.12×192px | false | RightSafe, (1657,741) |
| Sardine/Vortex | 384×384 / 363×321px | 217.12×192px | false | LeftSafe, (45,741) |

The earlier small implementation used a 190px square still presentation (~159px artwork height) but native 128px animated frames (~107px artwork height). A transient prior pass enlarged Controls to 384px/3× animation. This pass replaces that oversize fixed-square approach with the newly requested common ~150–190px visual standard (192px grid-friendly target), about **1.79× the original small animated artwork**, rather than falsely claiming another 2× increase over 384px.

Fishing Float's source tail points upper-right; `FlipH=true` mirrors the full still/animation to the upper-left, matching the Horn's naturally upper-left tail. No duplicate mirrored asset.

`SafeZones/TopLeftInterferenceSafe`: anchors (0,0,.24,.50), with Primary upper half and Secondary lower half. A lone Float or Horn uses Primary; when both are active, Horn retains Primary and Float uses Secondary. Whale uses RightSafe (.76,.55,1,1); Sardine uses LeftSafe (0,.55,.24,1). Positions above are measured reference-layout results, not per-resolution constants. Shared 32px margins reserve the full 1.12× overshoot envelope. Layout recalculates on viewport or active-slot changes; it avoids Satiety, Settings, Combo, judgment, tutorial message and other indicators. If a future layout cannot fit safely, the indicator is suppressed instead of covering critical UI.

- Initial pop: **.35 → 1.12 in .14s → 1.00 in .10s**, center pivot.
- Float/Horn repeat: same instance, **1.00 → 1.08 → 1.00 over .16s**. Initial-pop emissions do not restart the tiny pop.
- End: **.18s** shrink/fade, then hidden. All Controls ignore mouse input.
- Existing interference durations are observed; Vortex remains indicated for the full **5s**. No timing, wave target or gameplay state is changed.
- Fixed CanvasLayer 1; menus/pause/end states suppress indicators. Only one of still/animated art is visible at a time.
- Development-only family gallery is part of `game/debug/presentation_update_checks.tscn`, outside player routing; screenshot `artifacts/presentation-update/indicator-family-gallery.png`. All four final sizes inspected side-by-side.

## T developer unlock

Implementation: `game/startup/SceneRouter.cs`, scene-authored `Startup/DeveloperToast`, and `game/save/ProgressService.cs`.

Both input handler and unlock service guard **OS.IsDebugBuild()**. Physical T is accepted only in Title/Lobby (including Custom category), while Settings is closed. It is ignored in Calibration, Tutorial, Gameplay and therefore ResultScreen's gameplay context. Runtime flag **DebugUnlockAllStages** overrides lock checks; no save data, clear result, medal, FC or AP is fabricated. `ProgressService.Changed` immediately calls the existing Lobby.Refresh. Toast lasts **1.7s**.

All four main-stage cards unlock. Custom maps already bypass main progression locks; unavailable custom maps remain unavailable because their required supplied charts are missing. T does not invent playable data. New process starts without the runtime flag. Release export was not produced in this pass; release behavior is protected by the guard, not claimed as an exported-build playtest.

## Settings progression reset

Node: `SettingsPopup/Drawer/Margins/Layout/ScrollContainer/SettingsContent/DataSection/Reset/ResetButton`. New bottom **데이터** section reuses BITE theme/action/focus styles.

Scene-authored `SettingsPopup/ResetConfirmation`: navy Panel → Question, Warning, Actions/Cancel + Confirm. Question `모든 진행도를 초기화할까요?`; warning `스테이지 해금과 클리어 기록이 삭제됩니다.` Default focus **취소**; focus is trapped within the modal. ESC cancels. Keyboard Enter/Space use normal focused Button activation; mouse cancel/confirm verified. No default Godot focus rectangle is introduced.

Single source: `ProgressService.ResetProgress()` → `SaveStore.EraseSection()` → immediate Flush → Changed.

Cleared:

- Entire `progress` section: unlocks, `full_combo_songs`, `all_perfect_songs`, `stage_medal_ranks`, `tutorial_completed` and any other progression keys.
- Entire `custom_map_progress` namespace, including best ranks.
- In-memory unlock/FC/AP/medal caches; reinitialized to only `stage_1` and explicit `tutorial_completed=false`.

Preserved: entire `settings` section and live settings state: Master/Music/SFX, resolution/window mode, Input/Visual Offset, calibration completion and rhythm key binding. Optional custom content need not implement its own reset UI. Missing custom module is harmless.

After confirmation, modal closes, visible cards refresh and `진행도가 초기화되었습니다.` appears for **1.7s**. Settings stays open; Tutorial is not force-opened. Existing next-start routing reads TutorialCompleted=false after any already-completed calibration. A current debug override remains; developer feedback states `[DEBUG] 해금 표시 유지`. Underlying saved progression is fresh, and actual locks return when override is disabled or process restarts.

## Verification

- C# build: **0 warnings, 0 errors**.
- Actual Godot presentation fixture: **96 checks**, all four forced events, same-instance repeat, 5s Vortex/exit, no Labels or input interception; simultaneous/HUD-safe layout at 1920×1080,1600×900,1366×768,1280×720,2560×1440. Tutorial instruction safety additionally tested at all five sizes. Captures visually inspected, including family gallery.
- Progress-controls fixture: **36 checks** with isolated save; actual T/ESC/Tab/Enter and mouse confirmation input, immediate lock refresh, no debug writes, reset scope, all device keys preserved, tutorial state and save reload.
- **Separate new Godot process: 3 checks** confirm runtime override gone, persisted core/custom/tutorial reset, audio/calibration/offsets preserved.
- Retained regressions: anti-mash **20**, Settings context **33**, actual starvation/skeleton/GameOver retry/select **23**, all pass.
- Logs: `artifacts/gameover-bubble-fix/verified/`; retained core regressions in `current-final/`. Earlier failed fixture logs remain as audit evidence; final runs above are clean.

Remaining: exact approved GameOver visual cannot be identified from available history. No replacement was invented. Optional custom songs remain unplayable until the missing authored chart package is supplied. No release export test was performed.
