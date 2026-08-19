# The Games Master — Master Taskboard

> **The running board.** Every phase, every task, in execution order, with the test that proves it and
> the visual evidence that proves it *looks* right. Two separate columns on purpose: this project has
> repeatedly shipped green tests over broken frames.
>
> Established 2026-08-03. Ordered by **dependency**, not by phase number — see "Why this order".
> Delivery bars live here and in `STEAM-TRACKER.md`; this file carries the task-level detail.

> **Current authority, 2026-08-17/18:** this chat and `docs/HANDOFF-2026-08-17.md`. Active queue is
> House Memory remainder (Ledger Review, Restore Last Valid, Export Diagnostics) then five missing
> table games. Hub rooms exist. Spawn → vault → back onto the hall is EditMode-proven.
> Parlor 1080p series **3/3 + coverage** qualified. Unreadable House envelope recovery is
> EditMode-proven. Lane A's human gates remain. The 08-16 handoff is the last full opening-gate pack.

## How to read and update this

- Bars are 20 chars, weighted delivery estimates, **not** test scores.
- `TEST` = the command whose exit code decides it. `VIS` = the frame(s) a human reads at full size.
- A task is **not done** until both columns are satisfied and `npm run gates` is green.
- Never mark a row done from tests alone. Never inherit a grade — re-verify.
- Status: `TODO` · `WIP` · `RED` (built but failing) · `DONE` · `NICK` (human gate, agent cannot clear)

```
Overall (Unity/Steam)   [██████░░░░░░░░░░░░░░]  scene spine and opening proof complete; game content remains
```

## Why this order (four deliberate departures from phase numbering)

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
Gate defect confirmed          [████████████████████] 100%  DONE — rebuilt and adversarially proven
Sweep for sibling defects       [████████████████████] 100%  DONE 2026-08-15 — found worse than the gate
G0 house collision              [████████████████████] 100%  DONE — authored shell rebuilt and proven
```

| # | Task | Status | TEST |
|---|---|---|---|
| **G0** | Estate house collision shell, including Threshold Refusal at the porch face. | DONE 2026-08-15 | rebuilt saved-scene contract plus house/porch physical proof |
| **G0b** | Counterpart guards for named barriers, so collision-carving pressure cannot silently erase required solids. | DONE 2026-08-15 | contract and adversarial physical proof both go red on missing solids |
| G1 | Audit every "locked/blocked/closed" claim for matching physical enforcement. The sweep found the missing house shell, short gate wings and route obstructions that G0-G5 subsequently closed. Scaffold door flags now lead into production scene transitions; they are not evidence of animated physical doors. | DONE 2026-08-15 | findings recorded and shipping-opening barriers adversarially re-proven |
| G2 | Estate gate barrier plus terrain-following perimeter wings reaching the map boundary. | DONE 2026-08-16 | wall proof holds both ±55 m attacks |
| G3 | Reusable adversarial wall/bypass probe across gate wings and all four map boundaries. | DONE 2026-08-16 | 6/6 physical captures, zero integrity failures |
| G4 | Former ~158 m route stall. | DONE 2026-08-16 | 435/435 m, 43 NavMesh segments, 0 stalls/fallbacks |
| G5 | Former coach-house route stall. | DONE 2026-08-16 | 435/435 m, 43 NavMesh segments, 0 stalls/fallbacks |

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

## LANE F — Audit blockers (2026-08-13, resolved 2026-08-16)

```
Unity EditMode           [████████████████████]  455/455
Opening gates            [████████████████████]   14/14, 0 failed, 0 skipped
```

| # | Task | Status | TEST |
|---|---|---|---|
| F1 | **Review-claim authoring API mismatch.** All six generated rooms now use the supported composition API and pass their strict plans. | DONE | current EditMode suite and scene-system contract |
| F1b | **Court composition wiring.** Markers are attached to real geometry, light intent is authored and review framing passes. | DONE | `GmCourtBuildTests` |
| F2 | Duplicate `GmParlorRules` class (prologue vs parlor) | DONE | verified: EditMode compiles past CS0101 |
| F3 | `GmShutTheBoxController` → nonexistent `ShutBoxRules` methods | DONE | controller rules calls and Hold paths compile and pass focused tests |
| F4 | `GmWendOpening.RoadSurfaceY` missing; HDRP `lightTypeExtent`/`LightUnit` namespace issues (6 call sites) | DONE | verified compiles |
| F5 | `System.Linq` missing in 3 scene test files; 6 `BuildTests.cs` files missing scene teardown | DONE | verified: EditMode 455/455 |
| F6 | **Bridge `GmHouseProgress` into `GmRunStore`** | DONE — bridged; the house-caps-at-4-vs-store-caps-at-5 gap is confirmed intentional pacing, not a conflict (Nick, 2026-08-14): Parlor/Court/Shut the Box call `GmRunStore.RaiseCorruption` directly, uncapped, and are what can carry a run to Tier 5/Ending D — the House alone must never end a run in Corrupted Host | DONE | EditMode: a house catch is readable via `GmRunStore` |
| F7 | **Wire the wired-to-nothing systems.** Audio, pause tabs, credits, explicit Continue, save/load, the persistent scene director and production transitions all have callers and tests. | DONE | full PlayMode scene-chain proof plus focused EditMode tests |
| F8 | **Attribution gate & records** — `CREDITS.txt` and `assets/sfx/license.txt` tracked in git; `npm run verify:attribution` green (5 CC-BY entries accounted for) and wired into `npm run gates` | DONE | `npm run verify:attribution` green **and** invoked by gates |
| F9 | **Shipping verification harness.** Every production gate has reachable failure handling; its meta-gate runs paired bad/good inputs. Unreachable legacy scripts are archive debt, not evidence. | DONE | 22/22 paired reject/accept cases |
| F10 | `scan-frame-defects.mjs` rejects undecodable frames and missing target directories. | DONE | corrupt and absent fixtures both observed failing in the meta-gate |

The gate-2 deadlock and the composition failures are closed. The current production command reaches
all 14 gates and reports 0 failed and 0 skipped. Historical failures remain below only as incident
records; they are not the current state.

## LANE A — Phase 0 exit (blocking everything)

Phase 0 must close before Court by project rule, and Nick's walk is the gate.

```
Phase 0 Prologue        [███████████████████░]  objective gates green; Nick walk pending
```

| # | Task | Status | Bar | TEST | VIS |
|---|---|---|---|---|---|
| A1 | **Indoor/outdoor visibility rule for `HouseBeginning`** — runtime culling now derives the interior bounds, hides 233 interior renderers/lights outdoors, and reveals them on crossing/proximity without touching the estate | DONE | `[████████████████████]` 100% | route p95 11.36ms; standalone p95 10.25ms | current walk/porch evidence clean; no approach pop flagged |
| A2 | **Stop `GmWendPerformance` excluding the interior** — the performance pass now audits all 233 HouseBeginning renderers, preserves them for `GmWendRuntimeCulling`, and the saved-scene contract rejects any pre-disabled interior renderer | DONE | `[████████████████████]` 100% | regression seen red; EditMode 455/455; scene contract 20/20 | — |
| A3 | **Standalone p95 back under budget** | DONE | `[████████████████████]` 100% | final built-player proof p95 10.25ms at 0.48 load/core | 8/8 composition frames; 11/11 standalone captures clean |
| A4 | **Gate piers render as material, not voids** — piers are authored combined masonry meshes using the owned Victorian mantel-stone surface, with separate wrought-iron leaves and terrain-following wings | DONE | `[████████████████████]` 100% | saved gate rig audited; wall proof 6/6 | current gate evidence passes the exterior scanner |
| A5 | **Cemetery gets a real built-player frame** — the eight-frame player proof now has separate chapel and cemetery views; deterministic marker dressing, a measured 2.6 m owned cross monument and a protected sightline make the ground read as a cemetery | DONE | `[████████████████████]` 100% | standalone proof 8/8; EditMode 455/455; scene contract 20/20 | `06-cemetery-composition.png` inspected directly; 11/11 standalone frames pass the night scanner |
| A6 | Record host load + free RAM into `performance.json` so a p95 number is never ambiguous again | DONE | `[████████████████████]` 100% | report contains one-minute load, core count, load/core and free-memory MB; loaded-host timing is rejected | final standalone: 0.48/core, 3496MB free |
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
| R2 | Coach house interior, clue persistence, probe/registry/gate wiring | TODO after R1; the former A1 performance blocker is closed | `-gmOutbuildingProof` probe PASS; `npm run gates` clean x2 |
| R3 | Chapel / shed / icehouse — later slice, scope not yet decided | TODO (post-R2 walk) | — |

**F6 is closed (2026-08-14)**, so the Reckoning's optional corruption bridge (default off, routed
through `GmHouseProgress`'s 4-ceiling path) is unblocked on that front: it correctly cannot deliver
Ending D on its own, by the same confirmed-intentional design as the rest of the House chapter.

---

## LANE B — Foundations (do before any game logic)

Cheap, unblocks everything, currently scattered at the end of the roadmap.

```
Phase 5 shared state    [████████████████████]  100%  core state, save, Continue and credits shipped
```

| # | Task | Status | Bar | TEST | VIS |
|---|---|---|---|---|---|
| B1 | **`cheatsCaught` as shared cross-scene state** | DONE | `[████████████████████]` 100% | catch in one system is readable in the shared run store | HUD tally covered |
| B2 | Corruption / Sanity cross-scene model | DONE | `[████████████████████]` 100% | EditMode state and clamp tests | — |
| B3 | Defiance / Compliance — ending inputs | DONE | `[████████████████████]` 100% | EditMode state and ending-priority tests | — |
| B4 | Shard model + display (3 required for true ending) | DONE | `[████████████████████]` 100% | collect, persist and all-three state tests | shard tally covered |
| B5 | Save / load round-trip | DONE | `[████████████████████]` 100% | exact-state round-trip test | — |
| B6 | Continue button | DONE | `[████████████████████]` 100% | unavailable/valid/invalid-save paths tested | boot menu frame |
| B7 | **CC-BY attribution / credits screen** | DONE | `[████████████████████]` 100% | attribution gate and production catalog screen | credits UI covered |

---

## LANE C — The three games

Parlor first: it is the complexity budget every other room is scoped against, and it supplies most
of the true ending's 8+ cheats caught.

```
Phase 1 Entry Hall      [██████████████████░░]  90%   Sessions 1–6 proven (foyer through remaining game doors); POI examines remain
Phase 2 Parlor          [██████████░░░░░░░░░░]  50%   1080p 3/3 + coverage qualified; Aldric proxy; lantern-pool visual PARTIAL
Phase 4 Shut the Box    [█████░░░░░░░░░░░░░░░]  25%   rules 23/23; no board/AI/UI
Phase 3 Court           [░░░░░░░░░░░░░░░░░░░░]   0%   LOCKED behind Nick's Phase 0 walk
```

> **Tracker correction:** Unity Entry Hall is a walkable hub with parlor, Court, STB, and Hidden Room
> doors (90%). That is not Court gameplay. Parlor is 50%: editor table plus a qualified 1080p
> player series. Still not a finished AAA sitting. The other *games* still do not exist as playable matches.

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
| **H1** | Walkable hub Session 1: doors, stairs, 2F, Percival | DONE 2026-08-17 | EditMode 906/906, CC climb/block, audit, 12/12 tour (FLAKY retry) | `docs/playtest/screenshots/entry-hall-tour-*.png` (clock now dark wood; 09 orange-cast is pre-existing 5.37) |
| **H2** | North Library + Weighted Shelf | DONE 2026-08-17 | EditMode 916/916 (aisle walk, shelf→lever, furnished room, 14-shot tour contract); rebuild+audit PASS; GUI tour **14/14 first attempt** | `docs/playtest/screenshots/entry-hall-tour-13-library-interior.png`, `entry-hall-tour-14-weighted-shelf.png`. Inscription reads LTR after TextMesh +90. Shot 09 lintel gap is a leftover Session 1 visual, not this row. |

| **H3** | 2F debtor gallery + Marr study + barred guest | DONE 2026-08-17 | EditMode 922/922 (foyer still owns shard, 2F hang, Marr walk-after-key, barred body blocked); rebuild+audit PASS; GUI tour **17/17 first attempt** | `docs/playtest/screenshots/entry-hall-tour-15-upper-gallery.png`, `16-marr-study.png`, `17-barred-guest.png`. Foyer portraits were extended, not moved. |
| **H4** | Attic loft: hatch, 0.4 m-legal ladder, rafters, crates, dormer, Mirror Shard II | DONE 2026-08-18 | EditMode **927/927** (climb after Marr-desk key, locked hatch holds, loft furnished); rebuild+audit PASS; GUI tour **20/20 first attempt** | `docs/playtest/screenshots/entry-hall-tour-18-attic-hatch.png`, `19-attic-loft.png`, `20-attic-shard.png`. Shot 09 lintel void closed with a hall-side beam. Hub campaign puts Shard II in the attic until Court exists. |
| **H5** | Cellar + vault: lever-gated well, barrels, braziers, iron grate → Hidden Room | DONE 2026-08-18 | EditMode **945/945** including spawn → grate → climb-out onto HallFloorEast; rebuild+audit PASS; GUI tour **25/25 first attempt** | `docs/playtest/screenshots/entry-hall-tour-21-cellar-panel.png`, `22-cellar-descent.png`, `23-cellar-vault.png`. Shot 22 looks down the well; stairs read better from shot 07. Visual still PARTIAL. |
| **H6** | Court + Shut the Box hub doors + one campaign walk | DONE 2026-08-18 | EditMode **935/935** (hearing door after parlor, quieter hall after Court, conservatory still barred, campaign walk to parlor); rebuild+audit PASS; GUI tour **25/25 first attempt** after a lighting fix | `docs/playtest/screenshots/entry-hall-tour-24-court-door.png`, `25-stb-door.png`. Doors load existing scenes. Not Court trial content. Conservatory stays barred. |

**Hub campaign leftover:** none. H1–H6 are a climbable foyer, furnished library, 2F wing, keyed attic loft, lever-gated cellar with a proven return to the hall, and remaining game doors. Conservatory stays barred. Parlor editor shipping proof is done. Built-player 1080p series **3/3 + coverage** qualified. Court gameplay stays behind Nick's Phase 0 walk.

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
| **P1** | Editor shipping proof: HUD, dual-channel tells, restore, presentation audit, 24-shot tour | DONE 2026-08-18 | EditMode **944/944**, PlayMode **44/44**, rebuild+audit-saved, tour **24/24 first attempt** after evidence isolation | `docs/playtest/screenshots/parlor-tour-*.png`. Aldric is still the substitute proxy. |
| **P2** | Built-player 1080p series: 3 consecutive reps + honest/HC200 coverage | DONE 2026-08-18 | `npm run unity:proof:parlor` 3/3 + coverage, p95 8.10–8.26ms, host 0.54–0.67/core, app `cb62fa53…` | `docs/playtest/screenshots/parlor-1080p-*.png`. Visual PARTIAL (lantern pool). |
| **P3** | House recovery: unreadable envelope, isolated New Run, confirmed Reset | DONE 2026-08-18 mechanical | EditMode **949/949** including `UnreadableEnvelope…`, `UnreadableHouseDomain…`, `BootRecoveryNewRun…` | Title recovery copy not shot this session. Ordinary New Run stays clean when readable. |

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
Phase 8 Six endings     [████████░░░░░░░░░░░░]  resolver and reachable trigger shipped; authored presentation remains
```

| # | Task | Status | TEST | VIS |
|---|---|---|---|---|
| D1 | Hidden room + paneling door, Aldric's older invitation, journal fragments (one trails off) | TODO | EditMode | room frame |
| D2 | Labyrinth 7×7 maze generation — **design doc first** | TODO | EditMode determinism | maze map |
| D3 | The Huntsman — closes faster only when already near the limit | TODO | PlayMode | — |
| D4 | Mirrors (the only ones in the game) | TODO | EditMode | 3 mirror states |
| D5 | Shard #3 — found only by stopping to look | TODO | PlayMode | — |
| D6 | Six endings: resolution logic and production trigger are reachable; authored ending presentation remains | WIP | EditMode branches + PlayMode ending trigger | 6 ending frames still needed |
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
| E12 | The commit/push conversation (64 commits are local; the current continuation is uncommitted) | NICK | — | — |

**Controller support and the macOS build pipeline are already 100%** — universal signed app, virtual
gamepad proof, 2 controller UI frames.

---

## Standing verification

```bash
npm run test:fast        # portable editor-free quick suite
npm run gates            # THE command: all 14 opening gates, stops at first failure
npm run unity:proof:walk # full 435m route, p95 + stalls + nav fallbacks
npm run unity:proof:mac  # 8 composition + 3 controller-state captures, cues + p95
```

**Requires:** Unity closed · `node_modules` installed · host load <3.0/core · sandbox must permit `ps`.

## Current gate state (measured 2026-08-16)

```
Unity EditMode           [████████████████████]  455/455
Unity PlayMode           [████████████████████]   18/18
Opening contract         [████████████████████]   20/20
Harness meta-gate        [████████████████████]   22/22 paired reject/accept cases
Opening gates            [████████████████████]   14/14, 0 failed, 0 skipped
```

The production command now reaches the whole stack: portable checks, EditMode, PlayMode, rebuild,
saved-scene contract, visual tour, macOS player, standalone proof, house proof, full-route proof,
physical boundary proof and frame scanners. The current run must still be read from its own log;
the historical wrapper failures are recorded in `docs/TESTING.md`.

### Historical incident record — measured 2026-08-03, do not use as current evidence

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
