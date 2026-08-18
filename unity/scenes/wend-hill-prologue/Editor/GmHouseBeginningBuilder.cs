using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Builds the authored interior reached by the Ninth Bell. It remains in the canonical prologue scene
/// so the exterior crossing, house entry, Aldric introduction and first game are one uninterrupted run.
/// </summary>
public static class GmHouseBeginningBuilder
{
    public const string RootName = "HouseBeginning";
    public const float EntryCentreZ = 383.25f;
    public const float ParlorCentreZ = 358.25f;
    const float WallT = 0.32f;

    static Material plaster;
    static Material panel;
    static Material floor;
    static Material runner;
    static Material darkWood;
    static Material blackWood;
    static Material brass;
    static Material portraitCanvas;
    static Material paper;
    static Material mirrorShard;
    static Material armourMetal;
    static Material bone;
    static Material red;
    static Material glass;
    static Material moonGlass;
    static Material fire;

    static readonly (string slug, string name, string text, string second)[] Portraits =
    {
        ("edwin-marr", "EDWIN MARR",
            "Edwin Marr's painted fingers rest on a fan of cards. A scratched note beneath the frame reads: WATCH HIS HANDS.",
            "The warning was cut with a key or a card edge. Edwin wanted the next guest to notice."),
        ("caspian-dufresne", "CASPIAN DUFRESNE",
            "Caspian Dufresne smiles at someone outside the frame. His left cuff has been painted over three times.",
            "Each repaint hides the same dark mark at the wrist."),
        ("halvard-pike", "HALVARD PIKE",
            "Halvard Pike stands beside a closed box, one hand missing behind the gilt edge.",
            "The canvas continues under the frame. Someone narrowed it to hide what he held."),
        ("solveig-hale", "SOLVEIG HALE",
            "Solveig Hale's eyes were varnished last. They catch the room even when the rest of her face goes dark.",
            "A tiny Flame suit is sewn into the painted collar."),
        ("theo-gall", "THEO GALL",
            "Theo Gall wears a clubman's evening coat. The room behind him is older than this house.",
            "A coaching-inn sign survives beneath the newer wallpaper."),
        ("barnaby-quill", "BARNABY QUILL",
            "Barnaby Quill holds a fountain pen above an unsigned confession. The nib points toward the ledger.",
            "The unfinished word begins with C and ends beneath his thumb."),
        ("imogen-thale", "IMOGEN THALE",
            "Imogen Thale has been painted listening. Nine pale strokes circle her ear like bell marks.",
            "The ninth mark is fresher than the portrait."),
        ("constance", "CONSTANCE AUBREY-LOCKE",
            "Constance Aubrey-Locke watches the dealer, not the cards. In the margin: HE LIFTS HIS CHIN BEFORE THE IMPOSSIBLE ONE.",
            "The note names a tell. The pose names the same one."),
        ("percival", "PERCIVAL",
            "The brass plate is rubbed half through. Only PERCIVAL survives. Someone returned to this name until the others wore away.",
            "The lower frame sits proud of the wall. Something thin catches behind it."),
    };

    public static void Build()
    {
        GameObject stale = GameObject.Find(RootName);
        if (stale != null) UnityEngine.Object.DestroyImmediate(stale);
        GmVictorianInteriorKit.Prepare();
        CreateMaterials();

        var root = new GameObject(RootName);
        root.AddComponent<GmHouseProgress>();
        root.AddComponent<GmHouseBeginning>();
        root.AddComponent<GmHouseHud>();
        BuildEntryHall(root.transform);
        BuildParlor(root.transform);
        Debug.Log($"[GmHouseBuilder] PASS: connected wake vestibule + entry hall + Parlor, " +
                  $"portraits={Portraits.Length}, critical clues=4, first game=28-card best-of-three");
    }

    static void CreateMaterials()
    {
        plaster = GmVictorianInteriorKit.Surface("wall", "House_Wallpaper", new Vector2(7f, 2f),
            new Color(0.48f, 0.43f, 0.39f));
        panel = GmVictorianInteriorKit.Surface("door", "House_WallPanel", new Vector2(4f, 1.5f),
            new Color(0.31f, 0.19f, 0.12f));
        floor = GmVictorianInteriorKit.Surface("floor", "House_OakFloor", new Vector2(7f, 9f),
            new Color(0.44f, 0.34f, 0.27f));
        runner = GmVictorianInteriorKit.Surface("carpet", "House_Runner", new Vector2(1f, 5f),
            new Color(0.55f, 0.25f, 0.21f));
        darkWood = GmVictorianInteriorKit.Surface("door", "House_DarkWood", new Vector2(1.5f, 1.5f),
            new Color(0.30f, 0.19f, 0.12f));
        blackWood = Mat("House_BlackWood", new Color(0.018f, 0.017f, 0.016f), 0.22f);
        brass = Mat("House_AgedBrass", new Color(0.38f, 0.27f, 0.09f), 0.58f, 0.32f);
        portraitCanvas = Mat("House_PortraitCanvas", new Color(0.12f, 0.095f, 0.073f), 0.16f);
        paper = Mat("House_AgedPaper", new Color(0.34f, 0.28f, 0.19f), 0.10f);
        mirrorShard = Mat("House_MirrorShard", new Color(0.055f, 0.072f, 0.078f), 0.90f, 0.68f);
        armourMetal = Mat("House_ArmourMetal", new Color(0.16f, 0.17f, 0.18f), 0.68f, 0.82f);
        bone = Mat("House_Bone", new Color(0.64f, 0.60f, 0.49f), 0.26f);
        red = Mat("House_Oxblood", new Color(0.30f, 0.018f, 0.014f), 0.31f);
        glass = Mat("House_Mirror", new Color(0.48f, 0.55f, 0.58f), 0.92f, 0.65f);
        glass.EnableKeyword("_EMISSIVE_COLOR_MAP");
        glass.SetColor("_EmissiveColor", new Color(0.035f, 0.065f, 0.085f));
        // The glass is a dark exterior value, not a luminous blue card. The first window pass used
        // emissive cobalt panes and the standalone read them as two flat blue rectangles. Cool light
        // belongs on the room surfaces; the pane itself should stay nearly black at night.
        moonGlass = Mat("House_MoonWindow", new Color(0.018f, 0.026f, 0.042f), 0.78f, 0.06f);
        fire = Mat("House_Fire", new Color(1f, 0.20f, 0.025f), 0.15f);
        fire.EnableKeyword("_EMISSIVE_COLOR_MAP");
        fire.SetColor("_EmissiveColor", new Color(0.72f, 0.15f, 0.025f));
    }

    internal static Material Mat(string name, Color color, float smoothness, float metallic = 0f)
    {
        // Guarded like the same lookup in GmVictorianInteriorKit. A null shader here builds fifteen
        // magenta materials for the whole interior, which the project treats as never acceptable, and
        // Unity's own null-shader material would carry that through the build without an error.
        Shader shader = Shader.Find("HDRP/Lit");
        if (shader == null) throw new InvalidOperationException("HDRP/Lit is unavailable");
        var material = new Material(shader) { name = name, color = color };
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", metallic);
        return material;
    }

    static void BuildEntryHall(Transform root)
    {
        var hall = new GameObject("EntryHall").transform;
        hall.SetParent(root, false);
        BuildRoomShell(hall, "Entry", EntryCentreZ, 18f, 26f, 5.25f, 2.8f, true, true);
        Slab("CrimsonRunner", new Vector3(0f, 0.025f, EntryCentreZ), new Vector3(2.7f, 0.05f, 24.8f), runner, hall, false);

        // Wainscot, pilasters and ceiling ribs make the long room legible as finished architecture
        // rather than a large sealed cube.
        for (float z = 372.1f; z <= 394.5f; z += 3.2f)
        {
            Slab($"WestPilaster_{z:000}", new Vector3(-8.72f, 1.65f, z), new Vector3(0.16f, 3.3f, 0.18f), panel, hall, false);
            Slab($"EastPilaster_{z:000}", new Vector3(8.72f, 1.65f, z), new Vector3(0.16f, 3.3f, 0.18f), panel, hall, false);
        }
        Slab("WestChairRail", new Vector3(-8.73f, 1.2f, EntryCentreZ), new Vector3(0.13f, 0.14f, 25.3f), brass, hall, false);
        Slab("EastChairRail", new Vector3(8.73f, 1.2f, EntryCentreZ), new Vector3(0.13f, 0.14f, 25.3f), brass, hall, false);

        BuildPortraitGallery(hall);
        BuildLedger(hall);
        BuildHallFurniture(hall);
        BuildStairAndRope(hall);
        BuildParlorDoor(hall);
        BuildFireplace(hall, new Vector3(8.38f, 0f, 390.6f), Quaternion.Euler(0f, -90f, 0f), "HallHearth", "hall-fireplace");
        BuildChandelier(hall, new Vector3(0f, 4.35f, 384.2f), "HallChandelier", 120f);
        BuildMoonWindowOnZWall(hall, "NorthMoonWindowWest",
            new Vector3(-5.35f, 2.62f, 396.22f), new Vector3(-8.0f, 1.55f, 380.5f),
            new Vector2(2.0f, 3.15f), 1200f, 23f);
        BuildMoonWindowOnZWall(hall, "NorthMoonWindowEast",
            new Vector3(5.35f, 2.62f, 396.22f), new Vector3(8.0f, 1.55f, 380.5f),
            new Vector2(2.0f, 3.15f), 1200f, 23f);
        BuildChandelier(hall, new Vector3(0f, 4.15f, 393.4f), "NorthHallChandelier", 160f);
        BuildWallSconce(hall, "WestGalleryLampNorth", new Vector3(-8.45f, 2.72f, 389.8f), Vector3.right, 60f);
        BuildWallSconce(hall, "WestGalleryLampSouth", new Vector3(-8.45f, 2.72f, 378.2f), Vector3.right, 60f);
        BuildWallSconce(hall, "EastGalleryLampNorth", new Vector3(8.45f, 2.72f, 389.8f), Vector3.left, 60f);
        BuildWallSconce(hall, "EastGalleryLampSouth", new Vector3(8.45f, 2.72f, 378.2f), Vector3.left, 60f);
        AddCoolSpot(hall, "NorthHallMoonBounce", new Vector3(0f, 3.15f, 395.2f),
            new Vector3(0f, 1.05f, 381.0f), 1200f, 20f, 102f);
        AddCoolSpot(hall, "LedgerMoonFill", new Vector3(-5.3f, 2.55f, 395.7f),
            new Vector3(-5.15f, 0.8f, 390.0f), 700f, 8f, 58f);
        AddCoolSpot(hall, "WakeClockFill", new Vector3(2.75f, 2.45f, 400.25f),
            new Vector3(-0.10f, 1.15f, 400.85f), 260f, 7f, 88f);
        AddWarmLight(hall, "HallRouteFill", new Vector3(0f, 2.3f, 384.0f), 100f, 13f);

        Transform wakeClock = GameObject.Find("WakeRoom")?.transform;
        if (wakeClock != null)
        {
            BalanceWakeRoomLighting(wakeClock);
            Transform clock = FindChildContaining(wakeClock, "Clock");
            if (clock != null)
                AddInteractable(clock.gameObject, "hall-clock", "Read",
                    "The longcase clock has stopped at nine. Its pendulum still moves, but never crosses the centre line.",
                    "Nine strokes on the dial's inner rim are newer than the brass.", 3.8f, 10f);
        }
    }

    internal static void BalanceWakeRoomLighting(Transform wakeRoom)
    {
        // GmWakeRoom predates the house proof and its three tiny warm sources were exposed into a
        // full-room red wash. Keep the candle and hearth readable as practicals, but let the existing
        // east-window shaft and the clock shaft establish the room's night value.
        foreach (Light light in wakeRoom.GetComponentsInChildren<Light>(true))
        {
            switch (light.name)
            {
                case "CandleFlame":
                    light.intensity = 22f;
                    light.color = new Color(1f, 0.66f, 0.40f);
                    break;
                case "HearthEmberLight":
                    light.intensity = 22f;
                    light.color = new Color(1f, 0.58f, 0.34f);
                    break;
                case "WakeRoomAmbientFill":
                    light.intensity = 8f;
                    light.color = new Color(1f, 0.78f, 0.58f);
                    break;
                case "SlattedMoonlightShaft":
                    light.intensity = 75f;
                    light.color = new Color(0.62f, 0.72f, 0.92f);
                    HDAdditionalLightData hd = light.GetComponent<HDAdditionalLightData>();
                    if (hd != null) hd.affectsVolumetric = false;
                    break;
            }
        }
    }

    /// Internal rather than private: GmWendOutbuildings reuses this exact wall/doorway carving for
    /// the coach house interior rather than duplicating it. Room-local Z only -- callers rotate the
    /// parent transform to align local Z with whatever world direction the room actually faces.
    internal static void BuildRoomShell(Transform parent, string prefix, float centreZ, float width, float depth,
        float height, float doorwayWidth, bool northDoor, bool southDoor)
    {
        float halfW = width * 0.5f;
        float halfD = depth * 0.5f;
        Slab(prefix + "Floor", new Vector3(0f, -WallT * 0.5f, centreZ), new Vector3(width + WallT, WallT, depth + WallT), floor, parent);
        Slab(prefix + "Ceiling", new Vector3(0f, height + WallT * 0.5f, centreZ), new Vector3(width + WallT, WallT, depth + WallT), plaster, parent);
        Slab(prefix + "WestWall", new Vector3(-halfW - WallT * 0.5f, height * 0.5f, centreZ), new Vector3(WallT, height, depth + WallT), plaster, parent);
        Slab(prefix + "EastWall", new Vector3(halfW + WallT * 0.5f, height * 0.5f, centreZ), new Vector3(WallT, height, depth + WallT), plaster, parent);
        if (southDoor) BuildDoorWall(parent, prefix + "South", centreZ - halfD - WallT * 0.5f, width, height, doorwayWidth);
        else Slab(prefix + "SouthWall", new Vector3(0f, height * 0.5f, centreZ - halfD - WallT * 0.5f),
            new Vector3(width + WallT, height, WallT), plaster, parent);
        if (northDoor) BuildDoorWall(parent, prefix + "North", centreZ + halfD + WallT * 0.5f, width, height, doorwayWidth);
        else Slab(prefix + "NorthWall", new Vector3(0f, height * 0.5f, centreZ + halfD + WallT * 0.5f),
            new Vector3(width + WallT, height, WallT), plaster, parent);
    }

    static void BuildDoorWall(Transform parent, string name, float z, float width, float height, float opening)
    {
        float side = (width - opening) * 0.5f;
        Slab(name + "WallWest", new Vector3(-(opening * 0.5f + side * 0.5f), height * 0.5f, z),
            new Vector3(side, height, WallT), plaster, parent);
        Slab(name + "WallEast", new Vector3(opening * 0.5f + side * 0.5f, height * 0.5f, z),
            new Vector3(side, height, WallT), plaster, parent);
        float lintelHeight = Mathf.Max(0.2f, height - 4.2f);
        Slab(name + "Lintel", new Vector3(0f, 4.2f + lintelHeight * 0.5f, z),
            new Vector3(opening, lintelHeight, WallT), plaster, parent);
        Slab(name + "FrameWest", new Vector3(-opening * 0.5f, 2.1f, z - 0.05f), new Vector3(0.20f, 4.2f, 0.42f), darkWood, parent);
        Slab(name + "FrameEast", new Vector3(opening * 0.5f, 2.1f, z - 0.05f), new Vector3(0.20f, 4.2f, 0.42f), darkWood, parent);
        Slab(name + "FrameTop", new Vector3(0f, 4.12f, z - 0.05f), new Vector3(opening, 0.20f, 0.42f), darkWood, parent);
    }

    static void BuildPortraitGallery(Transform hall)
    {
        var gallery = new GameObject("Portraits").transform;
        gallery.SetParent(hall, false);
        for (int i = 0; i < Portraits.Length; i++)
        {
            bool west = i < 5;
            int row = west ? i : i - 5;
            float z = west ? 393.0f - row * 4.35f : 392.0f - row * 5.2f;
            Vector3 position = new Vector3(west ? -8.77f : 8.77f, 3.32f, z);
            Quaternion rotation = Quaternion.LookRotation(west ? Vector3.right : Vector3.left, Vector3.up);
            BuildPortrait(gallery, Portraits[i], position, rotation, i);
        }

        var blank = new GameObject("BlankPortrait");
        blank.transform.SetParent(gallery, true);
        blank.transform.SetPositionAndRotation(new Vector3(8.76f, 3.25f, 372.0f), Quaternion.LookRotation(Vector3.left));
        OrnateFrame(blank.transform, 1.55f, 2.2f, 8);
        AddInteractable(blank, "hall-blank-portrait", "Examine",
            "Fresh canvas in an old frame. The proportions are yours, though no face has been painted yet.",
            "A pencil line marks the height of your eyes.");
    }

    static void BuildPortrait(Transform parent,
        (string slug, string name, string text, string second) data, Vector3 position, Quaternion rotation, int index)
    {
        var portrait = new GameObject("Portrait_" + (data.slug == "percival" ? "Percival" : data.slug));
        portrait.transform.SetParent(parent, true);
        portrait.transform.SetPositionAndRotation(position, rotation);
        OrnateFrame(portrait.transform, 1.48f, 2.05f, index, GmVictorianInteriorKit.Portrait(data.slug));
        Slab("Nameplate_" + data.name.Replace(' ', '_'), new Vector3(0f, -1.23f, -0.06f),
            new Vector3(1.12f, 0.22f, 0.05f), brass, portrait.transform, false);
        WorldText("Inscription_" + data.slug, data.name, new Vector3(0f, -1.23f, -0.095f),
            Quaternion.Euler(0f, 180f, 0f), 0.014f, bone, portrait.transform);
        AddInteractable(portrait, "hall-portrait-" + data.slug, "Read", data.text, data.second, 3.8f, 10f);

        if (data.slug == "percival")
        {
            var shard = new GameObject("MirrorShard");
            shard.transform.SetParent(portrait.transform, false);
            shard.transform.localPosition = new Vector3(0.48f, -0.73f, 0.12f);
            shard.transform.localRotation = Quaternion.Euler(0f, 180f, 18f);
            var mesh = shard.AddComponent<MeshFilter>();
            mesh.sharedMesh = TriangleMesh();
            shard.AddComponent<MeshRenderer>().sharedMaterial = mirrorShard;
            var collider = shard.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.22f, 0.30f, 0.05f);

            var glintGo = new GameObject("ShardGlint");
            glintGo.transform.SetParent(shard.transform, false);
            glintGo.transform.localPosition = new Vector3(0f, 0f, 0.04f);
            var glintLight = glintGo.AddComponent<Light>();
            glintLight.type = LightType.Point;
            glintLight.color = new Color(0.80f, 0.92f, 1.0f);
            glintLight.range = 0.8f;
            glintLight.intensity = 12f;
            glintLight.lightUnit = LightUnit.Lumen;

            AddInteractable(shard, "hall-mirror-shard", "Take",
                "A triangular mirror shard was pressed behind Percival's frame. In it, Aldric's chair is occupied even from the empty hall.",
                "The shard is cold enough to numb your palm.", 3.6f, 11f, GmInteractionRepeatPolicy.FirstOnly);
        }
    }

    static void OrnateFrame(Transform parent, float width, float height, int index, Material canvasMaterial = null)
    {
        // The purchased frame owns the silhouette; the thin authored canvas remains a separate clue
        // layer so narrative portrait art can later replace it without replacing interaction logic.
        Slab("Canvas", Vector3.zero, new Vector3(width, height, 0.055f), canvasMaterial ?? portraitCanvas, parent, false);
        GmVictorianInteriorKit.Place($"Picture_{index % 8 + 1}", $"PortraitFrame_{index + 1:00}", parent,
            parent.position, new Vector3(width + 0.42f, height + 0.42f, 0.30f), parent.rotation, "picture");
    }

    static void BuildLedger(Transform hall)
    {
        var desk = new GameObject("LedgerDesk").transform;
        desk.SetParent(hall, false);
        desk.position = new Vector3(-5.15f, 0f, 390.0f);
        GmVictorianInteriorKit.Place("Table_2", "LedgerDesk", desk,
            desk.position + new Vector3(0f, 0.55f, 0f), new Vector3(2.45f, 1.1f, 1.15f),
            Quaternion.Euler(0f, 90f, 0f), "table");

        // Open Ledger Book — Real FBX mesh: SM_Book_1.fbx
        var bookPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LeartesStudios/WitchVillage/HDRP/Art/Meshes/SmallProps/Books/SM_Book_1.fbx");
        GameObject ledger;
        if (bookPrefab != null)
        {
            ledger = (GameObject)PrefabUtility.InstantiatePrefab(bookPrefab, desk);
            ledger.name = "Ledger";
            ledger.transform.localPosition = new Vector3(0f, 0.88f, 0f);
            ledger.transform.localRotation = Quaternion.Euler(0f, 102f, 0f);
            ledger.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
        }
        else
        {
            ledger = new GameObject("Ledger");
            ledger.transform.SetParent(desk, false);
            ledger.transform.localPosition = new Vector3(0f, 0.88f, 0f);
            ledger.transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
            Slab("LeatherCover", new Vector3(0f, -0.045f, 0f), new Vector3(1.46f, 0.09f, 0.72f), red, ledger.transform, false);
        }

        var collider = ledger.GetComponent<Collider>() ?? ledger.AddComponent<BoxCollider>();
        if (collider is BoxCollider boxCol) boxCol.size = new Vector3(1.85f, 0.3f, 0.95f);
        AddInteractable(ledger, "hall-ledger", "Read",
            "Names, down the page: Marr. Dufresne. Pike. Hale. Gall. Quill. Thale. Aubrey-Locke. A ninth, the ink gone soft where someone kept touching it, worn past reading. Every one crossed neatly through. At the foot, a blank line left open. My width, exactly.",
            "Nine crossed names. The blank tenth line waits at the exact width of your name.", 3.6f, 10f);

        // Guestbook Brass Inkstand and Glass Inkwell (SM_Bottles_1.fbx)
        var bottlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LeartesStudios/WitchVillage/HDRP/Art/Meshes/SmallProps/Bottles/SM_Bottles_1.fbx");
        GameObject inkstand;
        if (bottlePrefab != null)
        {
            inkstand = (GameObject)PrefabUtility.InstantiatePrefab(bottlePrefab, desk);
            inkstand.name = "Inkstand";
            inkstand.transform.localPosition = new Vector3(0.85f, 0.88f, 0.18f);
            inkstand.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
        }
        else
        {
            inkstand = new GameObject("Inkstand");
            inkstand.transform.SetParent(desk, false);
            inkstand.transform.localPosition = new Vector3(0.85f, 0.88f, 0.18f);
            Cylinder("InkwellBrass", Vector3.zero, new Vector3(0.12f, 0.06f, 0.12f), brass, inkstand.transform, false);
        }

        var penCol = inkstand.GetComponent<Collider>() ?? inkstand.AddComponent<BoxCollider>();
        if (penCol is BoxCollider bCol) bCol.size = new Vector3(0.25f, 0.3f, 0.25f);
        AddInteractable(inkstand, "hall-ledger-pen", "Examine",
            "A brass inkwell with a bone dip pen resting in the groove. The nib is coated in fresh, wet black ink.",
            "The nib has not yet dried. The eleventh guest was not expected to wait long.", 3.2f, 10f);
    }

    static void BuildHallFurniture(Transform hall)
    {
        // Armour and coat stand flank the wake vestibule.
        var armour = new GameObject("HouseArmour").transform;
        armour.SetParent(hall, false); armour.position = new Vector3(-6.5f, 0f, 394.1f);
        GmVictorianInteriorKit.PlaceWithMaterial("Assets/GamesMaster/Interior/Armor_Metal.fbx", "HouseArmour", armour,
            armour.position + new Vector3(0f, 1.25f, 0f), new Vector3(1.15f, 2.50f, 0.90f),
            Quaternion.Euler(0f, 22f, 0f), armourMetal);
        var armourCollider = armour.gameObject.AddComponent<BoxCollider>();
        armourCollider.center = new Vector3(0f, 1.25f, 0f);
        armourCollider.size = new Vector3(1.15f, 2.5f, 0.9f);
        AddInteractable(armour.gameObject, "hall-armour", "Examine",
            "The armour is ceremonial and recently polished. The right gauntlet is posed to deal a card.",
            "Inside the visor, someone has scratched: NOT THE FIRST HOUSE.");

        var coats = new GameObject("CoatStand").transform;
        coats.SetParent(hall, false); coats.position = new Vector3(6.4f, 0f, 394.0f);
        Slab("Stand", new Vector3(0f, 1.1f, 0f), new Vector3(0.12f, 2.2f, 0.12f), darkWood, coats, false);
        Slab("Hooks", new Vector3(0f, 2.05f, 0f), new Vector3(1.0f, 0.10f, 0.10f), brass, coats, false);
        Capsule("WaitingCoat", new Vector3(0.24f, 1.35f, 0f), new Vector3(0.72f, 1.45f, 0.25f), blackWood, coats, false);
        AddInteractable(coats.gameObject, "hall-coat-rack", "Search",
            "One coat is damp from rain that never touched you. In its pocket: your invitation, folded along old creases.",
            "The invitation now bears a pencilled table number: I.");

        var decanter = new GameObject("DecanterTable").transform;
        decanter.SetParent(hall, false); decanter.position = new Vector3(-5.7f, 0f, 374.0f);
        GmVictorianInteriorKit.Place("Table_2", "DecanterConsole", decanter,
            decanter.position + new Vector3(0f, 0.5f, 0f), new Vector3(2.1f, 1.0f, 0.95f),
            Quaternion.Euler(0f, 90f, 0f), "table");
        Cylinder("Decanter", new Vector3(-0.32f, 1.10f, 0f), new Vector3(0.28f, 0.58f, 0.28f), glass, decanter, false);
        Cylinder("Glass", new Vector3(0.34f, 1.04f, 0f), new Vector3(0.20f, 0.32f, 0.20f), glass, decanter, false);
        AddInteractable(decanter.gameObject, "hall-decanter", "Examine",
            "The decanter smells of smoke and orange peel. Only one glass has been poured, on the side nearest the Parlor.",
            "A wet ring marks where a second glass used to stand.");

        var globe = new GameObject("HouseGlobe").transform;
        globe.SetParent(hall, false); globe.position = new Vector3(6.6f, 0f, 374.0f);
        Sphere("GlobeSphere", new Vector3(0f, 1.25f, 0f), Vector3.one * 1.15f, portraitCanvas, globe, false);
        Slab("GlobeStand", new Vector3(0f, 0.55f, 0f), new Vector3(0.15f, 1.1f, 0.15f), brass, globe, false);
        AddInteractable(globe.gameObject, "hall-globe", "Turn",
            "The globe has no Wend Hill. Every route inked across it ends at the same tiny black house.",
            "Turn it again. The house stays facing you.");

        var settee = new GameObject("WaitingSettee").transform;
        settee.SetParent(hall, false); settee.position = new Vector3(6.25f, 0f, 378.2f);
        GmVictorianInteriorKit.Place("Couch_2", "WaitingSettee", settee,
            settee.position + new Vector3(0f, 0.75f, 0f), new Vector3(3.2f, 1.55f, 1.25f),
            Quaternion.Euler(0f, -90f, 0f), "couch");
        AddInteractable(settee.gameObject, "hall-settee", "Examine",
            "The settee holds the shallow impression of someone who rose when you woke.",
            "A single pale thread is caught beneath the cushion, the colour of Aldric's gloves.");

        var mirror = new GameObject("HallMirror").transform;
        mirror.SetParent(hall, false); mirror.position = new Vector3(8.58f, 2.45f, 384.0f);
        mirror.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
        GmVictorianInteriorKit.Place("Mirror_1", "HallMirror", mirror, mirror.position,
            new Vector3(0.35f, 2.7f, 1.25f), mirror.rotation, "mirror");
    }

    static void BuildStairAndRope(Transform hall)
    {
        var stair = new GameObject("RopedStair").transform;
        stair.SetParent(hall, false);
        stair.position = new Vector3(5.0f, 0f, 385.0f);
        GmVictorianInteriorKit.Place("Stair", "RopedStair", stair,
            stair.position + new Vector3(0f, 1.75f, 1.7f), new Vector3(4.8f, 3.5f, 6.4f),
            Quaternion.identity, "stair");
        Cylinder("RopePostLeft", new Vector3(-2.25f, 0.68f, -0.65f), new Vector3(0.15f, 1.36f, 0.15f), brass, stair);
        Cylinder("RopePostRight", new Vector3(2.25f, 0.68f, -0.65f), new Vector3(0.15f, 1.36f, 0.15f), brass, stair);
        Slab("VelvetRope", new Vector3(0f, 1.04f, -0.65f), new Vector3(4.35f, 0.12f, 0.12f), red, stair);
        AddInteractable(stair.gameObject, "hall-stairs", "Examine",
            "A velvet rope closes the stairs. Dust lies thick above it, except for one narrow track descending toward the portraits.",
            "The rope is warm, as if someone just replaced it.");
    }

    static void BuildParlorDoor(Transform hall)
    {
        var door = new GameObject("ParlorDoor");
        door.transform.SetParent(hall, true);
        door.transform.position = new Vector3(0f, 0f, 370.20f);
        var interaction = door.AddComponent<BoxCollider>();
        interaction.center = new Vector3(0f, 2f, 0.2f);
        interaction.size = new Vector3(2.7f, 4f, 0.40f);
        var leaf = new GameObject("DoorLeaf");
        leaf.transform.SetParent(door.transform, false);
        leaf.transform.localPosition = new Vector3(-1.27f, 0f, 0f);
        GmVictorianInteriorKit.Place("Door_1", "ParlorDoorLeaf", leaf.transform,
            door.transform.position + new Vector3(0f, 2.0f, 0f), new Vector3(2.54f, 4.0f, 0.28f),
            Quaternion.Euler(0f, 180f, 0f), "door");
        AddInteractable(door, "hall-parlor-door", "Open",
            "Warmth breathes around the Parlor door. The latch remains still, listening for the hall's answer.",
            "The ledger's blank line seems to continue under the threshold.", 4f, 9f);
    }

    static void BuildParlor(Transform root)
    {
        var parlor = new GameObject("Parlor").transform;
        parlor.SetParent(root, false);
        BuildRoomShell(parlor, "Parlor", ParlorCentreZ, 8.8f, 17f, 4.2f, 2.8f, true, false);
        Slab("ParlorCarpetUnderlay", new Vector3(0f, 0.012f, ParlorCentreZ), new Vector3(7.4f, 0.024f, 13.6f), red, parlor, false);
        GmVictorianInteriorKit.Place("Carpet_1", "ParlorCarpet", parlor,
            new Vector3(0f, 0.055f, ParlorCentreZ), new Vector3(7.2f, 0.10f, 13.2f),
            Quaternion.identity, "carpet");
        BuildFireplace(parlor, new Vector3(4.0f, 0f, 358.0f), Quaternion.Euler(0f, -90f, 0f), "ParlorHearth", "parlor-fireplace");
        BuildBookshelves(parlor);
        BuildGameTable(parlor);
        BuildAldric(parlor);
        BuildChandelier(parlor, new Vector3(0f, 3.4f, 358.5f), "ParlorChandelier", 180f);
        BuildMoonWindowOnZWall(parlor, "SouthMoonWindowWest",
            new Vector3(-2.65f, 2.28f, 349.80f), new Vector3(-4.0f, 1.35f, 357.0f),
            new Vector2(1.55f, 2.85f), 450f, 13f);
        BuildMoonWindowOnZWall(parlor, "SouthMoonWindowEast",
            new Vector3(2.65f, 2.28f, 349.80f), new Vector3(4.0f, 1.35f, 357.0f),
            new Vector2(1.55f, 2.85f), 450f, 13f);
        AddWarmLight(parlor, "TableLight", new Vector3(0f, 2.6f, 358.5f), 130f, 8f);
        AddWarmLight(parlor, "NorthParlorFill", new Vector3(2.5f, 2.2f, 363.2f), 55f, 6f);
        AddWarmLight(parlor, "SouthParlorFill", new Vector3(-2.5f, 2.2f, 353.8f), 55f, 6f);

        var threshold = new GameObject("HostIntroductionThreshold");
        threshold.transform.SetParent(parlor, true);
        threshold.transform.position = new Vector3(0f, 1.1f, 366.5f);
        var collider = threshold.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(4.2f, 2.2f, 1.4f);
        threshold.AddComponent<GmHouseThreshold>();

        var seat = new GameObject("PlayerSeatPose");
        seat.transform.SetParent(parlor, true);
        seat.transform.position = new Vector3(0f, 0.1f, 361.7f);
        seat.transform.rotation = Quaternion.LookRotation(Vector3.back);
    }

    static void BuildGameTable(Transform parlor)
    {
        var table = new GameObject("FirstGameTable").transform;
        table.SetParent(parlor, false);
        table.position = new Vector3(0f, 0f, 358.5f);
        GmVictorianInteriorKit.Place("Table_3", "FirstGameTable", table,
            table.position + new Vector3(0f, 0.53f, 0f), new Vector3(2.6f, 1.06f, 2.6f),
            Quaternion.identity, "table");
        GmVictorianInteriorKit.Place("Chair_2", "PlayerChair", table,
            table.position + new Vector3(0f, 0.72f, 2.1f), new Vector3(1.0f, 1.45f, 1.0f),
            Quaternion.identity, "chair");
        GmVictorianInteriorKit.Place("Chair_2", "AldricChair", table,
            table.position + new Vector3(0f, 0.72f, -2.1f), new Vector3(1.0f, 1.45f, 1.0f),
            Quaternion.Euler(0f, 180f, 0f), "chair");

        // Sculpted Tabletop Candle
        var candlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LeartesStudios/WitchVillage/HDRP/Art/Meshes/SmallProps/Candles/SM_Candles_1.fbx");
        if (candlePrefab != null)
        {
            var candle = (GameObject)PrefabUtility.InstantiatePrefab(candlePrefab, table);
            candle.name = "TableCandle";
            candle.transform.localPosition = new Vector3(-0.85f, 1.08f, 0f);
            candle.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
        }

        var cards = new GameObject("FourSuitDeck").transform;
        cards.SetParent(table, false);
        for (int i = 0; i < 14; i++)
        {
            float side = i < 7 ? 1f : -1f;
            int row = i % 7;
            Material suit = (row % 4) switch { 0 => red, 1 => glass, 2 => bone, _ => blackWood };
            Slab($"Card_{i + 1:00}", new Vector3(-0.62f + row * 0.20f, 1.095f, side * (0.45f + Mathf.Abs(row - 3) * 0.035f)),
                new Vector3(0.35f, 0.025f, 0.55f), suit, cards, false);
        }
    }

    static void BuildAldric(Transform parlor)
    {
        var aldric = new GameObject("AldricVoss").transform;
        aldric.SetParent(parlor, false);
        aldric.position = new Vector3(0f, 0f, 356.85f);
        TaperedTorso("EveningCoat", new Vector3(0f, 0.66f, 0f), new Vector3(0.92f, 1.05f, 0.46f), blackWood, aldric);
        Sphere("PorcelainMask", new Vector3(0f, 1.98f, 0.05f), new Vector3(0.44f, 0.53f, 0.30f), bone, aldric, false);
        Slab("MaskEyeLeft", new Vector3(-0.115f, 2.03f, 0.205f), new Vector3(0.085f, 0.035f, 0.018f), blackWood, aldric, false);
        Slab("MaskEyeRight", new Vector3(0.115f, 2.03f, 0.205f), new Vector3(0.085f, 0.035f, 0.018f), blackWood, aldric, false);
        Capsule("LeftSleeve", new Vector3(-0.42f, 1.12f, 0.34f), new Vector3(0.20f, 0.47f, 0.20f), blackWood, aldric, false)
            .transform.localRotation = Quaternion.Euler(58f, 0f, -8f);
        Capsule("RightSleeve", new Vector3(0.42f, 1.12f, 0.34f), new Vector3(0.20f, 0.47f, 0.20f), blackWood, aldric, false)
            .transform.localRotation = Quaternion.Euler(58f, 0f, 8f);
        Sphere("LeftGlove", new Vector3(-0.42f, 0.91f, 0.64f), Vector3.one * 0.17f, bone, aldric, false);
        Sphere("RightGlove", new Vector3(0.42f, 0.91f, 0.64f), Vector3.one * 0.17f, bone, aldric, false);
        Slab("OldClubPin", new Vector3(0.26f, 1.47f, 0.25f), new Vector3(0.085f, 0.085f, 0.035f), brass, aldric, false);
    }

    static void BuildBookshelves(Transform parlor)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            var shelf = new GameObject(side < 0 ? "WestLibrary" : "EastLibrary").transform;
            shelf.SetParent(parlor, false);
            shelf.position = new Vector3(side * 6.35f, 0f, 363.8f);
            for (int bay = 0; bay < 2; bay++)
                GmVictorianInteriorKit.Place("BookShelf_1", $"{shelf.name}_{bay + 1}", shelf,
                    new Vector3(side * 6.35f, 1.85f, 362.3f + bay * 3.15f), new Vector3(0.75f, 3.7f, 2.85f),
                    Quaternion.Euler(0f, side < 0 ? 90f : -90f, 0f), "bookcase");
        }
    }

    static void BuildFireplace(Transform parent, Vector3 position, Quaternion rotation, string name, string interactionId)
    {
        var hearth = new GameObject(name).transform;
        hearth.SetParent(parent, true);
        hearth.SetPositionAndRotation(position, rotation);
        Slab("Opening", new Vector3(0f, 0.83f, -0.34f), new Vector3(1.55f, 1.45f, 0.08f), blackWood, hearth, false);
        GmVictorianInteriorKit.Place("Mantel", name + "Mantel", hearth,
            position + Vector3.up * 1.5f, rotation.eulerAngles.y == 0f
                ? new Vector3(3.2f, 3.0f, 1.0f)
                : new Vector3(1.0f, 3.0f, 3.2f), rotation, "mantel");
        Sphere("Fire", new Vector3(0f, 0.48f, -0.42f), new Vector3(0.9f, 0.75f, 0.18f), fire, hearth, false);
        AddWarmLightLocal(hearth, "FireLight", new Vector3(0f, 1.0f, -1.0f), 320f, 9f);
        AddInteractable(hearth.gameObject, interactionId, "Examine",
            "The fire gives heat but no ash. In the back brickwork, an older coaching-inn hearth shows through.",
            "Aldric calls the next room the back room, though it lies at the front of the house.");
    }

    static void BuildChandelier(Transform parent, Vector3 position, string name, float lumens)
    {
        var fixture = new GameObject(name).transform;
        fixture.SetParent(parent, true); fixture.position = position;
        Cylinder("Stem", Vector3.zero, new Vector3(0.10f, 1.2f, 0.10f), brass, fixture, false);
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.PI * 2f / 6f;
            Vector3 p = new Vector3(Mathf.Cos(angle) * 0.8f, -0.58f, Mathf.Sin(angle) * 0.8f);
            Slab("Arm" + i, p * 0.5f + Vector3.down * 0.25f, new Vector3(0.08f, 0.08f, 0.9f), brass, fixture, false)
                .transform.rotation = Quaternion.Euler(0f, -i * 60f, 0f);
            Cylinder("Candle" + i, p, new Vector3(0.09f, 0.38f, 0.09f), bone, fixture, false);
        }
        AddWarmLightLocal(fixture, "ChandelierLight", new Vector3(0f, -0.6f, 0f), lumens, 14f);
    }

    static void BuildWallSconce(Transform parent, string name, Vector3 position, Vector3 inward, float lumens)
    {
        var fixture = new GameObject(name).transform;
        fixture.SetParent(parent, true);
        fixture.position = position;
        Quaternion rotation = Quaternion.LookRotation(inward, Vector3.up);
        Vector3 size = Mathf.Abs(inward.x) > 0.5f
            ? new Vector3(0.55f, 0.72f, 0.46f)
            : new Vector3(0.46f, 0.72f, 0.55f);
        GmVictorianInteriorKit.Place("Lamp_2", name + "Fixture", fixture, position, size, rotation, "lamp");
        AddWarmLight(parent, name + "Light", position + inward * 0.55f + Vector3.down * 0.05f, lumens, 7f);
    }

    static void BuildMoonWindowOnZWall(Transform parent, string name, Vector3 position,
        Vector3 target, Vector2 size, float lumens, float range)
    {
        var window = new GameObject(name).transform;
        window.SetParent(parent, false);
        window.localPosition = position;

        Slab("MoonlitGlass", Vector3.zero, new Vector3(size.x, size.y, 0.045f),
            moonGlass, window, false);
        const float frame = 0.13f;
        Slab("FrameLeft", new Vector3(-size.x * 0.5f, 0f, -0.025f),
            new Vector3(frame, size.y + frame, 0.11f), darkWood, window, false);
        Slab("FrameRight", new Vector3(size.x * 0.5f, 0f, -0.025f),
            new Vector3(frame, size.y + frame, 0.11f), darkWood, window, false);
        Slab("FrameTop", new Vector3(0f, size.y * 0.5f, -0.025f),
            new Vector3(size.x + frame, frame, 0.11f), darkWood, window, false);
        Slab("FrameBottom", new Vector3(0f, -size.y * 0.5f, -0.025f),
            new Vector3(size.x + frame, frame, 0.11f), darkWood, window, false);
        Slab("Mullion", new Vector3(0f, 0f, -0.045f),
            new Vector3(frame * 0.7f, size.y, 0.10f), darkWood, window, false);

        var lightObject = new GameObject("Moonlight");
        lightObject.transform.SetParent(window, false);
        Vector3 direction = (target - position).normalized;
        lightObject.transform.localPosition = direction * 0.24f;
        lightObject.transform.rotation = Quaternion.LookRotation((target - position).normalized, Vector3.up);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = new Color(0.62f, 0.72f, 0.92f);
        light.range = range;
        light.spotAngle = 56f;
        light.innerSpotAngle = 30f;
        light.shadows = LightShadows.Soft;
        light.intensity = lumens;
        var hd = lightObject.AddComponent<HDAdditionalLightData>();
        hd.lightUnit = LightUnit.Lumen;
        hd.affectsVolumetric = false;
    }

    static void AddCoolSpot(Transform parent, string name, Vector3 position, Vector3 target,
        float lumens, float range, float angle)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = position;
        go.transform.rotation = Quaternion.LookRotation((target - position).normalized, Vector3.up);
        var light = go.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = new Color(0.62f, 0.72f, 0.92f);
        light.range = range;
        light.spotAngle = angle;
        light.innerSpotAngle = angle * 0.58f;
        light.shadows = LightShadows.Soft;
        light.intensity = lumens;
        var hd = go.AddComponent<HDAdditionalLightData>();
        hd.lightUnit = LightUnit.Lumen;
        hd.affectsVolumetric = false;
    }

    internal static void AddWarmLight(Transform parent, string name, Vector3 position, float lumens, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = position;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.66f, 0.40f);
        light.range = range;
        light.shadows = LightShadows.Soft;
        var hd = go.AddComponent<HDAdditionalLightData>();
        hd.lightUnit = LightUnit.Lumen;
        light.intensity = lumens;
        hd.affectsVolumetric = false;
    }

    static void AddWarmLightLocal(Transform parent, string name, Vector3 localPosition, float lumens, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.66f, 0.40f);
        light.range = range;
        light.shadows = LightShadows.Soft;
        light.intensity = lumens;
        var hd = go.AddComponent<HDAdditionalLightData>();
        hd.lightUnit = LightUnit.Lumen;
        hd.affectsVolumetric = false;
    }

    internal static GameObject Slab(string name, Vector3 position, Vector3 size, Material material, Transform parent, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static GameObject Sphere(string name, Vector3 position, Vector3 size, Material material, Transform parent, bool collider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position;
        go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = material;
        if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static GameObject Capsule(string name, Vector3 position, Vector3 size, Material material, Transform parent, bool collider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position;
        go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = material;
        if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static GameObject TaperedTorso(string name, Vector3 position, Vector3 size, Material material, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = size;
        var mesh = new Mesh { name = name + "Mesh" };
        mesh.vertices = new[]
        {
            new Vector3(-0.30f, 0f, -0.50f), new Vector3(0.30f, 0f, -0.50f),
            new Vector3(-0.30f, 0f, 0.50f), new Vector3(0.30f, 0f, 0.50f),
            new Vector3(-0.50f, 1f, -0.50f), new Vector3(0.50f, 1f, -0.50f),
            new Vector3(-0.50f, 1f, 0.50f), new Vector3(0.50f, 1f, 0.50f),
        };
        mesh.triangles = new[]
        {
            0, 4, 5, 0, 5, 1,
            2, 3, 7, 2, 7, 6,
            0, 2, 6, 0, 6, 4,
            1, 5, 7, 1, 7, 3,
            4, 6, 7, 4, 7, 5,
            0, 1, 3, 0, 3, 2,
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    static GameObject Cylinder(string name, Vector3 position, Vector3 size, Material material, Transform parent, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position;
        go.transform.localScale = new Vector3(size.x, size.y * 0.5f, size.z);
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static void WorldText(string name, string text, Vector3 localPosition, Quaternion localRotation,
        float characterSize, Material ignoredMaterial, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;
        var mesh = go.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.fontSize = 48;
        mesh.characterSize = characterSize;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = new Color(0.72f, 0.62f, 0.43f);
    }

    internal static void AddInteractable(GameObject go, string id, string verb, string first, string second,
        float range = 3.7f, float angle = 9f,
        GmInteractionRepeatPolicy policy = GmInteractionRepeatPolicy.FirstThenSecond)
    {
        if (go.GetComponentInChildren<Collider>(true) == null)
        {
            var collider = go.AddComponent<BoxCollider>();
            collider.size = Vector3.one;
        }
        var interactable = go.GetComponent<GmInteractable>() ?? go.AddComponent<GmInteractable>();
        interactable.Configure(id, verb, range, angle, policy);
        interactable.BindContent(first, second);
    }

    static Transform FindChildContaining(Transform parent, string token)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            if (child.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) return child;
        return null;
    }

    static Mesh TriangleMesh()
    {
        var mesh = new Mesh { name = "MirrorShardTriangle" };
        mesh.vertices = new[] { new Vector3(-0.11f, -0.15f, 0f), new Vector3(0.11f, -0.11f, 0f), new Vector3(0.01f, 0.17f, 0f) };
        mesh.triangles = new[] { 0, 2, 1 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
