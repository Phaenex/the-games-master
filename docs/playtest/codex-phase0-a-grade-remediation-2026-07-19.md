# Codex Phase 0 A-grade remediation - 2026-07-19

## Verdict

Phase 0 is an **automated A review candidate**, not an A+ art claim or final human acceptance. Scene construction, verification,
interactions and the technical audio mix meet the target. Nick's uninterrupted walk and listen are
still mandatory because automation cannot grade fear, taste, display darkness or whether 4m45 earns
its length. Court remains locked. No purchase, commit or push was made.

Final progress: `[█████████▉] 99% - automated candidate complete; Nick walk/listen is the final 1%`

The accepted scene is deterministic at fingerprint `5ef26a690b0ad7a6`, with 8 zones, 15 clusters,
82 composition elements and 2 guarded adaptive slots. The saved report has zero findings.

## Final remediation

### Landscape and asset integrity

- Rejected the imported dead-willow moss sheet instead of trying to tint around it. The scene-only
  LOD clones retain the authored trunk and branches while dropping the bright rectangular moss
  submesh; purchased source prefabs remain untouched.
- Tightened asset selection so both `foliage` and the pack's `foilage` spelling are rejected across
  the complete asset path, not merely the prefab root name.
- Split the willow population into upright, broad/low and deep/twisted morphology families with
  deterministic width, depth and lean variation. Avenue, middle acreage and ridge silhouettes no
  longer expose one repeated transform recipe.
- Disabled shader-specific emissive controls as well as standard HDRP emission properties. The
  final 18-shot tour has no magenta renderers, white vegetation cards or rectangular tree sheets.

### Cemetery

- Retained seventeen authored family plots but distributed them across timber crosses, arched name
  stones, broken obelisks, low chest tombs, leaning slabs, one child marker and one fallen marker.
- Kept the recessed open grave readable with a warm eroded-earth lip and matte void, while its
  collider prevents the player walking across it.
- Corrected the memorial bench to human scale, moved it out of the entry camera, and placed the
  existing hand lantern on the north boundary. Its point light and composition source now occupy
  the same authored coordinate; the practical-light audit verifies the relationship.
- Preserved the cross-path and cemetery spine as renderer-free negative space. The route tests
  prove traversal rather than inferring it from a screenshot.
- Retired the generated family soil benches after two GPU treatments read as repeated black islands.
  Old plots now return to turf and communicate family history through marker form, spacing, lean,
  boundary growth and leaf accumulation; only the recent open grave cuts a dark ground shape.
- Replaced the temporary goat-skull silhouette on the memorial with a deterministic, repo-authored
  broken-antler crest derived from the owned asylum antler mesh. The source script removes one tine,
  preserves a visible stump, exports the asset reproducibly and has explicit span/vertex/story tests.

### Kitchen garden

- Retired the generated soil ribbons after two HDRP passes proved that even tapered crowns read as
  black holes at EV -2.55. Four interrupted bed families are now carried by owned dead growth,
  row stakes, twig edges, straw, desaturated produce and deliberate missing places; the semantic
  mud surface remains for footsteps and audits without drawing dishonest geometry.
- Added a half-standing owned ladder trellis with fallen pack-authored rails, creating a readable
  vertical cultivation silhouette without procedural cube scaffolding.
- Added one abandoned harvest-cart cluster beside the shed: cart, basket, failed produce and spill.
  It connects the well, shed, scarecrow and dropped work props into a stopped-mid-task story.
- The first cart placement failed the path audit at `(-22.0, 22.1)`. It was rejected and moved to
  the shed-side south edge. The final audit and all three adversarial walks confirm both authored
  garden lanes remain clear.
- Scarecrow visuals retain the owned authored frame and dinner coat. Imported decorative colliders
  are removed; the examine stance remains within the tested 2.8m interaction radius.

### Audio and interaction

- The exterior has one quiet high-passed bed, four localized mono gust emitters, sparse crickets and
  owl calls, authored silence, and seven semantic footstep pools with no immediate repeat.
- There is no exterior drone and no wind pitch modulation. Test evidence reports
  `amb_wind_natural: tone=1.7dB, stationarity=0.71, seam=0.023, spaceship=False` and
  `amb_wind_hybrid: tone=1.5dB, stationarity=0.84, seam=0.114, spaceship=False`.
- The hybrid bed's runtime gain is 0.015 and the natural/control bed's gain is 0.052, both delivering
  roughly -53dB mean after the 105Hz high-pass. Localized gusts are further reduced by 0.11 maximum
  gain, distance and envelope.
- Thirteen visible two-stage story targets, five drive beats, gate lock, closed threshold, ninth
  bell crossing and wake-room handoff remain wired. The route suite exercises the real player and
  interaction scanner; Threshold Refusal never opens the front doors.

### 2026-07-20 Ninth Bell audio closure

- Removed the plan/tracker contradiction that still called four cues placeholders. The final set is
  the licensed Horror Elements chapel toll, one CC0 archival longcase-clock strike, one CC0 real
  resting heartbeat, one CC0 wordless human whisper, and project-authored periodic tinnitus.
- Re-encoded the chapel cue from Opus-in-Ogg to Vorbis after the new test proved Unity had not been
  decoding the old file. ffprobe validity is no longer accepted as Unity validity.
- The SHA-256-pinned curation pipeline isolates one clock strike before the next physical onset,
  builds a six-second decay, trims seven complete heartbeat cycles into a safe loop, makes the
  whisper a single non-looping nine-second performance, and authors the eight-second tinnitus loop.
- Fixed the hearing transition: the low-pass now lives on the AudioListener, reuses the symptom
  filter, returns to 22kHz, and is never created on the source-less Systems object. Heartbeat and
  tinnitus stop at cutoff; crossing sources are parented and cleaned up.
- Audio-intelligence schema v3 records boundary jump, slope, normal sample motion, window texture,
  raw narrow-tone risk, intentional tonal context and environmental risk separately. Tinnitus stays
  honestly flagged as narrow/tonal while being classified as an internal symptom, not ambience.
- A fourth PlayMode test runs cutoff through wake/control restoration and observes the human whisper,
  listener-filtered hearing return and clock strike. The native proof independently loads and
  decodes all five final clips from the built app.

### 2026-07-20 fixed-night vegetation and cemetery closure

- Rejected two owned tarp meshes after they passed structure and path checks but rendered as broad
  black holes in the garden. The accepted baseline was restored before continuing.
- Viewport occupancy then proved the garden already contained substantial authored crop silhouettes;
  the pack foliage ShaderGraphs, not object count, were the failure. Garden rows, cemetery overgrowth
  and acreage hedgerows now reuse their owned albedo and normal textures through deterministic,
  non-emissive HDRP/Lit alpha cutouts. Trees, solid props, renderer count and placement are unchanged.
- Added a scope contract requiring the readable material in all three authored zones and forbidding
  it in distant woodland. The garden's four interrupted row families now read in both HDRP tour and
  native player frames instead of disappearing or requiring another scatter layer.
- Raised only the two cemetery moon returns, marker-stone reflection and the grave's warm soil lips.
  The nested void remains near-black and independently palette-audited. Chest tombs, leaning slabs,
  overgrowth, shovel and recess now separate without lifting global exposure.

### 2026-07-20 standalone and controller closure

- Replaced the partial controller claim with one shared seven-action map covering sticks, D-pad,
  A/Cross interaction and card advance, B/Circle intro skip, RB/R1 wind comparison, Menu/Options
  pause/resume and Y/Triangle quit from pause. Runtime prompts switch with the active device and the
  pause layer stops both game time and audio.
- Added a fifth PlayMode route that injects a virtual gamepad into the real scene and proves every
  controller path, including a focused two-stage interaction. The built-player proof repeats the
  same path outside the editor and records measured stick/D-pad motion and right-stick rotation.
- Added and visually inspected two native controller frames. The first text-size candidate was
  rejected as too small at 1280x720; the accepted cold-open and pause-menu frames use the enlarged
  prompt/legend treatment.
- Built a universal x86_64/arm64 `com.damatnic.thegamesmaster` version 0.1.0 app, verified its bundle
  signature and lack of UnityEditor assemblies, created an integrity-tested 614MB ZIP, extracted it
  to a clean temporary directory and reran all native render/audio/controller checks from that copy.
  No physical controller was connected, so hardware-model feel remains a human check.

## Verification on the accepted fingerprint

| Gate | Final result |
|---|---|
| Unity EditMode | 93/93 passed |
| Unity PlayMode routes/crossing/controller | 5/5 passed |
| Saved estate audit | PASS, zero findings, fingerprint `5ef26a690b0ad7a6` |
| Structural/perceptual report | PASS, zero errors and zero warnings |
| HDRP GPU tour | 18/18 current PNGs inspected at full size |
| Guarded variants | 6/6 rendered, selected none, exact fingerprint restored |
| Pacing simulation | 15/15 route/profile rows passed; no automatic selection |
| Native macOS build | PASS, universal x86_64/arm64, locally signed, one scene, no UnityEditor assembly |
| Native backbuffer/audio/controller proof | 7/7 scene + 2/2 controller frames, 5/5 final cues, full virtual-gamepad path, zero runtime errors |
| Native frame pacing | 240 frames at 1280x720: mean 16.67ms, p50 16.67, p95 17.40, p99 17.68, max 32.87 |
| Extracted distribution proof | PASS, 614MB ZIP integrity clean; extracted app repeated all 9 frames and native checks |
| Repo/Unity shared-source drift | PASS, 39/39 tracked files |
| Scene learning | 12/12 passed |
| Scene registry/scaffold/sync | 10/10 passed |
| Shut the Box C# parity | 23/23 passed |
| Shut the Box JavaScript | 23/23 passed |
| Browser harness | 47/47 passed |
| Full browser archaeology runner | 9/9 gates passed again on 2026-07-20 |

Evidence:

- `/Users/damato/GamesMaster-Unity/Screens/WendHill/tour-01-spawn.png` through
  `tour-18-figure-cutoff-off.png`
- `/Users/damato/GamesMaster-Unity/Library/GmSceneIntelligence/standalone-proof/`
- `/Users/damato/GamesMaster-Unity/Library/GmSceneIntelligence/audits/wend-hill-audit.json`
- `/Users/damato/GamesMaster-Unity/Library/GmSceneIntelligence/audits/wend-hill-viewport-occupancy.json`
- `/Users/damato/GamesMaster-Unity/Library/GmSceneIntelligence/pacing/wend-hill-simulation.json`
- `/Users/damato/GamesMaster-Unity/Logs/editmode-results.xml`
- `/Users/damato/GamesMaster-Unity/Logs/playmode-results.xml`

Review app:

`/Users/damato/GamesMaster-Unity/Builds/macOS/The Games Master.app`

Distribution ZIP:

`/Users/damato/GamesMaster-Unity/Builds/distribution/The Games Master - Phase 0 macOS.zip`

## Harsh grade

| Area | Grade | Remaining cap |
|---|---:|---|
| Scene engine | A+ | The rules, failure modes and reproducible evidence are strong; adoption by the next authored room will test generality. |
| Verification | A+ | Tests caught the new cart obstruction and unmotivated-light mismatch before handoff, then proved their fixes. |
| Mansion, threshold and drive | A | Strong route hierarchy, authored night depth, readable destination and closed-door continuity. |
| Cemetery | A candidate | Clear family-plot grammar, six-plus marker signatures, readable overgrowth, grave depth and specific story anchors; the owned cross kit remains recognizable at close range. |
| Garden | A candidate | The stopped-work story and four interrupted crop families now survive both HDRP and native player exposure without extra scatter or emission. |
| Acreage | A candidate | Layered ridges, three tree morphologies, broken field remnants and now-readable hedgerows provide foreground/middle/horizon depth. |
| Vehicle | A candidate | High-quality, readable, darkened and seated with authored arrival traces; contemporary contrast is deliberate but still a taste risk. |
| Interactables | A | Visible targets, two-layer copy, reachability, collision and route behavior are tested together. |
| Controller and standalone app | A | Complete generic Xbox/PlayStation-style surface, device-aware UI, pause/quit, native proof and extracted-package proof; physical pad feel remains Nick-owned. |
| Audio | A technical candidate | Exterior machine-drone risk is removed; all five story cues are final, provenanced, quality-gated and decoded in player. Nick still owns sensory acceptance. |
| Pacing | A provisional | Every profile completes safely; only a complete human walk can judge tension versus delay. |
| Phase 0 overall | **A candidate** | Every agent-owned category is at least A-range; human taste gates prevent a final acceptance or A+ claim. |

## Asset inventory truth

The historical intake recorded all nine bundle payloads downloaded. This Mac no longer has a
retained Asset Store payload cache in any known Unity location, so that claim cannot be reproduced
from current files. The project currently proves selective imports of Haunted Village, Witch Village
and Abandoned Village. `npm run assets:horror:check` now reports those imports and exits nonzero when
the nine source packages cannot be found, instead of falsely reporting success. Re-download a future
room's pack from My Assets when needed; do not bulk-import all nine and do not buy another environment
bundle.

## Nick gate

Launch the app and walk spawn to car, gate, cemetery, chapel, garden, coach yard and porch. Listen to
the full route, then press F8 once and repeat a short section. Judge the wind, SUV contrast, cemetery
repetition, garden personality, figure cutoff and 4m45 bell pacing. If any one fails, return a specific
note and Phase 0 remains open. If all pass, approve Phase 0 explicitly; only then may Court unlock.
