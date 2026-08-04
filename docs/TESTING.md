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

## When adding grounds content

Tag every mount with `userData.gmKind`, add expected height range to `EXPECT` in
`agent-playtest.mjs` if it's a new kind, give POIs a radius the player can physically reach
(collision boxes push players away — radius must exceed block clearance), and run `npm run gates`.

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
| Improving UI copy fails several gates at once | Assertions pinned to display prose (`prompt.text.Contains("A / CROSS")`). Five such assertions across three files had frozen the UI: any wording improvement broke gates, so the incentive was to leave bad UI alone — a suite defending a defect instead of preventing one | Expose a state accessor (`PromptUsesControllerLabels`) and assert behaviour. Keep `StringAssert` for **data** — input binding paths, JSON keys — never for display copy. |
| Frame-time spike right after a visibility change | Enabling/disabling a large object set in ONE frame. 208 objects cost a 123ms spike; the main sweep already learned this and slices to 24/frame | Slice the transition with a cursor, and pass an unbounded budget only for `Start` and review captures so no screenshot catches a half-revealed room. |
| Perf regression that every audit misses | The newly added thing was exempt from BOTH the runtime culling and the editor perf pass, so the one system nobody measured was the one that regressed | When something is excluded from a perf system, that exclusion is a measurement blind spot — record it, and never let the same object be exempt from culling *and* auditing. |
| A fix "does nothing" when it actually worked | Judged an interior-visibility fix from the STATIONARY spawn sample (−0.58ms) and called it a failure; the full-route sample, which actually walks past the thing, showed −2.53ms | Read the sample that exercises the change. A sample taken where the change cannot apply is not evidence about the change. |
| Scripted gamepad input is flaky, so you "fix" the synchronisation | `InputSystem.QueueStateEvent` is asynchronous and consumers read EDGES (`wasPressedThisFrame`), so the press can land on a frame whose consumer already ran. Reaching for `InputSystem.Update()` to force a flush **makes it strictly worse**: the manual flush consumes the event inside that call, so the edge is gone before ANY consumer's Update runs. Result: a defect that failed once and passed on retry became every assertion failing deterministically — left stick 0.000m, right stick 0.000 degrees | Leave the natural flush alone; game code is synchronised against it. A rare flake turned into a hard break is a strictly worse trade. If the ordering must be controlled, prove it on one assertion before changing the shared `SendGamepad` that ~20 call sites depend on. |
| Same route measures wildly different p95 across runs | Long-route timing IS host-dependent (the short stationary sample is not). The same route gave 8.71ms and 17.24ms within an hour at load 8 vs load 19 with 15GB of 16GB swap in use. Two investigations were spent arguing which number was real | `stampHostState()` now writes `hostOneMinuteLoad` / `hostLoadPerCore` / `hostFreeMemoryMB` into both perf JSONs and warns above 1.0/core. **Read those fields before treating a p95 as a verdict**, and get a quiet-host baseline before calling a perf gate stable. |
| A change made on a disproved hypothesis is left in "on its own merits" | Forcing mesh-only tree rendering was tried against the magenta, did not fix it, and was kept anyway. It cost route p95 8.71ms → 16.10ms and preceded a fatal walk-probe crash (exit 255) | **Revert the moment the hypothesis dies.** A change whose reason turned out false has no remaining justification; keeping it is rationalising, not engineering. Leave a comment saying what was tried and what it cost so nobody retries it blind. |
| Magenta in evidence frames that NO scene test can find | **The instrument was in the shot.** `GmStandaloneReviewProbe` spawned a runtime primitive 1.45m from the camera as a controller-interaction target; a runtime primitive gets the built-in default material, which HDRP cannot render, so Unity substituted `Hidden/InternalErrorShader`. Five hypotheses died because the object is not in the scene at all — it exists only in the built player, only during the controller sequence | Disable the Renderer on any proof-only object: the collider is what a test needs, drawing is not. **Instrumentation must never appear in the composition it is verifying.** And when a defect resists scene-level analysis, stop guessing — ask the RUNNING player what occupies the pixel (raycast + screen-bounds scan at the centroid). That named it in one build cycle after five had failed. |
| A render defect survives every green gate | Nothing scanned the captured frames for colour. Worse, an **opaque UI panel in a screenshot is a blind spot** — everything behind it is unverifiable, and a magenta object hid behind the pause card through every gate run this project has ever done | `npm run verify:frames` (gate 12) scans every captured frame for magenta/near-black/blown/flat. When a UI panel covers scene content, capture the same pose without it — that one extra frame is what exposed this. |
| Gate 1 fails on `Unity scene source drift` mid-session | A repo file was edited after the last `unity:scene:sync`, so the Unity copy differs | Sync before running gates, and never edit tracked sources while a gates run is in flight. The drift gate is working correctly — do not "fix" it. |

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
