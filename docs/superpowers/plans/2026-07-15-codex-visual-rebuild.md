# The Games Master — Opening Visual Rebuild Plan

## Goal

Raise the opening from the current automated **mechanics PASS / visual BORDERLINE** to a visually credible horror approach before Nick is asked to play it.

This is a selective rebuild of the opening presentation. It is not a second mansion, a new story opening, or a full Unity scene import.

## Non-negotiable locks

- Threshold Refusal remains intact: front doors stay closed through the porch dark and KO.
- Keep the existing mansion silhouette unless a specific facade component is proven unusable.
- Do not import or serve whole Unity demo scenes.
- Use only web-sized, selected GLBs/textures from the purchased bundle.
- Do not buy anything else during this pass.
- No commit or push without Nick's explicit approval.
- Nick's Phase 0 walk remains the final human taste gate.

## Acceptance gates

### Runtime gate

- `npm test` remains 23 Shut-the-Box tests plus 47 harness tests (or an explicitly documented intentional change).
- `node scripts/play-door.mjs` reaches `aftermath`, has `errors:0`, and reports both door rotations as `0`.
- `node scripts/play-full.mjs` reaches `aftermath`, has `errors:0`, and reports Leartes selected assets ready.
- `node scripts/verify-env-entrance.mjs` remains PASS.
- `node scripts/verify-handoff.mjs` remains PASS.

### Visual gate

Every visual batch must capture and read these views:

1. Spawn facing the house
2. Spawn looking back at the car and outer gate
3. Gate open and gate locked
4. Cemetery-side pass
5. Garden-side pass
6. Mid-drive house read
7. Porch arrival
8. Closed-door hold
9. Porch-dark beat
10. Look-back drive after the gate locks

Each shot is classified PASS, BORDERLINE, or FAIL against these checks:

- no pale billboard/box geometry in the main sightline;
- no material that reads as unlit white plastic or clean greybox stone;
- trees have readable trunks/branch silhouettes at near, mid and far distance;
- the car reads as a car, not a white slab or floating light;
- gate bars, piers and fence remain legible without filling the screen with a flat wall;
- porch columns/rails/steps belong to the mansion's dark weathered material family;
- closed doors are readable but never open in the Threshold Refusal sequence;
- narrative text does not hide the visual subject at the capture moment.

### Performance gate

- No single selected GLB over 2 MB in the served opening unless it is a proven hero asset.
- No full Unity package or demo scene path may be referenced by the served HTML.
- Record scene mesh count, selected GLB bytes, and screenshot load time.
- No new page errors, failed GLB requests, or unbounded clone loop.

## Work order

### Batch 0 — Baseline and inventory

- Freeze current screenshots as the comparison set.
- Record `devSnapshot`, selected asset readiness, mesh counts and page errors.
- Inventory current `gmKind` groups and material classes in Prologue.
- Confirm which current objects are visually failing rather than replacing everything.

### Batch 1 — Grounds and gate readability

- Keep the existing gate collision and animation.
- Reduce/replace the largest low-poly foreground silhouettes in the gate look-back.
- Use Leartes dead willow/cemetery pieces only where they improve the camera sightline.
- Keep fog-distance fill cheap and dark.
- Recheck car silhouette and remove any map/light combination that turns it into a pale slab.

### Batch 2 — Cemetery and garden composition

- Build two intentional side compositions rather than a uniform asset scatter:
  - cemetery: wall, crosses, one readable hero tree, low grave dressing;
  - garden: broken stone, branch/log, low weeds, one asymmetrical focal prop.
- Keep all hero objects out of the car keep-out and the walk corridor.
- Verify no floating or hovering geometry with bounds probes.

### Batch 3 — Porch/facade hierarchy

- Preserve the mansion shell and closed-door trap.
- Establish three material values: near-black structural mass, weathered midtone facade, warm restrained windows.
- Remove pale map bleed from columns, rails, steps and trim without flattening the entire facade.
- Make the door panels readable at arrival distance while keeping the seam subtle.
- Keep porch lamps as fixtures with light pools, never floating glowing orbs.

### Batch 4 — UI and composition polish

- Tune invitation/beat overlays so the subject remains visible during capture and play.
- Preserve readable story copy and accessibility scale.
- Verify reduced-motion mode does not reintroduce visual artifacts.

### Batch 5 — Regression battery

- Run all runtime gates.
- Run `scripts/verify-polish.mjs`, `scripts/qa-opening-phys.mjs`, `scripts/verify-gate-stars.mjs`, and `scripts/verify-green.mjs` when present.
- Run the full walk twice from cold browser contexts to catch async asset races.
- Capture the ten-view matrix from two viewport sizes.
- Read every new screenshot and record a verdict table.

### Batch 6 — Handoff gate

- Update `docs/PROGRESS.md` with honest results.
- Leave Nick Phase 0 open until his walk.
- Hand Nick one launch URL, one expected route, the screenshot matrix, and three taste questions: mood, car, and grounds.

## Final handoff definition

The rebuild is ready for Nick when every runtime gate is green, no visual shot is FAIL, no more than two are BORDERLINE with a documented reason, and two cold full walks are error-free. “Ready for Nick” does not mean “finished game”; Court remains gated by the human walk.
