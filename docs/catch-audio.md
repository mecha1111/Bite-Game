# Bite commitment and underwater cues

Player-facing composition: `Gameplay.tscn` instances `GameplayAudio.tscn` (sample-addressed chart cue player and eight feedback voices) and `CatchConfirmation.tscn` (fixed mouth-centered contracting capture seal). No HUD is generated from C#.

## Commitment and consequences

Gameplay becomes bite-interactive when its authoritative audio clock starts advancing. Empty presses before the first prey also produce MISS. Settings, focus pause, resume countdown, terminal screens and calibration do not generate empty-bite penalties. Calibration keeps its existing input-capture API.

An active prey's first press calls `RhythmController.TryCommitPrey(cueId)`, sets Attempted before callbacks and consumes its target regardless of how early the input is. Existing input offset and judgment windows classify the timestamp; no chart/target time changes. Later presses cannot repair that prey. They are empty-water MISS events once it is resolved. Inputs without a prey are also empty MISS. Automatic target timeout remains at the existing late-capture deadline. One hardware press never judges two targets; 50 ms configured duplicate-event debounce is separate from commitment.

`bite_policy.csv`: second consecutive MISS subtracts 10 satiety; third and later subtract 20 each. First MISS has zero extra loss. PERFECT/GOOD/BAD reset the streak. Drain/recovery values are unchanged. Empty whiffs count in final MISS totals and prevent Full Combo/All Perfect, so MISS totals can exceed the authored target count. Penalties clamp at zero and use the existing Game Over path.

## Sound timing and mixing

`gameplay_sfx.csv` defines processed pack paths, pitch, pan, playback mode and bounded lifetime. `data/audio/sfx_mix_recommendations.csv` is the runtime gain source; per-row dB is an additional editable trim. Stereo PCM16 / 48 kHz is preserved. Only successful snap/gulp/thump retain the earlier local family, because the supplied pack contains no successful-catch files. `catch_audio.csv` and `judgment_effects.csv` define grade layering. See [processed audio and targets](processed-audio-target-first.md).

Prey sound events use the exact Cue timestamps already in the chart (plus song offset). No additional sound is emitted for the target diamond. Interference uses its existing CSV delays, intervals, count and scene-authored origins. Each sample is addressed to the music timeline; there are no independent event timers. SFX follows the controller's existing start/pause/focus/resume lifecycle. Visual Offset remains visual-only; Input Offset remains judgment-only. Neither modifies cue audio timestamps.

The sample renderer keeps approximately 64 ms of lookahead. A consumed prey suppresses subsequent queued events; already buffered sound may have a bounded 64 ms tail. On an audio-buffer underrun only the SFX renderer resynchronizes to Song Clock; music, chart, targets and judgments are never restarted. All cues and feedback use SFX bus; the song remains on Music bus. Left/right source bias is at most 30% (Whale 25%), equal-power pan, never full hard-pan.

Small/medium/large all use `wave_note_ping.wav`, pitch 1.08 / 1.00 / 0.92. Float schedules four independent Large visual emissions with its artificial ping at 0/3/6/9 seconds. Horn plays its complete processed warning once per sequence; its six visual rings do not replay the five-second sound six times. Whale/Sardine play one texture, bounded at 2/3 seconds with 120/150 ms fades.

## Catch vs whiff

All successful grades play snap + gulp, fade the prey's existing rings and show a mouth-centered contracting seal plus expanding impact ripple. The seal follows a scene-authored `MouthImpact` marker and 20 editable frame mouth positions in `Shark.tscn`, so it remains inside the animated mouth. Fixed chart origins and `BiteTargetAnchor` never move. PERFECT adds 98 Hz impact, five bubbles, strongest gauge/combo/world accent. GOOD uses two bubbles and normal response. BAD remains a successful catch with quieter snap/gulp, weak mint response and smaller gauge pulse. MISS plays only the dry whiff, no gulp or capture seal, no recovery; penalty loss gets a separate brief coral/dark gauge pulse. The shark reacts immediately to input. No hit-stop pauses Song Clock, song audio or chart timeline.

## Verification

Developer fixture: `res://game/debug/catch_audio_checks.tscn`. Checks pre-prey empty MISS, strict early consumption, repeat tapping, empty MISS totals/penalties, all three catch grades with judgment images hidden, timeout, recorded SFX PCM, pause/countdown/resume and independent buses. Fixture seeks are developer-only; production timing is unchanged. The recorded `/private/tmp/bite-pattern-audio.wav` is for mix and audio-only listening review. Subjective pattern memorability and mix balance still require human listening.

Latest graphical verification: 54 catch/audio checks including 12 Hz mashing, constant-quarter-note attempts, exact grade impact sizes/opacities, recorded processed s1 PCM, pause/countdown/resume and separate buses. Song-by-song unskipped playback results are recorded in `processed-audio-target-first.md`. Subjective memorability, musical target feel and the final mix require human listening.
