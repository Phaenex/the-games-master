using System;
using UnityEngine;

public enum GmParlorMotionPhase
{
    Approach,
    Contact,
    Manipulate,
    Release,
    Settle,
    Complete,
}

public sealed class GmParlorCardMotion
{
    public const float ApproachSeconds = 0.16f;
    public const float ContactSeconds = 0.08f;
    public const float ManipulateSeconds = 0.18f;
    public const float ReleaseSeconds = 0.08f;
    public const float SettleSeconds = 0.12f;
    public const float FullDuration = ApproachSeconds + ContactSeconds + ManipulateSeconds +
        ReleaseSeconds + SettleSeconds;

    GmParlorCardView view;
    GmParlorCardBinding target;
    Vector3 startPosition;
    Quaternion startRotation;
    Vector3 targetPosition;
    Quaternion targetRotation;
    float phaseElapsed;
    float durationScale = 1f;

    public GmParlorMotionPhase Phase { get; private set; } = GmParlorMotionPhase.Complete;
    public float ApproachHeight { get; private set; }
    public float TotalDuration => FullDuration * durationScale;

    public void Begin(GmParlorCardView cardView, GmParlorCardBinding targetBinding,
        bool reducedMotion)
    {
        view = cardView != null ? cardView : throw new ArgumentNullException(nameof(cardView));
        if (view.PhysicalCard != targetBinding.PhysicalCard)
            throw new ArgumentException("Motion target does not match the physical card", nameof(targetBinding));
        target = targetBinding;
        startPosition = view.transform.localPosition;
        startRotation = view.transform.localRotation;
        targetPosition = GmParlorTableLayout.LocalPosition(targetBinding);
        targetRotation = GmParlorTableLayout.LocalRotation(targetBinding);
        durationScale = reducedMotion ? 0.20f : 1f;
        ApproachHeight = reducedMotion ? 0.012f : 0.055f;
        phaseElapsed = 0f;
        Phase = GmParlorMotionPhase.Approach;
        ApplyPose();
    }

    public void Advance(float deltaSeconds)
    {
        if (deltaSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        float remaining = deltaSeconds;
        while (Phase != GmParlorMotionPhase.Complete)
        {
            float duration = PhaseDuration(Phase) * durationScale;
            float available = Mathf.Max(0f, duration - phaseElapsed);
            float consumed = Mathf.Min(remaining, available);
            phaseElapsed += consumed;
            remaining -= consumed;
            ApplyPose();
            if (phaseElapsed + 0.000001f < duration) break;

            phaseElapsed = 0f;
            Phase++;
            if (Phase == GmParlorMotionPhase.Complete)
            {
                view.ApplyBinding(target);
                break;
            }
            if (remaining <= 0f) break;
        }
    }

    public void FastForward()
    {
        if (Phase == GmParlorMotionPhase.Complete) return;
        view.ApplyBinding(target);
        phaseElapsed = 0f;
        Phase = GmParlorMotionPhase.Complete;
    }

    void ApplyPose()
    {
        float duration = PhaseDuration(Phase) * durationScale;
        float progress = duration <= 0f ? 1f : Mathf.Clamp01(phaseElapsed / duration);
        float eased = progress * progress * (3f - 2f * progress);
        Vector3 startRaised = startPosition + Vector3.up * ApproachHeight;
        Vector3 targetRaised = targetPosition + Vector3.up * ApproachHeight;
        switch (Phase)
        {
            case GmParlorMotionPhase.Approach:
                view.transform.localPosition = Vector3.LerpUnclamped(startPosition, startRaised, eased);
                break;
            case GmParlorMotionPhase.Contact:
                view.transform.localPosition = startRaised + Vector3.down * (0.004f * eased);
                break;
            case GmParlorMotionPhase.Manipulate:
                view.transform.localPosition = Vector3.LerpUnclamped(
                    startRaised + Vector3.down * 0.004f, targetRaised, eased);
                view.transform.localRotation = Quaternion.SlerpUnclamped(startRotation, targetRotation, eased);
                break;
            case GmParlorMotionPhase.Release:
                view.transform.localPosition = Vector3.LerpUnclamped(
                    targetRaised, targetPosition + Vector3.up * 0.010f, eased);
                view.transform.localRotation = targetRotation;
                break;
            case GmParlorMotionPhase.Settle:
                view.transform.localPosition = Vector3.LerpUnclamped(
                    targetPosition + Vector3.up * 0.010f, targetPosition, eased);
                view.transform.localRotation = targetRotation;
                break;
        }
    }

    static float PhaseDuration(GmParlorMotionPhase phase)
    {
        switch (phase)
        {
            case GmParlorMotionPhase.Approach: return ApproachSeconds;
            case GmParlorMotionPhase.Contact: return ContactSeconds;
            case GmParlorMotionPhase.Manipulate: return ManipulateSeconds;
            case GmParlorMotionPhase.Release: return ReleaseSeconds;
            case GmParlorMotionPhase.Settle: return SettleSeconds;
            default: return 0f;
        }
    }
}
