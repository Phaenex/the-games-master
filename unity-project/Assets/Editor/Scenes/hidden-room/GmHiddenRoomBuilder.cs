using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

public sealed class GmHiddenRoomProps
{
    public GameObject RecessDoor;
    public GameObject BrassBolt;
    public GameObject DoorSconce;
    public GameObject DoorSconceLight;
    public GameObject RolltopDesk;
    public GameObject InvitationLetter;
    public GameObject WaxInkpot;
    public GameObject MirrorFrame;
    public GameObject ShardThree;
    public GameObject ShardReceptacle;
    public GameObject EmptyShardSlots;
    public GameObject ArchiveShelf;
    public GameObject JournalBinders;
    public GameObject Cobwebs;
    public GameObject StepStool;
    public GameObject DeskLantern;
    public GameObject DeskLanternLight;
    public GameObject MirrorColdLight;
    public GameObject MirrorGlowLight;
    public GameObject ShelfWallSconce;
    public GameObject ShelfSconceLight;
}

public static class GmHiddenRoomBuilder
{
    public const string SceneId = "hidden-room";
    public const string DisplayName = "The Hidden Room";
    public const string ScenePath = "Assets/Scenes/HiddenRoom.unity";

    public const float DeskSurfaceY = 0.9f;

    // 3D Asset Paths
    const string DeskPath = "Assets/LeartesStudios/WitchVillage/HDRP/Art/Prefabs/SM_Desk.prefab";
    const string BookshelfPath = "Assets/ThirdParty/MetalManVictorianInteriors/BookShelf_1.fbx";
    const string MirrorPath = "Assets/ThirdParty/MetalManVictorianInteriors/Mirror_1.fbx";
    const string CarpetPath = "Assets/ThirdParty/MetalManVictorianInteriors/Carpet_1.fbx";
    const string LampPath = "Assets/ThirdParty/MetalManVictorianInteriors/Lamp_2.fbx";
    const string StoolPath = "Assets/LeartesStudios/WitchVillage/HDRP/Art/Prefabs/SM_Stool.prefab";
    const string Book1Path = "Assets/LeartesStudios/WitchVillage/HDRP/Art/Prefabs/SM_Book_1.prefab";
    const string Book2Path = "Assets/LeartesStudios/WitchVillage/HDRP/Art/Prefabs/SM_Book_2.prefab";
    const string BottlePath = "Assets/LeartesStudios/WitchVillage/HDRP/Art/Prefabs/SM_Bottles_1.prefab";
    const string LanternPath = "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Prefabs/SM_Lantern.prefab";
    const string HandlePath = "Assets/LeartesStudios/HauntedVillage/Art/Meshes/SM_Handle.fbx";
    const string DoorPrefabPath = "Assets/LeartesStudios/HauntedVillage/Art/Prefabs/SM_Door_01.prefab";
    const string Cobweb02Path = "Assets/GamesMaster/Props/Cobweb_02.fbx";
    const string Cobweb03Path = "Assets/GamesMaster/Props/Cobweb_03.fbx";

    // PBR Textures
    const string WallpaperAlbedo = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Wall_1_Albedo.psd";
    const string WallpaperNormal = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Wall_1_Normal.png";
    const string FloorAlbedo = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Floor_Albedo.png";
    const string FloorNormal = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Floor_Normal.png";
    const string BookshelfAlbedo = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Bookshelf_Albedo.psd";
    const string BookshelfNormal = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Bookshelf_Normal.psd";
    const string MirrorAlbedo = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Mirrors_Albedo.psd";
    const string MirrorNormal = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Mirrors_Normal.psd";
    const string CarpetAlbedo = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Carpets_Albedo.psd";
    const string CarpetNormal = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Carpets_Normal.psd";
    const string LampAlbedo = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Lamp_Albedo.psd";
    const string LampNormal = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Lamp_Normal.psd";

    [MenuItem("GamesMaster/Scenes/Rebuild Hidden Room")]
    public static void Build()
    {
        Scene scene = GmSceneBuildUtility.CreateEmptyScene();
        var systems = new GameObject("SceneSystems");
        GmSceneBuildUtility.MarkScene(systems, SceneId, DisplayName);
        systems.AddComponent<GmHiddenRoomController>();
        systems.AddComponent<GmHiddenRoomShotTour>();

        var environment = new GameObject("Environment");
        var gameplay = new GameObject("Gameplay");
        var lighting = new GameObject("Lighting");

        var props = new GmHiddenRoomProps();
        BuildArchitecture(environment.transform, props);
        BuildGameplayProps(gameplay.transform, props);
        BuildLighting(lighting.transform, props);

        var composition = new GameObject("Composition");
        GmHiddenRoomCompositionPlan.Author(composition, props);

        // A player, at last. This room had real, tested gameplay and nobody who could reach
        // it. Spawn is the viewpoint the ReviewCamera used to sit at -- the one vantage a
        // human already chose for this room -- so it is the least arbitrary spawn available,
        // and the review tour prefers the player's own camera when a player exists.
        // No HDRP atmosphere at all until now: every room builder had zero Volume/Exposure
        // references against the prologue's 31, so HDRP fell back to AUTOMATIC exposure and
        // opened up until a lamp-lit room rendered as a white box.
        GmInteriorAtmosphere.Apply(null, GmHiddenRoomBuilder.SceneId);

        GmPlayerRig.Build(null, new Vector3(0f, 0f, -1.8f), new Vector3(0f, 1.2f, 0f));

        var arrival = systems.AddComponent<GmSceneArrival>();
        arrival.closingCard = "";

        GmSceneBuildUtility.SaveScene(scene, ScenePath);
        Debug.Log("[GmHiddenRoom] BUILD PASS: " + ScenePath);
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
            Debug.LogWarning($"[GmHiddenRoom] ASSET MISSING: {assetPath} — creating labeled placeholder for '{name}'");
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

    static void BuildArchitecture(Transform parent, GmHiddenRoomProps props)
    {
        Material wallMat = CreatePbrMaterial(WallpaperAlbedo, WallpaperNormal, new Color(0.50f, 0.45f, 0.40f), 0.15f, 0f, new Vector2(3f, 2f));
        Material floorMat = CreatePbrMaterial(FloorAlbedo, FloorNormal, new Color(0.40f, 0.32f, 0.25f), 0.35f, 0f, new Vector2(3f, 3f));

        // Floor
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "HiddenFloor";
        floor.transform.SetParent(parent, false);
        floor.transform.position = new Vector3(0f, -0.1f, 0f);
        floor.transform.localScale = new Vector3(6f, 0.2f, 6f);
        floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

        // Persian Rug
        var rug = LoadMesh(CarpetPath, "StudyCarpet", parent,
            new Vector3(0f, 0.01f, 0f), new Vector3(1.2f, 1f, 1.4f), Quaternion.identity);
        ApplyPbr(rug, CarpetAlbedo, CarpetNormal, 0.15f, 0f);

        // Walls
        GameObject northWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        northWall.name = "NorthWall";
        northWall.transform.SetParent(parent, false);
        northWall.transform.position = new Vector3(0f, 1.8f, 3f);
        northWall.transform.localScale = new Vector3(6f, 3.6f, 0.3f);
        northWall.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

        // The panel the player entered through remains the way out. Split the wall around it and
        // give the opened leaf a short black passage; the previous cube leaf was mounted on a solid
        // 6m wall, so the secret room was literally a one-way load.
        var southWall = new GameObject("SouthWall");
        southWall.transform.SetParent(parent, false);
        ArchitectureCube(southWall.transform, "SouthWallLeft", new Vector3(-1.825f, 1.8f, -3f),
            new Vector3(2.35f, 3.6f, 0.3f), wallMat);
        ArchitectureCube(southWall.transform, "SouthWallRight", new Vector3(1.825f, 1.8f, -3f),
            new Vector3(2.35f, 3.6f, 0.3f), wallMat);
        ArchitectureCube(southWall.transform, "SouthWallHeader", new Vector3(0f, 3f, -3f),
            new Vector3(1.3f, 1.2f, 0.3f), wallMat);
        ArchitectureCube(southWall.transform, "ReturnPassageFloor", new Vector3(0f, -0.06f, -3.8f),
            new Vector3(1.3f, 0.12f, 1.6f), floorMat);
        ArchitectureCube(southWall.transform, "ReturnPassageCeiling", new Vector3(0f, 2.46f, -3.8f),
            new Vector3(1.3f, 0.12f, 1.6f), wallMat);
        ArchitectureCube(southWall.transform, "ReturnPassageLeft", new Vector3(-0.7f, 1.2f, -3.8f),
            new Vector3(0.1f, 2.4f, 1.6f), wallMat);
        ArchitectureCube(southWall.transform, "ReturnPassageRight", new Vector3(0.7f, 1.2f, -3.8f),
            new Vector3(0.1f, 2.4f, 1.6f), wallMat);
        ArchitectureCube(southWall.transform, "ReturnPassageBlind", new Vector3(0f, 1.2f, -4.65f),
            new Vector3(1.3f, 2.4f, 0.1f), wallMat);

        GameObject eastWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        eastWall.name = "EastWall";
        eastWall.transform.SetParent(parent, false);
        eastWall.transform.position = new Vector3(3f, 1.8f, 0f);
        eastWall.transform.localScale = new Vector3(0.3f, 3.6f, 6f);
        eastWall.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

        GameObject westWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        westWall.name = "WestWall";
        westWall.transform.SetParent(parent, false);
        westWall.transform.position = new Vector3(-3f, 1.8f, 0f);
        westWall.transform.localScale = new Vector3(0.3f, 3.6f, 6f);
        westWall.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

        // Real panel leaf, already standing open because this is the route the player just used.
        var exitRoot = new GameObject("HiddenRoomExit");
        exitRoot.transform.SetParent(parent, false);
        var hinge = new GameObject("RecessDoorHinge");
        hinge.transform.SetParent(exitRoot.transform, false);
        hinge.transform.position = new Vector3(-0.65f, 0f, -2.79f);
        Material doorWood = CreatePbrMaterial(WallpaperAlbedo, WallpaperNormal,
            new Color(0.16f, 0.09f, 0.04f), 0.3f, 0.02f, Vector2.one);
        GameObject recessDoor = GmOwnedPropFactory.PlacePrefab(DoorPrefabPath, "RecessDoorPanel",
            exitRoot.transform, new Vector3(0f, 0f, -2.79f), new Vector3(1.3f, 2.35f, 0.16f),
            Quaternion.identity, ground: true, surfaceY: 0f, overrideMaterial: doorWood);
        recessDoor.transform.SetParent(hinge.transform, true);
        hinge.transform.localRotation = Quaternion.Euler(0f, -96f, 0f);
        props.RecessDoor = recessDoor;

        var transitionObject = new GameObject("LabyrinthTransition");
        transitionObject.transform.SetParent(exitRoot.transform, false);
        transitionObject.transform.position = new Vector3(0f, 1.15f, -3.45f);
        var volume = transitionObject.AddComponent<BoxCollider>();
        volume.isTrigger = true;
        volume.size = new Vector3(1.15f, 2.3f, 0.7f);
        var transition = transitionObject.AddComponent<GmSceneTransitionTrigger>();
        transition.TargetSceneId = GmLabyrinthBuilder.SceneId;
        transition.TargetScenePath = GmLabyrinthBuilder.ScenePath;
        transition.InteractionPrompt = "Back into the night";
        transition.UseCurtain = true;
        transition.CompleteRoomOnTransitionId = SceneId;
        transition.CompletedRoomCountsAsTableGame = false;

        // Brass Bolt (Handle FBX)
        GameObject brassBolt = LoadMesh(HandlePath, "BrassBolt", parent,
            new Vector3(0.42f, 1.15f, -2.70f), new Vector3(0.8f, 0.8f, 0.8f), Quaternion.identity);
        ApplyPbr(brassBolt, "", "", 0.72f, 0.82f, new Color(0.52f, 0.34f, 0.11f));
        brassBolt.transform.SetParent(hinge.transform, true);
        props.BrassBolt = brassBolt;

        // Door Sconce
        var doorSconce = LoadMesh(LampPath, "DoorSconce", parent,
            new Vector3(-0.6f, 1.8f, -2.85f), new Vector3(0.6f, 0.6f, 0.6f), Quaternion.identity);
        ApplyPbr(doorSconce, LampAlbedo, LampNormal, 0.65f, 0.7f);
        props.DoorSconce = doorSconce;
    }

    static GameObject ArchitectureCube(Transform parent, string name, Vector3 position,
        Vector3 scale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.position = position;
        cube.transform.localScale = scale;
        cube.GetComponent<MeshRenderer>().sharedMaterial = material;
        return cube;
    }

    static void BuildGameplayProps(Transform parent, GmHiddenRoomProps props)
    {
        // Rolltop Desk (SM_Desk FBX)
        GameObject desk = LoadMesh(DeskPath, "RolltopDesk", parent,
            new Vector3(0f, 0f, 2.2f), new Vector3(0.9f, 0.9f, 0.9f), Quaternion.Euler(0, 180f, 0));
        props.RolltopDesk = desk;

        // Original Invitation Letter
        GameObject letter = GameObject.CreatePrimitive(PrimitiveType.Cube);
        letter.name = "InvitationLetter";
        letter.transform.SetParent(desk.transform, false);
        letter.transform.localPosition = new Vector3(0f, 0.92f, 0f);
        letter.transform.localScale = new Vector3(0.3f, 0.02f, 0.22f);
        ApplyMaterial(letter, "HDRP/Lit", new Color(0.95f, 0.92f, 0.82f), 0.0f, 0.4f);
        props.InvitationLetter = letter;

        // Wax inkpot (Glass Bottle FBX)
        GameObject inkpot = LoadMesh(BottlePath, "WaxInkpot", parent,
            new Vector3(-0.5f, DeskSurfaceY + 0.055f, 2.15f), new Vector3(0.4f, 0.4f, 0.4f), Quaternion.identity);
        props.WaxInkpot = inkpot;

        // Standing Mirror Frame (Mirror_1 FBX)
        // Mirror_1's pivot is at its BASE (local Y 0.0000..2.2654), so a Y here is the height of the
        // mirror's feet, not its centre. Placing it at 1.2 floated the whole 1.81m frame between
        // Y 1.200 and 3.012 and put its bounds centre above the top of review shot 03's frame.
        GameObject mirror = LoadMesh(MirrorPath, "StandingMirrorFrame", parent,
            new Vector3(-2.4f, 0f, 0f), new Vector3(0.8f, 0.8f, 0.8f), Quaternion.Euler(0, 90f, 0));
        ApplyPbr(mirror, MirrorAlbedo, MirrorNormal, 0.85f, 0.35f);
        props.MirrorFrame = mirror;

        // Shard #3 in Mirror Frame
        GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shard.name = "MirrorShard_3";
        shard.transform.SetParent(mirror.transform, false);
        // Local Y is measured from the frame's base pivot, so 0 puts the shard on the floor. Half of
        // Mirror_1's 2.2654 local height sets it in the middle of the glass, where a shard wedged in
        // a standing mirror belongs and where review shot 03 frames it.
        shard.transform.localPosition = new Vector3(0f, 1.1327f, 0.1f);
        shard.transform.localScale = new Vector3(0.25f, 0.35f, 0.05f);
        ApplyMaterial(shard, "HDRP/Lit", new Color(0.9f, 0.95f, 1.0f), 0.9f, 0.95f);
        props.ShardThree = shard;

        GameObject receptacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        receptacle.name = "ShardReceptacle";
        receptacle.transform.SetParent(parent, false);
        receptacle.transform.position = new Vector3(-2.33f, 0.78f, 0f);
        receptacle.transform.localScale = new Vector3(0.08f, 0.07f, 0.36f);
        ApplyMaterial(receptacle, "HDRP/Lit", new Color(0.55f, 0.46f, 0.24f), 0.75f, 0.5f);
        props.ShardReceptacle = receptacle;

        var slots = new GameObject("EmptyShardSlots");
        slots.transform.SetParent(parent, false);
        slots.transform.position = new Vector3(-2.33f, 0f, 0f);
        GameObject upperSlot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        upperSlot.name = "ShardSlot_Upper";
        upperSlot.transform.SetParent(slots.transform, false);
        upperSlot.transform.localPosition = new Vector3(0f, 1.95f, 0f);
        upperSlot.transform.localScale = new Vector3(0.06f, 0.5f, 0.3f);
        ApplyMaterial(upperSlot, "HDRP/Lit", new Color(0.06f, 0.05f, 0.05f), 0.0f, 0.15f);
        GameObject lowerSlot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lowerSlot.name = "ShardSlot_Lower";
        lowerSlot.transform.SetParent(slots.transform, false);
        lowerSlot.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        lowerSlot.transform.localScale = new Vector3(0.06f, 0.42f, 0.3f);
        ApplyMaterial(lowerSlot, "HDRP/Lit", new Color(0.06f, 0.05f, 0.05f), 0.0f, 0.15f);
        props.EmptyShardSlots = slots;

        // Archive Shelves (BookShelf_1 FBX)
        GameObject shelves = LoadMesh(BookshelfPath, "ArchiveShelf", parent,
            new Vector3(2.4f, 0f, 0f), new Vector3(0.8f, 0.8f, 0.8f), Quaternion.Euler(0, -90f, 0));
        ApplyPbr(shelves, BookshelfAlbedo, BookshelfNormal, 0.40f, 0f);
        props.ArchiveShelf = shelves;

        // 8 Journal Binders on Shelves (SM_Book FBX)
        var binders = new GameObject("JournalBinders");
        binders.transform.SetParent(shelves.transform, false);
        for (int i = 1; i <= 8; i++)
        {
            string bookPath = (i % 2 == 0) ? Book2Path : Book1Path;
            GameObject journal = LoadMesh(bookPath, $"Journal_Guest_{i}", binders.transform,
                new Vector3(0.1f, 0.4f + (i % 4) * 0.35f, -0.6f + (i / 4) * 0.6f),
                new Vector3(0.55f, 0.55f, 0.55f), Quaternion.Euler(0, 90f, 0));
        }
        props.JournalBinders = binders;

        // Corner Cobwebs
        var cobwebs = new GameObject("Cobwebs");
        cobwebs.transform.SetParent(parent, false);
        cobwebs.transform.position = new Vector3(2.4f, 0f, 0f);
        GameObject northWeb = LoadMesh(Cobweb02Path, "Cobweb_North", cobwebs.transform,
            new Vector3(0f, 2.8f, -1.8f), Vector3.one * 0.35f, Quaternion.identity);
        GameObject southWeb = LoadMesh(Cobweb03Path, "Cobweb_South", cobwebs.transform,
            new Vector3(0f, 2.8f, 1.8f), Vector3.one * 0.35f, Quaternion.identity);
        ApplyPbr(northWeb, "", "", 0.05f, 0f, new Color(0.16f, 0.15f, 0.13f));
        ApplyPbr(southWeb, "", "", 0.05f, 0f, new Color(0.16f, 0.15f, 0.13f));
        props.Cobwebs = cobwebs;

        // Archive Step Stool (SM_Stool FBX)
        GameObject stool = LoadMesh(StoolPath, "ArchiveStepStool", parent,
            new Vector3(2.0f, 0f, 1.3f), new Vector3(0.7f, 0.7f, 0.7f), Quaternion.Euler(0, 25f, 0));
        props.StepStool = stool;
    }

    static void BuildLighting(Transform parent, GmHiddenRoomProps props)
    {
        GameObject doorLightObj = new GameObject("DoorSconceLight");
        doorLightObj.transform.SetParent(parent, false);
        doorLightObj.transform.position = new Vector3(-0.42f, 1.9f, -2.52f);
        Light doorLight = doorLightObj.AddComponent<Light>();
        doorLight.type = LightType.Point;
        doorLight.range = 3.2f;
        doorLight.color = new Color(1f, 0.72f, 0.4f);
        doorLight.lightUnit = LightUnit.Lumen;
        doorLight.intensity = GmInteriorAtmosphere.PracticalCeilingLumens;
        doorLightObj.AddComponent<HDAdditionalLightData>();
        props.DoorSconceLight = doorLightObj;

        // Desk Oil Lantern (SM_Lantern FBX)
        GameObject lanternBody = LoadMesh(LanternPath, "DeskLantern", parent,
            new Vector3(0.5f, DeskSurfaceY + 0.05f, 2.2f), new Vector3(0.5f, 0.5f, 0.5f), Quaternion.identity);
        props.DeskLantern = lanternBody;

        GameObject lanternObj = new GameObject("DeskLanternLight");
        lanternObj.transform.SetParent(parent, false);
        lanternObj.transform.position = new Vector3(0.5f, 1.3f, 2.2f);
        Light lantern = lanternObj.AddComponent<Light>();
        lantern.type = LightType.Point;
        lantern.range = 4.5f;
        lantern.color = new Color(1.0f, 0.82f, 0.55f);
        lantern.lightUnit = LightUnit.Lumen;
        lantern.intensity = 180f;
        lanternObj.AddComponent<HDAdditionalLightData>();
        props.DeskLanternLight = lanternObj;

        // Mirror Glow
        GameObject mirrorLightObj = new GameObject("MirrorColdLight");
        mirrorLightObj.transform.SetParent(parent, false);
        mirrorLightObj.transform.position = new Vector3(-1.65f, 1.45f, -0.55f);
        Light mirrorLight = mirrorLightObj.AddComponent<Light>();
        mirrorLight.type = LightType.Point;
        mirrorLight.range = 2.4f;
        mirrorLight.color = new Color(0.60f, 0.80f, 1.0f);
        mirrorLight.lightUnit = LightUnit.Lumen;
        mirrorLight.intensity = 8f;
        mirrorLightObj.AddComponent<HDAdditionalLightData>();
        props.MirrorColdLight = mirrorLightObj;
        props.MirrorGlowLight = mirrorLightObj;

        // Shelf Sconce (Lamp_2 FBX)
        var sconce = LoadMesh(LampPath, "ShelfWallSconce", parent,
            new Vector3(2.8f, 2.2f, 0f), new Vector3(0.7f, 0.7f, 0.7f), Quaternion.Euler(0, -90f, 0));
        ApplyPbr(sconce, LampAlbedo, LampNormal, 0.65f, 0.7f);
        props.ShelfWallSconce = sconce;

        GameObject shelfLightObj = new GameObject("ShelfSconceLight");
        shelfLightObj.transform.SetParent(parent, false);
        shelfLightObj.transform.position = new Vector3(2.5f, 2.2f, 0f);
        Light shelfLight = shelfLightObj.AddComponent<Light>();
        shelfLight.type = LightType.Point;
        shelfLight.range = 3.5f;
        shelfLight.color = new Color(0.95f, 0.75f, 0.45f);
        shelfLight.lightUnit = LightUnit.Lumen;
        shelfLight.intensity = 90f;
        shelfLightObj.AddComponent<HDAdditionalLightData>();
        props.ShelfSconceLight = shelfLightObj;
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
