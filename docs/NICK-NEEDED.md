# What needs Nick (human) vs what the agent can keep doing

Last audited: 2026-07-22 post-B+ A-candidate closure (103/103 EditMode, 5/5 PlayMode including
the complete live crossing and virtual-controller route, seven measured wall contacts, 18/18 tour,
7/7 clean scene frames plus 2/2 controller UI frames, 5/5 final Ninth Bell clips decoded in the built
app, all six Terrain foliage variants render without integrity warnings, audit
`2c9c97b0db8dc80b`; Codex self-grades A candidate and Claude/Nick review remains open).

## Unity: you no longer click anything

Build, audit, test, route-test, and screenshot capture are scripted. From
`~/Projects/games/the-games-master`, with the Unity editor CLOSED:

```bash
node scripts/unity-cli.mjs setup      # assign HDRP + rebuild Wend Hill
node scripts/unity-cli.mjs audit      # deterministic placement/material/audio audit
node scripts/unity-cli.mjs test       # 103 EditMode tests, headless
node scripts/unity-cli.mjs playtest   # 5 PlayMode tests: routes, walls, threshold, crossing, controller
node scripts/unity-cli.mjs tour       # 18 shots -> ~/GamesMaster-Unity/Screens/WendHill/
npm run unity:build:mac               # rebuild the local review app
npm run unity:proof:mac               # 7 scene + 2 controller frames, gamepad path, 5 final cues
```

The only Phase 0 gate left is **your walk and your ears**. Automation proves that it builds, routes,
stays sealed, reproduces, and loads/decodes every final cue; it cannot decide whether 4m45 feels
tense or slow, whether the night is too dark on your display, or whether the mix sounds convincing
through your speakers or headphones.

Legend:
- **NICK** — only you can clear this (play, buy, license, art tooling decisions)
- **AGENT** — code/wiring, agents can keep chasing
- **BOTH** — you decide taste; agent implements

---

## NICK — do these

### 1. Play the standalone opening (blocking)

Unity does not need to be open or installed to run the exported app. Launch
`/Users/damato/GamesMaster-Unity/Builds/macOS/The Games Master.app`, or unzip
`/Users/damato/GamesMaster-Unity/Builds/distribution/The Games Master - Phase 0 macOS.zip` and open
the extracted app.

Keyboard/mouse: WASD, mouse look, E interact, F8 wind comparison, Esc pause, Q quit while paused.
Controller: left stick or D-pad move, right stick look, A/Cross interact and advance cards,
B/Circle skip the intro, RB/R1 wind comparison, Menu/Options pause, Y/Triangle quit while paused.
While paused, Left/Right Arrow or D-pad Left/Right adjusts brightness across five restrained levels;
level 0 is the authored grade and the setting persists for normal play.
The beat HUD clears itself after 7.5 seconds. Then walk this exact route:

1. At spawn: look down the drive — black house, burning windows. Confirm the six foliage families
   now form dark/dry habitat rather than missing patches or bright-green tufts. E at the car.
2. At the gate: the framed plate on the right pier should read as intentional threshold furniture,
   not the former plain gray slab. E it for WEND HILL.
3. Walk through, then turn around — gate slams + locks behind you.
4. RIGHT branch: enter the cemetery, cross its central paths, inspect the open grave, mixed family
   markers, stag monument and north memorial bench/lantern, then continue to the chapel door.
   Confirm the grave recess, low chest tombs and fallen markers read as distinct objects rather than
   leftover black floor-cover.
5. LEFT branch: enter the kitchen garden, inspect the scarecrow/well/shed, then look for the
   half-standing trellis, interrupted dead-growth rows and abandoned harvest cart before continuing north into the
   coach yard and its loading/feed/repair clusters.
6. Return to the drive and walk to the porch. Push directly at the front threshold and both outer
   side bounds; the doors must remain shut and the world must not leak.
7. Stay for the full nine-bell sequence and wake-room handoff. Confirm the chapel toll is audible
   across the estate; the heartbeat feels bodily rather than synthesized; the tinnitus remains an
   internal symptom instead of becoming another background spaceship; the human whisper does not
   expose a repeat; and the final clock cue reads as one longcase strike. Decide whether the 4m45
   grounds duration earns its length.
8. Open `Screens/WendHill/tour-17-figure-cutoff-on.png` and `tour-18-figure-cutoff-off.png` at full
   size. They are the exact same camera near z=18. Decide whether the head-and-shoulders silhouette
   in the upper-right warm pane is perceptible but deniable, and whether its disappearance works.
9. Repeat a short section and press **F8** or **RB/R1** once. Compare filtered quiet wind against
   sparse wildlife + authored silence. Say which version sounds less like a spaceship.

Tell the agent: gate-lock feel; navigation darkness; car art match; cemetery repetition; garden
readability; coach-yard staging; porch close-up; wind ON or OFF; bell/heart/whine/whisper/clock mix;
and whether the nine-bell wait is tense or merely slow. Phase 0 stays open until you make those
calls.

Unity is not required for the walk. For optional structured feedback, open **Games Master > Scene
Intelligence > Review Window** in Unity. Keep,
Change, Reject, baseline approval, draft-rule promotion, and defect resolution are all explicit Nick
gates. Do not mark the five migrated historical defects resolved merely because the engine tests pass;
resolve one only after you have inspected the repaired scene evidence.

### 2. Purchased payload entitlement (closed); local cache inventory (open when a future room needs it)

The $50 Leartes Horror Environments Bundle is purchased. The original intake recorded all nine
payloads downloaded, but this Mac no longer contains the legacy Asset Store package cache, so the
nine source payload files cannot be reverified from current storage.

In Unity Package Manager → My Assets, download the nine packs listed in `docs/playtest/horror-bundle-intake.md`. Download is enough; do not import all nine into one Unity project. Confirm with:

```bash
npm run assets:horror:check
```

Current provable status: **3 packs selectively imported** in the Unity project: Haunted Village,
Witch Village and Abandoned Village. The checker exits nonzero while the source package cache is
absent and lists those three imports honestly. Re-download a specific owned pack from My Assets when
a later room needs it; do not bulk-import all nine.

No more asset purchases until this bundle has been mined. Modular English / KitBash / another mansion remain **locked out**.

**Money truth:** Car HD + Modular Interior earned their keep. Env underuse was file size (now LOD0-wired), not “didn’t buy enough fence.”

### 2b. SPOILERS — the planted layers (dev knowledge; players discover these)

Press E twice on things: every POI has a hidden second line (coach-house chalk numbers = your debts;
the fallen headstone = your surname; COUNTED OUT on the child's marker; heads-down coins; boot
prints; the pebble). The invitation wax = stag crest = broken-antler monument = "FROM ITS WINNERS."
About one walk in three, the upper-right mansion window contains a figure; it is permanently gone
once you pass z≈18. The deterministic screenshot tour forces it in shot 15, proves it gone in 16,
and holds the camera exactly still for the cutoff comparison in shots 17 and 18.
The old three-toll chapel knock-back is retired. The ninth bell now owns the crossing.

### 3. Taste calls (say yes/no)

- Keep the modern HD vehicle on a gothic estate? Canon supports it; the art language is still mixed.
- Is the kitchen garden readable enough in motion, or should it lose the produce and stay barer?
- Filtered quiet wind or sparse wildlife + silence (F8)? No new sound purchase is recommended yet.
- Is 4m45 across the grounds tense, or too slow before the game begins?
- Commit/push only when you are happy; the agent will ask first.

---

## AGENT — still fair game (no you required)

- Implement your Phase 0 taste notes after the walk.
- Then proceed to Court → Shut the Box → shards/persistence → hidden room → labyrinth → endings.
- Wire assets you deliberately approve; no broad pack dumping and no second mansion.

---

## Retired web reference

The commands below edit the retired HTML prototype, not the Steam/Unity product. Keep them only for
regression archaeology; do not use them for the Phase 0 approval walk.

## How you manually edit the retired prototype

### A. Run the game locally
```bash
cd ~/Projects/games/the-games-master
npx --yes serve -l 3456 .
# open http://localhost:3456/The%20Games%20Master%20-%20Prologue.dc.html
```
Or open the `.dc.html` file directly if your browser allows local GLB loads (serve is safer).

### B. Move / scale / tint things in code (no Blender)
Almost all Prologue placement is in:

`The Games Master - Prologue.dc.html`

Useful search terms:
| Want to change | Search for |
|----------------|------------|
| Car park spot | `placeCar` / `cx` near `4.8` / `76.5` |
| Gate lock z | `gateZ` / `triggerGateLock` |
| Fog darkness | `FogExp2` |
| Ambient / moon fill | `AmbientLight` / `DirectionalLight` |
| Ruin colors | `nightStone` / `#080706` |
| Porch lamps | `buildPorchSconces` / `_mountEnvPorchLamps` |
| Drive lanterns | `buildDriveLamps` |
| Owned trees | `_mountOwnedDeadTrees` |
| Door panels | `doorFaceTex` / `_dressDoorPanelOverlays` |
| Arrival satchel | `_dressArrivalCar` |

Save → hard refresh browser (Cmd+Shift+R).
