# Wend Hill Prologue audit — 2026-07-31

## Outcome

`wend-hill-prologue` is the default registered scene and the current authoritative opening. A clean `npm run verify:opening` completed on 2026-07-31 with every gate passing. The scene is ready for review as an opening slice, not a claim that the complete seven-game project is finished.

The Unity project used for verification was `/Users/damato/GamesMaster-Unity` with Unity 6000.5.3f1. Canonical repo overlays and the live project matched across all 225 tracked files at the start of the final run.

## Final verification matrix

| Surface | What the gate proves | Final result |
| --- | --- | --- |
| Portable C# logic | Shut the Box rules remain in parity outside Unity | 23/23 |
| Fast JavaScript checks | Learning ledger/schema, scene registry/scaffold, runtime-integrity policy, and Shut the Box logic | 50/50 |
| Archived browser harness | Prologue, Entry Hall, Court, Shut the Box, and Parlor build and complete their automated state/rule flows without runtime errors | 47/47 |
| Source/package check | Registry validity, 225-file Unity sync, and required package versions | Pass |
| Generated scaffold compile | Unity 6000.5.3f1 API compatibility for six generated files and five review shots | Pass |
| Unity EditMode | Scene structure, authored content, interaction, route, collision, rendering, audio, and builder contracts | 197/197 |
| Unity PlayMode | Runtime opening behavior and lifecycle integration | 10/10 |
| Canonical rebuild + saved-scene audit | Rebuild command succeeds and the reloaded scene satisfies `GmWendSceneContract` | Pass |
| Player-camera tour | Arrival, gate, lookback, route, chapel, grounds, manor, and porch; each frame has luminance/clipping evidence | 8/8 |
| macOS player | Player build succeeds and contains no `UnityEditor` assembly | Pass |
| Built-player proof | Seven story/composition frames, two controller UI frames, five decoded Ninth Bell clips, keyboard/controller-facing flow, interaction, wind, brightness, pause/resume, and runtime-integrity log | Pass |
| Full-route proof | Exact dense spline, 30 milestones, 435/435 m semantic coverage, 396.30 m physical controller travel | Pass; 0 stalls, 0 navigation fallbacks, 0 missing frames, 0 runtime defects |
| Boundary proof | Sustained movement into north/east/south/west boundary walls cannot escape | 4/4 |

## Performance evidence

The release gate is 1920×1080 output with p95 at or below 16.7 ms. HDRP renders at 67% internally (1287×724) and upscales with FSR 1 `EdgeAdaptiveScalingUpres`. LOD cross-fades are disabled, the Metal graphics queue is bounded to one frame, and vSync-off builds are capped at 120 FPS so an uncapped driver queue cannot manufacture periodic pacing spikes.

- Stationary built-player sample: 240 frames, 8.37 ms mean, 8.34 ms p50, 8.63 ms p95, 8.99 ms p99, 10.28 ms max.
- Full-route sample: 14,935 frames, 8.32 ms mean, 8.33 ms p50, 8.47 ms p95, 8.66 ms p99, 16.88 ms max.
- Route capture sampled a peak of 1,174 active scene renderers; runtime culling keeps the dense purchased environment bounded around the player.

These numbers are evidence for this Apple M5 Mac, not a minimum-spec guarantee. Windows and lower-tier GPU coverage still require a hardware matrix.

## Material fixes from this audit

- Replaced coordinate/straight-line assumptions with a semantic 435 m route used by story, collision, review, and traversal gates.
- Repaired route collision by testing the player controller's swept capsule and removing false lane blockers and their matching visuals.
- Added bounded renderer/light culling, a 67% FSR 1 render budget, a one-frame graphics queue, and a 120 FPS ceiling when vSync is off.
- Removed per-frame allocations from interaction focus scanning while preserving occlusion, range, focus-angle, closest-target, and hysteresis behavior.
- Hardened the scene contract around route anchors, POI presentation, non-primitive evidence props, vehicle scale/centering, runtime render integrity, and performance settings.
- Replaced black primitive POI stand-ins with owned environment props; normalized and centered the arrival car by its longest dimension.
- Corrected porch/gate readability and added deterministic review culling for teleported camera shots.
- Proved real built-player controller movement, look, interaction, pause, display calibration, wind selection, controller labels, and five decoded story clips.
- Made the full-route probe follow the exact dense spline, report semantic coverage separately from physical controller travel, and capture milestones from unobscured gameplay after the authored intro.
- Fixed the archived web prologue's public gate-lock helper so direct calls latch `gateLockFired`, restoring the 47/47 compatibility harness.
- Added a single release command, `npm run verify:opening`, that stops on the first untrustworthy dependency and reports a consolidated result.

## Review artifacts

- macOS player: `/Users/damato/GamesMaster-Unity/Builds/macOS-Wend/Wend Hill Prologue.app`
- Eight-shot editor tour: `/Users/damato/GamesMaster-Unity/Screens/WendHill_Prologue`
- Seven built-player frames plus two controller frames: `/Users/damato/GamesMaster-Unity/Library/GmSceneIntelligence/standalone-proof/wend-hill-prologue`
- Thirty full-route frames and pacing JSON: `/Users/damato/GamesMaster-Unity/Library/GmSceneIntelligence/player-probes/wend-hill-prologue/walk`
- Contact sheet: `/Users/damato/GamesMaster-Unity/Library/GmSceneIntelligence/player-probes/wend-hill-prologue/walk/wend-hill-contact-sheet.png`
- Walkthrough MP4: `/Users/damato/GamesMaster-Unity/Library/GmSceneIntelligence/player-probes/wend-hill-prologue/walk/wend-hill-walkthrough.mp4` (15 seconds, 1920×1080, 30 ordered 15 m milestone frames)
- Boundary frames: `/Users/damato/GamesMaster-Unity/Library/GmSceneIntelligence/player-probes/wend-hill-prologue/walls`

The walkthrough is a deterministic milestone recording assembled from built-player backbuffer frames, not a continuous real-time capture and not an audio review. That makes every frame comparable between runs, but continuous motion, audio mix on speakers/headphones, subjective pacing, accessibility, and minimum-spec hardware remain human/manual review items.

## Reproduce

From the repository root:

```sh
npm run verify:opening
```

Focused gates are also available as `npm run verify:portable`, `npm run test:unity`, `npm run unity:tour`, `npm run unity:build:mac`, `npm run unity:proof:mac`, `npm run unity:proof:walk`, and `npm run unity:proof:walls`.
