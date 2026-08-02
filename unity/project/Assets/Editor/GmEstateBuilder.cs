// Builds the Wend Hill estate scene from the web build's exported design data.
// Run headless:  Unity -batchmode -projectPath <proj> -executeMethod GmEstateBuilder.Build -quit
// The design JSON (StreamingAssets/prologue-design.json) is the single source of truth shared
// with the web prototype — coordinates, POIs, beats, and placements come from there verbatim.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GmEstateBuilder
{
    [Serializable] public class Placement { public string asset; public float x, z, targetHeight, rotationY, y, leanZ; public string kind; }
    [Serializable] public class Poi { public float x, z, radius; public string text, text2; }
    [Serializable] public class Beat { public float z; public string main, sub; }
    [Serializable] public class WorldConst { public float spawnZ, gateZ, carZ, arrivalZ, mansionZ, doorWallZ, eyeHeight; }
    [Serializable] public class Design { public float[][] walkRects; public Poi[] pois; public Beat[] beats; public string[] coldOpen; public Placement[] placements; public WorldConst world; }

    [MenuItem("GamesMaster/Build Estate Scene")]
    public static void Build()
    {
        string json = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "prologue-design.json"));
        Design d = JsonUtility.FromJson<Design>(WrapArrays(json));

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ground: broad dark plane; Leartes terrain material can replace later in-editor
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(30, 1, 30); // 300x300
        var groundMat = new Material(Shader.Find("HDRP/Lit"));
        groundMat.color = new Color(0.05f, 0.05f, 0.045f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;

        // night lighting rig: moonlight directional + ambient floor
        var moon = new GameObject("Moonlight").AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = new Color(0.55f, 0.62f, 0.8f);
        moon.intensity = 0.6f;
        moon.transform.rotation = Quaternion.Euler(38f, -140f, 0f);

        // placements from design data — native prefabs found by asset name
        int placed = 0, missing = 0;
        foreach (var p in d.placements ?? new Placement[0])
        {
            GameObject prefab = FindAssetPrefab(p.asset);
            if (prefab == null) { Debug.LogWarning($"[GmEstateBuilder] no asset found for '{p.asset}'"); missing++; continue; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = $"{p.kind} ({p.asset})";
            // scale to targetHeight like the web build's mounts
            var b = GetBounds(go);
            float s = b.size.y > 0.01f ? p.targetHeight / b.size.y : 1f;
            go.transform.localScale = Vector3.one * s;
            go.transform.rotation = Quaternion.Euler(0f, p.rotationY * Mathf.Rad2Deg, p.leanZ * Mathf.Rad2Deg);
            b = GetBounds(go);
            go.transform.position = new Vector3(p.x - b.center.x, p.y - b.min.y, p.z - b.center.z);
            placed++;
        }

        // player rig: capsule + camera at eye height, spawn at the drive start
        var player = new GameObject("Player");
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0, 0.9f, 0);
        player.transform.position = new Vector3(0, 0.05f, d.world.spawnZ);
        var camGo = new GameObject("PlayerCamera");
        camGo.transform.SetParent(player.transform, false);
        camGo.transform.localPosition = new Vector3(0, d.world.eyeHeight, 0);
        camGo.AddComponent<Camera>();
        camGo.tag = "MainCamera";
        player.AddComponent<GmPlayer>();

        // design-driven systems host
        var systems = new GameObject("GmSystems");
        systems.AddComponent<GmDesignRuntime>();

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/WendHill.unity");
        Debug.Log($"[GmEstateBuilder] DONE placed={placed} missing={missing} scene=Assets/Scenes/WendHill.unity");
    }

    static GameObject FindAssetPrefab(string name)
    {
        // prefer prefabs, fall back to model importers (FBX) — all native, no conversion
        foreach (string filter in new[] { $"{name} t:prefab", $"{name} t:model" })
        {
            foreach (string guid in AssetDatabase.FindAssets(filter))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileNameWithoutExtension(path);
                if (!string.Equals(file, name, StringComparison.OrdinalIgnoreCase)) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null) return go;
            }
        }
        return null;
    }

    static Bounds GetBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // JsonUtility can't do nested arrays (walkRects) — flatten them into objects it can read.
    static string WrapArrays(string json)
    {
        // walkRects: [[a,b,c,d],...] -> stripped; runtime reads rects via GmDesignRuntime's own parser
        int i = json.IndexOf("\"walkRects\"", StringComparison.Ordinal);
        if (i < 0) return json;
        int start = json.IndexOf('[', i);
        int depth = 0, end = start;
        for (int k = start; k < json.Length; k++)
        {
            if (json[k] == '[') depth++;
            else if (json[k] == ']') { depth--; if (depth == 0) { end = k; break; } }
        }
        return json.Remove(i, end - i + 2).Insert(i, "\"walkRectsRemoved\": 0,");
    }
}

