# Build Roadmap — From Story Bible to Playable Game

## What's actually shipped today (verified against the repo, not assumed)

- **Prologue, Entry Hall, Parlor** — playable, Test Harness passing. Entry Hall has 9 portrait POIs + ledger; Parlor has Corruption/Sanity/`cheatsCaught`/Read.
- **Shared modules** — `gm-doubt.js`, `gm-settings.js`, `gm-progress.js` exist; doubt letter stages and Options are wired; cross-scene `cheatsCaught` persistence has started.
- **Car / gate / secret ending** — coded in Prologue; Phase 0 is fixing the opening’s visual truth (tiny car, doubled gate, empty final approach, unlit facade).
- **Court, Shut the Box, Labyrinth, and the hidden room** still don’t exist as scenes.
- **Local git** — `main` may be dozens of commits ahead of `origin/main`. Do **not** push without explicit approval.

That's the real starting line. Phase 0 is mandatory before Court. After that, keep the cheapest-first room order. Endings stay last: they need real Corruption, Sanity, `cheatsCaught`, shards, and hidden-room-found.

## Playtest & panel gates (non-optional)

| Checkpoint | What |
|------------|------|
| After Phase 0 | **Nick plays** the first 5 minutes before Court starts |
| After Phase 2 | **Nick plays** + 9-persona AI panel (cross-room state first matters) |
| After Phase 7 | **Nick plays** + AI panel (endings integration) |
| Phase 9 | Final Nick play + AI panel + screenshot pass |

Do **not** re-run the full AI panel after every phase. Do **not** treat AI-only reviews as human playtesting.

## Phase 0 — Opening Rescue (FIRST)

**Why first:** the shipping first minutes are wrong — toy-scale car, doubled gate monument, dressing that dies ~18 units before the house, facade that contradicts “lit up like a birthday.” Building Court on top of that wastes the only free marketing / retention window the browser build gets.

**Plan:** `docs/superpowers/plans/2026-07-13-opening-rescue.md`

- Measure-and-fix car scale (~4.4–4.8 length, ~1.55 height) + matching fallback
- One coherent gate (sourced mesh + short closing leaves; no 12-unit arch)
- Extend trees / lamps / urns / fence into the final approach
- Warm facade glow from gate and mid-drive
- Harness: `secretEnding`, gate-lock latch, car size sanity
- Visual gate under `docs/playtest/screenshots/` — PASS/BORDERLINE/FAIL in `docs/PROGRESS.md`
- Catalog `old_car` / `graveyard_gate` with honest LICENSE UNKNOWN until attribution is proven
- **Gate out:** Nick playtest notes, then Phase 1

## Phase 1 — The Court

**Why after Phase 0:** cheapest of the three new rooms — re-dress dining hall as tribunal. Most fully-specified mechanic (evidence cards, gavel tarnish).

- Re-dress an existing manor room: candelabra → bench, chairs → jury box, table → bar.
- Jury dressing references the Entry Hall's 9 existing portraits — no new portrait assets needed.
- Evidence-card presentation mechanic + gavel-tarnish tell.
- The tiered fail-state: first visit plays straight (real evidence, real chance to lose the case); later visits, once corruption is high enough, reveal the planted-evidence rig.
- `cheatsCaught` increments locally for now (same pattern Parlor already uses) — cross-scene persistence is Phase 5, not a blocker here.
- Test Harness coverage: scene builds, evidence presentation resolves correctly, fail-state actually loseable on a first visit.

## Phase 2 — Shut the Box (replacing Gallery)

**Why second:** next in Art Direction's own build order (this is Gallery's old slot), though this design costs more than Gallery would have — a real opposing AI instead of a memory-match game with no opponent. That's an accepted, stated tradeoff (§1.7 of the per-game-cheats doc), not an oversight.

- Room: the old Gallery hall, dressed with a dice/tile table instead of portrait-matching.
- Two-box shut-the-box game logic + AI opponent, scoped to match `gmFollow()`'s existing complexity in Parlor, not exceed it.
- Tile-to-ledger reskin (tiles 1-9 map to Marr/Dufresne/Pike/Hale/Gall/Quill/Thale/Aubrey-Locke/Percival, in order).
- Three cheat types: palming, false calls, board tampering — plus the Hold catch action and its live on-screen tally.
- The hidden-room door trigger: a correct Hold catching board-tampering specifically on tile 9.
- Test Harness coverage: both boxes resolve correctly, Hold correctly flags real cheats and correctly penalizes false calls, tile-9 gate fires under the right condition and only that condition.
- **Gate out:** Nick playtest + AI panel.

**Known open item carried into this phase:** Gallery's secret young-Aldric portrait has no home in this design. Decide during this phase whether it gets folded in somewhere (an examine point in the old Gallery hall, now repurposed) or is formally dropped.

## Phase 3 — Shards and clue wiring

**Why here, not earlier or later:** two of the three shard locations depend on rooms that don't exist until Phases 1-2 land (Court, and eventually Labyrinth in Phase 6). The Entry Hall shard can technically be added any time after Phase 0, since that scene already exists — worth doing opportunistically alongside Phase 1 rather than waiting.

- Entry Hall: mirror shard behind Percival's portrait (new examine point on an existing scene — cheap).
- Court: mirror shard among the Evidence Cards (once Phase 1 exists).
- Labyrinth: mirror shard in a mirror room (once Phase 6 exists — this one genuinely waits).
- A shard-count tracker, shared across scenes (this is the first real consumer of the persistence work in Phase 5 — worth stubbing as simple local state now, generalizing later, rather than over-building infrastructure before there's a second user of it).

## Phase 4 — The hidden room

**Why here:** unlocked by Phase 2's tile-9 gate, so it can't be tested end-to-end until that exists. Small scene, low cost.

- Small chamber scene, door built into the Shut the Box hall's existing wall geometry.
- Aldric's original invitation (a static examine object, distinct from the player's own letter).
- Journal fragments — the prose itself still needs writing (a few short, incomplete entries, not a full narrated confession — see the story bible §5 for why).
- Wires into the true ending's "hidden room found" flag.

## Phase 5 — Cross-scene persistence

**Why here, not first:** by this point there are three real consumers (Parlor, Court, Shut the Box) all needing to share `cheatsCaught`, plus the ledger's cross-run growth and the shard tracker from Phase 3. Building this once there are real consumers to generalize from is cheaper than speculatively building a shared save system before any room actually needs it.

- Move `cheatsCaught` from Parlor-local state to a shared/save-backed store (likely `localStorage`, matching the one precedent already in the codebase).
- Ledger cross-run growth: a Collection-ending replay adds the player's own name to the ledger and a tenth tile to Shut the Box.
- Shard tracker and hidden-room-found flag move into the same shared store.
- Test Harness needs a new coverage category here: reload the page mid-run, confirm state actually survived.

## Phase 6 — The Labyrinth

**Why last among rooms:** Art Direction says this explicitly — biggest, highest-risk, least like the other rooms, can ship in a later update without blocking the rest of the game. Grey-box the corridor kit and the stalking AI long before dressing it, per Art Direction's own recommendation.

- 7×7 modular sliding-tile maze, grey-boxed first.
- Real-time Huntsman AI: 30-turn fuse, proximity-based catch.
- The reactive rubber-band (proximity/fuse only worsens when the player is already near the limit or Sanity is already low — never as a punishment for good progress).
- The deliberate palette break (desaturate, kill gaslamp warmth, gold reserved for the exit beacon and clue glints).
- The deliberate absence of the tell-glint HUD cue that every other room shows.
- Mirror room + its shard (completes Phase 3).
- Test Harness coverage here is genuinely harder than the other rooms — it's testing a real-time AI, not turn-based state. Scope this honestly as the riskiest testing work in the whole roadmap, not an afterthought.

## Phase 7 — The six endings

**Why here:** this is the phase that actually reads everything the previous phases built — Corruption, Sanity, `cheatsCaught` (needs Phase 5's shared state to count across rooms), 3+ shards (Phase 3), hidden room found (Phase 4). Building this earlier would mean building against state that doesn't exist yet. **Do not pull this forward.**

- Wire the six conditions (Escape, Replacement, Pact, Collection, Cheat/true, Hollow) to real end-of-game triggers instead of design intent.
- Build each ending's actual sequence/scripted moment.
- Build the player's own invitation letter as a real stateful object with its three-stage reveal, replacing the current static intro card — this is a bigger lift than it sounds, since right now that letter is decorative.
- Verify the 8+ cheats-caught threshold is actually reachable in a real playthrough — this has been a stated assumption since the design docs, never a playtested fact. This phase is where that either gets confirmed or the threshold gets adjusted.
- **Gate out:** Nick playtest + AI panel.

## Phase 8 — Manor puzzles

**Why last and independent:** these are Art Direction's own already-designed puzzles (tune the Study's viola, set the dice to a date, arrange the chess pieces as a lock, anagram a Court evidence word, a cross-game lock needing input from multiple rooms). They don't block anything above and nothing above blocks them — they can slot in whenever there's spare capacity, including in parallel with earlier phases if that's more efficient in practice.

**Explicitly out of scope for this roadmap:** Bones, Study, and Wager are mentioned in Art Direction as part of the game's fuller 8-game roster, but they were never part of this design pass (which scoped to Parlor + "the legacy three" — Court, Gallery/Shut the Box, Labyrinth). Building them is a future expansion, not part of getting the current story to a complete, playable state. The Downloads/asset-dump Godot brief (8 games / 4 floors) is also out of scope.

## Phase 9 — Full testing & polish pass

- Expand the Test Harness to cover every new scene with the same rigor Prologue/Entry Hall/Parlor already have.
- A real playtest for the 8+ cheats-caught arithmetic (flagged as unverified since the very first design draft).
- Screenshot-and-fix pass across every new scene, every state (default, in-progress, success, error/edge-case), matching how the mansion-intro-polish work was verified — not just trusting the Test Harness's automated assertions.
- Cross-scene persistence stress test: play through all six endings at least once each, confirm each resolves on real accumulated state, not a scripted shortcut like Parlor's current dev-menu `winGame` handler uses.
- Final Nick play + AI panel.

---

## Sizing, roughly, relative to each other

Phase 0 and Court/hidden room are the smaller finishes; Phase 0 is small in *code* but mandatory in *feel*. Shut the Box and persistence are medium. Labyrinth and endings are the two big ones. Manor puzzles stay deferrable.

Hall `.glb` props stay on disk for Entry Hall / Court upgrades — don’t mass-delete. Only drop proven duplicates after a side-by-side (e.g. `desk_google.glb` vs `desk_quaternius.glb`).

None of the later phases are broken into TDD-level tasks here — each still gets its own spec-and-plan cycle when started. Phase 0’s detailed tasks live in `2026-07-13-opening-rescue.md`.
