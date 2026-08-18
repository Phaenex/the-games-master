using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEngine;

/// <summary>
/// Builds enterable outbuilding interiors on the estate grounds. Slice 1: the coach house only --
/// the site's coaching-inn layer (docs/superpowers/specs/2026-07-14-house-history.md L4) made
/// physical, and the cheapest room on the estate to enter per
/// docs/superpowers/specs/2026-08-13-the-reckoning.md's "privacy, not curiosity" rule. Reuses
/// GmHouseBeginningBuilder's room-shell/material/interactable helpers rather than duplicating them.
/// </summary>
public static class GmWendOutbuildings
{
    const string CoachHouseId = "coach-house";
    const float RoomWidth = 7f;
    const float RoomDepth = 9f;
    const float RoomHeight = 4f;
    const float DoorwayWidth = 2.6f;
    const float WallT = 0.32f;
    const float RouteClearance = 4.2f;
    const string AbandonedVillageRoot =
        "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Meshes";
    const string WitchVillageRoot =
        "Assets/LeartesStudios/WitchVillage/HDRP/Art/Meshes/Architecture";
    const string TimberWallAsset = AbandonedVillageRoot + "/Structure/SM_Wood_PlankWall_SetB_250x300.fbx";
    const string TimberGateAsset = AbandonedVillageRoot + "/Structure/SM_Wood_PlankWallGate_SetB_250x200.fbx";
    const string TimberRoofAsset = AbandonedVillageRoot + "/Structure/Roof/SM_Roof_01_Large_000.fbx";
    const string BarnDoorAsset = AbandonedVillageRoot + "/Structure/SM_BarnDoor.fbx";
    const string FeedingTroughAsset = AbandonedVillageRoot + "/Props/SM_FeedingTrough.fbx";
    const string HayAsset = AbandonedVillageRoot + "/Props/SM_HayStack.fbx";
    const string CartAsset = AbandonedVillageRoot + "/Props/SM_WoodenCart_01.fbx";
    const string BarrelAsset = AbandonedVillageRoot + "/Props/SM_Barrel_01.fbx";
    const string CrateAsset = AbandonedVillageRoot + "/Props/SM_Crate_01.fbx";
    const string LanternAsset = AbandonedVillageRoot + "/Props/SM_Lantern.fbx";
    const string PlankFloorAsset = WitchVillageRoot + "/Floor/SM_PlankFloor_Floor1.fbx";
    const string TimberMaterialAsset = AbandonedVillageRoot + "/Structure/Materials/MI_WoodStructure_Dark_03.mat";
    const string RoofMaterialAsset = AbandonedVillageRoot + "/Structure/Roof/Materials/MI_Roof_02.mat";
    const string HayMaterialAsset = AbandonedVillageRoot + "/Props/Materials/MI_HayStack_Inst.mat";
    const string CartMaterialAsset = AbandonedVillageRoot + "/Props/Materials/MI_WoodenCart_Inst.mat";
    const string CrateMaterialAsset = AbandonedVillageRoot + "/Props/Materials/MI_Crate_Inst.mat";
    const string LanternMaterialAsset = AbandonedVillageRoot + "/Props/Materials/MI_Lantern_01.mat";

    internal struct CoachPlacement
    {
        public Vector3 door;
        public Vector3 center;
        public Vector3 outward;
        public float lateral;
        public float minRouteClearance;
    }

    public static void Build(Transform openingRoot, GmRouteSpline route)
    {
        GameObject stale = GameObject.Find("Outbuildings");
        if (stale != null) UnityEngine.Object.DestroyImmediate(stale);

        var outbuildingsRoot = new GameObject("Outbuildings").transform;
        outbuildingsRoot.SetParent(openingRoot, true);

        BuildCoachHouse(outbuildingsRoot, route);
    }

    static void BuildCoachHouse(Transform parent, GmRouteSpline route)
    {
        GmWorldAnchor doorAnchor = GmWorldAnchor.Find("coach-doors");
        if (doorAnchor == null)
            throw new InvalidOperationException("coach house interior requires the 'coach-doors' POI anchor to exist first");

        // A lateral offset from only the 338m tangent is not enough on this route: an earlier leg
        // bends back through that side. The first implementation put the entire coach house across
        // the physical walk, producing a black exterior at 300m, a camera inside the inactive shell
        // at 315m, and an exit through the far wall at 330m. Sample the whole route and choose the
        // nearest side/offset whose complete footprint keeps the 4.2m controller corridor clear.
        var routeSamples = new List<Vector3>();
        for (float metres = 0f; metres < route.Length; metres += 1f)
            routeSamples.Add(route.PointAt(metres));
        routeSamples.Add(route.PointAt(route.Length));

        Vector3 routePoint = route.PointAt(338f);
        Vector3 tangent = route.TangentAt(338f);
        Vector3 preferredRight = Vector3.Cross(Vector3.up, tangent).normalized;
        CoachPlacement placement = SelectCoachHousePlacement(routeSamples, routePoint, preferredRight);
        Vector3 doorPosition = GroundPoint(placement.door);
        doorAnchor.transform.position = doorPosition;
        Quaternion roomRotation = Quaternion.LookRotation(placement.outward, Vector3.up);

        var room = new GameObject("CoachHouse").transform;
        room.SetParent(parent, true);
        // Local +Z points outward, and the south doorway is centered exactly on the owned door POI.
        // The old extra 2.5m offset separated the facade prop from its actual opening.
        room.SetPositionAndRotation(doorPosition + placement.outward * (RoomDepth * 0.5f), roomRotation);

        Material wall = GmHouseBeginningBuilder.Mat("Coach_Wall", new Color(0.20f, 0.17f, 0.14f), 0.12f);
        Material floor = GmHouseBeginningBuilder.Mat("Coach_Floor", new Color(0.16f, 0.12f, 0.09f), 0.10f);
        Material chalk = GmHouseBeginningBuilder.Mat("Coach_Chalk", new Color(0.62f, 0.58f, 0.52f), 0.05f);

        BuildRoomShellWithFloorMaterial(room, "CoachHouse", 0f, RoomWidth, RoomDepth, RoomHeight,
            DoorwayWidth, wall, floor);

        var interiorRoot = new GameObject("Interior").transform;
        interiorRoot.SetParent(room, false);
        DressCoachHouse(room, interiorRoot);

        // Tally marks live over the real timber stall mesh rather than pretending a cube is a stall.
        GameObject tallyBoard = GmHouseBeginningBuilder.Slab("TallyBoard", new Vector3(2.55f, 1.5f, -2.6f),
            new Vector3(0.05f, 1.0f, 1.6f), chalk, interiorRoot);

        // Departures board: the coach house's multi-line clue, escalating cost per line read.
        GameObject departuresBoard = GmHouseBeginningBuilder.Slab("DeparturesBoard",
            new Vector3(-2.6f, 1.6f, 3.2f), new Vector3(1.4f, 1.0f, 0.06f), chalk, interiorRoot);

        // A single warm lantern -- the only light source, on while the interior is active.
        GmHouseBeginningBuilder.AddWarmLight(interiorRoot, "CoachLantern",
            room.TransformPoint(new Vector3(0f, RoomHeight - 0.6f, 0f)), 320f, 8f);

        interiorRoot.gameObject.SetActive(false);

        // Trigger volume covering the whole room footprint plus a short approach apron, so the
        // interior activates a step before the player actually crosses the doorway threshold.
        var volumeGo = room.gameObject;
        BoxCollider trigger = volumeGo.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, RoomHeight * 0.5f, 1.5f);
        trigger.size = new Vector3(RoomWidth + 1.5f, RoomHeight + 1f, RoomDepth + 4f);
        GmOutbuilding outbuilding = volumeGo.AddComponent<GmOutbuilding>();
        outbuilding.Configure(CoachHouseId, interiorRoot.gameObject);

        AddClue(interiorRoot, tallyBoard, "coach-house-tally-marks", CoachHouseId, "Examine",
            "Tally marks chalked in groups of seven, worn into the wood.",
            "The freshest group is unweathered. Someone marked this tonight.");
        AddClue(interiorRoot, departuresBoard, "coach-house-departures-line-1", CoachHouseId, "Read",
            "A departures board, chalk long dry. Coaches, timetabled, to towns that don't run coaches anymore.",
            "One line is rewritten over and over in the same hand: LAST CALL, WEND HILL.");
        AddClue(interiorRoot, GmHouseBeginningBuilder.Slab("WheelLedger", new Vector3(1.2f, 0.9f, 3.6f),
            new Vector3(0.5f, 0.05f, 0.4f), chalk, interiorRoot), "coach-house-wheel-ledger", CoachHouseId, "Read",
            "A leather ledger of wheel repairs, dated across two centuries in three different hands.",
            "The most recent entry is dated tonight, and signed with your own initials.");

        Debug.Log($"[GmWendOutbuildings] PASS: coach house built at {room.position}, " +
                  $"route clearance {placement.minRouteClearance:0.0}m at lateral {placement.lateral:0.0}m, " +
                  "3 clues, interior starts inactive");
    }

    /// <summary>
    /// Dresses the collision-only room shell with owned period meshes. Every placement is fitted
    /// from imported renderer bounds by GmOwnedPropFactory, so FBX pivots and source scale never
    /// become hand-authored assumptions. Exterior art stays outside the activation root because the
    /// roadside silhouette must exist before the player crosses the threshold.
    /// </summary>
    internal static void DressCoachHouse(Transform room, Transform interiorRoot)
    {
        if (room == null) throw new ArgumentNullException(nameof(room));
        if (interiorRoot == null) throw new ArgumentNullException(nameof(interiorRoot));

        // Several source-pack Shader Graph materials render magenta in the shipping HDRP path.
        // Keep the owned geometry, but map it through the same textured HDRP/Lit recipes already
        // proved in the house interiors. These are material families, not flat colour stand-ins.
        Material timber = SafeHdrpMaterial(TimberMaterialAsset, "Coach_Timber",
            new Color(0.56f, 0.48f, 0.39f), 0.18f);
        Material roof = SafeHdrpMaterial(RoofMaterialAsset, "Coach_Roof",
            new Color(0.52f, 0.49f, 0.45f), 0.14f);
        Material plankFloor = GmVictorianInteriorKit.Surface("floor", "Coach_PlankFloor", new Vector2(3f, 4f),
            new Color(0.42f, 0.31f, 0.23f));
        Material hay = SafeHdrpMaterial(HayMaterialAsset, "Coach_Hay",
            new Color(0.75f, 0.66f, 0.42f), 0.10f);
        Material cart = SafeHdrpMaterial(CartMaterialAsset, "Coach_Cart",
            new Color(0.58f, 0.49f, 0.39f), 0.16f);
        Material crate = SafeHdrpMaterial(CrateMaterialAsset, "Coach_Crate",
            new Color(0.62f, 0.51f, 0.39f), 0.14f);
        Material lantern = SafeHdrpMaterial(LanternMaterialAsset, "Coach_Lantern",
            new Color(0.56f, 0.48f, 0.38f), 0.24f);

        var exterior = new GameObject("ExteriorArt").transform;
        exterior.SetParent(room, false);

        // A purpose-built gate wall gives the facade one coherent coach opening instead of two
        // unrelated wall slabs with a hole between them.
        PlaceLocal(TimberGateAsset, "FacadeGate", exterior, room,
            new Vector3(0f, 2f, -RoomDepth * 0.5f - 0.03f), new Vector3(7f, 4f, 0.38f),
            Quaternion.identity, false, 0f, timber);
        PlaceLocal(TimberWallAsset, "RearWest", exterior, room,
            new Vector3(-1.75f, 2f, RoomDepth * 0.5f + 0.03f), new Vector3(3.5f, 4f, 0.34f),
            Quaternion.identity, false, 0f, timber);
        PlaceLocal(TimberWallAsset, "RearEast", exterior, room,
            new Vector3(1.75f, 2f, RoomDepth * 0.5f + 0.03f), new Vector3(3.5f, 4f, 0.34f),
            Quaternion.identity, false, 0f, timber);

        // Three real timber bays per long side avoid stretching one mesh into a texture-smeared wall.
        for (int bay = 0; bay < 3; bay++)
        {
            float z = -3f + bay * 3f;
            PlaceLocal(TimberWallAsset, $"WestBay{bay + 1}", exterior, room,
                new Vector3(-RoomWidth * 0.5f - 0.03f, 2f, z), new Vector3(0.34f, 4f, 3f),
                Quaternion.Euler(0f, 90f, 0f), false, 0f, timber);
            PlaceLocal(TimberWallAsset, $"EastBay{bay + 1}", exterior, room,
                new Vector3(RoomWidth * 0.5f + 0.03f, 2f, z), new Vector3(0.34f, 4f, 3f),
                Quaternion.Euler(0f, 90f, 0f), false, 0f, timber);
        }

        PlaceLocal(TimberRoofAsset, "CoachRoof", exterior, room,
            new Vector3(0f, RoomHeight + 0.75f, 0f), new Vector3(8.2f, 2.1f, 10.5f),
            Quaternion.identity, false, 0f, roof);
        PlaceLocal(BarnDoorAsset, "CoachDoorLeft", exterior, room,
            new Vector3(-0.78f, 1.75f, -RoomDepth * 0.5f - 0.22f), new Vector3(1.15f, 3.45f, 0.55f),
            Quaternion.Euler(0f, -32f, 0f), false, 0f, timber);
        PlaceLocal(BarnDoorAsset, "CoachDoorRight", exterior, room,
            new Vector3(0.78f, 1.75f, -RoomDepth * 0.5f - 0.22f), new Vector3(1.15f, 3.45f, 0.55f),
            Quaternion.Euler(0f, 32f, 0f), false, 0f, timber);
        PlaceLocal(LanternAsset, "CoachLantern", exterior, room,
            new Vector3(0f, 3.15f, -RoomDepth * 0.5f - 0.38f), new Vector3(0.32f, 0.56f, 0.32f),
            Quaternion.identity, false, 0f, lantern);
        GmHouseBeginningBuilder.AddWarmLight(exterior, "CoachDoorLight",
            room.TransformPoint(new Vector3(0f, 3.05f, -RoomDepth * 0.5f - 0.7f)), 70f, 5f);

        // Plank floor tiles and timber stall dividers appear only when the activation volume opens
        // the room. The exterior shell above remains present, so there is never a black-box pop.
        for (int x = 0; x < 2; x++)
        {
            for (int z = 0; z < 2; z++)
            {
                PlaceLocal(PlankFloorAsset, $"PlankFloor{x + 1}{z + 1}", interiorRoot, room,
                    new Vector3(x == 0 ? -1.75f : 1.75f, 0.02f, z == 0 ? -2.25f : 2.25f),
                    new Vector3(3.45f, 0.18f, 4.45f), Quaternion.identity, true, 0f, plankFloor);
            }
        }
        for (int stall = 0; stall < 3; stall++)
        {
            float z = -2.6f + stall * 2.6f;
            PlaceLocal(TimberWallAsset, $"StallDivider{stall + 1}", interiorRoot, room,
                new Vector3(2.25f, 1.25f, z), new Vector3(2.4f, 2.5f, 0.24f),
                Quaternion.identity, false, 0f, timber);
        }

        PlaceLocal(FeedingTroughAsset, "FeedingTrough", interiorRoot, room,
            new Vector3(2.25f, 0f, 0f), new Vector3(1.7f, 0.9f, 0.8f), Quaternion.Euler(0f, 90f, 0f), true, 0f, timber);
        PlaceLocal(HayAsset, "HayStack", interiorRoot, room,
            new Vector3(2.25f, 0f, 2.95f), new Vector3(1.75f, 1.45f, 1.65f), Quaternion.Euler(0f, -16f, 0f), true, 0f, hay);
        PlaceLocal(CartAsset, "WoodenCart", interiorRoot, room,
            new Vector3(-0.85f, 0f, 2.3f), new Vector3(2.4f, 1.75f, 3.2f), Quaternion.Euler(0f, -12f, 0f), true, 0f, cart);
        PlaceLocal(BarrelAsset, "Barrel", interiorRoot, room,
            new Vector3(-2.55f, 0f, -2.7f), new Vector3(0.85f, 1.25f, 0.85f), Quaternion.Euler(0f, 8f, 0f), true, 0f, timber);
        PlaceLocal(CrateAsset, "CrateStack", interiorRoot, room,
            new Vector3(-2.2f, 0f, -1.45f), new Vector3(1.15f, 1.1f, 1.15f), Quaternion.Euler(0f, -9f, 0f), true, 0f, crate);

        ClearVegetationIntrusions(room, 3f);
        HidePrimitiveShellRenderers(room);
    }

    static Material SafeHdrpMaterial(string sourcePath, string name, Color baseColor, float smoothness)
    {
        Material source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
        if (source == null)
            throw new InvalidOperationException($"coach-house material source is missing at {sourcePath}");
        Shader shader = Shader.Find("HDRP/Lit");
        if (shader == null) throw new InvalidOperationException("HDRP/Lit is unavailable");
        var material = new Material(shader) { name = name };
        foreach (string property in new[] { "_BaseColorMap", "_NormalMap", "_MaskMap" })
        {
            if (!source.HasProperty(property) || !material.HasProperty(property)) continue;
            Texture texture = source.GetTexture(property);
            if (texture == null) continue;
            material.SetTexture(property, texture);
            material.SetTextureScale(property, source.GetTextureScale(property));
            material.SetTextureOffset(property, source.GetTextureOffset(property));
        }
        material.SetColor("_BaseColor", baseColor);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", 0f);
        HDShaderUtils.ResetMaterialKeywords(material);
        return material;
    }

    internal static bool IsOutsideCoachHouseFootprint(Vector3 worldPoint, Transform coachHouse, float margin)
    {
        if (coachHouse == null) return true;
        Vector3 local = coachHouse.InverseTransformPoint(worldPoint);
        return Mathf.Abs(local.x) > RoomWidth * 0.5f + margin ||
               Mathf.Abs(local.z) > RoomDepth * 0.5f + margin;
    }

    internal static int ClearCoachHouseVegetation()
    {
        GameObject coachHouse = GameObject.Find("CoachHouse");
        return coachHouse != null ? ClearVegetationIntrusions(coachHouse.transform, 3f) : 0;
    }

    static int ClearVegetationIntrusions(Transform room, float margin)
    {
        int cleared = 0;
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null && terrain.terrainData != null)
        {
            TerrainData data = terrain.terrainData;
            TreeInstance[] trees = data.treeInstances;
            TreeInstance[] retained = trees.Where(instance =>
            {
                Vector3 world = terrain.transform.position + Vector3.Scale(instance.position, data.size);
                return IsOutsideCoachHouseFootprint(world, room, margin);
            }).ToArray();
            if (retained.Length != trees.Length)
            {
                cleared += trees.Length - retained.Length;
                data.treeInstances = retained;
            }
            cleared += ClearTerrainDetails(data, terrain.transform.position, room, margin);
        }

        var prefabRoots = new HashSet<GameObject>();
        foreach (Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (renderer == null || renderer.transform.IsChildOf(room)) continue;
            if (IsOutsideCoachHouseFootprint(renderer.bounds.center, room, margin)) continue;
            if (!LooksLikeVegetation(renderer.transform)) continue;
            GameObject prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(renderer.gameObject);
            if (prefabRoot != null && LooksLikeVegetation(prefabRoot.transform)) prefabRoots.Add(prefabRoot);
            else
            {
                renderer.enabled = false;
                foreach (Collider collider in renderer.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                cleared++;
            }
        }
        foreach (GameObject prefabRoot in prefabRoots)
        {
            if (prefabRoot == null) continue;
            UnityEngine.Object.DestroyImmediate(prefabRoot);
            cleared++;
        }
        if (cleared > 0)
            Debug.Log($"[GmWendOutbuildings] cleared {cleared} vegetation intrusion(s) from the coach-house footprint");
        return cleared;
    }

    static int ClearTerrainDetails(TerrainData data, Vector3 origin, Transform room, float margin)
    {
        if (data.detailPrototypes.Length == 0) return 0;
        Vector3[] corners =
        {
            room.TransformPoint(new Vector3(-RoomWidth * 0.5f - margin, 0f, -RoomDepth * 0.5f - margin)),
            room.TransformPoint(new Vector3(RoomWidth * 0.5f + margin, 0f, -RoomDepth * 0.5f - margin)),
            room.TransformPoint(new Vector3(-RoomWidth * 0.5f - margin, 0f, RoomDepth * 0.5f + margin)),
            room.TransformPoint(new Vector3(RoomWidth * 0.5f + margin, 0f, RoomDepth * 0.5f + margin)),
        };
        float minX = corners.Min(point => point.x);
        float maxX = corners.Max(point => point.x);
        float minZ = corners.Min(point => point.z);
        float maxZ = corners.Max(point => point.z);
        int x0 = Mathf.Clamp(Mathf.FloorToInt((minX - origin.x) / data.size.x * data.detailWidth), 0, data.detailWidth - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((maxX - origin.x) / data.size.x * data.detailWidth), x0 + 1, data.detailWidth);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((minZ - origin.z) / data.size.z * data.detailHeight), 0, data.detailHeight - 1);
        int z1 = Mathf.Clamp(Mathf.CeilToInt((maxZ - origin.z) / data.size.z * data.detailHeight), z0 + 1, data.detailHeight);
        int width = x1 - x0;
        int height = z1 - z0;
        int cleared = 0;
        for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
        {
            int[,] details = data.GetDetailLayer(x0, z0, width, height, layer);
            for (int z = 0; z < height; z++)
            for (int x = 0; x < width; x++)
            {
                Vector3 point = new Vector3(
                    origin.x + (x0 + x + 0.5f) / data.detailWidth * data.size.x,
                    room.position.y,
                    origin.z + (z0 + z + 0.5f) / data.detailHeight * data.size.z);
                if (!IsOutsideCoachHouseFootprint(point, room, margin) && details[z, x] > 0)
                {
                    cleared += details[z, x];
                    details[z, x] = 0;
                }
            }
            data.SetDetailLayer(x0, z0, layer, details);
        }
        return cleared;
    }

    static bool LooksLikeVegetation(Transform transform)
    {
        for (Transform candidate = transform; candidate != null; candidate = candidate.parent)
        {
            string name = candidate.name.ToLowerInvariant();
            if (name.Contains("tree") || name.Contains("foliage") || name.Contains("foilage") ||
                name.Contains("bush") || name.Contains("grass") || name.Contains("shrub")) return true;
        }
        return false;
    }

    static GameObject PlaceLocal(string assetPath, string objectName, Transform parent, Transform room,
        Vector3 localCenter, Vector3 targetSize, Quaternion localRotation = default,
        bool ground = false, float localSurfaceY = 0f, Material overrideMaterial = null)
    {
        Quaternion rotation = room.rotation * (localRotation == default ? Quaternion.identity : localRotation);
        Vector3 right = rotation * Vector3.right;
        Vector3 up = rotation * Vector3.up;
        Vector3 forward = rotation * Vector3.forward;
        Vector3 worldTargetSize = new Vector3(
            Mathf.Abs(right.x) * targetSize.x + Mathf.Abs(up.x) * targetSize.y + Mathf.Abs(forward.x) * targetSize.z,
            Mathf.Abs(right.y) * targetSize.x + Mathf.Abs(up.y) * targetSize.y + Mathf.Abs(forward.y) * targetSize.z,
            Mathf.Abs(right.z) * targetSize.x + Mathf.Abs(up.z) * targetSize.y + Mathf.Abs(forward.z) * targetSize.z);
        return GmOwnedPropFactory.PlacePrefab(assetPath, objectName, parent, room.TransformPoint(localCenter),
            worldTargetSize, rotation, ground, room.TransformPoint(new Vector3(0f, localSurfaceY, 0f)).y,
            overrideMaterial);
    }

    static void HidePrimitiveShellRenderers(Transform room)
    {
        string[] names =
        {
            "CoachHouseFloor", "CoachHouseCeiling", "CoachHouseWestWall", "CoachHouseEastWall",
            "CoachHouseNorthWall", "CoachHouseSouthWallWest", "CoachHouseSouthWallEast", "CoachHouseLintel",
        };
        foreach (string name in names)
        {
            Transform structural = room.Find(name);
            if (structural == null)
                throw new InvalidOperationException($"coach house structural shell is missing '{name}'");
            Renderer renderer = structural.GetComponent<Renderer>();
            if (renderer == null)
                throw new InvalidOperationException($"coach house structural shell '{name}' has no renderer to hide");
            renderer.enabled = false;
        }
    }

    internal static CoachPlacement SelectCoachHousePlacement(IReadOnlyList<Vector3> routeSamples,
        Vector3 routePoint, Vector3 preferredRight)
    {
        if (routeSamples == null || routeSamples.Count == 0)
            throw new ArgumentException("coach house placement requires route samples", nameof(routeSamples));
        preferredRight.y = 0f;
        if (preferredRight.sqrMagnitude < 0.001f)
            throw new ArgumentException("coach house placement requires a horizontal side vector", nameof(preferredRight));
        preferredRight.Normalize();

        // Nearest viable entrance wins. At each distance the authored side is tried first, then its
        // opposite. If a returning leg occupies the authored side, the building crosses the route
        // only in the selection math, never in the shipped scene.
        for (float lateral = 8f; lateral <= 30f; lateral += 2f)
        {
            for (int side = 0; side < 2; side++)
            {
                Vector3 outward = preferredRight * (side == 0 ? 1f : -1f);
                Vector3 door = routePoint + outward * lateral;
                Vector3 center = door + outward * (RoomDepth * 0.5f);
                float clearance = MinimumRouteClearance(routeSamples, center, outward);
                if (clearance >= RouteClearance)
                    return new CoachPlacement
                    {
                        door = door,
                        center = center,
                        outward = outward,
                        lateral = lateral,
                        minRouteClearance = clearance,
                    };
            }
        }

        throw new InvalidOperationException(
            $"no coach house placement within 30m keeps the required {RouteClearance:0.0}m route clearance");
    }

    static float MinimumRouteClearance(IReadOnlyList<Vector3> routeSamples, Vector3 center, Vector3 outward)
    {
        Vector3 across = Vector3.Cross(Vector3.up, outward).normalized;
        float halfWidth = RoomWidth * 0.5f + WallT;
        float halfDepth = RoomDepth * 0.5f + WallT;
        float minimum = float.PositiveInfinity;
        foreach (Vector3 sample in routeSamples)
        {
            Vector3 delta = sample - center;
            float dx = Mathf.Max(Mathf.Abs(Vector3.Dot(delta, across)) - halfWidth, 0f);
            float dz = Mathf.Max(Mathf.Abs(Vector3.Dot(delta, outward)) - halfDepth, 0f);
            minimum = Mathf.Min(minimum, Mathf.Sqrt(dx * dx + dz * dz));
        }
        return minimum;
    }

    static Vector3 GroundPoint(Vector3 point)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
            point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
        else if (Physics.Raycast(point + Vector3.up * 500f, Vector3.down, out RaycastHit hit, 1000f))
            point.y = hit.point.y;
        return point;
    }

    static void AddClue(Transform interiorParent, GameObject prop, string clueId, string buildingId,
        string verb, string first, string second)
    {
        GmHouseBeginningBuilder.AddInteractable(prop, $"outbuilding-{clueId}", verb, first, second, 3.2f, 10f);
        GmOutbuildingClue clue = prop.AddComponent<GmOutbuildingClue>();
        clue.Configure($"outbuilding-{clueId}", buildingId);
    }

    /// GmHouseBeginningBuilder.BuildRoomShell always uses its own shared plaster/floor materials --
    /// this local carve mirrors its wall/doorway geometry exactly but with the coach house's own
    /// materials, since the shared instance fields it reads (plaster/floor) are private to that class.
    static void BuildRoomShellWithFloorMaterial(Transform parent, string prefix, float centreZ,
        float width, float depth, float height, float doorwayWidth, Material wallMaterial, Material floorMaterial)
    {
        float halfW = width * 0.5f;
        float halfD = depth * 0.5f;
        GmHouseBeginningBuilder.Slab(prefix + "Floor", new Vector3(0f, -WallT * 0.5f, centreZ),
            new Vector3(width + WallT, WallT, depth + WallT), floorMaterial, parent);
        GmHouseBeginningBuilder.Slab(prefix + "Ceiling", new Vector3(0f, height + WallT * 0.5f, centreZ),
            new Vector3(width + WallT, WallT, depth + WallT), wallMaterial, parent);
        GmHouseBeginningBuilder.Slab(prefix + "WestWall", new Vector3(-halfW - WallT * 0.5f, height * 0.5f, centreZ),
            new Vector3(WallT, height, depth + WallT), wallMaterial, parent);
        GmHouseBeginningBuilder.Slab(prefix + "EastWall", new Vector3(halfW + WallT * 0.5f, height * 0.5f, centreZ),
            new Vector3(WallT, height, depth + WallT), wallMaterial, parent);
        GmHouseBeginningBuilder.Slab(prefix + "NorthWall", new Vector3(0f, height * 0.5f, centreZ + halfD + WallT * 0.5f),
            new Vector3(width + WallT, height, WallT), wallMaterial, parent);

        // South wall carries the doorway -- the big coach doors, open, facing back toward the route.
        float side = (width - doorwayWidth) * 0.5f;
        float southZ = centreZ - halfD - WallT * 0.5f;
        GmHouseBeginningBuilder.Slab(prefix + "SouthWallWest",
            new Vector3(-(doorwayWidth * 0.5f + side * 0.5f), height * 0.5f, southZ),
            new Vector3(side, height, WallT), wallMaterial, parent);
        GmHouseBeginningBuilder.Slab(prefix + "SouthWallEast",
            new Vector3(doorwayWidth * 0.5f + side * 0.5f, height * 0.5f, southZ),
            new Vector3(side, height, WallT), wallMaterial, parent);
        float lintelHeight = Mathf.Max(0.2f, height - 3.6f);
        GmHouseBeginningBuilder.Slab(prefix + "Lintel", new Vector3(0f, 3.6f + lintelHeight * 0.5f, southZ),
            new Vector3(doorwayWidth, lintelHeight, WallT), wallMaterial, parent);
    }
}
