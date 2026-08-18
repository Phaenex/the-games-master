using System;
using System.Collections.Generic;
using UnityEngine;

public enum GmParlorEvidenceChannel
{
    HandMotion,
    CardContact,
    Caption,
    FocusLog,
}

public readonly struct GmParlorAccessibilityProfile
{
    public static readonly GmParlorAccessibilityProfile Default =
        new GmParlorAccessibilityProfile(false, false, true, false);

    public readonly bool Captions;
    public readonly bool ReducedMotion;
    public readonly bool Vibration;
    public readonly bool MonoAudio;

    public GmParlorAccessibilityProfile(bool captions, bool reducedMotion,
        bool vibration, bool monoAudio)
    {
        Captions = captions;
        ReducedMotion = reducedMotion;
        Vibration = vibration;
        MonoAudio = monoAudio;
    }

    public static GmParlorAccessibilityProfile FromGlobal() =>
        new GmParlorAccessibilityProfile(
            GmAccessibilitySettings.Captions,
            GmAccessibilitySettings.ReducedMotion,
            GmAccessibilitySettings.Vibration,
            GmAccessibilitySettings.MonoAudio);
}

public readonly struct GmParlorEvidenceCue
{
    public readonly ulong CommandId;
    public readonly GmTellObservation Observation;
    public readonly IReadOnlyList<GmParlorEvidenceChannel> Channels;
    public readonly float MinimumReadableSeconds;
    public readonly string CaptionText;

    internal GmParlorEvidenceCue(ulong commandId, GmTellObservation observation,
        GmParlorEvidenceChannel[] channels, float minimumReadableSeconds, string captionText)
    {
        CommandId = commandId;
        Observation = observation;
        Channels = Array.AsReadOnly(channels);
        MinimumReadableSeconds = minimumReadableSeconds;
        CaptionText = captionText ?? string.Empty;
    }
}

/// <summary>
/// Interprets the public OpenJudgement observation for an authored character rig. The current scene
/// wires a hand/sleeve/contact proxy into these same sockets; it is deliberately not represented as
/// a final full-body Aldric asset.
/// </summary>
public sealed class GmParlorAldricPresenter : MonoBehaviour
{
    const float ReadableSeconds = 0.75f;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    Transform rightHandCue;
    Renderer sleeveCue;
    Transform contactCue;
    GmParlorEvidenceLog evidenceLog;
    Vector3 neutralHandPosition;
    MaterialPropertyBlock propertyBlock;
    bool characterMotionAvailable;

    public bool IsConfigured => rightHandCue != null && sleeveCue != null &&
        contactCue != null && evidenceLog != null;
    public bool UsesSubstituteProxy { get; private set; } = true;
    public bool HasCharacterMotionChannel => characterMotionAvailable;
    public GmParlorEvidenceCue LastCue { get; private set; }

    public event Action<GmParlorEvidenceCue> OnCuePresented;

    void Awake()
    {
        TryAutoConfigure(out _);
    }

    public bool TryAutoConfigure(out string error)
    {
        if (IsConfigured)
        {
            error = string.Empty;
            return true;
        }
        Transform hand = transform.Find("RightHandCue");
        Transform contact = transform.Find("CardContactCue");
        Renderer sleeve = transform.Find("SleeveCue")?.GetComponent<Renderer>();
        GmParlorEvidenceLog log = GetComponent<GmParlorEvidenceLog>() ??
            GetComponentInParent<GmParlorEvidenceLog>();
        if (hand != null && contact != null && sleeve != null && log != null)
            return TryConfigure(hand, sleeve, contact, log, out error);
        error = "Aldric presenter could not find its authored proxy bindings";
        return false;
    }

    public bool TryConfigure(Transform rightHand, Renderer sleeve, Transform contact,
        GmParlorEvidenceLog log, out string error)
    {
        if (rightHand == null || sleeve == null || contact == null || log == null)
        {
            error = "Aldric presenter needs hand, sleeve, card-contact, and evidence-log bindings";
            return false;
        }
        rightHandCue = rightHand;
        sleeveCue = sleeve;
        contactCue = contact;
        evidenceLog = log;
        neutralHandPosition = rightHand.localPosition;
        Renderer handRenderer = rightHand.GetComponentInChildren<Renderer>(true);
        characterMotionAvailable = sleeve.enabled && handRenderer != null && handRenderer.enabled;
        propertyBlock ??= new MaterialPropertyBlock();
        error = string.Empty;
        return true;
    }

    public bool TryPresent(GmParlorPresentationCommand command,
        GmParlorAccessibilityProfile accessibility, out GmParlorEvidenceCue cue,
        out string error)
    {
        cue = default;
        if (!IsConfigured)
        {
            error = "Aldric presenter is not configured";
            return false;
        }
        if (command.Action != GmParlorPresentationAction.OpenJudgement)
        {
            error = "Aldric presenter accepts only OpenJudgement commands";
            return false;
        }

        bool suspicious = command.Observation == GmTellObservation.Suspicious;
        if (!evidenceLog.TryRecordObservation(command.Id, command.Observation))
            return FailEvidence(out cue, out error);
        ApplyProxyPose(suspicious, accessibility.ReducedMotion);
        cue = BuildCue(command.Id, command.Observation, accessibility);
        LastCue = cue;
        error = string.Empty;
        return true;
    }

    public bool TryNotifyCuePresented(out string error)
    {
        Delegate[] listeners = OnCuePresented?.GetInvocationList();
        if (listeners == null)
        {
            error = string.Empty;
            return true;
        }
        for (int index = 0; index < listeners.Length; index++)
        {
            try { ((Action<GmParlorEvidenceCue>)listeners[index]).Invoke(LastCue); }
            catch (Exception exception)
            {
                error = $"Aldric cue observer failed: {exception.Message}";
                return false;
            }
        }
        error = string.Empty;
        return true;
    }

    /// <summary>Rebuilds the current public cue after load without recording or emitting it.</summary>
    public bool TryRestoreCurrentObservation(GmParlorPresentationCommand command,
        GmParlorAccessibilityProfile accessibility, out GmParlorEvidenceCue cue,
        out string error)
    {
        cue = default;
        if (!IsConfigured || command.Action != GmParlorPresentationAction.OpenJudgement)
        {
            error = "Aldric restore requires a configured OpenJudgement command";
            return false;
        }
        if (!evidenceLog.TryRecordObservation(command.Id, command.Observation))
            return FailEvidence(out cue, out error);
        ApplyProxyPose(command.Observation == GmTellObservation.Suspicious,
            accessibility.ReducedMotion);
        cue = BuildCue(command.Id, command.Observation, accessibility);
        LastCue = cue;
        error = string.Empty;
        return true;
    }

    /// <summary>Re-evaluates only the active pose. It never records facts or re-emits the cue.</summary>
    public void RefreshAccessibility(GmParlorAccessibilityProfile accessibility)
    {
        if (!IsConfigured || LastCue.CommandId == 0) return;
        ApplyProxyPose(LastCue.Observation == GmTellObservation.Suspicious,
            accessibility.ReducedMotion);
        var channels = new List<GmParlorEvidenceChannel>(4)
        {
            GmParlorEvidenceChannel.CardContact,
            GmParlorEvidenceChannel.FocusLog,
        };
        if (!accessibility.ReducedMotion && characterMotionAvailable)
            channels.Add(GmParlorEvidenceChannel.HandMotion);
        if (accessibility.Captions)
            channels.Add(GmParlorEvidenceChannel.Caption);
        LastCue = new GmParlorEvidenceCue(LastCue.CommandId, LastCue.Observation,
            channels.ToArray(), LastCue.MinimumReadableSeconds, LastCue.CaptionText);
    }

    void ApplyProxyPose(bool suspicious, bool reducedMotion)
    {
        rightHandCue.localPosition = suspicious && !reducedMotion && characterMotionAvailable
            ? neutralHandPosition + new Vector3(0.025f, 0.018f, -0.035f)
            : neutralHandPosition;
        contactCue.gameObject.SetActive(suspicious);

        sleeveCue.GetPropertyBlock(propertyBlock);
        Color baseColor = suspicious
            ? new Color(0.23f, 0.035f, 0.04f, 1f)
            : new Color(0.075f, 0.055f, 0.05f, 1f);
        propertyBlock.SetColor(BaseColorId, baseColor);
        sleeveCue.SetPropertyBlock(propertyBlock);
    }

    GmParlorEvidenceCue BuildCue(ulong commandId, GmTellObservation observation,
        GmParlorAccessibilityProfile accessibility)
    {
        var channels = new List<GmParlorEvidenceChannel>(4)
        {
            GmParlorEvidenceChannel.CardContact,
            GmParlorEvidenceChannel.FocusLog,
        };
        if (!accessibility.ReducedMotion && characterMotionAvailable)
            channels.Add(GmParlorEvidenceChannel.HandMotion);
        if (accessibility.Captions) channels.Add(GmParlorEvidenceChannel.Caption);
        string captionText = observation == GmTellObservation.Suspicious
            ? "His right hand stopped above the deck. The card left the baize, then touched it again."
            : "He set the card down without pausing.";
        return new GmParlorEvidenceCue(commandId, observation, channels.ToArray(),
            ReadableSeconds, captionText);
    }

    bool FailEvidence(out GmParlorEvidenceCue cue, out string error)
    {
        cue = default;
        error = string.IsNullOrEmpty(evidenceLog.LastPersistenceError)
            ? "Aldric evidence could not be recorded"
            : evidenceLog.LastPersistenceError;
        return false;
    }
}
