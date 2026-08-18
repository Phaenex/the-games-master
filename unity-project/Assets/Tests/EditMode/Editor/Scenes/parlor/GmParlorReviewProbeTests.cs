using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class GmParlorReviewProbeTests
{
    string directory;
    GameObject root;
    GmParlorRules rules;
    GmParlorPropBinder binder;
    GmParlorPresentationCoordinator presentation;
    GmParlorController controller;
    GmParlorEvidenceLog evidence;
    GmParlorFocusView focus;
    GmParlorInput input;
    GmParlorReviewProbe probe;
    GmPlayer player;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(),
            "gm-parlor-review-probe-" + Guid.NewGuid().ToString("N"));
        GmSaveSystem.ConfigureForTests(Path.Combine(directory, "save.json"),
            new FileBackend());
        GmRunStore.BeginNewRun();

        root = new GameObject("ParlorReviewProbeTest");
        player = root.AddComponent<GmPlayer>();
        rules = root.AddComponent<GmParlorRules>();
        if (rules.Match == null)
            Assert.That(rules.InitializeOrRestore(),
                Is.EqualTo(GmParlorInitializeResult.StartedNew));
        var cards = new GameObject("Cards");
        cards.transform.SetParent(root.transform, false);
        binder = cards.AddComponent<GmParlorPropBinder>();
        Assert.That(binder.TryConfigure(CreateViews(cards.transform), out string binderError),
            Is.True, binderError);
        Assert.That(binder.TryApply(rules.Match.ExportSnapshot(), out string applyError),
            Is.True, applyError);
        presentation = cards.AddComponent<GmParlorPresentationCoordinator>();
        Assert.That(presentation.TryConfigure(binder, out string presentationError),
            Is.True, presentationError);
        controller = root.AddComponent<GmParlorController>();
        Assert.That(controller.TryConfigure(rules, binder, presentation,
            out string controllerError), Is.True, controllerError);
        evidence = root.AddComponent<GmParlorEvidenceLog>();
        focus = root.AddComponent<GmParlorFocusView>();
        Assert.That(focus.TryConfigure(rules, controller, evidence, out string focusError),
            Is.True, focusError);
        input = root.AddComponent<GmParlorInput>();
        Assert.That(input.TryConfigure(controller, focus, out string inputError), Is.True,
            inputError);
        probe = root.AddComponent<GmParlorReviewProbe>();
        Assert.That(probe.TryConfigure(rules, controller, input, focus, presentation,
            out string probeError), Is.True, probeError);
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
    public void FrozenSeedMatrixNamesEveryRequiredSemanticCaseExactlyOnce()
    {
        GmParlorReviewCase[] cases = GmParlorReviewProbe.FrozenSeedMatrix;

        Assert.That(cases.Select(item => item.Id), Is.Unique);
        Assert.That(cases, Has.Some.Matches<GmParlorReviewCase>(item => item.ExpectCheat));
        Assert.That(cases, Has.Some.Matches<GmParlorReviewCase>(item => !item.ExpectCheat));
        Assert.That(cases, Has.Some.Matches<GmParlorReviewCase>(item =>
            item.ExpectCheat && item.ExpectedObservation == GmTellObservation.Suspicious));
        Assert.That(cases, Has.Some.Matches<GmParlorReviewCase>(item =>
            !item.ExpectCheat && item.ExpectedObservation == GmTellObservation.Suspicious));
        foreach (GmSuit suit in Enum.GetValues(typeof(GmSuit)))
            Assert.That(cases, Has.Some.Matches<GmParlorReviewCase>(item =>
                item.RequiredSuit == suit), $"matrix never exercises {suit}");
        Assert.That(cases, Has.Some.Matches<GmParlorReviewCase>(item => item.RequiredMatchWinner ==
            GmTrickOwner.Player));
        Assert.That(cases, Has.Some.Matches<GmParlorReviewCase>(item => item.RequiredMatchWinner ==
            GmTrickOwner.Aldric));
        Assert.That(cases, Has.Some.Matches<GmParlorReviewCase>(item => item.RequireRematch));
        Assert.That(cases, Has.Some.Matches<GmParlorReviewCase>(item => item.RequireRestore));
    }

    [Test]
    public void FrozenSeedMatrixIsLiteralVersionedAndReproducedByEditorGenerator()
    {
        GmParlorReviewCase[] regenerated =
            GmParlorReviewSeedCatalogGenerator.GenerateVersion1();

        Assert.That(regenerated.Select(item => item.Seed),
            Is.EqualTo(GmParlorReviewProbe.FrozenSeedMatrix.Select(item => item.Seed)));
        Assert.That(regenerated.Select(item => item.Id),
            Is.EqualTo(GmParlorReviewProbe.FrozenSeedMatrix.Select(item => item.Id)));
        foreach (GmParlorReviewCase item in GmParlorReviewProbe.FrozenSeedMatrix)
        {
            Assert.That(GmParlorReviewSeedCatalogGenerator.Validate(item, out string error),
                Is.True, $"{item.Id}: {error}");
        }
    }

    [Test]
    public void MatrixCoversBothCheatFamiliesAndAllReadConsequences()
    {
        GmParlorReviewCase[] cases = GmParlorReviewProbe.FrozenSeedMatrix;

        Assert.That(cases, Has.Some.Matches<GmParlorReviewCase>(item =>
            item.ExpectedCheatKind == GmParlorCheatKind.RenegedWithHeldFlame));
        Assert.That(cases, Has.Some.Matches<GmParlorReviewCase>(item =>
            item.ExpectedCheatKind == GmParlorCheatKind.ImpossibleEighthRank));
        foreach (GmParlorReviewDecision decision in new[]
        {
            GmParlorReviewDecision.Accept,
            GmParlorReviewDecision.CorrectRead,
            GmParlorReviewDecision.FalseRead,
            GmParlorReviewDecision.LateRead,
            GmParlorReviewDecision.LockedRead,
        })
            Assert.That(cases, Has.Some.Matches<GmParlorReviewCase>(item =>
                item.Decision == decision), $"matrix omits {decision}");
    }

    [Test]
    public void EveryFrozenCaseStagesThroughPublicControllerIntentAndMatchesItsOracle()
    {
        foreach (GmParlorReviewCase item in GmParlorReviewProbe.FrozenSeedMatrix)
        {
            Assert.That(probe.StageCaseJudgement(item, out string error), Is.True,
                $"{item.Id}: {error}");
            Assert.That(rules.Match.AldricCheated, Is.EqualTo(item.ExpectCheat), item.Id);
            Assert.That(rules.TellObservation, Is.EqualTo(item.ExpectedObservation), item.Id);
            Assert.That(rules.Match.AldricCheatKind, Is.EqualTo(item.ExpectedCheatKind), item.Id);
            presentation.FastForwardToCanonicalState();
        }
    }

    [Test]
    public void FrozenWinnerCasesReachMatchResultAndRematchThroughControllerOnly()
    {
        foreach (GmParlorReviewCase item in GmParlorReviewProbe.FrozenSeedMatrix
            .Where(candidate => candidate.RequiredMatchWinner.HasValue))
        {
            Assert.That(probe.StageCaseJudgement(item, out string stageError), Is.True,
                $"{item.Id}: {stageError}");
            Assert.That(probe.ResolveReviewDecision(item, out string decisionError), Is.True,
                $"{item.Id}: {decisionError}");
            Assert.That(probe.DriveToMatchResult(item.RequiredMatchWinner.Value,
                out string matchError), Is.True, $"{item.Id}: {matchError}");
            Assert.That(rules.Match.MatchWinner, Is.EqualTo(item.RequiredMatchWinner.Value));
            if (!item.RequireRematch) continue;
            Assert.That(controller.ConfirmFocusedAction(), Is.EqualTo(GmParlorActionError.None));
            presentation.FastForwardToCanonicalState();
            Assert.That(rules.Phase, Is.Not.EqualTo(GmParlorMatchPhase.MatchResult));
        }
    }

    [TestCase(GmParlorMatchPhase.PlayerFollowsAldricLead)]
    [TestCase(GmParlorMatchPhase.RoundResult)]
    public void ProbeCanStopAtIntermediatePublicPhasesForStateEvidence(
        GmParlorMatchPhase target)
    {
        GmParlorReviewCase item = GmParlorReviewProbe.FrozenSeedMatrix[0];
        Assert.That(probe.StageCaseJudgement(item, out string stageError), Is.True, stageError);
        Assert.That(probe.ResolveReviewDecision(item, out string decisionError), Is.True,
            decisionError);

        Assert.That(probe.DriveToPhase(target, GmTrickOwner.Aldric, out string error),
            Is.True, error);
        Assert.That(rules.Phase, Is.EqualTo(target));
    }

    [Test]
    public void ProbeDrivesHeldNavigationThroughControllerWithoutRepeating()
    {
        Assert.That(probe.ProveHeldNavigation(out string error), Is.True, error);
        Assert.That(controller.FocusedCardIndex, Is.EqualTo(1));
    }

    [Test]
    public void SimultaneousReadAndInteractHasOneDeterministicWinner()
    {
        Assert.That(probe.StageFirstJudgement(readUnlocked: true, out string stageError),
            Is.True, stageError);
        string before = rules.Match.PublicStateBytes;

        GmParlorFrameIntentResult result = input.ResolveFrameIntents(
            cancelPressed: false, readPressed: true, confirmPressed: true);

        Assert.That(result.Consumed, Is.EqualTo(GmParlorFrameIntent.Read));
        Assert.That(rules.Match.PublicStateBytes, Is.Not.EqualTo(before));
        Assert.That(rules.Match.Phase, Is.EqualTo(GmParlorMatchPhase.TrickResult));
        Assert.That(rules.Match.OutcomeSequence, Is.EqualTo(1),
            "same-frame Interact ran after Read and resolved a second action");
    }

    [Test]
    public void PauseDuringJudgementConsumesNoGameplayIntent()
    {
        Assert.That(probe.StageFirstJudgement(readUnlocked: true, out string stageError),
            Is.True, stageError);
        presentation.FastForwardToCanonicalState();
        focus.Open();
        string before = rules.Match.PublicStateBytes;
        int beforeFocus = controller.FocusedCardIndex;
        player.SetPaused(true);

        input.HandleNavigationIntent(Vector2.right);
        GmParlorFrameIntentResult result = input.ResolveFrameIntents(
            cancelPressed: false, readPressed: true, confirmPressed: true);

        Assert.That(result.Consumed, Is.EqualTo(GmParlorFrameIntent.None));
        Assert.That(rules.Match.PublicStateBytes, Is.EqualTo(before));
        Assert.That(controller.FocusedCardIndex, Is.EqualTo(beforeFocus));
        Assert.That(focus.IsOpen, Is.True);
        player.SetPaused(false);
    }

    [Test]
    public void PauseFreezesPresentationInsteadOfExpiringMotionBehindTheMenu()
    {
        Assert.That(probe.StageMotionPhase(GmParlorMotionPhase.Approach, out string error),
            Is.True, error);
        player.SetPaused(true);
        GmParlorMotionPhase before = presentation.CurrentMotionPhase;
        Vector3[] positions = root.GetComponentsInChildren<GmParlorCardView>(true)
            .Select(view => view.transform.localPosition).ToArray();

        presentation.Advance(0.05f);

        Assert.That(presentation.IsBlocking, Is.True);
        Assert.That(presentation.CurrentMotionPhase, Is.EqualTo(before));
        Assert.That(root.GetComponentsInChildren<GmParlorCardView>(true)
            .Select(view => view.transform.localPosition).ToArray(), Is.EqualTo(positions),
            "card choreography advanced while authoritative pause owned the frame");
        player.SetPaused(false);
        presentation.Advance(0.05f);
        Assert.That(root.GetComponentsInChildren<GmParlorCardView>(true)
            .Select(view => view.transform.localPosition).ToArray(), Is.Not.EqualTo(positions),
            "card choreography did not resume after authoritative pause released it");
    }

    [TestCase(GmParlorMotionPhase.Approach)]
    [TestCase(GmParlorMotionPhase.Contact)]
    [TestCase(GmParlorMotionPhase.Manipulate)]
    [TestCase(GmParlorMotionPhase.Release)]
    [TestCase(GmParlorMotionPhase.Settle)]
    public void CancelDuringEveryMotionPhaseSnapsToCanonicalState(
        GmParlorMotionPhase phase)
    {
        Assert.That(probe.StageMotionPhase(phase, out string error), Is.True, error);
        string canonical = rules.Match.PublicStateBytes;

        Assert.That(input.HandleCancelIntent(), Is.True);

        Assert.That(presentation.IsBlocking, Is.False);
        Assert.That(rules.Match.PublicStateBytes, Is.EqualTo(canonical));
        AssertCanonicalBindings(rules.Match.ExportSnapshot());
    }

    [Test]
    public void ProbeSourceCannotMutateCanonicalMatchOrRunStoreDirectly()
    {
        string source = File.ReadAllText(Path.Combine(Application.dataPath,
            "Scripts/Scenes/parlor/GmParlorReviewProbe.cs"));

        StringAssert.DoesNotContain("TryImport", source);
        StringAssert.DoesNotContain("GmRunStore", source);
        StringAssert.DoesNotContain("Match.PlayPlayerCard", source);
        StringAssert.DoesNotContain("Match.Continue", source);
        StringAssert.DoesNotContain("Match.Read", source);
    }

    static GmParlorCardView[] CreateViews(Transform parent)
    {
        var views = new List<GmParlorCardView>(GmParlorCore.TotalCards);
        foreach (GmSuit suit in Enum.GetValues(typeof(GmSuit)))
        {
            for (int rank = 1; rank <= GmParlorCore.RanksPerSuit; rank++)
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

    void AssertCanonicalBindings(GmParlorMatchSnapshot snapshot)
    {
        foreach (GmParlorCardBinding binding in GmParlorTableLayout.Build(snapshot))
        {
            Assert.That(binder.TryGetView(binding.PhysicalCard, out GmParlorCardView view),
                Is.True, $"missing view for {binding.PhysicalCard}");
            Assert.That(Vector3.Distance(view.transform.localPosition,
                    GmParlorTableLayout.LocalPosition(binding)),
                Is.LessThan(0.0001f), $"{binding.PhysicalCard} did not snap to {binding.Zone}");
        }
    }

    sealed class FileBackend : IGmAtomicSaveBackend
    {
        public void WriteAtomic(string target, string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllText(target, json);
        }
    }
}
