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
    public const float WaterClearance = 1.0f;   // never route to a point at or below the water surface
    public const float EstateRouteMetres = 435f;

    /// The authored opening ends at the manor on dry village ground. The purchased showcase road
    /// continues another ~140m into an uncollided canyon; that tail is scenery, not gameplay.
    public static List<Vector3> BuildEstate(out string report)
    {
        List<Vector3> raw = Build(out string rawReport);
        List<Vector3> estate = TakeThroughDistance(raw, EstateRouteMetres);
        report = rawReport + $"estate route: {estate.Count} point(s), capped at {RouteLength(estate):0}m " +
                 $"before the canyon/showcase tail\n";
        return estate;
    }

    public static List<Vector3> TakeThroughDistance(IReadOnlyList<Vector3> route, float metres)
    {
        var result = new List<Vector3>();
        if (route == null || route.Count == 0 || metres <= 0f) return result;
        result.Add(route[0]);
        float travelled = 0f;
        for (int i = 1; i < route.Count; i++)
        {
            float segment = Vector2.Distance(new Vector2(route[i - 1].x, route[i - 1].z),
                new Vector2(route[i].x, route[i].z));
            if (travelled + segment <= metres)
            {
                result.Add(route[i]);
                travelled += segment;
                continue;
            }
            float remaining = metres - travelled;
            if (remaining > 0.01f && segment > 0.01f)
                result.Add(Vector3.Lerp(route[i - 1], route[i], remaining / segment));
            break;
        }
        return result;
    }

    public static float RouteLength(IReadOnlyList<Vector3> route)
    {
        float length = 0f;
        if (route == null) return length;
        for (int i = 1; i < route.Count; i++)
            length += Vector2.Distance(new Vector2(route[i - 1].x, route[i - 1].z),
                new Vector2(route[i].x, route[i].z));
        return length;
    }

    /// The highest water surface in the scene, or null when it ships none.
    ///
    /// Lives here rather than in the walk probe so there is still exactly one definition of where the
    /// water is, which is the same reason the route itself lives here. Scans once; callers are expected
    /// to hold the result rather than ask per frame.
    public static float? WaterSurfaceY()
    {
        float surface = float.MinValue;
        bool found = false;

        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (!r.gameObject.activeInHierarchy) continue;
            if (!WaterPattern.IsMatch(r.name)) continue;
            if (r.bounds.size.y >= 5f) continue;   // a tall box is a volume, not a surface
            surface = Mathf.Max(surface, r.bounds.max.y);
            found = true;
        }

        return found ? surface : (float?)null;
    }

    /// Cuts the route where it descends into the lake.
    ///
    /// A walk found this by going in: the last ~45m of the route were spent SUBMERGED, looking up at
    /// the underside of the water surface, and the final waypoint sits at y=-22.80 against a spawn at
    /// -0.81. The frames are a golden band of refracted light and nothing else, and no lighting work
    /// can fix a prologue that ends underwater.
    ///
    /// Truncates on the water's HEIGHT ONLY, never on its footprint. That distinction is the whole
    /// reason this is safe: two earlier attempts filtered on the water's axis-aligned bounding box and
    /// both failed, the second instructively, because a winding river's AABB covers a great deal of dry
    /// land and it excluded 43 of 57 road pieces including the entire village street. A height test
    /// cannot do that, because the street is above the water and stays above it.

    /// Ray hits under a point, sorted nearest-first. Shared by the two functions below so there is one
    /// place that casts, not two copies that could disagree.
    static RaycastHit[] SortedHitsBelow(Vector3 at)
    {
        Vector3 origin = new Vector3(at.x, at.y + 500f, at.z);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 2000f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        return hits;
    }

    static float? FirstSolidHitY(RaycastHit[] sortedHits)
    {
        foreach (RaycastHit hit in sortedHits)
            if (!WaterPattern.IsMatch(hit.collider.name)) return hit.point.y;
        return null;
    }

    /// The walkable surface under a point, from whatever collider is actually there.
    ///
    /// This is attempt four from the analysis above. The first three each asked the TERRAIN a question
    /// the terrain cannot answer on this scene: the ground under the village genuinely dips below the
    /// water plane while the player walks on road and building meshes that sit above it, so no
    /// terrain-height signal, with or without a margin, can tell a dry street from a submerged one. A
    /// raycast from above answers a different, correct question: what would the player actually land on
    /// here. That is decided by physics colliders, not by the terrain heightmap.
    ///
    /// RaycastAll rather than a single Raycast, because a ray over open water hits the water's OWN
    /// collider first, at exactly the water's surface height. Comparing that to itself would never cut
    /// anything, so the water hit is skipped and the first solid hit beneath it -- lake bed, terrain,
    /// whatever is really there -- is what gets compared to the surface.
    public static float? RaycastGroundY(Vector3 at) => FirstSolidHitY(SortedHitsBelow(at));

    /// SUPERSEDED. Kept for what it proves rather than for what it does: the first full-route truncation
    /// with this wired in found a bug (waypoint 4 sits at terrain y=-1.46, 0.66m under the lake's y=-0.8
    /// surface, with no water anywhere near it, and the whole route got cut there) and this fixed it
    /// correctly, by requiring an actual water COLLIDER in the same vertical column before comparing
    /// depths. It was then checked against the real submerged point a live walk recorded, and it
    /// returned null there too: `PlaneWater`, the only water-named renderer this scene ships, HAS NO
    /// COLLIDER AT ALL. `Physics.RaycastAll` can never see it, at any point, which means this function
    /// can never fire on this scene no matter how correct its logic is. It is not wrong, it is aimed at
    /// something that is not there. `GroundInFootprint` below is what replaced it.
    public static float? GroundNearWater(Vector3 at)
    {
        RaycastHit[] hits = SortedHitsBelow(at);
        bool touchesWater = false;
        foreach (RaycastHit hit in hits)
            if (WaterPattern.IsMatch(hit.collider.name)) { touchesWater = true; break; }
        return touchesWater ? FirstSolidHitY(hits) : (float?)null;
    }

    /// The ground to compare against the water surface, using the water's RENDERED footprint rather
    /// than a physics collider -- because this scene's water has none. `PlaneWater` is a pure visual
    /// mesh, so `GroundNearWater` above is correct code aimed at a signal that does not exist here.
    ///
    /// This is the same two-signal combination the file's very first water attempt was missing one half
    /// of: attempt 1 (elsewhere in this file, for CLUSTER SELECTION) filtered on footprint alone and
    /// excluded 43 of 57 road pieces because a winding river's AABB covers a great deal of dry land.
    /// Attempts 2 through 4 (for TRUNCATION, above and in the header comments) filtered on height alone
    /// and cut a dry low spot half a kilometre from any water. Footprint ALONE over-excludes; height
    /// ALONE over-cuts. Requiring BOTH -- inside the water's rendered bounds, AND below its surface --
    /// is narrower than either failure mode: a dry corner of the bounding box is not under the surface,
    /// so it survives; a low spot outside the bounds is never even asked the height question.
    ///
    /// Verified against the one real data point available: the exact position a live walk recorded
    /// itself as SUBMERGED, (-18.89, -2.51, 81.26), sits inside `PlaneWater`'s XZ bounds
    /// (-66.2..25.4, -74.6..19.0), where the water-only attempts above found nothing at all.
    public static float? GroundInFootprint(Vector3 at, Bounds[] water)
    {
        if (water == null) return null;
        bool inside = false;
        foreach (Bounds w in water)
        {
            if (at.x < w.min.x || at.x > w.max.x || at.z < w.min.z || at.z > w.max.z) continue;
            inside = true;
            break;
        }
        return inside ? (RaycastGroundY(at) ?? at.y) : (float?)null;
    }

    /// Cuts rather than skips. Once the road has gone under, whatever follows is further in.
    /// `groundAt` returns the walkable surface height under a waypoint. It is a parameter rather than a
    /// direct terrain lookup so the decision stays testable, and because the FIRST version of this used
    /// the waypoint's own Y and was wrong in a way worth recording: route waypoints are road-mesh
    /// bounding-box CENTRES, not surface heights. The spawn's road mesh centres at y=-0.81 while the
    /// terrain under it is at +0.68, and the lake surface is -0.8, so comparing waypoint Y to the water
    /// excluded the entire village on the first run. The guard below caught it and reported rather than
    /// returning an empty route, which is the only reason it was a log line instead of a broken walk.
    public static List<Vector3> TruncateAtWater(
        List<Vector3> route, Bounds[] water, System.Func<Vector3, float> groundAt, StringBuilder sb)
    {
        if (route.Count == 0 || water == null || water.Length == 0 || groundAt == null) return route;

        float surface = float.MinValue;
        foreach (Bounds w in water) surface = Mathf.Max(surface, w.max.y);

        // SUBMERGED means the ground is under the water, with no safety margin added on top. That is not
        // laziness, it is what the scene measured: the water surface sits at y=-0.8 and the village
        // ground runs a little over half a metre above it, so a 1m margin, which is what this tried
        // first, condemns the entire village as underwater and the guard below has to refuse the cut.
        // There is no headroom here to spend on a margin.
        float floor = surface;
        int keep = route.Count;
        for (int i = 0; i < route.Count; i++)
        {
            if (groundAt(route[i]) >= floor) continue;
            keep = i;
            break;
        }

        if (keep == route.Count)
        {
            sb?.AppendLine($"water: surface at y={surface:0.0}, no waypoint falls below " +
                           $"{floor:0.0}, route kept whole");
            return route;
        }

        // Never truncate to nothing. A route of one point is not a walk, and a water surface that
        // somehow sits above the whole road is a scene problem to report rather than to obey.
        if (keep < 2)
        {
            sb?.AppendLine($"water: surface at y={surface:0.0} would cut the route to {keep} " +
                           "waypoint(s), which cannot be right; keeping it whole and reporting instead");
            return route;
        }

        sb?.AppendLine($"water: surface at y={surface:0.0}, cutting {route.Count - keep} waypoint(s) " +
                       $"that descend below {floor:0.0}. The route ended underwater before this.");
        return route.GetRange(0, keep);
    }

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

        // STILL not wired in, after two more attempts this session, each of which found a real bug in
        // the one before it and neither of which can actually be pointed at the thing that needs
        // catching. Recorded in order because the dead ends are what earns the conclusion below:
        //
        //   4. RaycastGroundY, a physics raycast instead of a terrain sample. Reproduced the OLD
        //      terrain-height bug through a new code path: it cut 7 of 11 waypoints at waypoint 4, a low
        //      spot 0.66m under the lake's absolute Y with no water anywhere near it.
        //   5. GroundNearWater, requiring an actual water COLLIDER in the same column before comparing
        //      depths, to fix attempt 4. Checked against the real SUBMERGED point a live walk recorded
        //      and found nothing: `PlaneWater`, the only water-named renderer this scene ships, has NO
        //      collider at all. Physics.RaycastAll can never see it. Correct code, aimed at a signal
        //      that is not there.
        //   6. GroundInFootprint, checking the water's RENDERED bounds instead of a collider, to fix
        //      attempt 5. Found two more things: `PlaneWater` sits mid-route, at waypoint 5 exactly, and
        //      is unrelated to what the player actually wades into. And the real submersion point a live
        //      walk recorded, (-18.89, -2.51, 81.26), is 60m from anything named "Water" -- what is
        //      actually there is a canyon of `SM_Cliff` meshes with NO COLLIDER EITHER, dropping to
        //      y=-18. There is no lake mesh at the far end of this route. What ends the walk is the
        //      terrain itself descending below the same global Y the small decorative pond happens to
        //      share, which GmWendWalkProbe already tests correctly and in real time. A shape-accurate
        //      static footprint would have to be a per-pixel water mask, not a bounding box, to avoid
        //      re-flagging waypoint 4 the same way attempt 1 over-excluded the village street.
        //
        // Six attempts, three of them this session, and the honest conclusion has not moved: there is no
        // single static signal on this scene that separates a dry dip from a real drop, because the pack
        // did not build one -- there is no water collider and no lake mesh to test against at the place
        // that matters. The runtime stop in GmWendWalkProbe is not a fallback for this; it is the only
        // thing that has ever correctly answered the question, because it is asking about the ACTUAL
        // path walked at the moment of walking it rather than guessing from 11 static points beforehand.
        //
        // RaycastGroundY, GroundNearWater and GroundInFootprint stay in this file, tested, unused, for
        // the same reason TruncateAtWater itself was kept after attempts 1 through 3: the analysis is
        // worth more than the code, and the next person who reaches for "just raycast it" should find
        // this instead of rediscovering the same three-hop dead end.

        float length = 0f;
        for (int i = 1; i < route.Count; i++)
            length += Vector2.Distance(new Vector2(route[i].x, route[i].z), new Vector2(route[i - 1].x, route[i - 1].z));
        sb.AppendLine($"route: {route.Count} waypoint(s), {length:0}m end to end, " +
                      $"from {route[0]} to {route[^1]}");

        report = sb.ToString();
        return route;
    }

    /// Interpolates extra points along the route so nothing checking coverage against it is blind to
    /// what happens BETWEEN waypoints.
    ///
    /// Found by `GmWendLamps`' gap check reporting the route "covered" while the walk photographed
    /// three near-black frames and a mint-green wall along one stretch of it. The reason: `Gaps()` only
    /// ever tested the route's own waypoints, an average of ~58m apart on an 11-waypoint, 580m route,
    /// while `GmWendWalkProbe` photographs the ACTUAL walked path every 15m. Waypoint 9 to waypoint 10
    /// is a single ~152m leg -- both ends individually within lamp reach, so `Gaps()` never flagged it,
    /// while the walk probe's 15m-spaced captures sat in the middle of it and read near-black. A gap
    /// check that never samples the middle of a long leg cannot see a gap in the middle of a long leg.
    ///
    /// Every original waypoint survives exactly, so anything keyed to "the 11 route waypoints" upstream
    /// of this (spawn placement, the walk probe's own steering) is unaffected; this only adds points a
    /// caller opts into consuming, such as `GmWendLamps.Apply`.
    public static List<Vector3> Densify(List<Vector3> route, float spacing)
    {
        var dense = new List<Vector3>();
        if (route == null || route.Count == 0 || spacing <= 0f) return route ?? dense;

        dense.Add(route[0]);
        for (int i = 1; i < route.Count; i++)
        {
            Vector3 a = route[i - 1], b = route[i];
            float legLength = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
            int steps = Mathf.Max(1, Mathf.CeilToInt(legLength / spacing));
            for (int s = 1; s <= steps; s++)
                dense.Add(Vector3.Lerp(a, b, (float)s / steps));
        }
        return dense;
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
