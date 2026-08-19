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

    static void ResetAccessibility() => typeof(GmAccessibilitySettings)
        .GetMethod("ResetToDefaultsForTests", BindingFlags.Static | BindingFlags.NonPublic)
        .Invoke(null, null);
}
