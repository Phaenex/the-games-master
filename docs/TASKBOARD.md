# The Games Master — Master Taskboard

> **The running board.** Every phase, every task, in execution order, with the test that proves it and
> the visual evidence that proves it *looks* right. Two separate columns on purpose: this project has
> repeatedly shipped green tests over broken frames.
>
> Established 2026-08-03. Ordered by **dependency**, not by phase number — see "Why this order".
> Delivery bars live here and in `STEAM-TRACKER.md`; this file carries the task-level detail.

## How to read and update this

- Bars are 20 chars, weighted delivery estimates, **not** test scores.
- `TEST` = the command whose exit code decides it. `VIS` = the frame(s) a human reads at full size.
- A task is **not done** until both columns are satisfied and `npm run gates` is green.
- Never mark a row done from tests alone. Never inherit a grade — re-verify.
- Status: `TODO` · `WIP` · `RED` (built but failing) · `DONE` · `NICK` (human gate, agent cannot clear)

```
Overall (Unity/Steam)   [████░░░░░░░░░░░░░░░░]  19%
```

## Why this order (three deliberate departures from phase numbering)

1. **Shared state (`cheatsCaught`) moves ahead of all three games.** STEAM-TRACKER §Phase 5 says it
   outright: *"until `cheatsCaught` is shared state, Court's and Shut the Box's catches cannot count
   toward the true ending at all."* Building Parlor, Court and STB against local state and
   retrofitting later is three rewrites. Do the plumbing first.
2. **CC-BY attribution moves from Phase 9 to now.** The gravyart mansion is CC-BY-4.0; commercial
   use without credit is a licence violation. It costs an afternoon and it is legal exposure that
   grows with every build shipped. There is no reason it sits at the end.
3. **The Phase 0 perf regression outranks new content.** The cause is the *interior* — so every
   additional interior room compounds it. Fix the visibility architecture before building more rooms
   inside that scene.
4. **2026-08-13: the physical/logic integrity audit (LANE G) outranks everything else, including
   items 1-3.** Nick manually found that the estate gate has zero physical enforcement — the
   "lock" is a narrative state flag with no matching collider, so a player can walk around it.
   Every green EditMode/PlayMode/gate number this project has ever produced proves code executes
   and scripted paths complete; none of them prove an object physically does what its logic
   claims. That gap could exist anywhere else "locked/blocked/closed" is asserted. Full writeup:
   `docs/CLAUDE-FABLE-HANDOFF.md`, 2026-08-13 section.

---

## LANE G — Physical/logic integrity audit (2026-08-13, BLOCKING, top priority)

```
Gate defect confirmed          [██████████████████░░]  90%  root cause known, fix not chosen
Sweep for sibling defects       [░░░░░░░░░░░░░░░░░░░░]   0%  not started
```

| # | Task | Status | TEST |
|---|---|---|---|
| G1 | **Audit every "locked/blocked/closed/can't-pass" narrative beat in the Prologue for matching physical enforcement**, not just a state flag + sound + cosmetic animation like the gate has. Start from every `GmThreshold`-adjacent system and every spec in `docs/superpowers/specs/` that asserts something is physically barred. | TODO | new adversarial probe per finding: try to walk around/through, not just down the scripted centerline |
| G2 | **Fix the estate gate** — Nick has not chosen an approach (real fence tying the piers to boundary vs. an invisible blocking volume vs. narrowing the walk deck at the approach). Do not pick for him. | **NICK** to pick the approach, then TODO | adversarial walk-around probe must fail to bypass it |
| G3 | Build the adversarial "try to walk around/through anything the game claims is blocked" probe pattern once and reuse it for every finding G1 surfaces | TODO | the probe itself, run against the fixed gate as its first proof |
| G4 | Root-cause the second, unrelated walk-proof stall at (-23.96, 0.57, -39.52), ~158m in — found 2026-08-13, not yet investigated | TODO | `npm run unity:proof:walk`, 0 stalls |
| G5 | Root-cause and fix the coach-house stall at waypoint 32 (~324m, "Coach doors, and chalk under the moss") — the new interior's trigger volume/geometry is blocking the walk path; needs an actual in-editor visual look, not just code review | TODO | `npm run unity:proof:walk`, 0 stalls near the coach house |

---

## Session log — 2026-08-03 (Claude)

The opening gates were **not runnable** at session start: `node_modules` was absent, so gate 1 died
on a missing `playwright` and gates 2–11 produced no evidence at all. Fixed, then worked the chain.

**Landed and verified by measurement:**

- Gate 12 built — `npm run verify:frames` scans every captured frame for magenta / near-black /
  blown / flat. Validated by catching a real defect before it was fixed.
- **Magenta eliminated** (62/62 frames clean). Root cause was the proof harness drawing its own
  interaction target — an unrenderable runtime primitive — into the evidence frames.
- Interior visibility gated by house phase and sliced: route p95 **20.35ms → ~8.7ms**, max
  **123ms → 29.9ms**.
- EditMode **203/204 → 206/206**; `FindRepoRoot` no longer pins a path the repo moved away from.
- **All five** copy-pinned assertions unpinned — they had frozen the UI, so improving wording failed
  gates and the incentive was to leave bad UI alone.
- Host load/free-RAM stamped into both perf JSONs, including on failing runs.
- Story cards, pause screen and typography rebuilt (Ibarra Real Nova, letterboxed, real slider).

**Two changes were reverted after measurement disproved them** — recorded in `docs/TESTING.md`:
forcing mesh-only tree rendering (cost p95 8.71 → 16.10ms plus a fatal crash, fixed nothing), and
flushing the input queue (turned one flaky assertion into every assertion failing).

**Open:** gate 10 route p95 is marginal on a loaded host (8.71–20.35ms range across the day, machine
at 15/16GB swap) — needs a quiet-host baseline. #42 cold-open flake is undiagnosed after a wrong
diagnosis was withdrawn. Nick's walk (#17) remains the Phase 0 exit gate.

## Session log — 2026-08-13 (Claude) — full ground-up audit

A 262-agent source audit read every C# file, all 82 scripts, and all 8 `.dc.html` prototypes, then
put every HIGH/MED finding through an adversarial verifier that defaults to refuting. **294 raw
findings → 196 confirmed (94 HIGH, 102 MED), 9 refuted, 89 LOW unverified.** Full list:
`docs/audit/2026-08-13-findings.md`.

**The headline is not any single bug. It is that Phase 1-8 is scaffolding, not a game.** Around 50
of the confirmed findings are one defect class: a system is built, unit-tested, green — and no
gameplay code ever calls it. `GmSaveSystem.Load()` is never called in production. `GmSceneDirector`
transitions are only ever invoked by their own test. `GmEndingManager.ResolveEnding()` is never
called, so **no ending resolves in a real playthrough**. `GmAudioManager` is never instantiated and
its `PlayAmbience`/`PlaySfx` never assign a clip or call `Play()`. The pause menu's four tabs render
zero content. `GmCreditsUI` is never instantiated — that one is licence exposure, not polish.
Underneath it all sit **two independent run-state stores** (`GmRunStore` and `GmHouseProgress`) that
never exchange a value, which by itself makes two of the six endings unreachable dead code.

**Correction (2026-08-14, itself corrected 2026-08-15):** this specific claim is stale against the
current working tree — `GmHouseProgress.MissCheat()`/`FalseRead()`/`FindShard()`, called from real
gameplay in `GmHouseBeginning.cs`, reach `GmRunStore.RecordCompliance()` and `CollectShard(0)`, so
HostSuccession and TrueEscape are reachable there. **But the bridge is not in any commit** — `da18f95`
does not touch `GmRunStore` (checked directly: `git show da18f95:.../GmHouseProgress.cs` has only a
private static `Run` class), and `GmRunStore.cs`/`GmEndingManager.cs` are untracked in git entirely.
So this correction describes the working tree, not the committed branch — a clean checkout of this
branch still has the original, un-bridged bug. Full annotation: `docs/audit/2026-08-13-findings.md`
lines 20/36/37/41/42/105-107. `GmEndingManager.ResolveEnding()` itself being uncalled from a real
scene transition is a separate, still-open issue (see F6/F7 below) — not fixed by this correction.

The bars in LANE C are honest about the *games* not existing, but they read as if the scene code
that does exist is sound. It is not: six of the seven scene folders have never compiled.

**Fixed and re-verified this session — EditMode taken from 0 compiling to 294/296 passing:**
duplicate `GmParlorRules`/`GmParlorRulesTests` (prologue vs parlor) · `GmShutTheBoxController`
calling three nonexistent `ShutBoxRules` methods · a missing `GmWendOpening.RoadSurfaceY` helper ·
a removed HDRP `lightTypeExtent` call · `System.Linq` missing in three test files ·
`Volume.profileRef` (internal to the SRP assembly, invisible to game code) → `sharedProfile` ·
`LightUnit` moved out of the HDRP namespace in this Unity 6 version, six call sites fixed (one
missing `using UnityEngine.Rendering;`) · none of the six new `*BuildTests.cs` files cleaned up the
scene their `Build()` created, so all six scenes' geometry piled up and corrupted an unrelated
ground-detection test — added `[OneTimeTearDown]` to all six · a Huntsman sanity-gain test asserted
an increase from an already-maxed starting value · the `ReviewClaim`/`Claim` API mismatch (below,
F1) resolved for 5 of 6 scenes, each camera fix verified against a from-scratch Python replica of
the audit's own `WorldToViewportPoint` math, not eyeballed. Also fixed: `NICK-NEEDED.md` sent Nick
to `/Users/damato/GamesMaster-Unity/…`, a directory that no longer exists, and a copy edit that
introduced a fresh self-contradiction in `Art Direction.dc.html` (reverted to original wording).

**Not fixed, deliberately:** Court's composition wiring (hard lock, see F1) and most of the other
findings from the 196-item audit — this pass targeted the compile chain and the systems that gate
it, not the full backlog. No commit, no push.

## LANE F — Audit blockers (2026-08-13)

```
Unity EditMode           [████████████████████]  294/296  ✓  only Court's 2 tests fail, on purpose
Gates 3-12               [░░░░░░░░░░░░░░░░░░░░]   not run this session — no evidence past gate 2
```

| # | Task | Status | TEST |
|---|---|---|---|
| F1 | **Review-claim authoring API mismatch.** Six `*CompositionPlan.cs` files called `GmCompositionAuthoring.ReviewClaim(...)`, a method that didn't exist. Resolved with a documented interim adapter on `GmCompositionAuthoring` (`docs/audit/F1-review-claim-decision.md` — read this before trusting any framing result) plus real geometry/camera fixes for **entry-hall, hidden-room, labyrinth, parlor, shut-the-box** — all 5 now pass. **Court's composition wiring is untouched on purpose** (hard lock, below) | 5/6 DONE, Court **NICK** | `node scripts/unity-cli.mjs test`: 5 scenes pass |
| F1b | **Court composition wiring** — 26 issues (11 elements with no renderer, 3 unmotivated lights, 3 under-authored cluster minimums, 9 shot-framing failures). Fixing means reattaching composition markers to real Court geometry, which is content work Court's hard lock reserves for after Nick's Phase 0 walk | **NICK** (unblocks after walk) | `GmCourtBuildTests` both tests |
| F2 | Duplicate `GmParlorRules` class (prologue vs parlor) | DONE | verified: EditMode compiles past CS0101 |
| F3 | `GmShutTheBoxController` → nonexistent `ShutBoxRules` methods | DONE | verified; `Hold` left unimplemented on purpose |
| F4 | `GmWendOpening.RoadSurfaceY` missing; HDRP `lightTypeExtent`/`LightUnit` namespace issues (6 call sites) | DONE | verified compiles |
| F5 | `System.Linq` missing in 3 scene test files; 6 `BuildTests.cs` files missing scene teardown | DONE | verified: EditMode 294/296 |
| F6 | **Bridge `GmHouseProgress` into `GmRunStore`** | DONE — bridged; the house-caps-at-4-vs-store-caps-at-5 gap is confirmed intentional pacing, not a conflict (Nick, 2026-08-14): Parlor/Court/Shut the Box call `GmRunStore.RaiseCorruption` directly, uncapped, and are what can carry a run to Tier 5/Ending D — the House alone must never end a run in Corrupted Host | DONE | EditMode: a house catch is readable via `GmRunStore` |
| F7 | **Wire the wired-to-nothing systems** — audio manager now actually plays clips, pause-menu tabs now render real content, credits UI now reads the real catalog. Save/load and scene-transition wiring are still open: there is no boot/title scene to call `GmSaveSystem.Load()` or instantiate `GmSceneDirector` from, and auto-resume vs. explicit Continue is a UX call | PARTIAL | one test per system asserting a **production** call path exists |
| F8 | **Attribution gate & records** — `CREDITS.txt` and `assets/sfx/license.txt` tracked in git; `npm run verify:attribution` green (5 CC-BY entries accounted for) and wired into `npm run gates` | DONE | `npm run verify:attribution` green **and** invoked by gates |
| F9 | **The verification harness is itself defective** — 86 confirmed findings in `scripts/`. `verify-unity-full.mjs` throws an uncaught `TypeError` on every run and has never once passed; ~12 more capture/verify scripts collect real error evidence and never check it | TODO | each script fails when given a known-bad input |
| F10 | `scan-frame-defects.mjs` counts undecodable frames as clean, and treats a missing target directory as zero frames | TODO | corrupt PNG in a target dir fails the run |

**Gates 3-12 have never run against a compiling build.** `npm run gates` is the next real step —
it will be the first full pipeline attempt against a tree that actually compiles.

## LANE A — Phase 0 exit (blocking everything)

Phase 0 must close before Court by project rule, and Nick's walk is the gate.

```
Phase 0 Prologue        [██████████████████░░]  92%
```

| # | Task | Status | Bar | TEST | VIS |
|---|---|---|---|---|---|
| A1 | **Indoor/outdoor visibility rule for `HouseBeginning`** — replace the blanket culling exemption (`GmWendRuntimeCulling.cs:39,47`) with a real rule; ~12 soft-shadow point lights currently live across all 435 outdoor m | RED | `[░░░░░░░░░░░░░░░░░░░░]` 0% | `npm run unity:proof:walk` p95 ≤16.7ms | `walk-*.png` contact sheet — no popping at the manor approach |
| A2 | **Stop `GmWendPerformance` excluding the interior** (`:30`) — the audit structurally cannot catch this regression class again | TODO | `[░░░░░░░░░░░░░░░░░░░░]` 0% | new EditMode test: audit sees `HouseBeginning` renderers | — |
| A3 | **Standalone p95 back under budget** | RED | `[░░░░░░░░░░░░░░░░░░░░]` 0% | `npm run unity:proof:mac` p95 ≤16.7ms | 7 composition frames clean |
| A4 | **Gate piers render as material, not voids** — `GmWendOpening.cs:447` primitive cube at Metallic 0.42; measured RGB (1.1,0.0,0.8) std 0.75 vs adjacent wall (17.9,9.9,8.3) std 16.6 under the same lamp | TODO | `[░░░░░░░░░░░░░░░░░░░░]` 0% | EditMode: no unlit primitive in the gate rig | `tour-02-gate.png` — piers read as iron |
| A5 | **Cemetery gets a real built-player frame** — `05-cemetery-composition.png` is authored to look at the *chapel* (`GmStandaloneReviewProbe.cs:116`); rename it and add a genuine cemetery shot | TODO | `[░░░░░░░░░░░░░░░░░░░░]` 0% | proof frame count +1 | new `cemetery` frame showing markers/monument |
| A6 | Record host load + free RAM into `performance.json` so a p95 number is never ambiguous again | TODO | `[░░░░░░░░░░░░░░░░░░░░]` 0% | schema test | — |
| A7 | Salmon ground cast at arrival (R:G:B 213:131:94) — reads sunset not moonlit | NICK | `[░░░░░░░░░░░░░░░░░░░░]` 0% | — | `tour-01-arrival.png` |
| A8 | Modern SUV vs gothic estate — the oldest unresolved art mismatch | NICK | `[██████████░░░░░░░░░░]` 50% | — | `02-spawn-facing-mansion.png` |
| A9 | 4m45 pacing — tense or slow? | NICK | `[░░░░░░░░░░░░░░░░░░░░]` 0% | — | full walkthrough MP4 |
| A10 | Wind character / bell / heart / whisper / clock mix | NICK | `[███████████████████░]` 98% | — | ears only (F8 / RB compare) |
| A11 | Figure subtlety — perceptible but deniable | NICK | `[████████████████████]` 100% | pixel-delta gate | `tour-17/18` cutoff pair |
| A12 | **Nick's uninterrupted Phase 0 walk** — the exit gate | NICK | `[░░░░░░░░░░░░░░░░░░░░]` 0% | — | the whole thing, on his display |

**Already green in Phase 0** (verified 2026-08-03): Threshold Refusal 100% · 13 two-layer POIs 100% ·
5 drive beats 100% · walk bounds 100% · gate lock 100% · controller path 100% · Ninth Bell chain 100% ·
boundary walls 4/4 · route 435/435m, 0 stalls, 0 nav fallbacks.

---

## LANE R — The Reckoning: contextual bell + enterable coach house (2026-08-13)

Full design: `docs/superpowers/specs/2026-08-13-the-reckoning.md`. Addendum to the ninth-bell
spec: `docs/superpowers/specs/2026-07-17-the-ninth-bell.md`. Owner-approved direction; specific
pacing values are not.

```
Phase A (schedule core, zero geometry)   [░░░░░░░░░░░░░░░░░░░░]   0%
Phase B (coach house interior)           [░░░░░░░░░░░░░░░░░░░░]   0%
```

| # | Task | Status | TEST |
|---|---|---|---|
| R1 | Reckoning schedule core + seeding + `GmFeelConfig` fields, ships at `authority=0` (byte-identical to today) | TODO | EditMode: bound/monotonicity/seeded-replay tests; existing PlayMode `285f` assertion unchanged |
| R2 | Coach house interior, clue persistence, probe/registry/gate wiring | **BLOCKED on A1** — every additional interior room compounds the exact perf regression A1 exists to fix; do not start until A1 has a measured p95 number | `-gmOutbuildingProof` probe PASS; `npm run gates` clean x2 |
| R3 | Chapel / shed / icehouse — later slice, scope not yet decided | TODO (post-R2 walk) | — |

**F6 is closed (2026-08-14)**, so the Reckoning's optional corruption bridge (default off, routed
through `GmHouseProgress`'s 4-ceiling path) is unblocked on that front: it correctly cannot deliver
Ending D on its own, by the same confirmed-intentional design as the rest of the House chapter.

---

## LANE B — Foundations (do before any game logic)

Cheap, unblocks everything, currently scattered at the end of the roadmap.

```
Phase 5 Shards + save   [░░░░░░░░░░░░░░░░░░░░]   0%
```

| # | Task | Status | Bar | TEST | VIS |
|---|---|---|---|---|---|
| B1 | **`cheatsCaught` as shared cross-scene state** — the single hard blocker on the true ending | TODO | `[░░░░░░░░░░░░░░░░░░░░]` 0% | EditMode: catch in scene X readable in scene Y | HUD tally frame |
| B2 | Corruption / Sanity cross-scene model | TODO | `[░░░░░░░░░░░░░░░░░░░░]` 0% | EditMode state transitions | — |
| B3 | Defiance / Compliance — drives 4 of 6 endings | TODO | `[░░░░░░░░░░░░░░░░░░░░]` 0% | EditMode | — |
| B4 | Shard model + display (3 required for true ending) | TODO | `[░░░░░░░░░░░░░░░░░░░░]` 0% | EditMode collect/persist | shard UI frame |
| B5 | Save / load round-trip | TODO | `[░░░░░░░░░░░░░░░░░░░░]` 0% | round-trip EditMode test | — |
| B6 | Continue button | TODO | `[░░░░░░░░░░░░░░░░░░░░]` 0% | PlayMode boot-into-save | menu frame |
| B7 | **CC-BY attribution / credits screen** — legal ship requirement (gravyart mansion CC-BY-4.0, plus `CREDITS.txt` + `assets/sfx/license.txt` audit) | TODO | `[░░░░░░░░░░░░░░░░░░░░]` 0% | test: every sourced asset has a credit line | credits screen frame |

---

## LANE C — The three games

Parlor first: it is the complexity budget every other room is scoped against, and it supplies most
of the true ending's 8+ cheats caught.

```
Phase 1 Entry Hall      [████░░░░░░░░░░░░░░░░]  20%   interior shell EXISTS in-engine (gate 9 green)
Phase 2 Parlor          [██░░░░░░░░░░░░░░░░░░]  10%   shell built; no card game
Phase 4 Shut the Box    [█████░░░░░░░░░░░░░░░]  25%   rules 23/23; no board/AI/UI
Phase 3 Court           [░░░░░░░░░░░░░░░░░░░░]   0%   LOCKED behind Nick's Phase 0 walk
```

> **Tracker correction:** STEAM-TRACKER lists Entry Hall and Parlor at 0%. `GmHouseBeginningBuilder`
> already builds *wake vestibule + entry hall + Parlor* and gate 9 (house-proof) passes with 10
> frames. The rooms exist; the *games* do not.

### C1 — Entry Hall (Phase 1)

| # | Task | Status | TEST | VIS |
|---|---|---|---|---|
| C1.1 | Marble wake continuous from the KO (no cutscene) | WIP | PlayMode crossing → wake | wake frame |
| C1.2 | Nine portraits (Marr…Percival) | TODO | EditMode: 9 present, named | portrait wall frame |
| C1.3 | The ledger — 9 names, 9th worn illegible, blank line below | TODO | EditMode text contract | ledger close-up |
| C1.4 | Percival's nameplate worn identically (same clue twice) | TODO | EditMode | close-up |
| C1.5 | Shard #1 behind Percival | TODO | EditMode + B4 | — |
| C1.6 | 22 interior POIs, two-layer examines | TODO | PlayMode: E at each, line asserted | POI sweep |
| C1.7 | Letter reveal stage 1 — ends "a friend" | TODO | story-canon check | letter frame |

**Canon trap:** "a friend" (the letter's signer = Aldric) and "the friend in the walls" (the host
before Aldric, never resolved) are different things sharing a word. Keep distinct in every line.

### C2 — Parlor (Phase 2) — the only game that has ever been playable

| # | Task | Status | TEST | VIS |
|---|---|---|---|---|
| C2.1 | Card game core (port from `The Parlor - Playable Prototype.dc.html`) | TODO | EditMode rules parity | table frame |
| C2.2 | `gmFollow()` AI | TODO | EditMode: legal follow every trick | — |
| C2.3 | Reactive cheat — fires **only** when he cannot legally win | TODO | EditMode: never cheats from safety | — |
| C2.4 | The Read verb (the catch model every room copies) | TODO | PlayMode catch flow | Read UI frame |
| C2.5 | Tells — gold→green 1 frame, IRRITATED state | TODO | frame-accurate test | tell frame pair |
| C2.6 | Corruption tiers (starts at 1, never 0) | TODO | EditMode | — |

**Binds every room:** Aldric cheats only when genuinely about to lose. Never from safety. No exceptions.

### C3 — Shut the Box (Phase 4) — rules already 23/23

| # | Task | Status | TEST | VIS |
|---|---|---|---|---|
| C3.1 | Board + hinged tiles | TODO | EditMode build | board frame |
| C3.2 | Aldric match AI (scoped to `gmFollow()` complexity) | TODO | EditMode | — |
| C3.3 | 3 cheats: palm / false call / tamper | DONE (rules) | `npm run test:logic` 23/23 | needs UI |
| C3.4 | Hold verb + tally UI | TODO | PlayMode | tally frame |
| C3.5 | Tile-9 door → hidden room | TODO | EditMode: correct Hold opens | door frame |
| C3.6 | Loss / rematch loop | TODO | PlayMode | — |

Nine tiles map in ledger order to the nine guests. Never stated; discoverable.
**Source rule:** C# lives in `unity/shut-the-box/`, copied into Unity. Change there, verify both suites.

### C4 — Court (Phase 3) — locked behind Nick's Phase 0 walk

| # | Task | Status | TEST | VIS |
|---|---|---|---|---|
| C4.1 | Room dress, jury wall ×9, wax-seal HUD | TODO | EditMode | room frame |
| C4.2 | Role-light swing (accuser/defender) | TODO | EditMode rig state | light state pair |
| C4.3 | Evidence deck — Deck A straight vs rigged | TODO | EditMode | card frames |
| C4.4 | Gavel tarnish tell | TODO | EditMode | tarnish pair |
| C4.5 | Loseable pressure clock | TODO | PlayMode: hearing CAN be lost | — |
| C4.6 | Shard #2, mislabeled among evidence | TODO | EditMode + B4 | — |

**Protect this:** a first visit at low corruption **plays straight** — a real chance to lose honestly.
Rigging only appears once he is threatened. That is what makes the reveal land.

---

## LANE D — Late content

```
Phase 6 Hidden room     [░░░░░░░░░░░░░░░░░░░░]   0%   gated behind STB tile-9
Phase 7 Labyrinth       [░░░░░░░░░░░░░░░░░░░░]   0%   design doc BEFORE code
Phase 8 Six endings     [░░░░░░░░░░░░░░░░░░░░]   0%   none coded in any engine
```

| # | Task | Status | TEST | VIS |
|---|---|---|---|---|
| D1 | Hidden room + paneling door, Aldric's older invitation, journal fragments (one trails off) | TODO | EditMode | room frame |
| D2 | Labyrinth 7×7 maze generation — **design doc first** | TODO | EditMode determinism | maze map |
| D3 | The Huntsman — closes faster only when already near the limit | TODO | PlayMode | — |
| D4 | Mirrors (the only ones in the game) | TODO | EditMode | 3 mirror states |
| D5 | Shard #3 — found only by stopping to look | TODO | PlayMode | — |
| D6 | Six endings: Escape / Replacement / Pact / Collection / Cheat(true) / Hollow | TODO | EditMode: each reachable | 6 ending frames |
| D7 | **Verify 8+ cheats-caught is actually reachable** — reasoned plausible, never playtested; it gates the true ending | TODO | full-playthrough sim | — |

**Deliberate absence:** the Labyrinth has no catch mechanic and no tell-glint. Forcing a catch there
would resolve the ambiguity the room exists to protect.

---

## LANE E — Ship

```
Phase 9 Steam layer     [██░░░░░░░░░░░░░░░░░░]   8%
Phase 10 Ship polish    [░░░░░░░░░░░░░░░░░░░░]   0%
```

| # | Task | Status | TEST | VIS |
|---|---|---|---|---|
| E1 | **Windows build pipeline** — Windows is the Steam baseline; macOS is the dev box | TODO | Win build + proof | Win frames |
| E2 | Steamworks SDK + appid | NICK | — | — |
| E3 | Achievements (six endings are the spine) | TODO | EditMode | — |
| E4 | Cloud saves (depends on B5) | TODO | round-trip | — |
| E5 | Settings/options — port text-size + reduce-motion from web; **do not lose reduce-motion** | TODO | EditMode | options frame |
| E6 | Input rebinding | TODO | PlayMode | rebind frame |
| E7 | Accessibility pass | TODO | a11y checks | — |
| E8 | Age rating / content survey | NICK | — | — |
| E9 | Store page: capsule art, trailer, screenshots | NICK | — | — |
| E10 | Performance on target hardware (not just this M5) | TODO | hardware matrix | — |
| E11 | 9-persona panel review | TODO | — | — |
| E12 | The commit/push conversation (everything to date is uncommitted) | NICK | — | — |

**Controller support and the macOS build pipeline are already 100%** — universal signed app, virtual
gamepad proof, 2 controller UI frames.

---

## Standing verification

```bash
npm run test:fast        # 50 tests, editor-free, no Unity — the quick check
npm run gates            # THE command: all 11 opening gates, stops at first failure
npm run unity:proof:walk # full 435m route, p95 + stalls + nav fallbacks
npm run unity:proof:mac  # 7 story + 2 controller frames, 5 cues, p95
```

**Requires:** Unity closed · `node_modules` installed · host load <3.0/core · sandbox must permit `ps`.

## Current gate state (measured 2026-08-13, end of session — SUPERSEDES the 2026-08-03 block below)

```
Unity EditMode           [████████████████████]  294/296  ✓  Court's 2 tests fail on purpose (F1, hard lock)
Gates executed            [██░░░░░░░░░░░░░░░░░░]   2/12    portable ✓, EditMode ✓; 3-12 not run this session
```

**Unity EditMode compiles and runs for the first time on record for the six Phase 1-7 scene
folders** (`entry-hall`, `court`, `parlor`, `shut-the-box`, `hidden-room`, `labyrinth`). The
session opened with EditMode unable to compile at all (47 CS0117 errors, every one
`GmCompositionAuthoring.ReviewClaim(...)` — a method that didn't exist). By the end: **294 of 296
EditMode tests pass.** The two that don't are Court's, left failing on purpose — Court is
content-locked behind Nick's Phase 0 walk, and closing its composition-audit failures means
re-parenting geometry, which is content work, not a compile fix. See **F1** below.

Gates 3-12 (PlayMode, rebuild, tour, build-mac, standalone-proof, walk-proof, wall-proof,
frame-defect scan) were **not run this session** — EditMode was the blocker all the way through
until the final hour, so there was no compiling build to run them against until now. They carry no
current evidence; run `npm run gates` fresh before trusting any number below Gate 2.

18 compile errors across 6 defect classes were found and fixed this session (F2-F7), plus 4
composition-geometry passes across 5 non-locked scenes, verified against a from-scratch
replica of the audit's actual `WorldToViewportPoint` math (numbers below are load-bearing, not
eyeballed). Every claim above was read from captured run output, never a completion notice: the harness
repeatedly reported "exit code 0" on background runs while their own logs ended in a real
failure — recorded in `docs/TESTING.md`.

### Superseded — measured 2026-08-03

```
Gates executed          [████████████████████]  11/11
Gates passing           [██████████████████░░]   9/11   8 and 10 fail p95 only
```

1 portable ✓ · 2 EditMode 204/204 ✓ · 3 PlayMode 12/12 ✓ · 4 rebuild ✓ · 5 contract ✓
`460221080faabbff` · 6 tour 8/8 ✓ · 7 build ✓ · **8 standalone ✗ p95** · 9 house 10 frames ✓ ·
**10 route ✗ p95 20.35/16.70** · 11 walls 4/4 ✓

## Hard locks (never violate)

- No commit, no push, no purchases without Nick's explicit word.
- No second mansion. The coaching-inn history is canon — never "saloon".
- Threshold Refusal stays closed-door; the front doors never open in the Prologue.
- Never hand-edit `.unity` / `.prefab` YAML — the C# builder is the authored source.
- Never overwrite Nick's calibration (display level, sensitivity).
- Aldric cheats only when genuinely about to lose. Never from safety.
- Fixed exposure, never automatic.
- Taste calls leave the loop and go to Nick with evidence attached.
