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

    /// Declares a scene that has no composed 3D space at all -- a title screen, a credits roll: UI
    /// drawn in screen space over an empty stage.
    ///
    /// This is an opt-out you must ASK for, not a number you can quietly lower. The floors below
    /// exist so an empty room cannot ship, and they are right for a room. Forcing a menu to invent a
    /// "title zone" and a "menu cluster" to satisfy that shape would be worse than skipping it: a
    /// contract padded to look uniform teaches the reader it is decorative, and that is exactly how
    /// a real room's floating furniture gets waved through.
    ///
    /// It is deliberately NOT silent -- the audit reports every scene that took this exit, so a room
    /// can never quietly acquire it and stop being checked.
    [SerializeField] bool screenSpaceOnly;

    public string SceneId => sceneId;
    public string VisualIntent => visualIntent;
    public int MinimumZones => minimumZones;
    public int MinimumClusters => minimumClusters;
    public int MinimumElements => minimumElements;
    public bool RequireReviewClaimForEveryShot => requireReviewClaimForEveryShot;
    public bool RequireMotivatedLocalLights => requireMotivatedLocalLights;
    public bool ScreenSpaceOnly => screenSpaceOnly;

    public void Configure(string id, string intent, int minZones = 1, int minClusters = 1,
        int minElements = 4, bool requireEveryShot = true, bool requireMotivatedLights = true,
        bool uiOnly = false)
    {
        sceneId = id;
        visualIntent = intent;
        screenSpaceOnly = uiOnly;
        // The floors still clamp for every ordinary scene. A UI-only scene skips them by declaration
        // rather than by passing zeros, so "this scene has no geometry" is a statement someone made
        // on purpose and can be found by grepping for it.
        minimumZones = uiOnly ? 0 : Mathf.Max(1, minZones);
        minimumClusters = uiOnly ? 0 : Mathf.Max(1, minClusters);
        minimumElements = uiOnly ? 0 : Mathf.Max(3, minElements);
        requireReviewClaimForEveryShot = !uiOnly && requireEveryShot;
        requireMotivatedLocalLights = !uiOnly && requireMotivatedLights;
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
        bool requireEveryShot = true, bool requireMotivatedLights = true, bool uiOnly = false)
    {
        var marker = AddOrGet<GmSceneComposition>(owner);
        marker.Configure(sceneId, intent, minZones, minClusters, minElements,
            requireEveryShot, requireMotivatedLights, uiOnly);
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

    // One owner carries a claim per review shot, so this cannot use AddOrGet: that would collapse
    // every shot onto a single component. The shot name is the identity instead, matching the audit,
    // which keys claims by ShotName and reports a repeated one as a duplicate. Re-authoring a shot
    // therefore reconfigures its existing claim rather than stacking a second one on a rebuild.
    public static GmReviewCompositionClaim Claim(GameObject owner, string shotName, string claim,
        string primaryElementId, string[] foregroundIds, string[] supportingIds,
        string[] backgroundIds, Vector2 target, Vector2 tolerance,
        float minimumViewportHeight = 0.03f, float maximumViewportHeight = 0.9f)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        var marker = ExistingClaim(owner, shotName) ?? owner.AddComponent<GmReviewCompositionClaim>();
        marker.Configure(shotName, claim, primaryElementId, foregroundIds, supportingIds,
            backgroundIds, target, tolerance, minimumViewportHeight, maximumViewportHeight);
        return marker;
    }

    static GmReviewCompositionClaim ExistingClaim(GameObject owner, string shotName)
    {
        foreach (GmReviewCompositionClaim existing in owner.GetComponents<GmReviewCompositionClaim>())
            if (string.Equals(existing.ShotName, shotName, StringComparison.Ordinal)) return existing;
        return null;
    }

    // INTERIM ADAPTER, pending Nick's decision. Full write-up: docs/audit/F1-review-claim-decision.md.
    // The six scaffold composition plans were authored against this shape before Claim existed, so
    // this exists to make them compile without rewriting 48 call sites around semantics nobody has
    // settled yet. It adapts onto Claim and adds no new serialized state. What it assumes:
    //   secondaryElementId lands in supportingIds. Support is the one layer the audit checks with no
    //     depth rule, so it asserts "visible and framed" without inventing a foreground/background
    //     ordering the plans never authored. Foreground and background stay empty for that reason.
    //   scopeClusterOrZoneId and scopeZoneId are ACCEPTED AND NOT VALIDATED. Neither names an element
    //     (across the 48 sites, 0 of the 96 resolve in any plan's element namespace) and the claim
    //     marker has no zone field, so routing them into an element array would only manufacture
    //     "references missing element" findings. They survive in source as authored annotation.
    //   viewportTolerance widens to a symmetric Vector2. That is the positional reading of the call
    //     sites and it is NOT confirmed: several authored values are wider than the acceptance window
    //     the audit's visibility test already applies, which leaves those framing checks unable to
    //     fail. Do not read a pass on those shots as a framing result until Nick rules on the number.
    public static GmReviewCompositionClaim ReviewClaim(GameObject owner, string shotName,
        string primaryElementId, string secondaryElementId, string scopeClusterOrZoneId,
        string scopeZoneId, Vector2 target, float viewportTolerance, string claim)
    {
        string[] supporting = string.IsNullOrEmpty(secondaryElementId)
            ? Array.Empty<string>()
            : new[] { secondaryElementId };
        return Claim(owner, shotName, claim, primaryElementId, Array.Empty<string>(), supporting,
            Array.Empty<string>(), target, new Vector2(viewportTolerance, viewportTolerance));
    }
}
