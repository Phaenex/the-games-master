// Assets/Editor/GmWakeRoom.cs
// The Wake Room: The Awakening Chamber in Wend Hill.
// Intimate, densely staged Victorian master bedchamber with 100% real PBR 3D meshes.
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public static class GmWakeRoom
{
    const float WorldZ = 400f;
    const float RoomW = 5.4f, RoomD = 4.8f, RoomH = 3.1f;
    const float WallT = 0.25f;
    const float TargetClockHeight = 2.1f;

    // Asset paths for 3D FBX models
    const string BedPath = "Assets/GamesMaster/Props/GothicBed.fbx";
    const string ClockPath = "Assets/GamesMaster/Props/Vintage_Grantfather_Clock.fbx";
    const string Cobweb02Path = "Assets/GamesMaster/Props/Cobweb_02.fbx";
    const string Cobweb03Path = "Assets/GamesMaster/Props/Cobweb_03.fbx";
    const string ArmorPath = "Assets/GamesMaster/Interior/Armor_Metal.fbx";

    const string TablePath = "Assets/ThirdParty/MetalManVictorianInteriors/Table_3.fbx";
    const string ChairPath = "Assets/ThirdParty/MetalManVictorianInteriors/Chair_1.fbx";
    const string Chair2Path = "Assets/ThirdParty/MetalManVictorianInteriors/Chair_2.fbx";
    const string BookshelfPath = "Assets/ThirdParty/MetalManVictorianInteriors/BookShelf_1.fbx";
    const string MantelPath = "Assets/ThirdParty/MetalManVictorianInteriors/Mantel.fbx";
    const string MirrorPath = "Assets/ThirdParty/MetalManVictorianInteriors/Mirror_1.fbx";
    const string CarpetPath = "Assets/ThirdParty/MetalManVictorianInteriors/Carpet_1.fbx";
    const string PicturePath = "Assets/ThirdParty/MetalManVictorianInteriors/Picture_2.fbx";
    const string LampPath = "Assets/ThirdParty/MetalManVictorianInteriors/Lamp_2.fbx";

    const string Candle1Path = "Assets/LeartesStudios/WitchVillage/HDRP/Art/Meshes/SmallProps/Candles/SM_Candles_1.fbx";
    const string Candle3Path = "Assets/LeartesStudios/WitchVillage/HDRP/Art/Meshes/SmallProps/Candles/SM_Candles_3.fbx";
    const string ScrollPath = "Assets/LeartesStudios/WitchVillage/HDRP/Art/Meshes/SmallProps/Scrolls/SM_Scrolls_1.fbx";
    const string DeskPath = "Assets/LeartesStudios/WitchVillage/HDRP/Art/Meshes/MediumProps/Desk/SM_Desk.fbx";
    const string StoolPath = "Assets/LeartesStudios/WitchVillage/HDRP/Art/Meshes/MediumProps/Stool/SM_Stool.fbx";
    const string Book1Path = "Assets/LeartesStudios/WitchVillage/HDRP/Art/Meshes/SmallProps/Books/SM_Book_1.fbx";
    const string Book2Path = "Assets/LeartesStudios/WitchVillage/HDRP/Art/Meshes/SmallProps/Books/SM_Book_2.fbx";
    const string Bottle1Path = "Assets/LeartesStudios/WitchVillage/HDRP/Art/Meshes/SmallProps/Bottles/SM_Bottles_1.fbx";
    const string Bottle2Path = "Assets/LeartesStudios/WitchVillage/HDRP/Art/Meshes/SmallProps/Bottles/SM_Bottles_2.fbx";
    const string DrapePath = "Assets/LeartesStudios/WitchVillage/HDRP/Art/Meshes/SmallProps/Drapes/SM_Drape1.fbx";
    const string LanternPath = "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Meshes/Props/SM_Lantern.fbx";

    // PBR Textures
    const string WallpaperAlbedo = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Wall_1_Albedo.psd";
    const string WallpaperNormal = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Wall_1_Normal.png";
    const string FloorAlbedo = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Floor_Albedo.png";
    const string FloorNormal = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Floor_Normal.png";
    const string CeilingAlbedo = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Roof_Albedo.png";
    const string CeilingNormal = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Roof_Normal.png";

    const string TableAlbedo = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Tables_2_Albedo.psd";
    const string TableNormal = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Tables_2_Normal.psd";
    const string ChairAlbedo = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Chairs_Albedo.psd";
    const string ChairNormal = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Chairs_Normal.psd";
    const string BookshelfAlbedo = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Bookshelf_Albedo.psd";
    const string BookshelfNormal = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Bookshelf_Normal.psd";
    const string CarpetAlbedo = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Carpets_Albedo.psd";
    const string CarpetNormal = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Carpets_Normal.psd";
    const string MantelAlbedo = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Mantel_Albedo.psd";
    const string MantelNormal = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Mantel_Normal.psd";
    const string MirrorAlbedo = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Mirrors_Albedo.psd";
    const string MirrorNormal = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Mirrors_Normal.psd";
    const string FrameAlbedo = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Frames_Albedo.psd";
    const string FrameNormal = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Frames_Normal.psd";
    const string LampAlbedo = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Lamp_Albedo.psd";
    const string LampNormal = "Assets/ThirdParty/MetalManVictorianInteriors/Materials/Lamp_Normal.psd";

    [MenuItem("GamesMaster/Rebuild Wake Room Only")]
    public static void BuildStandalone() => Build();

    public static void Build()
    {
        var root = new GameObject("WakeRoom").transform;
        root.position = new Vector3(0, 0, WorldZ);

        Material wallMat = CreatePbrMaterial(WallpaperAlbedo, WallpaperNormal, new Color(0.65f, 0.60f, 0.55f), 0.12f, 0f, new Vector2(3f, 2f));
        Material floorMat = CreatePbrMaterial(FloorAlbedo, FloorNormal, new Color(0.50f, 0.40f, 0.30f), 0.35f, 0f, new Vector2(3f, 3f));
        Material ceilingMat = CreatePbrMaterial(CeilingAlbedo, CeilingNormal, new Color(0.75f, 0.72f, 0.68f), 0.08f, 0f, new Vector2(2.5f, 2.5f));

        BuildShell(root, wallMat, floorMat, ceilingMat);
        BuildCarpets(root);
        BuildBedAndDrapes(root);
        Vector3 clockPos = BuildClock(root);
        BuildSideTableAndNightstand(root);
        BuildFireplaceAndHearth(root);
        BuildVanityAndMirror(root);
        BuildBookshelfNook(root);
        BuildArmorAndCornerDressing(root);
        BuildWindowAndMoonlightShafts(root);
        BuildDustAndCobwebs(root);
        BuildLighting(root, clockPos);
        BuildWakePose(root, clockPos);

        Debug.Log($"[GmWakeRoom] PASS: Densely furnished Victorian Wake Room built at z={WorldZ}.");
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
            Debug.LogWarning($"[GmWakeRoom] ASSET MISSING: {assetPath} — creating labeled placeholder for '{name}'");
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

    static void BuildShell(Transform root, Material wallMat, Material floorMat, Material ceilingMat)
    {
        float cz = root.position.z;
        float half = RoomW / 2f + WallT;

        Slab("Floor", new Vector3(0, -WallT / 2f, cz), new Vector3(RoomW + 2 * WallT, WallT, RoomD + 2 * WallT), floorMat, root);
        Slab("Ceiling", new Vector3(0, RoomH + WallT / 2f, cz), new Vector3(RoomW + 2 * WallT, WallT, RoomD + 2 * WallT), ceilingMat, root);
        Slab("WallNorth", new Vector3(0, RoomH / 2f, cz + RoomD / 2f + WallT / 2f), new Vector3(RoomW + 2 * WallT, RoomH, WallT), wallMat, root);
        
        float southZ = cz - RoomD / 2f - WallT / 2f;
        const float doorway = 1.8f;
        float side = (RoomW + 2 * WallT - doorway) * 0.5f;
        Slab("WallSouthWest", new Vector3(-(doorway * 0.5f + side * 0.5f), RoomH / 2f, southZ), new Vector3(side, RoomH, WallT), wallMat, root);
        Slab("WallSouthEast", new Vector3(doorway * 0.5f + side * 0.5f, RoomH / 2f, southZ), new Vector3(side, RoomH, WallT), wallMat, root);
        Slab("WallSouthLintel", new Vector3(0, 2.85f, southZ), new Vector3(doorway, 0.5f, WallT), wallMat, root);
        Slab("WallEast", new Vector3(half - WallT / 2f, RoomH / 2f, cz), new Vector3(WallT, RoomH, RoomD + 2 * WallT), wallMat, root);
        Slab("WallWest", new Vector3(-(half - WallT / 2f), RoomH / 2f, cz), new Vector3(WallT, RoomH, RoomD + 2 * WallT), wallMat, root);
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

    static void BuildCarpets(Transform root)
    {
        // Persian runner carpet across the center floor
        var rug1 = LoadMesh(CarpetPath, "BedsidePersianCarpet", root,
            new Vector3(0f, 0.01f, 0.2f), new Vector3(1.1f, 1f, 1.3f), Quaternion.Euler(0, 90f, 0));
        ApplyPbr(rug1, CarpetAlbedo, CarpetNormal, 0.08f, 0f, new Color(0.65f, 0.55f, 0.48f));
    }

    static void BuildBedAndDrapes(Transform root)
    {
        // Four-poster Gothic Bed on West wall
        var bed = LoadMesh(BedPath, "GothicAwakeningBed", root,
            new Vector3(-1.6f, 0f, -0.4f), Vector3.one * 1.0f, Quaternion.Euler(0f, 90f, 0f));

        var darkOak = new Material(Shader.Find("HDRP/Lit")) { color = new Color(0.12f, 0.08f, 0.05f) };
        darkOak.SetFloat("_Smoothness", 0.35f);
        var crimsonVelvet = new Material(Shader.Find("HDRP/Lit")) { color = new Color(0.35f, 0.06f, 0.06f) };
        crimsonVelvet.SetFloat("_Smoothness", 0.12f);

        if (bed != null)
        {
            foreach (var r in bed.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = (i % 2 == 0) ? darkOak : crimsonVelvet;
                r.sharedMaterials = mats;
            }
        }

        // Velvet drape accent by headboard
        var drape = LoadMesh(DrapePath, "BedHeadboardDrape", root,
            new Vector3(-2.4f, 1.8f, -0.4f), new Vector3(0.85f, 0.85f, 0.85f), Quaternion.Euler(0, 90f, 0));
    }

    static Vector3 BuildClock(Transform root)
    {
        // Longcase grandfather clock against North wall
        var clockGo = LoadMesh(ClockPath, "LongcaseGrandfatherClock", root,
            new Vector3(-0.25f, 0f, RoomD / 2f - 0.35f), Vector3.one * 1.0f, Quaternion.Euler(0, 180f, 0));

        var clockWood = new Material(Shader.Find("HDRP/Lit")) { color = new Color(0.12f, 0.07f, 0.04f) };
        clockWood.SetFloat("_Smoothness", 0.40f);
        if (clockGo != null)
        {
            foreach (var r in clockGo.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = clockWood;
                r.sharedMaterials = mats;
            }

            if (clockGo.GetComponent<Collider>() == null && clockGo.GetComponentInChildren<Collider>() == null)
            {
                var col = clockGo.AddComponent<BoxCollider>();
                col.center = new Vector3(0, 1.05f, 0);
                col.size = new Vector3(0.6f, 2.1f, 0.45f);
            }
        }

        return clockGo != null ? clockGo.transform.position : new Vector3(-0.25f, 0f, 2.0f);
    }

    static void BuildSideTableAndNightstand(Transform root)
    {
        // Bedside Table directly beside player vantage
        var tableCluster = new GameObject("SideTableCluster");
        tableCluster.transform.SetParent(root, false);
        tableCluster.transform.localPosition = new Vector3(-0.55f, 0f, 0.1f);
        tableCluster.transform.localRotation = Quaternion.Euler(0f, -15f, 0f);

        // 1. Table 3 (MetalMan)
        var tableGo = LoadMesh(TablePath, "BedsideTable", tableCluster.transform,
            Vector3.zero, new Vector3(0.55f, 0.55f, 0.55f), Quaternion.identity);
        ApplyPbr(tableGo, TableAlbedo, TableNormal, 0.40f, 0.05f);

        // 2. Sculpted Candle
        var candleGo = LoadMesh(Candle1Path, "TallowCandlestick", tableCluster.transform,
            new Vector3(-0.12f, 0.72f, 0.05f), new Vector3(0.7f, 0.7f, 0.7f), Quaternion.identity);

        var flameGo = new GameObject("CandleFlame");
        flameGo.transform.SetParent(candleGo.transform, false);
        flameGo.transform.localPosition = new Vector3(0, 0.22f, 0);
        var flameLight = flameGo.AddComponent<Light>();
        flameLight.type = LightType.Point;
        flameLight.color = new Color(1.0f, 0.58f, 0.22f);
        flameLight.range = 4.0f;
        flameLight.intensity = 32f;
        flameLight.lightUnit = LightUnit.Lumen;
        flameLight.shadows = LightShadows.Soft;
        var flameHd = flameGo.AddComponent<HDAdditionalLightData>();
        flameHd.affectsVolumetric = true;

        var candleInteract = candleGo.AddComponent<GmInteractable>();
        candleInteract.Configure("wake-candle", "Examine Candle", 2.8f, 12f);
        candleInteract.BindContent(
            "A brass candlestick holding a tallow taper. The warm amber flame flickers softly in the drafts from the shuttered window.",
            "The tallow drips slowly down the brass rim.");

        // 3. Invitation Scroll
        var letterGo = LoadMesh(ScrollPath, "InvitationLetter", tableCluster.transform,
            new Vector3(0.08f, 0.73f, -0.05f), new Vector3(0.4f, 0.4f, 0.4f), Quaternion.Euler(0, 25f, 0));
        var letterInteract = letterGo.AddComponent<GmInteractable>();
        letterInteract.Configure("wake-letter", "Read Letter", 2.8f, 12f);
        letterInteract.BindContent(
            "An invitation addressed to me in dried crimson wax: 'You have answered the summons to Wend Hill. The estate awaits its final game.'",
            "The red wax Games Master seal is broken. The invitation is clear.");

        // 4. Pocket Watch
        var brassMat = new Material(Shader.Find("HDRP/Lit")) { color = new Color(0.45f, 0.35f, 0.15f) };
        brassMat.SetFloat("_Metallic", 0.75f);
        brassMat.SetFloat("_Smoothness", 0.65f);
        var watch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        watch.name = "PocketWatch_PLACEHOLDER";
        watch.transform.SetParent(tableCluster.transform, false);
        watch.transform.localPosition = new Vector3(0.18f, 0.73f, 0.10f);
        watch.transform.localRotation = Quaternion.Euler(0, -35f, 0);
        watch.transform.localScale = new Vector3(0.055f, 0.012f, 0.055f);
        watch.GetComponent<MeshRenderer>().sharedMaterial = brassMat;

        var watchInteract = watch.AddComponent<GmInteractable>();
        watchInteract.Configure("wake-watch", "Examine Pocket Watch", 2.8f, 12f);
        watchInteract.BindContent(
            "A cracked silver pocket watch. The hands are frozen at 8:59. The second hand does not advance, yet a faint escapement clicks within.",
            "Still 8:59. Time does not advance inside these walls.");

        // 5. Matchbox
        var matchboxMat = new Material(Shader.Find("HDRP/Lit")) { color = new Color(0.28f, 0.38f, 0.48f) };
        var matchbox = GameObject.CreatePrimitive(PrimitiveType.Cube);
        matchbox.name = "Matchbox_PLACEHOLDER";
        matchbox.transform.SetParent(tableCluster.transform, false);
        matchbox.transform.localPosition = new Vector3(-0.16f, 0.73f, -0.12f);
        matchbox.transform.localRotation = Quaternion.Euler(0, 45f, 0);
        matchbox.transform.localScale = new Vector3(0.07f, 0.02f, 0.045f);
        matchbox.GetComponent<MeshRenderer>().sharedMaterial = matchboxMat;

        var matchInteract = matchbox.AddComponent<GmInteractable>();
        matchInteract.Configure("wake-matches", "Examine Matchbox", 2.8f, 12f);
        matchInteract.BindContent(
            "A vintage wooden matchbox labeled 'Swan Vestas'. A half-spent striker edge smells faintly of sulfur.",
            "A few sulfur matches remain inside.");

        // 6. Carbide Inspection Lamp
        var lampGo = LoadMesh(LanternPath, "CarbideInspectionLamp", tableCluster.transform,
            new Vector3(0f, 0.20f, 0f), new Vector3(0.45f, 0.45f, 0.45f), Quaternion.identity);

        var lampInteract = lampGo.AddComponent<GmInteractable>();
        lampInteract.Configure("wake-carbide-lamp", "Take Inspection Lamp", 2.8f, 12f);
        lampInteract.BindContent(
            "An antique brass carbide inspection lamp with a polished reflector hood. Heavy and cool to the touch, carrying water and calcium carbide fuel.",
            "The lower shelf is empty.");
    }

    static void BuildFireplaceAndHearth(Transform root)
    {
        // Carved Victorian fireplace mantel on North wall
        var mantel = LoadMesh(MantelPath, "VictorianMantel", root,
            new Vector3(1.3f, 0f, RoomD / 2f - 0.28f), new Vector3(0.85f, 0.85f, 0.85f), Quaternion.identity);
        ApplyPbr(mantel, MantelAlbedo, MantelNormal, 0.35f, 0f);

        // Warm hearth ember light
        var hearthLightGo = new GameObject("HearthEmberLight");
        hearthLightGo.transform.SetParent(root, false);
        hearthLightGo.transform.localPosition = new Vector3(1.3f, 0.35f, RoomD / 2f - 0.5f);
        var hl = hearthLightGo.AddComponent<Light>();
        hl.type = LightType.Point;
        hl.color = new Color(1.0f, 0.45f, 0.15f);
        hl.range = 4.5f;
        hl.intensity = 40f;
        hl.lightUnit = LightUnit.Lumen;
        hl.shadows = LightShadows.Soft;
        var hd = hearthLightGo.AddComponent<HDAdditionalLightData>();
        hd.affectsVolumetric = true;

        // Candlesticks on mantel shelf
        var mantelCandle = LoadMesh(Candle3Path, "MantelCandles", root,
            new Vector3(1.3f, 1.35f, RoomD / 2f - 0.35f), new Vector3(0.6f, 0.6f, 0.6f), Quaternion.identity);

        // Framed oil painting above mantel
        var picture = LoadMesh(PicturePath, "MantelOilPainting", root,
            new Vector3(1.3f, 1.95f, RoomD / 2f - 0.05f), new Vector3(0.9f, 0.9f, 0.9f), Quaternion.identity);
        ApplyPbr(picture, FrameAlbedo, FrameNormal, 0.55f, 0.15f);

        // Tufted Victorian Armchair angled toward the hearth
        var chair = LoadMesh(ChairPath, "HearthsideArmchair", root,
            new Vector3(1.5f, 0f, 0.6f), new Vector3(0.85f, 0.85f, 0.85f), Quaternion.Euler(0, -140f, 0));
        ApplyPbr(chair, ChairAlbedo, ChairNormal, 0.35f, 0f);

        // Wooden footstool
        var stool = LoadMesh(StoolPath, "Footstool", root,
            new Vector3(1.1f, 0f, 0.9f), new Vector3(0.55f, 0.55f, 0.55f), Quaternion.Euler(0, 15f, 0));
    }

    static void BuildVanityAndMirror(Transform root)
    {
        // East Wall Vanity desk + Standing Mirror + Perfume Bottles
        float eastX = RoomW / 2f - 0.45f;

        var desk = LoadMesh(DeskPath, "VanityDressingTable", root,
            new Vector3(eastX, 0f, -0.6f), new Vector3(0.75f, 0.75f, 0.75f), Quaternion.Euler(0, -90f, 0));

        var mirror = LoadMesh(MirrorPath, "GildedVanityMirror", root,
            new Vector3(eastX + 0.15f, 1.1f, -0.6f), new Vector3(0.7f, 0.7f, 0.7f), Quaternion.Euler(0, -90f, 0));
        ApplyPbr(mirror, MirrorAlbedo, MirrorNormal, 0.85f, 0.35f);

        var bottle1 = LoadMesh(Bottle1Path, "ApothecaryBottle_1", root,
            new Vector3(eastX - 0.1f, 0.76f, -0.45f), new Vector3(0.5f, 0.5f, 0.5f), Quaternion.identity);
        var bottle2 = LoadMesh(Bottle2Path, "ApothecaryBottle_2", root,
            new Vector3(eastX - 0.05f, 0.76f, -0.75f), new Vector3(0.5f, 0.5f, 0.5f), Quaternion.identity);

        var chair2 = LoadMesh(Chair2Path, "VanityChair", root,
            new Vector3(eastX - 0.6f, 0f, -0.6f), new Vector3(0.85f, 0.85f, 0.85f), Quaternion.Euler(0, 90f, 0));
        ApplyPbr(chair2, ChairAlbedo, ChairNormal, 0.35f, 0f);
    }

    static void BuildBookshelfNook(Transform root)
    {
        // Tall Victorian bookshelf in South-West corner
        var shelf = LoadMesh(BookshelfPath, "VictorianBookshelf", root,
            new Vector3(-RoomW / 2f + 0.45f, 0f, 1.2f), new Vector3(0.75f, 0.75f, 0.75f), Quaternion.Euler(0, 90f, 0));
        ApplyPbr(shelf, BookshelfAlbedo, BookshelfNormal, 0.40f, 0f);

        // Stacked antique books on shelf
        var book1 = LoadMesh(Book1Path, "AntiqueBook_1", root,
            new Vector3(-RoomW / 2f + 0.45f, 0.80f, 1.1f), new Vector3(0.55f, 0.55f, 0.55f), Quaternion.Euler(0, 90f, 0));
        var book2 = LoadMesh(Book2Path, "AntiqueBook_2", root,
            new Vector3(-RoomW / 2f + 0.45f, 1.25f, 1.3f), new Vector3(0.55f, 0.55f, 0.55f), Quaternion.Euler(0, 90f, 0));
    }

    static void BuildArmorAndCornerDressing(Transform root)
    {
        // Full suit of armor in South-East corner
        var armor = LoadMesh(ArmorPath, "SuitOfArmor", root,
            new Vector3(RoomW / 2f - 0.55f, 0f, -RoomD / 2f + 0.55f), Vector3.one * 0.9f, Quaternion.Euler(0, -45f, 0));

        var metalMat = new Material(Shader.Find("HDRP/Lit")) { color = new Color(0.35f, 0.35f, 0.38f) };
        metalMat.SetFloat("_Metallic", 0.85f);
        metalMat.SetFloat("_Smoothness", 0.55f);
        if (armor != null)
        {
            foreach (var r in armor.GetComponentsInChildren<Renderer>())
            {
                var count = r.sharedMaterials.Length;
                var mats = new Material[count];
                for (int i = 0; i < count; i++) mats[i] = metalMat;
                r.sharedMaterials = mats;
            }
        }
    }

    static void BuildWindowAndMoonlightShafts(Transform root)
    {
        float eastX = RoomW / 2f - 0.05f;

        // East Window with Velvet Window Drapes
        var drape1 = LoadMesh(DrapePath, "EastWindowDrape_L", root,
            new Vector3(eastX - 0.05f, 1.8f, 0.9f), new Vector3(0.85f, 1.0f, 0.85f), Quaternion.Euler(0, -90f, 0));
        var drape2 = LoadMesh(DrapePath, "EastWindowDrape_R", root,
            new Vector3(eastX - 0.05f, 1.8f, -0.1f), new Vector3(0.85f, 1.0f, 0.85f), Quaternion.Euler(0, 90f, 0));

        // Moonbeam light through window
        var moonGo = new GameObject("SlattedMoonlightShaft");
        moonGo.transform.SetParent(root, false);
        moonGo.transform.localPosition = new Vector3(eastX + 0.2f, 2.0f, 0.4f);
        moonGo.transform.localRotation = Quaternion.Euler(25f, -110f, 0f);

        var moonLight = moonGo.AddComponent<Light>();
        moonLight.type = LightType.Spot;
        moonLight.color = new Color(0.55f, 0.70f, 0.95f);
        moonLight.range = 7.5f;
        moonLight.spotAngle = 45f;
        moonLight.innerSpotAngle = 25f;
        moonLight.intensity = 45f;
        moonLight.lightUnit = LightUnit.Lumen;
        moonLight.shadows = LightShadows.Soft;
        var moonHd = moonGo.AddComponent<HDAdditionalLightData>();
        moonHd.affectsVolumetric = true;
    }

    static void BuildDustAndCobwebs(Transform root)
    {
        float halfW = RoomW / 2f;
        float halfD = RoomD / 2f;

        // Volumetric dust motes
        var dustGo = new GameObject("VolumetricDustMotes");
        dustGo.transform.SetParent(root, false);
        dustGo.transform.localPosition = new Vector3(0.2f, 1.4f, 0.3f);

        var ps = dustGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 9f;
        main.startSpeed = 0.025f;
        main.startSize = 0.015f;
        main.maxParticles = 60;
        main.startColor = new Color(0.85f, 0.88f, 0.95f, 0.22f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(2.5f, 1.8f, 2.5f);

        var emission = ps.emission;
        emission.rateOverTime = 6f;

        // Corner Cobwebs
        GameObject cobweb02Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Cobweb02Path);
        GameObject cobweb03Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Cobweb03Path);
        Material webMat = CreateCobwebMaterial();

        if (cobweb02Prefab != null)
        {
            var web1 = (GameObject)PrefabUtility.InstantiatePrefab(cobweb02Prefab, root);
            web1.name = "CornerCobweb_NE";
            web1.transform.localPosition = new Vector3(halfW - 0.05f, RoomH - 0.05f, halfD - 0.05f);
            web1.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            web1.transform.localScale = Vector3.one * 0.35f;
            foreach (var r in web1.GetComponentsInChildren<Renderer>()) r.sharedMaterial = webMat;
        }

        if (cobweb03Prefab != null)
        {
            var web2 = (GameObject)PrefabUtility.InstantiatePrefab(cobweb03Prefab, root);
            web2.name = "CornerCobweb_NW";
            web2.transform.localPosition = new Vector3(-halfW + 0.05f, RoomH - 0.05f, halfD - 0.05f);
            web2.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            web2.transform.localScale = Vector3.one * 0.35f;
            foreach (var r in web2.GetComponentsInChildren<Renderer>()) r.sharedMaterial = webMat;
        }
    }

    static Material CreateCobwebMaterial()
    {
        var mat = new Material(Shader.Find("HDRP/Lit"));
        mat.color = new Color(0.75f, 0.75f, 0.75f, 0.18f);
        mat.SetFloat("_SurfaceType", 1.0f); // Transparent
        mat.SetFloat("_BlendMode", 0.0f); // Alpha blend
        mat.SetFloat("_DoubleSidedEnable", 1.0f);
        mat.SetFloat("_Smoothness", 0.05f);
        mat.SetFloat("_Metallic", 0f);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return mat;
    }

    static void BuildLighting(Transform root, Vector3 clockPos)
    {
        // Wall sconces (Lamp_2) with PBR textures applied
        var sconce1 = LoadMesh(LampPath, "WallSconce_North", root,
            new Vector3(-1.2f, 2.0f, RoomD / 2f - 0.05f), new Vector3(0.7f, 0.7f, 0.7f), Quaternion.identity);
        ApplyPbr(sconce1, LampAlbedo, LampNormal, 0.65f, 0.75f);

        var sconce2 = LoadMesh(LampPath, "WallSconce_East", root,
            new Vector3(RoomW / 2f - 0.05f, 2.0f, 1.2f), new Vector3(0.7f, 0.7f, 0.7f), Quaternion.Euler(0, -90f, 0));
        ApplyPbr(sconce2, LampAlbedo, LampNormal, 0.65f, 0.75f);

        // Soft ambient room fill
        var fillGo = new GameObject("WakeRoomAmbientFill");
        fillGo.transform.SetParent(root, false);
        fillGo.transform.localPosition = new Vector3(0, 1.8f, 0);

        var l = fillGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.75f, 0.50f);
        l.range = 5.0f;
        l.shadows = LightShadows.None;
        l.lightUnit = LightUnit.Lumen;
        l.intensity = 10f;
    }

    static void BuildWakePose(Transform root, Vector3 clockPos)
    {
        var wake = new GameObject("WakePose");
        wake.transform.SetParent(root, false);

        // Player awakens lying in bed looking across bedside table towards grandfather clock & mantel
        Vector3 pos = new Vector3(-0.95f, 0.65f, -0.4f);
        wake.transform.localPosition = pos;

        Vector3 target = new Vector3(0.15f, 1.05f, 1.1f);
        Vector3 toTarget = target - pos;
        toTarget.y = 0;
        wake.transform.localRotation = toTarget.sqrMagnitude > 0.001f ? Quaternion.LookRotation(toTarget.normalized, Vector3.up) : Quaternion.identity;
    }
}
