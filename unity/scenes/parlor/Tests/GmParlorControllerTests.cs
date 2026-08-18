using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class GmParlorControllerTests
{
    string directory;
    GameObject root;
    GmParlorRules rules;
    GmParlorPropBinder binder;
    GmParlorPresentationCoordinator presentation;
    GmParlorController controller;
    MutableBackend backend;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "gm-parlor-controller-" + Guid.NewGuid().ToString("N"));
        backend = new MutableBackend();
        GmSaveSystem.ConfigureForTests(Path.Combine(directory, "save.json"), backend);
        GmRunStore.BeginNewRun();
        root = new GameObject("ParlorControllerTest");
        rules = root.AddComponent<GmParlorRules>();
        if (rules.Match == null)
            Assert.That(rules.InitializeOrRestore(), Is.EqualTo(GmParlorInitializeResult.StartedNew));
        var cards = new GameObject("Cards");
        cards.transform.SetParent(root.transform, false);
        GmParlorCardView[] views = CreateViews(cards.transform);
        binder = cards.AddComponent<GmParlorPropBinder>();
        Assert.That(binder.TryConfigure(views, out string bindError), Is.True, bindError);
        Assert.That(binder.TryApply(rules.Match.ExportSnapshot(), out string applyError), Is.True, applyError);
        presentation = cards.AddComponent<GmParlorPresentationCoordinator>();
        Assert.That(presentation.TryConfigure(binder, out string presentationError), Is.True,
            presentationError);
        controller = root.AddComponent<GmParlorController>();
        Assert.That(controller.TryConfigure(rules, binder, presentation, out string controllerError),
            Is.True, controllerError);
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
        GmSaveSystem.ResetTestConfiguration();
        GmRunStore.BeginNewRun();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    [Test]
    public void HeldNavigationMovesFocusOnlyOnceUntilReleased()
    {
        Assert.That(controller.FocusedCardIndex, Is.EqualTo(0));

        controller.ApplyNavigation(Vector2.right);
        controller.ApplyNavigation(Vector2.right);
        controller.ApplyNavigation(Vector2.right);
        Assert.That(controller.FocusedCardIndex, Is.EqualTo(1));

        controller.ApplyNavigation(Vector2.zero);
        controller.ApplyNavigation(Vector2.right);
        Assert.That(controller.FocusedCardIndex, Is.EqualTo(2));
    }

    [Test]
    public void ConfirmPlaysFocusedCardThroughRulesAndQueuesPresentation()
    {
        GmCard focused = rules.PlayerHand[0];

        GmParlorActionError error = controller.ConfirmFocusedAction();

        Assert.That(error, Is.EqualTo(GmParlorActionError.None));
        Assert.That(rules.Match.Phase, Is.EqualTo(GmParlorMatchPhase.AwaitingAldricJudgement));
        Assert.That(rules.PlayerHand, Has.None.EqualTo(focused));
        Assert.That(presentation.IsBlocking, Is.True);
        Assert.That(controller.CanAcceptInput, Is.False);
        controller.CancelOrFastForward();
        Assert.That(presentation.IsBlocking, Is.False);
        Assert.That(controller.CanAcceptInput, Is.True);
    }

    [Test]
    public void LockedReadReturnsRuleErrorAndDoesNotMutateMatchOrRunState()
    {
        Assert.That(controller.ConfirmFocusedAction(), Is.EqualTo(GmParlorActionError.None));
        controller.CancelOrFastForward();
        string before = rules.Match.PublicStateBytes;
        int catches = GmRunStore.CheatsCaughtCount;
        int corruption = GmRunStore.CorruptionTier;

        GmParlorActionError error = controller.CallRead();

        Assert.That(error, Is.EqualTo(GmParlorActionError.ReadLocked));
        Assert.That(rules.Match.PublicStateBytes, Is.EqualTo(before));
        Assert.That(GmRunStore.CheatsCaughtCount, Is.EqualTo(catches));
        Assert.That(GmRunStore.CorruptionTier, Is.EqualTo(corruption));
        Assert.That(presentation.IsBlocking, Is.False);
    }

    [Test]
    public void CorrectAndFalseReadsHaveDistinctTruthfulResolvedFeedback()
    {
        Assert.That(rules.StartGame(8, 4, 0, true, forceRestart: true),
            Is.EqualTo(GmParlorInitializeResult.StartedNew));
        Assert.That(controller.Activate(), Is.EqualTo(GmParlorActionError.None));
        PlaySuitToJudgement(GmSuit.Eyes);
        Assert.That(controller.CallRead(), Is.EqualTo(GmParlorActionError.None));
        string caught = controller.LastPlayerFeedback;

        Assert.That(rules.StartGame(27, 4, 0, true, forceRestart: true),
            Is.EqualTo(GmParlorInitializeResult.StartedNew));
        Assert.That(controller.Activate(), Is.EqualTo(GmParlorActionError.None));
        PlaySuitToJudgement(GmSuit.Teeth);
        Assert.That(controller.CallRead(), Is.EqualTo(GmParlorActionError.None));
        string falseRead = controller.LastPlayerFeedback;

        Assert.That(caught, Is.Not.EqualTo(falseRead));
        StringAssert.Contains("caught", caught.ToLowerInvariant());
        StringAssert.Contains("honest", falseRead.ToLowerInvariant());
        StringAssert.DoesNotContain("cheat", falseRead.ToLowerInvariant());
    }

    [Test]
    public void ConfirmDuringJudgementResolvesCanonicalResultWithoutInventingCardMotion()
    {
        Assert.That(controller.ConfirmFocusedAction(), Is.EqualTo(GmParlorActionError.None));
        controller.CancelOrFastForward();
        Assert.That(rules.Match.Phase, Is.EqualTo(GmParlorMatchPhase.AwaitingAldricJudgement));

        GmParlorActionError error = controller.ConfirmFocusedAction();

        Assert.That(error, Is.EqualTo(GmParlorActionError.None));
        Assert.That(rules.Match.Phase, Is.EqualTo(GmParlorMatchPhase.TrickResult));
        Assert.That(presentation.IsBlocking, Is.False);
        Assert.That(controller.LastPresentationError, Is.Empty);
    }

    [Test]
    public void InvalidFocusedCardNeverCallsRulesOrQueuesPresentation()
    {
        GmParlorMatch match = FindAldricLeadWithRequiredSuit(out GmCard illegal);
        Assert.That(rules.Match.TryImport(match.ExportSnapshot(), out string importError), Is.True, importError);
        Assert.That(binder.TryApply(rules.Match.ExportSnapshot(), out string bindError), Is.True, bindError);
        int illegalIndex = rules.PlayerHand.ToList().IndexOf(illegal);
        controller.SetFocusedCardIndex(illegalIndex);
        string before = rules.Match.PublicStateBytes;

        GmParlorActionError error = controller.ConfirmFocusedAction();

        Assert.That(error, Is.EqualTo(GmParlorActionError.MustFollowSuit));
        Assert.That(rules.Match.PublicStateBytes, Is.EqualTo(before));
        Assert.That(presentation.IsBlocking, Is.False);
    }

    [Test]
    public void ControllerSourceHasNoRunStoreOrHiddenCheatAuthority()
    {
        string source = File.ReadAllText(Path.Combine(
            Application.dataPath, "Scripts/Scenes/parlor/GmParlorController.cs"));

        StringAssert.DoesNotContain("GmRunStore", source);
        StringAssert.DoesNotContain("AldricCheated", source);
        StringAssert.DoesNotContain("AldricCheatKind", source);
    }

    [Test]
    public void WalkingIsReleasedOnlyAtMatchResultAndBlockedAgainForRematch()
    {
        Assert.That(controller.PlayerControlShouldBeBlocked, Is.True);
        GmParlorMatch completed = DriveMatchToResult();
        Assert.That(rules.Match.TryImport(completed.ExportSnapshot(), out string importError),
            Is.True, importError);
        Assert.That(controller.PlayerControlShouldBeBlocked, Is.False);

        Assert.That(rules.StartRematch(), Is.EqualTo(GmParlorActionError.None));
        Assert.That(controller.PlayerControlShouldBeBlocked, Is.True);
    }

    [Test]
    public void RestoredPendingOutcomeKeepsInputClosedUntilExplicitActivation()
    {
        var pending = new GmParlorMatch(42, 1, 0, false);
        Assert.That(pending.Start(), Is.EqualTo(GmParlorActionError.None));
        Assert.That(pending.PlayPlayerCard(0), Is.EqualTo(GmParlorActionError.None));
        Assert.That(pending.ContinueJudgement(), Is.EqualTo(GmParlorActionError.None));
        Assert.That(pending.TryPeekOutcome(out _, out ulong sequence), Is.True);
        Assert.That(pending.MarkOutcomeDurable(sequence), Is.True);
        ReplaceWithRestoredController(pending.ExportSnapshot());
        int delivered = 0;
        rules.OnOutcomeReady += (_, __) => delivered++;

        Assert.That(controller.IsActivated, Is.False);
        Assert.That(controller.CanAcceptInput, Is.False);
        Assert.That(delivered, Is.Zero);
        Assert.That(controller.Activate(), Is.EqualTo(GmParlorActionError.None));
        Assert.That(delivered, Is.EqualTo(1));
        Assert.That(controller.IsActivated, Is.True);
        Assert.That(controller.CanAcceptInput, Is.True);
    }

    [Test]
    public void PersistenceFailureRollsBackAndLeavesControllerReadyForExactRetry()
    {
        Assert.That(controller.ConfirmFocusedAction(), Is.EqualTo(GmParlorActionError.None));
        controller.CancelOrFastForward();
        Assert.That(GmSaveSystem.Flush(), Is.True);
        string before = rules.Match.PublicStateBytes;
        backend.Fail = true;
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("injected"));

        Assert.That(controller.ConfirmFocusedAction(),
            Is.EqualTo(GmParlorActionError.PersistenceFailed));
        Assert.That(rules.Match.PublicStateBytes, Is.EqualTo(before));
        Assert.That(presentation.IsBlocking, Is.False);
        Assert.That(controller.CanAcceptInput, Is.True);

        backend.Fail = false;
        Assert.That(controller.ConfirmFocusedAction(), Is.EqualTo(GmParlorActionError.None));
    }

    [Test]
    public void ThrowingOutcomeObserverReportsErrorButPresentsDurablyAdvancedState()
    {
        Assert.That(controller.ConfirmFocusedAction(), Is.EqualTo(GmParlorActionError.None));
        controller.CancelOrFastForward();
        rules.OnOutcomeReady += (_, __) => throw new InvalidOperationException("observer exploded");
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("observer exploded"));

        Assert.That(controller.ConfirmFocusedAction(),
            Is.EqualTo(GmParlorActionError.OutcomeHandlerFailed));
        Assert.That(rules.Match.Phase, Is.EqualTo(GmParlorMatchPhase.TrickResult));
        Assert.That(controller.LastPresentationError, Is.Empty);
        Assert.That(rules.Match.HighestAcknowledgedOutcomeSequence,
            Is.EqualTo(rules.Match.OutcomeSequence));
    }

    static GmParlorMatch FindAldricLeadWithRequiredSuit(out GmCard illegal)
    {
        for (int seed = 1; seed < 10000; seed++)
        {
            var match = new GmParlorMatch(seed, 1, 0, false);
            match.Start();
            match.PlayPlayerCard(0);
            match.ContinueJudgement();
            if (match.LastTrickWinner != GmTrickOwner.Aldric) continue;
            match.Continue();
            if (match.Phase != GmParlorMatchPhase.PlayerFollowsAldricLead) continue;
            bool mustFollow = match.PlayerHand.Any(card => card.Suit == match.CurrentLeadCard.Value.Suit);
            if (!mustFollow) continue;
            GmCard candidate = match.PlayerHand.FirstOrDefault(card =>
                card.Suit != match.CurrentLeadCard.Value.Suit);
            if (candidate.Suit == match.CurrentLeadCard.Value.Suit) continue;
            illegal = candidate;
            return match;
        }
        Assert.Fail("No deterministic required-follow fixture found");
        illegal = default;
        return null;
    }

    void PlaySuitToJudgement(GmSuit suit)
    {
        int legal = Enumerable.Range(0, rules.PlayerHand.Count)
            .First(index => rules.PlayerHand[index].Suit == suit &&
                rules.GetPlayerCardError(index) == GmParlorActionError.None);
        controller.SetFocusedCardIndex(legal);
        Assert.That(controller.ConfirmFocusedAction(), Is.EqualTo(GmParlorActionError.None));
        presentation.FastForwardToCanonicalState();
        Assert.That(rules.Phase, Is.EqualTo(GmParlorMatchPhase.AwaitingAldricJudgement));
    }

    void ReplaceWithRestoredController(GmParlorMatchSnapshot snapshot)
    {
        Object.DestroyImmediate(root);
        root = null;
        Assert.That(GmRunStore.TrySetParlorMatch(snapshot, out string storeError), Is.True, storeError);
        root = new GameObject("RestoredParlorControllerTest");
        rules = root.AddComponent<GmParlorRules>();
        Assert.That(rules.InitializeOrRestore(), Is.EqualTo(GmParlorInitializeResult.Restored));
        var cards = new GameObject("Cards");
        cards.transform.SetParent(root.transform, false);
        binder = cards.AddComponent<GmParlorPropBinder>();
        Assert.That(binder.TryConfigure(CreateViews(cards.transform), out string bindError),
            Is.True, bindError);
        presentation = cards.AddComponent<GmParlorPresentationCoordinator>();
        Assert.That(presentation.TryConfigure(binder, out string presentationError),
            Is.True, presentationError);
        controller = root.AddComponent<GmParlorController>();
        Assert.That(controller.TryConfigure(rules, binder, presentation, out string controllerError),
            Is.True, controllerError);
    }

    static GmParlorMatch DriveMatchToResult()
    {
        var match = new GmParlorMatch(117, 1, 0, false);
        Assert.That(match.Start(), Is.EqualTo(GmParlorActionError.None));
        int guard = 128;
        while (match.Phase != GmParlorMatchPhase.MatchResult && guard-- > 0)
        {
            switch (match.Phase)
            {
                case GmParlorMatchPhase.PlayerLeads:
                case GmParlorMatchPhase.PlayerFollowsAldricLead:
                    int legal = Enumerable.Range(0, match.PlayerHand.Count)
                        .First(index => match.GetPlayerCardError(index) == GmParlorActionError.None);
                    Assert.That(match.PlayPlayerCard(legal), Is.EqualTo(GmParlorActionError.None));
                    break;
                case GmParlorMatchPhase.AwaitingAldricJudgement:
                    Assert.That(match.ContinueJudgement(), Is.EqualTo(GmParlorActionError.None));
                    break;
                case GmParlorMatchPhase.TrickResult:
                case GmParlorMatchPhase.RoundResult:
                    Assert.That(match.Continue(), Is.EqualTo(GmParlorActionError.None));
                    break;
                default:
                    Assert.Fail($"Unexpected match phase {match.Phase}");
                    break;
            }
            if (match.TryPeekOutcome(out _, out ulong sequence))
            {
                match.MarkOutcomeDurable(sequence);
                Assert.That(match.AcknowledgeOutcome(sequence), Is.True);
            }
        }
        Assert.That(match.Phase, Is.EqualTo(GmParlorMatchPhase.MatchResult));
        return match;
    }

    static GmParlorCardView[] CreateViews(Transform parent)
    {
        var views = new List<GmParlorCardView>(28);
        foreach (GmSuit suit in new[] { GmSuit.Flames, GmSuit.Eyes, GmSuit.Teeth, GmSuit.Bones })
        {
            for (int rank = 1; rank <= 7; rank++)
            {
                var go = new GameObject($"{suit}_{rank}");
                go.transform.SetParent(parent, false);
                GmParlorCardView view = go.AddComponent<GmParlorCardView>();
                view.Configure(new GmCard(suit, rank));
                views.Add(view);
            }
        }
        return views.ToArray();
    }

    sealed class MutableBackend : IGmAtomicSaveBackend
    {
        public bool Fail;

        public void WriteAtomic(string target, string json)
        {
            if (Fail) throw new IOException("injected controller persistence failure");
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllText(target, json);
        }
    }
}
