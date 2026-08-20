using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TestTools;

public sealed class GmStudyHudTests
{
    string directory;
    GameObject graph;
    GmStudyController controller;
    GmStudyHud hud;
    GmStudyInput tableInput;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "gm-study-hud-" + Guid.NewGuid().ToString("N"));
        GmSaveSystem.ConfigureForTests(Path.Combine(directory, "save.json"));
        GmRunSeed.ForceForReview(4409);
        GmRunStore.BeginNewRun();
        ResetAccessibility();
        controller = new GmStudyController();
        controller.InitializeOrRestore();
        graph = new GameObject("StudyHudTest");
        tableInput = graph.AddComponent<GmStudyInput>();
        tableInput.ConfigureForTests(controller, () => false);
        hud = graph.AddComponent<GmStudyHud>();
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
        foreach (string name in new[] { "StudyStatus", "StudyBoard", "StudyActions", "StudyCard1",
            "StudyCard2", "StudyCard3", "StudyIntervention", "StudyChallenge",
            "StudyProceed", "StudyEvidence", "StudyActionLog", "StudyResult", "StudyCaption" })
            Assert.That(root.Q(name), Is.Not.Null, name);
        Button card1 = root.Q<Button>("StudyCard1");
        Assert.That(card1.style.minHeight.value.value, Is.GreaterThanOrEqualTo(44));
        Assert.That(card1.text, Does.Contain("SELECTED"));
        Assert.That(card1.style.borderTopWidth.value, Is.GreaterThanOrEqualTo(2));
        Assert.That(root.Q<Button>("StudyChallenge").enabledSelf, Is.False);
        Assert.That(root.Q<Button>("StudyProceed").enabledSelf, Is.False);
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
        Assert.That(root.Q<Label>("StudyCaption").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
        Assert.That(root.Q<Label>("StudyStatus").style.fontSize.value.value, Is.GreaterThanOrEqualTo(36));
        Assert.That(controller.Snapshot.stateFingerprint, Is.EqualTo(fingerprint));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InterventionReceiptHasPersistentShapeAndHonestVersusDisplayedFacts(bool challengeResult)
    {
        GmStudyMatch pending = PendingInterventionAtFinalPosition();
        GmRunStore.LoadFromSaveData(new GmSaveData { studyMatch = pending.ExportSnapshot() });
        controller = new GmStudyController();
        controller.InitializeOrRestore();
        tableInput.ConfigureForTests(controller, () => false);
        Assert.That(hud.TryConfigure(controller, out string error), Is.True, error);
        VisualElement root = hud.BuildForTests();
        GmStudyPosition finalPosition = GmStudyRules.GetPosition(2);
        Assert.That(root.Q<Label>("StudyAlteredFen").text, Does.Contain(finalPosition.alteredFen));
        Assert.That(root.Q<Label>("StudyObservedOriginalFen").text, Does.Contain("not observed"));
        VisualElement pendingMarker = root.Q("StudyArbiterMarker");
        Assert.That(pendingMarker, Is.Not.Null);
        Assert.That(pendingMarker.parent.name, Is.EqualTo("StudyBoardPanel"));
        Assert.That(pendingMarker.tooltip, Is.EqualTo(GmStudyRules.EvidenceIconId));

        GmAccessibilitySettings.SetCaptions(false);
        if (challengeResult) controller.Challenge(); else controller.ConfirmFocusedAction();
        Assert.That(root.Q("StudyArbiterMarker"), Is.Not.Null,
            "captions-off removed the non-text arbiter-override channel");
        Assert.That(root.Q("StudyArbiterMarker").parent.name, Is.EqualTo("StudyBoardPanel"));
        Assert.That(root.Q<Label>("StudyAlteredFen").text, Does.Contain(finalPosition.alteredFen));
        Assert.That(root.Q<Label>("StudyObservedOriginalFen").text,
            challengeResult ? Does.Contain(finalPosition.originalFen) : Does.Contain("not observed"));
        Assert.That(root.Q<Label>("StudyBoard").text,
            challengeResult ? Does.Contain(finalPosition.originalFen) : Does.Contain(finalPosition.alteredFen));

        GmRunStore.BeginNewRun();
        GmRunStore.LoadFromSaveData(GmSaveData.FromJson(File.ReadAllText(
            Path.Combine(directory, "save.json"))));
        var restored = new GmStudyController();
        Assert.That(restored.InitializeOrRestore(), Is.EqualTo(GmStudyInitializeResult.Restored));
        tableInput.ConfigureForTests(restored, () => false);
        Assert.That(hud.TryConfigure(restored, out error), Is.True, error);
        Assert.That(root.Q("StudyArbiterMarker"), Is.Not.Null);
        Assert.That(root.Q("StudyArbiterMarker").parent.name, Is.EqualTo("StudyBoardPanel"));
        Assert.That(root.Q<Label>("StudyAlteredFen").text, Does.Contain(finalPosition.alteredFen));
    }

    [Test]
    public void InactiveConfigureDefersSubscriptionUntilEnableAndRefreshesOnce()
    {
        UnityEngine.Object.DestroyImmediate(graph);
        graph = new GameObject("InactiveStudyHud");
        graph.SetActive(false);
        tableInput = graph.AddComponent<GmStudyInput>();
        tableInput.ConfigureForTests(controller, () => false);
        hud = graph.AddComponent<GmStudyHud>();
        Assert.That(hud.TryConfigure(controller, out string error), Is.True, error);
        int revision = hud.RefreshRevision;
        controller.MoveFocus(1);
        Assert.That(hud.RefreshRevision, Is.EqualTo(revision),
            "inactive configure subscribed to controller events");

        graph.SetActive(true);
        InvokeLifecycle(hud, "OnEnable");
        VisualElement root = hud.BuildForTests();
        Assert.That(root.Q<Button>("StudyCard2").text, Does.Contain("SELECTED"));
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
        Assert.That(root.Q<Button>("StudyCard1").text, Does.Contain("SELECTED"));
        Assert.That(root.ClassListContains("gm-high-contrast"), Is.False);

        graph.SetActive(true);
        InvokeLifecycle(hud, "OnEnable");
        Assert.That(root.Q<Button>("StudyCard3").text, Does.Contain("SELECTED"));
        Assert.That(root.ClassListContains("gm-high-contrast"), Is.True);
        int enabledRevision = hud.RefreshRevision;
        controller.MoveFocus(1);
        Assert.That(hud.RefreshRevision, Is.EqualTo(enabledRevision + 1));
    }

    [Test]
    public void ButtonPersistenceFailureRollsBackShowsRetryAndSuccessfulRetryClearsFeedback()
    {
        var backend = new ToggleBackend();
        string savePath = Path.Combine(directory, "hud-feedback-save.json");
        GmSaveSystem.ConfigureForTests(savePath, backend);
        GmRunStore.BeginNewRun();
        controller = new GmStudyController();
        controller.InitializeOrRestore();
        tableInput.ConfigureForTests(controller, () => false);
        Assert.That(hud.TryConfigure(controller, out string error), Is.True, error);
        VisualElement root = hud.BuildForTests();
        string fingerprint = controller.Snapshot.stateFingerprint;
        string disk = backend.LastJson;

        backend.Fail = true;
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("injected HUD failure"));
        Click(root.Q<Button>("StudyCard1"));
        Assert.That(controller.Snapshot.stateFingerprint, Is.EqualTo(fingerprint));
        Assert.That(GmRunStore.GetStudyMatchSnapshot().stateFingerprint, Is.EqualTo(fingerprint));
        Assert.That(backend.LastJson, Is.EqualTo(disk));
        Assert.That(root.Q<Label>("StudyFeedback").text, Does.Contain("Retry"));
        Assert.That(tableInput.LastActionError, Is.EqualTo(GmStudyActionError.PersistenceFailed));

        backend.Fail = false;
        tableInput.ConfirmAction();
        Assert.That(controller.PlayerDecisionCount, Is.EqualTo(1));
        Assert.That(tableInput.LastActionError, Is.EqualTo(GmStudyActionError.None));
        Assert.That(root.Q<Label>("StudyFeedback").text, Is.Empty);
    }

    [Test]
    public void InputPersistenceFailurePublishesIntoHudAndRetryClearsIt()
    {
        var backend = new ToggleBackend();
        GmSaveSystem.ConfigureForTests(Path.Combine(directory, "input-hud-save.json"), backend);
        GmRunStore.BeginNewRun();
        controller = new GmStudyController();
        controller.InitializeOrRestore();
        tableInput.ConfigureForTests(controller, () => false);
        Assert.That(hud.TryConfigure(controller, tableInput, out string error), Is.True, error);
        VisualElement root = hud.BuildForTests();

        backend.Fail = true;
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("injected HUD failure"));
        tableInput.ConfirmAction();
        Assert.That(root.Q<Label>("StudyFeedback").text, Does.Contain("Retry"));

        backend.Fail = false;
        Click(root.Q<Button>("StudyCard1"));
        Assert.That(tableInput.LastActionError, Is.EqualTo(GmStudyActionError.None));
        Assert.That(root.Q<Label>("StudyFeedback").text, Is.Empty);
        Assert.That(hud.TryConfigure(controller, tableInput, out error), Is.True, error);
        Assert.That(root.Q<Label>("StudyFeedback").text, Is.Empty);
    }

    [Test]
    public void WrongPhaseFromEitherChannelUsesAndClearsCentralFeedback()
    {
        VisualElement root = hud.BuildForTests();
        Assert.That(tableInput.ChallengeAction(), Is.EqualTo(GmStudyActionError.WrongPhase));
        Assert.That(root.Q<Label>("StudyFeedback").text, Does.Contain("not available"));
        Click(root.Q<Button>("StudyCard1"));
        Assert.That(tableInput.LastActionError, Is.EqualTo(GmStudyActionError.None));
        Assert.That(root.Q<Label>("StudyFeedback").text, Is.Empty);
        string source = File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../unity/project/Assets/Scripts/Scenes/study/GmStudyHud.cs")));
        Assert.That(source, Does.Not.Contain("feedbackText"));
    }

    static GmStudyMatch PendingInterventionAtFinalPosition()
    {
        var match = new GmStudyMatch(11UL);
        match.TryChoose(GmStudyRules.GetPosition(0).cards[0].actionId, out _);
        match.TryChoose(WrongActionId(1), out _);
        match.TryChoose(GmStudyRules.GetPosition(2).cards[0].actionId, out _);
        return match;
    }

    static string WrongActionId(int positionIndex)
    {
        foreach (GmStudyMoveCard card in GmStudyRules.GetPosition(positionIndex).cards)
            if (!card.isCorrect) return card.actionId;
        throw new InvalidOperationException("position " + positionIndex + " has no wrong card");
    }

    static void ResetAccessibility() => typeof(GmAccessibilitySettings)
        .GetMethod("ResetToDefaultsForTests", BindingFlags.Static | BindingFlags.NonPublic)
        .Invoke(null, null);

    static void InvokeLifecycle(GmStudyHud target, string method) => typeof(GmStudyHud)
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
        .Invoke(target, null);

    static void Click(Button button) => button.clickable.GetType()
        .GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic)
        .Invoke(button.clickable, new object[] { null });

    sealed class ToggleBackend : IGmAtomicSaveBackend
    {
        public bool Fail;
        public string LastJson { get; private set; }
        public void WriteAtomic(string target, string json)
        {
            if (Fail) throw new IOException("injected HUD failure");
            LastJson = json;
        }
    }
}
