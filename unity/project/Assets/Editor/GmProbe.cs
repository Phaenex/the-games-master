// Headless verification probe: opens a scene, renders frames to PNG, reports what's in view.
// Usage: Unity -projectPath <proj> -batchmode -executeMethod GmProbe.ShotLeartesOverview -quit
// Note: HDRP in -batchmode can render to RenderTextures on Metal; if frames come back black,
// the fallback is a GUI-open probe (documented in the rebuild plan).
using System.IO;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public static class GmProbe
{
    static bool renderPipelineWarmed;

    public static void AuditImportedTerrainDonors()
    {
        string[] paths = {
            "Assets/LeartesStudios/WitchVillage/HDRP/Art/Terrain/New Terrain.asset",
            "Assets/LeartesStudios/HauntedVillage/Art/Terrain/New Terrain.asset",
            "Assets/LeartesStudios/HauntedVillage/Art/Terrain/New Terrain 1.asset",
            "Assets/LeartesStudios/HauntedVillage/Art/Terrain/New Terrain 2.asset",
            "Assets/LeartesStudios/HauntedVillage/Art/Terrain/New Terrain 3.asset",
        };
        foreach (string path in paths)
        {
            TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            if (data == null)
            {
                Debug.LogWarning($"[GmProbe] missing terrain donor={path}");
                continue;
            }
            float[,] heights = data.GetHeights(0, 0, data.heightmapResolution,
                data.heightmapResolution);
            float minimum = 1f;
            float maximum = 0f;
            foreach (float height in heights)
            {
                minimum = Mathf.Min(minimum, height);
                maximum = Mathf.Max(maximum, height);
            }
            int detailInstances = 0;
            for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
            {
                int[,] details = data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, layer);
                foreach (int count in details) detailInstances += count;
            }
            Debug.Log($"[GmProbe] terrain donor={path} size={data.size} " +
                $"heightRes={data.heightmapResolution} relief={(maximum - minimum) * data.size.y:F2}m " +
                $"alpha={data.alphamapResolution}x{data.alphamapLayers} " +
                $"trees={data.treeInstanceCount} treePrototypes={data.treePrototypes.Length} " +
                $"details={detailInstances} detailPrototypes={data.detailPrototypes.Length}");
        }
        EditorApplication.Exit(0);
    }

    readonly struct VisualMetrics
    {
        public readonly long MeanLuminance;
        public readonly int P05;
        public readonly int P95;
        public readonly long MeanChroma;
        public readonly float DeepBlackPercent;
        public readonly float ClippedPercent;
        public readonly double AverageRenderMilliseconds;

        public VisualMetrics(long meanLuminance, int p05, int p95, long meanChroma,
            float deepBlackPercent, float clippedPercent, double averageRenderMilliseconds)
        {
            MeanLuminance = meanLuminance;
            P05 = p05;
            P95 = p95;
            MeanChroma = meanChroma;
            DeepBlackPercent = deepBlackPercent;
            ClippedPercent = clippedPercent;
            AverageRenderMilliseconds = averageRenderMilliseconds;
        }
    }

    public static void ShotLeartesOverview() { Shot("Assets/LeartesStudios/HauntedVillage/Scene/Overview.unity", "unity-probe-overview.png"); }
    public static void ShotShowcase()        { Shot("Assets/LeartesStudios/HauntedVillage/Scene/Showcase.unity", "unity-probe-showcase.png"); }
    public static void ShotWitchVillageReference()
    {
        Shot("Assets/LeartesStudios/WitchVillage/HDRP/Scene/HDRP_WitchVillage.unity",
            "unity-probe-witch-village-reference.png");
    }
    public static void ShotAbandonedVillageReference()
    {
        Shot("Assets/LeartesStudios/Abandoned Village/HDRP/Scene/HDRP_Abandoned_Village.unity",
            "unity-probe-abandoned-village-reference.png");
    }
    public static void ShotWendHill()        { Shot("Assets/Scenes/WendHill.unity", "unity-probe-wendhill.png"); }
    public static void ShotEnvironmentGrammarLab()
    {
        GmEnvironmentGrammarLabBuilder.BuildAndSave();
        EditorSceneManager.OpenScene(GmEnvironmentGrammarLabBuilder.ScenePath, OpenSceneMode.Single);
        var cam = Object.FindFirstObjectByType<Camera>();
        if (cam == null) { Debug.LogError("[GmProbe] environment lab has no camera"); EditorApplication.Exit(1); return; }
        HideLabDestination();
        var views = new[] {
            (z: 47f, targetZ: 15f, lateral: 0f, file: "unity-probe-environment-grammar-lab.png"),
            (z: 18f, targetZ: -14f, lateral: -0.55f, file: "unity-probe-environment-grammar-lab-02-mid.png"),
            (z: -11f, targetZ: -42f, lateral: 0.45f, file: "unity-probe-environment-grammar-lab-03-deep.png"),
            (z: -27f, targetZ: 3f, lateral: 1.25f, file: "unity-probe-environment-grammar-lab-04-reverse.png"),
        };
        bool metricsPass = GmEnvironmentGrammarLabBuilder.ValidateTransferBudgets();
        foreach (var view in views)
        {
            GmEnvironmentGrammarLabBuilder.PositionCamera(cam, view.z, view.targetZ, view.lateral);
            metricsPass &= ValidateDiagnosticMetrics(view.file, Capture(cam, view.file));
        }
        GmEnvironmentGrammarLabBuilder.PositionCameraSide(cam, 8f, false);
        metricsPass &= ValidateDiagnosticMetrics("unity-probe-environment-grammar-lab-05-left.png",
            Capture(cam, "unity-probe-environment-grammar-lab-05-left.png"));
        GmEnvironmentGrammarLabBuilder.PositionCameraSide(cam, 8f, true);
        metricsPass &= ValidateDiagnosticMetrics("unity-probe-environment-grammar-lab-06-right.png",
            Capture(cam, "unity-probe-environment-grammar-lab-06-right.png"));
        Debug.Log($"[GmProbe] environment diagnostic metrics={(metricsPass ? "PASS" : "FAIL")}");
        EditorApplication.Exit(metricsPass ? 0 : 1);
    }

    public static void ShotEnvironmentGrammarWalk()
    {
        EditorSceneManager.OpenScene(GmEnvironmentGrammarLabBuilder.ScenePath, OpenSceneMode.Single);
        var cam = Object.FindFirstObjectByType<Camera>();
        if (cam == null)
        {
            Debug.LogError("[GmProbe] environment walk has no camera");
            EditorApplication.Exit(1);
            return;
        }
        HideLabDestination();
        Debug.Log("[GmProbe] lab walk ends at the reveal/handoff; Wend separately proves its real closed-door destination");
        bool pass = GmEnvironmentGrammarLabBuilder.ValidateTransferBudgets();
        float[] stations = { 56f, 46f, 34f, 22f, 10f, -2f, -14f, -27f, -40f };
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        for (int stale = stations.Length + 1; stale <= 24; stale++)
        {
            string stalePath = Path.Combine(projectRoot,
                $"unity-probe-environment-walk-{stale:00}.png");
            if (File.Exists(stalePath)) File.Delete(stalePath);
        }
        for (int i = 0; i < stations.Length; i++)
        {
            float z = stations[i];
            float targetZ = z - 24f;
            GmEnvironmentGrammarLabBuilder.PositionCamera(cam, z, targetZ,
                Mathf.Sin(i * 1.71f) * 0.28f);
            string file = $"unity-probe-environment-walk-{i + 1:00}.png";
            pass &= ValidateDiagnosticMetrics(file, Capture(cam, file));
        }
        Debug.Log($"[GmProbe] player-height walk samples={stations.Length} authoredStations result={(pass ? "PASS" : "FAIL")}");
        EditorApplication.Exit(pass ? 0 : 1);
    }

    static void HideLabDestination()
    {
        GameObject destination = GameObject.Find("DestinationProxy_NotShipping");
        if (destination != null)
        {
            destination.SetActive(false);
            Debug.Log("[GmProbe] unbiased grammar capture: non-shipping destination hidden");
        }
    }

    public static void ShotEnvironmentTreeCandidates()
    {
        GmEnvironmentGrammarLabBuilder.BuildTreeValidationAndSave();
        EditorSceneManager.OpenScene(GmEnvironmentGrammarLabBuilder.TreeValidationScenePath, OpenSceneMode.Single);
        var cam = Object.FindFirstObjectByType<Camera>();
        GameObject root = GameObject.Find("TreeCandidates");
        if (cam == null || root == null)
        {
            Debug.LogError("[GmProbe] tree validation scene is missing its camera or candidate root");
            EditorApplication.Exit(1);
            return;
        }
        for (int i = 0; i < root.transform.childCount; i++)
        {
            Transform candidate = root.transform.GetChild(i);
            candidate.gameObject.SetActive(true);
            Capture(cam, $"unity-probe-tree-{candidate.name}.png");
            candidate.gameObject.SetActive(false);
        }
        EditorApplication.Exit(0);
    }

    public static void ShotEnvironmentSourceCandidates()
    {
        GmEnvironmentGrammarLabBuilder.BuildSourceValidationAndSave();
        EditorSceneManager.OpenScene(GmEnvironmentGrammarLabBuilder.SourceValidationScenePath,
            OpenSceneMode.Single);
        var cam = Object.FindFirstObjectByType<Camera>();
        GameObject root = GameObject.Find("EnvironmentSourceCandidates");
        if (cam == null || root == null)
        {
            Debug.LogError("[GmProbe] source validation scene is missing its camera or candidate root");
            EditorApplication.Exit(1);
            return;
        }
        for (int i = 0; i < root.transform.childCount; i++)
        {
            Transform candidate = root.transform.GetChild(i);
            candidate.gameObject.SetActive(true);
            Bounds bounds = RenderBounds(candidate.gameObject);
            float horizontal = Mathf.Max(bounds.size.x, bounds.size.z);
            float distance = Mathf.Max(3.2f, horizontal * 1.65f, bounds.size.y * 1.35f);
            float eye = Mathf.Max(1.65f, bounds.min.y + bounds.size.y * 0.52f);
            cam.transform.position = new Vector3(bounds.center.x + distance * 0.32f,
                eye, bounds.center.z + distance);
            cam.transform.LookAt(new Vector3(bounds.center.x,
                Mathf.Max(0.45f, bounds.min.y + bounds.size.y * 0.48f), bounds.center.z));
            Capture(cam, $"unity-probe-environment-source-{candidate.name}.png");
            Debug.Log($"[GmProbe] source={candidate.name} bounds={bounds.size} distance={distance:F2}");
            candidate.gameObject.SetActive(false);
        }
        EditorApplication.Exit(0);
    }

    static Bounds RenderBounds(GameObject owner)
    {
        Bounds bounds = GmOwnedEnvironmentGrammar.RenderableBounds(owner);
        return bounds.size.sqrMagnitude > 0.000001f
            ? bounds
            : new Bounds(owner.transform.position, Vector3.one);
    }

    public static void ShotEnvironmentTreeFamilies()
    {
        EditorSceneManager.OpenScene(GmEnvironmentGrammarLabBuilder.ScenePath, OpenSceneMode.Single);
        var cam = Object.FindFirstObjectByType<Camera>();
        GameObject root = GameObject.Find("PlannedWoodland");
        if (cam == null || root == null)
        {
            Debug.LogError("[GmProbe] environment lab is missing its camera or woodland root");
            EditorApplication.Exit(1);
            return;
        }
        GmEnvironmentGrammarLabBuilder.PositionCamera(cam, 47f, 15f, 0f);
        for (int family = 1; family <= 9; family++)
        {
            string token = $"_{family:00}_";
            int visible = 0;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                GameObject tree = root.transform.GetChild(i).gameObject;
                bool show = tree.name.Contains(token);
                tree.SetActive(show);
                if (show) visible++;
            }
            Capture(cam, $"unity-probe-tree-family-{family:00}.png");
            Debug.Log($"[GmProbe] family={family:00} visible={visible}");
        }
        EditorApplication.Exit(0);
    }

    public static void ShotEnvironmentLayerIsolation()
    {
        EditorSceneManager.OpenScene(GmEnvironmentGrammarLabBuilder.ScenePath, OpenSceneMode.Single);
        var cam = Object.FindFirstObjectByType<Camera>();
        if (cam == null)
        {
            Debug.LogError("[GmProbe] environment lab is missing its camera");
            EditorApplication.Exit(1);
            return;
        }
        string[] layerNames = {
            "VisibleHerbaceousLayer", "EcotoneUnderstory", "RoadReclamation",
            "PlannedWoodland", "CausalBranchFall", "CausalGroundStory",
            "DestinationProxy_NotShipping",
        };
        var layers = new GameObject[layerNames.Length];
        for (int i = 0; i < layerNames.Length; i++)
        {
            layers[i] = GameObject.Find(layerNames[i]);
            if (layers[i] != null) layers[i].SetActive(false);
        }
        GmEnvironmentGrammarLabBuilder.PositionCamera(cam, 47f, 15f, 0f);
        Capture(cam, "unity-probe-environment-layer-00-base.png");
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] == null) continue;
            layers[i].SetActive(true);
            Capture(cam, $"unity-probe-environment-layer-{i + 1:00}-{layerNames[i]}.png");
            layers[i].SetActive(false);
        }
        EditorApplication.Exit(0);
    }

    /// <summary>
    /// False-colour proof of the generated terrain splat mask. The road is magenta and the two
    /// compressed ruts are cyan. This clones the TerrainData and layers in memory, so the capture
    /// cannot contaminate the saved lab or any purchased source asset.
    /// </summary>
    public static void ShotEnvironmentRoadMask()
    {
        EditorSceneManager.OpenScene(GmEnvironmentGrammarLabBuilder.ScenePath, OpenSceneMode.Single);
        var terrain = Object.FindFirstObjectByType<Terrain>();
        if (terrain == null)
        {
            Debug.LogError("[GmProbe] road-mask proof is missing its terrain");
            EditorApplication.Exit(1);
            return;
        }
        bool pass = WriteTerrainAlphaProof(terrain.terrainData,
            "unity-probe-environment-road-mask.png", true);
        EditorApplication.Exit(pass ? 0 : 1);
    }

    /// <summary>
    /// Oblique false-colour plan proving that ecological terrain materials form connected
    /// territories rather than an evenly averaged beige field. Brown is soil, green meadow,
    /// amber humus, magenta drive and cyan compressed ruts.
    /// </summary>
    public static void ShotEnvironmentTerrainCommunityMask()
    {
        EditorSceneManager.OpenScene(GmEnvironmentGrammarLabBuilder.ScenePath, OpenSceneMode.Single);
        var terrain = Object.FindFirstObjectByType<Terrain>();
        if (terrain == null)
        {
            Debug.LogError("[GmProbe] terrain-community proof is missing its terrain");
            EditorApplication.Exit(1);
            return;
        }
        bool pass = WriteTerrainAlphaProof(terrain.terrainData,
            "unity-probe-environment-terrain-community-mask.png", false);
        EditorApplication.Exit(pass ? 0 : 1);
    }

    /// <summary>Paired capture proving whether Unity's instanced terrain details draw in HDRP.</summary>
    public static void ShotEnvironmentTerrainDetails()
    {
        EditorSceneManager.OpenScene(GmEnvironmentGrammarLabBuilder.ScenePath, OpenSceneMode.Single);
        var cam = Object.FindFirstObjectByType<Camera>();
        var terrain = Object.FindFirstObjectByType<Terrain>();
        if (cam == null || terrain == null)
        {
            Debug.LogError("[GmProbe] terrain-detail proof is missing its camera or terrain");
            EditorApplication.Exit(1);
            return;
        }
        GmEnvironmentGrammarLabBuilder.PositionCamera(cam, 47f, 15f, 0f);
        terrain.drawTreesAndFoliage = false;
        terrain.Flush();
        Capture(cam, "unity-probe-environment-details-off.png");
        terrain.drawTreesAndFoliage = true;
        terrain.terrainData.RefreshPrototypes();
        terrain.Flush();
        Capture(cam, "unity-probe-environment-details-on.png");
        EditorApplication.Exit(0);
    }

    /// <summary>
    /// Paired player-height proof that the donor-style Terrain tree instances contribute visible
    /// ground ecology. Object layers are hidden so tree wind or fog cannot create a false pass.
    /// </summary>
    public static void ShotEnvironmentTerrainFoliageContribution()
    {
        EditorSceneManager.OpenScene(GmEnvironmentGrammarLabBuilder.ScenePath, OpenSceneMode.Single);
        var cam = Object.FindFirstObjectByType<Camera>();
        var terrain = Object.FindFirstObjectByType<Terrain>();
        if (cam == null || terrain == null)
        {
            Debug.LogError("[GmProbe] terrain-foliage proof is missing its camera or terrain");
            EditorApplication.Exit(1);
            return;
        }
        foreach (string layerName in new[] {
            "VisibleHerbaceousLayer", "EcotoneUnderstory", "RoadReclamation",
            "PlannedWoodland", "CausalBranchFall", "CausalGroundStory",
            "RoadEdgeConsequences", "DestinationProxy_NotShipping"
        })
        {
            GameObject layer = GameObject.Find(layerName);
            if (layer != null) layer.SetActive(false);
        }
        GmEnvironmentGrammarLabBuilder.PositionCamera(cam, 18f, -14f, -0.55f);
        terrain.drawTreesAndFoliage = false;
        terrain.Flush();
        const string offFile = "unity-probe-environment-foliage-off.png";
        const string onFile = "unity-probe-environment-foliage-on.png";
        Capture(cam, offFile);
        terrain.drawTreesAndFoliage = true;
        terrain.terrainData.RefreshPrototypes();
        terrain.Flush();
        Capture(cam, onFile);
        bool pass = ValidateCaptureContribution(offFile, onFile, "terrain foliage", 0.45f, 1.0f);
        EditorApplication.Exit(pass ? 0 : 1);
    }

    /// <summary>
    /// Paired proof for the visible road treatment. The generated skin has repeatedly hidden a
    /// valid terrain splat, so every road revision must show both the terrain-only result and the
    /// composed result from the same camera before it can be accepted.
    /// </summary>
    public static void ShotEnvironmentRoadComparison()
    {
        EditorSceneManager.OpenScene(GmEnvironmentGrammarLabBuilder.ScenePath, OpenSceneMode.Single);
        var cam = Object.FindFirstObjectByType<Camera>();
        GameObject road = GameObject.Find("OwnedContinuousRoadSurface");
        if (cam == null)
        {
            Debug.LogError("[GmProbe] road comparison is missing its camera");
            EditorApplication.Exit(1);
            return;
        }
        foreach (string layerName in new[] {
            "VisibleHerbaceousLayer", "EcotoneUnderstory", "RoadReclamation",
            "PlannedWoodland", "CausalBranchFall", "CausalGroundStory",
            "DestinationProxy_NotShipping"
        })
        {
            GameObject layer = GameObject.Find(layerName);
            if (layer != null) layer.SetActive(false);
        }
        GmEnvironmentGrammarLabBuilder.PositionCamera(cam, 47f, 15f, 0f);
        if (road != null) road.SetActive(false);
        Capture(cam, "unity-probe-environment-road-terrain-only.png");
        if (road != null) road.SetActive(true);
        Capture(cam, "unity-probe-environment-road-composed.png");
        EditorApplication.Exit(0);
    }

    /// <summary>
    /// Read-only inventory of the complete purchased donor scenes. This prevents the composition
    /// engine from reimplementing terrain details or prototype systems which already work in an
    /// owned pack, and gives every admission decision an exact source path.
    /// </summary>
    public static void AuditEnvironmentDonors()
    {
        string[] scenes = {
            "Assets/LeartesStudios/HauntedVillage/Scene/Showcase.unity",
            "Assets/LeartesStudios/WitchVillage/HDRP/Scene/HDRP_WitchVillage.unity",
            "Assets/LeartesStudios/Abandoned Village/HDRP/Scene/HDRP_Abandoned_Village.unity",
        };
        foreach (string scenePath in scenes)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            Debug.Log($"[GmProbe] donor scene={scenePath} terrains={terrains.Length} renderers={renderers.Length}");
            for (int terrainIndex = 0; terrainIndex < terrains.Length; terrainIndex++)
            {
                Terrain terrain = terrains[terrainIndex];
                TerrainData data = terrain.terrainData;
                Debug.Log($"[GmProbe] donor terrain={terrain.name} index={terrainIndex} " +
                    $"data={AssetDatabase.GetAssetPath(data)} position={terrain.transform.position} " +
                    $"size={data.size} heightRes={data.heightmapResolution} alphaRes={data.alphamapResolution} " +
                    $"detailRes={data.detailResolution} details={data.detailPrototypes.Length} " +
                    $"trees={data.treeInstances.Length} treePrototypes={data.treePrototypes.Length}");
                for (int i = 0; i < data.terrainLayers.Length; i++)
                    Debug.Log($"[GmProbe] donor layer terrain={terrain.name} index={i} " +
                        $"path={AssetDatabase.GetAssetPath(data.terrainLayers[i])}");
                for (int i = 0; i < data.detailPrototypes.Length; i++)
                {
                    DetailPrototype detail = data.detailPrototypes[i];
                    string prototypePath = detail.prototype != null
                        ? AssetDatabase.GetAssetPath(detail.prototype)
                        : AssetDatabase.GetAssetPath(detail.prototypeTexture);
                    Debug.Log($"[GmProbe] donor detail terrain={terrain.name} index={i} " +
                        $"mesh={detail.usePrototypeMesh} instancing={detail.useInstancing} " +
                        $"width={detail.minWidth:F2}..{detail.maxWidth:F2} " +
                        $"height={detail.minHeight:F2}..{detail.maxHeight:F2} source={prototypePath}");
                }
                for (int i = 0; i < data.treePrototypes.Length; i++)
                {
                    GameObject prefab = data.treePrototypes[i].prefab;
                    Debug.Log($"[GmProbe] donor tree terrain={terrain.name} index={i} " +
                        $"source={AssetDatabase.GetAssetPath(prefab)}");
                }
            }
        }
        EditorApplication.Exit(0);
    }

    public static void ShotEnvironmentEcologySources()
    {
        EditorSceneManager.OpenScene(GmEnvironmentGrammarLabBuilder.ScenePath, OpenSceneMode.Single);
        var cam = Object.FindFirstObjectByType<Camera>();
        GameObject ecology = GameObject.Find("ContinuousEcology");
        if (cam == null || ecology == null)
        {
            Debug.LogError("[GmProbe] environment lab is missing its camera or ecology root");
            EditorApplication.Exit(1);
            return;
        }
        foreach (string rootName in new[] {
            "VisibleHerbaceousLayer", "RoadReclamation", "PlannedWoodland",
            "RoadEdgeConsequences", "DestinationProxy_NotShipping",
        })
        {
            GameObject other = GameObject.Find(rootName);
            if (other != null) other.SetActive(false);
        }
        string[] sources = {
            "SM_Bush_01", "SM_Bush_02", "SM_Bush_03", "SM_Bush_04", "SM_Bush_05", "SM_Bush_06", "SM_Bush_07",
            "SM_MossClump_1", "SM_MossClump_2", "SM_MossClump_3", "SM_MossClump_4",
        };
        GmEnvironmentGrammarLabBuilder.PositionCamera(cam, 47f, 15f, 0f);
        for (int sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
        {
            string source = sources[sourceIndex];
            int visible = 0;
            for (int i = 0; i < ecology.transform.childCount; i++)
            {
                GameObject item = ecology.transform.GetChild(i).gameObject;
                bool show = item.name.EndsWith(source);
                item.SetActive(show);
                if (show) visible++;
            }
            Capture(cam, $"unity-probe-ecology-source-{sourceIndex + 1:00}-{source}.png");
            Debug.Log($"[GmProbe] ecology source={source} visible={visible}");
        }
        EditorApplication.Exit(0);
    }


    static void Shot(string scenePath, string outName)
    {
        if (!File.Exists(scenePath)) { Debug.LogError($"[GmProbe] missing scene {scenePath}"); EditorApplication.Exit(1); return; }
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var cam = Object.FindFirstObjectByType<Camera>();
        if (cam == null)
        {
            var go = new GameObject("ProbeCam");
            cam = go.AddComponent<Camera>();
            // frame the scene contents from a raised three-quarter view
            var rs = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(Vector3.zero, Vector3.one * 10);
            foreach (var r in rs) b.Encapsulate(r.bounds);
            cam.transform.position = b.center + new Vector3(b.size.x * 0.4f, b.size.y * 0.6f, b.size.z * 0.5f);
            cam.transform.LookAt(b.center);
        }

        Capture(cam, outName);
        EditorApplication.Exit(0);
    }

    static VisualMetrics Capture(Camera cam, string outName)
    {
        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        // The first HDRP camera compiles/initialises substantially more state than later views.
        // Report steady render cost only after an explicit cold-pipeline warmup; subsequent views
        // still receive two temporal-settle frames.
        int warmupFrames = renderPipelineWarmed ? 2 : 12;
        for (int i = 0; i < warmupFrames; i++) cam.Render();
        renderPipelineWarmed = true;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 6; i++) cam.Render();
        stopwatch.Stop();
        double averageRenderMilliseconds = stopwatch.Elapsed.TotalMilliseconds / 6.0;
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;

        string outPath = Path.Combine(Directory.GetCurrentDirectory(), outName);
        File.WriteAllBytes(outPath, tex.EncodeToPNG());
        // quick luminance report so logs alone reveal black frames
        var px = tex.GetPixels32();
        long sum = 0;
        long saturationSum = 0;
        int deepBlack = 0;
        int clipped = 0;
        var histogram = new int[256];
        foreach (var p in px)
        {
            int luminance = (p.r + p.g + p.b) / 3;
            sum += luminance;
            histogram[luminance]++;
            int maximum = Mathf.Max(p.r, Mathf.Max(p.g, p.b));
            int minimum = Mathf.Min(p.r, Mathf.Min(p.g, p.b));
            saturationSum += maximum - minimum;
            if (luminance <= 8) deepBlack++;
            if (luminance >= 247) clipped++;
        }
        int p05 = Percentile(histogram, px.Length, 0.05f);
        int p95 = Percentile(histogram, px.Length, 0.95f);
        long meanLuminance = sum / px.Length;
        long meanChroma = saturationSum / px.Length;
        float deepBlackPercent = deepBlack * 100f / px.Length;
        float clippedPercent = clipped * 100f / px.Length;
        Debug.Log($"[GmProbe] wrote {outPath} meanLum={meanLuminance} p05={p05} p95={p95} " +
            $"range={p95 - p05} meanChroma={(saturationSum / px.Length)} " +
            $"deepBlack={deepBlackPercent:F2}% clipped={clippedPercent:F2}% " +
            $"avgRenderMs={averageRenderMilliseconds:F1} warmupFrames={warmupFrames}");
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(rt);
        return new VisualMetrics(meanLuminance, p05, p95, meanChroma, deepBlackPercent,
            clippedPercent, averageRenderMilliseconds);
    }

    static bool ValidateDiagnosticMetrics(string file, VisualMetrics metrics)
    {
        bool pass = metrics.MeanLuminance >= 30 && metrics.MeanLuminance <= 100 &&
            metrics.P95 - metrics.P05 >= 60 && metrics.DeepBlackPercent <= 18f &&
            metrics.ClippedPercent <= 1f && metrics.AverageRenderMilliseconds <= 20.0;
        if (!pass)
            Debug.LogError($"[GmProbe] diagnostic visual gate FAIL file={file} meanLum={metrics.MeanLuminance} " +
                $"range={metrics.P95 - metrics.P05} deepBlack={metrics.DeepBlackPercent:F2}% " +
                $"clipped={metrics.ClippedPercent:F2}% avgRenderMs={metrics.AverageRenderMilliseconds:F1}");
        return pass;
    }

    static bool ValidateCaptureContribution(string offFile, string onFile, string label,
        float minimumMeanChannelDelta, float minimumChangedPercent)
    {
        var offTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var onTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        offTexture.LoadImage(File.ReadAllBytes(Path.Combine(Directory.GetCurrentDirectory(), offFile)));
        onTexture.LoadImage(File.ReadAllBytes(Path.Combine(Directory.GetCurrentDirectory(), onFile)));
        Color32[] offPixels = offTexture.GetPixels32();
        Color32[] onPixels = onTexture.GetPixels32();
        if (offPixels.Length != onPixels.Length)
            throw new System.InvalidOperationException($"Capture contribution dimensions differ for {label}");
        long absoluteDelta = 0;
        int changed = 0;
        for (int i = 0; i < offPixels.Length; i++)
        {
            int delta = Mathf.Abs(onPixels[i].r - offPixels[i].r) +
                Mathf.Abs(onPixels[i].g - offPixels[i].g) +
                Mathf.Abs(onPixels[i].b - offPixels[i].b);
            absoluteDelta += delta;
            if (delta >= 12) changed++;
        }
        float meanChannelDelta = absoluteDelta / (offPixels.Length * 3f);
        float changedPercent = changed * 100f / offPixels.Length;
        bool pass = meanChannelDelta >= minimumMeanChannelDelta &&
            changedPercent >= minimumChangedPercent;
        Debug.Log($"[GmProbe] {label} contribution={(pass ? "PASS" : "FAIL")} " +
            $"meanChannelDelta={meanChannelDelta:F3} changed={changedPercent:F2}% " +
            $"minimum={minimumMeanChannelDelta:F2}/{minimumChangedPercent:F2}%");
        Object.DestroyImmediate(offTexture);
        Object.DestroyImmediate(onTexture);
        if (!pass) Debug.LogError($"[GmProbe] {label} is present by count but invisible at player height");
        return pass;
    }

    static bool WriteTerrainAlphaProof(TerrainData data, string fileName, bool roadOnly)
    {
        float[,,] alpha = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
        int height = alpha.GetLength(0);
        int width = alpha.GetLength(1);
        int layers = alpha.GetLength(2);
        if (layers < 5)
        {
            Debug.LogError($"[GmProbe] terrain alpha proof requires five layers, found {layers}");
            return false;
        }

        var colours = new[] {
            new Color(0.30f, 0.12f, 0.045f, 1f),
            new Color(0.08f, 0.58f, 0.16f, 1f),
            new Color(0.72f, 0.31f, 0.055f, 1f),
        };
        Color roadColour = new Color(1f, 0f, 0.72f, 1f);
        Color rutColour = new Color(0f, 0.88f, 1f, 1f);
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false, true);
        var pixels = new Color[width * height];
        var dominant = new byte[width * height];
        int[] dominantCounts = new int[3];

        for (int z = 0; z < height; z++)
            for (int x = 0; x < width; x++)
            {
                int index = z * width + x;
                float road = alpha[z, x, 3];
                float rut = alpha[z, x, 4];
                int ecological = 0;
                if (alpha[z, x, 1] > alpha[z, x, ecological]) ecological = 1;
                if (alpha[z, x, 2] > alpha[z, x, ecological]) ecological = 2;
                bool isRoad = road + rut >= 0.25f;
                dominant[index] = isRoad ? byte.MaxValue : (byte)ecological;
                if (!isRoad) dominantCounts[ecological]++;

                Color colour;
                if (roadOnly)
                {
                    colour = roadColour * Mathf.Clamp01(road * 1.25f) +
                        rutColour * Mathf.Clamp01(rut * 1.75f);
                    colour.a = 1f;
                }
                else if (road + rut >= 0.25f)
                {
                    colour = Color.Lerp(roadColour, rutColour,
                        rut / Mathf.Max(0.0001f, road + rut));
                }
                else colour = colours[ecological];
                pixels[(height - 1 - z) * width + x] = colour;
            }
        texture.SetPixels(pixels);
        texture.Apply(false, false);
        string path = Path.Combine(Directory.GetCurrentDirectory(), fileName);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        int firstRouteRow = Mathf.Clamp(Mathf.RoundToInt(height * ((-58f + 95f) / 190f)),
            0, height - 1);
        int lastRouteRow = Mathf.Clamp(Mathf.RoundToInt(height * ((58f + 95f) / 190f)),
            0, height - 1);
        int failedRows = 0;
        int currentFailedRun = 0;
        int longestFailedRun = 0;
        for (int z = firstRouteRow; z <= lastRouteRow; z++)
        {
            float strongestRoad = 0f;
            for (int x = 0; x < width; x++)
                strongestRoad = Mathf.Max(strongestRoad, alpha[z, x, 3] + alpha[z, x, 4]);
            if (strongestRoad < 0.74f)
            {
                failedRows++;
                currentFailedRun++;
                longestFailedRun = Mathf.Max(longestFailedRun, currentFailedRun);
            }
            else currentFailedRun = 0;
        }
        int routeRows = lastRouteRow - firstRouteRow + 1;
        float roadCoverage = 1f - failedRows / (float)routeRows;
        bool roadPass = roadCoverage >= 0.985f && longestFailedRun <= 1;

        int ecologicalPixels = dominantCounts[0] + dominantCounts[1] + dominantCounts[2];
        bool ecologyPass = true;
        var topology = new string[3];
        for (int layer = 0; layer < 3; layer++)
        {
            int largest = LargestConnectedDominant(dominant, width, height, (byte)layer);
            float share = dominantCounts[layer] / (float)Mathf.Max(1, ecologicalPixels);
            float connectedShare = largest / (float)Mathf.Max(1, dominantCounts[layer]);
            ecologyPass &= share >= 0.03f && connectedShare >= 0.08f;
            topology[layer] = $"L{layer}={share * 100f:F1}%/largest{connectedShare * 100f:F1}%";
        }
        bool pass = roadOnly ? roadPass : roadPass && ecologyPass;
        Debug.Log($"[GmProbe] terrain alpha proof={(pass ? "PASS" : "FAIL")} file={path} " +
            $"roadCoverage={roadCoverage * 100f:F2}% longestGapRows={longestFailedRun} " +
            $"topology={string.Join(",", topology)}");
        if (!pass) Debug.LogError("[GmProbe] terrain alpha topology/continuity gate failed");
        return pass;
    }

    static int LargestConnectedDominant(byte[] dominant, int width, int height, byte layer)
    {
        var visited = new bool[dominant.Length];
        var queue = new int[dominant.Length];
        int largest = 0;
        for (int start = 0; start < dominant.Length; start++)
        {
            if (visited[start] || dominant[start] != layer) continue;
            int head = 0;
            int tail = 0;
            queue[tail++] = start;
            visited[start] = true;
            while (head < tail)
            {
                int index = queue[head++];
                int x = index % width;
                int z = index / width;
                if (x > 0) EnqueueDominant(index - 1, layer, dominant, visited, queue, ref tail);
                if (x + 1 < width) EnqueueDominant(index + 1, layer, dominant, visited, queue, ref tail);
                if (z > 0) EnqueueDominant(index - width, layer, dominant, visited, queue, ref tail);
                if (z + 1 < height) EnqueueDominant(index + width, layer, dominant, visited, queue, ref tail);
            }
            largest = Mathf.Max(largest, tail);
        }
        return largest;
    }

    static void EnqueueDominant(int index, byte layer, byte[] dominant, bool[] visited,
        int[] queue, ref int tail)
    {
        if (visited[index] || dominant[index] != layer) return;
        visited[index] = true;
        queue[tail++] = index;
    }

    static int Percentile(int[] histogram, int total, float percentile)
    {
        int target = Mathf.CeilToInt(total * percentile);
        int cumulative = 0;
        for (int i = 0; i < histogram.Length; i++)
        {
            cumulative += histogram[i];
            if (cumulative >= target) return i;
        }
        return histogram.Length - 1;
    }
}

