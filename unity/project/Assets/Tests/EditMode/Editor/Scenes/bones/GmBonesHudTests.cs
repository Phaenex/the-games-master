using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class GmBonesHudTests
{
    string directory;
    GameObject graph;
    GmBonesController controller;
    GmBonesHud hud;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "gm-bones-hud-" + Guid.NewGuid().ToString("N"));
        GmSaveSystem.ConfigureForTests(Path.Combine(directory, "save.json"));
        GmRunSeed.ForceForReview(7711);
        GmRunStore.BeginNewRun();
        ResetAccessibility();
        controller = new GmBonesController();
        controller.InitializeOrRestore();
        graph = new GameObject("BonesHudTest");
        hud = graph.AddComponent<GmBonesHud>();
        Assert.That(hud.TryConfigure(controller, out string error), Is.True, error);
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(graph);
        ResetAccessibility();
        GmSaveSystem.ResetTestConfiguration();
        GmRunSeed.ResetForTests();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        GmRunStore.BeginNewRun();
    }

    [Test]
    public void NamedSurfacesAreAccessibleAndIllegalActionsDisable()
    {
        VisualElement root = hud.BuildForTests();
        foreach (string name in new[] { "BonesStatus", "BonesDice", "BonesActions", "BonesBank",
            "BonesPress1", "BonesPress2", "BonesPress3", "BonesIntervention", "BonesChallenge",
            "BonesProceed", "BonesEvidence", "BonesActionLog", "BonesResult", "BonesCaption" })
            Assert.That(root.Q(name), Is.Not.Null, name);
        Button bank = root.Q<Button>("BonesBank");
        Assert.That(bank.style.minHeight.value.value, Is.GreaterThanOrEqualTo(44));
        Assert.That(bank.text, Does.Contain("SELECTED"));
        Assert.That(bank.style.borderTopWidth.value, Is.GreaterThanOrEqualTo(2));
        Assert.That(root.Q<Button>("BonesChallenge").enabledSelf, Is.False);
        Assert.That(root.Q<Button>("BonesProceed").enabledSelf, Is.False);
        foreach (TextElement text in root.Query<TextElement>().ToList())
            Assert.That(text.style.unityTextGenerator.value, Is.EqualTo(TextGeneratorType.Standard));
    }

    [Test]
    public void AccessibilityRefreshIsImmediateAndCannotMutateMatch()
    {
        VisualElement root = hud.BuildForTests();
        string fingerprint = controller.Snapshot.stateFingerprint;
        GmAccessibilitySettings.SetHighContrast(true);
        GmAccessibilitySettings.SetCaptions(true);
        GmAccessibilitySettings.SetReducedMotion(true);
        GmAccessibilitySettings.SetTextScale(2f);
        Assert.That(root.ClassListContains("gm-high-contrast"), Is.True);
        Assert.That(root.ClassListContains("gm-reduced-motion"), Is.True);
        Assert.That(root.Q<Label>("BonesCaption").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
        Assert.That(root.Q<Label>("BonesStatus").style.fontSize.value.value, Is.GreaterThanOrEqualTo(36));
        Assert.That(controller.Snapshot.stateFingerprint, Is.EqualTo(fingerprint));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InterventionReceiptHasPersistentShapeAndHonestVersusDisplayedFacts(bool challengeResult)
    {
        GmBonesMatch pending = PendingLoadedSix();
        GmRunStore.LoadFromSaveData(new GmSaveData { bonesMatch = pending.ExportSnapshot() });
        controller = new GmBonesController();
        controller.InitializeOrRestore();
        Assert.That(hud.TryConfigure(controller, out string error), Is.True, error);
        VisualElement root = hud.BuildForTests();
        Assert.That(root.Q<Label>("BonesDisplayedReroll").text, Does.Contain("6, 2"));
        Assert.That(root.Q<Label>("BonesObservedHonestReroll").text, Does.Contain("not observed"));
        VisualElement pendingMarker = root.Q("BonesChangedDieMarker");
        Assert.That(pendingMarker, Is.Not.Null);
        Assert.That(pendingMarker.parent.name, Is.EqualTo("BonesDie2"));

        GmAccessibilitySettings.SetCaptions(false);
        if (challengeResult) controller.CallTell(); else controller.ConfirmFocusedAction();
        Assert.That(root.Q("BonesChangedDieMarker"), Is.Not.Null,
            "captions-off removed the non-text loaded-die channel");
        Assert.That(root.Q("BonesChangedDieMarker").parent.name, Is.EqualTo("BonesDie2"));
        Assert.That(root.Q<Label>("BonesDisplayedReroll").text, Does.Contain("6, 2"));
        Assert.That(root.Q<Label>("BonesObservedHonestReroll").text,
            challengeResult ? Does.Contain("1, 2") : Does.Contain("not observed"));
        Assert.That(root.Q<Label>("BonesDice").text,
            challengeResult ? Does.Contain("6  1  2") : Does.Contain("6  6  2"));

        GmRunStore.BeginNewRun();
        GmRunStore.LoadFromSaveData(GmSaveData.FromJson(File.ReadAllText(
            Path.Combine(directory, "save.json"))));
        var restored = new GmBonesController();
        Assert.That(restored.InitializeOrRestore(), Is.EqualTo(GmBonesInitializeResult.Restored));
        Assert.That(hud.TryConfigure(restored, out error), Is.True, error);
        Assert.That(root.Q("BonesChangedDieMarker"), Is.Not.Null);
        Assert.That(root.Q("BonesChangedDieMarker").parent.name, Is.EqualTo("BonesDie2"));
        Assert.That(root.Q<Label>("BonesDisplayedReroll").text, Does.Contain("6, 2"));
    }

    [Test]
    public void InactiveConfigureDefersSubscriptionUntilEnableAndRefreshesOnce()
    {
        UnityEngine.Object.DestroyImmediate(graph);
        graph = new GameObject("InactiveBonesHud");
        graph.SetActive(false);
        hud = graph.AddComponent<GmBonesHud>();
        Assert.That(hud.TryConfigure(controller, out string error), Is.True, error);
        int revision = hud.RefreshRevision;
        controller.MoveFocus(1);
        Assert.That(hud.RefreshRevision, Is.EqualTo(revision),
            "inactive configure subscribed to controller events");

        graph.SetActive(true);
        InvokeLifecycle(hud, "OnEnable");
        VisualElement root = hud.BuildForTests();
        Assert.That(root.Q<Button>("BonesPress1").text, Does.Contain("SELECTED"));
        int enabledRevision = hud.RefreshRevision;
        controller.MoveFocus(1);
        Assert.That(hud.RefreshRevision, Is.EqualTo(enabledRevision + 1),
            "enable installed duplicate controller subscriptions");
    }

    [Test]
    public void DisableLeavesStaleViewThenEnableRefreshesAndResubscribesExactlyOnce()
    {
        VisualElement root = hud.BuildForTests();
        int beforeDisable = hud.RefreshRevision;
        InvokeLifecycle(hud, "OnDisable");
        graph.SetActive(false);
        controller.MoveFocus(2);
        GmAccessibilitySettings.SetHighContrast(true);
        Assert.That(hud.RefreshRevision, Is.EqualTo(beforeDisable));
        Assert.That(root.Q<Button>("BonesBank").text, Does.Contain("SELECTED"));
        Assert.That(root.ClassListContains("gm-high-contrast"), Is.False);

        graph.SetActive(true);
        InvokeLifecycle(hud, "OnEnable");
        Assert.That(root.Q<Button>("BonesPress2").text, Does.Contain("SELECTED"));
        Assert.That(root.ClassListContains("gm-high-contrast"), Is.True);
        int enabledRevision = hud.RefreshRevision;
        controller.MoveFocus(1);
        Assert.That(hud.RefreshRevision, Is.EqualTo(enabledRevision + 1));
    }

    static GmBonesMatch PendingLoadedSix()
    {
        int[] dice = { 6,6,6, 6,6,6, 6,6,6, 6,6,6, 6,6,5, 6,2,1, 1,2 };
        var match = new GmBonesMatch(3UL, dice);
        match.TryChoose(GmBonesChoice.Bank, -1, out _);
        match.TryChoose(GmBonesChoice.Bank, -1, out _);
        match.TryChoose(GmBonesChoice.Bank, -1, out _);
        return match;
    }

    static void ResetAccessibility() => typeof(GmAccessibilitySettings)
        .GetMethod("ResetToDefaultsForTests", BindingFlags.Static | BindingFlags.NonPublic)
        .Invoke(null, null);

    static void InvokeLifecycle(GmBonesHud target, string method) => typeof(GmBonesHud)
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
        .Invoke(target, null);
}
