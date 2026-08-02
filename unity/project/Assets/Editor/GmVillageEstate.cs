// M2: drops the estate -- mansion, gate, arrival car and the player's cold-open spawn -- into the
// village, at positions MEASURED by GmVillageSitePlan rather than inherited from the old flat-ground
// estate scene.
//
// Two different placement strategies, on purpose:
//
//   * The MANSION is a rigid authored assembly. GmMansion.Build hardcodes world position
//     (0.52, 1.43, mansionZ) and yaw 90, and GmMansion.BuildWindowFigureRig then seats the figure
//     from `mansion.transform.position.z + 5.9f`. Those numbers only agree with each other in the
//     estate's own coordinate space. So the mansion and its figure rig are built in estate space
//     under one root, and the ROOT is then rotated and translated as a rigid body. Their internal
//     geometry -- porch, door wall, which pane the figure stands behind -- is preserved exactly.
//     (BuildWindowFigureRig creates its root at scene level, not under the mansion, so it has to be
//     re-parented before the move or the figure stays behind at the old coordinates.)
//
//   * The GATE and CAR are single props with no internal relationships to protect, so they are
//     placed directly at measured points on the village road. Doing these rigidly with the mansion
//     would have forced a uniform scale to satisfy both ends of the corridor, and scaling the root
//     would have scaled the mansion itself.
//
// No drive mesh is built. The village already ships a dressed road and the brief is "road -> drive",
// so the existing road IS the drive; laying a second plane over it would only z-fight.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;                    // LightUnit lives here, not in the HDRP namespace
using UnityEngine.Rendering.HighDefinition;

public static class GmVillageEstate
{
    const string LogTag = "GmVillageEstate";
    public const string RootName = "WendHillEstate";

    // Measured by GmVillageSitePlan against the built village scene (village-siteplan.json):
    // principal axis through the seven village buildings, i.e. the road they were dressed along.
    public static readonly Vector2 SpineCentroid = new Vector2(96.08f, -102.15f);
    public static readonly Vector2 SpineAxis = new Vector2(0.26f, 0.97f).normalized;

    // Distances ALONG that spine, +north. Chosen from the clearance profile, not by eye:
    //   t=-86  clearance 30.7m -- open ground south of the village, 49m clear of the church
    //   t=+58  clearance 16.5m -- just past the northernmost building, the village's real entrance
    //   t=+71  clearance 22m   -- open approach north of the gate, keeps the estate's 13m gate/car gap
    const float MansionT = -86f;
    const float GateT = 58f;

    // Derived through the estate->village map rather than hand-picked, so the car and spawn keep
    // exactly the spacing prologue-design.json authored relative to the gate and mansion.
    static float CarT => EstateZToT(EstateCarZ);
    static float SpawnT => EstateZToT(EstateSpawnZ);

    const float EstateMansionZ = -58f;   // world.mansionZ from prologue-design.json
    const float EstateGateZ = 65f;       // world.gateZ
    const float EstateCarZ = 78f;        // world.carZ
    const float EstateSpawnZ = 72f;      // world.spawnZ
    const float EstateArrivalZ = -36f;   // world.arrivalZ -- the porch refusal trigger

    /// Village world-Z values the Z-threshold gameplay systems need, all derived from one map.
    public static float VillageGateZ => EstateZToVillageZ(EstateGateZ);
    public static float VillageArrivalZ => EstateZToVillageZ(EstateArrivalZ);
    public static float VillageCarZ => EstateZToVillageZ(EstateCarZ);
    const float GateTargetHeight = 3.6f;
    const float CarTargetHeight = 1.45f;
    const float CarYawEstateDeg = 165f;  // 2.88 rad, from the design's estateCar placement

    public struct Site
    {
        public Vector3 spawn;
        public Quaternion spawnRotation;
        public Vector3 mansion;
        public Vector3 gate;
        public Vector3 car;
        public bool valid;
    }

    /// Yaw that carries estate +Z (its north, toward the car) onto the village spine's north.
    public static float SpineYawDeg => Mathf.Atan2(SpineAxis.x, SpineAxis.y) * Mathf.Rad2Deg;

    public static Vector2 SpinePoint(float t) => SpineCentroid + SpineAxis * t;

    /// Lateral (cross-road) axis. Estate +x is EAST, which is on the LEFT hand of a walker heading
    /// south down the drive; this vector reproduces that side, so the cemetery stays on the same
    /// side of the road it was authored on and the garden stays opposite it.
    public static Vector2 LateralAxis => new Vector2(SpineAxis.y, -SpineAxis.x);

    // ── estate space -> village space ───────────────────────────────────────────────────────────
    //
    // Every gameplay system in this project is ONE-DIMENSIONAL along world Z: GmThreshold compares
    // player.transform.position.z against gateZ/arrivalZ, GmDesignRuntime fires drive beats on
    // z-crossings, GmSecretEnding watches carZ. That works here without refactoring any of them,
    // because the village road runs 97% along -Z (spine axis z-component 0.97), so world Z stays
    // monotonic from the car to the mansion. The systems are left alone and the DATA is remapped.
    //
    // The map is the straight line through the two placements that were measured from the clearance
    // profile: the gate (estate z 65 -> spine t 58) and the mansion (estate z -58 -> spine t -86).
    // Deriving everything else through it keeps the placements and the gameplay thresholds from
    // drifting apart, which is what would happen if both were hand-typed.
    public static float EstateZToT(float estateZ) =>
        (estateZ - EstateGateZ) * ((MansionT - GateT) / (EstateMansionZ - EstateGateZ)) + GateT;

    public static Vector2 EstateToVillage(float estateX, float estateZ) =>
        SpinePoint(EstateZToT(estateZ)) + LateralAxis * estateX;

    /// World Z a given estate Z lands on. This is what the Z-threshold systems get configured with.
    public static float EstateZToVillageZ(float estateZ) => EstateToVillage(0f, estateZ).y;

    public static Site Build(Terrain terrain)
    {
        if (terrain == null) throw new ArgumentNullException(nameof(terrain));

        GameObject stale = GameObject.Find(RootName);
        if (stale != null) UnityEngine.Object.DestroyImmediate(stale);

        float yaw = SpineYawDeg;
        Quaternion rootRot = Quaternion.Euler(0f, yaw, 0f);

        // Built at identity so every child lands at its authored estate coordinate first.
        var root = new GameObject(RootName);

        GameObject mansion = GmMansion.Build(EstateMansionZ, root.transform);
        if (mansion == null) throw new InvalidOperationException("GmMansion.Build returned null");

        // Take the rig from the return value, NOT from GameObject.Find: GmMansion creates it
        // deactivated ("inactive until armed"), and Find skips inactive objects. Searching for it by
        // name silently returned null and left the figure stranded at estate coordinates while the
        // mansion moved to the village -- the window it is supposed to be standing in would have
        // walked away from it.
        GameObject rig = GmMansion.BuildWindowFigureRig(mansion);
        if (rig != null) rig.transform.SetParent(root.transform, true);
        else Debug.LogWarning($"[{LogTag}] BuildWindowFigureRig returned null; no window figure to re-parent");

        // Relocate the whole assembly so the mansion lands on the measured site. The root's own y
        // becomes the village ground plane, so the mansion's authored foundation lift rides on top
        // of it untouched instead of being re-derived.
        Vector2 mansionXZ = SpinePoint(MansionT);
        float mansionGroundY = GroundAt(terrain, mansionXZ);
        Vector3 local = mansion.transform.position;
        Vector3 rotatedXZ = rootRot * new Vector3(local.x, 0f, local.z);

        root.transform.rotation = rootRot;
        root.transform.position = new Vector3(
            mansionXZ.x - rotatedXZ.x, mansionGroundY, mansionXZ.y - rotatedXZ.z);

        Vector3 mansionWorld = mansion.transform.position;

        // Detach the figure rig back to a SCENE ROOT now that the estate has been moved into place.
        //
        // It had to be parented under the root for the move, or it would have stayed at the old
        // estate coordinates while the mansion walked to the village. But GmRareEvents locates it
        // with FindSceneRoot("WindowFigureRig"), which only searches scene roots, so leaving it as a
        // child made it invisible to the system that owns it: the built app logged "WindowFigureRig
        // missing -- one-in-three figure disabled" and the figure never appeared at all.
        // SetParent(null, true) keeps the world transform it just inherited.
        if (rig != null) rig.transform.SetParent(null, true);

        Vector3 gateWorld = BuildGate(root.transform, terrain);
        GameObject carGo = PlaceProp(root.transform, "RealisticCar03_HD_Exterior_LOD0", "ArrivalCar",
            terrain, SpinePoint(CarT), CarTargetHeight, yaw + CarYawEstateDeg);
        Vector3 carWorld = carGo != null ? carGo.transform.position : Vector3.zero;

        // The gate is dark iron on dark stone. In the old estate scene the whole frame was near-black
        // so it read fine by contrast; in this brighter village it rendered as two flat black
        // cutouts against a lit street. A lantern at the gate is how the estate scene solved the same
        // problem (BuildPracticalLights: "gate, chapel lantern and braziers"), and it doubles as the
        // landmark marking where the estate proper begins.
        MakeGateLanterns(root.transform, SpinePoint(GateT), terrain, yaw);

        int dressed = DressMansionGrounds(root.transform, terrain);
        GmVillagePropertyLights.Result lights =
            GmVillagePropertyLights.Build(root.transform, terrain, mansionWorld);

        Vector2 spawnXZ = SpinePoint(SpawnT);
        var site = new Site
        {
            spawn = new Vector3(spawnXZ.x, GroundAt(terrain, spawnXZ) + 0.1f, spawnXZ.y),
            // Facing SOUTH down the spine: gate, then the village street, then the mansion. That is
            // the order the prologue walks them in, so it is what the cold open should open on.
            spawnRotation = Quaternion.Euler(0f, yaw + 180f, 0f),
            mansion = mansionWorld,
            gate = gateWorld,
            car = carWorld,
            valid = true,
        };

        Debug.Log($"[{LogTag}] PASS: yaw={yaw:0.##} mansion={Fmt(mansionWorld)} gate={Fmt(gateWorld)} " +
                  $"car={Fmt(carWorld)} spawn={Fmt(site.spawn)} groundsProps={dressed} " +
                  $"lampPosts={lights.lampPosts} braziers={lights.braziers} porch={lights.porchLights} " +
                  $"mansionBounds={FmtBounds(mansion)}");
        return site;
    }

    /// Builds two hinged, closable leaves to hang in the gateway.
    ///
    /// The purchased gate has none: GmGateProbe confirmed it is one static mesh with its leaves
    /// modelled already swung open. Threshold Refusal needs the gate to shut behind the player, so
    /// the leaves are authored here as their own geometry, hinged at the pillars, and swung closed
    /// at runtime by GmGateLeaves. Built from scaled boxes rather than an authored mesh because a
    /// wrought-iron gate IS rails and bars, so primitives give the right silhouette with no asset
    /// pipeline, and it stays fully parameterised off the real gate's measured bounds.
    /// The whole gate: two stone piers and two hinged iron leaves, all built.
    ///
    /// The purchased graveyard_gate is gone. It is a single 1,536-vert mesh whose leaves are modelled
    /// permanently open, so it could never satisfy the Threshold Refusal beat, and at the scale this
    /// road needs it rendered as two flat slabs with protruding stubs. Building it means the geometry
    /// is right, the leaves genuinely close, and the whole thing is parameterised rather than fought.
    static Vector3 BuildGate(Transform parent, Terrain terrain)
    {
        Vector2 xz = SpinePoint(GateT);
        float groundY = GroundAt(terrain, xz);
        Vector3 centre = new Vector3(xz.x, groundY, xz.y);
        Vector3 lateral = new Vector3(LateralAxis.x, 0f, LateralAxis.y);

        const float pierHeight = 3.15f;
        const float halfGap = 2.35f;              // gateway is ~4.7m across, a cart's width
        float leafHeight = pierHeight * 0.78f;
        float leafWidth = halfGap * 0.97f;        // the pair meets at the centreline

        Material iron = MakeMaterial("Gm_GateIron", new Color(0.055f, 0.052f, 0.050f), 0.42f, 0.85f);
        Material stone = MakeMaterial("Gm_GateStone", new Color(0.20f, 0.195f, 0.185f), 0.12f, 0f);

        var rig = new GameObject("EstateGate");
        rig.transform.SetParent(parent, true);
        rig.transform.position = centre;

        BuildGatePiers(rig.transform, centre, lateral, halfGap, pierHeight, stone);

        var leaves = rig.AddComponent<GmGateLeaves>();
        Transform left = MakeLeaf(rig.transform, "LeafWest", centre - lateral * halfGap,
            lateral, leafWidth, leafHeight, iron);
        Transform right = MakeLeaf(rig.transform, "LeafEast", centre + lateral * halfGap,
            -lateral, leafWidth, leafHeight, iron);
        leaves.Configure(left, right, 96f, 0.55f);

        Debug.Log($"[{LogTag}] gate built: piers {pierHeight:0.##}m, leaves {leafWidth:0.##}x{leafHeight:0.##}m");
        return centre;
    }

    static Material MakeMaterial(string name, Color colour, float smoothness, float metallic)
    {
        var m = new Material(Shader.Find("HDRP/Lit")) { name = name };
        m.SetColor("_BaseColor", colour);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", metallic);
        return m;
    }

    /// Width of an object measured along an arbitrary world direction, by projecting every mesh's
    /// transformed local-bounds corners onto it. Immune to the object's own rotation.
    static float ExtentAlong(GameObject go, Vector3 direction)
    {
        Vector3 dir = direction.normalized;
        float min = float.MaxValue, max = float.MinValue;
        bool any = false;

        foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Bounds lb = mf.sharedMesh.bounds;
            Matrix4x4 m = mf.transform.localToWorldMatrix;
            for (int c = 0; c < 8; c++)
            {
                var corner = new Vector3(
                    (c & 1) == 0 ? lb.min.x : lb.max.x,
                    (c & 2) == 0 ? lb.min.y : lb.max.y,
                    (c & 4) == 0 ? lb.min.z : lb.max.z);
                float d = Vector3.Dot(m.MultiplyPoint3x4(corner), dir);
                min = Mathf.Min(min, d);
                max = Mathf.Max(max, d);
                any = true;
            }
        }
        return any ? max - min : 0f;
    }

    /// An anchor carries the world orientation, a pivot child does the swinging. Splitting them
    /// keeps GmGateLeaves able to express the swing as a plain localRotation about Y, instead of
    /// having to compose the gate's world yaw into every frame of the animation.
    ///
    /// The leaf is built like an actual wrought-iron gate rather than as a grid of identical bars:
    /// a thick hanging stile at the hinge, a thick leading stile at the meeting edge, three rails,
    /// slim pickets between them, and spear finials standing proud of the top rail. The first
    /// version was a flat lattice of same-sized cubes and read as scaffolding.
    static Transform MakeLeaf(Transform parent, string name, Vector3 hinge, Vector3 inward,
        float width, float height, Material iron)
    {
        var anchor = new GameObject($"{name}_Anchor");
        anchor.transform.SetParent(parent, true);
        anchor.transform.position = hinge;
        // Unity's right vector is Cross(up, forward); solving for right == inward gives this forward.
        anchor.transform.rotation =
            Quaternion.LookRotation(Vector3.Cross(inward.normalized, Vector3.up), Vector3.up);

        var pivot = new GameObject(name);
        pivot.transform.SetParent(anchor.transform, false);

        const float picket = 0.032f;   // slim: real gate pickets are much thinner than their rails
        const float rail = 0.062f;
        const float stile = 0.085f;

        float railTopY = height * 0.80f;
        float railMidY = height * 0.40f;
        float railBotY = height * 0.09f;

        // Stiles: the structural verticals. These carry the weight and read as the gate's frame.
        AddBar(pivot.transform, "StileHanging", new Vector3(0f, height * 0.44f, 0f),
            new Vector3(stile, height * 0.88f, stile), iron);
        AddBar(pivot.transform, "StileLeading", new Vector3(width, height * 0.44f, 0f),
            new Vector3(stile, height * 0.88f, stile), iron);

        AddBar(pivot.transform, "RailTop", new Vector3(width * 0.5f, railTopY, 0f),
            new Vector3(width, rail, rail), iron);
        AddBar(pivot.transform, "RailMid", new Vector3(width * 0.5f, railMidY, 0f),
            new Vector3(width, rail * 0.8f, rail * 0.8f), iron);
        AddBar(pivot.transform, "RailBottom", new Vector3(width * 0.5f, railBotY, 0f),
            new Vector3(width, rail, rail), iron);

        // Pickets run from the bottom rail up THROUGH the top rail, ending in a spear. Stopping them
        // flush at the rail is what made the first version look like a fence panel.
        int count = Mathf.Max(5, Mathf.RoundToInt(width / 0.19f));
        for (int i = 1; i < count; i++)
        {
            float x = width * (i / (float)count);
            float tipY = height * 0.94f;
            AddBar(pivot.transform, $"Picket{i:D2}",
                new Vector3(x, (railBotY + tipY) * 0.5f, 0f),
                new Vector3(picket, tipY - railBotY, picket), iron);

            // Spearhead: a cube turned 45 degrees reads as a four-sided point at this scale, and
            // costs one primitive instead of a custom mesh.
            var spear = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spear.name = $"Spear{i:D2}";
            spear.transform.SetParent(pivot.transform, false);
            spear.transform.localPosition = new Vector3(x, tipY + 0.055f, 0f);
            spear.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            spear.transform.localScale = new Vector3(picket * 2.4f, 0.16f, picket * 2.4f);
            UnityEngine.Object.DestroyImmediate(spear.GetComponent<Collider>());
            if (iron != null) spear.GetComponent<MeshRenderer>().sharedMaterial = iron;
        }

        // A single diagonal brace per leaf, hinge-low to leading-high, which is how a real gate
        // resists its own weight. Also breaks up the repetition of the picket rhythm.
        var brace = GameObject.CreatePrimitive(PrimitiveType.Cube);
        brace.name = "Brace";
        brace.transform.SetParent(pivot.transform, false);
        float braceLen = Mathf.Sqrt(width * width + Mathf.Pow(railTopY - railBotY, 2f));
        brace.transform.localPosition = new Vector3(width * 0.5f, (railTopY + railBotY) * 0.5f, 0f);
        brace.transform.localRotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(railTopY - railBotY, width) * Mathf.Rad2Deg);
        brace.transform.localScale = new Vector3(braceLen, rail * 0.62f, rail * 0.62f);
        UnityEngine.Object.DestroyImmediate(brace.GetComponent<Collider>());
        if (iron != null) brace.GetComponent<MeshRenderer>().sharedMaterial = iron;

        return pivot.transform;
    }

    /// Stone piers to hang the gate on.
    ///
    /// The purchased graveyard_gate reads as two flat slabs with odd protrusions -- it is a single
    /// 1,536-vert mesh with its leaves modelled open, and at the scale this scene needs it does not
    /// hold up. Replaced with built piers: plinth, tapered shaft, cap and finial. Four primitives
    /// each, fully controlled, and they actually look like something a gate could hang from.
    static void BuildGatePiers(Transform parent, Vector3 centre, Vector3 lateral, float halfGap,
        float height, Material stone)
    {
        foreach (int s in new[] { -1, 1 })
        {
            Vector3 at = centre + lateral * (s * (halfGap + 0.34f));
            var pier = new GameObject($"GatePier_{(s < 0 ? "W" : "E")}");
            pier.transform.SetParent(parent, true);
            pier.transform.position = at;
            pier.transform.rotation = Quaternion.Euler(0f, SpineYawDeg, 0f);

            AddBar(pier.transform, "Plinth", new Vector3(0f, 0.16f, 0f),
                new Vector3(0.92f, 0.32f, 0.92f), stone);
            AddBar(pier.transform, "ShaftLower", new Vector3(0f, height * 0.34f, 0f),
                new Vector3(0.74f, height * 0.52f, 0.74f), stone);
            AddBar(pier.transform, "ShaftUpper", new Vector3(0f, height * 0.76f, 0f),
                new Vector3(0.66f, height * 0.34f, 0.66f), stone);
            AddBar(pier.transform, "Cap", new Vector3(0f, height * 0.95f, 0f),
                new Vector3(0.96f, 0.14f, 0.96f), stone);

            var finial = GameObject.CreatePrimitive(PrimitiveType.Cube);
            finial.name = "Finial";
            finial.transform.SetParent(pier.transform, false);
            finial.transform.localPosition = new Vector3(0f, height * 1.06f, 0f);
            finial.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            finial.transform.localScale = new Vector3(0.44f, 0.42f, 0.44f);
            UnityEngine.Object.DestroyImmediate(finial.GetComponent<Collider>());
            if (stone != null) finial.GetComponent<MeshRenderer>().sharedMaterial = stone;
        }
    }

    static void AddBar(Transform parent, string name, Vector3 localPos, Vector3 size, Material iron)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        // No collider: the gate is a story beat, not an obstacle. A solid gate across the drive
        // would trap the player behind it the moment it closes, and GmVillageWalkTest would fail.
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        if (iron != null) go.GetComponent<MeshRenderer>().sharedMaterial = iron;
    }

    /// One lantern per pillar. The first attempt hung a single 220-lumen volumetric lamp at the
    /// centre of the gateway at head height, which put a white sun in the middle of the road and
    /// washed the whole approach (mean frame luminance went 0.234 -> 0.729). Two dim lamps sitting
    /// ON the pillars light the stonework, leave the roadway dark to walk down, and read as fixtures
    /// belonging to the gate rather than as a floating glow.
    static void MakeGateLanterns(Transform parent, Vector2 xz, Terrain terrain, float yawDeg)
    {
        Vector2 axis = SpineAxis;
        Vector2 right = new Vector2(axis.y, -axis.x);
        float groundY = GroundAt(terrain, xz);

        foreach (int side in new[] { -1, 1 })
        {
            Vector2 at = xz + right * (side * 2.3f);
            var go = new GameObject($"GateLantern_{(side < 0 ? "W" : "E")}");
            go.transform.SetParent(parent, true);
            go.transform.position = new Vector3(at.x, groundY + 2.6f, at.y);

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.58f, 0.24f);
            light.range = 6.5f;
            light.shadows = LightShadows.None;

            var hd = go.AddComponent<HDAdditionalLightData>();
            // Volumetric OFF, same conclusion the village practicals reached. Even at 38 lumens the
            // fog near the ground is dense enough that each lamp grew a halo wider than the gateway
            // and washed the whole foreground (frame luminance 0.558 against 0.234 unlit). Without
            // the fog term the same lamp lights the stonework and leaves the road dark to walk down.
            hd.affectsVolumetric = false;
            light.lightUnit = LightUnit.Lumen;
            // Enough to pick the ironwork out of the dark and no more. At 45 these two lamps were
            // the brightest thing on screen and lit the whole approach like a forecourt.
            light.intensity = 26f;

            var flicker = go.AddComponent<GmLightFlicker>();
            flicker.baseIntensity = 26f;
            // Slower and shallower than the window practicals: these read as lamps in glass boxes
            // out in the wind, not as open flame indoors.
            flicker.variation = 0.13f;
            flicker.speed = side < 0 ? 1.15f : 1.42f;
        }
    }

    // The mansion was measured onto the clearest ground on the site, which is exactly why it landed
    // on an empty plain: "clear of obstacles" and "has anything to look at" are opposite conditions.
    // Placed and unlit, it read as a dollhouse on a table -- the same bare-ground failure the whole
    // environment pivot exists to fix, just relocated to the mansion.
    //
    // Everything here is instantiated from the VILLAGE'S OWN prop set (the same SM_Tree/SM_Bush
    // meshes the Haunted Village pack dresses its street with) so the approach reads as the same
    // place continuing, rather than a second art kit bolted onto the end of the road. Seeded, so
    // rebuilding the scene produces the identical layout every time.
    static int DressMansionGrounds(Transform parent, Terrain terrain)
    {
        var rng = new System.Random(20260724);
        var grounds = new GameObject("MansionGrounds");
        grounds.transform.SetParent(parent, true);

        GameObject[] trees = LoadSet("SM_Tree_02", "SM_Tree_04", "SM_Tree_05", "SM_Tree_07", "SM_Tree_08");
        GameObject[] bushes = LoadSet("SM_Bush_01", "SM_Bush_03", "SM_Bush_04", "SM_Bush_06", "SM_Bush_07");
        if (trees.Length == 0 && bushes.Length == 0)
        {
            Debug.LogWarning($"[{LogTag}] no village foliage prefabs resolved; mansion grounds left bare");
            return 0;
        }

        int placed = 0;

        // Flanking tree line down the approach. It does two jobs: it gives the walk a middle layer
        // at eye level, and it funnels sightlines onto the mansion instead of letting them run off
        // into empty terrain on both sides. Spacing and lateral offset are deliberately generous --
        // the first pass used 7.5m spacing at 10-18m out and closed the drive into a tunnel of
        // branches with the mansion barely visible through it.
        for (float t = -42f; t >= -110f; t -= 12f)
        {
            foreach (int side in new[] { -1, 1 })
            {
                float lateral = side * (float)(15.0 + rng.NextDouble() * 9.0);
                float jitter = (float)(rng.NextDouble() * 6.0 - 3.0);
                if (!Buildable(t + jitter, lateral)) continue;
                if (trees.Length == 0) continue;
                PlaceScatter(grounds.transform, trees[rng.Next(trees.Length)], terrain,
                    t + jitter, lateral, rng, 0.8f, 1.15f);
                placed++;
            }
        }

        // Bush clusters. Scattered evenly they read as a texture; in clumps of two or three they
        // read as growth that chose where to be, which is what the village's own dressing does.
        //
        // These are scaled DOWN hard. SM_Bush_01 measures 15 x 7 x 15m in the survey -- it is a
        // thicket, not a shrub -- so the first pass's 0.7-1.35 range produced 10-20m masses that
        // walled the drive in. 0.25-0.5 puts them at roughly waist-to-head height, which is the
        // middle layer that was actually missing.
        for (int cluster = 0; cluster < 16; cluster++)
        {
            float ct = -40f - (float)(rng.NextDouble() * 72.0);
            float cl = (float)(rng.NextDouble() * 24.0 - 12.0);
            cl += Mathf.Sign(cl) * 10f;
            if (!Buildable(ct, cl)) continue;

            int members = 1 + rng.Next(3);
            for (int i = 0; i < members; i++)
            {
                float dt = ct + (float)(rng.NextDouble() * 6.0 - 3.0);
                float dl = cl + (float)(rng.NextDouble() * 6.0 - 3.0);
                if (!Buildable(dt, dl)) continue;
                if (bushes.Length == 0) continue;
                PlaceScatter(grounds.transform, bushes[rng.Next(bushes.Length)], terrain,
                    dt, dl, rng, 0.25f, 0.5f);
                placed++;
            }
        }

        // Verge scrub. Trees and bushes gave the approach a skyline and a middle layer, but the
        // ground between them stayed bare dirt, which is what made the mansion read as a model on a
        // table. The reference frame from the old estate build (Screens/WendHill/tour-03-gate.png)
        // has tufted growth running right up to the wheel ruts, and that is the layer this adds.
        // Allowed closer to the drive than the bushes are, because ankle-height growth on the verge
        // narrows the road usefully instead of blocking it.
        GameObject[] scrub = LoadSet("SM_Grass_09", "SM_Bush_07", "SM_Bush_05", "SM_Bush_02");
        if (scrub.Length > 0)
        {
            for (int i = 0; i < 190; i++)
            {
                float st = -24f - (float)(rng.NextDouble() * 92.0);
                float sl = (float)(rng.NextDouble() * 34.0 - 17.0);
                sl += Mathf.Sign(sl) * 5f;
                if (Mathf.Abs(sl) < 5.5f) continue;
                if (Mathf.Abs(st - MansionT) < 13f && Mathf.Abs(sl) < 12f) continue;
                PlaceScatter(grounds.transform, scrub[rng.Next(scrub.Length)], terrain,
                    st, sl, rng, 0.10f, 0.26f);
                placed++;
            }
        }

        Debug.Log($"[{LogTag}] mansion grounds: {placed} props from {trees.Length} tree, " +
                  $"{bushes.Length} bush and {scrub.Length} scrub prefabs");
        return placed;
    }

    /// Keeps the drive walkable and the mansion's own footprint clear. Without the second test the
    /// scatter happily plants trees through the porch.
    static bool Buildable(float t, float lateral)
    {
        if (Mathf.Abs(lateral) < 9f) return false;                          // the drive itself
        if (Mathf.Abs(t - MansionT) < 15f && Mathf.Abs(lateral) < 14f) return false;  // mansion footprint
        return true;
    }

    static void PlaceScatter(Transform parent, GameObject prefab, Terrain terrain,
        float t, float lateral, System.Random rng, float minScale, float maxScale)
    {
        Vector2 axis = SpineAxis;
        Vector2 right = new Vector2(axis.y, -axis.x);
        Vector2 xz = SpinePoint(t) + right * lateral;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.localScale = Vector3.one * Mathf.Lerp(minScale, maxScale, (float)rng.NextDouble());
        go.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);

        Bounds b = Encapsulate(go);
        float groundY = GroundAt(terrain, xz);
        go.transform.position += new Vector3(xz.x - b.center.x, groundY - b.min.y, xz.y - b.center.z);
    }

    static GameObject[] LoadSet(params string[] names) =>
        names.Select(FindPrefab).Where(p => p != null).ToArray();

    /// Scale-to-target-height then ground-seat, matching how GmEstateBuilder mounts design
    /// placements: bounds are re-read AFTER scaling and rotating, because both change them.
    static GameObject PlaceProp(Transform parent, string assetName, string label,
        Terrain terrain, Vector2 xz, float targetHeight, float yawDeg)
    {
        GameObject prefab = FindPrefab(assetName);
        if (prefab == null)
        {
            Debug.LogWarning($"[{LogTag}] asset not found: '{assetName}' -- {label} not placed");
            return null;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = label;

        Bounds b = Encapsulate(go);
        if (b.size.y > 0.01f) go.transform.localScale = Vector3.one * (targetHeight / b.size.y);
        go.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);

        b = Encapsulate(go);
        float groundY = GroundAt(terrain, xz);
        go.transform.position += new Vector3(xz.x - b.center.x, groundY - b.min.y, xz.y - b.center.z);
        return go;
    }

    static float GroundAt(Terrain terrain, Vector2 xz) =>
        terrain.SampleHeight(new Vector3(xz.x, 0f, xz.y)) + terrain.transform.position.y;

    static Bounds Encapsulate(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    internal static GameObject FindPrefab(string name)
    {
        foreach (string filter in new[] { $"{name} t:prefab", $"{name} t:model" })
        {
            foreach (string guid in AssetDatabase.FindAssets(filter))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.Equals(Path.GetFileNameWithoutExtension(path), name,
                        StringComparison.OrdinalIgnoreCase)) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null) return go;
            }
        }
        return null;
    }

    static string Fmt(Vector3 v) => $"({v.x:0.#},{v.y:0.#},{v.z:0.#})";

    static string FmtBounds(GameObject go)
    {
        Bounds b = Encapsulate(go);
        return $"size({b.size.x:0.#}x{b.size.y:0.#}x{b.size.z:0.#})";
    }
}

