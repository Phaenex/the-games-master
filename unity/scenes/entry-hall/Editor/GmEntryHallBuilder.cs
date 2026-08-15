using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

public static class GmEntryHallBuilder
{
    public const string SceneId = "entry-hall";
    public const string DisplayName = "Entry Hall";
    public const string ScenePath = "Assets/Scenes/EntryHall.unity";

    [MenuItem("GamesMaster/Scenes/Rebuild Entry Hall")]
    public static void Build()
    {
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

        var composition = new GameObject("Composition");
        GmEntryHallCompositionPlan.Author(composition, authored);

        // A player, at last. This room had real, tested gameplay and nobody who could reach
        // it. Spawn is the viewpoint the ReviewCamera used to sit at -- the one vantage a
        // human already chose for this room -- so it is the least arbitrary spawn available,
        // and the review tour prefers the player's own camera when a player exists.
        GmPlayerRig.Build(null, new Vector3(0f, 0f, -6f), new Vector3(0f, 1.6f, 0f));
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
        // Floor (Marble)
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "HallFloor";
        floor.transform.SetParent(parent, false);
        floor.transform.position = new Vector3(0f, -0.1f, 0f);
        floor.transform.localScale = new Vector3(12f, 0.2f, 20f);
        ApplyMaterial(floor, "HDRP/Lit", new Color(0.85f, 0.85f, 0.85f), 0.1f, 0.7f);

        // Carpet Runner
        GameObject runner = GameObject.CreatePrimitive(PrimitiveType.Cube);
        runner.name = "CarpetRunner";
        runner.transform.SetParent(parent, false);
        runner.transform.position = new Vector3(0f, 0.01f, 0f);
        runner.transform.localScale = new Vector3(2.4f, 0.02f, 18f);
        ApplyMaterial(runner, "HDRP/Lit", new Color(0.45f, 0.08f, 0.08f), 0.0f, 0.15f);
        GmSceneBuildUtility.MakeDecorativeOverlay(runner);

        // Left Wall (Wood Panel)
        GameObject leftWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftWall.name = "LeftWall";
        leftWall.transform.SetParent(parent, false);
        leftWall.transform.position = new Vector3(-6f, 3f, 0f);
        leftWall.transform.localScale = new Vector3(0.3f, 6f, 20f);
        ApplyMaterial(leftWall, "HDRP/Lit", new Color(0.22f, 0.14f, 0.09f), 0.05f, 0.4f);

        // Right Wall (Wood Panel)
        GameObject rightWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightWall.name = "RightWall";
        rightWall.transform.SetParent(parent, false);
        rightWall.transform.position = new Vector3(6f, 3f, 0f);
        rightWall.transform.localScale = new Vector3(0.3f, 6f, 20f);
        ApplyMaterial(rightWall, "HDRP/Lit", new Color(0.22f, 0.14f, 0.09f), 0.05f, 0.4f);

        // North Wall (Staircase wall)
        GameObject northWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        northWall.name = "NorthWall";
        northWall.transform.SetParent(parent, false);
        northWall.transform.position = new Vector3(0f, 3f, 10f);
        northWall.transform.localScale = new Vector3(12f, 6f, 0.3f);
        ApplyMaterial(northWall, "HDRP/Lit", new Color(0.25f, 0.16f, 0.10f), 0.05f, 0.4f);

        // South Wall (Entrance / Wake wall)
        GameObject southWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        southWall.name = "SouthWall";
        southWall.transform.SetParent(parent, false);
        southWall.transform.position = new Vector3(0f, 3f, -10f);
        southWall.transform.localScale = new Vector3(12f, 6f, 0.3f);
        ApplyMaterial(southWall, "HDRP/Lit", new Color(0.25f, 0.16f, 0.10f), 0.05f, 0.4f);

        // Ceiling
        GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceiling.name = "Ceiling";
        ceiling.transform.SetParent(parent, false);
        ceiling.transform.position = new Vector3(0f, 6.1f, 0f);
        ceiling.transform.localScale = new Vector3(12f, 0.2f, 20f);
        ApplyMaterial(ceiling, "HDRP/Lit", new Color(0.3f, 0.25f, 0.2f), 0.0f, 0.2f);
    }

    static void BuildGameplayProps(Transform parent, Dictionary<string, GameObject> authored)
    {
        // Console table
        GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
        table.name = "ConsoleTable";
        table.transform.SetParent(parent, false);
        table.transform.position = new Vector3(0f, 0.45f, -1f);
        table.transform.localScale = new Vector3(1.8f, 0.9f, 0.7f);
        ApplyMaterial(table, "HDRP/Lit", new Color(0.18f, 0.10f, 0.06f), 0.05f, 0.5f);
        authored["console-table"] = table;

        // Ledger Book
        GameObject ledger = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ledger.name = "GuestLedger";
        ledger.transform.SetParent(table.transform, false);
        ledger.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        ledger.transform.localScale = new Vector3(0.45f, 0.08f, 0.35f);
        ApplyMaterial(ledger, "HDRP/Lit", new Color(0.35f, 0.20f, 0.12f), 0.0f, 0.3f);
        authored["ledger-book"] = ledger;

        // Quill and inkwell resting beside the ledger where the last guest signed it.
        GameObject quill = GameObject.CreatePrimitive(PrimitiveType.Cube);
        quill.name = "LedgerQuill";
        quill.transform.SetParent(table.transform, false);
        quill.transform.localPosition = new Vector3(0.45f, 0.55f, 0.12f);
        quill.transform.localScale = new Vector3(0.05f, 0.03f, 0.28f);
        ApplyMaterial(quill, "HDRP/Lit", new Color(0.10f, 0.08f, 0.06f), 0.1f, 0.35f);
        authored["ledger-quill"] = quill;

        // Portrait gallery on left wall (9 portraits)
        var gallery = new GameObject("PortraitGallery");
        gallery.transform.SetParent(parent, false);
        gallery.transform.position = new Vector3(-5.8f, 2.2f, 0f);

        string[] names = { "Edwin Marr", "Caspian Dufresne", "Halvard Pike", "Solveig Hale",
                           "Theo Gall", "Barnaby Quill", "Imogen Thale", "Constance", "Percival" };

        for (int i = 0; i < names.Length; i++)
        {
            float zOffset = -6f + i * 1.5f;
            GameObject portrait = GameObject.CreatePrimitive(PrimitiveType.Cube);
            portrait.name = $"Portrait_{i + 1}_{names[i]}";
            portrait.transform.SetParent(gallery.transform, false);
            portrait.transform.localPosition = new Vector3(0f, 0f, zOffset);
            portrait.transform.localScale = new Vector3(0.08f, 1.2f, 0.9f);
            ApplyMaterial(portrait, "HDRP/Lit", new Color(0.6f, 0.5f, 0.3f), 0.4f, 0.4f);

            // Brass nameplate
            GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = $"Plate_{names[i]}";
            plate.transform.SetParent(portrait.transform, false);
            plate.transform.localPosition = new Vector3(0.6f, -0.6f, 0f);
            plate.transform.localScale = new Vector3(0.1f, 0.12f, 0.6f);
            ApplyMaterial(plate, "HDRP/Lit", new Color(0.85f, 0.75f, 0.35f), 0.7f, 0.6f);

            // The composition plan needs a real renderer to attach its markers to, not a proxy:
            // Marr's and Percival's are the two portraits the plan names directly.
            if (i == 0) authored["marr-frame"] = portrait;
            if (i == names.Length - 1) authored["percival-frame"] = portrait;
        }

        // Shard #1 behind Percival
        GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shard.name = "MirrorShard_1";
        shard.transform.SetParent(gallery.transform, false);
        shard.transform.localPosition = new Vector3(0.05f, -0.2f, 6.0f);
        shard.transform.localScale = new Vector3(0.02f, 0.25f, 0.15f);
        ApplyMaterial(shard, "HDRP/Lit", new Color(0.9f, 0.95f, 1.0f), 0.9f, 0.95f);
        authored["shard-one"] = shard;

        // Staircase representation
        GameObject stairs = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stairs.name = "GrandStaircase";
        stairs.transform.SetParent(parent, false);
        stairs.transform.position = new Vector3(0f, 1.5f, 8.5f);
        stairs.transform.localScale = new Vector3(5f, 3f, 3f);
        stairs.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
        ApplyMaterial(stairs, "HDRP/Lit", new Color(0.2f, 0.12f, 0.08f), 0.05f, 0.35f);
        authored["grand-staircase"] = stairs;
    }

    // The wake-cluster and stair-cluster composition elements named a settee, a marble side table
    // and a newel post that nothing above ever built. Blockout primitives in the same idiom as the
    // props above, placed where the composition plan already expects them.
    static void BuildDressing(Transform parent, Dictionary<string, GameObject> authored)
    {
        // Low settee where the player regains footing after the porch knockout.
        GameObject settee = GameObject.CreatePrimitive(PrimitiveType.Cube);
        settee.name = "WakeSettee";
        settee.transform.SetParent(parent, false);
        settee.transform.position = new Vector3(0f, 0.25f, -8f);
        settee.transform.localScale = new Vector3(1.6f, 0.5f, 0.6f);
        ApplyMaterial(settee, "HDRP/Lit", new Color(0.30f, 0.05f, 0.10f), 0.0f, 0.2f); // worn velvet
        authored["wake-settee"] = settee;

        // Small marble table beside the settee -- the wake-cluster's own purpose text already
        // promised one; it just never had geometry until now.
        GameObject wakeTable = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wakeTable.name = "WakeTable";
        wakeTable.transform.SetParent(parent, false);
        wakeTable.transform.position = new Vector3(1.3f, 0.25f, -8f);
        wakeTable.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        ApplyMaterial(wakeTable, "HDRP/Lit", new Color(0.75f, 0.74f, 0.72f), 0.1f, 0.55f); // marble
        authored["wake-table"] = wakeTable;

        // Carved newel post at the base of the grand staircase.
        GameObject newel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        newel.name = "StairNewel";
        newel.transform.SetParent(parent, false);
        newel.transform.position = new Vector3(2.0f, 0.4f, 6.0f);
        newel.transform.localScale = new Vector3(0.2f, 0.8f, 0.2f);
        ApplyMaterial(newel, "HDRP/Lit", new Color(0.18f, 0.11f, 0.07f), 0.05f, 0.4f);
        authored["stair-newel"] = newel;
    }

    static void BuildLighting(Transform parent, Dictionary<string, GameObject> authored)
    {
        // Chandelier fixture (the visible iron frame the light hangs from) + its light. Composition
        // markers measure elements through their renderers, so the light alone (no renderer) can
        // never stand in for the "hall-chandelier" element the plan describes.
        GameObject chandelierFixture = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        chandelierFixture.name = "ChandelierFixture";
        chandelierFixture.transform.SetParent(parent, false);
        chandelierFixture.transform.position = new Vector3(0f, 4.8f, 0f);
        chandelierFixture.transform.localScale = new Vector3(0.6f, 0.3f, 0.6f);
        ApplyMaterial(chandelierFixture, "HDRP/Lit", new Color(0.12f, 0.11f, 0.10f), 0.6f, 0.3f); // wrought iron
        authored["hall-chandelier"] = chandelierFixture;

        GameObject chandelierObj = new GameObject("ChandelierLight");
        chandelierObj.transform.SetParent(parent, false);
        chandelierObj.transform.position = new Vector3(0f, 4.8f, 0f);
        Light chandelier = chandelierObj.AddComponent<Light>();
        chandelier.type = LightType.Point;
        chandelier.range = 14f;
        chandelier.color = new Color(1.0f, 0.88f, 0.72f);
        chandelier.intensity = 800f;
        chandelierObj.AddComponent<HDAdditionalLightData>();
        authored["chandelier-light"] = chandelierObj;

        // Ledger lamp fixture (green glass shade) + light
        GameObject ledgerLampFixture = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ledgerLampFixture.name = "LedgerLampFixture";
        ledgerLampFixture.transform.SetParent(parent, false);
        ledgerLampFixture.transform.position = new Vector3(0.4f, 1.05f, -1f);
        ledgerLampFixture.transform.localScale = new Vector3(0.15f, 0.3f, 0.15f);
        ApplyMaterial(ledgerLampFixture, "HDRP/Lit", new Color(0.1f, 0.35f, 0.15f), 0.2f, 0.8f); // green glass
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
        GameObject wakeLampFixture = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wakeLampFixture.name = "WakeLampFixture";
        wakeLampFixture.transform.SetParent(parent, false);
        wakeLampFixture.transform.position = new Vector3(-1.0f, 1.55f, -8.1f);
        wakeLampFixture.transform.localScale = new Vector3(0.18f, 0.28f, 0.1f);
        ApplyMaterial(wakeLampFixture, "HDRP/Lit", new Color(0.55f, 0.42f, 0.20f), 0.5f, 0.5f); // tarnished brass
        authored["wake-lamp"] = wakeLampFixture;

        // Gallery wall sconces: fixtures + lights
        GameObject sconce1Fixture = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sconce1Fixture.name = "GallerySconceFixture_1";
        sconce1Fixture.transform.SetParent(parent, false);
        sconce1Fixture.transform.position = new Vector3(-5.2f, 2.5f, -3f);
        sconce1Fixture.transform.localScale = new Vector3(0.18f, 0.28f, 0.1f);
        ApplyMaterial(sconce1Fixture, "HDRP/Lit", new Color(0.55f, 0.42f, 0.20f), 0.5f, 0.5f);
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

        GameObject sconce2Fixture = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sconce2Fixture.name = "GallerySconceFixture_2";
        sconce2Fixture.transform.SetParent(parent, false);
        sconce2Fixture.transform.position = new Vector3(-5.2f, 2.5f, 3f);
        sconce2Fixture.transform.localScale = new Vector3(0.18f, 0.28f, 0.1f);
        ApplyMaterial(sconce2Fixture, "HDRP/Lit", new Color(0.55f, 0.42f, 0.20f), 0.5f, 0.5f);
        authored["gallery-sconce-2"] = sconce2Fixture;

        GameObject sconce2 = new GameObject("GallerySconce_2");
        sconce2.transform.SetParent(parent, false);
        sconce2.transform.position = new Vector3(-5.2f, 2.5f, 3f);
        Light sc2 = sconce2.AddComponent<Light>();
        sc2.type = LightType.Point;
        sc2.range = 5f;
        sc2.color = new Color(1.0f, 0.82f, 0.6f);
        sc2.intensity = 200f;
        sconce2.AddComponent<HDAdditionalLightData>();
        authored["gallery-sconce-2-light"] = sconce2;
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
