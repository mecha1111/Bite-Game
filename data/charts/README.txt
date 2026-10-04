BITE Integrated Musical Charts v4

Purpose:
- Rhythm chart only. Camera choreography is NOT embedded in these chart files.
- Camera rules should be layered separately after chart timing is finalized.

Current authoring rules:
1. MUSIC-FIRST / TARGET-FIRST.
2. A phrase defaults to whatever the music requires. Multi-target is NOT the default.
3. 2~4 target phrases are used only when distinct musical attacks support them.
4. Every target is a separate wave event:
   - separate wave_event_id
   - separate TargetTime
   - separate judgment/resolution
   One wave NEVER produces multiple Bite judgments.
5. Phrase sides alternate strictly L -> R -> L -> R.
6. Multiple targets inside one phrase stay on the same side.
7. Visual wave rings may overlap.
8. One physical Bite input may resolve only one target.
9. Chart timing is independent from camera choreography.
10. Camera data must be stored separately and must never modify TargetTime.

Columns:
- phrase_id: phrase grouping
- phrase_side: strict alternating phrase side
- phrase_start_time_sec: lead-in start
- pattern_id: base sensory pattern type
- target_index / target_count: target location inside phrase
- target_time_sec: individual Bite target
- target_gap_from_prev_ms: spacing for rapid phrases
- wave_event_id: unique visual wave event for THIS target
- separate_wave_event: always TRUE in this version
- is_multi_target: whether phrase contains multiple targets
- accent_strength / accent_class: musical impact metadata
