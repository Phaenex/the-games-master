// M3, Unity half: exports the estate->village transform for the remapper, then places a physical,
// examinable marker at every POI the remapper produced.
//
// The remap itself (scripts/remap-village-design.py) runs outside Unity because it has to rewrite
// arbitrary JSON, and JsonUtility cannot round-trip a document whose shape it does not have a
// [Serializable] class for. Rather than hand-roll a JSON parser inside the editor, the authoritative
// numbers are exported from here -- GmVillageEstate stays the single source of truth for the
// transform -- and the remapper only APPLIES them. The file it writes back
// (village-pois.json) uses a fixed schema JsonUtility can read, so nothing is parsed by hand.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class GmVillageDesign
{
    const string LogTag = "GmVillageDesign";
    public const string VillageDesignFile = "village-design.json";

    public static string TransformPath =>
        Path.Combine(Directory.GetCurrentDirectory(), "Screens", "DemoScenes", "village-transform.json");
    public static string PoiMarkerPath =>
        Path.Combine(Directory.GetCurrentDirectory(), "Screens", "DemoScenes", "village-pois.json");

    static readonly Regex LodSuffix = new Regex(@"_LOD[1-9]$");
    static readonly Regex BuildingPattern =
        new Regex(@"House|Church|Barn|Shed|Hut|Cabin|Chapel", RegexOptions.IgnoreCase);

    [Serializable] public class PoiMarker
    {
        public string id;
        public string verb;
        public float x, y, z;
        public float radius;
        public bool anchored;
    }

    [Serializable] public class PoiMarkerList { public PoiMarker[] items; }

    /// The prop that MAKES each POI. Without these the POIs are invisible interaction volumes: the
    /// player walks into a radius, gets an examine prompt and a paragraph of prose, and there is
    /// nothing on screen that explains why. Anchored POIs are deliberately absent from this table --
    /// their building already is the thing being examined.
    /// (poi id, asset name, target height in metres)
    static readonly (string id, string asset, float height)[] PoiProps =
    {
        ("open-grave",       "SM_GraveCross_01", 1.35f),
        ("stag-plinth",      "BrokenStagCrest",  1.55f),
        ("weathered-marker", "SM_GraveCross_02", 1.05f),
        ("fallen-marker",    "SM_GraveCross_03", 0.95f),
        ("child-marker",     "SM_GraveCross_01", 0.72f),   // smaller: it is a child's
        ("garden-scarecrow", "SM_Pugalo",        2.35f),
        ("garden-well",      "SM_Well_01",       1.60f),
        ("garden-basin",     "SM_Bucket",        0.55f),
        ("gate-plaque",      "SM_Wood_01",       1.15f),
    };

    /// Writes everything the remapper needs: the spine, the estate anchor pairs that define the
    /// map, and every village building's measured footprint (so POIs can be pushed out of walls).
    public static void ExportTransform()
    {
        var sb = new StringBuilder();
        sb.Append("{\n");
        sb.Append($"  \"spineCentroid\": [{F(GmVillageEstate.SpineCentroid.x)}, {F(GmVillageEstate.SpineCentroid.y)}],\n");
        sb.Append($"  \"spineAxis\": [{F(GmVillageEstate.SpineAxis.x)}, {F(GmVillageEstate.SpineAxis.y)}],\n");
        sb.Append($"  \"lateralAxis\": [{F(GmVillageEstate.LateralAxis.x)}, {F(GmVillageEstate.LateralAxis.y)}],\n");
        // The two (estateZ -> spine t) pairs that define the linear map. Exported rather than
        // duplicated in the script, so changing a placement here cannot silently desync the data.
        sb.Append($"  \"anchorGate\":   {{\"estateZ\": 65, \"t\": {F(GmVillageEstate.EstateZToT(65f))}}},\n");
        sb.Append($"  \"anchorMansion\": {{\"estateZ\": -58, \"t\": {F(GmVillageEstate.EstateZToT(-58f))}}},\n");

        sb.Append("  \"buildings\": [\n");
        var rows = new List<string>();
        foreach (Renderer r in FindBuildings())
        {
            Bounds b = r.bounds;
            rows.Add($"    {{\"name\": \"{r.name.Replace("_LOD0", "")}\", " +
                     $"\"x\": {F(b.center.x)}, \"z\": {F(b.center.z)}, " +
                     $"\"radius\": {F(Mathf.Max(b.size.x, b.size.z) * 0.5f)}}}");
        }
        sb.Append(string.Join(",\n", rows));
        sb.Append("\n  ]\n}\n");

        Directory.CreateDirectory(Path.GetDirectoryName(TransformPath));
        File.WriteAllText(TransformPath, sb.ToString());
        Debug.Log($"[{LogTag}] exported transform + {rows.Count} buildings -> {TransformPath}");
    }

    /// Places an examinable marker at every remapped POI. Without this the POIs are invisible
    /// coordinate bubbles -- the player walks into a radius and gets prose with nothing on screen
    /// that explains why. Each marker gets a GmInteractable so GmInteractionScanner can focus it.
    public static int PlacePoiMarkers(Transform parent, Terrain terrain)
    {
        if (!File.Exists(PoiMarkerPath))
        {
            Debug.LogWarning($"[{LogTag}] no {PoiMarkerPath}; run scripts/remap-village-design.py first");
            return 0;
        }

        var list = JsonUtility.FromJson<PoiMarkerList>(File.ReadAllText(PoiMarkerPath));
        if (list?.items == null || list.items.Length == 0)
        {
            Debug.LogWarning($"[{LogTag}] {PoiMarkerPath} contained no POIs");
            return 0;
        }

        GameObject stale = GameObject.Find("VillagePois");
        if (stale != null) UnityEngine.Object.DestroyImmediate(stale);
        var root = new GameObject("VillagePois");
        root.transform.SetParent(parent, true);

        int placed = 0, props = 0;
        foreach (PoiMarker poi in list.items)
        {
            float groundY = terrain != null
                ? terrain.SampleHeight(new Vector3(poi.x, 0f, poi.z)) + terrain.transform.position.y
                : poi.y;

            var go = new GameObject($"POI_{poi.id}");
            go.transform.SetParent(root.transform, true);
            go.transform.position = new Vector3(poi.x, groundY, poi.z);

            // The trigger is what the scanner focuses. Kept non-solid: these mark story beats, they
            // are not obstacles, and a solid collider on the drive would block the walk.
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = Mathf.Max(1.2f, poi.radius);
            col.center = new Vector3(0f, 1.1f, 0f);

            var interactable = go.AddComponent<GmInteractable>();
            interactable.Configure(poi.id, string.IsNullOrWhiteSpace(poi.verb) ? "Examine" : poi.verb,
                Mathf.Max(2.5f, poi.radius), 55f);

            if (AttachProp(go.transform, poi, terrain)) props++;
            placed++;
        }

        Debug.Log($"[{LogTag}] placed {placed} POI markers ({props} with props, " +
                  $"{list.items.Count(i => i.anchored)} anchored to village buildings)");
        return placed;
    }

    /// Instantiates the POI's prop, scaled to a sensible height and seated on the terrain. Rotation
    /// is derived from the POI id rather than randomised, so a rebuild reproduces the same scene.
    static bool AttachProp(Transform marker, PoiMarker poi, Terrain terrain)
    {
        var entry = PoiProps.FirstOrDefault(p => p.id == poi.id);
        if (entry.id == null) return false;

        GameObject prefab = GmVillageEstate.FindPrefab(entry.asset);
        if (prefab == null)
        {
            Debug.LogWarning($"[{LogTag}] POI '{poi.id}' wants '{entry.asset}' but it was not found");
            return false;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, marker);
        go.name = $"Prop_{entry.asset}";

        Bounds b = Encapsulate(go);
        // Scale by the LONGEST extent, not by height. Scaling by Y silently fails for anything flat:
        // SM_Wood_01 is a 17 x 1.13 x 19.78m slab of boards, so asking for "1.15m tall" scaled it by
        // 1.02 and left a 17x20m plank lying across the drive. It had no collider, so the walk test
        // passed straight through it and only a rendered frame caught it.
        float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (longest > 0.01f) go.transform.localScale = Vector3.one * (entry.height / longest);
        go.transform.rotation = Quaternion.Euler(0f, Mathf.Abs(poi.id.GetHashCode()) % 360, 0f);

        b = Encapsulate(go);
        float groundY = terrain != null
            ? terrain.SampleHeight(new Vector3(poi.x, 0f, poi.z)) + terrain.transform.position.y
            : marker.position.y;
        go.transform.position += new Vector3(poi.x - b.center.x, groundY - b.min.y, poi.z - b.center.z);

        // Markers are story beats, not obstacles. A grave cross with a collider standing in the
        // cemetery is fine, but one that drifted onto the drive would block the walk, and
        // GmVillageWalkTest would catch it as a hard failure rather than a nuisance.
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
            UnityEngine.Object.DestroyImmediate(c);

        // Guard the class of bug above rather than just the one instance of it. Any POI prop that
        // ends up bigger than a garden shed is wrong by definition and should be shouted about.
        Bounds final = Encapsulate(go);
        float footprint = Mathf.Max(final.size.x, final.size.z);
        if (footprint > 4f)
            Debug.LogWarning($"[{LogTag}] POI '{poi.id}' prop '{entry.asset}' is {footprint:0.#}m across " +
                             "after scaling — that is too big for a story marker, check the asset");
        return true;
    }

    static Bounds Encapsulate(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static IEnumerable<Renderer> FindBuildings()
    {
        var seen = new HashSet<string>();
        foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)
                     .OrderBy(r => r.bounds.center.z))
        {
            if (!r.gameObject.activeInHierarchy || LodSuffix.IsMatch(r.name)) continue;
            if (!BuildingPattern.IsMatch(r.name)) continue;
            if (!seen.Add(r.name.Replace("_LOD0", ""))) continue;
            yield return r;
        }
    }

    static string F(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);
}

