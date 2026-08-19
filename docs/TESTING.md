# Testing — what to run, when

One rule: **`npm run gates` after any change.** Green = safe. A red gate's last output prints inline.

## What `npm run gates` actually runs

`gates` → `verify:opening` → `scripts/run-opening-gates.mjs`. It drives the **Unity opening**, in this
order, and **stops at the first failure** — every later gate depends on the earlier artifact being
trustworthy, so one red gate means the ones below it did not run at all and you have no evidence
from them:

| # | Gate | What it proves |
|---|---|---|
| 1 | portable source and archive checks (`verify:portable`) | C# parity 23, `test:fast` 50, scene/package sync, scaffold compile, **and the archived web harness 47** |
| 2 | Unity EditMode | scene structure, authored content, interaction, route, collision, rendering, audio, builder contracts |
| 3 | canonical Unity PlayMode | runtime opening behaviour and lifecycle |
| 4 | canonical scene rebuild | the builder reproduces the scene |
| 5 | saved-scene contract (`audit`) | the reloaded scene satisfies `GmWendSceneContract` |
| 6 | player-camera visual tour | 8 frames with luminance/clipping evidence |
| 7 | canonical macOS build | player builds, contains no `UnityEditor` assembly |
| 8 | standalone story/input/audio proof | 7 story frames + 2 controller frames, 5 decoded cues, pause/wind/brightness |
| 9 | house entry and first-game proof | entry handoff |
| 10 | full-route 1080p performance proof | dense spline, milestones, stalls, nav fallbacks, p95 |
| 11 | standalone boundary-wall proof | sustained pushes into all four boundary walls cannot escape |
| 12 | captured-frame render defects | scans every frame gates 6–11 produced for magenta, near-black, blown and flat — the classes a luminance percentile cannot see |

**Requires:** Unity **closed**, `node_modules` installed (`npm install` — the harness dies at gate 1
without `playwright`), and host load under 3.0/core. `unity-cli.mjs` shells out to `ps`, so it cannot
run inside a sandbox that blocks process listing.

`npm run test:fast` is the editor-free quick check (50 tests, no Unity, no browser).

## The archived web harness (inside gate 1)

These test the **retired HTML build** — design-source regression only, not the shipping product.

| Gate | Script | What it proves |
|---|---|---|
| unit+harness tests | `npm test` | 23 Shut-the-Box rules + 47 in-page harness tests (beats, gate lock, car, grounds, Court/STB shells) |
| **agent playtest** | `npm run playtest:agent` | Plays the whole opening: geometry audit of every tagged object (size/float/sink/tilt/overlap), walk-rect coverage grid (stuck pockets), 18+ stop walkthrough with real key input, E at every POI asserted, per-frame luminance metering, gate-lock → KO → aftermath, doors-stay-shut, perf thresholds, hall/court/stb boots. Auto-derives extra stops from live POIs/rects, so NEW content is covered without editing the rig. Findings: `docs/playtest/agent-report.json` (HIGH/MED/LOW) |
| door sequence | `scripts/play-door.mjs` | Threshold Refusal beats real-time, doors frozen shut, aftermath |
| full walk | `scripts/play-full.mjs` | Spawn → gate lock → full drive → arrival → aftermath with screenshots |
| env entrance | `scripts/verify-env-entrance.mjs` | Door rig present, leaves sized, hinges open/close |
| porch breath | `scripts/verify-breath.mjs` | Breath one-shot fires on the porch-dark beat |
| g2 bell+lamp | `scripts/verify-g2.mjs` | Chapel bell gated to the forecourt; dying lamp dips, no lamp goes negative |
| hall handoff | `scripts/verify-handoff.mjs` | Aftermath → Entry Hall lands in a live scene, narration continues |
| polish | `scripts/verify-polish.mjs` | Leaves drift/respawn/reduce-motion, crickets, owl |

## Reading playtest findings

`docs/playtest/agent-report.json` — fix HIGH first; MED next; LOW is judgment. Screenshots land as
`docs/playtest/screenshots/apt-*.png` — read them by eye, the meter only catches brightness.

## Unity and standalone Phase 0 gates

With the Unity editor closed:

```bash
node scripts/unity-cli.mjs audit wend-hill
node scripts/unity-cli.mjs test       # 93 EditMode
node scripts/unity-cli.mjs playtest   # 5 PlayMode, including crossing + virtual gamepad
npm run unity:build:mac
npm run unity:proof:mac               # 7 scene + 2 controller UI frames, audio + controller runtime
```

The fifth PlayMode route and native proof both drive the complete Phase 0 controller surface:
A/Cross card advance and interaction, B/Circle intro skip, left stick and D-pad movement,
right-stick look, RB/R1 wind cycling, Menu/Options pause/resume and Y/Triangle quit routing. Native
proof creates a virtual gamepad inside the exported player, measures actual movement/rotation,
captures controller-specific UI and fails on any runtime error. A physical Xbox/PlayStation pad is
still a human hardware/feel check; do not represent virtual-device proof as a named-device test.

## Hard-won harness rules (learned the expensive way, 2026-07-16)

1. **Playwright `waitForFunction(fn, arg, options)`** — passing `{timeout}` as the 2nd argument
   silently uses the 30s default. Always `waitForFunction(fn, null, { timeout })`.
2. **Never trust `cmd | tail` exit codes** — pipes report the LAST command's status. Use
   `set -o pipefail` or check `PIPESTATUS`.
3. **Headless is slow and dt is clamped (0.05)** — game-time accrues at a fraction of wall-clock
   under software rendering. Never wait real-time for a timed beat: advance the timer variable
   (e.g. `arrHold`) to just under its threshold and let the code path fire itself.
4. **Static-pose captures kill the RAF loop** — restart it (`C._raf = requestAnimationFrame(()=>C.loop())`)
   before any real-input test, or nothing moves.
5. **Raycasts miss what pixels show** — for "what IS that pixel", render to a target, read the
   pixel, and bisect scene visibility (see probe-pale2.mjs pattern).
6. **Measure, don't eyeball** — dark scenes fool the eye; PNG pixel stats (median/p90/max) decide.
7. **Pixel stats prove brightness and nothing else.** Every frame in a 17-frame set scored "ok" on
   percentile luminance while containing gate piers rendered as black voids (measured RGB 1.1/0.0/0.8
   against an adjacent wall at 17.9/9.9/8.3 under the same lamp) and a magenta object. Meter FIRST to
   reject black/blown/flat frames, then LOOK at full size for material, colour and composition.
8. **`cmd > log; echo $?` reports the echo, not the command.** A background wrapper ending in `echo`
   returns 0 no matter what failed. Write the real status into the log (`echo "EXIT=$?" >> log`) and
   read THAT — a "completed (exit code 0)" notification is about the wrapper, not the gates.
9. **When a failure has an obvious defect class, sweep for the class immediately.** Three separate
   gate cycles were burned fixing copy-pinned assertions one at a time; one `grep -rn
   "StringAssert.Contains"` up front would have found all five at once.
10. **Do not reach for a cause before measuring.** Four hypotheses about one magenta object were each
    plausible and each wrong. The fixes that stuck all started from a measurement; every confident
    story told ahead of evidence had to be retracted.
11. **`unity-cli.mjs` needs `ps`, and an agent sandbox usually denies it.** Symptom: the run dies
    instantly with `spawnSync ps EPERM` and no Unity log, which reads like a Unity problem and is
    not — it is the host-health check that runs before Unity is ever launched. Run Unity tasks with
    the sandbox disabled. Recorded 2026-08-15 after rule 2 was re-learned the hard way in the same
    minute: the failure was piped through `| tail`, which reported exit 0 over the top of it.
12. **A batch run reporting an exit code is not the same as reading the results file.** The 2026-08-15
    run exited 1 and printed `324/332 passed, 8 failed`, but which 8, and whether they were the
    documented ones, is only in `unity-project/Logs/editmode-results.xml`. Parse that XML — the
    console summary named four scenes where the tracker claimed one.

## A test can encode a bug as a requirement (2026-08-15)

`NinthTollCrossesToWakeRoomWithoutFiringSecretEnding` asserted the player was at the wake room
**immediately** after toll 9 — no wait, no deadline, just a distance check on the next line. That
passed for a long time, and it was only ever true because the toll-nine payoff card was being
destroyed in its own frame: `ShowBeat` and `BeginCrossing` were called consecutively, so the crossing
took the screen instantly and nobody ever read the line the whole nine-count exists to deliver.

Fixing the bug — holding the card for its reading window before the cut — broke the test. The test
was wrong, not the fix. It had quietly encoded "toll nine IS the crossing", which was a description
of the defect.

Two things to carry forward:

1. **When a fix breaks a test, ask which one is describing the intended behaviour before touching
   either.** The reflex is to make the test pass again; here that would have meant deleting the fix
   and restoring unreadable text.
2. **An assertion with no wait is an assertion that something is instantaneous.** If the thing under
   test is a sequence, say so: wait with a deadline and assert after. The rewritten version waits for
   the crossing and now proves both halves — the card gets its window *and* the player arrives.

Related: give any new authored delay a test-side override the way `GmCrossing` already does for
`deadAir`/`whisperTime`/`irisTime`/`cardTime`. `GmBellSummons.takenCardHoldOverride` exists so a
proof can compress the hold rather than wait out a reading window at 20x timeScale — compressing a
real beat is fine; skipping it is not.

## The gate-2 deadlock (found 2026-08-15)

`scripts/run-opening-gates.mjs:11` runs gate 2 as `npm run test:unity` — **every** EditMode test,
unfiltered — and line 54 `break`s on first failure. Court's two tests fail *by design* (its content
is hard-locked behind Nick's Phase 0 walk). So the opening's gates 3-12 are not merely un-run, they
are **structurally unreachable**: the opening cannot be verified until Court is fixed, and Court
cannot be touched until after the walk that verification is supposed to precede.

This is the real reason "Gates 3-12 have never run against a compiling build" has persisted across
sessions. It is not a scheduling gap and no amount of fixing wend-hill-prologue will clear it.
Resolving it is a policy call (scope gate 2 to the scene under test plus shared systems, or carry an
explicit documented Court exclusion) and belongs to Nick, not to whoever notices it next.

## When adding grounds content

Tag every mount with `userData.gmKind`, add expected height range to `EXPECT` in
`agent-playtest.mjs` if it's a new kind, give POIs a radius the player can physically reach
(collision boxes push players away — radius must exceed block clearance), and run `npm run gates`.

**Coverage gap: `agent-playtest.mjs` drives the archived web build, not Unity.** It cannot
literally exercise a Unity-only interior (e.g. the coach house, `2026-08-13-the-reckoning.md`).
The honest substitute is a dedicated player probe modeled on `GmHouseProbe.cs` (see
`-gmOutbuildingProof`). Do not describe new Unity-only interiors as "covered by the walkthrough
rig" — that's the exact kind of claim the wiring-blind-spot lesson below warns about. Call it
what it is: covered by its own probe, not by the rig.

## Fix playbook — defect class → proven fix (all battle-tested 2026-07-16)

| Playtest finding / symptom | Root cause pattern | The fix that worked |
|---|---|---|
| Object reads pale/washed at night | r128 `outputEncoding=sRGB` lifts MAP-LESS material hexes ~2.2x. Textured materials display as authored. | The `_linearizeFlatColors()` sweep handles it automatically — author the hex at the value you want ON SCREEN. Never hex-darken to fight a wash; find the pipeline cause. |
| Distant objects wash pale / `fog:false` temptation | Fog color mixes pre-encode | Fog color is linearized at creation; keep world objects fogged. Only SKY (moon/stars/glow) and the story-critical mansion stay `fog:false`. |
| Frame near-black in the meter (p90 < 3 raw) | Unlit MeshBasic ignores lights — pools only light the Std-material ground | Lift the building's authored hex a notch AND add a cold moon-pool for the ground. Both, not either. |
| Floating / sunken object | Mount didn't ground the Box3 | Use the seat pattern: measure world box AFTER scale+rotate, set `y = (opt.y||0) - box.min.y`. Scatter (rock/mound/log) is deliberately part-buried — don't "fix" it. |
| POI won't fire though it looks close | Examine radius smaller than the object's collision clearance | Radius must exceed the block's push-away distance. Verify with the rig — it presses E from a walkable spot. |
| Area unwalkable / stuck | Rotated building's axis-aligned block balloons far past its footprint | Move the building, align it to an axis, or keep destinations outside the swollen AABB. Check `_blocks` vs `_walkRects` overlap when placing. |
| Door/prop reads as a sticker | Flat plane proud of a wall, no reveal depth | Build a reveal: deep jambs + lintel + threshold, object recessed INSIDE. Geometry beats paint at close range. |
| New asset intake | 30GB payloads, GUID-referenced textures that don't survive FBX→GLB | Hash-targeted tar extraction (index pathnames → hash dirs) → Blender convert → PREVIEW before placing (`preview-glbs.mjs` — the trunk totem died here) → night-dress in code → `EXPECT` range → gates. |
| Beat text unreadable | Hold time shorter than reading time (~250 wpm) | Words ÷ 4 ≈ seconds needed. The porch-dark beat was 22 words at 1.25s once. |
| Harness flakes under load | See the six harness rules above — it is nearly always the instrument, not the game | Rules 1–4 cover every flake seen so far. |
| `npm run gates` "passes" in seconds, or dies instantly | `node_modules` absent → gate 1 crashes on a missing `playwright` import. The runner **stops at the first failure**, so gates 2–11 never execute and produce NO evidence | `npm install`. Then read the summary table: a short run means gates never ran, not that they passed. |
| Unity editor code throws `Cannot find the-games-master repo` | `FindRepoRoot()` walked up from `Application.dataPath`, but the Unity project lives OUTSIDE the repo, and its fallback pinned one hardcoded path that broke when the repo moved | Search the Projects tree instead of pinning a path, and have `unity-cli.mjs` export `GM_REPO_ROOT` so the editor never has to guess. |
| `spawnSync ps EPERM` on every Unity command | `unity-cli.mjs` shells out to `ps` for its "editor closed" guard; a sandbox that blocks process listing kills every Unity gate | Run Unity commands outside the sandbox, or allow `ps`. Not a project defect — do not debug the game. |
| Magenta object that no test catches | The guard read `if (m == null \|\| m.shader == null) continue;` — **a null material renders magenta exactly like a built-in shader**, so it caught only half its own defect class. Separately, Terrain trees/details are drawn by the Terrain system and are invisible to `FindObjectsByType<Renderer>()` | Assert on null material AND null shader AND builtin shader; add a second guard walking `terrainData.treePrototypes`. Print full hierarchy paths — a bare GameObject name is useless in a 7,000-object estate. |
| Closed hub door tour is crushed black (mean ~9, 60% black) | Camera stood 4 m back in the hall while the only practical sat on the lintel. A shut dark leaf does not catch chandelier spill the way an open parlor threshold does. | Move the review camera inside the sconce pool (~2 m) and add a motivated fill on the leaf, still under 1.5 m from the visible fixture. Re-shot 24 went mean 9 → 32. |
| Hub "campaign walk" is green but a player cannot follow it | Each leg teleported the probe to a hardcoded pose. The foyer assertion set `z = -6` then demanded `z < -5`. The parlor finish used `bounds.Contains(...) \|\| x > 5.4`. Grate proof was `TargetSceneId` field equality. Tread tests counted name prefixes and never measured spacing. | Walk from the real player spawn with `Move` only. End pose of one leg is start of the next. Body must sit inside the grate and parlor trigger AABBs. Consecutive treads must match authored rise/run. |
| Cellar descent works, climb-out stalls mid-well | 0.12 m run on 0.24 m rise is a ladder. A hall-height landing collider sat in the shaft like a lid. At 0.24/0.24 with the top tread at the east panel, a 1.8 m capsule still snagged two lips: `HallFloorMidSouth`'s north face at `CellarHoleSouth` (y=-0.2..0 is a wall across a body still on the flight) and the vault north wall spanning the well x. | Pull the hall-floor north face south of the well (`HallFloorMidSouthNorth`). Keep the south header at y>=1.0 so hall walkers stop and a descending head (~0.6 at feet y=-1.2) passes under. Open the vault north wall across the well x. Prove with a connected spawn → grate → climb-out onto `HallFloorEast` (`x > CellarPanelX+0.4`, `y > -0.2`). |
| Improving UI copy fails several gates at once | Assertions pinned to display prose (`prompt.text.Contains("A / CROSS")`). Five such assertions across three files had frozen the UI: any wording improvement broke gates, so the incentive was to leave bad UI alone — a suite defending a defect instead of preventing one | Expose a state accessor (`PromptUsesControllerLabels`) and assert behaviour. Keep `StringAssert` for **data** — input binding paths, JSON keys — never for display copy. |
| Frame-time spike right after a visibility change | Enabling/disabling a large object set in ONE frame. 208 objects cost a 123ms spike; the main sweep already learned this and slices to 24/frame | Slice the transition with a cursor, and pass an unbounded budget only for `Start` and review captures so no screenshot catches a half-revealed room. |
| Perf regression that every audit misses | The newly added thing was exempt from BOTH the runtime culling and the editor perf pass, so the one system nobody measured was the one that regressed | When something is excluded from a perf system, that exclusion is a measurement blind spot — record it, and never let the same object be exempt from culling *and* auditing. |
| A fix "does nothing" when it actually worked | Judged an interior-visibility fix from the STATIONARY spawn sample (−0.58ms) and called it a failure; the full-route sample, which actually walks past the thing, showed −2.53ms | Read the sample that exercises the change. A sample taken where the change cannot apply is not evidence about the change. |
| A flake-rate measurement gives an alarming number | **The measurement was contaminated by other work on the same machine.** A 20-run batch reported 13 pass / 7 fail; a Unity editor batchmode suite had been launched in parallel. With nothing else running: 5/5 clean. Worse, 5 of the 7 "failures" had NO `FAILED` line in the player log at all — the player exited cleanly and the CLI rejected on the marginal p95 budget, so they were never the assertion under investigation | Run timing-sensitive batches with **nothing else executing** — no builds, no editor runs, not even in another terminal. Then read WHICH check failed before naming a cause: a nonzero exit from the CLI is not the same as a failed assertion inside the player. |
| Scripted gamepad input is flaky, so you "fix" the synchronisation | `InputSystem.QueueStateEvent` is asynchronous and consumers read EDGES (`wasPressedThisFrame`), so the press can land on a frame whose consumer already ran. Reaching for `InputSystem.Update()` to force a flush **makes it strictly worse**: the manual flush consumes the event inside that call, so the edge is gone before ANY consumer's Update runs. Result: a defect that failed once and passed on retry became every assertion failing deterministically — left stick 0.000m, right stick 0.000 degrees | Leave the natural flush alone; game code is synchronised against it. A rare flake turned into a hard break is a strictly worse trade. If the ordering must be controlled, prove it on one assertion before changing the shared `SendGamepad` that ~20 call sites depend on. |
| Same route measures wildly different p95 across runs | Long-route timing IS host-dependent (the short stationary sample is not). The same route gave 8.71ms and 17.24ms within an hour at load 8 vs load 19 with 15GB of 16GB swap in use. Two investigations were spent arguing which number was real | `stampHostState()` now writes `hostOneMinuteLoad` / `hostLoadPerCore` / `hostFreeMemoryMB` into both perf JSONs and warns above 1.0/core. **Read those fields before treating a p95 as a verdict**, and get a quiet-host baseline before calling a perf gate stable. |
| A change made on a disproved hypothesis is left in "on its own merits" | Forcing mesh-only tree rendering was tried against the magenta, did not fix it, and was kept anyway. It cost route p95 8.71ms → 16.10ms and preceded a fatal walk-probe crash (exit 255) | **Revert the moment the hypothesis dies.** A change whose reason turned out false has no remaining justification; keeping it is rationalising, not engineering. Leave a comment saying what was tried and what it cost so nobody retries it blind. |
| Magenta in evidence frames that NO scene test can find | **The instrument was in the shot.** `GmStandaloneReviewProbe` spawned a runtime primitive 1.45m from the camera as a controller-interaction target; a runtime primitive gets the built-in default material, which HDRP cannot render, so Unity substituted `Hidden/InternalErrorShader`. Five hypotheses died because the object is not in the scene at all — it exists only in the built player, only during the controller sequence | Disable the Renderer on any proof-only object: the collider is what a test needs, drawing is not. **Instrumentation must never appear in the composition it is verifying.** And when a defect resists scene-level analysis, stop guessing — ask the RUNNING player what occupies the pixel (raycast + screen-bounds scan at the centroid). That named it in one build cycle after five had failed. |
| A render defect survives every green gate | Nothing scanned the captured frames for colour. Worse, an **opaque UI panel in a screenshot is a blind spot** — everything behind it is unverifiable, and a magenta object hid behind the pause card through every gate run this project has ever done | `npm run verify:frames` (gate 12) scans every captured frame for magenta/near-black/blown/flat. When a UI panel covers scene content, capture the same pose without it — that one extra frame is what exposed this. |
| Gate 1 fails on `Unity scene source drift` mid-session | A repo file was edited after the last `unity:scene:sync`, so the Unity copy differs | Sync before running gates, and never edit tracked sources while a gates run is in flight. The drift gate is working correctly — do not "fix" it. |
| A background job's "exit code 0" is a lie | **The harness completion notice reported `exit code 0` on repeated `npm run gates`/`unity-cli.mjs test` runs whose captured output ended in a real failure.** Trusting the notice would have recorded a false pass every single time | Never grade a background run from its completion notice. `echo "EXIT_CODE=$?"` as the last command, then read the tail of the captured output. The log is the evidence; the notification is a hint. |
| A fix that reasons correctly about timing still breaks the test | Entry Hall's `devGoTo('card')` sets `isArrival:true` so `devSnapshot()` reports `phase:'card'`, which the harness asserts 160ms later. A LOW finding said `isArrival:true` also hides narrative beat text if the arrival sequence is left running. The obvious fix — clear `isArrival` inside the per-frame beat-firing code once a beat has something to show — computed correctly on paper (`dt` capped at 50ms/frame, `a` needs ~216ms of accumulated `dt` to cross the 0.06 threshold that fires the first beat, well past the 160ms wait) but the harness still failed with `phase=door` after the fix, meaning something in the headless timing crossed that threshold faster than the arithmetic predicted | Ran it, it failed, reverted immediately rather than iterate blind on a timing theory with no way to instrument the headless run further in the moment. **A fix that "should" work by the numbers still has to be run.** Documented the real, narrower gap (dev-tool-only, cosmetic) in a code comment instead of shipping an unverified guess. |
| Renaming a synced C# file leaves the old class still compiling | `sync-unity-scenes.mjs` is **additive only** — it copies repo→Unity and has no prune/delete path. After a rename, `unity-project/` keeps the stale file *and* its `.meta`, so Unity compiles both copies and reports a duplicate-definition CS0101 that does not exist in the repo | After any rename/delete of a synced source, remove the stale `unity-project/` copy **and its `.meta`** by hand, then `unity:scene:check`. The ownership-gap line (`Unity-only project source: …`) is the check naming exactly which orphans to delete. |
| `test:scene-system:csharp` fails with `CS2001: Source file … could not be found` | The Roslyn smoke test compiles against `Library/Bee/artifacts/**/Assembly-CSharp.rsp`, a **cache regenerated only by a real Unity compile**. After renaming/adding sources, the cached `.rsp` still lists the old file set. In `run-opening-gates.mjs` this check runs in gate 1, *before* the gate 2 Unity EditMode run that would refresh it | Run `node scripts/unity-cli.mjs test` once to refresh the `.rsp`, then re-run gates. A stale-cache failure here is the instrument, not the product. |
| `EADDRINUSE :::8813` mid-gates | The gates pipeline runs its own `test:archive:web`; a second harness run started by hand (or a leaked server from an earlier crash) already holds the port. Most `verify-*.mjs` scripts bind a hardcoded port with no `try/finally`, so any thrown assertion leaks the listener | Never run a harness script and `npm run gates` concurrently. If the port is stuck, find the owning PID — do not blanket-kill node. |
| `root commit is invalid: House envelope magic is invalid` on Boot, Mirror hidden, New Run refuses | Title `TryOpenOrCreate` treated a bad envelope as a hard stop. `TryResetHouseMemory` also called Open first, so recovery could not start. Short garbage fails length before magic; a long non-`TGMHOUSE` prefix is the real player case. | Do not auto-wipe. Record a sibling `.recovery-intent`, rename the domain to `.quarantine-{incidentId}`, genesis a new lineage. Crash resume completes that intent. Isolated New Run is in-memory Ordinary with `IsolatedRecovery` and never teaches. Boot: New Run still works, Reset is two confirms, Quit keeps files. Ordinary New Run stays clean when the domain is readable. |

## The wiring blind spot — unit-tested is not connected (2026-08-13)

A 262-agent source audit found ~50 confirmed findings that are all **one defect class**: a system is
built, unit-tested, and green, but **no gameplay code ever calls it.** The suite passes because the
tests invoke the scaffolding directly; the player never reaches it.

Confirmed instances: `GmSaveSystem.Load()` never called in production · `GmSceneDirector.TransitionTo`
only ever called by its own test · `GmEndingManager.ResolveEnding()` never called, so no ending
resolves in a real playthrough · `GmAudioManager` never instantiated and its `PlayAmbience/PlaySfx`
never assign a clip or call `Play()` · `GmPauseMenu`'s four tabs render zero content · `GmCreditsUI`
never instantiated (this one is a **licensing** exposure, not a polish item) · two independent
run-state stores (`GmRunStore` vs `GmHouseProgress`) that never exchange a value, which alone makes
two of the six endings unreachable.

**The rule this earns:** a green EditMode suite proves a unit behaves, never that it is reachable.
For any system that must run during play, add one test that asserts a **production call path**
exists — assert the caller, not just the callee. "Has tests" and "is wired" are different claims and
this project has been reporting the first as if it were the second.

## Ask the engine, don't do the arithmetic (2026-08-15)

**Symptom:** a prop or a spawn is placed at a position that reads as correct in the source and is
wrong in the scene — dice sunk 2.25cm into felt, a player capsule standing inside an armchair.

**Root cause:** the position was derived by hand from other numbers in the file. Every one of these
was produced *while carefully fixing the same class of defect somewhere else*. Hand-arithmetic does
not get more reliable with practice on the same afternoon; it gets less reliable, because the
attention has moved on to the next instance.

**The fix that worked:** stop deriving and start querying. `Physics.OverlapCapsule` with the
player's real capsule, inset by the controller's own skin width, plus a downward ray for the mirror
defect (overlaps nothing, stands on nothing). It went red on four of six rooms, **two of which a
careful manual read — an adversarial one, specifically looking for this — had called clean.**

Corollary, and the reason this is a rule rather than a note: **a check that measures cannot be
fooled by the confidence of the person who wrote the thing it measures.** Prefer a query over a
calculation anywhere the engine already knows the answer.

## Sweep the class in the same pass, or the gate teaches you the wrong lesson (2026-08-15)

The spawn probe found three decorative floor overlays with colliders (two carpets, a gravel path).
Fixing exactly those three would have passed the gate and left `HuntsmanPatrolTrack` — a fourth,
identical, 6cm strip across the middle of the Labyrinth — in the tree, because no spawn sits on it
and therefore no spawn test can ever see it.

**A gate reports the instances it can reach. It never reports the size of the class.** After any
gate goes red, grep for the shape of the defect before fixing the instances it named. Here that was
a four-line script for "primitive, thin in Y, at floor height, wide footprint."

Related: `GameObject.CreatePrimitive` attaches a collider to *everything*. A 2cm rug ships as a 2cm
kerb and the player walks with their ankles inside it for the whole room, not just at spawn. Use
`GmSceneBuildUtility.MakeDecorativeOverlay` for anything meant to be walked over.

## Two rules for writing a guard that will not quietly stop working (2026-08-15)

Both learned by writing guards that did exactly that, on the same day they were written.

**1. A guard must be exercised where it runs, not only where you wrote it.** `test-harness-integrity`
took its "accept good input" fixture from a gitignored Unity output directory. On the only CI job
that runs (ubuntu-latest, no Unity, fresh checkout) that file never exists, two of ten cases silently
skipped, and the summary still printed clean. Build fixtures in-process — the PNG in that file is
now assembled byte by byte — and **report attempted alongside passed**, so 8/10 can never read the
same as 10/10.

**2. Check for the thing, not for the marker that is supposed to accompany the thing.** The
composition contract's UI-only exit refuted itself by counting zones, clusters and elements. But a
scene that quietly grows geometry grows it *without* markers — markers are the first thing anyone
skips — so counting them to detect unmarked geometry is backwards. It now counts world-space
renderers and lights. The commit that introduced it claimed "a room can never quietly acquire this
and stop being checked"; that claim was false as written, and no test existed to contradict it.

**The tell for both:** ask how the check could pass while the defect is present. If the answer is
"the case never ran" or "the evidence it reads is optional," it is not a check yet.

## Choose the invariant that must hold, not the one that first suggests itself (2026-08-15)

Measuring Court's judge bench, the obvious assertion was "the chair must not overlap the desk." That
would have been a false alarm on **every tucked-in chair in the project** — a chair overlapping a
desk is what pushed-in furniture looks like. What actually has to be true is that a body fits: the
chair stays out of the wall, and there is clear floor behind the bench to sit in.

A guard that fires on correct work gets switched off, and then it protects nothing. Before asserting,
name the failure the assertion is for, and check it would not also condemn the healthy case.

## A percentile needs to say how repeatable it is, or it is not a measurement (2026-08-15)

**Symptom:** gate 8's p95 read 19.25ms in the morning and 24.67ms in the evening, on lighter load.
The tracker held four values (19.25 / 19.72 / 20.83 / 21.71) as a performance *trend*.

**Root cause:** the sample was 240 frames — about 2.5 seconds — which puts p95 at the twelfth-worst
frame. Hitches are rare and clustered, so the twelfth-worst mostly reports whether a cluster landed
inside the window. Three back-to-back runs of the *same unchanged build on the same idle machine*
gave **18.39 / 21.17 / 24.32ms — a 34% spread.** Every value in the "trend" fell inside one build's
noise. `p50` over the same runs was 8.44 / 8.45 / 8.48, so only the tail was noisy, and the tail is
the only thing the budget tests.

**The fix that worked:** 1800 frames, and each run splits its own sample in half and reports both
p95s. That gap is the measurement's reproducibility, established by the run that produced it rather
than assumed from a past one. Run-to-run spread fell to 5.4%; within-run agreement is now 0.4–3%.
If the halves disagree by more than 25% the run reports that it **judges nothing** — checked before
the budget, because a non-converged run that lands under the budget is not a pass either, and that
is precisely how a flaky gate teaches people to re-run until green.

**What it cost to learn twice:** this morning I added conditions to every p95 specifically so nobody
could supply a cause for a bare number, and wrote up the mansion-collision false lead. That same
evening I read two samples and called it a regression. **Conditions are not enough. A number also
has to carry its own variance**, or the next reader — including the person who built the tool —
will read two samples as a trend.

Between-build spread is still larger than within-run spread (a freshly rebuilt app measured 17.53ms
against 21ms for the previous binary), so a cross-build comparison still needs repeats. The failure
itself is real and reproducible: p95 exceeds the 16.7ms budget in every observation taken.

## Run the build. The test suite cannot see the player build (2026-08-15)

**Symptom:** none. Every suite green, 371 EditMode and 17 PlayMode, and the title screen threw
**9,906 NullReferenceExceptions in a twenty-second run** of the real macOS app.

**Root cause:** Unity 6 `PanelSettings` created at runtime with `CreateInstance` carry no ICU
payload. UI Toolkit's *advanced* text generator needs it, so `UITKTextHandle.ShapeText` throws inside
a job once per text element per frame and draws nothing. No crash, no visible error, no failing
test — the text is simply absent.

**The fix that worked:** `element.style.unityTextGenerator = TextGeneratorType.Standard`, applied
across a whole subtree by `GmUiText.UseStandardGenerator`.

**The part worth carrying:** this defect class had already been beaten. `GmPrologueHud` and
`GmHouseHud` each found it independently and each fixed it *on their own labels, with a comment
explaining it*. The boot scene written months later reintroduced it, and `GmCreditsUI` had it the
whole time. **A fix that lives in a comment in one file is not a fix for the next file.** When a
defect class is beaten, the fix belongs in a shared, callable thing, or the next file re-earns it.

Two habits this justifies:

1. **Build and run it, then read the whole log — not just for your own tag.** I found this by
   grepping the player log for exceptions after checking my own line was there. Had I grepped only
   for `[GmBoot]`, the run would have "passed."
2. **Do not dismiss a broad-pattern hit as noise.** The check that caught this was a loose
   `^\w*Exception:|^\s*Error` regex that looked like it would false-positive. It returned `True`,
   and the honest move was to go read what matched rather than tighten the pattern.

## A gate going green is not the defect going away (2026-08-15)

Chasing the prologue's orange cast produced two lessons worth more than the fix.

**1. A metric can improve while the defect survives.** Raising the moon took the worst frame's
red:blue from 2.94 to 2.09 and moved it under the new gate threshold. The ground in that frame is
*still* blown orange. The number improved because the cool background became visible, not because
the foreground got fixed — the metric was dominated by the part that was already fine. When a fix
moves a number, check that it moved the thing the number was standing in for.

**2. Sweep the suspect before rewriting it.** 2000K lamps read as sodium vapour on paper and looked
like the obvious cause. Swept it: 2000K gave 2.03, 2700K gave 2.13. Innocent. It stays at 2000
rather than being "improved" for nothing, and it is now overridable so the next person re-sweeps
instead of re-arguing. Two suspects eliminated with evidence beats one plausible story.

The bisect flags exist for exactly this: `-gmMoonLux`, `-gmSkyExposureDrop`, `-gmPracticalScale`,
`-gmIndirectDiffuse`, `-gmLampKelvin`, `-gmExposureEV`, passed straight through `unity-cli.mjs`
after the task name. Rebuild, tour, measure, compare. Never argue about a look you can render twice.

**Read the bisect line from the log the run actually wrote.** `rebuild wend-hill-prologue` writes
`cli-rebuild.log`, with no scene prefix, because the prologue is the default scene — every other
scene gets `cli-<scene>-rebuild.log`. Reading the prefixed file showed a two-hour-old line with the
default values and nearly cost a correct result its attribution.

## Interrogate the scene, not the code that writes it (2026-08-15)

**Symptom:** the drive's ground rendered as molten orange through every green gate.

**Four wrong answers, all reached the same way.** The lamps, the TerrainLayer diffuse remaps, the
cliff tint, the moon. Each was investigated by opening the file that sets that value and reading it.
Each reading was *accurate*. Three of them were about surfaces the camera was not looking at, which
is the most expensive kind of evidence: true, and about the wrong thing.

**Root cause, found in one pass by asking the scene instead.** A raycast down the actual route,
reporting the renderer, the material, the shader and the colours really on it:

```
_Mud_Tint = (0.576, 0.380, 0.000)
```

Blue is not low. Blue is **zero**. A surface with a dead channel cannot be lit in that colour by
anything — no moon brightness will ever cool it, because there is nothing there to reflect. It also
explains why raising the moon fixed the background and made the foreground *worse*.

**The specific trap:** `GmWendTerrainSurface` sets `diffuseRemapMin/Max` on the TerrainLayers, and
the terrain draws through `S_Landscape`, a **purchased Shader Graph**, which is free to ignore
TerrainLayer remaps entirely. It does. So the layer values were neutral, correct, and irrelevant.

**Rules this earns:**

1. **When a value you set does not show up on screen, check that the thing drawing it reads that
   value at all.** A custom shader graph, a material override, or a second material slot will
   silently win over the property you carefully set.
2. **Reading the writer proves what was written, never what is rendered.** For "why does this look
   wrong", query the live scene — what renderer is under this point, what material, what shader,
   what colour. `GmGroundToneProbe` is that query and is cheap to extend.
3. **A dead colour channel on a walkable surface is a defect, not a style.** Now asserted, with
   emission exempt because a lit window is allowed to be one hue. Proven by sabotage.

## A string tag is a dependency that can silently not exist (2026-08-15)

`GmSceneTransitionTrigger.OnTriggerEnter` read `CompareTag("Player")`. Nothing in this project has
ever set that tag — `GmPlayerRig` tags the *camera* `MainCamera` and leaves the body untagged. So
the component's only real entry path could never fire, and every test called `TriggerTransition()`
directly, which is why it looked covered.

**Prefer a component to a tag, a layer name, or a `GameObject.Find` path.** A missing component is a
compile-time or null-check failure. A missing tag is silence. `GetComponentInParent<GmPlayer>()`
also survives the hit landing on a child collider, which a tag on the root would not.

**And test the entry point Unity actually calls.** Reflecting into the private `OnTriggerEnter` is
worth the ugliness — the public method was green the whole time the component was dead. Include the
negative case, or "fire for anything" passes as a fix: a chair must not walk into the Parlor.

## Two Unity tasks back to back will collide (2026-08-15)

Chaining `unity-cli` invocations in one shell line fails the second with *"Unity has … open. Close
the editor first."* The previous editor has exited but not released the project yet. It reads like a
stale-lock bug and it is just a race. Wait for release between runs:

```bash
until ! pgrep -f "Unity.app/Contents/MacOS/Unity.*<project>" >/dev/null 2>&1; do sleep 5; done
```

Related, and it bit twice in one session: **run Unity tasks with the sandbox disabled.** Without it
`unity-cli` dies on `spawnSync ps EPERM` before Unity ever launches (rule 11), which also reads like
a test failure and is not one. A green-looking commit message was written on the back of that once
today; the tests did pass on re-run, but the claim was made before the evidence existed.

## The moon lights surfaces; the sky lights fog (2026-08-15)

The prologue's night was fixed three times in one session, and the reason it took three is that two
levers looked interchangeable and are not.

- **Moon (`-gmMoonLux`)** lights *surfaces*. Raising it gives warm lamps something cool to be warm
  against, which is what the night was actually missing.
- **Sky (`-gmSkyExposureDrop`)** lights *fog*. Raising it scatters across the entire upper half of
  every open frame, and turned nine at night into overcast dusk.

Raising both together fixed the cast and washed the night out. The answer was more moon and no more
sky: **6 lux at 4 stops**. Cast 2.94 → 2.31, night intact.

**The wash was caught by a person looking at a live window, not by any check**, and the reasons are
worth keeping:

1. The colour-cast rule measures **hue**, so it cannot see a frame that is correctly balanced and far
   too bright. It read 1.35 and passed.
2. The eight tour frames all point at the drive, not at open sky, so they stayed dark (median 18–54)
   while route frames hit 74 and map edges 122–164.
3. The frames that showed it **did not exist yet** — gates 9–11 had never run, because gate 8 stopped
   the pipeline dead.

## Separate the gates that BUILD from the gates that JUDGE (2026-08-15)

The pipeline stopped at the first failure, on the reasoning that every later gate depends on the
earlier artifact. True of the gates that produce things, false of the gates that assess them — and
it cost real coverage: gate 8's perf budget had been 4ms high all day, so gates 9, 10, 11 and 12 had
**never run**. Four gates' worth of evidence thrown away by one number.

This is the gate-2 deadlock again, one gate along. Each gate now declares whether its failure
invalidates what follows; blocking stays the default and every exception carries its reason. The run
still fails on any failure — it just stops discarding the evidence that would have followed. And
"did not run" prints as a **third state**, because the old summary silently omitted gates it never
reached, so seven-of-twelve looked identical to twelve-minus-one.

## Classify by origin consistently, or a font server fails your build (2026-08-15)

The web harness routed third-party *network* failures to a non-gating bucket and then pushed the
console error that **the same failure** produces straight into the gating list. A Google Fonts 404
therefore printed "47 passed, 0 failed" and returned red — and with the pipeline stopping at the
first failure, one CDN hiccup cost all twelve gates below it.

If a rule has an exemption, apply it to **every** signal the exempted thing emits, not just the one
you thought of. And when a gate fails while its own summary says everything passed, that gap is the
bug.

## Change an input by 20x. If the output does not move, you are tuning the wrong thing (2026-08-15)

**Symptom:** six rooms rendered as white boxes while the drive outside was a moonlit night.

**Three wrong answers**, each plausible, each carefully reasoned, each tuning something that
contributed a rounding error: add a fixed exposure; fix the fog colour mode; clamp the chandelier
from 800 lumens to the prologue's 200.

**What ended it was not thinking harder.** Cutting every practical in the Entry Hall from
800/200/200/150 lumens to 35 — between **6x and 23x** — changed the render almost not at all.

> A room whose appearance does not respond to a 23x cut in its own lights is not being lit by its
> lights.

**Root cause:** a volume profile carrying `Exposure` and `Fog` but no `VisualEnvironment` leaves
HDRP's **default sky** switched on, and it was lighting windowless interiors through ambient. A
windowless room has no sky; turning it off removes a light source that is not physically there.

**The technique, generalised.** When a fix does not land, stop refining it and instead move one
input by an absurd amount — 10x, 20x, to zero. The output either moves, which confirms you have hold
of the right lever, or it does not, which is far more informative than another careful adjustment.
It is the fastest way to find out that the thing you have been tuning does not matter, and it costs
one run.

Corollary, learned the same night: **two systems that share a constant cannot also share a budget.**
Interiors and exteriors share a fixed 0.3 EV deliberately, so the crossing does not read as a cut to
another film — which is precisely why they need *different* light budgets. The prologue's 200-lumen
practical ceiling is right at tens of metres and is a modern LED bulb at arm's length. Interiors cap
at 35: a paraffin lamp is 10-40 lumens, a candle about 12.

## Parlor review shots must isolate observed facts (2026-08-18)

The 24-shot Parlor tour stages unrelated frozen cases in one process. The evidence log is a run
journal. If the tour does not `Clear()` it before each case, shot 11 (a calm missed cheat) still
shows "His right hand stopped above the deck" from shot 05. Restore then faithfully round-trips the
pollution, so matching hashes are not proof the baseline was honest.

**Fix:** `GmParlorShotTour.IsolateReviewEvidence()` and the restore orchestrator both clear the log
and reset transient presentation before staging. Result shots that `FastForwardToCanonicalState()`
skip the Aldric presenter on purpose; their decision object is the feedback banner, not leftover
facts from a previous case.

**Rule:** a review harness that force-restarts canonical state must also reset player-facing
observation. Restore proofs compare isolated baselines, not the residue of earlier shots.

## House envelope magic on Boot (2026-08-18)

The 1080p player logged `[GmBoot] House title state unavailable: root commit is invalid: House envelope magic is invalid`. Ordinary New Run in that player still worked because the standalone probe never goes through Boot New Run. A real title click would have refused a persistent New Run, and Reset could not open the broken domain.

**Fix:** unreadable House files stay on disk until the player confirms Reset. New Run during recovery is isolated and cannot teach. A crash after the recovery intent is written finishes quarantine and genesis on the next open.

## Known coverage boundaries (honest)

- **Court / Shut the Box**: boot + phase screenshots only. Their gameplay is Phase 1/2 work,
  gated on Nick's Phase 0 walk; when they're built, extend the rig with a walkthrough section
  per scene (same pattern: stops, real input, interact asserts, metering).
- **Entry Hall**: boot + 5 dev phases shot; its 22 POIs are not yet individually E-verified by
  the rig. G3 item: hall walkthrough section.
- **Audio**: presence, provenance, duration, energy, spectral band, environmental drone risk,
  loop-boundary evidence, listener-filter routing, crossing order and native decode are automated.
  Actual loudness balance, fear and fatigue remain human-ear territory for Nick's walk.
- **Real-GPU pacing/feel**: headless proves sequence and logic, never feel. The human gate.
