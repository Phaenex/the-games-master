# Codex Phase 0 completion audit - 2026-07-20

## Verdict

`[█████████▉] 99% - all agent-owned work is A-range; Nick's walk/listen remains the final 1%`

Phase 0 is ready for Nick and an independent Claude review. It is not declared finished because the
approved takeover plan requires Nick to walk the opening or explicitly waive that gate. Court stays
locked. No purchase, commit, push, second mansion or opened Threshold Refusal occurred.

The accepted saved scene fingerprints to `5ef26a690b0ad7a6`: 8 zones, 15 clusters, 82 composition
elements, 2 guarded slots and zero findings. The 2026-07-20 work first closed the last audio
contradiction by replacing four Ninth Bell placeholders, then closed the fixed-night readability
gap in garden crops, cemetery growth and middle-acreage hedgerows without adding scatter or changing
the authored layout. The final standalone pass then closed controller, pause, exported-player and
distribution-package coverage without changing the authored scene fingerprint.

## Requirement-to-evidence audit

| Requirement | Status | Direct evidence |
|---|---|---|
| One mansion only | PASS | Durable identity audit plus `SingleMansionAuditRejectsAnUnpackedRenamedDuplicate`; saved report zero findings |
| Threshold Refusal remains closed-door | PASS | Door-slab guard, adversarial threshold push, live crossing teleport; the player never traverses a door |
| Authored night depth, not pale demo lighting | PASS | Fixed exposure, ACES, controlled gradient, TAA, motivated-light tests; 18/18 HDRP and 7/7 player frames inspected |
| Mansion/drive destination hierarchy | PASS | Authored depth, varied window states, visible gate/car, route and occupancy tests |
| Cemetery is a place, not a repeated grid | PASS | Mixed family plots, recessed grave, north bench/lantern, clear cross-paths, readable base growth and differentiated marker response; composition story test and player route |
| Garden has history and readable work | PASS | Broken furrows, trellis, cart/spill, well, shed and scarecrow; owned crop cards now survive the fixed exposure; environmental-story test and two clear player lanes |
| Acreage avoids one repeated tree stamp | PASS | Three morphology families, readable middle hedgerows and layered ridges; morphology, material-scope and landscape-depth tests |
| Fixed-night vegetation stays authored and scoped | PASS | Non-emissive HDRP/Lit cutouts reuse owned albedo/normal maps in garden, cemetery and acreage; a dedicated test forbids the treatment in distant woodland |
| Vehicle is placed and integrated | PASS technical | Real arrival contrast, oxblood/mud treatment, correct parking and interaction; final taste stays Nick's |
| Interactables are classified, visible and reachable | PASS | 13 stable bound IDs, two-stage copy, wall/range/hysteresis rejection, route and scanner tests |
| Controller covers the complete Phase 0 control surface | PASS technical | One Input System map covers sticks/D-pad, look, card advance/skip, interaction, wind, pause/resume and pause-menu quit; the fifth PlayMode route drives every path |
| Exterior does not sound like a spaceship | PASS technical | One low bed, four located gusts, silence/wildlife gaps; both candidates report `spaceship=False` |
| Ninth Bell audio is final | PASS | Five provenanced cues, 93-test Unity quality envelope, native player loads and decodes 5/5 |
| Ninth Bell crossing is correctly mixed | PASS | Listener low-pass, symptom cutoff, non-looping whisper, one clock strike, 22kHz/control restore in PlayMode |
| Nine-toll pacing remains canonical and safe | PASS technical | 15/15 route/profile simulation rows; no candidate auto-selected; feel remains Nick's |
| Scene-production engine prevents plausible nonsense | PASS | Composition, perceptual, asset, route, light, audio, review-shot and supervised-learning contracts; 39/39 source sync |
| Native review build is real | PASS | Fresh locally signed universal macOS app, no UnityEditor assembly, 7/7 scene plus 2/2 controller frames, zero runtime errors |
| Standalone package runs without the project or editor | PASS | 614MB ZIP passes integrity; a clean extracted copy repeated render, audio and virtual-controller proof independently |

## Final Ninth Bell cue evidence

| Cue | Final source/treatment | Unity metrics | Runtime policy |
|---|---|---|---|
| `chapel_bell` | Horror Elements `Amb_bell`; re-encoded from unreliable Opus to Unity-safe Vorbis | 7.000s, RMS .043, peak .177, centroid 456Hz, machine risk false | Spatial chapel toll, never loops |
| `clock_chime` | CC0 craigsmith Freesound 438313; one archival physical strike plus restrained room tail | 6.000s, RMS .050, peak .571, centroid 815Hz, stationarity .42, silence .32 | One crossing strike, never loops |
| `heartbeat` | CC0 JonasTisell Freesound 670465; real resting heart, seven complete cycles | 7.201s, RMS .088, peak .728, centroid 106Hz, boundary jump .0007 | Internal loop from toll five, cut at crossing |
| `ear_whine` | Project-authored deterministic periodic tinnitus | 8.000s, RMS .109, peak .269, centroid 3461Hz, boundary ratio 2.49, slope .043 | Internal loop from toll six, cut at crossing |
| `whisper_bed` | CC0 PlumForestPodcast Freesound 517868; human whisper with no meaning | 8.956s, RMS .037, peak .530, centroid 3716Hz, silence .25 | One non-looping performance during darkness |

The raw analyser deliberately reports machine-tone risk for `ear_whine`; hiding that would be
dishonest. Asset-intelligence schema v3 records it as intentional internal tonality and
environmental risk false. The exterior soundscape receives no such exception. Provenance is in
`assets/sfx/license.txt` and `Assets/Audio/NINTH-BELL-SOURCE-README.md`; the pinned reproducible
pipeline is `scripts/gen-bell-audio.py`.

## Final verification ledger

| Gate | Result |
|---|---|
| Unity EditMode | 93/93 |
| Unity PlayMode | 5/5, including complete accelerated crossing and full virtual-controller surface |
| Saved estate audit | PASS, zero findings, fingerprint `5ef26a690b0ad7a6`; rerun after the final material/light pass |
| Structural/perceptual report | PASS, 0 errors, 0 warnings, 0 findings |
| HDRP review tour | 18/18 current frames, inspected |
| Built-player proof | 7/7 current scene frames plus 2/2 controller UI frames, inspected |
| Built-player audio proof | 5/5 final cues loaded and decoded |
| Built-player errors/editor leakage | 0 runtime errors, no UnityEditor assembly |
| Built-player controller proof | PASS: 0.644m stick, 0.319m D-pad, 16.53° look, interaction, wind, pause/resume and controller UI |
| Built-player performance | 240 frames at 1280x720: mean 16.67ms, p50 16.67, p95 17.40, p99 17.68, max 32.87 |
| Distribution proof | PASS: signed universal app, clean 614MB ZIP, extracted copy repeated all 9 frames/checks |
| Guarded variants | 6/6, selected none, exact fingerprint restored |
| Pacing | 15/15 route/profile rows |
| Shared source | 39/39 byte-clean |
| Asset index | schema 3, 1,731 assets, hash `6211277dd78c7fae47aea3f3d37e5f60` |
| Learning/scene-system | 12/12 and 10/10 |
| Shut the Box parity | C# 23/23 and JavaScript 23/23 |
| Browser harness | 47/47 |
| Full browser archaeology | 9/9 gates |

Evidence roots:

- `/Users/damato/GamesMaster-Unity/Screens/WendHill/`
- `/Users/damato/GamesMaster-Unity/Library/GmSceneIntelligence/standalone-proof/`
- `/Users/damato/GamesMaster-Unity/Library/GmSceneIntelligence/audits/wend-hill-audit.json`
- `/Users/damato/GamesMaster-Unity/Library/GmSceneIntelligence/asset-index.json`
- `/Users/damato/GamesMaster-Unity/Logs/editmode-results.xml`
- `/Users/damato/GamesMaster-Unity/Logs/playmode-results.xml`
- `/Users/damato/GamesMaster-Unity/Builds/macOS/The Games Master.app`
- `/Users/damato/GamesMaster-Unity/Builds/distribution/The Games Master - Phase 0 macOS.zip`

## Harsh final self-grade before human review

| Area | Grade | Why it is not being called A+ artwork |
|---|---:|---|
| Scene engine | A+ | Strong red-by-design contracts, failure injection, exact restoration and production proof |
| Verification | A+ | Editor, PlayMode, GPU, native, performance, browser and provenance evidence agree |
| Mansion and drive | A | Strong destination and threshold read; display darkness is still human-owned |
| Cemetery | A candidate | Specific stories, routes, marker families, readable growth and grave depth; the owned cross family can still be recognized close up |
| Garden | A candidate | Coherent interrupted-work story and readable crop bands at the authored exposure; final personality remains a taste call |
| Acreage | A candidate | Three silhouettes, readable hedgerows and real depth; distant land remains intentionally subordinate to the mansion |
| Vehicle | A candidate | Integrated, readable and canonically modern; contemporary contrast is a deliberate taste risk |
| Interactables | A | Identity, focus, distance, copy, collision and traversal are tested together |
| Controller and standalone app | A | Complete mapped surface, active-device UI, pause/quit and extracted-player proof; no physical pad was connected, so hardware feel remains human-owned |
| Audio | A technical candidate | No placeholders, honest provenance, strong signal/runtime/native gates; only ears can accept the mix |
| Pacing | A technical candidate | Canon and safe opportunities are proven; tension versus delay is not machine-gradable |
| Phase 0 | **A candidate** | Every agent-owned category is at least A-range, but Nick has not supplied sensory acceptance |

## Exact remaining gate

Nick launches the fresh app, walks car to both side grounds and porch, stays through all nine tolls,
and reports on display darkness, SUV contrast, cemetery repetition, garden readability, figure
cutoff, wind, bell/heart/whine/whisper/clock balance and 4m45 pacing. A failure returns one precise
note for remediation. An explicit pass unlocks Court. Until then progress remains 99%.
