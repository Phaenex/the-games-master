# Codex Standalone Visual Rescue - 2026-07-19

## Verdict

The rejected standalone build was not passable. The new candidate is an A- visual opening and is
ready for Nick's real walk. It is not an A+ ship grade yet. The modern SUV, repeated cemetery kit,
distant acreage, live wind mix, and 4m45 pacing still cap it until the human gate is complete.

## What was actually wrong

- The player camera and mouse capture were unreliable in the standalone app.
- Runtime text was oversized, persistent, and blocked too much of the scene.
- The editor tour looked better than the shipped player, so it hid player-only rendering defects.
- A narrow local fog box cut pale geometric seams through the cemetery and garden.
- Extreme ground tints crushed the purchased 4K textures before blue fog was added.
- Cemetery and garden paths were perfect primitive rectangles.
- Garden beds were flat painted strips with plants that vanished under the fixed night exposure.
- The mansion shell and bare trees caught the moon as chalky showcase assets.
- Side rooms had props, but too little accumulation or physical evidence connecting them.

## Changes

- Corrected spawn orientation, camera look, pointer capture, and macOS cursor-warp suppression.
- Added deterministic standalone player-backbuffer review poses and blocked live mouse drift only
  while the opt-in review probe is active.
- Enabled HDRP temporal antialiasing, sharpening, dithering, and stop-NaN on the player camera.
- Rebuilt the local mist as a wide, low, dark noise volume with no visible side-room boundaries.
- Rebalanced global fog so nearby material contrast survives before distant layers dissolve.
- Restored the 4K mud and road maps with world-readable tiling and sane HDRP material tint energy.
- Reduced broad wet glare on earth and roads.
- Replaced cemetery and garden path planes with uneven generated ribbon meshes.
- Replaced garden paint strips with low raised soil meshes, then split each row at a different
  erosion point.
- Added sparse dry growth, footprint-scaled leaf litter, work debris, produce, and a motivated
  well-to-shed practical-light hierarchy.
- Added grave-base growth and accumulated leaves around cemetery plots.
- Darkened non-window mansion materials without changing the deterministic lit/dim/dark windows.
- Retoned bare trees and preserved their layered silhouettes.
- Rebuilt the runtime UI in UI Toolkit, narrowed the beat panel, and made it clear itself after
  7.5 seconds instead of captioning the entire walk.
- Added edit-mode guards against rectangular paths, flat beds, missing edge litter, missing
  grave-base dressing, pale mansion materials, fog seams, and loss of TAA.

## Final verification

| Gate | Result |
|---|---|
| Rebuild | PASS |
| EditMode | 49/49 PASS |
| PlayMode route and camera | 3/3 PASS |
| Estate audit | PASS, fingerprint `cb198edc4905ac2e` |
| HDRP review tour | 18/18 frames |
| macOS standalone build | PASS |
| Player backbuffer probe | 7/7 frames, zero runtime errors |

Final player captures are in `/tmp/gm-standalone-depth-v7/`. The launchable app is:

`/Users/damato/GamesMaster-Unity/Builds/macOS/The Games Master.app`

## Honest residuals

- The vehicle is still a modern SUV in a period-horror art language.
- Cemetery crosses and wall segments still reveal kit repetition at close range.
- The distant acreage is deliberately sparse and can use terrain decals or authored topology in a
  later optimization/polish pass.
- The dry garden reads clearly now, but it is still an abandoned functional plot, not a hero scene.
- Sound and full-walk pacing cannot be graded from screenshot automation. Nick must walk and listen.
