# Prologue Intro Scene (Wend Hill Approach) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended)
> or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Make the Unity Prologue approach a walkable, judgeable opening — arrive at a car, pass a gate
that locks behind you, walk a drive under a lit Victorian house, and be taken on its porch — matching
the tested web build beat for beat.

**Architecture:** The web build (`The Games Master - Prologue.dc.html`) is the tested design source and
owns every coordinate, transform and line of copy. `unity/design-data/prologue-design.json` is the
shared contract, already consumed by both. The Unity runtime scripts that *drive* the intro are already
written and working; what is missing is almost entirely **geometry to look at**. This plan adds the
three absent hero assets (mansion, gate, car), enforces the walk bounds, ports the two unported
systems (cold open, secret ending), and locks both of this session's root causes behind regression
tests so they cannot silently return.

**Tech Stack:** Unity 6000.5.3f1 / HDRP · C# (editor builders + runtime MonoBehaviours) · NUnit EditMode
tests · Blender 4.x headless (glTF→FBX) · `scripts/unity-cli.mjs` for all Unity invocation.

---

## Read this before touching anything

Three constraints are settled by evidence this session. Re-litigating them wastes hours:

1. **HDRP will not render in batchmode here.** `GmProbe` already tried the RenderTexture path and got
   blank frames. `unity-cli.mjs tour` launches a *windowed* editor deliberately. Builders and tests
   stay headless because they never render.
2. **`~/GamesMaster-Unity` is outside the Bash sandbox write allowlist.** Every Unity invocation needs
   the sandbox disabled or it dies instantly with exit 127 and no log.
   **Blender needs it too, with a different signature** (found executing Task 1): it *segfaults* inside
   Metal GPU-backend detection (`MTLBackend::metal_is_supported`) during `wm_homefile_read`, even with
   `--background` and no rendering. Not exit 127, not "Operation not permitted" — a crash. Disable the
   sandbox for any Blender invocation and don't waste time debugging the model.
3. **The Leartes packs ship built-in AND HDRP variants under the same names.** Built-in copies use the
   Standard shader (`guid: 0000000000000000f000000000000000`) which HDRP renders magenta. Always go
   through `GmEstateBuilderV2.PreferHdrp()` / `FindAssetPrefab()`. Never call `AssetDatabase.FindAssets`
   directly for a prop.

And one trap that is invisible in the editor: `VolumeProfile.Add<T>()` only builds overrides in memory.
Without `AssetDatabase.AddObjectToAsset` the profile reloads as `components: []`, HDRP falls back to its
default sky **and default automatic exposure**, and the estate renders as blue daylight while every
lighting value you change does nothing. Task 10 locks this behind a test.

---

## State audit — what already exists (do not rebuild)

Verified by reading the source this session, not assumed:

| System | File | State |
|---|---|---|
| First-person walk, mouse look, `E` interact | `Assets/Scripts/GmPlayer.cs` | **Works.** InputSystem API. |
| 13 POIs with the two-layer `text2` reveal | `Assets/Scripts/GmDesignRuntime.cs` | **Works.** `seen++` drives layer 2. |
| 5 z-triggered drive beats | `Assets/Scripts/GmDesignRuntime.cs` | **Works.** Fires on z-crossing, 4.2s hold. |
| Threshold Refusal (settle, 3 beats, KO, fade, aftermath) | `Assets/Scripts/GmThreshold.cs` | **Complete.** Timings 0.15/3.2/6.4 match design data. |
| Gate lock on first backward move | `Assets/Scripts/GmThreshold.cs` | **Complete logic.** No gate to look at. |
| Chapel bell rare event | `Assets/Scripts/GmRareEvents.cs` | **Works.** Forecourt-gated. |
| Ambience beds + footsteps, 14 SFX | `Assets/Scripts/GmAmbience.cs` | **Works in editor.** See Task 11 for the build bug. |
| Estate: 15 placements, trees, fence, drive, night | `Assets/Editor/GmEstateBuilderV2.cs` | **Works.** Real night as of this session. |
| Shut the Box rules + 23 NUnit tests | `Assets/GamesMaster/ShutTheBox/` | **Green.** Not part of the intro. |

## State audit — what is missing (this plan)

| Gap | Why it matters | Task |
|---|---|---|
| **No mansion** | The Prologue *is* walking toward a lit house. `mansionZ = -58` is authored; nothing is placed there. | 1–3 |
| **No gate** | `gateZ = 65`. `GmThreshold` locks a gate that does not visually exist. Beat 1 is "The gate was open." | 4 |
| **No car** | `carZ = 78`. POI at `(-4.8, 76.5)` has copy but no object. Spawn reads as an empty road. | 5 |
| **Secret ending not ported** | Retreating to the car before the gate is a real shipped state in the web build. | 6 |
| **Cold open not loaded** | 4 authored lines. `GmDesignRuntime` never reads `coldOpen`. | 7 |
| **Walk rects not enforced** | Parsed into `walkRects` and never used. Player can walk into the void. | 8 |
| **No porch/drive lamps** | "Lit up like a birthday" needs light. `GmRareEvents` window-figure hook waits on this. | 9 |
| **Window figure unarmed** | Hook exists, needs the mansion. ~1 walk in 3. | 9 |
| `SM_House_02` magenta | Coach house at z=50. No HDRP variant exists in the pack. | 10 |
| Spawn bare | Tree lines start z=58; spawn is z=72. 14 units of nothing. | 10 |

---

## File structure

**Create:**
- `scripts/gltf-to-fbx-blender.py` — headless Blender glTF→FBX with embedded textures. One job.
- `Assets/Editor/GmMansion.cs` — mansion import/place/dress. Isolated because the gravyart shell has
  model-specific quirks (`Cube043`, window emissive) that must not leak into the generic estate builder.
- `Assets/Scripts/GmColdOpen.cs` — cold-open card sequence. Runtime, separate from `GmDesignRuntime`
  because it owns pre-walk state, not walk state.
- `Assets/Scripts/GmSecretEnding.cs` — retreat-to-car exit. Runtime, own file: it is a *seventh state*
  outside the six endings (story bible §7) and should read as its own thing in the codebase too.
- `Assets/Tests/EditMode/GmEstateBuildTests.cs` — builds the scene and asserts against design data.
  This is where both root causes get locked down.

**Modify:**
- `Assets/Editor/GmEstateBuilderV2.cs` — call the new builders; spawn-end tree fill.
- `Assets/Scripts/GmDesignRuntime.cs` — load `coldOpen`; expose `WalkRects` for the collider builder.
- `Assets/Scripts/GmRareEvents.cs` — arm the window figure now that a mansion exists.
- `Assets/Scripts/GmAmbience.cs` — fix the editor-only clip lookup.
- `unity/design-data/prologue-design.json` + `Assets/StreamingAssets/prologue-design.json` — add the
  three hero placements. **Both copies. They must not diverge.**

**Do not touch:** `Assets/GamesMaster/ShutTheBox/` (source of truth is the web repo,
`unity/shut-the-box/`). `GmThreshold.cs` (complete and correct).

---

## Story fit — what each object is doing narratively

Not decoration. Every hero asset carries a beat that is already written:

- **The car** is the escape that stays possible until it isn't. Its POI: *"The keys are still in it.
  Nobody took them. Nobody needed to."* Layer 2 is Nora's drawing of *"a house with too many windows.
  I never showed her this place."* It is also the secret ending's exit — walk back to it before the
  gate and you leave, no trace, because per story bible §1 the invitation was never fate.
- **The gate** is the point of no return, and the name. Canon: *"Wend"* is an old word for a turn in a
  road; the drive that turns you toward the house **is** the name. Beat 1 fires at z=64, one unit past
  it: *"The gate was open. It shouldn't have been."* Then it slams and the lock turns by itself.
- **The mansion** is *"lit up like a birthday, and not one sound coming off it."* It must read as
  occupied and silent from 130 units away, and its doors must never open — Threshold Refusal is locked
  canon. You are not a civil caller; you are cargo for the ledger night. The `Cube043` slab is hidden
  precisely so the doors *can't* be walked through.

---

## Task 1: Convert the gravyart mansion to FBX

The asset is already owned and already art-directed: `assets/models/exterior/scene.gltf` +
`scene.bin` (17.5MB) + `textures/`. "Haunted Victorian House" by gravyart, CC-BY-4.0, commercial use
allowed, **author must be credited**. The threshold spec says explicitly: no second mansion purchase,
the gravyart shell stays. Unity cannot import `.gltf` without a package, so convert with the Blender
that is already installed.

**Files:**
- Create: `scripts/gltf-to-fbx-blender.py`
- Create (output): `assets/models/exterior/fbx/haunted_victorian_house.fbx`

- [ ] **Step 1: Write the conversion script**

```python
# scripts/gltf-to-fbx-blender.py
# Headless glTF -> FBX for Unity import. Blender is Z-up and glTF is Y-up; the FBX exporter's
# axis_forward/axis_up below hand Unity a Y-up, -Z-forward model so the native orientation survives
# the round trip. Textures are embedded so the FBX is self-contained inside Assets/.
#
#   blender --background --python scripts/gltf-to-fbx-blender.py -- <in.gltf> <out.fbx>
import bpy, sys, os

argv = sys.argv[sys.argv.index("--") + 1:]
src, dst = argv[0], argv[1]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
os.makedirs(os.path.dirname(dst), exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=dst,
    path_mode='COPY',
    embed_textures=True,
    axis_forward='-Z',
    axis_up='Y',
    apply_unit_scale=True,
    bake_space_transform=False,
    object_types={'MESH', 'EMPTY'},
    use_mesh_modifiers=True,
)

# Report the bbox so the caller can verify orientation survived rather than trusting the exporter.
xs, ys, zs = [], [], []
for o in bpy.context.scene.objects:
    if o.type != 'MESH':
        continue
    for c in o.bound_box:
        w = o.matrix_world @ __import__('mathutils').Vector(c)
        xs.append(w.x); ys.append(w.y); zs.append(w.z)
print(f"[gltf2fbx] BBOX x {min(xs):.2f}..{max(xs):.2f} y {min(ys):.2f}..{max(ys):.2f} z {min(zs):.2f}..{max(zs):.2f}")
print(f"[gltf2fbx] WROTE {dst}")
```

- [ ] **Step 2: Run the conversion**

```bash
/Applications/Blender.app/Contents/MacOS/Blender --background \
  --python scripts/gltf-to-fbx-blender.py -- \
  assets/models/exterior/scene.gltf \
  assets/models/exterior/fbx/haunted_victorian_house.fbx 2>&1 | grep -E 'BBOX|WROTE|Error'
```

Expected: a `[gltf2fbx] BBOX ...` line and `[gltf2fbx] WROTE ...`.

**Verify the bbox against the web build's measured native bbox** (from the Prologue source comment):
`x -8.04..7.96, y -1.43..15.69, z -6.07..7.11`. Blender is Z-up, so its reported Y and Z will be
swapped relative to that. What must hold: the model is **~16 wide, ~17 tall, ~13 deep**. If the tall
axis is not ~17, the axis conversion is wrong — fix `axis_up`/`axis_forward` before continuing. Do not
"correct" it later with a rotation on the prefab; that hides the problem from every future import.

- [ ] **Step 3: Copy into the Unity project**

```bash
mkdir -p ~/GamesMaster-Unity/Assets/GamesMaster/Exterior
cp assets/models/exterior/fbx/haunted_victorian_house.fbx ~/GamesMaster-Unity/Assets/GamesMaster/Exterior/
cp assets/models/exterior/license.txt ~/GamesMaster-Unity/Assets/GamesMaster/Exterior/
```

The license file travels with the model. CC-BY requires attribution and this is the only thing that
keeps the credit attached to the asset.

- [ ] **Step 4: Commit**

```bash
git add scripts/gltf-to-fbx-blender.py assets/models/exterior/fbx/
git commit -m "feat(unity): convert gravyart mansion glTF to FBX for Unity import"
```

---

## Task 2: Place the mansion at the authored transform

Every number here is solved already — copy them, do not re-derive. From the web Prologue (lines
~851-860), with the reasoning it recorded:

```js
const mansionScale = 1.0;  // reads as a proper 2.5-story Victorian at 1.7-unit eye height
const mansionX = 0.52;     // recenters the model's left-right axis onto the path (world x=0)
const mansionY = 1.43;     // lifts the foundation's lowest point to ground level
const mansionZ = -58;
mansion.rotation.y = -Math.PI / 2;  // native front (+X) -> world +Z, the approach direction
```

`rotY = -90` maps local +X to world +Z in Unity's left-handed system exactly as it does in Three's
right-handed one (both use `x' = x·cosθ + z·sinθ`), so the value carries over unchanged.

**Files:**
- Create: `Assets/Editor/GmMansion.cs`
- Modify: `Assets/Editor/GmEstateBuilderV2.cs` (call it from `Build()`)

- [ ] **Step 1: Write the mansion builder**

```csharp
// Assets/Editor/GmMansion.cs
// The gravyart "Haunted Victorian House" shell (CC-BY-4.0, credit in Exterior/license.txt).
// Kept out of GmEstateBuilderV2 because every constant here is specific to THIS model: the
// transform was measured against its native bbox, and Cube043 is its own sealed-door slab.
using UnityEngine;
using UnityEditor;

public static class GmMansion
{
    const string Fbx = "Assets/GamesMaster/Exterior/haunted_victorian_house.fbx";

    // Measured in the web build against the model's native bbox (x -8.04..7.96, y -1.43..15.69,
    // z -6.07..7.11). Do not re-derive: these are visually verified at 1.7-unit eye height.
    const float Scale = 1.0f;
    const float PosX  = 0.52f;   // recenters local Z onto the path at world x=0
    const float PosY  = 1.43f;   // lifts the foundation to ground
    const float RotY  = -90f;    // native front (+X) -> world +Z, the approach direction

    public static GameObject Build(float mansionZ, Transform parent)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        if (prefab == null)
        {
            Debug.LogError($"[GmMansion] FAILED: {Fbx} missing — run scripts/gltf-to-fbx-blender.py first");
            return null;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = "Mansion (gravyart Haunted Victorian House)";
        go.transform.position = new Vector3(PosX * Scale, PosY * Scale, mansionZ);
        go.transform.rotation = Quaternion.Euler(0, RotY, 0);
        go.transform.localScale = Vector3.one * Scale;

        SealTheDoors(go);
        WarmTheWindows(go);
        return go;
    }

    /// Threshold Refusal is locked canon: the front doors never open. Cube043 is the model's own
    /// door slab; hiding it means there is no leaf to swing and no "how I got inside" to explain.
    static void SealTheDoors(GameObject root)
    {
        int hidden = 0;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != "Cube043" && !t.name.StartsWith("Cube043_")) continue;
            var r = t.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
            else t.gameObject.SetActive(false);
            hidden++;
        }
        Debug.Log($"[GmMansion] sealed door slab: {hidden} Cube043 object(s) hidden");
    }

    /// "A house this size lit up like a birthday, and not one sound coming off it." The window
    /// materials arrive as dark PBR with zero emissive and read as a dead box from the gate. Warm
    /// ONLY the windows -- warming the facade bleaches it to limestone, the "clean marble" tell.
    static void WarmTheWindows(GameObject root)
    {
        var warm = new Color(1f, 0.72f, 0.36f);
        int lit = 0;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            foreach (var m in r.sharedMaterials)
            {
                if (m == null || !m.name.ToLower().Contains("glass") && !m.name.ToLower().Contains("window")) continue;
                if (!m.HasProperty("_EmissiveColor")) continue;
                m.SetColor("_EmissiveColor", warm * 8f);   // HDRP emissive is in nits; 8 burns at night
                m.SetFloat("_UseEmissiveIntensity", 0);
                m.EnableKeyword("_EMISSIVE_COLOR_MAP");
                lit++;
            }
        }
        Debug.Log($"[GmMansion] warmed {lit} window material(s)");
        if (lit == 0)
            Debug.LogWarning("[GmMansion] no window/glass materials matched — the house will read as a dead box from the gate");
    }
}
```

- [ ] **Step 2: Call it from the estate builder**

In `Assets/Editor/GmEstateBuilderV2.cs`, inside `Build()`, after `BuildPlacements(d);`:

```csharp
        BuildPlacements(d);
        GmMansion.Build(d.world.mansionZ, new GameObject("Mansion").transform);
        BuildPlayerAndSystems(d);
```

If `GmEstateBuilder.Design.world` has no `mansionZ` field, add it alongside the existing world fields
(it is already in the JSON at `-58`).

- [ ] **Step 3: Rebuild and read the log**

```bash
node scripts/unity-cli.mjs rebuild 2>&1 | grep -E '✓|✗'
grep -E '\[GmMansion\]' ~/GamesMaster-Unity/Logs/cli-rebuild.log
```

Expected: `sealed door slab: N Cube043 object(s) hidden` with N ≥ 1, and `warmed N window material(s)`
with N ≥ 1. **If either is 0, stop.** Zero hidden slabs means the doors are walkable and Threshold
Refusal is broken. Zero warmed windows means the house is a dead box and beat 1 is a lie.

- [ ] **Step 4: Look at it**

```bash
node scripts/unity-cli.mjs tour 2>&1 | tail -16
```

Then **open `Screens/WendHill/tour-01-spawn.png` and `tour-12-porch.png` and actually look**. The
luminance gate only proves pixels exist. What you are judging: is there a house at the end of the
drive, does it read as occupied, is it the right size against a 1.7-unit eye. A meanLum number cannot
answer any of that.

- [ ] **Step 5: Commit**

```bash
git -C ~/GamesMaster-Unity add Assets/GamesMaster/Exterior Assets/Editor/GmMansion.cs Assets/Editor/GmEstateBuilderV2.cs 2>/dev/null || true
git add docs/superpowers/plans/2026-07-17-prologue-intro-scene.md
git commit -m "feat(unity): place gravyart mansion at authored transform, seal doors, warm windows"
```

> Note: `~/GamesMaster-Unity` may not be a git repo. If it is not, skip the Unity-side add; the plan
> and scripts still commit in the web repo. Do not `git init` it without Nick's word.

---

## Task 3: Add the three hero placements to the design data

The mansion is placed by `GmMansion` (its transform is model-specific). The gate and car go through the
normal placement path so they stay data-driven like the other 15.

**Files:**
- Modify: `unity/design-data/prologue-design.json`
- Modify: `Assets/StreamingAssets/prologue-design.json` (**the same edit — these must not diverge**)

- [ ] **Step 1: Add the two placements**

Append to the `placements` array in **both** files. Coordinates come from the authored world constants
(`gateZ: 65`, `carZ: 78`) and the car POI at `(-4.8, 76.5)`:

```json
    { "kind": "estateGate",  "asset": "graveyard_gate", "x": 0,    "z": 65,   "y": 0, "targetHeight": 4.2, "rotationY": 0, "leanZ": 0 },
    { "kind": "estateCar",   "asset": "RealisticCar03_HD_Exterior_LOD0", "x": -4.8, "z": 76.5, "y": 0, "targetHeight": 1.45, "rotationY": 2.618, "leanZ": 0 }
```

`rotationY` is radians (the existing entries are; `GmEstateBuilderV2` multiplies by `Mathf.Rad2Deg`).
`2.618` ≈ 150°, parking the car angled across the verge rather than square to the road — it was
abandoned, not parked. `targetHeight` 1.45 is a sedan's roofline; 4.2 is a gate a man walks under.

- [ ] **Step 2: Verify both copies are identical**

```bash
diff unity/design-data/prologue-design.json ~/GamesMaster-Unity/Assets/StreamingAssets/prologue-design.json && echo "✓ identical"
```

Expected: `✓ identical`. If they differ, the web build and Unity are now telling different stories.

- [ ] **Step 3: Commit**

```bash
git add unity/design-data/prologue-design.json
git commit -m "feat(design-data): add gate and car placements at authored world coordinates"
```

---

## Task 4: Import the gate and car meshes

Both assets are already owned. The estate builder's `FindAssetPrefab()` searches the whole
AssetDatabase, so they only need to exist under `Assets/`.

**Files:**
- Create (copy): `Assets/GamesMaster/Props/graveyard_gate.fbx`
- Create (copy): `Assets/GamesMaster/Props/RealisticCar03_HD_Exterior_LOD0.fbx`

- [ ] **Step 1: Convert both to FBX**

```bash
for m in \
  "assets/models/sourced/graveyard_gate.glb:assets/models/exterior/fbx/graveyard_gate.fbx" \
  "assets/models/unity/realistic-car-hd-03/RealisticCars_HD/RealisticCar03_HD/Meshes/RealisticCar03_HD_Exterior_LOD0.glb:assets/models/exterior/fbx/RealisticCar03_HD_Exterior_LOD0.fbx"
do
  IFS=: read -r src dst <<< "$m"
  /Applications/Blender.app/Contents/MacOS/Blender --background \
    --python scripts/gltf-to-fbx-blender.py -- "$src" "$dst" 2>&1 | grep -E 'BBOX|WROTE|Error'
done
```

Expected: a BBOX + WROTE line for each. The car's BBOX should read roughly 4.5 long × 1.8 wide × 1.4
tall. The gate should be roughly 4 wide × 4 tall. If either is off by 10x, the source glb is in
centimetres — fix it with `apply_unit_scale` rather than fudging `targetHeight`, because
`PlaceScaled` normalises by height and a 10x error will silently look "fine" while the mesh detail is
wrong for its size.

- [ ] **Step 2: Copy into Unity**

```bash
mkdir -p ~/GamesMaster-Unity/Assets/GamesMaster/Props
cp assets/models/exterior/fbx/graveyard_gate.fbx ~/GamesMaster-Unity/Assets/GamesMaster/Props/
cp assets/models/exterior/fbx/RealisticCar03_HD_Exterior_LOD0.fbx ~/GamesMaster-Unity/Assets/GamesMaster/Props/
```

- [ ] **Step 3: Rebuild and confirm they placed**

```bash
node scripts/unity-cli.mjs rebuild 2>&1 | grep -E '✓|✗'
grep -E '\[GmV2\] placements' ~/GamesMaster-Unity/Logs/cli-rebuild.log
```

Expected: `placements: 17 placed, 0 missing` (15 + gate + car). **If it says 15 placed, 2 missing, the
FBX names do not match the `asset` fields** — fix the names, do not fix the data to match a typo.

- [ ] **Step 4: Check for magenta**

```bash
grep -E 'EXPECT MAGENTA' ~/GamesMaster-Unity/Logs/cli-rebuild.log
```

These come from outside the Leartes packs, so they will not have HDRP variants and will trip the
built-in-shader fallback. Expected: warnings for `graveyard_gate` and the car. Task 10 converts them.
A magenta gate at the point of no return is not shippable, but it is also not a blocker for seeing the
scene compose.

- [ ] **Step 5: Commit**

```bash
git add assets/models/exterior/fbx/
git commit -m "feat(unity): convert and import gate + car meshes"
```

---

## Task 5: Port the secret ending

Story bible §7: a **seventh state, outside the six**. Retreating to the car before ever passing the
gate ends the game immediately. It leaves no trace — no portrait, no ledger line, nothing written to
shared state. That absence is the point: the invitation was never fate, so declining costs nothing and
changes nothing for the house, which will simply find someone else.

The web build ships this as `triggerSecretEnding()`. It is not ported to Unity.

**Files:**
- Create: `Assets/Scripts/GmSecretEnding.cs`
- Test: `Assets/Tests/EditMode/GmEstateBuildTests.cs` (added in Task 10)

- [ ] **Step 1: Read the web build's trigger so the port matches**

```bash
grep -n -A14 'triggerSecretEnding' "The Games Master - Prologue.dc.html" | head -24
```

Match its exact trigger condition and copy. Do not invent new prose for a shipped state.

- [ ] **Step 2: Write the runtime**

```csharp
// Assets/Scripts/GmSecretEnding.cs
// The seventh state (story bible §7): reach the car again before ever passing the gate and you
// leave. Not one of the six endings -- those are all reached by sitting at the table. This one is
// reached by never sitting down, and it deliberately writes nothing anywhere.
using UnityEngine;

public class GmSecretEnding : MonoBehaviour
{
    public float carZ = 78f, carX = -4.8f, gateZ = 65f, triggerRadius = 3.2f;
    GmPlayer player;
    GmDesignRuntime rt;
    bool passedGate, fired;

    void Start()
    {
        player = FindFirstObjectByType<GmPlayer>();
        rt = FindFirstObjectByType<GmDesignRuntime>();
    }

    void Update()
    {
        if (player == null || fired) return;
        var p = player.transform.position;

        // Once you are past the gate this exit is closed for good -- that is what the gate lock means.
        if (p.z < gateZ) { passedGate = true; return; }
        if (passedGate) return;

        if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(carX, carZ)) > triggerRadius) return;

        fired = true;
        player.enabled = false;
        rt?.ShowAftermath("I got back in the car. The house kept its light on for somebody else.");
        Debug.Log("[GmSecretEnding] fired — left before the gate, nothing written");
    }
}
```

- [ ] **Step 3: Attach it in the builder**

In `GmEstateBuilderV2.BuildPlayerAndSystems`, beside the other systems:

```csharp
        systems.AddComponent<GmRareEvents>();
        systems.AddComponent<GmSecretEnding>();
        systems.AddComponent<GmShotTour>();
```

- [ ] **Step 4: Rebuild and confirm it compiles**

```bash
node scripts/unity-cli.mjs rebuild 2>&1 | grep -E '✓|✗'
```

Expected: `✓ done: rebuild`. A compile error surfaces as `error CS…` and the script fails loudly.

- [ ] **Step 5: Commit**

```bash
git commit -am "feat(unity): port the secret ending (leave before the gate)"
```

---

## Task 6: Load and play the cold open

Four authored lines sit unused in the design data. They establish everything the walk pays off: the
debt, the daughter, the dead wife, the invitation. Without them the beats at z=38 ("Forty-one
thousand") and z=12 ("The rest is for Nora") arrive with no setup.

The lines, verbatim from `coldOpen`:

1. *"Three calls before sunrise. Men like that don't leave messages."*
2. *"The hospital wants an answer by Friday. The only answer is money — the one thing I've never learned how to keep."*
3. *"Her side of the bed is still made. Two years, and I still sleep on top of the covers, like a guest."*
4. *"Then it was there — slid under the door sometime before dawn. No stamp, no sender. Only a crest in red wax: a stag, one antler snapped."*

Line 4 is load-bearing: the stag crest is the same crest as the broken-antler monument POI at
`(18, 20)` — *"FOR THE HOUSE, FROM ITS WINNERS."* A player who sees the cold open and then reads that
plinth has the whole mythology without a word of exposition.

**Files:**
- Create: `Assets/Scripts/GmColdOpen.cs`
- Modify: `Assets/Scripts/GmDesignRuntime.cs`

- [ ] **Step 1: Load `coldOpen` in the runtime**

`JsonUtility` cannot read a bare `string[]` at the root of a wrapper it does not know about, but it
handles it fine as a field. In `GmDesignRuntime`, extend the private DTO and expose the result:

```csharp
    [Serializable] class PoiList { public Poi[] pois; public Beat[] beats; public string[] coldOpen; }

    public List<string> coldOpen = new List<string>();
```

And inside `Start()`, beside the existing `pois`/`beats` hydration:

```csharp
        if (wrapped != null)
        {
            if (wrapped.pois != null) pois.AddRange(wrapped.pois);
            if (wrapped.beats != null) beats.AddRange(wrapped.beats);
            if (wrapped.coldOpen != null) coldOpen.AddRange(wrapped.coldOpen);
        }
```

Update the existing log line so a silent load failure is visible:

```csharp
        Debug.Log($"[GmDesignRuntime] pois={pois.Count} beats={beats.Count} rects={walkRects.Count} coldOpen={coldOpen.Count}");
```

- [ ] **Step 2: Write the cold-open player**

```csharp
// Assets/Scripts/GmColdOpen.cs
// The four cards before the walk: the debt, the daughter, the wife, the letter. Holds the player
// still until they finish -- the walk's beats have no setup without them. Skippable, because a
// player on their second run should not be made to sit through it; the pacing fix in the web build
// learned that the hard way.
using UnityEngine;
using UnityEngine.InputSystem;

public class GmColdOpen : MonoBehaviour
{
    public float cardSeconds = 5.2f;
    GmDesignRuntime rt;
    GmPlayer player;
    int index = -1;
    float next;
    bool done;

    void Start()
    {
        rt = FindFirstObjectByType<GmDesignRuntime>();
        player = FindFirstObjectByType<GmPlayer>();
        if (rt == null || rt.coldOpen.Count == 0) { done = true; return; }
        if (player != null) player.enabled = false;   // no walking during the cold open
        Advance();
    }

    void Update()
    {
        if (done) return;
        bool skip = Keyboard.current != null &&
                    (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame);
        if (skip || Time.time >= next) Advance();
    }

    void Advance()
    {
        index++;
        if (index >= rt.coldOpen.Count) { Finish(); return; }
        rt.ShowAftermath(rt.coldOpen[index]);   // reuses the centred card presentation
        next = Time.time + cardSeconds;
    }

    void Finish()
    {
        done = true;
        rt.ShowAftermath("");
        if (player != null) player.enabled = true;
        Debug.Log("[GmColdOpen] complete — player released");
    }
}
```

- [ ] **Step 3: Attach it in the builder**

In `GmEstateBuilderV2.BuildPlayerAndSystems`, **before** `GmSecretEnding` so it runs first:

```csharp
        systems.AddComponent<GmColdOpen>();
        systems.AddComponent<GmSecretEnding>();
```

- [ ] **Step 4: Rebuild and confirm the data loads**

```bash
node scripts/unity-cli.mjs rebuild 2>&1 | grep -E '✓|✗'
node scripts/unity-cli.mjs tour 2>&1 | grep -E '✓|✗'
grep -E '\[GmDesignRuntime\]|\[GmColdOpen\]' ~/GamesMaster-Unity/Logs/cli-tour.log
```

Expected: `pois=13 beats=5 rects=8 coldOpen=4`. **If `coldOpen=0`, the JsonUtility field name does not
match the JSON key** — fix the DTO, not the data.

Note: the shot tour teleports the player and will run straight through the cold open. That is fine;
this step verifies the data loads and the sequence completes, not how it looks.

- [ ] **Step 5: Commit**

```bash
git commit -am "feat(unity): load and play the four cold-open cards"
```

---

## Task 7: Enforce the walk rectangles

`GmDesignRuntime` parses 8 walk rects and never uses them. The player can walk off the drive into open
void. The web build clamps to these exact rects, which is why its cemetery and garden read as *places*
rather than as open field.

The rects, `[xMin, xMax, zMin, zMax]`, straight from the design data:

```
[-3.0,  3.0, -36.5, 103.0]   the drive itself, spawn to arrival
[ 3.0, 13.2,  26.3,  30.7]   east side path (sidePathEastZ = 28.5)
[ 8.6, 24.6,  12.6,  40.6]   the cemetery
[-13.2, -3.0, 19.8,  24.2]   west side path (sidePathWestZ = 22.0)
[-23.6, -8.6, 15.6,  34.6]   the ruined garden
[24.6, 38.5,  26.0,  34.0]   chapel forecourt (through the lychgate)
[-26.0,-21.0, 34.0,  41.0]   the west flank path north
[-33.5,-23.0, 40.0,  56.0]   the coach-house yard
```

**Files:**
- Modify: `Assets/Editor/GmEstateBuilderV2.cs`

- [ ] **Step 1: Build invisible bounding walls from the rects**

Rather than clamp in `GmPlayer` (which fights the CharacterController and feels like glue), fence the
union with colliders. Add to `GmEstateBuilderV2`:

```csharp
    /// The 8 walk rects are the level's real shape. Unfenced, the estate is an open field with props
    /// on it and the cemetery stops being a place you enter. Colliders rather than a position clamp:
    /// a clamp fights CharacterController and reads as glue underfoot.
    static void BuildWalkBounds(GmEstateBuilder.Design d)
    {
        var rects = ParseWalkRects();
        if (rects.Count == 0) { Debug.LogError("[GmV2] FAILED: 0 walk rects parsed — the estate would be unbounded"); return; }
        var parent = new GameObject("WalkBounds").transform;
        int walls = 0;
        foreach (var r in rects)
        {
            float x0 = r[0], x1 = r[1], z0 = r[2], z1 = r[3];
            // One thin box per edge, 3u tall. Overlapping rects leave their shared edges walled, which
            // is wrong -- so only wall an edge if its midpoint is not inside some OTHER rect.
            AddEdge(parent, new Vector3((x0 + x1) / 2, 1.5f, z1), new Vector3(x1 - x0, 3f, 0.2f), rects, ref walls);
            AddEdge(parent, new Vector3((x0 + x1) / 2, 1.5f, z0), new Vector3(x1 - x0, 3f, 0.2f), rects, ref walls);
            AddEdge(parent, new Vector3(x1, 1.5f, (z0 + z1) / 2), new Vector3(0.2f, 3f, z1 - z0), rects, ref walls);
            AddEdge(parent, new Vector3(x0, 1.5f, (z0 + z1) / 2), new Vector3(0.2f, 3f, z1 - z0), rects, ref walls);
        }
        Debug.Log($"[GmV2] walk bounds: {walls} wall(s) from {rects.Count} rects");
    }

    static void AddEdge(Transform parent, Vector3 pos, Vector3 size, List<float[]> rects, ref int walls)
    {
        foreach (var r in rects)
            if (pos.x > r[0] + 0.3f && pos.x < r[1] - 0.3f && pos.z > r[2] + 0.3f && pos.z < r[3] - 0.3f)
                return;   // this edge opens into another rect: it is a doorway, not a wall
        var go = new GameObject("bound");
        go.transform.SetParent(parent);
        go.transform.position = pos;
        var bc = go.AddComponent<BoxCollider>();
        bc.size = size;
        walls++;
    }

    static List<float[]> ParseWalkRects()
    {
        string json = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "prologue-design.json"));
        var outp = new List<float[]>();
        foreach (Match m in Regex.Matches(json, "\\[\\s*(-?[\\d.]+),\\s*(-?[\\d.]+),\\s*(-?[\\d.]+),\\s*(-?[\\d.]+)\\s*\\]"))
        {
            var r = new float[4];
            for (int i = 0; i < 4; i++) r[i] = float.Parse(m.Groups[i + 1].Value, System.Globalization.CultureInfo.InvariantCulture);
            if (Mathf.Abs(r[3] - r[2]) > 2f && Mathf.Abs(r[1] - r[0]) > 2f) outp.Add(r);
        }
        return outp;
    }
```

Add `using System.Text.RegularExpressions;` to the file's usings if absent.

- [ ] **Step 2: Call it from `Build()`**

```csharp
        BuildPlacements(d);
        BuildWalkBounds(d);
        GmMansion.Build(d.world.mansionZ, new GameObject("Mansion").transform);
```

- [ ] **Step 3: Rebuild and check the count**

```bash
node scripts/unity-cli.mjs rebuild 2>&1 | grep -E '✓|✗'
grep -E '\[GmV2\] walk bounds' ~/GamesMaster-Unity/Logs/cli-rebuild.log
```

Expected: `walk bounds: N wall(s) from 8 rects`, with N between 20 and 32. **Exactly 32 means no
doorways were detected and the side paths are sealed** — the player will be stuck on the drive. Walk
it and confirm you can still enter the cemetery and the garden.

- [ ] **Step 4: Commit**

```bash
git commit -am "feat(unity): enforce the 8 walk rects with bounding colliders"
```

---

## Task 8: Porch sconces, drive lamps, and the window figure

`GmRareEvents` says it plainly: *"hooks for the window figure + dying lamp (armed once the mansion +
drive lamps exist here)."* The mansion now exists.

Web build reference: `buildPorchSconces(scene, basic, mansionZ + mansionScale * 5.9)` — so the porch
light plane sits at `doorWallZ` ≈ **-52.1**, the same plane the figure glow uses (`doorWallZ + 0.03`)
and the figure itself (`-2.86, 10.5, doorWallZ + 0.06`). That y=10.5 is an **upper** window: the
figure is two storeys up, watching you walk in.

Canon for the figure, from `NICK-NEEDED.md` §2b: ~1 walk in 3 an upper window is lit with a figure in
it, and it is **gone under z≈18**. Forced with the dev console flag `__GMC._figureForce = true`.

**Files:**
- Modify: `Assets/Editor/GmEstateBuilderV2.cs` (lamps)
- Modify: `Assets/Scripts/GmRareEvents.cs` (arm the figure)

- [ ] **Step 1: Read the web build's sconce + figure code before porting**

```bash
grep -n -A18 'buildPorchSconces' "The Games Master - Prologue.dc.html" | head -26
grep -n -A12 '_figureForce' "The Games Master - Prologue.dc.html" | head -20
```

Port the placements and the probability, not an approximation of them. The one-in-three and the z≈18
cutoff are tuned numbers.

- [ ] **Step 2: Add porch sconces to the builder**

Use the owned `SM_Lantern` (already placed elsewhere via the estate data, so `FindAssetPrefab` resolves
it to its HDRP variant). Two sconces flanking the door plane at `doorWallZ = mansionZ + 5.9`:

```csharp
    /// "Lit up like a birthday" needs a source. Porch sconces also give the KO beat something to
    /// silhouette against when the player is held at settleZ = -49.6.
    static void BuildPorchLights(float mansionZ)
    {
        float doorWallZ = mansionZ + 5.9f;
        var parent = new GameObject("PorchLights").transform;
        foreach (float x in new[] { -2.6f, 2.6f })
        {
            var go = new GameObject($"sconce{x}");
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(x, 3.1f, doorWallZ + 0.15f);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.72f, 0.36f);
            l.range = 9f;
            l.shadows = LightShadows.Soft;
            var hd = go.AddComponent<HDAdditionalLightData>();
            hd.affectsVolumetric = true;
            // Lux-scaled to the 1.7-lux moon: a sconce must burn against the night, not match it.
            l.lightUnit = LightUnit.Lumen;
            l.intensity = 600f;
        }
        Debug.Log($"[GmV2] porch lights at doorWallZ={doorWallZ}");
    }
```

Call it from `Build()` right after `GmMansion.Build(...)`.

- [ ] **Step 3: Rebuild, tour, and look at the porch**

```bash
node scripts/unity-cli.mjs rebuild 2>&1 | grep -E '✓|✗'
node scripts/unity-cli.mjs tour 2>&1 | tail -16
```

Open `Screens/WendHill/tour-12-porch.png` and look. The sconces should read as two warm sources on a
dark facade, not as a bleached wall. If the porch is blown out, drop the lumens; do **not** raise
`NightExposureEV` to compensate — that would darken the whole estate to fix one prop.

- [ ] **Step 4: Commit**

```bash
git commit -am "feat(unity): porch sconces at the door wall plane"
```

---

## Task 9: Fix the remaining magenta

Three assets will render magenta: `SM_House_02` (the coach house — no HDRP variant exists in the
Leartes pack), plus the newly imported gate and car (they come from outside the packs entirely).
Retargeting cannot fix any of them, because there is nothing to retarget *to*. This is the route-2
work that route 1 structurally could not do.

**Files:**
- Modify: materials under `Assets/GamesMaster/Props/` and the `SM_House_02` material

- [ ] **Step 1: List exactly what is still built-in**

```bash
grep -E 'EXPECT MAGENTA' ~/GamesMaster-Unity/Logs/cli-rebuild.log
```

Expected: `SM_House_02`, `graveyard_gate`, `RealisticCar03_HD_Exterior_LOD0`. Work from this list, not
from memory.

- [ ] **Step 2: Convert them with Unity's own converter**

Open the editor and run **Window → Rendering → Render Pipeline Converter**, select *Built-in to HDRP*,
and scope it to **Materials only** on those three assets. Do not run it project-wide: WitchVillage has
141 built-in materials whose HDRP twins already exist, and converting them would create duplicates that
diverge from the pack.

Nick's editor must be closed to run `unity-cli.mjs` afterward. Tell him when you are in and out.

- [ ] **Step 3: Verify no magenta survives**

```bash
node scripts/unity-cli.mjs rebuild 2>&1 | grep -E '✓|✗'
grep -cE 'EXPECT MAGENTA' ~/GamesMaster-Unity/Logs/cli-rebuild.log
```

Expected: `0`.

- [ ] **Step 4: Look, do not just count**

```bash
node scripts/unity-cli.mjs tour 2>&1 | tail -16
```

Read `tour-02-car.png` (the car), `tour-03-gate.png` (the gate) and `tour-11-coach-yard.png` by eye. A
converted material can be non-magenta and still wrong: flat, unlit, or plastic. The count only proves
the shader resolved.

- [ ] **Step 5: Commit**

```bash
git commit -am "fix(unity): convert the three non-Leartes materials to HDRP"
```

---

## Task 10: Lock both root causes behind tests

Both bugs that ate this session were invisible: the estate *built successfully* and *looked plausible*
while being wrong. Logs said `15 placed, 0 missing` and `TOUR COMPLETE`. Neither bug is caught by
anything currently in the suite. These tests are the point of the whole plan.

**Files:**
- Create: `Assets/Tests/EditMode/GmEstateBuildTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// Assets/Tests/EditMode/GmEstateBuildTests.cs
// Builds the real estate and asserts against the design data. Every test here exists because the
// bug it catches already shipped once and was invisible: the scene built, the log said 0 missing,
// and it rendered a magenta daylight field.
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

public class GmEstateBuildTests
{
    [OneTimeSetUp]
    public void BuildOnce() => GmEstateBuilderV2.Build();

    static GameObject Find(string namePart)
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go.name.Contains(namePart)) return go;
        return null;
    }

    /// Root cause 1. The Leartes packs ship built-in twins of every HDRP asset; a built-in material
    /// renders magenta under HDRP. This failed silently for the entire estate.
    [Test]
    public void NoRendererUsesABuiltinShader()
    {
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            foreach (var m in r.sharedMaterials)
            {
                if (m == null || m.shader == null) continue;
                var path = AssetDatabase.GetAssetPath(m.shader);
                bool builtin = string.IsNullOrEmpty(path) || path.StartsWith("Resources/") || path.StartsWith("Library/");
                Assert.IsFalse(builtin, $"{r.gameObject.name} uses built-in shader '{m.shader.name}' — renders magenta under HDRP");
            }
        }
    }

    /// Root cause 2. VolumeProfile.Add<T>() is memory-only; without AddObjectToAsset the profile
    /// reloads as components:[] and HDRP silently falls back to default sky + AUTOMATIC exposure,
    /// which re-brightens authored darkness and makes every lighting value a no-op.
    [Test]
    public void NightVolumeProfilePersistsItsOverrides()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Scenes/WendHillNight.asset");
        Assert.IsNotNull(profile, "night volume profile asset missing");
        Assert.Greater(profile.components.Count, 0, "profile persisted 0 overrides — HDRP will fall back to default sky + auto exposure");
        Assert.IsTrue(profile.Has<UnityEngine.Rendering.HighDefinition.Exposure>(), "no Exposure override — auto-exposure will re-brighten the night");
    }

    /// Fixed exposure is deliberate (see GmEstateBuilderV2 header). Automatic would undo the dark.
    [Test]
    public void ExposureIsFixedNotAutomatic()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Scenes/WendHillNight.asset");
        Assert.IsTrue(profile.TryGet(out UnityEngine.Rendering.HighDefinition.Exposure e), "no Exposure override");
        Assert.AreEqual((int)UnityEngine.Rendering.HighDefinition.ExposureMode.Fixed, e.mode.value,
            "exposure is not Fixed — automatic re-brightens authored darkness");
    }

    /// The Prologue is walking toward a lit house. Without it there is no opening to judge.
    [Test]
    public void MansionExistsAtAuthoredDepth()
    {
        var m = Find("Mansion (gravyart");
        Assert.IsNotNull(m, "no mansion in the built scene");
        Assert.AreEqual(-58f, m.transform.position.z, 0.01f, "mansion is not at the authored mansionZ");
    }

    /// Threshold Refusal is locked canon: the doors never open. Cube043 is the model's door slab.
    [Test]
    public void MansionDoorSlabIsHidden()
    {
        var m = Find("Mansion (gravyart");
        Assert.IsNotNull(m);
        foreach (var t in m.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != "Cube043" && !t.name.StartsWith("Cube043_")) continue;
            var r = t.GetComponent<Renderer>();
            Assert.IsFalse(r != null && r.enabled && t.gameObject.activeInHierarchy,
                "the sealed door slab is visible — Threshold Refusal is broken");
        }
    }

    /// The gate is the point of no return and GmThreshold already locks it. It must be visible.
    [Test]
    public void GateAndCarArePlacedAtAuthoredCoordinates()
    {
        var gate = Find("estateGate");
        var car = Find("estateCar");
        Assert.IsNotNull(gate, "no gate — GmThreshold locks a gate the player cannot see");
        Assert.IsNotNull(car, "no car — spawn reads as an empty road and the secret ending has no exit");
        Assert.AreEqual(65f, gate.transform.position.z, 1.5f, "gate is not at gateZ");
        Assert.AreEqual(76.5f, car.transform.position.z, 1.5f, "car is not at its POI");
    }

    /// The estate is unbounded without these; the cemetery stops being a place you enter.
    [Test]
    public void WalkBoundsExist()
    {
        var bounds = Find("WalkBounds");
        Assert.IsNotNull(bounds, "no walk bounds — the player can walk into the void");
        Assert.Greater(bounds.GetComponentsInChildren<BoxCollider>().Length, 15, "suspiciously few bound walls");
    }

    /// The design data is the shared contract. If Unity's copy drifts from the web build's, the two
    /// tell different stories from the same file name.
    [Test]
    public void DesignDataHasEveryAuthoredElement()
    {
        var json = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "prologue-design.json"));
        StringAssert.Contains("\"coldOpen\"", json);
        StringAssert.Contains("estateGate", json);
        StringAssert.Contains("estateCar", json);
    }
}
```

- [ ] **Step 2: Run them and watch them fail first**

```bash
node scripts/unity-cli.mjs test 2>&1 | tail -12
```

Before Tasks 1-8 land, expect failures naming the mansion, gate, car and walk bounds. **A test that
has never failed has never been shown to test anything.** If one of these passes before its feature
exists, it is asserting nothing — fix the test.

- [ ] **Step 3: Run them after the features land**

```bash
node scripts/unity-cli.mjs test 2>&1 | tail -12
```

Expected: `EditMode tests: 31/31 passed, 0 failed` (23 Shut the Box + 8 estate — the Step 1 code block
above has 8 `[Test]` methods, not 9; corrected here after a real count so the next person running this
isn't surprised).

- [ ] **Step 4: Prove the root-cause tests actually bite**

Temporarily revert `AddOverride<T>` to `profile.Add<T>(true)` in `GmEstateBuilderV2`, rebuild, run the
tests. `NightVolumeProfilePersistsItsOverrides` must fail. Put it back. Do the same for `PreferHdrp` if
you want the same confidence on the magenta test. This costs four minutes and is the only way to know
the tests are real rather than decorative.

- [ ] **Step 5: Commit**

```bash
git commit -am "test(unity): lock the magenta and empty-volume-profile root causes behind EditMode tests"
```

---

## Task 11: Fix the editor-only audio lookup

`GmAmbience.Find()` is wrapped in `#if UNITY_EDITOR` and resolves clips via `AssetDatabase`. In a built
player it returns null and **every sound in the game goes silent** — the gate slam, the porch breath,
the KO thud. It works in play mode, so nobody has noticed. It is not a blocker for Nick's walk, but it
is a shipped-build bug sitting in the dark.

**Files:**
- Modify: `Assets/Scripts/GmAmbience.cs`
- Move: `Assets/Audio/Sfx/*.ogg` → `Assets/Resources/Sfx/*.ogg`

- [ ] **Step 1: Move the clips into Resources**

```bash
mkdir -p ~/GamesMaster-Unity/Assets/Resources/Sfx
mv ~/GamesMaster-Unity/Assets/Audio/Sfx/*.ogg ~/GamesMaster-Unity/Assets/Resources/Sfx/
mv ~/GamesMaster-Unity/Assets/Audio/Sfx/*.ogg.meta ~/GamesMaster-Unity/Assets/Resources/Sfx/ 2>/dev/null || true
```

Moving the `.meta` files with them preserves the GUIDs, so nothing that already references a clip
breaks.

- [ ] **Step 2: Replace the lookup**

```csharp
    // Resources.Load works in editor AND in a build. The previous AssetDatabase lookup was inside
    // #if UNITY_EDITOR, so a built player silently lost every sound in the game.
    static AudioClip Find(string name) => Resources.Load<AudioClip>($"Sfx/{name}");
```

Delete the `#if UNITY_EDITOR` block it replaces.

- [ ] **Step 3: Verify all 14 clips still resolve**

```bash
node scripts/unity-cli.mjs rebuild 2>&1 | grep -E '✓|✗'
node scripts/unity-cli.mjs tour 2>&1 | grep -E '✓|✗'
grep -iE 'clip|audio' ~/GamesMaster-Unity/Logs/cli-tour.log | grep -i 'null\|missing\|fail' | head
```

Expected: no null/missing clip errors. Walking it is the real check: the gate must still slam.

- [ ] **Step 4: Commit**

```bash
git commit -am "fix(unity): resolve SFX via Resources so audio survives a build"
```

---

## Final verification — before Nick walks

Run all of it. Every claim below must be observed, not assumed.

- [ ] **Step 1: Full suite**

```bash
npm run test:all          # C# parity 23 + JS logic 23 + browser harness 47
node scripts/unity-cli.mjs test    # EditMode: expect 31/31 (23 Shut the Box + 8 estate)
```

- [ ] **Step 2: Fresh build and tour**

```bash
node scripts/unity-cli.mjs setup && node scripts/unity-cli.mjs tour 2>&1 | tail -18
```

Expected: `17 placed, 0 missing` · `0` EXPECT MAGENTA · `4 overrides persisted` · 12/12 shots,
meanLum in the 20-70 band with real variance between them.

- [ ] **Step 3: Read every shot by eye**

All twelve. Not a sample. The luminance gate proves pixels exist; it cannot tell you the house looks
right. Specifically judge: the house is visible from spawn and reads occupied · the gate is a real
threshold · the car reads abandoned, not parked · nothing is magenta · the night is dark but navigable.

- [ ] **Step 4: Fix what the shots show, then re-tour**

Iterate until the shots hold up. This is the "reviewed, then fixed" step — do not hand Nick a walk that
the screenshots already told you was broken.

- [ ] **Step 5: Update the docs and hand off**

Refresh `docs/PROGRESS.md`, `docs/SESSION-STATE-2026-07-17.md`, and `docs/NICK-NEEDED.md` with what
actually shipped and what is still open. Then, and only then, Nick walks.

---

## Deliberately out of scope

Named so nobody thinks they were forgotten:

- **Real UI.** `GmDesignRuntime.OnGUI` is a placeholder. The letter/menu/cold-open UGUI port is its own
  plan. The cold open ships on the placeholder card presentation here.
- **Entry Hall.** The KO fades to black and stops. Waking on the marble is the next scene's job.
- **Dying drive lamps.** The `GmRareEvents` hook stays unarmed until drive lamps exist; porch sconces
  only in this plan.
- **The Showcase visual baseline.** `GmShotTour`'s 12 waypoints are Wend Hill coordinates and are
  meaningless in another scene. The agent panel needs its own waypoint set first.
- **`NightExposureEV = -3`.** An agent's number. Nick's call after the walk, not a task.

## Self-review

**Spec coverage.** Every gap in the state audit maps to a task: mansion 1-2, data 3, gate+car 4,
secret ending 5, cold open 6, walk rects 7, lamps+figure 8, magenta 9, tests 10, audio 11. The two
root causes from this session both get regression tests in Task 10.

**Placeholder scan.** No TBDs. Every code step carries the actual code; every command carries its
expected output and what a wrong answer means.

**Type consistency.** `GmMansion.Build(float, Transform)` is called with `d.world.mansionZ` in Task 2
and asserted at `-58` in Task 10. `GmDesignRuntime.coldOpen` is `List<string>`, added in Task 6 and
read by `GmColdOpen` in the same task. `GmSecretEnding` fields (`carZ`, `carX`, `gateZ`) match the
design data's `world` constants. `AddOverride<T>` is the existing helper, referenced in Task 10's
revert check.

**One known gap, stated rather than hidden:** Task 8 ports the window figure's *lights* but leaves the
figure itself to the web build's probability code, which the task says to read first rather than
approximate. If that code turns out to depend on Three-specific plumbing, the figure becomes its own
task rather than getting faked here.
