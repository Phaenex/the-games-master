using System;

public enum GmAldricActionRole
{
    Lead,
    Follow,
    Judgement,
}

public enum GmAldricPublicOutcome
{
    None,
    CorrectRead,
    FalseAccusation,
    RoundWon,
    RoundLost,
    MatchWon,
    MatchLost,
    Rematch,
}

public enum GmAldricMotionIntent
{
    CalmLead,
    CalmFollow,
    CalmJudgement,
    SuspiciousLead,
    SuspiciousFollow,
    SuspiciousJudgement,
}

public enum GmAldricIkIntent
{
    RightHandToLeadCard,
    RightHandToFollowCard,
    RightHandToJudgementCard,
}

public enum GmAldricGazeIntent
{
    LeadCard,
    FollowCard,
    JudgementCard,
}

public enum GmAldricAudioIntent
{
    // The project currently has no licensed Aldric foley or VO. A semantic plan must say so
    // explicitly instead of pretending a missing clip or synthetic marker is a shipped channel.
    Unavailable,
}

public enum GmAldricCameraIntent
{
    None,
}

public enum GmAldricPublicReaction
{
    None,
    Caught,
    FalselyAccused,
    RoundWin,
    RoundLoss,
    MatchWin,
    MatchLoss,
    Rematch,
}

/// <summary>
/// The complete input boundary for Aldric's performance selection. Every field is already public to
/// the player at the moment the plan is created. Hidden match truth has no representation here.
/// </summary>
public readonly struct GmAldricPerformanceRequest
{
    public readonly GmTellObservation Observation;
    public readonly GmAldricActionRole ActionRole;
    public readonly int TrickOrdinal;
    public readonly ulong StableCommandId;
    public readonly GmAldricPublicOutcome PublicOutcome;

    public GmAldricPerformanceRequest(GmTellObservation observation,
        GmAldricActionRole actionRole, int trickOrdinal, ulong stableCommandId,
        GmAldricPublicOutcome publicOutcome)
    {
        ValidateObservation(observation, nameof(observation));
        ValidateRole(actionRole, nameof(actionRole));
        ValidateOutcome(publicOutcome, nameof(publicOutcome));
        if (trickOrdinal < 0) throw new ArgumentOutOfRangeException(nameof(trickOrdinal));
        if (stableCommandId == 0) throw new ArgumentOutOfRangeException(nameof(stableCommandId));
        Observation = observation;
        ActionRole = actionRole;
        TrickOrdinal = trickOrdinal;
        StableCommandId = stableCommandId;
        PublicOutcome = publicOutcome;
    }

    internal void Validate()
    {
        ValidateObservation(Observation, nameof(Observation));
        ValidateRole(ActionRole, nameof(ActionRole));
        ValidateOutcome(PublicOutcome, nameof(PublicOutcome));
        if (TrickOrdinal < 0) throw new ArgumentOutOfRangeException(nameof(TrickOrdinal));
        if (StableCommandId == 0) throw new ArgumentOutOfRangeException(nameof(StableCommandId));
    }

    static void ValidateObservation(GmTellObservation observation, string name)
    {
        if (observation != GmTellObservation.Calm && observation != GmTellObservation.Suspicious)
            throw new ArgumentOutOfRangeException(name);
    }

    static void ValidateRole(GmAldricActionRole role, string name)
    {
        if (role != GmAldricActionRole.Lead && role != GmAldricActionRole.Follow &&
            role != GmAldricActionRole.Judgement) throw new ArgumentOutOfRangeException(name);
    }

    static void ValidateOutcome(GmAldricPublicOutcome outcome, string name)
    {
        if (outcome < GmAldricPublicOutcome.None || outcome > GmAldricPublicOutcome.Rematch)
            throw new ArgumentOutOfRangeException(name);
    }
}

public readonly struct GmAldricPreJudgementTrace : IEquatable<GmAldricPreJudgementTrace>
{
    public readonly GmAldricMotionIntent Motion;
    public readonly GmAldricIkIntent Ik;
    public readonly GmAldricGazeIntent Gaze;
    public readonly GmAldricAudioIntent Foley;
    public readonly GmAldricAudioIntent Voice;
    public readonly GmAldricCameraIntent Camera;
    public readonly int Variant;
    public readonly int HoverMilliseconds;
    public readonly int MinimumEvidenceMilliseconds;

    internal GmAldricPreJudgementTrace(GmAldricMotionIntent motion, GmAldricIkIntent ik,
        GmAldricGazeIntent gaze, int variant, int hoverMilliseconds)
    {
        Motion = motion;
        Ik = ik;
        Gaze = gaze;
        Foley = GmAldricAudioIntent.Unavailable;
        Voice = GmAldricAudioIntent.Unavailable;
        Camera = GmAldricCameraIntent.None;
        Variant = variant;
        HoverMilliseconds = hoverMilliseconds;
        MinimumEvidenceMilliseconds = 750;
    }

    public bool Equals(GmAldricPreJudgementTrace other)
    {
        return Motion == other.Motion && Ik == other.Ik && Gaze == other.Gaze &&
            Foley == other.Foley && Voice == other.Voice && Camera == other.Camera &&
            Variant == other.Variant && HoverMilliseconds == other.HoverMilliseconds &&
            MinimumEvidenceMilliseconds == other.MinimumEvidenceMilliseconds;
    }

    public override bool Equals(object obj) =>
        obj is GmAldricPreJudgementTrace other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = (int)Motion;
            hash = (hash * 397) ^ (int)Ik;
            hash = (hash * 397) ^ (int)Gaze;
            hash = (hash * 397) ^ Variant;
            hash = (hash * 397) ^ HoverMilliseconds;
            return hash;
        }
    }
}

public readonly struct GmAldricPerformancePlan : IEquatable<GmAldricPerformancePlan>
{
    public readonly GmAldricPreJudgementTrace PreJudgementTrace;
    public readonly GmAldricPublicReaction Reaction;
    public readonly bool ReactionStartsAfterPublicOutcome;
    public readonly int PublicRestorePoseId;

    internal GmAldricPerformancePlan(GmAldricPreJudgementTrace preJudgementTrace,
        GmAldricPublicReaction reaction, bool reactionStartsAfterPublicOutcome,
        int publicRestorePoseId)
    {
        PreJudgementTrace = preJudgementTrace;
        Reaction = reaction;
        ReactionStartsAfterPublicOutcome = reactionStartsAfterPublicOutcome;
        PublicRestorePoseId = publicRestorePoseId;
    }

    public bool Equals(GmAldricPerformancePlan other)
    {
        return PreJudgementTrace.Equals(other.PreJudgementTrace) &&
            Reaction == other.Reaction &&
            ReactionStartsAfterPublicOutcome == other.ReactionStartsAfterPublicOutcome &&
            PublicRestorePoseId == other.PublicRestorePoseId;
    }

    public override bool Equals(object obj) =>
        obj is GmAldricPerformancePlan other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = PreJudgementTrace.GetHashCode();
            hash = (hash * 397) ^ (int)Reaction;
            hash = (hash * 397) ^ PublicRestorePoseId;
            return hash;
        }
    }
}

/// <summary>
/// Freezes performance variation from public semantic state. This class never reads the match,
/// rules adapter, persistence store, transforms, Animator state, frame time, or random state.
/// </summary>
public static class GmAldricPerformancePlanner
{
    static readonly int[] SuspiciousHoverMilliseconds = { 180, 220, 260 };

    public static GmAldricPerformancePlan Build(GmAldricPerformanceRequest request)
    {
        request.Validate();
        uint stable = StableHash(request.StableCommandId, request.TrickOrdinal,
            (int)request.ActionRole, (int)request.Observation);
        int variant = (int)(stable % 3u);
        bool suspicious = request.Observation == GmTellObservation.Suspicious;
        int hover = suspicious ? SuspiciousHoverMilliseconds[variant] : 0;
        var trace = new GmAldricPreJudgementTrace(
            MotionFor(request.ActionRole, suspicious),
            IkFor(request.ActionRole),
            GazeFor(request.ActionRole),
            variant,
            hover);
        GmAldricPublicReaction reaction = ReactionFor(request.PublicOutcome);
        int restorePoseId = 1 + ((int)request.ActionRole * 8) +
            ((int)request.Observation * 4) + variant;
        return new GmAldricPerformancePlan(trace, reaction,
            request.PublicOutcome != GmAldricPublicOutcome.None, restorePoseId);
    }

    static GmAldricMotionIntent MotionFor(GmAldricActionRole role, bool suspicious)
    {
        if (suspicious)
        {
            switch (role)
            {
                case GmAldricActionRole.Lead: return GmAldricMotionIntent.SuspiciousLead;
                case GmAldricActionRole.Follow: return GmAldricMotionIntent.SuspiciousFollow;
                default: return GmAldricMotionIntent.SuspiciousJudgement;
            }
        }
        switch (role)
        {
            case GmAldricActionRole.Lead: return GmAldricMotionIntent.CalmLead;
            case GmAldricActionRole.Follow: return GmAldricMotionIntent.CalmFollow;
            default: return GmAldricMotionIntent.CalmJudgement;
        }
    }

    static GmAldricIkIntent IkFor(GmAldricActionRole role)
    {
        switch (role)
        {
            case GmAldricActionRole.Lead: return GmAldricIkIntent.RightHandToLeadCard;
            case GmAldricActionRole.Follow: return GmAldricIkIntent.RightHandToFollowCard;
            default: return GmAldricIkIntent.RightHandToJudgementCard;
        }
    }

    static GmAldricGazeIntent GazeFor(GmAldricActionRole role)
    {
        switch (role)
        {
            case GmAldricActionRole.Lead: return GmAldricGazeIntent.LeadCard;
            case GmAldricActionRole.Follow: return GmAldricGazeIntent.FollowCard;
            default: return GmAldricGazeIntent.JudgementCard;
        }
    }

    static GmAldricPublicReaction ReactionFor(GmAldricPublicOutcome outcome)
    {
        switch (outcome)
        {
            case GmAldricPublicOutcome.CorrectRead: return GmAldricPublicReaction.Caught;
            case GmAldricPublicOutcome.FalseAccusation: return GmAldricPublicReaction.FalselyAccused;
            case GmAldricPublicOutcome.RoundWon: return GmAldricPublicReaction.RoundWin;
            case GmAldricPublicOutcome.RoundLost: return GmAldricPublicReaction.RoundLoss;
            case GmAldricPublicOutcome.MatchWon: return GmAldricPublicReaction.MatchWin;
            case GmAldricPublicOutcome.MatchLost: return GmAldricPublicReaction.MatchLoss;
            case GmAldricPublicOutcome.Rematch: return GmAldricPublicReaction.Rematch;
            default: return GmAldricPublicReaction.None;
        }
    }

    static uint StableHash(ulong commandId, int trickOrdinal, int role, int observation)
    {
        unchecked
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)commandId) * 16777619u;
            hash = (hash ^ (uint)(commandId >> 32)) * 16777619u;
            hash = (hash ^ (uint)trickOrdinal) * 16777619u;
            hash = (hash ^ (uint)role) * 16777619u;
            return (hash ^ (uint)observation) * 16777619u;
        }
    }
}
