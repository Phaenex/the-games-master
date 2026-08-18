using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

public static class GmLabyrinthBuilder
{
    public const string SceneId = "labyrinth";
    public const string DisplayName = "The Labyrinth";
    public const string ScenePath = "Assets/Scenes/Labyrinth.unity";

    // 3D Asset Paths
    const string GatePath = "Assets/GamesMaster/Props/graveyard_gate.fbx";
    const string BrasierPath = "Assets/LeartesStudios/WitchVillage/HDRP/Art/Prefabs/SM_brasier.prefab";
    const string LanternPath = "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Prefabs/SM_Lantern.prefab";
    const string PostPath = "Assets/LeartesStudios/WitchVillage/HDRP/Art/Prefabs/SM_SculptedPost.prefab";
    const string TotemPath = "Assets/LeartesStudios/WitchVillage/HDRP/Art/Prefabs/SM_TwigTotems-1.prefab";
    const string BushBodyPath = "Assets/LeartesStudios/HauntedVillage/Art/Prefabs/SM_Bush_01.prefab";
    const string StoneWallPath = "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Prefabs/SM_StoneWall_01.prefab";
    const string MirrorPath = "Assets/ThirdParty/MetalManVictorianInteriors/Mirror_1.fbx";
    const string MirrorAlbedo = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Mirrors_Albedo.psd";
    const string MirrorNormal = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Mirrors_Normal.psd";
    const string GroundAlbedo = "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Textures/T_Moss_Ground_02_Albedo.PNG";
    const string GroundNormal = "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Textures/T_Moss_Ground_02_Normal.png";
    const string RockAlbedo = "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Textures/T_Rock_Albedo.PNG";
    const string RockNormal = "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Textures/T_Rock_Normal.png";

    static readonly Color IronOxide = new Color(0.14f, 0.14f, 0.13f);

    [MenuItem("GamesMaster/Scenes/Rebuild Labyrinth")]
    public static void Build()
    {
        Scene scene = GmSceneBuildUtility.CreateEmptyScene();
        var systems = new GameObject("SceneSystems");
        GmSceneBuildUtility.MarkScene(systems, SceneId, DisplayName);

        GmLabyrinthGenerator maze = systems.AddComponent<GmLabyrinthGenerator>();
        maze.GenerateDeterministicMaze();
        systems.AddComponent<GmHuntsmanAI>();
        systems.AddComponent<GmLabyrinthShotTour>();

        var environment = new GameObject("Environment");
        var gameplay = new GameObject("Gameplay");
        var lighting = new GameObject("Lighting");

        var parts = new GmLabyrinthSceneParts();
        BuildArchitecture(environment.transform, maze, parts);
        BuildGameplayProps(gameplay.transform, parts);
        BuildLighting(lighting.transform, parts);
        GmLabyrinthNightAtmosphere.Apply(lighting.transform, parts);

        var composition = new GameObject("Composition");
        GmLabyrinthCompositionPlan.Author(composition, parts);

        // A player, at last. This room had real, tested gameplay and nobody who could reach
        // it. Spawn is the viewpoint the ReviewCamera used to sit at -- the one vantage a
        // human already chose for this room -- so it is the least arbitrary spawn available,
        // and the review tour prefers the player's own camera when a player exists.
        // No HDRP atmosphere at all until now: every room builder had zero Volume/Exposure
        // references against the prologue's 31, so HDRP fell back to AUTOMATIC exposure and
        // opened up until a lamp-lit room rendered as a white box.
        GmPlayerRig.Build(null, new Vector3(-15f, 0f, -15f), new Vector3(0f, 1.2f, 0f));

        var arrival = systems.AddComponent<GmSceneArrival>();
        arrival.closingCard = "";

        GmSceneBuildUtility.SaveScene(scene, ScenePath);
        Debug.Log("[GmLabyrinth] BUILD PASS: " + ScenePath);
    }

    public static GameObject LoadMesh(string assetPath, string name, Transform parent, Vector3 localPos, Vector3 localScale, Quaternion localRot)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[GmLabyrinth] ASSET MISSING: {assetPath} — creating labeled placeholder for '{name}'");
            var fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallback.name = name + "_PLACEHOLDER";
            fallback.transform.SetParent(parent, false);
            fallback.transform.localPosition = localPos;
            fallback.transform.localScale = localScale;
            fallback.transform.localRotation = localRot;
            return fallback;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        go.transform.localRotation = localRot;
        return go;
    }

    static void BuildArchitecture(Transform parent, GmLabyrinthGenerator maze, GmLabyrinthSceneParts parts)
    {
        // Ground Terrain Plane
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "MazeGround";
        ground.transform.SetParent(parent, false);
        ground.transform.position = new Vector3(0f, -0.1f, 0f);
        ground.transform.localScale = new Vector3(36f, 0.2f, 36f);
        ground.GetComponent<Renderer>().sharedMaterial = CreatePbrMaterial(GroundAlbedo, GroundNormal,
            new Color(0.26f, 0.31f, 0.23f), 0.18f, 0f, new Vector2(9f, 9f));

        // Outer Boundary Walls
        // Leave a real four-metre opening at the authored gate. The old 36m wall ran straight
        // behind the gate, so reaching the visual exit still meant colliding with the boundary.
        BuildWall(parent, "NorthOuterWall_West", new Vector3(-2.5f, 2f, 18f), new Vector3(31f, 4f, 0.6f));
        BuildWall(parent, "NorthOuterWall_East", new Vector3(17.5f, 2f, 18f), new Vector3(1f, 4f, 0.6f));
        BuildWall(parent, "SouthOuterWall_West", new Vector3(-17.25f, 2f, -18f), new Vector3(1.5f, 4f, 0.6f));
        BuildWall(parent, "SouthOuterWall_East", new Vector3(2.25f, 2f, -18f), new Vector3(31.5f, 4f, 0.6f));
        BuildWall(parent, "EastOuterWall", new Vector3(18f, 2f, 0f), new Vector3(0.6f, 4f, 36f));
        BuildWall(parent, "WestOuterWall", new Vector3(-18f, 2f, 0f), new Vector3(0.6f, 4f, 36f));

        // Hedge blocks
        var hedges = new GameObject("MazeHedges");
        hedges.transform.SetParent(parent, false);
        for (int x = 0; x < GmLabyrinthGenerator.GridSize; x++)
        {
            for (int y = 0; y < GmLabyrinthGenerator.GridSize; y++)
            {
                if (!maze.WallGrid[x, y]) continue;
                Vector3 cell = GmLabyrinthGenerator.CellToWorldPos(new Vector2Int(x, y));
                BuildHedge(hedges.transform, $"Hedge_{x}_{y}", new Vector3(cell.x, 1.8f, cell.z),
                    new Vector3(4f, 3.6f, 4f));
            }
        }
        parts.HedgeWalls = hedges;

        // Entrance Archway
        var arch = new GameObject("EntranceCryptArch");
        arch.transform.SetParent(parent, false);
        arch.transform.position = new Vector3(-15f, 0f, -17f);
        for (int side = -1; side <= 1; side += 2)
            LoadMesh(PostPath, side < 0 ? "EntrancePostWest" : "EntrancePostEast", arch.transform,
                new Vector3(side * 1.25f, 0f, 0f), new Vector3(1.35f, 1.35f, 1.35f),
                Quaternion.identity);
        LoadMesh(StoneWallPath, "EntranceStoneLintel", arch.transform,
            new Vector3(0f, 3.25f, 0f), new Vector3(1f, 0.9f, 1f),
            Quaternion.Euler(0f, 90f, 0f));
        parts.CryptArch = arch;

        var sconces = new GameObject("EntranceTorchSconces");
        sconces.transform.SetParent(parent, false);
        sconces.transform.position = new Vector3(-15f, 0f, -16.35f);
        for (int side = -1; side <= 1; side += 2)
            LoadMesh(BrasierPath, side < 0 ? "EntranceBrazierWest" : "EntranceBrazierEast",
                sconces.transform, new Vector3(side * 1.05f, 0f, 0f),
                new Vector3(0.62f, 0.62f, 0.62f), Quaternion.identity);
        parts.TorchSconce = sconces;

        // Gravel approach path
        GameObject gravel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gravel.name = "GravelApproachPath";
        gravel.transform.SetParent(parent, false);
        gravel.transform.position = new Vector3(-15f, 0.03f, -14f);
        gravel.transform.localScale = new Vector3(2.4f, 0.06f, 4f);
        gravel.GetComponent<Renderer>().sharedMaterial = CreatePbrMaterial(RockAlbedo, RockNormal,
            new Color(0.44f, 0.40f, 0.34f), 0.12f, 0f, new Vector2(1.5f, 3f));
        GmSceneBuildUtility.MakeDecorativeOverlay(gravel);
        parts.GravelPath = gravel;

        // Exit Wrought Gate (graveyard_gate FBX)
        // Unlike most props here, graveyard_gate's pivot is at MID-HEIGHT (local Y -1.5843..+1.4172),
        // so Y=0 buried the bottom 1.90m of the gate under the ground plane. Lift by 1.2 * 1.5843 to
        // stand it on the floor; it then spans Y 0..3.60 against flanking piers that span 0..3.2.
        GameObject exitGate = LoadMesh(GatePath, "ExitWroughtGate", parent,
            new Vector3(15f, 1.9012f, 17f), new Vector3(1.2f, 1.2f, 1.2f), Quaternion.identity);
        ApplyMaterialTree(exitGate, IronOxide, 0.72f, 0.24f);
        parts.ExitGate = exitGate;

        var endingVolume = new GameObject("EndingThreshold");
        endingVolume.transform.SetParent(parent, false);
        endingVolume.transform.position = new Vector3(15f, 1.2f, 16.8f);
        var endingCollider = endingVolume.AddComponent<BoxCollider>();
        endingCollider.isTrigger = true;
        endingCollider.size = new Vector3(3.4f, 2.4f, 1.0f);
        endingVolume.AddComponent<GmEndingTrigger>();

        // Twin piers flanking gate
        var piers = new GameObject("ExitStonePiers");
        piers.transform.SetParent(parent, false);
        piers.transform.position = new Vector3(15f, 0f, 17f);
        BuildPier(piers.transform, "ExitPier_West", new Vector3(-2f, 0f, 0f));
        BuildPier(piers.transform, "ExitPier_East", new Vector3(2f, 0f, 0f));
        parts.StonePiers = piers;

        var exitLanterns = new GameObject("ExitPierLanterns");
        exitLanterns.transform.SetParent(parent, false);
        exitLanterns.transform.position = new Vector3(15f, 3.95f, 16.75f);
        LoadMesh(LanternPath, "ExitLanternWest", exitLanterns.transform,
            new Vector3(-2f, 0f, 0f), Vector3.one * 2.1f, Quaternion.identity);
        LoadMesh(LanternPath, "ExitLanternEast", exitLanterns.transform,
            new Vector3(2f, 0f, 0f), Vector3.one * 2.1f, Quaternion.identity);
        parts.ExitLanterns = exitLanterns;

    }

    static void BuildGameplayProps(Transform parent, GmLabyrinthSceneParts parts)
    {
        // Central Mirror Shrine (0, 0, 0)
        GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pedestal.name = "MirrorShrinePedestal";
        pedestal.transform.SetParent(parent, false);
        pedestal.transform.position = new Vector3(0f, 0.6f, 0f);
        pedestal.transform.localScale = new Vector3(1.6f, 0.6f, 1.6f);
        pedestal.GetComponent<Renderer>().sharedMaterial = CreatePbrMaterial(RockAlbedo, RockNormal,
            new Color(0.46f, 0.49f, 0.52f), 0.32f, 0f, new Vector2(2f, 1f));
        parts.MirrorPedestal = pedestal;

        GameObject mirror = LoadMesh(MirrorPath, "AssembledMirror", parent,
            new Vector3(0f, 1.2f, 0f), new Vector3(1.15f, 1.15f, 1.15f),
            Quaternion.identity);
        ApplyPbrTree(mirror, MirrorAlbedo, MirrorNormal, new Color(0.72f, 0.76f, 0.82f), 0.78f, 0.32f);
        parts.AssembledMirror = mirror;

        // 4 Stone Pillars surrounding Altar (SM_SculptedPost FBX)
        var pillars = new GameObject("ShrinePillars");
        pillars.transform.SetParent(parent, false);
        pillars.transform.position = new Vector3(0f, 0f, 0f);
        Vector3[] pillarOffsets = { new Vector3(-2.5f, 0f, -2.5f), new Vector3(2.5f, 0f, -2.5f),
                                    new Vector3(-2.5f, 0f, 2.5f), new Vector3(2.5f, 0f, 2.5f) };
        for (int i = 0; i < pillarOffsets.Length; i++)
        {
            GameObject pillar = LoadMesh(PostPath, $"ShrinePillar_{i + 1}", pillars.transform,
                pillarOffsets[i], new Vector3(1.2f, 1.2f, 1.2f), Quaternion.identity);
        }
        parts.ShrinePillars = pillars;

        // Huntsman Lantern Prop (SM_Lantern FBX)
        GameObject lantern = LoadMesh(LanternPath, "HuntsmanLantern", parent,
            new Vector3(5f, 1.0f, -5f), new Vector3(2.4f, 2.4f, 2.4f), Quaternion.identity);
        parts.HuntsmanLantern = lantern;

        // Patrol Track
        GameObject track = GameObject.CreatePrimitive(PrimitiveType.Cube);
        track.name = "HuntsmanPatrolTrack";
        track.transform.SetParent(parent, false);
        track.transform.position = new Vector3(5f, 0.03f, -5f);
        track.transform.localScale = new Vector3(1.6f, 0.06f, 10f);
        track.GetComponent<Renderer>().sharedMaterial = CreatePbrMaterial(GroundAlbedo, GroundNormal,
            new Color(0.27f, 0.20f, 0.14f), 0.08f, 0f, new Vector2(1f, 5f));
        GmSceneBuildUtility.MakeDecorativeOverlay(track);
        parts.PatrolTrack = track;

        // Bone Totem (SM_TwigTotems-1 FBX)
        GameObject totem = LoadMesh(TotemPath, "BoneTotem", parent,
            new Vector3(4.5f, 0f, -4f), new Vector3(0.8f, 0.8f, 0.8f), Quaternion.identity);
        parts.BoneTotem = totem;
    }

    static void BuildLighting(Transform parent, GmLabyrinthSceneParts parts)
    {
        GameObject moonbeamObj = new GameObject("MoonbeamAltarLight");
        moonbeamObj.transform.SetParent(parent, false);
        moonbeamObj.transform.position = new Vector3(0f, 7.5f, 0f);
        moonbeamObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        Light moonbeam = moonbeamObj.AddComponent<Light>();
        moonbeam.type = LightType.Spot;
        moonbeam.range = 10f;
        moonbeam.spotAngle = 32f;
        moonbeam.color = new Color(0.65f, 0.80f, 1.0f);
        moonbeam.lightUnit = LightUnit.Lumen;
        moonbeam.intensity = 800f;
        HDAdditionalLightData moonbeamHd = moonbeamObj.AddComponent<HDAdditionalLightData>();
        moonbeamHd.affectsVolumetric = true;
        moonbeamHd.volumetricDimmer = 0.55f;
        parts.MoonbeamLight = moonbeam;

        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = "MoonbeamShaft";
        shaft.transform.SetParent(parent, false);
        shaft.transform.position = new Vector3(0f, 1.22f, 0f);
        shaft.transform.localScale = new Vector3(1.7f, 0.018f, 1.7f);
        ApplyMaterial(shaft, "HDRP/Lit", new Color(0.16f, 0.27f, 0.48f), 0.35f, 0.72f);
        StripCollider(shaft);
        parts.MoonbeamShaft = shaft;

        for (int side = -1; side <= 1; side += 2)
        {
            GameObject torchLightObj = new GameObject(side < 0 ? "EntranceTorchLightWest" : "EntranceTorchLightEast");
            torchLightObj.transform.SetParent(parent, false);
            torchLightObj.transform.position = new Vector3(-15f + side * 1.05f, 1.2f, -16.2f);
            Light torchLight = torchLightObj.AddComponent<Light>();
            torchLight.type = LightType.Point;
            torchLight.range = 4f;
            torchLight.color = new Color(1.0f, 0.60f, 0.20f);
            torchLight.lightUnit = LightUnit.Lumen;
            torchLight.intensity = 60f;
            HDAdditionalLightData torchHd = torchLightObj.AddComponent<HDAdditionalLightData>();
            torchHd.affectsVolumetric = true;
            torchHd.volumetricDimmer = 0.35f;
            if (side < 0) parts.TorchLight = torchLight; else parts.TorchLightRight = torchLight;
        }

        GameObject huntsmanLightObj = new GameObject("HuntsmanLanternLight");
        huntsmanLightObj.transform.SetParent(parent, false);
        huntsmanLightObj.transform.position = new Vector3(5f, 1.4f, -5f);
        Light huntsmanLight = huntsmanLightObj.AddComponent<Light>();
        huntsmanLight.type = LightType.Point;
        huntsmanLight.range = 5f;
        huntsmanLight.color = new Color(1.0f, 0.70f, 0.30f);
        huntsmanLight.lightUnit = LightUnit.Lumen;
        huntsmanLight.intensity = 60f;
        huntsmanLightObj.AddComponent<HDAdditionalLightData>();
        // Handed to the plan so it can carry authored intent. Without this the plan structurally
        // COULD NOT motivate it -- there was no field to reach it through -- so the brightest local
        // light in the scene answered to nothing.
        parts.HuntsmanLanternLight = huntsmanLight;

        for (int side = -1; side <= 1; side += 2)
        {
            GameObject exitLightObject = new GameObject(side < 0 ? "ExitLanternLightWest" : "ExitLanternLightEast");
            exitLightObject.transform.SetParent(parent, false);
            exitLightObject.transform.position = new Vector3(15f + side * 2f, 4.2f, 16.75f);
            Light exitLight = exitLightObject.AddComponent<Light>();
            exitLight.type = LightType.Point;
            exitLight.range = 5f;
            exitLight.color = new Color(1f, 0.68f, 0.30f);
            exitLight.lightUnit = LightUnit.Lumen;
            exitLight.intensity = 60f;
            exitLightObject.AddComponent<HDAdditionalLightData>();
            if (side < 0) parts.ExitLanternLeftLight = exitLight; else parts.ExitLanternRightLight = exitLight;
        }
    }

    static void BuildWall(Transform parent, string name, Vector3 pos, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent, false);
        wall.transform.position = pos;
        wall.transform.localScale = scale;
        wall.GetComponent<Renderer>().sharedMaterial = CreatePbrMaterial(RockAlbedo, RockNormal,
            new Color(0.35f, 0.37f, 0.39f), 0.22f, 0f,
            new Vector2(Mathf.Max(1f, scale.x / 4f), Mathf.Max(1f, scale.y / 2f)));
    }

    static void BuildHedge(Transform parent, string name, Vector3 pos, Vector3 scale)
    {
        var hedge = new GameObject(name);
        hedge.transform.SetParent(parent, false);
        hedge.transform.position = pos - new Vector3(0f, scale.y * 0.5f, 0f);
        var blocker = hedge.AddComponent<BoxCollider>();
        blocker.center = new Vector3(0f, scale.y * 0.5f, 0f);
        blocker.size = scale;

        int variation = Mathf.Abs(Mathf.RoundToInt(pos.x + pos.z)) % 4;
        LoadMesh(StoneWallPath, name + "_StoneNorthSouth", hedge.transform, Vector3.zero,
            new Vector3(1.05f, 3.25f, 1f), Quaternion.identity);
        LoadMesh(StoneWallPath, name + "_StoneEastWest", hedge.transform, Vector3.zero,
            new Vector3(1.05f, 3.25f, 1f), Quaternion.Euler(0f, 90f, 0f));
        LoadMesh(BushBodyPath, name + "_Body", hedge.transform, Vector3.zero,
            new Vector3(0.42f, 0.80f, 0.40f), Quaternion.Euler(0f, variation * 90f, 0f));
    }

    static void BuildPier(Transform parent, string name, Vector3 pos)
    {
        LoadMesh(PostPath, name, parent, pos, Vector3.one * 1.38f, Quaternion.identity);
    }

    static void StripCollider(GameObject go)
    {
        Collider col = go.GetComponent<Collider>();
        if (col != null) UnityEngine.Object.DestroyImmediate(col);
    }

    static void ApplyMaterial(GameObject go, string shaderName, Color color, float metallic, float smoothness)
    {
        Renderer r = go.GetComponent<Renderer>();
        if (r == null) return;
        Shader s = Shader.Find(shaderName);
        if (s == null) s = Shader.Find("HDRP/Lit");
        Material mat = new Material(s);
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        r.sharedMaterial = mat;
    }

    static void ApplyMaterialTree(GameObject go, Color color, float metallic, float smoothness)
    {
        Shader shader = Shader.Find("HDRP/Lit");
        var material = new Material(shader);
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true))
        {
            var materials = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            renderer.sharedMaterials = materials;
        }
    }

    static Material CreatePbrMaterial(string albedoPath, string normalPath, Color tint,
        float smoothness, float metallic, Vector2 tiling)
    {
        var material = new Material(Shader.Find("HDRP/Lit"));
        material.SetColor("_BaseColor", tint);
        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        if (albedo != null) material.SetTexture("_BaseColorMap", albedo);
        if (normal != null)
        {
            material.SetTexture("_NormalMap", normal);
            material.EnableKeyword("_NORMALMAP");
        }
        material.SetTextureScale("_BaseColorMap", tiling);
        material.SetTextureScale("_NormalMap", tiling);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", metallic);
        return material;
    }

    static void ApplyPbrTree(GameObject go, string albedoPath, string normalPath, Color tint,
        float smoothness, float metallic)
    {
        Material material = CreatePbrMaterial(albedoPath, normalPath, tint, smoothness, metallic, Vector2.one);
        foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true))
        {
            var materials = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            renderer.sharedMaterials = materials;
        }
    }
}
