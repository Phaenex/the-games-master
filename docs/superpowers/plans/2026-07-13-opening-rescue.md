# Opening Rescue (Phase 0) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Prologue’s first 3–5 minutes feel like a real approach to a lit manor — correct car scale, one coherent gate, filled dressing to the door, and a facade that matches “lit up like a birthday” — before any new rooms start.

**Architecture:** All changes stay inside `The Games Master - Prologue.dc.html`’s existing `initScene()` / `loop()` / `devGoTo` surface, plus catalog and harness updates. No build step. Visual proof goes under `docs/playtest/screenshots/`. Progress lives in `docs/PROGRESS.md`.

**Tech Stack:** Vanilla JS, Three.js r128, existing `loadGLTF` / `GLTFLoader` pipeline, Playwright Test Harness via `npm test`.

---

### Task 1: Progress tracker

**Files:**
- Create: `docs/PROGRESS.md`

- [ ] **Step 1: Write the tracker** with overall %, Phase 0 checklist, 44-commit-ahead warning, visual gate requirements, and a dated CHECKIN entry for this session.

- [ ] **Step 2: Confirm path exists**

Run: `test -f docs/PROGRESS.md && head -20 docs/PROGRESS.md`

---

### Task 2: Catalog the live but undocumented models

**Files:**
- Modify: `asset-catalog.html`

- [ ] **Step 1: Add cards** for `assets/models/sourced/old_car.glb` and `assets/models/sourced/graveyard_gate.glb` under Exterior & Grounds.

- [ ] **Step 2: Mark license status honestly** — if no `license.txt` is on disk, tag as `LICENSE UNKNOWN / BLOCKED FOR SHIP CREDIT` until source is proven. Still catalog them because they are live in Prologue.

- [ ] **Step 3: Bump the “Downloaded & Ready” count** from 27 to 29.

---

### Task 3: Fix car scale and placement (TDD where possible)

**Files:**
- Modify: `The Games Master - Prologue.dc.html` (car load ~L494–507, `buildFallbackCar`)

**Target:** world-space length about 4.4–4.8 units, height about 1.45–1.65, parked off the left curb, visible when the player turns 180° at spawn (`cam` at z=72).

- [ ] **Step 1: Replace uniform `scale = 0.6` with measured scale**

After load, set temporary scale 1, measure `THREE.Box3().setFromObject(car)`, then set non-uniform or tuned uniform scale so the longest horizontal axis ≈ 4.6 and Y ≈ 1.55. Re-derive ground lift from the measured `box.min.y` after scale (do not hardcode `1.0186 * 0.6` forever).

```js
this.loadGLTF('assets/models/sourced/old_car.glb', (car) => {
  car.scale.set(1, 1, 1);
  car.position.set(0, 0, 0);
  car.rotation.set(0, 0, 0);
  car.updateMatrixWorld(true);
  const box = new THREE.Box3().setFromObject(car);
  const size = box.getSize(new THREE.Vector3());
  const targetLen = 4.6;
  const targetH = 1.55;
  const horiz = Math.max(size.x, size.z);
  const sXZ = targetLen / Math.max(0.001, horiz);
  const sY = targetH / Math.max(0.001, size.y);
  car.scale.set(sXZ, sY, sXZ);
  car.rotation.y = Math.PI * 0.15;
  car.updateMatrixWorld(true);
  const box2 = new THREE.Box3().setFromObject(car);
  car.position.set(-5.2, -box2.min.y, 78);
  this.scene.add(car);
  this.carModel = car;
  this._carSize = box2.getSize(new THREE.Vector3()).toArray().map(n => +n.toFixed(2));
}, () => { /* fallback */ });
```

- [ ] **Step 2: Resize `buildFallbackCar`** so the fallback length is ~4.6 (body `BoxGeometry` depth ~4.2+, wheelbase matching), same x/z as the real car.

- [ ] **Step 3: Expose size in `devSnapshot()`** as `carSize: this._carSize || null` for harness / console checks.

- [ ] **Step 4: Visual check** — screenshot looking back at the car from spawn before considering this done.

---

### Task 4: One coherent gate

**Files:**
- Modify: `The Games Master - Prologue.dc.html` (procedural gate block ~L450–468, gate swing in `loop`, `triggerGateLock`)

- [ ] **Step 1: Remove the oversized z=68 posts / arch / globe lights** that dwarf `graveyard_gate.glb` at z=65.

- [ ] **Step 2: Reposition hinged leaves** (`mkGate`) to `this.gateZ` (65) and shorten them to ~4.5 tall so they read as the closing leaves of the same gate, not a second monument.

- [ ] **Step 3: Keep retreat clamp + lock SFX**; when Reduce Motion is on, snap leaves closed instead of animating (match existing accessibility intent).

- [ ] **Step 4: Screenshot** gate from z≈70 and from just inside (z≈62).

---

### Task 5: Fill the empty approach

**Files:**
- Modify: `The Games Master - Prologue.dc.html` (trees, lamps, urns, hedges, fence span)

- [ ] **Step 1: Extend fence run** from `fenceRunEnd = -34` to `fenceRunEnd = -48` (stop before porch collision).

- [ ] **Step 2: Extend trees** loop to `z > -50` (same step 11).

- [ ] **Step 3: Add lamps** at `-36` and `-48`, and an urn pair at `-32`.

- [ ] **Step 4: Lengthen hedges** so they reach the same final third (adjust box depth/center, not only copies).

- [ ] **Step 5: Screenshot** mid-approach (`goTo('door')` or cam z≈-40).

---

### Task 6: Lit facade

**Files:**
- Modify: `The Games Master - Prologue.dc.html` (mansion load callback + door lights)

- [ ] **Step 1: After mansion loads**, traverse meshes; for materials whose name/map suggests glass/window (or all lighter trim), raise `emissive` / `emissiveIntensity` mildly. Safer complementary approach: add 4–6 warm `MeshBasicMaterial` window-glow quads on the front wall at door-wall depth.

- [ ] **Step 2: Boost facade point lights** so the house reads lit from the gate (z=72) without blowing the fog.

- [ ] **Step 3: Screenshot** from spawn and from mid-approach — windows must read as warm, not black.

---

### Task 7: Harness + secretEnding jump

**Files:**
- Modify: `The Games Master - Prologue.dc.html` (`devPhaseList`, `devGoTo`, `devSnapshot`)
- Modify: `The Games Master - Test Harness.dc.html`

- [ ] **Step 1: Add `'secretEnding'` to `devPhaseList()`** and a `devGoTo('secretEnding')` branch that calls `triggerSecretEnding()` (or sets the same state cleanly).

- [ ] **Step 2: Expose gate/car state in `devSnapshot()`:** `passedGate`, `gateLockFired`, `carSize`, `minZ`.

- [ ] **Step 3: Add Prologue harness tests:**
  - `goTo(secretEnding) → phase = secretEnding`
  - after `goTo(walk)`, simulate: set `_passedGate=true`, call `triggerGateLock`, assert `gateLockFired`
  - letter raised: `letterMounted` true blocks walk (existing path) — assert `goTo` walk then force letter raises and confirm phase still walk / walk flag false when letter up if snapshot exposes it

- [ ] **Step 4: Run `npm test`** — all green.

---

### Task 8: Visual gate log

**Files:**
- Create: `docs/playtest/screenshots/` shots (before/after)
- Modify: `docs/PROGRESS.md` CHECKIN with PASS / BORDERLINE / FAIL

Required shots:
1. Spawn facing house
2. Spawn turned 180° — car
3. Gate silhouette
4. Mid-approach (z≈-40)
5. Door / facade close

Do not claim Phase 0 complete on harness green alone.

---

### Task 9: Roadmap gate update

**Files:**
- Modify: `docs/superpowers/plans/2026-07-12-build-roadmap.md`

- [ ] **Step 1: Insert Phase 0** (opening rescue) before Court; note mandatory Nick playtests after Phase 0, 2, 7, and 9; panel only after 2 / 7 / 9; do not push 44 commits without explicit approval.

---

### Task 10: Human playtest checkpoint (handoff)

Not an automated task. After visual gate PASS or honest BORDERLINE, stop and wait for Nick to walk the approach once before Phase 1 Court begins.
