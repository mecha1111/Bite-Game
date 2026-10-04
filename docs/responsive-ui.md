# Responsive UI

Reference: 1920×1080. `project.godot` uses `canvas_items` + `keep` (formerly `expand`). Fractional scale mode remains the Godot default; nearest filtering remains authored on the pixel UI. No resolution-specific coordinate tables or additional UI scale script exist.

The logical layout remains 1920×1080 by design. Actual window pixels scale with `min(windowWidth/1920, windowHeight/1080)`. Keeping logical coordinates unchanged does not mean the UI is physically fixed-size. In a different aspect ratio, the engine adds letterbox/pillarbox borders rather than stretching or expanding the composition. [Godot 4.7 resolution documentation](https://docs.godotengine.org/en/4.7/tutorials/rendering/multiple_resolutions.html).

## Scene changes

- Title: existing START/SETTING/EXIT Control groups now use center anchors and preserve every authored reference position. Logo hit area uses top-center anchors. Decorative Sprite2D artwork and Logo transforms remain untouched; the existing root Control/canvas scales the entire composition uniformly.
- Lobby: Composition is Full Rect. CardOrigin/arrows use relative center anchors; Back is top-left; the former top-right Settings icon was removed in the latest navigation change. Card positions remain relative to CardOrigin in the existing carousel code. No gameplay/data/routing change.
- StageCard: TitleBand is bottom/full-width anchored; title/backing labels inherit the card width while preserving 1920 reference geometry.
- Calibration: existing central focus column explicitly uses anchor layout; focus panel stretches within that column with unchanged margins. Pulse/meter/actions remain grouped and scale together.
- SettingsPopup: inspected, no content/style changes needed. Root/DimOverlay are Full Rect, drawer uses 700/1920 proportional width, margins/sections are Containers, dropdown/slider/offset controls use Expand+Fill. Existing minimum label widths are design-space values, scaled by the common canvas.

## Actual GUI verification

Godot 4.7.2 Mono in a standalone native game window. Four screens (Title, Settings, Lobby, Calibration) were run/captured at all six sizes:

| Window | Scale | Drawer pixels | Slider pixels |
|---|---:|---:|---:|
|1920×1080|1|700|310|
|1600×900|0.8333|583.3|258.3|
|1366×768|~0.711|497.7|220.4|
|1280×720|0.6667|466.7|206.7|
|2560×1440|1.3333|933.3|413.3|
|1440×900|0.75|525|232.5|

Measured root canvas/screen transforms and physical control rectangles, not just logical viewport size. Actual PNG sizes match output content. The 1440×900 window displays 1440×810 content with 45px top/bottom letterbox margins. 1366×768 may have a 1px rounding border. All relevant controls fit; Settings needs no default scrolling; selected card/title strip and Calibration meter remain within bounds. Settings resolution dropdown was also used to change actual native window sizes. Captures were visually inspected as a full matrix and individual smaller-screen images.

C# build: zero errors/warnings. The editor verification opens all five scenes, checks anchors/reference positions and scene-authored visibility. No rhythm timing code changed. Fractional scaling at non-integer sizes can make pixel widths uneven; nearest filtering prevents blur, while layout/readability take priority over strict integer scaling.

## Editor preview

The Godot Game workspace can resize/composite an embedded preview independently of the requested native game window. For stretch testing, use **Stretch to Fit** or disable embedding and run a standalone game window; otherwise the same fitted preview can mask a resolution change. This is editor preview behavior, not shipped runtime settings. Existing user editor state was not modified. [Godot 4.7 game embedding](https://docs.godotengine.org/en/4.7/tutorials/editor/game_embedding.html).
