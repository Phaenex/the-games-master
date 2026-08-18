using System;

[Flags]
public enum GmSemanticResource
{
    None = 0,
    AldricRightHand = 1 << 0,
    AldricLeftHand = 1 << 1,
    PlayerRightHand = 1 << 2,
    PlayerLeftHand = 1 << 3,
    TableCard = 1 << 4,
    CameraEmphasis = 1 << 5,
}

public enum GmSemanticActionPhase
{
    Approach,
    Contact,
    Manipulate,
    Release,
    Settle,
    Complete,
}

public readonly struct GmSemanticActionRequest
{
    const GmSemanticResource AllResources = GmSemanticResource.AldricRightHand |
        GmSemanticResource.AldricLeftHand | GmSemanticResource.PlayerRightHand |
        GmSemanticResource.PlayerLeftHand | GmSemanticResource.TableCard |
        GmSemanticResource.CameraEmphasis;
    public readonly ulong StableActionId;
    public readonly GmSemanticResource Resources;
    public readonly int PublicPoseId;
    public readonly float ApproachSeconds;
    public readonly float ContactSeconds;
    public readonly float ManipulateSeconds;
    public readonly float ReleaseSeconds;
    public readonly float SettleSeconds;

    public GmSemanticActionRequest(ulong stableActionId, GmSemanticResource resources,
        int publicPoseId, float approachSeconds, float contactSeconds, float manipulateSeconds,
        float releaseSeconds, float settleSeconds)
    {
        if (stableActionId == 0) throw new ArgumentOutOfRangeException(nameof(stableActionId));
        if (resources == GmSemanticResource.None)
            throw new ArgumentOutOfRangeException(nameof(resources));
        if ((resources & ~AllResources) != 0)
            throw new ArgumentOutOfRangeException(nameof(resources));
        ValidateDuration(approachSeconds, nameof(approachSeconds));
        ValidateDuration(contactSeconds, nameof(contactSeconds));
        ValidateDuration(manipulateSeconds, nameof(manipulateSeconds));
        ValidateDuration(releaseSeconds, nameof(releaseSeconds));
        ValidateDuration(settleSeconds, nameof(settleSeconds));
        StableActionId = stableActionId;
        Resources = resources;
        PublicPoseId = publicPoseId;
        ApproachSeconds = approachSeconds;
        ContactSeconds = contactSeconds;
        ManipulateSeconds = manipulateSeconds;
        ReleaseSeconds = releaseSeconds;
        SettleSeconds = settleSeconds;
    }

    static void ValidateDuration(float duration, string name)
    {
        if (duration < 0f || float.IsNaN(duration) || float.IsInfinity(duration))
            throw new ArgumentOutOfRangeException(name);
    }

    internal bool IsValid => StableActionId != 0 && Resources != GmSemanticResource.None &&
        (Resources & ~AllResources) == 0 && IsValidDuration(ApproachSeconds) &&
        IsValidDuration(ContactSeconds) && IsValidDuration(ManipulateSeconds) &&
        IsValidDuration(ReleaseSeconds) && IsValidDuration(SettleSeconds);

    static bool IsValidDuration(float duration) => duration >= 0f &&
        !float.IsNaN(duration) && !float.IsInfinity(duration);
}

public interface IGmSemanticActionDriver
{
    bool TryEnterPhase(in GmSemanticActionRequest request,
        GmSemanticActionPhase phase, out string error);
    void SnapToSettled(in GmSemanticActionRequest request);
    void Complete(in GmSemanticActionRequest request);
    void RestorePublicPose(int publicPoseId);
}

public interface IGmSemanticActionContact
{
    bool TryAttach(in GmSemanticActionRequest request, out string error);
    void Release(in GmSemanticActionRequest request);
    /// <summary>
    /// Detaches without normal release foley, markers, or secondary effects. Implementations must
    /// clear their external attachment in a finally block before propagating an exception; the
    /// executor can contain and report an adapter fault but cannot repair state owned by an adapter.
    /// </summary>
    void Cancel(in GmSemanticActionRequest request);
}

public interface IGmSemanticActionDiagnostics
{
    void ReportFailure(ulong actionId, GmSemanticActionPhase phase, string error);
}

/// <summary>
/// Shared, fixed-size ownership for actor limbs and props. Claims are semantic gates, not rules
/// state. They exist solely to stop two presentation executors from driving one hand or card.
/// </summary>
public sealed class GmSemanticResourceClaims
{
    const int ResourceCount = 6;
    readonly ulong[] owners = new ulong[ResourceCount];
    ulong nextOwner = 1;

    public GmSemanticResource Claimed
    {
        get
        {
            GmSemanticResource claimed = GmSemanticResource.None;
            for (int bit = 0; bit < ResourceCount; bit++)
                if (owners[bit] != 0) claimed |= (GmSemanticResource)(1 << bit);
            return claimed;
        }
    }

    public bool TryClaim(ulong owner, GmSemanticResource resources)
    {
        if (owner == 0 || resources == GmSemanticResource.None) return false;
        for (int bit = 0; bit < ResourceCount; bit++)
        {
            GmSemanticResource resource = (GmSemanticResource)(1 << bit);
            if ((resources & resource) == 0) continue;
            if (owners[bit] != 0 && owners[bit] != owner) return false;
        }
        for (int bit = 0; bit < ResourceCount; bit++)
        {
            GmSemanticResource resource = (GmSemanticResource)(1 << bit);
            if ((resources & resource) != 0) owners[bit] = owner;
        }
        return true;
    }

    internal ulong CreateOwner()
    {
        if (nextOwner == 0) throw new InvalidOperationException("Semantic claim owner space exhausted");
        return nextOwner++;
    }

    public void Release(ulong owner, GmSemanticResource resources)
    {
        if (owner == 0) return;
        for (int bit = 0; bit < ResourceCount; bit++)
        {
            GmSemanticResource resource = (GmSemanticResource)(1 << bit);
            if ((resources & resource) != 0 && owners[bit] == owner) owners[bit] = 0;
        }
    }
}

/// <summary>
/// Preallocated, explicit-clock presentation executor. It owns no match data and cannot advance a
/// game turn. Animator/IK and prop drivers receive the same five phases; contact is attached and
/// released here instead of through Animator events.
/// </summary>
public sealed class GmSemanticActionExecutor
{
    const int CircuitBreakerThreshold = 3;
    const int AudioMarkerCapacity = 16;
    const float ReducedMotionScale = 0.20f;

    readonly GmSemanticActionRequest[] queue;
    readonly int[] audioMarkers = new int[AudioMarkerCapacity];
    readonly IGmSemanticActionDriver driver;
    readonly IGmSemanticActionContact contact;
    readonly GmSemanticResourceClaims claims;
    readonly IGmSemanticActionDiagnostics diagnostics;
    readonly ulong claimOwner;

    int queueHead;
    int queueCount;
    int audioMarkerCount;
    bool active;
    bool contactHeld;
    float phaseElapsed;
    float speed = 1f;
    int consecutiveFailures;
    bool lastStartWasContention;

    public bool Paused { get; set; }
    public bool ReducedMotion { get; set; }
    public float Speed
    {
        get => speed;
        set
        {
            if (value < 0f || value > 4f || float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            speed = value;
        }
    }
    public GmSemanticActionPhase Phase { get; private set; } = GmSemanticActionPhase.Complete;
    public bool IsIdle => !active && queueCount == 0;
    public int PendingCount => queueCount;
    public int AudioMarkerCount => audioMarkerCount;
    public bool IsCircuitOpen => consecutiveFailures >= CircuitBreakerThreshold;
    public string LastFailure { get; private set; } = string.Empty;

    public GmSemanticActionExecutor(int capacity, IGmSemanticActionDriver actionDriver,
        IGmSemanticActionContact actionContact, GmSemanticResourceClaims resourceClaims,
        IGmSemanticActionDiagnostics actionDiagnostics = null)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        queue = new GmSemanticActionRequest[capacity];
        driver = actionDriver ?? throw new ArgumentNullException(nameof(actionDriver));
        contact = actionContact ?? throw new ArgumentNullException(nameof(actionContact));
        claims = resourceClaims ?? throw new ArgumentNullException(nameof(resourceClaims));
        diagnostics = actionDiagnostics;
        claimOwner = claims.CreateOwner();
    }

    public bool TryEnqueue(GmSemanticActionRequest request, out string error)
    {
        if (!request.IsValid)
        {
            error = "Semantic action request is invalid";
            return false;
        }
        if (IsCircuitOpen)
        {
            error = "Semantic action circuit is open after repeated presentation failures";
            return false;
        }
        if (queueCount >= queue.Length)
        {
            error = "Semantic action queue is full";
            return false;
        }
        bool hadPendingHead = queueCount > 0;
        int tail = (queueHead + queueCount) % queue.Length;
        queue[tail] = request;
        queueCount++;
        if (active || hadPendingHead)
        {
            error = string.Empty;
            return true;
        }
        if (TryStartHead(out error)) return true;
        if (queueCount > 0)
        {
            // Claim contention is not an execution failure. Remove only the action this caller
            // tried to start, leave the circuit counter untouched, and let the owner finish.
            PopHead();
        }
        return false;
    }

    public void Advance(float deltaSeconds)
    {
        if (deltaSeconds < 0f || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds))
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        if (Paused || deltaSeconds == 0f || speed == 0f) return;
        if (!active)
        {
            if (queueCount == 0 || !TryStartHead(out _)) return;
        }
        float remaining = deltaSeconds * speed;
        while (active)
        {
            float duration = CurrentPhaseDuration();
            float available = duration - phaseElapsed;
            if (available < 0f) available = 0f;
            float consumed = remaining < available ? remaining : available;
            phaseElapsed += consumed;
            remaining -= consumed;
            if (phaseElapsed + 0.000001f < duration) return;
            if (!Transition(out string error))
            {
                // Starting a queued action can fail inside TryStartHead, which already performs the
                // one allowed failure cleanup/report. A phase transition leaves the action active
                // and is cleaned here.
                if (active || queueCount > 0) Fail(error);
                return;
            }
            if (remaining <= 0f) return;
        }
    }

    public bool MarkAudioMarker(int markerId)
    {
        if (!active || markerId == 0 || audioMarkerCount >= audioMarkers.Length) return false;
        audioMarkers[audioMarkerCount++] = markerId;
        return true;
    }

    public void SkipCurrent()
    {
        if (!active) return;
        GmSemanticActionRequest request = queue[queueHead];
        GmSemanticActionPhase skippedPhase = Phase;
        bool succeeded = TryCancelContact(request, out string error);
        if (succeeded) succeeded = TrySnap(request, out error);
        if (succeeded) succeeded = TryComplete(request, out error);
        ReleaseClaim(request);
        PopHead();
        active = false;
        Phase = GmSemanticActionPhase.Complete;
        phaseElapsed = 0f;
        audioMarkerCount = 0;
        if (!succeeded)
        {
            ClearQueue();
            RecordFailure(request.StableActionId, skippedPhase, error);
            return;
        }
        consecutiveFailures = 0;
        LastFailure = string.Empty;
        TryStartHead(out _);
    }

    /// <summary>
    /// Reconstruction path. It releases all transient ownership and applies one already-public pose
    /// without entering phases, completing actions, emitting cues, or replaying audio markers.
    /// </summary>
    public void RestoreSnap(int publicPoseId)
    {
        GmSemanticActionRequest request = default;
        GmSemanticActionPhase restorePhase = Phase;
        bool succeeded = true;
        string error = string.Empty;
        if (active)
        {
            request = queue[queueHead];
            succeeded = TryCancelContact(request, out error);
            ReleaseClaim(request);
        }
        ClearQueue();
        active = false;
        phaseElapsed = 0f;
        Phase = GmSemanticActionPhase.Complete;
        audioMarkerCount = 0;
        try { driver.RestorePublicPose(publicPoseId); }
        catch (Exception exception)
        {
            AppendError(ref error, $"restore adapter threw: {exception.Message}");
            succeeded = false;
        }
        if (!succeeded) RecordFailure(request.StableActionId, restorePhase, error);
    }

    public void ResetCircuitBreaker()
    {
        if (!IsIdle) throw new InvalidOperationException("Cannot reset the circuit while an action is active");
        consecutiveFailures = 0;
        LastFailure = string.Empty;
    }

    bool TryStartHead(out string error)
    {
        lastStartWasContention = false;
        if (queueCount == 0)
        {
            error = string.Empty;
            return true;
        }
        GmSemanticActionRequest request = queue[queueHead];
        if (!claims.TryClaim(claimOwner, request.Resources))
        {
            lastStartWasContention = true;
            error = "One or more semantic action resources are already claimed";
            return false;
        }
        active = true;
        Phase = GmSemanticActionPhase.Approach;
        phaseElapsed = 0f;
        if (TryEnterPhase(request, Phase, out error)) return true;
        Fail(error);
        return false;
    }

    bool Transition(out string error)
    {
        GmSemanticActionRequest request = queue[queueHead];
        phaseElapsed = 0f;
        if (Phase == GmSemanticActionPhase.Settle)
        {
            if (!TrySnap(request, out error)) return false;
            if (!TryComplete(request, out error)) return false;
            ReleaseClaim(request);
            PopHead();
            active = false;
            Phase = GmSemanticActionPhase.Complete;
            consecutiveFailures = 0;
            LastFailure = string.Empty;
            audioMarkerCount = 0;
            if (queueCount == 0)
            {
                error = string.Empty;
                return true;
            }
            if (TryStartHead(out error)) return true;
            if (lastStartWasContention)
            {
                error = string.Empty;
                return true;
            }
            return false;
        }

        GmSemanticActionPhase next = Phase + 1;
        Phase = next;
        if (!TryEnterPhase(request, next, out error)) return false;
        if (next == GmSemanticActionPhase.Contact)
        {
            contactHeld = true;
            if (!TryAttach(request, out error)) return false;
        }
        else if (next == GmSemanticActionPhase.Release)
        {
            if (!TryReleaseContact(request, out error)) return false;
        }
        return true;
    }

    void Fail(string error)
    {
        GmSemanticActionRequest request = queueCount > 0 ? queue[queueHead] : default;
        GmSemanticActionPhase failedPhase = Phase;
        if (active)
        {
            TryCancelContact(request, out string cancelError);
            AppendError(ref error, cancelError);
            TrySnap(request, out string snapError);
            AppendError(ref error, snapError);
            ReleaseClaim(request);
        }
        active = false;
        Phase = GmSemanticActionPhase.Complete;
        phaseElapsed = 0f;
        audioMarkerCount = 0;
        ClearQueue();
        RecordFailure(request.StableActionId, failedPhase, error);
    }

    bool TryEnterPhase(GmSemanticActionRequest request, GmSemanticActionPhase phase,
        out string error)
    {
        try { return driver.TryEnterPhase(request, phase, out error); }
        catch (Exception exception)
        {
            error = $"phase adapter threw: {exception.Message}";
            return false;
        }
    }

    bool TryAttach(GmSemanticActionRequest request, out string error)
    {
        try { return contact.TryAttach(request, out error); }
        catch (Exception exception)
        {
            error = $"contact adapter threw: {exception.Message}";
            return false;
        }
    }

    bool TryReleaseContact(GmSemanticActionRequest request, out string error)
    {
        if (!contactHeld)
        {
            error = string.Empty;
            return true;
        }
        try
        {
            contact.Release(request);
            contactHeld = false;
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = $"release adapter threw: {exception.Message}";
            return false;
        }
    }

    bool TryCancelContact(GmSemanticActionRequest request, out string error)
    {
        if (!contactHeld)
        {
            error = string.Empty;
            return true;
        }
        contactHeld = false;
        try
        {
            contact.Cancel(request);
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = $"cancel adapter threw: {exception.Message}";
            return false;
        }
    }

    bool TrySnap(GmSemanticActionRequest request, out string error)
    {
        try
        {
            driver.SnapToSettled(request);
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = $"snap adapter threw: {exception.Message}";
            return false;
        }
    }

    bool TryComplete(GmSemanticActionRequest request, out string error)
    {
        try
        {
            driver.Complete(request);
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = $"complete adapter threw: {exception.Message}";
            return false;
        }
    }

    void RecordFailure(ulong actionId, GmSemanticActionPhase phase, string error)
    {
        consecutiveFailures++;
        LastFailure = string.IsNullOrEmpty(error) ? "Semantic presentation failed" : error;
        try { diagnostics?.ReportFailure(actionId, phase, LastFailure); }
        catch (Exception exception)
        {
            LastFailure = $"{LastFailure}; diagnostic adapter threw: {exception.Message}";
        }
    }

    static void AppendError(ref string destination, string addition)
    {
        if (string.IsNullOrEmpty(addition)) return;
        destination = string.IsNullOrEmpty(destination) ? addition : $"{destination}; {addition}";
    }

    void ReleaseClaim(GmSemanticActionRequest request) =>
        claims.Release(claimOwner, request.Resources);

    void PopHead()
    {
        queue[queueHead] = default;
        queueHead = (queueHead + 1) % queue.Length;
        queueCount--;
    }

    void ClearQueue()
    {
        while (queueCount > 0) PopHead();
        queueHead = 0;
    }

    float CurrentPhaseDuration()
    {
        GmSemanticActionRequest request = queue[queueHead];
        float duration;
        switch (Phase)
        {
            case GmSemanticActionPhase.Approach: duration = request.ApproachSeconds; break;
            case GmSemanticActionPhase.Contact: duration = request.ContactSeconds; break;
            case GmSemanticActionPhase.Manipulate: duration = request.ManipulateSeconds; break;
            case GmSemanticActionPhase.Release: duration = request.ReleaseSeconds; break;
            case GmSemanticActionPhase.Settle: duration = request.SettleSeconds; break;
            default: return 0f;
        }
        if (ReducedMotion && Phase != GmSemanticActionPhase.Contact)
            duration *= ReducedMotionScale;
        return duration;
    }
}
