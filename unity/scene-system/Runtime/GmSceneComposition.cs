// Authored visual intent for generated scenes. Each serialized marker type lives in a matching
// file so Unity can resolve it after domain reloads; this file owns the scene manifest and API.
using System;
using UnityEngine;

public enum GmCompositionRole
{
    Anchor,
    Gameplay,
    Support,
    Detail,
    Boundary,
    Background,
    Ground,
    RouteCue
}

public enum GmSpatialRelation
{
    Grounded,
    AgainstBoundary,
    FlanksAnchor,
    LeadsToAnchor,
    FramesRoute,
    BackgroundLayer,
    Suspended,
    Embedded
}

[DisallowMultipleComponent]
public sealed class GmSceneComposition : MonoBehaviour
{
    [SerializeField] string sceneId;
    [SerializeField, TextArea] string visualIntent;
    [SerializeField] int minimumZones = 1;
    [SerializeField] int minimumClusters = 1;
    [SerializeField] int minimumElements = 4;
    [SerializeField] bool requireReviewClaimForEveryShot = true;
    [SerializeField] bool requireMotivatedLocalLights = true;

    public string SceneId => sceneId;
    public string VisualIntent => visualIntent;
    public int MinimumZones => minimumZones;
    public int MinimumClusters => minimumClusters;
    public int MinimumElements => minimumElements;
    public bool RequireReviewClaimForEveryShot => requireReviewClaimForEveryShot;
    public bool RequireMotivatedLocalLights => requireMotivatedLocalLights;

    public void Configure(string id, string intent, int minZones = 1, int minClusters = 1,
        int minElements = 4, bool requireEveryShot = true, bool requireMotivatedLights = true)
    {
        sceneId = id;
        visualIntent = intent;
        minimumZones = Mathf.Max(1, minZones);
        minimumClusters = Mathf.Max(1, minClusters);
        minimumElements = Mathf.Max(3, minElements);
        requireReviewClaimForEveryShot = requireEveryShot;
        requireMotivatedLocalLights = requireMotivatedLights;
    }
}

public static class GmCompositionAuthoring
{
    static T AddOrGet<T>(GameObject owner) where T : Component
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        return owner.GetComponent<T>() ?? owner.AddComponent<T>();
    }

    public static GmSceneComposition Begin(GameObject owner, string sceneId, string intent,
        int minZones = 1, int minClusters = 1, int minElements = 4,
        bool requireEveryShot = true, bool requireMotivatedLights = true)
    {
        var marker = AddOrGet<GmSceneComposition>(owner);
        marker.Configure(sceneId, intent, minZones, minClusters, minElements,
            requireEveryShot, requireMotivatedLights);
        return marker;
    }

    public static GmCompositionZone Zone(GameObject owner, string id, string purpose, Vector3 size,
        int minClusters = 1, int minElements = 3, Vector3 centerOffset = default)
    {
        var marker = AddOrGet<GmCompositionZone>(owner);
        marker.Configure(id, purpose, size, minClusters, minElements, centerOffset);
        return marker;
    }

    public static GmCompositionCluster Cluster(GameObject owner, string id, string zoneId,
        string purpose, string anchorId, int minSupports = 1, int minDetails = 1,
        int maxMembers = 24, float maxRadius = 8f, bool requireVariation = true,
        float maxFamilyShare = 0.75f, Vector3 centerOffset = default)
    {
        var marker = AddOrGet<GmCompositionCluster>(owner);
        marker.Configure(id, zoneId, purpose, anchorId, minSupports, minDetails, maxMembers,
            maxRadius, requireVariation, maxFamilyShare, centerOffset);
        return marker;
    }

    public static GmCompositionElement Element(GameObject owner, string id, string clusterId,
        string family, string rationale, GmCompositionRole role,
        GmSpatialRelation relation = GmSpatialRelation.Grounded, string targetId = "",
        float maxRelationDistance = 5f, float surfaceY = 0f, float groundTolerance = 0.2f,
        bool blocksRoutes = true)
    {
        var marker = AddOrGet<GmCompositionElement>(owner);
        marker.Configure(id, clusterId, family, rationale, role, relation, targetId,
            maxRelationDistance, surfaceY, groundTolerance, blocksRoutes);
        return marker;
    }

    public static GmRouteReservation Route(GameObject owner, string id, string purpose,
        Vector3[] localPoints, float halfWidth = 0.7f)
    {
        var marker = AddOrGet<GmRouteReservation>(owner);
        marker.Configure(id, purpose, localPoints, halfWidth);
        return marker;
    }

    public static GmNegativeSpace Reserve(GameObject owner, string id, string purpose,
        Vector3 size, string allowedElementId = "", Vector3 centerOffset = default)
    {
        var marker = AddOrGet<GmNegativeSpace>(owner);
        marker.Configure(id, purpose, size, allowedElementId, centerOffset);
        return marker;
    }

    public static GmMotivatedLight Motivate(GameObject owner, string id, string sourceElementId,
        string purpose, float maximumSourceDistance = 1.5f)
    {
        var marker = AddOrGet<GmMotivatedLight>(owner);
        marker.Configure(id, sourceElementId, purpose, maximumSourceDistance);
        return marker;
    }

    public static GmReviewCompositionClaim Claim(GameObject owner, string shotName, string claim,
        string primaryElementId, string[] foregroundIds, string[] supportingIds,
        string[] backgroundIds, Vector2 target, Vector2 tolerance,
        float minimumViewportHeight = 0.03f, float maximumViewportHeight = 0.9f)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        var marker = owner.AddComponent<GmReviewCompositionClaim>();
        marker.Configure(shotName, claim, primaryElementId, foregroundIds, supportingIds,
            backgroundIds, target, tolerance, minimumViewportHeight, maximumViewportHeight);
        return marker;
    }
}
