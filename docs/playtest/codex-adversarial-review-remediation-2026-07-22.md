# Codex adversarial-review remediation - 2026-07-22

## Verdict

Claude's **B+** is accepted. Codex's earlier A- self-grade was premature. Four of six generated
Terrain foliage prefabs were present and populated but rejected by Unity's built-player instancer,
while the proof reported "zero runtime errors" because Unity emitted warnings. That is an integrity
failure even though the surviving grass kept the screenshots from looking bald.

The technical must-fix is now closed. The grade remains **B+** until Nick settles the pervasive
darkness and completes the sensory/pacing walk. This report does not self-promote it to A-.

## Root cause and durable fix

`WendHill_EstateFoliage_Reed01` through `Reed04` inherited a valid MeshFilter/MeshRenderer on a child,
but their prefab roots owned neither a renderer nor an LODGroup. Unity Terrain requires one of those
root contracts and logged `couldn't be instanced ... no valid mesh renderer` for all four.

The generator now adds a root LODGroup when a calibrated source has only child meshes. It references
only real MeshFilter/SkinnedMeshRenderer data and recalculates bounds. The estate audit and a dedicated
EditMode test independently require:

- a valid root renderer or root LODGroup for every Terrain prototype;
- non-empty LOD renderer lists backed by real meshes;
- all six prototypes registered; and
- at least 50 placed instances per prototype, preventing a nominal six-variant claim.

The standalone proof now scans the complete player log for `couldn't be instanced` and `contains no
valid mesh renderer`, in addition to the runtime callback. This catches warnings emitted before the
probe subscribes and refuses a green result even if Unity does not classify them as errors.

## Visual rejection after the structural fix

The first corrected render exposed a second defect: the four newly visible Reed families were
saturated green. Their purchased ShaderGraph exposed `_BaseMap` but no colour property used by the
generic night tint, so the previous calibration was a no-op.

That render was rejected. The generated Reed copies now use project-owned HDRP/Lit alpha-cut
materials with the purchased albedo and normal textures, double-sided cards, explicit roughness,
zero emission/transmission and a bounded night reflectance. Purchased source assets remain untouched.
The accepted 18-shot tour shows dark, dry foliage with no neon-green clumps or rectangular cards.

## Other adversarial flags

- The gray rectangle left of the gate was the Wend Hill plaque. Its shallow carving was mistakenly
  placed on the far face. It is now a dark, framed, arrival-facing pier plaque with a readable
  monogram; a test pins its face, dimensions and multi-part detail.
- There is no monolithic cemetery floor-cover renderer. `CemeteryGrounding` is metadata-only. The
  discrete flat forms are the intentional open-grave void plus low chest-tomb/fallen-marker families;
  the broad dark band is shadowed Terrain. They remain a human close-walk check because the darkness
  makes that distinction less obvious in screenshots.
- A first cold `audit-saved` run uncovered a separate false-negative/false-positive risk: static
  Terrain height data vanished between editor processes, so the audit compared the sunken cemetery
  against y=0. Ground sampling now rehydrates from the serialized `Ground` Terrain. Two cold audits
  reproduce the final fingerprint.

## Final evidence

| Evidence | Result |
|---|---|
| Scene fingerprint | `eda4f37223d0895b`, reproduced by cold saved-scene audits |
| Scene inventory | 7,752 objects; 5,421 renderers; 270 colliders; 21 lights |
| Terrain foliage | 7,800 instances; six valid and meaningfully populated prototypes |
| Unity EditMode | 98/98 passed |
| Unity PlayMode | 5/5 passed |
| HDRP tour | 18/18 passed and visually inspected; first green-Reed candidate rejected |
| Shared scene source | 39/39 repository-to-Unity files match |
| Figure perceptual pair | 22x42 ROI; mean delta 15.91; 30.8% materially changed |
| Fresh macOS player | 7/7 scene frames; 2/2 controller frames; no editor assembly |
| Player integrity | zero runtime errors and zero Terrain render-integrity warnings |
| Audio | 5/5 final Ninth Bell cues loaded and decoded |
| Virtual controller | cold-open, movement, look, interaction, wind and pause/resume passed |
| Performance at 1280x720 | 240 frames; mean 12.50ms, p50 12.53ms, p95 13.27ms, p99 13.67ms, max 13.90ms |
| Repository | 23/23 C# parity, 12/12 learning, 10/10 scene system, 23/23 JS, 47/47 browser harness |
| Full archaeology runner | 9/9 gates green |

## Remaining human gate

The strongest shots remain A- quality, but darkness is pervasive: the chapel, cemetery and garden
can lose expensive detail on a bright room, weak display or handheld outdoors. Nick must walk the
fresh app on his own panel and judge darkness, SUV readability, cemetery repetition, garden/porch
detail, figure subtlety, wind, the final five-cue mix and the 4m45 pacing. A physical controller has
not been tested. Court remains locked until that verdict.
