// Explicit lighting, audio and guarded-variation intent for generated scenes. These components
// describe authored decisions; they do not move objects, select assets, or alter saved scenes.
using System;
using System.Collections.Generic;
using UnityEngine;

public enum GmLightIntentKind
{
    Practical,
    Environmental,
    CompositionFill
}

public enum GmAudioIntentKind
{
    Bed,
    Diegetic,
    Stinger,
    Foley,
    UI
}

public enum GmAudioLoopPolicy
{
    Never,
    Allowed,
    Required
}

[Serializable]
public sealed class GmAdaptiveCandidate
{
    [SerializeField] string candidateId;
    [SerializeField] string assetGuid;
    [SerializeField] string assetFamily;
    [SerializeField] Vector3 localOffset;
    [SerializeField] float yawOffset;
    [SerializeField] Vector3 scaleMultiplier = Vector3.one;

    public string CandidateId => candidateId;
    public string AssetGuid => assetGuid;
    public string AssetFamily => assetFamily;
    public Vector3 LocalOffset => localOffset;
    public float YawOffset => yawOffset;
    public Vector3 ScaleMultiplier => scaleMultiplier;

    public GmAdaptiveCandidate(string id, string guid, string family, Vector3 offset,
        float yaw, Vector3 scale)
    {
        candidateId = id;
        assetGuid = guid;
        assetFamily = family;
        localOffset = offset;
        yawOffset = yaw;
        scaleMultiplier = scale;
    }
}

public static class GmAdaptiveIntentAuthoring
{
    static T AddOrGet<T>(GameObject owner) where T : Component
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        return owner.GetComponent<T>() ?? owner.AddComponent<T>();
    }

    public static GmLightIntent Light(GameObject owner, string id, GmLightIntentKind kind,
        string rationale, string sourceElementId = "", string subjectElementId = "",
        float maximumSourceDistance = 1.5f, float maximumSubjectDistance = 16f)
    {
        var intent = AddOrGet<GmLightIntent>(owner);
        intent.Configure(id, kind, rationale, sourceElementId, subjectElementId,
            maximumSourceDistance, maximumSubjectDistance);
        return intent;
    }

    public static GmAudioIntent Audio(GameObject owner, string id, GmAudioIntentKind kind,
        GmAudioLoopPolicy loopPolicy, string rationale, string sourceElementId = "")
    {
        var intent = AddOrGet<GmAudioIntent>(owner);
        intent.Configure(id, kind, loopPolicy, rationale, sourceElementId);
        return intent;
    }

    public static GmAdaptiveSlot Slot(GameObject owner, string id, GmCompositionRole role,
        string zoneId, string clusterId, string rationale, string[] allowedFamilies,
        string[] swapElementIds, Vector3 maximumOffset, float maximumYaw, Vector2 scaleRange,
        float groundingTolerance, float collisionPadding, int seed,
        GmAdaptiveCandidate[] candidates)
    {
        var slot = AddOrGet<GmAdaptiveSlot>(owner);
        slot.Configure(id, role, zoneId, clusterId, rationale, allowedFamilies, swapElementIds,
            maximumOffset, maximumYaw, scaleRange, groundingTolerance, collisionPadding, seed,
            candidates);
        return slot;
    }
}
