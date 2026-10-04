# Musical chart review

**Current status: phrase-curated candidates, listening approval pending.** Encounter quotas are removed. No event count is a quality score. The actual four current MP3 sources and Song Clock remain unchanged. Runtime now selects `data/charts/*_musical_chart.csv` through `songs.csv`; supplied v2 charts are reference material.

## What was authored

The explicit phrase plan selects individual payoff candidates, patterns, sides, deliberate rests and call/response motifs. Band attacks refine an already chosen payoff by at most140ms. It does not pick the next strongest peak until a count is reached. The choices and rationale are in `phrase_plan.json`, `musical_sections.csv` and each event's `optional_notes`. Starts derive from targets by four gameplay pulses. No simultaneous prey is permitted. Interference may overlap.

The section boundaries below are inferred from measured band-energy changes, not a verified musical transcription. Instrument identity (kick vs bass attack, snare vs high percussion), phrase feel and pattern fit require listening. **Manual target moves after human listening: zero on every song.** `authoring_decisions.csv` records the proposed payoff and attack-refined target with `human_listened=false`.

## Audition each target

Open `res://game/debug/ChartAudition.tscn` in Godot and press F6. Choose a song. Space replays the original music from target−2s to target+1s. Left/Right move targets. A/D shift the target±10ms. S deliberately writes that target to the actual CSV, recomputes starts and checks overlap including the current negative input offset. Y records listening approval only after completing the clip and saving edits. There is no correct-answer sound. The screen shows original-music time, target, error, pattern, side, nearest quarter-beat subdivision and section.

Approvals write `data/charts/listening_reviews.csv` with song, target, pattern, side and the actual audio SHA256. Automatic playback does not write approvals. A modified target requires another audition. Regeneration tools refuse to overwrite a chart after listening review records exist. The audition player is independent of Gameplay Song Clock and cannot change judgment timing. Editing is a development/editor workflow, not a shipped PCK feature.

## Per-song review

### Hear the Tide

Audio `res://assets/MUSIC/1_edited.mp3`. Chart `res://data/charts/hear_the_tide_musical_chart.csv`.

Music BPM **100.0**, gameplay pulse **200.0**. Phase **0.577500s** remains editable. Double-time/eighth-note lead-in interpretation. These tempo interpretations have not been re-certified by a listener.

43 prey entries. Pattern distribution: s1 9, s2 7, m1 11, m2 7, m3 4, b1 5.

| Section | Seconds | Musical evidence / authored pacing |
|---|---|---|
| opening_space | 0–1.7 | track opening. rest |
| groove_a | 1.7–37.8 | sustained bass/percussion energy; repeated motif candidates. call-and-response with repeated side |
| low_energy_break | 37.8–77.5 | large measured drop in all bands; sparse periodic bass attacks. long rests with three isolated callbacks |
| groove_return | 77.5–116.2 | abrupt energy return followed by repeated full-band rhythm. active motif reprise and response variation |
| breath | 116.2–123.7 | short measured full-band dip. rest |
| closing_reprise | 123.7–139 | return of full-band groove then decay. learned motifs; sparse final punctuation |
| tail | 139–140.435944 | ending decay. rest |

Payoff examples (attack candidates, **not instrument-confirmed**):

| Target | Pattern / side | Section | Rationale |
|---:|---|---|---|
| 2.983214s | m1 / L | groove_a | straight anticipation; low-band attack candidate; listening pending |
| 7.183668s | m2 / R | groove_a | offbeat response; high-band attack candidate; listening pending |
| 82.178226s | m2 / R | groove_return | offbeat response; high-band attack candidate; listening pending |
| 134.918362s | m3 / R | closing_reprise | broken response; high-band attack candidate; listening pending |

Interference entrances:

- ship_horn **77.982761s**: energy-return entrance after long low-energy break; listening pending
- ship_horn **114.534688s**: last high-energy phrase before short breath; listening pending

Listening priorities: confirm band attacks are the intended kick/snare/bass or melodic payoff; compare every ±2s/1s clip against the no-visual tapping expectation; check the sparse sections for worthwhile omitted notes and whether the chosen lead-in pattern matches the motif. No target is declared final from quantization alone.

### Hidden Current

Audio `res://assets/MUSIC/2-2.mp3`. Chart `res://data/charts/hidden_current_musical_chart.csv`.

Music BPM **143.554688**, gameplay pulse **143.554688**. Phase **0.256000s** remains editable. One gameplay pulse per musical beat candidate. These tempo interpretations have not been re-certified by a listener.

32 prey entries. Pattern distribution: s1 4, s2 3, m1 8, m2 7, m3 4, b1 6.

| Section | Seconds | Musical evidence / authored pacing |
|---|---|---|
| atmospheric_open | 0–28.2 | lower bass level; upper-band movement and gradual rise. three isolated anchors; no continuous filling |
| first_rise | 28.2–59 | percussion energy rises; repeated phrase candidates. paired calls/responses with punctuation |
| lower_bridge | 59–83 | bass falls away and mid/high levels ease. isolated long accents and breathing room |
| main_return | 83–114.5 | clear measured sustained full-band energy increase. active repeated motif and offbeat responses |
| late_punctuation | 114.5–135.5 | lower sustained energy with isolated bass/percussion peaks. four phrase punctuation targets |
| tail | 135.5–138.5 | fade. rest |

Payoff examples (attack candidates, **not instrument-confirmed**):

| Target | Pattern / side | Section | Rationale |
|---:|---|---|---|
| 7.283441s | m1 / L | atmospheric_open | straight anticipation; low-band attack candidate; listening pending |
| 22.912920s | m2 / R | atmospheric_open | offbeat response; high-band attack candidate; listening pending |
| 78.406797s | b1 / L | lower_bridge | isolated phrase weight; low-band attack candidate; listening pending |
| 126.193192s | m2 / L | late_punctuation | offbeat response; high-band attack candidate; listening pending |

Interference entrances:

- fishing_float **28.480266s**: repeating artificial 3-second signal contrasts with first rhythmic rise; listening pending
- sardine **49.268022s**: first active motif cluster; listening pending
- sardine **101.130153s**: strong reprise motif cluster; listening pending

Listening priorities: confirm band attacks are the intended kick/snare/bass or melodic payoff; compare every ±2s/1s clip against the no-visual tapping expectation; check the sparse sections for worthwhile omitted notes and whether the chosen lead-in pattern matches the motif. No target is declared final from quantization alone.

### Predator's Pulse

Audio `res://assets/MUSIC/3.mp3`. Chart `res://data/charts/predators_pulse_musical_chart.csv`.

Music BPM **130.0**, gameplay pulse **130.0**. Phase **0.028846s** remains editable. One gameplay pulse per musical beat candidate. These tempo interpretations have not been re-certified by a listener.

49 prey entries. Pattern distribution: s1 7, s2 7, m1 3, m2 12, m3 11, b1 9.

| Section | Seconds | Musical evidence / authored pacing |
|---|---|---|
| opening_space | 0–13 | low measured opening energy. rest |
| rise | 13–35 | increasing bass/percussion energy. straight anticipation to broken response |
| drive_a | 35–89 | strong sustained percussion energy. compact syncopated call-and-response |
| bridge | 89–111 | measured energy drop then gradual return. three spaced punctuations |
| drive_b | 111–160.5 | full-band sustained return and dense percussion candidates. syncopated reprise with heavier phrase endings |
| outro_hits | 160.5–182 | major full-band fall with isolated remaining attacks. sparse closing weight |
| tail | 182–188.34285 | quiet tail. rest |

Payoff examples (attack candidates, **not instrument-confirmed**):

| Target | Pattern / side | Section | Rationale |
|---:|---|---|---|
| 16.128339s | m1 / L | rise | straight anticipation; low-band attack candidate; listening pending |
| 25.437183s | m2 / R | rise | offbeat response; high-band attack candidate; listening pending |
| 88.194552s | b1 / R | drive_a | isolated phrase weight; low-band attack candidate; listening pending |
| 174.114280s | m2 / R | outro_hits | offbeat response; high-band attack candidate; listening pending |

Interference entrances:

- whale **89.376865s**: bridge entry after sustained first drive; listening pending
- ship_horn **125.649427s**: heavy punctuation inside second drive; listening pending
- fishing_float **143.798180s**: steady fake pulse against late syncopated responses; listening pending
- sardine **152.333781s**: late high-energy rhythm cluster before ending; listening pending

Listening priorities: confirm band attacks are the intended kick/snare/bass or melodic payoff; compare every ±2s/1s clip against the no-visual tapping expectation; check the sparse sections for worthwhile omitted notes and whether the chosen lead-in pattern matches the motif. No target is declared final from quantization alone.

### Deep Current

Audio `res://assets/MUSIC/4.mp3`. Chart `res://data/charts/deep_current_ex_musical_chart.csv`.

Music BPM **112.3471465**, gameplay pulse **224.694293**. Phase **0.066757s** remains editable. Double-time/eighth-note lead-in interpretation. These tempo interpretations have not been re-certified by a listener.

46 prey entries. Pattern distribution: s1 5, s2 6, m1 5, m2 7, m3 8, b1 8, ex1 7.

| Section | Seconds | Musical evidence / authored pacing |
|---|---|---|
| opening_space | 0–12 | rising layered introduction. one isolated callback |
| callback_a | 12–36 | full-band groove arrives; shallow-language callback. straight call then quick response |
| callback_b | 36–60 | continuing rhythmic layer with upper-band phrases. learned syncopation and phrase weight |
| pivot | 60–68 | reduced and irregular energy around section change. two measured punctuation targets |
| pressure_drive | 68–92.3 | strong rhythmic return; callback variations. shorter spacing and EX responses |
| breath | 92.3–104 | major full-band dip; lower bass texture. one sparse learned callback |
| final_remix | 104–127.5 | rhythmic return ending in large upper-band punctuation. EX responses to familiar calls; no continuous ex1 cycling |
| tail | 127.5–131.683258 | rapid ending decay. rest |

Payoff examples (attack candidates, **not instrument-confirmed**):

| Target | Pattern / side | Section | Rationale |
|---:|---|---|---|
| 7.672557s | b1 / L | opening_space | isolated phrase weight; low-band attack candidate; listening pending |
| 15.025845s | s1 / L | callback_a | quick percussion motif; high-band attack candidate; listening pending |
| 69.028112s | m2 / L | pressure_drive | offbeat response; high-band attack candidate; listening pending |
| 123.384575s | ex1 / R | final_remix | final-stage subdivision callback; high-band attack candidate; listening pending |

Interference entrances:

- ship_horn **29.193645s**: heavy end of first learned callback phrase; listening pending
- sardine **52.795006s**: active callback cluster; listening pending
- fishing_float **69.023124s**: pressure-drive return; false 3-second metronome; listening pending
- whale **92.145573s**: large full-band drop into breath; listening pending
- ship_horn **106.527885s**: first EX answer of final reprise; listening pending
- sardine **114.908838s**: dense familiar-to-EX exchange; listening pending
- fishing_float **117.024031s**: late fake metronome under final exam responses; listening pending

Listening priorities: confirm band attacks are the intended kick/snare/bass or melodic payoff; compare every ±2s/1s clip against the no-visual tapping expectation; check the sparse sections for worthwhile omitted notes and whether the chosen lead-in pattern matches the motif. No target is declared final from quantization alone.

## Validation and balance consequences

All170 candidate encounters have exact target−four-pulse starts, section metadata, actual-audio bounds and no overlap including the unchanged250ms late deadline. Minimum release (target→next start):0.585941 /0.792562 /0.418698 /0.687891s. Very negative input offsets can still invalidate a chart (approximately beyond−336 /−543 /−169 /−438ms respectively). Loader errors are explicit; neither timing windows nor old-chart fallbacks are used to conceal overlap.

Fishing Float now emits **four Large waves at0/3/6/9s**, with the same four scheduled artificial-ping audio emissions. Horn retains warning +1s +six independent rings. Whale remains2s, Sardine3s. Entrances were authored around measured returns, full-band drops and active clusters, then locally attack-refined. These entrance decisions still need the same musical listening approval as prey targets.

Before the gameplay-polish request, all-PERFECT Song2 and EX were below CLEAR800. The subsequent authorized balance pass corrected drain rates without changing musical chart density or adding filler targets. See the latest balance figures below.

## Runtime evidence

Automated full-song playback and target-window tool checks are runtime verification, not musician listening approval. The [normal-main runtime log](../artifacts/musical-charts/full-runtime.log) passed73 checks: all four tracks were played unskipped through the real menu route; actual encoded MP3 bytes and every chart target matched current source files. Each target resolved once, each Result appeared once at actual audio end, and all wave/interference cues had zero buffer underruns. Maximum audio/clock drift:17.59 /18.55 /17.16 /20.95ms. All170 controlled target attempts were PERFECT in the final run. Controlled events were isolated from external input only in the automated fixture, never in production Gameplay. All final musical-feel requirements remain open until the audition records and beginning-to-end human playtest are completed.

Historical result before the gameplay-polish balance correction: Hear the Tide829.6121 (CLEAR), Hidden Current587.0682 (FAILED), Predator’s Pulse935.7303 (CLEAR), EX783.8510 (FAILED), all with no MISS. This is the explicit balance consequence of removing count-driven filling, not a reason to add filler notes.


Latest polish balance: MISS streak extra penalties0 /5 /10. Hidden Current drain per3s is4.5; EX9.5; others unchanged. Exact All-PERFECT final values829.612070 /836.250000 /935.730310 /865.003016, with no clamping losses. No filler targets added. Musical listening approval remains pending; this update does not claim any target was manually moved after hearing it.
