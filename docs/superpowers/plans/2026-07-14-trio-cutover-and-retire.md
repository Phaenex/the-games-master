# Trio Cutover + Old-Asset Retirement Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** After Nick buys the Unity trio, convert → wire → visually gate each slice, then **delete only what those packs replace** (Model T car, gravyart shell + painted facade doors if Env wins, dead stubs). Do **not** wipe MetalMan / ruins / asylum / bulk Unity dressing.

**Architecture:** New packs land under `assets/models/unity/{modular-victorian-interior,victorian-mansion-environment,realistic-car-hd-03}/` via the existing extract→FBX→GLB pipeline. Scenes keep loading through `THREE.GLTFLoader` / `gm-unity-props.js`. Retirement is two-phase: **quarantine** (stop referencing) → **delete** only after agent visual gate PASS on that slice. Gate / fence / grounds stay.

**Tech Stack:** Unity `.unitypackage` → `scripts/extract-unitypackage.mjs` → `scripts/fbx-to-glb.mjs` / `scripts/unity-bulk-convert.mjs` → Prologue / Entry Hall / Court / STB `.dc.html` · Playwright capture scripts · `npm test`

**Buy trio (Nick):**
1. Modular Victorian Interior — $35 — [Unity](https://assetstore.unity.com/packages/3d/environments/modular-victorian-interior-mansion-167750)
2. Victorian Mansion Environment — $79.99 — [Unity](https://assetstore.unity.com/packages/3d/environments/victorian-mansion-environment-269740) · **door trailer check before buy**
3. Realistic Car HD 03 — $30 — [Unity](https://assetstore.unity.com/packages/3d/vehicles/land/realistic-car-hd-03-113200) (or HD 02 sedan $45)

**Canon locks:** Modern car · Victorian house face · coaching-inn history (not saloon) · closed-door KO · procedural gate stays.

---

## Kill vs Keep (locked before any `rm`)

### KILL after gate PASS (replaced by trio)

| Asset | Why | After |
|-------|-----|--------|
| `assets/models/sourced/car_model_t.glb` | Wrong era Model T | HD 03 live on spawn + secret ending |
| `assets/models/sourced/old_car.glb` | Dead stub / prior blob | Deleted with Model T |
| Model T lines in `sourced/CREDITS.txt` | Stale credit | Replace with Unity EULA credit for HD 03 |
| `assets/models/exterior/` (gravyart `scene.gltf` + bin + textures + license) | Replaced by Env shell | **Only if** Env hinged doors + porch gate PASS |
| Prologue painted `doorFaceTex` facade leaves | Fake doors on sealed gravyart | **Only if** Env supplies hinged door meshes |
| Catalog cards marking Model T / gravyart as live dress | Stale | Point at new paths |

### KEEP (trio does not replace these)

| Asset | Why |
|-------|-----|
| Procedural front **gate** + `iron_fence/` | Not in Env cart · already ships |
| `ruins_pack.glb` + lanterns / drive dress | Estate grounds |
| `assets/sfx/*` | Already wired |
| MetalMan / study / furniture Unity GLBs | Dress Modular rooms |
| `Door_Double.glb` | Interior Hall parlor doors until Modular has better |
| Asylum / Flooded / wall_modular | Labyrinth L5 |
| Hall pack (`assets/models/hall/*`) + sourced dice / book / candelabra / piano… | Encounter props |
| Bulk `assets/models/unity/*` (5023) | Scar mine · do **not** mass-delete |
| Procedural gavel / STB tiles / portraits / ledger code | Systems |

### QUARANTINE (stop using; delete only if confirmed unused by grep)

| Asset | Notes |
|-------|--------|
| `assets/models/sourced/graveyard_gate.glb` | LICENSE UNKNOWN · unused by live gate (procedural) — delete once `rg` clean |
| Rejected Downloads zips references in catalog | Docs cleanup only |
| LOWPOLY Victorian exterior as **shell candidate** | Demote in docs; keep GLBs in unity tree for parts if useful |

**Hard rule:** Never delete gravyart or painted doors in the same commit as first Env load. Quarantine → screenshots → Nick OK or agent gate PASS → then delete.

---

## File map (create / modify)

| Path | Role |
|------|------|
| `assets/models/unity-import/<pack>/` | Extracted FBX (gitignored if huge) |
| `assets/models/unity/modular-victorian-interior/` | Final interior GLBs |
| `assets/models/unity/victorian-mansion-environment/` | Final exterior GLBs |
| `assets/models/unity/realistic-car-hd-03/` | Final car GLB(s) |
| `assets/models/unity/CREDITS.txt` + pack CREDITS | Unity Standard EULA attribution |
| `The Games Master - Prologue.dc.html` | Car + Env facade + door hinges + secret ending |
| `The Games Master - Entry Hall.dc.html` | Modular shell under MetalMan dress |
| `The Games Master - Court.dc.html` | Modular room walls |
| `The Games Master - Shut the Box.dc.html` | Modular hall |
| `scripts/verify-trio-car.mjs` | New · spawn + secret car shots |
| `scripts/verify-trio-facade.mjs` | New · porch / doors / drive |
| `scripts/verify-trio-modular.mjs` | New · Hall / Court / STB room shells |
| `scripts/retire-old-assets.mjs` | New · lists + deletes only after `--confirm` + path allowlist |
| `asset-catalog.html` | Mark trio HAVE · update kill list |
| `docs/PROGRESS.md` | Check-ins |

---

### Task 0: Purchase gate + drop packages on disk

**Files:**
- Nick: Unity Asset Store downloads
- Create: drop path note in check-in only

- [ ] **Step 1: Door trailer check on Env**

Watch [Victorian Mansion Environment](https://assetstore.unity.com/packages/3d/environments/victorian-mansion-environment-269740) trailer.  
**Pass:** separate hinged door leaves. **Fail:** do **not** buy Env; buy Modular + HD 03 only (~$65); keep gravyart for Task 4 skip path.

- [ ] **Step 2: Buy + download all three `.unitypackage` files**

Copy into a staging folder Nick owns, e.g.:

```bash
mkdir -p ~/Projects/the-games-master/_incoming-unity
# move downloads:
# Modular Victorian Interior*.unitypackage
# Victorian Mansion Environment*.unitypackage
# Realistic Car HD 03*.unitypackage   # or HD 02
ls -la ~/Projects/the-games-master/_incoming-unity/
```

Expected: three packages present (or two if Env skipped).

- [ ] **Step 3: Tell the agent “trio landed”** with exact filenames

Do not start extract until files are on disk.

---

### Task 1: Extract + convert to GLB

**Files:**
- Use: `scripts/extract-unitypackage.mjs`, `scripts/fbx-to-glb.mjs` or `scripts/unity-bulk-convert.mjs`
- Create: `assets/models/unity/modular-victorian-interior/`, `victorian-mansion-environment/`, `realistic-car-hd-03/`
- Modify: `assets/models/unity/CREDITS.txt`

- [ ] **Step 1: Extract each package**

```bash
cd /Users/damato/Projects/the-games-master
node scripts/extract-unitypackage.mjs \
  "_incoming-unity/<Modular…>.unitypackage" \
  assets/models/unity-import/modular-victorian-interior
node scripts/extract-unitypackage.mjs \
  "_incoming-unity/<Victorian Mansion…>.unitypackage" \
  assets/models/unity-import/victorian-mansion-environment
node scripts/extract-unitypackage.mjs \
  "_incoming-unity/<Realistic Car HD…>.unitypackage" \
  assets/models/unity-import/realistic-car-hd-03
```

Expected: each outdir has `.gm-extracted` + Models/FBX tree.

- [ ] **Step 2: Convert FBX → GLB into final folders**

Prefer pack-scoped convert (same pattern as prior MetalMan runs):

```bash
# Example — adjust globs after listing FBX:
find assets/models/unity-import/realistic-car-hd-03 -iname '*.fbx' | head
node scripts/unity-bulk-convert.mjs \
  --in assets/models/unity-import/realistic-car-hd-03 \
  --out assets/models/unity/realistic-car-hd-03
# repeat for modular-victorian-interior + victorian-mansion-environment
```

Expected: car has ≥1 hero GLB with materials; Modular has room modules/walls; Env has house + **Door** named meshes.

- [ ] **Step 3: Inventory door + car hero meshes**

```bash
node scripts/inspect-glb.mjs assets/models/unity/realistic-car-hd-03/**/*.glb 2>/dev/null | head -80
# or open catalog / temporary harness — list mesh names containing Door|Car|Wheel
rg -l 'Door|door' assets/models/unity/victorian-mansion-environment | head
```

Write a short block into `assets/models/unity/CREDITS.txt`:

```
## NEW 2026-07-14 — buy trio
Modular Victorian Interior — Unity Standard EULA — commercial OK
Victorian Mansion Environment — Unity Standard EULA — commercial OK
Realistic Car HD 03 — Unity Standard EULA — commercial OK
Hero car GLB: <exact relative path>
Env door meshes: <names>
```

- [ ] **Step 4: Commit assets + credits (when Nick asks to commit)**

```bash
git add assets/models/unity/modular-victorian-interior \
  assets/models/unity/victorian-mansion-environment \
  assets/models/unity/realistic-car-hd-03 \
  assets/models/unity/CREDITS.txt
git commit -m "$(cat <<'EOF'
Add converted Unity trio GLBs (Modular, Env, Car HD 03).

EOF
)"
```

---

### Task 2: Wire modern car (Prologue) — then retire Model T

**Files:**
- Modify: `The Games Master - Prologue.dc.html` (~line 610 `car_model_t.glb` load)
- Modify: secret-ending car path if separate load
- Create: `scripts/verify-trio-car.mjs`
- Delete (after PASS): `assets/models/sourced/car_model_t.glb`, `old_car.glb`
- Modify: `assets/models/sourced/CREDITS.txt`

- [ ] **Step 1: Point Prologue at HD 03 hero GLB**

Replace load path (exact path from Task 1 inventory):

```javascript
// was: assets/models/sourced/car_model_t.glb
this.loadGLTF('assets/models/unity/realistic-car-hd-03/<HERO>.glb', (car) => {
  // keep spawn world pose; remeasure bbox and set uniform scale so length ≈ 4.4–4.8 world units
  // dull near-black paint: traverse meshes → color.lerp(nearBlack, 0.35–0.5) if too shiny/chrome
  // headlights ON at night (keep existing spot + emissive pattern)
});
```

Re-fit: after load, `Box3().setFromObject(car)` → scale so length on drive axis matches current ~4.6 stance; nose toward house; Y so tires sit on road.

- [ ] **Step 2: Wire secret-ending / leave-before-gate to same asset**

Same hero GLB (or LOD). No Model T fallback.

- [ ] **Step 3: Verify script + shots**

```bash
node scripts/verify-trio-car.mjs
# or extend capture-car.mjs paths
npm test
```

Expected: shots `docs/playtest/screenshots/trio-car-01-spawn.png`, `trio-car-02-3q.png`, `trio-car-03-secret.png` · modern sedan/hatch · no Model T silhouette · `npm test` green.

- [ ] **Step 4: Quarantine Model T (stop referencing)**

```bash
rg -n 'car_model_t|old_car' --glob '!docs/**' --glob '!**/PROGRESS.md'
```

Expected: zero hits outside quarantine docs / this plan.

- [ ] **Step 5: Delete retired car files + CREDITS rewrite**

```bash
rm -f assets/models/sourced/car_model_t.glb assets/models/sourced/old_car.glb
# Edit CREDITS: remove Model T block; note HD 03 lives under unity/CREDITS.txt
```

- [ ] **Step 6: Commit when Nick asks**

```bash
git commit -m "$(cat <<'EOF'
Replace Model T with Realistic Car HD 03; delete car stubs.

EOF
)"
```

---

### Task 3: Wire Modular interiors (Hall → Court → STB)

**Files:**
- Modify: `The Games Master - Entry Hall.dc.html`
- Modify: `The Games Master - Court.dc.html`
- Modify: `The Games Master - Shut the Box.dc.html`
- Keep: MetalMan prop loads in `gm-unity-props.js` / per-scene `placeUnity`
- Create: `scripts/verify-trio-modular.mjs`

- [ ] **Step 1: Entry Hall — Modular shell behind existing props**

Load a Modular corridor/room kit GLB as the **walls/floor/ceiling** group. Keep MetalMan chairs/couches/clock/portraits. Do not delete MetalMan.

Pattern (match existing Hall loaders):

```javascript
const loader = new THREE.GLTFLoader();
loader.load('assets/models/unity/modular-victorian-interior/<ROOM_SHELL>.glb', (gltf) => {
  const shell = gltf.scene;
  // scale/position to envelope current Hall bounds (~existing floor plane)
  shell.traverse((o) => {
    if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; }
  });
  scene.add(shell);
  this.modularHall = shell;
});
```

Hide/remove only **box/primitive wall planes** that Modular now covers (named hall wall meshes), not furniture.

- [ ] **Step 2: Court shell redress**

Same: Modular room under Table_Large / chairs / candelabra / gavel props. Keep `gm-court-props.js`.

- [ ] **Step 3: STB shell redress**

Modular hall; keep dice.glb + hinged tiles + dust frames.

- [ ] **Step 4: Visual gate**

```bash
node scripts/verify-trio-modular.mjs
npm test
```

Expected: `trio-modular-hall.png`, `trio-modular-court.png`, `trio-modular-stb.png` · Victorian walls readable · furniture not floating / double-walls fixed.

- [ ] **Step 5: Commit when Nick asks**

```bash
git commit -m "$(cat <<'EOF'
Dress Hall/Court/STB with Modular Victorian Interior shells.

EOF
)"
```

---

### Task 4: Wire Env facade (Prologue) — then retire gravyart **only if PASS**

**Files:**
- Modify: `The Games Master - Prologue.dc.html` (`assets/models/exterior/scene.gltf` load ~line 726 + painted doors ~761–850)
- Create: `scripts/verify-trio-facade.mjs`
- Delete (conditional): entire `assets/models/exterior/`
- Modify: catalog attribution footer

**Skip this whole task if Env was not purchased** (door trailer fail). Leave gravyart.

- [ ] **Step 1: Load Env mansion as Prologue shell**

Replace:

```javascript
// was: assets/models/exterior/scene.gltf
this.loadGLTF('assets/models/unity/victorian-mansion-environment/<HOUSE>.glb', (mansion) => {
  // remeasure: porch at drive end, eye-height 1.7 vs facade
  // position so porch door ≈ world z≈-52 band matching gate/drive clamps
});
```

- [ ] **Step 2: Bind Env door meshes (or keep painted fallback one session)**

If Env has Left/Right door meshes:

```javascript
// find by name from Task 1 inventory
this.doorL = mansion.getObjectByName('<LeftDoor>');
this.doorR = mansion.getObjectByName('<RightDoor>');
// KO beat: doors stay CLOSED (current closed-door trap) — do not swing open for fashion
```

If doors are sealed slabs: **abort Env as shell**, revert to gravyart this task, do not delete exterior/.

- [ ] **Step 3: Strip painted procedural door group when Env doors work**

Remove `doorFaceTex` canvas leaves / frame that sat on gravyart. Keep frame only if Env needs an occlusion plane for KO blackout.

- [ ] **Step 4: Facade visual gate (mandatory before delete)**

```bash
node scripts/verify-trio-facade.mjs
node scripts/play-door.mjs   # KO still closed-door
node scripts/play-full.mjs   # drive still locks
```

Expected shots: `trio-facade-01-drive.png`, `trio-facade-02-porch.png`, `trio-facade-03-door-closed.png`, `trio-facade-04-ko.png`.  
**PASS criteria:** hinges read as real doors · night lit · porch not kit-clash with MetalMan when entering Hall · `errors:0`.

- [ ] **Step 5: Quarantine gravyart**

```bash
rg -n 'models/exterior|gravyart' --glob '!docs/**' --glob '!asset-catalog.html'
```

Point every runtime path at Env. Catalog still mentions gravyart as retired until delete.

- [ ] **Step 6: Delete gravyart exterior tree**

```bash
rm -rf assets/models/exterior
```

Update catalog attribution: Env pack EULA instead of gravyart CC-BY.

- [ ] **Step 7: Commit when Nick asks**

```bash
git commit -m "$(cat <<'EOF'
Replace gravyart facade with Victorian Mansion Environment; remove painted doors.

EOF
)"
```

---

### Task 5: Retire other dead assets + catalog/PROGRESS sync

**Files:**
- Create: `scripts/retire-old-assets.mjs` (allowlist delete)
- Modify: `asset-catalog.html` (trio → HAVE; kill cards)
- Modify: `docs/PROGRESS.md`
- Delete: `graveyard_gate.glb` if unused

- [ ] **Step 1: Grep safety before any bulk delete**

```bash
rg -n 'car_model_t|old_car|models/exterior|graveyard_gate|doorFaceTex' \
  --glob '*.html' --glob '*.js' --glob '*.mjs'
```

Expected after Tasks 2–4: only docs / comments / this plan (optional).

- [ ] **Step 2: Allowlisted retire script**

`scripts/retire-old-assets.mjs` only deletes this allowlist (hardcoded):

```js
const ALLOW = [
  'assets/models/sourced/car_model_t.glb',
  'assets/models/sourced/old_car.glb',
  'assets/models/sourced/graveyard_gate.glb', // only if rg clean
  // exterior/ only if --with-exterior and Env PASS flag file exists:
  // 'assets/models/exterior/**'
];
```

```bash
node scripts/retire-old-assets.mjs --dry-run
node scripts/retire-old-assets.mjs --confirm
# optional:
node scripts/retire-old-assets.mjs --confirm --with-exterior
```

- [ ] **Step 3: Update `asset-catalog.html`**

- Buy-now trio → status HAVE / WIRED  
- Stage 0 car + Env rows green  
- Stage 1/3/4/5 Modular green  
- On-disk preview: remove Model T card or mark DELETED  
- Owned dress: gravyart → RETIRED (or remove)

- [ ] **Step 4: Full regression**

```bash
npm test
node scripts/play-gate.mjs
node scripts/play-full.mjs
node scripts/verify-handoff.mjs
```

Expected: all green · no 404 on retired paths.

- [ ] **Step 5: PROGRESS check-in**

Append dated entry: trio wired · files deleted · visual gate paths · % note honestly (visual holes closed; systems still open).

---

### Task 6: What we are **not** doing in this plan

Explicit YAGNI / no scope creep:

- [ ] Do **not** delete `assets/models/unity/` bulk (5023 GLB)  
- [ ] Do **not** rebuild procedural gate  
- [ ] Do **not** buy KitBash / saloon / Synty  
- [ ] Do **not** replace MetalMan furniture just because Modular landed  
- [ ] Do **not** implement Court/STB full AI / labyrinth / endings in this cutover  
- [ ] Do **not** force-push or push without Nick approval  

---

## Order of operations (single checklist)

1. Nick buys + drops packages (Task 0)  
2. Extract + convert (Task 1)  
3. Car wire → delete Model T (Task 2)  
4. Modular Hall/Court/STB (Task 3)  
5. Env facade → delete gravyart **only if PASS** (Task 4)  
6. Retire stubs + catalog (Task 5)  

**Rollback:** keep `_incoming-unity/` + `unity-import/` until Task 5 done. If Env fails porch: keep `assets/models/exterior/` forever for this release line.

---

## Self-review

1. **Spec coverage:** Buy → convert → car → Modular → Env → retire → catalog. Matches catalog stages 0–5 visual holes. Systems 2–8 left out on purpose.  
2. **Placeholders:** Hero GLB / door mesh names filled at Task 1 inventory (cannot know pre-extract). Marked as `<HERO>` / `<LeftDoor>` from inventory step, not “TBD later.”  
3. **Consistency:** Kill list matches Tasks 2/4/5; KEEP list forbids mass Unity purge.
