# The Games Master — Steam Build Tracker

> **The single dashboard.** Every scene, every story system, every Steam requirement, by phase.
> Refresh this before any check-in. Bars are weighted delivery estimates, not test scores.
>
> Established 2026-07-17, when the target became **a Steam game in Unity** and the HTML build was
> retired as a product.

## The reset you need to understand

`docs/PROGRESS.md` says Phase 0 is 98%, Court is 60%, Shut the Box is 65%. **Those are web-build
percentages and they are now measuring a dead artifact.** In Unity, Court is 0%, the Parlor is 0%, the
Entry Hall is 0%. Nothing was lost — the web build proved the designs and its data/copy carry over —
but the delivery numbers reset to the engine that ships.

This file tracks **Unity truth**. Where a thing is "done in web, absent in Unity," it reads 0% with the
web work credited as design source, because a player on Steam cannot play a design source.

> **2026-08-18 update.** Hub rooms exist. A connected walk reaches the parlor door, the vault grate,
> and back onto the hall. Parlor is 42%, not finished AAA. Built-player 1080p probe completes a
> match at 1920x1080; the 3-rep series is still not qualified (split-half p95 / host load). The
> 08-16 handoff remains the last full opening-gate pack. Live plan: `docs/HANDOFF-2026-08-17.md`.
> Inherit no later chat's 886/886 or "whole mansion" claim.

> **2026-08-13 update (historical).** Unity EditMode went from **not compiling at all** to **294/296 passing**
> this session. The opening blocker (47 CS0117 errors, all `GmCompositionAuthoring.ReviewClaim(...)`,
> a method that didn't exist) is resolved for 5 of the 6 Phase 1-7 scenes — `docs/audit/
> F1-review-claim-decision.md` documents the interim adapter and what it assumes. **Court's 2 tests
> are the only remaining failures, left failing on purpose**: Court is content-locked behind Nick's
> Phase 0 walk, and closing its composition-audit failures means re-parenting real geometry, which
> is content work. Gates 3-12 have never run against a compiling build — no evidence exists past
> gate 2 yet; `npm run gates` is the next real step. Separately, a 262-agent source audit confirmed
> 196 findings; the dominant theme was Phase 1-8 story systems being unit-tested scaffolding not
> wired to gameplay. This session closed part of that (audio now plays, pause-menu tabs render,
> credits read the real catalog, `GmRunStore`/`GmHouseProgress` are bridged) — save/load and scene
> transitions are still unwired pending a boot/title-scene decision. See
> `docs/audit/2026-08-13-findings.md` and TASKBOARD LANE F.
> The scene-factory infrastructure below is **not** implicated: the scaffold generator emits no
> `ReviewClaim` call and its Roslyn compile test still passes. The 47 bad calls were hand-authored.

```
Overall (Unity/Steam): [████░░░░░░░░░░░░░░░░]  22%

Phase  0 Prologue        [██████████████████░░]  92%  08-16 gates 14/14 on a trusted host; Nick walk still open
Phase  1 Entry Hall      [█████████████████░░░]  85%  connected hub walks including cellar climb-out; visual PARTIAL
Phase  2 Parlor          [█████████░░░░░░░░░░░]  42%  editor table slice shipping-proven; Aldric proxy; 1080p series not qualified
Phase  3 Court           [██░░░░░░░░░░░░░░░░░░]  10%  scene spine only; LOCKED behind Nick's Phase 0 walk
Phase  4 Shut the Box    [██████░░░░░░░░░░░░░░]  30%  rules + tile-9 path in source; no finished match UI
Phase  5 Shards + save   [████████░░░░░░░░░░░░]  40%  shared run store + save round-trip; true ending still needs 8 catches
Phase  6 Hidden room     [██░░░░░░░░░░░░░░░░░░]  10%  scaffold + transition; player-facing journals remain
Phase  7 Labyrinth       [██░░░░░░░░░░░░░░░░░░]  10%  scaffold + Huntsman machine; not a finished chase
Phase  8 Six endings     [████░░░░░░░░░░░░░░░░]  20%  resolver reachable; authored presentation remains
Phase  9 Steam layer     [██░░░░░░░░░░░░░░░░░░]   8%  macOS player + controller path; Steam/Windows untouched
Phase 10 Ship polish     [░░░░░░░░░░░░░░░░░░░░]   0%
```

### Cross-phase Unity scene factory `95%` (infrastructure, not shipped-content credit)

```
Scene registry          [████████████████████] 100%  validated scene IDs/paths/methods/markers/shot counts
Scaffold generator      [████████████████████] 100%  six-file red-by-design pack: builder/composition/audit/tour/tests/readme
Repo to Unity sync      [████████████████████] 100%  39 shared C# files tracked; read-only drift gate
Shared scene contract   [████████████████████] 100%  identity/path/roots/camera/scripts/build-list; live-proven on Wend Hill
Reusable review tour    [████████████████████] 100%  18-shot HDRP capture plus extensible scene-specific pixel/perceptual gates
Composition contract    [█████████████████░░░]  85%  metadata, protected routes and budgets are live; taste still needs human review
Adaptive scene intent   [████████████████████] 100%  guarded slots, asset/audio/light intent, exact restoration, no auto-selection
Supervised memory       [████████████████████] 100%  immutable sessions, weighted evidence, Nick-only promotion/resolution/baseline gates
Imported asset index    [████████████████████] 100%  1,731 imported assets; schema v3 distinguishes loop boundaries, texture change, tonal intent and environmental risk
Player visual proof     [████████████████████] 100%  fresh post-fingerprint native sweep, controller frames, pixel gate and zero-error runtime proof
Host capacity guard     [████████████████████] 100%  refuses untrustworthy Unity launches above 3.0 load/core
Workflow documentation [████████████████████] 100%  workflow + composition-engine research and authoring contract
Standalone review app  [███████████████████░]  95%  fresh universal app is valid; proof overlays remain instrumentation-only evidence
```

Post-B+ closure 2026-07-22: Claude's Reed finding is now a shared project contract rather than a
Wend Hill-only patch. `GmTerrainPrototypeAudit` rejects child-only Terrain roots and false variety
populations; paired player/Node policies reject Terrain-instancing and broken-shader warnings. Two
chapel candidates failed a new readability gate before the retained bounded fill passed without
changing fixed/global exposure. The SUV has readable oxblood body planes, a five-level keyboard and
controller display calibration surrounds authored level 0, and native composition frames are clean
of proof UI residue. Current fingerprint `2c9c97b0db8dc80b`; 103/103 EditMode, 5/5 PlayMode, 18/18
tour, 7/7 native scene frames, 2/2 controller frames, five of five final cues, zero runtime or
render-integrity warnings. Codex self-grades **A candidate**, not A+; Claude's new adversarial prompt
and Nick's sensory/pacing/display gates remain open. Current prompt:
`docs/CLAUDE-PHASE0-A-CANDIDATE-REVIEW-2026-07-22.md`.

---

## Phase 0 - Prologue: the Wend Hill approach `92%`

### 2026-08-03 — Claude session: gates re-baselined, performance regression found

**Every claim below this section predates 2026-07-31 22:42 and no longer describes the working tree.**
The Jul 31 audit's `performance.json` was written at 20:12; scene sources were then edited until
22:42 and never perf-measured again. This session ran all eleven opening gates:

| # | Gate | Result |
|---|---|---|
| 1 | portable source + archive | PASS — C# 23/23, fast 50/50, web harness 47/47, sync 233 files |
| 2 | Unity EditMode | PASS **204/204** (was 203/204 before the repo-root fix) |
| 3 | Unity PlayMode | PASS **12/12** |
| 4 | canonical scene rebuild | PASS |
| 5 | saved-scene contract | PASS — fingerprint `460221080faabbff` |
| 6 | player-camera visual tour | PASS 8/8 |
| 7 | canonical macOS build | PASS |
| 8 | standalone story/input/audio | **FAIL** — p95 19.72/20.83/21.71ms over three runs vs 16.70ms |
| 9 | house entry + first game | PASS — 10 frames, zero integrity failures |
| 10 | full-route 1080p performance | **FAIL** — p95 20.35ms vs 16.70ms; route itself clean (435/435m, 0 stalls, 0 nav fallbacks, 0 defects) |
| 11 | standalone boundary walls | PASS — 4 frames |

### 2026-08-15 re-measurement — gates 1-7 green, gate 8 still the same perf wall

First run past gate 2 since the pipeline was unblocked (gate 2 previously ran the whole EditMode
suite and broke on Court's by-design failures, making 3-12 structurally unreachable; that now
carries a named, self-expiring exclusion).

| Gate | 2026-08-03 | 2026-08-15 |
|---|---|---|
| 1 portable | PASS | PASS (101s) |
| 2 EditMode | PASS | PASS (42s) — 337/339, Court's 2 tolerated by name |
| 3 PlayMode | PASS | PASS (44s) — 12/12 |
| 4 rebuild | PASS | PASS (26s) |
| 5 saved-scene contract | PASS | PASS (16s) |
| 6 visual tour | PASS 8/8 | PASS (63s) |
| 7 macOS build | PASS | PASS (57s) |
| 8 standalone proof | **FAIL** p95 19.72/20.83/21.71 | **FAIL** p95 **19.25** |
| 9-12 | 9 and 11 passed then | not reached — 8 blocks them |

**Gate 8 is not a new regression.** 19.25ms is the lowest p95 ever recorded for this gate, against a
historical 19.72-21.71. The mansion gained 216,820 triangles of static collision this session
(`GmMansion.MakeSolid`, fixing a walk-through house) and that was the obvious suspect — it is not
supported by the numbers, and naming it as the cause ahead of the measurement would have been wrong.

The 2026-08-03 diagnosis below blamed the interior being exempt from both performance systems. That
exemption is gone: `GmWendRuntimeCulling` now collects `interiorLights` and toggles them with
interior visibility, which is the likeliest reason the number improved at all. The remaining ~2.5ms
belongs to LANE A1 and is unfinished, not misdiagnosed.

The harness flagged its own conditions honestly: `host at measurement: 23.0 load / 10 cores = 2.30
per core — treat p95 as indicative, not a verdict`. Under the 3.0/core requirement, but a quiet-host
run is still owed before any release claim.

**Root cause of 8 and 10.** Host load fell 2.5x (19 → 8.6) across the three standalone runs while p95
moved 5%, so CPU contention is not the driver. The regression window is 2026-07-31 20:12–22:42, in
which `GmHouseBeginningBuilder` + `GmVictorianInteriorKit` added an entry hall and Parlor **inside the
canonical prologue scene**. That interior is exempt from both performance systems:
`GmWendRuntimeCulling.cs:39,47` skips its renderers *and* lights, and `GmWendPerformance.cs:30` skips
it in the editor audit. It contributes roughly a dozen soft-shadowed HDRP point lights that stay
active across all 435 outdoor metres. Peak renderers 1,359 vs 1,174 on Jul 31.

Not fixed here: the culling exemption is plausibly deliberate (the interior must not pop while the
player stands in it) and needs a real visibility rule rather than a blanket exemption. Nick's call.

Repo blockers fixed this session: `node_modules` was absent so gate 1 died on a missing `playwright`
and gates 2–11 never ran at all; `GmSceneIntelligencePaths.FindRepoRoot()` hardcoded
`~/Projects/the-games-master` after the repo moved to `~/Projects/games/the-games-master`.



The walk from the car to the porch. Plan: `docs/superpowers/plans/2026-07-17-prologue-intro-scene.md`.

**Final agent pass (2026-07-17): B+ / READY FOR NICK REVIEW.** The original D+ estate and B- repair
were rebuilt, audited twice, route-tested, and inspected in a deterministic 18-shot HDRP tour.
Window states now vary, the one-in-three figure exists and disappears permanently, cemetery paths
are clear, the coach yard is staged in work clusters, and the exterior bed is filtered quiet wind
with an F8 silence/wildlife comparison. The scene fingerprint reproduced exactly across two cold
rebuilds. This is not a ship sign-off: the sparse garden, modern SUV, 4m45 pacing, and audio mix still
need Nick's eyes and ears. Final report: `docs/playtest/codex-phase0-final-pass-2026-07-17.md`.
The 2026-07-18 independent-review baseline also passed the complete nine-gate browser runner in one
uninterrupted invocation; this improves tooling confidence but does not change the B+ visual grade.

**Final scene-engine pass (2026-07-19): B+ art candidate, A engine, A+ verification.** The first
review app was rejected for unreadable UI, unreliable mouse capture, pale ground, flat side rooms,
and editor-only screenshots that did not match the player backbuffer. The rebuilt candidate now has
deterministic player captures, TAA, corrected fog bounds, visible 4K mud and road materials, lower
glare, darker mansion and tree materials, renderer-free negative-space paths, broken failed-crop
bands, dry growth, leaf accumulation, grave-base dressing, warm practical depth anchors, and a timed
compact runtime HUD. Final evidence: 80/80 EditMode, 3/3 PlayMode, 18/18 HDRP tour, 7/7 standalone frames
with zero runtime errors, 240 native frames at 8.72ms mean / 8.77ms p95, and estate audit fingerprint
`43523adf14ad0c57`. The remaining grade cap is
human taste plus known content mismatch: modern SUV, repeated cemetery kit, distant acreage, wind,
and 4m45 pacing. Report: `docs/playtest/codex-phase0-engine-final-2026-07-19.md`.

**A-grade remediation (2026-07-19): A- Phase 0 candidate, A+ engine and verification.** The final
tree-card artifact was removed without modifying pack sources; three willow morphologies break the
acreage rhythm; cemetery family markers, bench/lantern and open grave now form a readable room; and
the garden now links broken furrows, a half-standing trellis and an abandoned harvest cart into one
work story. The audit rejected the cart's first route-obstructing placement and a mismatched light
source before acceptance. Final fingerprint `672184a9d7a0456a`; current suite 88/88 EditMode and
4/4 PlayMode,
18/18 tour, 7/7 native proof, zero report findings and `spaceship=False` for both wind candidates.
All authored categories are now A-range. The 2026-07-20 audio closure replaced every remaining
Ninth Bell placeholder, corrected listener-filter routing, added a live crossing test, and proved
all five cues decode from the built app. Pacing and final sensory acceptance remain Nick's taste
gate. Reports: `docs/playtest/codex-phase0-a-grade-remediation-2026-07-19.md` and
`docs/playtest/codex-phase0-completion-audit-2026-07-20.md`.

**Fixed-night closure (2026-07-20): A Phase 0 candidate, A+ engine and verification.** Full-size
HDRP and native-player review proved the garden, cemetery growth and middle-acreage hedgerows had
enough geometry but their pack foliage ShaderGraphs collapsed to black at the locked exposure.
Those authored cards now reuse their owned albedo and normal maps through deterministic,
non-emissive HDRP/Lit alpha cutouts; woodland and object placement remain unchanged. Two rejected
garden tarp candidates were removed. Cemetery moon return, marker response and warm grave lips were
then balanced without lifting global exposure. Evidence at that checkpoint was fingerprint `5ef26a690b0ad7a6`,
93/93 EditMode, 4/4 PlayMode, audit zero findings, 18/18 tour, 7/7 native proof, 39/39 source sync,
and five of five final cues decoded. Every agent-owned category is now at least an A candidate;
Nick still owns sensory acceptance.

**Standalone/controller closure (2026-07-20): A player-control and distribution candidate.** The
shipping input asset now owns keyboard, mouse and gamepad actions together: left stick or D-pad
movement, right-stick look, A/Cross interaction and cold-card advance, B/Circle intro skip, RB/R1
wind comparison, Menu/Options pause/resume and Y/Triangle quit from pause. Prompts switch with the
active device and the pause layer suspends both game time and audio. The fifth PlayMode test drives
that complete surface through a virtual gamepad. The freshly exported universal macOS player repeats
the same proof at runtime, including measured stick/D-pad motion, focused interaction, wind and
pause/resume, then captures 2/2 controller-specific UI frames. The app contains no UnityEditor
assembly, identifies as `com.damatnic.thegamesmaster` version `0.1.0`, passes deep bundle-signature
verification and ships in an integrity-tested 614MB ZIP whose extracted copy independently passed
all native audio/controller/render checks. No physical controller was connected to this Mac, so
hardware-model acceptance remains a Nick check rather than a fabricated claim.

**Selective environment-blend closure (2026-07-22): A- Phase 0 candidate, A+ engine and
verification.** Wend Hill now uses the owned packs as an authored kit rather than importing a
second environment wholesale: 7,800 Terrain foliage instances across six calibrated variants, 173
living/wet/dead trees in 20 unequal communities and 11 exact horizon/reveal trees create continuous
habitat around the retained mansion, drive, cemetery, garden and work yards. A dark distant-soil
layer and matte ridge remove the pale boundary; porch drainage, leaf drift and foundation growth
integrate the final arrival without blocking the stair. A neon-green moss candidate was rejected
from screenshots and removed. The one-in-three figure now occupies the actual upper-right pane, and
the tour's new scene-specific pixel gate measures its on/off difference instead of trusting active
state. Final fingerprint `34a5a6b24c55e2d5`; 96/96 EditMode, 5/5 PlayMode, 18/18 tour, 7/7 native
scene frames, 2/2 controller frames, 39/39 source sync, all nine browser gates and a fresh macOS
build/proof are green. This supersedes the withdrawn correction below; human taste remains open.

**Post-B+ prevention/display closure (2026-07-22): Codex A candidate, Claude/Nick pending.** The
immediate Reed fix is now shared `GmTerrainPrototypeAudit` policy plus paired C#/Node player-log
integrity policy. A scene-specific chapel pixel floor rejected two still-too-dark candidates; the
retained bounded cold fill passes without lifting global exposure. The SUV's restrained oxblood
planes read at arrival, and the pause screen exposes persisted keyboard/controller calibration
within plus or minus 0.5 stops of authored level 0. The first technically green native capture set
was rejected because probe UI obscured scene evidence; the retained seven composition frames are
clean, while two dedicated frames still prove controller UI. Current fingerprint
`2c9c97b0db8dc80b`; 103/103 EditMode, 5/5 PlayMode, 18/18 tour, 7/7 clean native scenes, 2/2
controller UI, five audio decodes and zero runtime/integrity findings. Claude review and Nick's real
walk/ears/display/physical-pad gate remain mandatory.

**Adversarial remediation (2026-07-22): B+ verdict retained pending Nick.** Claude proved that the
fresh executable rejected Reed01-04 because their generated prefab roots had neither renderer nor
LODGroup. The generator now adds a root LOD contract; audit/tests pin valid mesh references and real
per-prototype populations; and the native proof treats Terrain instancing warnings as failures. The
first visible-Reed rerender was rejected for saturated green and replaced with project-owned
HDRP/Lit alpha-cut materials using the purchased textures. A cold saved-audit cache flaw and the
back-faced gray gate plaque were also fixed. Final fingerprint `eda4f37223d0895b`; 98/98 EditMode,
5/5 PlayMode, 18/18 tour, fresh app/proof without instancing warnings, and 9/9 gates. This supersedes
the A- self-grade above. Nick's darkness and sensory walk still control promotion.

**Natural-estate correction (2026-07-20): CLAIM WITHDRAWN after adversarial review.** Nick correctly
rejected the previous result as isolated objects arranged on a room-like floor. Wend Hill now builds
on a cloned Witch Village TerrainData asset rather than a flat procedural stage. Four authored terrain
layers form broad mud, leaf and moss biomes; broken twin wheel-track masks form a narrower drive
without a repeated directional road photograph. A deterministic ecological layer places 695 owned
grass clumps in unequal, overlapping communities along the avenue, burial plots and failed garden
margins while preserving every playable route. A denser woodland attempt failed the mid-drive audit
because it made the horizon wall-like, so it was reverted instead of weakening the gate. Final proof:
fingerprint `ba3c79d6929fde11`, 94/94 EditMode, 5/5 PlayMode, 18/18 tour, 7/7 native composition,
2/2 controller UI and zero runtime errors. Those results prove execution, not believable artwork.
The latest native performance file reads 16.67ms mean / 17.44ms p95. The screenshots still show a
D+ arena-like environment, and the stored audit evidence is stale. The rebuild is active; Nick
should not walk this candidate.

```
Estate + night         [██████████████████░░]  90%  continuous owned ecology, layered ground and dark horizon; Nick mood judgment remains
Threshold Refusal      [████████████████████] 100%  GmThreshold complete: lock, settle, 3 beats, KO
POIs (13, two-layer)   [████████████████████] 100%  GmDesignRuntime, text2 reveal working
Drive beats (5)        [████████████████████] 100%  z-triggered, 4.2s hold
Mansion                [█████████████████░░░]  85%  single sealed shell integrated by woodland screens and a dressed arrival court
Gate material          [█████████████████░░░]  85%  dark wrought iron; renamed HDRP material recognized; no false FAILED log
Ground material        [██████████████████░░]  90%  land-use masks, wheel-track breakup, leaf banks and dark distant-soil layer read in player frames
Giant leaf-plane bug   [████████████████████] 100%  FIXED: SM_DriedLeaves scaled 25x → 100-unit z-fighting planes, removed
Ground mist / density  [██████████████████░░]  90%  near/middle/far separation closes the exposed horizon without lifting global exposure
Car parking            [████████████████████] 100%  fence gap carved, car no longer clips
Coach house            [████████████████░░░░]  80%  loading/feed/repair clusters and recovered yard ecology read; Nick judges staging economy
Drive material         [█████████████████░░░]  85%  broken wheel tracks, centre recovery and frayed shoulders connect the lane to the land
Cemetery dressing      [█████████████████░░░]  85%  burial room, family plots, growth and open grave read; kit repetition remains a taste residual
Kitchen garden         [█████████████████░░░]  85%  failed-work story and recovered margins read; personality remains a taste residual
Bare-tree avenue       [██████████████████░░]  90%  three calibrated families plus living/wet/snag communities and exact reveal screens
Building fill lights   [█████████████████░░░]  85%  controlled practical depth anchors retain dark recesses without global exposure lift
Night mood restore     [██████████████████░░]  90%  shaped low-lux depth with a dark ridge; final display judgment remains human
CLI / tour harness     [████████████████████] 100%  synchronous HDRP capture; deterministic 18/18 clean exit + preserved retry logs
Estate quality audit   [████████████████████] 100%  budgets/routes/materials plus scene-specific figure pixel gate; audit zero findings
Route/adversarial walk [████████████████████] 100%  3/3; seven side bounds log direct Sides contact at the intended WalkBounds wall
Gate                   [████████████████████] 100%  visible/authored at gateZ=65; locked by Threshold Refusal
Car placement          [████████████████████] 100%  authored at z=76.5; fence clearance + near-black oxblood treatment
Vehicle art match      [██████████░░░░░░░░░░]  50%  mechanically integrated; modern contrast remains the weakest unresolved visual choice
Cold open (5 cards)    [██████████████████░░]  90%  loaded and skippable; needs Nick pacing pass
Secret ending (7th)    [██████████████████░░]  90%  pre-gate return-to-car state implemented; human route test owed
Walk bounds (8 rects)  [████████████████████] 100%  enforced by segmented colliders with side-path openings
Porch lights           [████████████████████] 100%  warm paired sources; threshold silhouette reads in tour
Window figure (1-in-3) [████████████████████] 100%  forced far/gone shots + exact same-camera cutoff on/off pair; gameplay roll stays random
Chapel bell            [████████████████████] 100%  RETIRED as a rare event — it is now the clock
Ambience (14 SFX)      [███████████████████░]  98%  105Hz high-pass, roughly -53dB bed delivery, four local gusts, sparse wildlife, spaceship=False; Nick ear check owed
Controller input       [████████████████████] 100%  complete gamepad path, active-device prompts, pause/quit/wind; PlayMode + extracted-player proof
Real UI (not OnGUI)    [██████████████████░░]  90%  functional legible UI Toolkit surface; proof-only overlays are not shipping presentation
```

### The Ninth Bell (added 2026-07-17, owner decision)

The porch KO is retired. The invitation says nine o'clock; the chapel bell tolls nine; on the ninth
you are taken wherever you stand and come to inside as a clock finishes its own ninth chime.
Spec: `specs/2026-07-17-the-ninth-bell.md` · Plan: `plans/2026-07-17-the-ninth-bell.md`.

```
Bell audio (5 sounds)  [████████████████████] 100%  real chapel + clock + heart + wordless whisper, authored seam-safe tinnitus; provenance and native decode green
GmBellSummons (9 toll) [████████████████████] 100%  single authority, 45s first + 30s cadence, diegetic at chapel
Symptoms (toll 4→9)    [████████████████████] 100%  HDRP post + listener lowpass + real heart + authored whine; layers cut cleanly at crossing
GmThreshold rewrite    [████████████████████] 100%  porch KO retired; sealed-door refusal remains canon
The crossing           [████████████████████] 100%  cut → non-looping human whisper → listener-filtered iris → one real clock strike; live PlayMode proof
Wake room (EH stub)    [████████████████████] 100%  dark 7×7 stub + owned longcase clock. NOT the Entry Hall.
Branch beats (7)       [████████████████████] 100%  parsed and fired once per walk rect
The invitation card    [████████████████████] 100%  nine o'clock + old-road directions; no street address
```

**Why nine:** nine names in the ledger, nine tiles in Shut the Box, nine portraits on Court's jury
wall, COUNTED OUT on the child's marker. The bell is the house **counting**. This retro-loads every
nine already in the game and costs nothing.

**The payoff was already planted.** Beat 2 (z=38): *"There was a version of me that paid on time —
Mara married that one."* A man who lost everything to being unreliable is on time for exactly one
thing in his life.

**Open for Nick:** 4m45s of grounds — right length? · wake room ships as a stub, real Entry Hall is
Phase 1 — confirm the split · whispers stay processed audio, VO deferred to Phase 9 with Aldric —
confirm · the old "3 tolls knocks you back" rare event is retired — confirm it's not missed.

**Story beats owned by this phase:** the four cold-open cards (debt / Nora / Mara / the letter) · five
drive beats (gate open → 41k owed → the rest is for Nora → it wasn't always me against the odds → two
years chasing one good night) · the stag-crest thread (letter wax = monument = "FOR THE HOUSE, FROM ITS
WINNERS") · the ledger tally in sevens on the plaque · the surname on the fallen stone · COUNTED OUT on
the child's marker · the chalked "41. 9." and a third number scratched out.

**Exit gate:** Nick walks it and calls the opening good.

---

## Phase 1 — Entry Hall `0%`

Where you wake. *"Cold. The marble had my cheek."* The KO currently fades to black and stops.

```
Room + marble wake     [░░░░░░░░░░░░░░░░░░░░]   0%  continuous from the KO — no "how I got in" cutscene
Nine portraits         [░░░░░░░░░░░░░░░░░░░░]   0%  Marr, Dufresne, Pike, Hale, Gall, Quill, Thale, Aubrey-Locke, Percival
The ledger             [░░░░░░░░░░░░░░░░░░░░]   0%  9 names; 9th entry worn illegible; blank line below it for you
Percival's nameplate   [░░░░░░░░░░░░░░░░░░░░]   0%  worn the same way — same clue told twice
Shard #1 (behind Percival) [░░░░░░░░░░░░░░░░]   0%  intended after the ledger, not enforced
22 interior POIs       [░░░░░░░░░░░░░░░░░░░░]   0%  per the web build's walkthrough
Letter reveal (stage 1)[░░░░░░░░░░░░░░░░░░░░]   0%  three-stage; ends "a friend" = Aldric's own hand
```

**Canon trap:** "a friend" (the letter's signer, resolved = Aldric) and "the friend in the walls" (the
host before Aldric, never resolved, never given a face) are two different things sharing a word. Keep
them distinct in every line of copy.

---

## Phase 2 — Parlor `0%`

The trick-taking card game. **The only playable game that exists in any engine**, and it exists only in
the HTML we just retired. It is also expected to supply most of the true ending's 8+ cheats caught.

```
Card game core         [░░░░░░░░░░░░░░░░░░░░]   0%  design source: The Parlor - Playable Prototype.dc.html
gmFollow() AI          [░░░░░░░░░░░░░░░░░░░░]   0%  the complexity budget every other room's AI is scoped against
Reactive cheat         [░░░░░░░░░░░░░░░░░░░░]   0%  fires ONLY when he cannot legally win the hand
The Read verb          [░░░░░░░░░░░░░░░░░░░░]   0%  the catch model every other room copies in shape
Tells (gold→green 1f)  [░░░░░░░░░░░░░░░░░░░░]   0%  IRRITATED state: "relief he can't afford to show"
Corruption tiers       [░░░░░░░░░░░░░░░░░░░░]   0%  starts at Tier 1, never 0 — frequency + visibility climb
```

**Rule that binds every room:** he cheats only when genuinely about to lose. No room, at any tier, ever
has him cheat from safety.

---

## Phase 3 — Court: "The Assize of One" `0%`

Every station is him. The jury is the Entry Hall's nine portraits.

```
Room dress             [░░░░░░░░░░░░░░░░░░░░]   0%  hall furniture, jury wall ×9, wax-seal HUD
Role-light swing       [░░░░░░░░░░░░░░░░░░░░]   0%  accuser/defender rig swinging with argument state
Evidence deck (5 cards)[░░░░░░░░░░░░░░░░░░░░]   0%  Deck A straight vs rigged
Gavel tarnish tell     [░░░░░░░░░░░░░░░░░░░░]   0%  gold tarnishes when the true card is presented
Loseable pressure clock[░░░░░░░░░░░░░░░░░░░░]   0%  the hearing CAN be lost
Shard #2 (mislabeled)  [░░░░░░░░░░░░░░░░░░░░]   0%  Court evidence mount still unbuilt. Hub Session 4 placed a walkable Mirror Shard II in the Entry Hall attic until Court exists.
```

**Design note worth protecting:** a first visit at low corruption **plays straight** — no planted
evidence, a real chance to lose honestly. The rigging only appears once he is actually threatened. That
is what makes the later reveal land: he is rigging the trial *for* you, and it costs him control of it.

---

## Phase 4 — Shut the Box `25%`

Nine tiles map, in ledger order, to the nine named guests. Never stated; discoverable.

```
Rules module (C#)      [████████████████████] 100%  23/23 EditMode green, imported from Codex's staged port
Board + tiles          [░░░░░░░░░░░░░░░░░░░░]   0%  design source: Shut the Box.dc.html (16K)
Aldric match AI        [░░░░░░░░░░░░░░░░░░░░]   0%  scoped to gmFollow() complexity, not beyond
3 cheats (palm/false/tamper) [░░░░░░░░░░░░░░]   0%  rules exist; UI does not
Hold verb + tally UI   [░░░░░░░░░░░░░░░░░░░░]   0%  lays both boxes open against a running tally
Tile-9 door            [░░░░░░░░░░░░░░░░░░░░]   0%  correct Hold on tile-9 tampering → hidden room
Loss/rematch loop      [░░░░░░░░░░░░░░░░░░░░]   0%
```

**Source-of-truth rule:** the C# lives in the web repo at `unity/shut-the-box/` and is copied into
Unity. Change it there, verify both suites, re-import. Do not let them diverge.

---

## Phase 5 — Shards + persistence `0%`

```
Shard model + display  [░░░░░░░░░░░░░░░░░░░░]   0%  3 required for the true ending
cheatsCaught (shared)  [░░░░░░░░░░░░░░░░░░░░]   0%  currently Parlor-local; BLOCKS the true ending entirely
Corruption / Sanity    [░░░░░░░░░░░░░░░░░░░░]   0%  cross-scene
Defiance / Compliance  [░░░░░░░░░░░░░░░░░░░░]   0%  drives 4 of the 6 endings
Save / load            [░░░░░░░░░░░░░░░░░░░░]   0%  Steam wants cloud saves too (Phase 9)
Continue button        [░░░░░░░░░░░░░░░░░░░░]   0%
```

**Known blocker, flagged in the story bible §9:** until `cheatsCaught` is shared state, Court's and Shut
the Box's catches cannot count toward the true ending at all. Nothing downstream works without this.

---

## Phase 6 — Hidden room `0%`

Aldric's own sealed chamber. Reached only via a correct Hold on tile-9 tampering, which only appears at
high corruption — so never on a casual first visit.

```
Room + paneling door   [░░░░░░░░░░░░░░░░░░░░]   0%  disguised in the STB hall
Aldric's older invitation [░░░░░░░░░░░░░░░░░]   0%  aged, younger hand, never says "friend"
Journal fragments      [░░░░░░░░░░░░░░░░░░░░]   0%  deliberately incomplete — one entry just trails off
Shard #3 candidate     [░░░░░░░░░░░░░░░░░░░░]   0%  (3rd shard canonically lives in the Labyrinth)
```

---

## Phase 7 — Labyrinth: "The House Between" `0%`

7×7, first-person always. The Huntsman, never fully seen, thirty turns.

```
Maze generation        [░░░░░░░░░░░░░░░░░░░░]   0%  design doc BEFORE code
The Huntsman           [░░░░░░░░░░░░░░░░░░░░]   0%  proximity closes faster ONLY when already near the limit
Mirrors (the only ones)[░░░░░░░░░░░░░░░░░░░░]   0%  show nothing / the GM / a figure a beat behind
Shard #3               [░░░░░░░░░░░░░░░░░░░░]   0%  found only by stopping to look, not fleeing past
```

**Deliberate absence:** this is the one room with **no catch mechanic and no tell-glint HUD cue**. That
is not an oversight. Forcing a catch here would resolve the ambiguity the room exists to protect.

---

## Phase 8 — Six endings `0%`

None are coded. In any engine. Currently design intent only.

```
Escape       [░░░░░░░░░░░░░░░░░░░░] 0%   Defiance ≥15, Sanity ≥40 — he watches from the window
Replacement  [░░░░░░░░░░░░░░░░░░░░] 0%   Defiance ≥18 + Inversion — the mask comes off, nothing behind
Pact         [░░░░░░░░░░░░░░░░░░░░] 0%   balanced — a negotiated third option
Collection   [░░░░░░░░░░░░░░░░░░░░] 0%   Compliance ≥15 — your portrait joins the wall
Cheat (true) [░░░░░░░░░░░░░░░░░░░░] 0%   8+ caught, 3+ shards, hidden room — "Finally."
Hollow       [░░░░░░░░░░░░░░░░░░░░] 0%   Sanity 0 — no portrait, no ledger line, forgotten completely
```

Plus the **seventh state** (leave before the gate), which ships in Phase 0 and is not one of the six.

**Unverified since the bible was written:** whether 8+ cheats-caught is actually reachable in a real
playthrough. Reasoned as plausible, never playtested. It gates the true ending.

---

## Phase 9 — Steam layer `5%` (NEW — did not exist before 2026-07-17)

```
Steamworks SDK         [░░░░░░░░░░░░░░░░░░░░]   0%  partner account + appid needed — NICK ONLY
Achievements           [░░░░░░░░░░░░░░░░░░░░]   0%  design pass: the six endings are the obvious spine
Cloud saves            [░░░░░░░░░░░░░░░░░░░░]   0%  depends on Phase 5
Settings / options      [░░░░░░░░░░░░░░░░░░░░]   0%  web build had text-size + reduce-motion; port them
Input rebinding        [░░░░░░░░░░░░░░░░░░░░]   0%  InputSystem already in use
Controller support     [████████████████████] 100%  Xbox/PlayStation-style generic layout, D-pad fallback, pause/quit, native virtual-device proof
Accessibility          [░░░░░░░░░░░░░░░░░░░░]   0%  reduce-motion existed in web; do not lose it
Build pipeline (macOS) [████████████████████] 100%  universal locally signed app + tested ZIP; not notarized and not a Steam depot
Build pipeline (Win)   [░░░░░░░░░░░░░░░░░░░░]   0%  Windows is the Steam baseline; macOS is the dev box
Store page assets      [░░░░░░░░░░░░░░░░░░░░]   0%  capsule art, trailer, screenshots — NICK ONLY
Age rating / content   [░░░░░░░░░░░░░░░░░░░░]   0%
CC-BY attribution      [░░░░░░░░░░░░░░░░░░░░]   0%  gravyart mansion is CC-BY: credit is a SHIP REQUIREMENT
```

**Legal, not optional:** the mansion is CC-BY-4.0. Commercial use is allowed **only with attribution**.
A Steam build without a credits screen naming gravyart is a licence violation. Same audit needed for
every sourced asset in `CREDITS.txt` and `assets/sfx/license.txt`.

---

## Phase 10 — Ship polish `0%`

Full-game gates · Nick walks everything · the 9-persona panel · performance budget on target hardware ·
the commit/push conversation (everything to date is uncommitted by Nick's lock).

---

## The HTML: retired as a product, alive as canon

Retired 2026-07-17. **Not deleted.** It is the design source and, in one case, the primary canon.

| File | Size | Status |
|---|---|---|
| `The Games Master - Art Direction.dc.html` | 156K | **PRIMARY CANON.** Thesis, six endings, Court + Labyrinth designs, persistence. Never retire until fully superseded by specs. |
| `The Parlor - Playable Prototype.dc.html` | 52K | Design source for Phase 2. The only playable game that exists. |
| `The Games Master - Prologue.dc.html` | 216K | Design source for Phase 0. **Still holds unported logic:** `triggerSecretEnding`, `placeCar`, `buildPorchSconces`, `_figureForce`, `buildDriveLamps`. |
| `The Games Master - Entry Hall.dc.html` | 76K | Design source for Phase 1. |
| `The Games Master - Court.dc.html` | 36K | Design source for Phase 3. |
| `The Games Master - Shut the Box.dc.html` | 16K | Design source for Phase 4 (board/UI; rules already ported). |
| `The Games Master - Test Harness.dc.html` | 20K | Tests the dead build. Keep until each scene ports, then retire with it. |
| `The Games Master - Playtest & Review.dc.html` | 24K | Review rig for the dead build. |

**Retire a scene's HTML only when its Unity port is green and Nick has walked it.** Mark it here.

### Test suites, honestly labelled

| Suite | Tests | What it actually proves now |
|---|---|---|
| `npm run gates` / browser harness | 47 | The **dead build** still works. Design-source regression only. |
| `npm run test:logic` (JS STB) | 23 | The **parity partner** for the C# port. The only thing proving the port is faithful. Keep until Unity has its own coverage. |
| `npm run test:logic:csharp` (Mono) | 23 | The port, outside Unity. |
| `npm run test:scene-system` | 10 | Registry, scaffold, sync, marker, path, overwrite, and host-guard contracts. |
| `npm run test:scene-system:csharp` | 5 files | A fresh generated scene pack compiles against pinned Unity references. |
| `node scripts/unity-cli.mjs test` | 45 | Project-wide EditMode coverage in the shipping engine: estate, scene contract, and STB rules. |
| `node scripts/unity-cli.mjs playtest` | 3 | Real Unity route/adversarial walks, including direct perimeter-wall contacts. |
| `node scripts/unity-cli.mjs tour <scene-id>` | per registry | Real HDRP compositions through the reusable review engine; Wend Hill is 18/18. |

---

## Standing rules

- No commit/push/purchases without Nick's explicit word. The large uncommitted tree is intentional.
- Threshold Refusal: the front doors never open in the Prologue.
- Aldric cheats **only** when genuinely about to lose. Never from safety. No exceptions, any room, any tier.
- Fixed exposure, never automatic — auto-exposure re-brightens authored darkness.
- Evidence before claims. Screenshots read by eye. `meanLum` proves pixels, not quality.
