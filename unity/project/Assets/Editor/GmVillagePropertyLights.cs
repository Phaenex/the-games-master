// Motivated light sources across the whole property: lamp posts down the drive, braziers at the
// thresholds, candles in windows.
//
// Before this the entire 180m walk had TEN light sources -- eight window practicals on four of the
// seven buildings, plus two gate lanterns -- so most of the route was unlit ground with nothing to
// look at and nothing to walk toward. The note was "needs to have lights around the property that
// work or flicker or both".
//
// The reason these exist in the fiction, which is also why they are not just decoration: the letter
// says "Nine o'clock. Take the old road." Someone is expecting him. A property lit end to end for an
// arriving guest is worse than a dark one, because it means the preparation was deliberate and the
// house knew he was coming. Every lamp here is something a person walked out and lit.
//
// Everything flickers. GmLightFlicker is seeded per world position so no two sources breathe
// together, and the fire props carry real particle flame so the source is visible, not just implied.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public static class GmVillagePropertyLights
{
    const string LogTag = "GmVillageLights";
    public const string RootName = "VillagePropertyLights";

    // Warm oil-lamp colour. Deliberately further from white than the window practicals: these are
    // out in the open air where the cool moonlight is the thing they have to read against.
    static readonly Color LampColour = new Color(1f, 0.62f, 0.29f);
    static readonly Color FireColour = new Color(1f, 0.52f, 0.20f);

    public struct Result
    {
        public int lampPosts;
        public int braziers;
        public int porchLights;
    }

    public static Result Build(Transform parent, Terrain terrain, Vector3 mansionWorld)
    {
        GameObject stale = GameObject.Find(RootName);
        if (stale != null) UnityEngine.Object.DestroyImmediate(stale);

        var root = new GameObject(RootName);
        root.transform.SetParent(parent, true);

        var result = new Result();
        var rng = new System.Random(778899);

        GameObject polePrefab = GmVillageEstate.FindPrefab("SM_LightPole");
        GameObject firePrefab = GmVillageEstate.FindPrefab("SM_Fireplace");
        GameObject flamePrefab = GmVillageEstate.FindPrefab("P_Fire");

        if (polePrefab == null)
            Debug.LogWarning($"[{LogTag}] SM_LightPole not found; drive will have no lamp posts");

        // ── lamp posts down the drive ───────────────────────────────────────────────────────────
        //
        // Alternating sides rather than paired: a matched avenue of lamps reads as municipal
        // streetlighting, and this is a private road someone has walked down with a taper. Spacing is
        // wide enough that the player passes through real darkness between pools, which is what makes
        // the lit patches worth anything.
        int side = 1;
        for (float t = 70f; t >= -78f; t -= 21f)
        {
            float lateral = side * Mathf.Lerp(5.2f, 7.4f, (float)rng.NextDouble());
            side = -side;

            Vector2 xz = GmVillageEstate.SpinePoint(t) + GmVillageEstate.LateralAxis * lateral;
            float groundY = Ground(terrain, xz);

            float poleHeight = 3.5f;
            if (polePrefab != null)
            {
                GameObject pole = Place(polePrefab, root.transform, $"LampPost_{result.lampPosts:D2}",
                    xz, groundY, poleHeight,
                    GmVillageEstate.SpineYawDeg + (float)(rng.NextDouble() * 24.0 - 12.0));
                if (pole != null)
                {
                    // No collider: a lamp post the player can walk into on a narrow drive is a
                    // nuisance, and GmVillageWalkTest would report it as a hard route failure.
                    foreach (Collider c in pole.GetComponentsInChildren<Collider>(true))
                        UnityEngine.Object.DestroyImmediate(c);
                }
            }

            MakeLamp(root.transform, $"LampLight_{result.lampPosts:D2}",
                new Vector3(xz.x, groundY + poleHeight * 0.86f, xz.y),
                LampColour, lumens: 52f, range: 13f, variation: 0.22f, rng);
            result.lampPosts++;
        }

        // ── braziers at the thresholds ──────────────────────────────────────────────────────────
        //
        // Placed where the walk changes state rather than at even intervals: either side of the
        // gate, either side of the porch approach, and one at the church. A fire at a threshold is
        // the oldest possible way of saying someone is expected.
        var brazierSites = new List<(Vector2 xz, string label)>();
        Vector2 gate = GmVillageEstate.SpinePoint(GmVillageEstate.EstateZToT(65f));
        brazierSites.Add((gate + GmVillageEstate.LateralAxis * 4.6f, "Gate_E"));
        brazierSites.Add((gate - GmVillageEstate.LateralAxis * 4.6f, "Gate_W"));

        Vector2 porch = new Vector2(mansionWorld.x, mansionWorld.z) +
                        GmVillageEstate.SpineAxis * 15f;
        brazierSites.Add((porch + GmVillageEstate.LateralAxis * 6.5f, "Porch_E"));
        brazierSites.Add((porch - GmVillageEstate.LateralAxis * 6.5f, "Porch_W"));

        // Anchored to the CHURCH MESH, not to the chapel POI marker: property lights are built with
        // the estate, and GmVillageDesign.PlacePoiMarkers does not run until afterwards, so looking
        // for the marker here silently found nothing and the chapel simply had no fire.
        Renderer church = UnityEngine.Object
            .FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)
            .FirstOrDefault(r => r.name.StartsWith("SM_Church", StringComparison.OrdinalIgnoreCase));
        if (church != null)
        {
            Vector2 churchXZ = new Vector2(church.bounds.center.x, church.bounds.center.z);
            brazierSites.Add((churchXZ + GmVillageEstate.LateralAxis * 9f, "Chapel"));
        }
        else
        {
            Debug.LogWarning($"[{LogTag}] no SM_Church found; chapel brazier skipped");
        }

        foreach ((Vector2 xz, string label) in brazierSites)
        {
            float groundY = Ground(terrain, xz);
            if (firePrefab != null)
            {
                GameObject bowl = Place(firePrefab, root.transform, $"Brazier_{label}",
                    xz, groundY, 1.15f, (float)(rng.NextDouble() * 360.0));
                if (bowl != null)
                    foreach (Collider c in bowl.GetComponentsInChildren<Collider>(true))
                        UnityEngine.Object.DestroyImmediate(c);
            }

            // Visible flame. A pool of light with no source in it is the thing that reads as "a game
            // effect"; the particle is what makes it a fire.
            if (flamePrefab != null)
            {
                var flame = (GameObject)PrefabUtility.InstantiatePrefab(flamePrefab, root.transform);
                flame.name = $"Flame_{label}";
                flame.transform.position = new Vector3(xz.x, groundY + 0.85f, xz.y);
                flame.transform.localScale = Vector3.one * 0.55f;
            }

            MakeLamp(root.transform, $"BrazierLight_{label}",
                new Vector3(xz.x, groundY + 1.15f, xz.y),
                FireColour, lumens: 96f, range: 15f, variation: 0.30f, rng);
            result.braziers++;
        }

        // ── mansion porch ───────────────────────────────────────────────────────────────────────
        //
        // The doors never open, so the porch has to carry the whole arrival on light alone. Two
        // steady-ish lamps under the eaves: warm, close, and completely useless to him.
        foreach (int s in new[] { -1, 1 })
        {
            Vector2 at = new Vector2(mansionWorld.x, mansionWorld.z) +
                         GmVillageEstate.SpineAxis * 7.4f + GmVillageEstate.LateralAxis * (s * 4.3f);
            MakeLamp(root.transform, $"PorchLight_{(s < 0 ? "W" : "E")}",
                new Vector3(at.x, Ground(terrain, at) + 3.2f, at.y),
                LampColour, lumens: 68f, range: 11f, variation: 0.10f, rng);
            result.porchLights++;
        }

        Debug.Log($"[{LogTag}] PASS: {result.lampPosts} lamp posts, {result.braziers} braziers, " +
                  $"{result.porchLights} porch lights " +
                  $"(pole={(polePrefab != null ? "ok" : "MISSING")} " +
                  $"fire={(firePrefab != null ? "ok" : "MISSING")} " +
                  $"flame={(flamePrefab != null ? "ok" : "MISSING")})");
        return result;
    }

    static void MakeLamp(Transform parent, string name, Vector3 pos, Color colour,
        float lumens, float range, float variation, System.Random rng)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = pos;

        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = colour;
        light.range = range;
        // Shadows off across the board. Ten-plus shadow-casting point lights over a scene with 3,300
        // renderers is the single easiest way to wreck the frame time, and these read as glow.
        light.shadows = LightShadows.None;

        var hd = go.AddComponent<HDAdditionalLightData>();
        // Volumetric off, same conclusion the window practicals and gate lanterns reached: with fog
        // this dense every source grows a halo wider than the thing it is lighting.
        hd.affectsVolumetric = false;
        light.lightUnit = LightUnit.Lumen;
        light.intensity = lumens;

        var flicker = go.AddComponent<GmLightFlicker>();
        flicker.baseIntensity = lumens;
        flicker.variation = variation;
        flicker.speed = 1.05f + (float)rng.NextDouble() * 1.6f;
    }

    static GameObject Place(GameObject prefab, Transform parent, string name,
        Vector2 xz, float groundY, float targetHeight, float yaw)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;

        Bounds b = Encapsulate(go);
        float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (longest > 0.01f) go.transform.localScale = Vector3.one * (targetHeight / longest);
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        b = Encapsulate(go);
        go.transform.position += new Vector3(xz.x - b.center.x, groundY - b.min.y, xz.y - b.center.z);
        return go;
    }

    static Bounds Encapsulate(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static float Ground(Terrain terrain, Vector2 xz) =>
        terrain.SampleHeight(new Vector3(xz.x, 0f, xz.y)) + terrain.transform.position.y;
}

