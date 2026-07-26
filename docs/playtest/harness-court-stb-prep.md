# Test Harness Prep — Court + Shut the Box

Do **not** add these scene slots until Nick clears Phase 0 and Court/STB `.dc.html` files exist. This note is the copy-paste checklist so the first Court stub does not break the current **35** harness tests.

## Current harness shape

`The Games Master - Test Harness.dc.html` loads scenes in order:

1. Prologue
2. Entry Hall (`pois===21`)
3. Parlor

Each scene is an iframe. Assertions use `window.__GM` (`goTo`, `getState`, `errors`).

## Planned scene files (names reserved)

| Scene | File | When |
|-------|------|------|
| Court | `The Games Master - Court.dc.html` | After Nick Phase 0 walk |
| Shut the Box | `The Games Master - Shut the Box.dc.html` | After Court ships |

## `__GM` contract Court should expose (mirror Entry Hall)

```js
window.__GM = {
  scene: 'court',
  phases: () => ['idle','argument','verdict'], // whatever CourtdevPhaseList returns
  goTo: (n) => this.devGoTo(n),
  getState: () => this.devSnapshot(),
  errors: () => this._errs || [],
};
```

Minimum `devSnapshot` fields for first harness group:

- `sceneReady` (bool)
- `errors` length 0
- `pois` (number — set expected in harness after POIs land)
- `sealCount` / `sealsCracked` once wax mechanics exist
- `gavelTarnish` (0..1) optional for tarnish assertion

## Suggested first Court harness tests (add as new group, leave existing 35 alone)

1. bridge present · scene = court  
2. three.js scene builds (`sceneReady`)  
3. no runtime errors  
4. gavel present (`__GMC.gavel` or snapshot flag)  
5. present true evidence cracks a seal (drive via `__GM` helper once built)  
6. straight trial loseable (scripted fail path) — later phase

## Shut the Box harness (after scene)

Load scripts in scene HTML **before** boot:

```html
<script src="gm-shutbox-logic.js"></script>
<script src="gm-shutbox-board.js"></script>
```

Logic is already covered by Node: `node scripts/test-shutbox.mjs` (chained from `npm test`).

Browser tests later:

1. both boards sync from fresh boxes  
2. legal move shuts tiles  
3. Hold palm / falseCall / tamper via `__GM.hold(...)`  
4. tile-9 Hold sets `opensHiddenDoor`

## Do not

- Pre-register empty iframes pointing at missing files (404 → harness timeout).  
- Change Entry Hall `pois===21` unless Entry Hall POIs actually change.  
- Bump CI timeout only if Court GLTF load makes 150s tight.

## Modules ready today (no harness registration)

| File | Role |
|------|------|
| `gm-shutbox-logic.js` | Pure rules + Hold |
| `gm-shutbox-board.js` | Procedural boards (needs THREE + logic) |
| `gm-court-props.js` | Gavel / wax seal / dust sheet |
| `scripts/test-shutbox.mjs` | Node gate for logic |
