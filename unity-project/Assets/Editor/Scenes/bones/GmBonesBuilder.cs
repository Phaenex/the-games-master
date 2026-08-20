using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

public static class GmBonesBuilder
{
    public const string SceneId = "bones";
    public const string DisplayName = "Bones";
    public const string ScenePath = "Assets/Scenes/Bones.unity";

    const string TablePath = "Assets/ThirdParty/MetalManVictorianInteriors/Table_2.fbx";
    const string ChairPath = "Assets/ThirdParty/MetalManVictorianInteriors/Chair_1.fbx";
    const string CarpetPath = "Assets/ThirdParty/MetalManVictorianInteriors/Carpet_1.fbx";
    const string CandlePath = "Assets/LeartesStudios/WitchVillage/HDRP/Art/Meshes/SmallProps/Candles/SM_Candles_1.fbx";
    const float TableTop = 0.86f;

    [MenuItem("GamesMaster/Scenes/Rebuild Bones")]
    public static void Build()
    {
        Scene scene = GmSceneBuildUtility.CreateEmptyScene();
        var systems = new GameObject("SceneSystems");
        GmSceneBuildUtility.MarkScene(systems, SceneId, DisplayName);
        GmBonesSceneHost host = systems.AddComponent<GmBonesSceneHost>();
        systems.AddComponent<GmBonesInput>();
        GmBonesHud hud = systems.AddComponent<GmBonesHud>();
        GmBonesPresenter presenter = systems.AddComponent<GmBonesPresenter>();
        GmBonesAudio audio = systems.AddComponent<GmBonesAudio>();
        systems.AddComponent<GmBonesShotTour>();
        GmAdaptiveIntentAuthoring.Audio(audio.gameObject, "bones-dice-roll", GmAudioIntentKind.Foley,
            GmAudioLoopPolicy.Never,
            "The existing bone-dice roll is heard only when a new throw becomes visible, never when a Challenge corrects evidence.",
            "bones-dice");

        var environment = new GameObject("Environment");
        var gameplay = new GameObject("Gameplay");
        var lighting = new GameObject("Lighting");
        var composition = new GameObject("Composition");
        Material wall = Material("BonesWallpaper", new Color(0.19f, 0.14f, 0.12f), 0.18f, 0f);
        Material wood = Material("BonesFloorboards", new Color(0.16f, 0.09f, 0.055f), 0.28f, 0f);
        Material bone = Material("AgedBone", new Color(0.88f, 0.82f, 0.68f), 0.38f, 0f);
        Material pip = Material("CutPips", new Color(0.035f, 0.025f, 0.018f), 0.16f, 0f);
        Material brass = Material("AlteredBrassEvidence", new Color(0.62f, 0.38f, 0.08f), 0.55f, 0.7f);
        Material wax = Material("PeriodCandleWax", new Color(0.58f, 0.46f, 0.28f), 0.24f, 0f);
        Material mahogany = Material("BonesMahogany", new Color(0.29f, 0.105f, 0.045f), 0.42f, 0f);
        Material burgundy = Material("BonesBurgundyCarpet", new Color(0.24f, 0.035f, 0.045f), 0.22f, 0f);
        BuildShell(environment.transform, wall, wood);

        GameObject carpet = GmOwnedPropFactory.PlacePrefab(CarpetPath, "BonesCarpet",
            environment.transform, new Vector3(0f, 0.012f, 0f), new Vector3(4.8f, 0.08f, 5.4f),
            Quaternion.identity, true, 0.01f, burgundy);
        GameObject table = GmOwnedPropFactory.PlacePrefab(TablePath, "BonesTable",
            gameplay.transform, new Vector3(0f, 0.43f, 0f), new Vector3(2.45f, 0.84f, 1.42f),
            Quaternion.identity, false, 0f, mahogany);
        AddInvisibleBox("BonesTableCollision", gameplay.transform, new Vector3(0f, 0.43f, 0f),
            new Vector3(2.38f, 0.84f, 1.34f));
        GameObject playerChair = GmOwnedPropFactory.PlacePrefab(ChairPath, "PlayerChair",
            gameplay.transform, new Vector3(-1.5f, 0.55f, -2.2f), new Vector3(0.8f, 1.1f, 0.8f),
            Quaternion.Euler(0f, 28f, 0f), true, 0f, mahogany);
        GameObject aldricChair = GmOwnedPropFactory.PlacePrefab(ChairPath, "AldricChair",
            gameplay.transform, new Vector3(0f, 0.55f, 2.05f), new Vector3(0.8f, 1.1f, 0.8f),
            Quaternion.Euler(0f, 180f, 0f), true, 0f, mahogany);
        GmOwnedPropFactory.PlacePrefab(CandlePath, "TableCandles",
            gameplay.transform, new Vector3(0.82f, TableTop + 0.15f, 0.34f),
            new Vector3(0.24f, 0.3f, 0.24f), Quaternion.identity, true, TableTop, wax);

        var diceRoot = new GameObject("BonesDice");
        diceRoot.transform.SetParent(gameplay.transform, false);
        var dice = new GmBonesDieView[3];
        for (int index = 0; index < dice.Length; index++)
        {
            dice[index] = GmOwnedPropFactory.CreatePhysicalDie($"PhysicalDie_{index + 1}",
                diceRoot.transform, new Vector3((index - 1) * 0.21f, TableTop + 0.055f,
                    index == 1 ? 0.02f : -0.04f), bone, pip, brass);
            dice[index].SetFace(index + 1, false, true);
        }
        presenter.BindDice(dice);

        GameObject stableFixture = GmOwnedPropFactory.PlacePrefab(CandlePath, "TaskLightFixture",
            lighting.transform, new Vector3(-0.75f, TableTop + 0.15f, 0.35f),
            new Vector3(0.24f, 0.3f, 0.24f), Quaternion.identity, true, TableTop, wax);
        GameObject tableLight = CreateLight("BonesTableTaskLight", lighting.transform,
            new Vector3(0f, 2.55f, -0.25f), new Color(1f, 0.68f, 0.36f), 240f,
            LightType.Spot, new Vector3(0f, TableTop, 0f));
        GmAdaptiveIntentAuthoring.Light(tableLight, "bones-table-task", GmLightIntentKind.CompositionFill,
            "A stable task light keeps all three faces readable during choices and evidence review.",
            "task-fixture", "bones-dice", 2.7f, 3f);
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject fill = CreateLight(side < 0 ? "BonesWarmFillWest" : "BonesWarmFillEast",
                lighting.transform, new Vector3(side * 2.2f, 2.2f, -0.8f),
                new Color(1f, 0.48f, 0.22f), 65f, LightType.Point, Vector3.zero);
            GmAdaptiveIntentAuthoring.Light(fill, side < 0 ? "bones-fill-west" : "bones-fill-east",
                GmLightIntentKind.CompositionFill,
                "A stable low warm bounce reveals chair and wood silhouettes while the central task pool preserves face contrast.",
                "", "bones-table", 1f, 5f);
        }
        GameObject faceFill = CreateLight("BonesDieFaceReadFill", lighting.transform,
            new Vector3(0f, 1.28f, -1.0f), new Color(1f, 0.72f, 0.48f), 24f,
            LightType.Point, Vector3.zero);
        GmAdaptiveIntentAuthoring.Light(faceFill, "bones-die-face-fill",
            GmLightIntentKind.CompositionFill,
            "A low stable bounce separates the front pip cuts from the bone body without flattening the overhead task pool.",
            "", "bones-dice", 1f, 2f);
        for (int side = -1; side <= 1; side += 2)
        {
            GmOwnedPropFactory.PlacePrefab(CandlePath,
                side < 0 ? "WestDecorativeCandles" : "EastDecorativeCandles", lighting.transform,
                new Vector3(side * 3.25f, 1.3f, 1.75f), new Vector3(0.25f, 0.32f, 0.25f),
                Quaternion.identity, false, 0f, wax);
            GameObject flame = CreateLight(side < 0 ? "WestDecorativeFlicker" : "EastDecorativeFlicker",
                lighting.transform, new Vector3(side * 3.25f, 1.75f, 1.75f),
                new Color(1f, 0.48f, 0.20f), 115f, LightType.Point, Vector3.zero);
            flame.AddComponent<GmPeriodLampFlicker>().baseIntensity = 115f;
            GmAdaptiveIntentAuthoring.Light(flame, side < 0 ? "bones-west-candle" : "bones-east-candle",
                GmLightIntentKind.Environmental,
                "Peripheral candle flutter dresses the room edge without changing the stable dice exposure.",
                "", "", 0.8f, 8f);
        }
        BuildDust(environment.transform, new Vector3(-3.15f, 1.4f, 2.85f), "WestCornerDust");
        BuildDust(environment.transform, new Vector3(3.15f, 1.2f, 2.65f), "EastCornerDust");

        var clearance = new GameObject("PlayerTableClearance");
        clearance.transform.SetParent(gameplay.transform, false);
        clearance.transform.position = new Vector3(0f, 1f, -1.85f);
        clearance.transform.localScale = new Vector3(1.4f, 1.8f, 1.25f);
        GmBonesCompositionPlan.Author(composition, table, diceRoot, carpet, playerChair,
            aldricChair, stableFixture, tableLight);
        GmInteriorAtmosphere.Apply(null, SceneId);
        GmPlayerRig.Build(null, new Vector3(0f, 0f, -3.15f), new Vector3(0f, 1.05f, 0f));

        GmRunStore.BeginNewRun();
        host.InitializeCurrentRun();
        hud.BuildForTests();
        GmSceneBuildUtility.SaveScene(scene, ScenePath);
        Debug.Log("[GmBones] BUILD PASS: " + ScenePath);
    }

    static void BuildShell(Transform parent, Material wall, Material floor)
    {
        Architecture("BonesFloor", parent, new Vector3(0f, -0.1f, 0f), new Vector3(8f, 0.2f, 8f), floor);
        Architecture("NorthWall", parent, new Vector3(0f, 2.5f, 4f), new Vector3(8f, 5f, 0.2f), wall);
        Architecture("SouthWall", parent, new Vector3(0f, 2.5f, -4f), new Vector3(8f, 5f, 0.2f), wall);
        Architecture("WestWall", parent, new Vector3(-4f, 2.5f, 0f), new Vector3(0.2f, 5f, 8f), wall);
        Architecture("EastWall", parent, new Vector3(4f, 2.5f, 0f), new Vector3(0.2f, 5f, 8f), wall);
        Architecture("BonesCeiling", parent, new Vector3(0f, 5f, 0f), new Vector3(8f, 0.2f, 8f), wall);
    }

    static void Architecture(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name; go.transform.SetParent(parent, false);
        go.transform.position = position; go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    static void AddInvisibleBox(string name, Transform parent, Vector3 position, Vector3 size)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = position;
        go.AddComponent<BoxCollider>().size = size;
    }

    static void BuildDust(Transform parent, Vector3 position, string name)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = position;
        go.AddComponent<GmBonesDustField>().Configure(Vector3.zero, 1.35f);
        ParticleSystem particles = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = true; main.startLifetime = 8f; main.startSpeed = 0.018f;
        main.startSize = 0.012f; main.maxParticles = 70;
        main.startColor = new Color(0.72f, 0.62f, 0.44f, 0.24f);
        ParticleSystem.EmissionModule emission = particles.emission; emission.rateOverTime = 4f;
        ParticleSystem.ShapeModule shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.8f, 2.2f, 0.8f);
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Material("DustMotes",
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
}
