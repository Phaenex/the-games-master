// The one definition of the prologue's walk: where it starts, where it goes, in order.
//
// Everything that needs the route uses this, so the spawn and the walk cannot disagree. Two earlier
// attempts disagreed and both produced nonsense:
//
//   1. The spawn took the CENTROID of all 57 road meshes and snapped to the nearest piece. The pack's
//      roads are scattered clusters spread over about 1.5km, so their centroid is not a place. It put
//      the player on a downhill slope heading into the water plane, and every review frame in the
//      session was shot from there.
//   2. The walk ordered those same 57 meshes, first by nearest-neighbour, then along a global principal
//      axis. Nearest-neighbour cut over a hill and fell out of the world after 250m of climbing rock.
//      The global axis was worse: it left gaps of 177m to 944m between consecutive "road" pieces,
//      because a global axis through scattered clusters describes none of them.
//
// The mistake both share is treating the road as one object. It is not. So: cluster the pieces, pick the
// cluster the village is actually built along, order that one, and walk it from the outside in.
//
// No hardcoded coordinates. This project has been burned repeatedly by baked positions going stale the
// moment the scene is rebuilt, and the scene is rebuilt from the purchased source on every run.
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

public static class GmWendRoute
{
    static readonly Regex RoadPattern =
        new Regex(@"Road|Path|Street|Track|Lane|Trail|Cobble", RegexOptions.IgnoreCase);
    static readonly Regex WaterPattern = new Regex(@"Water|River|Lake|Pond", RegexOptions.IgnoreCase);

    const float LinkDistance = 12f;      // mesh-to-mesh gap that still counts as the same road
    const float SettlementRadius = 50f;  // how near a prop has to be to count as "the village is here"
    const int MinSettlementProps = 40;   // below this a road piece is countryside, not street
    const float MinSpacing = 12f;        // thin dense tiles so the walk advances instead of shuffling
    const float WaterClearance = 1.0f;   // never route to a point at or below the water surface

    /// Ordered walk, outside the village to the far end of its road. Empty if the scene has no road.
    public static List<Vector3> Build(out string report)
    {
        var sb = new StringBuilder();

        Renderer[] all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
            .Where(r => r.gameObject.activeInHierarchy)
            .ToArray();

        // Water is REPORTED, not filtered on. Two attempts at filtering both failed, and the second
        // failure is the instructive one: a winding river's axis-aligned bounding box covers a great deal
        // of dry land, so "inside the water's AABB and below its surface" excluded 43 of 57 road pieces
        // including the whole village street. Selecting the street by how much village stands along it
        // already excludes the lakeside pieces, and it does so on evidence rather than on a proxy.
        Bounds[] water = all
            .Where(r => WaterPattern.IsMatch(r.name) && r.bounds.size.y < 5f)
            .Select(r => r.bounds)
            .ToArray();

        Bounds[] pieceBounds = all
            .Where(r => RoadPattern.IsMatch(r.name))
            .Where(r => r.bounds.size.y < 3f && Mathf.Max(r.bounds.size.x, r.bounds.size.z) > 6f)
            .Select(r => r.bounds)
            .ToArray();

        sb.AppendLine($"{pieceBounds.Length} road piece(s), {water.Length} water surface(s) noted");
        if (pieceBounds.Length == 0) { report = sb.ToString(); return new List<Vector3>(); }

        // Keep only pieces the village actually stands beside, BEFORE clustering.
        //
        // Doing this after clustering did not work: some of the pack's road meshes are landscape-scale,
        // hundreds of metres across, so their bounding boxes touch everything and single-linkage welded
        // all 20 pieces into one 3676m "road" running from a map corner. Filtering first removes those
        // giants and the empty field tracks together, and it does it on the measurement that has been
        // right at every step: a village street has hundreds of standing props within 50m, a canyon road
        // has none. Preferred over another size threshold, which is what I would be guessing at.
        Vector3[] props = all
            .Where(r => !RoadPattern.IsMatch(r.name))
            .Where(r => r.bounds.size.y > 1.5f)
            .Select(r => r.bounds.center)
            .ToArray();

        int PropsNear(Vector3 at) => props.Count(p =>
            (new Vector2(p.x, p.z) - new Vector2(at.x, at.z)).sqrMagnitude < SettlementRadius * SettlementRadius);

        Bounds[] inhabited = pieceBounds.Where(b => PropsNear(b.center) >= MinSettlementProps).ToArray();
        sb.AppendLine($"{inhabited.Length} of those have at least {MinSettlementProps} prop(s) within " +
                      $"{SettlementRadius}m and are treated as street; the rest are field or canyon road");
        if (inhabited.Length == 0) { report = sb.ToString(); return new List<Vector3>(); }
        pieceBounds = inhabited;

        // Clustered on the GAP BETWEEN MESHES, not between their centres. These pieces are big: two
        // abutting 50m road sections have centres 50m apart, so a 45m centre-distance linkage split every
        // single one into its own cluster, fourteen clusters of one. The gap between their bounding boxes
        // is nearly zero, which is what "the same road" actually looks like in this data.
        List<List<Bounds>> clusters = Cluster(pieceBounds);
        sb.AppendLine($"{clusters.Count} cluster(s) at {LinkDistance}m mesh-to-mesh gap, sizes: " +
                      string.Join(", ", clusters.Select(c => c.Count.ToString())));

        // Pick by how much village sits along it. A road with 200 props within 50m is the village
        // street; a road with 3 is a track across a field, and the biggest cluster is not reliably
        // either. Counting the settlement is what distinguishes them.
        Vector3[] propCentres = all
            .Where(r => !RoadPattern.IsMatch(r.name))
            .Where(r => r.bounds.size.y > 1.5f)      // standing things, not ground decals
            .Select(r => r.bounds.center)
            .ToArray();

        int bestScore = -1;
        List<Bounds> chosen = clusters[0];
        foreach (List<Bounds> cluster in clusters)
        {
            int score = propCentres.Count(p => cluster.Any(c =>
                (new Vector2(p.x, p.z) - new Vector2(c.center.x, c.center.z)).sqrMagnitude
                    < SettlementRadius * SettlementRadius));
            bool wet = cluster.Any(c => water.Any(w =>
                c.center.x > w.min.x && c.center.x < w.max.x &&
                c.center.z > w.min.z && c.center.z < w.max.z));
            sb.AppendLine($"  cluster of {cluster.Count} piece(s): {score} settlement prop(s) within " +
                          $"{SettlementRadius}m{(wet ? ", overlaps water" : "")}");
            if (score > bestScore) { bestScore = score; chosen = cluster; }
        }
        sb.AppendLine($"chose the cluster of {chosen.Count} piece(s) with {bestScore} prop(s) along it");

        List<Vector3> road = chosen.Select(b => b.center).ToList();

        // Order along the chosen cluster's OWN axis, which is meaningful where a global one was not.
        float mx = road.Average(p => p.x), mz = road.Average(p => p.z);
        double sxx = 0, szz = 0, sxz = 0;
        foreach (Vector3 p in road)
        {
            double dx = p.x - mx, dz = p.z - mz;
            sxx += dx * dx; szz += dz * dz; sxz += dx * dz;
        }
        float theta = 0.5f * Mathf.Atan2((float)(2 * sxz), (float)(sxx - szz));
        var axis = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta));
        float Project(Vector3 p) => (p.x - mx) * axis.x + (p.z - mz) * axis.y;

        List<Vector3> ordered = road.OrderBy(Project).ToList();

        // Walk INTO the village: start at whichever end has less of it around, which is the approach.
        int NearProps(Vector3 at) => propCentres.Count(p =>
            (new Vector2(p.x, p.z) - new Vector2(at.x, at.z)).sqrMagnitude < SettlementRadius * SettlementRadius);
        int headProps = NearProps(ordered[0]), tailProps = NearProps(ordered[ordered.Count - 1]);
        if (headProps > tailProps) ordered.Reverse();
        sb.AppendLine($"start end has {Mathf.Min(headProps, tailProps)} prop(s), far end {Mathf.Max(headProps, tailProps)}; " +
                      "walking from the sparse end inward");

        // Thin, so consecutive targets are worth walking to.
        var route = new List<Vector3>();
        foreach (Vector3 p in ordered)
        {
            if (route.Count > 0 &&
                Vector2.Distance(new Vector2(p.x, p.z), new Vector2(route[^1].x, route[^1].z)) < MinSpacing)
                continue;
            route.Add(p);
        }

        float length = 0f;
        for (int i = 1; i < route.Count; i++)
            length += Vector2.Distance(new Vector2(route[i].x, route[i].z), new Vector2(route[i - 1].x, route[i - 1].z));
        sb.AppendLine($"route: {route.Count} waypoint(s), {length:0}m end to end, " +
                      $"from {route[0]} to {route[^1]}");

        report = sb.ToString();
        return route;
    }

    /// Single-linkage clustering on the gap between meshes. Two road pieces whose bounding boxes are
    /// within LinkDistance of each other are the same road; a 177m gap is not a road with a gap in it,
    /// it is two roads.
    static List<List<Bounds>> Cluster(Bounds[] pieces)
    {
        var clusters = new List<List<Bounds>>();
        var taken = new bool[pieces.Length];

        for (int i = 0; i < pieces.Length; i++)
        {
            if (taken[i]) continue;
            var cluster = new List<Bounds>();
            var queue = new Queue<int>();
            queue.Enqueue(i);
            taken[i] = true;

            while (queue.Count > 0)
            {
                int at = queue.Dequeue();
                cluster.Add(pieces[at]);
                for (int j = 0; j < pieces.Length; j++)
                {
                    if (taken[j]) continue;
                    if (GapXZ(pieces[at], pieces[j]) > LinkDistance) continue;
                    taken[j] = true;
                    queue.Enqueue(j);
                }
            }
            clusters.Add(cluster);
        }
        return clusters.OrderByDescending(c => c.Count).ToList();
    }

    /// Shortest horizontal distance between two boxes. Zero when they touch or overlap.
    static float GapXZ(Bounds a, Bounds b)
    {
        float dx = Mathf.Max(0f, Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x));
        float dz = Mathf.Max(0f, Mathf.Max(a.min.z - b.max.z, b.min.z - a.max.z));
        return Mathf.Sqrt(dx * dx + dz * dz);
    }
}
