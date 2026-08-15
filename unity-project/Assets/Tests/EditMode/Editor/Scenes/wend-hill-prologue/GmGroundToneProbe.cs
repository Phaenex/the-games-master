// What is actually under the player's feet, and what colour is it.
//
// The drive's ground renders as molten orange and three suspects have been cleared by measurement:
// the lamps (2000K vs 2700K moved the frame cast 2.03 -> 2.13, i.e. nothing), the terrain layers
// (diffuse remap is a near-neutral 0.72/0.68/0.60) and the cliff/rock tint (0.62/0.64/0.60, grey-
// green). Each of those was checked by reading the code that writes them, which is how three
// plausible stories in a row turned out to be about surfaces the camera may not even be looking at.
//
// So stop reading writers and interrogate the scene: raycast down along the actual route and report
// the renderer, the material, the shader and the base colour that is really there.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class GmGroundToneProbe
{
    const string ScenePath = "Assets/Scenes/WendHill_Prologue.unity";

    /// A tint channel at or under this is dead. The pack shipped the drive's mud at
    /// (0.576, 0.380, 0.000) and no amount of moon could cool it, because there was nothing in the
    /// blue channel to reflect. Not a style rule -- a surface with a dead channel is unlightable in
    /// that colour by construction, which is a fact about the material and not a taste about it.
    const float DeadChannel = 0.001f;

    [Test]
    public void ReportWhatTheRouteIsMadeOf()
    {
        if (!System.IO.File.Exists(ScenePath)) Assert.Ignore($"{ScenePath} has not been built");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Physics.SyncTransforms();

        var player = Object.FindAnyObjectByType<GmPlayer>(FindObjectsInactive.Include);
        Vector3 origin = player != null ? player.transform.position : new Vector3(-79.95f, 0.78f, -104.59f);

        var spline = Object.FindAnyObjectByType<GmRouteSpline>();
        var samples = new List<(string label, Vector3 point)> { ("spawn", origin) };
        if (spline != null)
            for (int i = 1; i <= 6; i++)
            {
                float t = i / 7f;
                samples.Add(($"route {t:P0}", spline.PointAt(t)));
            }

        var seen = new Dictionary<string, int>();
        var deadChannels = new List<string>();
        foreach ((string label, Vector3 point) in samples)
        {
            // Past the player's own capsule, and past RouteWalkDeck -- an invisible collider the
            // route rides on, whose renderer GmWendTerrainSurface deliberately disables. Neither is
            // the surface a camera sees, and the first version of this probe reported both as the
            // ground, which is a probe measuring itself.
            Vector3 from = point + Vector3.up * 3f;
            RaycastHit[] all = Physics.RaycastAll(from, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(all, (a, b) => a.distance.CompareTo(b.distance));
            RaycastHit hit = default;
            bool found = false;
            foreach (RaycastHit candidate in all)
            {
                if (candidate.transform.GetComponentInParent<GmPlayer>() != null) continue;
                var vis = candidate.transform.GetComponentInParent<Renderer>();
                var terr = candidate.transform.GetComponent<Terrain>();
                if (terr == null && (vis == null || !vis.enabled)) continue;   // invisible collider
                hit = candidate; found = true; break;
            }
            if (!found)
            {
                Debug.Log($"[GroundTone] {label,-12} nothing VISIBLE under it " +
                    $"({all.Length} collider(s), all invisible or the player)");
                continue;
            }

            var renderer = hit.transform.GetComponentInParent<Renderer>();
            var terrain = hit.transform.GetComponent<Terrain>();
            string what = terrain != null ? $"TERRAIN '{terrain.name}'" :
                renderer != null ? $"{renderer.GetType().Name} '{renderer.name}'" : $"collider-only '{hit.transform.name}'";

            string material = "n/a", shader = "n/a", colour = "n/a";
            if (renderer != null && renderer.sharedMaterial != null)
            {
                Material m = renderer.sharedMaterial;
                material = m.name;
                shader = m.shader != null ? m.shader.name : "null";
                Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor")
                        : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.clear;
                colour = $"({c.r:0.00},{c.g:0.00},{c.b:0.00}) r:b={c.r / Mathf.Max(c.b, 0.0001f):0.00}";
            }
            else if (terrain != null && terrain.terrainData != null)
            {
                var layers = terrain.terrainData.terrainLayers;
                material = $"{layers.Length} layer(s)";
                shader = terrain.materialTemplate != null ? terrain.materialTemplate.shader.name : "default";
                var parts = new List<string>();
                foreach (TerrainLayer l in layers)
                {
                    Vector4 hi = l.diffuseRemapMax;
                    parts.Add($"{l.name}:{hi.x:0.00}/{hi.y:0.00}/{hi.z:0.00}");
                }
                colour = string.Join("  ", parts);
            }

            string key = $"{what} | {material}";
            seen[key] = seen.TryGetValue(key, out int n) ? n + 1 : 1;
            Debug.Log($"[GroundTone] {label,-12} y={hit.point.y:0.00}  {what}\n" +
                      $"              material='{material}' shader='{shader}'\n" +
                      $"              baseColour {colour}");
        }

        // The layers say neutral and the ground renders orange, which means whatever is drawing it is
        // not reading the layers. Dump the terrain's actual material -- a purchased Shader Graph is
        // free to ignore diffuseRemap entirely and colour the ground from its own inputs.
        foreach (Terrain t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Material m = t.materialTemplate;
            if (m == null) { Debug.Log($"[GroundTone] terrain '{t.name}' uses the DEFAULT material"); continue; }
            Debug.Log($"[GroundTone] terrain '{t.name}' material='{m.name}' shader='{m.shader.name}'");
            int count = m.shader.GetPropertyCount();
            for (int i = 0; i < count; i++)
            {
                var kind = m.shader.GetPropertyType(i);
                string pname = m.shader.GetPropertyName(i);
                if (kind == UnityEngine.Rendering.ShaderPropertyType.Color)
                {
                    Color c = m.GetColor(pname);
                    float rb = c.r / Mathf.Max(c.b, 0.0001f);
                    Debug.Log($"[GroundTone]    COLOR {pname,-34} ({c.r:0.000},{c.g:0.000},{c.b:0.000}) r:b={rb:0.00}" +
                        (rb > 1.6f ? "   <== WARM" : ""));
                    // Emission is allowed to be any colour it likes -- a lit window is supposed to be
                    // one hue. A SURFACE tint is not: a channel at zero cannot reflect that colour
                    // from any light, at any brightness, ever.
                    if (pname.Contains("Emission")) continue;
                    float brightest = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                    if (brightest < 0.02f) continue;   // a near-black tint is a choice, not a defect
                    if (Mathf.Min(c.r, Mathf.Min(c.g, c.b)) <= DeadChannel)
                        deadChannels.Add($"{t.name} / {m.name} / {pname} = " +
                            $"({c.r:0.000},{c.g:0.000},{c.b:0.000}) — this surface cannot be lit by " +
                            "the missing channel, so no amount of moonlight will ever cool it");
                }
                else if (kind == UnityEngine.Rendering.ShaderPropertyType.Float ||
                         kind == UnityEngine.Rendering.ShaderPropertyType.Range)
                {
                    float v = m.GetFloat(pname);
                    if (Mathf.Abs(v) > 0.0001f) Debug.Log($"[GroundTone]    FLOAT {pname,-34} {v:0.###}");
                }
            }
        }

        Debug.Log("[GroundTone] surfaces along the route: " + string.Join(" | ",
            new List<string>(seen.Keys).ConvertAll(k => $"{k} x{seen[k]}")));

        Assert.IsEmpty(deadChannels, "a surface the player walks on has a dead colour channel:\n- " +
            string.Join("\n- ", deadChannels));
    }
}
