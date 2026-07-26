using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GmAdaptiveSlot : MonoBehaviour
{
    [SerializeField] string slotId;
    [SerializeField] GmCompositionRole role = GmCompositionRole.Detail;
    [SerializeField] string zoneId;
    [SerializeField] string clusterId;
    [SerializeField, TextArea] string rationale;
    [SerializeField] string[] allowedAssetFamilies = Array.Empty<string>();
    [SerializeField] string[] allowedSwapElementIds = Array.Empty<string>();
    [SerializeField] Vector3 maximumLocalOffset = new Vector3(0.5f, 0.1f, 0.5f);
    [SerializeField, Range(0f, 180f)] float maximumYawOffset = 30f;
    [SerializeField] Vector2 scaleRange = new Vector2(0.9f, 1.1f);
    [SerializeField] float groundingTolerance = 0.15f;
    [SerializeField] float collisionPadding = 0.08f;
    [SerializeField] int deterministicSeed;
    [SerializeField] GmAdaptiveCandidate[] candidates = Array.Empty<GmAdaptiveCandidate>();

    public string SlotId => slotId;
    public GmCompositionRole Role => role;
    public string ZoneId => zoneId;
    public string ClusterId => clusterId;
    public string Rationale => rationale;
    public IReadOnlyList<string> AllowedAssetFamilies => allowedAssetFamilies;
    public IReadOnlyList<string> AllowedSwapElementIds => allowedSwapElementIds;
    public Vector3 MaximumLocalOffset => maximumLocalOffset;
    public float MaximumYawOffset => maximumYawOffset;
    public Vector2 ScaleRange => scaleRange;
    public float GroundingTolerance => groundingTolerance;
    public float CollisionPadding => collisionPadding;
    public int DeterministicSeed => deterministicSeed;
    public IReadOnlyList<GmAdaptiveCandidate> Candidates => candidates;

    public void Configure(string id, GmCompositionRole adaptiveRole, string zone, string cluster,
        string why, string[] allowedFamilies, string[] swapElementIds, Vector3 maxOffset,
        float maxYaw, Vector2 allowedScaleRange, float groundTolerance, float padding,
        int seed, GmAdaptiveCandidate[] deterministicCandidates)
    {
        slotId = id;
        role = adaptiveRole;
        zoneId = zone;
        clusterId = cluster;
        rationale = why;
        allowedAssetFamilies = allowedFamilies == null ? Array.Empty<string>() : (string[])allowedFamilies.Clone();
        allowedSwapElementIds = swapElementIds == null ? Array.Empty<string>() : (string[])swapElementIds.Clone();
        maximumLocalOffset = new Vector3(Mathf.Abs(maxOffset.x), Mathf.Abs(maxOffset.y), Mathf.Abs(maxOffset.z));
        maximumYawOffset = Mathf.Clamp(Mathf.Abs(maxYaw), 0f, 180f);
        scaleRange = new Vector2(Mathf.Max(0.05f, allowedScaleRange.x), Mathf.Max(allowedScaleRange.x, allowedScaleRange.y));
        groundingTolerance = Mathf.Max(0.01f, groundTolerance);
        collisionPadding = Mathf.Max(0f, padding);
        deterministicSeed = seed;
        candidates = deterministicCandidates == null ? Array.Empty<GmAdaptiveCandidate>() : (GmAdaptiveCandidate[])deterministicCandidates.Clone();
    }
}
