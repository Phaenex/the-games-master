using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

public static class GmStudyBuilder
{
    public const string SceneId = "study";
    public const string DisplayName = "Study";
    public const string ScenePath = "Assets/Scenes/Study.unity";

    const string TablePath = "Assets/ThirdParty/MetalManVictorianInteriors/Table_2.fbx";
    const string ChairPath = "Assets/ThirdParty/MetalManVictorianInteriors/Chair_1.fbx";
    const string CarpetPath = "Assets/ThirdParty/MetalManVictorianInteriors/Carpet_1.fbx";
    const string CandlePath = "Assets/LeartesStudios/WitchVillage/HDRP/Art/Meshes/SmallProps/Candles/SM_Candles_1.fbx";
    const float TableTop = 0.86f;
    const float SquareSize = 0.05f;
    const float BoardSurfaceY = TableTop + 0.008f;

    [MenuItem("GamesMaster/Scenes/Rebuild Study")]
    public static void Build()
    {
        using (GmStudyReviewPersistence.BeginScope("builder")) BuildIsolated();
    }

    static void BuildIsolated()
    {
        GmVictorianInteriorKit.Prepare();
        Scene scene = GmSceneBuildUtility.CreateEmptyScene();
        var systems = new GameObject("SceneSystems");
        GmSceneBuildUtility.MarkScene(systems, SceneId, DisplayName);
        GmStudySceneHost host = systems.AddComponent<GmStudySceneHost>();
        host.ConfigureForDirectReview();
        systems.AddComponent<GmStudyInput>();
        GmStudyHud hud = systems.AddComponent<GmStudyHud>();
        GmStudyPresenter presenter = systems.AddComponent<GmStudyPresenter>();
        GmStudyAudio audio = systems.AddComponent<GmStudyAudio>();
        systems.AddComponent<GmStudyShotTour>();

        var sharedAudio = new GameObject("SharedAudio");
        sharedAudio.AddComponent<GmAudioManager>();
        AudioSource sharedSfx = sharedAudio.AddComponent<AudioSource>();
        sharedSfx.playOnAwake = false;
        sharedSfx.loop = false;
        GmAdaptiveIntentAuthoring.Audio(sharedAudio, "study-piece-move", GmAudioIntentKind.Foley,
            GmAudioLoopPolicy.Never,
            "The shared SFX manager plays a piece-placed sound only for a new player decision, never when a Challenge restores the honest board.",
            "study-board");
        var environment = new GameObject("Environment");
        var gameplay = new GameObject("Gameplay");
        var lighting = new GameObject("Lighting");
        var composition = new GameObject("Composition");
        Material lightSquare = Material("StudyLightSquare", new Color(0.82f, 0.76f, 0.62f), 0.28f, 0f);
        Material darkSquare = Material("StudyDarkSquare", new Color(0.24f, 0.16f, 0.11f), 0.28f, 0f);
        Material whitePiece = Material("StudyWhitePiece", new Color(0.88f, 0.84f, 0.74f), 0.42f, 0f);
        Material blackPiece = Material("StudyBlackPiece", new Color(0.08f, 0.07f, 0.07f), 0.32f, 0f);
        Material evidenceBrass = Material("StudyArbiterEvidence", new Color(0.62f, 0.38f, 0.08f), 0.55f, 0.7f);
        Material tableSurface = GmVictorianInteriorKit.Surface("table", "Study_Table_PBR", Vector2.one);
        Material chairSurface = GmVictorianInteriorKit.Surface("chair", "Study_Chair_PBR", Vector2.one);
        Material carpetSurface = GmVictorianInteriorKit.Surface("carpet", "Study_Carpet_PBR", Vector2.one);
        Material candleSurface = CandlePackSurface();
        BuildShell(environment.transform);

        GameObject carpet = GmOwnedPropFactory.PlacePrefab(CarpetPath, "StudyCarpet",
            environment.transform, new Vector3(0f, 0.012f, 0f), new Vector3(4.8f, 0.08f, 5.4f),
            Quaternion.identity, true, 0.01f, carpetSurface);
        GameObject table = GmOwnedPropFactory.PlacePrefab(TablePath, "StudyTable",
            gameplay.transform, new Vector3(0f, 0.43f, 0f), new Vector3(2.45f, 0.84f, 1.42f),
            Quaternion.identity, false, 0f, tableSurface);
        AddInvisibleBox("StudyTableCollision", gameplay.transform, new Vector3(0f, 0.43f, 0f),
            new Vector3(2.38f, 0.84f, 1.34f));
        GameObject playerChair = GmOwnedPropFactory.PlacePrefab(ChairPath, "PlayerChair",
            gameplay.transform, new Vector3(-1.5f, 0.55f, -2.2f), new Vector3(0.8f, 1.1f, 0.8f),
            Quaternion.Euler(0f, 28f, 0f), true, 0f, chairSurface);
        GameObject aldricChair = GmOwnedPropFactory.PlacePrefab(ChairPath, "AldricChair",
            gameplay.transform, new Vector3(0f, 0.55f, 2.05f), new Vector3(0.8f, 1.1f, 0.8f),
            Quaternion.Euler(0f, 180f, 0f), true, 0f, chairSurface);
        GmOwnedPropFactory.PlacePrefab(CandlePath, "TableCandles",
            gameplay.transform, new Vector3(0.82f, TableTop + 0.15f, 0.34f),
            new Vector3(0.24f, 0.3f, 0.24f), Quaternion.identity, true, TableTop, candleSurface);

        var boardRoot = new GameObject("StudyBoard");
        boardRoot.transform.SetParent(gameplay.transform, false);
        boardRoot.transform.position = new Vector3(0f, TableTop, 0f);
        BuildBoardTiles(boardRoot.transform, lightSquare, darkSquare);

        var pieceRoot = new GameObject("StudyPieces");
        pieceRoot.transform.SetParent(gameplay.transform, false);
        Vector3 boardOrigin = new Vector3(0f, TableTop, 0f);
        GmStudyPieceView whiteKing = GmOwnedPropFactory.CreatePhysicalChessPiece("WhiteKing",
            pieceRoot.transform, GmChessPieceType.King, true, whitePiece, evidenceBrass);
        GmStudyPieceView blackKing = GmOwnedPropFactory.CreatePhysicalChessPiece("BlackKing",
            pieceRoot.transform, GmChessPieceType.King, false, blackPiece, evidenceBrass);
        GmStudyPieceView whiteQueen = GmOwnedPropFactory.CreatePhysicalChessPiece("WhiteQueen",
            pieceRoot.transform, GmChessPieceType.Queen, true, whitePiece, evidenceBrass);
        GmStudyPieceView whiteRook = GmOwnedPropFactory.CreatePhysicalChessPiece("WhiteRook",
            pieceRoot.transform, GmChessPieceType.Rook, true, whitePiece, evidenceBrass);
        GmStudyPieceView blackPawn = GmOwnedPropFactory.CreatePhysicalChessPiece("BlackPawn",
            pieceRoot.transform, GmChessPieceType.Pawn, false, blackPiece, evidenceBrass);
        // pieceSurfaceY is a small offset ABOVE boardOrigin.y (SquareToWorld adds it to
        // boardOrigin), not an absolute world height -- passing BoardSurfaceY here double-counted
        // TableTop and floated every piece ~0.87m above the board.
        presenter.BindPieces(whiteKing, blackKing, whiteQueen, whiteRook, blackPawn,
            boardOrigin, SquareSize, 0.009f);

        GameObject stableFixture = GmOwnedPropFactory.PlacePrefab(CandlePath, "TaskLightFixture",
            lighting.transform, new Vector3(-0.75f, TableTop + 0.15f, 0.35f),
            new Vector3(0.24f, 0.3f, 0.24f), Quaternion.identity, true, TableTop, candleSurface);
        GameObject tableLight = CreateLight("StudyTableTaskLight", lighting.transform,
            new Vector3(0f, 2.55f, -0.25f), new Color(1f, 0.68f, 0.36f), 240f,
            LightType.Spot, new Vector3(0f, TableTop, 0f));
        GmAdaptiveIntentAuthoring.Light(tableLight, "study-table-task", GmLightIntentKind.CompositionFill,
            "A stable task light keeps every square and piece readable during choices and evidence review.",
            "task-fixture", "study-board", 2.7f, 3f);
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject fill = CreateLight(side < 0 ? "StudyWarmFillWest" : "StudyWarmFillEast",
                lighting.transform, new Vector3(side * 2.2f, 2.2f, -0.8f),
                new Color(1f, 0.48f, 0.22f), 65f, LightType.Point, Vector3.zero);
            GmAdaptiveIntentAuthoring.Light(fill, side < 0 ? "study-fill-west" : "study-fill-east",
                GmLightIntentKind.CompositionFill,
                "A stable low warm bounce reveals chair and wood silhouettes while the central task pool preserves board contrast.",
                "", "study-table", 1f, 5f);
        }
        GameObject boardFill = CreateLight("StudyBoardReadFill", lighting.transform,
            new Vector3(0f, 1.28f, -1.0f), new Color(1f, 0.72f, 0.48f), 24f,
            LightType.Point, Vector3.zero);
        GmAdaptiveIntentAuthoring.Light(boardFill, "study-board-fill",
            GmLightIntentKind.CompositionFill,
            "A low stable bounce separates piece silhouettes from the board without flattening the overhead task pool.",
            "", "study-board", 1f, 2f);
        for (int side = -1; side <= 1; side += 2)
        {
            GmOwnedPropFactory.PlacePrefab(CandlePath,
                side < 0 ? "WestDecorativeCandles" : "EastDecorativeCandles", lighting.transform,
                new Vector3(side * 3.25f, 1.3f, 1.75f), new Vector3(0.25f, 0.32f, 0.25f),
                Quaternion.identity, false, 0f, candleSurface);
            GameObject flame = CreateLight(side < 0 ? "WestDecorativeFlicker" : "EastDecorativeFlicker",
                lighting.transform, new Vector3(side * 3.25f, 1.75f, 1.75f),
                new Color(1f, 0.48f, 0.20f), 115f, LightType.Point, Vector3.zero);
            flame.AddComponent<GmPeriodLampFlicker>().baseIntensity = 115f;
            GmAdaptiveIntentAuthoring.Light(flame, side < 0 ? "study-west-candle" : "study-east-candle",
                GmLightIntentKind.Environmental,
                "Peripheral candle flutter dresses the room edge without changing the stable board exposure.",
                "", "", 0.8f, 8f);
        }
        BuildDust(environment.transform, new Vector3(-3.15f, 1.4f, 2.85f), "WestCornerDust");
        BuildDust(environment.transform, new Vector3(3.15f, 1.2f, 2.65f), "EastCornerDust");

        var clearance = new GameObject("PlayerTableClearance");
        clearance.transform.SetParent(gameplay.transform, false);
        clearance.transform.position = new Vector3(0f, 1f, -1.85f);
        clearance.transform.localScale = new Vector3(1.4f, 1.8f, 1.25f);
        GmStudyCompositionPlan.Author(composition, table, boardRoot, carpet, playerChair,
            aldricChair, stableFixture, tableLight);
        GmInteriorAtmosphere.Apply(null, SceneId);
        GmPlayerRig.Build(null, new Vector3(0f, 0f, -3.15f), new Vector3(0f, 1.05f, 0f));

        GmRunStore.BeginNewRun();
        host.InitializeCurrentRun();
        hud.BuildForTests();
        GmSceneBuildUtility.SaveScene(scene, ScenePath);
        Debug.Log("[GmStudy] BUILD PASS: " + ScenePath);
    }

    static void BuildBoardTiles(Transform parent, Material lightSquare, Material darkSquare)
    {
        for (int file = 0; file < 8; file++)
        {
            for (int rank = 0; rank < 8; rank++)
            {
                bool light = (file + rank) % 2 == 0;
                GameObject tile = GmOwnedPropFactory.CreateLocalRoundedProp($"BoardTile_{file}_{rank}",
                    parent, new Vector3((file - 3.5f) * SquareSize, 0.004f, (rank - 3.5f) * SquareSize),
                    Quaternion.identity, new Vector3(SquareSize * 0.98f, 0.008f, SquareSize * 0.98f),
                    0.002f, light ? lightSquare : darkSquare);
                tile.AddComponent<GmStudyBoardTile>().Configure(file, rank);
            }
        }
    }

    static void BuildShell(Transform parent)
    {
        Architecture("StudyFloor", parent, new Vector3(0f, -0.1f, 0f), new Vector3(8f, 0.2f, 8f), "floor", 2.2f);
        Architecture("NorthWall", parent, new Vector3(0f, 2.5f, 4f), new Vector3(8f, 5f, 0.2f), "wall", 2.6f);
        Architecture("SouthWall", parent, new Vector3(0f, 2.5f, -4f), new Vector3(8f, 5f, 0.2f), "wall", 2.6f);
        Architecture("WestWall", parent, new Vector3(-4f, 2.5f, 0f), new Vector3(0.2f, 5f, 8f), "wall", 2.6f);
        Architecture("EastWall", parent, new Vector3(4f, 2.5f, 0f), new Vector3(0.2f, 5f, 8f), "wall", 2.6f);
        Architecture("StudyCeiling", parent, new Vector3(0f, 5f, 0f), new Vector3(8f, 0.2f, 8f), "ceiling", 2.6f);
    }

    static void Architecture(string name, Transform parent, Vector3 position, Vector3 scale,
        string surfaceFamily, float metresPerTile)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name; go.transform.SetParent(parent, false);
        go.transform.position = position; go.transform.localScale = scale;
        GmSceneBuildUtility.ApplyVictorianSurface(go, surfaceFamily, metresPerTile);
    }

    static void AddInvisibleBox(string name, Transform parent, Vector3 position, Vector3 size)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = position;
        go.AddComponent<BoxCollider>().size = size;
    }

    static void BuildDust(Transform parent, Vector3 position, string name)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = position;
        go.AddComponent<GmStudyDustField>().Configure(Vector3.zero, 1.35f);
        ParticleSystem particles = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = true; main.startLifetime = 8f; main.startSpeed = 0.018f;
        main.startSize = 0.012f; main.maxParticles = 70;
        main.startColor = new Color(0.72f, 0.62f, 0.44f, 0.24f);
        ParticleSystem.EmissionModule emission = particles.emission; emission.rateOverTime = 4f;
        ParticleSystem.ShapeModule shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.8f, 2.2f, 0.8f);
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Material("StudyDustMotes",
            new Color(0.8f, 0.7f, 0.48f, 0.3f), 0f, 0f, "HDRP/Unlit");
    }

    static GameObject CreateLight(string name, Transform parent, Vector3 position, Color color,
        float lumens, LightType type, Vector3 target)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = position;
        if (type == LightType.Spot) go.transform.rotation = Quaternion.LookRotation(target - position);
        Light light = go.AddComponent<Light>();
        light.type = type; light.color = color; light.lightUnit = LightUnit.Lumen;
        light.intensity = lumens; light.range = 7f; light.spotAngle = 62f;
        go.AddComponent<HDAdditionalLightData>();
        return go;
    }

    static Material Material(string name, Color color, float smoothness, float metallic,
        string shaderName = "HDRP/Lit")
    {
        var material = new Material(Shader.Find(shaderName)) { name = name, color = color };
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        return material;
    }

    static Material CandlePackSurface()
    {
        const string textureRoot = "Assets/LeartesStudios/WitchVillage/HDRP/Art/Textures/";
        Shader shader = Shader.Find("HDRP/Lit");
        if (shader == null) throw new System.InvalidOperationException("[GmStudy] HDRP/Lit is unavailable");
        var material = new Material(shader) { name = "Study_Candles_Pack_PBR" };
        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(textureRoot + "T_Candles_B.PNG");
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(textureRoot + "T_Candles_N.PNG");
        Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(textureRoot + "T_Candles_ORM.PNG");
        if (albedo == null || normal == null || mask == null)
            throw new System.InvalidOperationException("[GmStudy] Witch Village candle PBR textures are missing");
        material.SetTexture("_BaseColorMap", albedo);
        material.SetTexture("_NormalMap", normal);
        material.SetTexture("_MaskMap", mask);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", 0.32f);
        material.SetFloat("_Metallic", 0f);
        return material;
    }
}
