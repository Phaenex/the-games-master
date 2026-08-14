using GamesMaster.ShutTheBox;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

public static class GmShutTheBoxBuilder
{
    public const string SceneId = "shut-the-box";
    public const string DisplayName = "Shut the Box";
    public const string ScenePath = "Assets/Scenes/ShutTheBox.unity";

    // 3D Asset Paths
    const string TablePath = "Assets/ThirdParty/MetalManVictorianInteriors/Table_3.fbx";
    const string ChairPath = "Assets/ThirdParty/MetalManVictorianInteriors/Chair_1.fbx";
    const string Chair2Path = "Assets/ThirdParty/MetalManVictorianInteriors/Chair_2.fbx";
    const string BookshelfPath = "Assets/ThirdParty/MetalManVictorianInteriors/BookShelf_1.fbx";
    const string CarpetPath = "Assets/ThirdParty/MetalManVictorianInteriors/Carpet_1.fbx";
    const string LampPath = "Assets/ThirdParty/MetalManVictorianInteriors/Lamp_2.fbx";
    const string DoorPath = "Assets/LeartesStudios/HauntedVillage/Art/Meshes/SM_Door_01.fbx";
    const string CandlePath = "Assets/LeartesStudios/WitchVillage/HDRP/Art/Meshes/SmallProps/Candles/SM_Candles_1.fbx";
    const string Book1Path = "Assets/LeartesStudios/WitchVillage/HDRP/Art/Meshes/SmallProps/Books/SM_Book_1.fbx";

    // PBR Textures
    const string WallpaperAlbedo = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Wall_1_Albedo.psd";
    const string WallpaperNormal = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Wall_1_Normal.png";
    const string FloorAlbedo = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Floor_Albedo.png";
    const string FloorNormal = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Floor_Normal.png";
    const string TableAlbedo = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Tables_2_Albedo.psd";
    const string TableNormal = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Tables_2_Normal.psd";
    const string BookshelfAlbedo = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Bookshelf_Albedo.psd";
    const string BookshelfNormal = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Bookshelf_Normal.psd";
    const string ChairAlbedo = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Chairs_Albedo.psd";
    const string ChairNormal = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Chairs_Normal.psd";
    const string CarpetAlbedo = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Carpets_Albedo.psd";
    const string CarpetNormal = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Carpets_Normal.psd";
    const string LampAlbedo = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Lamp_Albedo.psd";
    const string LampNormal = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Lamp_Normal.psd";

    [MenuItem("GamesMaster/Scenes/Rebuild Shut the Box")]
    public static void Build()
    {
        Scene scene = GmSceneBuildUtility.CreateEmptyScene();
        var systems = new GameObject("SceneSystems");
        GmSceneBuildUtility.MarkScene(systems, SceneId, DisplayName);
        systems.AddComponent<GmShutTheBoxController>();
        systems.AddComponent<GmShutTheBoxShotTour>();

        var environment = new GameObject("Environment");
        var gameplay = new GameObject("Gameplay");
        var lighting = new GameObject("Lighting");

        var refs = new GmShutTheBoxCompositionPlan.SceneRefs();
        BuildArchitecture(environment.transform, refs);
        BuildGameplayProps(gameplay.transform, refs);
        BuildLighting(lighting.transform, refs);

        var composition = new GameObject("Composition");
        GmShutTheBoxCompositionPlan.Author(composition, refs);

        var cameraRig = new GameObject("ReviewCamera");
        cameraRig.transform.position = new Vector3(0f, 1.4f, -1.2f);
        cameraRig.transform.rotation = Quaternion.Euler(25f, 0f, 0f);
        cameraRig.AddComponent<Camera>();
        cameraRig.AddComponent<HDAdditionalCameraData>();
        cameraRig.AddComponent<AudioListener>();
        cameraRig.tag = "MainCamera";

        GmSceneBuildUtility.SaveScene(scene, ScenePath);
        Debug.Log("[GmShutTheBox] BUILD PASS: " + ScenePath);
    }

    public static Material CreatePbrMaterial(string albedoPath, string normalPath, Color tint, float smoothness, float metallic, Vector2 tiling)
    {
        var mat = new Material(Shader.Find("HDRP/Lit"));
        mat.color = tint;
        if (!string.IsNullOrEmpty(albedoPath))
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            if (tex != null) mat.SetTexture("_BaseColorMap", tex);
        }
        if (!string.IsNullOrEmpty(normalPath))
        {
            var norm = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (norm != null)
            {
                mat.SetTexture("_NormalMap", norm);
                mat.EnableKeyword("_NORMALMAP");
            }
        }
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", metallic);
        mat.SetTextureScale("_BaseColorMap", tiling);
        mat.SetTextureScale("_NormalMap", tiling);
        return mat;
    }

    public static void ApplyPbr(GameObject go, string albedoPath, string normalPath, float smoothness = 0.35f, float metallic = 0f, Color? tint = null)
    {
        if (go == null) return;
        Color c = tint ?? Color.white;
        Material mat = CreatePbrMaterial(albedoPath, normalPath, c, smoothness, metallic, Vector2.one);
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var count = r.sharedMaterials.Length;
            var mats = new Material[count];
            for (int i = 0; i < count; i++) mats[i] = mat;
            r.sharedMaterials = mats;
        }
    }

    public static GameObject LoadMesh(string assetPath, string name, Transform parent, Vector3 localPos, Vector3 localScale, Quaternion localRot)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[GmShutTheBox] ASSET MISSING: {assetPath} — creating labeled placeholder for '{name}'");
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

    static void BuildArchitecture(Transform parent, GmShutTheBoxCompositionPlan.SceneRefs refs)
    {
        Material wallMat = CreatePbrMaterial(WallpaperAlbedo, WallpaperNormal, new Color(0.60f, 0.55f, 0.50f), 0.15f, 0f, new Vector2(4f, 2f));
        Material floorMat = CreatePbrMaterial(FloorAlbedo, FloorNormal, new Color(0.50f, 0.40f, 0.30f), 0.35f, 0f, new Vector2(4f, 4f));

        // Floor
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "AlcoveFloor";
        floor.transform.SetParent(parent, false);
        floor.transform.position = new Vector3(0f, -0.1f, 0f);
        floor.transform.localScale = new Vector3(8f, 0.2f, 8f);
        floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

        // Persian Rug
        var rug = LoadMesh(CarpetPath, "AlcoveCarpet", parent,
            new Vector3(0f, 0.01f, 0f), new Vector3(1.6f, 1f, 1.6f), Quaternion.identity);
        ApplyPbr(rug, CarpetAlbedo, CarpetNormal, 0.15f, 0f);

        // North Wall
        GameObject northWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        northWall.name = "NorthWall";
        northWall.transform.SetParent(parent, false);
        northWall.transform.position = new Vector3(0f, 2f, 4f);
        northWall.transform.localScale = new Vector3(8f, 4f, 0.3f);
        northWall.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

        // East Wall
        GameObject eastWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        eastWall.name = "EastWall";
        eastWall.transform.SetParent(parent, false);
        eastWall.transform.position = new Vector3(4f, 2f, 0f);
        eastWall.transform.localScale = new Vector3(0.3f, 4f, 8f);
        eastWall.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

        // Secret Panel Door (Tile-9 Latch)
        // Two errors, and fixing only the height leaves the shot still failing. SM_Door_01's pivot is
        // at its base, so Y=1.2 floated the leaf; and yaw -90 turned it perpendicular to the east
        // wall, so its 1.02m width jutted into the room. The wainscot runs leave a 1.2m doorway gap
        // at z in (-0.6, +0.6) and BrassSeam is a vertical hairline at z=-0.6 -- the leaf belongs in
        // that gap with its free edge on the seam, which yaw 180 gives (hinge at +0.427).
        GameObject panelDoor = LoadMesh(DoorPath, "PanelDoor", parent,
            new Vector3(3.78f, 0f, 0.42f), new Vector3(0.9f, 1.1f, 1f), Quaternion.Euler(0, 180f, 0));
        refs.panelDoor = panelDoor;

        // Wainscoting flanking the door
        var wainscot = new GameObject("DoorWainscot");
        wainscot.transform.SetParent(parent, false);
        wainscot.transform.position = new Vector3(3.8f, 0.55f, 0f);
        Cube(wainscot.transform, "WainscotSouth", new Vector3(3.8f, 0.55f, -2.3f),
            new Vector3(0.06f, 1.1f, 3.4f), new Color(0.24f, 0.15f, 0.10f), 0.05f, 0.4f);
        Cube(wainscot.transform, "WainscotNorth", new Vector3(3.8f, 0.55f, 2.3f),
            new Vector3(0.06f, 1.1f, 3.4f), new Color(0.24f, 0.15f, 0.10f), 0.05f, 0.4f);
        refs.doorWainscot = wainscot;

        // Hairline seam down the door's leading edge
        refs.brassSeam = Cube(parent, "BrassSeam", new Vector3(3.79f, 1.2f, -0.6f),
            new Vector3(0.02f, 2.4f, 0.02f), new Color(0.62f, 0.48f, 0.22f), 0.85f, 0.7f);

        // South & West Walls
        GameObject southWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        southWall.name = "SouthWall";
        southWall.transform.SetParent(parent, false);
        southWall.transform.position = new Vector3(0f, 2f, -4f);
        southWall.transform.localScale = new Vector3(8f, 4f, 0.3f);
        southWall.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

        GameObject westWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        westWall.name = "WestWall";
        westWall.transform.SetParent(parent, false);
        westWall.transform.position = new Vector3(-4f, 2f, 0f);
        westWall.transform.localScale = new Vector3(0.3f, 4f, 8f);
        westWall.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

        // Victorian Bookshelf with books
        // BookShelf_1's pivot is at its base (local Y 0.0000..3.1764), so Y=1.1 floated it. The same
        // mesh at the same 0.8 scale is placed at Y=0 in GmHiddenRoomBuilder's ArchiveShelf and
        // passes its grounded check there.
        var shelf = LoadMesh(BookshelfPath, "Bookshelf", parent,
            new Vector3(-1.4f, 0f, 3.55f), new Vector3(0.8f, 0.8f, 0.8f), Quaternion.identity);
        ApplyPbr(shelf, BookshelfAlbedo, BookshelfNormal, 0.40f, 0f);
        refs.bookshelf = shelf;

        LoadMesh(Book1Path, "AlcoveBook_1", parent,
            new Vector3(-1.4f, 1.0f, 3.55f), new Vector3(0.6f, 0.6f, 0.6f), Quaternion.identity);

        refs.wallClock = Cube(parent, "WallClock", new Vector3(0.2f, 2.4f, 3.8f),
            new Vector3(0.42f, 0.42f, 0.08f), new Color(0.30f, 0.20f, 0.12f), 0.1f, 0.45f);
    }

    static void BuildGameplayProps(Transform parent, GmShutTheBoxCompositionPlan.SceneRefs refs)
    {
        // Gaming Table (Table_3 FBX)
        GameObject table = LoadMesh(TablePath, "GameTable", parent,
            new Vector3(0f, 0f, 0f), new Vector3(1.1f, 0.9f, 1.1f), Quaternion.identity);
        ApplyPbr(table, TableAlbedo, TableNormal, 0.45f, 0.05f);

        // Player and Host Armchairs
        var playerChair = LoadMesh(ChairPath, "PlayerChair", parent,
            new Vector3(0f, 0f, -1.2f), new Vector3(0.9f, 0.9f, 0.9f), Quaternion.identity);
        ApplyPbr(playerChair, ChairAlbedo, ChairNormal, 0.35f, 0f);

        var hostChair = LoadMesh(Chair2Path, "HostChair", parent,
            new Vector3(0f, 0f, 1.2f), new Vector3(0.9f, 0.9f, 0.9f), Quaternion.Euler(0, 180f, 0));
        ApplyPbr(hostChair, ChairAlbedo, ChairNormal, 0.35f, 0f);

        // Tabletop Sculpted Candles
        LoadMesh(CandlePath, "TableCandle", parent,
            new Vector3(-0.9f, 0.8f, 0f), new Vector3(0.7f, 0.7f, 0.7f), Quaternion.identity);

        // Dice Tray (Center)
        GameObject tray = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tray.name = "DiceTray";
        tray.transform.SetParent(table.transform, false);
        tray.transform.localPosition = new Vector3(0f, 0.88f, 0f);
        tray.transform.localScale = new Vector3(0.45f, 0.08f, 0.45f);
        ApplyMaterial(tray, "HDRP/Lit", new Color(0.08f, 0.30f, 0.12f), 0.0f, 0.2f); // Green felt
        refs.diceTray = tray;

        // Bone Dice (Pair)
        var dicePair = new GameObject("BoneDice");
        dicePair.transform.SetParent(tray.transform, false);

        GameObject die1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        die1.name = "Die1";
        die1.transform.SetParent(dicePair.transform, false);
        die1.transform.localPosition = new Vector3(-0.1f, 0.6f, 0f);
        die1.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);
        ApplyMaterial(die1, "HDRP/Lit", new Color(0.92f, 0.90f, 0.82f), 0.0f, 0.6f);

        GameObject die2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        die2.name = "Die2";
        die2.transform.SetParent(dicePair.transform, false);
        die2.transform.localPosition = new Vector3(0.1f, 0.6f, 0f);
        die2.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);
        ApplyMaterial(die2, "HDRP/Lit", new Color(0.92f, 0.90f, 0.82f), 0.0f, 0.6f);
        refs.boneDice = dicePair;

        // Leather rim of rolling pit
        refs.diceRim = Cube(parent, "DiceRim", new Vector3(0f, 0.79f, 0f),
            new Vector3(1.15f, 0.04f, 0.86f), new Color(0.19f, 0.11f, 0.07f), 0.0f, 0.25f);

        // Player Board (South side)
        refs.playerBox = BuildBox(table.transform, "PlayerBox", new Vector3(0f, 0.88f, -0.5f), true,
            out refs.playerTiles);

        // Host Board (North side)
        refs.hostBox = BuildBox(table.transform, "HostBox", new Vector3(0f, 0.88f, 0.5f), false,
            out refs.hostTiles);

        refs.playerPivotRod = Cube(parent, "PlayerPivotRod", new Vector3(0f, 0.845f, -0.78f),
            new Vector3(2.5f, 0.02f, 0.02f), new Color(0.60f, 0.45f, 0.20f), 0.9f, 0.6f);
        refs.playerNamePlate = Cube(parent, "PlayerNamePlate", new Vector3(0f, 0.816f, -1.06f),
            new Vector3(2.4f, 0.03f, 0.01f), new Color(0.55f, 0.42f, 0.20f), 0.8f, 0.55f);
        refs.hostPivotRod = Cube(parent, "HostPivotRod", new Vector3(0f, 0.845f, 0.78f),
            new Vector3(2.5f, 0.02f, 0.02f), new Color(0.60f, 0.45f, 0.20f), 0.9f, 0.6f);
        refs.hostTile9Hinge = Cube(parent, "HostTile9Hinge", new Vector3(1.2672f, 0.845f, 0.78f),
            new Vector3(0.1f, 0.035f, 0.06f), new Color(0.48f, 0.36f, 0.16f), 0.9f, 0.5f);
    }

    static GameObject BuildBox(Transform parent, string name, Vector3 localPos, bool isPlayer,
        out GameObject tilesRoot)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPos;
        box.transform.localScale = new Vector3(1.2f, 0.06f, 0.35f);
        ApplyMaterial(box, "HDRP/Lit", isPlayer ? new Color(0.35f, 0.22f, 0.14f) : new Color(0.18f, 0.10f, 0.06f), 0.05f, 0.5f);

        tilesRoot = new GameObject(isPlayer ? "PlayerTiles" : "HostTiles");
        tilesRoot.transform.SetParent(box.transform, false);

        for (int i = 1; i <= 9; i++)
        {
            float xOffset = -0.48f + (i - 1) * 0.12f;
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = $"Tile_{i}_{ShutBoxRules.GuestForTile(i)}";
            tile.transform.SetParent(tilesRoot.transform, false);
            tile.transform.localPosition = new Vector3(xOffset, 0.8f, 0f);
            tile.transform.localScale = new Vector3(0.08f, 0.6f, 0.15f);
            ApplyMaterial(tile, "HDRP/Lit", new Color(0.70f, 0.55f, 0.38f), 0.0f, 0.3f);
        }

        return box;
    }

    static void BuildLighting(Transform parent, GmShutTheBoxCompositionPlan.SceneRefs refs)
    {
        // Hanging Table Lamp fixture (Lamp_2)
        var fixture = LoadMesh(LampPath, "TableLampFixture", parent,
            new Vector3(0f, 2.2f, 0f), new Vector3(0.8f, 0.8f, 0.8f), Quaternion.identity);
        ApplyPbr(fixture, LampAlbedo, LampNormal, 0.65f, 0.7f);
        refs.tableLampFixture = fixture;

        GameObject lampObj = new GameObject("TableLampLight");
        lampObj.transform.SetParent(parent, false);
        lampObj.transform.position = new Vector3(0f, 2.2f, 0f);
        Light lamp = lampObj.AddComponent<Light>();
        lamp.type = LightType.Spot;
        lamp.range = 4.5f;
        lamp.spotAngle = 70f;
        lamp.color = new Color(1.0f, 0.88f, 0.65f);
        lamp.intensity = 220f;
        lampObj.AddComponent<HDAdditionalLightData>();
        refs.tableLampLight = lampObj;

        // Secret Door raking accent light
        GameObject doorLightObj = new GameObject("DoorAccentLight");
        doorLightObj.transform.SetParent(parent, false);
        doorLightObj.transform.position = new Vector3(3.2f, 2.0f, -0.5f);
        Light doorLight = doorLightObj.AddComponent<Light>();
        doorLight.type = LightType.Point;
        doorLight.range = 3.5f;
        doorLight.color = new Color(0.85f, 0.75f, 0.55f);
        doorLight.intensity = 90f;
        doorLightObj.AddComponent<HDAdditionalLightData>();
        refs.doorAccentLight = doorLightObj;

        // Alcove wall sconce (Lamp_2)
        var sconce = LoadMesh(LampPath, "WallSconce", parent,
            new Vector3(1.6f, 2.2f, 3.78f), new Vector3(0.7f, 0.7f, 0.7f), Quaternion.identity);
        ApplyPbr(sconce, LampAlbedo, LampNormal, 0.65f, 0.7f);
        refs.wallSconce = sconce;

        GameObject sconceLightObj = new GameObject("WallSconceLight");
        sconceLightObj.transform.SetParent(parent, false);
        sconceLightObj.transform.position = new Vector3(1.6f, 2.2f, 3.78f);
        Light sconceLight = sconceLightObj.AddComponent<Light>();
        sconceLight.type = LightType.Point;
        sconceLight.range = 3f;
        sconceLight.color = new Color(0.95f, 0.72f, 0.42f);
        sconceLight.intensity = 60f;
        sconceLightObj.AddComponent<HDAdditionalLightData>();
        refs.wallSconceLight = sconceLightObj;
    }

    static GameObject Cube(Transform parent, string name, Vector3 position, Vector3 scale,
        Color color, float metallic, float smoothness)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = scale;
        ApplyMaterial(go, "HDRP/Lit", color, metallic, smoothness);
        return go;
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
