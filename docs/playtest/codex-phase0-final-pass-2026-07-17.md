# Codex Phase 0 Final Pass — 2026-07-17

## Verdict

**B+ / READY FOR NICK'S PASS. Do not call it finished yet.**

The Wend Hill opening is now a coherent, reproducible Unity scene instead of an asset dump. The
mansion approach, night treatment, cemetery/chapel route, coach yard, sealed threshold, varied
windows, and rare figure are credible. Automation is unusually strong for an art-heavy scene. The
kitchen garden remains the weakest composition, the modern vehicle is an unresolved taste clash,
and no automated test can certify that the wind mix or 4m45 pacing feels good. Those are Nick gates,
not excuses to buy another pack.

No purchase, commit, push, second mansion, or open-door Threshold Refusal was made.

## Baseline findings: final disposition

| ID | Final call | Evidence |
|---|---|---|
| F0-01 cemetery repetition | **PARTIAL** | Lower/broken dark wall, clear cross-path and spine, irregular graves. The same wall/cross kit is still visible up close. |
| F0-02 garden readability | **PARTIAL** | Rejected soil slabs, firewood-like row caps, green grass, and invisible-only variants. Final uses paths/fence/scarecrow/shed, sparse dead beds and two small produce clusters. It reads, but only at B quality. |
| F0-03 coach yard sparse | **CONFIRMED FIXED** | Loading, feed, and repair clusters; wheel ruts; motivated 7-lumen lantern; clear turning area. |
| F0-04 uniform mansion | **CONFIRMED FIXED** | Eleven glass slots deterministically resolve as 2 dark, 4 dim, 5 lit. |
| F0-05 window figure absent | **CONFIRMED FIXED** | One-in-three upper-window rig, forced shot 15, permanently gone below z=18 in shot 16 and tests. |
| F0-06 spaceship ambience | **PARTIAL** | Wind max 0.08, 145 Hz high-pass, slow non-periodic drift, sparse insects/owl, F8 wind/no-wind A/B. Nick's ears still decide. |
| F0-07 modern SUV | **NICK** | Correctly parked and contemporary-canon; period-language clash remains visible. |
| F0-08 lighting rigs | **CONFIRMED FIXED** | Final 16-shot range is mean luminance 14–26; no white clipping or dead-black frame. Local light remains motivated. |
| F0-09 missing deterministic audit | **CONFIRMED FIXED** | One command checks required roots, one mansion, HDRP materials, grounding, paths, colliders, audio, and a layout fingerprint. |
| F0-10 sealed threshold | **CONFIRMED PRESERVED** | Adversarial PlayMode pressure and full browser walk leave both front-door rotations exactly 0. Old chapel knock-back remains retired. |
| F0-11 premature purchase | **CONFIRMED AVOIDED** | Owned bundle supplied all final dressing. No additional environment or sound pack is justified before Nick's walk. |

## What changed

- Added deterministic estate audit and `unity-cli.mjs audit`.
- Removed 249 decorative colliders from tree/groundcover roots and protected authored paths.
- Re-composed cemetery boundaries, path breathing room, graves, and perimeter scruff.
- Added a three-purpose coach-yard composition with restrained practical light.
- Added deterministic mansion-window occupancy variation.
- Implemented the one-in-three upper-window figure and permanent approach cutoff.
- Expanded the visual tour from 12 to 16 shots and made per-shot capture synchronous so the editor
  cannot strand the coroutine between waypoints.
- Reworked the garden through multiple rejected visual candidates; final contains no procedural soil
  slab, green crop, clipped prop, or decorative bed collision.
- Rebuilt ambience as quiet filtered wind plus sparse wildlife, with F8 comparison against silence.
- Added PlayMode exploration and adversarial boundary/threshold walks.
- Repaired two stale web-only gate scripts after the full gate runner exposed invalid headless timing
  and post-lock camera choreography. Gameplay code was not weakened to satisfy them.

## Verification evidence

### Unity

- EditMode: **41/41 passed**.
- PlayMode: **2/2 passed** (full exploration route plus adversarial bounds/threshold pressure).
- Cold audit run 1: **PASS** — `objects=3968 renderers=2949 colliders=496 lights=9`.
- Cold audit run 2: **PASS** — the same counts and fingerprint
  `046d838dfbce58dd`.
- HDRP visual tour: **16/16 PNGs**, clean editor exit, luminance by shot:
  `17, 22, 14, 24, 19, 25, 18, 24, 22, 22, 26, 24, 21, 17, 18, 16`.
- Witch Village built-in materials remain **141**; the pack was not mass-converted.
- No temporary C# scripts found. Forbidden synthetic exterior clips are absent from the Unity audio
  graph; their names only remain in explanatory comments or the retired web asset library.

### Cross-project regressions

- Browser harness: **47/47 passed**.
- Shut the Box JavaScript: **23/23 passed**.
- Shut the Box C# parity: **23/23 passed**.
- Nine-gate legacy browser runner first pass: **7 passed, 2 failed**.
  - `play-full`: stale test walked backward after the gate had already latched, stranding itself
    outside the closed gate.
  - `verify-breath`: stale test waited on the hold timer before the headless arrival glide settled.
- Both harness defects were corrected without changing gameplay and passed targeted reruns:
  - Full walk: all **18 shots**, aftermath reached, `errors=0`, gate locked, front doors
    `{left:0,right:0}`.
  - Breath: `gate_lock.ogg` and `porch_breath.ogg` both observed, no 404, no page error.
- The other seven unchanged gates passed: unit+harness, agent playtest, door sequence, environment
  entrance, bell/lamp, hall handoff, and leaves/owl polish.

### Post-report baseline hardening — 2026-07-18

The stitched result above was not treated as final proof. Additional uninterrupted runs exposed
headless input/timing and screenshot starvation under heavy machine contention. Test-only hardening
preserved the live RAF/collision route, removed the post-lock teleport/clamp, waited for the real
porch-settled state, added bounded screenshot/navigation waits, retained better failure diagnostics,
and isolated each gate's browser descendants for cleanup. No gameplay source changed for this work.

A later single uninterrupted invocation passed all nine gates:

```text
unit+harness 67s · agent playtest 306s · door 72s · full walk 402s ·
environment 49s · porch breath 63s · bell/lamp 72s · hall handoff 55s · polish 27s
ALL GATES GREEN
```

This raises robustness confidence, not the visual grade. The product remains B+ until independent
image review and Nick's visual/audio/pacing pass.

## Visual grade by zone

| Zone | Grade | Hard read |
|---|---:|---|
| Arrival / gate / drive | A- | Strong silhouette and destination; modern vehicle remains a taste clash. |
| Mansion facade / porch | A- | Varied windows and restrained warm focus; single shell, sealed door, no clipping. |
| Cemetery / chapel | B+ | Navigable and composed; kit repetition is still visible at detail distance. |
| Kitchen garden | B | Semantic cues now make sense, but dead beds are sparse under the fixed exposure. Do not pretend this is hero art. |
| Coach yard | A- | Best asset-integration improvement: clear work logic, readable light, useful negative space. |
| Exterior audio | B+ pending ears | Structurally sane and no longer layered like an engine; subjective verdict still missing. |
| Test/review tooling | A | Reproducible rebuild fingerprint, route pressure, 16-shot tour, and redundant regressions. |

## Rejected iterations (not counted as progress)

1. Raised cube rows looked like planks/coffins.
2. One large flat soil plane looked like an empty clean rectangle.
3. Small dead meshes disappeared at night.
4. Twig row caps read as tied firewood and clipped the review camera.
5. Tall-grass rows stayed green and looked landscaped.
6. Large evenly scattered pumpkins looked like red boulders.
7. A shovel's oversized source bounds intruded into the authored entry path and the audit rejected it.

The final garden is deliberately restrained because every more aggressive owned-asset treatment was
visually worse.

## Nick's required pass

1. Walk car → gate → cemetery/chapel → garden → coach yard → porch.
2. Push the side bounds and sealed threshold.
3. Stay through the ninth-bell crossing; call 4m45 tense or slow.
4. Press F8 and compare filtered wind against wildlife + silence.
5. Call: EV -3 navigation, modern vehicle, cemetery repetition, garden readability, coach staging,
   porch close-up, and audio A/B.

If Nick accepts those calls, Phase 0 can close and work moves to Court onward. If he rejects one,
fix that observed defect; do not restart the whole estate or buy another broad bundle.
