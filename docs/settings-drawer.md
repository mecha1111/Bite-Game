# BITE shared Settings drawer

`res://game/settings/SettingsPopup.tscn` is the shared left-side overlay. Scene files author the complete layout; C# binds existing settings and manages animation/context. No separate gameplay settings scene.

## Current presentation

- Drawer width700/1920 (~36.46%); horizontal padding48, top/bottom28. Header → ScrollContainer with three sections → footer divider/actions.
- Header: actual `설정` Label64px; gameplay song metadata directly underneath,36px muted cyan. No `현재 곡` caption. Song header disappears outside gameplay.
- The header underline is purely decorative. SectionDivider instances explicitly override titles to `화면`, `소리`, `리듬`; reusable audio rows explicitly override `음악`/`효과음`. This fixes leaked `화면`/`전체 음량` defaults.
- Section spacing8, row gap4, row height64, label minimum width192. Control columns expand with drawer. All nine settings rows remain visible without scrolling at1920×1080; ScrollContainer/follow_focus retained.
- neodgm: heading64, song36, categories36, labels/values/actions32. No chunky outlines on settings text. Navy panel, cyan dividers, crisp pixel controls.
- Offsets use48px minus/plus buttons and plain signed millisecond Labels. Existing1ms steps and input±500ms/visual±200ms limits unchanged. No large numeric entry boxes.
- Footer separates gameplay-only `메뉴로 나가기` from `닫기`. Removed ambiguous `Esc - 나가기` hint.
- Open.34s with8px overshoot; close.22s; underlying screen dim56%. No heavy blur. Existing frame/edge sweep preserved.

## Context and navigation

SceneRouter supplies GameplayData.SongName through SetSongTitle; Title/Lobby clear context with null. Gameplay-only exit is visible only in that context. Display/audio/calibration/offset binding and SaveStore persistence stay unchanged.

Gameplay ESC or settings icon opens the drawer and safely pauses Song Clock/audio/chart/input/satiety. Normal close (ESC/button/outside) finishes the slide, then existing3/2/1 countdown runs before synchronized resume. Dropdown Escape first closes its own menu.

`메뉴로 나가기` opens a scene-authored compact confirmation: `현재 플레이를 종료하고 곡 선택 화면으로 돌아갈까요?` with `취소`/`나가기`. Cancel receives initial focus. Drawer focus is temporarily blocked; Tab stays within confirmation, Left/Right selects actions, ESC cancels confirmation without closing/resuming the drawer. Overlay blocks underlying mouse interaction.

Confirm emits LeaveGameplayRequested. SceneRouter stops gameplay and routes to Lobby while suppressing the normal close-to-countdown callback. No resume countdown, application quit, or automatic Title routing. Non-gameplay contexts hide the song header and exit action.

Calibration retains suspended Gameplay and returns to the same paused drawer with its song context. Title calibration return remains unchanged.

## Verification

Developer-only `game/debug/settings_context_checks.tscn`, isolated `/private/tmp/bite-settings-context.cfg`:29 actual GUI checks passed. Covers Title context, gameplay ESC/pause, song-header ancestry/data, correct category/audio labels,1080p no scrolling, no-outline text, live volume/bus binding, compact offset step, confirmation/Cancel/ESC/Tab/Enter, dropdown ESC priority, normal close/countdown/resume, calibration return, and confirmed exit to Lobby without countdown/audio restart.

Build:0 errors/0 warnings. Godot editor copy opened SettingsPopup before runtime, selected header/slider/offset/footer/confirmation nodes and confirmed category overrides. Runtime screenshots inspected at1920×1080. Existing responsive stretch/container foundation retained; this change does not add resolution-specific code.

현재 검증: native Button/OptionButton/Slider focus StyleBox는 공용 StyleBoxEmpty. scene-authored UiFocusFeedback child가 focus/hover brightness와 pressed tint를 표시하며 keyboard focus는 유지한다. Gameplay contextual header·3categories·두 offset·confirmation·정상 countdown/abandon no-countdown 32 checks 통과.

Rhythm includes a scene-authored tutorial replay row (`RhythmSection/Tutorial/ReplayButton`); SceneRouter enters Tutorial and returns to Lobby without changing unlocks/FC/AP. See [tutorial report](story-tutorial-catch-feedback.md).
