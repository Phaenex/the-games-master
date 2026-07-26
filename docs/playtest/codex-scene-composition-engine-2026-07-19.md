# Codex scene-composition engine verification - 2026-07-19

## Verdict

**A- engine foundation. Ready to use for Entry Hall. Not yet A+.**

The previous factory could prove identity, deterministic construction, route mechanics, screenshot
count, and source drift. It could not distinguish an authored scene from plausible asset scatter.
The new layer makes visual intent explicit and rejects common forms of meaningless placement before
the screenshot tour.

The grade is capped because the contract has not yet shaped a new production room from blockout to
Nick approval. Synthetic adversarial tests prove the rules execute; Entry Hall must prove that those
rules improve actual pictures and iteration speed.

## Implemented contract

- One scene-level visual intent manifest.
- Named bounded zones.
- Anchored story clusters with support and detail floors.
- Visible elements with stable IDs, asset families, roles, rationales, and spatial relationships.
- Route reservations and negative-space volumes.
- Visible motivation for every enabled local light.
- One claim per review shot with primary, foreground, support, background, target screen position,
  tolerance, and apparent-size range.
- Percentile and clipping validation on the actual rendered PNGs.

The scaffold is red by design. A placeholder scene compiles but cannot pass its composition tests.

## Failure cases proved

The six new Unity tests include a valid miniature authored composition and counterexamples for:

1. orphaned elements;
2. occupied negative space;
3. blocked authored routes;
4. unmotivated local lights;
5. exact overlapping placements.

All passed in the complete EditMode suite.

## Verification evidence

| Gate | Result |
|---|---|
| Registry/scaffold/sync tests | 10/10 PASS |
| Fresh six-file scaffold, Unity Roslyn | PASS against Unity 6000.5.3f1 |
| Repository-to-Unity source drift | 8/8 clean |
| Unity EditMode | 55/55 PASS |
| Unity PlayMode | 3/3 PASS |
| Shut the Box logic | 23/23 PASS |
| Browser regression harness | 47/47 PASS |
| Wend Hill HDRP tour | 18/18 PASS, attempt 1 |

Tour image evidence ranged from p05/p95 spreads of 25 to 90. Near-black coverage ranged from 1.4%
to 18.8%; near-white clipping was 0.0% on all 18 frames. This proves the stronger capture gate works
on the current dark production scene without weakening the horror exposure or false-failing it.

## Open-source decision

The research is recorded in `docs/UNITY-COMPOSITION-ENGINE.md`. No external code or package was
copied into the project. Prefab-painter palettes and masks, procedural determinism, adjacency
constraints, and camera requirement concepts informed the design. The shipping project keeps its
existing dependency surface.

## Remaining risks

- Tagged authored elements are the audit boundary. A builder that deliberately refuses to tag visible
  content can still hide it, so room-specific tests and human review remain mandatory.
- Frustum and screen-size checks prove framing, not renderer-level occlusion by an untagged mesh.
- Asset-family labels are authored metadata, not automatic visual similarity analysis.
- Pixel metrics reject unusable evidence; they do not grade art direction.
- Golden-image comparison should wait until Nick approves a production baseline. Freezing the current
  candidate first would automate preservation of defects.

## Next production proof

Use the six-file scaffold for Entry Hall. Freeze the room's story job, author its zones and route,
build final-scale architecture before detail, and require every screenshot to name what it proves.
Only after the complete tour passes should Court inherit the pattern.

No scene content, purchase, commit, push, second mansion, or Threshold Refusal state changed in this
tooling pass.
