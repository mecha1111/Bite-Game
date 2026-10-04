# GameOver history audit / graphic-only interference

## GameOver — exact restoration blocked

No GameOver visual was replaced in this pass. The user rejected the current screen; its composition is not treated as the approved design. There is no Git repository/revision history in the current project.

All scene candidates found:

| Candidate | Evidence / content | Runtime connection |
|---|---|---|
| `game/gameplay/GameOverScreen.tscn` (task-entry state) | 64px heading, 720px-wide VBox, upper/lower rules, hidden shark gauge artwork, 0%, starvation shader | `Gameplay.tscn` ext_resource `GameOver` → `GameOverLayer`; only active scene path |
| `/private/tmp/scene-ui-editor-verification/game/gameplay/GameOverScreen.tscn` | 80px heading, 960px-wide VBox, no rules, old long SharkResultGauge shown; older Tween heading→gauge→actions | Temporary editor test copy, not a current game route |
| `GameOverScreen.tscn` (external save detected during final verification) | Editor-serialized UID/resources; same 64px/rules layout, but hidden-art override removed and local fill material added | Same active path, preserved without overwriting; intended status unconfirmed |

No separate `GameOver`, `Starvation`, `FailScreen`, old/backup/legacy/prototype GameOver scene was found in the project or available BITE temporary backup folders. The editor copy's scene/script source is preserved as non-runtime `.txt` files in `artifacts/gameover-bubble-fix/candidate-editor-*`.

Screenshots `artifacts/underwater-retune/bite-subtle-game-over.png`, `artifacts/gameplay-polish/bite-polish-game-over.png`, `artifacts/cleanup/screenshots/bite-polish-game-over.png` show the same rules/0% composition, with starvation world/skeleton differences. The recent `artifacts/presentation-update/gameover-*.png` are overlay-only UI fixtures and do not prove actual starvation composition or approval. `/private/tmp/bite_subtle_water.py` records a Tween sequence change, but does not identify the last accepted visual revision.

The temporary copy's date proves only an earlier file, not the **last correct player-facing version**. No reliable rollback-selection history is available; we cannot establish why a prior rollback chose the current version. The immediately preceding presentation work kept the existing GameOver scene path rather than switching to a different scene. Choosing the temporary copy now would be another unverified substitution.

**Restored scene by this agent: none. Visual nodes removed by this agent: none.** Required evidence: the intended old screen/reference description or exact approved backup path, or confirmation that the externally saved version is intended. GameOver C# script, starvation routing and shared retry/select controls remain hash-identical to this task's baseline (`preserved-logic.json`). During final verification the scene file changed externally; this was detected by the hash check, recorded in `external-scene-save.json` and copied to `candidate-external-save-GameOverScreen.tscn.txt`. It was not reverted. Save, audio settings and focus styling are untouched.

## Interference — previous pass (superseded)

The 384px sizing and top-center Float placement below are historical. Current normalized sizing, top-left slots, debug unlock and reset are documented in `interference-progress-corrections.md`.

### Previous changes

- Removed all four `Name` Label nodes and their theme dependency. No replacement text, caption, subtitle or dynamically created Label.
- Final authored size **190×190 → 384×384**, 2.021×. Existing 128px animation frames use **3× integer scale**; still artwork fits the same square with preserved aspect ratio and nearest filtering. Original PNGs/import scale are untouched.
- Static pop remains relative **.35 → 1.12 → 1.00** over **.14 + .10 = .24s**. Exit retains **.18s** shrink/fade. Animation frame selection follows existing presentation time.
- Fixed `CanvasLayer` and scene-authored anchored `SafeZones` separate indicators from WORLD movement. Layout only recalculates on initialization/re-entry/viewport changes.
- Placement tests the full **1.12 overshoot envelope**, critical HUD and tutorial message rectangles, then other indicators. Candidates derive from authored zone/HUD boundaries, not per-resolution coordinates. Horn and Float have distinct zones; Float prefers the upper-right part of its zone and moves below the reserved HUD when necessary. No screen-space room means hide safely, never cover a critical HUD.
- Float and Horn emissions re-pop the same existing indicator. Whale/Vortex stay for the active event; Vortex remains for all **5 seconds**. All Controls use `mouse_filter=Ignore`, including invisible safe-zone Controls.

| Indicator | Authored safe-zone anchors (left,top,right,bottom) | Final 1920×1080 position (top-left) | Final size |
|---|---|---|---|
| Whale | RightSafe (.75,.44,1,1) | (1488,586) | 384×384 |
| Fishing Float | TopCenterSafe (.30,.10,.80,.82) | (1049,462) | 384×384 |
| Ship Horn | TopLeftSafe (0,0,.25,.46) | (48,56) | 384×384 |
| Sardine/Vortex | LeftSafe (0,.48,.25,1) | (48,607) | 384×384 |

Float cannot retain its former near-top-right position at doubled size without covering Satiety/Settings/Combo/judgment. Its anchored top zone therefore includes free space below those HUD reservations. These table coordinates are measured results, not hardcoded resolution-specific values.

## Actual verification

- Clean C# build, 0 warnings/errors.
- Rendered presentation checks **75**: graphic-only, 384px/3× frames, input ignore, four independent event captures, emission reuse/no duplicates, Vortex active duration/exit, 5 resolutions (1920×1080,1600×900,1366×768,1280×720,2560×1440), HUD safety and modal suppression.
- Existing **WaterSkeleton 23**: force actual Satiety=0, stop song/input, GameOver only (no Result), skeleton transition, Enter retry and mouse Song Select after keyboard focus navigation. This verifies retained logic, **not identification/restoration of the intended old screen**.
- Existing **Settings context 33** pass. No audio/save/default-focus rollback.
- Captures/logs: `artifacts/gameover-bubble-fix/`. GameOver restoration remains explicitly pending the missing reference.
