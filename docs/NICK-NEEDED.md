# What needs Nick (human) vs what the agent can keep doing

## 2026-08-15 (later) — RESOLVED: everything below this heading is done

Nick's call: **arm the bell on crossing the gate house-ward.** Implemented. He also said "fix all
the rest", so the remaining open decisions were taken with the recommendations already documented
here, each one noted in its commit:

| Was blocking | Resolution |
|---|---|
| What arms the bell | Crossing the gate house-ward (Nick). `GmSecretEnding` already used that exact line as the point of no return, so the two systems now agree instead of contradicting each other. Retreating to the car before the gate still beats the house. |
| House collision approach | Mesh colliders on the mansion (`GmMansion.MakeSolid`) rather than a hand-placed box shell, so the porch stays walkable without guessing extents. Triangle total is logged because of the LANE A1 perf regression. |
| Gate-2 deadlock | Named `EXPECTED_FAILURES` rule in `verifyTests`, carrying its reason and its retirement condition. Any unlisted failure still fails; an excluded test that starts PASSING also fails, so the exclusion cannot go stale silently. Gate 2 now exits 0 — gates 3-12 are reachable for the first time. |
| Hedge taxonomy | Retagged `maze-hedge`. Hedges are planting, not carved stone, and the guard is family-scoped by design. |
| Does the Huntsman's lantern burn | Yes — it already did. The builder lit it; only the plan's comment claimed otherwise, and that comment was stale on both of its claims. Intent authored. |
| Bone-totem framing | Camera backed off 1m. A review shot that overflows its frame cannot do the job a review shot exists for. |

Measured after all of it: **EditMode 330/332**, the two remaining being Court's by-design failures.
Session started at 324/332 with the tracker claiming 294/296.

**Still genuinely open and still yours:** Aldric is absent from his own opening, and the player is
never handed the game's core verb before sitting at the table — the panel's blockers 2 and 3, both
marked owner-decision. Those are design work, not defects. See
`docs/reviews/2026-08-15-opening-panel.md`.

## 2026-08-15 — STOP. The prologue does not play for a player who obeys the invitation.

Verified in source, not inferred. `bell?.Arm()` has exactly two production call sites:
`GmThreshold.cs:63` and `GmVillageSave.cs:93` (which only restores an already-armed save). The first
sits inside `lockNow`, which requires:

```
maxProgress >= gateAnchor.RouteMetres + 1f   &&   progress < maxProgress - 0.5f
```

— the player must pass the gate **and then move backward more than half a metre**. And
`GmBellSummons.Update()` returns immediately unless armed.

So: a player who does exactly what the letter tells them to do — walk to the house — passes the
gate, reaches the porch, reads "The doors did not open", and **stands there forever**. No gate slam.
No nine tolls. No crossing. No Entry Hall. The entire designed opening, and every page of the Ninth
Bell spec, sits behind an optional decision to glance over your shoulder on a dark drive.

This is not a bug I can fix for you, because the fix is a canon decision. The current behaviour is
*deliberate* — the spec's reasoning is that "the house can only count someone it already has", so
retreating to the car **before** the gate beats it outright, which is the secret ending. That
reasoning is good. The hole is that it never considered the player who simply never turns around.

Your options, and they change what the secret ending means:

- **Arm on crossing the gate house-ward.** Committing is passing the gate, not being caught looking
  back. Keeps the secret ending intact (leave before the gate and nothing is written). My
  recommendation, and it is what the review panel independently converged on.
- **Arm on porch arrival.** Latest possible commit; makes the grounds fully optional but means a
  player who stops halfway is still un-counted.
- **Arm on a timer from spawn.** The hour is coming regardless of what you do. Thematically the
  strongest reading of "the ninth bell takes you wherever you stand", but it removes the player's
  agency in starting the count.

**Everything else the panel found is in `docs/reviews/2026-08-15-opening-panel.md`.** Six
independent reviewers, all six FAIL, none would sign off even with their own top three fixed. Four
more defects I verified in source myself: the first line inside the house contradicts the
never-open-doors canon; beats run at 640-730 wpm against a hardcoded 4.2s dwell; the nine-count's
payoff line is destroyed by a same-frame call ordering; and `GmSymptoms` puts a lowpass on the
camera's AudioListener that progressively muffles the very bell the player must count.

The one unanimous thing across all six reviewers: **the prose is genuinely good and must not be
sanded down** by any systems or accessibility pass. Several quoted the same lines unprompted.


## 2026-08-15 — READ FIRST: the gate was not the worst of it. The house has no collision at all.

You found the gate. The same audit, run properly across the whole opening, found something worse:
**the estate house — the object the game's central canon depends on — is a hologram.** Not its
doors. The entire building. You can walk to the porch, keep walking, pass through the front wall,
through the interior, and out the back into the field.

Verified directly, three independent ways:

1. `unity-project/Assets/GamesMaster/Exterior/haunted_victorian_house.fbx.meta:94` → `addColliders: 0`.
   The house imports with zero colliders. (So do all nine owned FBX files.)
2. Nothing ever adds one. The only collider-adding code near the house is
   `GmHouseBeginningBuilder.cs`, which builds the *interior pocket* at z≈358-402 — a different
   place entirely from the exterior manor you walk up to.
3. `GmWendOpening.BuildManor` actively **disables** any collider it finds inside the manor
   footprint (`unity/scenes/wend-hill-prologue/Editor/GmWendOpening.cs`, the footprint-clearing
   loop), so even an incidental one would be switched off.

And the thing named as the seal is not one. `GmMansion.SealTheDoors()`
(`unity/project/Assets/Editor/GmMansion.cs:180-193`) is, in full, `r.enabled = false` — it hides a
renderer. Its own log line says "hidden", which is honest; the method name says "sealed", which is
not.

**Why this is canon-critical, not cosmetic:** Threshold Refusal is the opening. Drive → gate locks →
porch → *the doors never open* → the ninth bell takes you wherever you stand. Today a player walks
through the front wall in about four seconds, and `GmThreshold` still prints "The doors did not
open" from route-distance projection while they are standing inside the building. The reveal the
whole prologue is built to deliver is defeated by walking forward.

**This needs your call, and I have deliberately not made it**, because the three fixes have real and
different costs in a scene already carrying a perf regression (LANE A1):

- **Flip `addColliders` on the FBX.** Cheapest to write. Generates per-mesh collision on a large
  building, and it collides head-on with the route-carving passes below.
- **Author a collision shell** (a handful of box colliders forming the facade and walls). Controlled,
  cheap at runtime, deliberate — my recommendation, but it is geometry authoring and that is yours.
- **One blocking volume across the porch face.** Smallest change; only stops the front approach and
  leaves the sides and rear open.

**Second thing you should know, a process one:** the gate fix you were asked to *choose* has already
been built and shipped into the saved scene — a `GateBarrier` collider plus 45m perimeter fence
wings each side, present in `WendHill_Prologue.unity` right now, in uncommitted work, with no
commit selecting the approach. It is decent work and the wings are solid, but it pre-empted your
call. Its residual hole: the wings stop at ±45m and there is nothing beyond them but terrain.

**Root cause of the whole defect class, worth fixing above any single door:** three build passes
(`GmWendPerformance.CarveFalseRouteObstacles`, `ClearFalseDoorwayColliders`,
`DisableIntersectingColliders`) delete or disable colliders by *name token* — the list includes
"wall", "door", "house", "building", "fence" — and `GmWendSceneContract.cs:249-252` **fails the
build** if purchased doorway colliders still seal the route. So there is hard automated pressure to
remove collision and **no counterpart check anywhere that asserts anything is still solid**. That
asymmetry is the machine that produces this bug, and it will keep producing it.

## 2026-08-15 (latest) — three calls you handed me, made. Veto any of them.

You said to make the best choice, so these are decided rather than asked. Each is grounded in a doc
rather than my taste, and each is cheap to reverse.

### 1. The tell verb — DECIDED, shipped in `f0e6560`
**Six tells, eight innocent.** The line is whether something is WRONG and checkable, not whether it
is interesting. Kept: the debt written short in an unfamiliar hand, one mason's hand on stones a
century apart, every coin heads-down, boots to the shed and none back, scratches from *under* the
chapel door, a chalked number scratched out hard. The other eight keep their line as a second
Examine — the writing is not lost, it simply is not evidence. Six against a True Escape threshold of
eight means the tables now decide the ending, which is what "seven games a night" requires.

### 2. Which rooms are games at the table — DECIDED
**Parlor and Shut the Box are two of the seven. Court, the Hidden Room and the Labyrinth are not.**
Their own content settles it: a trial is a judgement on games already played, the Hidden Room is a
secret you find, and the Labyrinth is a chase. None of them is a game Aldric deals you a hand in.
So five of the seven table games are unbuilt, and the three story rooms punctuate the night rather
than counting toward it. If you disagree, the cheapest place to say so is the order list in #38
before any completion conditions get written against it.

### 3. Interiors and cool light — DECIDED in principle, not yet built
**Yes, they should carry cool separation, and it must come from a window.** Right now every interior
surface is warm brown lit by 2000K lamps, so the rooms read as one flat sepia; the porch shot works
precisely because warm lamplight sits against cool fog. The motivated source is moonlight through a
window, and it is thematically the right one: the outside is visible and unreachable, which is
Threshold Refusal stated in light instead of dialogue. An unmotivated fill light would be rejected
by the composition audit anyway, correctly.

Not built yet because it is room content rather than a lighting constant. Tracked on #39. The gate
no longer blocks on it — interiors are judged on a wider band, which is a real distinction and not
a silencing: a room lit by oil lamps genuinely has one colour of light.

## 2026-08-15 (later) — the tell verb has no risk, and it quietly decides which ending you get

Not a bug. A balance hole I opened myself when I made the grounds discrepancies callable, and it
needs a design call, not a fix I can pick.

**What ships right now:** all **14** grounds POIs carry a tell. `GmDesignRuntime` binds every POI
the same way — `BindTell(poi.text2)` — and every one of the 14 `text2` fields in
`prologue-design.json` is non-empty, so `CarriesTell` is true everywhere. **There is no object on
the grounds where calling a tell returns a false read.** The verb cannot be got wrong.

**Why that matters beyond the grounds:** `CatchCheat` records both a catch and a defiance point.
`CheatsCaughtCount >= 8` is the gate on **True Escape**, and `DefianceCount > ComplianceCount`
decides **Defiant Sacrifice** vs **Host Succession**. A player who calls a tell on everything banks
up to 14 free catches and 14 free defiance points before sitting down at a single card table. The
8-catch threshold is met almost twice over on the walk in, by pressing a button on everything.

**The tests did not and could not catch this.** `GmGroundsTellTests` does exercise the false-read
path — against a *fabricated* interactable with an empty tell, using the id `arrival-car`. That id
is real production data, and in production `arrival-car` carries a tell and returns Caught. The test
passes; it is not testing what ships.

**My read of the 14, against the game's own bar — "a discrepancy shaped exactly like a caught
cheat":**

- **Genuine tells (6):** `gate-card` (the debt written short in an unfamiliar hand), `weathered-marker`
  (same mason's hand a century apart), `garden-basin` (every coin heads-down), `garden-shed` (boot
  prints in, none out), `chapel-door` (scratches from *under* the door), `coach-doors` (a chalked
  number scratched out hard).
- **Borderline (2):** `open-grave` ("dug slowly. Or kept ready."), `child-marker` ("No dates.
  COUNTED OUT.") — thematically loud, weak as *evidence*.
- **Atmosphere, no wrongness to catch (6):** `arrival-car` (Nora's drawing), `gate-plaque` (tally
  strokes grouped in sevens), `stag-plinth` (the plaque inscription), `garden-scarecrow` (a watch
  chain with no watch), `garden-well` ("I'm choosing to believe it landed"), `fallen-marker` ("The
  surname was mine.").

**What I need from you — pick one:**

1. **Mark the six atmosphere POIs innocent** (clear the `text2` field, or better, give them a
   distinct "you were wrong" response). Leaves 8 real tells against a threshold of 8, which makes
   the walk exactly sufficient and leaves no slack — probably still too generous.
2. **Mark eight innocent** (the six plus both borderlines). Leaves 6 on the grounds, so the
   remaining catches must come from the tables. This is the version where the verb has teeth.
3. **Add a cost for a false call** rather than removing tells — a wrong call is heard, and Aldric
   adjusts. More interesting, more work, and it makes reading the grounds a real risk.
4. **Raise the True Escape threshold** and leave the grounds generous. Cheapest, but it makes the
   walk a checklist.

`garden-well` is the clearest innocent candidate in any of these — it is explicitly the character
choosing an interpretation, not evidence. I did not change any of it, because which objects are
innocent is a canon and taste call, and getting it wrong quietly rewrites the ending.

## 2026-08-15 — three small calls, each 30 seconds, each blocking a green test

The 2026-08-15 EditMode run found 8 failures (the tracker claimed 2). Five were objective
pivot/placement errors and are fixed. Three are yours, because they are taste, not correctness —
all three are in Labyrinth and all three keep its two tests red until answered:

1. **Is a hedge "architecture"?** `shrine-pillars` and `hedge-walls` both declare the asset family
   `maze-architecture`, and the audit's duplicate-placement guard is deliberately family-scoped, so
   the two empty container roots (both legitimately at the world origin) hash to the same key and
   trip it. Nothing is actually misplaced — the pillars are at ±2.5 around the shrine and the hedges
   are spread across the 7×7 grid. Cleanest fix is a one-token retag of the hedges to `maze-hedge`
   (hedges are planting, not carved stone). The alternative — nudging an invisible parent to dodge a
   hash collision — works and is less honest.
2. **Does the Huntsman's lantern burn?** `GmLabyrinthCompositionPlan` says "whether the Huntsman's
   lantern should move or emit light is Nick's call, not invented here" — but that comment is now
   stale on both counts: the prop is the `SM_Lantern` FBX (not the cube it describes) and the
   builder already creates and enables a 200-intensity light at it. So either author the intent
   (the light stays, comment gets corrected) or delete the light (comment becomes true again).
   Deleting removes the only warm light in the stalk corridor.
3. **Should the bone totem fill the frame?** Review shot `05-bone-totem` frames a 2.04m prop from
   1.84m away, so it overflows top and bottom (viewport height 1.00 against a 0.90 ceiling). Nothing
   is misplaced; it is purely how close the camera sits. Backing off to z=-7.0 gives 0.657 and keeps
   the framing target. The alternative is to declare that the totem filling the frame IS the shot.
   Note the plan file's own warning against tuning claim numbers to move the audit.

## 2026-08-13 — the gate you found is a real, confirmed defect

You were right — the estate gate has no physical enforcement at all, open or closed. It's two
disconnected pier posts with nothing between them and no boundary tying them to anything; the
"lock" is a route-progress flag that plays a sound and swings a cosmetic leaf animation. A player
can walk 4-5m around either side and never touch it, before or after it "locks." Full root cause,
why hundreds of walkthroughs never caught it, and everything else open from this session (a real
stall the new coach house content introduced, a perf regression, deferred plumbing, and your
still-open taste calls) are all in `docs/CLAUDE-FABLE-HANDOFF.md`'s 2026-08-13 section — that's
the actual handoff, read it before anything else. **AGENT priority next session, before any new
content: audit for this same failure pattern (logic says "blocked/locked/closed" with no matching
physical collider) across the rest of the Prologue, then fix the gate itself once you pick an
approach** (a real fence, an invisible blocking volume, or narrowing the walk deck — your call,
not decided for you).

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
`~/Projects/games/the-games-master/unity-project/Builds/macOS/The Games Master.app`, or unzip
`~/Projects/games/the-games-master/unity-project/Builds/distribution/The Games Master - Phase 0 macOS.zip`
and open the extracted app. (The Unity project moved from `~/GamesMaster-Unity` to
`./unity-project` inside the repo; update any saved shortcuts.)

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

### 2c. The Reckoning — contextual bell + coach house (2026-08-13, new)

Full design: `docs/superpowers/specs/2026-08-13-the-reckoning.md`. You already gave the core
direction directly (outbuildings become genuinely enterable; the KO still always happens, but
its timing should vary with what the player did rather than always being the same 4m45). That's
recorded as an owner decision superseding part of the ninth-bell spec. What's still open:

1. **Does exploring make the house hurry, or buy time?** One signed dial
   (`reckoningPressureAuthority`). Ships neutral (no change from today) until you set it on a
   walk with the coach house built.
2. **How much randomness, concretely?** Ships at zero jitter until tuned — a feel call, not a
   number anyone should pick in the abstract.
3. **The chapel question.** Leave it shut (it's the bell's own diegetic source, and its shipped
   "scratches from under the door" line is worth more closed than any room), or open it and let
   the player pull the rope to answer a toll early (makes "nothing you do matters" mechanical
   instead of asserted)? Recommend deciding this after walking the coach house, not before it
   exists.
4. Should the shed and a new below-grade icehouse follow later? The icehouse means a real new
   asset-intake pass; a cheaper substitute (a cellar hatch inside the shed) gets the same "you
   lose your read on the count" effect for zero new assets, if that's the part that matters.
5. TASKBOARD lane F6 (corruption ceiling 4 vs 5) now also gates whether the Reckoning is
   *allowed* to raise corruption at all — still open, still yours.

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
