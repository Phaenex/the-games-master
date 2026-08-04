using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>A non-shipping proof that scene-craft contracts are reusable beyond Wend Hill.</summary>
public static class GmSceneCraftLabBuilder
{
    public const string ScenePath = "Assets/Scenes/Tests/SceneCraftLab.unity";

    [MenuItem("GamesMaster/Scene Intelligence/Build Craft Lab")]
    public static void BuildAndSave()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BuildInMemory(1907);
        EnsureFolders();
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[GmCraftLab] PASS: {ScenePath}");
    }

    public static GameObject BuildInMemory(int seed)
    {
        var root = new GameObject("SceneCraftLab");
        var profile = root.AddComponent<GmSceneCraftProfile>();
        profile.Configure("scene-craft-lab",
            "A small ruined field proves terrain, depth, surface, audio and interaction contracts without becoming production content.",
            seed, new[] {
                new GmCraftZoneRule("lab-arrival", "A compressed threshold opens into a wider field.",
                    "A traveller stopped here and left one object behind.", "lab-anchor",
                    new[] { "lab-threshold" }, new[] { "lab-anchor" }, new[] { "lab-ridge" }, 0.45f, 3),
                new GmCraftZoneRule("lab-field", "Low ground texture leads to a broken distant silhouette.",
                    "Weather has erased the route but not the interruption.", "lab-interactable",
                    new[] { "lab-surface" }, new[] { "lab-interactable" }, new[] { "lab-ridge" }, 0.48f, 3),
            });

        var groundMaterial = Material("Gm_CraftLabGround", new Color(0.15f, 0.12f, 0.09f), 0.06f);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "LabGround";
        ground.transform.SetParent(root.transform);
        ground.transform.localScale = new Vector3(3f, 1f, 5f);
        ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;
        ground.AddComponent<GmSurfaceTag>().Configure(GmSurfaceKind.PackedMud,
            "Craft-lab base surface for deterministic footstep and grounding checks.");
        Element(ground, "lab-surface", "lab-ground", GmCompositionRole.Ground,
            "A semantic packed-earth foreground makes the proof scene physically and aurally grounded.");
        ground.AddComponent<GmIntentionallySilent>().Configure("Walkable ground is not a story target.");

        var threshold = Cube(root.transform, "LabThreshold", new Vector3(0f, 0.75f, 12f),
            new Vector3(7f, 1.5f, 0.35f), new Color(0.20f, 0.17f, 0.13f));
        Element(threshold, "lab-threshold", "lab-arrival", GmCompositionRole.RouteCue,
            "The compressed arrival plane establishes the first depth layer.");
        threshold.AddComponent<GmIntentionallySilent>().Configure("Navigation boundary, not a story target.");

        var anchor = Cube(root.transform, "LabAnchor", new Vector3(-2.2f, 0.55f, 0f),
            new Vector3(1.2f, 1.1f, 1.2f), new Color(0.24f, 0.16f, 0.10f));
        Element(anchor, "lab-anchor", "lab-arrival", GmCompositionRole.Anchor,
            "A stopped-traveller mass catches the eye beyond the threshold.");
        anchor.AddComponent<GmIntentionallySilent>().Configure("Cluster anchor; its smaller abandoned object carries the story interaction.");

        var storyObject = Cube(root.transform, "LabInteractable", new Vector3(-1.35f, 0.20f, 0.38f),
            new Vector3(0.36f, 0.40f, 0.52f), new Color(0.30f, 0.15f, 0.07f));
        Element(storyObject, "lab-interactable", "lab-field", GmCompositionRole.Detail,
            "One abandoned object converts the field's visual interruption into inspectable evidence.");
        var interactable = storyObject.AddComponent<GmInteractable>();
        interactable.Configure("lab-interactable", "Examine", 3.2f, 6f);
        interactable.BindContent("A test object with an authored purpose.", "It remains the same on a second build.");

        var ridge = Cube(root.transform, "LabRidge", new Vector3(0f, 1.4f, -18f),
            new Vector3(26f, 2.8f, 2.5f), new Color(0.08f, 0.09f, 0.10f));
        Element(ridge, "lab-ridge", "lab-depth", GmCompositionRole.Background,
            "An irregular closure proves the contract has a background layer rather than empty sky.");
        ridge.AddComponent<GmIntentionallySilent>().Configure("Distant depth closure, outside interaction range.");

        for (int i = 0; i < 3; i++)
        {
            var emitter = new GameObject($"LabAudioEmitter_{i + 1:00}");
            emitter.transform.SetParent(root.transform);
            emitter.transform.position = new Vector3(-7f + i * 7f, 1f, -4f - i * 3f);
            var source = emitter.AddComponent<AudioSource>();
            source.spatialBlend = 1f;
            source.playOnAwake = false;
            GmAdaptiveIntentAuthoring.Audio(emitter, $"lab-emitter-{i + 1:00}", GmAudioIntentKind.Diegetic,
                GmAudioLoopPolicy.Never, "Spatial proof emitter; deliberately silent in the saved test scene.");
        }
        return root;
    }

    static void Element(GameObject owner, string id, string cluster, GmCompositionRole role, string rationale)
    {
        owner.AddComponent<GmCompositionElement>().Configure(id, cluster, "craft-lab", rationale,
            role, GmSpatialRelation.Embedded, "", 100f, 0f, 0.25f, false);
    }

    static GameObject Cube(Transform parent, string name, Vector3 position, Vector3 scale, Color colour)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = Material("Gm_" + name, colour, 0.08f);
        return go;
    }

    static Material Material(string name, Color colour, float smoothness)
    {
        var material = new Material(Shader.Find("HDRP/Lit")) { name = name };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        return material;
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
        if (!AssetDatabase.IsValidFolder("Assets/Scenes/Tests")) AssetDatabase.CreateFolder("Assets/Scenes", "Tests");
    }
}

