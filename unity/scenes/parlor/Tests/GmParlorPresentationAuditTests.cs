using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class GmParlorPresentationAuditTests
{
    [OneTimeSetUp]
    public void BuildOnce() => GmParlorBuilder.Build();

    [OneTimeTearDown]
    public void TearDownOnce() =>
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

    [Test]
    public void BuiltParlorPassesThePresentationAudit()
    {
        List<string> issues = GmParlorPresentationAudit.ValidateOpenScene();
        Assert.IsEmpty(issues, "Parlor presentation failed:\n- " + string.Join("\n- ", issues));
    }

    [Test]
    public void QualityAuditInvokesThePresentationAudit()
    {
        string source = File.ReadAllText(Path.Combine(
            Environment.GetEnvironmentVariable("GM_REPO_ROOT") ??
            Directory.GetCurrentDirectory(), "unity", "scenes", "parlor", "Editor",
            "GmParlorQualityAudit.cs"));
        StringAssert.Contains("GmParlorPresentationAudit.ValidateOpenScene()", source);
    }

    [Test]
    public void EveryPhysicalCardKeepsACardSizedFocusCollider()
    {
        GmParlorCardView[] cards = Object.FindObjectsByType<GmParlorCardView>(
            FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(card => card.gameObject.scene.IsValid()).ToArray();

        Assert.That(cards, Has.Length.EqualTo(GmParlorCore.TotalCards));
        foreach (GmParlorCardView card in cards)
        {
            BoxCollider focus = card.GetComponent<BoxCollider>();
            Assert.That(focus, Is.Not.Null, card.name);
            Assert.That(focus.size.x, Is.GreaterThanOrEqualTo(
                GmParlorPresentationAudit.MinFocusColliderWidth), card.name);
            Assert.That(focus.size.y, Is.GreaterThanOrEqualTo(
                GmParlorPresentationAudit.MinFocusColliderHeight), card.name);
            Assert.That(Mathf.Max(focus.size.x, focus.size.y, focus.size.z),
                Is.LessThanOrEqualTo(GmParlorPresentationAudit.MaxFocusColliderExtent), card.name);
        }
    }

    [Test]
    public void PlayerCameraCanReadTheHandWithoutAMacroLens()
    {
        GmPlayer player = Object.FindAnyObjectByType<GmPlayer>();
        Camera camera = player.GetComponentInChildren<Camera>(true);
        Transform cards = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .First(candidate => candidate.gameObject.scene.IsValid() &&
                candidate.name == "PhysicalCards");

        Assert.That(Vector3.Distance(camera.transform.position, cards.position),
            Is.LessThanOrEqualTo(GmParlorPresentationAudit.MaxHandReadDistance));
    }

    [Test]
    public void CoordinatorAndPresenterExposeRestoreAndReducedMotionSeams()
    {
        GmParlorPresentationCoordinator coordinator =
            Object.FindAnyObjectByType<GmParlorPresentationCoordinator>();
        Assert.That(coordinator, Is.Not.Null);
        Assert.That(coordinator.GetType().GetMethod("TryRestoreCanonicalState"), Is.Not.Null);
        Assert.That(coordinator.GetType().GetMethod("ResetTransientState"), Is.Not.Null);
        Assert.That(typeof(GmParlorAldricPresenter).GetMethod("RefreshAccessibility"), Is.Not.Null);
        Assert.That(coordinator.ReducedMotion, Is.False);
    }

    [Test]
    public void ReducedMotionCaptionsOffProfileStillHasTwoReadableChannels()
    {
        Assert.That(GmParlorPresentationAudit.MinimumReadableEvidenceChannels, Is.EqualTo(2));
        Assert.That(System.Enum.GetNames(typeof(GmParlorEvidenceChannel)),
            Does.Not.Contain("ContrastPulse"));
    }

    [Test]
    public void ReviewTourAndRestoreIsolateEvidenceBeforeEachStagedCase()
    {
        string root = Environment.GetEnvironmentVariable("GM_REPO_ROOT") ??
            Directory.GetCurrentDirectory();
        string tour = File.ReadAllText(Path.Combine(root, "unity", "scenes", "parlor",
            "Runtime", "GmParlorShotTour.cs"));
        string restore = File.ReadAllText(Path.Combine(root, "unity", "scenes", "parlor",
            "Runtime", "GmParlorRestoreReviewOrchestrator.cs"));
        StringAssert.Contains("IsolateReviewEvidence()", tour);
        StringAssert.Contains("evidence?.Clear()", tour);
        StringAssert.Contains("evidence.Clear()", restore);
    }

    [Test]
    public void MissingPresenterFailsTheAuditAndRebuildRestoresIt()
    {
        GmParlorAldricPresenter presenter = Object.FindAnyObjectByType<GmParlorAldricPresenter>();
        Assert.That(presenter, Is.Not.Null);
        Object.DestroyImmediate(presenter.gameObject);

        List<string> issues = GmParlorPresentationAudit.ValidateOpenScene();
        Assert.That(issues, Has.Some.Matches<string>(issue =>
            issue.IndexOf("Presenter", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            issue.IndexOf("RightHandCue", System.StringComparison.Ordinal) >= 0));

        GmParlorBuilder.Build();
        Assert.IsEmpty(GmParlorPresentationAudit.ValidateOpenScene());
    }
}
