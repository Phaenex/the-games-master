// Wend Hill shipped a night that looks right and sounds like nothing. The walk-probe evidence in
// docs/WEND-NIGHT-LADDER.md found zero AudioSources in the built scene -- not "nobody has listened
// yet", nothing is playing into the one live AudioListener at all.
//
// The clips and the proven mix already exist, built for the retired village pass and never carried
// over here (GmAmbience.cs, ~/GamesMaster-Unity/Assets/Scripts). Reusing its VALUES -- one quiet
// modulated wind bed, sparse wildlife one-shots, distance-driven footsteps, no second bed stacked on
// top -- is right; that recipe is exactly what replaced an earlier version Nick called "like being on
// a spaceship". Reusing its POSITIONS is not. Its wind-gust anchors and its cricket/owl perches are
// hand-placed coordinates in the retired estate's own layout, and Wend Hill is a different purchased
// pack with a different footprint. Planting those numbers here would repeat the exact mistake this
// scene's lighting work spent a week correcting: a value that was right for one place, applied
// unchanged to a different one, on the assumption that geometry travels between them. It does not
// (see the EV -1.0 "metered outside the village" postmortem for the lighting version of this).
//
// So positions are DERIVED, the same way GmWendLamps derives its gap lamps: walk the route this
// scene actually has and place anchors beside it, at a spacing this scene actually measured rather
// than a spacing borrowed from a different one.
//
// Footsteps use a single pooled rotation across every surface prefix the retired scene's clip library
// ships, rather than the retired scene's per-material GmSurfaceTag lookup. The purchased Abandoned
// Village pack was never tagged with GmSurfaceTag -- doing that properly is its own project -- so a
// surface-accurate footstep would need work this pass is not scoped for. A blended rotation across
// stone/gravel/dirt/leaves/grass reads as "footsteps on varied ground", which is honest for a walk
// down a village street with verges either side, even though it is not synced per-step to what is
// actually underfoot.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class GmWendAmbience
{
    const string LogTag = "GmWendAmbience";

    /// How far apart cricket anchors sit along the route. Crickets read as a near, local call, so
    /// spacing them tighter than the owl means more than one patch of insects across a long walk
    /// instead of one source doing service for the whole route.
    public const float CricketSpacingMetres = 70f;

    /// Owls are rare and read as distant, so they can sit much sparser than crickets without the walk
    /// ever crossing two anchors close enough together to sound like a chorus.
    public const float OwlSpacingMetres = 180f;

    /// How far off the walked line an anchor sits. On the route itself a spatial source would be
    /// loudest standing still on top of it, which reads as the player walking INTO the sound rather
    /// than past it.
    public const float LateralOffsetMetres = 8f;

    /// Owl anchors sit up in whatever canopy is nearby rather than at head height, because every call
    /// in real use comes from a tree, not from the ground.
    public const float OwlHeightMetres = 6f;
    const float CricketHeightMetres = 0.4f;

    static readonly string[] FootstepPrefixes = { "dirt", "grass", "gravel", "leaves", "stone" };
    const int FootstepClipsPerSurface = 8;

    /// Anchors for a wildlife source: every `stride`-th point of an already-densified route, offset
    /// sideways so the source sits beside the walked line rather than on it, alternating which side
    /// so consecutive anchors do not all sit on the same flank.
    ///
    /// Pure so placement can be tested without a scene, the same reason `GmWendLamps.Gaps` is pure.
    public static List<Vector3> Anchors(IList<Vector3> densePoints, int stride, float lateralOffset)
    {
        var anchors = new List<Vector3>();
        if (densePoints == null || densePoints.Count < 2 || stride < 1) return anchors;

        for (int i = stride; i < densePoints.Count; i += stride)
        {
            Vector3 a = densePoints[i - 1], b = densePoints[i];
            Vector3 direction = b - a;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) continue;   // degenerate leg; skip rather than NaN

            Vector3 perpendicular = new Vector3(-direction.z, 0f, direction.x).normalized;
            float side = anchors.Count % 2 == 0 ? 1f : -1f;
            anchors.Add(b + perpendicular * lateralOffset * side);
        }
        return anchors;
    }

    /// Builds and wires the ambience onto the player. Returns how many anchors and clips were placed
    /// so the caller can log it; throws if the core clips do not resolve, because a silent build that
    /// reports success is exactly the failure this file exists to close.
    public static (int cricketAnchors, int owlAnchors, int footstepClips) Apply()
    {
        GameObject player = GameObject.Find(GmWendBuilder.PlayerName);
        if (player == null)
            throw new System.InvalidOperationException(
                $"no '{GmWendBuilder.PlayerName}' to attach ambience to. Call this after the player exists.");

        var stale = player.GetComponentInChildren<GmWendAmbienceSource>(true);
        if (stale != null) Object.DestroyImmediate(stale.gameObject);

        List<Vector3> route = GmWendRoute.BuildEstate(out _);
        List<Vector3> dense = GmWendRoute.Densify(route, GmWendLamps.GapSampleSpacing);
        var terrain = Object.FindAnyObjectByType<Terrain>();

        List<Vector3> cricketAnchors = PlaceOnGround(
            Anchors(dense, StrideFor(CricketSpacingMetres), LateralOffsetMetres),
            terrain, CricketHeightMetres);
        List<Vector3> owlAnchors = PlaceOnGround(
            Anchors(dense, StrideFor(OwlSpacingMetres), LateralOffsetMetres * 1.5f),
            terrain, OwlHeightMetres);

        AudioClip windClip = Resources.Load<AudioClip>("Sfx/amb_wind_natural")
            ?? Resources.Load<AudioClip>("Sfx/amb_wind");
        AudioClip cricketClip = Resources.Load<AudioClip>("Sfx/amb_crickets");
        AudioClip owlClip = Resources.Load<AudioClip>("Sfx/amb_owl");
        List<AudioClip> footstepPool = LoadFootstepPool();

        if (windClip == null || footstepPool.Count == 0)
            throw new System.InvalidOperationException(
                $"ambience clips did not resolve (wind={(windClip != null)}, " +
                $"footsteps={footstepPool.Count}). The scene would ship as silent as it started.");

        var root = new GameObject("GmWendAmbience");
        root.transform.SetParent(player.transform, false);

        AudioSource wind = MakeSource(root.transform, "wind_bed", windClip, loop: true, spatial: 0f);
        wind.volume = 0.5f * (GmWendAmbienceSource.WindVolumeMin + GmWendAmbienceSource.WindVolumeMax);
        AudioSource footsteps = MakeSource(root.transform, "footsteps", null, loop: false, spatial: 0f);
        AudioSource crickets = MakeSource(root.transform, "crickets", cricketClip, loop: false, spatial: 1f);
        AudioSource owl = MakeSource(root.transform, "owl", owlClip, loop: false, spatial: 1f);

        var behaviour = root.AddComponent<GmWendAmbienceSource>();
        behaviour.windBed = wind;
        behaviour.footstepSource = footsteps;
        behaviour.cricketSource = crickets;
        behaviour.owlSource = owl;
        behaviour.footstepPool = footstepPool.ToArray();
        behaviour.cricketAnchors = cricketAnchors.ToArray();
        behaviour.owlAnchors = owlAnchors.ToArray();
        EditorUtility.SetDirty(behaviour);

        Debug.Log($"[{LogTag}] wired: wind bed '{windClip.name}', {footstepPool.Count} footstep clip(s), " +
                  $"{cricketAnchors.Count} cricket anchor(s) (clip={(cricketClip != null)}), " +
                  $"{owlAnchors.Count} owl anchor(s) (clip={(owlClip != null)})");
        return (cricketAnchors.Count, owlAnchors.Count, footstepPool.Count);
    }

    static int StrideFor(float spacingMetres) =>
        Mathf.Max(1, Mathf.RoundToInt(spacingMetres / GmWendLamps.GapSampleSpacing));

    static List<Vector3> PlaceOnGround(List<Vector3> anchors, Terrain terrain, float height)
    {
        var placed = new List<Vector3>(anchors.Count);
        foreach (Vector3 at in anchors)
        {
            float ground = terrain != null ? terrain.SampleHeight(at) + terrain.transform.position.y : at.y;
            placed.Add(new Vector3(at.x, ground + height, at.z));
        }
        return placed;
    }

    static List<AudioClip> LoadFootstepPool()
    {
        var clips = new List<AudioClip>();
        foreach (string prefix in FootstepPrefixes)
            for (int i = 1; i <= FootstepClipsPerSurface; i++)
            {
                var clip = Resources.Load<AudioClip>($"Sfx/foot_{prefix}_{i:00}");
                if (clip != null) clips.Add(clip);
            }
        return clips;
    }

    static AudioSource MakeSource(Transform parent, string name, AudioClip clip, bool loop, float spatial)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = loop;
        source.playOnAwake = false;
        source.spatialBlend = spatial;
        if (spatial > 0f)
        {
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 6f;
            source.maxDistance = 45f;
            source.dopplerLevel = 0f;
        }
        return source;
    }
}
