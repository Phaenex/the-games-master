# The Games Master — Progress

> **⚠ SUPERSEDED 2026-07-17 for delivery tracking → `docs/STEAM-TRACKER.md`.**
> The target is now a **Steam game in Unity**; the HTML build is retired as a product (it survives as
> design source and, for Art Direction, as primary canon). **The dashboard below measures the web
> build.** Its percentages are real for that artifact and misleading for the thing that ships: Court,
> Parlor and Entry Hall are 0% in Unity. This file continues as the **day-log / check-in history**,
> which is still worth reading top to bottom. Delivery bars live in STEAM-TRACKER.

```
Overall (WEB BUILD, retired as a product): [████████████████░░░░] 80% — Estate + playtest system complete, 9/9 gates green

Check-in rule: every agent progress update must refresh this dashboard first. Percentages are weighted delivery estimates, not test scores.

| Phase | Progress | State | Current next gate |
|------|----------|-------|-------------------|
| 0 — Opening rescue / visual rebuild | [████████████████████] 99% | Post-B+ Codex A candidate; 103/103 EditMode, 5/5 PlayMode, 18/18 tour, 7/7 clean scene + 2/2 controller UI proof | Claude adversarial review, then Nick walk/listen/display/physical-controller call |
| 1 — Court | [████████████░░░░░░░░] 60% | Shell, props, evidence draft ready | Full furniture, role-light, pressure clock, shard card |
| 2 — Shut the Box | [█████████████░░░░░░░] 65% | Rules + board shell tested; C# port imported to Unity, 23/23 EditMode green | Full match AI, Hold cheats, tile-9 door |
| 3 — Shards | [███░░░░░░░░░░░░░░░░░] 15% | Roadmap/spec only | Implement shard collection/state |
| 4 — Hidden room | [██░░░░░░░░░░░░░░░░░░] 10% | Roadmap/spec only | Build room after tile-9 handoff |
| 5 — Persistence | [██░░░░░░░░░░░░░░░░░░] 10% | Shared progress plumbing exists | Wire cross-room persistence |
| 6 — Labyrinth | [█░░░░░░░░░░░░░░░░░░░░] 5% | Not started | Design/build after persistence |
| 7 — Six endings | [█░░░░░░░░░░░░░░░░░░░░] 5% | Not started | End-state logic + panel review |
| 8 — Manor puzzles | [░░░░░░░░░░░░░░░░░░░░] 0% | Deferred | Revisit after core route |
| 9 — Final polish | [░░░░░░░░░░░░░░░░░░░░] 0% | Deferred | Nick + AI panel final pass |

Current blocker (updated 2026-08-03): **a performance regression, not a review.** Opening gates 8 and
10 fail the 16.7ms p95 budget (19.72–21.71ms) because the entry hall/Parlor interior added on
2026-07-31 after the last perf measurement is exempt from runtime culling and from the editor perf
audit. Nick owns the fix direction (a real indoor/outdoor visibility rule vs the current blanket
exemption). His walk/listen and display call remain open behind it.

Superseded blocker text: Claude's post-remediation adversarial review, followed by Nick's walk/listen and
display call. Codex now self-grades fingerprint `2c9c97b0db8dc80b` as an **A candidate**, not A+:
the Reed defect class is shared project policy, chapel readability has a pixel gate, the oxblood SUV
reads in the arrival frame, players can calibrate brightness by keyboard or controller, and native
composition proof is free of probe UI residue. Nick still owns filtered-wind character,
cemetery/garden personality, vehicle taste, figure subtlety and the 4m45 approach/ninth-bell pace.
No purchase, commit, or push is pending. Horror Elements remains deliberately out of the exterior
bed: its deep-space/rumble textures would recreate the exact "spaceship" problem Nick flagged.
```

## Status

### 2026-08-18 - Cellar climb-out

A body that walks down now walks back onto the hall. Not committed unless Nick asks.

**What landed**

- Top tread at the east panel. Run 0.24 m, matching the grand stair.
- `HallFloorMidSouth` north face pulled south of the well. A floor lip at `CellarHoleSouth` was a wall across any 1.8 m capsule still on the flight.
- South well header sits on that shortened lip at y>=1.0. Hall walkers stop. A descending head passes under.
- Vault north wall open across the well x. East-low wall stops under the hall slab so the panel opening is clear at foot height.

**Verification this pass**

- EditMode **945/945**. `ACharacterControllerCanWalkFromSpawnIntoTheVaultAndBackOntoTheHall` passed.
- rebuild / audit entry-hall PASS
- GUI tour **25/25 first attempt**
- `test:fast` green, sources 420/420
- Frames: `docs/playtest/screenshots/entry-hall-tour-21-cellar-panel.png`, `22-cellar-descent.png`, `23-cellar-vault.png`

**Visual**

- Shot 21: east-flank panel under a sconce. Dark door, damask. Black 27% is the unlit hall, not a crushed frame.
- Shot 22: looking down the well onto herringbone. Stairs are easier to read from shot 07.
- Shot 23: vault barrels and open iron grate. Dark beyond.

**Not this pass**

- Built-player Parlor 1080p series. Fresh app exists (`unity-project/Builds/macOS-Game`, binary 15:18 today, SHA `4cd06990…`). Proof started: reps 1–2 qualified at 1920x1080 (p95 ~8.2ms). Rep 3 failed the host-load ≤1.0/core gate. Coilworks e2e Electron was still on the machine. Did not force `GM_UNITY_IGNORE_HOST_LOAD`. Coverage modes did not run.
- PlayMode not re-run this slice.
- `npm run gates` (last full pack is still 08-16)

Human eye still Nick's. Court still locked.

### 2026-08-18 - Hub retrospective (do not inherit H1–H6)

Re-verified Sessions 1–6 instead of copying the previous pass. The old campaign walk was not a walk.

**What landed**

- Connected spawn → library → 2F → Marr → attic → parlor trigger. Capsule must sit in `ParlorTransition`.
- Connected spawn → cellar well → vault grate. Capsule must sit in `VaultTransition`.
- Tread tests measure rise/run, not just name counts.
- Weighted shelf books are `SM_Book` meshes.
- Cellar run 0.24 m. Removed the well landing lid. Climb-out still fails.

**Verification this pass**

- EditMode **945/945**, PlayMode **44/44** (XML)
- rebuild / audit entry-hall PASS
- GUI tour **25/25 first attempt**
- `test:fast` green, sources 420/420
- Docs shots recopied from this tour so they match `Screens/EntryHall`

**Still HIGH at the time:** cellar climb-out. Closed in the climb-out check-in above.

Human eye still Nick's. Court still locked. No `npm run gates` this session.

### 2026-08-18 - Parlor shipping proof

Closed the editor table slice. Did not run a fresh macOS player build. No commit.

**What landed**

- `GmParlorPresentationAudit` measures card identities, focus colliders, baize penetration, proxy clearance, restore seams, and two readable evidence channels. Wired into the quality audit.
- Tour isolation: each staged case clears the evidence log. First pass leaked shot-05 facts onto later frames. That is not the pass.
- Tasks 5–8 were already in source. Checkboxes were stale. Verified, not reimplemented.

**Verification this pass**

- `test:fast` green, rebuild parlor PASS, audit-saved PASS
- EditMode **944/944**, PlayMode **44/44**
- GUI tour **24/24 first attempt** after isolation (`unity-project/Logs/cli-parlor-tour-attempt-1.log`)
- Restore: 28 cards, firstDelta=0, secondDelta=0, cue=0
- Frames inspected: `docs/playtest/screenshots/parlor-tour-*.png`

**Not this pass**

- Built-player 1080p. The Aug 14 game app is stale.
- Aldric character art. Still the substitute proxy.
- `npm run gates` (last full pack is still 08-16)

### 2026-08-18 - Hub Session 6: remaining game doors

Closed H6. Hub campaign leftover is gone. Did not start Parlor shipping proof. No commit for this slice.

**What landed**

- East-wall hearing door loads `Court.unity` after parlor is complete. Locked until then. Conservatory stays barred.
- West-wall quieter-hall door loads `ShutTheBox.unity` after Court is complete.
- Campaign walk covers foyer → library → 2F → attic → cellar → parlor door in one EditMode probe.

**Verification this pass**

- rebuild entry-hall PASS, audit entry-hall PASS
- EditMode **935/935**, 0 failed
- GUI tour **25/25 first attempt** on the lighting retake. Earlier tour that night idle-stalled once, then shot 24 was crushed black (mean 9). That is not the pass.
- Frames 24–25 inspected: `docs/playtest/screenshots/entry-hall-tour-24-court-door.png`, `25-stb-door.png`

**Not this pass**

- Court trial content. Still locked behind Nick's Phase 0 walk.
- `npm run gates` (last full pack is still 08-16)

### 2026-08-18 - Hub Session 5: cellar + vault

Closed H5. Did not start Session 6 remaining game doors. No commit.

**What landed**

- Library lever still unlatches the east-flank cellar panel. A 1.55 x 3.3 m well sits behind it. Without the lever a body cannot enter.
- 0.24 m rise / 0.12 m run treads drop a 1.8 m capsule under the hall floor. South lip wall sits on the hall side of the hole so the head is not wedged.
- Vault: owned barrels, two braziers, hanging lantern, open iron grate. Grate transition loads Hidden Room the same way parlor doors load the card room.

**Verification this pass**

- rebuild entry-hall PASS, audit entry-hall PASS
- EditMode **930/930**, 0 failed. XML: `unity-project/Logs/editmode-results.xml`
- GUI tour **23/23 first attempt**. Log: `unity-project/Logs/cli-entry-hall-tour-attempt-1.log`
- `npm run test:fast` PASS
- Frames: `docs/playtest/screenshots/entry-hall-tour-21-cellar-panel.png`, `22-cellar-descent.png`, `23-cellar-vault.png`

**Visual (agent, full-size)**

- Shot 21 panel + sconce: PASS. Dark stair mass on the left is the well enclosure, not a void. Mean 20, black 27%.
- Shot 22 descent: PASS. Looking down the well onto herringbone cellar floor.
- Shot 23 vault: PASS. Barrels left, iron grate leaves open, warm lantern. Mean 17, black 0.3%.

Human eye still Nick's. Court still locked. No `npm run gates` this session.

### 2026-08-18 - Hub Session 4: attic loft + leftover lintel/books

Closed H4. Did not start Session 5 cellar. No commit.

**What landed**

- Library door lintel void closed with a hall-side beam (collider stays above 2.12 m) plus a collider-free fill over the short leaf.
- Marr's west case and desk now carry owned book prefabs. Attic key sits on that desk.
- Attic hatch is a locked ceiling slab. Ladder uses the same 0.24 m rise as the grand stair, inside a well big enough for the 1.8 m capsule. Loft has rafters, crates, cobwebs, dormer, Mirror Shard II.

**Verification this pass**

- rebuild entry-hall PASS, audit entry-hall PASS
- EditMode **927/927**, 0 failed. XML: `unity-project/Logs/editmode-results.xml`
- GUI tour **20/20 first attempt**. Log: `unity-project/Logs/cli-entry-hall-tour-attempt-1.log`
- Frames: `docs/playtest/screenshots/entry-hall-tour-09-library-door.png`, `16-marr-study.png`, `18-attic-hatch.png`, `19-attic-loft.png`, `20-attic-shard.png`
- `npm run test:fast` PASS. `npm run gates` not run tonight.

**Visual (inspected at full size)**

- Shot 09: PASS. Lintel beam on the door. Seam highlight at the leaf, not a missing-geometry hole.
- Shot 16: PASS. Books on the desk and case, key under the sconce.
- Shot 18: PASS. Well and ladder from the 2F gallery.
- Shot 19: PASS for a dark loft. Rafters, crates, west lantern.
- Shot 20: PASS. Dormer and Shard II readable (mean 33, black 0.1%).

**Not done, and not claimed**

Cellar/vault, remaining game doors, Parlor shipping proof, five missing table games. Human gates unchanged. Court locked. This is not 900/900.

### 2026-08-17 - Hub Session 3: 2F gallery, Marr study, barred guest

Closed H3. Did not start Session 4 attic. No commit.

**What landed**

- Foyer still owns the original nine portraits and Shard #1. The stair did not eat that wall, so the nine were extended onto the 2F east and north walls, plus a landing runner.
- Lady Marr's study is a furnished room north of the landing (desk, chair, bookcase, blotter that repeats WATCH HIS HANDS). Locked until Percival's desk key (`key:lady_marr`).
- Barred guest room is dressed (bed, chair jammed at the inner latch) and a body cannot enter.

**Verification this pass**

- rebuild entry-hall PASS, audit entry-hall PASS
- EditMode **922/922**, 0 failed. New tests in that XML: foyer still owns shard, 2F hang, Marr furnished, key on Percival's desk, walk-in after key, barred body blocked.
- GUI tour **17/17 first attempt**. Log: `unity-project/Logs/cli-entry-hall-tour-attempt-1.log`
- Frames: `docs/playtest/screenshots/entry-hall-tour-15-upper-gallery.png`, `16-marr-study.png`, `17-barred-guest.png`
- `npm run test:fast` PASS

**Visual (inspected at full size)**

- Shot 15: PASS. Five named debtor frames on the east wall, sconce over Halvard.
- Shot 16: PASS for a furnished study. Desk, chair, scroll, lamp. Bookcase shelves read empty (kit mesh).
- Shot 17: PASS for a dressed barred room. Gothic bed and sconce. The jammed chair sits at the door behind this camera. Tests prove a body cannot pass.

**Not done, and not claimed**

Attic loft, cellar/vault, remaining game doors, Parlor shipping proof, five missing table games. Human gates unchanged. Court locked. This is not 900/900.

### 2026-08-17 - Hub Session 2: North Library is a furnished room

Closed H2. Did not start Sessions 3–6. No commit.

**What landed**

- Library rebuilt into `EntryHall.unity` from authored C#. Aisle walkable after the wake-table key. Weighted Shelf starts V/I/III/IV/II, solve banks `clue:library_lever`.
- Review tour grew to 14 shots (`13-library-interior`, `14-weighted-shelf`). Registry `entry-hall.tour.shots = 14`.
- Test-order pollution: aisle/shelf tests left the door open. `GmEstateDoor.RelockClosed()`, `GmWeightedShelf.ResetPuzzle()`, `[SetUp] IsolateEachTest`.
- Inscription was showing TextMesh backs. Same +90 facing as the parlor clock numerals. Crop of tour-14 reads "Every game has an order. Even this one." left to right, right-side up.

**Verification this pass**

- rebuild entry-hall PASS, audit entry-hall PASS (`[GmEntryHallAudit] PASS`)
- EditMode **916/916**, 0 failed. Library tests in that XML: aisle walk, shelf→lever, furnished room, review tour includes interior, bible start order.
- GUI tour **14/14 first attempt** (not FLAKY). Log: `unity-project/Logs/cli-entry-hall-tour-attempt-1.log`
- Frames: `docs/playtest/screenshots/entry-hall-tour-13-library-interior.png` (furnished table/chairs/lamp/ladder, black 2.3%) and `entry-hall-tour-14-weighted-shelf.png` (five books + readable inscription, black 1.2%)
- `npm run test:fast` PASS. Scene sources match (418 files).

**Visual (inspected at full size, not inferred from code)**

- Shot 13: PASS for furnished room. West stacks still dark at the edges. Intentional gloom, not a hole.
- Shot 14: PASS for the puzzle and the inscription. Spine ink is dark on leather. Color order from the south slot is V, I, III, IV, II.
- Shot 09 library door: leftover lintel gap. Pre-existing Session 1 visual. Not part of H2.

**Not done, and not claimed**

Attic, cellar geometry, Marr study, contiguous mansion, Parlor shipping proof, five missing table games. Human gates unchanged: Phase 0 walk, display, audio, feel. Court locked. 08-16 still owns the last full gate pack. This is not 900/900.

### 2026-08-17 - Session 1 leftovers: clock wood, climb path, body probes

Closed the Session 1 holes that were still open after the hub landed. Did not start H2 / Sessions 2–6. No commit.

**What was actually broken**

- `LandingClock` had no HDRP override, so the imported Standard case rendered as a bright untextured pillar. Same dark wood recipe as the wake-room clock, plus a BoxCollider (PlacePrefab strips imported colliders).
- The secret cellar panel sat at `(0, 6.55)`, on the first tread. A CharacterController climb stalled at `(0.00, 0.23, 6.03)`. Moved to the east flank under the rising flights `(2.28, 8.35)`, facing west into the stair mass.
- Physical proofs were rays and flags. The plan asked for a body.

**Verification this pass**

- `npm run unity:scene:sync` then `rebuild entry-hall` PASS, `audit entry-hall` PASS
- EditMode **906/906** (was 905/906 until the cellar moved off the climb)
- CharacterController: locked/barred doors hold, Percival's open door admits a body, stairs reach 2F, clock case is solid
- GUI tour **12/12**, **FLAKY**: attempt 1 idle-stalled, attempt 2 passed. Failure log kept: `unity-project/Logs/cli-entry-hall-tour-attempt-1.log`
- Copies: `docs/playtest/screenshots/entry-hall-tour-*.png`. Clock is a dark upright case in shots 07/08, not the white pillar from the prior tour.
- `npm run test:fast` PASS
- Interior frame scan: **11/12 clean**. Shot 09 library door is orange-cast red:blue **5.37** (indoor max 3.6). The previous copy of that same shot measures **5.37** too — pre-existing lamp-only corner, not this pass. Scanner comments file that look call to Nick (#39).

**Not done, and not claimed**

H2 library, Marr interior, attic, cellar/vault geometry, remaining game-door wiring, prologue merge, git packaging. Human gates unchanged: Phase 0 walk, display, audio, feel.

### 2026-08-17 - This chat is the working trunk

Audit of every overlapping session, then a snapshot commit Nick asked for. Live plan: `docs/superpowers/plans/2026-08-17-main-continuation.md`. Handoff: `docs/HANDOFF-2026-08-17.md`.

**Repo that ships:** `/Users/damato/Projects/games/the-games-master` on `wend-prologue-boundary-navmesh-harnesses`. `/Users/damato/Games Master` is the April skeleton. Several chats tonight wrote plans against it and aborted on folder switch.

**What is true**

- Hub Session 1 is in the Unity project: foyer doors, walkable stairs, 2F Percival, Lane G barriers. Entry Hall tour 12/12 tonight, FLAKY (attempt 1 idle-stalled).
- Hub Session 2 library/Weighted Shelf is authored C#. It was not in `unity-project/` at audit time and has no library-interior tour.
- 08-16 still owns the last full gate pack (14/14, EditMode 455/455). Later chats claiming 886/886 or "all rooms exist" are false.
- Parlor physical slice and House Memory are in the same uncommitted tree. Not re-proven tonight.

**Human gates still open:** Nick's Phase 0 walk, display/audio/feel, vehicle taste, Aldric character art. Court stays locked behind the walk.

### 2026-08-17 - Session 1: Entry Hall is a walkable house hub, not a dead-end box

Session 1 of the Blackwood Manor hub campaign. Isolated minigame scenes (Parlor, Court, STB, Hidden Room, Labyrinth) stay separate. This did **not** dump interior into `WendHill_Prologue`. No commit.

**What is actually walkable now**

- Foyer crossroads: barred front doors (Threshold Refusal), open parlor double doors still load `Parlor.unity`, locked north library door (`key:brass_skeleton_key` on the wake table), west conservatory barred from the other side, secret under-stair cellar panel that does not open (needs `clue:library_lever`, not spawned).
- Grand staircase is 14 treads at 0.24m rise (agent step 0.4m), not the old 4.6×3.4×5 solid. A 2F landing slab sits at 3.4m with a north arch the body can pass.
- 2F gallery: Percival's door starts open onto a dressed bedroom (`GothicBed` + desk). Lady Marr locked (no key this session). Third guest barred. Attic hatch visible and locked.
- North of the library door: a short stub with one bookcase. Full library is Session 2.

**Door contract (Lane G)**

`GmEstateDoor.Configure` now takes `secret` and `openAtStart`. Closed / locked / barred leaves keep a solid non-trigger `DoorBarrier`. Open leaves disable it. Unlock persists as `unlocked:{doorId}` on `GmRunStore`. Factory uses Victorian `Door_1` for the leaf (SM_Door_01 was uniform-scale crushed into a postage stamp).

**Verification this session**

- `npm run unity:scene:sync` then `rebuild entry-hall` PASS
- `audit entry-hall` PASS
- EditMode **900/900**
- GUI tour **12/12** (shots 09–12 are new). Copies: `docs/playtest/screenshots/entry-hall-tour-*.png`
- Physical proofs in EditMode: locked/barred openings hit `DoorBarrier`; Percival's open leaf does not; first tread is under a climbing foot; desk sits on 2F, not the foyer; door AABB fills the wall instead of stabbing through the opening.

**Not done, and not claimed**

Full library, 2F debtor gallery move, Marr's interior, attic loft, cellar/vault, remaining game-door wiring, prologue `HouseBeginning` merge, git packaging. The grandfather clock in the hall is still a bright untextured upright case. That predates this session's 2F work.

### 2026-08-13 - Claude session: Reckoning/coach-house work, PAUSED — read `docs/CLAUDE-FABLE-HANDOFF.md` first

Session paused on Nick's explicit instruction after he manually found a serious defect the
automated gates never caught: **the estate gate has no physical enforcement and can be walked
around** — full root cause, why every prior test missed it, and the full punch list of
everything open (blocking, deferred, Nick's calls, and pre-existing unrelated items) are all in
`docs/CLAUDE-FABLE-HANDOFF.md`'s top section, dated 2026-08-13. Read that before touching this
project again. Short version: don't trust green EditMode/PlayMode/gate numbers as proof the game
physically works — they proved code executes and scripted paths complete, not that objects
collide/block/sit the way their narrative claims. A dedicated audit for that whole defect class
is the next priority, ahead of any new content.

### 2026-08-13 - Claude session: full ground-up audit; EditMode taken from non-compiling to 294/296

- **Unity EditMode compiles and passes 294/296**, up from not compiling at all at session start.
  The six Phase 1-7 scene folders (`entry-hall`, `court`, `parlor`, `shut-the-box`, `hidden-room`,
  `labyrinth`) had never once been compiled in-engine; the opening blocker was 47 CS0117 errors, all
  `GmCompositionAuthoring.ReviewClaim(...)`, a method that existed nowhere in the repo. Fixed with a
  documented interim adapter (`docs/audit/F1-review-claim-decision.md`) plus real geometry/camera
  fixes verified against a from-scratch replica of the audit's `WorldToViewportPoint` math for 5 of
  the 6 scenes. **The remaining 2 failures are Court's, left failing on purpose** — Court's
  composition wiring is content work, and Court stays hard-locked behind Nick's Phase 0 walk.
  TASKBOARD **F1/F1b**. Gates 3-12 have never run against a compiling build — no evidence exists
  past gate 2 yet.
- **262-agent source audit: 294 raw findings → 196 confirmed** (94 HIGH, 102 MED) after adversarial
  verification, 9 refuted, 89 LOW unverified. `docs/audit/2026-08-13-findings.md`.
- **The dominant theme was wiring, not bugs.** ~50 confirmed findings were one class: built,
  unit-tested, green, and never called by gameplay. Partially closed this session: the audio
  manager now actually plays clips, the pause menu's tabs now render real content, `GmCreditsUI`
  now reads the real credits catalog, and `GmRunStore`/`GmHouseProgress` are bridged (a corruption-
  tier ceiling conflict surfaced in the process — house caps at 4, run-store at 5 — and needs
  Nick's canon call). Save/load and scene-transition wiring are still open: there is no boot/title
  scene to call them from yet, and that's a UX decision, not a bug fix.
- **Fixed and re-verified: 18 compile errors across 6 classes** (duplicate `GmParlorRules`,
  `GmShutTheBoxController` calling nonexistent rules methods, a missing `RoadSurfaceY` helper, an
  HDRP `LightUnit` namespace move affecting 6 call sites, `System.Linq` missing in 3 test files,
  6 `BuildTests.cs` files missing scene teardown that was corrupting an unrelated ground-detection
  test). Also corrected `NICK-NEEDED.md`, which pointed Nick's Phase 0 walk at a deleted directory,
  and reverted a copy edit that introduced a fresh self-contradiction in `Art Direction.dc.html`.
- **Harness trap worth remembering:** background gate/test runs repeatedly reported "exit code 0"
  while their own captured logs ended in a real failure. Every result here was read from the log,
  not the notification. Recorded in `docs/TESTING.md`.
- No commit, no push, no purchase.

### 2026-08-03 - Claude session: gates re-baselined; performance regression found and root-caused

- **The opening gates had not actually been runnable.** `node_modules` was absent, so gate 1 crashed
  on a missing `playwright` import and gates 2–11 never executed. Installed deps; the archived web
  harness runs again at 47/47.
- **Fixed a hard-coded path that broke Unity's repo resolution.** `GmSceneIntelligencePaths
  .FindRepoRoot()` pinned `~/Projects/the-games-master`, but the repo now lives at
  `~/Projects/games/the-games-master`; it now searches the Projects tree, and `unity-cli.mjs` exports
  `GM_REPO_ROOT` explicitly. EditMode went 203/204 → **204/204**.
- **Full gate run: 9/11 pass, 2 fail.** Gates 8 and 10 both miss the 16.7ms p95 budget (19.72–21.71ms
  standalone across three runs; 20.35ms full-route). Everything non-performance is clean: 435/435
  route metres, 0 stalls, 0 nav fallbacks, 0 missing frames, 0 runtime defects, PlayMode 12/12,
  tour 8/8, house-proof 10 frames, wall-proof 4 frames. Fingerprint `460221080faabbff`.
- **Root cause.** Host load fell 2.5x across runs while p95 moved 5%, excluding CPU contention. The
  Jul 31 audit's perf artifact was written 20:12; scene sources were edited until 22:42 and never
  re-measured. That window added an entry hall + Parlor interior inside the canonical prologue scene
  (`GmHouseBeginningBuilder`, `GmVictorianInteriorKit`), which is exempt from culling
  (`GmWendRuntimeCulling.cs:39,47` — renderers *and* lights) and invisible to the editor perf audit
  (`GmWendPerformance.cs:30`). Roughly a dozen soft-shadowed HDRP point lights stay live across all
  435 outdoor metres. Peak renderers 1,359 vs 1,174 on Jul 31.
- **Not fixed, deliberately.** The culling exemption has a plausible gameplay reason; replacing it
  with a real indoor/outdoor visibility rule is a scene-architecture decision for Nick.
- Objective visual defects found that every automated gate passed: gate piers render as black voids
  (`GmWendOpening.cs:447` — primitive cube at Metallic 0.42; measured RGB 1.1/0.0/0.8 std 0.75 vs the
  adjacent stone wall at 17.9/9.9/8.3 std 16.6 under the same lamp), and
  `05-cemetery-composition.png` is misnamed — it is authored to look at the **chapel**
  (`GmStandaloneReviewProbe.cs:116`), so the cemetery has no dedicated built-player frame.
- No commit, no push, no purchase.

### 2026-07-22 - post-B+ prevention, display-readability and clean-player closure

- Extracted the Reed lesson into `GmTerrainPrototypeAudit`, a shared scene-system module that rejects
  child-only Terrain prefabs, validates root LOD mesh references and checks meaningful populations.
  Wend Hill's audit and tests now consume the shared contract instead of duplicating it.
- Added paired C# and Node runtime-integrity policies for Terrain-instancing and broken-shader
  failures. Four parity/behavior tests prove the original Claude warning fails while shutdown chatter
  does not. The final native log contains zero matching defects.
- Rejected two chapel lighting candidates through a new scene-specific pixel gate at mean 8.9/9.0.
  The retained bounded cold fill passes at mean 10 without changing fixed EV -2.55 or authored post
  exposure 0. The SUV now exposes restrained oxblood body planes with its lights still off.
- Added shared five-level display calibration from -0.5 to +0.5 stops. Keyboard arrows and controller
  D-pad operate it from the pause screen; normal play persists it, while automated proof forces level
  0 in memory and cannot overwrite the player's preference.
- Rejected the first native proof even though it passed technically: controller-test text, story
  cards and wind toasts obscured scene frames. Dedicated UI frames remain; final composition frames
  02-07 are clean.
- Recorded structured automation evidence in the append-only learning store. It did not issue taste
  verdicts, resolve the five Nick-gated defects, promote a rule or approve a baseline.
- Final evidence: fingerprint `2c9c97b0db8dc80b`; **103/103 EditMode**, **5/5 PlayMode**,
  **18/18 tour**, **7/7 clean native scenes + 2/2 controller UI**, 5/5 audio decode, zero runtime or
  render-integrity findings. Native 1280x720 timing over 240 frames: mean **11.76ms**, p50
  **10.46ms**, p95 **14.89ms**, p99 **15.52ms**, max **15.61ms**.

**Codex grade: A candidate, not A+.** Claude should attack the delta using
`docs/CLAUDE-PHASE0-A-CANDIDATE-REVIEW-2026-07-22.md`; Nick retains sensory, pacing, display and
physical-controller approval. Full evidence:
`docs/playtest/codex-phase0-post-bplus-a-candidate-2026-07-22.md`. Court remains locked; no purchase,
commit or push.

### 2026-07-22 - adversarial B+ remediation: Reed runtime integrity and cold-audit repair

- Accepted Claude's B+ regrade. Four generated Reed prefabs kept their only MeshRenderer on a child,
  so Unity Terrain rejected them in the executable while the old probe counted only errors. The
  prior A- and "six rendered variants" claims were withdrawn for that fingerprint.
- Fixed the generator rather than generated YAML: every Terrain prototype root now owns a validated
  LODGroup, every LOD references a real mesh, and every one of the six prototypes must have at least
  50 placed instances. The estate audit and a dedicated EditMode test both enforce the contract.
- Rejected the first structural fix after its newly visible source ShaderGraph produced saturated
  green tufts. The four Reed copies now use project-owned, non-emissive HDRP/Lit alpha-cut materials
  with the purchased albedo/normal textures and explicit night reflectance. Purchased sources remain
  untouched; the accepted 18-shot tour is dark/dry rather than neon.
- The standalone proof now fails on `couldn't be instanced` and `no valid mesh renderer` warnings,
  even if Unity labels them warnings or emits them before the runtime probe subscribes. The final
  player log contains neither pattern and reports zero runtime errors **or render-integrity warnings**.
- Identified Claude's gray gate slab as the Wend Hill plaque: its former carving was on the far face.
  It is now a dark framed, arrival-facing pier plaque with a visible monogram and a placement/detail
  test. Cemetery inspection confirmed no monolithic floor-cover renderer; the discrete dark forms are
  the authored grave recess and low/fallen marker families, while the broad band is shadowed Terrain.
- A cold `audit-saved` attempt uncovered another real proof bug: the fresh editor process had no
  build-time Terrain height cache and falsely measured the 1.82m cemetery hollow against y=0. The
  sampler now rehydrates from serialized `Ground`; two cold saved-scene audits reproduce
  `eda4f37223d0895b` with zero findings.
- Final evidence: **7,800 genuinely renderable instances across six populated variants**; audit
  **7,752 objects, 5,421 renderers, 270 colliders, 21 lights**; **98/98 EditMode**, **5/5 PlayMode**,
  **18/18 tour**, five of five final cues, **7/7 native scene + 2/2 controller frames**, zero runtime
  or render-integrity defects, repository suites green and full gates **9/9**. Native 1280x720 timing:
  mean **12.50ms**, p50 **12.53ms**, p95 **13.27ms**, p99 **13.67ms**, max **13.90ms**.

**Phase 0 remains 99% and B+ after adversarial review.** The must-fix build defect is closed, but an
A- depends on Nick accepting the actual darkness and sensory/pacing experience. Court remains locked;
no purchase, commit or push.

### 2026-07-22 - prebuilt-environment blend and perceptual-proof closure

- Rebuilt the estate ecology from selected owned Horror Environment assets rather than dropping a
  donor demo scene into canon. The accepted result uses **7,800** Terrain foliage instances across
  six calibrated variants, **173** living/wet/dead trees in 20 unequal communities, and **11** exact
  horizon/reveal trees while preserving the drive, side-room routes and the one-mansion rule.
- Added a dark distant-soil terrain layer and matte ridge backdrop to eliminate the pale north
  boundary. The porch now has broken drainage remnants, six leaf drifts, six foundation-growth
  anchors and ecology encroachment around a compact clear stair route.
- Rejected and removed a structurally valid imported moss treatment after HDRP screenshots exposed
  it as neon-green floor sheets. Purchased source files were not modified and no temporary script or
  diagnostic material survives in the final candidate.
- Re-seated the one-in-three figure in the actual upper-right warm pane and added a reusable
  scene-specific pixel gate to the review-tour engine. The final same-camera on/off comparison
  measures a **22x42** ROI, **16.26** mean-channel delta and **32.8%** changed pixels, so an invisible
  or billboard-dominant figure now fails instead of passing from hierarchy state alone.
- Two independent saved-estate audits reproduced fingerprint `34a5a6b24c55e2d5` with **7,751
  objects, 5,420 renderers, 269 colliders and 21 lights**. Final evidence is **96/96 EditMode**,
  **5/5 PlayMode**, **18/18 HDRP tour**, **39/39 shared-source sync**, **7/7 native scene frames**,
  **2/2 controller frames**, five of five final cues decoded, zero runtime errors, repository
  harnesses green and the uninterrupted browser archaeology runner **9/9**.
- The fresh universal macOS player repeated cold-open, left-stick and D-pad movement, right-stick
  look, interaction, wind switching and pause/resume. Its 240-frame 1280x720 sample was mean
  **16.37ms**, p50 **16.59ms**, p95 **18.30ms**, p99 **20.02ms**, max **33.25ms**.

**Progress remains 99% for Phase 0 and 17% overall Unity/Steam.** This closes the agent-owned
environment integration and proof work at an A- candidate, not A+ artwork. Nick's uninterrupted
walk/listen and Claude's adversarial delta review remain mandatory. Court stays locked; no purchase,
commit or push.

### 2026-07-20 - standalone distribution and complete controller closure

- Confirmed the existing macOS app was self-contained, then closed the partial-controller gaps:
  cold-open cards, intro skip, D-pad fallback, in-player F8/RB wind comparison, active-device UI,
  Menu/Options pause with time/audio suspension, and Y/Triangle quit from pause now join the existing
  stick movement/look and A/Cross interaction in one Input System action map.
- Added the fifth live PlayMode route. It drives a virtual controller through card advance/skip,
  left-stick and D-pad travel, right-stick look, a real focused interaction, wind cycling,
  pause/resume, quit routing and the controller HUD. Final Unity results are **93/93 EditMode** and
  **5/5 PlayMode**, with the estate audit still clean at `5ef26a690b0ad7a6`.
- Extended the exported-player proof instead of trusting editor simulation. The final universal
  macOS app measured **0.644m** left-stick travel, **0.319m** D-pad travel and **16.53 degrees** of
  right-stick rotation, then passed interaction, wind and pause/resume, captured **2/2** controller
  UI frames alongside the **7/7** scene frames, decoded all five final cues and reported zero
  runtime errors. Final 240-frame timing: mean **16.67ms**, p95 **17.40ms**, max **32.87ms**.
- Rejected the first controller UI capture as too small at 1280x720, increased prompt/legend sizes,
  rebuilt and inspected both final native frames. The app now identifies as
  `com.damatnic.thegamesmaster` version `0.1.0`, is universal x86_64/arm64, contains no UnityEditor
  assembly and passes deep bundle-signature verification.
- Created the **614MB** `The Games Master - Phase 0 macOS.zip`, passed archive integrity, extracted it
  to a clean temporary directory and reran all nine native frames plus audio/controller/runtime proof
  from that extracted copy. No physical controller was attached, so Xbox/PlayStation hardware feel
  remains part of Nick's mandatory walk rather than an automated claim.
- The final full regression matrix remains green: repository 23/23 C# parity, 12/12 learning,
  10/10 scene system, 23/23 JavaScript, 47/47 browser harness, scaffold compile, 39/39 source sync,
  and the uninterrupted browser archaeology runner **9/9**.

**Progress remains 99% for Phase 0 and 17% overall Unity/Steam.** Controller and standalone
distribution are agent-complete. Nick's actual walk/listen on his chosen input device and Claude's
adversarial review remain the approval gates. Court stays locked; no purchase, commit or push.

### 2026-07-20 - fixed-night dry-growth closure and final A candidate

- Rejected two torn-canvas garden covers after their owned meshes passed structure/path tests but
  rendered as black floor holes in HDRP. The accepted baseline was restored exactly before any new
  candidate work continued.
- Used viewport occupancy evidence to prove the garden was not missing geometry: individual crop
  cards already occupied 4-8% of the close frames but their pack ShaderGraphs collapsed to black at
  the fixed low-lux exposure. Garden rows, cemetery overgrowth and middle-acreage hedgerows now reuse
  the same owned albedo/normal textures through deterministic non-emissive HDRP/Lit alpha cutouts.
  Woodland, solid hay, coordinates, routes and renderer counts are unchanged.
- Rebalanced only the cemetery's authored moon returns, marker-stone multiplier and warm grave lips.
  The near-black recess remains separate; chest tombs, leaning slabs and soil edges now survive both
  editor HDRP and the brighter native player backbuffer.
- Final objective evidence: fingerprint `5ef26a690b0ad7a6`; audit zero findings; EditMode **93/93**;
  PlayMode **4/4**; HDRP tour **18/18**; variants **6/6**, selected none, exact restore; pacing
  **15/15**; scene system **10/10**; scaffold compile PASS; browser **47/47**; full gates **9/9**;
  fresh Mac build; native proof **7/7**, five final cues decoded, zero runtime errors. The final
  240-frame native sample was mean **9.65ms**, p50 **9.06ms**, p95 **12.33ms**, p99 **12.89ms**,
  max **22.19ms**.
- Reopened the completion evidence after the build and found the Steam tracker plus Claude handoff
  still naming the pre-closure fingerprint and 88-test suite. Both now point at the current
  candidate. The saved-estate audit was rerun after the final material/light pass and remained clean
  at `5ef26a690b0ad7a6`; source sync was independently rechecked at **39/39** and the current repository
  suite reran at 23/23 C# parity, 12/12 learning, 10/10 scene system, 23/23 JavaScript and 47/47
  browser harness.

**Progress remains 99%.** Every agent-owned Phase 0 category is now an A candidate, with engine and
verification at A+. A+ art/audio/pacing acceptance still belongs to Nick's uninterrupted walk and
listen, followed by the requested Claude review. Court remains locked; no purchase, commit or push.

### 2026-07-20 - prior scene regrade checkpoint (superseded above)

- Replaced the cemetery's floating goat-skull stand-in with a deterministic broken-antler crest
  derived from an owned antler mesh. The repo-owned Blender build script removes one tine, leaves a
  readable stump and exports the exact Unity FBX; tests pin its vertex range, span, material story and
  single deliberate interaction target.
- Removed the generated cemetery soil benches and garden soil ribbons after repeated HDRP review
  showed them as black islands/bars. Old plots now return to turf; failed beds use owned dead growth,
  stakes, twig edges, straw, produce and missing places. A final shadowed-garden light candidate was
  also rejected because it reduced readability without adding depth; the accepted light rig was
  restored and reproduced at fingerprint `5ef26a690b0ad7a6`.
- Expanded the reusable craft audit and synchronized it as the 39th repo-owned scene-system source.
  The C# scaffold gate now compiles that dependency instead of passing only the older subset.
- Final objective evidence: estate audit zero findings; Unity EditMode **92/92**; PlayMode **4/4**;
  HDRP tour **18/18**; pacing **15/15**; scene engine **10/10**; browser **47/47**; full release runner
  **9/9**; fresh macOS build; native proof **7/7**, all five Ninth Bell cues decoded, zero runtime
  errors, and 240-frame timing mean **8.33ms**, p95 **8.99ms**, p99 **9.53ms**.

This checkpoint graded the candidate A-. It is superseded by the fixed-night closure above; the
Nick walk/listen and Claude review gates remain unchanged, and Court remains locked.

### 2026-07-20 - Ninth Bell audio and Phase 0 automated closure

- Replaced the four remaining Ninth Bell stand-ins with a reproducible SHA-256-pinned curation
  pipeline: one CC0 archival grandfather-clock strike, a CC0 real resting heartbeat, a CC0 wordless
  human whisper, and project-authored seam-safe tinnitus. The existing Horror Elements chapel bell
  was re-encoded from Unity-incompatible Opus to Unity-safe Vorbis without changing its source.
- Fixed the crossing's low-pass filter so it lives on the AudioListener instead of a source-less
  Systems object. The whisper no longer loops, heartbeat and tinnitus cut at the transition, and
  crossing sources are parented and cleaned up.
- Audio intelligence schema v3 now separates actual boundary discontinuity from first/last-window
  texture change and records intentional tonal context. It correctly keeps tinnitus visible as a
  raw narrow-tone signature while classifying it as an internal symptom, not an environmental bed.
- Final evidence: fingerprint `672184a9d7a0456a`; scene report zero findings; 88/88 EditMode; 4/4
  PlayMode including the complete accelerated crossing; 18/18 HDRP tour; 7/7 native proof; 6/6
  variants exact-restore; 15/15 pacing; repo tests 12/12, 10/10, 23/23, 23/23 and 47/47; browser
  archaeology 9/9. The built player loaded and decoded 5/5 final cues and reported zero runtime
  errors. Native 240-frame sample: 8.51ms mean, 8.32ms p50, 9.49ms p95, 11.01ms p99, 15.97ms max.
- All agent-owned Phase 0 scene, interaction and audio categories are A-range. Nick's uninterrupted
  visual/listen/pacing walk is still the mandatory final 1%; Court remains locked.
- No purchase, commit or push.

### 2026-07-19 - Phase 0 A-grade remediation complete

- Removed the final purchased-tree sheet artifact at the scene LOD level, added three deterministic
  willow morphology families, and rejected both `foliage` and misspelled `foilage` asset paths.
- Raised cemetery and garden from their prior grade caps with varied family markers, a scaled
  memorial bench and sourced lantern, readable open-grave earth, a half-standing cultivation
  trellis, interrupted furrows, and an abandoned harvest-cart story cluster.
- The quality audit caught the first cart placement obstructing the garden route and caught a
  practical light separated from its semantic source. Both were fixed before acceptance.
- Final evidence: fingerprint `672184a9d7a0456a`; 86/86 EditMode; 3/3 PlayMode; zero-finding scene
  report; 18/18 HDRP tour; 7/7 native proof; 6/6 exact-restore variants; pacing 15/15; repo tests
  12/12, 10/10, 23/23, 23/23 and 47/47. Native proof is zero-error at 8.63ms mean / 9.94ms p95.
- Objective audio evidence marks both wind candidates `spaceship=False`; audio remains A
  provisional until Nick listens in motion.
- Full report: `docs/playtest/codex-phase0-a-grade-remediation-2026-07-19.md`.
- No purchase, commit or push. Court remains locked.

### 2026-07-19 - Final Phase 0 scene-engine and evidence pass

- Replaced cemetery/garden rendered floor ribbons with measured negative-space path guides and
  exact mesh-ray occupancy evidence.
- Rejected four bad soil materials and a black-bollard stake pass from actual GPU frames. Final
  garden uses broken dry-growth bands, discontinuous leaf/twig traces, ankle-height remnants and an
  authored work story.
- Fixed guarded variants so the garden swaps one detail, never a path/container; temporary
  candidates inherit authored surface controls without modifying pack assets.
- Final evidence: fingerprint `43523adf14ad0c57`; 80/80 EditMode; 3/3 PlayMode; 18/18 HDRP tour;
  7/7 native backbuffer proof; 6/6 variants with exact restore; pacing 15/15; repository 12/12,
  10/10, 23/23 and 47/47; browser gates 9/9; native Mac build has zero runtime errors. Native
  frame pacing at 1280x720 over 240 frames is 8.72ms mean, 8.77ms p95 and 24.78ms max.
- Full report and harsh grades:
  `docs/playtest/codex-phase0-engine-final-2026-07-19.md`.
- No purchase, commit or push. Phase 0 remains open for Claude and Nick.

| Area | State |
|------|--------|
| Prologue / Entry Hall / Parlor | Playable |
| House site history | **Canon** — `docs/superpowers/specs/2026-07-14-house-history.md` (coaching inn, not saloon) |
| Timeline / full asset plan | **`asset-catalog.html` · stages 0–8** + buy-now trio + later/never · asset book |
| Horror bundle | **BOUGHT $50** · historical intake recorded 9/9 downloaded; current cache not retained · 3 packs selectively imported in Unity |
| Car / gate / secret ending | Gate/facade/exit functional; modern SUV is the largest remaining visual asset mismatch |
| Court → Endings roadmap | **Court shell + STB shell** + Modular wall dress; full match still open |
| Local commits ahead of `origin/main` | **do not push without explicit approval** |
| Human playtest | None yet — still owed for Phase 0 opening |

## Phase checklist

### Phase 0 — Opening Rescue (NOW)
- [x] Detailed plan on disk (`docs/superpowers/plans/2026-07-13-opening-rescue.md`)
- [x] Catalog car + gate (license honesty — both tagged LICENSE UNKNOWN)
- [x] Car scale / placement / fallback (measured bbox → ~4.6 x 1.55 x 4.6)
- [x] Single coherent gate (removed procedural posts + swinging leaves; sourced gate only)
- [x] Live-walk playthrough harness (`scripts/play-gate.mjs`, `play-tour.mjs`) — drives real keyboard input with the RAF loop live (real clamp/beats), captures frames + devSnapshot. Walk-tour of the whole approach surfaced three big tells, now fixed: (1) the 26 mist sprites were pale `#aeb6cc` at 0.5-0.8 opacity and stacked into a bright wall that hid the "lit up like a birthday" mansion the entire drive → darkened to `#5a6274`, opacity 0.2-0.38, spread down to z=-70 and kept low; (2) the sourced iron_fence shipped bright white → recolored to near-black wrought iron; (3) gate/grille ironwork + pier lamps darkened so bars read as cold iron even face-first against the shut gate. Mansion now clearly visible + lit down the drive (docs/playtest/screenshots/app-*.png, tour-*.png). 35/35 tests pass, zero runtime errors across the playthrough.
- [x] Full live walk to the mansion + look-around + door entry (`scripts/play-full.mjs`, `play-door.mjs`, `capture-mansion.mjs`). Walk-up, cemetery/garden passes, porch flanks, and entry sequence verified. Fixes from this pass: (1) mansion porch flanks were bare → bushes/pots/broken columns + denser scatter band z=-52..-34; (2) pale curb / headstones / kit bushes / fence map wash → darkened; (3) open doors showed a flat black/white void because the mansion GLB is solid and occluded anything behind the facade → painted checker+gold hall glimpse on the doorway plate, swaps in as the leaves open; (4) arrival pitch held nearer to horizontal so the threshold is readable. Door sequence: threshold → climb → open → knockout → aftermath. 35/35 tests, errors:0.
- [x] Full pose-sweep of the estate off the walk tour (`scripts/capture-sweep.mjs`, `capture-approach.mjs`) found + fixed more tells: (1) kit dead trees shipped light tan bark → darkened to cold near-black so they read as bare silhouettes (lamp-lit ones by the drive stay warm, which is correct); (2) 32 "moss smear" planes were floating in open air as translucent glass panels through the graveyard → removed; (3) cut-stone walls/columns/pots read as pale clean cinderblocks → darkened harder (×0.24); (4) the cemetery/garden cold-pool lights were flooding nearby stone + trees pale → dimmed (0.85→0.55, 0.7→0.5) and range tightened. Cemetery now reads as a moonlit graveyard, garden as a ruined plot. 35/35 tests, live playthrough clean (gate locks + blocks, zero errors).
- [x] Front gate rebuilt as a real procedural swinging double-gate — dropped the sourced `graveyard_gate.glb` (single flared-open mesh that couldn't close; "lock" just spun the whole slab). Now: two drive-edge piers, grille wings sealing pier→fence, two hinged leaves that start open into the estate and slam shut on the lock beat. Fence extended to z=68 so it brackets the gate; lateral clamp funnels the player through the opening so you can't walk around the piers. Verified open/shut/turned-around (docs/playtest/screenshots/gate-*.png), rotations interpolate + recoil settle, 35/35 tests pass.
- [x] Fill approach dressing (trees/lamps/urns/fence extended to the porch)
- [x] Lit mansion facade (window emissive + warm wash; floating fake quads removed)
- [x] Harness: secretEnding jump + gate-lock latch + car-size sanity + estate-grounds build (34 passed)
- [x] Full estate exterior from `ruins_pack.glb` — real dead-tree avenue, cemetery, ruined garden, yard clutter (replaces box trees + green hedges)
- [x] Scale audit — every exterior object measured vs 1.7u eye height (see scale report below)
- [x] Visual gate screenshots + verdict — **PASS** (11 shots, drive + side + facade + sky + back)
- [x] Sourced open audio (CC-BY/CC0) + wired: wind bed, dark drone, footsteps, door creak (`assets/sfx/license.txt`)
- [x] Full opening element inventory — every scene/view classified (`docs/playtest/opening-inventory.md`)
- [x] Sky pass — moon/stars were fog-washed to invisible; now `fog:false`, moon over the drive + glow, 620 stars read (shots 01/09/10)
- [x] Fill the void behind the car at spawn — shut outer gate + lit piers + low walls + back treeline (shot 02)
- [x] Dense flank fill — ground scatter (rocks/weeds/logs/mounds) + denser avenue + mid-flank trees/bushes + far treeline (shots 01/02/04)
- [x] Driveway continuous 200u plane (no path/pathN seam) + darkened outer walls
- [x] Car polish: wet paint + headlights left on + taillights + beam pool (model itself still WEAK)
- [x] Falling-leaves particle layer + better car model + crickets/owl — all shipped and now **runtime-verified**, not just present in source (`scripts/verify-polish.mjs`): leaves drift + respawn (12/12 change y over 0.9s), crickets bed armed on the walk gesture, owl one-shot fires on its timer. Added reduce-motion honoring to the leaves + mist drift (was head-bob only) — with Reduce Motion ON the leaves freeze (0/12 move). Period Model T + close-ups read correct (`scripts/capture-car.mjs`, `probe-green.mjs` confirmed no stray green art). Aftermath → Continue → Entry Hall handoff lands in a real scene with narration continuing ("the dark took the rest" → "Cold. The marble had my cheek."), zero page errors (`scripts/verify-handoff.mjs`).
- [x] Leartes Horror Environments Bundle purchased for $50 — supersedes separate Dead Tree/Cemetery buy calls; no further asset spend until mined
- [x] Historical nine-payload download completed; current Unity cache is no longer retained.
  `npm run assets:horror:check` now proves the three selective project imports and fails honestly
  until a retained nine-package cache is supplied.
- [x] Unity adversarial repair — controlled night/ridges, bare trees, composed cemetery/garden,
  balanced building lights, amber mansion, reliable 12-shot tour, and non-mechanical exterior audio
  (`docs/playtest/codex-unity-repair-pass-2026-07-17.md`)
- [x] Unity final quality pass — deterministic estate audit/fingerprint, varied windows, one-in-three
  figure, staged coach yard, filtered audio A/B, 41/41 EditMode, 2/2 PlayMode, reliable 16-shot tour
  (`docs/playtest/codex-phase0-final-pass-2026-07-17.md`)
- [x] Unity Phase 0 takeback hardening — unpack-resistant one-mansion identity, direct seven-wall
  contact evidence, diagnosed-stall-only GUI retry with preserved attempt logs, and an 18-shot tour
  with exact same-camera figure cutoff pair. 42/42 EditMode, 3/3 PlayMode, audit fingerprint stable.
- [x] Cross-phase Unity scene factory — validated registry, dry-run scaffold, byte-drift sync,
  durable scene contract, reusable HDRP review tour, host-capacity guard, and seven-gate workflow.
  Live-proven on Wend Hill with 45/45 EditMode, 3/3 PlayMode, 18/18 shared-engine tour, and stable
  fingerprint.
- [ ] Nick plays/listens to the opening (mandatory before Court) — **human gate remains open**

### Phase 1 — Court
- [x] Build plan + asset book (`docs/superpowers/plans/2026-07-14-court-and-shut-the-box.md`)
- [x] Evidence deck draft copy (`docs/superpowers/specs/2026-07-14-court-evidence-draft.md`)
- [x] Asset inventory (`docs/playtest/phase1-2-asset-inventory.md`) — Table_Large / Chair_1 / candelabra / portraits ready; **gavel = procedural** (Poly Pizza CDN blocked)
- [x] Drop-in props module (`gm-court-props.js` — gavel/tarnish, wax seal, dust sheet)
- [x] Dining → Court redress notes + shots (`docs/playtest/dining-court-redress.md`, `dining-01..04`)
- [x] Harness prep note (`docs/playtest/harness-court-stb-prep.md`)
- [x] Court shell scene (`The Games Master - Court.dc.html`) — walk, gavel, evidence 1–5, seals, tarnish RAF, Entry Hall door "Take the hearing"
- [ ] Full Court: hall GLTF furniture, role-light swing, loseable pressure clock, shard card — next
- [ ] Nick plays opening (still owed; shell started on Nick's "wire a little" OK)

### Phase 2 — Shut the Box
- [x] Design folded into same plan + inventory (dice.glb ready; tiles procedural; tile-9 door specified)
- [x] Pure rules module + Node tests (`gm-shutbox-logic.js`, `scripts/test-shutbox.mjs` — 23/23; chained in `npm test`)
- [x] Procedural board builder (`gm-shutbox-board.js` — hinged tiles, `buildMatch`)
- [x] STB shell scene (`The Games Master - Shut the Box.dc.html`) — hall + boards + Aldric placeholder + demoShut9
- [ ] Full match AI + Hold cheats + tile-9 door — after Court polish

### Phases 3–6
- [ ] Shards → Hidden room → Persistence → Labyrinth

### Phase 7 — Six endings
- [ ] Not started · Nick playtest + AI panel after this

### Phase 8 — Manor puzzles
- [ ] Deferrable

### Phase 9 — Full polish
- [ ] Final playtest + panel

## Playtest / panel cadence

- Nick: after Phase 0, 2, 7, 9
- 9-persona AI panel: after Phase 2, 7, 9 only (not every phase)
- Push: only with explicit approval after tests + visual gate

## Visual gate (Phase 0)

Save under `docs/playtest/screenshots/`. Required:
- spawn facing house
- spawn looking at car
- gate
- mid-approach
- facade / door

Verdict: PASS / BORDERLINE / FAIL — never “verified” from unit tests alone.

## Scale report (exterior vs 1.7u eye height)

Measured world-space bounding boxes via `scripts/capture-phase0.mjs` (logs live Box3 sizes):

| Object | Height (u) | Read |
|--------|-----------:|------|
| Player eye | 1.70 | reference |
| Car | 1.55 (5.4 long) | sedan, correct |
| Headstones (16) | 0.75–1.59 | chest-to-head, correct |
| Fence panel | 2.73 | taller than eye, correct |
| Gate | 4.45 | looms, correct |
| Cemetery gothic arch | 4.70 | gateway, correct |
| Statue (stag monument) | 4.40 | monument, correct |
| Dead trees (52) | 6.2–8.3 | mature trees, correct |
| Mansion | 17.1 | 2.5-story + tower, correct |

No outliers. Everything scales sensibly against a 1.7u human.

## CHECKIN log

### 2026-07-17 — Codex Unity repair pass: D+ → B-, audio reactor removed, harness 47/47

Four supervised rebuild/tour iterations addressed the independent review's ranked defects. Final
Unity scene: controlled moonless GradientSky + ACES, four non-colliding terrain ridges, ridge woodland,
foliage/foilage tree exclusion, broken/lowered estate fence, authored cemetery and kitchen garden,
rendered-bounds outbuilding lights, amber mansion emission, dark gate/car treatment, 17/0 placements,
and twelve nonblank frames at mean luminance 14–27. Honest residuals: modern SUV, repeated cemetery
stone, sparse coach yard, close garden readability, uniform window lighting.

Nick's "spaceship" audio call was confirmed: wind 0.50 + dark drone 0.30 + a 3.326s cricket loop 0.26
were stacked continuously. The estate now has one quiet modulated wind loop (0.07–0.12), intermittent
crickets, and a rare owl; the dark drone is disabled outdoors. Horror Elements was inspected and not
used for the bed because its ambience list includes three `Amb_Deep_space` files and other designed
rumbles. Buy nothing pending Nick's ear check.

Automation was repaired at the root: timestamped one-shot Unity tour arming survives domain reload;
CLI recognizes every `[Gm…] … FAILED` spelling; the browser harness names current tests, deadlines
each at 30s, and unloads completed WebGL previews only in headless mode. Verification: **Unity 37/37,
browser 47/47, C# 23/23, JS 23/23, Unity tour 12/12**. No purchase, commit, or push.

### 2026-07-17 — Independent Codex adversarial Unity review
Fresh supervised rebuild, fresh post-rebuild 12-shot HDRP tour, EditMode tests, combined logic/browser tests, and static regression checks completed. Verdict: **D+ / visual FAIL** for “believable real horror-game opening.” Literal fixes confirmed: tiling mud/road, 29 grave crosses, car clearance, 17/0 placements, fixed exposure, no magenta, and no temp scripts. Major failures: cobalt dusk instead of night, flat horizon/terrain, sparse asset-scatter composition, blocky leafy trees, overexposed dollhouse mansion, modern white SUV, chapel crushed dark, and brittle automation. Unity 34/34, C# parity 23/23, JS 23/23; browser harness timed out at 4/47. Steam dashboard corrected from 14% overall / 60% Prologue to **12% / 45%**. Review: `docs/playtest/codex-adversarial-review-2026-07-17.md`.

### 2026-07-17 (cont. 2) — THE LAYERS PASS: story tightened, secrets planted, theory engine armed
Nick: "tighten the story, small things to find, clues, easter eggs, layers people can fan about."
Shipped (all verified by the extended verify-g2 — knock/figure/two-press asserts — plus eyeball):

- **Two-layer examines:** every one of the 13 POIs now has a second E-press line — the wrong
  detail under the first observation. Highlights: the coach-house door frame has "41. 9." and a
  scratched-out third number chalked inside it, decades old (the narrator's exact debts); the
  fallen headstone he "chose not to read" carries his own surname ("It's a common name. It's a
  common name."); the child's marker reads COUNTED OUT; the fountain coins are all heads-down;
  the shed's small boot prints go in and never come back; the well pebble never lands.
- **The stag crest motif:** the invitation's wax seal is now "a stag, one antler snapped" — cold
  open → cemetery monument ("snapped clean off. Deliberate.") → plinth: "FOR THE HOUSE, FROM ITS
  WINNERS." One symbol, three sites, zero explanation.
- **THE WINDOW FIGURE:** ~1 walk in 3, an upper window that is never otherwise lit glows warm
  with a thin silhouette in it — readable from the gate and the drive, permanently gone once you
  pass z<18. Dev-forceable (`_figureForce`) and rig-verified (appears far / vanishes near).
- **The chapel knocks back:** stand in the forecourt through three bell tolls and something
  answers once from inside, low. Rig-verified.
- **Wend motif locked:** the secret ending closes on "the road only turns one way."
- **Tally strokes** on the gate pier plaque, grouped in sevens.

**Bug found by the layer probe (fixed at the root):** freshly mounted GLB materials displayed
gamma-lifted until the 30-frame periodic sweep ticked — the mansion visibly popped white after
its GLB streamed in. `loadGLTF` now sweeps immediately after every mount callback.

Verification: `verify-g2` extended (bell gating, dying lamp, chapel knock, figure lifecycle,
two-layer examine) — PASS. Full `npm run gates` re-run follows this entry.

### 2026-07-17 (cont.) — ONE-COMMAND GATES + the regression that proved them
`npm run gates` (scripts/run-gates.mjs) now runs all nine gates in sequence with a pass/fail
table: tests → agent playtest → door → full walk → env → breath → g2 → handoff → polish.
`docs/TESTING.md` documents every harness, six hard-won harness rules, a ten-row fix playbook
(defect class → the proven fix), and the honest coverage boundaries (court/stb gameplay depth,
hall POI walk, audio mix, real-GPU feel = human).

Rig upgrades: stops are now AUTO-DERIVED from the live POI list + walk rects (new content is
covered without editing the rig; an unreachable POI = automatic HIGH), and perf is sampled at
boot with budget thresholds (5,200 calls / 1.8M tris) inside the report.

The gates' first full run immediately caught a real regression: the repo-wide waitForFunction
arg-fix made every WRITTEN timeout real — including several written SHORTER than the broken 30s
default they had always effectively run at. The porch settle-glide (dt-scaled camera animation,
20–30s wall-clock at headless fps) lost the race against a now-real 20s wait. Fix: play-door
nudges the glide to the settle plane (animation, not gameplay), and every remaining sub-30s
real-time wait was raised. Definitive run after: **9/9 GATES GREEN**, playtest HIGH:0/MED:0/LOW:0
across 23 stops (18 curated + 5 auto-derived), perf honestly measured at boot: 3,665 calls /
1,038,504 tris / 21 textures — inside budget.

### 2026-07-17 — AGENT PLAYTEST RIG: the game now plays itself and reports findings
New standing capability: `npm run playtest:agent` (`scripts/agent-playtest.mjs`) plays the entire
opening like a player and emits ranked findings (`docs/playtest/agent-report.json` + ~30 apt-*.png):
1. GEOMETRY — all ~1,470 tagged objects: position, size vs per-kind expected ranges,
   floating/sunken/tilt checks, building overlaps, POI reachability from the walk rects.
2. COVERAGE — 925-sample grid over the 8 walk rects hunting stuck pockets.
3. WALKTHROUGH — 18 stops along the golden route with REAL key input at each (proves local
   walkability), E pressed at every POI with the line asserted, per-frame luminance metering,
   gate-lock beat, arrival → KO → aftermath, doors-stay-shut assert.
4. SCENES — Entry Hall / Court / STB boot + dev-phase screenshots + error counts.

Converged over 5 runs (each run either found a real defect or a wrong rig expectation; both fixed):

**Real game defects found by the rig, all fixed + re-verified:**
- Coach house's rotated AABB collision box ([-36.9,-23.1]×[41.4,58.6]) swallowed nearly its whole
  yard — the destination was effectively unwalkable. Building moved west; cart/trough followed;
  the yard has a real lane now.
- Stag monument + dry fountain examine radii were smaller than their own collision clearance — a
  player could never physically trigger those lines. Radii widened past block reach (3.9/3.5).
- Chapel door + coach yard frames measured near-black (p90 raw 1–2) — building values lifted a
  notch + two cold moon-pools added; both frames clear the meter now.
- Shed POI sat inside its building's collision box; relocated.
- `_walkRects` was first-frame lazy — eager now so tools can read the world's walkable shape.

**Final run: HIGH:0 MED:0 LOW:0** — 13/13 POIs verified in-game, 0 stuck pockets, aftermath
reached with doors 0,0, hall/court/stb boot with 0 errors. This rig is the standing regression
gate for every future grounds change.

### 2026-07-16 (late night) — G2 shipped + cross-repo audit
**Audit (Nick's ask):** async-test-without-await swept across every other repo — 757 JS/TS test
files + ~300 Python. Zero real hits (flagged JS lines were synchronous jest-dom matchers; omnivore's
async tests are correctly configured with pytest-asyncio auto mode). canvas-scraper had the same
Playwright waitForFunction trap as this repo — 3 sites fixed there (its "2s" page waits were
silently 30s each).

**G2 grounds (from the full-grounds plan, all verified):** well in the garden (POI: "the dark down
there has a smell"), ember braziers with dying point lights at both path junctions, feeding trough
at the coach house, hand lantern on the cemetery bench — mined via hash-targeted extraction from
the Abandoned Village + Witch Village payloads (no 4GB full unpacks). Chapel bell: single 7s toll
(Horror Elements, licensed), random 28–50s timer, fires ONLY in the chapel forecourt — verify-g2
asserts both sides of the gate. One drive lamp now visibly dying (deep swings + blackouts). Three
new examine POIs (13 total): the well, a face-down headstone, a child's marker. Chapel base broken
up with stone-wall segments. Trunk totem: converted, previewed, rejected (bland).

**Bug the new verifier caught on first run (pre-existing):** the shared lamp flicker subtracted a
flat 0.5 from every lamp on dip frames — lamps with base < 0.5 (gate pier lamps 0.26, new embers
0.34) went NEGATIVE, and negative PointLight intensity in r128 subtracts light (a dark flash).
Dips now scale with each lamp's base and clamp at 0.

**Gates (Z-battery, all post-change):** tests 23+47 · play-door · play-full · env · breath ·
verify-g2 · handoff · polish · baseline — ALL PASS, chain exit 0. Perf: 3,982 calls / 1,297,065
tris / 26 textures / 0 errors (grounds total +46% tris over the pre-estate corridor — that is the
cost of an actual estate, logged and fine on hardware GPUs).

### 2026-07-16 (night) — Full-grounds plan + G1: examine system, lychgate/chapel court, coach yard, WEND HILL plaque
Master plan written: `docs/superpowers/plans/2026-07-16-full-grounds-plan.md` — the complete
walkway network (W1–W5), every destination/POI, audio POIs, per-payload asset sourcing for the
rest of the bundle, and G1/G2/G3 phasing with a verification contract.

**G1 shipped (all live-verified via the extended `capture-estate` gate):**
- Examine system: E near a point of interest shows one interior-monologue line (new quiet HUD
  element + "E · look" proximity hint; hidden during beats/letter). Ten POIs across the grounds —
  car keys, WEND HILL plaque, open grave, stag crest, legible headstone date, chapel door,
  scarecrow, dry fountain, shed padlock, coach doors. The legend's "E interact" finally does
  something in the Prologue. Gate asserts the grave line fires: PASS.
- W2b: lychgate (timber posts + gable) at the cemetery's east wall → walkable chapel forecourt;
  the chapel's own plank door (Leartes SM_Door_01 — right at home on the rustic chapel) mounted
  shut. W3b: west connector + coach-house yard walkable. Three new walk rects, all reachability-
  asserted: PASS.
- WEND HILL plaque: carved stone plate flush on the east gate pier (first mount was a floating
  billboard — caught by the shot, remounted 0.56×0.28 on the shaft). Reads as weathered stone;
  canon name planted at the threshold.
- Fix: examine line no longer collides with beat text (hidden while beatOn).

(The repo-wide `waitForFunction` timeout bug — 82 call sites across 36 scripts — is detailed in
the evening check-in below; it was found and fixed during this pass.)

### 2026-07-16 (evening) — Nick's walk verdict: "shit art / no cemetery path / sticker doors" → estate rebuild
Nick's three complaints, all confirmed by reading the shots with his eyes: the cemetery was a
diorama strip behind bars with no way in, the estate was a fenced corridor, and the mansion door
was a painted plate floating proud of a flat facade. Only 6 files had been mined from the $50
Leartes bundle. Shipped this pass:

**Flush recessed entrance (real geometry):** killed the floating overlay planes and the invisible
Door_Double shells. The doorway is now a real reveal — deep jambs + lintel + architrave trim +
stone threshold — with solid thick leaves (painted-panel faces, raised rails/stiles as geometry)
hinged at the jambs, sitting flush INSIDE the wall. The hinges rotate like any game door
(verify-env-entrance literally opens them for its shots); the Threshold Refusal canon keeps them
shut in-story. Door canvas lifted one notch so panels read inside the reveal shadow. Dead code
removed (_mountOwnedFrontDoors, _dressDoorPanelOverlays, doorMat).

**Walkable side paths:** the ±3 corridor clamp is gone, replaced by a walk-rect union (drive +
2 gravel paths + 2 plots) with slide-along-edges resolution and solid AABBs for buildings and
monuments. Fence openings cut at z 28.5 (east → cemetery) and z 22 (west → garden) with iron
gate posts; gravel path strips + rustic Leartes fence runs line both branches. Live-walk proof in
`capture-estate.mjs`: the harness physically walks off the drive into the cemetery (PASS, x=8.8).

**Estate build-out (Haunted Village payload, 16 FBX → GLB via Blender):** chapel with spire as
the cemetery landmark, coach house on the west approach (the site's coaching-inn bones), grounds-
keeper shed in the garden, cart, scarecrow, bench, open grave pit + leaning shovel, bucket. Night
treatment matches the mansion (charcoal silhouettes; textures didn't survive the Unity-GUID
material refs, documented in assets/models/unity/CREDITS.txt). Two moon-pools light the walkable
plot ground (Std-material ground only — all props are unlit MeshBasic, nothing bleaches).

**Perf (measured):** 3,794 → 3,884 calls (+90), 891k → 1,195k tris (+34%), textures 24→25, still
0 errors / 0 failed requests. The +34% is nine buildings; trivial for a hardware GPU, logged
honestly. Headless-harness note: the estate mesh load dropped software-rendering fps enough that
real-time waits on arrival holds became unbounded — play-full/verify-breath/play-door now advance
the hold timer explicitly (same technique as verify-polish's owl timer); real-time pacing is the
human gate's job.

**Repo-wide harness bug found while chasing the flakes:** every script called Playwright as
`waitForFunction(fn, { timeout: X })` — but the signature is `waitForFunction(fn, arg, options)`,
so the timeout object was passed as `arg` and every custom timeout silently fell back to the 30s
default. Every "120s" wait this project ever wrote was a no-op. Fixed across all 36 scripts
(82 call sites): `waitForFunction(fn, null, { timeout: X })`. This explains months of load-
sensitive harness flakiness in one line.

**Gates (all this pass, post-changes):** `npm test` 23+47 PASS · `play-door` aftermath errors:0
doors:0 · `play-full` aftermath errors:0 doors:0 · `verify-env-entrance` PASS (doors open/close on
hinges) · `verify-breath` PASS · `capture-estate` PASS incl. live walk-into-cemetery · baseline
0 errors. New shots: `estate-01..11`, fresh `door-*`/`full-*`.

**Honest residuals:** estate buildings are untextured silhouettes (atlas mapping via Unity GUID
parse is future work if daylight detail is ever wanted); door faces still canvas-painted panels
(real relief geometry for rails/stiles only); chapel style is Slavic-wooden rather than English
gothic (reads fine as a night silhouette, flagged for Nick's judgment). Nick walk still the gate.

### 2026-07-16 (later) — "Get Phase 0 to 100%": copy, every-state, pacing, and hall-wake pass
Nick asked for a full re-check of story, visuals, and writing before his walk. Everything below was
found by actually capturing/measuring, and every fix was re-verified live.

**Story/writing (fiction-tell-detector verdict: PASS — zero AI tells, all names/numbers consistent):**
- Fixed a hard timeline contradiction: cold open said Mara was gone "eight months" while the drive
  beats say the gambling started at her death and has run "two years." Cold open now plants "two
  years," which beat 5 pays off.
- "The house on Wend Hill" existed only on the invitation — canonized in the house-history spec so
  later rooms/host speech use it consistently.
- Beat 2 rewritten per the detector's two flags: "Nine thousand more" (unit was ambiguous at a 4s
  read) and the beat now ends on a flat fact instead of a 9th-consecutive clincher line.

**Every-screen/every-state capture (new `capture-states.mjs`, 17 states + small viewport ×2 runs):**
- Options sliders were stock browser blue in a sepia gothic UI → gold accent.
- Controls legend + cold-open hint + letter hint all sat inside the 8vh letterbox bars (bottom
  4.5/7/5vh, bars z-index above) → moved to 9.5vh; verified unclipped at 1280 and 1024.
- Prologue legend taught "F light" but the flashlight doesn't exist until inside the mansion →
  trimmed from this scene only.
- Letter flip verified front/back mid-walk; back's "faint impression" foreshadow reads.

**Pacing (measured, not vibes):** the porch-dark beat — 22 words — displayed for 1.25s before the KO
wiped it. Now: glimpse ~3s, porch-dark ~3.2s (threshold 3.2, knock 6.4). The breath one-shot plays
under the held beat. Note for harness authors: headless software rendering runs this scene at a few
fps and dt clamps at 0.05, so arrival holds accrue at ~1/6 wall-clock — play-door/play-full/breath
wait timeouts raised to 60–90s after play-full timed out at the old 30s (root-caused, not papered).

**Entry Hall wake path (aftermath lands here — it IS part of the opening):**
- Premise correction: Hall/Court/STB never had the Prologue's sRGB lift (no outputEncoding set) —
  handoff doc corrected; no pipeline change made there.
- Removed a dead FogExp2 line (immediately overwritten by linear Fog).
- The sloped stair handrail centered 2u below the baluster tops and cut through their waists —
  reading as a rod floating across the staircase; recentered onto the baluster-top line and
  thickened balusters 0.05→0.09/0.11 so the rail visibly has legs. Wake→hall→portraits→stairs→door
  all captured clean, 0 errors, doors shut, wake copy canon-true ("No memory of a doorway").

**Visual residuals closed:** headstones now carry a weathered-stone canvas (was blank primitives);
door face canvas gained edge AO + water tails (was flat at nose distance); mist verified intact.
Note: `verify-visual-baseline` brightNonWindow rose 18→77 — false positives from the headstone
material's WHITE TINT (the dark value is in its map; probe reads color only). Display measured 27–46.

**Audio:** all opening one-shots + beds verified firing (breath included, `verify-breath.mjs` PASS);
`door_creak.ogg` correctly unused in closed-door canon; two dead `_doorCreakPlayed` writes removed.

**Gates (final, all post-change):** `npm test` 23+47 PASS · `play-door` aftermath errors:0 doors:0 ·
`play-full` aftermath errors:0 doors:0 (after timeout fix) · `verify-breath` PASS · `verify-env-entrance`
PASS · `verify-handoff` PASS pageerrors:0 · `verify-polish` ALL PASS · hall-wake capture PASS ·
baseline: 0 errors / 0 failed requests / 3,794 calls / 891,180 tris / 24 textures.

**Verdict:** Phase 0 at 92% — everything agent-verifiable is verified. The remaining 8% is Nick's
walk plus whatever it surfaces. Honest residuals unchanged: painted door panels at nose distance,
primitive headstone geometry (now textured), kit-tree silhouettes up close.

### 2026-07-16 — ROOT CAUSE FOUND + FIXED: r128 color pipeline was gamma-lifting every flat material
Three r128 with `renderer.outputEncoding = sRGBEncoding` re-encodes every fragment at present time, so
any **map-less** material's hex was treated as linear and displayed ~2.2x lighter. Authored `#050403`
trees could never display below ~#262220 — a hard display floor. Every "pale styrofoam / cardboard /
washed" tell Nick flagged across three days of darkening passes was this one bug; the passes were
authoring ever-darker hexes against a floor they could never get under. Fog had the same flaw (fog
mixes pre-encode), which made distance fade BRIGHTER than the sky — that's what motivated the old
`fog:false` hacks on trees, which then froze them as constant-value cutouts (no depth fade at all).

**Shipped:**
- `_linearizeFlatColors()` sweep: map-less material colors converted to linear once (flag-guarded),
  re-run every 30 RAF frames so async GLB mounts get the same treatment. Display now == authored hex.
- `scene.fog.color.convertSRGBToLinear()` — distance fade sinks into the night instead of washing pale.
- Reverted `fog:false` on the grounds (kit trees, owned trees, willows, bushes, night stone, cemetery
  walls, crosses, env fence). Sky (moon/stars/glow) keeps `fog:false` correctly. The mansion + door
  hardware keep `fog:false` deliberately: at 124u FogExp2(0.019) is ~97% opaque and the lit house must
  stay visible from the gate ("lit up like a birthday" canon).
- Leartes willows: the dresser loaded 3 purchased WebP maps then never assigned them — and alphaTest
  without a map is a no-op, so branch canopies rendered as SOLID cards (the flat-cutout tree tell).
  Maps restored under dark tints; branch silhouettes are real alpha cutouts now.
- Door overlay faces carried a flat `map:null` stamp — now carry the baked paneled-wood canvas.
- Facade retune for the honest pipeline: lit-window chance 0.28→0.45, emissive 0.38→0.9 warmer hex,
  body #0b0908→#171310 so the roofline silhouettes against the sky.
- Free "Horror Elements" (Anthon, Asset Store) reviewed: 193MB, 49 WAVs, audio-only. Selected ONE:
  `porch_breath.ogg` on the "Something moved in the porch dark" beat (new `verify-breath.mjs` PASS —
  spied playSfx, breath + latch both fire, 0 errors). License logged in `assets/sfx/license.txt`;
  intake doc `docs/playtest/horror-elements-intake.md`. Rest deliberately untapped.

**Measured (PNG pixels, before → after the fix, same poses):**
- porch closed-door: median 59→13, max 117→177 (beige veil dead; blacks black, highlights burn)
- cemetery: p90 51→23 · spawn: p90 38→20 · all poses keep moon/window highlights (170-255)
- baseline probe: errors 0, failed requests 0, calls 3797→3746, tris 885.6k→889.6k (+0.45%),
  textures 20→23 (the 3 willow maps). No performance regression.

**Gates (all this session, post-fix):** `npm test` 23+47 PASS · `play-door` aftermath errors:0 doors:0
(twice — re-run after the breath wire) · `play-full` aftermath errors:0 · `verify-env-entrance` PASS ·
`verify-handoff` PASS pageerrors:0 · `verify-polish` ALL PASS · `verify-breath` PASS.

**Verdict:** mechanics/canon PASS. Agent visual gate at eye-level poses: the opening finally reads as
a horror game — black Victorian silhouette with burning windows, textured willow bark + hanging moss,
iron-black fence, real depth fade. Honest residuals: primitive headstone boxes and painted (not
modeled) door panels at nose distance. Phase 0 88%; **the deciding gate is Nick's walk** — exact
instructions refreshed in `docs/NICK-NEEDED.md`. Progress 76 → 78 overall.

### 2026-07-16 — Runtime baseline refresh
Visual baseline probe remains clean: 0 page errors, 0 failed requests, 5,010 visible meshes, 3,671 render calls, and 886,230 triangles. Runtime snapshot confirms ruins grounds, 20 Leartes willows, 12 crosses, and Unity front doors are active. No phase percentage changes: Phase 0 remains 82% and is still blocked on the outdoor visual screenshot gate plus Nick's walk.

### 2026-07-16 — Rebuild status refresh
The live visual baseline remains healthy: page errors 0, failed requests 0, 5,106 visible meshes, 3,792 render calls, 885,739 triangles. Runtime snapshot confirms the car, 40 owned dead-tree placements, 20 Leartes willows, 12 cemetery crosses, Env fence, Env porch sconces, and Unity front doors are all mounted. Overall progress remains 76% because the outdoor visual gate and Nick Phase 0 walk are still open; the free Horror Elements package remains unintegrated pending license/content review.

### 2026-07-15 — Rebuild progress check-in
The opening rebuild remains behaviorally stable: gate/door canon, asset wiring, full-walk harnesses, and polish checks are passing. The mansion facade received a controlled material pass to prevent white imported atlases from bleaching the structure. The remaining Phase 0 work is visual: improve the outdoor drive/cemetery/garden silhouettes and complete the screenshot matrix without introducing a dark-frame or performance regression. The newly grabbed free Horror Elements package has not been integrated yet; it is pending inventory and license review.

### 2026-07-15 — Global progress dashboard rule
Added a required check-in protocol to `docs/CODEX-HANDOFF.md`: Codex must refresh this dashboard before every progress report, include the overall bar, phase bars, completed work, and the next blocker. Percentages are delivery estimates and do not replace visual or runtime gates.

### 2026-07-15 — Horror bundle purchase + fresh Codex re-audit
Nick bought Leartes Studios' nine-pack Horror Environments Bundle for $50. This is now the source library for opening vegetation/grounds and later-room decoration; no separate tree/cemetery purchase remains approved.

**Fresh gates:** `npm test` = 23 STB + 47 harness PASS · `play-door` aftermath/errors:0/doors:0 · `play-full` aftermath/errors:0/doors:0. Current screenshots re-read: mechanics/canon PASS, opening mood BORDERLINE, shipped-art bar FAIL on low-poly grounds/gate surrounds/porch framing.

**Intake:** Unity cached only `Horror Environments Bundle 9 Packs.unitypackage` (5.4 KB). The included publisher readme says to download every owned pack from its own Package Content link. Added `npm run assets:horror:check` (currently 0/9), `docs/playtest/horror-bundle-intake.md`, refreshed `docs/NICK-NEEDED.md`, and retired stale buy guidance in `asset-catalog.html`. Catalog visual gate: `catalog-horror-bundle-intake.png`, hash navigation PASS, pageerrors:0.

**Lock:** existing mansion shell and closed-door Threshold Refusal remain unchanged. Integration waits on the nine package payloads and will be selective/optimized rather than whole-scene shipping.

### 2026-07-15 — Payload-ready selective intake
Nick downloaded all nine Leartes payload packages. `npm run assets:horror:check` reports **9/9 ready** (approximately 30 GB in the Unity cache). The existing selective opening intake is now live: 20 Leartes willow placements, 12 grave crosses and authored stone-wall segments report ready in `verify-env-entrance.mjs`; no full Unity demo scenes are served.

Fresh gates after the material pass: `npm test` = 23 STB + 47 harness PASS; `verify-env-entrance` PASS; `play-door` reaches aftermath with `errors:0`, `leartesTreesReady:true`, `leartesGroundsReady:true`, and closed front doors. Re-read `env-outgate-01-lookback.png`, `env-door-01-closed.png`, `door-03-closed-hold.png`, and `door-03b-porch-dark.png`: mechanics/canon **PASS**, shipped-art bar **still FAIL/BORDERLINE** because the gate look-back and porch remain visibly low-poly/procedural. Nick Phase 0 walk is still required before Court is declared complete.

### 2026-07-15 — Codex cold review + agent-owned opening pass
Executed Part 1 through the human gate. Audit: `docs/playtest/codex-review-2026-07-15.md`.

**Shipped:**
- Hardened `play-door`, `play-full`, and `play-gate`: required waits no longer fail silently; page errors/final states are asserted; door/full runs assert both front leaves remain at rotation 0.
- Corrected the door harness's threshold pose and stale open-door wording.
- Restored the Gravyart facade's weathered maps under dark MeshBasic tint instead of flattening porch/facade architecture to grey shapes.
- Normalized/recentered `Door_Handle_01` by full GLB bounds; handles no longer read as bright paddles.
- Added four owned BotD hero-tree placements; dev snapshot now proves `ownedTreesReady:true`, `ownedTreeCount:40` before the full visual walk.

**Gates:** `npm test` = 23 STB + 47 harness PASS · `play-gate` PASS · `play-door` aftermath/errors:0/doors:0 · `play-full` aftermath/errors:0/doors:0 · `verify-env-entrance` PASS · `verify-handoff` PASS. Read new `door-*` and `full-*` screenshots.

**Verdict:** mechanics/canon **PASS**. Porch/facade is materially improved. Overall shipped visual remains **BORDERLINE↑** because the grounds still expose low-poly tree/ruin geometry. No purchase made; Nick's walk remains the gate and the Dead Tree Pack remains his post-walk buy call.

### 2026-07-14 (night) — Outside-gate rebuild (Nick: “looks shit”)
Look-back past the car was box piers + one slab wall + sparse bone trees. Rebuilt:
- Weathered canvas-stone tiered piers + segmented walls + spear grille
- Iron fence now runs to z=102 (past outer gate)
- Denser back trees + bushes + ruins wall/column dress on outer perimeter
- Outer mist band; satchel off mid-path onto grass curb
Shots: `outgate-01..04`. Hard refresh before looking back at spawn.

### 2026-07-14 (night) — Extra miss loops + Nick guide
Ran another audit loop after Nick noted we keep finding leftovers.

**New misses found + fixed:**
- Drive `lantern.glb` still `#d8d8d8` Std (chalk posts) → MeshBasic iron + warm glass
- Rocks/logs/mounds Std → MeshBasic (no more lamp bleach)
- Door leaf map defaulted white multiply → tint `#1a1612`
- Cobble-edge leaf litter densified; cold open soft wind/dark pad
- Weed clumps tagged `gmKind:'tuft'`

**Probe after:** `paleLantern:0`, `stdRock:0`, `basicRock:199`. Horror shots + `npm test` 23+47 PASS.

**Shipped docs:** `docs/NICK-NEEDED.md` — what only Nick can do + how to manually edit placements/models/console.

**Still FAIL vs shipped horror art** (kit shape + gravyart). Blocking human item: Nick Phase 0 walk. Progress **74 → 75**.

### 2026-07-14 (session) — Fix leftovers; keep hunting misses
Nick: fix everything still found, keep finding misses, add missing elements.

**Shipped this pass:**
- Car: killed floating headlight lens/halo + SpotLight/beam pool bleach; soft near-black tire scuffs only; HD light mesh emissives dialed down
- Outer piers/walls near-black MeshBasic; quieter pier lamps
- Gravyart: MeshBasic shell keeps weathered maps (drop-map was flat greybox); white lit windows clamped; Cube043 group hidden; doors thicker with proud rails/panels; quieter porch washes
- Ruins: stone `#080706`, statues `#1c1a16`, headstone insets + cemetery ivy cling on wall planes; cold pools stay off
- Env porch columns/rails/plinth → MeshBasic near-black (not Std bleach)
- Drive lamp posts MeshBasic; quieter lantern fills; grass tufts/#hero darker
- Atmosphere: menu soft wind + owl timer; owl asset already on disk
- Court/STB HTML vignette soft + hemi up; Entry Hall headache vignette softer

**Measured (PNG pixels, not vision guesses):**
- Car arrival cemetery wall/tree ~RGB 7–15 (near black) — headlight float discs gone
- Door rails ~39 — dark, not chalk
- Cemetery mid ~55 — muted, not styrofoam
- Porch approach door/center still ~120 warm (gravyart + door paint) — still flat facade read

**Gates:** `capture-horror` done · `play-full` errors:0 gate lock + `env-lamp` · `verify-handoff` PASS · `npm test` 23+47 PASS

**Verdict:** playable opening mood **BORDERLINE↑**. Shipped horror look still **FAIL** (gravyart facade + kit trees/stone geometry). Progress **72 → 74**. Nick walk still owed.

### 2026-07-15 (late) — Honesty check: is this an actual horror game?
Nick asked if I really walked it and whether it looks like a horror game.

**Answer: No. Not yet.** Four mid-polish fixes were real. Claiming "done/horror" oversold.

**Full live walk tonight** (`play-full.mjs`): spawn→gate lock→cemetery→porch→arrival, `porchSconces:'env-lamp'`, errors:0. Then read shots hard.

**What still fails the horror bar:**
- Gravyart mansion shell (flat weathered paint, not Env kit architecture)
- Cemetery/ruin walls still read as pale greybox blocks under any light
- Night was parking-lot bright (ambient 0.62 / fog 0.011) before tonight's pass
- Low-poly avenue trees stay kit silhouettes

**Atmosphere pass shipped same session** (partially mitigates, does not finish):
- FogExp2 0.019, ambient 0.28, moon dir 0.28
- Darker mist / path / ground; sparse window emissives (~38% lit, dimmer)
- Harder stone/ruin darken; dimmer cold pools + drive lamps + facade washes
- Shots: `horror-01..06.png` — mid-drive now loses the house into fog (good), porch still gravyart

**Verdict:** agent gate for "playable opening with mood" = BORDERLINE. Agent gate for "shipped horror look" = **FAIL**. Progress dropped 91 → **72**.

### 2026-07-15 — Four mid-polish fixes (agent gate)
Nick: car clutter fighting HD model; hall frames black voids; Court/STB too dark for Modular; Env money unused on facade.

**Shipped:**
1. **Car keep-clear** — wider `nearParkedCar` (7.2×9.5), tagged scatter (`rock`/`tuft`/`litter`/`mound`/`log`), AABB `_clearCarFootprint` after place + 2.5s sweep; dropped trunk/fake-door dress that fought the hatch silhouette. Probe: **0** scatter meshes within 8u of car center.
2. **Hall frames** — Modular walls parked outboard; gilt portraits pulled into the hall; `MeshBasic` + `toneMapped:false` canvases + picture lights. Shot `fix-03d-portrait.png` shows face, not void.
3. **Court / STB** — ambient/hemi/wall-washes up; Modular wall tint raised in `dressModular`; HTML vignettes softened (was eating panels). `fix-05d-court-lit.png` / `fix-06e-court-side.png` read panel grid + jury lights.
4. **Env porch** — root cause: `SM_MansionLamp.glb` is **8.7MB** and lost the old 5s timeout race. Now `preloadEnvPorchKit()` early; Env lamps replace brackets when ready (`_porchSconcesBuilt:'env-lamp'`). 31MB `SM_ColumnFlower` skipped (stalls boot); lowpoly Victorian columns dress flanks instead. `fix-02d-porch.png`.

**Gate:** `npm test` 23 STB + 47 harness PASS. Shots under `docs/playtest/screenshots/fix-*.png`.  
**Still open:** Nick Phase 0 walk; full Env facade swap still deferred (no hinged door leaves).

### 2026-07-14 (evening) — Physical + visual QA battery
- New harness `scripts/qa-opening-phys.mjs` — car upright, gate pass/lock/block, funnel, facade, hall textures, Court/STB boot. **QA OPENING PHYS PASS** solo.
- Live `play-gate.mjs`: lock fires, blocked at z≈64.2. `play-full.mjs`: spawn→cemetery→porch→arrival→knockout→aftermath, errors:0.
- `verify-handoff.mjs` PASS · `verify-polish.mjs` PASS (leaves/crickets/owl).
- Fixes from reading shots: gate iron → MeshBasic (headlights no longer bleach bars pale); car keep-clear scatter footprint; hall floor metalness down (less white glare); car seats +0.03u.
- `npm test`: 23 STB + 47 harness.

### 2026-07-14 (school) — Trio rework to clearly working
- Nick: at school, spend the time reworking until it clearly works (visual gate, not mechanical-only).
- **Car HD 03:** upright hatch at spawn (`1.93×1.55×3.83`); mesh-name paint; Model T / `old_car` gone. Canvas shots: `trio-gate-car-front/3q/side.png`.
- **Modular Interior:** 102 GLB + `dressModular()` with 1K wood/plaster under `_tex/`. Entry Hall textured wainscot + wallpaper (not white slabs). Court + STB walls dressed; Hall forward is the cleanest read (`trio-gate-hall-forward.png`).
- **gravyart facade kept:** Env `SM_Wall_Door` has no hinged leaves. Porch sconces stay procedural brackets, fired from mansion onLoad so doors never float without the house (`trio-gate-porch-lamps.png`).
- Gate: `scripts/verify-trio.mjs` + canvas-only screenshots. `npm test`: 23 STB + 47 harness.
- Still open: Env demo/Unity facade export if we want full Env shell; Court/STB wall close-ups are dark (fills help capture); Nick Phase 0 walk; catalog tag polish BUY→HAVE.

### 2026-07-14 — Catalog: complete asset need list
- Replaced abbreviated need section with full checklist in `asset-catalog.html`: tooling, SFX, textures, Prologue→Labyrinth, endings, host, history scars, UI, owned Unity mine-first (~100+ rows). Buy holes still: modern car, optional facade, taproom scar props.
- Shots: `catalog-complete-need-01.png`, `catalog-complete-scars-01.png`.

### 2026-07-14 — Buy-now trio locked (~$145)
- Nick: rather get the best needed assets now and get it out of the way.
- Cart: Modular $35 + Victorian Env $80 (door trailer gate) + Car HD 03 $30 ≈ **$145**. Catalog + asset book updated. No more drip advice.

### 2026-07-14 — Priority flip: house over car
- *(Superseded same day by buy-now trio.)*

### 2026-07-14 — Final catalog: Unity car pick + absolute need list
- `asset-catalog.html` header, timeline P0, Budget lock, Arrival Car card (BUY PAID PBR · DO NOT SHIP), need-list rows, Tier 0 podium, buy pyramid, footer — all point at paid PBR car + Env + Modular. Free/toy cars rejected on-page.

### 2026-07-14 — Quality bar: no toy cars
- Nick: don’t justify bad/free assets; final product must look and play good.
- Cart revised: **paid PBR car ~$25–45 required** (CGTrader PurePolygon sedan/hatch shortlist). Free Sketchfab / Quaternius / Unity $5 stylized **demoted**. Env $80 + Modular $35 → honest ~$145–$155 floor.
- Catalog Budget lock + asset book §4 updated.

### 2026-07-14 — $150 budget cart locked
- Nick: affordable ~$120–150 for assets we truly need.
- *(Superseded same day by quality-bar revision — paid car required, not free-first.)*

### 2026-07-14 — Catalog complete need list
- Replaced abbreviated need section with **full-game checklist** in `asset-catalog.html`: tooling, SFX, textures, Prologue→Labyrinth, endings, host, history scars, UI, owned Unity mine-first (~193 table rows). Buy-first cards still P0 modern car / optional facade / taproom scars.
- Status tags: HAVE / OWN / BUY / PROC / WRITE / OPEN. Paths called out where known.

### 2026-07-14 — House history canon (saloon retired)
- Nick: saloon was only a suggestion; build a history that makes sense for this house.
- Wrote `docs/superpowers/specs/2026-07-14-house-history.md`: **L5 underside → L4 coaching inn (taproom) → L3 private club → L2 failed manor → L1 frozen invitation games house → L0 modern guest**. Same drink/rooms/gambling functions, correct for Victorian shell. Wild West saloon demoted.
- Asset book + story bible §9 updated. Catalog: Timeline+history podium, full need-list tables A–I, shop cards (modern car / facade / taproom scars), Western packs on demoted list.
- Host slips: “bar/tap/member/desk,” not cowboy lexicon. Aldric still runs the games house, never “ran a saloon.”

### 2026-07-14 — Full-game asset book: modern guest / trapped house
- Nick: player timeline = now / few years ago; house stays old (host trap); prior uses (saloon → …) as subtle clues; host slips; plan all assets to completion; Nick must be able to fix alignment himself.
- Wrote `docs/superpowers/plans/2026-07-14-full-game-asset-book.md` — time layers L0–L5, full room matrix to endings, buy stack, 9 host/env clues, `assets/placements/*.json` + `?place=1` tweak tool spec.
- Catalog: Timeline lock section; Arrival Car marked **REPLACE · WRONG ERA**; Tier 0 podium item 0 = modern car hole.
- Story bible unchanged (Aldric guest→host cycle). Era dressing is environmental, not a lore rewrite.
- Open for Nick: car vibe, clue density, phone UI yes/no, young-Aldric portrait, buy $80 shell now vs tweak gravyart first.

### 2026-07-14 — Nick feedback batch: dialogue, gate, stars, polish, catalog
- Invitation copy: losers leave with less; winners get what they came for.
- **Gate:** closed plane at `gateZ-0.8` (old `gateZ+2` retreatCap allowed walking through shut leaves). Verified blocked (`gate-block-closed.png`).
- **Stars:** 2800+400 full-hemisphere field that tracks camera — not a slab behind the house (`sky-01/02`).
- Polish: stronger porch sconces + door seam, Court Table_Large/Chair_1/candelabra load + clearer seal HUD, STB readable tiles + brighter light + seated silhouette, Entry Hall warmer portraits + darkened dust sheets, brighter car beams.
- **Mansion:** catalog marked gravyart **REPLACE** (no door meshes). Paid shortlist in `asset-catalog.html` — recommend Unity "Victorian house" ~$19 (doors prepared for animation).
- Tests green after batch; game + catalog relaunched locally.

### 2026-07-14 — Morning: polish pass + light Court/STB wire
- Nick: keep polishing + start wiring next stuff a little.
- **Entrance:** warm door seam between closed leaves (pulses on approach, fades as doors open). Porch reshot `nite-04..06`.
- **Court shell:** new `The Games Master - Court.dc.html` — dining-scale room, procedural gavel, jury wall ×9, Deck A evidence fan (keys 1–5), wax-seal HUD, continuous tarnish on rigged+true. Entry Hall dining POI **Take the hearing** (pois 21→22). Shots `court-01..04`.
- **STB shell:** new `The Games Master - Shut the Box.dc.html` — dust-sheet frames, `buildMatch` boards, Aldric blob, `demoShut9`. Shots `stb-01..02`.
- **Harness:** Court + STB groups added. **`npm test` → 23 logic + 47 harness, 0 failed.** Visual gate PASS on shell boots + present/tarnish + boards.

### 2026-07-14 — Overnight cont.: continue-while-i-sleep pass
- Nick asleep; kept polishing + stacking Court/STB prep without stubbing playable scenes.
- **Entrance sconces v2:** still-too-orb iron+sphere → hooded iron plate/arm/cup + short candle (`nite-04..06` via `scripts/capture-porch.mjs`). Close-up reads as dark metal fixtures, not yellow placards. Ajar still shows checker + chandelier. Pageerrors:0.
- **Modules landed (no HTML scene wire yet):**
  - `gm-court-props.js` — `buildGavel` + `setTarnish`, `buildWaxSeal`, `buildDustSheet`
  - `gm-shutbox-logic.js` + `gm-shutbox-board.js` — rules + hinged boards
  - `scripts/test-shutbox.mjs` — **23 passed**; `npm test` = logic then harness → **23 + 35 passed, 0 failed**
- **Dining audit:** shots `dining-01..04`, notes `docs/playtest/dining-court-redress.md` (box table/10 chairs/candy candelabra → Table_Large / Chair_1 / candelabra.glb / gavel).
- **Harness prep** only as docs (`harness-court-stb-prep.md`) — no empty iframes. Court/STB `.dc.html` still wait for Nick Phase 0 walk.

### 2026-07-14 — Overnight: entrance sconces + Court/STB design + asset book
- Nick went to bed asking for more polish passes **and** design/assets for the next rooms. Stayed on Phase 0 polish while writing Phase 1–2 so Court doesn't start blind.
- **Entrance:** porch Kenney sconces were still two floating yellow placards in close-ups → ripped them out, replaced with small iron brackets + warm embers (`_porchSconcesBuilt='bracket'`). Door-approach glow now fades out on the final porch so it can't bleach the real doors. Verified `nite-04..06`, yellow placards gone; ajar still shows checker + chandelier. **35 passed, 0 failed.**
- **Court + Shut the Box designed for build:** plan at `docs/superpowers/plans/2026-07-14-court-and-shut-the-box.md` (geometry reuse, build order, acceptance, open decisions). Asset inventory at `docs/playtest/phase1-2-asset-inventory.md`. Evidence deck draft copy at `docs/superpowers/specs/2026-07-14-court-evidence-draft.md` (straight vs rigged decks + shard label). Catalog + CREDITS updated (gavel = procedural first; Poly Pizza CC-BY download 403'd).
- **Owned & ready when Court starts:** hall `Table_Large` / `Chair_1` / `Chandelier`, `candelabra.glb`, Entry Hall 9 portraits, `dice.glb` for STB, `dark_book.glb` for hidden room. **Gaps:** evidence UI, seated Aldric silhouette, young-Aldric portrait decision (gavel/tiles/sheets now have modules).
- Deliberately **did not stub Court/STB HTML scenes** tonight — Nick still owes the Phase 0 opening walk before Court code starts.

### 2026-07-13 — Cut sealed door panel + match doors to mansion (keep asset, rewrite entrance)
- Nick asked for a 1000% verify whether we need a new mansion. Audit (`scripts/audit-mansion.mjs`): native model has **zero door meshes** — just a sealed `Cube043_*` slab over the porch. Body/silhouette/porch quality is fine from the walk; the entrance failure is a missing-feature in the GLB, not proof the whole house is wrong. **Verdict: keep Haunted Victorian House, rewrite entrance only.**
- Hid `Cube043_*` on load (outer sealed slab only — cutting `Cube027`/`Cube042` punched a house-wide hole). Vestibule now sits in that shallow pocket (~1.55u) flush with the porch wall plane, not standing proud as a fat box on the steps.
- Rebuilt door leaves with a baked paneled face (vertical boards + recessed panels + peel/water stains matching the facade), slim dark frame/sill, aged brass knobs. Switched leaves to MeshBasic so porch PointLights stop bleaching them beige. Dimmed vestibule back wall + doorInner so the open reveal isn't a flat yellow void.
- Verified: door-01..06 through aftermath, errors:0. Mid-approach reads as doors belonging to the porch; extreme close-up will always be simpler than the ornate balustrade (procedural vs GLB detail). Still not asking for a full mansion replacement.

### 2026-07-13 — Entrance rebuilt: real 3D vestibule replaces the flat "glimpse" sticker (agent gate PASS)
- Nick walked it: "the walk up is fine up until the entrance part holy shit is that bad." He was right. The old entrance faked the interior with a flat painted canvas swapped onto the door plane — it read as a cardboard cutout pasted on the steps (crude checker, a flat cream wall with one dot for a chandelier, door leaves splaying out as flat boards), and the arrival camera sat too high/far so the tiny sticker was dead center under three stories of facade.
- **Rebuilt the doorway as a real recessed 3D vestibule** (`this.vestibule`): checker-marble floor (real tiles, real perspective from geometry), warm plaster side walls, dark ceiling, and a warm back wall painted with a real staircase + a dark hall archway continuing on (pays off the beat text). Built OUT toward the player from the facade so the solid GLB wall can't occlude it. A real hanging **chandelier** (rod + hub + six arms + point light) sways on the RAF tick.
- **Doorframe/architrave** (lintel, jambs, pediment) around the opening so it reads as a built entrance, not a hole. Two **paneled leaves** with inset panels + brass knobs, hinged at the opening edges, now swinging **inward** to fold flat against the side walls instead of splaying out as flat boards.
- **Reframed the arrival camera**: settle closer (z −47.4) and lower (eye 3.55) and tilt down onto the doorway (pitch −0.17) so the opening + lit interior fill the frame for the reveal. Knockout pulls the camera through the opening into the vestibule (Dutch angle, floor rising) — real parallax now.
- **Killed two washes** that were drowning the reveal: the far-approach `doorGlow` plane (zeroed during arrival — it's only for the drive) and the fullscreen warm radial DOM overlay (`warmStyle`, was 0.32 → 0.1 during door-open; the heavy version stays for the knockout blackout).
- Verified interactively frame-by-frame (`scripts/play-door.mjs`): door-01 arrive at framed doorway + stairs → door-02 settle → door-03 leaves crack (warm seam) → door-04 open on the real vestibule → door-05 dragged down into the hall → door-06 aftermath. errors:0, reached aftermath. **35 passed, 0 failed.**

### 2026-07-13 — Phase 0 remaining-polish verified + reduce-motion fix + full-loop gate (agent gate PASS)
- Ran the "remaining exterior polish" checkbox to ground instead of trusting source: falling leaves, Model T, crickets, owl were all already coded, so the work was proving each fires at runtime and catching what doesn't.
- New runtime checks (`scripts/verify-polish.mjs`): **leaves drift** (sampled leaf y over ~0.9s → 12/12 change, none stuck below floor), **crickets bed armed** (ambOn + crickets track present, not paused, baseVol 0.26), **owl one-shot** (spied `playSfx`, forced the timer to elapse, confirmed `amb_owl.ogg` fired). All PASS.
- **Fix found + made:** reduce-motion only affected the head-bob; the two big ambient motions (drifting mist + falling leaves) ignored it. Extended the RAF loop to freeze both when Reduce Motion is on (leaves stay on-screen as dressing but stop falling). Re-verified: Reduce Motion ON → 0/12 leaves move.
- Car: re-shot close-ups (`scripts/capture-car.mjs`) — the Model T reads period from front/side/3q/arrival. Chased down a "bright green box" I saw in one 3q frame with `scripts/probe-green.mjs` (scans every mesh for green-dominant materials) → **zero green meshes**; it was the dark-green grass mound `#2a3220` under a lamp, not stray art. Bushes already tinted dried-olive.
- Handoff (`scripts/verify-handoff.mjs`): jumped to aftermath, clicked Continue → lands `The Games Master - Entry Hall.dc.html`, boots a real scene (canvas + GMC live), **zero page errors**, narration continues seamlessly ("the dark took the rest" → "Cold. The marble had my cheek.").
- Full interactive loop re-run (`scripts/play-full.mjs` + `scripts/play-door.mjs`): spawn → through gate → turn → gate slams + locks → up the drive past cemetery/garden → porch flanks (dressed, no voids) → facade → arrival → doors swing open on the checker+gold hall glimpse → knockout pull-in → **aftermath** (phase reached, ambience faded, camY drops to floor). errors:0 at every frame; read spawn/mid-drive/porch/facade/flanks/arrival/doors-open/knockout/aftermath shots.
- Tests: **35 passed, 0 failed.** Only open Phase 0 item is Nick walking it.

### 2026-07-13 — Opening rescue session start
- Confirmed target repo is `the-games-master` (asset-dump Downloads folder out of scope).
- Wrote Phase 0 plan + this tracker.
- Next: implement car/gate/approach/facade, harness, screenshots.

### 2026-07-13 — Game-feel polish: sourced lamps, arrival dressing, billboard grass (agent gate PASS)
- Replaced box+sphere drive lampposts with **lantern.glb** (CC0) housings on thin iron posts — fixed an initial scale bug that blew them up to 4m-wide blobs before landing on post+housing.
- Added **wall_sconce.glb** (CC0) porch fixtures flanking the front door instead of naked point lights in space.
- **Arrival dressing** at the car: driver door ajar, leather trunk, dropped invitation envelope on the grass, faint tire scuffs on the cobbles.
- Grass tufts: cone spikes → crossed billboard planes (unlit, muted).
- Path urns: ruins_pack `Pot1`/`Pot2` replace box+cylinder primitives; fallback urns if the kit misses.
- Tests: **35 passed, 0 failed.** Shots: `phase0-04`, `porch-door`, `car-3q`, `car-arrival`.

### 2026-07-13 — Period car + foliage/leaves/verge/stone rework (agent gate PASS)
- Nick asked to upgrade bad assets on sight and make the car match the period. Confirmed the era: faded Victorian/Edwardian gothic, and the player explicitly arrives by car ("step out of the car" / "I got back in the car"), so an early automobile is correct.
- Car: replaced the low-detail `old_car.glb` blob with the **Ford Model T** (Bruno Oliveira, CC BY 3.0, Poly Pizza — credited in `assets/models/sourced/CREDITS.txt`). 10 real part-materials (body/chrome/glass/tyres/brass grille). Uniform-scaled to an authentic tall/narrow stance (3.66L × 2.35H × 1.96W, not squashed into a modern sedan), rotated so its long axis lies down the drive nosed at the house, headlights/taillights/beam placed off the world bbox. Desaturated 42% toward weathered near-black + tarnished brass (Model Ts were famously black) so it reads period, not a bright toy. Shots `car-front` / `car-3q`.
- Falling leaves: 64 tumbling leaf planes on the RAF tick, sway + spin, seamless respawn at the top. Motion proven (`leaves-t0/t1`: leaf y drops frame-to-frame, `moved:true`).
- Verge: ~480 flat leaf-litter planes on the curb line + cobble corners, weeds pulled in to hug the curb (x from 3.9), so the path no longer meets grass at a clean edge.
- Foliage fixes: grass was neon-green (low-albedo olive blowing out under stacked warm lamps) → muted dead/dry browns; `ruins_pack` bushes were bright spring-green sponges → tinted dark dried olive; cemetery cut stone was near-white fresh blocks → darkened ~50% toward weathered damp grey.
- Scatter fixes: pale styrofoam boulders next to the car (near-band rocks up to ~4u catching the beam) → small dark pebbles near the drive with size split out per band, boulders moved to x≥20 out of the beam; earth mounds were pale smooth domes ("tents") → flat-shaded, darker, flattened.
- Tests: updated the car-size assertion for the Model T's authentic short/tall proportions. **35 passed, 0 failed.**
- Agent visual gate: re-shot car + flanks + full 11 angles, read them, PASS. Still awaiting Nick's eyes.

### 2026-07-13 — Flanks + driveway + car pass (agent gate BORDERLINE on car, PASS elsewhere)
- Nick flagged (correctly) empty flanks, a bad car, and a broken driveway after the sky/outer-gate pass. He was right: everything lived inside x=±5, trees were sparse, cemetery/garden were islands in bare grass.
- Driveway: replaced the path + pathN seam (different tiling densities overlapping) with ONE continuous 200u cobble plane from z=-86 to z=114, matching continuous curbs. Seam gone.
- Outer walls/piers darkened from concrete-pale `#3a352c` → weathered `#241f18` / `#1e1a13`.
- Car: wet-paint materials (metalness 0.45 / roughness 0.4), headlights left ON (emissive lenses + spotlights aimed down the drive) + red taillights + a beam pool on the cobbles. Still WEAK underneath — `old_car.glb` itself is a low-detail silhouette.
- Ground scatter: always-present primitive rocks (dark flat-shaded), weed clumps, fallen logs, earth mounds across both flanks the whole drive length. Dense near the fence, thinning into fog. Independent of ruins_pack, so flanks fill even on a GLB miss.
- Flank trees densified: avenue every 6u (was 8), ~60 mid-flank trees+bushes (skipping cemetery/garden footprints), ~46 big far-treeline trees at x=±46..76, plus denser fallbacks.
- Tests: timeout bumped 90→150s for denser scene. **35 passed, 0 failed.** Visual gate: flanks PASS, driveway PASS, car BORDERLINE (better lights, still a bad model).

### 2026-07-13 — Sky + behind-car void fixed (agent visual gate PASS)
- Sky was dead because moon/stars had `fog:true` and FogExp2 (0.011) at ~180u+ washed them ~99% to the fog colour. Fix: `fog:false` on moon disc + glow + craters + stars; moon repositioned over the drive at (-46,64,-179) with an additive glow halo; 620 stars with `sizeAttenuation:false` so they read at any distance. Now visible in shots 01/09/10.
- Fought two sky-backdrop artifacts: a gradient dome left a hard dark disc at the zenith (sphere's dark cap projects as a circle when looking up), and a gradient plane showed its rectangular edge. Dropped both; using a uniform `#0c111d` background — clean night sky from every angle.
- Behind-car void (shot 02) filled: shut wrought-iron outer gate + two lit stone piers + low outer walls + a back treeline (real dead trees in `buildRuinsGrounds`, box trees in the fallback), plus the cobble path extended north to meet it. Tagged `outerPier/outerGate/outerWall` for the scale report.
- Tests: 35 passed, 0 failed. Visual gate: re-shot 11 angles, PASS.

### 2026-07-13 — Audio sourced + wired + full opening inventory
- Sourced open audio (curl from OpenGameArt, licenses confirmed on page): Wind Loop (CC-BY, AntumDeluge/InspectorJ), Dark Ambience Loop (CC-BY, qubodup), Creaky wooden door (CC-BY, fractilegames), Different steps stone/gravel/leaves (CC0, TinyWorlds). Encoded door wav→ogg with ffmpeg. All credited in `assets/sfx/license.txt`; the two pre-existing gate ogg files flagged as origin-unconfirmed.
- Wired an audio system on the SFX bus (no separate music slider): two looping ambience beds start on the walk gesture, distance-based footsteps (stone-weighted pool, pitch-varied), door creak on arrival open, beds fade into the knockout blackout, all stopped on secret ending + unmount. Sliders live-update the beds.
- Harness: added "walk starts the ambience beds" — 35 passed, 0 failed.
- Wrote `docs/playtest/opening-inventory.md`: every opening scene + view classified OK/WEAK/MISSING/UNPROVEN from code, shots, or assets. Re-shot 11 angles incl. sky + behind-mansion.
- Evidence-based gaps now on record: sky is dead (moon/stars invisible in shot 10), void behind the car at spawn (shot 02), no falling leaves, no crickets/owl. These are the next exterior passes — NOT claiming the exterior fully done.

### 2026-07-13 — Opening rescue implemented — agent visual gate PASS
- Car: was uniform 0.6 → ~2.8u toy. Now measures native bbox and scales to ~4.6L x 1.55H x 4.6W, parked left of the curb at z=76.5 with a small warm underglow; fallback car resized to match. Harness asserts car reports real size on load.
- Gate: found THREE gates stacked at gateZ (procedural posts + procedural swinging leaves + sourced `graveyard_gate.glb`). Removed all procedural gate geometry; the sourced gate is now the only gate. Lock beat rotates the sourced model; retreat clamp does the real blocking.
- Approach: fence run extended -34 → -48, trees to -52, added lamps at -36/-48, urn pair at -32, hedges lengthened. No more dead ~18u gap before the porch.
- Facade: window materials get warm emissive + a toned warm wash so the house reads "lit up like a birthday" from the gate without blowing out up close. Removed misaligned floating fake-window quads (were rendering as grey slabs off the sides).
- Roadmap updated: Phase 0 inserted before Court; endings stay Phase 7; Nick playtest gates after 0/2/7/9; AI panel only 2/7/9; no push of local commits without explicit approval.
- `npm test` → 33 passed, 0 failed. Screenshots in `docs/playtest/screenshots/phase0-01..05`.
- Verdict: **agent gate PASS** — one coherent gate, real-scale car, filled approach, lit facade all confirmed by reading the shots. Waiting on Nick to walk it before Court.
- Capture note: MCP screenshot tool 5s-caps on this WebGL page; used `scripts/capture-phase0.mjs` (Node Playwright) for reliable shots.

### 2026-07-13 — Full estate exterior build-out — agent visual gate PASS
- Audited every unused sourced model (`scripts/inspect-glb.mjs`, `scripts/inspect-ruins.mjs`). Found `ruins_pack.glb` is a full gothic ruins/graveyard kit at clean ~2u modular scale: dead trees, a 4.6u stag statue, broken/overgrown walls, gothic arches, columns, planters, bushes, cart, barrels, crates, skull.
- Replaced the box-tree avenue + flat green box hedges with real grounds built from that kit (`buildRuinsGrounds`): a 52-tree dead-tree avenue + back treeline, a walled **cemetery** off the right of the drive (finally pays off the "graveyard gate": ruined walls, gothic entrance arch, 16 hand-made leaning/toppled headstones, stag monument, dead trees, skull, cold moon-pool light), a **ruined garden** around the dry fountain on the left (broken + fallen columns, fox statue, planters, bushes), and abandoned yard clutter (cart/barrels/crate) just inside the gate. `_bakePiece()` extracts each kit piece to a grounded, centered, cheap-to-clone group. Primitive `buildFallbackTrees()` covers a ruins load failure.
- Killed the green box hedges — they read as plastic AND occluded everything built beyond the fence. The see-through iron fence is the drive edge now; both side plots read through it.
- Tinted the graveyard gate to weathered iron (was pale/white plastic — the one caveat flagged last session). Now near-black with a little metalness so the pillar lamps catch it as a cold sheen.
- Scale audit done (table above) — trees 6–8u, arch/statue/gate ~4.4–4.7u, headstones ~1–1.6u, mansion 17u, car 1.55u. No outliers.
- `npm test` → **34 passed, 0 failed** (added estate-grounds build assertion; bumped CI timeout 60→90s since the 95-mesh kit + live previews legitimately need more wall-clock).
- Visual gate: 9 shots (`phase0-01..09`) read this session — dead-tree avenue framing, dark iron gate, cemetery + garden visible through the fence from the drive, lit Victorian at the end, all fog-wrapped. **Agent gate PASS.** Overview shot (09) is not useful — FogExp2 eats the distance at night by design; judge from the eye-level shots.
- Still awaiting Nick to actually walk it before Court.

### 2026-07-14 — Unity asset mine + door/furniture redesign
- Inventoried Asset Store cache + `~/Games Master/Assets/ThirdParty` (UltimateHouseInterior, FreeHorrorKit, Victorian Study, IV Art LOWPOLY Victorian Mansion, door packs, MetalMan Victorian Interior, Alchemist House, etc.).
- Built `scripts/extract-unitypackage.mjs` (pathname first-line fix) + `scripts/fbx-to-glb.mjs` (Playwright FBXLoader→GLTFExporter, texture stub/remap).
- Exported live GLBs under `assets/models/unity/{doors,furniture,study,exterior}/` + `CREDITS.txt`.
- Prologue: front leaves are `Door_Double.glb` (Left/Right hinged). Gravuart shell kept (best silhouette); Cube043 still hidden + vestibule.
- Court: Unity `Chair_1` + `Light_Chandelier`; hall table fallback remains.
- Entry Hall: Soft_Chair / Couch_Large1 / Table_RoundSmall / Shelf_Large with procedural fallbacks.
- Visual gate: `scripts/verify-unity-doors.mjs` PASS — upright leaves 1.05×2.53; shots `unity-door-01/02`, `unity-court-01`, `unity-hall-library`, live `door-01..06`. `npm test` 23+47 pass.
- Still open: extract MetalMan / Alchemist / Haunted House for a full exterior shell replacement; Nick Phase 0 walk.

### 2026-07-14 — Full owned-Unity integration gate (before any $30 buy)
- Extracted + converted: MetalMan Victorian Interior, Stealth Haunted House, Horror Starter FREE, more UltimateHouse.
- Live wiring: Prologue Door_Double; Entry Hall MetalMan furniture + parlor Door_Double; Court haunted table + MetalMan chairs; STB MetalMan table/chairs + haunted frames.
- `scripts/verify-unity-full.mjs` → **UNITY FULL GATE PASS** (8 shots `unity-full-01..08`). Live `play-door` arrival still opens Unity leaves.
- `npm test` harness timeout raised to 240s for heavier GLB scenes.
- **Buy call: DO NOT buy the $30 horror pack yet.** Owned packs already cover hinged front/parlor doors + Court/Hall/STB furniture. Remaining gap is a better *exterior mansion shell* than gravyart — try assembling IV Art LOWPOLY exterior modules / FreeHorrorKit house / Alchemist House (owned, not yet converted as a full shell) before spending.

### 2026-07-14 — Asset catalog buy shortlist (hard marketplace sweep)
- Updated `asset-catalog.html` with ranked Best Buys across Unity Asset Store, Synty, KitBash3D, Fab, Sketchfab/CGTrader notes.
- S-picks: Victorian Mansion Environment ($80), Victorian Grand Mansion Interior / Abandoned Horror Interior, KitBash Victorian + House of Horror.
- Confirmed likely “$30 pack” = Horror Modular Environment Pack ($29.99) — mid-tier A, not auto-buy.
- Owned Unity todos marked WIRED/PARTIAL; gravyart shell status “SHELL + REAL DOORS”; checkout rule = confirm hinged leaves before cart.
- Agent gate shots: `docs/playtest/screenshots/catalog-buy-00-header.png` … `catalog-buy-04-owned.png` (26 marketplace links, 5 S-ranks, 3 highlighted best tiers).

### 2026-07-14 — Catalog cart lanes A–E
- Rewrote buy decision block as Lane A/A2/B/B2/C/D/E blends with pack links, within-lane swap table, and skip-doors note. Default callout: Lane A (~$115).
- Shots: `docs/playtest/screenshots/catalog-lanes-01.png`, `catalog-lanes-02.png`.

### 2026-07-14 — FULL Unity Asset Store bulk convert
- Extracted **106** packages from `~/Library/Unity/Asset Store-5.x` → `assets/models/unity-import/` (~36GB, gitignored).
- Converted **4917 FBX → 5023 GLB** under `assets/models/unity/<slug>/` (~2GB). **0 FBX left without GLB.**
- Pipeline: `scripts/unity-bulk-convert.mjs` + Three FBXLoader; legacy ASCII/FBX 6.1 via Unity `GmExportLegacyFbx.cs` → OBJ → `scripts/obj-to-glb-blender.py`.
- Docs: `OWNED-PACKS.md`, `CREDITS.txt`, `BULK-MANIFEST.md`; catalog section updated.
- Still not auto-wired into Prologue shell — now we *have* Alchemist / Asylum / Flooded / Stylized House trees to pick from before buying.

### 2026-07-14 — Catalog “absolute cut”
- Replaced Lane A–E sprawl with Tier 0 owned podium (gravyart + Door_Double + MetalMan + Alchemist + LOWPOLY Victorian + Asylum + Flooded + dressing) and a 3-tier buy pyramid (ONE buy = Victorian Mansion Environment $80).
- Demoted $30 pack / Abandoned Horror buy / Synty / Gothic / door packs.
- Shots: `docs/playtest/screenshots/catalog-cut-01.png`, `catalog-cut-02.png`.

### 2026-07-14 — Prologue: pull Unity Door_Double off facade
- Nick call: Unity door on gravyart looked kit-pack shit. Reverted Prologue front leaves to matched painted panels (`doorFaceTex`). `Door_Double` stays stock for Hall only.
- Catalog / verify scripts updated. Shot: `docs/playtest/screenshots/prologue-painted-doors-01.png`.

### 2026-07-14 — Closed-door trap beat (no door fashion show)
- Arrival no longer swings leaves open then KO. Settle on closed door → short hold → knockout with doors frozen shut. Frame thinned + wood-mapped (grey sticker frame was the tell).
- KitBash $290 answer logged for Nick: workable via FBX/Unity→GLB, does **not** fully provide “all we need” (still assemble/integrate; heavy for web; won’t replace games/writing).
- Shots: `door-01`…`door-06` via `play-door.mjs`.


### 2026-07-14 — Full game asset catalog by stage
- Rewrote `asset-catalog.html` as **stages 0–8** finish plan (Opening→Hall→Parlor→Court→STB→Hidden→Labyrinth→Endings→Host/tooling) + cross-cut SFX/textures + buy-later/never.
- Buy-now trio unchanged (~$145): Modular $35 + Env $80 (door check) + HD 03 $30.
- On-disk GLB previews kept below stages. Honest scope: packs ≠ minigames/endings.
- Agent gate PASS: `docs/playtest/screenshots/catalog-stages-01-header.png` … `catalog-stages-06-later.png` (toc click → #stage-7 OK; only favicon 404).

### 2026-07-14 — Trio cutover plan (pre-purchase)
- Plan: `docs/superpowers/plans/2026-07-14-trio-cutover-and-retire.md`
- After buy: convert → car → Modular → Env → retire Model T / gravyart (Env PASS only) / stubs
- Keep: MetalMan, ruins, gate, asylum/flooded, bulk Unity mine — not a mass wipe

### 2026-07-14 — Threshold Refusal + no-second-mansion lock
- Canon: `docs/superpowers/specs/2026-07-14-opening-threshold.md` — front doors stay shut; KO from porch dark; wake Hall. Side path = optional later only.
- Catalog `#buy-next` rewritten: trees / cemetery / ground / SFX · Modular English **locked out**. Target ~$20–40.
- Prologue KO beats lengthened (refusal → porch dark → collar). Entry Hall wake: “No memory of a doorway.”
- Shots: `door-01..06`, `catalog-buy-next-nomansion.png`. `play-door.mjs` + `npm test` green.

### 2026-07-14 — Env fence wired + Door_Double back on facade
- Root cause of “unused Env”: Modular `SM_Fence*` / `SM_Wall_*` GLBs are **31–45MB** (LOD stack) — stalled boot. Strip script → `assets/.../web/*_LOD0.glb` (~0.2–0.4MB).
- Outer look-back: box walls hidden; Env FenceSmall + Fence LOD0; ruin Wall_* slabs removed from outer line (read as styrofoam).
- Entrance: `Door_Double` hinged leaves + lowpoly handles; **SM_Wall_Door rejected** (solid plate over the opening). Porch balustrade/columns force MeshBasic dark.
- Shots: `docs/playtest/screenshots/env-outgate-01..02.png`, `env-door-01..03.png`. Verify: `node scripts/verify-env-entrance.mjs` PASS. `npm test` 23+47.
- Honest: money not wasted; usage gap on Env (size), not “didn’t buy enough fence.” Trees stay low-poly kit. Nick Phase 0 walk still owed.

### 2026-07-15 — Codex takeover pack
- Guide: `docs/CODEX-HANDOFF.md` (read first)
- Plan: `docs/superpowers/plans/2026-07-15-codex-takeover.md` (Part 1 review/fix → Part 2 phases 1–9)
- Kickoff paste lives in both files’ appendix / start prompt

### 2026-07-15 — Pre-buy polish (before Nick walk / Dead Tree Pack call)
- Killed remaining pier/drive MeshBasic glow spheres → iron cages + soft PointLights
- Wired **Book of the Dead** `tree_dead_001` as hero accents + more Polytope fill (36 owned trees)
- Updated `docs/NICK-NEEDED.md`: SFX license closed; buy only after walk if trees still fail
- Shots: `begin-outgate-prebuy.png`, door sequence green. `npm test` 23+47

### 2026-07-15 — Beginning kinks pass (Threshold Refusal polish)
Agent continued opening without a second mansion buy.

**Shipped:**
- Licensed audio gap closed: project-authored `gate_slam` / `gate_lock` / new `ko_thud` (ffmpeg→Opus OGG). `assets/sfx/license.txt` updated.
- Threshold Refusal: porch side-dark volumes pulse on "beside them" beat; `ko_thud` on knock; doors stay shut.
- Door readability: Door_Double shells hidden; canvas panel overlays + rails parented correctly on hinges (earlier overlays sat beside doors).
- Removed porch floating MeshBasic glow spheres; Env lamp glass toned down.
- Owned Polytope dead trees wired into avenue + outer look-back (`_mountOwnedDeadTrees`, 22 accents). Ruins bark pushed nearer black.
- Porch pale-paint heuristic widened (bright non-window → night MeshBasic).
- `play-door.mjs` waits for `_thresholdSaid` + `door-03b-porch-dark` shot.

**Gates:** `play-door` errors:0 → aftermath · `npm test` 23+47 PASS · shots `door-01..06`, `door-03b-porch-dark`, `begin-outgate-trees.png`

**Honest residual:** gravyart porch still flat grey at a glance; kit trees still read tan/low-poly even darkened (Polytope accents help but Dead Tree Pack buy is still the look gap); bush atlas green killed (drop-map → night MeshBasic). **Nick Phase 0 walk still owed.** Progress **77 → 79**.

### 2026-07-14 — Trio cutover started (Nick purchased)
- **Car HD 03:** extracted 17 GLB, wired `Exterior_LOD0` in Prologue (upright 1.93×1.55×3.83). Model T + `old_car` **deleted**. Shots `trio-car-01..03`.
- **Env (BEFOUR):** extract OK; Three FBXLoader fails most meshes → Blender path. Modular kit pieces converted (walls/door/fence…). `SM_Wall_Door` = single mesh (no hinged leaves) → **keep gravyart painted doors**; do not delete `assets/models/exterior/` yet.
- **Modular Interior (SimViz):** extract + **102/102 GLB**. Entry Hall dresses `Wall_01_NoLedge` + ceiling over shells. Shot `trio-modular-hall-walk.png`.
- Pipeline: `scripts/fbx-blender-to-glb.py` added for Unity FBX Three rejects.
- `npm test`: 23 + 47 pass.
- Still open: Env full facade assemble OR Unity demo export; Court/STB Modular dress; Catalog buy→HAVE polish; Nick Phase 0 walk.

---

## Check-in — 2026-07-17 late (Opus): Unity clicks automated, two invisible root causes killed, STB imported

**Nick's four Unity clicks are gone.** `scripts/unity-cli.mjs` drives the editor from the terminal:
`pipeline`, `rebuild`, `setup`, `tour`, `test`. Only the walk still needs a human, by design. Two
constraints are settled and should not be re-litigated: HDRP will not render in batchmode here (the
tour launches a windowed editor on purpose), and `~/GamesMaster-Unity` sits outside the Bash sandbox
write allowlist, so Unity runs need it disabled or they exit 127 with no log.

**Root cause 1 — magenta everything.** The Leartes packs ship built-in and HDRP variants of the same
assets under the same names, and both were imported. `FindAssets("SM_Tree_")` returned both; the
built-in copies use the Standard shader (`guid: 0000000000000000f000000000000000`), which HDRP has no
pass for. 168 materials across the three packs are on built-in shaders. Fixed by making the builder
prefer HDRP-resolving assets and warn loudly on fallback. `SM_House_02` has no HDRP variant at all and
stays magenta until it is converted or swapped.

**Root cause 2 — the night was never applied, in any run.** `VolumeProfile.Add<T>()` builds overrides
in memory only; they were never written into the `.asset`, so it reloaded as `components: []` and
HDRP fell back to its default sky and **default automatic exposure**. That is why the estate looked
like blue daylight and why dropping the moon from 2200 lux to 1.7 moved measured luminance by two
points (84 → 82) — auto-exposure compensated for whatever was set. Every theory built on lighting
values was chasing a ghost. Fixed with `AssetDatabase.AddObjectToAsset` per override plus an assert
that fails the build at zero. The bug is invisible in the editor and only shows in play mode.

Lighting now: `MoonLux = 1.7` (matches Leartes' own Showcase light) and `NightExposureEV = -3`,
giving meanLum 29–58 across the tour. Fixed exposure is deliberate — auto-exposure re-brightens
authored darkness, the exact failure the web build spent three days rooting out. **EV -3 is an
agent's number and has not been art-directed.**

**Shut the Box — Codex's lane closed.** Codex is dead but did not leave the lane empty; an earlier
check this session wrongly said so because it only looked inside the Unity project. Codex had staged
a complete tested C# port in the web repo at `unity/shut-the-box/`, deliberately outside Unity to
avoid domain reloads during the capture pass. Now imported to
`Assets/GamesMaster/ShutTheBox/` and green in-engine. The web repo copy remains the source of truth;
do not let the implementations diverge.

Suites green this session: C# parity 23/23 (Mono), JS logic 23/23, browser harness 47/47, Unity
EditMode 23/23.

Open: `SM_House_02` magenta · spawn is bare (no mansion/car/trees at z=72) · the white tree may be
overexposed (unverified) · the Showcase baseline needs its own waypoints, since GmShotTour's 12 are
Wend Hill coordinates · Nick's walk and the EV call.

---

## Check-in — 2026-07-17 final (Codex): Phase 0 final asset/audio/placement pass ready for Nick

**Progress:** overall Unity/Steam **16%** · Phase 0 **84%** · final quality-pass tasks **100%**.
Phase 0 itself stays open because Nick's visual/audio/pacing walk is a hard human gate.

### Implemented

- Deterministic `GmEstateQualityAudit` + `unity-cli.mjs audit`: required roots, exactly one mansion,
  HDRP materials, grounded props, clear authored paths, decorative collision, audio graph, and scene
  fingerprint.
- Cemetery: lower/darker broken wall, clear cross-path/spine, irregular grave clusters and scruff.
- Coach yard: loading/feed/repair clusters, ruts, and one restrained motivated lantern.
- Mansion: 11 window slots now deterministically vary as 2 dark / 4 dim / 5 lit.
- Rare event: one-in-three upper-window figure; forced review evidence; permanently gone below z=18.
- Audio: wind capped at 0.08 and high-passed at 145 Hz, slow drift, sparse wildlife, F8 wind/no-wind
  comparison. No new sound purchase recommended before Nick listens.
- Route tests: full exploration plus adversarial side-bound/threshold pressure.
- Tour: expanded 12 → 16 frames; capture made synchronous after Unity intermittently lost yielded
  coroutines. Final frame luminance range 14–26 and clean editor exit.
- Garden: rejected six bad treatments (slabs, clean soil rectangle, invisible beds, firewood caps,
  green grass, boulder pumpkins). Final is restrained fence/work-path/scarecrow/shed/dead-bed grammar
  with two small produce clusters. It is the weakest zone and graded B, not hidden as “done.”

### Final gates

- Unity EditMode **41/41**.
- Unity PlayMode **2/2**.
- Two cold estate audits: identical `objects=3968 renderers=2949 colliders=496 lights=9`, fingerprint
  `046d838dfbce58dd`.
- HDRP tour **16/16** inspected, including forced figure present/gone pair.
- Browser harness **47/47**.
- Shut the Box JS **23/23**; C# parity **23/23**.
- Legacy nine-gate runner: seven passed immediately. It exposed two stale harness defects: full-walk
  kept walking backward after the gate latched, and porch-breath waited on the hold before the slow
  headless arrival glide settled. Harness-only fixes made both targeted reruns pass. Full walk reached
  all 18 frames and aftermath with `errors=0`; Threshold Refusal doors remained `{left:0,right:0}`.

### Grade and residual

**B+ / READY FOR NICK PASS.** Mansion/arrival/coach yard and verification are A-range; cemetery is
B+; kitchen garden is B; audio is B+ pending ears. Residual taste calls: modern vehicle, EV -3 on
Nick's display, cemetery repetition, garden readability, filtered wind vs silence, and 4m45 pacing.

Full report: `docs/playtest/codex-phase0-final-pass-2026-07-17.md`.

No purchase, commit, push, second mansion, or opened Threshold Refusal.

## Check-in — 2026-07-18 (Codex): clean 9/9 baseline and adversarial Claude handoff

**Progress:** overall Unity/Steam **16%** · Phase 0 **84%** · Claude review package **100%**.
Product grade remains **B+**; green automation did not promote the art to A+.

Several intermediate browser runs were deliberately kept in the record. They exposed long-walk
input/timing starvation, an arrival-settle assumption, and a screenshot timeout under severe external
SwiftShader load. Test-only hardening preserved the real RAF/collision walk, removed any gate
teleport/clamp, waited for the actual porch-settled position, added explicit bounded capture/navigation
waits, retained 30-line failure tails, and isolated each gate's own browser descendants for cleanup.
No gameplay source changed for these harness repairs.

Targeted hostile-load validation passed the full 18-shot walk through aftermath with `errors=0` and
front doors `{left:0,right:0}`, then passed the aftermath→Entry Hall handoff with zero page errors.
The decisive uninterrupted `node scripts/run-gates.mjs` invocation then passed **9/9**:

```text
unit+harness 67s · agent playtest 306s · door 72s · full walk 402s ·
environment 49s · porch breath 63s · bell/lamp 72s · hall handoff 55s · polish 27s
```

Claude's review-only, evidence-first prompt is `docs/CLAUDE-REVIEW-PROMPT.md`; the exact baseline and
defect history are in `docs/playtest/codex-to-claude-review-handoff-2026-07-18.md`. Claude is told to
rerun everything, inspect all 16 Unity shots, grade 18 claims CONFIRMED/PARTIAL/FALSE, and write a
ranked fix queue before touching code. Nick's eyes/ears/pacing gate remains blocking.

No purchase, commit, push, second mansion, or opened Threshold Refusal.

## Check-in — 2026-07-18 (Codex): Phase 0 takeback objective hardening complete

**Progress remains:** overall Unity/Steam **16%** · Phase 0 **84%**. Objective automation is stronger,
but the percentage does not move because Nick's visual/audio/pacing walk is still the exit gate.

The takeback cold audit was written before implementation at
`docs/playtest/codex-phase0-takeback-audit-2026-07-18.md`. Four agent-owned defects were repaired:

- Side-zone PlayMode coverage now isolates non-bound props, identifies the intended generated
  `WalkBounds` collider, commands travel beyond it, requires `CollisionFlags.Sides`, and requires the
  endpoint within contact distance. All seven walls logged direct contact at a 0.43m center-to-wall
  gap. The formerly vacuous chapel case now commands x=42.80 and stops at x=37.97 on the x=38.50 wall.
- The mansion root now carries a durable `GmMansionIdentity`. A new EditMode counterexample duplicates,
  completely unpacks, and renames the shell to `Summer House`; the audit rejects it after prefab
  provenance is gone.
- GUI retry is limited to a diagnosed idle stall with zero artifacts. First and second attempt logs
  are distinct and retained; a successful retry prints a final flaky-green summary. Reported defects,
  partial captures, deadlines, spawn errors, and unexpected exits are not retried. The pure retry
  policy self-test passes.
- The visual tour is 18 shots. Shots 17 and 18 hold the exact same camera at z=19.5 and change only
  the forced figure state around its z=18 cutoff. The pair proves the special bright pane disappears
  cleanly but conspicuously in one frame; whether that reads as supernatural or cheap is now an
  explicit Nick call, not an unreviewed assumption.

Post-change evidence: rebuild PASS; two audits identical at
`objects=3968 renderers=2949 colliders=496 lights=9 fingerprint=4707410a08b94635`; EditMode **42/42**;
PlayMode **3/3**; HDRP tour **18/18** on GUI attempt 1 with luminance
`17,22,14,24,19,25,18,18,22,22,26,24,21,17,18,16,17,17`. The successful attempt log is preserved at
`cli-tour-attempt-1.log` and copied byte-identically to `cli-tour.log`. Retired-web regression floor:
Shut the Box JS **23/23** and browser harness **47/47**. Unity closed cleanly.

No garden, cemetery, exposure, vehicle, wind-mix, pacing, or figure-style change was made. No
purchase, commit, push, second mansion, gameplay-randomness seed, or opened Threshold Refusal.

## Check-in — 2026-07-18 (Codex): reusable Unity scene factory complete and live-proven

**Progress remains:** overall Unity/Steam **16%** · Phase 0 **84%**. Cross-phase scene-production
infrastructure is **100%**, but infrastructure earns no shipped-content percentage by itself.

Implemented the reusable workflow in `docs/UNITY-SCENE-WORKFLOW.md`:

- `unity/scene-system/scene-registry.json` is the validated command contract for scene ID, path,
  phase, builder, audit, review tour, success markers, and screenshot count.
- `scripts/scaffold-unity-scene.mjs` is dry-run by default and generates a deterministic builder,
  room audit, reusable-tour subclass, minimum tests, and README. It refuses unsafe names, duplicate
  IDs/paths, and overwrites. Generated `*-replace-me` shot names intentionally keep the first room
  test red until every composition is authored.
- `scripts/sync-unity-scenes.mjs` copies repository-owned C# into Unity and has a read-only byte-drift
  gate. The five shared framework files are now repository source of truth and clean 5/5 against the
  Unity project.
- `GmSceneIdentity`, `GmSceneBuildUtility`, and `GmSceneContractAudit` standardize durable identity,
  scene creation/save/build settings, exact path, required roots, camera ownership, missing scripts,
  and build-list health.
- `GmSceneReviewTour` provides reusable real RenderTexture capture, shot-name validation, luminance
  evidence, exact output count, one-shot arming, state hooks, and clean editor exit.
- `scripts/unity-cli.mjs` keeps all old default commands but now accepts a registered scene ID and
  derives methods, markers, screenshot folder, log tag, and shot count from the registry.
- The driver now refuses Unity launches above 3.0 load per CPU core unless a deliberate diagnostic
  override is supplied. This prevents resource-starved startup failures from being misreported as
  code failures.

Adversarial evidence is green: scene-system Node tests **10/10**; unsafe traversal, duplicate IDs,
registry/runtime drift, method syntax, literal markers, host capacity, overwrite, generated-file
coverage, missing registered source, and repo-to-Unity drift are exercised. The handwritten C#
framework plus Wend Hill integration compiles against Unity 6000.5.3f1 references. A completely
fresh five-file scene pack also generates and compiles against those real references.

Wend Hill is wired to the shared save/identity/audit contract and has three new EditMode tests for
identity, duplicate-identity rejection, and required-root rejection. No transform, material,
exposure, audio, pacing, figure, garden, cemetery, car, or gameplay setting was changed.

Live editor verification is green. Two initial rebuild launches exited before compilation when Unity
Package Manager could not create its IPC stream within 30 seconds; the first failure remains at
`Logs/cli-rebuild-upm-failure-2026-07-18.log`. The new host-capacity guard prevented more misleading
launches, and no unrelated process was killed or paused. Once capacity returned, Wend Hill passed a
cold rebuild; identical pre/post audits at
`objects=3968 renderers=2949 colliders=496 lights=9 fingerprint=4707410a08b94635`; EditMode
**45/45**; PlayMode **3/3**; and the production **18/18** tour through `GmSceneReviewTour` on GUI
attempt 1. The shared-engine frames remained visually consistent, with luminance
`17,22,14,25,20,26,18,18,22,22,26,24,21,17,19,17,17,17`. Retired-web floors also remain green:
Shut the Box JS **23/23** and browser harness **47/47**. Unity closed cleanly.

No purchase, commit, push, unrelated-process termination, second mansion, or opened Threshold
Refusal.

## Check-in — 2026-07-19 (Codex): standalone macOS review app built and running

**Progress:** overall Unity/Steam remains **16%** · Phase 0 remains **84%** · Steam layer is **3%**.
Added `npm run unity:build:mac`, a strict player-build gate that requires Wend Hill as the first
enabled scene, validates every enabled scene path, configures native-resolution borderless
fullscreen, deletes only the previous generated review bundle, and refuses success without Unity's
build report plus the app executable. The five repository-owned framework files are synchronized
5/5 and scene-system tests remain 10/10; the fresh generated scaffold and build framework compile
against Unity 6000.5.3f1.

The first build passed at `~/GamesMaster-Unity/Builds/macOS/The Games Master.app`: one scene,
1,300,538,030 reported bytes, universal x86_64 + arm64 executable, locally valid code signature,
and macOS 12.0 minimum. The app was launched in fullscreen review mode and its independent player
log reached `GmDesignRuntime` with 13 POIs, 5 drive beats, 8 walk rectangles, 5 cold-open cards, and
7 branch beats; no startup exception or failure marker appeared. This is an unsigned local review
build. Windows, Steamworks, depot upload, public signing/notarization, settings UI, and the placeholder
`com.DefaultCompany` bundle identifier remain ship work.

No scene content, taste-owned visual/audio setting, purchase, commit, push, second mansion, or
Threshold Refusal state changed.

## Check-in - 2026-07-19 (Codex): authored scene-composition engine complete

**Progress remains:** overall Unity/Steam **17%** and Phase 0 **90%**. This is cross-phase
infrastructure and does not receive fake shipped-content credit.

Extended the deterministic scene factory so a future room cannot pass as a pile of technically valid
assets. Research covered MIT prefab-painting and procedural-generation projects, WFC constraints, an
Apache-2.0 camera-requirement solver, and Unity's official Splines and Graphics Test Framework. No
third-party code or package was imported. The implementation adopts the useful patterns in a small
repo-owned contract without adding a shipping dependency.

- `GmSceneComposition` now owns the visual intent and minimum authored coverage.
- Builders can mark bounded zones, story clusters, visible elements and roles, spatial relationships,
  player routes, negative-space volumes, motivated lights, and measurable screenshot claims.
- `GmSceneCompositionAudit` rejects placeholders, duplicate IDs, orphan props, missing anchors,
  unsupported clusters, excessive family repetition, bad grounding, broken relationships, exact
  overlaps, occupied negative space, blocked routes, unmotivated local lights, and shots that miss
  their declared subject/framing/depth hierarchy.
- `GmSceneReviewTour` now records p05/p95 luminance and black/white clipping fractions and fails
  captures that are too black, white, or flat to review. Mean luminance alone is no longer trusted.
- New scaffolds contain six files, including a dedicated red-by-design composition plan. Their audit
  and tests cannot pass until the placeholder intent and shots are replaced with authored claims.
- Shared test source now synchronizes with the framework, bringing repository-to-Unity ownership to
  8/8 files. Detailed design and source decisions are in `docs/UNITY-COMPOSITION-ENGINE.md`.

Verification is green: scene-system Node tests **10/10**; fresh six-file scaffold compilation against
Unity 6000.5.3f1 **PASS**; Unity EditMode **55/55**, including six new adversarial composition cases;
Unity PlayMode **3/3**; Shut the Box logic **23/23**; browser harness **47/47**; source drift **8/8
clean**. Wend Hill's complete **18/18** HDRP tour passed the stronger evidence gate on attempt 1. Its
p05/p95 ranges were 25-90, near-black coverage 1.4%-18.8%, and every frame had 0.0% near-white
clipping. No visual candidate, gameplay behavior, asset package, purchase, commit, push, second
mansion, or Threshold Refusal state changed.

Honest grade: **A- for the engine foundation, not A+ yet.** It is tested against synthetic failures
and the production capture path, but no new shipped room has yet authored the full contract. Entry
Hall is the first proper proof. The engine must earn an A by preventing real composition mistakes
during that build, then still survive screenshot inspection and Nick's walk.

## Check-in - 2026-07-19 (Codex): supervised scene intelligence production proof

**Progress remains:** overall Unity/Steam **17%** and Phase 0 **90%**. The adaptive tooling is
cross-phase infrastructure and does not receive fictional content credit.

Implemented the complete supervised layer above the composition contract. The repository now owns
atomic immutable review sessions, conservative Nick/agent evidence weighting, automation taste
lockout, draft rules, explicit confirmation-gated promotion, append-only defect resolution,
contextual exclusions, baseline approval blockers, and empty-by-default adaptive overrides. Imported
asset intelligence profiled 1,676 existing Unity assets and produced rendered role contact sheets;
two Wend Hill slots produced six temporary HDRP alternatives without selecting one.

Wend Hill received a metadata-only retrofit: 8 zones, 15 clusters, 57 elements, 5 route reservations,
5 negative-space volumes, 18 shot claims, 17 classified lights, and 2 adaptive slots. A saved-scene
test closes and reopens the scene before verifying those components. The variant failure-path test
injects an exception after mutation and proves exact restoration. The authored fingerprint remains
`cb198edc4905ac2e`; approved rules, defect resolutions, adaptive overrides, and visual baselines all
remain empty.

Verification is green: knowledge **11/11**, scene-system **10/10**, fresh Unity compile **PASS**,
repo-to-Unity sync **22/22**, EditMode **61/61**, PlayMode **3/3**, Shut the Box **23/23**, browser
harness **47/47**, HDRP tour **18/18**, macOS build **PASS**, and fresh built-player proof **7/7**
with zero runtime errors and no UnityEditor assembly. The imported index was byte-identical across
two runs at SHA-256 `91a66e0c020badd05742a252af3df8ab3d15937b533d122822d6fcbe998594b3`.

Honest grades: **A for the engine, A- for the current opening candidate, not A+**. The engine still
has to prove itself while authoring Entry Hall from scratch, and Nick still owes the Phase 0 visual,
audio, and pacing walk. No purchase, commit, push, second mansion, selected adaptive variant,
approved visual baseline, or opened Threshold Refusal.

Implementation guide: `docs/UNITY-SCENE-INTELLIGENCE.md`. Evidence report:
`docs/playtest/codex-adaptive-scene-intelligence-2026-07-19.md`.
