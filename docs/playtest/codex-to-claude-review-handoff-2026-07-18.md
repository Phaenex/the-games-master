# Codex → Claude Independent Review Handoff — 2026-07-18

## Honest baseline

**Product grade remains B+. Tooling/verification grade is A. This is not an A+ claim.**

Codex's final Unity scene audit remains:

```text
objects=3968 renderers=2949 colliders=496 lights=9 fingerprint=046d838dfbce58dd
```

The 16-shot Unity tour, 41/41 EditMode tests, 2/2 PlayMode tests, 47/47 browser harness, 23/23
Shut the Box JavaScript tests, and 23/23 C# parity tests were green in the final pass. The visual
assessment is still A- arrival/mansion/coach yard, B+ cemetery, B garden, and B+ audio pending human
ears. Nick has not cleared EV -3 on his display, the modern vehicle, the 4m45 pacing, or wind F8 A/B.

## Browser-gate defect history

Do not erase the red runs from the record:

1. The first final report used seven unchanged passes plus two repaired targeted reruns. That was not
   equivalent to a clean nine-gate invocation.
2. A later uninterrupted run exposed intermittent long-walk input/timing failures. `play-full.mjs`
   was changed to drive the game's actual controller key state while retaining the live RAF and
   collision path. It no longer teleports or clamps the player through the locked gate.
3. A porch hold timed out because arrival mode begins before the slow headless porch glide settles.
   The test now waits for the real settled position before checking the refusal beat.
4. A hostile-load run failed the first movement bound and an Entry Hall screenshot while a separate
   Playwright project saturated SwiftShader. Bounds were calibrated to observed progress;
   screenshots/navigation received explicit limits; failed gates are isolated in descendant-only
   process groups; failure output retains 30 lines.
5. The repaired full walk and handoff then passed back-to-back under that hostile load.

Operational disclosure: while diagnosing the contention, Codex manually sent `TERM` to two headless
Chromium root processes before checking their full ancestry. Their parent workers belonged to a
separate `/Users/damato/Projects/pheme` Playwright job, not this repository. Codex stopped there and
did not touch the parent workers or any further external processes. This was an operator mistake, not
behavior of `run-gates.mjs`; Claude should independently verify that the new process-group cleanup is
strictly descendant-scoped.

No gameplay source was changed for this hardening. Relevant review targets:

- `scripts/play-full.mjs`
- `scripts/verify-breath.mjs`
- `scripts/verify-handoff.mjs`
- `scripts/run-gates.mjs`

Claude should decide whether these are legitimate E2E reliability fixes or whether any bound/test
hook creates false confidence.

## Clean uninterrupted browser baseline

Final command:

```bash
node scripts/run-gates.mjs
```

Final output:

```text
✓ unit+harness tests     PASS 67s
✓ agent playtest         PASS 306s
✓ door sequence          PASS 72s
✓ full walk              PASS 402s
✓ env entrance           PASS 49s
✓ porch breath           PASS 63s
✓ g2 bell+lamp           PASS 72s
✓ hall handoff           PASS 55s
✓ polish (leaves/owl)    PASS 27s

ALL GATES GREEN
```

The full-walk final state was `phase=aftermath`, `errors=0`, with Threshold Refusal door rotations
`{left:0,right:0}`. The separate handoff landed on `The Games Master - Entry Hall.dc.html` with zero
page errors.

## What Claude must do next

Use `docs/CLAUDE-REVIEW-PROMPT.md`. Review first and write the audit before editing. Rerun every Unity
and browser command independently, inspect all sixteen Unity shots at full resolution, and return a
ranked defect queue. Green automation is only a floor. Claude must not award A+ while any visual zone
is below A or while Nick's audio/pacing/display gates remain human-unverified.

No purchase, commit, push, second mansion, or open-door Threshold Refusal was made.
