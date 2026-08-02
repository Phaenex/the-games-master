// Deterministic, headless quality gate for the generated Wend Hill estate. This does not attempt to
// replace the HDRP screenshot review; it catches the structural asset/placement failures that can be
// proven without taste: missing authored zones, wrong-pipeline materials, decorative collision,
// blocked authored paths, floating dressing, a second mansion, and forbidden continuous audio.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GmEstateQualityAudit
{
    static readonly string[] RequiredRoots =
    {
        "TerrainBackdrop", "GroundMist", "HorizonMist", "TreeLines", "LivingWoodlandStage", "LivingWoodlandAccent", "FlankGroundcover", "DriveVergeCommunities", "EstateUnderstory", "PorchArrivalCourt", "Estate", "Cemetery",
        "KitchenGarden", "CoachYard", "WalkBounds", "Mansion", "WindowFigureRig"
    };

    static readonly string[] ColliderFreeRoots =
    {
        "TerrainBackdrop", "GroundMist", "HorizonMist", "TreeLines", "LivingWoodlandStage", "LivingWoodlandAccent", "FlankGroundcover", "DriveVergeCommunities", "EstateUnderstory", "PorchArrivalCourt"
    };

    [MenuItem("GamesMaster/Audit Wend Hill Quality")]
    public static void Run()
    {
        GmEstateBuilderV2.Build();
        var issues = ValidateOpenScene();
        if (issues.Count > 0)
        {
            foreach (string issue in issues) Debug.LogError("[GmEstateAudit] FAILED: " + issue);
            return;
        }

        int objects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include).Length;
        int renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include).Length;
        int colliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include).Length;
        int lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include).Length;
        Debug.Log($"[GmEstateAudit] PASS: objects={objects} renderers={renderers} colliders={colliders} " +
                  $"lights={lights} fingerprint={LayoutFingerprint()} — required roots/materials/grounding/paths/audio verified");
    }

    public static void RunSavedScene()
    {
        EditorSceneManager.OpenScene(GmEstateBuilderV2.ScenePath, OpenSceneMode.Single);
        var issues = ValidateOpenScene();
        if (issues.Count > 0)
        {
            foreach (string issue in issues) Debug.LogError("[GmEstateAudit] SAVED FAILED: " + issue);
            return;
        }
        Debug.Log($"[GmEstateAudit] SAVED PASS: zones={UnityEngine.Object.FindObjectsByType<GmCompositionZone>(FindObjectsInactive.Include).Length} " +
            $"clusters={UnityEngine.Object.FindObjectsByType<GmCompositionCluster>(FindObjectsInactive.Include).Length} " +
            $"elements={UnityEngine.Object.FindObjectsByType<GmCompositionElement>(FindObjectsInactive.Include).Length} " +
            $"slots={UnityEngine.Object.FindObjectsByType<GmAdaptiveSlot>(FindObjectsInactive.Include).Length} fingerprint={LayoutFingerprint()}");
    }

    /// <summary>
    /// Durable visual-debug companion to the screenshot tour. A material tweak is meaningless if
    /// the offending pixels belong to a different prop family, so this reports the renderers that
    /// occupy the largest projected area in the three current woodland transfer gates. It is
    /// intentionally evidence only: no scene or asset is modified.
    /// </summary>
    public static void ReportWoodlandShotContributors()
    {
        EditorSceneManager.OpenScene(GmEstateBuilderV2.ScenePath, OpenSceneMode.Single);
        Camera camera = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
        if (camera == null)
        {
            Debug.LogError("[GmEstateAudit] CONTRIBUTOR FAILED: no review camera");
            return;
        }

        var requested = new HashSet<string>(StringComparer.Ordinal)
            { "01-spawn", "03-gate", "05-middrive", "09-gdn-inside", "15-figure-far" };
        foreach (GmReviewShot shot in GmShotTour.AuthoringShots.Where(value => requested.Contains(value.Name)))
        {
            camera.transform.SetPositionAndRotation(shot.Position,
                Quaternion.Euler(shot.Pitch, shot.Yaw + 180f, 0f));
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
            var contributors = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)
                .Where(renderer => renderer.enabled && GeometryUtility.TestPlanesAABB(planes, renderer.bounds))
                .Select(renderer => new
                {
                    Renderer = renderer,
                    Coverage = ProjectedCoverage(camera, renderer.bounds),
                    Distance = Vector3.Distance(camera.transform.position, renderer.bounds.center),
                })
                .Where(value => value.Coverage > 0.0002f)
                .OrderByDescending(value => value.Coverage)
                .ThenBy(value => value.Distance)
                // Keep enough low-ground contributors to diagnose small but glaring reflective
                // artifacts. A 2% screen-space puddle can be more damaging than a 30% tree crown.
                .Take(140)
                .ToArray();

            Debug.Log($"[GmEstateAudit] CONTRIBUTORS {shot.Name} count={contributors.Length}");
            foreach (var value in contributors)
            {
                string materials = string.Join(",", value.Renderer.sharedMaterials
                    .Where(material => material != null)
                    .Select(material => $"{material.name}@{AssetDatabase.GetAssetPath(material)}"));
                Debug.Log($"[GmEstateAudit] CONTRIBUTOR shot={shot.Name} coverage={value.Coverage:P1} " +
                    $"distance={value.Distance:F1} path={HierarchyPath(value.Renderer.transform)} " +
                    $"materials=[{materials}]");
            }
        }
    }

    static float ProjectedCoverage(Camera camera, Bounds bounds)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector3[] corners =
        {
            new(min.x, min.y, min.z), new(max.x, min.y, min.z),
            new(min.x, max.y, min.z), new(max.x, max.y, min.z),
            new(min.x, min.y, max.z), new(max.x, min.y, max.z),
            new(min.x, max.y, max.z), new(max.x, max.y, max.z),
        };
        float minX = 1f, minY = 1f, maxX = 0f, maxY = 0f;
        bool visible = false;
        foreach (Vector3 corner in corners)
        {
            Vector3 viewport = camera.WorldToViewportPoint(corner);
            if (viewport.z <= 0f) continue;
            visible = true;
            minX = Mathf.Min(minX, Mathf.Clamp01(viewport.x));
            minY = Mathf.Min(minY, Mathf.Clamp01(viewport.y));
            maxX = Mathf.Max(maxX, Mathf.Clamp01(viewport.x));
            maxY = Mathf.Max(maxY, Mathf.Clamp01(viewport.y));
        }
        return visible ? Mathf.Max(0f, maxX - minX) * Mathf.Max(0f, maxY - minY) : 0f;
    }

    static string HierarchyPath(Transform transform)
    {
        var names = new List<string>();
        for (Transform current = transform; current != null; current = current.parent)
            names.Add(current.name);
        names.Reverse();
        return string.Join("/", names);
    }

    public static List<string> ValidateOpenScene()
    {
        var issues = GmSceneContractAudit.ValidateOpenScene(
            GmEstateBuilderV2.SceneId,
            GmEstateBuilderV2.DisplayName,
            GmEstateBuilderV2.ScenePath,
            new[] { "GmSystems", "Player" });
        foreach (string root in RequiredRoots)
            if (FindRoot(root) == null) issues.Add($"required scene root '{root}' is missing");

        ValidateSingleMansion(issues);
        ValidateNaturalEstate(issues);
        ValidateMaterials(issues);
        ValidateDecorativeCollision(issues);
        ValidateGroundedChildren("Cemetery", issues);
        ValidateGroundedChildren("KitchenGarden", issues);
        ValidateGroundedChildren("CoachYard", issues);
        ValidateCemeteryMarkerVariation(issues);
        ValidateClearPath("Cemetery", new Vector2(9.4f, 28.5f), new Vector2(23.3f, 28.5f), 0.72f, issues);
        ValidateClearPath("Cemetery", new Vector2(16.2f, 16.5f), new Vector2(16.2f, 27.5f), 0.72f, issues);
        ValidateClearPath("KitchenGarden", new Vector2(-22.0f, 22.1f), new Vector2(-10.0f, 22.1f), 0.72f, issues);
        // The dropped bucket marks the lane's entrance, so test from its far edge rather than
        // treating the authored hand prop itself as an obstruction.
        ValidateClearPath("KitchenGarden", new Vector2(-17.75f, 26.25f), new Vector2(-15.75f, 28.0f), 0.55f, issues);
        ValidateCoachActionComposition(issues);
        ValidateGardenBeds(issues);
        ValidateArrivalPresentation(issues);
        ValidateExteriorAudio(issues);
        var compositionTour = UnityEngine.Object.FindAnyObjectByType<GmShotTour>();
        var reviewCamera = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
        issues.AddRange(GmSceneCompositionAudit.ValidateOpenScene(
            GmEstateBuilderV2.SceneId, compositionTour, reviewCamera));
        return issues;
    }

    static void ValidateCoachActionComposition(List<string> issues)
    {
        GameObject cart = GameObject.Find("estateCart (SM_Cart)");
        GameObject loading = GameObject.Find("LoadingCluster");
        if (cart == null || loading == null)
        {
            issues.Add("coach action composition is missing its cart or loading cluster");
            return;
        }

        Renderer[] cartRenderers = cart.GetComponentsInChildren<Renderer>(true);
        Renderer[] loadingRenderers = loading.GetComponentsInChildren<Renderer>(true);
        if (cartRenderers.Length == 0 || loadingRenderers.Length == 0)
        {
            issues.Add("coach action composition has no rendered cart/loading evidence");
            return;
        }

        Bounds cartBounds = cartRenderers[0].bounds;
        foreach (Renderer renderer in cartRenderers.Skip(1)) cartBounds.Encapsulate(renderer.bounds);
        Bounds loadingBounds = loadingRenderers[0].bounds;
        foreach (Renderer renderer in loadingRenderers.Skip(1)) loadingBounds.Encapsulate(renderer.bounds);
        float separation = Vector2.Distance(new Vector2(cartBounds.center.x, cartBounds.center.z),
            new Vector2(loadingBounds.center.x, loadingBounds.center.z));
        if (separation > 4.0f)
            issues.Add($"coach cart and loading threshold are {separation:F1}m apart; they read as separate prop islands");

        GameObject serviceWindow = GameObject.Find("CoachServiceWindowDepth");
        if (serviceWindow == null || serviceWindow.transform.Find("ServiceWindowBack") == null ||
            serviceWindow.transform.Find("ServiceWindowSideReveal") == null ||
            serviceWindow.transform.Find("ServiceWindowSillReveal") == null)
            issues.Add("coach loading cluster lacks an aligned service-window back, jamb or sill return");
        else
        {
            Renderer back = serviceWindow.transform.Find("ServiceWindowBack").GetComponent<Renderer>();
            float depth = back == null ? 0f : HorizontalDistance(serviceWindow.transform.position, back.bounds.center);
            if (back == null || back.bounds.size.y < 1.20f || depth < 0.30f || depth > 0.52f)
                issues.Add($"coach service-window depth regressed: depth={depth:F2}m");
            if (serviceWindow.GetComponentsInChildren<Collider>(true).Length != 0)
                issues.Add("coach service-window reveal gained decorative collision");
        }
    }

    static void ValidateCemeteryMarkerVariation(List<string> issues)
    {
        GameObject cemetery = FindRoot("Cemetery");
        if (cemetery == null) return;
        int plots = 0, timberCrosses = 0, arches = 0, obelisks = 0, chests = 0, slabs = 0;
        foreach (Transform child in cemetery.transform)
        {
            if (!child.name.StartsWith("SM_GraveCross_Plot_", StringComparison.Ordinal)) continue;
            plots++;
            if (child.Find("RoundedCrown") != null) arches++;
            else if (child.Find("BrokenObeliskShaft") != null) obelisks++;
            else if (child.Find("ChestBody") != null) chests++;
            else if (child.Find("LeaningNameSlab") != null) slabs++;
            else timberCrosses++;
        }
        if (plots != 17)
            issues.Add($"cemetery has {plots} explicit family markers instead of 17");
        if (timberCrosses > 10 || arches < 2 || obelisks < 2 || chests < 2 || slabs < 1)
            issues.Add($"cemetery marker hierarchy regressed: crosses={timberCrosses}, arches={arches}, obelisks={obelisks}, chests={chests}, slabs={slabs}");
        int materiallyLeaning = 0, collapsed = 0;
        foreach (Transform child in cemetery.transform)
        {
            if (!child.name.StartsWith("SM_GraveCross_Plot_", StringComparison.Ordinal)) continue;
            if (child.Find("RoundedCrown") != null || child.Find("BrokenObeliskShaft") != null ||
                child.Find("ChestBody") != null || child.Find("LeaningNameSlab") != null) continue;
            float xLean = Mathf.Abs(Mathf.DeltaAngle(0f, child.eulerAngles.x));
            float zLean = Mathf.Abs(Mathf.DeltaAngle(0f, child.eulerAngles.z));
            if (Mathf.Max(xLean, zLean) >= 12f) materiallyLeaning++;
            if (xLean >= 55f) collapsed++;
        }
        if (materiallyLeaning < 5 || collapsed < 2)
            issues.Add($"cemetery timber age states flattened: leaning={materiallyLeaning}, collapsed={collapsed}");

        GameObject graveVisual = GameObject.Find("OpenGraveVisual");
        Transform innerCut = graveVisual == null ? null : graveVisual.transform.Find("OpenGraveInnerCut");
        Transform rimStory = graveVisual == null ? null : graveVisual.transform.Find("OpenGraveRimStory");
        MeshFilter cutMesh = innerCut == null ? null : innerCut.GetComponent<MeshFilter>();
        if (innerCut == null || cutMesh == null || cutMesh.sharedMesh == null ||
            cutMesh.sharedMesh.vertexCount != 16 ||
            innerCut.GetComponent<Renderer>().bounds.size.y < 0.09f)
            issues.Add("open grave lost its four-wall interior soil cut and reads as a flat void");
        if (rimStory == null || rimStory.childCount != 4 ||
            rimStory.GetComponentsInChildren<Collider>(true).Length != 0)
            issues.Add("open grave rim lost its restrained three-spoil-plus-shovel action story");
    }

    /// Counts cannot prove good art, but they can prevent the exact structural regression Nick
    /// caught: one flat material, evenly spaced isolated trees, clean rectangular room edges and a
    /// continuous fence corridor. Screenshot review still decides whether the result looks natural.
    static void ValidateNaturalEstate(List<string> issues)
    {
        GameObject ground = FindRoot("Ground");
        Terrain terrain = ground == null ? null : ground.GetComponent<Terrain>();
        if (terrain == null || terrain.terrainData == null)
        {
            issues.Add("estate ground is not the authored multi-layer TerrainData base");
        }
        else
        {
            string dataPath = AssetDatabase.GetAssetPath(terrain.terrainData);
            if (dataPath != "Assets/Scenes/WendHill_AuthoredTerrain.asset")
                issues.Add($"estate terrain points at '{dataPath}' instead of its isolated Wend Hill clone");
            TerrainLayer[] layers = terrain.terrainData.terrainLayers;
            int distinctTextures = new HashSet<Texture>(Array.ConvertAll(layers,
                layer => layer == null ? null : layer.diffuseTexture)).Count(texture => texture != null);
            if (layers.Length < 3 || distinctTextures < 3)
                issues.Add($"estate terrain has {layers.Length} layer(s) and {distinctTextures} distinct diffuse texture(s); need three");
            int normalMappedLayers = layers.Count(layer => layer != null && layer.normalMapTexture != null);
            if (normalMappedLayers < 3)
                issues.Add($"estate terrain has only {normalMappedLayers} normal-mapped layers; open ground will read flat under moonlight");
            int warmEarthLayers = layers.Count(layer => layer != null &&
                layer.diffuseRemapMax.x > layer.diffuseRemapMax.z * 1.8f);
            if (warmEarthLayers < 2)
                issues.Add($"estate terrain has only {warmEarthLayers} warm-earth layers; acreage risks returning to blue-grey snow");
            if (layers.Length >= 4)
            {
                float[,,] paint = terrain.terrainData.GetAlphamaps(0, 0,
                    terrain.terrainData.alphamapWidth, terrain.terrainData.alphamapHeight);
                for (int layer = 0; layer < 4; layer++)
                {
                    float maximum = 0f;
                    for (int z = 0; z < terrain.terrainData.alphamapHeight; z += 8)
                        for (int x = 0; x < terrain.terrainData.alphamapWidth; x += 8)
                            maximum = Mathf.Max(maximum, paint[z, x, layer]);
                    if (maximum < 0.02f)
                        issues.Add($"estate terrain layer {layer + 1} exists but is unpainted (sample max {maximum:F3})");
                }
            }
            float[] samples =
            {
                GmExteriorTerrainComposer.GroundHeight(-48f, -14f),
                GmExteriorTerrainComposer.GroundHeight(46f, 8f),
                GmExteriorTerrainComposer.GroundHeight(-38f, 69f),
                GmExteriorTerrainComposer.GroundHeight(42f, 61f),
                GmExteriorTerrainComposer.GroundHeight(0f, 8f),
            };
            if (samples.Max() - samples.Min() < 0.30f)
                issues.Add($"estate terrain relief is only {samples.Max() - samples.Min():F2}m across flank samples");
            ValidateEstateTerrainFoliage(terrain, issues);
        }

        GameObject livingWoodland = FindRoot("LivingWoodlandStage");
        if (livingWoodland != null)
        {
            int trees = livingWoodland.transform.childCount;
            int snags = livingWoodland.transform.Cast<Transform>()
                .Count(child => child.name.IndexOf("snag", StringComparison.OrdinalIgnoreCase) >= 0);
            if (trees < 145 || trees > 240)
                issues.Add($"estate living woodland has {trees} trees; expected 145..240");
            if (snags < 8 || snags > 22)
                issues.Add($"estate living woodland has {snags} snags; expected 8..22");
            int verifiedFoliageMaterials = 0;
            foreach (Material material in livingWoodland.GetComponentsInChildren<Renderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials).Where(material => material != null)
                .Distinct())
            {
                if (material.HasProperty("_TransmissionEnable") &&
                    material.GetFloat("_TransmissionEnable") > 0.001f)
                    issues.Add($"living woodland material '{material.name}' retains donor transmission");
                if (material.HasProperty("_EmissiveColor") &&
                    material.GetColor("_EmissiveColor").maxColorComponent > 0.001f)
                    issues.Add($"living woodland material '{material.name}' retains emissive colour");
                Texture baseTexture = material.HasProperty("_BaseColorMap")
                    ? material.GetTexture("_BaseColorMap")
                    : material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
                bool pineFoliage = baseTexture != null && baseTexture.name.IndexOf(
                    "PineBranches", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!pineFoliage) continue;
                verifiedFoliageMaterials++;
                if (material.shader == null || material.shader.name != "HDRP/Lit")
                    issues.Add($"living woodland foliage '{material.name}' uses '{material.shader?.name ?? "<null>"}' instead of the reviewed clean cutout shader");
                if (!material.HasProperty("_AlphaCutoffEnable") ||
                    material.GetFloat("_AlphaCutoffEnable") < 0.5f)
                    issues.Add($"living woodland foliage '{material.name}' is not alpha clipped");
                if (material.HasProperty("_ReceivesSSR") && material.GetFloat("_ReceivesSSR") > 0.001f)
                    issues.Add($"living woodland foliage '{material.name}' receives screen-space reflections");
                if (!material.HasProperty("_Smoothness") || material.GetFloat("_Smoothness") > 0.08f)
                    issues.Add($"living woodland foliage '{material.name}' smoothness is not safely matte");
                if (material.HasProperty("_NormalMap") && material.GetTexture("_NormalMap") != null)
                    issues.Add($"living woodland foliage '{material.name}' reintroduced the rejected silver-shell normal path");
            }
            if (verifiedFoliageMaterials < 3)
                issues.Add($"controlled living woodland exposes only {verifiedFoliageMaterials} verified foliage materials; expected at least three calibrated cohorts");
            GmCanopySway[] sways = livingWoodland.GetComponentsInChildren<GmCanopySway>(true);
            int expectedSways = trees - snags;
            if (sways.Length != expectedSways)
                issues.Add($"controlled living woodland has {sways.Length} sway components for {expectedSways} living trees");
            foreach (GmCanopySway sway in sways)
            {
                if (sway.AmplitudeDegrees < 0.05f || sway.MaximumAngleDegrees > 0.24f ||
                    sway.PrimaryFrequencyHz < 0.035f || sway.PrimaryFrequencyHz > 0.11f)
                    issues.Add($"living woodland sway '{sway.name}' exceeds restrained motion bounds");
                Vector2 sample0 = sway.EvaluateOffsetDegrees(0f);
                float motion = Mathf.Max(
                    (sample0 - sway.EvaluateOffsetDegrees(1.13f)).sqrMagnitude,
                    Mathf.Max(
                        (sample0 - sway.EvaluateOffsetDegrees(2.71f)).sqrMagnitude,
                        (sample0 - sway.EvaluateOffsetDegrees(4.93f)).sqrMagnitude));
                if (motion < 0.0001f)
                    issues.Add($"living woodland sway '{sway.name}' does not produce measurable deterministic motion");
            }
        }

        GameObject livingExpansion = FindRoot("LivingWoodlandAccent");
        if (livingExpansion != null)
        {
            int trees = livingExpansion.transform.childCount;
            if (trees != 11)
                issues.Add($"living woodland accent has {trees} trees; expected eleven exact horizon/reveal trees");
            int colliders = livingExpansion.GetComponentsInChildren<Collider>(true).Length;
            if (colliders > 0)
                issues.Add($"living woodland accent carries {colliders} decorative collider(s)");
            GmCanopySway[] sways = livingExpansion.GetComponentsInChildren<GmCanopySway>(true);
            if (sways.Length != trees)
                issues.Add($"living woodland accent has {sways.Length} sway components for {trees} living trees");
            foreach (GmCanopySway sway in sways)
            {
                if (sway.AmplitudeDegrees < 0.05f || sway.MaximumAngleDegrees > 0.24f ||
                    sway.PrimaryFrequencyHz < 0.035f || sway.PrimaryFrequencyHz > 0.11f)
                    issues.Add($"living accent sway '{sway.name}' exceeds restrained motion bounds");
            }
        }

        GameObject ecotone = GameObject.Find("RoomEdgeEcology");
        if (ecotone == null || ecotone.transform.childCount < 27)
            issues.Add($"room-edge ecology has {(ecotone == null ? 0 : ecotone.transform.childCount)} anchors; garden/cemetery rectangles will read as clean rooms");
        GameObject groundcover = FindRoot("FlankGroundcover");
        if (groundcover == null || groundcover.transform.childCount < 150)
            issues.Add($"flank ecology has {(groundcover == null ? 0 : groundcover.transform.childCount)} members; authored communities are missing");
        GameObject verge = FindRoot("DriveVergeCommunities");
        if (verge == null || verge.transform.childCount < 100)
            issues.Add($"drive verge has {(verge == null ? 0 : verge.transform.childCount)} members; route still jumps from floor to tree line");
        GameObject understory = FindRoot("EstateUnderstory");
        int understoryRenderers = understory == null ? 0 : understory.GetComponentsInChildren<Renderer>(true).Length;
        if (understoryRenderers < 650)
            issues.Add($"estate understory has {understoryRenderers} renderer(s); player-height views will return to bare terrain and isolated props");
        if (understory != null)
        {
            Transform road = understory.transform.Find("RoadsideUnderstory");
            Transform cemetery = understory.transform.Find("CemeteryUnderstory");
            Transform garden = understory.transform.Find("GardenUnderstory");
            if (road == null || road.childCount < 420)
                issues.Add($"roadside understory has {(road == null ? 0 : road.childCount)} clumps; avenue edges lack a foreground vegetation layer");
            if (cemetery == null || cemetery.childCount < 145)
                issues.Add($"cemetery understory has {(cemetery == null ? 0 : cemetery.childCount)} clumps; grave plots return to a bare prop room");
            if (garden == null || garden.childCount < 90)
                issues.Add($"garden understory has {(garden == null ? 0 : garden.childCount)} clumps; failed beds lack an ecological margin");
        }

        GameObject stag = GameObject.Find("BrokenStagPlinth");
        if (stag != null)
        {
            Renderer[] renderers = stag.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) issues.Add("broken-stag memorial has no rendered geometry");
            else
            {
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                float cemeteryGround = GmExteriorTerrainComposer.GroundHeight(18f, 20f);
                if (Mathf.Abs(bounds.min.y - cemeteryGround) > 0.08f)
                    issues.Add($"broken-stag memorial floats {bounds.min.y - cemeteryGround:F2}m above cemetery ground");
                if (bounds.size.y > 2.1f || bounds.size.x > 2.0f)
                    issues.Add($"broken-stag memorial bounds {bounds.size} exceed the reviewed compact hero envelope");

                Transform crest = stag.transform.Find("GroundedAngledMemorial/StagHead");
                Renderer[] crestRenderers = crest == null
                    ? Array.Empty<Renderer>()
                    : crest.GetComponentsInChildren<Renderer>(true);
                if (crestRenderers.Length == 0)
                    issues.Add("broken-stag memorial is missing its required heraldic relief");
                else
                {
                    Bounds crestBounds = crestRenderers[0].bounds;
                    foreach (Renderer renderer in crestRenderers.Skip(1)) crestBounds.Encapsulate(renderer.bounds);
                    if (crestBounds.center.y > cemeteryGround + 0.95f)
                        issues.Add($"stag relief center {crestBounds.center.y - cemeteryGround:F2}m above ground still reads as a trophy topper, not an embedded funerary crest");
                }
            }
        }

        GameObject fence = FindRoot("FenceRun");
        if (fence != null)
        {
            if (fence.transform.childCount > 48)
                issues.Add($"drive fence has {fence.transform.childCount} pieces and risks returning to a continuous corridor wall");
            foreach (bool left in new[] { true, false })
            {
                float[] zs = fence.transform.Cast<Transform>()
                    .Where(child => left ? child.position.x < 0f : child.position.x > 0f)
                    .Select(child => child.position.z).OrderBy(value => value).ToArray();
                int longGaps = 0;
                for (int i = 1; i < zs.Length; i++) if (zs[i] - zs[i - 1] >= 8f) longGaps++;
                if (longGaps < 4)
                    issues.Add($"{(left ? "west" : "east")} drive boundary has only {longGaps} long ecological gaps; it reads as a corridor");
            }
        }
    }

    static void ValidateEstateTerrainFoliage(Terrain terrain, List<string> issues)
    {
        TerrainData data = terrain.terrainData;
        foreach (string issue in GmTerrainPrototypeAudit.Validate(terrain, 50))
            issues.Add(issue);
        string[] allowedPaths = {
            "Assets/Scenes/WendHill_EstateFoliage_GrassWild.prefab",
            "Assets/Scenes/WendHill_EstateFoliage_GrassWheat.prefab",
            "Assets/Scenes/WendHill_EstateFoliage_Reed01.prefab",
            "Assets/Scenes/WendHill_EstateFoliage_Reed02.prefab",
            "Assets/Scenes/WendHill_EstateFoliage_Reed03.prefab",
            "Assets/Scenes/WendHill_EstateFoliage_Reed04.prefab",
        };
        var allowed = new HashSet<string>(allowedPaths, StringComparer.Ordinal);
        if (data.treePrototypes.Length != allowedPaths.Length)
            issues.Add($"estate terrain foliage has {data.treePrototypes.Length} prototypes; expected six calibrated sources");
        for (int prototypeIndex = 0; prototypeIndex < data.treePrototypes.Length; prototypeIndex++)
        {
            TreePrototype prototype = data.treePrototypes[prototypeIndex];
            string path = prototype.prefab == null ? "<null>" : AssetDatabase.GetAssetPath(prototype.prefab);
            if (!allowed.Contains(path))
                issues.Add($"estate terrain foliage uses unapproved prototype '{path}'");
            Material material = prototype.prefab?.GetComponentInChildren<Renderer>(true)?.sharedMaterial;
            if (material == null)
                issues.Add($"estate terrain foliage prototype '{path}' has no material");
            else
            {
                if (prototypeIndex >= 2 && material.shader?.name != "HDRP/Lit")
                    issues.Add($"estate reed foliage '{material.name}' uses '{material.shader?.name}' instead of the calibrated HDRP/Lit cutout");
                if (prototypeIndex >= 2 && (!material.HasProperty("_AlphaCutoffEnable") ||
                    material.GetFloat("_AlphaCutoffEnable") < 0.5f))
                    issues.Add($"estate reed foliage '{material.name}' is not alpha-cut and may render as a rectangular sheet");
                if (material.HasProperty("_Albedo_Intensity") &&
                    material.GetFloat("_Albedo_Intensity") > 0.44f)
                    issues.Add($"estate terrain foliage '{material.name}' exceeds night albedo intensity");
                if (material.HasProperty("_Smoothness") && material.GetFloat("_Smoothness") > 0.04f)
                    issues.Add($"estate terrain foliage '{material.name}' is too smooth for dry groundcover");
                if (material.HasProperty("_EmissiveColor") &&
                    material.GetColor("_EmissiveColor").maxColorComponent > 0.001f)
                    issues.Add($"estate terrain foliage '{material.name}' retains emission");
            }
        }

        TreeInstance[] instances = data.treeInstances;
        if (instances.Length < 7000 || instances.Length > 8500)
            issues.Add($"estate terrain foliage has {instances.Length} instances; expected 7000..8500 after repetition thinning");
        bool reserveFailure = false;
        int left = 0, right = 0, verge = 0;
        int arrival = 0, upper = 0, middle = 0, lower = 0;
        int cemetery = 0, garden = 0, porch = 0, workYards = 0;
        int tall = 0, cemeteryWild = 0, cemeteryTall = 0, gardenWheat = 0, gardenTall = 0;
        const float gridMinX = -60f, gridMinZ = -78f, gridStep = 12f;
        const int gridX = 10, gridZ = 15;
        var occupiedCells = new HashSet<int>();
        foreach (TreeInstance instance in instances)
        {
            float x = terrain.transform.position.x + instance.position.x * data.size.x;
            float z = terrain.transform.position.z + instance.position.z * data.size.z;
            if (GmExteriorTerrainComposer.IsEstateFoliageReservedAt(x, z)) reserveFailure = true;
            if (x < 0f) left++; else right++;
            if (Mathf.Abs(x - GmExteriorTerrainComposer.DriveCentreAt(z)) < 8.5f) verge++;
            if (z >= 62f) arrival++;
            else if (z >= 30f) upper++;
            else if (z >= 0f) middle++;
            else lower++;
            bool inCemetery = x > 7.8f && x < 25.4f && z > 11.8f && z < 41.5f;
            bool inGarden = x > -24.5f && x < -7.7f && z > 14.8f && z < 35.6f;
            if (inCemetery) cemetery++;
            if (inGarden) garden++;
            if (instance.prototypeIndex >= 2) tall++;
            if (inCemetery && instance.prototypeIndex == 0) cemeteryWild++;
            if (inCemetery && instance.prototypeIndex >= 2) cemeteryTall++;
            if (inGarden && instance.prototypeIndex == 1) gardenWheat++;
            if (inGarden && instance.prototypeIndex >= 2) gardenTall++;
            if (Mathf.Abs(x) < 19f && z > -67f && z < -36f) porch++;
            if ((x < -18f && x > -43f && z > 35f && z < 61f) ||
                (x > 22f && x < 44f && z > 18f && z < 43f)) workYards++;
            int cellX = Mathf.FloorToInt((x - gridMinX) / gridStep);
            int cellZ = Mathf.FloorToInt((z - gridMinZ) / gridStep);
            if (cellX >= 0 && cellX < gridX && cellZ >= 0 && cellZ < gridZ)
                occupiedCells.Add(cellZ * gridX + cellX);
        }
        if (reserveFailure)
            issues.Add("estate terrain foliage entered a protected tread, room path or structure footprint");
        if (Mathf.Min(left, right) < 2500)
            issues.Add($"terrain ecology is laterally unbalanced: left={left}, right={right}");
        if (verge < 500)
            issues.Add($"estate terrain has only {verge} verge instances; the route lacks continuous edge ecology");
        if (arrival < 700 || upper < 700 || middle < 700 || lower < 1200)
            issues.Add($"terrain ecology has an empty walk band: arrival={arrival}, upper={upper}, middle={middle}, lower={lower}");
        if (cemetery < 70 || garden < 55 || porch < 65 || workYards < 110)
            issues.Add($"story-room terrain ecology is incomplete: cemetery={cemetery}, garden={garden}, porch={porch}, workYards={workYards}");
        if (tall > instances.Length * 0.18f)
            issues.Add($"dominant reed silhouettes returned: tall={tall}/{instances.Length}");
        if (cemetery > 0 && (cemeteryWild < cemetery * 0.55f || cemeteryTall > cemetery * 0.14f))
            issues.Add($"cemetery lost its low wild-turf cohort: wild={cemeteryWild}, tall={cemeteryTall}, total={cemetery}");
        if (garden > 0 && (gardenWheat < garden * 0.50f || gardenTall > garden * 0.18f))
            issues.Add($"garden lost its failed-grain cohort: wheat={gardenWheat}, tall={gardenTall}, total={garden}");
        int eligibleCells = 0, coveredCells = 0;
        for (int iz = 0; iz < gridZ; iz++)
            for (int ix = 0; ix < gridX; ix++)
            {
                float x = gridMinX + (ix + 0.5f) * gridStep;
                float z = gridMinZ + (iz + 0.5f) * gridStep;
                if (GmExteriorTerrainComposer.IsEstateFoliageReservedAt(x, z)) continue;
                eligibleCells++;
                if (occupiedCells.Contains(iz * gridX + ix)) coveredCells++;
            }
        float coverage = eligibleCells == 0 ? 0f : coveredCells / (float)eligibleCells;
        if (coverage < 0.78f)
            issues.Add($"estate terrain ecology covers only {coveredCells}/{eligibleCells} ({coverage:P0}) twelve-metre habitat cells");
    }

    static void ValidateGardenBeds(List<string> issues)
    {
        var garden = FindRoot("KitchenGarden");
        if (garden == null) return;
        var beds = garden.transform.Find("DeadBeds");
        if (beds == null) { issues.Add("kitchen garden has no 'DeadBeds' composition root"); return; }
        int renderers = beds.GetComponentsInChildren<Renderer>(true).Length;
        int colliders = beds.GetComponentsInChildren<Collider>(true).Length;
        if (renderers < 18) issues.Add($"kitchen garden dead beds have only {renderers} renderers (need at least 18 at night exposure)");
        if (colliders > 0) issues.Add($"kitchen garden dead beds carry {colliders} decorative collider(s)");
        if (garden.transform.Find("GardenSoil") != null) issues.Add("rejected monolithic 'GardenSoil' slab returned");
        int sparseBed = 0, denseBed = 0;
        foreach (Transform child in beds)
        {
            Renderer renderer = child.GetComponentInChildren<Renderer>(true);
            if (renderer == null) continue;
            float x = renderer.bounds.center.x;
            if (Mathf.Abs(x + 18.35f) < 0.9f) sparseBed++;
            if (Mathf.Abs(x + 12.85f) < 0.9f) denseBed++;
        }
        if (denseBed < sparseBed + 2)
            issues.Add($"garden bed hierarchy flattened: dense row={denseBed}, abandoned row={sparseBed}");
        var bandHeights = new List<float>();
        foreach (Transform child in beds)
        {
            if (!child.name.StartsWith("GardenDenseBand_", StringComparison.Ordinal)) continue;
            Renderer[] bandRenderers = child.GetComponentsInChildren<Renderer>(true);
            if (bandRenderers.Length == 0) continue;
            Bounds bandBounds = bandRenderers[0].bounds;
            foreach (Renderer renderer in bandRenderers.Skip(1)) bandBounds.Encapsulate(renderer.bounds);
            bandHeights.Add(bandBounds.size.y);
        }
        if (bandHeights.Count != 3 || bandHeights.Max() < 1.20f || bandHeights.Min() > 0.72f)
            issues.Add($"garden dense-row silhouette bands regressed: count={bandHeights.Count}, " +
                $"min={(bandHeights.Count == 0 ? 0f : bandHeights.Min()):F2}, max={(bandHeights.Count == 0 ? 0f : bandHeights.Max()):F2}");
        Transform cultivationLine = garden.transform.Find("GardenDenseCultivationLine");
        if (cultivationLine == null || cultivationLine.childCount != 4 ||
            cultivationLine.GetComponentsInChildren<Renderer>(true).Length < 4 ||
            cultivationLine.GetComponentInChildren<LineRenderer>(true) == null ||
            cultivationLine.GetComponentsInChildren<Collider>(true).Length != 0)
            issues.Add("garden dense row lost its single three-stake, sagging-twine cultivation line");
        Transform rowStakes = garden.transform.Find("GardenRowStakes");
        if (rowStakes == null || rowStakes.childCount != 8 ||
            rowStakes.GetComponentsInChildren<Collider>(true).Length != 0)
            issues.Add($"garden row endpoint stakes regressed: count={(rowStakes == null ? 0 : rowStakes.childCount)}");

        GameObject shed = GameObject.Find("estateShed (SM_House_06)");
        GameObject well = GameObject.Find("estateWell (SM_Well_01)");
        Transform work = garden.transform.Find("GardenWorkCluster");
        Transform traces = garden.transform.Find("GardenHumanTraces");
        Transform cart = work?.Find("GardenAbandonedHarvestCart");
        Transform basket = work?.Find("GardenAbandonedBasket");
        Transform cover = traces?.Find("GardenRottedStrawCover");
        if (shed == null || well == null || cart == null || basket == null || cover == null)
            issues.Add("garden interrupted-work chain is missing shed, cart, well, basket or rotted bed cover");
        else
        {
            float shedCart = HorizontalDistance(shed.transform.position, cart.position);
            float cartWell = HorizontalDistance(cart.position, well.transform.position);
            float wellBasket = HorizontalDistance(well.transform.position, basket.position);
            float basketCover = HorizontalDistance(basket.position, cover.position);
            if (shedCart > 4.5f || cartWell > 5.2f || wellBasket > 5.0f || basketCover > 4.2f)
                issues.Add($"garden work chain is visually disconnected: shed/cart={shedCart:F1}, cart/well={cartWell:F1}, well/basket={wellBasket:F1}, basket/cover={basketCover:F1}");
        }
    }

    static float HorizontalDistance(Vector3 a, Vector3 b) =>
        Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    static void ValidateArrivalPresentation(List<string> issues)
    {
        GameObject traces = GameObject.Find("ArrivalStoryTraces");
        Transform left = traces == null ? null : traces.transform.Find("PressedTyre_Left");
        Transform right = traces == null ? null : traces.transform.Find("PressedTyre_Right");
        if (left == null || right == null)
        {
            issues.Add("arrival vehicle lost its two authored terrain-following tyre traces");
            return;
        }
        foreach (Transform trace in new[] { left, right })
        {
            MeshFilter filter = trace.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount != 42 ||
                Mathf.Max(filter.sharedMesh.bounds.size.x, filter.sharedMesh.bounds.size.z) < 2.70f)
                issues.Add($"{trace.name} returned to short clean blocks instead of an irregular arrival ribbon");
        }
        if (traces.GetComponentsInChildren<Collider>(true).Length != 0)
            issues.Add("arrival surface evidence gained decorative collision");

    }

    /// "Exactly one mansion" is canon, so it must survive a rename and a prefab unpack. Counting
    /// objects called "Mansion (" is a weak guard on its own, and prefab-source provenance disappears
    /// when an instance is completely unpacked. Use three independent signals: authored root name,
    /// connected source asset, and the durable identity component GmMansion authors onto its root.
    ///
    /// Honest limit: this catches a second copy of THIS shell, not an arbitrary third-party house
    /// prop pressed into service as disguised architecture. That case is a judgement call about
    /// what reads as a mansion and is left to the visual review rather than faked with a
    /// size heuristic that would false-positive on the chapel and coach house.
    static void ValidateSingleMansion(List<string> issues)
    {
        int mansions = 0;
        foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            if (go.name.StartsWith("Mansion (", StringComparison.OrdinalIgnoreCase)) mansions++;
        if (mansions != 1) issues.Add($"expected exactly one mansion shell, found {mansions}");

        var shellRoots = new HashSet<GameObject>();
        foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(go)) continue;
            var root = PrefabUtility.GetNearestPrefabInstanceRoot(go);
            if (root == null || shellRoots.Contains(root)) continue;
            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(root);
            if (source == null) continue;
            string path = AssetDatabase.GetAssetPath(source);
            if (!string.IsNullOrEmpty(path) &&
                path.EndsWith("haunted_victorian_house.fbx", StringComparison.OrdinalIgnoreCase))
                shellRoots.Add(root);
        }
        if (shellRoots.Count != 1)
            issues.Add($"expected exactly one instance of the mansion shell asset, found {shellRoots.Count} " +
                       "(connected-prefab provenance check)");

        var identities = UnityEngine.Object.FindObjectsByType<GmMansionIdentity>(FindObjectsInactive.Include);
        int canonicalIdentities = 0;
        foreach (var identity in identities)
            if (identity.IsCanonical) canonicalIdentities++;
        if (canonicalIdentities != 1)
            issues.Add($"expected exactly one durable canonical mansion identity, found {canonicalIdentities} " +
                       "(duplicate/rename/prefab-unpack check)");
    }

    static void ValidateMaterials(List<string> issues)
    {
        foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
        {
            if (!r.enabled) continue;
            foreach (var material in r.sharedMaterials)
            {
                if (material == null) { issues.Add($"{Path(r.transform)} has a missing material"); continue; }
                if (material.shader == null) { issues.Add($"{Path(r.transform)} material '{material.name}' has no shader"); continue; }
                string shaderPath = AssetDatabase.GetAssetPath(material.shader);
                if (string.IsNullOrEmpty(shaderPath) || shaderPath.StartsWith("Resources/") || shaderPath.StartsWith("Library/"))
                    issues.Add($"{Path(r.transform)} uses built-in shader '{material.shader.name}' (magenta in HDRP)");
            }
        }
    }

    static void ValidateDecorativeCollision(List<string> issues)
    {
        foreach (string rootName in ColliderFreeRoots)
        {
            var root = FindRoot(rootName);
            if (root == null) continue;
            int count = root.GetComponentsInChildren<Collider>(true).Length;
            if (count > 0) issues.Add($"decorative root '{rootName}' carries {count} collider(s)");
        }
    }

    static void ValidateGroundedChildren(string rootName, List<string> issues)
    {
        var root = FindRoot(rootName);
        if (root == null) return;
        foreach (Transform child in root.transform)
        {
            string lower = child.name.ToLowerInvariant();
            if (lower.Contains("path") || lower.Contains("bed_") || lower.Contains("boundary") || lower.Contains("rut")) continue;
            ValidateGroundedComposite(child, issues);
        }
    }

    static void ValidateGroundedComposite(Transform node, List<string> issues)
    {
        Renderer[] renderers = node.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        // Pure grouping roots sit at scene zero and contain many independently seated props. Test
        // each child composite; combining the whole group and sampling one terrain point is invalid
        // once the ground has real relief. A placed prefab or authored marker has a meaningful root
        // position, so all of its parts (including raised crowns) are evaluated as one object.
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        bool groupingRoot = node.GetComponent<Renderer>() == null &&
            Mathf.Abs(node.position.x) < 0.01f && Mathf.Abs(node.position.z) < 0.01f &&
            node.childCount > 0 && Mathf.Max(bounds.size.x, bounds.size.z) > 8f;
        if (groupingRoot)
        {
            foreach (Transform child in node)
                ValidateGroundedComposite(child, issues);
            return;
        }

        float sampleX = Mathf.Abs(node.position.x) > 0.01f ? node.position.x : bounds.center.x;
        float sampleZ = Mathf.Abs(node.position.z) > 0.01f ? node.position.z : bounds.center.z;
        float terrainY = GmExteriorTerrainComposer.GroundHeight(sampleX, sampleZ);
        float contactDelta = bounds.min.y - terrainY;
        if (contactDelta < -0.28f || contactDelta > 0.20f)
            issues.Add($"{Path(node)} is not grounded (renderer min.y={bounds.min.y:F2}, " +
                $"terrain={terrainY:F2}, delta={contactDelta:F2})");
    }

    static void ValidateClearPath(string rootName, Vector2 from, Vector2 to, float halfWidth, List<string> issues)
    {
        var root = FindRoot(rootName);
        if (root == null) return;
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        int steps = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(from, to) / 0.6f));
        for (int step = 0; step <= steps; step++)
        {
            Vector2 p = Vector2.Lerp(from, to, step / (float)steps);
            foreach (var r in renderers)
            {
                if (!r.enabled || IsFloorDressing(r.transform)) continue;
                Bounds b = r.bounds;
                if (b.max.y < 0.22f) continue;
                float dx = Mathf.Max(b.min.x - p.x, 0f, p.x - b.max.x);
                float dz = Mathf.Max(b.min.z - p.y, 0f, p.y - b.max.z);
                if (new Vector2(dx, dz).magnitude < halfWidth)
                {
                    issues.Add($"{rootName} authored path is obstructed near ({p.x:F1},{p.y:F1}) by {Path(r.transform)}");
                    return;
                }
            }
        }
    }

    static bool IsFloorDressing(Transform t)
    {
        for (var p = t; p != null; p = p.parent)
        {
            string n = p.name.ToLowerInvariant();
            if (n.Contains("path") || n.Contains("bed_") || n.Contains("rut")) return true;
        }
        return false;
    }

    static void ValidateExteriorAudio(List<string> issues)
    {
        var go = new GameObject("GmEstateAudit_Ambience");
        try
        {
            var ambience = go.AddComponent<GmAmbience>();
            var initialize = typeof(GmAmbience).GetMethod("Initialize", BindingFlags.Instance | BindingFlags.NonPublic);
            if (initialize == null) { issues.Add("GmAmbience test initializer is missing"); return; }
            initialize.Invoke(ambience, new object[] { false });
            int loops = 0;
            int localized = 0;
            foreach (var source in go.GetComponentsInChildren<AudioSource>(true))
            {
                if (!source.loop && source.spatialBlend >= 0.99f && source.clip != null &&
                    source.clip.name.StartsWith("wind_local_")) localized++;
                if (source.loop) loops++;
            }
            if (ambience.reviewProfile != GmWindReviewProfile.SparseLocalized || ambience.useContinuousWind)
                issues.Add("exterior does not default to the reviewed sparse-localized wind profile");
            if (loops != 0) issues.Add($"exterior has {loops} continuous beds; shipping default requires zero");
            if (localized != 4) issues.Add($"exterior has {localized} localized wind emitters; expected four authored positions");
            if (ambience.LocalizedWildlifeEmitterCount != 2)
                issues.Add($"exterior has {ambience.LocalizedWildlifeEmitterCount} localized wildlife emitters; expected cricket and owl sources");
            if (ambience.ActiveLocalizedGustCount > 1)
                issues.Add($"exterior initializes with {ambience.ActiveLocalizedGustCount} simultaneous localized gusts");
            foreach (var surface in new[] { GmSurfaceKind.PackedMud, GmSurfaceKind.WetMud,
                GmSurfaceKind.Gravel, GmSurfaceKind.DeadGrass, GmSurfaceKind.LeafLitter,
                GmSurfaceKind.Stone, GmSurfaceKind.Wood })
                if (ambience.FootstepClipCount(surface) != 8)
                    issues.Add($"{surface} has {ambience.FootstepClipCount(surface)} footstep clips; expected eight");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    static string Path(Transform t)
    {
        string result = t.name;
        while (t.parent != null) { t = t.parent; result = t.name + "/" + result; }
        return result;
    }

    static GameObject FindRoot(string name)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) return null;
        foreach (var root in scene.GetRootGameObjects()) if (root.name == name) return root;
        return null;
    }

    public static string LayoutFingerprint()
    {
        var rows = new List<string>();
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) return "invalid-scene";
        foreach (var root in scene.GetRootGameObjects()) Collect(root.transform, rows);
        foreach (GameObject livingWoodland in new[]
        {
            FindRoot("LivingWoodlandStage"), FindRoot("LivingWoodlandAccent")
        }.Where(item => item != null))
        {
            foreach (Material material in livingWoodland.GetComponentsInChildren<Renderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null).Distinct()
                .OrderBy(material => material.name, StringComparer.Ordinal))
            {
                Texture baseTexture = material.HasProperty("_BaseColorMap")
                    ? material.GetTexture("_BaseColorMap")
                    : material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
                Texture normalTexture = material.HasProperty("_NormalMap")
                    ? material.GetTexture("_NormalMap")
                    : material.HasProperty("_Normal") ? material.GetTexture("_Normal") : null;
                Color color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") :
                    material.HasProperty("_BaseColor_Value") ? material.GetColor("_BaseColor_Value") : Color.clear;
                float smoothness = material.HasProperty("_Smoothness") ? material.GetFloat("_Smoothness") : -1f;
                float alphaCutoff = material.HasProperty("_AlphaCutoff") ? material.GetFloat("_AlphaCutoff") :
                    material.HasProperty("_Alpha_Clip_Treshold") ? material.GetFloat("_Alpha_Clip_Treshold") : -1f;
                rows.Add($"WOODLAND-MATERIAL|{livingWoodland.name}|{material.name}|{material.shader?.name ?? "<null>"}|" +
                    $"base={AssetDatabase.GetAssetPath(baseTexture)}|normal={AssetDatabase.GetAssetPath(normalTexture)}|" +
                    $"color={color.r:F4},{color.g:F4},{color.b:F4},{color.a:F4}|smooth={smoothness:F4}|cutoff={alphaCutoff:F4}");
            }
            foreach (GmCanopySway sway in livingWoodland.GetComponentsInChildren<GmCanopySway>(true)
                .OrderBy(item => Path(item.transform), StringComparer.Ordinal))
                rows.Add($"WOODLAND-SWAY|{Path(sway.transform)}|{sway.AmplitudeDegrees:F4}|" +
                    $"{sway.PrimaryFrequencyHz:F4}|{sway.MaximumAngleDegrees:F4}");
        }
        foreach (Terrain terrain in UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include)
            .OrderBy(item => Path(item.transform), StringComparer.Ordinal))
        {
            TerrainData data = terrain.terrainData;
            if (data == null) continue;
            rows.Add($"TERRAIN|{Path(terrain.transform)}|{AssetDatabase.GetAssetPath(data)}|" +
                $"prototypes={data.treePrototypes.Length}|instances={data.treeInstanceCount}");
            for (int i = 0; i < data.terrainLayers.Length; i++)
            {
                TerrainLayer layer = data.terrainLayers[i];
                if (layer == null) { rows.Add($"TERRAIN-LAYER|{i}|<null>"); continue; }
                rows.Add($"TERRAIN-LAYER|{i}|base={AssetDatabase.GetAssetPath(layer.diffuseTexture)}|" +
                    $"normal={AssetDatabase.GetAssetPath(layer.normalMapTexture)}|" +
                    $"remapMin={layer.diffuseRemapMin.x:F4},{layer.diffuseRemapMin.y:F4}," +
                    $"{layer.diffuseRemapMin.z:F4},{layer.diffuseRemapMin.w:F4}|" +
                    $"remap={layer.diffuseRemapMax.x:F4},{layer.diffuseRemapMax.y:F4}," +
                    $"{layer.diffuseRemapMax.z:F4},{layer.diffuseRemapMax.w:F4}|" +
                    $"tile={layer.tileSize.x:F3},{layer.tileSize.y:F3}|" +
                    $"normalScale={layer.normalScale:F3}|smooth={layer.smoothness:F4}");
            }
            for (int i = 0; i < data.treePrototypes.Length; i++)
                rows.Add($"TERRAIN-PROTOTYPE|{i}|" +
                    $"{AssetDatabase.GetAssetPath(data.treePrototypes[i].prefab)}|{data.treePrototypes[i].bendFactor:F4}");
            TreeInstance[] instances = data.treeInstances;
            for (int i = 0; i < instances.Length; i++)
            {
                TreeInstance tree = instances[i];
                rows.Add($"TERRAIN-TREE|{i:D5}|{tree.prototypeIndex}|" +
                    $"{tree.position.x:F6},{tree.position.y:F6},{tree.position.z:F6}|" +
                    $"{tree.widthScale:F4},{tree.heightScale:F4},{tree.rotation:F4}|" +
                    $"{tree.color.r:F4},{tree.color.g:F4},{tree.color.b:F4},{tree.color.a:F4}");
            }
        }
        rows.Sort(StringComparer.Ordinal);
        unchecked
        {
            ulong hash = 14695981039346656037UL;
            foreach (string row in rows)
                foreach (char c in row) { hash ^= c; hash *= 1099511628211UL; }
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }
    }

    static void Collect(Transform t, List<string> rows)
    {
        Vector3 p = t.position, r = t.eulerAngles, s = t.lossyScale;
        rows.Add($"{Path(t)}|{p.x:F3},{p.y:F3},{p.z:F3}|{r.x:F2},{r.y:F2},{r.z:F2}|{s.x:F3},{s.y:F3},{s.z:F3}");
        foreach (Transform child in t) Collect(child, rows);
    }
}

