# 튜토리얼 물고기 방향 · 세 번째 성공 즉시 완료

## 원인과 수정

1. `assets/fish/catch_burst_frame_0.png`는 **기본 왼쪽 방향**이다. 밝은 배경 확대 렌더로 확인했다(`artifacts/tutorial-mastery/source-facing.png`). 현재 TutorialFish는 Sprite2D이며 animation frame별 변경은 없다.
2. `TutorialGameplayScreen.PresentFish`: `directionX = segmentTarget.X - Fish.GlobalPosition.X`. 양수면 `FlipH=true`(오른쪽), 음수면 `false`(왼쪽), 거의 0이면 현재 방향 유지. spawn side를 방향 판단에 사용하지 않는다. 도주 경로에도 동일하게 적용한다. early sighted fish만 수정했고 normal Gameplay에 fish를 추가하지 않았다.
3. 기존에는 성공3 이후에도 `AdvancePracticeBlock`의 **6마디 checkpoint 종료**까지 기다렸다. 그 동안 남은 chart prey가 입력/auto-MISS를 만들 수 있었다.
4. `Profile.RequiredSuccesses=3` 유지. PERFECT/GOOD만 +1, BAD/MISS는 +0, 시범은 제외, 연속 성공 필요 없음.
5. `JudgmentObserved`에서 세 번째 성공을 받은 **같은 callback**에 `LessonState=Mastered`와 `MasteredAtSeconds`를 기록한다. 이후 input 비활성, ●●● 강조/짧은 잘했어요 메시지. 상태는 Demonstrating→Practicing→Mastered→Transitioning→다음 레슨으로 이동한다.
6. `SkipFuturePracticeEncounters`는 아직 Start가 오지 않은 prey만 취소한다. gameplay spawn cursor와 direction-effect cursor를 종료하고, `Rhythm.SkipPracticePrey`는 cue/target을 processed + `SkippedByMastery`로 표시한다. judgment callback 없이 input cursor도 넘긴다. `SignalPresenter.SkipEncounter`는 target-wave 예외도 포함해 취소한다. audio `ConsumePrey`가 남은 prey SFX를 막고, fish presenter도 skipped ID를 제외한다. 현재 성공 catch는 reset/free하지 않는다.
7. 최소 **0.85초**의 성공 presentation을 보장한 뒤, `beatOffset + ceil((masteryTime+.85-beatOffset)/bar)*bar`, `bar=240/BPM`으로 다음 마디를 고른다. 현재 125 BPM/.07초 기준이며 chart target, 음악/offset을 변경하지 않는다. 기존 6마디 block 끝을 기다리지 않는다.
8. 다음 레슨은 현재 음악 timeline의 선택된 마디부터 future chart plan을 만든다(기존 pattern/template 재사용, CSV 재파싱 없음). 충분한 phrase가 들어갈 checkpoint end를 선택하고 `InstallPracticeData/InstallFutureChart`로 교체한다. 현재 Bite .36초, judgment .85초, burst/debris ≤.67초를 끝낸 뒤 교체한다.
9. skip에는 `JudgmentResolved`가 발생하지 않는다. Satiety/Combo/MissStreak/통계/학습 성공 수를 변경하지 않는다. 악의적인 추가 input도 Mastered 상태에서 막는다.
10. mastery 경로는 음악 Play/Stop/Seek/Restart를 호출하지 않는다. current song/clock을 그대로 사용한다. **자연 EOF에 아직 레슨이 남은 경우만** 기존 fade+checkpoint 재생 정책을 유지하며 완료된 레슨은 다시 연습시키지 않는다. EOF가 성공 효과 중에 오면 Song Clock은 EOF에 그대로 고정하고 짧은 presentation만 끝낸 뒤 전환한다.

## 검증

- 실제 source 확대 렌더와 양쪽 방향 fixture 확인: LEFT→RIGHT는 FlipH=true, RIGHT→LEFT는 false. fish는 각 chart TargetTime에 실제 mouth anchor에 도착한다. `artifacts/tutorial-mastery/fish-directions.png`.
- `game/debug/tutorial_mastery_checks.tscn`: 실제 연속 음악 위에 6회 practice 구성. GOOD→MISS→PERFECT→GOOD 결과 후 3회 성공, 즉시 Mastered, attempts5/6 skip. 미래 timeout을 진행해도 callback/stat/MISS/streak/Satiety 변동 없음. 파동도 생성되지 않음. 현재 Bite/burst/judgment 보존, 다음 마디 transition, 음악 재시작0회 검사: **22 checks 통과**.
- C# build: 경고0/오류0. 기존 state 30 / UX 32 / vision 18 / anti-mash 20 / stage completion 231 / flow 21 checks 통과. 전체 14개 레슨 실제 traversal 193.89초/33 checks 통과: 실패 재시범, attack/도주, 최종 완료 저장, Lobby 복귀, Settings replay 및 progress 보존 확인. 로그와 결과는 `artifacts/tutorial-mastery/regression-results.json`.
- initial state/flow timing fixture 각 1회 실패 후 단독 재검증 통과. 일반 gameplay 코드는 바꾸지 않았다. state 입력의 실제 전달 오차를 fixture 로그에 기록해 frame 지연을 확인할 수 있다. controlled judgment 경계는 별도 고정 입력 검사로 보존한다.

## 범위

음원, 레슨 CSV, 세 번 GOOD+ 규칙, 실패 checkpoint와 재시범, 이야기/시야 progression, normal blind Gameplay fish 규칙, Settings/skip/persistence를 유지했다. 새로운 artwork/좌우 별도 asset은 만들지 않았다.
