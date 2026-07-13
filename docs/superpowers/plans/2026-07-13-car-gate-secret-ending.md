# Car, Gate & Secret Ending Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a visible car at the walk's start, a real hinged-look gate near the existing "the gate was open" beat, retreat-detection that locks the gate shut if the player tries to back out after passing it, and a secret ending if the player instead retreats all the way back to the car before ever reaching the gate.

**Architecture:** First real integration of the glTF asset pipeline into a live scene — everything before this was cataloged but unused. Adds a `GLTFLoader` script include and a small loader/audio helper to Prologue, two new positioned models (car, gate), a z-position-tracking retreat detector, and one new game phase (`isSecretEnding`).

**Tech Stack:** Three.js r128's `GLTFLoader` (examples/js build via CDN, confirmed compatible), native `Audio` for one-shot SFX (no Web Audio API complexity needed for this), existing `GMSettings` for volume.

---

### Task 1: GLTFLoader + audio helper

**Files:**
- Modify: `The Games Master - Prologue.dc.html`

- [ ] **Step 1: Add the GLTFLoader script, after the existing three.min.js include**

```html
<script src="https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js"></script>
<script src="https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/loaders/GLTFLoader.js" integrity="sha384-fljlqkjWlmSFjkESkQvm77heIZpoWmXEOzlCA7kOpGUH+95Zk0yGfQieWM2q136E" crossorigin="anonymous"></script>
```
This hash was computed directly against the live file this session (`curl` the URL, `openssl dgst -sha384 -binary | openssl base64 -A`) — verify it still matches before using it, in case the CDN file has changed since.

- [ ] **Step 2: Add a `loadGLTF` helper method to the Component class**

```js
loadGLTF(url, onLoad) {
  if (!this._gltfLoader) this._gltfLoader = new THREE.GLTFLoader();
  this._gltfLoader.load(url, (gltf) => onLoad(gltf.scene), undefined, (err) => {
    console.warn('GLTF load failed:', url, err);
  });
}
```

- [ ] **Step 3: Add a `playSfx` helper method**

```js
playSfx(url) {
  try {
    const settings = window.GMSettings ? GMSettings.get() : { volMaster: 100, volSfx: 100 };
    const vol = (settings.volMaster / 100) * (settings.volSfx / 100);
    if (vol <= 0) return;
    const a = new Audio(url);
    a.volume = Math.min(1, vol);
    a.play().catch(() => {});
  } catch (e) {}
}
```
Note: this is the first real sound playback in the codebase — the Options screen's sliders have been silently controlling nothing until now. This makes them functional for the first time.

- [ ] **Step 4: Commit**

```bash
git add "The Games Master - Prologue.dc.html"
git commit -m "Prologue: add GLTFLoader pipeline and SFX playback helper"
```

---

### Task 2: Place the car

**Files:**
- Modify: `The Games Master - Prologue.dc.html`

**Context:** The walk currently starts at `cam.position.set(0, 1.7, 72)` (three call sites: `devGoTo('menu')`, `componentDidMount`-adjacent reset, and the main scene-init `cam.position.set(0, 1.7, 72)` at line 328). The car should sit just behind this start point, off to one side of the walkable path (not blocking it), visible as the thing the player just stepped out of.

- [ ] **Step 1: Load and position the car in scene construction** (near where other scene objects are added, in the method containing the line-328 camera setup)

```js
this.loadGLTF('assets/models/sourced/old_car.glb', (car) => {
  car.position.set(-4, 0, 78);
  car.rotation.y = Math.PI * 0.15;
  const scale = 1.4; // tune after visual check in Step 3
  car.scale.set(scale, scale, scale);
  this.scene.add(car);
  this.carModel = car;
});
```

- [ ] **Step 2: Record the car's z-position as a named constant** used later by the retreat detector — add near the top of the class (alongside `arrivalZ = -36`):

```js
carZ = 78;
gateZ = 65;
```

- [ ] **Step 3 — manual visual verification, do this for real:**
Serve the repo, load Prologue, skip to the walk phase, look at the car: confirm it's visible, roughly the right size relative to the player's eye height (1.7 units), not clipping through the ground, and not blocking the walkable path. Adjust `scale`, `position`, and `rotation.y` in Step 1 based on what you actually see — the values above are a starting guess, not final. Take a screenshot and look at it before deciding it's right.

- [ ] **Step 4: Commit**

```bash
git add "The Games Master - Prologue.dc.html"
git commit -m "Prologue: add visible car model at the walk's starting point"
```

---

### Task 3: Place the gate

**Files:**
- Modify: `The Games Master - Prologue.dc.html`

**Context:** `graveyard_gate.glb` is a single mesh already modeled as two flared-open panels (confirmed via direct inspection — not separately hinged, don't try to animate the two halves independently). Position it at `gateZ` (65), straddling the path so the player walks between/through it, matching the existing first beat's line ("The gate was open. It shouldn't have been.") which fires right around this z-position already.

- [ ] **Step 1: Load and position the gate**

```js
this.loadGLTF('assets/models/sourced/graveyard_gate.glb', (gate) => {
  gate.position.set(0, 0, this.gateZ);
  const scale = 1.0; // tune after visual check
  gate.scale.set(scale, scale, scale);
  this.scene.add(gate);
  this.gateModel = gate;
});
```

- [ ] **Step 2 — manual visual verification:**
Confirm the gate straddles the walkable path with a real gap for the player to walk through (the model's own two-panel geometry should already provide this — verify it actually does once placed, don't assume the isolated preview generalizes to in-context scale), sits at the right height (not floating, not sunk into the ground), and doesn't clip the existing fence/walk geometry. Adjust position/scale based on what's actually seen.

- [ ] **Step 3: Commit**

```bash
git add "The Games Master - Prologue.dc.html"
git commit -m "Prologue: add gate model at the walk's existing gate position"
```

---

### Task 4: Retreat detection — secret ending and gate-lock

**Files:**
- Modify: `The Games Master - Prologue.dc.html`

**Context:** The walk's per-frame update loop already reads `this.vx`/`this.vz` and updates camera position from WASD input (search for where `cam.position.z` is mutated each frame during `mode==='walk'`). Add tracking for: the furthest-forward z reached (`this._minZReached`), whether the player has passed the gate (`this._passedGate`), and whether the gate-lock sting has already fired (`this._gateLockFired`) so it only triggers once.

- [ ] **Step 1: Initialize tracking state** — add to `componentDidMount` or wherever walk-related instance state is reset when entering the walk phase:

```js
this._minZReached = 72; // starting z
this._passedGate = false;
this._gateLockFired = false;
```

- [ ] **Step 2: Add the check to the per-frame walk update**, right after the camera's z-position is updated each frame:

```js
if (this.mode === 'walk' && this.cam) {
  const z = this.cam.position.z;
  if (z < this._minZReached) this._minZReached = z;
  if (!this._passedGate && z <= this.gateZ) this._passedGate = true;

  const movingBackward = z > this._minZReached + 0.5; // small tolerance, not every frame's float jitter
  if (movingBackward) {
    if (!this._passedGate && z >= this.carZ - 3) {
      this.triggerSecretEnding();
    } else if (this._passedGate && !this._gateLockFired) {
      this._gateLockFired = true;
      this.triggerGateLock();
    }
  }
}
```

- [ ] **Step 3: Add `triggerGateLock()`**

```js
triggerGateLock() {
  this.playSfx('assets/sfx/gate_slam.ogg');
  setTimeout(() => this.playSfx('assets/sfx/gate_lock.ogg'), 550);
  if (this.gateModel) {
    const startRot = this.gateModel.rotation.y;
    const start = Date.now();
    const tick = () => {
      const t = (Date.now() - start) / 260;
      if (t >= 1) { this.gateModel.rotation.y = startRot + 0.35; return; }
      this.gateModel.rotation.y = startRot + 0.35 * t;
      requestAnimationFrame(tick);
    };
    tick();
  }
  this.speak('Something slammed shut behind me.', 'When I looked back, the gate was closed — and the lock, somehow, had already turned.', 6800);
}
```

- [ ] **Step 4: Add `triggerSecretEnding()`**

```js
triggerSecretEnding() {
  this.walkEnabled = false;
  this.mode = 'secretEnding';
  this.setState({ isSecretEnding: true, fade: 1 });
}
```

- [ ] **Step 5 — manual verification, actually do this live, don't just read the code:**
Serve the repo, enter the walk, walk forward past the gate (confirm `_passedGate` becomes true via a dev-bridge state check), then walk backward — confirm the gate-lock sting fires exactly once (sound plays, gate rotates slightly, beat text appears), and walking backward again afterward does NOT re-trigger it. Separately, restart the walk and immediately walk backward toward the car without ever passing the gate — confirm `triggerSecretEnding` fires and `isSecretEnding` becomes true.

- [ ] **Step 6: Commit**

```bash
git add "The Games Master - Prologue.dc.html"
git commit -m "Prologue: retreat detection — gate locks after passing it, secret ending if retreating to the car first"
```

---

### Task 5: The secret ending screen

**Files:**
- Modify: `The Games Master - Prologue.dc.html`

- [ ] **Step 1: Add the `isSecretEnding` overlay**, near the other full-screen phase overlays (`isArrival`, etc.):

```html
<sc-if value="{{ isSecretEnding }}" hint-placeholder-val="{{ false }}">
  <div style="position:fixed;inset:0;z-index:55;display:flex;flex-direction:column;justify-content:center;align-items:center;text-align:center;background:#000;">
    <div style="animation:rise 2.6s ease both;max-width:640px;padding:0 8vw;">
      <div style="font-family:'Cormorant Garamond',serif;font-style:italic;font-size:clamp(20px,2.7vw,31px);line-height:1.4;color:#EBE0D1;text-shadow:0 2px 18px #000;">I got back in the car.<br>Whatever was waiting up that drive would have to wait for someone else — there would always be someone else, hungrier or more desperate than I was tonight.<br><br>The house didn't need me specifically. It only needed someone to say yes.</div>
      <div style="margin-top:30px;font-family:'JetBrains Mono',monospace;font-size:11px;letter-spacing:0.3em;color:#8a6a1e;text-transform:uppercase;">— you left —</div>
      <a href="#" onClick="{{ onReturnToMenu }}" style="display:inline-block;margin-top:44px;font-family:'JetBrains Mono',monospace;font-size:12px;letter-spacing:0.26em;text-transform:uppercase;color:#A69980;border-bottom:1px solid rgba(166,153,128,0.45);padding-bottom:7px;text-decoration:none;">return to the menu</a>
    </div>
  </div>
</sc-if>
```

- [ ] **Step 2: Add `isSecretEnding:false` to the initial `state = {...}` object.**

- [ ] **Step 3: Add the `onReturnToMenu` handler**

```js
onReturnToMenu(e) {
  if (e && e.preventDefault) e.preventDefault();
  this.devGoTo('menu');
}
```

- [ ] **Step 4: Expose `isSecretEnding` and `onReturnToMenu` in the render-values method**, matching how other state/handlers are already exposed there.

- [ ] **Step 5 — manual verification:**
Trigger the secret ending (via the retreat mechanic or a dev-bridge shortcut), confirm the screen renders with the epilogue text, confirm "return to the menu" actually returns to the main menu and the game is in a clean, replayable state afterward (no leftover `isSecretEnding:true` blocking a fresh walk).

- [ ] **Step 6: Commit**

```bash
git add "The Games Master - Prologue.dc.html"
git commit -m "Prologue: add the secret ending screen for retreating to the car"
```

---

### Task 6: Test Harness + full suite verification

- [ ] **Step 1:** Check whether the Test Harness's Prologue assertions reference specific `devPhaseList()` entries or scene object counts that this change affects. Add `'secretEnding'` as a `devGoTo` target if not already covered by existing dev-bridge patterns, so the harness (or manual testing) can jump straight to it.
- [ ] **Step 2:** Run `npm test` — confirm no regressions.
- [ ] **Step 3:** Commit any Test Harness updates.

### Task 7: Final whole-implementation review

Dispatch a code-quality reviewer (or do this directly) across all files touched by Tasks 1-6:
- Confirm the GLTFLoader script's integrity hash is real, not the Step-1 placeholder.
- Confirm the retreat-detection tolerance (`+0.5`) doesn't false-trigger on ordinary camera jitter during normal forward walking — verify by walking forward continuously for the length of the whole beat sequence without ever triggering either the lock or the secret ending.
- Confirm `triggerGateLock`'s `requestAnimationFrame` loop doesn't leak if the player navigates away (e.g., via dev-menu jump) mid-animation — apply the same cancellation pattern used for the Entry Hall chandelier sway earlier this session if warranted.
- Full `npm test` passes one final time.
