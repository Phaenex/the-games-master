# Main Continuation Plan — 2026-08-17

> **For agentic workers:** this chat is the working trunk. Do not start a second campaign in `/Users/damato/Games Master`. Do not inherit grades from other chats. REQUIRED: `docs/TESTING.md` before any Unity job.

**Goal:** Keep one walkable manor hub in `EntryHall.unity`, keep minigames as separate scenes, then finish Parlor as the first complete table game.

**Architecture:** Authored C# lives under `unity/`. Sync with `npm run unity:scene:sync`. Rebuild through `node scripts/unity-cli.mjs`. Never hand-edit `.unity` YAML. Do not dump this interior into `WendHill_Prologue`.

**Tech Stack:** Unity 6000.5.3f1 HDRP, new Input System, scene builders, EditMode/PlayMode, macOS standalone proof.

---

## Locked facts

- Shipping repo: `/Users/damato/Projects/games/the-games-master`
- Branch: `wend-prologue-boundary-navmesh-harnesses`
- Front doors never open on foot. Ninth Bell still carries the player into Entry Hall.
- Isolated minigame scenes stay isolated. The hub loads them at doors.
- Player step is 0.4 m. Stair risers must stay under that.
- Lane G: locked/barred/closed claims need a solid non-trigger collider.
- No push. No purchase. No Court content until Nick's Phase 0 walk.

## Honest inventory

| Slice | Disk | Synced | Rebuilt | Visual | Claim |
|---|---|---|---|---|---|
| Phase A scene spine | yes | yes | 08-16 | PlayMode only | reachability, not finished games |
| Hub Session 1 foyer/stairs/2F Percival | yes | yes | yes | 12/12 tour, FLAKY 2026-08-17 | walkable stub house |
| Hub Session 2 North Library + Weighted Shelf | authored C# | was missing at audit | no | no | source only until rebuild+tour |
| Hub Sessions 3–6 | no | no | no | no | not built |
| Parlor physical slice / House Memory | yes | mixed | 08-16 era | not this session | do not call finished |
| Five missing table games | names/design only | — | — | — | Nick still owns rules for 6–7 |

## Session 2 closeout (next)

Files:

- `unity/scenes/entry-hall/Editor/GmEntryHallBuilder.cs`
- `unity/scene-system/Runtime/GmWeightedShelf.cs`
- `unity/scenes/entry-hall/Tests/GmEntryHallBuildTests.cs`
- `unity/scenes/entry-hall/Editor/GmEntryHallCompositionPlan.cs`
- `unity/scenes/entry-hall/Runtime/GmEntryHallShotTour.cs`

- [ ] **Step 1:** `npm run unity:scene:check`. If it drifts, `npm run unity:scene:sync`.
- [ ] **Step 2:** Unity closed. `node scripts/unity-cli.mjs rebuild entry-hall`
- [ ] **Step 3:** `node scripts/unity-cli.mjs audit entry-hall`
- [ ] **Step 4:** `node scripts/unity-cli.mjs test` and read the log. Library tests must be in that log, not a remembered 900/900.
- [ ] **Step 5:** `node scripts/unity-cli.mjs tour entry-hall`. Need frames *inside* the library (aisle, weighted shelf, reading table), not only `tour-09-library-door`. A retry after `gui-idle-stall` is FLAKY, not green.
- [ ] **Step 6:** Inspect those frames at full size. PASS / BORDERLINE / FAIL. Copy into `docs/playtest/screenshots/`.
- [ ] **Step 7:** Update TASKBOARD H2 and PROGRESS from this run's log.

Done for Session 2 means: key opens the library door, aisle is walkable, five books start V/I/III/IV/II, solving banks `clue:library_lever`, cellar panel then opens, tour shows the room.

## Hub Sessions 3–6 (after Session 2 is actually green)

### Session 3 — 2F debtor gallery + remaining bedrooms

Move/extend the nine portraits onto the 2F run if the foyer wall is now the stair. Dress Marr's locked study. Keep the barred guest room unenterable.

### Session 4 — Attic loft

Hatch unlocks with a 2F-found key. Sloped rafters, crates, cobwebs, Mirror Shard II, dormer. Same scene. 0.4 m-legal ladder.

### Session 5 — Cellar + vault

Library lever opens the under-stair descent. Barrels, braziers, iron grate. Grate loads `HiddenRoom.unity` / `Labyrinth.unity` the same way parlor doors load the card room.

### Session 6 — Remaining game doors + one campaign walk

Court / Shut the Box doorways from the hub as canon allows. One walk: foyer → library → 2F → attic → cellar → a minigame door.

Conservatory interior stays barred on purpose.

## After the hub

1. Parlor shipping proof from `docs/superpowers/plans/2026-08-16-parlor-physical-vertical-slice.md`. Direct frame inspection. Test green is not enough.
2. House Memory / Mirror mode from `docs/superpowers/specs/2026-08-16-house-remembers-replayability-design.md`. Ordinary New Run stays clean.
3. Five missing table games. Do not invent rules for games 6–7.
4. Nick's Phase 0 walk. Then Court.

## Verification commands

```bash
npm run test:fast
npm run unity:scene:check
node scripts/unity-cli.mjs rebuild entry-hall
node scripts/unity-cli.mjs audit entry-hall
node scripts/unity-cli.mjs test
node scripts/unity-cli.mjs tour entry-hall
npm run gates   # before claiming any content work done
```
