# Codex Delta-Review Prompt: Claude's post-review fixes (2026-07-18)

> Paste everything below the line into Codex. This is a **delta review**, not another full-estate
> pass. The estate has now been reviewed end to end twice (Codex's own final pass, then Claude's
> independent adversarial review). What has never been reviewed by anyone but its author is the code
> Claude wrote afterwards. That is the target.

---

You are the independent, adversarial reviewer for a set of changes another AI (Claude) made to the
Wend Hill Unity opening. Claude reviewed your Phase 0 work, graded it B+, then Nick authorized it to
fix only the items that do not depend on his taste. Claude then verified its own fixes and declared
them good.

That last part is the problem, and it is why you are here. Claude was explicitly told to assume the
previous author was biased toward their own work. Claude is now the author. Apply the same rule to it.

Your job is to verify or refute what Claude claims it fixed, and to find what it broke, missed, or
graded too generously.

## Read these first

1. `docs/playtest/claude-phase0-independent-review-2026-07-18.md` (especially **section 10**, the
   addendum listing the fixes). Treat it as claims to test, not as truth.
2. `docs/playtest/codex-to-claude-review-handoff-2026-07-18.md` (your own prior handoff, for context).

## Projects and hard locks

- Unity 6000.5.3f1 HDRP project: `/Users/damato/GamesMaster-Unity` (not a git repo)
- Web/docs/test repository: `/Users/damato/Projects/the-games-master`
- Unity is single-instance. Never run two Unity commands at once. `~/GamesMaster-Unity` is outside the
  default sandbox, so Unity commands need the sandbox disabled.

Locks that still apply:

- No commit. No push. No purchases.
- Exactly one mansion. Threshold Refusal stays closed-door. The three-toll chapel knock-back stays retired.
- **Do not touch the kitchen garden or the cemetery.** Those are the two zones that actually cap the
  grade, and they are deliberately left alone pending Nick's walk, because garden readability is his
  taste call and seven iterations were already burned guessing at it. If you think they need work, say
  so in writing. Do not implement it.
- **Do not seed the one-in-three window-figure roll.** Making every playthrough identical would destroy
  the canon. If you think Claude's approach to tour determinism is wrong, argue it, do not "fix" it.
- Review and report first. Do not edit code until your findings are written down.

## The new baseline

Claude's changes moved the scene fingerprint. Verify this yourself:

```text
before Claude's fixes:  objects=3968 renderers=2949 colliders=496 lights=9 fingerprint=046d838dfbce58dd
after  Claude's fixes:  objects=3968 renderers=2949 colliders=496 lights=9 fingerprint=4707410a08b94635
```

Claude's argument is that the counts staying identical while the hash moved is the correct signature
for "geometry tuned, nothing added or removed." Test that argument. If object counts match but
something unintended moved, the counts would not catch it and neither would Claude's reasoning.

## What Claude changed (six items, all to be verified)

| # | File | Claim |
|---|---|---|
| 1 | `Assets/Editor/GmMansion.cs` | Window-figure silhouette widened (body scale 0.23 to 0.46, head 0.27 to 0.34) and glow emissive raised from `(0.19,0.055,0.008)` to `(0.85,0.30,0.06)` so the figure is perceptible at the shot-15 framing |
| 2 | `Assets/Scripts/GmRareEvents.cs` + `GmShotTour.cs` | New `HideFigureForReview()`, called once by the tour so shots 01-14 are always figure-free and reproducible, while gameplay randomness is untouched |
| 3 | `Assets/Editor/GmEstateQualityAudit.cs` | `ValidateSingleMansion` now also counts prefab instances sourced from `haunted_victorian_house.fbx`, so a renamed second shell cannot pass |
| 4 | `Assets/Tests/PlayMode/GmPrologueRouteTests.cs` | New `SideZonePerimetersContainThePlayer` covering seven previously untested outer bounds. PlayMode goes 2/2 to 3/3 |
| 5 | `Assets/Scripts/GmShotTour.cs` | Chapel waypoint 08 moved from `(26,1.7,30) @ -88/6` to `(19,1.7,24) @ -113/-12` so the chapel reads as a building instead of a flat wall |
| 6 | `scripts/unity-cli.mjs` | GUI tasks retry once on failure, because the windowed tour stalled on Claude's first run and passed on retry |

## Drive it yourself

```bash
cd /Users/damato/Projects/the-games-master
node scripts/unity-cli.mjs rebuild
node scripts/unity-cli.mjs audit
node scripts/unity-cli.mjs test        # expect 41/41 EditMode
node scripts/unity-cli.mjs playtest    # expect 3/3 PlayMode, including the new perimeter test
node scripts/unity-cli.mjs tour        # expect 16/16
```

Run `audit` twice and compare fingerprints. Open all sixteen PNGs at full resolution. A luminance
number is a blank-frame guard, never a visual grade.

## Attack these specifically

Claude flagged most of these against itself. Do not let that soften them, and do not assume a
self-flagged weakness was therefore handled.

1. **Is the figure now too visible?** Claude set the glow so that pane is the brightest thing on the
   facade, then judged the result "perceptible but deniable" by looking at its own screenshot. That is
   a taste call made unilaterally by the author. Look at shot 15 at full resolution and at 1:1. Does it
   read as a figure glimpsed in a window, or as a spotlight pointed at the plot? Also check whether the
   brighter pane now unbalances the facade in shots 05 and 12.
2. **Does the figure survive its own disappearance?** In shot 16 the whole rig hides, so the room light
   goes out along with the figure. Is that a good beat or a visible pop? Check the transition reads.
3. **The perimeter tests may pass for the wrong reason.** They are containment-only assertions, and
   Claude admits props may stop the push before the wall is ever reached. That means a missing wall
   segment behind a prop would still pass. Prove whether each of the seven pushes actually reaches its
   wall. If some do not, the coverage is thinner than 3/3 suggests.
4. **The audit's new mansion check could false-negative.** It relies on the prefab instance connection
   surviving. Break it deliberately (or reason about `PrefabUtility.GetCorrespondingObjectFromOriginalSource`
   returning null) and decide whether the check silently passes when it should fail.
5. **The chapel reframe was verified from one capture.** Claude picked the coordinates by trigonometry
   and looked at a single render. Is the new shot actually better, or did it trade a flat wall for a
   dark shape? Does the camera clip anything, and does the sightline really pass through the boundary
   opening at all camera heights?
6. **Claude did not re-run the browser gates.** Its justification is that no web or harness code was
   touched, only `scripts/unity-cli.mjs`, which `run-gates.mjs` does not invoke. Verify that claim by
   reading `run-gates.mjs`, and if you doubt it, run `node scripts/run-gates.mjs` and report the result.
   "Should be fine" is exactly the reasoning the evidence rule exists to catch.
7. **The retry can mask flakiness.** One automatic retry turns an intermittent stall into a green run.
   Claude argues the retry is logged loudly so flakiness stays visible. Decide whether that is true in
   practice, and whether an unattended or CI run would now hide a degrading editor.
8. **Did anything else move?** The fingerprint changed. Confirm the only geometry that changed is the
   figure rig. If more moved, Claude's "counts are identical so nothing was added or removed" argument
   is insufficient and you should say so.

## What Claude deliberately did not do

Confirm these were correctly left alone, and challenge the reasoning if you disagree:

- Kitchen garden and cemetery, the two blocking art defects, untouched pending Nick's walk.
- The gate-lock backtrack test was dropped. Claude read `GmThreshold` and found the gate lock is a
  narrative and audio beat that adds no physical barrier, and that retreating to the car before the
  lock is deliberate canon, so a containment test there would assert unspecified behaviour. Verify that
  reading of `GmThreshold` is correct. If a barrier is supposed to exist, that is a real defect Claude
  missed.

## Deliverable

Write `docs/playtest/codex-delta-review-2026-07-18.md` containing:

1. Each of the six changes graded CONFIRMED / PARTIAL / FALSE, with evidence you gathered yourself.
2. Exact outputs: both audit fingerprints, EditMode count, PlayMode count with test names, tour count.
3. A blunt verdict on shots 08, 12, 15 and 16 specifically, since those are the ones Claude changed or
   claims improved.
4. Anything Claude broke, missed, or graded too generously, ranked worst first.
5. Whether the perimeter tests genuinely pressure the walls, with your evidence either way.
6. A yes or no on: is the product still B+, or did these changes move it?
7. Explicit confirmation that you committed nothing and pushed nothing.

Be harsh. Claude was harsh about your work and asked for the same in return. If a fix is cosmetic, say
so. If a test is self-congratulatory, explain exactly how.

## One caution on scope

The real Phase 0 blocker is still Nick's walk, not another agent review. The garden, the cemetery,
EV -3, the modern car, the 4m45 pacing and the wind A/B all need his eyes and ears, and no amount of
review by either of us resolves them. Do not let this delta review turn into a third full-estate pass,
and do not hold up his walk waiting for it. Keep it to the six changes and what they touched.
