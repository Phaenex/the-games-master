using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

public sealed class GmParlorFocusViewTests
{
    string directory;
    GameObject root;
    GmParlorRules rules;
    GmParlorController controller;
    GmParlorFocusView focus;
    GmParlorEvidenceLog evidence;
    GmParlorPropBinder binder;
    GmParlorPresentationCoordinator coordinator;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "gm-parlor-focus-" + Guid.NewGuid().ToString("N"));
        GmSaveSystem.ConfigureForTests(Path.Combine(directory, "save.json"));
        GmRunStore.BeginNewRun();
        ResetAccessibility();
        root = new GameObject("ParlorFocusTest");
        rules = root.AddComponent<GmParlorRules>();
        if (rules.Match == null)
            Assert.That(rules.InitializeOrRestore(), Is.EqualTo(GmParlorInitializeResult.StartedNew));
        var cards = new GameObject("Cards");
        cards.transform.SetParent(root.transform, false);
        binder = cards.AddComponent<GmParlorPropBinder>();
        Assert.That(binder.TryConfigure(CreateViews(cards.transform), out string bindError),
            Is.True, bindError);
        Assert.That(binder.TryApply(rules.Match.ExportSnapshot(), out string applyError),
            Is.True, applyError);
        coordinator = cards.AddComponent<GmParlorPresentationCoordinator>();
        Assert.That(coordinator.TryConfigure(binder, out string presentationError),
            Is.True, presentationError);
        controller = root.AddComponent<GmParlorController>();
        Assert.That(controller.TryConfigure(rules, binder, coordinator, out string controllerError),
            Is.True, controllerError);
        evidence = root.AddComponent<GmParlorEvidenceLog>();
        focus = root.AddComponent<GmParlorFocusView>();
        Assert.That(focus.TryConfigure(rules, controller, evidence, out string focusError),
            Is.True, focusError);
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
        GmSaveSystem.ResetTestConfiguration();
        GmRunStore.BeginNewRun();
        ResetAccessibility();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    [Test]
    public void FocusModelMatchesTheCanonicalPlayableHandAndObservedFacts()
    {
        evidence.Record(10, GmParlorObservedFact.RightHandPausedAboveDeck);
        controller.SetFocusedCardIndex(2);

        GmParlorFocusModel model = focus.Refresh();

        Assert.That(model.Cards.Select(card => card.Index),
            Is.EqualTo(Enumerable.Range(0, rules.PlayerHand.Count)));
        Assert.That(model.Cards.Select(card => card.Label),
            Is.EqualTo(rules.PlayerHand.Select(card => card.TableLabel)));
        Assert.That(model.Cards.Single(card => card.Focused).Index, Is.EqualTo(2));
        Assert.That(model.Cards.Select(card => card.Legal), Is.All.True);
        Assert.That(model.Evidence, Is.EqualTo(new[]
        {
            "His right hand stopped above the deck.",
        }));
        Assert.That(model.ReadAvailable, Is.EqualTo(rules.ReadEnabled));
    }

    [Test]
    public void FocusJournalDeduplicatesRepeatedPlayerFacingObservationText()
    {
        Assert.That(evidence.Record(10, GmParlorObservedFact.CardContactBroke), Is.True);
        Assert.That(evidence.Record(11, GmParlorObservedFact.CardContactBroke), Is.True);

        GmParlorFocusModel model = focus.Refresh();

        Assert.That(evidence.Facts, Has.Count.EqualTo(2),
            "durable evidence must retain both command identities");
        Assert.That(model.Evidence, Is.EqualTo(new[]
        {
            "The card left the baize, then touched it again.",
        }), "the player-facing journal repeated an identical observation line");
    }

    [Test]
    public void FocusViewOpensClosesAndDelegatesSelectionWithoutRulesAuthority()
    {
        Assert.That(focus.IsOpen, Is.False);
        focus.Open();
        Assert.That(focus.IsOpen, Is.True);
        focus.Move(1);
        Assert.That(controller.FocusedCardIndex, Is.EqualTo(1));
        string before = rules.Match.PublicStateBytes;
        focus.Close();
        Assert.That(focus.IsOpen, Is.False);
        Assert.That(rules.Match.PublicStateBytes, Is.EqualTo(before));
    }

    [Test]
    public void FocusSourceCannotReadHiddenAldricTruthOrHand()
    {
        string source = File.ReadAllText(Path.Combine(
            Application.dataPath, "Scripts/Scenes/parlor/GmParlorFocusView.cs"));
        StringAssert.DoesNotContain("HostHand", source);
        StringAssert.DoesNotContain("AldricHand", source);
        StringAssert.DoesNotContain("aldricHand", source);
        StringAssert.DoesNotContain("AldricCheated", source);
        StringAssert.DoesNotContain("AldricCheatKind", source);
        StringAssert.DoesNotContain("GmRunStore", source);
    }

    [Test]
    public void HudBuildsSparseReadableUiFromTheSameFocusModel()
    {
        var hud = root.AddComponent<GmParlorHud>();
        Assert.That(hud.TryConfigure(focus, out string error), Is.True, error);
        VisualElement ui = hud.BuildForTests();
        focus.Open();
        hud.Refresh();

        Assert.That(ui.Q<Label>("ParlorScore"), Is.Not.Null);
        Assert.That(ui.Q<Label>("ParlorAction"), Is.Not.Null);
        Assert.That(ui.Q<VisualElement>("ParlorHand"), Is.Not.Null);
        Assert.That(ui.Q<VisualElement>("ParlorEvidence"), Is.Not.Null);
        Assert.That(ui.Q<VisualElement>("ParlorHand").childCount,
            Is.EqualTo(rules.PlayerHand.Count));
        Label score = ui.Q<Label>("ParlorScore");
        StringAssert.Contains("Round", score.text);
        Assert.That(score.style.width.value.value, Is.GreaterThanOrEqualTo(900f),
            "the score mesh collapsed and dropped glyphs after the MatchResult string grew");
        Assert.That(score.style.height.value.value, Is.GreaterThanOrEqualTo(60f));
        Assert.That(score.style.whiteSpace.value, Is.EqualTo(WhiteSpace.NoWrap));
        Assert.That(score.style.overflow.value, Is.EqualTo(Overflow.Visible));
    }

    [Test]
    public void HudNamesLockedAndLateReadFailuresInsteadOfSilentlyContinuing()
    {
        var hud = root.AddComponent<GmParlorHud>();
        Assert.That(hud.TryConfigure(focus, coordinator, out string error), Is.True, error);
        VisualElement ui = hud.BuildForTests();

        Assert.That(rules.StartGame(1, 1, 0, false, forceRestart: true),
            Is.EqualTo(GmParlorInitializeResult.StartedNew));
        Assert.That(controller.Activate(), Is.EqualTo(GmParlorActionError.None));
        Assert.That(controller.ConfirmFocusedAction(), Is.EqualTo(GmParlorActionError.None));
        coordinator.FastForwardToCanonicalState();
        Assert.That(controller.CallRead(), Is.EqualTo(GmParlorActionError.ReadLocked));
        focus.Open();
        hud.Refresh();
        StringAssert.Contains("locked", ui.Q<Label>("ParlorFeedback").text.ToLowerInvariant());
        Label feedback = ui.Q<Label>("ParlorFeedback");
        Assert.That(feedback.style.bottom.value.value, Is.GreaterThanOrEqualTo(380f),
            "central feedback sits behind the open focus journal");
        Assert.That(feedback.parent.IndexOf(feedback), Is.EqualTo(feedback.parent.childCount - 1),
            "central feedback is not the top player-facing overlay");
        Color feedbackColor = feedback.style.color.value;
        Assert.That(Mathf.Min(feedbackColor.r, feedbackColor.g, feedbackColor.b),
            Is.GreaterThan(0.7f), "locked feedback is too dark to read");

        Assert.That(controller.ConfirmFocusedAction(), Is.EqualTo(GmParlorActionError.None));
        coordinator.FastForwardToCanonicalState();
        Assert.That(controller.CallRead(), Is.EqualTo(GmParlorActionError.WrongPhase));
        hud.Refresh();
        StringAssert.Contains("closed", ui.Q<Label>("ParlorFeedback").text.ToLowerInvariant());
    }

    [Test]
    public void HudAppliesTwoTimesTextHighContrastAndObservedFactCaptionsImmediately()
    {
        StartActiveCaption();
        var hud = root.AddComponent<GmParlorHud>();
        Assert.That(hud.TryConfigure(focus, coordinator, out string error), Is.True, error);
        VisualElement ui = hud.BuildForTests();
        focus.Open();

        string canonicalBefore = rules.Match.PublicStateBytes;
        InvokeAccessibility("SetTextScale", 2f);
        InvokeAccessibility("SetHighContrast", true);
        InvokeAccessibility("SetCaptions", true);

        Label caption = ui.Q<Label>("ParlorCaptionSurface");
        Assert.That(caption, Is.Not.Null, "captions have no rendered surface");
        Assert.That(caption.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        StringAssert.Contains("card", caption.text.ToLowerInvariant());
        StringAssert.DoesNotContain("cheat", caption.text.ToLowerInvariant());
        StringAssert.DoesNotContain("honest", caption.text.ToLowerInvariant());
        StringAssert.DoesNotContain("suspicious", caption.text.ToLowerInvariant());
        Assert.That(ui.Q<Label>("ParlorScore").style.fontSize.value.value,
            Is.EqualTo(36f).Within(0.001f));
        Assert.That(ui.Q<VisualElement>("ParlorFocusPanel").ClassListContains(
            "gm-high-contrast"), Is.True);
        Assert.That(rules.Match.PublicStateBytes, Is.EqualTo(canonicalBefore),
            "accessibility settings changed the canonical match or its RNG");
    }

    [Test]
    public void CaptionSurfaceHidesImmediatelyWhenCaptionsAreDisabled()
    {
        StartActiveCaption();
        InvokeAccessibility("SetCaptions", true);
        var hud = root.AddComponent<GmParlorHud>();
        Assert.That(hud.TryConfigure(focus, coordinator, out string error), Is.True, error);
        VisualElement ui = hud.BuildForTests();
        Assert.That(ui.Q<Label>("ParlorCaptionSurface").style.display.value,
            Is.EqualTo(DisplayStyle.Flex));

        InvokeAccessibility("SetCaptions", false);

        Assert.That(ui.Q<Label>("ParlorCaptionSurface").style.display.value,
            Is.EqualTo(DisplayStyle.None));
    }

    [Test]
    public void HudExpiresActiveCaptionWithoutDeletingObservedEvidence()
    {
        StartActiveCaption();
        InvokeAccessibility("SetCaptions", true);
        var hud = root.AddComponent<GmParlorHud>();
        Assert.That(hud.TryConfigure(focus, coordinator, out string error), Is.True, error);
        VisualElement ui = hud.BuildForTests();
        int journalCount = evidence.Facts.Count;
        Assert.That(ui.Q<Label>("ParlorCaptionSurface").style.display.value,
            Is.EqualTo(DisplayStyle.Flex));

        coordinator.Advance(1f);

        Assert.That(ui.Q<Label>("ParlorCaptionSurface").style.display.value,
            Is.EqualTo(DisplayStyle.None));
        Assert.That(evidence.Facts, Has.Count.EqualTo(journalCount));
    }

    [Test]
    public void EveryHudRefreshStandardizesNewCardsEvidenceAndCaptionText()
    {
        evidence.Record(90, GmParlorObservedFact.CardContactBroke);
        var hud = root.AddComponent<GmParlorHud>();
        Assert.That(hud.TryConfigure(focus, coordinator, out string error), Is.True, error);
        VisualElement ui = hud.BuildForTests();
        focus.Open();
        hud.Refresh();

        Assert.That(ui.Q<Button>("ParlorCard_0"), Is.Not.Null);
        Assert.That(ui.Q<Label>("ParlorEvidence_0"), Is.Not.Null);
        Assert.That(ui.Q<Label>("ParlorCaptionSurface"), Is.Not.Null);
        foreach (TextElement element in ui.Query<TextElement>().ToList())
            Assert.That(element.style.unityTextGenerator.value,
                Is.EqualTo(TextGeneratorType.Standard),
                $"Parlor HUD dynamic text '{element.name}' was created after the only sweep");
    }

    [Test]
    public void HudSourceSweepsAfterRefreshCreatesDynamicButtonsAndEvidence()
    {
        string source = File.ReadAllText(Path.Combine(Application.dataPath,
            "Scripts/Scenes/parlor/GmParlorHud.cs"));
        int refresh = source.IndexOf("public void Refresh()", StringComparison.Ordinal);
        int destroy = source.IndexOf("void OnDestroy()", refresh, StringComparison.Ordinal);
        Assert.That(refresh, Is.GreaterThanOrEqualTo(0));
        Assert.That(destroy, Is.GreaterThan(refresh));
        StringAssert.Contains("GmUiText.UseStandardGenerator(root)",
            source.Substring(refresh, destroy - refresh),
            "Refresh creates TextElements after the BuildUi sweep and never standardizes them");
    }

    void StartActiveCaption()
    {
        var presenterRoot = new GameObject("AldricPresenter");
        presenterRoot.transform.SetParent(root.transform, false);
        var hand = new GameObject("RightHandCue");
        hand.transform.SetParent(presenterRoot.transform, false);
        var contact = new GameObject("CardContactCue");
        contact.transform.SetParent(presenterRoot.transform, false);
        var glove = GameObject.CreatePrimitive(PrimitiveType.Cube);
        glove.transform.SetParent(hand.transform, false);
        var sleeve = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sleeve.transform.SetParent(presenterRoot.transform, false);
        GmParlorAldricPresenter presenter = presenterRoot.AddComponent<GmParlorAldricPresenter>();
        Assert.That(presenter.TryConfigure(hand.transform, sleeve.GetComponent<Renderer>(),
            contact.transform, evidence, out string presenterError), Is.True, presenterError);
        Assert.That(coordinator.TryConfigure(binder, presenter,
            GmParlorAccessibilityProfile.Default, out string coordinatorError), Is.True,
            coordinatorError);
        GmParlorMatchSnapshot before = rules.Match.ExportSnapshot();
        Assert.That(rules.Match.PlayPlayerCard(0), Is.EqualTo(GmParlorActionError.None));
        GmParlorMatchSnapshot after = rules.Match.ExportSnapshot();
        after.tellObservation = GmTellObservation.Suspicious;
        Assert.That(coordinator.TryEnqueue(GmParlorPresentationJournal.Build(before, after),
            after, out string queueError), Is.True, queueError);
        for (int step = 0; step < 12 && coordinator.IsBlocking; step++)
            coordinator.Advance(1f);
        Assert.That(coordinator.IsBlocking, Is.False,
            "fixture never completed the active semantic cue");
        Assert.That(evidence.Facts, Is.Not.Empty,
            "fixture never retained its observed public cue");
        Assert.That(coordinator.HasActiveCaption, Is.False,
            "caption-disabled cue created a hidden caption lifecycle");
    }

    [Test]
    public void InputOpensFocusBeforeConfirmingAndCancelClosesItBeforeSkippingMotion()
    {
        var input = root.AddComponent<GmParlorInput>();
        Assert.That(input.TryConfigure(controller, focus, out string error), Is.True, error);
        string before = rules.Match.PublicStateBytes;

        Assert.That(input.HandleConfirmIntent(), Is.EqualTo(GmParlorActionError.None));
        Assert.That(focus.IsOpen, Is.True);
        Assert.That(rules.Match.PublicStateBytes, Is.EqualTo(before));
        Assert.That(input.HandleConfirmIntent(), Is.EqualTo(GmParlorActionError.None));
        Assert.That(rules.Match.PublicStateBytes, Is.Not.EqualTo(before));
        Assert.That(input.HandleCancelIntent(), Is.True);
        Assert.That(focus.IsOpen, Is.False);
    }

    static GmParlorCardView[] CreateViews(Transform parent)
    {
        var views = new List<GmParlorCardView>();
        foreach (GmSuit suit in new[] { GmSuit.Flames, GmSuit.Eyes, GmSuit.Teeth, GmSuit.Bones })
        for (int rank = 1; rank <= GmParlorCore.RanksPerSuit; rank++)
        {
            var go = new GameObject($"{suit}_{rank}");
            go.transform.SetParent(parent, false);
            GmParlorCardView view = go.AddComponent<GmParlorCardView>();
            view.Configure(new GmCard(suit, rank));
            views.Add(view);
        }
        return views.ToArray();
    }

    static void InvokeAccessibility(string method, params object[] arguments)
    {
        Type type = typeof(GmRunStore).Assembly.GetType("GmAccessibilitySettings");
        Assert.That(type, Is.Not.Null, "there is no global accessibility authority");
        MethodInfo info = type.GetMethod(method,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(info, Is.Not.Null);
        info.Invoke(null, arguments);
    }

    static void ResetAccessibility()
    {
        Type type = typeof(GmRunStore).Assembly.GetType("GmAccessibilitySettings");
        type?.GetMethod("ResetToDefaultsForTests",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.Invoke(null, null);
    }
}
