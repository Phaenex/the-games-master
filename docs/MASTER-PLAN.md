# The Games Master — master build plan

Written 2026-08-15, replacing the phase order in `docs/STEAM-TRACKER.md` as the *build* sequence.
STEAM-TRACKER remains the delivery-truth ledger (what % is actually done); this is the order to do it
in and how each piece is proven.

---

## Why the order changed

The bullets below record the 2026-08-15 diagnosis that forced the reorder. Phase A and the opening
instrumentation have since shipped; the current status table at the end of this file is authoritative.

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

# PHASE A — The spine — DONE 2026-08-16

This phase now converts the tested systems into a production scene chain. It proves reachability and
state continuity. The five missing table games were approved later as Seven Debts; their runtime and
scenes remain unbuilt.

### A1 · Boot/title scene — DONE
`Boot` presents explicit Continue, New Run and Quit choices. Continue is disabled without a valid
save; New Run clears the store; Continue restores the save and resumes its recorded scene.

### A2 · One player rig, six scenes — DONE
Entry Hall, Parlor, Court, Shut the Box, Hidden Room and Labyrinth each contain one shared player
rig at an authored, audited spawn. Their scene-build tests validate camera, player and spawn state.

### A3 · Scene transitions wired — DONE
The currently built production chain runs Boot → Prologue → Entry Hall → Parlor → Court → Shut the
Box → Hidden Room → Labyrinth. Seven Debts expands the final player-facing sequence with Bones,
Study, Wager, Black Ledger, and Last Candle as specified in
`docs/SEVEN-DEBTS-CANON-2026-08-19.md`. Every successful transition writes
`GmRunStore.CurrentSceneId`; failed scene loads leave both the director and save state on the room
the player actually occupies.

### A4 · Ending resolution reachable — DONE
Labyrinth completion reaches `GmEndingTrigger`, which calls the persistent scene director's ending
resolver. Unit tests cover all six resolution branches and priority collisions; PlayMode drives the
production scene chain through an ending.

### A5 · Save/load round trip — DONE
Transitions save the entered scene, endings save final state, and Boot restores the complete run.
The exact-state round-trip covers catches, shards, corruption, sanity, defiance, compliance,
checkpoint and current scene.

**Phase A exit proof:** PlayMode drives Boot → Prologue → Entry Hall → Parlor → Court → Shut the
Box → Hidden Room → Labyrinth → ending with run state preserved. The separate built-player opening
proof covers the complete 435 m route and the adversarial wall proof covers six bypass attacks.

---

# PHASE B — Verification parity (aim the instrument at what ships)

**Why here:** Phase C is content work, and content work without this phase is what produced a
1.9m-buried gate and a floating mirror in scenes nobody ships — while the scene that *does* ship went
unchecked.

### B1 · CompositionPlan for `wend-hill-prologue`
- **DONE 2026-08-16:** `GmWendCompositionPlan.cs` declares the drive, gate, chapel, grounds, manor,
  wake/house interiors, review subjects and every enabled local light. The saved opening passes the
  strict `GmSceneCompositionAudit`; deleting intent or a visible practical source makes it fail.

### B2 · Close the collision asymmetry — DONE
The saved-scene contract now requires the house shell, porch refusal, estate gate and terrain-
following boundary barriers. The built-player wall proof attacks all four map edges and both gate
wings beyond the old ±45 m endpoints; all six attacks are stopped.

### B3 · Audit the interior that ships — DONE
`GmWendPerformance` audits all 233 `HouseBeginning` renderers, preserves them for runtime culling,
and fails if the culler is absent. The saved-scene contract rejects any interior renderer disabled
before discovery. The regression test was observed red before it passed.

### B4 · Fix the shipping harness's remaining theater — DONE
The gate harness now has 22 paired reject/accept cases, including corrupt and missing frame input,
and the production command runs all 14 gates. Unreachable legacy scripts remain historical cleanup,
not evidence and not part of the shipping gate path.

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
Nine portraits, the ledger, Shard #1 behind Percival. The shipping House path collects the shard
into shared slot 0 and the generated Entry Hall places it visibly. The remaining room-content work
is player-facing examine/pickup parity and evidence for the generated room, plus Nick's pacing call
on the ledger-and-portraits gate.

### C2 · Parlor *(Phase 2)* — the first real game
The most complete logic in the project: full 4-suit trick-taking, legal-follow enforcement, a host AI
that only cheats when about to lose a match-point trick, and the Read's time-dilation. It needs card
UI, hand rendering, and input. **The card deck is currently a `CreatePrimitive` cube.**

### C3 · Court *(Phase 3)* — the trial
The phase machine, evidence logic, composition wiring, owned gavel and Shard #2 collection path are
implemented and tested. Remaining work is the player-facing hearing UI/input, pacing and reviewed
presentation of a complete hearing.

### C4 · Shut the Box *(Phase 4)* — dice and the tile-9 door
The rules engine, board shell, controller and tile-9 catch path are implemented. Holding Tile 9 now
rotates a physical panel, removes its blocker and arms the Hidden Room transition. The remaining
content work is the player-facing dice/AI turn/UI presentation and full-match evidence.

### C5 · Hidden Room *(Phase 6)* — the mirror
Shard #3, the original invitation, the eight journals. Re-entry safety is already carefully designed
and tested. The recess door is authored open on arrival and the onward Labyrinth transition exists;
the remaining work is the player-facing journal, shard and assembled-mirror interaction flow.

### C6 · Labyrinth *(Phase 7)* — the chase
Huntsman AI is a real state machine. The "generator" is a fixed pattern with an unused seed — decide
whether it stays authored or becomes procedural before building content on top of it.

---

# PHASE D — The systems that span rooms

### D1 · Three shards, one true ending
The House, Court and Hidden Room paths bank slots 0, 1 and 2 in `GmRunStore`; collection and
`AllShardsCollected` are tested. The remaining true-ending reachability risk is earning eight catches
through authored play. Seven Debts deliberately leaves that catch economy with Parlor, Court, and
Shut the Box; the five new games add clues and run pressure without inflating the count.

### D2 · Corruption schedule
The mechanism exists and is clamped and tested; **what raises the tier is deliberately undecided.**
Parlor/Court/Shut the Box already call `RaiseCorruption` uncapped; the House caps at 4 so it cannot
deliver Ending D alone (confirmed intentional, 2026-08-15). **Nick:** the schedule itself.

### D3 · The six endings, authored
Resolution logic and the production ending trigger are reachable. Authored ending text, art, audio,
six reviewed frames and the missing table-game catch economy remain.

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

## Current state, honestly (2026-08-17)

The 08-16 gate pack still stands until a fresh `npm run gates`. Hub Session 1 is a walkable Entry Hall stub. Hub Session 2 library exists as authored C# only. See `docs/HANDOFF-2026-08-17.md`.

## Current state, honestly (2026-08-16)

| | |
|---|---|
| Gates | **14/14 PASS**, 0 failed, 0 skipped; standalone p95 10.25 ms and route p95 11.36 ms on a trusted host |
| EditMode | **455/455** |
| PlayMode | **18/18** |
| Playable end to end | Scene spine **yes**: Boot through Labyrinth to an ending. Five promised table games remain absent content |
| Phase 0 | Objective automation is green; Nick's physical/audio/feel walk is still the human exit gate |
| Wake room | Covered by the shipping opening's strict CompositionPlan and current interior evidence |

## The order, in one line

**A** make it playable → **B** aim the instrument at what ships → **C** fill the rooms →
**D** join them up → **E** decide the pacing → **F** ship.

Everything before A1 was building rooms nobody could enter.
