# The Games Master

> "Seven games. One host. He's never lost."

A first-person psychological-horror card game (solo dev, atmospheric, Inscryption-adjacent). An engraved invitation arrives that knows your debts to the cent. It draws you to a decaying manor. Inside, a bound host — Aldric Voss, "the Games Master" — seats you at his table. He is a cheat and he never loses. He also *wants* to lose: he's trapped in the house by the same game, and only a player who beats him fairly can end the cycle, so he plants tells hoping you'll finally catch him.

Core loop: a trick-taking card game with four custom suits (Flames, Eyes, Bones, Teeth; Flames trump). At first you can't accuse him — he's simply too good, and you lose tricks you know you should have won. A Suspicion meter rises as something feels off. Only once you've felt it do you earn "the Read" — the ability to slow the table and catch a specific cheat. Catching cheats, not winning tricks, is the real game. Two gauges track the state: Corruption (the house's grip — as it climbs, the UI and Aldric distort) and Sanity (your own grip — low sanity tints and warps the room).

The current production target is the Unity 6000.5.3f1 HDRP opening, `WendHill_Prologue`. Its canonical source overlays live under `unity/` and sync into the local Unity project. The older Three.js pages remain useful as archived gameplay prototypes and compatibility fixtures; they are no longer the authoritative opening.

## Current status

The 435 m Wend Hill opening is the default registered scene and is review-ready: it rebuilds deterministically, passes its saved-scene contract, produces a macOS player, and passes story/input/audio, full-route performance, visual-tour, and boundary proofs. The rest of the seven-game experience is not yet a complete Unity game; Entry Hall, Court, Shut the Box, and Parlor remain represented by the archived web prototypes.

See [the 2026-07-31 audit](docs/playtest/wend-hill-prologue-audit-2026-07-31.md) for the exact coverage, results, artifacts, fixes, and remaining limits.

## Archived web prototype

Run these in order to experience the preserved browser prototype:

1. **`The Games Master - Prologue.dc.html`** — cold open. WASD + mouse-look walk up to the mansion through the fog, an invitation you can re-read, story beats, an animated door, the knockout.
2. **`The Games Master - Entry Hall.dc.html`** — explorable mansion interior (library, drawing room, dining room, study, grand staircase). 11 examinable points of interest, fully hand-built from primitive Three.js geometry + procedurally-generated canvas textures (wood grain, damask wallpaper, painted portraits) — no external model files.
3. **`The Games Master - Court.dc.html`** — the Court scene and evidence-presentation prototype.
4. **`The Games Master - Shut the Box.dc.html`** — the two-board Shut the Box game and its Hold/false-call rules.
5. **`The Parlor - Playable Prototype.dc.html`** — home base. The actual trick-taking card game against Aldric, with the blind phase → earned Read → Corruption/Sanity arc, win/lose states.

Each scene supports a dev menu (press `` ` `` in-browser) for jumping directly to any phase, and exposes `window.__GM` (`goTo`, `getState`, `errors`) for external test automation.

## Dev tools (not part of the shipped game)

- **`The Games Master - Test Harness.dc.html`** — automated smoke tests. Loads all 5 archived playable scenes into iframes, drives each through its `window.__GM` bridge, and asserts real behavior (scene builds, phase transitions, deck/suit/rule correctness, no runtime errors). See "Testing" below.
- **`The Games Master - Art Direction.dc.html`** — a moodboard/style-reference doc (palette, materials, tone, build order). Uses `image-slot.js` to drop in reference renders; that only works inside the claude.ai Design tool's live "omelette runtime" — outside it, the slots are read-only and log two harmless console errors (an invalid data-URI icon and a missing `.image-slots.state.json` sidecar).
- **`The Games Master - Playtest & Review.dc.html`** ("The Table Talks Back") — 6 fictional playtester personas critique the current build via `window.claude.complete` (Vera the horror devotee, Dex the systems optimizer, E. Halloran the narrative critic, Sam the first-timer, Iris on accessibility/UX, and a commercial skeptic), producing a severity-sorted fix backlog with suggested tests. Only functional inside a claude.ai environment that exposes `window.claude.complete`.

## Running the archived prototype locally

No build step — any static file server works:

```
npx serve .
# or
python3 -m http.server 8000
```

Then open `The Games Master - Prologue.dc.html` in a browser.

## Testing

**Interactive**: open `The Games Master - Test Harness.dc.html` in a browser. It runs automatically on load and reports live pass/fail per scene.

**Headless / CI**:

```
npm install
npm test
```

Runs `scripts/run-tests.mjs`, which drives the Test Harness headlessly via Playwright, waits for it to complete, and exits non-zero if anything fails.

For the complete current opening gate—including portable checks, Unity EditMode and PlayMode, canonical rebuild/audit, visual tour, macOS build, built-player controls/audio, the 435 m route, 1080p/60 performance, and boundary walls—run:

```
npm run verify:opening
```

## Assets

`assets/tex/*.jpg` — 8 textures used by the Prologue's outdoor path scene (brick/stone/grass/wood, each with diffuse/bump/roughness maps). `wood_bump.jpg` and `wood_roughness.jpg` are original; the other 6 are CC0 substitutes from [Poly Haven](https://polyhaven.com) (`brick_wall_001`, `stone_floor`, `sparse_grass`, `dark_wooden_planks`) — the originals hit a 256KB read-size limit when pulled from the source design project and came back truncated/corrupted.

The Entry Hall scene needs no external assets at all — its floor, walls, ceiling, carpet, and portraits are all generated procedurally via HTML5 Canvas at scene-init time.
