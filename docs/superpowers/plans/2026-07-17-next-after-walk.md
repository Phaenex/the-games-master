# What's Next — the road from Nick's walk to the six endings

Written 2026-07-17, with Phase 0 at 95% (agent-side complete, 9/9 gates green) and the walk as
the open human gate. This is the execution plan for everything after. Build order, acceptance,
testing contract, asset sourcing, and Nick-only decision points per phase.

## Step 0 — Nick's walk (the fork)

Walk it per `docs/NICK-NEEDED.md`. Two outcomes:

- **PASS / "good enough, move on"** → declare Phase 0 closed in PROGRESS.md, start Phase 1 (Court)
  immediately. G3 polish items park in the backlog and ride along with later passes.
- **Issues surfaced** → they become the G3 punch list, fixed FIRST (each fix re-verified by
  `npm run gates`), then re-walk only what changed. Do not start Court until Nick says the
  opening stands.

Known G3 backlog regardless of outcome (park or fix, Nick's call):
- Entry Hall walkthrough section in the playtest rig (22 interior POIs individually E-verified)
- Chapel close-range wall detail (buttresses or safe texture) · headstone NAME mapping vs story
  bible (bible owner decides names) · optional density from remaining payloads (Aftermath ruins,
  more Abandoned Village) · window-glow richness pass on the mansion (per walk read)

## Phase 1 — Court, from shell to playable hearing (est. 2–4 focused sessions)

Base: `2026-07-14-court-and-shut-the-box.md` + `2026-07-14-court-evidence-draft.md` + the shell
that already boots. Work items in build order:

1. **Room dress to spec** — hall furniture set (owned Table_Large/Chair_1/candelabra + MetalMan),
   jury wall ×9 portraits, wax-seal HUD polish. Mine `Historical Museum` payload (bundle map:
   rails, display cases, institutional lights) with the hash-targeted pipeline.
2. **Role-light swing** — the accuser/defender light rig that swings with the argument state.
3. **Evidence deck full loop** — Deck A straight vs rigged per the draft spec; five evidence
   cards playable; host tells per story bible §Aldric.
4. **Loseable pressure clock** — the hearing can be LOST; loss path text + consequence (doubt
   meter feeds the endings math per the asset book).
5. **Shard card award** — first shard enters the shared persistence plumbing (`GMDoubt`/shard
   store), visible in inventory.
6. **Testing contract** — extend `agent-playtest.mjs` with a `court` walkthrough section (same
   pattern: stops, interacts asserted, deck plays scripted, clock loss + win paths driven,
   metering); add court cases to the in-page harness; `npm run gates` stays the single command.

Nick decision points: young-Aldric portrait yes/no · pressure-clock harshness · evidence copy
final read (voice check before ship).

## Phase 2 — Shut the Box, full match (est. 2–3 sessions)

Base: rules module already 23/23 green; board shell boots. Order:
1. **Match AI (Aldric)** — plays by the tested rules module; personality via pacing + table talk,
   not dice cheating (his cheats are DECLARED design: Hold cheats per plan doc).
2. **Hold cheats + falseCall integration** — the catch-the-cheat loop wired to real UI.
3. **Tile-9 door** — winning path opens the tile-9 door (hidden-room handoff per plan).
4. **Loss/rematch loop** — stakes text; doubt integration.
5. **Testing** — rig `stb` section: scripted full match (deterministic dice via seed hook),
   cheat-catch path, tile-9 door open assert; harness cases for AI legality.

Nick gates after Phase 2 (per cadence): his playtest + the 9-persona AI panel.

## Phase 3 — Shards (1 session)
Shard model + display + award/consume plumbing across scenes (persistence groundwork). Rig: shard
state asserted across a scene handoff.

## Phase 4 — Hidden room (1–2 sessions)
Behind the tile-9 door. Mine `Sorcerers Hut` + `Demonic Village` payloads (ritual props, journals,
candles per bundle map). `dark_book.glb` already owned. Small explorable space, 3–5 examines,
one shard, one host slip. Rig: room walkthrough section.

## Phase 5 — Persistence (1 session)
Cross-scene save (localStorage): shards, doubt, seen-beats, options. Continue button goes live on
the menu. Rig: save→reload→state assert; harness cases for corrupt/missing saves.

## Phase 6 — Labyrinth (design first, then 2–3 sessions)
Design doc BEFORE code (rooms-as-cards concept per story bible). Mine `Haunted Prison` (modular
stone, iron doors, grilles). Playtest rig extended the same day the first corridor exists.

## Phase 7 — Six endings (design + 2 sessions)
End-state math from doubt/shards/secret flags per the asset book; six ending scenes (mostly
text/tone + selective dressing). Nick playtest + AI panel after. Copy through fiction-tell gate.

## Phases 8–9 — Manor puzzles / final polish
Deferred per PROGRESS. Final pass: full-game gates run, Nick walk of EVERYTHING, panel, then the
commit/push conversation (everything to date remains uncommitted by Nick's lock).

## Standing rules for every phase

- `npm run gates` after any change; extend the rig the same session a scene becomes walkable.
- New assets: hash-targeted extraction → Blender → preview gate → night-dress → EXPECT range →
  credits + license lines. No purchases (bundle covers everything listed).
- Copy: fiction-tell check before any beat ships. Canon: house-history spec + story bible own
  names/dates; "Wend Hill" everywhere.
- Honest verdicts: mechanics green ≠ visual pass ≠ fun. Screenshots read by eye every phase;
  Nick's gates at 0/2/7/9 per the cadence.
- No commit, no push, no purchases without Nick's word. Ever.

## Current dashboard tie-in

Phase 0: 95% (walk pending) · Phase 1: 60% → this plan takes it to 100% · Phase 2: 55% · the
rest per PROGRESS.md. The next agent session starts at Step 0's fork — read Nick's walk verdict
first, then this plan top to bottom.
