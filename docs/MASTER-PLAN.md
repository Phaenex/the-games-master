# The Games Master — master build plan

Written 2026-08-15, replacing the phase order in `docs/STEAM-TRACKER.md` as the *build* sequence.
STEAM-TRACKER remains the delivery-truth ledger (what % is actually done); this is the order to do it
in and how each piece is proven.

---

## Why the order changed

The old order is the story's order: Prologue → Entry Hall → Parlor → Court → Shut the Box → Shards →
Hidden Room → Labyrinth → Endings → Steam. That is the order a *player* meets the game. It is the
wrong order to *build* it, and this project has the scars to prove it:

- **Nothing has ever been played end to end.** Not once, in any engine. Every quality judgment in
  every design doc is therefore a guess with a citation.
- **No ending has ever resolved.** `GmEndingManager.ResolveEnding()` is only reachable from
  `GmSceneDirector.ResolveAndShowEnding()`, which has zero callers, because `GmSceneDirector` is
  never instantiated, because there is no boot scene to instantiate it from. Endings are Phase 8 —
  last — so this was never going to surface until the end.
- **Six rooms have real, tested game logic and no player in them.** Court's trial, the Parlor's
  trick-taking with a cheating AI, Shut the Box's dice/tile rules, the Hidden Room's shard mechanic,
  the Labyrinth's chase AI — all real, all unit-tested, all unreachable. None of those scenes
  contains a `GmPlayer` or a camera rig.
- **The true ending is blocked by Phase 5, scheduled after Phases 1-4.** It needs three shards that
  span the Prologue, Court and the Hidden Room, plus a save that survives a scene change.
- **The shipping scene is the least-verified one in the repo.** All six scaffold scenes have a
  `CompositionPlan` and are validated by `GmSceneCompositionAudit` — the tool that found six real
  defects in them on 2026-08-15. `wend-hill-prologue` has no plan and has never been passed to it.

The corrected principle: **build the spine before the body, and the instrument before either.** A
room you cannot walk into is not 0% built, it is 100% built and 0% reachable — and that distinction
is invisible on a phase bar, which is why it survived this long.

---

## Standing rules — apply to every chunk below, no exceptions

Every chunk in every phase follows the same five beats. A chunk is not done until beat 5.

| Beat | What | Gate |
|---|---|---|
| 1 **Build** | Write it. Authored source only — never hand-edit `.unity`/`.prefab` YAML; changes to serialized assets go through Editor scripts. | — |
| 2 **Prove** | A test that fails without the change and passes with it. Break it on purpose once and watch it go red. | test exists and was seen red |
| 3 **Run** | `npm run test:fast` always; `npm run gates` before reporting any content work done. | real exit code read, not a wrapper's |
| 4 **Review** | Adversarial pass: what could this be wrong about? For UI/feel, screenshots at full size, classified PASS/BORDERLINE/FAIL. For anything player-facing, a panel review. | findings ranked HIGH/MED/LOW |
| 5 **Verify twice** | Fix HIGH, re-run; fix MED, re-run. Done = **two consecutive clean runs**. A run that passed only on retry is FLAKY, not green. | two clean runs logged |

**Three permanent hazards, learned the expensive way** (full list: `docs/TESTING.md`):
- A test that asserts an *instantaneous* outcome encodes a bug as a requirement. If a fix breaks a
  test, decide which one describes the intent before touching either.
- Absence of evidence must be the failure condition. A check that passes on missing input is theater.
- Measure before naming a cause. Every confident story told ahead of evidence has had to be retracted.

**What never leaves my hands:** purchases, canon changes, taste calls, and the human gates —
physical-controller feel, display brightness, audio character and loudness balance, figure subtlety,
pacing, fear. Those go to Nick with evidence attached.

---

# PHASE A — The spine (makes the game playable end to end, even while empty)

**Why first:** every one of these is a dependency of something already built. Until they exist, six
finished rooms stay unreachable and no ending can resolve. This is the phase that converts "systems
that pass tests" into "a game."

### A1 · Boot/title scene
The single highest-leverage missing object in the project.
- **Build:** a `Boot` scene: title, New Run, Continue (disabled with no save), Quit. Instantiates
  `GmSceneDirector` with `DontDestroyOnLoad`, calls `GmSaveSystem.Load()` on Continue, calls
  `GmRunStore.BeginNewRun()` on New Run.
- **Prove:** PlayMode — Continue is disabled with no save file; New Run enters the Prologue with a
  cleared store; Continue restores a written save and lands at `GmRunStore.CurrentSceneId`.
- **Unblocks:** A2, A3, A4, every ending, save/load, and the ability to play the game at all.
- **Nick:** auto-resume vs explicit Continue is a UX call.

### A2 · One player rig, six scenes
- **Build:** extract the `GmPlayer` + `CharacterController` + camera + `GmInteractionScanner` rig
  from `GmWendBuilder` into a shared builder helper. Place it in entry-hall, parlor, court,
  shut-the-box, hidden-room, labyrinth, each at an authored spawn.
- **Prove:** EditMode per scene — exactly one `GmPlayer`, spawn is on the navmesh/floor, camera is
  `Camera.main`. PlayMode — the player can move and interact in each.
- **Unblocks:** every room's mechanic becomes reachable for the first time; `GmPauseMenu` gains
  somewhere to appear.

### A3 · Scene transitions wired
- **Build:** place `GmSceneTransitionTrigger` volumes at the real doorways: Prologue→Entry Hall (the
  crossing already does this), Entry Hall→Parlor, Parlor→Court, Court→Shut the Box, Shut the
  Box→Hidden Room (tile-9 gated), →Labyrinth. Write `GmRunStore.CurrentSceneId` on every transition
  (today it is never assigned, so every save records a stale scene).
- **Prove:** PlayMode — walking each trigger loads the next scene with run state intact; the saved
  `currentSceneId` matches where the player actually is.

### A4 · Ending resolution reachable
- **Build:** call `ResolveAndShowEnding()` at the real end-of-run points; build the ending screen.
- **Prove:** PlayMode, one test per ending, each driving *real* state rather than poking the store:
  A True Escape (3 shards + 8 catches), B Defiant Sacrifice, C Host Succession, D Corrupted Host
  (tier 5), E Madness (sanity 0), F Trapped Loop. Plus a priority-collision test — today every
  ending test triggers exactly one condition in isolation, so a swapped branch would ship.

### A5 · Save/load round trip
- **Build:** save on transition and on ending; load from Boot.
- **Prove:** write → quit → load → every `GmRunStore` field matches, including `LastCheckpoint` and
  `CurrentSceneId`, which today have no production writers at all.

**Phase A exit gate:** a headless full-run added to the gates table that plays Boot → Prologue →
Entry Hall → Parlor → an ending, asserting nine tolls, crossing entered, and an ending resolved,
across **three walk profiles**: obedient straight line, full-grounds detour, and lateral fence
bypass. This is the run that proves the game exists.

---

# PHASE B — Verification parity (aim the instrument at what ships)

**Why here:** Phase C is content work, and content work without this phase is what produced a
1.9m-buried gate and a floating mirror in scenes nobody ships — while the scene that *does* ship went
unchecked.

### B1 · CompositionPlan for `wend-hill-prologue`
- **Build:** `GmWendPrologueCompositionPlan.cs` declaring zones/clusters/elements for the drive, the
  gate, the porch, the wake room, the entry hall and the parlor interior, with `surfaceY` on every
  grounded element and authored intent on every local light.
- **Prove:** the scene passes `GmSceneCompositionAudit`. **Expect this to go red first** — that is
  the point. It is the only way to answer "is the wake room right?"
- **Note:** this directly answers a question that is currently unanswerable.

### B2 · Close the collision asymmetry
`GmWendPerformance` deletes colliders by name token ("wall", "door", "house", "fence") and
`GmWendSceneContract` *fails the build* if purchased doorway colliders still seal the route — hard
automated pressure to remove collision, with **no counterpart check that anything is still solid**.
That asymmetry is the machine that produced the walk-through house.
- **Build:** a contract assertion that every named barrier stops a `CharacterController`.
- **Prove:** wire `GmPhysicalIntegrityProbe.SweepLateralBypass` (already written, still uncalled)
  against the porch face, the gate, and each boundary wall, at offsets **beyond** the barrier's own
  width — the bypass lives where nobody authored geometry.

### B3 · Stop the audit excluding the interior (`A2` in the old lane)
`GmWendPerformance:30` skips `HouseBeginning`, so the audit structurally cannot catch the regression
class that caused the current perf wall. Remove the exclusion; add an EditMode test that the audit
sees `HouseBeginning` renderers.

### B4 · Fix the harness's remaining theater
`verify-gate-stars.mjs:97` prints `PASS` unconditionally. `verify-unity-full.mjs:260` asserts a value
it wrote itself. 51 of 68 scripts in `scripts/` are unreachable from `package.json`, including every
`verify-*.mjs`. **Decide wire-in vs delete before investing another hour in their assertions.**

---

# PHASE C — Content, room by room (now that rooms are reachable)

Each room repeats the same chunk shape. Listed once, applied to all six.

> **Per-room chunk shape**
> 1. **Spawn & shell** — player lands somewhere authored; room reads as a place, not a box.
> 2. **CompositionPlan** — grounded elements, light intent, review shots. Red first, then green.
> 3. **Mechanic wiring** — the existing tested controller gets real input and real UI.
> 4. **Cheat + tell** — the room's signature cheat, catchable with the core verb.
> 5. **Clue/shard** — what it plants and what it pays off.
> 6. **Review** — panel pass on the room as an experience, then fix, then two clean runs.

### C1 · Entry Hall *(Phase 1)* — the hub
Nine portraits, the ledger, Shard #1 behind Percival. **Today Shard #1 is placed as a prop with no
collection code anywhere** — the True Escape ending is unreachable because of it. Wire it first.
Reduce the parlor gate from "ledger + 3 portraits" if pacing (see E1) says so.

### C2 · Parlor *(Phase 2)* — the first real game
The most complete logic in the project: full 4-suit trick-taking, legal-follow enforcement, a host AI
that only cheats when about to lose a match-point trick, and the Read's time-dilation. It needs card
UI, hand rendering, and input. **The card deck is currently a `CreatePrimitive` cube.**

### C3 · Court *(Phase 3)* — the trial
Real phase machine and evidence logic already tested. Needs the Court composition wiring finished
(lock lifted 2026-08-15), the gavel, and Shard #2's collection path.

### C4 · Shut the Box *(Phase 4)* — dice and the tile-9 door
Rules engine is the deepest in the project (420 lines, own test suite, 23/23). Needs the board, the
dice, the AI turn, the UI, and the tile-9 Hold that unlocks the Hidden Room — **today that unlock
sets a bool and moves no geometry.**

### C5 · Hidden Room *(Phase 6)* — the mirror
Shard #3, the original invitation, the eight journals. Re-entry safety is already carefully designed
and tested. Needs the recess door to actually open.

### C6 · Labyrinth *(Phase 7)* — the chase
Huntsman AI is a real state machine. The "generator" is a fixed pattern with an unused seed — decide
whether it stays authored or becomes procedural before building content on top of it.

---

# PHASE D — The systems that span rooms

### D1 · Three shards, one true ending
Shard #1 (Entry Hall) has no collection path; #2 (Court) and #3 (Hidden Room) call
`GmRunStore.CollectShard` directly. Unify, and prove `AllShardsCollected` can become true *through
real play* — today it cannot.

### D2 · Corruption schedule
The mechanism exists and is clamped and tested; **what raises the tier is deliberately undecided.**
Parlor/Court/Shut the Box already call `RaiseCorruption` uncapped; the House caps at 4 so it cannot
deliver Ending D alone (confirmed intentional, 2026-08-15). **Nick:** the schedule itself.

### D3 · The six endings, authored
Text, art and the conditions each reads. Blocked on A4 for reachability, D1 for the true ending.

### D4 · Sanity, symptoms and the second channel
The count has one output channel and `GmSymptoms` filters it — fixed for the bell 2026-08-15, but the
wider rule stands: every critical beat needs a non-audio channel. Wire `Reduce Motion` and
`Text Scale` to real consumers or remove them from the menu, because a settings toggle that does
nothing is worse than an absent one.

---

# PHASE E — Pacing and the shape of the opening *(runs alongside C, decided by Nick)*

### E1 · Time to first card — the open question
Measured today: the bell floor alone is **4m45s** (`firstTollDelay 45s + 8 × 30s`). Then the wake
room, then the Entry Hall gated on the ledger **and** three portraits, then **nine** intro lines of
rules, then a hand. Realistically **8-12 minutes to the first card.**

The first *decision* is now ~1 minute in (the gate card tell, built 2026-08-15). The first *card* is
not. Three independent levers, in the order I would pull them:

1. **Deal the rules instead of reciting them.** Nine exposition lines before a single card is the
   most conventional thing in an otherwise unconventional opening. Teach the four suits by playing
   the first trick. Cheapest, touches no canon.
2. **Halve the Entry Hall gate** — ledger + one portrait rather than three.
3. **The 285s bell floor** — this is A9, an explicit Nick call, and the one I would touch last
   because the count *is* the opening.

### E2 · Aldric's presence beyond the card
He now leaves one slip on the grounds. The panel's note stands: across ten minutes he is otherwise a
countdown timer wearing a bell. More hand, still no face.

---

# PHASE F — Ship

**F1** Steam integration (Windows build, achievements, cloud saves) · **F2** controller parity ·
**F3** accessibility pass against real consumers · **F4** perf under budget on a quiet host ·
**F5** attribution/credits verified in the built player, not just in the record ·
**F6** the Phase 0 walk and every human gate, on Nick's display.

---

## Current state, honestly (2026-08-15)

| | |
|---|---|
| Gates | 1-7 **PASS** (3-7 for the first time ever); 8 FAIL p95 19.25ms vs 16.70 — *best on record*, not a new regression; 9-12 unreached |
| EditMode | 337/339, the two being Court's, whose lock is now lifted |
| PlayMode | 12/12 |
| Playable end to end | **No.** No boot scene, no player in six rooms, no ending resolves |
| Phase 0 | not "92%" — six objective LANE A items still at 0%, including gate piers rendering as black voids and a "cemetery" proof frame that points at the chapel |
| Wake room | **unknown** — it lives in the one scene with no CompositionPlan |

## The order, in one line

**A** make it playable → **B** aim the instrument at what ships → **C** fill the rooms →
**D** join them up → **E** decide the pacing → **F** ship.

Everything before A1 was building rooms nobody could enter.
