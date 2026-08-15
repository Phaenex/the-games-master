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
        Scene scene = GmSceneBuildUtility.CreateEmptyScene();
        var systems = new GameObject("SceneSystems");
        GmSceneBuildUtility.MarkScene(systems, SceneId, DisplayName);
        systems.AddComponent<GmParlorRules>();
        systems.AddComponent<GmHostAI>();
        systems.AddComponent<GmTheReadController>();
        systems.AddComponent<GmParlorShotTour>();

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

        var composition = new GameObject("Composition");
        GmParlorCompositionPlan.Author(composition, authored);

        // A player, at last. This room had real, tested gameplay and nobody who could reach
        // it. Spawn is the viewpoint the ReviewCamera used to sit at -- the one vantage a
        // human already chose for this room -- so it is the least arbitrary spawn available,
        // and the review tour prefers the player's own camera when a player exists.
        GmPlayerRig.Build(null, new Vector3(0f, 0f, -1.35f), new Vector3(0f, 1.0f, 0f));

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
        ApplyMaterial(floor, "HDRP/Lit", new Color(0.24f, 0.16f, 0.10f), 0.05f, 0.45f);

        // Carpet
        GameObject carpet = GameObject.CreatePrimitive(PrimitiveType.Cube);
        carpet.name = "ParlorCarpet";
        carpet.transform.SetParent(parent, false);
        carpet.transform.position = new Vector3(0f, 0.01f, 0f);
        carpet.transform.localScale = new Vector3(6f, 0.02f, 6f);
        ApplyMaterial(carpet, "HDRP/Lit", new Color(0.40f, 0.12f, 0.12f), 0.0f, 0.15f);

        // Walls (Mahogany paneling)
        GameObject northWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        northWall.name = "NorthWall";
        northWall.transform.SetParent(parent, false);
        northWall.transform.position = new Vector3(0f, 2f, 4f);
        northWall.transform.localScale = new Vector3(8f, 4f, 0.3f);
        ApplyMaterial(northWall, "HDRP/Lit", new Color(0.20f, 0.12f, 0.08f), 0.05f, 0.4f);

        GameObject southWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        southWall.name = "SouthWall";
        southWall.transform.SetParent(parent, false);
        southWall.transform.position = new Vector3(0f, 2f, -4f);
        southWall.transform.localScale = new Vector3(8f, 4f, 0.3f);
        ApplyMaterial(southWall, "HDRP/Lit", new Color(0.20f, 0.12f, 0.08f), 0.05f, 0.4f);

        GameObject eastWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        eastWall.name = "EastWall_Fireplace";
        eastWall.transform.SetParent(parent, false);
        eastWall.transform.position = new Vector3(4f, 2f, 0f);
        eastWall.transform.localScale = new Vector3(0.3f, 4f, 8f);
        ApplyMaterial(eastWall, "HDRP/Lit", new Color(0.20f, 0.12f, 0.08f), 0.05f, 0.4f);

        GameObject westWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        westWall.name = "WestWall";
        westWall.transform.SetParent(parent, false);
        westWall.transform.position = new Vector3(-4f, 2f, 0f);
        westWall.transform.localScale = new Vector3(0.3f, 4f, 8f);
        ApplyMaterial(westWall, "HDRP/Lit", new Color(0.20f, 0.12f, 0.08f), 0.05f, 0.4f);

        // Ceiling
        GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceiling.name = "Ceiling";
        ceiling.transform.SetParent(parent, false);
        ceiling.transform.position = new Vector3(0f, 4.1f, 0f);
        ceiling.transform.localScale = new Vector3(8f, 0.2f, 8f);
        ApplyMaterial(ceiling, "HDRP/Lit", new Color(0.18f, 0.12f, 0.09f), 0.0f, 0.2f);
    }

    static void BuildGameplayProps(Transform parent, Dictionary<string, GameObject> authored)
    {
        // Gaming table (Green Baize)
        GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        table.name = "CardTable";
        table.transform.SetParent(parent, false);
        table.transform.position = new Vector3(0f, 0.38f, 0f);
        table.transform.localScale = new Vector3(1.6f, 0.38f, 1.6f);
        ApplyMaterial(table, "HDRP/Lit", new Color(0.08f, 0.28f, 0.12f), 0.0f, 0.2f); // Green felt
        authored["card-table"] = table;

        // Player Chair (South)
        GameObject playerChair = GameObject.CreatePrimitive(PrimitiveType.Cube);
        playerChair.name = "PlayerChair";
        playerChair.transform.SetParent(parent, false);
        playerChair.transform.position = new Vector3(0f, 0.5f, -1.2f);
        playerChair.transform.localScale = new Vector3(0.65f, 1.0f, 0.65f);
        ApplyMaterial(playerChair, "HDRP/Lit", new Color(0.35f, 0.15f, 0.10f), 0.05f, 0.3f);

        // Aldric Voss Chair (North)
        GameObject aldricChair = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aldricChair.name = "AldricChair";
        aldricChair.transform.SetParent(parent, false);
        aldricChair.transform.position = new Vector3(0f, 0.5f, 1.2f);
        aldricChair.transform.localScale = new Vector3(0.7f, 1.2f, 0.7f);
        ApplyMaterial(aldricChair, "HDRP/Lit", new Color(0.15f, 0.05f, 0.05f), 0.1f, 0.4f);
        authored["aldric-chair"] = aldricChair;

        // Card Deck placeholder on table
        GameObject deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
        deck.name = "CardDeck";
        deck.transform.SetParent(table.transform, false);
        deck.transform.localPosition = new Vector3(0.2f, 1.05f, 0f);
        deck.transform.localScale = new Vector3(0.15f, 0.05f, 0.22f);
        ApplyMaterial(deck, "HDRP/Lit", new Color(0.85f, 0.85f, 0.8f), 0.0f, 0.6f);
        authored["card-deck"] = deck;

        // Banker's lamp
        GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        lamp.name = "BankerLamp";
        lamp.transform.SetParent(parent, false);
        lamp.transform.position = new Vector3(0.55f, 0.85f, 0f);
        lamp.transform.localScale = new Vector3(0.12f, 0.15f, 0.12f);
        ApplyMaterial(lamp, "HDRP/Lit", new Color(0.1f, 0.45f, 0.2f), 0.3f, 0.85f); // Green glass
        authored["banker-lamp"] = lamp;

        // Stone Fireplace & Mantel (East Wall)
        GameObject mantel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mantel.name = "StoneMantel";
        mantel.transform.SetParent(parent, false);
        mantel.transform.position = new Vector3(3.6f, 1.0f, 0f);
        mantel.transform.localScale = new Vector3(0.6f, 2.0f, 2.4f);
        ApplyMaterial(mantel, "HDRP/Lit", new Color(0.35f, 0.33f, 0.30f), 0.0f, 0.3f);
        authored["stone-mantel"] = mantel;

        // Fire embers
        GameObject embers = GameObject.CreatePrimitive(PrimitiveType.Cube);
        embers.name = "FireEmbers";
        embers.transform.SetParent(mantel.transform, false);
        embers.transform.localPosition = new Vector3(-0.2f, -0.3f, 0f);
        embers.transform.localScale = new Vector3(0.4f, 0.2f, 1.2f);
        ApplyMaterial(embers, "HDRP/Lit", new Color(1.0f, 0.35f, 0.05f), 0.0f, 0.1f);
        authored["fire-embers"] = embers;
    }

    // The hearth, cabinet and doorway clusters the composition plan declares were never built, so
    // six of its elements had nothing to attach to. Blockout primitives in the same idiom as the
    // props above, placed where the review tour already looks for them.
    static void BuildDressing(Transform parent, Dictionary<string, GameObject> authored)
    {
        // Mantel clock, mounted on the west face of the fireplace breast (x 3.30)
        GameObject clock = GameObject.CreatePrimitive(PrimitiveType.Cube);
        clock.name = "StagClock";
        clock.transform.SetParent(parent, false);
        clock.transform.position = new Vector3(3.22f, 1.45f, 0f);
        clock.transform.localScale = new Vector3(0.16f, 0.34f, 0.30f);
        ApplyMaterial(clock, "HDRP/Lit", new Color(0.52f, 0.38f, 0.14f), 0.7f, 0.6f); // Tarnished brass
        authored["stag-clock"] = clock;

        // Sideboard against the west wall (inner face x -3.85)
        GameObject cabinet = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cabinet.name = "BarCabinet";
        cabinet.transform.SetParent(parent, false);
        cabinet.transform.position = new Vector3(-3.4f, 0.55f, 0f);
        cabinet.transform.localScale = new Vector3(0.55f, 1.1f, 2.0f);
        ApplyMaterial(cabinet, "HDRP/Lit", new Color(0.19f, 0.11f, 0.07f), 0.05f, 0.45f);
        authored["bar-cabinet"] = cabinet;

        GameObject decanter = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        decanter.name = "CrystalDecanter";
        decanter.transform.SetParent(parent, false);
        decanter.transform.position = new Vector3(-3.4f, 1.24f, -0.35f);
        decanter.transform.localScale = new Vector3(0.14f, 0.14f, 0.14f);
        ApplyMaterial(decanter, "HDRP/Lit", new Color(0.62f, 0.38f, 0.12f), 0.0f, 0.95f); // Amber crystal
        authored["crystal-decanter"] = decanter;

        GameObject letters = GameObject.CreatePrimitive(PrimitiveType.Cube);
        letters.name = "SealedLetters";
        letters.transform.SetParent(parent, false);
        letters.transform.position = new Vector3(-3.4f, 1.115f, 0.45f);
        letters.transform.localScale = new Vector3(0.26f, 0.03f, 0.19f);
        ApplyMaterial(letters, "HDRP/Lit", new Color(0.78f, 0.72f, 0.60f), 0.0f, 0.25f);
        authored["sealed-letters"] = letters;

        // Doorway back to the Entry Hall, against the south wall (inner face z -3.85)
        GameObject doors = GameObject.CreatePrimitive(PrimitiveType.Cube);
        doors.name = "ParlorDoors";
        doors.transform.SetParent(parent, false);
        doors.transform.position = new Vector3(0f, 1.1f, -3.78f);
        doors.transform.localScale = new Vector3(1.7f, 2.2f, 0.12f);
        ApplyMaterial(doors, "HDRP/Lit", new Color(0.16f, 0.09f, 0.06f), 0.05f, 0.35f);
        authored["parlor-doors"] = doors;

        GameObject drapes = new GameObject("DamaskDrapes");
        drapes.transform.SetParent(parent, false);
        drapes.transform.position = new Vector3(0f, 0f, -3.70f);
        MakeDrapePanel(drapes.transform, "DrapeLeft", -1.15f);
        MakeDrapePanel(drapes.transform, "DrapeRight", 1.15f);
        authored["damask-drapes"] = drapes;

        GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        handle.name = "BrassHandle";
        handle.transform.SetParent(parent, false);
        handle.transform.position = new Vector3(0.14f, 1.05f, -3.70f);
        handle.transform.localScale = new Vector3(0.06f, 0.16f, 0.05f);
        ApplyMaterial(handle, "HDRP/Lit", new Color(0.58f, 0.44f, 0.16f), 0.8f, 0.7f);
        authored["brass-handle"] = handle;
    }

    static void MakeDrapePanel(Transform parent, string name, float localX)
    {
        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = name;
        panel.transform.SetParent(parent, false);
        panel.transform.localPosition = new Vector3(localX, 1.35f, 0f);
        panel.transform.localScale = new Vector3(0.5f, 2.7f, 0.14f);
        ApplyMaterial(panel, "HDRP/Lit", new Color(0.28f, 0.06f, 0.08f), 0.0f, 0.12f);
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

        // Subtle ambient ceiling fill
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
