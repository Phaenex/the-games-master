using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public static class GmWendOpening
{
    public const string RootName = "GmWendOpening";
    public const string SystemsName = "GmSystems";
    public const string DesignFile = "wend-opening-design.json";
    public const float GateMetres = 18f;
    public const float ChapelMetres = 220f;
    public const float PorchMetres = GmWendRoute.EstateRouteMetres;
    public const int WalkDeckLayer = 30;

    /// The walk deck's object name, shared so the passes that look it up cannot drift from the pass
    /// that builds it. They already had: two of them searched for a 'RouteSurface' that no version of
    /// this builder creates, which made both a permanent no-op that nothing reported.
    public const string WalkDeckName = "RouteWalkDeck";
    const string WalkDeckMeshPath = "Assets/Scenes/Generated/GmWendRouteWalkDeck.asset";
    const string LanternPrefabPath =
        "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Prefabs/SM_Lantern.prefab";
    const float WalkDeckLift = 0.22f;

    static readonly (string id, string verb, float metres, float lateral)[] Pois =
    {
        ("arrival-car", "Examine", 0f, -3.2f),
        ("gate-plaque", "Read", GateMetres, 3.4f),
        ("open-grave", "Examine", 100f, 7f),
        ("stag-plinth", "Examine", 112f, 5f),
        ("weathered-marker", "Read", 132f, 7f),
        ("chapel-door", "Examine", ChapelMetres, -7f),
        ("garden-scarecrow", "Examine", 260f, -7f),
        ("garden-basin", "Examine", 276f, -5f),
        ("garden-shed", "Examine", 294f, -8f),
        ("coach-doors", "Examine", 338f, 8f),
        ("garden-well", "Examine", 308f, -6f),
        ("fallen-marker", "Read", 152f, 6f),
        ("child-marker", "Read", 168f, 8f),
    };

    // Owned, textured evidence for every non-building POI. The earlier fallback primitives were
    // physically harmless but rendered as featureless black cuboids/cylinders in the built-player
    // contact sheet, which made a finished story beat look like grey-box notation.
    static readonly (string id, string asset, float longestMetres)[] PoiProps =
    {
        ("open-grave", "SM_GraveCross_01", 1.35f),
        ("stag-plinth", "SM_SmallCabin_RoofOrnament2", 1.55f),
        ("weathered-marker", "SM_GraveCross_02", 1.05f),
        ("fallen-marker", "SM_GraveCross_03", 0.95f),
        ("child-marker", "SM_GraveCross_01", 0.72f),
        ("garden-scarecrow", "SM_Pugalo", 2.35f),
        ("garden-well", "SM_Well_01", 1.60f),
        ("garden-basin", "SM_Bucket", 0.55f),
        ("garden-shed", "SM_BarnDoor", 2.80f),
        ("coach-doors", "SM_Wood_Door_Set_C", 2.80f),
        ("gate-plaque", "SM_Wood_01", 1.15f),
    };

    public struct Result
    {
        public int anchors;
        public int pois;
        public int collidersRepaired;
        public int renderersDisabled;
        public float routeLength;
    }

    public static Result Build()
    {
        GameObject stale = GameObject.Find(RootName);
        if (stale != null) UnityEngine.Object.DestroyImmediate(stale);
        stale = GameObject.Find(SystemsName);
        if (stale != null) UnityEngine.Object.DestroyImmediate(stale);

        List<Vector3> route = GmWendRoute.BuildEstate(out string report);
        if (route.Count < 2) throw new InvalidOperationException("canonical opening route has fewer than two points");
        List<Vector3> splinePoints = GmWendRoute.Densify(route, 10f)
            .Select(point => GroundPoint(point) + Vector3.up * WalkDeckLift).ToList();

        var root = new GameObject(RootName);
        var spline = root.AddComponent<GmRouteSpline>();
        spline.Configure(splinePoints);
        BuildWalkDeck(root.transform, splinePoints);

        GameObject player = GameObject.Find(GmWendBuilder.PlayerName);
        if (player == null) throw new InvalidOperationException("opening build requires the player");
        player.transform.position = spline.PointAt(0f) + Vector3.up * 0.1f;
        player.transform.rotation = Quaternion.LookRotation(spline.TangentAt(0f), Vector3.up);
        GmPlayer controls = player.GetComponent<GmPlayer>();
        if (controls != null) controls.walkSpeed = GmWendBuilder.WalkSpeed;

        int anchors = 0;
        MakeAnchor(root.transform, "arrival-car", 0f, Offset(spline, 0f, -3.2f)); anchors++;
        MakeAnchor(root.transform, "gate", GateMetres, spline.PointAt(GateMetres)); anchors++;
        MakeAnchor(root.transform, "chapel", ChapelMetres, Offset(spline, ChapelMetres, -7f)); anchors++;
        MakeAnchor(root.transform, "manor-porch", PorchMetres, spline.PointAt(PorchMetres)); anchors++;

        BuildCar(root.transform, spline);
        BuildGate(root.transform, spline);
        BuildManor(root.transform, spline);
        int pois = BuildPois(root.transform, spline, ref anchors);
        AlignChapelAnchorsToRenderedDoor();
        // Requires the coach-doors POI anchor BuildPois just created above.
        GmWendOutbuildings.Build(root.transform, spline);
        GmWendEstateForest.Apply(root.transform, spline);
        GmWendEstateForest.ClearCemeterySightline();
        GmWendEstateForest.ClearChapelSightline(spline);
        GmWendOutbuildings.ClearCoachHouseVegetation();
        GmWendFoliage.Apply();
        BuildStoryAnchors(root.transform, spline, ref anchors);
        BuildWakeRoom(ref anchors);
        GmHouseBeginningBuilder.Build();
        BuildSystems(root.transform);

        int repaired = GmWendColliderRepair.Apply();
        int disabled = GmWendPerformance.Apply(spline);
        var composition = new GameObject("Composition");
        composition.transform.SetParent(root.transform, true);
        GmWendCompositionPlan.Author(composition, spline);
        GmSceneBuildUtility.MarkScene(root, "wend-hill-prologue", "Wend Hill Prologue");

        List<string> anchorIssues = GmWorldAnchor.ValidateScene();
        if (anchorIssues.Count > 0)
            throw new InvalidOperationException($"opening anchor contract failed:\n  - {string.Join("\n  - ", anchorIssues)}");

        Debug.Log($"[GmWendOpening] PASS: route={spline.Length:0}m anchors={anchors} pois={pois} " +
                  $"collidersRepaired={repaired} renderersDisabled={disabled}\n{report}");
        return new Result { anchors = anchors, pois = pois, collidersRepaired = repaired,
            renderersDisabled = disabled, routeLength = spline.Length };
    }

    static void BuildSystems(Transform openingRoot)
    {
        // The purchased/legacy scene may still carry its 18-shot estate tour. A global arm token
        // would start both tours in PlayMode and let the obsolete one fail the canonical run.
        foreach (GmShotTour legacy in UnityEngine.Object.FindObjectsByType<GmShotTour>(FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
            UnityEngine.Object.DestroyImmediate(legacy);
        var systems = new GameObject(SystemsName);
        systems.AddComponent<GmExperienceTelemetry>();
        systems.AddComponent<GmAudioMixController>();
        var design = systems.AddComponent<GmDesignRuntime>();
        design.designFile = DesignFile;
        systems.AddComponent<GmAmbience>();
        var bell = systems.AddComponent<GmBellSummons>();
        bell.ChapelPosition = RequireAnchor("chapel").transform.position + Vector3.up * 6f;
        // Reads the branch-beat/POI signals GmDesignRuntime already tracks; the bell finds this via
        // FindFirstObjectByType in its own Start(), so add order relative to the bell doesn't matter.
        systems.AddComponent<GmGroundsExploration>();
        systems.AddComponent<GmSymptoms>();
        var threshold = systems.AddComponent<GmThreshold>();
        threshold.gateAnchorId = "gate";
        threshold.porchAnchorId = "manor-porch";
        systems.AddComponent<GmCrossing>();
        systems.AddComponent<GmColdOpen>();
        var secret = systems.AddComponent<GmSecretEnding>();
        secret.carAnchorId = "arrival-car";
        secret.gateAnchorId = "gate";
        systems.AddComponent<GmPrologueHud>();
        systems.AddComponent<GmVillageSave>();
        systems.AddComponent<GmWendStoryTour>();
        systems.AddComponent<GmWendRuntimeCulling>();
        systems.AddComponent<GmWendRenderBudget>();

        Volume grade = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate.sharedProfile != null &&
                candidate.sharedProfile.TryGet<ColorAdjustments>(out _));
        if (grade == null) throw new InvalidOperationException("canonical opening has no display-grade volume");
        if (grade.GetComponent<GmDisplayCalibration>() == null) grade.gameObject.AddComponent<GmDisplayCalibration>();
    }

    static void BuildStoryAnchors(Transform parent, GmRouteSpline route, ref int anchors)
    {
        float[] beatMetres = { 45f, 120f, 205f, 300f, 390f };
        for (int i = 0; i < beatMetres.Length; i++)
        {
            MakeAnchor(parent, $"beat-{i + 1:D2}", beatMetres[i], route.PointAt(beatMetres[i]));
            anchors++;
        }
        float[] branchMetres = { 96f, 116f, ChapelMetres, 258f, 280f, 326f, 350f };
        float[] sides = { 8f, 10f, -7f, -8f, -10f, 8f, 10f };
        for (int i = 0; i < branchMetres.Length; i++)
        {
            MakeAnchor(parent, $"branch-{i + 1:D2}", branchMetres[i], Offset(route, branchMetres[i], sides[i]));
            anchors++;
        }
    }

    static int BuildPois(Transform parent, GmRouteSpline route, ref int anchors)
    {
        int count = 0;
        foreach (var poi in Pois)
        {
            Vector3 position = Offset(route, poi.metres, poi.lateral);
            GmWorldAnchor existing = GmWorldAnchor.Find(poi.id);
            GameObject go = existing != null ? existing.gameObject :
                MakeAnchor(parent, poi.id, poi.metres, position).gameObject;
            if (existing == null) anchors++;
            go.name = $"POI_{poi.id}";
            // Keep interaction volumes scene-owned and positive-scale. A box also goes through the
            // same repair/audit path as every other walkable opening collider.
            var trigger = go.GetComponent<BoxCollider>();
            if (trigger == null) trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(3f, 2.2f, 3f);
            trigger.center = Vector3.up * 1.1f;
            var interactable = go.GetComponent<GmInteractable>() ?? go.AddComponent<GmInteractable>();
            interactable.Configure(poi.id, poi.verb, 3.8f, 8f);
            if (poi.id != "arrival-car" && poi.id != "chapel-door") BuildMarker(go.transform, poi.id);
            count++;
        }
        return count;
    }

    static void AlignChapelAnchorsToRenderedDoor()
    {
        GmWorldAnchor chapel = GmWorldAnchor.Find("chapel");
        GmWorldAnchor chapelDoor = GmWorldAnchor.Find("chapel-door");
        if (chapel == null || chapelDoor == null)
            throw new InvalidOperationException("chapel alignment requires both chapel anchors");

        Renderer door = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Where(renderer => renderer.enabled &&
                renderer.name.StartsWith("SM_Church_Door", StringComparison.OrdinalIgnoreCase))
            .OrderBy(renderer => renderer.bounds.SqrDistance(chapel.transform.position))
            .FirstOrDefault();
        if (door == null)
            throw new InvalidOperationException("canonical opening has no rendered SM_Church_Door geometry");

        Vector3 seated = new Vector3(door.bounds.center.x, door.bounds.min.y, door.bounds.center.z);
        chapel.transform.position = seated;
        chapelDoor.transform.position = seated;
        Debug.Log($"[GmWendOpening] chapel anchors seated on '{door.name}' at {seated}");
    }

    static void BuildMarker(Transform parent, string id)
    {
        var entry = PoiProps.FirstOrDefault(candidate => candidate.id == id);
        if (entry.id == null)
            throw new InvalidOperationException($"POI '{id}' has no authored evidence asset");
        GameObject prefab = GmVillageEstate.FindPrefab(entry.asset);
        if (prefab == null)
            throw new InvalidOperationException($"POI '{id}' requires missing owned asset '{entry.asset}'");

        var marker = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        marker.name = $"Prop_{id}_{entry.asset}";
        if (marker.GetComponentsInChildren<Renderer>(true).Length == 0)
            marker = BuildImportedMeshProp(marker, prefab, parent, id, entry.asset);
        marker.transform.SetPositionAndRotation(parent.position,
            Quaternion.Euler(id == "weathered-marker" ? -4f : 0f, StableYaw(id),
                id == "fallen-marker" ? 72f : 0f));

        Bounds bounds = Encapsulate(marker);
        float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (longest <= 0.01f)
            throw new InvalidOperationException($"POI '{id}' asset '{entry.asset}' has no renderable bounds");
        marker.transform.localScale = Vector3.one * (entry.longestMetres / longest);
        bounds = Encapsulate(marker);
        // BuildPois already seated the anchor. Re-raycasting after the prefab exists can hit the
        // prefab's own collider and stack the prop one full prop-height above the ground.
        Vector3 ground = parent.position;
        marker.transform.position += new Vector3(parent.position.x - bounds.center.x,
            ground.y - bounds.min.y, parent.position.z - bounds.center.z);

        foreach (Collider collider in marker.GetComponentsInChildren<Collider>(true))
            UnityEngine.Object.DestroyImmediate(collider);
    }

    static GameObject BuildImportedMeshProp(GameObject emptyInstance, GameObject source,
        Transform parent, string id, string assetName)
    {
        string path = AssetDatabase.GetAssetPath(source);
        Mesh[] meshes = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>()
            .Where(mesh => mesh != null && mesh.vertexCount > 0).ToArray();
        UnityEngine.Object.DestroyImmediate(emptyInstance);
        if (meshes.Length == 0)
            throw new InvalidOperationException($"POI '{id}' asset '{assetName}' has no renderable mesh sub-assets");

        var root = new GameObject($"Prop_{id}_{assetName}");
        root.transform.SetParent(parent, true);
        var material = new Material(Shader.Find("HDRP/Lit")) { name = $"GmEvidence_{id}" };
        material.SetColor("_BaseColor", new Color(0.16f, 0.105f, 0.06f));
        material.SetFloat("_Metallic", 0.2f);
        material.SetFloat("_Smoothness", 0.26f);
        for (int i = 0; i < meshes.Length; i++)
        {
            var part = new GameObject($"Mesh_{i:D2}_{meshes[i].name}");
            part.transform.SetParent(root.transform, false);
            part.AddComponent<MeshFilter>().sharedMesh = meshes[i];
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
        return root;
    }

    static Bounds Encapsulate(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = new Bounds(go.transform.position, Vector3.zero);
        foreach (Renderer renderer in renderers)
        {
            if (renderer.bounds.size.sqrMagnitude <= 0.000001f) continue;
            if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        if (hasBounds) return bounds;

        // MeshRenderers assembled from FBX sub-assets do not publish world bounds until Unity's
        // renderer update, which has not happened during this synchronous editor build. Transform
        // the imported mesh bounds ourselves so placement is deterministic in the same build tick.
        foreach (MeshFilter filter in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || filter.sharedMesh.bounds.size.sqrMagnitude <= 0.000001f) continue;
            Bounds local = filter.sharedMesh.bounds;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = filter.transform.TransformPoint(local.center +
                    Vector3.Scale(local.extents, new Vector3(x, y, z)));
                if (!hasBounds) { bounds = new Bounds(corner, Vector3.zero); hasBounds = true; }
                else bounds.Encapsulate(corner);
            }
        }
        return bounds;
    }

    static int StableYaw(string value)
    {
        unchecked
        {
            int hash = 17;
            foreach (char character in value) hash = hash * 31 + character;
            return Mathf.Abs(hash % 360);
        }
    }

    static void BuildCar(Transform parent, GmRouteSpline route)
    {
        GmWorldAnchor anchor = RequireAnchor("arrival-car");
        GameObject prefab = GmVillageEstate.FindPrefab("RealisticCar03_HD_Exterior_LOD0");
        GameObject car = prefab != null
            ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent)
            : GameObject.CreatePrimitive(PrimitiveType.Cube);
        car.name = "ArrivalCar";
        if (car.transform.parent == null) car.transform.SetParent(parent, true);
        car.transform.position = anchor.transform.position;
        car.transform.rotation = ArrivalCarRotation(route.TangentAt(0f));
        Renderer[] renderers = car.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            // Normalize by the longest dimension. This particular model's imported Y bounds are
            // much smaller than its footprint; height-normalizing turned it into a road-spanning
            // object that swallowed a camera nine metres away. A 4.6m longest side is an ordinary
            // estate car regardless of which local axis the FBX uses as forward.
            float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (longest > 0.01f) car.transform.localScale *= 4.6f / longest;
            bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            Vector3 seated = GroundPoint(anchor.transform.position);
            // This purchased car's mesh pivot is not at its visual centre. Correct all three axes,
            // otherwise the interaction/tour aim at the anchor while the actual car sits mostly off
            // frame and can overlap the route despite its anchor being clear of it.
            car.transform.position += new Vector3(anchor.transform.position.x - bounds.center.x,
                seated.y - bounds.min.y, anchor.transform.position.z - bounds.center.z);
            bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);

            StyleArrivalCar(renderers);

            AddArrivalCarHeadlamps(car.transform, bounds, route.TangentAt(0f));
            AddArrivalCarMoonKey(car.transform, bounds, route.TangentAt(0f));

            Bounds clearance = bounds;
            clearance.Expand(new Vector3(2.5f, 1f, 2.5f));
            foreach (Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                if (renderer.transform.root.name == RootName || renderer.GetComponent<Terrain>() != null ||
                    Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.z) > 15f ||
                    !renderer.bounds.Intersects(clearance)) continue;
                renderer.enabled = false;
                foreach (Collider collider in renderer.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            }
        }
    }

    public static Quaternion ArrivalCarRotation(Vector3 routeForward)
    {
        routeForward.y = 0f;
        if (routeForward.sqrMagnitude < 0.001f)
            throw new ArgumentException("arrival car needs a horizontal route direction", nameof(routeForward));
        // This FBX imports with its wheel side on positive local Y. A conventional world-up
        // LookRotation put all four wheels above the roof. Roll the asset basis once while keeping
        // local forward on the route tangent.
        return Quaternion.LookRotation(-routeForward.normalized, Vector3.down);
    }

    public static void StyleArrivalCar(IEnumerable<Renderer> renderers)
    {
        Shader hdrp = Shader.Find("HDRP/Lit");
        if (hdrp == null) throw new InvalidOperationException("HDRP/Lit is unavailable for the arrival car");
        foreach (Renderer renderer in renderers.Where(candidate => candidate != null))
        {
            Material[] sourceMaterials = renderer.sharedMaterials;
            if (sourceMaterials.Length == 0) sourceMaterials = new Material[] { null };
            var styled = new Material[sourceMaterials.Length];
            for (int slot = 0; slot < sourceMaterials.Length; slot++)
            {
                Material source = sourceMaterials[slot];
                string semantic = (renderer.name + "/" + source?.name).ToLowerInvariant();
                Color color;
                float metallic;
                float smoothness;
                if (semantic.Contains("glass"))
                {
                    color = new Color(0.012f, 0.016f, 0.018f);
                    metallic = 0.05f;
                    smoothness = 0.82f;
                }
                else if (semantic.Contains("grill") || semantic.Contains("grille") ||
                         semantic.Contains("chrome") || semantic.Contains("parts") ||
                         semantic.Contains("metal"))
                {
                    color = new Color(0.17f, 0.145f, 0.105f);
                    metallic = 0.76f;
                    smoothness = 0.54f;
                }
                else if (semantic.Contains("light") || semantic.Contains("lamp"))
                {
                    color = new Color(0.46f, 0.25f, 0.085f);
                    metallic = 0.18f;
                    smoothness = 0.62f;
                }
                else if (semantic.Contains("wheel") || semantic.Contains("tire") ||
                         semantic.Contains("tyre"))
                {
                    color = new Color(0.027f, 0.024f, 0.021f);
                    metallic = 0.08f;
                    smoothness = 0.22f;
                }
                else
                {
                    color = new Color(0.14f, 0.068f, 0.038f);
                    metallic = 0.42f;
                    smoothness = 0.46f;
                }

                Material material = source != null && source.shader == hdrp
                    ? new Material(source)
                    : new Material(hdrp);
                material.name = $"GmArrivalCar_{renderer.name}_{slot}";
                CopyCarTexture(source, material, "_BaseColorMap", "_MainTex");
                CopyCarTexture(source, material, "_NormalMap", "_BumpMap");
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Metallic", metallic);
                material.SetFloat("_Smoothness", smoothness);
                styled[slot] = material;
            }
            renderer.sharedMaterials = styled;
        }
    }

    static void CopyCarTexture(Material source, Material destination, string destinationProperty,
        string legacyProperty)
    {
        if (source == null || !destination.HasProperty(destinationProperty)) return;
        Texture texture = source.HasProperty(destinationProperty) ? source.GetTexture(destinationProperty) : null;
        if (texture == null && source.HasProperty(legacyProperty)) texture = source.GetTexture(legacyProperty);
        if (texture != null) destination.SetTexture(destinationProperty, texture);
    }

    public static void AddArrivalCarHeadlamps(Transform car, Bounds bodyBounds, Vector3 forward)
    {
        if (car == null) throw new ArgumentNullException(nameof(car));
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            throw new ArgumentException("arrival-car headlamps need a horizontal vehicle direction", nameof(forward));
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        float forwardExtent = Mathf.Abs(forward.x) * bodyBounds.extents.x +
                              Mathf.Abs(forward.z) * bodyBounds.extents.z;
        float rightExtent = Mathf.Abs(right.x) * bodyBounds.extents.x +
                            Mathf.Abs(right.z) * bodyBounds.extents.z;
        Vector3 front = bodyBounds.center + forward * Mathf.Max(0.1f, forwardExtent - 0.16f);
        front.y = Mathf.Lerp(bodyBounds.min.y, bodyBounds.max.y, 0.58f);
        Quaternion aim = Quaternion.LookRotation((forward + Vector3.down * 0.035f).normalized, Vector3.up);

        for (int side = -1; side <= 1; side += 2)
        {
            var mount = new GameObject(side < 0 ? "ArrivalHeadlampLeft" : "ArrivalHeadlampRight");
            mount.transform.SetParent(car, true);
            mount.transform.SetPositionAndRotation(front + right * rightExtent * 0.56f * side, aim);
            string fixtureName = side < 0 ? "ArrivalLanternLeftFixture" : "ArrivalLanternRightFixture";
            GmOwnedPropFactory.PlacePrefab(LanternPrefabPath, fixtureName, mount.transform,
                mount.transform.position - forward * 0.04f, new Vector3(0.24f, 0.22f, 0.18f), aim);

            Light light = mount.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(1f, 0.67f, 0.36f);
            light.range = 22f;
            light.spotAngle = 46f;
            light.innerSpotAngle = 29f;
            light.shadows = LightShadows.None;
            var hd = mount.AddComponent<HDAdditionalLightData>();
            hd.lightUnit = LightUnit.Lumen;
            hd.intensity = 780f;
            hd.range = 22f;
            hd.affectsVolumetric = false;
        }
    }

    public static Light AddArrivalCarMoonKey(Transform car, Bounds bodyBounds, Vector3 forward)
    {
        if (car == null) throw new ArgumentNullException(nameof(car));
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            throw new ArgumentException("arrival-car moon key needs a horizontal vehicle direction", nameof(forward));
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        var go = new GameObject("ArrivalMoonlightKey");
        go.transform.SetParent(car, true);
        go.transform.position = bodyBounds.center + forward * 4.2f + right * 3.2f + Vector3.up * 5.4f;
        go.transform.rotation = Quaternion.LookRotation((bodyBounds.center - go.transform.position).normalized,
            Vector3.up);
        Light light = go.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = new Color(0.42f, 0.56f, 1f);
        light.range = 16f;
        light.spotAngle = 68f;
        light.innerSpotAngle = 44f;
        light.shadows = LightShadows.None;
        var hd = go.AddComponent<HDAdditionalLightData>();
        hd.lightUnit = LightUnit.Lumen;
        hd.intensity = 1200f;
        hd.range = 16f;
        hd.affectsVolumetric = false;
        return light;
    }

    static void BuildWalkDeck(Transform parent, IReadOnlyList<Vector3> points)
    {
        // The source pack's road is a set of overlapping display meshes, not a continuous physics
        // surface. One shared-vertex ribbon (rather than separate slabs) keeps the baked polygons
        // connected at every bend and turns the authored spline into an honest physical route.
        var vertices = new Vector3[points.Count * 2];
        var triangles = new int[(points.Count - 1) * 6];
        const float halfWidth = 2.4f;
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 before = points[Mathf.Max(0, i - 1)];
            Vector3 after = points[Mathf.Min(points.Count - 1, i + 1)];
            Vector3 tangent = after - before;
            tangent.y = 0f;
            Vector3 right = Vector3.Cross(Vector3.up, tangent.normalized);
            Vector3 point = points[i];
            vertices[i * 2] = point - right * halfWidth;
            vertices[i * 2 + 1] = point + right * halfWidth;
            if (i == points.Count - 1) continue;
            int t = i * 6;
            int v = i * 2;
            triangles[t] = v;
            triangles[t + 1] = v + 2;
            triangles[t + 2] = v + 1;
            triangles[t + 3] = v + 1;
            triangles[t + 4] = v + 2;
            triangles[t + 5] = v + 3;
        }

        var mesh = new Mesh { name = "GmWendRouteWalkDeck" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        if (!AssetDatabase.IsValidFolder("Assets/Scenes/Generated"))
            AssetDatabase.CreateFolder("Assets/Scenes", "Generated");
        if (AssetDatabase.LoadMainAssetAtPath(WalkDeckMeshPath) != null)
            AssetDatabase.DeleteAsset(WalkDeckMeshPath);
        AssetDatabase.CreateAsset(mesh, WalkDeckMeshPath);

        var deck = new GameObject(WalkDeckName);
        deck.transform.SetParent(parent, true);
        deck.layer = WalkDeckLayer;
        deck.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    static void BuildGate(Transform parent, GmRouteSpline route)
    {
        Vector3 center = route.PointAt(GateMetres);
        Vector3 forward = route.TangentAt(GateMetres);
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        var rig = new GameObject("EstateGate");
        rig.transform.SetParent(parent, true);
        rig.transform.SetPositionAndRotation(center, Quaternion.LookRotation(forward, Vector3.up));
        Material stone = GmVictorianInteriorKit.Surface("mantel", "EstateGateStone", new Vector2(1f, 2f),
            new Color(0.46f, 0.44f, 0.40f));
        BuildGatePierArt(rig.transform, "PierLeft", -2.7f, stone);
        BuildGatePierArt(rig.transform, "PierRight", 2.7f, stone);
        Transform left = MakeLeaf(rig.transform, "LeafLeft", -2.35f, 1f);
        Transform rightLeaf = MakeLeaf(rig.transform, "LeafRight", 2.35f, -1f);

        var barrier = new GameObject("GateBarrier");
        barrier.transform.SetParent(rig.transform, false);
        barrier.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        var barrierCol = barrier.AddComponent<BoxCollider>();
        barrierCol.size = new Vector3(5.2f, 3.3f, 0.4f);
        barrierCol.enabled = false;

        rig.AddComponent<GmGateLeaves>().Configure(left, rightLeaf, 96f, 0.55f, barrierCol);
        MakeGateLantern(rig.transform, new Vector3(-2.7f, 3.45f, 0.15f));
        MakeGateLantern(rig.transform, new Vector3(2.7f, 3.45f, 0.15f));

        Bounds world = GmWendBounds.WorldBounds();
        BuildPerimeterWingArt(rig.transform, -1f, 2.7f,
            PerimeterWingEnd(world, center, rig.transform.right, -1f));
        BuildPerimeterWingArt(rig.transform, 1f, 2.7f,
            PerimeterWingEnd(world, center, rig.transform.right, 1f));
    }

    internal static float PerimeterWingEnd(
        Bounds world, Vector3 gateCenter, Vector3 gateRight, float side)
    {
        if (side != -1f && side != 1f) throw new ArgumentOutOfRangeException(nameof(side));
        Vector3 direction = gateRight.normalized * side;
        if (direction.sqrMagnitude < 0.99f)
            throw new ArgumentException("gate right axis must be non-zero", nameof(gateRight));

        float xDistance = AxisBoundaryDistance(
            gateCenter.x, direction.x, world.min.x, world.max.x);
        float zDistance = AxisBoundaryDistance(
            gateCenter.z, direction.z, world.min.z, world.max.z);
        float distance = Mathf.Min(xDistance, zDistance);
        if (!float.IsFinite(distance) || distance <= 2.7f)
            throw new InvalidOperationException(
                $"estate gate at {gateCenter} cannot connect its {side:+0;-0} wing to world bounds {world}");
        return distance;
    }

    static float AxisBoundaryDistance(float origin, float direction, float min, float max)
    {
        if (Mathf.Abs(direction) < 0.0001f) return float.PositiveInfinity;
        float edge = direction > 0f ? max : min;
        float distance = (edge - origin) / direction;
        return distance > 0f ? distance : float.PositiveInfinity;
    }

    internal static GameObject BuildPerimeterWingArt(Transform parent, float side, float startX, float endX)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));
        if (side != -1f && side != 1f) throw new ArgumentOutOfRangeException(nameof(side));
        if (endX <= startX) throw new ArgumentException("fence wing end must be beyond its start");

        var wing = new GameObject(side < 0f ? "PerimeterWingLeft" : "PerimeterWingRight");
        wing.transform.SetParent(parent, false);
        float span = endX - startX;
        int sections = Mathf.CeilToInt(span / 3.5f);
        float sectionWidth = span / sections;
        var stoneBoxes = new List<Matrix4x4>();
        var ironBoxes = new List<Matrix4x4>();

        for (int i = 0; i < sections; i++)
        {
            float x1 = side * (startX + i * sectionWidth);
            float x2 = side * (startX + (i + 1) * sectionWidth);
            float midX = (x1 + x2) * 0.5f;
            float y1 = SampleFenceGroundY(parent, x1);
            float y2 = SampleFenceGroundY(parent, x2);
            float midY = (y1 + y2) * 0.5f;
            Vector3 sectionAxis = new Vector3(x2 - x1, y2 - y1, 0f);
            float sectionLength = sectionAxis.magnitude;
            Quaternion sectionRotation = Quaternion.FromToRotation(Vector3.right, sectionAxis.normalized);

            stoneBoxes.Add(BoxMatrix(new Vector3(midX, midY + 0.45f, 0f),
                sectionRotation, new Vector3(sectionLength + 0.04f, 0.9f, 0.5f)));
            ironBoxes.Add(BoxMatrix(new Vector3(midX, midY + 2.28f, 0f),
                sectionRotation, new Vector3(sectionLength + 0.04f, 0.10f, 0.10f)));
            ironBoxes.Add(BoxMatrix(new Vector3(midX, midY + 1.35f, 0f),
                sectionRotation, new Vector3(sectionLength + 0.04f, 0.09f, 0.09f)));

            int pickets = Mathf.Max(4, Mathf.FloorToInt(sectionWidth / 0.48f));
            for (int p = 0; p < pickets; p++)
            {
                float px = Mathf.Lerp(x1, x2, (p + 0.5f) / pickets);
                float py = SampleFenceGroundY(parent, px);
                ironBoxes.Add(BoxMatrix(new Vector3(px, py + 1.77f, 0f),
                    Quaternion.identity, new Vector3(0.075f, 1.75f, 0.075f)));
                ironBoxes.Add(BoxMatrix(new Vector3(px, py + 2.68f, 0f),
                    Quaternion.Euler(0f, 0f, 45f), new Vector3(0.13f, 0.22f, 0.13f)));
            }

            if (i % 2 == 1 || i == sections - 1)
            {
                stoneBoxes.Add(BoxMatrix(new Vector3(x2, y2 + 1.15f, 0f),
                    Quaternion.identity, new Vector3(0.48f, 2.3f, 0.48f)));
                stoneBoxes.Add(BoxMatrix(new Vector3(x2, y2 + 2.34f, 0f),
                    Quaternion.Euler(0f, 45f, 0f), new Vector3(0.62f, 0.16f, 0.62f)));
            }

            var collisionSection = new GameObject($"FenceCollision_{i:00}");
            collisionSection.transform.SetParent(wing.transform, false);
            collisionSection.transform.localPosition = new Vector3(midX, midY + 1.35f, 0f);
            collisionSection.transform.localRotation = sectionRotation;
            BoxCollider collision = collisionSection.AddComponent<BoxCollider>();
            collision.size = new Vector3(sectionLength + 0.04f, 2.7f, 0.5f);
        }

        Material stone = GmVictorianInteriorKit.Surface("mantel", "EstateFenceStone", new Vector2(4f, 1f),
            new Color(0.43f, 0.41f, 0.37f));
        Material iron = GmHouseBeginningBuilder.Mat("EstateFenceIron",
            new Color(0.15f, 0.14f, 0.125f), 0.38f, 0.18f);
        CreateCombinedBoxVisual("Stonework", wing.transform, stoneBoxes, stone);
        CreateCombinedBoxVisual("WroughtIron", wing.transform, ironBoxes, iron);

        return wing;
    }

    static float SampleFenceGroundY(Transform gate, float localX)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) return 0f;
        Vector3 world = gate.TransformPoint(new Vector3(localX, 0f, 0f));
        float ground = terrain.SampleHeight(world) + terrain.transform.position.y;
        return gate.InverseTransformPoint(new Vector3(world.x, ground, world.z)).y;
    }

    static void BuildGatePierArt(Transform parent, string name, float x, Material stone)
    {
        var root = new GameObject(name).transform;
        root.SetParent(parent, false);
        root.localPosition = new Vector3(x, 0f, 0f);
        var boxes = new List<Matrix4x4>
        {
            BoxMatrix(new Vector3(0f, 0.18f, 0f), Quaternion.identity, new Vector3(1.05f, 0.36f, 1.05f)),
            BoxMatrix(new Vector3(0f, 1.55f, 0f), Quaternion.identity, new Vector3(0.76f, 2.45f, 0.76f)),
            BoxMatrix(new Vector3(0f, 2.84f, 0f), Quaternion.identity, new Vector3(0.96f, 0.14f, 0.96f)),
            BoxMatrix(new Vector3(0f, 3.12f, 0f), Quaternion.Euler(0f, 45f, 0f), new Vector3(0.42f, 0.42f, 0.42f)),
        };
        CreateCombinedBoxVisual("Masonry", root, boxes, stone);
        BoxCollider collider = root.gameObject.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 1.65f, 0f);
        collider.size = new Vector3(0.8f, 3.3f, 0.8f);
    }

    static void MakeGateLantern(Transform parent, Vector3 localPosition)
    {
        var go = new GameObject("GateLantern");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        GmOwnedPropFactory.PlacePrefab(LanternPrefabPath, "GateLanternFixture", go.transform,
            go.transform.position, new Vector3(0.52f, 0.78f, 0.52f), parent.rotation);
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.48f, 0.18f);
        light.shadows = LightShadows.None;
        var hd = go.AddComponent<HDAdditionalLightData>();
        hd.lightUnit = LightUnit.Lumen;
        hd.intensity = 420f;
        hd.range = 20f;
        hd.affectsVolumetric = false;
    }

    static Transform MakeLeaf(Transform parent, string name, float x, float direction)
    {
        var pivot = new GameObject(name).transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = new Vector3(x, 0f, 0f);
        var boxes = new List<Matrix4x4>
        {
            BoxMatrix(new Vector3(direction * 1.15f, 2.25f, 0f), Quaternion.identity, new Vector3(2.3f, 0.12f, 0.12f)),
            BoxMatrix(new Vector3(direction * 1.15f, 0.45f, 0f), Quaternion.identity, new Vector3(2.3f, 0.12f, 0.12f)),
            BoxMatrix(new Vector3(direction * 1.15f, 1.35f, 0f), Quaternion.Euler(0f, 0f, direction * 38f),
                new Vector3(2.7f, 0.10f, 0.10f)),
        };
        for (int i = 0; i < 6; i++)
        {
            float barX = direction * (0.2f + i * 0.38f);
            boxes.Add(BoxMatrix(new Vector3(barX, 1.38f, 0f), Quaternion.identity,
                new Vector3(0.08f, 2.0f, 0.08f)));
            boxes.Add(BoxMatrix(new Vector3(barX, 2.45f, 0f), Quaternion.Euler(0f, 0f, 45f),
                new Vector3(0.14f, 0.22f, 0.14f)));
        }
        Material iron = GmHouseBeginningBuilder.Mat("EstateGateLeafIron",
            new Color(0.15f, 0.14f, 0.125f), 0.4f, 0.2f);
        CreateCombinedBoxVisual("WroughtLeaf", pivot, boxes, iron);
        return pivot;
    }

    static Matrix4x4 BoxMatrix(Vector3 position, Quaternion rotation, Vector3 size) =>
        Matrix4x4.TRS(position, rotation, size);

    static GameObject CreateCombinedBoxVisual(string name, Transform parent,
        IReadOnlyList<Matrix4x4> transforms, Material material)
    {
        Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        if (cube == null) throw new InvalidOperationException("Unity cube mesh is unavailable for authored gate geometry");
        var combines = new CombineInstance[transforms.Count];
        for (int i = 0; i < transforms.Count; i++)
        {
            combines[i].mesh = cube;
            combines[i].transform = transforms[i];
        }
        var mesh = new Mesh { name = "GmAuthored" + name };
        mesh.indexFormat = transforms.Count * cube.vertexCount > 65535
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.CombineMeshes(combines, true, true, false);
        mesh.RecalculateBounds();

        var visual = new GameObject(name);
        visual.transform.SetParent(parent, false);
        visual.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        return visual;
    }

    static void BuildManor(Transform parent, GmRouteSpline route)
    {
        var assembly = new GameObject("ManorAssembly");
        assembly.transform.SetParent(parent, true);
        GameObject mansion = GmMansion.Build(-58f, assembly.transform);
        if (mansion == null) throw new InvalidOperationException("canonical opening could not build its manor");
        GameObject figure = GmMansion.BuildWindowFigureRig(mansion);
        if (figure != null) figure.transform.SetParent(assembly.transform, true);

        Vector3 target = route.PointAt(PorchMetres);
        Vector3 approach = -route.TangentAt(PorchMetres);
        Quaternion rotation = Quaternion.LookRotation(approach, Vector3.up);
        Vector3 local = mansion.transform.position;
        assembly.transform.rotation = rotation;
        // The spline ends at the porch, not in the middle of the building. Seat the manor one
        // half-depth beyond that endpoint with its facade looking back down the approach.
        assembly.transform.position = target - approach * 8f - rotation * local;
        if (figure != null) figure.transform.SetParent(null, true);
        Vector3 right = Vector3.Cross(Vector3.up, approach).normalized;
        MakeManorLantern(assembly.transform, "PorchLanternLeft",
            target + right * 2.2f - approach * 0.7f + Vector3.up * 2.5f);
        MakeManorLantern(assembly.transform, "PorchLanternRight",
            target - right * 2.2f - approach * 0.7f + Vector3.up * 2.5f);

        Renderer[] manorRenderers = mansion.GetComponentsInChildren<Renderer>(true);
        if (manorRenderers.Length == 0) return;
        Bounds footprint = manorRenderers[0].bounds;
        foreach (Renderer renderer in manorRenderers.Skip(1)) footprint.Encapsulate(renderer.bounds);
        footprint.Expand(new Vector3(2f, 1f, 2f));
        int cleared = 0;
        foreach (Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude,
                     FindObjectsSortMode.None))
        {
            if (renderer.transform.root.name == RootName || renderer.GetComponent<Terrain>() != null ||
                Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.z) > 250f ||
                !renderer.bounds.Intersects(footprint)) continue;
            renderer.enabled = false;
            foreach (Collider collider in renderer.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            cleared++;
        }
        Debug.Log($"[GmWendOpening] cleared {cleared} source renderer(s) from the manor footprint");
    }

    static void MakeManorLantern(Transform parent, string name, Vector3 position)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = position;
        GmOwnedPropFactory.PlacePrefab(LanternPrefabPath, name + "Fixture", go.transform,
            position, new Vector3(0.52f, 0.78f, 0.52f), parent.rotation);
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.47f, 0.2f);
        light.shadows = LightShadows.None;
        var hd = go.AddComponent<HDAdditionalLightData>();
        hd.lightUnit = LightUnit.Lumen;
        hd.intensity = 320f;
        hd.range = 13f;
        hd.affectsVolumetric = false;
    }

    static void BuildWakeRoom(ref int anchors)
    {
        GameObject stale = GameObject.Find("WakeRoom");
        if (stale != null) UnityEngine.Object.DestroyImmediate(stale);
        GmWakeRoom.Build();
        GameObject pose = GameObject.Find("WakeRoom/WakePose");
        if (pose == null) throw new InvalidOperationException("wake room did not create WakePose");
        var anchor = pose.GetComponent<GmWorldAnchor>() ?? pose.AddComponent<GmWorldAnchor>();
        anchor.Configure("wake-pose", PorchMetres);
        anchors++;
    }

    static GmWorldAnchor MakeAnchor(Transform parent, string id, float metres, Vector3 position)
    {
        var go = new GameObject("Anchor_" + id);
        go.transform.SetParent(parent, true);
        go.transform.position = GroundPoint(position);
        var anchor = go.AddComponent<GmWorldAnchor>();
        anchor.Configure(id, metres);
        return anchor;
    }

    static GmWorldAnchor RequireAnchor(string id) => GmWorldAnchor.Find(id) ??
        throw new InvalidOperationException($"required opening anchor '{id}' is missing");

    static Vector3 Offset(GmRouteSpline route, float metres, float lateral)
    {
        Vector3 point = route.PointAt(metres);
        Vector3 right = Vector3.Cross(Vector3.up, route.TangentAt(metres)).normalized;
        return GroundPoint(point + right * lateral);
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

    /// <summary>
    /// The road's physical riding surface height at a route point. BuildWalkDeck places its ribbon
    /// vertices at the route spline's own Y with no added offset, so the spline height already is
    /// the surface height; terrainY is accepted for callers that report clearance against it.
    /// </summary>
    public static float RoadSurfaceY(float routeCenterY, float terrainY) => routeCenterY;
}
