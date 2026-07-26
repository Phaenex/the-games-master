# Codex Phase 0 scene-engine final report - 2026-07-19

## Verdict

The Wend Hill opening is ready for independent Claude review and Nick's required walk. It is not
honestly A+ art yet. The engine and verification are substantially stronger than the scene's weakest
remaining content. No purchase, commit, or push was made.

Current progress:

`[██████████] Engine 100% · [██████████] Wend Hill 100% · [██████████] Verification 100% · [░░░░░░░░░░] Nick review`

The candidate is deterministic at fingerprint `43523adf14ad0c57`: 4,716 objects, 3,443 renderers,
491 colliders and 19 lights. The structural and perceptual report has zero errors and one deliberate
warning: `amb_wind` may retain stationary machine-like character (`tone=7.1dB`,
`stationarity=0.78`, `silence=0%`, `seam=0.21`).

## What changed

### Repetition and composition

- Cemetery markers are authored as family plots with six-plus perceptual signatures, breathing
  room, interrupted wall runs, grave-base accumulation and a true recessed open grave.
- The cemetery and garden floor ribbons were removed. Both routes are now measurable
  `GmClearedPathGuide` negative space with no renderer or collider.
- Review cameras were moved away from path endpoints. The garden detail shot now looks diagonally
  across four bands from outside the east fence instead of photographing an empty centerline.
- Viewport occupancy now reports collider, projected-bounds and exact readable-mesh ray evidence on
  a 5x5 grid. Near-camera failures can be named from actual triangles instead of guessed from bounds.

### Garden

- Rejected raised soil meshes because four material approaches rendered as a fake mound, navy
  cutout, black strip or glossy brown ribbon in real GPU frames.
- Rejected twelve waist-height row stakes because the GPU tour showed black bollards in a grid.
- Final garden uses four broken bands of dead growth, dry clumps, discontinuous leaf and twig traces,
  eight ankle-height cultivation remnants, pumpkins, a dropped basket/bucket work cluster, well,
  scarecrow, shed, fence breaches and authored negative-space circulation.
- The dry foliage material has a fixed night-palette contract. Only garden-row vegetation receives
  the lifted dry tint; cemetery and acreage growth keep their darker ranges.

### Adaptive engine

- Garden variants now replace one authored detail, never the complete bed family or an invisible
  path owner.
- Preview candidates clone the replaced object's authored surface controls before audit and capture.
  Raw pack materials cannot turn a dry night comparison green/white, and source assets are not
  modified.
- Six guarded variants render, select none and restore the exact scene fingerprint.
- A regression test forbids adaptive slots on negative-space path guides.

### Landscape, style, sound and pacing

- Middle acreage uses staggered trees, broken hedgerows and incomplete field-fence remnants; four
  distant ridges and irregular woodland close the horizon without a second playable room.
- The modern vehicle is explicitly documented as deliberate contemporary contrast, with viewport
  budgets and weathering limits. Automation cannot decide if the contrast is tasteful.
- Soundscape analysis measures continuous-bed count, persistent tone, stationarity, silence share,
  loop seam and prior rejection. It reports the current wind risk instead of laundering it as PASS.
- Three pacing candidates are simulated against five player profiles. All 15 rows complete the
  route before blackout and remain within the authored quiet-gap budget. This does not prove that
  the current 4m45 control feels tense.

## Verification evidence

| Gate | Result |
|---|---|
| Scene registry and repo-to-Unity drift | PASS, 1 registered scene, 38/38 tracked files |
| Scene learning | 12/12 |
| Registry/scaffold/sync | 10/10 |
| Shut the Box logic | 23/23 |
| Browser in-page harness | 47/47 |
| Generated C# scene scaffold | Compiles against Unity 6000.5.3f1 |
| Unity EditMode | 80/80 |
| Unity PlayMode | 3/3 |
| Structural/perceptual report | PASS, zero errors, one wind warning |
| Pacing simulation | 15/15 |
| Guarded variants | 6/6, selected none, exact restoration |
| HDRP review tour | 18/18 PNGs inspected at full size |
| Native macOS build | PASS |
| Native player proof | 7/7 real backbuffer frames, zero runtime errors, no UnityEditor assembly |
| Native frame pacing | PASS at 1280x720 over 240 frames: mean 8.72ms, p50 8.32ms, p95 8.77ms, p99 17.13ms, max 24.78ms |
| Browser archaeology runner | 9/9 gates green, including 163s full walk and 144s agent playtest |
| Imported asset intelligence | 1,676 assets, 13 audio clips, hash `4cfe41b8e1a60d6d127ddbfb30f340ff` |

Final HDRP tour luminance sequence:

`20, 22, 21, 28, 17, 27, 20, 16, 26, 28, 22, 23, 24, 18, 13, 13, 14, 14`

Primary visual evidence:

- `/Users/damato/GamesMaster-Unity/Screens/WendHill/tour-01-spawn.png` through
  `tour-18-figure-cutoff-off.png`
- `/Users/damato/GamesMaster-Unity/Library/GmSceneIntelligence/standalone-proof/01-cold-open-ui.png`
  through `07-porch-composition.png`
- `/Users/damato/GamesMaster-Unity/Library/GmSceneIntelligence/standalone-proof/performance.json`
- `/Users/damato/GamesMaster-Unity/Library/GmSceneIntelligence/audits/wend-hill-audit.json`
- `/Users/damato/GamesMaster-Unity/Library/GmSceneIntelligence/audits/wend-hill-viewport-occupancy.json`
- `/Users/damato/GamesMaster-Unity/Library/GmSceneIntelligence/pacing/wend-hill-simulation.json`

## Harsh grade

| Area | Grade | Why it is not A+ |
|---|---:|---|
| Scene engine | A | It is tested deeply on Wend Hill but has not yet survived adoption by a second full authored room. |
| Verification | A+ | Cold audits, exact restoration, GPU tour, native build/proof, route tests and browser gates all agree. |
| Mansion, threshold and main drive | A- | Strong hierarchy and readable route; some pale tree materials still expose pack seams. |
| Cemetery | B+ | Clearer room and improved plot grammar, but the cross kit is still recognizable at detail distance. |
| Garden | B | It now makes sense and no longer looks procedurally painted, but it is intentionally sparse and lacks one memorable hero detail. |
| Distant acreage | B+ | Depth exists in every required view; some middle distance still reads as economical repetition. |
| Vehicle | B- | Canon can support a present-day SUV, but its silhouette is a blunt art-language contrast. |
| Audio | C+ provisional | The mix is structurally safe, but the continuous wind still triggers the machine-character warning and requires ears. |
| Pacing | Ungraded taste | Simulation passes; 4m45 still requires a human full walk. |
| Phase 0 overall | B+ candidate | Ready to review, not ready to call A+ or ship. |

## Asset recommendation

Do not buy another environment pack. The owned nine-pack bundle already covers the remaining visual
work. If Nick rejects a visual element, first mine the existing grave, vegetation, prop and vehicle
candidates through the guarded preview system.

Do not buy a broad sound pack solely for this wind. First audition the current filtered-wind,
breathing and sparse/silence profiles in motion. The free Horror Elements package should remain
inventory-only until its license/content is indexed and its clips pass the same tone, stationarity,
silence and seam analysis. If all owned candidates fail Nick's ear test, then a targeted natural
wind/woodland ambience pack is justified.

## Remaining gates

1. Claude independently reviews the delta and full-size evidence without trusting this report's
   grades.
2. Nick walks the native Mac build on his display, listens to both F8 profiles, judges the SUV,
   cemetery, garden, EV, figure cutoff and 4m45 pacing.
3. Only feedback from those gates should trigger another Phase 0 art pass.
4. No Court completion claim, purchase, commit or push before the applicable Nick gate.
