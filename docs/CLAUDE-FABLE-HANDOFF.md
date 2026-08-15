# Claude's Fable Handoff — The Games Master

## Current override - 2026-08-13 Reckoning/outbuildings session, paused on Nick's instruction

Nick stopped this session mid-work to get a full handoff after finding a real, serious defect by
hand that automated verification never caught (below). **Do not inherit any grade or "green"
claim from this session or from anything dated 2026-08-13 without re-verifying it yourself** —
that includes this session's own 319/319 EditMode and 12/12 PlayMode numbers, which are real and
reproduced, but which the section below proves are not sufficient evidence that the game works.

### Read this first: the estate gate has no physical enforcement, and green tests hid it

Nick manually noticed the front gate can be walked around. Root-cause confirmed by reading the
actual code, not assumed:

- `GmThreshold.cs`'s gate-lock logic fires purely off **route-spline projection distance**
  (`route.ProjectDistance`) — did the player's projected position pass the gate's route-metre
  mark, then come back. There is no proximity check and no collision check against the gate mesh
  at all.
- When it fires: a sound plays, a narrative beat shows, and `GmGateLeaves.Close()` runs — which is
  **a cosmetic rotation of two leaf props**, nothing else.
- The gate geometry itself (`GmWendOpening.BuildGate`) is two 0.65m-wide pier posts ~5.4m apart in
  open space. **Nothing connects them to a fence, wall, or any boundary** — confirmed by grep,
  there is no fence-building call anywhere near the gate. The walk deck is a uniform 4.8m-wide
  ribbon (`halfWidth = 2.4f`, `GmWendOpening.cs:372`) for the entire 435m route; it never narrows
  to funnel the player through the opening. The only real boundary walls in the scene
  (`GmWendBounds.cs`, the "4 walls" the contract checks) are the outer map-edge perimeter, nowhere
  near the gate.

**A player can walk 4-5m to either side of the gate and bypass it, before or after it "locks."**
And because the lock trigger is route-progress-based rather than proximity-based, the narrative
("Something slammed shut behind me") fires correctly-looking even if the player never went near
the physical gate mesh — the illusion is internally consistent and completely hollow.

**Why this passed "1000 walkthroughs" and every automated gate:** `GmWendWalkProbe` follows
scripted waypoints down the center of the authored route — it has no reason to ever try walking
wide around an obstacle. `GmWendWallProbe` ("walks into each of the four boundary walls") only
tests the far outer map edge. Neither tool was ever designed to adversarially test "can I bypass
this specific chokepoint," and no test in this repo does that for ANY object. This is not a
one-off bug — it is a **defect class**: narrative/logical state (`gateLocked`, beat text, sound)
completely decoupled from physical world state (colliders, geometry that actually blocks
movement). The gate is the confirmed instance; nothing has audited whether it is the only one.

**Nick's own words, verbatim, because they set the priority for the next session:** *"i know the
engine is badly broken and not working right still and needs to be reworked and fixed and
understand game logic, world logic, physics logic, logic for how objects sit and look and are all
that."* Read that as a standing instruction, not a one-time complaint. Before more content work:

1. **Do not trust green EditMode/PlayMode/gate results as proof of physical correctness.** They
   prove code executes and scripted paths complete. They do not prove an object physically does
   what its narrative claims, or that geometry sits/collides/looks right. Treat "tests pass" and
   "the game works" as two separate claims until proven otherwise, per this project's own Evidence
   Before Claims rule — this session is the concrete example of why.
2. **A dedicated adversarial pass is owed**, scoped to exactly this class: for every object whose
   logic implies a physical constraint (locks, walls, closed doors, "you can't get past this"),
   verify the physical enforcement actually exists and actually matches the logical state — not
   just that the logic flag flips and a sound plays. Start with every other `GmThreshold`-adjacent
   system and every other "closed/locked/blocked" narrative beat in `docs/superpowers/specs/` and
   confirm each one has a real collider doing the work, not just a state machine and an animation.
3. **Fix the gate itself** once that audit tells you the right fix (a fence tying the piers to
   real boundary, an invisible blocking volume, narrowing the walk deck near the gate approach —
   Nick has not picked one; don't pick for him without asking, this is core-mechanic surface).
4. **Build a real adversarial test for it**: a probe that deliberately tries to walk around/through
   things the game claims are blocked, not just down the scripted centerline. Retrofit this
   pattern to whatever the audit in item 2 finds.

### This session's actual work (Reckoning + coach house) — status, not a verdict

Full plan: `/Users/damato/.claude/plans/estate-property-gameplay-modular-zephyr.md`. Started from a
pasted external-tool ("Gemini Antigravity IDE") plan with fabricated completion claims and canon
violations (a "Manor House," open outbuildings contradicting the locked Ninth Bell spec) — that
plan was discarded, not built. What actually shipped this session, all **uncommitted**:

```
Cleanup (discard scaffold, revert perf edits)   [██████████] verified clean, unity:scene:check green
Canon docs (5 files)                            [██████████] written
Phase A — Reckoning schedule core               [██████████] built; 285s regression PROVEN in real PlayMode run
Phase B — coach house core                      [████████░░] built, compiles, EditMode/PlayMode green, BUT:
  Phase B plumbing (probe/registry/tour/gate)   [░░░░░░░░░░] not built — explicitly deferred
  Phase B perf/visual verification              [██░░░░░░░░] real numbers below; not clean
```

**Real perf/stability evidence from a fresh standalone build on a quiet host (2.37 load/core,
trustworthy per this project's own host-load rule)**:

```
[GmWendWalkProbe] WALK FAIL: covered 435/435m, 32 frames, 2 stall(s), p95=17.42ms/16.70ms budget
```

- **The coach house causes a real stall**: `STALLED 2 at (-23.85, -1.47, -9.36), 4.5m short of
  waypoint 32` — right at "Coach doors, and chalk under the moss," the beat next to the new
  interior. This is my new geometry (`GmWendOutbuildings.cs`) blocking the walk path — I placed
  the trigger volume / room bounds by math against the route spline, with no way to render and
  look at it in this session. **This needs a visual pass in the actual editor before it ships**,
  not just a code review.
- **A second, pre-existing stall** at `STALLED 1 (-23.96, 0.57, -39.52)`, ~158m in, well past the
  gate (route-metre 18) — not something I introduced, not yet root-caused, not yet reported to
  Nick before this session paused. Needs the same "actually look at it" treatment.
- **p95 = 17.42ms vs 16.70ms budget — a real regression**, small but real, on top of an
  **already-open, pre-existing, unrelated regression**: TASKBOARD lane A1 (`GmWendRuntimeCulling`
  exempting `HouseBeginning`'s ~12 soft-shadow lights from culling) was RED at 0% before this
  session touched anything. My plan's own risk section said not to build new interior content
  until A1 has a measured number — I built anyway (judgment call, reversible) and did get a
  number, and it's over budget. **Do not read the 17.42ms figure as "the coach house's cost"** —
  it's the coach house PLUS the pre-existing A1 regression, unseparated. Isolating them needs the
  A1 fix landing first.
- I have not looked at a single screenshot of the coach house. Zero visual verification exists.
  Do not describe this content as done, shippable, or even "probably fine" until someone (agent or
  Nick) has actually looked at it rendered.

**What's solid**: Phase A (the bell's contextual-cadence *mechanism*, zero new geometry) has real
evidence behind it — an actual Unity PlayMode run confirms the shipped 285s cadence is unchanged
at default config, and 16 new unit tests cover the schedule math's bounds/monotonicity/determinism
directly. That part is in good shape independent of everything above.

### Everything still open, consolidated in one place

**Blocking, in priority order (per Nick's instruction above, do the audit before more content):**

1. Defect-class audit: narrative/logical state vs. physical enforcement, gate first, then sweep
   for siblings (see above).
2. Fix the gate itself, once Nick picks an approach.
3. Visually inspect the coach house in-editor; fix the stall at waypoint 32; re-measure perf in
   isolation once TASKBOARD lane A1 is actually fixed (not before — the numbers are entangled).
4. Root-cause the second, unrelated stall near waypoint 16 (~158m, well before the coach house).

**Deferred by explicit scope decision this session (designed, not built):**

5. `-gmOutbuildingProof` player probe (modeled on `GmHouseProbe.cs`).
6. `scene-registry.json` `probes.outbuildings` entry + two matching `unity-cli.mjs` wiring edits.
7. `GmWendStoryTour` +2 shots for the coach house; `GmWendSceneContract`'s shot-count assert stays
   at whatever the real current count is until this lands (I did not touch shot-count contracts
   this session — confirm current count before assuming 8 or 10).
8. New gate row in `scripts/run-opening-gates.mjs` for the outbuilding probe.

**Nick's calls, not decided by this session (see `2026-08-13-the-reckoning.md` and
`docs/NICK-NEEDED.md` §2c for full framing):**

9. Does exploring make the bell hurry, or buy time? (`reckoningPressureAuthority` direction)
10. How much toll-jitter, concretely?
11. The chapel question — leave shut (it's the bell's own diegetic source) or make it interactive
    (pulling the rope answers a toll early)? Two real, opposed arguments written up in the plan.
12. Should entering an outbuilding raise corruption at all? Ships off by default.
13. TASKBOARD lane F6 — corruption ceiling 4 (`GmHouseProgress`) vs 5 (`GmRunStore`), still open,
    now also gates the Reckoning's dormant corruption bridge.
14. Should the 13 existing grounds POI examines start banking persistent clues too? Default: no.

**Pre-existing, unrelated, found sitting in the tree this session, not touched (do not silently
resolve any of these without Nick — see hard lock "never edit unrelated dirty worktree files"):**

15. A large amount of uncommitted work already existed before this session touched anything:
    `GmRunStore.cs`, `GmEndingManager.cs`, modified `GmHouseProgress.cs`, and new `RaiseCorruption`
    call sites in the Court/Parlor/Shut-the-Box controllers, plus nearly all of `unity/scenes/`
    outside `wend-hill-prologue` and most of `unity/scene-system/`. None of it was touched, edited,
    or reverted by this session. `git status` on `unity/` shows the true current scope.
16. TASKBOARD LANE F (audit blockers) items F7 (partial — save/load and scene-transition wiring
    still have no boot/title-scene caller), F8 (attribution gate not wired into `npm run gates`),
    F9 (the verification harness itself has 86 confirmed findings, `verify-unity-full.mjs` has
    never once passed), F10 (`scan-frame-defects.mjs` miscounts corrupt/missing frames as clean) —
    all still open, all still real, none touched this session.

### Verification commands that actually produced the numbers above

```bash
npm run unity:scene:check                 # sync/registry drift check
node scripts/unity-cli.mjs test            # EditMode: 319/319 this session
node scripts/unity-cli.mjs playtest        # PlayMode: 12/12 this session, incl. the 285s regression test
node scripts/unity-cli.mjs rebuild wend-hill-prologue
node scripts/unity-cli.mjs audit wend-hill-prologue
npm run unity:build:mac                    # fresh standalone app — required before any perf number means anything
npm run unity:proof:walk                   # REFUSES to run above ~3.0 load/core; check `uptime` first, wait for real quiet
```

The host-load gate in `unity:proof:walk`/etc. is doing its job, not being obstructive — this
session watched it correctly block two runs on an overloaded host (this machine runs multiple
concurrent sessions) and then produce a trustworthy, damning number the moment the host quieted.
Don't bypass it (`GM_UNITY_IGNORE_HOST_LOAD=1`) to make a number appear faster; the whole point of
this session's perf finding is that a number obtained the honest way is worth more than a fast one.

## Current override - 2026-07-22 A-candidate review

Codex has completed a post-B+ prevention/readability pass and now self-grades Phase 0 as an **A
candidate**, not A+. Do not inherit that grade. Use the dedicated adversarial prompt:

`docs/CLAUDE-PHASE0-A-CANDIDATE-REVIEW-2026-07-22.md`

Codex evidence report: `docs/playtest/codex-phase0-post-bplus-a-candidate-2026-07-22.md`.

Current proof target:

`[██████████] Engine 100% · [██████████] Codex A candidate 100% · [██████████] Verification 100% · [░░░░░░░░░░] Claude/Nick review`

- Estate/material/Terrain fingerprint `2c9c97b0db8dc80b`; composition/variant fingerprint
  `460221080faabbff`
- 103/103 EditMode, 5/5 PlayMode, 18/18 editor tour
- 7/7 clean native composition frames plus 2/2 dedicated controller UI frames
- Five final Ninth Bell clips decoded; zero runtime or render-integrity findings
- Native 1280x720, 240 frames: 11.76ms mean, 10.46ms p50, 14.89ms p95, 15.52ms p99, 15.61ms max
- Shared Terrain-prefab audit and shared C#/Node runtime-integrity policies now prevent the Reed
  failure class across future scenes
- Persisted keyboard/controller display calibration spans -0.5 to +0.5 stops; automated evidence
  stays at authored level 0 and does not save over player preference
- Chapel has a scene-specific readability floor and passes after two rejected candidates
- SUV has a readable restrained oxblood body treatment; lights remain off
- Objective learning session recorded without Nick verdicts, rule promotion, defect resolution or
  baseline approval
- No purchase, commit or push

Nick still owns physical-controller feel, display brightness choice, audio/wind character, figure
subtlety and the uninterrupted 4m45 pacing call. Court remains locked.

## Prior override - 2026-07-22 adversarial B+ remediation

The 2026-07-16 material below is historical. Current delivery truth is
`docs/STEAM-TRACKER.md`. Claude's adversarial B+ verdict was accepted, and Codex's remediation
evidence is in `docs/playtest/codex-adversarial-review-remediation-2026-07-22.md`. The earlier A-
self-grade in `codex-prebuilt-environment-blend-2026-07-22.md` is explicitly superseded.

Claude found a real built-player defect: Reed01-04 had child meshes but no root renderer/LODGroup,
so Unity Terrain rejected them as warnings while the probe still claimed zero errors. Codex fixed
that defect and the proof gap, rejected one newly visible saturated-green material pass, repaired a
cold saved-audit cache bug and corrected the back-faced gate plaque. This should be reviewed as a
narrow delta, not another unbounded estate sweep:

`[██████████] Engine 100% · [██████████] Automated candidate 100% · [██████████] Verification 100% · [░░░░░░░░░░] Nick review`

- Scene fingerprint: `eda4f37223d0895b`, reproduced by cold saved-estate audits
- Audit inventory: 7,752 objects, 5,421 renderers, 270 colliders, 21 lights, zero findings
- Unity: 98/98 EditMode, 5/5 PlayMode; the fourth test drives the complete crossing and the fifth
  drives the full virtual-controller surface at runtime
- Visual: 18/18 HDRP tour plus 7/7 native scene and 2/2 controller UI backbuffer frames, all inspected
- Repository: learning 12/12, scene system 10/10, 39/39 shared-source sync, C# logic 23/23,
  JS logic 23/23, browser harness 47/47
- Full browser archaeology: 9/9 gates green
- Native build: fresh universal macOS app, zero runtime errors, zero Terrain render-integrity warnings
  and no UnityEditor assembly; 240 frames at 12.50ms mean, 12.53ms p50, 13.27ms p95, 13.67ms p99 and
  13.90ms max at 1280x720
- Native controller: 0.472m stick travel, 0.230m D-pad travel and 12.01-degree look plus cold-open,
  focused interaction, wind and pause/resume all passed in the built player. No physical pad was
  connected for this proof.
- Environment blend: 7,800 Terrain foliage instances across six calibrated owned variants, 173
  living/wet/dead trees in 20 unequal communities and 11 exact horizon/reveal accents
- Figure proof: same-camera 22x42 pixel ROI, 15.91 mean-channel delta and 30.8% materially changed
  pixels; the tour now fails an invisible or overly dominant figure rather than trusting active state
- Audio evidence: natural `tone=1.7dB, stationarity=.71, seam=.023, spaceship=False`; hybrid
  `tone=1.5dB, stationarity=.84, seam=.114, spaceship=False`
- Ninth Bell audio: 5/5 final cues load and decode in the built app. Chapel bell is licensed Horror
  Elements; clock, real heartbeat and wordless human whisper have CC0 provenance; tinnitus is
  project-authored deterministic synthesis. No cue is documented or shipped as a placeholder.
- Asset intelligence: schema v3, 1,731 imported assets, content hash
  `6211277dd78c7fae47aea3f3d37e5f60`; loop boundary and intentional tonal context are separate.

Be adversarial. Do not inherit Codex's grade. Claude's current grade is B+, and Codex retains it
until Nick accepts darkness. Review only the remediation delta first; expand scope only if the new
evidence exposes collateral change. Specifically attempt to refute these claims:

1. No willow has a bright moss card, rectangular sheet or magenta renderer in any final frame.
2. Avenue and acreage trees expose at least three coherent morphology families instead of one
   scaled prefab rhythm.
3. Cemetery markers read as family plots rather than a repeated cross grid.
4. The open grave reads as a recess with disturbed earth, not a flat rectangle.
5. The scaled north bench and its lantern make physical and semantic sense without clipping a
   review camera.
6. The garden reads as interrupted work through furrows, trellis, harvest cart, scarecrow, well and
   shed, rather than props scattered across an empty lot.
7. The garden cart and every other dressing item stay outside the two authored routes in actual
   player traversal.
8. The modern SUV is a deliberate readable arrival contrast rather than an unintegrated asset.
9. The sound bed no longer has the spaceship character Nick reported.
10. All thirteen story interactions are visible, reachable and two-stage.
11. Threshold Refusal remains closed-door through the ninth-bell crossing.
12. Current native-player frames materially match the HDRP review tour.
13. The chapel bell actually decodes in Unity and the player, rather than merely passing ffprobe.
14. The crossing filters the AudioListener, not a source-less object, and restores 22kHz/control.
15. Heartbeat and tinnitus stop at the ninth-toll cutoff; the human whisper is non-looping; the
    final clock cue contains one physical strike.
16. The tinnitus analyser exception is contextual, not a hidden waiver: raw machine-tone risk stays
    true, intentional internal-symptom context stays explicit, and environmental risk stays false.
17. The garden crop cards, cemetery overgrowth and middle-acreage hedgerows are visibly readable at
    the locked exposure, not merely present in the hierarchy or occupancy report.
18. Their readable materials reuse owned albedo/normal textures, remain non-emissive and alpha-cut,
    and do not rewrite the distant woodland, solid hay or purchased source prefabs.
19. The two rejected garden tarp candidates are absent, and no temporary black floor-cover geometry
    survives in the saved scene.
20. Cemetery moon return, marker response and the open-grave earth edge improve separation without
    making the cemetery or global ground pale and flat.
21. Controller support is complete rather than binding-deep: cold-open advance/skip, stick and D-pad
    movement, right-stick look, interaction, wind A/B, pause/resume and pause-menu quit all work.
22. Controller prompts and pause UI remain legible at 1280x720 in the two native controller frames;
    the first undersized candidate is not the one that ships.
23. The freshly exported app is independent of the Unity editor: it contains no editor assembly and
    repeats native render/audio/controller proof with zero runtime errors.
24. The estate's 7,800 foliage instances and 173-tree, 20-community woodland create continuous
    habitat from arrival through porch without becoming one repeated tree wall or blocking a route.
25. The rejected bright-green porch moss is absent from the saved scene and final frames; no neon
    slab, temporary diagnostic material or temporary script survives.
26. The dark distant-soil layer and matte ridge remove the pale horizontal north boundary without
    flattening the foreground or changing the locked global exposure.
27. The figure's pixel gate is meaningful: it would fail an invisible on-state, it would fail a
    billboard-dominant event, and the final 15.91/30.8% result corresponds to the actual upper-right
    pane visible in shots 17 and 18.
28. The porch arrival court reads as integrated drainage, leaf accumulation, foundation growth and
    recovered ecology around a compact real route, rather than another broad empty apron or a prop
    island.
29. All six Terrain foliage prefab roots now own a valid root LODGroup or renderer, every referenced
    LOD renderer has real mesh data, and every prototype has at least 50 actual placements.
30. The built-player proof fails on the exact Reed warning class even when Unity labels it a warning
    or emits it before the runtime probe subscribes; the final player.log contains no such warning.
31. Reed01-04 visibly render in the final native/editor frames with owned-texture, non-emissive,
    alpha-cut HDRP/Lit materials and no saturated-green or rectangular-card regression.
32. A cold saved-scene audit samples the serialized Terrain rather than a stale static cache and
    reproduces `eda4f37223d0895b` without false cemetery-grounding findings.
33. The former gray gate slab is now an arrival-facing framed plaque with visible detail rather than
    an untextured box; no monolithic cemetery floor-cover renderer exists.

For the delta, attack Reed renderability/material colour, warning classification, cold saved-audit
grounding, the gate plaque and any actual cemetery floor-cover. Preserve Claude's existing taste
findings on darkness, cemetery repetition, SUV readability, porch detail and figure subtlety for
Nick; do not re-litigate them from the same unchanged frames.
Do not select a guarded candidate, change the scene as part of review, buy anything, open the
mansion doors, add a second mansion, commit or push.

Run from `/Users/damato/Projects/the-games-master` with Unity closed:

```bash
npm run unity:scene:check
npm run test:all
node scripts/unity-cli.mjs setup
node scripts/unity-cli.mjs audit-saved wend-hill
node scripts/unity-cli.mjs test
node scripts/unity-cli.mjs playtest
node scripts/unity-cli.mjs report
node scripts/unity-cli.mjs pacing
node scripts/unity-cli.mjs variants
node scripts/unity-cli.mjs tour wend-hill
npm run unity:build:mac
npm run unity:proof:mac
npm run gates
```

Report CONFIRMED/PARTIAL/FALSE for Codex's claims, inspect every relevant PNG at full size, give a
letter grade with no kindness, inspect the five source/provenance records and native player log, and
leave Nick's sensory/display/pacing calls open. If a claim is false, name the exact frame, object
path or log evidence. Do not implement fixes during this review.

Date: 2026-07-16  
Project: `/Users/damato/Projects/the-games-master`

## Mission

Continue the visual rebuild and roadmap implementation. Do not declare the opening ready until the screenshot matrix is genuinely good and Nick has completed the Phase 0 walk.

## Current dashboard

`Overall: 79%` (updated 2026-07-16 after the full-coverage polish pass — see PROGRESS.md check-ins)

| Phase | Progress | State |
|---|---:|---|
| 0 — Opening visual rebuild | 92% | Pipeline fix + copy/state/pacing/hall pass; all 8 gates green; Nick walk is the open gate |
| 1 — Court | 60% | Shell, props, evidence draft; full gameplay incomplete |
| 2 — Shut the Box | 55% | Rules tested; full match incomplete |
| 3 — Shards | 15% | Roadmap/spec stage |
| 4 — Hidden room | 10% | Roadmap/spec stage |
| 5 — Persistence | 10% | Shared plumbing exists; wiring incomplete |
| 6 — Labyrinth | 5% | Not started |
| 7 — Six endings | 5% | Not started |
| 8 — Manor puzzles | 0% | Deferred |
| 9 — Final polish | 0% | Deferred |

Latest live baseline (2026-07-16, post estate rebuild): 0 page errors, 0 failed requests, 3,884
render calls, 1,194,647 triangles (+34% from the nine estate buildings — measured and logged, fine
for hardware GPUs), 25 textures. Runtime snapshot: ruins grounds, 20 textured willows, 12 crosses,
flush recessed front doors (real hinged geometry), walkable side paths into cemetery + garden,
estate buildings mounted (chapel, coach house, shed + 6 dressing pieces), breath layer wired.

## Read first

1. `docs/CODEX-HANDOFF.md`
2. `docs/PROGRESS.md`
3. `docs/superpowers/plans/2026-07-15-codex-takeover.md`
4. `docs/superpowers/plans/2026-07-15-codex-visual-rebuild.md`
5. `docs/NICK-NEEDED.md`
6. `docs/superpowers/specs/2026-07-14-opening-threshold.md`
7. `docs/superpowers/specs/2026-07-14-house-history.md`

## Hard locks

- No push.
- No commit unless Nick explicitly authorizes it.
- No asset purchases without asking Nick first.
- No second mansion.
- Preserve the coaching-inn history; never call it a saloon.
- Threshold Refusal stays closed-door: drive → gate lock → porch → doors remain shut → porch dark/KO → Entry Hall.
- Do not reopen the door fashion show or add `SM_Wall_Door` as a surround. (2026-07-16: the
  entrance is now a real recessed reveal with flush hinged leaves — built procedurally; the
  Leartes SM_Door plank doors were rejected for the manor as style-wrong, available for
  outbuildings.)
- Do not serve full Unity demo scenes or giant Modular GLBs; use selected web/LOD assets only.
- Nick's Phase 0 walk is the human gate before Court is declared complete.

## What is already wired

- Realistic Car HD 03; Model T retired.
- Env fence and Env porch sconces.
- Unity front doors with closed-door canon.
- 40 owned dead-tree placements.
- 20 Leartes willow placements.
- 12 Leartes cemetery crosses and authored stone-wall segments.
- Ruins grounds: cemetery, ruined garden, dead-tree avenue, props.
- Wind, dark ambience, crickets, owl, footsteps, door creak, gate slam/lock, KO thud.
- Court shell and Shut-the-Box shell.
- Nine Leartes horror environment payloads downloaded; selective intake only.
- Free “Horror Elements” package is inventory-only and not yet integrated.

## Remaining priority order (refreshed 2026-07-17)

**THE plan for everything after the walk: `docs/superpowers/plans/2026-07-17-next-after-walk.md`**
— read Nick's walk verdict, then follow it top to bottom (fork → G3/Court → STB → shards →
hidden room → persistence → labyrinth → endings, with per-phase testing contracts).

1. **Nick's Phase 0 walk** — the human gate. Exact instructions in `docs/NICK-NEEDED.md`.
2. CORRECTED (2026-07-16, later same day): Entry Hall / Court / Shut the Box do NOT have the
   Prologue's gamma lift — none of them set `renderer.outputEncoding`, so their flat colors always
   displayed as authored. Verified by grep + live hall captures. No pipeline work needed there;
   the earlier version of this item was wrong. (Their GLB textures render linear/darker — that is
   the look they were tuned under; leave it unless a matrix pass says otherwise.)
3. Post-walk fixes from whatever Nick's in-motion read surfaces (known candidates: primitive
   headstone geometry, painted door panels at nose distance).
4. Keep the performance baseline near current levels (3,746 calls / ~890k tris); investigate
   regressions instead of hiding them.
5. Only after Nick's walk, continue Court → Shut the Box → shards → hidden room → persistence →
   labyrinth → endings → final polish.

## Verification commands

```bash
npm run gates            # THE command: all nine gates in sequence with a pass/fail table
npm run playtest:agent   # just the agent playtest (plays the whole opening, ranked findings)
npm test                 # just unit + in-page harness
```

Full harness inventory + the six hard-won harness rules: `docs/TESTING.md`.
Unit/runtime green does not equal visual pass. Open the generated PNGs (apt-*.png and the
capture sets) and classify the visual gate honestly as PASS, BORDERLINE, or FAIL.

## Check-in rule

Before every progress report:

1. Refresh `docs/PROGRESS.md`.
2. Include the overall bar and every phase bar.
3. State completed work since the prior check-in.
4. State the next blocker and whether Nick action is required.
5. Report test results and visual verdict separately.

Never claim “perfect,” “done,” or “ready for Nick” from automated tests alone.

## Current honest verdict (2026-07-16, after the full-coverage polish pass)

Mechanics/canon: PASS.  
Asset wiring/runtime: PASS.  
Story/writing: PASS (fiction-tell-detector, timeline contradiction fixed, Wend Hill canonized).  
UI states: every reachable screen/state captured and read (17 states + hall wake path); slider,
letterbox-clipping, and dead-control defects found by the sweep are fixed and re-shot.  
Opening shipped-art visual bar: PASS at eye-level poses. Honest residuals: painted door panels at
nose distance, primitive (now textured) headstone geometry, kit-tree silhouettes up close.  
Beat pacing: porch-dark hold fixed from an unreadable 1.25s to 3.2s; breath presence layer wired.  
Nick Phase 0 walk: NOT YET DONE — this is the deciding gate before Court continues, and the only
item standing between Phase 0 at 92% and closure.
