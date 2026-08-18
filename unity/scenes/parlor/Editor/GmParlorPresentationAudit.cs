using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Measurable presentation contract for the shipping Parlor table. Composition and room identity
/// stay in <see cref="GmParlorQualityAudit"/>. This audit only answers whether the physical cards,
/// focus colliders, tell proxy, and restore seams can present a legal Read.
/// </summary>
public static class GmParlorPresentationAudit
{
    public const float AuthoredBaizeY = 0.76f;
    public const float MaxCardPenetrationBelowTable = 0.03f;
    public const float MaxFocusColliderExtent = 0.35f;
    public const float MinFocusColliderWidth = 0.12f;
    public const float MinFocusColliderHeight = 0.18f;
    public const float MaxHandReadDistance = 2.25f;
    public const float MaxContactHeightAboveTable = 0.08f;
    public const int MinimumReadableEvidenceChannels = 2;

    public static List<string> ValidateOpenScene()
    {
        var issues = new List<string>();
        GmParlorCardView[] cards = UnityEngine.Object.FindObjectsByType<GmParlorCardView>(
            FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(card => card.gameObject.scene.IsValid()).ToArray();
        GmParlorAldricPresenter presenter =
            UnityEngine.Object.FindAnyObjectByType<GmParlorAldricPresenter>();
        GmParlorPresentationCoordinator coordinator =
            UnityEngine.Object.FindAnyObjectByType<GmParlorPresentationCoordinator>();
        GmParlorEvidenceLog evidence =
            UnityEngine.Object.FindAnyObjectByType<GmParlorEvidenceLog>();
        Transform physicalCards = FindNamed("PhysicalCards");
        Transform rightHand = FindNamed("RightHandCue");
        Transform sleeve = FindNamed("SleeveCue");
        Transform contact = FindNamed("CardContactCue");
        Camera playerCamera = PlayerCamera();

        AuditCardIdentities(cards, issues);
        AuditFocusColliders(cards, issues);
        AuditCardTablePenetration(cards, issues);
        AuditHandAndContactClearance(rightHand, sleeve, contact, issues);
        AuditLegibilityDistance(physicalCards, playerCamera, issues);
        AuditPresenterBindings(presenter, rightHand, sleeve, contact, evidence, issues);
        AuditCommandRecovery(coordinator, issues);
        AuditReducedMotionSupport(coordinator, issues);
        AuditEvidenceChannelContract(issues);
        return issues;
    }

    static void AuditCardIdentities(GmParlorCardView[] cards, List<string> issues)
    {
        if (cards.Length != GmParlorCore.TotalCards)
        {
            issues.Add($"physical card count is {cards.Length}, expected {GmParlorCore.TotalCards}");
            return;
        }

        GmCard[] identities = cards.Select(card => card.PhysicalCard).ToArray();
        if (identities.Distinct().Count() != GmParlorCore.TotalCards)
            issues.Add("physical card identities are duplicated; every canonical card needs one view");

        int rankMarks = cards.SelectMany(card => card.GetComponentsInChildren<Transform>(true))
            .Count(child => child.name.StartsWith("Rank_"));
        int suitMarks = cards.SelectMany(card => card.GetComponentsInChildren<Transform>(true))
            .Count(child => child.name.StartsWith("Suit_"));
        if (rankMarks != GmParlorCore.TotalCards * 2)
            issues.Add($"card rank marks are {rankMarks}, expected {GmParlorCore.TotalCards * 2}");
        if (suitMarks != GmParlorCore.TotalCards)
            issues.Add($"card suit marks are {suitMarks}, expected {GmParlorCore.TotalCards}");
    }

    static void AuditFocusColliders(GmParlorCardView[] cards, List<string> issues)
    {
        foreach (GmParlorCardView card in cards)
        {
            BoxCollider focus = card.GetComponent<BoxCollider>();
            if (focus == null)
            {
                issues.Add($"{card.name} has no focus collider");
                continue;
            }

            Vector3 size = focus.size;
            if (size.x < MinFocusColliderWidth || size.y < MinFocusColliderHeight)
                issues.Add($"{card.name} focus collider {size} is too small to select");
            if (size.x > MaxFocusColliderExtent || size.y > MaxFocusColliderExtent ||
                size.z > MaxFocusColliderExtent)
                issues.Add($"{card.name} focus collider {size} is larger than a card");
        }
    }

    static void AuditCardTablePenetration(GmParlorCardView[] cards, List<string> issues)
    {
        foreach (GmParlorCardView card in cards)
        {
            if (!TryBounds(card.transform, out Bounds cardBounds)) continue;
            float penetration = AuthoredBaizeY - cardBounds.min.y;
            if (penetration > MaxCardPenetrationBelowTable)
                issues.Add($"{card.name} penetrates the baize by {penetration:0.000} m");
        }
    }

    static void AuditHandAndContactClearance(Transform rightHand, Transform sleeve,
        Transform contact, List<string> issues)
    {
        if (sleeve != null && TryBounds(sleeve, out Bounds sleeveBounds) &&
            sleeveBounds.max.y < AuthoredBaizeY - 0.02f)
            issues.Add($"SleeveCue sits {AuthoredBaizeY - sleeveBounds.max.y:0.000} m under the baize");

        if (rightHand != null)
        {
            Renderer glove = rightHand.GetComponentInChildren<Renderer>(true);
            if (glove != null && glove.enabled)
                issues.Add("RightHandCue still renders the disconnected rehearsal glove");
            if (rightHand.position.y + 0.02f < AuthoredBaizeY)
                issues.Add("RightHandCue is below the baize");
        }

        if (contact != null)
        {
            float above = contact.position.y - AuthoredBaizeY;
            if (above < -MaxCardPenetrationBelowTable)
                issues.Add($"CardContactCue tunnels {Mathf.Abs(above):0.000} m into the baize");
            if (above > MaxContactHeightAboveTable)
                issues.Add($"CardContactCue floats {above:0.000} m above the baize");
        }
    }

    static void AuditLegibilityDistance(Transform physicalCards, Camera playerCamera,
        List<string> issues)
    {
        if (physicalCards == null || playerCamera == null) return;
        Transform playerHandAnchor = physicalCards;
        foreach (Transform child in physicalCards)
        {
            GmParlorCardView view = child.GetComponent<GmParlorCardView>();
            if (view != null && view.Binding.Zone == GmParlorCardZone.PlayerHand)
            {
                playerHandAnchor = child;
                break;
            }
        }

        float distance = Vector3.Distance(playerCamera.transform.position, playerHandAnchor.position);
        if (distance > MaxHandReadDistance)
            issues.Add($"player camera is {distance:0.00} m from the playable hand; ranks will not read");
    }

    static void AuditPresenterBindings(GmParlorAldricPresenter presenter, Transform rightHand,
        Transform sleeve, Transform contact, GmParlorEvidenceLog evidence, List<string> issues)
    {
        if (presenter == null || !presenter.IsConfigured)
            issues.Add("configured GmParlorAldricPresenter is missing");
        if (rightHand == null) issues.Add("RightHandCue binding is missing");
        if (sleeve == null) issues.Add("SleeveCue binding is missing");
        if (contact == null) issues.Add("CardContactCue binding is missing");
        if (evidence == null) issues.Add("GmParlorEvidenceLog is missing");
        if (presenter != null && !presenter.UsesSubstituteProxy)
            issues.Add("Aldric presenter claims a finished character asset the scene does not contain");
        if (presenter != null && presenter.HasCharacterMotionChannel)
            issues.Add("disconnected rehearsal glove is advertising a hand-motion evidence channel");
    }

    static void AuditCommandRecovery(GmParlorPresentationCoordinator coordinator,
        List<string> issues)
    {
        if (coordinator == null)
        {
            issues.Add("GmParlorPresentationCoordinator is missing");
            return;
        }

        Type type = coordinator.GetType();
        if (type.GetMethod("TryRestoreCanonicalState") == null)
            issues.Add("presentation coordinator cannot restore canonical poses without replaying effects");
        if (type.GetMethod("ResetTransientState") == null)
            issues.Add("presentation coordinator cannot clear stale commands after load");
        if (type.GetMethod("FastForwardToCanonicalState") == null)
            issues.Add("presentation coordinator cannot recover from interrupted card motion");
    }

    static void AuditReducedMotionSupport(GmParlorPresentationCoordinator coordinator,
        List<string> issues)
    {
        if (coordinator == null) return;
        PropertyInfo reduced = coordinator.GetType().GetProperty("ReducedMotion");
        if (reduced == null || reduced.PropertyType != typeof(bool))
            issues.Add("presentation coordinator has no reduced-motion flag");
        if (typeof(GmParlorAldricPresenter).GetMethod("RefreshAccessibility") == null)
            issues.Add("Aldric presenter cannot re-evaluate cues after an accessibility change");
    }

    static void AuditEvidenceChannelContract(List<string> issues)
    {
        var channels = Enum.GetNames(typeof(GmParlorEvidenceChannel));
        if (channels.Contains("PositionalAudio") || channels.Contains("ControllerPulse") ||
            channels.Contains("ContrastPulse"))
            issues.Add("evidence vocabulary advertises a channel the shipping presenter does not drive");

        var profile = new GmParlorAccessibilityProfile(false, true, false, true);
        var present = new List<GmParlorEvidenceChannel>
        {
            GmParlorEvidenceChannel.CardContact,
            GmParlorEvidenceChannel.FocusLog,
        };
        if (!profile.ReducedMotion) present.Add(GmParlorEvidenceChannel.HandMotion);
        if (profile.Captions) present.Add(GmParlorEvidenceChannel.Caption);
        if (present.Distinct().Count() < MinimumReadableEvidenceChannels)
            issues.Add("reduced-motion captions-off profile drops below two readable evidence channels");
    }

    static Camera PlayerCamera()
    {
        GmPlayer player = UnityEngine.Object.FindAnyObjectByType<GmPlayer>();
        if (player != null)
        {
            Camera owned = player.GetComponentInChildren<Camera>(true);
            if (owned != null) return owned;
        }
        return Camera.main;
    }

    static Transform FindNamed(string name)
    {
        return UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate.gameObject.scene.IsValid() &&
                candidate.name == name);
    }

    static bool TryBounds(Transform root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true)
            .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy)
            .ToArray();
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }
        bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
            bounds.Encapsulate(renderers[index].bounds);
        return true;
    }
}
