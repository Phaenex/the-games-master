# Codex adversarial delta review: Claude post-review fixes

Date: 2026-07-18

Scope: the six changes listed in `docs/CODEX-DELTA-REVIEW-PROMPT-2026-07-18.md` only

## Verdict

Claude's change set earns **C+**: 2 CONFIRMED, 4 PARTIAL, 0 FALSE.

This is not a disaster. The chapel shot is genuinely better, the deterministic tour control is
correct, and every automated suite is green. It is also not the clean victory Claude gave itself.
The figure fix substitutes a plot-beacon window for a clearly readable silhouette, one perimeter
case is mathematically incapable of detecting its missing wall, two garden cases are stopped by
visible fences before they can test the invisible bounds, the mansion audit still has a simple
unpack-and-rename escape, and the retry discards most durable evidence of its first failure.

**Is the product still B+? Yes.** These changes do not move the product grade. They improve review
repeatability and one composition, but they do not resolve the garden, cemetery, EV -3, modern car,
4m45 pacing, or wind A/B decisions that were already holding Phase 0. Nick's walk remains the gate.

## Six-change grading

| # | Change | Grade | Independent finding |
|---|---|---|---|
| 1 | Wider, brighter window figure | **PARTIAL** | The authored values are present: body `0.46`, head `0.34`, emissive `(0.85, 0.30, 0.06)`. Shot 15 now announces the correct pane, but at full-frame scale it reads as a conspicuously bright special window. At 1:1 crop the dark interruption resembles a glowing numeral or glyph more readily than a human figure. The event is detectable, but "perceptible but deniable" overgrades its visual storytelling. |
| 2 | Deterministic figure handling in the tour | **CONFIRMED** | `HideFigureForReview()` clears and hides the rig before the loop, shot 15 forces it, and shot 16 forces then dismisses it. Normal `Start()` still uses `Random.value < 1f / 3f`; the gameplay roll was not seeded. Shots 01 through 14 were figure-free in the new tour. The stated determinism claim is sound. The disappearance transition itself remains a review blind spot, discussed below. |
| 3 | Name-independent single-mansion audit | **PARTIAL** | A connected prefab instance sourced from `haunted_victorian_house.fbx` is now counted independently of its name, so simple renaming is caught. An unpacked duplicate renamed to anything not beginning `Mansion (` has no prefab-instance connection: `IsPartOfPrefabInstance` is false and `GetCorrespondingObjectFromOriginalSource` cannot identify it. Both counters then still report one. The guard improved, but "cannot hide" is false once the prefab is unpacked. |
| 4 | Seven side-zone perimeter pushes | **PARTIAL** | PlayMode is 3/3 and seven pushes execute, but the new test records only final containment. It never records endpoint, collision, or contact with the intended bound. The chapel-east case cannot fail when its wall is missing, and two garden cases hit authored fence colliders first. Calling this wall coverage is self-congratulatory. It is partial containment coverage. |
| 5 | Chapel waypoint 08 reframe | **CONFIRMED** | Shot 08 is materially better. It reads as a three-quarter chapel with a full spire and usable cemetery foreground, not a flat material sample. No camera clipping is visible. The horizontal ray from `(19, 24)` at Unity yaw 67 crosses the east boundary near `(24.3, 26.25)`, inside the authored `z=25.6..34.2` opening. The right side remains dark, but the composition succeeds. |
| 6 | One retry for GUI Unity tasks | **PARTIAL** | The tour passed on its first attempt in this review, so the failure path was not exercised. The implementation retries every exception from the GUI run, not only a proven editor-open stall. Its warning exists only in Node console output; the next attempt deletes and replaces `cli-tour.log`, so the first attempt's detailed log is not durable. An unattended job that retains only exit status gets green with the degradation hidden. |

## Exact verification evidence

Commands were run serially with one Unity instance. Unity was closed before and after the sequence.

```text
node scripts/unity-cli.mjs rebuild
PASS

node scripts/unity-cli.mjs audit       # first audit
PASS: objects=3968 renderers=2949 colliders=496 lights=9 fingerprint=4707410a08b94635

node scripts/unity-cli.mjs test
Unity tests: 41/41 passed, 0 failed

node scripts/unity-cli.mjs playtest
Unity tests: 3/3 passed, 0 failed
GmPrologueRouteTests.AdversarialBoundaryPushesCannotEscapeOrEnterTheMansion
GmPrologueRouteTests.ExplorationRouteReachesEveryAuthoredExteriorZone
GmPrologueRouteTests.SideZonePerimetersContainThePlayer

node scripts/unity-cli.mjs tour
TOUR COMPLETE 16/16
mean luminance: 17, 22, 14, 25, 19, 25, 18, 18, 22, 22, 26, 24, 21, 17, 18, 16
GUI attempt: 1/2 succeeded; no retry warning was emitted

node scripts/unity-cli.mjs audit       # second audit
PASS: objects=3968 renderers=2949 colliders=496 lights=9 fingerprint=4707410a08b94635
```

Both post-rebuild audits are identical. They match Claude's claimed new baseline. The previous
fingerprint in the supplied review record was `046d838dfbce58dd`.

All 16 PNGs under `GamesMaster-Unity/Screens/WendHill` were opened at full resolution. Luminance was
used only as a blank-frame guard, not as an art grade.

The skipped browser work was also run fresh. `scripts/run-gates.mjs` does not import or invoke
`unity-cli.mjs`, but relying on that source-level separation was unnecessary. One uninterrupted run
produced:

```text
unit+harness tests      PASS 66s
agent playtest          PASS 272s
door sequence           PASS 64s
full walk               PASS 362s
env entrance            PASS 49s
porch breath            PASS 65s
g2 bell+lamp            PASS 69s
hall handoff            PASS 56s
polish (leaves/owl)     PASS 41s
ALL GATES GREEN
```

## Blunt shot review

### Shot 08: chapel

**Good fix.** This is the strongest change in the set. The chapel finally reads as a building. The
spire, roof mass, side wall, and cemetery relationship are legible. The frame is still weighted dark
on the right and would not earn an A+ beauty-shot grade, but it is no longer a diagnostic non-shot.

### Shot 12: porch

**Clean and unaffected.** The review hook kept the rare figure out. The porch and facade hold their
existing hierarchy; there is no bright figure-pane contamination. Shot 05 is likewise balanced because
the review tour hides the rig for ordinary estate views.

### Shot 15: figure far

**The pane reads; the person does not.** At normal viewing size, the upper-right dormer pane is the
obvious brightest window on the facade. That makes it look authored and important before it looks
haunted. At 1:1, the silhouette remains too abstract to read confidently as a standing human. Raising
the emissive field roughly fivefold solved discoverability by shouting from the window, not by making
the figure's pose clear. This is a cosmetic visibility fix, not a finished horror beat.

### Shot 16: figure gone

**The end state is clean, but the transition was not reviewed.** The pane returns to an ordinary amber
window and leaves no visible black card or rig artifact. However, shot 15 is captured from `z=44` and
shot 16 from `z=15`, with different pitch and framing. A static before/after pair from different cameras
cannot reveal whether disabling the whole special glow creates a distracting pop during live movement.
The code removes the entire rig in one frame. The tour proves final states, not the quality of the beat
between them.

## Perimeter attack

Each push calls `CharacterController.Move(direction * 0.22f)` for a fixed number of frames and then
checks only whether the final coordinate is inside a tolerance. There is no contact assertion and no
endpoint evidence in the XML output.

| Push | Nominal no-collision endpoint | Intended bound | What the test really establishes |
|---|---:|---:|---|
| Cemetery south from `(16.2, 17)` for 45 | `z=7.1` | `z=12.6` | The authored spine is audited clear below `z=28.1`, so this is the strongest case and likely reaches the bound. The test still does not record that contact. |
| Chapel forecourt east from `(23, 28.5)` for 70 | `x=38.4` | `x=38.5` | **Vacuous.** The assertion allows `x<=38.9`. With the wall completely absent, the commanded endpoint is still `38.4`, so this case passes. It cannot detect the missing wall it claims to cover. |
| Cemetery north from `(16.2, 36)` for 45 | `z=45.9` | `z=40.6` | It can detect an open route only if no grave or stone prop catches the controller first. The test deliberately refuses to distinguish those outcomes, so wall pressure is unproved. |
| Garden west from `(-22, 22.1)` for 45 | `x=-31.9` | `x=-23.6` | The collidable `GardenFence` run is authored at `x=-23.2`, before the invisible bound. The source prefab contains a `MeshCollider`. This push can pass with the invisible west wall missing. |
| Garden south from `(-16, 18)` for 45 | `z=8.1` | `z=15.6` | The collidable garden fence is authored at `z=16.0`, before the invisible bound. This also passes without proving the walk-bound wall. |
| Coach yard west from `(-28, 48)` for 45 | `x=-37.9` | `x=-33.5` | Coach-house and repair-cluster geometry can absorb the push. No endpoint or collider identity is reported. Wall pressure is unproved. |
| Coach yard north from `(-31.5, 52)` for 45 | `z=61.9` | `z=56.0` | The start avoids the named feed cluster and probably reaches the bound, but "probably" is not test evidence. No endpoint or contact is recorded. |

Verdict: **the suite does not genuinely prove seven walls are pressured.** It provides some useful
zone-containment regression coverage, but 1 of 7 cases is logically incapable of failing for the
claimed defect, 2 of 7 are intercepted by known visible fences, and the remaining cases do not report
the information needed to distinguish intended-wall contact from prop contact.

The right repair is not seven more final-coordinate assertions. Capture the actual endpoint and
collision target for every push, require travel to within controller-radius tolerance of the intended
`WalkBounds/bound` collider, and use a start lane proven clear of unrelated colliders. The chapel-east
push must command travel beyond its allowed tolerance before it can test anything.

## Ranked misses and overgrading

1. **The perimeter test advertises evidence it does not collect.** The chapel-east assertion is
   mathematically toothless, and garden fences make two more cases false-confidence generators. This
   is the worst defect because green automation can now discourage a necessary manual check.
2. **The figure is over-signaled and under-shaped.** The brightest facade pane points directly at the
   trick, while the silhouette still does not clearly read as human. Horror needs recognition followed
   by doubt. This currently produces beacon followed by squinting.
3. **The mansion audit's provenance disappears when the prefab connection does.** Unpack, rename, and
   the supposedly name-independent guard is blind. The message claiming a renamed shell "cannot hide"
   is stronger than the implementation.
4. **The retry's telemetry is too disposable.** A second attempt deletes the first `cli-tour.log` and
   the final exit status is green. Preserve an attempt-numbered log and restrict retry eligibility to
   a diagnosed stall with zero artifacts if this is meant to be evidence rather than convenience.
5. **The disappearance is not a transition test.** Two stills from two locations prove two states.
   They do not prove that the one-frame glow removal looks good while the player is moving.
6. **The fingerprint does not isolate the changed objects.** It hashes sorted path, world position,
   rotation, and lossy scale rows. Equal counts plus a changed hash proves only that the totals stayed
   equal while at least one transform row changed. It cannot rule out swaps, balanced additions and
   removals, material changes, component changes, or multiple unintended moves. The older scene is not
   under version control, so there is no independent row-by-row baseline diff. Source timestamps and
   the six-file delta make the figure-scale explanation plausible, not proven. Emit the sorted rows as
   a diffable audit artifact if future reviews are expected to identify exactly what moved.

## Deliberate omissions checked

- Kitchen garden and cemetery authored logic was left alone. `GmEstateBuilderV2.cs` predates the six
  delta files, and none of the six changes edits those compositions. Their known art defects remain.
- The one-in-three gameplay roll remains random. Only the review tour uses the explicit hide/force
  hooks.
- Dropping a gate-backtrack containment test was correct. `GmThreshold` sets `gateLocked`, plays slam
  and lock audio, shows the beat, and arms the bell. It creates no physical barrier. Pre-lock retreat
  remains deliberate canon, so a containment assertion there would invent behavior not in the spec.
- Threshold Refusal remains closed-door, the three-toll knock-back remains retired, and the rebuild
  audit reports exactly one connected mansion-shell asset.

## Repository actions

I changed no Unity source, scene-authoring source, web source, tests, or production assets during this
review. The only authored repository change is this report. I committed nothing and pushed nothing.
