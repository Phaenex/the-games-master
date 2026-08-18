using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

public static class GmParlorBuilder
{
    public const string SceneId = "parlor";
    public const string DisplayName = "The Parlor";
    public const string ScenePath = "Assets/Scenes/Parlor.unity";

    [MenuItem("GamesMaster/Scenes/Rebuild Parlor")]
    public static void Build()
    {
        GmVictorianInteriorKit.Prepare();
        Scene scene = GmSceneBuildUtility.CreateEmptyScene();
        var systems = new GameObject("SceneSystems");
        GmSceneBuildUtility.MarkScene(systems, SceneId, DisplayName);
        GmParlorRules rules = systems.AddComponent<GmParlorRules>();
        GmParlorShotTour reviewTour = systems.AddComponent<GmParlorShotTour>();
        // The high-contrast settings and pause screens intentionally make >94% of the frame dark.
        // The gold and white text/borders are plainly visible but lie above the generic p95 cut,
        // so the room's evidence threshold uses range >= 1 instead of rejecting that design.
        reviewTour.minimumLuminanceRange = 1;

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

        GmParlorPropBinder tableBinder = gameplay.GetComponentInChildren<GmParlorPropBinder>(true);
        GmParlorPresentationCoordinator tablePresentation =
            gameplay.GetComponentInChildren<GmParlorPresentationCoordinator>(true);
        GmParlorController controller = systems.AddComponent<GmParlorController>();
        if (!controller.TryConfigure(rules, tableBinder, tablePresentation, out string controllerError))
            throw new InvalidOperationException(controllerError);
        GmParlorEvidenceLog evidenceLog = gameplay.GetComponentInChildren<GmParlorEvidenceLog>(true);
        GmParlorFocusView focusView = systems.AddComponent<GmParlorFocusView>();
        if (!focusView.TryConfigure(rules, controller, evidenceLog, out string focusError))
            throw new InvalidOperationException(focusError);
        GmParlorHud hud = systems.AddComponent<GmParlorHud>();
        if (!hud.TryConfigure(focusView, out string hudError))
            throw new InvalidOperationException(hudError);
        GmParlorInput input = systems.AddComponent<GmParlorInput>();
        if (!input.TryConfigure(controller, focusView, out string inputError))
            throw new InvalidOperationException(inputError);
        GmParlorReviewProbe reviewProbe = systems.AddComponent<GmParlorReviewProbe>();
        if (!reviewProbe.TryConfigure(rules, controller, input, focusView, tablePresentation,
            out string reviewProbeError))
            throw new InvalidOperationException(reviewProbeError);

        var composition = new GameObject("Composition");
        GmParlorCompositionPlan.Author(composition, authored);

        // A player, at last. This room had real, tested gameplay and nobody who could reach it.
        //
        // The spawn was first placed where the ReviewCamera used to sit, on the reasoning that it was
        // the one vantage a human had already chosen. That reasoning has a hole in it: a camera has no
        // body. The old spawn (0, 0, -1.35) put the capsule inside PlayerChair's box collider, so the
        // first frame of this room was the inside of an armchair. Behind the chair instead, facing the
        // table across it -- the chair's rear face is z=-1.525, the capsule radius is 0.35, and the
        // drapes are at -3.70, so this sits in the clear band with room on both sides.
        // No HDRP atmosphere at all until now: every room builder had zero Volume/Exposure
        // references against the prologue's 31, so HDRP fell back to AUTOMATIC exposure and
        // opened up until a lamp-lit room rendered as a white box.
        GmInteriorAtmosphere.Apply(null, GmParlorBuilder.SceneId);

        // Seated Player (South Chair). Placing the player directly seated in the player's armchair
        // looking across the baize table at the host Aldric Voss.
        GmPlayerRig.Build(null, new Vector3(0f, 0f, -1.05f), new Vector3(0f, 1.05f, 0f));

        // Arriving from the Entry Hall happens behind the same curtain the ninth bell uses, so the
        // player never watches a room dissolve. Does nothing when the room is entered any other way.
        composition.AddComponent<GmSceneArrival>();

        GmSceneBuildUtility.SaveScene(scene, ScenePath);
        Debug.Log("[GmParlor] BUILD PASS: " + ScenePath);
    }

    static void BuildArchitecture(Transform parent)
    {
        // Floor
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "ParlorFloor";
        floor.transform.SetParent(parent, false);
        floor.transform.position = new Vector3(0f, -0.1f, 0f);
        floor.transform.localScale = new Vector3(8f, 0.2f, 8f);
        GmSceneBuildUtility.ApplyVictorianSurface(floor, "floor", 2.2f);

        // One measured carpet section leaves a deliberate timber border around the room. It is an
        // actual pile mesh, not a paper-thin block with a carpet shader.
        var carpet = new GameObject("ParlorCarpet");
        carpet.transform.SetParent(parent, false);
        GmOwnedPropFactory.PlacePrefab(
            "Assets/ThirdParty/MetalManVictorianInteriors/Carpet_1.fbx",
            "ParlorCarpet", carpet.transform, new Vector3(0f, 0.01f, 0f),
            new Vector3(4.15f, 0f, 5.9f), Quaternion.identity,
            ground: true, surfaceY: 0.01f,
            overrideMaterial: GmVictorianInteriorKit.Surface("carpet", "Parlor_Carpet",
                Vector2.one, new Color(0.30f, 0.045f, 0.055f)));

        // Walls (Mahogany paneling)
        GameObject northWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        northWall.name = "NorthWall";
        northWall.transform.SetParent(parent, false);
        northWall.transform.position = new Vector3(0f, 2f, 4f);
        northWall.transform.localScale = new Vector3(8f, 4f, 0.3f);
        GmSceneBuildUtility.ApplyVictorianSurface(northWall, "wall", 2.6f);

        // The Court doors below are a route, not wall dressing. Split the south wall around a real
        // doorway so the opened leaves frame the threshold instead of floating over a solid box.
        GameObject southWall = new GameObject("SouthWall_CourtBoundary");
        southWall.transform.SetParent(parent, false);
        southWall.transform.position = new Vector3(0f, 2f, -4f);
        WallSegment(southWall.transform, "SouthWallLeft", new Vector3(-2.6f, 0f, 0f),
            new Vector3(2.8f, 4f, 0.3f));
        WallSegment(southWall.transform, "SouthWallRight", new Vector3(2.6f, 0f, 0f),
            new Vector3(2.8f, 4f, 0.3f));
        WallSegment(southWall.transform, "SouthWallHeader", new Vector3(0f, 1.25f, 0f),
            new Vector3(2.4f, 1.5f, 0.3f));

        // A short, unlit passage gives the opened leaves depth instead of exposing HDRP's sky. Its
        // transition catches the player before the blind end, but the geometry remains truthful if
        // that volume is disabled during a review.
        WallSegment(southWall.transform, "CourtPassageFloor", new Vector3(0f, -2.06f, -0.8f),
            new Vector3(2.4f, 0.12f, 1.6f));
        WallSegment(southWall.transform, "CourtPassageCeiling", new Vector3(0f, 0.56f, -0.8f),
            new Vector3(2.4f, 0.12f, 1.6f));
        WallSegment(southWall.transform, "CourtPassageLeft", new Vector3(-1.25f, -0.75f, -0.8f),
            new Vector3(0.1f, 2.5f, 1.6f));
        WallSegment(southWall.transform, "CourtPassageRight", new Vector3(1.25f, -0.75f, -0.8f),
            new Vector3(0.1f, 2.5f, 1.6f));
        WallSegment(southWall.transform, "CourtPassageBlind", new Vector3(0f, -0.75f, -1.65f),
            new Vector3(2.4f, 2.5f, 0.1f));

        GameObject eastWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        eastWall.name = "EastWall_Fireplace";
        eastWall.transform.SetParent(parent, false);
        eastWall.transform.position = new Vector3(4f, 2f, 0f);
        eastWall.transform.localScale = new Vector3(0.3f, 4f, 8f);
        GmSceneBuildUtility.ApplyVictorianSurface(eastWall, "wall", 2.6f);

        GameObject westWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        westWall.name = "WestWall";
        westWall.transform.SetParent(parent, false);
        westWall.transform.position = new Vector3(-4f, 2f, 0f);
        westWall.transform.localScale = new Vector3(0.3f, 4f, 8f);
        GmSceneBuildUtility.ApplyVictorianSurface(westWall, "wall", 2.6f);

        // Ceiling
        GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceiling.name = "Ceiling";
        ceiling.transform.SetParent(parent, false);
        ceiling.transform.position = new Vector3(0f, 4.1f, 0f);
        ceiling.transform.localScale = new Vector3(8f, 0.2f, 8f);
        GmSceneBuildUtility.ApplyVictorianSurface(ceiling, "ceiling", 2.6f);
    }

    static void BuildGameplayProps(Transform parent, Dictionary<string, GameObject> authored)
    {
        // Gaming table (Green Baize)
        GameObject table = ImportedProp(parent, "CardTable", "Table_3",
            new Vector3(0f, 0.38f, 0f), new Vector3(1.6f, 0.76f, 1.2f),
            Quaternion.identity, "table", new Color(0.08f, 0.28f, 0.12f));
        authored["card-table"] = table;

        // Player Chair (South) - remove solid collider so player sits cleanly in chair
        GameObject playerChair = ImportedProp(parent, "PlayerChair", "Chair_1",
            new Vector3(0f, 0.5f, -1.2f), new Vector3(0.65f, 1.0f, 0.65f),
            Quaternion.identity, "chair", new Color(0.35f, 0.15f, 0.10f));
        foreach (Collider c in playerChair.GetComponentsInChildren<Collider>(true))
            UnityEngine.Object.DestroyImmediate(c);

        // Aldric Voss Chair (North)
        GameObject aldricChair = ImportedProp(parent, "AldricChair", "Chair_2",
            new Vector3(0f, 0.6f, 1.2f), new Vector3(0.7f, 1.2f, 0.7f),
            Quaternion.Euler(0f, 180f, 0f), "chair", new Color(0.15f, 0.05f, 0.05f));
        authored["aldric-chair"] = aldricChair;

        GmParlorAldricPresenter aldricPresenter = BuildAldricPerformanceProxy(parent);

        // The canonical match owns 28 cards. Give every identity one physical view instead of the
        // old decorative stack, which could never represent hands, table play, Eyes or a reload.
        Material cardBack = CreateMaterial("Parlor_CardBack", new Color(0.20f, 0.025f, 0.035f), 0f, 0.34f);
        Material cardFace = CreateMaterial("Parlor_CardFace", new Color(0.82f, 0.74f, 0.56f), 0f, 0.20f);
        Material cardEdge = CreateMaterial("Parlor_CardEdge", new Color(0.30f, 0.21f, 0.12f), 0f, 0.18f);
        Material[] suitMaterials =
        {
            CreateMaterial("Parlor_Card_Flames", new Color(0.62f, 0.075f, 0.035f), 0f, 0.28f),
            CreateMaterial("Parlor_Card_Eyes", new Color(0.055f, 0.31f, 0.30f), 0f, 0.28f),
            CreateMaterial("Parlor_Card_Bones", new Color(0.60f, 0.43f, 0.15f), 0f, 0.25f),
            CreateMaterial("Parlor_Card_Teeth", new Color(0.77f, 0.70f, 0.53f), 0f, 0.22f),
        };
        var physicalCards = new GameObject("PhysicalCards");
        physicalCards.transform.SetParent(parent, false);
        physicalCards.transform.position = new Vector3(0f, 0.79f, 0f);
        var cardViews = new List<GmParlorCardView>(GmParlorCore.TotalCards);
        foreach (GmSuit suit in new[] { GmSuit.Flames, GmSuit.Eyes, GmSuit.Teeth, GmSuit.Bones })
        {
            for (int rank = 1; rank <= GmParlorCore.RanksPerSuit; rank++)
            {
                GmCard card = new GmCard(suit, rank);
                cardViews.Add(GmOwnedPropFactory.CreatePhysicalCard(
                    $"Card_{suit}_{rank}", physicalCards.transform, card,
                    cardEdge, cardFace, cardBack, suitMaterials[(int)suit]));
            }
        }
        GmParlorPropBinder binder = physicalCards.AddComponent<GmParlorPropBinder>();
        if (!binder.TryConfigure(cardViews, out string configureError))
            throw new InvalidOperationException(configureError);
        GmParlorPresentationCoordinator coordinator =
            physicalCards.AddComponent<GmParlorPresentationCoordinator>();
        if (!coordinator.TryConfigure(binder, aldricPresenter,
            GmParlorAccessibilityProfile.Default, out string coordinatorError))
            throw new InvalidOperationException(coordinatorError);
        var preview = new GmParlorMatch(42, 1, 0, false);
        preview.Start();
        if (!binder.TryApply(preview.ExportSnapshot(), out string applyError))
            throw new InvalidOperationException(applyError);
        authored["card-deck"] = physicalCards;

        // Table lantern. Lamp_2 is a wall sconce; standing it on the baize produced a giant shade
        // and exposed backplate in every table shot. This owned lantern is a real freestanding prop
        // with enough silhouette and surface detail to survive a close review frame.
        var lamp = new GameObject("BankerLamp");
        lamp.transform.SetParent(parent, false);
        lamp.transform.position = new Vector3(0.55f, 0.76f, 0f);
        GmOwnedPropFactory.PlacePrefab(
            "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Prefabs/SM_Lantern.prefab",
            "ParlorTableLantern", lamp.transform, new Vector3(0.55f, 0.98f, 0f),
            new Vector3(0.30f, 0.44f, 0.30f), Quaternion.identity,
            ground: true, surfaceY: 0.76f);
        authored["banker-lamp"] = lamp;

        // Stone Fireplace & Mantel (East Wall)
        GameObject mantel = ImportedProp(parent, "StoneMantel", "Mantel",
            new Vector3(3.6f, 1.0f, 0f), new Vector3(0.6f, 2.0f, 2.4f),
            Quaternion.Euler(0f, -90f, 0f), "mantel", new Color(0.35f, 0.33f, 0.30f));
        authored["stone-mantel"] = mantel;

        // Three measured log clusters give the fire a readable crossed silhouette. The existing
        // motivated fireplace light supplies the ember colour without turning the logs neon orange.
        var embers = new GameObject("FireEmbers");
        embers.transform.SetParent(mantel.transform, true);
        embers.transform.position = new Vector3(3.28f, 0.20f, 0f);
        for (int log = -1; log <= 1; log++)
        {
            GmOwnedPropFactory.PlacePrefab(
                "Assets/LeartesStudios/WitchVillage/HDRP/Art/Prefabs/SM_WoodLogs_2.prefab",
                $"HearthLogs_{log + 2}", embers.transform,
                new Vector3(3.28f, 0.20f + Mathf.Abs(log) * 0.035f, log * 0.28f),
                new Vector3(0.22f, 0.20f, 0.58f), Quaternion.Euler(90f, log * 13f, 0f),
                ground: true, surfaceY: 0.12f);
        }
        authored["fire-embers"] = embers;
    }

    static GmParlorAldricPresenter BuildAldricPerformanceProxy(Transform parent)
    {
        var proxy = new GameObject("AldricPerformanceProxy");
        proxy.transform.SetParent(parent, false);
        GmParlorEvidenceLog evidenceLog = proxy.AddComponent<GmParlorEvidenceLog>();
        Material coatMaterial = CreateMaterial("Parlor_AldricCoat",
            new Color(0.06f, 0.05f, 0.05f), 0f, 0.78f);
        Material sleeveMaterial = CreateMaterial("Parlor_AldricSleeveProxy",
            new Color(0.075f, 0.055f, 0.05f), 0f, 0.72f);
        Material gloveMaterial = CreateMaterial("Parlor_AldricGloveProxy",
            new Color(0.055f, 0.040f, 0.035f), 0f, 0.64f);
        Material collarMaterial = CreateMaterial("Parlor_AldricCollar",
            new Color(0.85f, 0.82f, 0.76f), 0f, 0.35f);
        Material maskMaterial = CreateMaterial("Parlor_AldricMask",
            new Color(0.12f, 0.10f, 0.09f), 0.15f, 0.65f);
        Material contactMaterial = CreateMaterial("Parlor_AldricContactCue",
            new Color(0.40f, 0.12f, 0.055f), 0.12f, 0.35f);

        // Seated Torso & Shoulders
        var body = new GameObject("AldricBody");
        body.transform.SetParent(proxy.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.98f, 1.15f);
        GmOwnedPropFactory.CreateLocalRoundedProp("CoatTorso", body.transform,
            Vector3.zero, Quaternion.Euler(-6f, 0f, 0f),
            new Vector3(0.58f, 0.72f, 0.36f), 0.06f, coatMaterial);
        GmOwnedPropFactory.CreateLocalRoundedProp("Shoulders", body.transform,
            new Vector3(0f, 0.28f, 0f), Quaternion.identity,
            new Vector3(0.66f, 0.18f, 0.32f), 0.05f, coatMaterial);

        // Head & High Stiff Collar
        var head = new GameObject("AldricHead");
        head.transform.SetParent(proxy.transform, false);
        head.transform.localPosition = new Vector3(0f, 1.38f, 1.10f);
        GmOwnedPropFactory.CreateLocalRoundedProp("HighCollar", head.transform,
            new Vector3(0f, -0.06f, -0.04f), Quaternion.Euler(5f, 0f, 0f),
            new Vector3(0.24f, 0.12f, 0.22f), 0.02f, collarMaterial);
        GmOwnedPropFactory.CreateLocalRoundedProp("HeadSilhouette", head.transform,
            Vector3.zero, Quaternion.Euler(8f, 0f, 0f),
            new Vector3(0.22f, 0.28f, 0.24f), 0.05f, maskMaterial);

        // Left Arm (resting on armrest towards table)
        var leftArm = new GameObject("LeftArmCue");
        leftArm.transform.SetParent(proxy.transform, false);
        leftArm.transform.localPosition = new Vector3(0.28f, 0.88f, 0.82f);
        GmOwnedPropFactory.CreateLocalRoundedProp("LeftSleeve", leftArm.transform,
            new Vector3(0f, 0.02f, -0.15f), Quaternion.Euler(18f, -12f, 0f),
            new Vector3(0.18f, 0.14f, 0.42f), 0.035f, sleeveMaterial);
        GmOwnedPropFactory.CreateLocalRoundedProp("LeftGlove", leftArm.transform,
            new Vector3(0f, -0.06f, -0.36f), Quaternion.Euler(8f, -6f, 0f),
            new Vector3(0.14f, 0.055f, 0.22f), 0.025f, gloveMaterial);

        // Right Arm (active tell/play hand)
        var rightHand = new GameObject("RightHandCue");
        rightHand.transform.SetParent(proxy.transform, false);
        rightHand.transform.localPosition = new Vector3(-0.17f, 0.88f, 0.52f);
        GameObject glove = GmOwnedPropFactory.CreateRoundedProp("GloveProxy", rightHand.transform,
            rightHand.transform.position, Quaternion.Euler(4f, 0f, -7f),
            new Vector3(0.16f, 0.055f, 0.25f), 0.025f, gloveMaterial);
        var gloveRenderer = glove.GetComponent<Renderer>();
        if (gloveRenderer != null) gloveRenderer.enabled = false;

        GameObject sleeve = GmOwnedPropFactory.CreateRoundedProp("SleeveCue", proxy.transform,
            new Vector3(-0.17f, 0.91f, 0.73f), Quaternion.Euler(9f, 0f, 0f),
            new Vector3(0.24f, 0.13f, 0.34f), 0.045f, sleeveMaterial);

        var contact = new GameObject("CardContactCue");
        contact.transform.SetParent(proxy.transform, false);
        contact.transform.localPosition = new Vector3(-0.17f, 0.792f, 0.24f);
        GmOwnedPropFactory.CreateDisc("ContactContrast", contact.transform,
            contact.transform.position, Quaternion.Euler(90f, 0f, 0f),
            new Vector3(0.085f, 0.085f, 0.008f), contactMaterial, 28);
        contact.SetActive(false);

        GmParlorAldricPresenter presenter = proxy.AddComponent<GmParlorAldricPresenter>();
        if (!presenter.TryConfigure(rightHand.transform, sleeve.GetComponent<Renderer>(),
            contact.transform, evidenceLog, out string error))
            throw new InvalidOperationException(error);
        return presenter;
    }

    // The hearth, cabinet and doorway clusters stay as stable logical roots for the composition
    // plan. Their visible children are measured owned assets or deliberately authored prop meshes.
    static void BuildDressing(Transform parent, Dictionary<string, GameObject> authored)
    {
        // Mantel clock, mounted on the west face of the fireplace breast (x 3.30). The full-height
        // grandfather-clock import becomes a featureless sliver at mantel scale. A carved owned
        // picture frame gives the case useful relief around an authored dial, readable numerals,
        // separate hands, and the bespoke broken-stag crest.
        var clock = new GameObject("StagClock");
        clock.transform.SetParent(parent, false);
        clock.transform.position = new Vector3(3.20f, 1.48f, 0f);
        Material clockBrass = CreateMaterial("Parlor_ClockBrass", new Color(0.48f, 0.29f, 0.075f), 0.74f, 0.48f);
        Material clockFace = CreateMaterial("Parlor_ClockFace", new Color(0.82f, 0.68f, 0.42f), 0.03f, 0.28f);
        GmVictorianInteriorKit.Place("Picture_5", "MantelClockFrame", clock.transform,
            new Vector3(3.18f, 1.52f, 0f), new Vector3(0.12f, 0.62f, 0.52f),
            Quaternion.Euler(0f, -90f, 0f), "picture");
        GmOwnedPropFactory.CreateDisc("ClockDial", clock.transform,
            new Vector3(3.105f, 1.52f, 0f), Quaternion.Euler(0f, -90f, 0f),
            new Vector3(0.38f, 0.38f, 0.025f), clockFace);
        AddClockNumerals(clock.transform, new Vector3(3.086f, 1.52f, 0f));
        var hands = new GameObject("ClockHands");
        hands.transform.SetParent(clock.transform, true);
        hands.transform.SetPositionAndRotation(new Vector3(3.078f, 1.52f, 0f),
            Quaternion.Euler(0f, -90f, 0f));
        GameObject hourHand = GmOwnedPropFactory.CreateRoundedProp("HourHand", hands.transform,
            Vector3.zero, Quaternion.identity, new Vector3(0.018f, 0.115f, 0.010f), 0.006f, clockBrass);
        hourHand.transform.localPosition = new Vector3(0f, 0.045f, 0f);
        hourHand.transform.localRotation = Quaternion.Euler(0f, 0f, 4f);
        GameObject minuteHand = GmOwnedPropFactory.CreateRoundedProp("MinuteHand", hands.transform,
            Vector3.zero, Quaternion.identity, new Vector3(0.014f, 0.155f, 0.010f), 0.005f, clockBrass);
        minuteHand.transform.localPosition = new Vector3(0.045f, 0.038f, -0.002f);
        minuteHand.transform.localRotation = Quaternion.Euler(0f, 0f, -38f);
        GmOwnedPropFactory.PlacePrefab("Assets/GamesMaster/Props/BrokenStagCrest.fbx",
            "ClockStagCrest", clock.transform, new Vector3(3.17f, 1.86f, 0f),
            new Vector3(0.15f, 0.17f, 0.11f), Quaternion.Euler(0f, -90f, 0f),
            overrideMaterial: clockBrass);
        authored["stag-clock"] = clock;

        // Sideboard against the west wall (inner face x -3.85). Table_2 was a narrow tea table, not
        // a cabinet, and the hard-coded 1.1m dressing surface left every prop visibly floating.
        var cabinet = new GameObject("BarCabinet");
        cabinet.transform.SetParent(parent, false);
        cabinet.transform.position = new Vector3(-3.42f, 0f, 0f);
        GmOwnedPropFactory.PlacePrefab(
            "Assets/LeartesStudios/WitchVillage/HDRP/Art/Prefabs/SM_Cabinet.prefab",
            "ParlorBarCabinet", cabinet.transform, new Vector3(-3.42f, 0.62f, 0f),
            new Vector3(0.76f, 1.24f, 1.20f), Quaternion.Euler(0f, 90f, 0f),
            ground: true, surfaceY: 0f);
        Bounds cabinetBounds = GmPropPlacementEngine.EncapsulateBounds(cabinet);
        BoxCollider cabinetCollider = cabinet.AddComponent<BoxCollider>();
        cabinetCollider.center = cabinet.transform.InverseTransformPoint(cabinetBounds.center);
        cabinetCollider.size = cabinetBounds.size;
        float cabinetTop = cabinetBounds.max.y;
        authored["bar-cabinet"] = cabinet;

        var decanter = new GameObject("CrystalDecanter");
        decanter.transform.SetParent(parent, false);
        decanter.transform.position = new Vector3(-3.35f, cabinetTop, -0.24f);
        Material decanterGlass = CreateMaterial("Parlor_DecanterAmberGlass",
            new Color(0.42f, 0.18f, 0.035f), 0.04f, 0.92f);
        GmOwnedPropFactory.PlacePrefab(
            "Assets/LeartesStudios/WitchVillage/HDRP/Art/Prefabs/SM_Bottles_2.prefab",
            "CrystalDecanter", decanter.transform, new Vector3(-3.35f, cabinetTop, -0.24f),
            new Vector3(0.24f, 0.36f, 0.24f), Quaternion.identity,
            ground: true, surfaceY: cabinetTop, overrideMaterial: decanterGlass);
        authored["crystal-decanter"] = decanter;

        var letters = new GameObject("SealedLetters");
        letters.transform.SetParent(parent, false);
        letters.transform.position = new Vector3(-3.34f, cabinetTop, 0.25f);
        GmOwnedPropFactory.PlacePrefab(
            "Assets/LeartesStudios/WitchVillage/HDRP/Art/Prefabs/SM_Scrolls_2.prefab",
            "SealedLetters", letters.transform, new Vector3(-3.34f, cabinetTop, 0.25f),
            new Vector3(0.42f, 0.06f, 0.32f), Quaternion.Euler(0f, -8f, 0f),
            ground: true, surfaceY: cabinetTop);
        authored["sealed-letters"] = letters;

        // Complete the room perimeter around the game table. The centre remains open for play and
        // the south axis remains clear for the Court exit; furniture belongs to readable wall and
        // hearth clusters rather than being scattered across the player's route.
        ImportedProp(parent, "NorthBookcaseWest", "BookShelf_1",
            new Vector3(-2.75f, 1.28f, 3.55f), new Vector3(1.30f, 2.56f, 0.62f),
            Quaternion.Euler(0f, 180f, 0f), "bookcase", new Color(0.24f, 0.105f, 0.052f));
        ImportedProp(parent, "NorthBookcaseEast", "BookShelf_1",
            new Vector3(2.75f, 1.28f, 3.55f), new Vector3(1.30f, 2.56f, 0.62f),
            Quaternion.Euler(0f, 180f, 0f), "bookcase", new Color(0.24f, 0.105f, 0.052f));
        ImportedProp(parent, "NorthSettee", "Couch_2",
            new Vector3(0f, 0.48f, 3.22f), new Vector3(2.20f, 0.96f, 0.78f),
            Quaternion.Euler(0f, 180f, 0f), "couch", new Color(0.22f, 0.045f, 0.065f));

        ImportedProp(parent, "HearthChair", "Chair_2",
            new Vector3(2.55f, 0.58f, -2.15f), new Vector3(0.82f, 1.16f, 0.82f),
            Quaternion.Euler(0f, -38f, 0f), "chair", new Color(0.19f, 0.055f, 0.045f));
        GameObject readingTable = ImportedProp(parent, "ReadingSideTable", "Table_3",
            new Vector3(3.18f, 0.34f, -3.05f), new Vector3(0.68f, 0.68f, 0.68f),
            Quaternion.identity, "table", new Color(0.27f, 0.12f, 0.055f));
        GmOwnedPropFactory.PlacePrefab(
            "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Prefabs/SM_Lantern.prefab",
            "ReadingLantern", readingTable.transform, new Vector3(3.18f, 0.86f, -3.05f),
            new Vector3(0.26f, 0.40f, 0.26f), Quaternion.identity,
            ground: true, surfaceY: 0.68f);

        var westMirror = new GameObject("WestMirror");
        westMirror.transform.SetParent(parent, false);
        westMirror.transform.position = new Vector3(-3.72f, 2.28f, 0f);
        GmVictorianInteriorKit.Place("Mirror_1", "WestMirror", westMirror.transform,
            westMirror.transform.position, new Vector3(0.14f, 1.46f, 1.02f),
            Quaternion.Euler(0f, 90f, 0f), "mirror");

        var northPictures = new GameObject("NorthPictureGroup");
        northPictures.transform.SetParent(parent, false);
        for (int picture = -1; picture <= 1; picture++)
        {
            Vector3 position = new Vector3(picture * 0.72f, 2.48f, 3.78f);
            GmVictorianInteriorKit.Place(picture == 0 ? "Picture_8" : "Picture_1",
                $"NorthPicture_{picture + 2}", northPictures.transform, position,
                new Vector3(0.58f, 0.82f, 0.12f), Quaternion.Euler(0f, 180f, 0f), "picture");
        }

        // The onward doors to Court, against the south wall (inner face z -3.85). They begin
        // physically shut and swing from the outer jambs only once the Parlor match is complete.
        var doors = new GameObject("ParlorDoors");
        doors.transform.SetParent(parent, false);
        doors.transform.position = new Vector3(0f, 1.1f, -3.78f);
        Material doorWood = CreateMaterial("Parlor_DoorWood",
            new Color(0.32f, 0.13f, 0.052f), 0.03f, 0.36f);
        Transform leftHinge = null;
        Transform rightHinge = null;
        for (int side = -1; side <= 1; side += 2)
        {
            var hinge = new GameObject(side < 0 ? "ParlorDoorLeftHinge" : "ParlorDoorRightHinge");
            hinge.transform.SetParent(doors.transform, false);
            hinge.transform.position = new Vector3(side * 1.2f, 0f, -3.78f);
            Vector3 center = new Vector3(side * 0.6f, 1.03f, -3.78f);
            GameObject leaf = GmVictorianInteriorKit.PlaceGrounded("Door_1",
                side < 0 ? "ParlorDoorLeft" : "ParlorDoorRight", doors.transform,
                center, new Vector3(1.20f, 2.10f, 0.16f), Quaternion.identity,
                "door", surfaceY: 0f, tintOverride: doorWood.color);
            leaf.transform.SetParent(hinge.transform, true);
            if (side < 0) leftHinge = hinge.transform; else rightHinge = hinge.transform;
        }
        BoxCollider doorCollider = doors.AddComponent<BoxCollider>();
        doorCollider.center = Vector3.zero;
        doorCollider.size = new Vector3(2.4f, 2.2f, 0.16f);

        var transitionObject = new GameObject("CourtTransition");
        transitionObject.transform.SetParent(doors.transform, false);
        transitionObject.transform.position = new Vector3(0f, 1.1f, -4.35f);
        var transitionVolume = transitionObject.AddComponent<BoxCollider>();
        transitionVolume.isTrigger = true;
        transitionVolume.size = new Vector3(2.2f, 2.2f, 0.7f);
        var transition = transitionObject.AddComponent<GmSceneTransitionTrigger>();
        transition.TargetSceneId = GmCourtBuilder.SceneId;
        transition.TargetScenePath = GmCourtBuilder.ScenePath;
        transition.InteractionPrompt = "The hearing is waiting";
        transition.UseCurtain = true;

        var sequenceExit = doors.AddComponent<GmSequenceExit>();
        sequenceExit.Configure(SceneId, transition, transitionVolume, doorCollider,
            leftHinge, rightHinge, new Vector3(0f, -96f, 0f), new Vector3(0f, 96f, 0f),
            authorClosed: true);
        authored["parlor-doors"] = doors;

        GameObject drapes = new GameObject("DamaskDrapes");
        drapes.transform.SetParent(parent, false);
        drapes.transform.position = new Vector3(0f, 0f, -3.70f);
        MakeDrapePanel(drapes.transform, "DrapeLeft", -1.15f);
        MakeDrapePanel(drapes.transform, "DrapeRight", 1.15f);
        authored["damask-drapes"] = drapes;

        var handle = new GameObject("BrassHandle");
        handle.transform.SetParent(parent, false);
        handle.transform.position = new Vector3(0.14f, 1.05f, -3.70f);
        GmOwnedPropFactory.PlacePrefab(
            "Assets/LeartesStudios/HauntedVillage/Art/Prefabs/SM_Handle.prefab",
            "BrassHandle", handle.transform, handle.transform.position,
            new Vector3(0.10f, 0.19f, 0.08f), Quaternion.Euler(90f, 0f, 0f),
            overrideMaterial: clockBrass);
        // Hardware belongs to the leaf. Leaving this under Environment made the doors swing away
        // while the handle remained floating at the centre of the open passage.
        handle.transform.SetParent(rightHinge, true);
        authored["brass-handle"] = handle;
    }

    static void MakeDrapePanel(Transform parent, string name, float localX)
    {
        var panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        Vector3 center = parent.TransformPoint(new Vector3(localX, 1.35f, 0.04f));
        GmOwnedPropFactory.CreateCurtainPanel(name, panel.transform, center, Quaternion.identity,
            new Vector2(1.12f, 2.55f), 0.085f,
            GmVictorianInteriorKit.Surface("carpet", name + "_Damask", new Vector2(1f, 2f),
                new Color(0.27f, 0.035f, 0.055f)), foldCount: 8);
    }

    static void WallSegment(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
        segment.name = name;
        segment.transform.SetParent(parent, false);
        segment.transform.localPosition = position;
        segment.transform.localScale = scale;
        GmSceneBuildUtility.ApplyVictorianSurface(segment, "wall", 2.6f);
    }

    static void AddClockNumerals(Transform parent, Vector3 center)
    {
        var root = new GameObject("ClockNumerals");
        root.transform.SetParent(parent, true);
        root.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, 90f, 0f));
        string[] numerals = { "XII", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI" };
        for (int index = 0; index < numerals.Length; index++)
        {
            // +90 faces the glyph fronts toward the west-facing camera instead of showing their
            // mirrored backs. Local +X then maps to screen-right, keeping I/II/III clockwise.
            float angle = index * Mathf.PI * 2f / numerals.Length;
            var label = new GameObject("ClockNumeral_" + numerals[index]);
            label.transform.SetParent(root.transform, false);
            label.transform.localPosition = new Vector3(Mathf.Sin(angle) * 0.153f,
                Mathf.Cos(angle) * 0.153f, -0.004f);
            TextMesh text = label.AddComponent<TextMesh>();
            text.text = numerals[index];
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.0085f;
            text.fontSize = 64;
            text.color = new Color(0.08f, 0.045f, 0.02f);
            text.richText = false;
        }
    }

    // HDRP renders from HDAdditionalLightData, not from Light.intensity: adding the component and
    // leaving its unit unset abandons the authored number on a field the pipeline never reads. Same
    // unit-then-intensity shape the reviewed Wend builders use. The lumen values are the numbers this
    // room was already authored with; whether they read as this room's mood on screen is Nick's gate.
    static void BuildLighting(Transform parent, Dictionary<string, GameObject> authored)
    {
        // Banker's Lamp Light (Warm yellow downlight)
        GameObject lampLightObj = new GameObject("BankerLampLight");
        lampLightObj.transform.SetParent(parent, false);
        lampLightObj.transform.position = new Vector3(0.55f, 1.15f, 0f);
        lampLightObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        Light lampLight = lampLightObj.AddComponent<Light>();
        lampLight.type = LightType.Spot;
        lampLight.range = 3.5f;
        lampLight.spotAngle = 75f;
        lampLight.color = new Color(1.0f, 0.92f, 0.70f);
        var lampHd = lampLightObj.AddComponent<HDAdditionalLightData>();
        lampHd.lightUnit = LightUnit.Lumen;
        lampHd.intensity = 450f;
        lampHd.range = 3.5f;
        authored["banker-lamp-light"] = lampLightObj;

        // Fireplace glow (Amber point light)
        GameObject fireLightObj = new GameObject("FireplaceLight");
        fireLightObj.transform.SetParent(parent, false);
        fireLightObj.transform.position = new Vector3(3.2f, 0.8f, 0f);
        Light fireLight = fireLightObj.AddComponent<Light>();
        fireLight.type = LightType.Point;
        fireLight.range = 6.0f;
        fireLight.color = new Color(1.0f, 0.55f, 0.15f);
        var fireHd = fireLightObj.AddComponent<HDAdditionalLightData>();
        fireHd.lightUnit = LightUnit.Lumen;
        fireHd.intensity = 300f;
        fireHd.range = 6.0f;
        authored["fireplace-light"] = fireLightObj;

        // A real ceiling practical replaces the invisible ambient fill. Thirty-five lumens from an
        // actual fixture retains the fixed-exposure period look while keeping the table and the host
        // readable outside the lantern's cone.
        var chandelier = new GameObject("ParlorChandelier");
        chandelier.transform.SetParent(parent, false);
        chandelier.transform.position = new Vector3(0f, 3.72f, 0f);
        GmVictorianInteriorKit.Place("Lamp_1_LOD0", "ParlorChandelier", chandelier.transform,
            chandelier.transform.position, new Vector3(0.90f, 0.72f, 0.90f),
            Quaternion.identity, "lamp");
        authored["parlor-chandelier"] = chandelier;

        GameObject ambientObj = new GameObject("AmbientFill");
        ambientObj.transform.SetParent(parent, false);
        ambientObj.transform.position = new Vector3(0f, 3.5f, 0f);
        Light ambientLight = ambientObj.AddComponent<Light>();
        ambientLight.type = LightType.Point;
        ambientLight.range = 10f;
        ambientLight.color = new Color(0.35f, 0.30f, 0.45f);
        var ambientHd = ambientObj.AddComponent<HDAdditionalLightData>();
        ambientHd.lightUnit = LightUnit.Lumen;
        ambientHd.intensity = 50f;
        ambientHd.range = 10f;
        authored["ambient-fill-light"] = ambientObj;

        GmInteriorMoonWindow.Result westMoon = GmInteriorMoonWindow.Build(parent,
            "NorthMoonWindowWest", new Vector3(-2.65f, 3.28f, 3.82f),
            new Vector3(-0.8f, 1.15f, -3.25f), new Vector2(1.10f, 1.18f), 210f);
        authored["north-moon-window-west"] = westMoon.fixture;
        authored["north-moon-light-west"] = westMoon.light;
        GmInteriorMoonWindow.Result eastMoon = GmInteriorMoonWindow.Build(parent,
            "NorthMoonWindowEast", new Vector3(2.65f, 3.28f, 3.82f),
            new Vector3(3.15f, 0.95f, 0f), new Vector2(1.10f, 1.18f), 170f);
        authored["north-moon-window-east"] = eastMoon.fixture;
        authored["north-moon-light-east"] = eastMoon.light;
        GmInteriorMoonWindow.Result courtTransom = GmInteriorMoonWindow.Build(parent,
            "SouthCourtTransom", new Vector3(0f, 3.28f, -3.82f),
            new Vector3(2.85f, 1.05f, 0f), new Vector2(1.75f, 0.78f), 150f,
            Vector3.forward);
        authored["south-court-transom"] = courtTransom.fixture;
        authored["south-court-transom-light"] = courtTransom.light;

        // Two dark review zones need their own period fixtures. These were 100% and near-100% black
        // with every test green, so each gets a visible sconce and a local 35-lumen practical.
        var cabinetFixture = new GameObject("CabinetSconce");
        cabinetFixture.transform.SetParent(parent, false);
        cabinetFixture.transform.position = new Vector3(-3.78f, 2.35f, 0f);
        GmVictorianInteriorKit.Place("Lamp_2", "CabinetSconce", cabinetFixture.transform,
            cabinetFixture.transform.position, new Vector3(0.28f, 0.44f, 0.30f),
            Quaternion.Euler(0f, 90f, 0f), "lamp");
        authored["cabinet-sconce"] = cabinetFixture;

        GameObject cabinetLightObj = new GameObject("CabinetSconceLight");
        cabinetLightObj.transform.SetParent(parent, false);
        cabinetLightObj.transform.position = new Vector3(-3.25f, 2.20f, 0f);
        Light cabinetLight = cabinetLightObj.AddComponent<Light>();
        cabinetLight.type = LightType.Point;
        cabinetLight.range = 4.5f;
        cabinetLight.color = new Color(1f, 0.78f, 0.48f);
        var cabinetHd = cabinetLightObj.AddComponent<HDAdditionalLightData>();
        cabinetHd.lightUnit = LightUnit.Lumen;
        cabinetHd.intensity = 35f;
        cabinetHd.range = 4.5f;
        authored["cabinet-sconce-light"] = cabinetLightObj;

        var entryFixtures = new GameObject("EntrySconces");
        entryFixtures.transform.SetParent(parent, false);
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 position = new Vector3(side * 1.55f, 2.40f, -3.72f);
            GmVictorianInteriorKit.Place("Lamp_2", side < 0 ? "EntrySconceLeft" : "EntrySconceRight",
                entryFixtures.transform, position, new Vector3(0.28f, 0.44f, 0.30f),
                Quaternion.identity, "lamp");
        }
        authored["entry-sconces"] = entryFixtures;

        GameObject entryLightObj = new GameObject("EntrySconceLight");
        entryLightObj.transform.SetParent(parent, false);
        entryLightObj.transform.position = new Vector3(0f, 2.25f, -3.15f);
        Light entryLight = entryLightObj.AddComponent<Light>();
        entryLight.type = LightType.Point;
        entryLight.range = 4.5f;
        entryLight.color = new Color(1f, 0.76f, 0.45f);
        var entryHd = entryLightObj.AddComponent<HDAdditionalLightData>();
        entryHd.lightUnit = LightUnit.Lumen;
        entryHd.intensity = 35f;
        entryHd.range = 4.5f;
        authored["entry-sconce-light"] = entryLightObj;
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
            throw new InvalidOperationException("[GmParlor] HDRP/Lit shader is unavailable");
        var material = new Material(shader) { name = name };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        return material;
    }

    static GameObject ImportedProp(Transform parent, string name, string modelName,
        Vector3 targetCenter, Vector3 targetSize, Quaternion rotation, string materialFamily,
        Color? tintOverride = null)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = targetCenter;

        var collider = root.AddComponent<BoxCollider>();
        collider.size = targetSize;

        GmVictorianInteriorKit.PlaceGrounded(modelName, name, root.transform, targetCenter, targetSize,
            rotation, materialFamily, surfaceY: 0f, tintOverride: tintOverride);
        return root;
    }
}
