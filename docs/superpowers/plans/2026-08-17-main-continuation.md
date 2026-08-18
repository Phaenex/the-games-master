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
| Hub Session 1 foyer/stairs/2F Percival | yes | yes | yes | tour-01–12 this run | function CONFIRMED, visual PARTIAL |
| Hub Session 2 North Library + Weighted Shelf | yes | yes | yes | tour-13/14 this run | function CONFIRMED, visual PARTIAL |
| Hub Session 3 2F gallery + Marr + barred guest | yes | yes | yes | tour-15/16/17 this run | function CONFIRMED, visual PARTIAL |
| Hub Session 4 attic loft | yes | yes | yes | tour-18/19/20 this run | function CONFIRMED, visual PARTIAL |
| Hub Session 5 cellar + vault | yes | yes | yes | tour-21/22/23 this run | descent+grate+climb-out CONFIRMED, visual PARTIAL |
| Hub Session 6 remaining doors | yes | yes | yes | tour-24/25 this run | doors CONFIRMED, visual PARTIAL |
| Parlor physical slice / House Memory | yes | yes | yes prior run | 24/24 after isolation | table slice CONFIRMED; Aldric proxy; no fresh player build |
| Five missing table games | names/design only | — | — | — | Nick still owns rules for 6–7 |

## Session 2 closeout (DONE 2026-08-17)

Files:

- `unity/scenes/entry-hall/Editor/GmEntryHallBuilder.cs`
- `unity/scene-system/Runtime/GmWeightedShelf.cs`
- `unity/scenes/entry-hall/Tests/GmEntryHallBuildTests.cs`
- `unity/scenes/entry-hall/Editor/GmEntryHallCompositionPlan.cs`
- `unity/scenes/entry-hall/Runtime/GmEntryHallShotTour.cs`

- [x] **Step 1:** `npm run unity:scene:check`. If it drifts, `npm run unity:scene:sync`.
- [x] **Step 2:** Unity closed. `node scripts/unity-cli.mjs rebuild entry-hall`
- [x] **Step 3:** `node scripts/unity-cli.mjs audit entry-hall`
- [x] **Step 4:** `node scripts/unity-cli.mjs test` and read the log. Library tests must be in that log, not a remembered 900/900.
- [x] **Step 5:** `node scripts/unity-cli.mjs tour entry-hall`. Need frames *inside* the library (aisle, weighted shelf, reading table), not only `tour-09-library-door`. A retry after `gui-idle-stall` is FLAKY, not green.
- [x] **Step 6:** Inspect those frames at full size. PASS / BORDERLINE / FAIL. Copy into `docs/playtest/screenshots/`.
- [x] **Step 7:** Update TASKBOARD H2 and PROGRESS from this run's log.

Evidence from this run: EditMode **916/916**, audit PASS, tour **14/14 first attempt**, inscription readable on the tour-14 crop. Shot 09 lintel gap remains a Session 1 leftover.

## Hub Session 3 (DONE 2026-08-17)

Foyer wall was not the stair, so the nine portraits were extended onto the 2F run rather than moved. Shard #1 stays downstairs. Marr's study is furnished and locked. Barred guest is dressed and unenterable. Key on Percival's desk.

Evidence: EditMode **922/922**, audit PASS, tour **17/17 first attempt**, frames 15–17 inspected.

## Hub Session 4 (DONE 2026-08-18)

Hatch key on Marr's desk. Ceiling-slab hatch. 0.24 m-legal ladder inside a well the 1.8 m capsule can actually occupy. Loft: rafters, crates, cobwebs, dormer, Mirror Shard II. Shot 09 lintel void closed. Marr books on the case and desk.

Evidence: EditMode **927/927**, audit PASS, tour **20/20 first attempt**, frames 09/16/18–20 inspected.

## Hub Session 5 (DONE 2026-08-18)

Library lever opens the under-stair well. 0.24 m rise / 0.12 m run. Vault: barrels, braziers, lantern, iron grate. Grate loads `HiddenRoom.unity` the same way parlor doors load the card room.

Evidence: EditMode **930/930**, audit PASS, tour **23/23 first attempt**, frames 21–23 inspected.

## Hub Session 6 (DONE 2026-08-18)

Hearing door on the east wall loads `Court.unity` after parlor is complete. Quieter-hall door on the west wall loads `ShutTheBox.unity` after Court is complete. Conservatory stays barred. Campaign walk: foyer → library → 2F → attic → cellar → parlor door.

Evidence: EditMode **935/935**, audit PASS, tour **25/25 first attempt** after the court-door lighting fix, frames 24–25 inspected. First lighting pass on shot 24 was crushed black (mean 9); not inherited as a pass.

This is hub reachability. It is not Court trial content.

## After the hub

1. Parlor shipping proof from `docs/superpowers/plans/2026-08-16-parlor-physical-vertical-slice.md`. Direct frame inspection. Test green is not enough. **DONE 2026-08-18** for the editor table slice. Built-player 1080p and Aldric art remain.
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
