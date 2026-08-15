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
    public void BuildUiCreatesPauseMenuRowsWithMarkersAndLabels()
    {
        VisualElement root = InvokeBuildUi(hud);
        Assert.IsNotNull(root, "GmPrologueHud root visual element is null");

        var resumeRow = root.Q<VisualElement>("PauseResume");
        var quitRow = root.Q<VisualElement>("PauseQuit");
        Assert.IsNotNull(resumeRow, "PauseResume row is missing from visual tree");
        Assert.IsNotNull(quitRow, "PauseQuit row is missing from visual tree");

        var resumeMarker = root.Q<VisualElement>("PauseResumeMarker");
        var quitMarker = root.Q<VisualElement>("PauseQuitMarker");
        Assert.IsNotNull(resumeMarker, "PauseResume marker is missing");
        Assert.IsNotNull(quitMarker, "PauseQuit marker is missing");

        var resumeLabel = root.Q<Label>("PauseResumeLabel");
        var quitLabel = root.Q<Label>("PauseQuitLabel");
        Assert.IsNotNull(resumeLabel, "PauseResume label is missing");
        Assert.IsNotNull(quitLabel, "PauseQuit label is missing");
        Assert.AreEqual("Resume", resumeLabel.text);
        Assert.AreEqual("Quit to desktop", quitLabel.text);

        var runState = root.Q<Label>("RunState");
        Assert.IsNotNull(runState, "RunState label is missing from pause card");
    }

    [Test]
    public void SetPauseFocusAppliesTwoChannelAccessibilityFocus()
    {
        VisualElement root = InvokeBuildUi(hud);

        MethodInfo setFocusMethod = typeof(GmPrologueHud).GetMethod("SetPauseFocus",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(setFocusMethod, "GmPrologueHud.SetPauseFocus method missing");

        var resumeMarker = root.Q<VisualElement>("PauseResumeMarker");
        var quitMarker = root.Q<VisualElement>("PauseQuitMarker");
        var resumeLabel = root.Q<Label>("PauseResumeLabel");
        var quitLabel = root.Q<Label>("PauseQuitLabel");

        // Focus Resume
        setFocusMethod.Invoke(hud, new object[] { true });
        Assert.AreEqual(DisplayStyle.Flex, resumeMarker.style.display.value, "Resume marker must be visible when focused");
        Assert.AreEqual(DisplayStyle.None, quitMarker.style.display.value, "Quit marker must be hidden when Resume is focused");
        Assert.Greater(resumeLabel.style.color.value.r, quitLabel.style.color.value.r,
            "Resume label ink must be brighter than Quit label ink when Resume is focused");

        // Focus Quit
        setFocusMethod.Invoke(hud, new object[] { false });
        Assert.AreEqual(DisplayStyle.None, resumeMarker.style.display.value, "Resume marker must be hidden when Quit is focused");
        Assert.AreEqual(DisplayStyle.Flex, quitMarker.style.display.value, "Quit marker must be visible when focused");
        Assert.Greater(quitLabel.style.color.value.r, resumeLabel.style.color.value.r,
            "Quit label ink must be brighter than Resume label ink when Quit is focused");
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
}
