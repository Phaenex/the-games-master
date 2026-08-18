using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

public static class GmEntryHallBuilder
{
    public const string SceneId = "entry-hall";
    public const string DisplayName = "Entry Hall";
    public const string ScenePath = "Assets/Scenes/EntryHall.unity";

    public const string LibraryKeyClueId = "key:brass_skeleton_key";
    public const string MarrKeyClueId = "key:lady_marr";
    public const string LibraryLeverClueId = GmWeightedShelf.LeverClueId;

    public static readonly string[] DebtorNames =
    {
        "Edwin Marr", "Caspian Dufresne", "Halvard Pike", "Solveig Hale",
        "Theo Gall", "Barnaby Quill", "Imogen Thale", "Constance", "Percival"
    };

    public static readonly string[] DebtorSlugs =
    {
        "edwin-marr", "caspian-dufresne", "halvard-pike", "solveig-hale",
        "theo-gall", "barnaby-quill", "imogen-thale", "constance", "percival"
    };
    public const string LibraryDoorId = "door_library";
    public const string ConservatoryDoorId = "door_conservatory";
    public const string FrontDoorId = "door_front";
    public const string CellarDoorId = "door_cellar";
    public const string PercivalDoorId = "door_percival";
    public const string MarrDoorId = "door_marr";
    public const string BarredGuestDoorId = "door_barred_guest";
    public const string AtticHatchId = "door_attic_hatch";
    public const string AtticKeyClueId = "key:attic_hatch";
    public const string VaultGrateId = "door_vault_grate";
    public const string CourtDoorId = "door_court";
    public const string ShutTheBoxDoorId = "door_shut_the_box";
    public const float CourtDoorZ = -5.0f;
    public const float ShutTheBoxDoorZ = 8.65f;
    public const float GameDoorWidth = 1.3f;
    public const float SecondFloorY = 3.4f;
    public const float AtticFloorY = 6.5f;
    // 12 * 0.24 m. A 1.8 m capsule needs the cellar floor this far below the hall
    // slab (underside y=-0.2) before the head clears the walking surface.
    public const float CellarFloorY = -2.88f;
    public const float CellarPanelX = 2.85f;
    public const float CellarPanelZ = 8.35f;
    public const float CellarHoleX = 1.55f;
    public const float CellarHoleZ = 3.3f;

    public static float CellarWellCenterX => CellarPanelX - CellarHoleX * 0.5f;
    public static float CellarHoleWest => CellarWellCenterX - CellarHoleX * 0.5f;
    public static float CellarHoleEast => CellarWellCenterX + CellarHoleX * 0.5f;
    public static float CellarHoleSouth => CellarPanelZ - CellarHoleZ * 0.5f;
    public static float CellarHoleNorth => CellarPanelZ + CellarHoleZ * 0.5f;
    public static float HallFloorMidSouthNorth => CellarHoleSouth - 1.25f;

    [MenuItem("GamesMaster/Scenes/Rebuild Entry Hall")]
    public static void Build()
    {
        GmVictorianInteriorKit.Prepare();
        Scene scene = GmSceneBuildUtility.CreateEmptyScene();
        var systems = new GameObject("SceneSystems");
        GmSceneBuildUtility.MarkScene(systems, SceneId, DisplayName);

        var environment = new GameObject("Environment");
        var gameplay = new GameObject("Gameplay");
        var lighting = new GameObject("Lighting");

        // Every object the composition plan authors a marker onto is registered here under the id
        // the plan declares. The plan builds no geometry of its own: a marker on an empty GameObject
        // has no renderer, and the composition audit rejects an element it cannot measure.
        var authored = new Dictionary<string, GameObject>(StringComparer.Ordinal);

        BuildArchitecture(environment.transform);
        BuildGameplayProps(gameplay.transform, authored);
        BuildDressing(environment.transform, authored);
        BuildLighting(lighting.transform, authored);
        BuildParlorDoorway(environment.transform, authored);
        BuildCourtDoorway(environment.transform, authored, lighting.transform);
        BuildShutTheBoxDoorway(environment.transform, authored, lighting.transform);
        BuildFoyerCrossroads(environment.transform, gameplay.transform, authored, lighting.transform);
        BuildSecondFloor(environment.transform, authored, lighting.transform);

        var composition = new GameObject("Composition");
        GmEntryHallCompositionPlan.Author(composition, authored);

        // A player, at last. This room had real, tested gameplay and nobody who could reach
        // it. Spawn is the viewpoint the ReviewCamera used to sit at -- the one vantage a
        // human already chose for this room -- so it is the least arbitrary spawn available,
        // and the review tour prefers the player's own camera when a player exists.
        // No HDRP atmosphere at all until now: every room builder had zero Volume/Exposure
        // references against the prologue's 31, so HDRP fell back to AUTOMATIC exposure and
        // opened up until a lamp-lit room rendered as a white box.
        GmInteriorAtmosphere.Apply(null, GmEntryHallBuilder.SceneId);

        GmPlayerRig.Build(null, new Vector3(0f, 0f, -6f), new Vector3(0f, 1.6f, 0f));
        systems.AddComponent<GmGameHud>();
        var design = systems.AddComponent<GmDesignRuntime>();
        design.designFile = "entry-hall-design.json";
        systems.AddComponent<GmEntryHallShotTour>();

        // This is the room the ninth bell delivers you to, so it owns the second half of that
        // crossing: sight returning, the closing card, control coming back. GmCrossing cannot own
        // it because the load that carries the player here destroys GmCrossing. Does nothing at all
        // unless the curtain is raised, so entering the hall any other way starts normally.
        systems.AddComponent<GmSceneArrival>();

        GmSceneBuildUtility.SaveScene(scene, ScenePath);
        Debug.Log("[GmEntryHall] BUILD PASS: " + ScenePath);
    }

    static void BuildArchitecture(Transform parent)
    {
        // Floor split around the under-stair well. A single 12x20 slab used to fill the
        // east-flank panel, so opening it dumped a body into stair mesh.
        var hallFloor = new GameObject("HallFloor");
        hallFloor.transform.SetParent(parent, false);
        FloorSlab(hallFloor.transform, "HallFloorWest",
            new Vector3((-6f + CellarHoleWest) * 0.5f, -0.1f, 0f),
            new Vector3(CellarHoleWest - (-6f), 0.2f, 20f));
        FloorSlab(hallFloor.transform, "HallFloorEast",
            new Vector3((CellarHoleEast + 6f) * 0.5f, -0.1f, 0f),
            new Vector3(6f - CellarHoleEast, 0.2f, 20f));
        // Stop this slab south of the well. Its north face used to sit on CellarHoleSouth at
        // y=-0.2..0, which is a wall across any 1.8 m capsule still on the cellar flight.
        FloorSlab(hallFloor.transform, "HallFloorMidSouth",
            new Vector3(CellarWellCenterX, -0.1f, (-10f + HallFloorMidSouthNorth) * 0.5f),
            new Vector3(CellarHoleX, 0.2f, HallFloorMidSouthNorth - (-10f)));

        // The runner uses five real carpet sections. Stretching one carpet to the hall's 7.5:1
        // footprint would either crush its pile flat or widen it across most of the room.
        var runner = new GameObject("CarpetRunner");
        runner.transform.SetParent(parent, false);
        for (int section = 0; section < 5; section++)
        {
            float z = -7f + section * 3.5f;
            GmOwnedPropFactory.PlacePrefab(
                "Assets/ThirdParty/MetalManVictorianInteriors/Carpet_1.fbx",
                $"HallRunner_{section + 1:00}", runner.transform,
                new Vector3(0f, 0.01f, z), new Vector3(2.4f, 0f, 3.5f),
                Quaternion.identity, ground: true, surfaceY: 0.01f,
                overrideMaterial: GmVictorianInteriorKit.Surface("carpet", "EntryHall_Runner", Vector2.one,
                    new Color(0.34f, 0.055f, 0.065f)));
        }

        // West wall, split around the barred conservatory opening near the wake vestibule so the
        // portrait run further north keeps a continuous surface.
        var leftWall = new GameObject("LeftWall");
        leftWall.transform.SetParent(parent, false);
        WallSlab(leftWall.transform, "LeftWallPortraitRun", new Vector3(-6f, 3f, 0.325f),
            new Vector3(0.3f, 6f, 15.35f));
        WallSlab(leftWall.transform, "LeftWallSouthRun", new Vector3(-6f, 3f, -9.325f),
            new Vector3(0.3f, 6f, 1.35f));
        WallSlab(leftWall.transform, "LeftWallConservatoryHeader", new Vector3(-6f, 4.05f, -8f),
            new Vector3(0.3f, 3.9f, 1.3f));
        WallSlab(leftWall.transform, "LeftWallConservatoryLintel", new Vector3(-6f, 2.28f, -8f),
            new Vector3(0.34f, 0.36f, 1.38f));
        WallSlab(leftWall.transform, "LeftWallStbHeader", new Vector3(-6f, 4.05f, ShutTheBoxDoorZ),
            new Vector3(0.3f, 3.9f, GameDoorWidth));
        WallSlab(leftWall.transform, "LeftWallStbLintel", new Vector3(-6f, 2.28f, ShutTheBoxDoorZ),
            new Vector3(0.34f, 0.36f, GameDoorWidth + 0.08f));
        WallSlab(leftWall.transform, "LeftWallNorthEnd", new Vector3(-6f, 3f, 9.65f),
            new Vector3(0.3f, 6f, 0.7f));

        // Right wall, built around the parlor opening. A single wall cube used to sit behind the
        // open leaves and transition trigger, so the route existed in code but was physically
        // impassable. Keep one architectural root for lint/audit purposes and give each solid run
        // its own collider.
        var rightWall = new GameObject("RightWall");
        rightWall.transform.SetParent(parent, false);

        GameObject rightWallSouth = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightWallSouth.name = "RightWallSouthEnd";
        rightWallSouth.transform.SetParent(rightWall.transform, false);
        rightWallSouth.transform.position = new Vector3(6f, 3f, -7.825f);
        rightWallSouth.transform.localScale = new Vector3(0.3f, 6f, 4.35f);
        GmSceneBuildUtility.ApplyVictorianSurface(rightWallSouth, "wall", 2.6f);

        GameObject rightWallMid = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightWallMid.name = "RightWallMidSouth";
        rightWallMid.transform.SetParent(rightWall.transform, false);
        rightWallMid.transform.position = new Vector3(6f, 3f, 0.225f);
        rightWallMid.transform.localScale = new Vector3(0.3f, 6f, 9.15f);
        GmSceneBuildUtility.ApplyVictorianSurface(rightWallMid, "wall", 2.6f);

        GameObject rightWallCourtHeader = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightWallCourtHeader.name = "RightWallCourtHeader";
        rightWallCourtHeader.transform.SetParent(rightWall.transform, false);
        rightWallCourtHeader.transform.position = new Vector3(6f, 4.35f, CourtDoorZ);
        rightWallCourtHeader.transform.localScale = new Vector3(0.3f, 3.3f, GameDoorWidth + 0.1f);
        GmSceneBuildUtility.ApplyVictorianSurface(rightWallCourtHeader, "wall", 2.6f);

        GameObject rightWallCourtLintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightWallCourtLintel.name = "RightWallCourtLintel";
        rightWallCourtLintel.transform.SetParent(rightWall.transform, false);
        rightWallCourtLintel.transform.position = new Vector3(6f, 2.28f, CourtDoorZ);
        rightWallCourtLintel.transform.localScale = new Vector3(0.34f, 0.36f, GameDoorWidth + 0.08f);
        GmSceneBuildUtility.ApplyVictorianSurface(rightWallCourtLintel, "wall", 2.6f);

        GameObject rightWallNorth = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightWallNorth.name = "RightWallNorthRun";
        rightWallNorth.transform.SetParent(rightWall.transform, false);
        rightWallNorth.transform.position = new Vector3(6f, 3f, 8.6f);
        rightWallNorth.transform.localScale = new Vector3(0.3f, 6f, 2.8f);
        GmSceneBuildUtility.ApplyVictorianSurface(rightWallNorth, "wall", 2.6f);

        GameObject rightWallHeader = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightWallHeader.name = "RightWallParlorHeader";
        rightWallHeader.transform.SetParent(rightWall.transform, false);
        rightWallHeader.transform.position = new Vector3(6f, 4.35f, 6f);
        rightWallHeader.transform.localScale = new Vector3(0.3f, 3.3f, 2.4f);
        GmSceneBuildUtility.ApplyVictorianSurface(rightWallHeader, "wall", 2.6f);

        // A short return beyond the opening hides the exterior HDRP sky while the destination scene
        // is still unloaded. It is deliberately deeper than the trigger so the player transitions
        // before reaching the backing wall, but the view through the doors still has floor, ceiling,
        // side reveals, and a shadowed interior plane to establish believable depth.
        var threshold = new GameObject("ParlorThresholdInterior");
        threshold.transform.SetParent(rightWall.transform, false);

        GameObject thresholdFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        thresholdFloor.name = "ParlorThresholdFloor";
        thresholdFloor.transform.SetParent(threshold.transform, false);
        thresholdFloor.transform.position = new Vector3(7.1f, -0.08f, 6f);
        thresholdFloor.transform.localScale = new Vector3(2.2f, 0.16f, 2.4f);
        GmSceneBuildUtility.ApplyVictorianSurface(thresholdFloor, "floor", 2.2f);

        GameObject thresholdCeiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
        thresholdCeiling.name = "ParlorThresholdCeiling";
        thresholdCeiling.transform.SetParent(threshold.transform, false);
        thresholdCeiling.transform.position = new Vector3(7.1f, 2.78f, 6f);
        thresholdCeiling.transform.localScale = new Vector3(2.2f, 0.16f, 2.4f);
        GmSceneBuildUtility.ApplyVictorianSurface(thresholdCeiling, "ceiling", 2.4f);

        for (int side = -1; side <= 1; side += 2)
        {
            GameObject reveal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            reveal.name = side < 0 ? "ParlorThresholdSouthReveal" : "ParlorThresholdNorthReveal";
            reveal.transform.SetParent(threshold.transform, false);
            reveal.transform.position = new Vector3(7.1f, 1.35f, 6f + side * 1.16f);
            reveal.transform.localScale = new Vector3(2.2f, 2.7f, 0.16f);
            GmSceneBuildUtility.ApplyVictorianSurface(reveal, "wall", 2.6f);
        }

        GameObject thresholdBacking = GameObject.CreatePrimitive(PrimitiveType.Cube);
        thresholdBacking.name = "ParlorThresholdBacking";
        thresholdBacking.transform.SetParent(threshold.transform, false);
        thresholdBacking.transform.position = new Vector3(8.18f, 1.35f, 6f);
        thresholdBacking.transform.localScale = new Vector3(0.16f, 2.7f, 2.4f);
        GmSceneBuildUtility.ApplyVictorianSurface(thresholdBacking, "wall", 2.6f);

        // North wall: library door west at ground, solid under the 2F arch, 2F archway at landing
        // height so the stairs can actually deliver a body onto the gallery.
        var northWall = new GameObject("NorthWall");
        northWall.transform.SetParent(parent, false);
        WallSlab(northWall.transform, "NorthWallWestJamb", new Vector3(-5.675f, 3f, 10f),
            new Vector3(0.65f, 6f, 0.3f));
        WallSlab(northWall.transform, "NorthWallLibraryHeader", new Vector3(-4.7f, 4.2f, 10f),
            new Vector3(1.3f, 3.6f, 0.3f));
        // Colliding beam stays above 2.12 m. The leaf mesh is shorter than the factory AABB, so a
        // collider-free strip closes the hall-side void without eating the 1.8 m walk.
        WallSlab(northWall.transform, "NorthWallLibraryLintel", new Vector3(-4.7f, 2.48f, 9.88f),
            new Vector3(1.52f, 0.72f, 0.42f));
        GmOwnedPropFactory.CreateRoundedProp("NorthWallLibraryLintelFill", northWall.transform,
            new Vector3(-4.7f, 1.98f, 9.90f), Quaternion.identity,
            new Vector3(1.48f, 0.28f, 0.30f), 0.02f,
            CreateMaterial("EntryHall_LibraryLintelFill", new Color(0.16f, 0.07f, 0.035f), 0.03f, 0.28f));
        WallSlab(northWall.transform, "NorthWallBetween", new Vector3(-2.675f, 3f, 10f),
            new Vector3(2.75f, 6f, 0.3f));
        WallSlab(northWall.transform, "NorthWallArchSill", new Vector3(0f, 1.7f, 10f),
            new Vector3(2.6f, 3.4f, 0.3f));
        WallSlab(northWall.transform, "NorthWallArchHeader", new Vector3(0f, 5.95f, 10f),
            new Vector3(2.6f, 0.3f, 0.3f));
        WallSlab(northWall.transform, "NorthWallEastRun", new Vector3(3.65f, 3f, 10f),
            new Vector3(4.7f, 6f, 0.3f));

        // South wall, split around the barred front doors. Threshold Refusal stays physical.
        var southWall = new GameObject("SouthWall");
        southWall.transform.SetParent(parent, false);
        WallSlab(southWall.transform, "SouthWallWestRun", new Vector3(-3.55f, 3f, -10f),
            new Vector3(4.9f, 6f, 0.3f));
        WallSlab(southWall.transform, "SouthWallEastRun", new Vector3(3.55f, 3f, -10f),
            new Vector3(4.9f, 6f, 0.3f));
        WallSlab(southWall.transform, "SouthWallFrontHeader", new Vector3(0f, 4.3f, -10f),
            new Vector3(2.2f, 3.4f, 0.3f));

        // Ceiling
        GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceiling.name = "Ceiling";
        ceiling.transform.SetParent(parent, false);
        ceiling.transform.position = new Vector3(0f, 6.1f, 0f);
        ceiling.transform.localScale = new Vector3(12f, 0.2f, 20f);
        GmSceneBuildUtility.ApplyVictorianSurface(ceiling, "ceiling", 2.6f);
    }

    static void BuildGameplayProps(Transform parent, Dictionary<string, GameObject> authored)
    {
        // Console table
        GameObject table = ImportedProp(parent, "ConsoleTable", "Table_2",
            new Vector3(0f, 0.45f, -1f), new Vector3(1.8f, 0.9f, 0.7f),
            Quaternion.identity, "table");
        authored["console-table"] = table;

        // Open ledger, fitted from the real book prefab and grounded to the measured tabletop.
        var ledger = new GameObject("GuestLedger");
        ledger.transform.SetParent(table.transform, true);
        ledger.transform.position = new Vector3(0f, 0.94f, -1f);
        GmOwnedPropFactory.PlacePrefab(
            "Assets/LeartesStudios/WitchVillage/HDRP/Art/Prefabs/SM_Book_2.prefab",
            "GuestLedger", ledger.transform, ledger.transform.position,
            new Vector3(0.50f, 0.09f, 0.38f), Quaternion.Euler(90f, 0f, 0f),
            ground: true, surfaceY: 0.90f);
        var ledgerCollider = ledger.AddComponent<BoxCollider>();
        ledgerCollider.size = new Vector3(0.50f, 0.09f, 0.38f);
        authored["ledger-book"] = ledger;

        // No owned pack contains a quill. This is an authored feather silhouette paired with a real
        // glass bottle as the inkwell, not a stretched cube pretending to be both objects.
        var quill = new GameObject("LedgerQuill");
        quill.transform.SetParent(table.transform, true);
        quill.transform.position = new Vector3(0.48f, 0.91f, -0.96f);
        GmOwnedPropFactory.CreateQuill("AuthoredQuill", quill.transform,
            new Vector3(0.48f, 0.925f, -0.94f), Quaternion.Euler(90f, 18f, 0f),
            new Vector3(0.42f, 0.42f, 0.42f),
            CreateMaterial("EntryHall_Quill", new Color(0.63f, 0.56f, 0.45f), 0f, 0.24f));
        GmOwnedPropFactory.PlacePrefab(
            "Assets/LeartesStudios/WitchVillage/HDRP/Art/Prefabs/SM_Bottles_4.prefab",
            "LedgerInkwell", quill.transform, new Vector3(0.34f, 0.95f, -0.90f),
            new Vector3(0.10f, 0.12f, 0.10f), Quaternion.identity,
            ground: true, surfaceY: 0.90f);
        authored["ledger-quill"] = quill;

        // Portrait gallery on left wall (9 portraits)
        var gallery = new GameObject("PortraitGallery");
        gallery.transform.SetParent(parent, false);
        gallery.transform.position = Vector3.zero;

        Material brass = CreateMaterial("EntryHall_Nameplate", new Color(0.48f, 0.31f, 0.10f), 0.72f, 0.42f);

        for (int i = 0; i < DebtorNames.Length; i++)
        {
            float zOffset = -6f + i * 1.5f;
            GameObject portrait = HangDebtorPortrait(gallery.transform, i, DebtorNames[i], DebtorSlugs[i],
                new Vector3(-5.78f, 2.2f, zOffset), Quaternion.Euler(0f, -90f, 0f),
                new Vector3(-5.715f, 2.2f, zOffset), new Vector3(-5.70f, 1.53f, zOffset), brass);
            // The composition plan needs a real renderer to attach its markers to, not a proxy:
            // Marr's and Percival's are the two portraits the plan names directly.
            if (i == 0) authored["marr-frame"] = portrait;
            if (i == DebtorNames.Length - 1) authored["percival-frame"] = portrait;
        }

        // Shard #1 is a triangular prism with a mirror material, visible behind Percival's loose edge.
        GameObject shard = GmOwnedPropFactory.CreateMirrorShard("MirrorShard_1", gallery.transform,
            new Vector3(-5.64f, 1.93f, 6.06f), Quaternion.Euler(0f, -90f, 16f),
            new Vector3(0.34f, 0.46f, 0.055f),
            CreateMaterial("EntryHall_MirrorShard", new Color(0.46f, 0.62f, 0.68f), 0.86f, 0.92f));
        shard.AddComponent<BoxCollider>().size = new Vector3(1f, 1f, 1f);
        authored["shard-one"] = shard;

        // Two full-depth measured flights create the broad silhouette the room calls "grand". The
        // previous single asset was fitted into a 3m depth box; uniform scaling then crushed its
        // 2.74m native width to roughly 1.3m, which is why the reviewed stair read as a black slot.
        var stairs = new GameObject("GrandStaircase");
        stairs.transform.SetParent(parent, false);
        stairs.transform.position = new Vector3(0f, 0f, 8.4f);
        GmVictorianInteriorKit.PlaceGrounded("Stair", "GrandStairLeft", stairs.transform,
            new Vector3(-1.15f, 0f, 8.4f), new Vector3(2.2f, 3.4f, 5.0f),
            Quaternion.Euler(0f, -3f, 0f), "stair", surfaceY: 0f);
        // East flight stays under the treads. The old 2.2 m AABB reached x=2.25 and
        // filled the cellar well behind the panel.
        GmVictorianInteriorKit.PlaceGrounded("Stair", "GrandStairRight", stairs.transform,
            new Vector3(0.50f, 0f, 8.4f), new Vector3(1.4f, 3.4f, 5.0f),
            Quaternion.Euler(0f, 3f, 0f), "stair", surfaceY: 0f);
        BuildWalkableTreads(stairs.transform);
        authored["grand-staircase"] = stairs;
    }

    // The wake-cluster and stair-cluster composition elements remain separate logical roots because
    // the composition plan measures those roots, but every visible child comes from owned art.
    static void BuildDressing(Transform parent, Dictionary<string, GameObject> authored)
    {
        // Low settee where the player regains footing after the porch knockout.
        GameObject settee = ImportedProp(parent, "WakeSettee", "Couch_2",
            new Vector3(0f, 0.25f, -8f), new Vector3(1.6f, 0.5f, 0.6f),
            Quaternion.identity, "couch", new Color(0.30f, 0.05f, 0.10f));
        authored["wake-settee"] = settee;

        // Small marble table beside the settee -- the wake-cluster's own purpose text already
        // promised one; it just never had geometry until now.
        GameObject wakeTable = ImportedProp(parent, "WakeTable", "Table_3",
            new Vector3(1.3f, 0.25f, -8f), new Vector3(0.5f, 0.5f, 0.5f),
            Quaternion.identity, "table", new Color(0.75f, 0.74f, 0.72f));
        authored["wake-table"] = wakeTable;

        // Carved newel post at the base of the grand staircase.
        var newel = new GameObject("StairNewel");
        newel.transform.SetParent(parent, false);
        newel.transform.position = new Vector3(2.0f, 0f, 6.0f);
        GmOwnedPropFactory.PlacePrefab(
            "Assets/LeartesStudios/WitchVillage/HDRP/Art/Meshes/Architecture/SculptedPost/SM_SculptedPost.fbx",
            "StairNewel", newel.transform, new Vector3(2.0f, 0f, 6.0f),
            new Vector3(0.38f, 0.95f, 0.38f), Quaternion.identity,
            ground: true, surfaceY: 0f,
            overrideMaterial: CreateMaterial("EntryHall_NewelWood",
                new Color(0.17f, 0.07f, 0.03f), 0.03f, 0.36f));
        authored["stair-newel"] = newel;

        // The central runner remains the circulation spine. Dress the two long perimeter bands so
        // the hall reads as an occupied estate interior instead of a table and stair on an empty set.
        // Every imported object is fitted from its renderer bounds; none of these coordinates assume
        // the source FBX pivot or native scale.
        ImportedProp(parent, "EastConsole", "Table_2",
            new Vector3(5.28f, 0.44f, -3.65f), new Vector3(0.70f, 0.88f, 1.75f),
            Quaternion.Euler(0f, 90f, 0f), "table", new Color(0.25f, 0.12f, 0.065f));

        var eastMirror = new GameObject("EastHallMirror");
        eastMirror.transform.SetParent(parent, false);
        eastMirror.transform.position = new Vector3(5.72f, 2.25f, -3.65f);
        GmVictorianInteriorKit.Place("Mirror_1", "EastHallMirror", eastMirror.transform,
            eastMirror.transform.position, new Vector3(0.14f, 1.55f, 1.02f),
            Quaternion.Euler(0f, -90f, 0f), "mirror");

        ImportedProp(parent, "GalleryChairSouth", "Chair_1",
            new Vector3(-4.78f, 0.52f, -4.65f), new Vector3(0.78f, 1.04f, 0.78f),
            Quaternion.Euler(0f, 82f, 0f), "chair", new Color(0.30f, 0.075f, 0.085f));
        ImportedProp(parent, "GalleryChairNorth", "Chair_2",
            new Vector3(-4.78f, 0.58f, 3.95f), new Vector3(0.82f, 1.16f, 0.82f),
            Quaternion.Euler(0f, 98f, 0f), "chair", new Color(0.20f, 0.055f, 0.065f));

        ImportedProp(parent, "StairSideTable", "Table_3",
            new Vector3(-4.75f, 0.36f, 7.65f), new Vector3(0.72f, 0.72f, 0.72f),
            Quaternion.identity, "table", new Color(0.28f, 0.14f, 0.07f));

        var landingClock = new GameObject("LandingClock");
        landingClock.transform.SetParent(parent, false);
        landingClock.transform.position = new Vector3(4.72f, 0f, 7.85f);
        GmOwnedPropFactory.PlacePrefab("Assets/GamesMaster/Props/Vintage_Grantfather_Clock.fbx",
            "LandingClock", landingClock.transform, new Vector3(4.72f, 1.15f, 7.85f),
            new Vector3(0.88f, 2.30f, 0.72f), Quaternion.Euler(90f, -18f, 0f),
            ground: true, surfaceY: 0f,
            overrideMaterial: CreateMaterial("EntryHall_LandingClockWood",
                new Color(0.12f, 0.07f, 0.04f), 0.03f, 0.40f));
        Renderer[] clockRenderers = landingClock.GetComponentsInChildren<Renderer>(true);
        if (clockRenderers.Length > 0)
        {
            Bounds clockBounds = clockRenderers[0].bounds;
            for (int i = 1; i < clockRenderers.Length; i++) clockBounds.Encapsulate(clockRenderers[i].bounds);
            var clockBody = landingClock.AddComponent<BoxCollider>();
            clockBody.center = landingClock.transform.InverseTransformPoint(clockBounds.center);
            clockBody.size = clockBounds.size;
        }
    }

    static void BuildLighting(Transform parent, Dictionary<string, GameObject> authored)
    {
        // Chandelier fixture (the visible iron frame the light hangs from) + its light. Composition
        // markers measure elements through their renderers, so the light alone (no renderer) can
        // never stand in for the "hall-chandelier" element the plan describes.
        var chandelierFixture = new GameObject("ChandelierFixture");
        chandelierFixture.transform.SetParent(parent, false);
        chandelierFixture.transform.position = new Vector3(0f, 4.8f, 4.5f);
        GmVictorianInteriorKit.Place("Lamp_1_LOD0", "HallChandelier", chandelierFixture.transform,
            chandelierFixture.transform.position, new Vector3(1.15f, 0.92f, 1.15f),
            Quaternion.identity, "lamp");
        authored["hall-chandelier"] = chandelierFixture;

        GameObject chandelierObj = new GameObject("ChandelierLight");
        chandelierObj.transform.SetParent(parent, false);
        chandelierObj.transform.position = new Vector3(0f, 4.8f, 4.5f);
        Light chandelier = chandelierObj.AddComponent<Light>();
        chandelier.type = LightType.Point;
        chandelier.range = 14f;
        chandelier.color = new Color(1.0f, 0.88f, 0.72f);
        chandelier.lightUnit = LightUnit.Candela;
        chandelier.intensity = 800f;
        HDAdditionalLightData chandelierHd = chandelierObj.AddComponent<HDAdditionalLightData>();
        chandelierHd.lightUnit = LightUnit.Candela;
        chandelierHd.intensity = 800f;
        authored["chandelier-light"] = chandelierObj;

        // Ledger lamp fixture (green glass shade) + light
        var ledgerLampFixture = new GameObject("LedgerLampFixture");
        ledgerLampFixture.transform.SetParent(parent, false);
        ledgerLampFixture.transform.position = new Vector3(0.4f, 0.9f, -1f);
        GmVictorianInteriorKit.PlaceGrounded("Lamp_2", "LedgerBankerLamp", ledgerLampFixture.transform,
            new Vector3(0.4f, 0.9f, -1f), new Vector3(0.32f, 0.46f, 0.34f),
            Quaternion.identity, "lamp", surfaceY: 0.9f,
            tintOverride: new Color(0.16f, 0.42f, 0.22f));
        authored["ledger-lamp"] = ledgerLampFixture;

        GameObject ledgerLampObj = new GameObject("LedgerLampLight");
        ledgerLampObj.transform.SetParent(parent, false);
        ledgerLampObj.transform.position = new Vector3(0f, 1.3f, -1f);
        Light ledgerLamp = ledgerLampObj.AddComponent<Light>();
        ledgerLamp.type = LightType.Point;
        ledgerLamp.range = 3.5f;
        ledgerLamp.color = new Color(0.9f, 0.95f, 0.8f);
        ledgerLamp.intensity = 150f;
        ledgerLampObj.AddComponent<HDAdditionalLightData>();
        authored["ledger-lamp-light"] = ledgerLampObj;

        // Wake vestibule sconce -- mesh only. No Light was ever built for it, and inventing one here
        // means inventing a brightness nobody has authored; the visible fixture is enough to satisfy
        // the composition audit's renderer check for the "wake-lamp" element.
        // Positioned beside the settee, not flush on the south wall: the wake-cluster's own purpose
        // text promises a sconce over the settee ("beneath a dying gas wall sconce"), and a fixture
        // sitting 1.85m further south at the room's very boundary can never share a frame with the
        // settee from any camera that also looks north into the hall -- it is always behind such a
        // camera by construction. This is the same "plan and builder drifted apart" class the wake
        // settee/table/newel needed above, just on a lit fixture instead of a bare id.
        var wakeLampFixture = new GameObject("WakeLampFixture");
        wakeLampFixture.transform.SetParent(parent, false);
        wakeLampFixture.transform.position = new Vector3(-1.0f, 1.72f, -8.1f);
        GmVictorianInteriorKit.Place("Lamp_2", "WakeGasSconce", wakeLampFixture.transform,
            wakeLampFixture.transform.position, new Vector3(0.34f, 0.56f, 0.38f),
            Quaternion.Euler(0f, 180f, 0f), "lamp");
        authored["wake-lamp"] = wakeLampFixture;
        authored["wake-lamp-light"] = CreatePointLight(parent, "WakeSconceLight",
            new Vector3(-1.0f, 1.72f, -7.85f), 4.5f, 35f,
            new Color(1.0f, 0.64f, 0.30f));

        // Gallery wall sconces: fixtures + lights
        var sconce1Fixture = new GameObject("GallerySconceFixture_1");
        sconce1Fixture.transform.SetParent(parent, false);
        sconce1Fixture.transform.position = new Vector3(-5.72f, 2.5f, -3f);
        GmVictorianInteriorKit.Place("Lamp_2", "GallerySconceSouth", sconce1Fixture.transform,
            sconce1Fixture.transform.position, new Vector3(0.28f, 0.48f, 0.34f),
            Quaternion.Euler(0f, 90f, 0f), "lamp");
        authored["gallery-sconce-1"] = sconce1Fixture;

        GameObject sconce1 = new GameObject("GallerySconce_1");
        sconce1.transform.SetParent(parent, false);
        sconce1.transform.position = new Vector3(-5.2f, 2.5f, -3f);
        Light sc1 = sconce1.AddComponent<Light>();
        sc1.type = LightType.Point;
        sc1.range = 5f;
        sc1.color = new Color(1.0f, 0.82f, 0.6f);
        sc1.intensity = 200f;
        sconce1.AddComponent<HDAdditionalLightData>();
        authored["gallery-sconce-1-light"] = sconce1;

        var sconce2Fixture = new GameObject("GallerySconceFixture_2");
        sconce2Fixture.transform.SetParent(parent, false);
        sconce2Fixture.transform.position = new Vector3(-5.72f, 2.5f, 5.1f);
        GmVictorianInteriorKit.Place("Lamp_2", "GallerySconceNorth", sconce2Fixture.transform,
            sconce2Fixture.transform.position, new Vector3(0.28f, 0.48f, 0.34f),
            Quaternion.Euler(0f, 90f, 0f), "lamp");
        authored["gallery-sconce-2"] = sconce2Fixture;

        GameObject sconce2 = new GameObject("GallerySconce_2");
        sconce2.transform.SetParent(parent, false);
        sconce2.transform.position = new Vector3(-5.2f, 2.5f, 5.1f);
        Light sc2 = sconce2.AddComponent<Light>();
        sc2.type = LightType.Point;
        sc2.range = 5f;
        sc2.color = new Color(1.0f, 0.82f, 0.6f);
        sc2.intensity = 200f;
        sconce2.AddComponent<HDAdditionalLightData>();
        authored["gallery-sconce-2-light"] = sconce2;

        // The north end was previously lit only from the chandelier at z=0, leaving the stair and
        // the newly built doorway as silhouettes. These are visible east-wall gas fixtures, each
        // paired with the local practical it motivates in the composition plan.
        var stairFixture = new GameObject("StairSconceFixture");
        stairFixture.transform.SetParent(parent, false);
        stairFixture.transform.position = new Vector3(5.72f, 2.55f, 7.45f);
        GmVictorianInteriorKit.Place("Lamp_2", "StairGasSconce", stairFixture.transform,
            stairFixture.transform.position, new Vector3(0.30f, 0.50f, 0.36f),
            Quaternion.Euler(0f, -90f, 0f), "lamp");
        authored["stair-sconce"] = stairFixture;
        GameObject stairSconceLight = CreatePointLight(parent, "StairSconceLight",
            new Vector3(4.30f, 2.55f, 7.20f), 7f, 35f,
            new Color(1.0f, 0.72f, 0.44f));
        HDAdditionalLightData stairHd = stairSconceLight.GetComponent<HDAdditionalLightData>();
        stairSconceLight.GetComponent<Light>().lightUnit = LightUnit.Candela;
        stairHd.lightUnit = LightUnit.Candela;
        stairHd.intensity = 35f;
        authored["stair-sconce-light"] = stairSconceLight;

        var stairWestFixture = new GameObject("StairWestSconceFixture");
        stairWestFixture.transform.SetParent(parent, false);
        stairWestFixture.transform.position = new Vector3(-5.72f, 2.55f, 7.45f);
        GmVictorianInteriorKit.Place("Lamp_2", "StairWestGasSconce", stairWestFixture.transform,
            stairWestFixture.transform.position, new Vector3(0.30f, 0.50f, 0.36f),
            Quaternion.Euler(0f, 90f, 0f), "lamp");
        authored["stair-west-sconce"] = stairWestFixture;
        GameObject stairWestLight = CreatePointLight(parent, "StairWestSconceLight",
            new Vector3(-4.30f, 2.55f, 7.20f), 7f, 35f,
            new Color(1.0f, 0.72f, 0.44f));
        stairWestLight.GetComponent<Light>().lightUnit = LightUnit.Candela;
        HDAdditionalLightData stairWestHd = stairWestLight.GetComponent<HDAdditionalLightData>();
        stairWestHd.lightUnit = LightUnit.Candela;
        stairWestHd.intensity = 35f;
        authored["stair-west-sconce-light"] = stairWestLight;

        var doorFixture = new GameObject("ParlorDoorSconceFixture");
        doorFixture.transform.SetParent(parent, false);
        doorFixture.transform.position = new Vector3(5.72f, 2.40f, 4.65f);
        GmVictorianInteriorKit.Place("Lamp_2", "ParlorDoorGasSconce", doorFixture.transform,
            doorFixture.transform.position, new Vector3(0.28f, 0.47f, 0.34f),
            Quaternion.Euler(0f, -90f, 0f), "lamp");
        authored["parlor-door-sconce"] = doorFixture;
        authored["parlor-door-sconce-light"] = CreatePointLight(parent, "ParlorDoorSconceLight",
            new Vector3(5.38f, 2.40f, 4.65f), 5f, 35f,
            new Color(1.0f, 0.70f, 0.42f));

        GmInteriorMoonWindow.Result westMoon = GmInteriorMoonWindow.Build(parent,
            "NorthMoonWindowWest", new Vector3(-3.45f, 4.15f, 9.82f),
            new Vector3(-3.8f, 1.15f, 5.5f), new Vector2(1.45f, 2.15f), 190f);
        authored["north-moon-window-west"] = westMoon.fixture;
        authored["north-moon-light-west"] = westMoon.light;
        GmInteriorMoonWindow.Result eastMoon = GmInteriorMoonWindow.Build(parent,
            "NorthMoonWindowEast", new Vector3(3.45f, 4.15f, 9.82f),
            new Vector3(3.8f, 1.15f, 7.0f), new Vector2(1.45f, 2.15f), 190f);
        authored["north-moon-window-east"] = eastMoon.fixture;
        authored["north-moon-light-east"] = eastMoon.light;
    }

    static GameObject CreatePointLight(Transform parent, string name, Vector3 position,
        float range, float lumens, Color color)
    {
        var lightObject = new GameObject(name);
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.position = position;
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = range;
        light.color = color;
        light.intensity = lumens;
        HDAdditionalLightData hd = lightObject.AddComponent<HDAdditionalLightData>();
        hd.lightUnit = LightUnit.Lumen;
        hd.intensity = lumens;
        hd.range = range;
        return lightObject;
    }

    static void ApplyMaterial(GameObject go, string shaderName, Color color, float metallic, float smoothness)
    {
        Renderer r = go.GetComponent<Renderer>();
        if (r == null) return;
        r.sharedMaterial = CreateMaterial(go.name + "_Material", color, metallic, smoothness, shaderName);
    }

    static Material CreateMaterial(string name, Color color, float metallic, float smoothness,
        string shaderName = "HDRP/Lit")
    {
        Shader shader = Shader.Find(shaderName) ?? Shader.Find("HDRP/Lit");
        if (shader == null)
            throw new InvalidOperationException("[GmEntryHall] HDRP/Lit shader is unavailable");
        var material = new Material(shader) { name = name };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (!HDShaderUtils.ResetMaterialKeywords(material))
            Debug.LogWarning($"[GmEntryHall] HDRP keyword reset rejected '{name}'");
        return material;
    }

    static void AddWorldText(string objectName, string text, Transform parent,
        Vector3 localPosition, Quaternion localRotation, float characterSize)
    {
        var label = new GameObject(objectName);
        label.transform.SetParent(parent, false);
        label.transform.localPosition = localPosition;
        label.transform.localRotation = localRotation;
        TextMesh mesh = label.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.characterSize = characterSize;
        mesh.fontSize = 64;
        mesh.color = new Color(0.08f, 0.045f, 0.02f);
        mesh.richText = false;
    }

    static GameObject ImportedProp(Transform parent, string name, string modelName,
        Vector3 targetCenter, Vector3 targetSize, Quaternion rotation, string materialFamily,
        Color? tintOverride = null, float surfaceY = 0f)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, true);
        root.transform.position = targetCenter;

        var collider = root.AddComponent<BoxCollider>();
        collider.size = targetSize;

        GmVictorianInteriorKit.PlaceGrounded(modelName, name, root.transform, targetCenter, targetSize,
            rotation, materialFamily, surfaceY: surfaceY, tintOverride: tintOverride);
        return root;
    }

    /// The way to the table.
    ///
    /// Canon puts this on the east wall at the north end, and the geometry is the argument: the nine
    /// portraits hang on the WEST wall and the staircase closes the north, so walking the hall means
    /// passing the nine who came before on your left before you turn right to the table. The front
    /// doors stay behind you and stay shut -- Threshold Refusal is not undone by being indoors.
    ///
    /// A walk-through volume rather than an examine prompt because this room has no interaction
    /// stack at all yet: no GmInteractable, no GmDesignRuntime. Inventing one here to hang a single
    /// transition off would be room content smuggled in as plumbing. The player still chooses when
    /// to leave, which is the part that matters.
    static void BuildParlorDoorway(Transform parent, Dictionary<string, GameObject> authored)
    {
        const float WallX = 6f;          // east wall centre
        const float DoorZ = 6f;          // north end, past the portrait run
        const float Opening = 2.2f;

        var doorway = new GameObject("ParlorDoorway");
        doorway.transform.SetParent(parent, false);
        authored["parlor-doorway"] = doorway;

        Material doorWood = CreateMaterial("EntryHall_ParlorDoorWood",
            new Color(0.34f, 0.15f, 0.065f), 0.03f, 0.34f);

        // Two measured carved frames articulate the double opening. The old single stretched cube
        // read as a construction beam in the review frame and carried no period detail at all.
        var lintel = new GameObject("ParlorDoorLintel");
        lintel.transform.SetParent(doorway.transform, false);
        for (int side = -1; side <= 1; side += 2)
        {
            GmOwnedPropFactory.PlacePrefab(
                "Assets/LeartesStudios/HauntedVillage/Art/Prefabs/SM_DoorFrame.prefab",
                side < 0 ? "ParlorDoorFrameSouth" : "ParlorDoorFrameNorth", lintel.transform,
                new Vector3(WallX - 0.12f, 1.1f, DoorZ + side * 0.58f),
                new Vector3(0.18f, 2.35f, 1.28f), Quaternion.identity,
                ground: true, surfaceY: 0f, overrideMaterial: doorWood);
        }
        authored["parlor-door-lintel"] = lintel;

        // Both leaves, standing open. The house does not open its front doors; it has no reservation
        // whatsoever about the ones that lead further in, and an open door is an invitation the
        // player can decline by not walking through it.
        for (int side = -1; side <= 1; side += 2)
        {
            string leafName = side < 0 ? "ParlorDoorLeafSouth" : "ParlorDoorLeafNorth";
            var leaf = new GameObject(leafName);
            leaf.transform.SetParent(doorway.transform, false);
            // Swing each centre around its outer jamb. Translating two barely rotated slabs away
            // from the opening made the near leaf fill the review frame and hid the threshold.
            // The hinge calculation keeps the clear opening centred while both leaves fold into
            // the hall by the same amount.
            float swingAngle = side * 65f;
            Vector3 hinge = new Vector3(WallX - 0.05f, 1.10f,
                DoorZ + side * Opening * 0.5f);
            Vector3 closedCenterFromHinge = new Vector3(0f, 0f, -side * Opening * 0.25f);
            Vector3 center = hinge + Quaternion.Euler(0f, swingAngle, 0f) * closedCenterFromHinge;
            leaf.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, swingAngle, 0f));
            GmOwnedPropFactory.PlacePrefab(
                side < 0
                    ? "Assets/LeartesStudios/HauntedVillage/Art/Prefabs/SM_Door_01.prefab"
                    : "Assets/LeartesStudios/HauntedVillage/Art/Prefabs/SM_Door_02.prefab",
                leafName, leaf.transform, center, new Vector3(0f, 2.2f, 1.17f),
                Quaternion.Euler(0f, swingAngle, 0f), ground: true, surfaceY: 0f,
                overrideMaterial: doorWood);
            BoxCollider collider = leaf.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.12f, 2.2f, 1.17f);
        }

        // The volume itself, set INSIDE the opening rather than across the hall, so brushing past the
        // doorway on the way to the staircase does not take the player out of the room.
        var volume = new GameObject("ParlorTransition");
        volume.transform.SetParent(doorway.transform, false);
        volume.transform.position = new Vector3(WallX - 0.15f, 1.2f, DoorZ);
        var box = volume.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(0.6f, 2.4f, Opening);

        var trigger = volume.AddComponent<GmSceneTransitionTrigger>();
        trigger.TargetSceneId = GmParlorBuilder.SceneId;
        trigger.TargetScenePath = GmParlorBuilder.ScenePath;
        trigger.InteractionPrompt = "Through the double doors, to the table";

        var parlorDoor = doorway.AddComponent<GmEstateDoor>();
        parlorDoor.Configure("door_parlor", "Parlor Doors", "", "",
            locked: false, barred: false, openAng: 0f, leaf: doorway.transform,
            secret: false, openAtStart: true);
        parlorDoor.EnsureBarrier(new Vector3(0.2f, 2.4f, Opening));
    }

    static void BuildCourtDoorway(Transform environment, Dictionary<string, GameObject> authored,
        Transform lighting)
    {
        // Dining-wing hearing door. Canon puts Court off the manor, not only at the Parlor
        // match exit. The leaf stays shut until parlor is complete so a body cannot skip
        // the first table. Loading Court.unity is reachability, not trial content.
        Material doorWood = CreateMaterial("EntryHall_CourtDoorWood",
            new Color(0.34f, 0.15f, 0.065f), 0.03f, 0.34f);
        Transform wall = GameObject.Find("RightWall").transform;
        GmEstateDoor door = GmEstateDoorFactory.Place(environment, "CourtDoor",
            new Vector3(6f, 1.2f, CourtDoorZ), GameDoorWidth, 2.4f, GmEstateDoorFacing.East,
            CourtDoorId, "Hearing Door", locked: true, barred: false, secret: false,
            wood: doorWood, requiredCompletedRoom: GmParlorBuilder.SceneId,
            lockedUntilMessage: "The hearing has not been called. Sit at the table first.");
        authored["court-door"] = door.gameObject;

        BuildWallThreshold(wall, "Court", 6f, CourtDoorZ, GameDoorWidth, intoPositiveX: true);

        var volume = new GameObject("CourtTransition");
        volume.transform.SetParent(door.transform, false);
        volume.transform.position = new Vector3(6.2f, 1.2f, CourtDoorZ);
        var box = volume.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(0.55f, 2.3f, GameDoorWidth);
        var trigger = volume.AddComponent<GmSceneTransitionTrigger>();
        trigger.TargetSceneId = GmCourtBuilder.SceneId;
        trigger.TargetScenePath = GmCourtBuilder.ScenePath;
        trigger.RequiredCompletedRoomId = GmParlorBuilder.SceneId;
        trigger.InteractionPrompt = "The hearing is waiting";
        trigger.UseCurtain = true;

        var sconce = new GameObject("CourtDoorSconceFixture");
        sconce.transform.SetParent(lighting, false);
        sconce.transform.position = new Vector3(5.72f, 2.40f, CourtDoorZ);
        GmVictorianInteriorKit.Place("Lamp_2", "CourtDoorGasSconce", sconce.transform,
            sconce.transform.position, new Vector3(0.28f, 0.47f, 0.34f),
            Quaternion.Euler(0f, -90f, 0f), "lamp");
        authored["court-door-sconce"] = sconce;
        authored["court-door-sconce-light"] = CreatePointLight(lighting, "CourtDoorSconceLight",
            new Vector3(5.38f, 2.40f, CourtDoorZ), 7.5f, 58f, new Color(1.0f, 0.72f, 0.44f));
        authored["court-door-fill"] = CreatePointLight(lighting, "CourtDoorFill",
            new Vector3(5.15f, 1.62f, CourtDoorZ), 4.2f, 36f, new Color(1.0f, 0.68f, 0.40f));
    }

    static void BuildShutTheBoxDoorway(Transform environment, Dictionary<string, GameObject> authored,
        Transform lighting)
    {
        // Quieter hall, later in the chain. Locked until Court is complete. Conservatory
        // stays barred on purpose and is not this door.
        Material doorWood = CreateMaterial("EntryHall_StbDoorWood",
            new Color(0.34f, 0.15f, 0.065f), 0.03f, 0.34f);
        Transform wall = GameObject.Find("LeftWall").transform;
        GmEstateDoor door = GmEstateDoorFactory.Place(environment, "ShutTheBoxDoor",
            new Vector3(-6f, 1.2f, ShutTheBoxDoorZ), GameDoorWidth, 2.4f, GmEstateDoorFacing.West,
            ShutTheBoxDoorId, "Quieter Hall", locked: true, barred: false, secret: false,
            wood: doorWood, requiredCompletedRoom: GmCourtBuilder.SceneId,
            lockedUntilMessage: "The quieter hall is not yet offered.");
        authored["stb-door"] = door.gameObject;

        BuildWallThreshold(wall, "ShutTheBox", -6f, ShutTheBoxDoorZ, GameDoorWidth, intoPositiveX: false);

        var volume = new GameObject("ShutTheBoxTransition");
        volume.transform.SetParent(door.transform, false);
        volume.transform.position = new Vector3(-6.2f, 1.2f, ShutTheBoxDoorZ);
        var box = volume.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(0.55f, 2.3f, GameDoorWidth);
        var trigger = volume.AddComponent<GmSceneTransitionTrigger>();
        trigger.TargetSceneId = GmShutTheBoxBuilder.SceneId;
        trigger.TargetScenePath = GmShutTheBoxBuilder.ScenePath;
        trigger.RequiredCompletedRoomId = GmCourtBuilder.SceneId;
        trigger.InteractionPrompt = "Into the quieter hall";
        trigger.UseCurtain = true;

        var sconce = new GameObject("ShutTheBoxDoorSconceFixture");
        sconce.transform.SetParent(lighting, false);
        sconce.transform.position = new Vector3(-5.72f, 2.40f, ShutTheBoxDoorZ);
        GmVictorianInteriorKit.Place("Lamp_2", "ShutTheBoxDoorGasSconce", sconce.transform,
            sconce.transform.position, new Vector3(0.28f, 0.47f, 0.34f),
            Quaternion.Euler(0f, 90f, 0f), "lamp");
        authored["stb-door-sconce"] = sconce;
        authored["stb-door-sconce-light"] = CreatePointLight(lighting, "ShutTheBoxDoorSconceLight",
            new Vector3(-5.38f, 2.40f, ShutTheBoxDoorZ), 5f, 32f, new Color(1.0f, 0.70f, 0.42f));
    }

    static void BuildWallThreshold(Transform wall, string prefix, float wallX, float doorZ,
        float opening, bool intoPositiveX)
    {
        float dir = intoPositiveX ? 1f : -1f;
        float midX = wallX + dir * 1.1f;
        float backX = wallX + dir * 2.18f;
        var threshold = new GameObject(prefix + "ThresholdInterior");
        threshold.transform.SetParent(wall, false);
        FloorSlab(threshold.transform, prefix + "ThresholdFloor",
            new Vector3(midX, -0.08f, doorZ), new Vector3(2.2f, 0.16f, opening + 0.2f));
        CeilingSlab(threshold.transform, prefix + "ThresholdCeiling",
            new Vector3(midX, 2.78f, doorZ), new Vector3(2.2f, 0.16f, opening + 0.2f));
        for (int side = -1; side <= 1; side += 2)
        {
            WallSlab(threshold.transform,
                prefix + (side < 0 ? "ThresholdSouthReveal" : "ThresholdNorthReveal"),
                new Vector3(midX, 1.35f, doorZ + side * (opening * 0.5f + 0.16f)),
                new Vector3(2.2f, 2.7f, 0.16f));
        }
        WallSlab(threshold.transform, prefix + "ThresholdBacking",
            new Vector3(backX, 1.35f, doorZ), new Vector3(0.16f, 2.7f, opening + 0.2f));
    }

    static GameObject WallSlab(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        slab.name = name;
        slab.transform.SetParent(parent, false);
        slab.transform.position = position;
        slab.transform.localScale = scale;
        GmSceneBuildUtility.ApplyVictorianSurface(slab, "wall", 2.6f);
        return slab;
    }

    static GameObject FloorSlab(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        slab.name = name;
        slab.transform.SetParent(parent, false);
        slab.transform.position = position;
        slab.transform.localScale = scale;
        GmSceneBuildUtility.ApplyVictorianSurface(slab, "floor", 2.2f);
        return slab;
    }

    static GameObject CeilingSlab(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        slab.name = name;
        slab.transform.SetParent(parent, false);
        slab.transform.position = position;
        slab.transform.localScale = scale;
        GmSceneBuildUtility.ApplyVictorianSurface(slab, "ceiling", 2.6f);
        return slab;
    }

    static void AddBarrierCollider(Transform parent, string name, Vector3 position, Vector3 size)
    {
        var holder = new GameObject(name);
        holder.transform.SetParent(parent, false);
        holder.transform.position = position;
        var box = holder.AddComponent<BoxCollider>();
        box.size = size;
    }

    static void BuildWalkableTreads(Transform stairs)
    {
        const int Steps = 14;
        const float Rise = 0.24f;
        const float Run = 0.24f;
        const float Width = 2.2f;
        const float StartZ = 6.4f;
        var treads = new GameObject("StairTreads");
        treads.transform.SetParent(stairs, false);
        for (int i = 0; i < Steps; i++)
        {
            float top = (i + 1) * Rise;
            AddBarrierCollider(treads.transform, $"StairTread_{i + 1:00}",
                new Vector3(0f, top - 0.04f, StartZ + i * Run),
                new Vector3(Width, 0.08f, Run + 0.04f));
        }

        AddBarrierCollider(treads.transform, "StairStringerWest",
            new Vector3(-Width * 0.5f - 0.06f, 1.7f, StartZ + (Steps * Run) * 0.5f),
            new Vector3(0.12f, 3.5f, Steps * Run + 0.4f));
        AddBarrierCollider(treads.transform, "StairStringerEast",
            new Vector3(Width * 0.5f + 0.06f, 1.7f, StartZ + (Steps * Run) * 0.5f),
            new Vector3(0.12f, 3.5f, Steps * Run + 0.4f));

        // Hall-side landing against the north wall, top flush with SecondFloorY (14 * 0.24 = 3.36).
        // Parent it beside the staircase, not under it: the composition audit measures
        // grand-staircase through every renderer underneath, and a 2F slab in that AABB
        // makes the climb shot read as a full-frame wall.
        FloorSlab(stairs.parent, "StairLanding", new Vector3(0f, SecondFloorY - 0.1f, 9.95f),
            new Vector3(4.8f, 0.2f, 1.15f));
    }

    static void BuildFoyerCrossroads(Transform environment, Transform gameplay,
        Dictionary<string, GameObject> authored, Transform lighting)
    {
        Material doorWood = CreateMaterial("EntryHall_EstateDoorWood",
            new Color(0.34f, 0.15f, 0.065f), 0.03f, 0.34f);

        GmEstateDoor front = GmEstateDoorFactory.Place(environment, "FrontDoors",
            new Vector3(0f, 1.3f, -10f), 2.2f, 2.6f, GmEstateDoorFacing.South,
            FrontDoorId, "Front Doors", locked: false, barred: true, secret: false,
            wood: doorWood);
        authored["front-doors"] = front.gameObject;

        GmEstateDoor conservatory = GmEstateDoorFactory.Place(environment, "ConservatoryDoor",
            new Vector3(-6f, 1.2f, -8f), 1.3f, 2.4f, GmEstateDoorFacing.West,
            ConservatoryDoorId, "Conservatory Door", locked: false, barred: true,
            wood: doorWood);
        authored["conservatory-door"] = conservatory.gameObject;

        GmEstateDoor library = GmEstateDoorFactory.Place(environment, "LibraryDoor",
            new Vector3(-4.7f, 1.2f, 10f), 1.3f, 2.4f, GmEstateDoorFacing.North,
            LibraryDoorId, "Library Door", locked: true, barred: false, secret: false,
            requiredKey: LibraryKeyClueId, keyName: "Brass Skeleton Key", wood: doorWood);
        authored["library-door"] = library.gameObject;

        // Under-stair access is the east flank of the rising flights, not the south face.
        // The south face is the first tread. A panel there (0, 6.55) stopped a body at y=0.23.
        // x=2.85 is the east lip of a 1.55 m well: 2.28 left only 1.12 m, and 1.15 m failed
        // for a 1.8 m capsule in the attic.
        GmEstateDoor cellar = GmEstateDoorFactory.Place(environment, "CellarPanel",
            new Vector3(CellarPanelX, 1.05f, CellarPanelZ), 1.15f, 2.05f, GmEstateDoorFacing.West,
            CellarDoorId, "Cellar Panel", locked: true, barred: false, secret: true,
            requiredKey: LibraryLeverClueId, keyName: "bookcase lever", wood: doorWood);
        authored["cellar-panel"] = cellar.gameObject;

        BuildCellar(environment, authored, lighting);

        BuildNorthLibrary(environment, authored, lighting);

        var key = new GameObject("BrassSkeletonKey");
        key.transform.SetParent(gameplay, false);
        key.transform.position = new Vector3(1.3f, 0.58f, -8f);
        GmOwnedPropFactory.CreateRoundedProp("BrassSkeletonKeyMesh", key.transform,
            key.transform.position, Quaternion.Euler(0f, 35f, 12f),
            new Vector3(0.16f, 0.035f, 0.045f), 0.012f,
            CreateMaterial("EntryHall_SkeletonKey", new Color(0.62f, 0.44f, 0.16f), 0.72f, 0.46f));
        key.AddComponent<BoxCollider>().size = new Vector3(0.22f, 0.08f, 0.10f);
        var item = key.AddComponent<GmEstateKeyItem>();
        item.Configure(LibraryKeyClueId, "Brass Skeleton Key",
            "Acquired the Brass Skeleton Key.",
            "An ornate, tarnished brass key bearing the Blackwood crest. The library lock looks to match.");
        authored["brass-skeleton-key"] = key;
    }

    static void BuildCellar(Transform parent, Dictionary<string, GameObject> authored,
        Transform lighting)
    {
        var vault = new GameObject("CellarVault");
        vault.transform.SetParent(parent, false);
        authored["cellar-vault"] = vault;

        float wellX = CellarWellCenterX;
        float wallH = 2.2f - CellarFloorY;
        float wallMidY = (2.2f + CellarFloorY) * 0.5f;
        float belowH = 0.1f - CellarFloorY;
        float belowMidY = (0.1f + CellarFloorY) * 0.5f;

        FloorSlab(vault.transform, "CellarFloor",
            new Vector3(2.75f, CellarFloorY - 0.1f, 4.08f),
            new Vector3(5.2f, 0.2f, 5.25f));
        FloorSlab(vault.transform, "CellarWellFloor",
            new Vector3(wellX, CellarFloorY - 0.1f, CellarPanelZ),
            new Vector3(CellarHoleX, 0.2f, CellarHoleZ));

        WallSlab(vault.transform, "CellarWestWall",
            new Vector3(0.15f, CellarFloorY + 1.45f, 4.08f),
            new Vector3(0.28f, 2.9f, 5.25f));
        WallSlab(vault.transform, "CellarEastWall",
            new Vector3(5.35f, CellarFloorY + 1.45f, 4.08f),
            new Vector3(0.28f, 2.9f, 5.25f));
        WallSlab(vault.transform, "CellarSouthWallWest",
            new Vector3(1.05f, CellarFloorY + 1.45f, 1.45f),
            new Vector3(1.8f, 2.9f, 0.28f));
        WallSlab(vault.transform, "CellarSouthWallEast",
            new Vector3(4.45f, CellarFloorY + 1.45f, 1.45f),
            new Vector3(1.8f, 2.9f, 0.28f));
        WallSlab(vault.transform, "CellarSouthWallHeader",
            new Vector3(2.75f, CellarFloorY + 2.55f, 1.45f),
            new Vector3(1.6f, 0.7f, 0.28f));
        WallSlab(vault.transform, "CellarNorthWallWest",
            new Vector3(0.35f, CellarFloorY + 1.45f, CellarHoleSouth),
            new Vector3(0.70f, 2.9f, 0.28f));
        WallSlab(vault.transform, "CellarNorthWallEast",
            new Vector3(4.50f, CellarFloorY + 1.45f, CellarHoleSouth),
            new Vector3(1.70f, 2.9f, 0.28f));

        WallSlab(vault.transform, "CellarWellWest",
            new Vector3(CellarHoleWest, wallMidY, CellarPanelZ),
            new Vector3(0.16f, wallH, CellarHoleZ));
        // Stop this wall under the hall slab. If it pokes to y=0.1 it fills the panel
        // opening at foot height, and a body on the top tread cannot step east onto the hall.
        float eastLowTop = -0.2f;
        float eastLowH = eastLowTop - CellarFloorY;
        float eastLowMid = (eastLowTop + CellarFloorY) * 0.5f;
        WallSlab(vault.transform, "CellarWellEastLow",
            new Vector3(CellarHoleEast, eastLowMid, CellarPanelZ),
            new Vector3(0.16f, eastLowH, CellarHoleZ));
        WallSlab(vault.transform, "CellarWellEastSouthJamb",
            new Vector3(CellarHoleEast, 1.1f, (CellarHoleSouth + 7.775f) * 0.5f),
            new Vector3(0.16f, 2.2f, 7.775f - CellarHoleSouth));
        WallSlab(vault.transform, "CellarWellEastNorthJamb",
            new Vector3(CellarHoleEast, 1.1f, (8.925f + CellarHoleNorth) * 0.5f),
            new Vector3(0.16f, 2.2f, CellarHoleNorth - 8.925f));
        WallSlab(vault.transform, "CellarWellEastHeader",
            new Vector3(CellarHoleEast, 2.28f, CellarPanelZ),
            new Vector3(0.16f, 0.36f, 1.22f));
        // Header on the shortened hall-floor lip. Bottom at y=1.0 so a descending
        // 1.8 m capsule (head ~0.6 when feet are at y=-1.2) passes under it, while a
        // hall walker still hits it before walking off the slab into the well.
        WallSlab(vault.transform, "CellarWellSouthHall",
            new Vector3(wellX, 1.7f, HallFloorMidSouthNorth - 0.08f),
            new Vector3(CellarHoleX, 1.4f, 0.16f));
        WallSlab(vault.transform, "CellarWellNorthLow",
            new Vector3(wellX, belowMidY, CellarHoleNorth),
            new Vector3(CellarHoleX, belowH, 0.16f));

        authored["cellar-stair"] = BuildCellarStairs(vault.transform, wellX);

        Material iron = CreateMaterial("EntryHall_VaultIron",
            new Color(0.07f, 0.065f, 0.06f), 0.62f, 0.38f);

        GameObject barrel = new GameObject("CellarBarrel");
        barrel.transform.SetParent(vault.transform, false);
        barrel.transform.position = new Vector3(4.55f, CellarFloorY, 3.35f);
        GmOwnedPropFactory.PlacePrefab(
            "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Prefabs/SM_Barrel_01.prefab",
            "CellarBarrel", barrel.transform, new Vector3(4.55f, CellarFloorY + 0.42f, 3.35f),
            new Vector3(0.62f, 0.84f, 0.62f), Quaternion.Euler(0f, 18f, 0f),
            ground: true, surfaceY: CellarFloorY);
        barrel.AddComponent<BoxCollider>().size = new Vector3(0.66f, 0.84f, 0.66f);
        authored["cellar-barrel"] = barrel;

        GameObject barrelTwo = new GameObject("CellarBarrelStack");
        barrelTwo.transform.SetParent(vault.transform, false);
        barrelTwo.transform.position = new Vector3(4.35f, CellarFloorY, 4.15f);
        GmOwnedPropFactory.PlacePrefab(
            "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Prefabs/SM_Barrel_01.prefab",
            "CellarBarrelStack", barrelTwo.transform, new Vector3(4.35f, CellarFloorY + 0.38f, 4.15f),
            new Vector3(0.55f, 0.76f, 0.55f), Quaternion.Euler(0f, -22f, 0f),
            ground: true, surfaceY: CellarFloorY);
        barrelTwo.AddComponent<BoxCollider>().size = new Vector3(0.6f, 0.76f, 0.6f);

        GameObject crate = new GameObject("CellarCrate");
        crate.transform.SetParent(vault.transform, false);
        crate.transform.position = new Vector3(4.7f, CellarFloorY, 5.05f);
        GmOwnedPropFactory.PlacePrefab(
            "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Prefabs/SM_Crate_01.prefab",
            "CellarCrate", crate.transform, new Vector3(4.7f, CellarFloorY + 0.32f, 5.05f),
            new Vector3(0.64f, 0.64f, 0.64f), Quaternion.Euler(0f, 40f, 0f),
            ground: true, surfaceY: CellarFloorY);
        crate.AddComponent<BoxCollider>().size = new Vector3(0.68f, 0.64f, 0.68f);

        GameObject brazier = new GameObject("CellarBrazier");
        brazier.transform.SetParent(vault.transform, false);
        brazier.transform.position = new Vector3(0.85f, CellarFloorY, 3.55f);
        GmOwnedPropFactory.PlacePrefab(
            "Assets/LeartesStudios/WitchVillage/HDRP/Art/Prefabs/SM_brasier.prefab",
            "CellarBrazier", brazier.transform, new Vector3(0.85f, CellarFloorY + 0.38f, 3.55f),
            new Vector3(0.55f, 0.72f, 0.55f), Quaternion.identity,
            ground: true, surfaceY: CellarFloorY);
        authored["cellar-brazier"] = brazier;

        GameObject brazierTwo = new GameObject("CellarBrazierNorth");
        brazierTwo.transform.SetParent(vault.transform, false);
        brazierTwo.transform.position = new Vector3(0.95f, CellarFloorY, 5.25f);
        GmOwnedPropFactory.PlacePrefab(
            "Assets/LeartesStudios/WitchVillage/HDRP/Art/Prefabs/SM_brasier.prefab",
            "CellarBrazierNorth", brazierTwo.transform, new Vector3(0.95f, CellarFloorY + 0.38f, 5.25f),
            new Vector3(0.5f, 0.68f, 0.5f), Quaternion.Euler(0f, 35f, 0f),
            ground: true, surfaceY: CellarFloorY);
        authored["cellar-brazier-two"] = brazierTwo;

        var lantern = new GameObject("CellarLantern");
        lantern.transform.SetParent(lighting, false);
        lantern.transform.position = new Vector3(2.75f, CellarFloorY + 2.05f, 4.2f);
        GmVictorianInteriorKit.Place("Lamp_2", "CellarLantern", lantern.transform,
            lantern.transform.position, new Vector3(0.28f, 0.4f, 0.28f),
            Quaternion.identity, "lamp");
        authored["cellar-lantern"] = lantern;

        authored["cellar-grate"] = BuildVaultGrate(vault.transform, iron);

        var panelLamp = new GameObject("CellarPanelLamp");
        panelLamp.transform.SetParent(lighting, false);
        panelLamp.transform.position = new Vector3(3.42f, 2.12f, CellarPanelZ);
        GmVictorianInteriorKit.Place("Lamp_2", "CellarPanelSconce", panelLamp.transform,
            panelLamp.transform.position, new Vector3(0.26f, 0.42f, 0.3f),
            Quaternion.Euler(0f, 90f, 0f), "lamp");
        authored["cellar-panel-lamp"] = panelLamp;
        authored["cellar-panel-lamp-light"] = CreatePointLight(lighting, "CellarPanelSconceLight",
            new Vector3(3.28f, 2.02f, CellarPanelZ), 4.8f, 28f, new Color(1.0f, 0.7f, 0.4f));

        authored["cellar-brazier-light"] = CreatePointLight(lighting, "CellarBrazierLight",
            new Vector3(0.85f, CellarFloorY + 0.85f, 3.55f), 6.5f, 48f,
            new Color(1.0f, 0.55f, 0.22f));
        authored["cellar-brazier-two-light"] = CreatePointLight(lighting, "CellarBrazierNorthLight",
            new Vector3(0.95f, CellarFloorY + 0.82f, 5.25f), 5.8f, 36f,
            new Color(1.0f, 0.5f, 0.2f));
        authored["cellar-vault-fill"] = CreatePointLight(lighting, "CellarVaultFill",
            new Vector3(2.75f, CellarFloorY + 1.85f, 4.2f), 8.5f, 40f,
            new Color(1.0f, 0.62f, 0.32f));
    }

    static GameObject BuildCellarStairs(Transform parent, float wellX)
    {
        var stairs = new GameObject("CellarStairs");
        stairs.transform.SetParent(parent, false);
        stairs.transform.position = new Vector3(wellX, 0f, CellarPanelZ);
        // Top tread sits at the east panel so a 1.8 m capsule can step onto HallFloorEast.
        // Run matches the grand stair. The flight spills south through the vault lip.
        const int Steps = 12;
        const float Rise = 0.24f;
        const float Run = 0.24f;
        float startZ = CellarPanelZ + 0.12f;
        Material treadWood = CreateMaterial("EntryHall_CellarTread",
            new Color(0.16f, 0.08f, 0.04f), 0.03f, 0.28f);
        float treadWidth = CellarHoleX - 0.12f;
        GameObject rail = GmOwnedPropFactory.CreateRoundedProp("CellarStairRail",
            stairs.transform, new Vector3(wellX - 0.52f, -0.12f, CellarPanelZ - 0.25f),
            Quaternion.Euler(22f, 0f, 0f), new Vector3(0.08f, 0.08f, 0.85f), 0.012f, treadWood);
        for (int i = 0; i < Steps; i++)
        {
            float top = -(i + 1) * Rise;
            float z = startZ - i * Run;
            GmOwnedPropFactory.CreateRoundedProp($"CellarTread_{i + 1:00}",
                stairs.transform, new Vector3(wellX, top - 0.02f, z), Quaternion.identity,
                new Vector3(treadWidth - 0.08f, 0.05f, Run + 0.02f), 0.01f, treadWood);
            AddBarrierCollider(stairs.transform, $"CellarStairTread_{i + 1:00}",
                new Vector3(wellX, top - Rise * 0.5f, z),
                new Vector3(treadWidth, Rise, Run + 0.06f));
        }
        return rail;
    }

    static GameObject BuildVaultGrate(Transform parent, Material iron)
    {
        const float WallZ = 1.45f;
        const float Opening = 1.45f;
        const float GrateX = 2.75f;
        const float GrateY = CellarFloorY + 1.05f;

        var grate = new GameObject("VaultGrate");
        grate.transform.SetParent(parent, false);
        grate.transform.position = new Vector3(GrateX, GrateY, WallZ);

        for (int side = -1; side <= 1; side += 2)
        {
            float swingAngle = side * 62f;
            Vector3 hinge = new Vector3(GrateX + side * Opening * 0.5f, GrateY, WallZ + 0.04f);
            Vector3 closedCenterFromHinge = new Vector3(-side * Opening * 0.25f, 0f, 0f);
            Vector3 center = hinge + Quaternion.Euler(0f, swingAngle, 0f) * closedCenterFromHinge;
            string leafName = side < 0 ? "VaultGrateLeafWest" : "VaultGrateLeafEast";
            var leaf = new GameObject(leafName);
            leaf.transform.SetParent(grate.transform, false);
            leaf.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, swingAngle, 0f));
            GmOwnedPropFactory.CreateRoundedProp(leafName + "StileOuter", leaf.transform,
                center + Quaternion.Euler(0f, swingAngle, 0f) * new Vector3(-side * Opening * 0.22f, 0f, 0f),
                Quaternion.Euler(0f, swingAngle, 0f),
                new Vector3(0.05f, 2.05f, 0.05f), 0.01f, iron);
            GmOwnedPropFactory.CreateRoundedProp(leafName + "StileInner", leaf.transform,
                center + Quaternion.Euler(0f, swingAngle, 0f) * new Vector3(side * Opening * 0.18f, 0f, 0f),
                Quaternion.Euler(0f, swingAngle, 0f),
                new Vector3(0.04f, 2.05f, 0.05f), 0.01f, iron);
            GmOwnedPropFactory.CreateRoundedProp(leafName + "RailTop", leaf.transform,
                center + new Vector3(0f, 0.92f, 0f),
                Quaternion.Euler(0f, swingAngle, 0f),
                new Vector3(Opening * 0.44f, 0.045f, 0.045f), 0.01f, iron);
            GmOwnedPropFactory.CreateRoundedProp(leafName + "RailBottom", leaf.transform,
                center + new Vector3(0f, -0.92f, 0f),
                Quaternion.Euler(0f, swingAngle, 0f),
                new Vector3(Opening * 0.44f, 0.045f, 0.045f), 0.01f, iron);
            for (int bar = 0; bar < 5; bar++)
            {
                Vector3 barOffset = Quaternion.Euler(0f, swingAngle, 0f) *
                    new Vector3((bar - 2) * 0.12f, 0f, 0f);
                GmOwnedPropFactory.CreateRoundedProp(leafName + "Bar_" + bar, leaf.transform,
                    center + barOffset, Quaternion.Euler(0f, swingAngle, 0f),
                    new Vector3(0.04f, 1.82f, 0.04f), 0.008f, iron);
            }
            BoxCollider collider = leaf.AddComponent<BoxCollider>();
            collider.size = new Vector3(Opening * 0.48f, 2.05f, 0.08f);
        }

        var volume = new GameObject("VaultTransition");
        volume.transform.SetParent(grate.transform, false);
        volume.transform.position = new Vector3(GrateX, GrateY, WallZ - 0.22f);
        var box = volume.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(Opening, 2.1f, 0.55f);

        var trigger = volume.AddComponent<GmSceneTransitionTrigger>();
        trigger.TargetSceneId = GmHiddenRoomBuilder.SceneId;
        trigger.TargetScenePath = GmHiddenRoomBuilder.ScenePath;
        trigger.InteractionPrompt = "Through the iron grate, into the archive";

        var door = grate.AddComponent<GmEstateDoor>();
        door.Configure(VaultGrateId, "Iron Grate", "", "",
            locked: false, barred: false, openAng: 0f, leaf: grate.transform,
            secret: false, openAtStart: true);
        door.EnsureBarrier(new Vector3(Opening, 2.1f, 0.16f));

        var threshold = new GameObject("VaultThresholdInterior");
        threshold.transform.SetParent(grate.transform, false);
        FloorSlab(threshold.transform, "VaultThresholdFloor",
            new Vector3(GrateX, CellarFloorY - 0.08f, WallZ - 1.05f),
            new Vector3(Opening + 0.3f, 0.16f, 2.0f));
        CeilingSlab(threshold.transform, "VaultThresholdCeiling",
            new Vector3(GrateX, CellarFloorY + 2.15f, WallZ - 1.05f),
            new Vector3(Opening + 0.3f, 0.16f, 2.0f));
        for (int side = -1; side <= 1; side += 2)
        {
            WallSlab(threshold.transform, side < 0 ? "VaultThresholdWestReveal" : "VaultThresholdEastReveal",
                new Vector3(GrateX + side * (Opening * 0.5f + 0.12f), CellarFloorY + 1.05f, WallZ - 1.05f),
                new Vector3(0.16f, 2.1f, 2.0f));
        }
        WallSlab(threshold.transform, "VaultThresholdBacking",
            new Vector3(GrateX, CellarFloorY + 1.05f, WallZ - 2.02f),
            new Vector3(Opening + 0.4f, 2.1f, 0.16f));

        return grate;
    }

    static void BuildNorthLibrary(Transform parent, Dictionary<string, GameObject> authored,
        Transform lighting)
    {
        var library = new GameObject("NorthLibrary");
        library.transform.SetParent(parent, false);
        authored["library-stub"] = library;

        FloorSlab(library.transform, "LibraryFloor", new Vector3(-6.7f, -0.1f, 13.2f),
            new Vector3(8.8f, 0.2f, 6.4f));
        CeilingSlab(library.transform, "LibraryCeiling", new Vector3(-6.7f, 3.12f, 13.2f),
            new Vector3(8.8f, 0.16f, 6.4f));
        WallSlab(library.transform, "LibraryWestWall", new Vector3(-11.1f, 1.55f, 13.2f),
            new Vector3(0.3f, 3.1f, 6.4f));
        WallSlab(library.transform, "LibraryEastWallSouth", new Vector3(-2.3f, 1.55f, 11.05f),
            new Vector3(0.3f, 3.1f, 2.1f));
        WallSlab(library.transform, "LibraryEastWallNorth", new Vector3(-2.3f, 1.55f, 15.35f),
            new Vector3(0.3f, 3.1f, 2.1f));
        WallSlab(library.transform, "LibraryEastWallHeader", new Vector3(-2.3f, 2.55f, 13.2f),
            new Vector3(0.3f, 1.1f, 2.2f));
        WallSlab(library.transform, "LibraryEastNicheBack", new Vector3(-1.92f, 1.55f, 13.2f),
            new Vector3(0.28f, 3.1f, 2.25f));
        WallSlab(library.transform, "LibraryNorthWall", new Vector3(-6.7f, 1.55f, 16.4f),
            new Vector3(8.8f, 3.1f, 0.3f));

        Color bookcaseTint = new Color(0.36f, 0.22f, 0.12f);
        GameObject westSouth = ImportedProp(library.transform, "LibraryWestBaySouth", "BookShelf_1",
            new Vector3(-10.55f, 1.4f, 11.35f), new Vector3(0.55f, 2.8f, 1.7f),
            Quaternion.Euler(0f, 90f, 0f), "bookcase", bookcaseTint);
        GameObject westMid = ImportedProp(library.transform, "LibraryWestBayMid", "BookShelf_1",
            new Vector3(-10.55f, 1.4f, 13.2f), new Vector3(0.55f, 2.8f, 1.7f),
            Quaternion.Euler(0f, 90f, 0f), "bookcase", bookcaseTint);
        GameObject westNorth = ImportedProp(library.transform, "LibraryWestBayNorth", "BookShelf_1",
            new Vector3(-10.55f, 1.4f, 15.05f), new Vector3(0.55f, 2.8f, 1.7f),
            Quaternion.Euler(0f, 90f, 0f), "bookcase", bookcaseTint);
        GameObject northWest = ImportedProp(library.transform, "LibraryNorthBayWest", "BookShelf_1",
            new Vector3(-8.85f, 1.4f, 15.95f), new Vector3(1.7f, 2.8f, 0.5f),
            Quaternion.Euler(0f, 180f, 0f), "bookcase", bookcaseTint);
        GameObject northCenter = ImportedProp(library.transform, "LibraryStubShelf", "BookShelf_1",
            new Vector3(-4.7f, 1.4f, 15.95f), new Vector3(1.8f, 2.8f, 0.5f),
            Quaternion.Euler(0f, 180f, 0f), "bookcase", bookcaseTint);
        GameObject eastNorth = ImportedProp(library.transform, "LibraryEastBayNorth", "BookShelf_1",
            new Vector3(-2.85f, 1.4f, 15.15f), new Vector3(0.55f, 2.8f, 1.55f),
            Quaternion.Euler(0f, -90f, 0f), "bookcase", bookcaseTint);
        authored["library-stub-shelf"] = northCenter;
        authored["library-west-bay"] = westMid;

        GameObject table = ImportedProp(library.transform, "LibraryReadingTable", "Table_2",
            new Vector3(-7.35f, 0.42f, 13.2f), new Vector3(1.55f, 0.84f, 0.85f),
            Quaternion.Euler(0f, 90f, 0f), "table", new Color(0.28f, 0.13f, 0.06f));
        authored["library-reading-table"] = table;
        ImportedProp(library.transform, "LibraryChairSouth", "Chair_1",
            new Vector3(-7.35f, 0.52f, 12.25f), new Vector3(0.72f, 1.04f, 0.72f),
            Quaternion.Euler(0f, 8f, 0f), "chair", new Color(0.26f, 0.07f, 0.06f));
        ImportedProp(library.transform, "LibraryChairNorth", "Chair_2",
            new Vector3(-7.35f, 0.56f, 14.15f), new Vector3(0.76f, 1.12f, 0.76f),
            Quaternion.Euler(0f, 172f, 0f), "chair", new Color(0.22f, 0.06f, 0.05f));

        authored["library-ladder"] = BuildLibraryLadder(library.transform,
            new Vector3(-10.05f, 0f, 14.35f));

        var eleanor = new GameObject("LibraryEleanorNote");
        eleanor.transform.SetParent(library.transform, false);
        eleanor.transform.position = new Vector3(-10.22f, 1.42f, 13.2f);
        GmOwnedPropFactory.PlacePrefab(
            "Assets/LeartesStudios/WitchVillage/HDRP/Art/Prefabs/SM_Scrolls_2.prefab",
            "LibraryEleanorNote", eleanor.transform, eleanor.transform.position,
            new Vector3(0.22f, 0.05f, 0.16f), Quaternion.Euler(0f, 90f, 8f),
            ground: false);
        eleanor.AddComponent<BoxCollider>().size = new Vector3(0.28f, 0.12f, 0.20f);
        BindExamine(eleanor, "library-eleanor-note",
            "A margin in Eleanor Blackwood's hand: \"The shelf in the east wall is a lock. The books are the key. Even the order in which he loses follows a pattern.\"",
            "She underlined east. Not these west stacks.");
        authored["library-eleanor-note"] = eleanor;

        var inscription = GmOwnedPropFactory.CreateRoundedProp("LibraryInscription",
            library.transform, new Vector3(-2.48f, 2.22f, 13.2f),
            Quaternion.Euler(0f, -90f, 0f), new Vector3(1.85f, 0.16f, 0.04f), 0.012f,
            CreateMaterial("EntryHall_LibraryInscription", new Color(0.32f, 0.16f, 0.07f), 0.04f, 0.3f));
        // Plaque -90 puts local +Z toward the aisle. TextMesh fronts live on -Z (same as the
        // parlor clock numerals), so 180 yaw plus a +Z offset faces the east-looking shelf camera
        // instead of the mirrored backs that Euler(0,-90) kept showing on tour-14.
        AddWorldText("LibraryInscriptionText", "Every game has an order. Even this one.",
            inscription.transform, new Vector3(0f, 0f, 0.03f), Quaternion.Euler(0f, 180f, 0f), 0.008f);
        inscription.AddComponent<BoxCollider>().size = new Vector3(1.9f, 0.2f, 0.08f);
        BindExamine(inscription, "library-inscription",
            "Carved into the east frame: \"Every game has an order. Even this one.\"",
            "Five notches in the rail below. Roman numerals on the spines.");
        authored["library-inscription"] = inscription;

        BuildWeightedShelf(library.transform, authored,
            new[] { westSouth.transform, westMid.transform, westNorth.transform,
                northWest.transform, eastNorth.transform });

        var doorLamp = new GameObject("LibraryStubLamp");
        doorLamp.transform.SetParent(lighting, false);
        doorLamp.transform.position = new Vector3(-4.7f, 2.35f, 10.85f);
        GmVictorianInteriorKit.Place("Lamp_2", "LibraryStubSconce", doorLamp.transform,
            doorLamp.transform.position, new Vector3(0.28f, 0.46f, 0.32f),
            Quaternion.Euler(0f, 180f, 0f), "lamp");
        authored["library-stub-lamp"] = doorLamp;
        authored["library-stub-lamp-light"] = CreatePointLight(lighting, "LibraryStubLight",
            new Vector3(-4.7f, 2.25f, 11.05f), 5.5f, 28f, new Color(1.0f, 0.72f, 0.42f));

        var tableLamp = new GameObject("LibraryReadingLamp");
        tableLamp.transform.SetParent(lighting, false);
        tableLamp.transform.position = new Vector3(-7.05f, 1.18f, 13.2f);
        GmVictorianInteriorKit.Place("Lamp_1_LOD0", "LibraryReadingLamp", tableLamp.transform,
            tableLamp.transform.position, new Vector3(0.28f, 0.42f, 0.28f),
            Quaternion.identity, "lamp");
        authored["library-reading-lamp"] = tableLamp;
        authored["library-reading-lamp-light"] = CreatePointLight(lighting, "LibraryReadingLight",
            new Vector3(-7.05f, 1.12f, 13.2f), 8.5f, 38f, new Color(1.0f, 0.74f, 0.44f));

        var shelfLamp = new GameObject("LibraryShelfLamp");
        shelfLamp.transform.SetParent(lighting, false);
        shelfLamp.transform.position = new Vector3(-3.15f, 2.35f, 13.2f);
        GmVictorianInteriorKit.Place("Lamp_2", "LibraryShelfSconce", shelfLamp.transform,
            shelfLamp.transform.position, new Vector3(0.28f, 0.46f, 0.32f),
            Quaternion.Euler(0f, -90f, 0f), "lamp");
        authored["library-shelf-lamp"] = shelfLamp;
        authored["library-shelf-lamp-light"] = CreatePointLight(lighting, "LibraryShelfLight",
            new Vector3(-3.35f, 2.22f, 13.2f), 6.5f, 32f, new Color(1.0f, 0.7f, 0.4f));
    }

    static GameObject BuildLibraryLadder(Transform parent, Vector3 origin)
    {
        var ladder = new GameObject("LibraryLadder");
        ladder.transform.SetParent(parent, false);
        ladder.transform.position = origin;
        Material rail = CreateMaterial("EntryHall_LibraryLadder",
            new Color(0.22f, 0.11f, 0.05f), 0.03f, 0.32f);
        Quaternion lean = Quaternion.Euler(0f, 90f, 8f);
        GmOwnedPropFactory.CreateRoundedProp("LadderRailLeft", ladder.transform,
            origin + new Vector3(0f, 1.35f, -0.18f), lean, new Vector3(0.05f, 2.7f, 0.05f), 0.012f, rail);
        GmOwnedPropFactory.CreateRoundedProp("LadderRailRight", ladder.transform,
            origin + new Vector3(0f, 1.35f, 0.18f), lean, new Vector3(0.05f, 2.7f, 0.05f), 0.012f, rail);
        for (int rung = 0; rung < 7; rung++)
        {
            float y = 0.28f + rung * 0.36f;
            GmOwnedPropFactory.CreateRoundedProp($"LadderRung_{rung + 1:00}", ladder.transform,
                origin + new Vector3(0.02f, y, 0f), lean, new Vector3(0.04f, 0.035f, 0.42f), 0.01f, rail);
        }
        Material iron = CreateMaterial("EntryHall_LibraryLadderWheel",
            new Color(0.12f, 0.11f, 0.1f), 0.55f, 0.4f);
        GmOwnedPropFactory.CreateRoundedProp("LadderWheelSouth", ladder.transform,
            origin + new Vector3(0.06f, 0.06f, -0.18f), Quaternion.identity,
            new Vector3(0.12f, 0.12f, 0.05f), 0.04f, iron);
        GmOwnedPropFactory.CreateRoundedProp("LadderWheelNorth", ladder.transform,
            origin + new Vector3(0.06f, 0.06f, 0.18f), Quaternion.identity,
            new Vector3(0.12f, 0.12f, 0.05f), 0.04f, iron);
        var body = ladder.AddComponent<BoxCollider>();
        body.center = new Vector3(0.04f, 1.35f, 0f);
        body.size = new Vector3(0.28f, 2.7f, 0.5f);
        return ladder;
    }

    static void BuildWeightedShelf(Transform library, Dictionary<string, GameObject> authored,
        Transform[] tiltOnSolve)
    {
        var caseRoot = new GameObject("WeightedShelfCase");
        caseRoot.transform.SetParent(library, false);
        caseRoot.transform.position = new Vector3(-2.72f, 1.38f, 13.2f);
        authored["library-weighted-shelf"] = caseRoot;
        var caseBlock = caseRoot.AddComponent<BoxCollider>();
        caseBlock.center = Vector3.zero;
        caseBlock.size = new Vector3(0.42f, 1.9f, 2.1f);

        Material railWood = CreateMaterial("EntryHall_WeightedRail",
            new Color(0.3f, 0.14f, 0.06f), 0.04f, 0.28f);
        GmOwnedPropFactory.CreateRoundedProp("WeightedRail", caseRoot.transform,
            new Vector3(-2.72f, 1.18f, 13.2f), Quaternion.identity,
            new Vector3(0.22f, 0.05f, 2.05f), 0.012f, railWood);

        const string Book1 = "Assets/LeartesStudios/WitchVillage/HDRP/Art/Prefabs/SM_Book_1.prefab";
        const string Book2 = "Assets/LeartesStudios/WitchVillage/HDRP/Art/Prefabs/SM_Book_2.prefab";

        var books = new Transform[GmWeightedShelf.SlotCount];
        var slots = new Vector3[GmWeightedShelf.SlotCount];
        var rotations = new Quaternion[GmWeightedShelf.SlotCount];
        Quaternion spineFace = Quaternion.Euler(0f, -90f, 0f);
        for (int i = 0; i < GmWeightedShelf.SlotCount; i++)
        {
            float z = 12.4f + i * 0.4f;
            slots[i] = new Vector3(-2.78f, 1.38f, z);
            rotations[i] = spineFace;
            var book = new GameObject("WeightedBook_" + GmWeightedShelf.Numerals[i]);
            book.transform.SetParent(caseRoot.transform, false);
            book.transform.SetPositionAndRotation(slots[i], spineFace);
            string path = (i % 2 == 0) ? Book1 : Book2;
            GmOwnedPropFactory.PlacePrefab(path, "WeightedBookMesh_" + i, book.transform,
                slots[i], new Vector3(0.16f, 0.28f, 0.18f), spineFace, ground: false);
            AddWorldText("WeightedSpine_" + i, GmWeightedShelf.SpineLabel(i), book.transform,
                new Vector3(0f, 0f, 0.09f), Quaternion.Euler(0f, 180f, 0f), 0.0065f);
            var collider = book.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.18f, 0.3f, 0.08f);
            var interact = book.AddComponent<GmInteractable>();
            interact.Configure(GmWeightedShelf.BookInteractionId(i), "Move", 2.6f, 10f,
                GmInteractionRepeatPolicy.RepeatSecond);
            string second = i == 2
                ? "A scrap tucked in The Middlegame, Marguerite's hand: \"III is correctly placed. Start from there.\""
                : "Five notches. The titles name the phases of a game.";
            interact.BindContent(
                $"{GmWeightedShelf.SpineLabel(i)}. The rail under it is notched.", second);
            books[i] = book.transform;
        }

        var lever = new GameObject("LibraryBrassLever");
        lever.transform.SetParent(library, false);
        lever.transform.position = new Vector3(-2.55f, 1.32f, 13.2f);
        GmOwnedPropFactory.CreateRoundedProp("LibraryBrassLeverMesh", lever.transform,
            lever.transform.position, Quaternion.Euler(0f, 0f, 18f),
            new Vector3(0.18f, 0.035f, 0.045f), 0.01f,
            CreateMaterial("EntryHall_LibraryLever", new Color(0.62f, 0.44f, 0.16f), 0.72f, 0.46f));
        lever.AddComponent<BoxCollider>().size = new Vector3(0.22f, 0.08f, 0.1f);
        BindExamine(lever, "library-lever",
            "A brass catch released by the ordered shelf. The under-stair panel should give now.",
            "The latch is already open.");
        authored["library-lever"] = lever;

        var shelf = caseRoot.AddComponent<GmWeightedShelf>();
        shelf.Bind(books, slots, rotations, caseRoot.transform, lever.transform, tiltOnSolve,
            new Vector3(0.38f, 0f, 0f));
    }

    static void BindExamine(GameObject go, string id, string first, string second)
    {
        var interact = go.GetComponent<GmInteractable>() ?? go.AddComponent<GmInteractable>();
        interact.Configure(id, "Examine", 2.8f, 8f, GmInteractionRepeatPolicy.RepeatSecond);
        interact.BindContent(first, second);
    }

    static GameObject HangDebtorPortrait(Transform parent, int index, string name, string slug,
        Vector3 framePos, Quaternion facing, Vector3 canvasPos, Vector3 platePos, Material brass,
        string objectPrefix = "Portrait")
    {
        var portrait = new GameObject($"{objectPrefix}_{index + 1}_{name}");
        portrait.transform.SetParent(parent, false);
        portrait.transform.position = framePos;
        GmVictorianInteriorKit.Place($"Picture_{index % 8 + 1}", $"{objectPrefix}Frame_{index + 1:00}",
            portrait.transform, framePos, new Vector3(0.12f, 1.24f, 0.92f), facing, "picture");
        GmOwnedPropFactory.CreateRoundedProp("PortraitCanvas", portrait.transform,
            canvasPos, facing, new Vector3(0.72f, 1.02f, 0.025f), 0.012f,
            GmVictorianInteriorKit.Portrait(slug));
        GameObject plate = GmOwnedPropFactory.CreateRoundedProp($"Plate_{name}", portrait.transform,
            platePos, facing, new Vector3(0.56f, 0.13f, 0.035f), 0.025f, brass);
        AddWorldText($"Name_{slug}", name, plate.transform,
            new Vector3(0f, 0f, -0.021f), Quaternion.identity, 0.0105f);
        return portrait;
    }

    static void BuildSecondFloor(Transform parent, Dictionary<string, GameObject> authored,
        Transform lighting)
    {
        Material doorWood = CreateMaterial("EntryHall_UpperDoorWood",
            new Color(0.32f, 0.14f, 0.06f), 0.03f, 0.34f);

        var gallery = new GameObject("SecondFloorGallery");
        gallery.transform.SetParent(parent, false);
        authored["second-floor-landing"] = FloorSlab(gallery.transform, "SecondFloorLanding",
            new Vector3(0f, SecondFloorY - 0.1f, 13.2f),
            new Vector3(4.8f, 0.2f, 6.2f));
        FloorSlab(gallery.transform, "SecondFloorWestWing", new Vector3(-5.05f, SecondFloorY - 0.1f, 13.2f),
            new Vector3(5.3f, 0.2f, 6.2f));
        FloorSlab(gallery.transform, "SecondFloorEastWing", new Vector3(5.05f, SecondFloorY - 0.1f, 13.2f),
            new Vector3(5.3f, 0.2f, 6.2f));
        // Hatch well at (4.2, 14.6). Split the 2F ceiling so a body can climb through after the key.
        // Z has to cover the whole stretch where the 1.8 m capsule still overlaps the 2F ceiling.
        const float HatchX = 4.2f;
        const float HatchZ = 14.6f;
        const float HoleX = 1.5f;
        const float HoleZ = 3.2f;
        float holeWest = HatchX - HoleX * 0.5f;
        float holeEast = HatchX + HoleX * 0.5f;
        float holeSouth = HatchZ - HoleZ * 0.5f;
        float holeNorth = HatchZ + HoleZ * 0.5f;
        CeilingSlab(gallery.transform, "SecondFloorCeilingWest",
            new Vector3((-5.8f + holeWest) * 0.5f, 6.2f, 13.2f),
            new Vector3(holeWest - (-5.8f), 0.2f, 6.2f));
        CeilingSlab(gallery.transform, "SecondFloorCeilingEastSouth",
            new Vector3((holeWest + 5.8f) * 0.5f, 6.2f, (10.1f + holeSouth) * 0.5f),
            new Vector3(5.8f - holeWest, 0.2f, holeSouth - 10.1f));
        CeilingSlab(gallery.transform, "SecondFloorCeilingEastNorth",
            new Vector3((holeWest + 5.8f) * 0.5f, 6.2f, (holeNorth + 16.3f) * 0.5f),
            new Vector3(5.8f - holeWest, 0.2f, 16.3f - holeNorth));
        CeilingSlab(gallery.transform, "SecondFloorCeilingEastMid",
            new Vector3((holeEast + 5.8f) * 0.5f, 6.2f, HatchZ),
            new Vector3(5.8f - holeEast, 0.2f, HoleZ));
        WallSlab(gallery.transform, "GalleryNorthWallWest", new Vector3(-4.4f, 4.8f, 16.2f),
            new Vector3(2.8f, 2.8f, 0.3f));
        WallSlab(gallery.transform, "GalleryNorthWallMid", new Vector3(0f, 4.8f, 16.2f),
            new Vector3(3.6f, 2.8f, 0.3f));
        WallSlab(gallery.transform, "GalleryNorthWallEast", new Vector3(4.4f, 4.8f, 16.2f),
            new Vector3(2.8f, 2.8f, 0.3f));
        WallSlab(gallery.transform, "GalleryNorthMarrHeader", new Vector3(-2.4f, 5.975f, 16.2f),
            new Vector3(1.2f, 0.45f, 0.3f));
        WallSlab(gallery.transform, "GalleryNorthBarredHeader", new Vector3(2.4f, 5.975f, 16.2f),
            new Vector3(1.2f, 0.45f, 0.3f));
        WallSlab(gallery.transform, "GalleryEastWall", new Vector3(5.8f, 4.8f, 13.2f),
            new Vector3(0.3f, 2.8f, 6.2f));
        WallSlab(gallery.transform, "GalleryWestWallNorth", new Vector3(-5.8f, 4.8f, 15.075f),
            new Vector3(0.3f, 2.8f, 2.45f));
        WallSlab(gallery.transform, "GalleryWestWallSouth", new Vector3(-5.8f, 4.8f, 11.325f),
            new Vector3(0.3f, 2.8f, 2.45f));
        WallSlab(gallery.transform, "GalleryWestHeader", new Vector3(-5.8f, 5.975f, 13.2f),
            new Vector3(0.3f, 0.45f, 1.3f));

        GmEstateDoor percivalDoor = GmEstateDoorFactory.Place(gallery.transform, "PercivalDoor",
            new Vector3(-5.8f, SecondFloorY + 1.2f, 13.2f), 1.3f, 2.3f, GmEstateDoorFacing.West,
            PercivalDoorId, "Percival's Door", locked: false, barred: false, startOpen: true,
            wood: doorWood);
        authored["percival-door"] = percivalDoor.gameObject;

        GmEstateDoor marr = GmEstateDoorFactory.Place(gallery.transform, "MarrDoor",
            new Vector3(-2.4f, SecondFloorY + 1.2f, 16.2f), 1.2f, 2.3f, GmEstateDoorFacing.North,
            MarrDoorId, "Lady Marr's Study", locked: true, barred: false,
            requiredKey: MarrKeyClueId, keyName: "Lady Marr's key", wood: doorWood);
        authored["marr-door"] = marr.gameObject;

        GmEstateDoor barredGuest = GmEstateDoorFactory.Place(gallery.transform, "BarredGuestDoor",
            new Vector3(2.4f, SecondFloorY + 1.2f, 16.2f), 1.2f, 2.3f, GmEstateDoorFacing.North,
            BarredGuestDoorId, "Barricaded Guest Room", locked: false, barred: true,
            wood: doorWood);
        authored["barred-guest-door"] = barredGuest.gameObject;

        authored["attic-hatch"] = BuildAtticHatch(gallery.transform, HatchX, HatchZ, HoleX, HoleZ, doorWood).gameObject;

        BuildPercivalRoom(parent, authored, lighting);

        var landingLamp = new GameObject("LandingUpperLamp");
        landingLamp.transform.SetParent(lighting, false);
        landingLamp.transform.position = new Vector3(0f, 5.15f, 13.0f);
        GmVictorianInteriorKit.Place("Lamp_1_LOD0", "LandingUpperFixture", landingLamp.transform,
            landingLamp.transform.position, new Vector3(0.7f, 0.55f, 0.7f),
            Quaternion.identity, "lamp");
        authored["landing-upper-lamp"] = landingLamp;
        authored["landing-upper-lamp-light"] = CreatePointLight(lighting, "LandingUpperLight",
            new Vector3(0f, 5.05f, 13.0f), 7f, 40f, new Color(1.0f, 0.78f, 0.52f));

        BuildUpperDebtorGallery(gallery.transform, authored, lighting);
        BuildMarrStudy(parent, authored, lighting);
        BuildBarredGuestRoom(parent, authored, lighting);
        PlaceMarrKey(parent, authored);
        PlaceAtticKey(parent, authored);
        BuildAtticLoft(parent, authored, lighting, HatchX, HatchZ, HoleX, HoleZ);
    }

    static void BuildUpperDebtorGallery(Transform gallery, Dictionary<string, GameObject> authored,
        Transform lighting)
    {
        // Foyer west wall still holds the original nine and Shard #1. The stair did not eat that
        // wall, so this is an extension: the same debtors look down the 2F run.
        var upper = new GameObject("UpperDebtorGallery");
        upper.transform.SetParent(gallery, false);
        authored["upper-debtor-gallery"] = upper;
        Material brass = CreateMaterial("EntryHall_UpperNameplate", new Color(0.48f, 0.31f, 0.10f), 0.72f, 0.42f);

        Vector3[] frames =
        {
            new Vector3(5.72f, 5.15f, 11.05f),
            new Vector3(5.72f, 5.15f, 12.2f),
            new Vector3(5.72f, 5.15f, 13.35f),
            new Vector3(5.72f, 5.15f, 14.5f),
            new Vector3(5.72f, 5.15f, 15.65f),
            new Vector3(-5.15f, 5.15f, 16.12f),
            new Vector3(-3.85f, 5.15f, 16.12f),
            new Vector3(3.85f, 5.15f, 16.12f),
            new Vector3(5.15f, 5.15f, 16.12f)
        };
        Quaternion[] facings =
        {
            Quaternion.Euler(0f, 90f, 0f), Quaternion.Euler(0f, 90f, 0f),
            Quaternion.Euler(0f, 90f, 0f), Quaternion.Euler(0f, 90f, 0f),
            Quaternion.Euler(0f, 90f, 0f),
            Quaternion.Euler(0f, 180f, 0f), Quaternion.Euler(0f, 180f, 0f),
            Quaternion.Euler(0f, 180f, 0f), Quaternion.Euler(0f, 180f, 0f)
        };
        Vector3[] intoRoom =
        {
            new Vector3(-0.065f, 0f, 0f), new Vector3(-0.065f, 0f, 0f),
            new Vector3(-0.065f, 0f, 0f), new Vector3(-0.065f, 0f, 0f),
            new Vector3(-0.065f, 0f, 0f),
            new Vector3(0f, 0f, -0.065f), new Vector3(0f, 0f, -0.065f),
            new Vector3(0f, 0f, -0.065f), new Vector3(0f, 0f, -0.065f)
        };

        for (int i = 0; i < DebtorNames.Length; i++)
        {
            Vector3 frame = frames[i];
            Vector3 canvas = frame + intoRoom[i];
            Vector3 plate = canvas + new Vector3(0f, -0.67f, 0f) + intoRoom[i] * 0.2f;
            GameObject portrait = HangDebtorPortrait(upper.transform, i, DebtorNames[i], DebtorSlugs[i],
                frame, facings[i], canvas, plate, brass, "UpperPortrait");
            if (i == 2) authored["upper-gallery-frame"] = portrait;
        }

        GameObject runner = ImportedProp(gallery, "UpperGalleryRunner", "Carpet_1",
            new Vector3(0f, SecondFloorY + 0.03f, 13.2f), new Vector3(2.4f, 0.06f, 4.8f),
            Quaternion.identity, "carpet", surfaceY: SecondFloorY);
        authored["upper-gallery-runner"] = runner;

        var eastSconce = new GameObject("UpperEastSconce");
        eastSconce.transform.SetParent(lighting, false);
        eastSconce.transform.position = new Vector3(5.55f, 5.35f, 13.2f);
        GmVictorianInteriorKit.Place("Lamp_2", "UpperEastSconce", eastSconce.transform,
            eastSconce.transform.position, new Vector3(0.28f, 0.46f, 0.32f),
            Quaternion.Euler(0f, -90f, 0f), "lamp");
        authored["upper-east-sconce"] = eastSconce;
        authored["upper-east-sconce-light"] = CreatePointLight(lighting, "UpperEastLight",
            new Vector3(5.2f, 5.2f, 13.2f), 6.5f, 34f, new Color(1.0f, 0.76f, 0.48f));
    }

    static void BuildMarrStudy(Transform parent, Dictionary<string, GameObject> authored,
        Transform lighting)
    {
        var room = new GameObject("MarrStudy");
        room.transform.SetParent(parent, false);
        authored["marr-study"] = room;
        FloorSlab(room.transform, "MarrFloor", new Vector3(-2.4f, SecondFloorY - 0.1f, 18.55f),
            new Vector3(4.4f, 0.2f, 4.5f));
        CeilingSlab(room.transform, "MarrCeiling", new Vector3(-2.4f, 6.2f, 18.55f),
            new Vector3(4.4f, 0.2f, 4.5f));
        WallSlab(room.transform, "MarrWestWall", new Vector3(-4.55f, 4.8f, 18.55f),
            new Vector3(0.3f, 2.8f, 4.5f));
        WallSlab(room.transform, "MarrEastWall", new Vector3(-0.15f, 4.8f, 18.55f),
            new Vector3(0.3f, 2.8f, 4.5f));
        WallSlab(room.transform, "MarrNorthWall", new Vector3(-2.4f, 4.8f, 20.7f),
            new Vector3(4.4f, 2.8f, 0.3f));

        GameObject desk = ImportedProp(room.transform, "MarrDesk", "Table_3",
            new Vector3(-2.55f, SecondFloorY + 0.36f, 19.55f), new Vector3(0.95f, 0.72f, 0.85f),
            Quaternion.Euler(0f, 180f, 0f), "table", surfaceY: SecondFloorY);
        authored["marr-desk"] = desk;
        GameObject chair = ImportedProp(room.transform, "MarrChair", "Chair_2",
            new Vector3(-2.55f, SecondFloorY + 0.52f, 18.85f), new Vector3(0.72f, 1.04f, 0.72f),
            Quaternion.Euler(0f, 8f, 0f), "chair", new Color(0.24f, 0.07f, 0.06f), SecondFloorY);
        authored["marr-chair"] = chair;

        GameObject bookcase = ImportedProp(room.transform, "MarrBookcase", "BookShelf_1",
            new Vector3(-4.05f, SecondFloorY + 1.4f, 18.4f), new Vector3(0.5f, 2.6f, 1.6f),
            Quaternion.Euler(0f, 90f, 0f), "bookcase", new Color(0.22f, 0.12f, 0.06f), SecondFloorY);
        authored["marr-bookcase"] = bookcase;
        PlaceMarrBooks(room.transform, authored);

        var note = new GameObject("MarrHandNote");
        note.transform.SetParent(room.transform, false);
        note.transform.position = new Vector3(-2.4f, SecondFloorY + 0.78f, 19.55f);
        GmOwnedPropFactory.PlacePrefab(
            "Assets/LeartesStudios/WitchVillage/HDRP/Art/Prefabs/SM_Scrolls_2.prefab",
            "MarrHandNote", note.transform, note.transform.position,
            new Vector3(0.22f, 0.05f, 0.16f), Quaternion.Euler(0f, 0f, 6f),
            ground: false);
        note.AddComponent<BoxCollider>().size = new Vector3(0.28f, 0.1f, 0.2f);
        BindExamine(note, "marr-hand-note",
            "A blotter in a woman's hand: \"Watch his hands. Not the cards. Not the smile. The hands.\"",
            "The ink is newer than the portrait downstairs. She was still writing after he sat down.");
        authored["marr-hand-note"] = note;

        var lamp = new GameObject("MarrLamp");
        lamp.transform.SetParent(lighting, false);
        lamp.transform.position = new Vector3(-2.4f, 5.05f, 18.7f);
        GmVictorianInteriorKit.Place("Lamp_2", "MarrSconce", lamp.transform,
            lamp.transform.position, new Vector3(0.28f, 0.46f, 0.32f),
            Quaternion.Euler(0f, 180f, 0f), "lamp");
        authored["marr-lamp"] = lamp;
        authored["marr-lamp-light"] = CreatePointLight(lighting, "MarrLight",
            new Vector3(-2.4f, 4.95f, 18.55f), 6f, 34f, new Color(1.0f, 0.74f, 0.46f));
    }

    static void BuildBarredGuestRoom(Transform parent, Dictionary<string, GameObject> authored,
        Transform lighting)
    {
        var room = new GameObject("BarredGuestRoom");
        room.transform.SetParent(parent, false);
        authored["barred-guest-room"] = room;
        FloorSlab(room.transform, "BarredGuestFloor", new Vector3(2.4f, SecondFloorY - 0.1f, 18.55f),
            new Vector3(4.4f, 0.2f, 4.5f));
        CeilingSlab(room.transform, "BarredGuestCeiling", new Vector3(2.4f, 6.2f, 18.55f),
            new Vector3(4.4f, 0.2f, 4.5f));
        WallSlab(room.transform, "BarredGuestWestWall", new Vector3(0.15f, 4.8f, 18.55f),
            new Vector3(0.3f, 2.8f, 4.5f));
        WallSlab(room.transform, "BarredGuestEastWall", new Vector3(4.55f, 4.8f, 18.55f),
            new Vector3(0.3f, 2.8f, 4.5f));
        WallSlab(room.transform, "BarredGuestNorthWall", new Vector3(2.4f, 4.8f, 20.7f),
            new Vector3(4.4f, 2.8f, 0.3f));

        var bed = new GameObject("BarredGuestBed");
        bed.transform.SetParent(room.transform, false);
        bed.transform.position = new Vector3(3.15f, SecondFloorY, 19.35f);
        GmOwnedPropFactory.PlacePrefab("Assets/GamesMaster/Props/GothicBed.fbx",
            "BarredGuestBed", bed.transform, new Vector3(3.15f, SecondFloorY + 0.7f, 19.35f),
            new Vector3(1.9f, 1.3f, 1.5f), Quaternion.Euler(90f, 0f, 0f),
            ground: true, surfaceY: SecondFloorY,
            overrideMaterial: CreateMaterial("EntryHall_BarredGuestBed",
                new Color(0.16f, 0.08f, 0.05f), 0.04f, 0.26f));
        bed.AddComponent<BoxCollider>().size = new Vector3(1.9f, 1.15f, 1.5f);
        authored["barred-guest-bed"] = bed;

        GameObject chair = ImportedProp(room.transform, "BarredGuestChair", "Chair_1",
            new Vector3(2.35f, SecondFloorY + 0.52f, 16.85f), new Vector3(0.7f, 1.0f, 0.7f),
            Quaternion.Euler(0f, 180f, 0f), "chair", new Color(0.2f, 0.08f, 0.05f), SecondFloorY);
        authored["barred-guest-chair"] = chair;
        BindExamine(chair, "barred-guest-chair",
            "A chair jammed under the inner latch. Whoever is in here does not want the landing.",
            "The legs have scored the parquet. This was done in a hurry, and it has stayed.");

        var lamp = new GameObject("BarredGuestLamp");
        lamp.transform.SetParent(lighting, false);
        lamp.transform.position = new Vector3(2.4f, 5.05f, 18.7f);
        GmVictorianInteriorKit.Place("Lamp_2", "BarredGuestSconce", lamp.transform,
            lamp.transform.position, new Vector3(0.28f, 0.46f, 0.32f),
            Quaternion.Euler(0f, 180f, 0f), "lamp");
        authored["barred-guest-lamp"] = lamp;
        authored["barred-guest-lamp-light"] = CreatePointLight(lighting, "BarredGuestLight",
            new Vector3(2.4f, 4.95f, 18.55f), 5.5f, 28f, new Color(1.0f, 0.7f, 0.42f));
    }

    static void PlaceMarrKey(Transform parent, Dictionary<string, GameObject> authored)
    {
        var key = new GameObject("LadyMarrKey");
        key.transform.SetParent(parent, false);
        key.transform.position = new Vector3(-10.05f, SecondFloorY + 0.78f, 12.15f);
        GmOwnedPropFactory.CreateRoundedProp("LadyMarrKeyMesh", key.transform,
            key.transform.position, Quaternion.Euler(0f, -20f, 8f),
            new Vector3(0.14f, 0.03f, 0.04f), 0.01f,
            CreateMaterial("EntryHall_MarrKey", new Color(0.55f, 0.42f, 0.18f), 0.7f, 0.5f));
        key.AddComponent<BoxCollider>().size = new Vector3(0.2f, 0.07f, 0.09f);
        var item = key.AddComponent<GmEstateKeyItem>();
        item.Configure(MarrKeyClueId, "Lady Marr's key",
            "Acquired Lady Marr's key.",
            "A smaller brass key, the bow stamped M. It was left on Percival's blotting paper as if he meant to go back.");
        authored["lady-marr-key"] = key;
    }

    static void PlaceMarrBooks(Transform room, Dictionary<string, GameObject> authored)
    {
        const string Book1 = "Assets/LeartesStudios/WitchVillage/HDRP/Art/Prefabs/SM_Book_1.prefab";
        const string Book2 = "Assets/LeartesStudios/WitchVillage/HDRP/Art/Prefabs/SM_Book_2.prefab";
        var books = new GameObject("MarrBooks");
        books.transform.SetParent(room, false);
        authored["marr-books"] = books;
        Vector3[] posts =
        {
            new Vector3(-3.82f, SecondFloorY + 0.55f, 17.85f),
            new Vector3(-3.82f, SecondFloorY + 0.55f, 18.15f),
            new Vector3(-3.82f, SecondFloorY + 0.55f, 18.55f),
            new Vector3(-3.82f, SecondFloorY + 1.15f, 17.9f),
            new Vector3(-3.82f, SecondFloorY + 1.15f, 18.25f),
            new Vector3(-3.82f, SecondFloorY + 1.15f, 18.65f),
            new Vector3(-3.82f, SecondFloorY + 1.75f, 18.05f),
            new Vector3(-3.82f, SecondFloorY + 1.75f, 18.45f),
            new Vector3(-3.82f, SecondFloorY + 2.35f, 18.2f),
            new Vector3(-3.82f, SecondFloorY + 2.35f, 18.55f),
            new Vector3(-2.35f, SecondFloorY + 0.78f, 19.35f),
            new Vector3(-2.22f, SecondFloorY + 0.78f, 19.48f),
            new Vector3(-2.48f, SecondFloorY + 0.78f, 19.22f)
        };
        Quaternion spine = Quaternion.Euler(0f, 90f, 0f);
        for (int i = 0; i < posts.Length; i++)
        {
            string path = (i % 2 == 0) ? Book1 : Book2;
            Quaternion rot = i >= 10 ? Quaternion.Euler(0f, i * 17f, 0f) : spine;
            GmOwnedPropFactory.PlacePrefab(path, "MarrBook_" + (i + 1), books.transform,
                posts[i], new Vector3(0.13f, 0.24f, 0.18f), rot, ground: false);
        }
    }

    static void PlaceAtticKey(Transform parent, Dictionary<string, GameObject> authored)
    {
        var key = new GameObject("AtticHatchKey");
        key.transform.SetParent(parent, false);
        key.transform.position = new Vector3(-2.85f, SecondFloorY + 0.78f, 19.42f);
        GmOwnedPropFactory.CreateRoundedProp("AtticHatchKeyMesh", key.transform,
            key.transform.position, Quaternion.Euler(0f, 25f, -10f),
            new Vector3(0.13f, 0.03f, 0.035f), 0.01f,
            CreateMaterial("EntryHall_AtticKey", new Color(0.42f, 0.38f, 0.28f), 0.55f, 0.46f));
        key.AddComponent<BoxCollider>().size = new Vector3(0.18f, 0.07f, 0.08f);
        var item = key.AddComponent<GmEstateKeyItem>();
        item.Configure(AtticKeyClueId, "attic key",
            "Acquired the attic key.",
            "Iron, not brass. The bow is cut with a tiny loft window. It was under Marr's blotter.");
        authored["attic-hatch-key"] = key;
    }

    static void BuildAtticLoft(Transform parent, Dictionary<string, GameObject> authored,
        Transform lighting, float hatchX, float hatchZ, float holeX, float holeZ)
    {
        var loft = new GameObject("AtticLoft");
        loft.transform.SetParent(parent, false);
        authored["attic-loft"] = loft;

        float holeWest = hatchX - holeX * 0.5f;
        float holeEast = hatchX + holeX * 0.5f;
        float holeSouth = hatchZ - holeZ * 0.5f;
        float holeNorth = hatchZ + holeZ * 0.5f;
        FloorSlab(loft.transform, "AtticFloorWest",
            new Vector3((-5.8f + holeWest) * 0.5f, AtticFloorY - 0.1f, 13.2f),
            new Vector3(holeWest - (-5.8f), 0.2f, 6.2f));
        FloorSlab(loft.transform, "AtticFloorEastSouth",
            new Vector3((holeWest + 5.8f) * 0.5f, AtticFloorY - 0.1f, (10.1f + holeSouth) * 0.5f),
            new Vector3(5.8f - holeWest, 0.2f, holeSouth - 10.1f));
        FloorSlab(loft.transform, "AtticFloorEastNorth",
            new Vector3((holeWest + 5.8f) * 0.5f, AtticFloorY - 0.1f, (holeNorth + 16.3f) * 0.5f),
            new Vector3(5.8f - holeWest, 0.2f, 16.3f - holeNorth));
        FloorSlab(loft.transform, "AtticFloorEastMid",
            new Vector3((holeEast + 5.8f) * 0.5f, AtticFloorY - 0.1f, hatchZ),
            new Vector3(5.8f - holeEast, 0.2f, holeZ));

        WallSlab(loft.transform, "AtticSouthWall", new Vector3(0f, 7.9f, 10.1f),
            new Vector3(11.6f, 2.8f, 0.3f));
        WallSlab(loft.transform, "AtticEastWall", new Vector3(5.8f, 7.9f, 13.2f),
            new Vector3(0.3f, 2.8f, 6.2f));
        WallSlab(loft.transform, "AtticWestWall", new Vector3(-5.8f, 7.9f, 13.2f),
            new Vector3(0.3f, 2.8f, 6.2f));
        WallSlab(loft.transform, "AtticNorthWallWest", new Vector3(-3.35f, 7.9f, 16.2f),
            new Vector3(4.9f, 2.8f, 0.3f));
        WallSlab(loft.transform, "AtticNorthWallEast", new Vector3(3.35f, 7.9f, 16.2f),
            new Vector3(4.9f, 2.8f, 0.3f));
        WallSlab(loft.transform, "AtticNorthDormerHeader", new Vector3(0f, 8.85f, 16.2f),
            new Vector3(1.8f, 0.9f, 0.3f));
        WallSlab(loft.transform, "AtticNorthDormerSill", new Vector3(0f, 6.85f, 16.2f),
            new Vector3(1.8f, 0.7f, 0.3f));

        Material beam = CreateMaterial("EntryHall_AtticRafter", new Color(0.18f, 0.09f, 0.04f), 0.02f, 0.22f);
        var rafterRoot = new GameObject("AtticRafters");
        rafterRoot.transform.SetParent(loft.transform, false);
        rafterRoot.transform.position = new Vector3(0f, 8.15f, 13.2f);
        GameObject firstRafter = null;
        for (int i = 0; i < 5; i++)
        {
            float z = 11.0f + i * 1.25f;
            GameObject west = GmOwnedPropFactory.CreateRoundedProp("AtticRafterWest_" + (i + 1), rafterRoot.transform,
                new Vector3(-2.7f, 8.15f, z), Quaternion.Euler(0f, 0f, 18f),
                new Vector3(5.4f, 0.12f, 0.16f), 0.03f, beam);
            if (i == 1) firstRafter = west;
            GmOwnedPropFactory.CreateRoundedProp("AtticRafterEast_" + (i + 1), rafterRoot.transform,
                new Vector3(2.7f, 8.15f, z), Quaternion.Euler(0f, 0f, -18f),
                new Vector3(5.4f, 0.12f, 0.16f), 0.03f, beam);
        }
        authored["attic-rafter"] = firstRafter != null ? firstRafter : rafterRoot;

        authored["attic-ladder"] = BuildAtticLadder(loft.transform, hatchX, hatchZ, holeX, holeZ);

        GameObject crate = new GameObject("AtticCrate");
        crate.transform.SetParent(loft.transform, false);
        crate.transform.position = new Vector3(-3.4f, AtticFloorY, 12.15f);
        GmOwnedPropFactory.PlacePrefab(
            "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Prefabs/SM_Crate_01.prefab",
            "AtticCrate", crate.transform, new Vector3(-3.4f, AtticFloorY + 0.35f, 12.15f),
            new Vector3(0.7f, 0.7f, 0.7f), Quaternion.Euler(0f, 18f, 0f),
            ground: true, surfaceY: AtticFloorY);
        crate.AddComponent<BoxCollider>().size = new Vector3(0.75f, 0.7f, 0.75f);
        authored["attic-crate"] = crate;

        GameObject crateTwo = new GameObject("AtticCrateStack");
        crateTwo.transform.SetParent(loft.transform, false);
        crateTwo.transform.position = new Vector3(-3.85f, AtticFloorY, 12.85f);
        GmOwnedPropFactory.PlacePrefab(
            "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Prefabs/SM_Crate_01.prefab",
            "AtticCrateStack", crateTwo.transform, new Vector3(-3.85f, AtticFloorY + 0.28f, 12.85f),
            new Vector3(0.55f, 0.55f, 0.55f), Quaternion.Euler(0f, -12f, 0f),
            ground: true, surfaceY: AtticFloorY);

        var cobwebs = new GameObject("AtticCobwebs");
        cobwebs.transform.SetParent(loft.transform, false);
        authored["attic-cobweb"] = cobwebs;
        Material web = CreateMaterial("EntryHall_AtticCobweb", new Color(0.16f, 0.15f, 0.13f), 0.02f, 0.08f);
        GmOwnedPropFactory.PlacePrefab("Assets/GamesMaster/Props/Cobweb_02.fbx",
            "AtticCobwebWest", cobwebs.transform, new Vector3(-5.15f, 8.35f, 11.05f),
            new Vector3(1.4f, 1.1f, 0.08f), Quaternion.Euler(0f, 90f, 12f),
            ground: false, overrideMaterial: web);
        GmOwnedPropFactory.PlacePrefab("Assets/GamesMaster/Props/Cobweb_03.fbx",
            "AtticCobwebEast", cobwebs.transform, new Vector3(5.15f, 8.2f, 15.55f),
            new Vector3(1.2f, 0.9f, 0.08f), Quaternion.Euler(0f, -90f, -8f),
            ground: false, overrideMaterial: web);

        GameObject shard = GmOwnedPropFactory.CreateMirrorShard("MirrorShard_2", loft.transform,
            new Vector3(0.15f, AtticFloorY + 0.72f, 15.55f), Quaternion.Euler(12f, 200f, 8f),
            new Vector3(0.34f, 0.46f, 0.055f),
            CreateMaterial("EntryHall_MirrorShardTwo", new Color(0.42f, 0.58f, 0.66f), 0.86f, 0.92f));
        shard.AddComponent<BoxCollider>().size = new Vector3(1f, 1f, 1f);
        authored["shard-two"] = shard;

        GmInteriorMoonWindow.Result dormer = GmInteriorMoonWindow.Build(loft.transform,
            "AtticDormer", new Vector3(0f, 7.55f, 16.05f),
            new Vector3(0.15f, 6.9f, 14.6f), new Vector2(1.35f, 1.55f), 220f);
        dormer.light.name = "AtticDormerMoonLight";
        authored["attic-dormer"] = dormer.fixture;
        authored["attic-dormer-light"] = dormer.light;

        var lantern = new GameObject("AtticLantern");
        lantern.transform.SetParent(lighting, false);
        lantern.transform.position = new Vector3(-1.2f, 8.05f, 12.4f);
        GmVictorianInteriorKit.Place("Lamp_2", "AtticLantern", lantern.transform,
            lantern.transform.position, new Vector3(0.26f, 0.4f, 0.28f),
            Quaternion.identity, "lamp");
        authored["attic-lantern"] = lantern;
        authored["attic-lantern-light"] = CreatePointLight(lighting, "AtticLanternLight",
            new Vector3(-1.2f, 7.85f, 12.4f), 8f, 35f, new Color(1.0f, 0.68f, 0.38f));
        authored["attic-dormer-fill"] = CreatePointLight(lighting, "AtticDormerFill",
            new Vector3(0.2f, 7.55f, 15.15f), 4.2f, 35f, new Color(0.72f, 0.82f, 1.0f));
    }

    static GmEstateDoor BuildAtticHatch(Transform parent, float hatchX, float hatchZ,
        float holeX, float holeZ, Material wood)
    {
        const float HatchY = 6.18f;
        var root = new GameObject("AtticHatch");
        root.transform.SetParent(parent, false);
        root.transform.position = new Vector3(hatchX, HatchY, hatchZ);

        var leaf = new GameObject("AtticHatchLeaf");
        leaf.transform.SetParent(root.transform, false);
        leaf.transform.localPosition = new Vector3(0f, 0f, -holeZ * 0.5f);

        GmOwnedPropFactory.CreateRoundedProp("AtticHatchBoard", leaf.transform,
            new Vector3(hatchX, HatchY, hatchZ), Quaternion.identity,
            new Vector3(holeX - 0.1f, 0.08f, holeZ - 0.1f), 0.02f, wood);

        var door = root.AddComponent<GmEstateDoor>();
        door.Configure(AtticHatchId, "Attic Hatch", AtticKeyClueId, "attic key",
            locked: true, barred: false, 85f, leaf.transform, secret: false, openAtStart: false,
            barrierCollider: null, swingAxis: Vector3.right);
        BoxCollider barrier = door.EnsureBarrier(new Vector3(holeX - 0.08f, 0.14f, holeZ - 0.08f));
        barrier.transform.SetParent(root.transform, false);
        barrier.transform.localPosition = Vector3.zero;
        barrier.transform.localRotation = Quaternion.identity;
        return door;
    }

    static GameObject BuildAtticLadder(Transform parent, float hatchX, float hatchZ,
        float holeX, float holeZ)
    {
        var ladder = new GameObject("AtticLadder");
        ladder.transform.SetParent(parent, false);
        // Copy the grand stair's 0.24 m rise / thin tread formula. The first tread sits 0.7 m
        // north of the probe spawn, already inside the well, so the head is in the hole before
        // it reaches the 2F ceiling.
        const int Steps = 13;
        const float Rise = 0.24f;
        const float Run = 0.18f;
        float startZ = 13.5f;
        Material rail = CreateMaterial("EntryHall_AtticLadder",
            new Color(0.2f, 0.1f, 0.045f), 0.03f, 0.3f);
        GmOwnedPropFactory.CreateRoundedProp("AtticLadderRailLeft", ladder.transform,
            new Vector3(hatchX - 0.28f, SecondFloorY + 1.6f, startZ + Steps * Run * 0.45f),
            Quaternion.Euler(-20f, 0f, 0f), new Vector3(0.05f, 3.4f, 0.05f), 0.012f, rail);
        GmOwnedPropFactory.CreateRoundedProp("AtticLadderRailRight", ladder.transform,
            new Vector3(hatchX + 0.28f, SecondFloorY + 1.6f, startZ + Steps * Run * 0.45f),
            Quaternion.Euler(-20f, 0f, 0f), new Vector3(0.05f, 3.4f, 0.05f), 0.012f, rail);
        float treadWidth = Mathf.Min(1.2f, holeX - 0.2f);
        for (int i = 0; i < Steps; i++)
        {
            float top = SecondFloorY + (i + 1) * Rise;
            float z = startZ + i * Run;
            GmOwnedPropFactory.CreateRoundedProp($"AtticLadderRung_{i + 1:00}", ladder.transform,
                new Vector3(hatchX, top - 0.02f, z), Quaternion.identity,
                new Vector3(0.62f, 0.04f, 0.1f), 0.01f, rail);
            AddBarrierCollider(ladder.transform, $"AtticLadderTread_{i + 1:00}",
                new Vector3(hatchX, top - 0.04f, z),
                new Vector3(treadWidth, 0.08f, Run + 0.04f));
        }
        return ladder;
    }

    static void BuildPercivalRoom(Transform parent, Dictionary<string, GameObject> authored,
        Transform lighting)
    {
        var room = new GameObject("PercivalBedroom");
        room.transform.SetParent(parent, false);
        FloorSlab(room.transform, "PercivalFloor", new Vector3(-8.6f, SecondFloorY - 0.1f, 13.2f),
            new Vector3(5.4f, 0.2f, 5.0f));
        CeilingSlab(room.transform, "PercivalCeiling", new Vector3(-8.6f, 6.2f, 13.2f),
            new Vector3(5.4f, 0.2f, 5.0f));
        WallSlab(room.transform, "PercivalWestWall", new Vector3(-11.2f, 4.8f, 13.2f),
            new Vector3(0.3f, 2.8f, 5.0f));
        WallSlab(room.transform, "PercivalNorthWall", new Vector3(-8.6f, 4.8f, 15.6f),
            new Vector3(5.4f, 2.8f, 0.3f));
        WallSlab(room.transform, "PercivalSouthWall", new Vector3(-8.6f, 4.8f, 10.8f),
            new Vector3(5.4f, 2.8f, 0.3f));

        var bed = new GameObject("PercivalBed");
        bed.transform.SetParent(room.transform, false);
        bed.transform.position = new Vector3(-9.6f, SecondFloorY, 13.8f);
        GmOwnedPropFactory.PlacePrefab("Assets/GamesMaster/Props/GothicBed.fbx",
            "PercivalBed", bed.transform, new Vector3(-9.6f, SecondFloorY + 0.7f, 13.8f),
            new Vector3(2.1f, 1.4f, 1.6f), Quaternion.Euler(90f, 90f, 0f),
            ground: true, surfaceY: SecondFloorY,
            overrideMaterial: CreateMaterial("EntryHall_PercivalBed",
                new Color(0.14f, 0.07f, 0.045f), 0.04f, 0.28f));
        bed.AddComponent<BoxCollider>().size = new Vector3(2.1f, 1.2f, 1.6f);
        authored["percival-bed"] = bed;

        GameObject desk = ImportedProp(room.transform, "PercivalDesk", "Table_3",
            new Vector3(-10.05f, SecondFloorY + 0.36f, 12.15f), new Vector3(0.85f, 0.72f, 0.85f),
            Quaternion.Euler(0f, 90f, 0f), "table", surfaceY: SecondFloorY);
        authored["percival-desk"] = desk;
        authored["percival-room"] = room;

        var lamp = new GameObject("PercivalLamp");
        lamp.transform.SetParent(lighting, false);
        lamp.transform.position = new Vector3(-8.6f, 5.05f, 13.2f);
        GmVictorianInteriorKit.Place("Lamp_2", "PercivalSconce", lamp.transform,
            lamp.transform.position, new Vector3(0.28f, 0.46f, 0.32f),
            Quaternion.Euler(0f, 90f, 0f), "lamp");
        authored["percival-lamp"] = lamp;
        authored["percival-lamp-light"] = CreatePointLight(lighting, "PercivalLight",
            new Vector3(-8.35f, 4.95f, 13.2f), 5.5f, 32f, new Color(1.0f, 0.74f, 0.46f));
    }
}
