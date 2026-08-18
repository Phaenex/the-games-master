using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class GmPrologueHudTests
{
    GameObject hudObj;
    GmPrologueHud hud;

    [SetUp]
    public void SetUp()
    {
        GmRunStore.BeginNewRun();
        ResetAccessibility();
        hudObj = new GameObject("TestPrologueHud");
        hud = hudObj.AddComponent<GmPrologueHud>();
    }

    [TearDown]
    public void TearDown()
    {
        if (hudObj != null)
        {
            // PanelSettings cleanup to prevent leaks in editor test runs
            FieldInfo panelField = typeof(GmPrologueHud).GetField("panelSettings",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (panelField != null)
            {
                var settings = (PanelSettings)panelField.GetValue(hud);
                panelField.SetValue(hud, null);
                if (settings != null) Object.DestroyImmediate(settings);
            }
            Object.DestroyImmediate(hudObj);
        }
        ResetAccessibility();
    }

    [Test]
    public void RunStateLineFormatsTallyAccurately()
    {
        MethodInfo runStateMethod = typeof(GmPrologueHud).GetMethod("RunStateLine",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(runStateMethod, "GmPrologueHud.RunStateLine method missing");

        // Fresh run: empty string
        GmRunStore.BeginNewRun();
        string lineEmpty = (string)runStateMethod.Invoke(null, null);
        Assert.AreEqual("", lineEmpty);

        // 1 catch, 0 shards
        GmRunStore.RecordCatch("parlor-palm-trick-1");
        string line1Catch = (string)runStateMethod.Invoke(null, null);
        Assert.AreEqual("1 cheat caught   ·   0 shards", line1Catch);

        // 1 catch, 1 shard
        GmRunStore.CollectShard(0);
        string line1Each = (string)runStateMethod.Invoke(null, null);
        Assert.AreEqual("1 cheat caught   ·   1 shard", line1Each);

        // 2 catches, 3 shards
        GmRunStore.RecordCatch("parlor-palm-trick-2");
        GmRunStore.CollectShard(1);
        GmRunStore.CollectShard(2);
        string linePlural = (string)runStateMethod.Invoke(null, null);
        Assert.AreEqual("2 cheats caught   ·   3 shards", linePlural);
    }

    [Test]
    public void BuildUiCedesPauseRowsToTheCommonPauseMenu()
    {
        VisualElement root = InvokeBuildUi(hud);
        Assert.IsNotNull(root, "GmPrologueHud root visual element is null");

        var resumeRow = root.Q<VisualElement>("PauseResume");
        var quitRow = root.Q<VisualElement>("PauseQuit");
        Assert.IsNull(resumeRow, "Prologue still renders a second competing pause menu");
        Assert.IsNull(quitRow, "Prologue still renders a second competing pause menu");
    }

    [Test]
    public void BuildUiAppliesGlobalTextScaleAndHighContrast()
    {
        GmAccessibilitySettings.SetTextScale(2f);
        GmAccessibilitySettings.SetHighContrast(true);
        VisualElement root = InvokeBuildUi(hud);
        Assert.That(root.Q<Label>("CardBody").style.fontSize.value.value,
            Is.EqualTo(76f).Within(0.01f));
        Assert.That(root.ClassListContains("gm-high-contrast"), Is.True);
    }

    static VisualElement InvokeBuildUi(GmPrologueHud hudInstance)
    {
        MethodInfo buildMethod = typeof(GmPrologueHud).GetMethod("BuildUi",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(buildMethod, "GmPrologueHud.BuildUi method missing");
        buildMethod.Invoke(hudInstance, null);
        var doc = hudInstance.GetComponent<UIDocument>();
        Assert.IsNotNull(doc, "UIDocument was not created by BuildUi");
        return doc.rootVisualElement;
    }

    static void ResetAccessibility()
    {
        typeof(GmRunStore).Assembly.GetType("GmAccessibilitySettings")?.GetMethod(
            "ResetToDefaultsForTests", BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Static)?.Invoke(null, null);
    }
}
