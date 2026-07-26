# Testing — what to run, when

One rule: **`npm run gates` after any change.** It runs everything below in sequence and prints a
pass/fail table. Green = safe. A red gate's last output prints inline.

## The gates (all run by `npm run gates`)

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
