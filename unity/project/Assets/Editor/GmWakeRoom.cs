// Assets/Editor/GmWakeRoom.cs
// The wake room -- the crossing's landing spot. He comes to INSIDE as the longcase clock finishes
// its own ninth chime. Phase 1 now continues through a framed opening into the independently built
// HouseBeginning root. This file remains only the small landing vestibule; portraits, ledger, shard,
// Aldric and the first game are owned by GmHouseBeginningBuilder.
//
// Built at WorldZ, far off in +Z, well clear of every estate coordinate (spawnZ=72 down to
// mansionZ=-58 -- see prologue-design.json's "world" block): the estate's review tour can never see
// this room. Its only opening connects directly to the sealed Entry Hall.
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public static class GmWakeRoom
{
    // Isolated on purpose -- see header. Not a magic number: chosen because the estate's own
    // authored world-Z range (mansionZ=-58 .. carZ=78) does not come within 300 units of it.
    const float WorldZ = 400f;

    const float RoomW = 7f, RoomD = 7f, RoomH = 3.2f;
    const float WallT = 0.3f;

    const float TargetClockHeight = 2.1f;   // a real longcase clock: 1.8-2.3m is the normal range

    // Priority order: our one converted-and-imported candidate first (see Task 6's report -- pulled
    // from the owned library via `find assets/models/unity -iname '*clock*'` and run through
    // scripts/gltf-to-fbx-blender.py into Assets/GamesMaster/Props/), then two more owned-library
    // names left as a defensive fallback in case a future pass imports one of them instead. If none
    // resolve, BuildClock() builds a placeholder and says so loudly -- it does not fail silently.
    static readonly string[] ClockCandidates = { "Vintage_Grantfather_Clock", "SM_Clock", "Clock" };

    [MenuItem("GamesMaster/Rebuild Wake Room Only")]
    public static void BuildStandalone() => Build();

    /// Called from GmEstateBuilderV2.Build(). Deliberately NOT parented under anything -- WakePose is
    /// found at runtime via GameObject.Find("WakeRoom/WakePose"; Unity's path-form Find only resolves
    /// starting from a scene ROOT, so "WakeRoom" must stay a root object or that lookup returns null
    /// and GmCrossing logs "the player wakes in the mud" (its own words) instead of teleporting.
    public static void Build()
    {
        var root = new GameObject("WakeRoom").transform;
        root.position = new Vector3(0, 0, WorldZ);

        var stoneWall = new Material(Shader.Find("HDRP/Lit")) { color = new Color(0.10f, 0.10f, 0.11f) };
        var stoneFloor = new Material(Shader.Find("HDRP/Lit")) { color = new Color(0.15f, 0.14f, 0.13f) };

        BuildShell(root, stoneWall, stoneFloor);
        Vector3 clockPos = BuildClock(root);
        BuildLight(root, clockPos);
        BuildWakePose(root, clockPos);

        Debug.Log($"[GmWakeRoom] built at world z={WorldZ}, floor y=0, room {RoomW}x{RoomD}x{RoomH}, clock at {clockPos}");
    }

    // ── shell: the south wall is a framed threshold into the Entry Hall ─────────────────────
    static void BuildShell(Transform root, Material wallMat, Material floorMat)
    {
        float cz = root.position.z;
        float half = RoomW / 2f + WallT;   // walls overrun the interior span so corners have no gap

        Slab("Floor",   new Vector3(0, -WallT / 2f, cz), new Vector3(RoomW + 2 * WallT, WallT, RoomD + 2 * WallT), floorMat, root);
        Slab("Ceiling", new Vector3(0, RoomH + WallT / 2f, cz), new Vector3(RoomW + 2 * WallT, WallT, RoomD + 2 * WallT), wallMat, root);
        Slab("WallNorth", new Vector3(0, RoomH / 2f, cz + RoomD / 2f + WallT / 2f), new Vector3(RoomW + 2 * WallT, RoomH, WallT), wallMat, root);
        float southZ = cz - RoomD / 2f - WallT / 2f;
        const float doorway = 2.4f;
        float side = (RoomW + 2 * WallT - doorway) * 0.5f;
        Slab("WallSouthWest", new Vector3(-(doorway * 0.5f + side * 0.5f), RoomH / 2f, southZ), new Vector3(side, RoomH, WallT), wallMat, root);
        Slab("WallSouthEast", new Vector3(doorway * 0.5f + side * 0.5f, RoomH / 2f, southZ), new Vector3(side, RoomH, WallT), wallMat, root);
        Slab("WallSouthLintel", new Vector3(0, 2.95f, southZ), new Vector3(doorway, 0.5f, WallT), wallMat, root);
        Slab("WallEast",  new Vector3(half - WallT / 2f, RoomH / 2f, cz), new Vector3(WallT, RoomH, RoomD + 2 * WallT), wallMat, root);
        Slab("WallWest",  new Vector3(-(half - WallT / 2f), RoomH / 2f, cz), new Vector3(WallT, RoomH, RoomD + 2 * WallT), wallMat, root);
    }

    static void Slab(string n, Vector3 pos, Vector3 size, Material m, Transform parent)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = n;
        g.transform.SetParent(parent, true);
        g.transform.position = pos;
        g.transform.localScale = size;
        g.GetComponent<MeshRenderer>().sharedMaterial = m;
    }

    // ── one warm light. Nothing else. It is a stub ──────────────────────────────────────────
    static void BuildLight(Transform root, Vector3 clockPos)
    {
        var go = new GameObject("WakeLight");
        go.transform.SetParent(root, true);
        // Pulled back off the clock (0.35 -> 0.22 of the way toward it) -- at 0.35 the case sat close
        // enough to the point light's falloff that it blew fully white while the room read correctly;
        // confirmed by the same screenshot as the intensity note below.
        //
        // Offset +2.0 on X (a side-table-lamp position, not directly over the clock) is a SECOND,
        // separate fix on top of that: WakePose, this Lerp point and clockPos all sit at x=0, i.e.
        // exactly collinear along Z. A light on that line sits almost exactly between the camera and
        // the surface it is lighting, so the camera looks straight down the specular reflection cone
        // back at its own light source -- a retroreflective hotspot, not normal illumination. Moving
        // the light to the side breaks that alignment: light now hits the clock at a real angle, which
        // is also what let ApplyClockFinish's dark wood tone actually read as dark wood instead of
        // blowing to flat cream regardless of albedo. Confirmed by screenshot (see Task 6 report):
        // this single change is what took the clock from "glowing pillar, no visible material" to
        // "case, waist and hood distinguishable, moulding visible, still reads as lit-not-blown".
        go.transform.position = Vector3.Lerp(root.position, clockPos, 0.22f) + Vector3.up * 1.0f + Vector3.right * 2.0f;

        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.72f, 0.36f);   // same warm as GmMansion.WarmTheWindows / porch sconces
        l.range = 9f;
        l.shadows = LightShadows.Soft;
        var hd = go.AddComponent<HDAdditionalLightData>();
        hd.affectsVolumetric = true;
        l.lightUnit = LightUnit.Lumen;
        // 600 lumens was tried first on the theory that a sealed room needs to burn hotter than a
        // 50-lumen porch sconce competing with moonlight outdoors -- WRONG, confirmed by an actual
        // screenshot (tour-13-TEMP-wakeroom.png, meanLum=191 against 14-54 for every outdoor shot):
        // a sealed room has nowhere for light to escape to, so it does the opposite of an open porch
        // and blows the whole box to near-white instead. Cut by 20x and re-verified by screenshot
        // (see Task 6 report) before landing on this value.
        // The original 22-lumen stub was readable in an isolated editor frame but fell below the
        // built-player exposure once the south threshold opened onto the much larger hall. 70 keeps
        // the clock legible in the real player recording without returning to the 600-lumen blowout.
        l.intensity = 70f;
    }

    // ── the clock: owned model preferred, primitive placeholder if none resolves ────────────
    static Vector3 BuildClock(Transform root)
    {
        float cz = root.position.z;
        Vector3 target = new Vector3(0, 0, cz + RoomD / 2f - 0.55f);   // against the north wall, inset for depth

        GameObject prefab = null;
        string foundName = null;
        foreach (var name in ClockCandidates)
        {
            prefab = GmEstateBuilderV2.FindAssetPrefab(name);
            if (prefab != null) { foundName = name; break; }
        }

        GameObject go;
        if (prefab != null)
        {
            go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            go.name = $"LongcaseClock ({foundName})";
            var b = GetBounds(go);
            float scale = b.size.y > 0.01f ? TargetClockHeight / b.size.y : 1f;
            go.transform.localScale = Vector3.one * scale;
            // COMPOSE the extra yaw with whatever rotation the prefab already had at fresh instantiate
            // -- do not overwrite it. Measured empirically (rebuild log): this model's fresh-instantiate
            // orientation already stands correctly Y-up (the first GetBounds() above, taken BEFORE any
            // rotation is touched, is what produced a sane 0.0096 scale factor from a ~219-unit Y
            // extent). A prior version of this line REPLACED rotation with Quaternion.Euler(0,180,0)
            // wholesale, discarding that correct baked orientation and laying the clock on its side --
            // confirmed by the second GetBounds() then reporting height on Z (2.10) instead of Y. Same
            // bug class as the car/gate baked-rotation notes above this file's sibling GmEstateBuilderV2,
            // opposite fix: THIS model's default is already right, so extend it instead of discarding it.
            go.transform.rotation = Quaternion.Euler(0, 180f, 0) * go.transform.rotation;
            b = GetBounds(go);
            go.transform.position += new Vector3(target.x - b.center.x, target.y - b.min.y, target.z - b.center.z);
            b = GetBounds(go);
            int retinted = ApplyClockFinish(go);
            Debug.Log($"[GmWakeRoom] clock: owned model '{foundName}' scaled x{scale:F4} to height {b.size.y:F2}, rendered size {b.size}, retinted {retinted} material slot(s)");
        }
        else
        {
            Debug.LogWarning("[GmWakeRoom] PLACEHOLDER CLOCK -- no owned longcase/grandfather clock model resolved " +
                "via GmEstateBuilderV2.FindAssetPrefab for any of: " + string.Join(", ", ClockCandidates) + ". " +
                "Built from primitives instead. THIS IS NOT A SHIPPABLE CLOCK. Fix by converting one of the owned " +
                "library candidates (e.g. assets/models/unity/fps-horror-game-starter-pack/FpsHorrorKit/Models/" +
                "Furnitures/Vintage_Grantfather_Clock.glb) with scripts/gltf-to-fbx-blender.py and copying the " +
                "result into Assets/GamesMaster/Props/.");
            go = BuildPlaceholderClock(root);
            go.transform.position = new Vector3(target.x, 0, target.z);
        }

        return GetBounds(go).center;
    }

    // Every owned-library clock candidate (all 12, checked by hand against the raw glTF JSON --
    // see Task 6 report) ships with ZERO textures and the same generic default material:
    // baseColorFactor ~0.8 grey, metallicFactor 0.5, roughnessFactor 0.5. That is not a stylised
    // choice, it is an unauthored placeholder value, and at 50% metallic it behaves like brushed
    // steel, not painted/lacquered wood: confirmed by screenshot (tour-13-TEMP-wakeroom.png) --
    // under the room's own point light it read as a uniform cream-white silhouette with NO visible
    // case/hood/waist shading and no trim distinction, i.e. a glowing pillar, not a longcase clock.
    // Retinting is done unconditionally on every real-model instantiate rather than left as a
    // per-model maybe, because the defect is library-wide, not this one asset.
    static int ApplyClockFinish(GameObject go)
    {
        var wood = new Material(Shader.Find("HDRP/Lit")) { color = new Color(0.11f, 0.07f, 0.04f) };
        wood.SetFloat("_Metallic", 0f);
        wood.SetFloat("_Smoothness", 0.32f);   // waxed wood, not a mirror

        var glass = new Material(Shader.Find("HDRP/Lit")) { color = new Color(0.03f, 0.03f, 0.035f) };
        glass.SetFloat("_Metallic", 0f);
        glass.SetFloat("_Smoothness", 0.55f);  // darker + glossier than the case, reads as the face/door

        int touched = 0;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                bool isGlass = mats[i] != null && mats[i].name.IndexOf("glass", System.StringComparison.OrdinalIgnoreCase) >= 0;
                mats[i] = isGlass ? glass : wood;
                touched++;
            }
            r.sharedMaterials = mats;
        }
        return touched;
    }

    static GameObject BuildPlaceholderClock(Transform parent)
    {
        var root = new GameObject("PlaceholderLongcaseClock (NOT A REAL ASSET -- see warning above)");
        root.transform.SetParent(parent, true);

        var wood = new Material(Shader.Find("HDRP/Lit")) { color = new Color(0.16f, 0.10f, 0.05f) };
        var brass = new Material(Shader.Find("HDRP/Lit")) { color = new Color(0.55f, 0.42f, 0.18f) };
        var face = new Material(Shader.Find("HDRP/Lit")) { color = new Color(0.85f, 0.82f, 0.72f) };

        // Case built as three stacked blocks (base / waist / hood), 0.9 + 0.9 + 0.3 = 2.1m -- matches
        // TargetClockHeight exactly so the real-model path and the placeholder path read the same
        // size from the wake pose.
        Box("Case_Base",  new Vector3(0, 0.45f, 0), new Vector3(0.50f, 0.90f, 0.32f), wood, root.transform);
        Box("Case_Waist", new Vector3(0, 1.35f, 0), new Vector3(0.42f, 0.90f, 0.28f), wood, root.transform);
        Box("Case_Hood",  new Vector3(0, 1.95f, 0), new Vector3(0.56f, 0.30f, 0.34f), wood, root.transform);
        Box("Pendulum_Door_Trim", new Vector3(0, 1.35f, 0.145f), new Vector3(0.30f, 0.85f, 0.02f), brass, root.transform);

        var dial = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        dial.name = "Face";
        dial.transform.SetParent(root.transform, true);
        dial.transform.localPosition = new Vector3(0, 1.95f, 0.18f);
        dial.transform.localRotation = Quaternion.Euler(90, 0, 0);
        dial.transform.localScale = new Vector3(0.24f, 0.02f, 0.24f);
        dial.GetComponent<MeshRenderer>().sharedMaterial = face;

        return root;
    }

    static GameObject Box(string n, Vector3 localPos, Vector3 size, Material m, Transform parent)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = n;
        g.transform.SetParent(parent, true);
        g.transform.localPosition = localPos;
        g.transform.localScale = size;
        g.GetComponent<MeshRenderer>().sharedMaterial = m;
        return g;
    }

    // ── WakePose: where GmCrossing teleports the player ─────────────────────────────────────
    //
    // WakePose.transform.position becomes the PLAYER ROOT's position directly (GmCrossing calls
    // player.transform.SetPositionAndRotation(wake.transform.position, wake.transform.rotation) on
    // the Player object itself, not its camera). GmEstateBuilderV2.BuildPlayerAndSystems parents
    // PlayerCamera under Player at localPosition (0, eyeHeight, 0) and spawns the root at
    // (x, 0.1, spawnZ) -- root.y is a FLOOR position, not an eye position, and the camera's own
    // local offset is what supplies eye height on top of it.
    //
    // If WakePose were placed at literal eye height (root.y = ~1.7), the camera would land at
    // root.y + eyeHeight = ~3.4 -- floating near the ceiling -- and would STAY there rather than
    // settling, because GmPlayer (and with it, GmPlayer.Update's CharacterController.SimpleMove,
    // which is the only place gravity gets applied) stays disabled for the ENTIRE crossing sequence,
    // including the full 5-second closing card AFTER the screen is already visible. A literal
    // eye-height WakePose would put a floating first-person camera on screen for those 5 seconds
    // before gravity ever got a chance to run. Matching the 0.1-above-floor spawn convention instead
    // means the camera lands at the correct eye height via the SAME offset every other spawn already
    // relies on -- "at eye height" (the design ask) is satisfied by the camera's existing offset, not
    // by this transform's raw Y.
    static void BuildWakePose(Transform root, Vector3 clockPos)
    {
        var wake = new GameObject("WakePose");
        wake.transform.SetParent(root, true);   // root object is "WakeRoom" itself -- keep it a DIRECT
                                                 // child so GameObject.Find("WakeRoom/WakePose") resolves.

        Vector3 pos = new Vector3(root.position.x, 0.1f, root.position.z - RoomD / 2f + 1.4f);
        wake.transform.position = pos;

        Vector3 toClock = clockPos - pos;
        toClock.y = 0;   // face level, not up at the hood -- a standing pose looks forward, not up
        wake.transform.rotation = toClock.sqrMagnitude > 0.001f ? Quaternion.LookRotation(toClock.normalized, Vector3.up) : Quaternion.identity;
    }

    static Bounds GetBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }
}
