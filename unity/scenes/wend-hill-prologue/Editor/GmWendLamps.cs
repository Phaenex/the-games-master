// Fills the dark stretches of the route with light, because nothing else can.
//
// THE MEASUREMENT THAT PRODUCED THIS FILE. Eight lighting levers were bracketed against the walk and
// seven moved nothing. The one number that never budged is p5, the darkest frames, sitting at 0.012
// through every rung INCLUDING a five times increase in practical intensity. Turning lamps up lifted
// only the frames that already had a lamp in them (p95 0.213 -> 0.229) and left every dark frame
// exactly where it was. Light you turn up does not reach places that have no light.
//
// So the dark stretches do not have a weak lamp. They have none, and the fix is placement.
//
// WHY THIS IS DERIVED AND NOT AUTHORED: the scene is recopied from the purchased source on every
// build, so any hand placed lamp would be destroyed on the next run and any baked coordinate would go
// stale the moment the pack updates. This walks the route, measures the distance from each point to
// the nearest light the pack shipped, and puts a lamp where that distance exceeds what a lamp can
// carry. It fills the measured gaps rather than a remembered list of them.
//
// WHY IT DOES NOT TOUCH THE PACK'S LIGHTS: rule one of GmWendBuilder. The pack's artist lit this place
// and everything of theirs survives. These are additions, parented under one root, and excluded from
// the lighting census by ancestry so "24 practical" keeps meaning "the artist's 24 are all still here".
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public static class GmWendLamps
{
    const string LogTag = "GmWendLamps";

    public const string RootName = "GmWendLamps";

    /// How far a lamp is treated as carrying. Beyond this the route is unlit, which the walk measures
    /// as a frame around 0.012 against a lit frame's 0.1 to 0.2.
    public const float LampReach = 30f;

    /// Lumens per added lamp. The pack's own practicals land near 93 lumens after the night scale, and
    /// its brightest are clamped to 200. Sitting at the top of the pack's own range keeps these
    /// consistent with the lamps already in the street rather than introducing a brighter species.
    public const float Lumens = 200f;

    /// 2000K, matching the paraffin colour temperature the practicals were retinted to. A different
    /// temperature in the gaps would read as a different kind of light source.
    public const float Kelvin = 2000f;

    /// Lamp height above the ground, roughly a lamp bracket on a wall or a post.
    public const float Height = 3.2f;

    /// How finely the route is sampled before checking for gaps, in metres. Matches
    /// `GmWendWalkProbe.CaptureEveryMeters`, which is not a coincidence: checking gaps at any coarser
    /// resolution than the walk itself photographs the route is how a real 152m dark stretch between
    /// two individually-covered waypoints went unnoticed. See `GmWendRoute.Densify`.
    public const float GapSampleSpacing = 15f;

    /// Route points needing a lamp: those further than `reach` from every existing light.
    ///
    /// Pure, so the placement rule can be tested without a scene. Greedy and order dependent on
    /// purpose: once a lamp is placed it counts as cover for the points behind it, so a straight run
    /// gets lamps every `reach` metres rather than one per waypoint.
    public static List<Vector3> Gaps(IList<Vector3> route, IList<Vector3> existing, float reach)
    {
        var placed = new List<Vector3>();
        if (route == null) return placed;

        var cover = new List<Vector3>(existing ?? new Vector3[0]);
        foreach (Vector3 p in route)
        {
            bool lit = cover.Any(c =>
                (new Vector2(c.x, c.z) - new Vector2(p.x, p.z)).magnitude <= reach);
            if (lit) continue;
            placed.Add(p);
            cover.Add(p);
        }
        return placed;
    }

    /// Places the lamps into the open scene. Returns how many were added.
    public static int Apply()
    {
        GameObject stale = GameObject.Find(RootName);
        if (stale != null) Object.DestroyImmediate(stale);

        List<Vector3> route = GmWendRoute.BuildEstate(out _);
        if (route.Count == 0)
        {
            Debug.LogWarning($"[{LogTag}] no route, so no gaps to fill");
            return 0;
        }

        // Checked at walk-probe resolution, not at the 11 sparse waypoints. A gap check against the raw
        // route missed a real 152m dark stretch between two waypoints that individually read "covered";
        // see GmWendRoute.Densify for the measured reason.
        List<Vector3> dense = GmWendRoute.Densify(route, GapSampleSpacing);

        // Every light the pack shipped, which is what defines a gap.
        List<Vector3> existing = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude)
            .Where(l => l.type != LightType.Directional && l.gameObject.activeInHierarchy)
            .Select(l => l.transform.position)
            .ToList();

        List<Vector3> gaps = Gaps(dense, existing, LampReach);
        if (gaps.Count == 0)
        {
            Debug.Log($"[{LogTag}] no gaps: every point sampled every {GapSampleSpacing}m along the " +
                      $"route is within {LampReach}m of one of {existing.Count} existing light(s)");
            return 0;
        }

        var terrain = Object.FindAnyObjectByType<Terrain>();
        var root = new GameObject(RootName);

        foreach (Vector3 at in gaps)
        {
            float ground = terrain != null
                ? terrain.SampleHeight(at) + terrain.transform.position.y
                : at.y;

            var go = new GameObject($"GapLamp_{gaps.IndexOf(at):D2}");
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(at.x, ground + Height, at.z);

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.useColorTemperature = true;
            light.colorTemperature = Kelvin;
            light.color = Color.white;
            light.shadows = LightShadows.None;   // shadow cost for a fill lamp is not worth it

            var hd = go.AddComponent<HDAdditionalLightData>();
            hd.lightUnit = LightUnit.Lumen;
            hd.intensity = Lumens;
            hd.range = LampReach * 1.5f;
            hd.affectsVolumetric = false;   // same reason the moon does not: fog turns it into glare

            // The same flicker the pack's practicals were given, so these breathe like the rest.
            if (go.GetComponent<GmLightFlicker>() == null) go.AddComponent<GmLightFlicker>();
        }

        Debug.Log($"[{LogTag}] added {gaps.Count} gap lamp(s) at {Lumens} lumens {Kelvin}K, " +
                  $"filling {dense.Count} sampled point(s) (every {GapSampleSpacing}m) further than " +
                  $"{LampReach}m from any of the pack's {existing.Count} light(s)");
        return gaps.Count;
    }
}
