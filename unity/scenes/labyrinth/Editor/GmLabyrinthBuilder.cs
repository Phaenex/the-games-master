using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

public static class GmLabyrinthBuilder
{
    public const string SceneId = "labyrinth";
    public const string DisplayName = "The Labyrinth";
    public const string ScenePath = "Assets/Scenes/Labyrinth.unity";

    // 3D Asset Paths
    const string GatePath = "Assets/GamesMaster/Props/graveyard_gate.fbx";
    const string BrasierPath = "Assets/LeartesStudios/WitchVillage/HDRP/Art/Meshes/MediumProps/Brasier/SM_brasier.fbx";
    const string LanternPath = "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Meshes/Props/SM_Lantern.fbx";
    const string PostPath = "Assets/LeartesStudios/WitchVillage/HDRP/Art/Meshes/Architecture/SculptedPost/SM_SculptedPost.fbx";
    const string TotemPath = "Assets/LeartesStudios/WitchVillage/HDRP/Art/Meshes/SmallProps/TwigTotem/SM_TwigTotems-1.fbx";

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

        var composition = new GameObject("Composition");
        GmLabyrinthCompositionPlan.Author(composition, parts);

        // A player, at last. This room had real, tested gameplay and nobody who could reach
        // it. Spawn is the viewpoint the ReviewCamera used to sit at -- the one vantage a
        // human already chose for this room -- so it is the least arbitrary spawn available,
        // and the review tour prefers the player's own camera when a player exists.
        GmPlayerRig.Build(null, new Vector3(-15f, 0f, -15f), new Vector3(0f, 1.2f, 0f));

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
        ApplyMaterial(ground, "HDRP/Lit", new Color(0.12f, 0.16f, 0.10f), 0.0f, 0.2f);

        // Outer Boundary Walls
        BuildWall(parent, "NorthOuterWall", new Vector3(0f, 2f, 18f), new Vector3(36f, 4f, 0.6f));
        BuildWall(parent, "SouthOuterWall", new Vector3(0f, 2f, -18f), new Vector3(36f, 4f, 0.6f));
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
        GameObject arch = GameObject.CreatePrimitive(PrimitiveType.Cube);
        arch.name = "EntranceCryptArch";
        arch.transform.SetParent(parent, false);
        arch.transform.position = new Vector3(-15f, 2f, -17f);
        arch.transform.localScale = new Vector3(3f, 4f, 0.8f);
        ApplyMaterial(arch, "HDRP/Lit", new Color(0.25f, 0.25f, 0.22f), 0.0f, 0.3f);
        parts.CryptArch = arch;

        // Iron sconce on arch (SM_brasier FBX)
        GameObject sconce = LoadMesh(BrasierPath, "EntranceTorchSconce", parent,
            new Vector3(-15f, 2.2f, -16.35f), new Vector3(0.6f, 0.6f, 0.6f), Quaternion.identity);
        parts.TorchSconce = sconce;

        // Gravel approach path
        GameObject gravel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gravel.name = "GravelApproachPath";
        gravel.transform.SetParent(parent, false);
        gravel.transform.position = new Vector3(-15f, 0.03f, -14f);
        gravel.transform.localScale = new Vector3(2.4f, 0.06f, 4f);
        ApplyMaterial(gravel, "HDRP/Lit", new Color(0.22f, 0.21f, 0.19f), 0.0f, 0.15f);
        GmSceneBuildUtility.MakeDecorativeOverlay(gravel);
        parts.GravelPath = gravel;

        // Exit Wrought Gate (graveyard_gate FBX)
        // Unlike most props here, graveyard_gate's pivot is at MID-HEIGHT (local Y -1.5843..+1.4172),
        // so Y=0 buried the bottom 1.90m of the gate under the ground plane. Lift by 1.2 * 1.5843 to
        // stand it on the floor; it then spans Y 0..3.60 against flanking piers that span 0..3.2.
        GameObject exitGate = LoadMesh(GatePath, "ExitWroughtGate", parent,
            new Vector3(15f, 1.9012f, 17f), new Vector3(1.2f, 1.2f, 1.2f), Quaternion.identity);
        parts.ExitGate = exitGate;

        // Twin piers flanking gate
        var piers = new GameObject("ExitStonePiers");
        piers.transform.SetParent(parent, false);
        piers.transform.position = new Vector3(15f, 1.6f, 17f);
        BuildPier(piers.transform, "ExitPier_West", new Vector3(13f, 1.6f, 17f));
        BuildPier(piers.transform, "ExitPier_East", new Vector3(17f, 1.6f, 17f));
        parts.StonePiers = piers;

        GameObject mist = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mist.name = "ExitGroundMist";
        mist.transform.SetParent(parent, false);
        mist.transform.position = new Vector3(15f, 0.15f, 16f);
        mist.transform.localScale = new Vector3(7f, 0.3f, 3f);
        ApplyMaterial(mist, "HDRP/Lit", new Color(0.55f, 0.58f, 0.62f), 0.0f, 0.05f);
        StripCollider(mist);
        parts.ExitMist = mist;
    }

    static void BuildGameplayProps(Transform parent, GmLabyrinthSceneParts parts)
    {
        // Central Mirror Shrine (0, 0, 0)
        GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pedestal.name = "MirrorShrinePedestal";
        pedestal.transform.SetParent(parent, false);
        pedestal.transform.position = new Vector3(0f, 0.6f, 0f);
        pedestal.transform.localScale = new Vector3(1.6f, 0.6f, 1.6f);
        ApplyMaterial(pedestal, "HDRP/Lit", new Color(0.35f, 0.35f, 0.32f), 0.0f, 0.4f);
        parts.MirrorPedestal = pedestal;

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
            new Vector3(5f, 1.4f, -5f), new Vector3(0.5f, 0.5f, 0.5f), Quaternion.identity);
        parts.HuntsmanLantern = lantern;

        // Patrol Track
        GameObject track = GameObject.CreatePrimitive(PrimitiveType.Cube);
        track.name = "HuntsmanPatrolTrack";
        track.transform.SetParent(parent, false);
        track.transform.position = new Vector3(5f, 0.03f, -5f);
        track.transform.localScale = new Vector3(1.6f, 0.06f, 10f);
        ApplyMaterial(track, "HDRP/Lit", new Color(0.15f, 0.13f, 0.10f), 0.0f, 0.1f);
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
        moonbeamObj.transform.position = new Vector3(0f, 12f, 0f);
        moonbeamObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        Light moonbeam = moonbeamObj.AddComponent<Light>();
        moonbeam.type = LightType.Spot;
        moonbeam.range = 16f;
        moonbeam.spotAngle = 40f;
        moonbeam.color = new Color(0.65f, 0.80f, 1.0f);
        moonbeam.intensity = 800f;
        moonbeamObj.AddComponent<HDAdditionalLightData>();
        parts.MoonbeamLight = moonbeam;

        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = "MoonbeamShaft";
        shaft.transform.SetParent(parent, false);
        shaft.transform.position = new Vector3(0f, 4.5f, 0f);
        shaft.transform.localScale = new Vector3(3.2f, 3.8f, 3.2f);
        ApplyMaterial(shaft, "HDRP/Lit", new Color(0.70f, 0.85f, 1.0f), 0.0f, 0.05f);
        StripCollider(shaft);
        parts.MoonbeamShaft = shaft;

        GameObject torchLightObj = new GameObject("EntranceTorchLight");
        torchLightObj.transform.SetParent(parent, false);
        torchLightObj.transform.position = new Vector3(-15f, 2.7f, -16.2f);
        Light torchLight = torchLightObj.AddComponent<Light>();
        torchLight.type = LightType.Point;
        torchLight.range = 7f;
        torchLight.color = new Color(1.0f, 0.60f, 0.20f);
        torchLight.intensity = 350f;
        torchLightObj.AddComponent<HDAdditionalLightData>();
        parts.TorchLight = torchLight;

        GameObject huntsmanLightObj = new GameObject("HuntsmanLanternLight");
        huntsmanLightObj.transform.SetParent(parent, false);
        huntsmanLightObj.transform.position = new Vector3(5f, 1.4f, -5f);
        Light huntsmanLight = huntsmanLightObj.AddComponent<Light>();
        huntsmanLight.type = LightType.Point;
        huntsmanLight.range = 5f;
        huntsmanLight.color = new Color(1.0f, 0.70f, 0.30f);
        huntsmanLight.intensity = 200f;
        huntsmanLightObj.AddComponent<HDAdditionalLightData>();
        // Handed to the plan so it can carry authored intent. Without this the plan structurally
        // COULD NOT motivate it -- there was no field to reach it through -- so the brightest local
        // light in the scene answered to nothing.
        parts.HuntsmanLanternLight = huntsmanLight;
    }

    static void BuildWall(Transform parent, string name, Vector3 pos, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent, false);
        wall.transform.position = pos;
        wall.transform.localScale = scale;
        ApplyMaterial(wall, "HDRP/Lit", new Color(0.18f, 0.18f, 0.16f), 0.0f, 0.25f);
    }

    static void BuildHedge(Transform parent, string name, Vector3 pos, Vector3 scale)
    {
        GameObject hedge = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hedge.name = name;
        hedge.transform.SetParent(parent, false);
        hedge.transform.position = pos;
        hedge.transform.localScale = scale;
        ApplyMaterial(hedge, "HDRP/Lit", new Color(0.08f, 0.14f, 0.06f), 0.0f, 0.15f);
    }

    static void BuildPier(Transform parent, string name, Vector3 pos)
    {
        GameObject pier = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pier.name = name;
        pier.transform.SetParent(parent, false);
        pier.transform.position = pos;
        pier.transform.localScale = new Vector3(1.2f, 3.2f, 1.2f);
        ApplyMaterial(pier, "HDRP/Lit", new Color(0.28f, 0.28f, 0.25f), 0.0f, 0.35f);
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
}
