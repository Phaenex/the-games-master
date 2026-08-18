using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

public sealed class GmParlorPresentationRestoreTests
{
    string directory;
    string savePath;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(),
            "gm-parlor-presentation-restore-" + Guid.NewGuid().ToString("N"));
        savePath = Path.Combine(directory, "save.json");
        GmSaveSystem.ConfigureForTests(savePath);
        GmRunStore.BeginNewRun();
    }

    [TearDown]
    public void TearDown()
    {
        GmSaveSystem.Flush();
        GmSaveSystem.ResetTestConfiguration();
        GmRunStore.BeginNewRun();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    [Test]
    public void PresentationStateIsVersionedOwnedAndValidatedBeforeDiskPersistence()
    {
        RuntimeGraph graph = StartProducer(317, 4, read: true);
        var state = new GmParlorPresentationState
        {
            version = GmParlorPresentationState.CurrentVersion,
            observedFacts = new()
            {
                new GmParlorObservedFactData(17,
                    GmParlorObservedFact.RightHandPausedAboveDeck),
                new GmParlorObservedFactData(17,
                    GmParlorObservedFact.CardContactBroke),
            },
        };
        SetPresentationIdentity(state, graph.Rules.Match.ExportSnapshot().seed,
            GmRunStore.ParlorOutcomeNamespace);

        Assert.That(GmRunStore.TrySetParlorPresentationState(state, out string error),
            Is.True, error);
        state.observedFacts.Clear();
        Assert.That(GmRunStore.GetParlorPresentationState().observedFacts, Has.Count.EqualTo(2),
            "the run store must own a deep copy");
        Assert.That(GmSaveSystem.Save(), Is.True);

        GmRunStore.BeginNewRun();
        Assert.That(GmSaveSystem.Load(), Is.True);
        GmParlorPresentationState restored = GmRunStore.GetParlorPresentationState();
        Assert.That(restored.version, Is.EqualTo(GmParlorPresentationState.CurrentVersion));
        Assert.That(restored.observedFacts.Select(item => item.commandId), Is.All.EqualTo(17));

        restored.observedFacts.Add(restored.observedFacts[0].DeepCopy());
        Assert.That(GmRunStore.TrySetParlorPresentationState(restored, out error), Is.False);
        StringAssert.Contains("duplicate", error.ToLowerInvariant());

        restored.observedFacts = null;
        Assert.That(GmRunStore.TrySetParlorPresentationState(restored, out error), Is.False);
        StringAssert.Contains("null", error.ToLowerInvariant());
        Object.DestroyImmediate(graph.Root);
    }

    [Test]
    public void PresentationIdentityAndFactSetsRejectSplicesOrphansAndPartialCommandsWithoutMutation()
    {
        RuntimeGraph graph = StartProducer(417, 4, read: true);
        try
        {
            var valid = GmParlorPresentationState.Empty();
            SetPresentationIdentity(valid, graph.Rules.Match.ExportSnapshot().seed,
                GmRunStore.ParlorOutcomeNamespace);
            valid.observedFacts.Add(new GmParlorObservedFactData(71,
                GmParlorObservedFact.RightHandPausedAboveDeck));
            valid.observedFacts.Add(new GmParlorObservedFactData(71,
                GmParlorObservedFact.CardContactBroke));
            Assert.That(GmRunStore.TrySetParlorPresentationState(valid, out string error),
                Is.True, error);
            string accepted = JsonUtility.ToJson(GmRunStore.GetParlorPresentationState());

            GmParlorPresentationState spliced = valid.DeepCopy();
            SetPresentationIdentity(spliced, graph.Rules.Match.ExportSnapshot().seed + 1,
                GmRunStore.ParlorOutcomeNamespace);
            Assert.That(GmRunStore.TrySetParlorPresentationState(spliced, out error), Is.False);
            StringAssert.Contains("identity", error.ToLowerInvariant());
            Assert.That(JsonUtility.ToJson(GmRunStore.GetParlorPresentationState()),
                Is.EqualTo(accepted), "rejected splice mutated the accepted journal");

            GmParlorPresentationState partial = valid.DeepCopy();
            partial.observedFacts.RemoveAt(1);
            Assert.That(GmRunStore.TrySetParlorPresentationState(partial, out error), Is.False);
            StringAssert.Contains("partial", error.ToLowerInvariant());
            Assert.That(JsonUtility.ToJson(GmRunStore.GetParlorPresentationState()),
                Is.EqualTo(accepted));

            GmParlorPresentationState mixed = valid.DeepCopy();
            mixed.observedFacts.Add(new GmParlorObservedFactData(71,
                GmParlorObservedFact.CardPlacedWithoutPause));
            Assert.That(GmRunStore.TrySetParlorPresentationState(mixed, out error), Is.False);
            StringAssert.Contains("mixed", error.ToLowerInvariant());
            Assert.That(JsonUtility.ToJson(GmRunStore.GetParlorPresentationState()),
                Is.EqualTo(accepted));
        }
        finally { Object.DestroyImmediate(graph.Root); }

        GmRunStore.BeginNewRun();
        Assert.That(GmRunStore.TrySetParlorPresentationState(
            BoundCalmState(417, GmRunStore.ParlorOutcomeNamespace, 81), out string orphanError),
            Is.False);
        StringAssert.Contains("match", orphanError.ToLowerInvariant());
    }

    [Test]
    public void LoadRejectsOrphanOrSplicedPresentationAndClearAlwaysRemovesTheOrphan()
    {
        GmSaveData orphan = GmRunStore.ToSaveData();
        orphan.parlorMatch = null;
        orphan.parlorPresentation = BoundCalmState(99, 0, 91);
        GmRunStore.LoadFromSaveData(orphan);
        Assert.That(GmRunStore.ParlorPresentationRestoreError.ToLowerInvariant(),
            Does.Contain("match"));
        GmRunStore.ClearParlorMatch();
        Assert.That(GmRunStore.GetParlorPresentationState().observedFacts, Is.Empty);
        Assert.That(GmRunStore.ParlorPresentationRestoreError, Is.Empty);

        RuntimeGraph graph = StartProducer(517, 4, read: true);
        GmSaveData spliced = GmRunStore.ToSaveData();
        spliced.parlorPresentation = BoundCalmState(518,
            GmRunStore.ParlorOutcomeNamespace, 101);
        Object.DestroyImmediate(graph.Root);
        GmRunStore.LoadFromSaveData(spliced);
        Assert.That(GmRunStore.ParlorPresentationRestoreError.ToLowerInvariant(),
            Does.Contain("identity"));
    }

    [Test]
    public void AbandonAndNewRunClearPresentationIdentityAndRejectThePriorSession()
    {
        RuntimeGraph graph = StartProducer(617, 4, read: true);
        ulong oldNamespace = GmRunStore.ParlorOutcomeNamespace;
        GmParlorPresentationState old = BoundCalmState(617, oldNamespace, 111);
        Assert.That(GmRunStore.TrySetParlorPresentationState(old, out string error),
            Is.True, error);
        Assert.That(graph.Rules.AbandonSavedMatch(), Is.True);
        Assert.That(GmRunStore.GetParlorPresentationState().observedFacts, Is.Empty);
        Assert.That(GmRunStore.ParlorOutcomeNamespace, Is.EqualTo(oldNamespace + 1));
        Object.DestroyImmediate(graph.Root);

        RuntimeGraph replacement = StartProducer(617, 4, read: true);
        try
        {
            Assert.That(GmRunStore.TrySetParlorPresentationState(old, out error), Is.False);
            StringAssert.Contains("identity", error.ToLowerInvariant());
            GmRunStore.BeginNewRun();
            Assert.That(GmRunStore.GetParlorPresentationState().observedFacts, Is.Empty);
        }
        finally { Object.DestroyImmediate(replacement.Root); }
    }

    [Test]
    public void CrashBeforeOpenJudgementRepairsMissingObservationOnceAcrossTwoDiskReloads()
    {
        var backend = new CountingBackend();
        GmSaveSystem.ConfigureForTests(savePath, backend);
        GmRunStore.BeginNewRun();
        GmAccessibilitySettings.SetCaptions(true);
        JudgementFixture fixture = FindJudgementFixture(cheated: true);
        RuntimeGraph producer = StartProducer(fixture.Seed, 4, read: true);
        producer.Controller.SetFocusedCardIndex(fixture.CardIndex);
        Assert.That(producer.Controller.ConfirmFocusedAction(),
            Is.EqualTo(GmParlorActionError.None));
        Assert.That(producer.Rules.Match.Phase,
            Is.EqualTo(GmParlorMatchPhase.AwaitingAldricJudgement));
        Assert.That(producer.Coordinator.IsBlocking, Is.True,
            "the crash point must be before queued card motion reaches OpenJudgement");
        Assert.That(producer.Evidence.Facts, Is.Empty);
        Assert.That(GmSaveSystem.Flush(), Is.True);
        Object.DestroyImmediate(producer.Root);

        GmRunStore.BeginNewRun();
        Assert.That(GmSaveSystem.Load(), Is.True);
        RuntimeGraph restored = BuildGraphCore();
        var firstEvents = new RuntimeEventCounts(restored);
        Assert.That(restored.Rules.InitializeOrRestore(),
            Is.EqualTo(GmParlorInitializeResult.Restored));
        FinishGraph(restored);
        int changesAfterConfigure = firstEvents.EvidenceChanges;
        int writesBeforeRepair = backend.WriteCount;
        Assert.That(restored.Evidence.Facts, Is.Empty);
        Assert.That(restored.Controller.Activate(), Is.EqualTo(GmParlorActionError.None));
        Assert.That(restored.Evidence.Facts, Has.Count.EqualTo(2));
        Assert.That(firstEvents.EvidenceChanges, Is.EqualTo(changesAfterConfigure + 1));
        Assert.That(firstEvents.Cues, Is.Zero);
        Assert.That(backend.WriteCount, Is.EqualTo(writesBeforeRepair + 1));
        Assert.That(restored.Coordinator.HasActiveCaption, Is.True);
        Assert.That(GmSaveSystem.Flush(), Is.True);
        Object.DestroyImmediate(restored.Root);

        GmRunStore.BeginNewRun();
        Assert.That(GmSaveSystem.Load(), Is.True);
        RuntimeGraph second = BuildGraphCore();
        var secondEvents = new RuntimeEventCounts(second);
        Assert.That(second.Rules.InitializeOrRestore(), Is.EqualTo(GmParlorInitializeResult.Restored));
        FinishGraph(second);
        int secondChangesAfterConfigure = secondEvents.EvidenceChanges;
        int writesBeforeSecondActivate = backend.WriteCount;
        try
        {
            Assert.That(second.Controller.Activate(), Is.EqualTo(GmParlorActionError.None));
            Assert.That(secondEvents.EvidenceChanges, Is.EqualTo(secondChangesAfterConfigure));
            Assert.That(secondEvents.Cues, Is.Zero);
            Assert.That(backend.WriteCount, Is.EqualTo(writesBeforeSecondActivate));
            Assert.That(second.Evidence.Facts, Has.Count.EqualTo(2));
        }
        finally { Object.DestroyImmediate(second.Root); }
    }

    [Test]
    public void ThrowingCueObserverIsContainedAndVisibleThroughControllerError()
    {
        JudgementFixture fixture = FindJudgementFixture(cheated: true);
        RuntimeGraph graph = StartProducer(fixture.Seed, 4, read: true);
        try
        {
            graph.Presenter.OnCuePresented += _ =>
                throw new InvalidOperationException("graph cue observer exploded");
            SelectCard(graph, fixture.CardIndex);
            Assert.That(graph.Coordinator.IsBlocking, Is.False);
            Assert.That(graph.Coordinator.LastError,
                Does.Contain("graph cue observer exploded"));
            Assert.That(graph.Controller.LastPresentationError,
                Does.Contain("graph cue observer exploded"));
            AssertBindings(GmParlorTableLayout.Build(graph.Rules.Match.ExportSnapshot()),
                graph.Binder);
        }
        finally { Object.DestroyImmediate(graph.Root); }
    }

    [Test]
    public void RestoreSnapClearsTransientWorkAndRebuildsCurrentPublicTellWithoutReplayingCue()
    {
        GmParlorMatch match = FindAwaitingJudgement(cheated: true);
        GmParlorMatchSnapshot before = match.ExportSnapshot();
        before.phase = GmParlorMatchPhase.PlayerLeads;
        before.hasCurrentLeadCard = false;
        before.hasCurrentFollowCard = false;
        before.playerHand.Add(before.currentLeadCard);
        GmParlorPresentationCommand command =
            GmParlorPresentationJournal.Build(before, match.ExportSnapshot())
                .Single(item => item.Action == GmParlorPresentationAction.OpenJudgement);

        GameObject root = BuildPresentationRig(out GmParlorPropBinder binder,
            out GmParlorPresentationCoordinator coordinator, out GmParlorAldricPresenter presenter,
            out GmParlorEvidenceLog evidence);
        try
        {
            int cues = 0;
            presenter.OnCuePresented += _ => cues++;
            Assert.That(evidence.Record(command.Id,
                GmParlorObservedFact.RightHandPausedAboveDeck), Is.True);
            Assert.That(evidence.Record(command.Id,
                GmParlorObservedFact.CardContactBroke), Is.True);

            Assert.That(coordinator.TryRestoreCanonicalState(match.ExportSnapshot(),
                out string error), Is.True, error);

            Assert.That(coordinator.IsBlocking, Is.False);
            Assert.That(coordinator.CurrentMotionPhase, Is.EqualTo(GmParlorMotionPhase.Complete));
            Assert.That(coordinator.HasActiveCaption,
                Is.EqualTo(GmAccessibilitySettings.Captions));
            Assert.That(presenter.LastCue.CommandId, Is.EqualTo(command.Id));
            Assert.That(presenter.LastCue.Observation, Is.EqualTo(match.TellObservation));
            Assert.That(cues, Is.Zero, "restore must not re-emit the cue event");
            Assert.That(evidence.Facts, Has.Count.EqualTo(2),
                "restore must not duplicate retained observations");
            GmParlorCardBinding[] expected = GmParlorTableLayout.Build(match.ExportSnapshot());
            GmParlorCardView[] actual =
                binder.GetComponentsInChildren<GmParlorCardView>(true);
            foreach (GmParlorCardBinding binding in expected)
                Assert.That(actual.Single(view => view.PhysicalCard == binding.PhysicalCard).Binding,
                    Is.EqualTo(binding), binding.PhysicalCard.ToString());
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void SuspiciousObservationPersistsBothFactsAsOneAtomicTransaction()
    {
        var backend = new CountingBackend();
        GmSaveSystem.ConfigureForTests(savePath, backend);
        GmRunStore.BeginNewRun();
        RuntimeGraph graph = StartProducer(327, 4, read: true);
        var root = new GameObject("AtomicEvidence");
        var evidence = root.AddComponent<GmParlorEvidenceLog>();
        evidence.EnableDurablePersistence();
        try
        {
            backend.Fail = true;
            LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex("injected presentation write"));
            Assert.That(evidence.TryRecordObservation(91,
                GmTellObservation.Suspicious), Is.False);
            Assert.That(evidence.Facts, Is.Empty);
            Assert.That(GmRunStore.GetParlorPresentationState().observedFacts, Is.Empty);

            backend.Fail = false;
            int beforeWrites = backend.WriteCount;
            Assert.That(evidence.TryRecordObservation(91,
                GmTellObservation.Suspicious), Is.True);
            Assert.That(backend.WriteCount, Is.EqualTo(beforeWrites + 1));
            Assert.That(evidence.Facts, Has.Count.EqualTo(2));
            Assert.That(GmRunStore.GetParlorPresentationState().observedFacts,
                Has.Count.EqualTo(2));
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(graph.Root);
        }
    }

    [Test]
    public void CorruptPresentationPayloadCannotBeBypassedByCallingActivateAfterConfigureFails()
    {
        GmParlorMatch match = Started(88, true);
        var corrupt = GmParlorPresentationState.Empty();
        SetPresentationIdentity(corrupt, match.ExportSnapshot().seed,
            GmRunStore.ParlorOutcomeNamespace);
        corrupt.observedFacts.Add(new GmParlorObservedFactData(12,
            GmParlorObservedFact.CardContactBroke));
        corrupt.observedFacts.Add(new GmParlorObservedFactData(12,
            GmParlorObservedFact.CardContactBroke));
        GmSaveData save = GmRunStore.ToSaveData();
        save.parlorMatch = match.ExportSnapshot();
        save.parlorPresentation = corrupt;
        GmRunStore.LoadFromSaveData(save);
        Assert.That(GmRunStore.ParlorPresentationRestoreError, Does.Contain("duplicate"));

        GameObject root = BuildPresentationRig(out GmParlorPropBinder binder,
            out GmParlorPresentationCoordinator coordinator, out _, out _);
        try
        {
            var rules = root.AddComponent<GmParlorRules>();
            if (rules.Match == null) rules.InitializeOrRestore();
            var controller = root.AddComponent<GmParlorController>();
            Assert.That(controller.TryConfigure(rules, binder, coordinator,
                out string error), Is.False);
            StringAssert.Contains("duplicate", error);
            Assert.That(controller.IsActivated, Is.False);
            Assert.That(controller.CanAcceptInput, Is.False);
            Assert.That(controller.Activate(), Is.EqualTo(GmParlorActionError.WrongPhase));
            Assert.That(controller.IsActivated, Is.False);
            Assert.That(controller.CanAcceptInput, Is.False);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void ActivationDetectsRestoreEvenWhenControllerConfiguredBeforeRulesAwakeOrder()
    {
        GmParlorMatch match = FindAwaitingJudgement(true);
        GmParlorMatchSnapshot snapshot = match.ExportSnapshot();
        GmParlorPresentationState expectedEvidence = EvidenceFor(snapshot);
        Assert.That(GmRunStore.TrySetParlorMatch(snapshot, out string matchError),
            Is.True, matchError);
        Assert.That(GmRunStore.TrySetParlorPresentationState(expectedEvidence,
            out string evidenceError), Is.True, evidenceError);

        GameObject root = BuildPresentationRig(out GmParlorPropBinder binder,
            out GmParlorPresentationCoordinator coordinator, out GmParlorAldricPresenter presenter,
            out GmParlorEvidenceLog evidence);
        try
        {
            var rules = root.AddComponent<GmParlorRules>();
            var controller = root.AddComponent<GmParlorController>();
            Assert.That(controller.TryConfigure(rules, binder, coordinator,
                out string configureError), Is.True, configureError);
            Assert.That(rules.Match, Is.Null, "this fixture proves the hostile Awake order");
            Assert.That(rules.InitializeOrRestore(), Is.EqualTo(GmParlorInitializeResult.Restored));

            Assert.That(controller.Activate(), Is.EqualTo(GmParlorActionError.None));
            Assert.That(controller.IsActivated, Is.True);
            Assert.That(controller.CanAcceptInput, Is.True);
            Assert.That(presenter.LastCue.CommandId,
                Is.EqualTo(GmParlorPresentationJournal.RestoreOpenJudgement(snapshot).Id));
            AssertEvidence(expectedEvidence, evidence);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void LegacyAwaitingSaveRepairsMissingCurrentObservationExactlyOnce()
    {
        GmParlorMatch match = FindAwaitingJudgement(true);
        GmParlorMatchSnapshot snapshot = match.ExportSnapshot();
        Assert.That(GmRunStore.TrySetParlorMatch(snapshot, out string matchError),
            Is.True, matchError);
        GmParlorPresentationState empty = GmParlorPresentationState.Empty();
        SetPresentationIdentity(empty, snapshot.seed, GmRunStore.ParlorOutcomeNamespace);
        Assert.That(GmRunStore.TrySetParlorPresentationState(empty, out string evidenceError),
            Is.True, evidenceError);
        Assert.That(GmSaveSystem.Save(), Is.True);
        GmRunStore.BeginNewRun();
        Assert.That(GmSaveSystem.Load(), Is.True);

        GameObject restored = BuildFullRuntime(out _, out _, out _,
            out GmParlorAldricPresenter presenter, out GmParlorEvidenceLog evidence,
            out GmParlorController controller, out _);
        try
        {
            int cues = 0;
            int evidenceChanges = 0;
            presenter.OnCuePresented += _ => cues++;
            evidence.OnChanged += () => evidenceChanges++;
            Assert.That(evidence.Facts, Is.Empty);
            Assert.That(controller.Activate(), Is.EqualTo(GmParlorActionError.None));
            Assert.That(evidence.Facts, Has.Count.EqualTo(2));
            Assert.That(GmRunStore.GetParlorPresentationState().observedFacts,
                Has.Count.EqualTo(2));
            Assert.That(evidenceChanges, Is.EqualTo(1));
            Assert.That(cues, Is.Zero);
            Assert.That(presenter.LastCue.CommandId,
                Is.EqualTo(GmParlorPresentationJournal.RestoreOpenJudgement(snapshot).Id));
        }
        finally { Object.DestroyImmediate(restored); }
    }

    [Test]
    public void RepairingPartialLegacyObservationKeepsBothFactsAtCapacityBoundary()
    {
        RuntimeGraph graph = StartProducer(337, 4, read: true);
        var state = GmParlorPresentationState.Empty();
        state.version = 1;
        state.observedFacts.Add(new GmParlorObservedFactData(44,
            GmParlorObservedFact.RightHandPausedAboveDeck));
        for (ulong id = 100; id < 107; id++)
            state.observedFacts.Add(new GmParlorObservedFactData(id,
                GmParlorObservedFact.SleeveBrushedTable));
        var root = new GameObject("PartialLegacyEvidence");
        var evidence = root.AddComponent<GmParlorEvidenceLog>();
        try
        {
            GmSaveData legacy = GmRunStore.ToSaveData();
            legacy.parlorPresentation = state;
            GmRunStore.LoadFromSaveData(legacy);
            Assert.That(GmRunStore.ParlorPresentationRestoreError, Is.Empty);
            Assert.That(evidence.TryRestoreFromRunStore(out string error), Is.True, error);
            Assert.That(evidence.Facts.Any(item => item.CommandId == 44 &&
                item.Fact == GmParlorObservedFact.RightHandPausedAboveDeck), Is.True);
            Assert.That(evidence.Facts.Any(item => item.CommandId == 44 &&
                item.Fact == GmParlorObservedFact.CardContactBroke), Is.True);
            Assert.That(evidence.Facts, Has.Count.EqualTo(2),
                "obsolete free-form legacy facts should be dropped during explicit migration");
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(graph.Root);
        }
    }

    [Test]
    public void LegacyMigrationRejectsMixedDuplicateAndInvalidFacts()
    {
        RuntimeGraph graph = StartProducer(347, 4, read: true);
        try
        {
            GmParlorPresentationState[] corrupt =
            {
                LegacyState(
                    new GmParlorObservedFactData(51,
                        GmParlorObservedFact.RightHandPausedAboveDeck),
                    new GmParlorObservedFactData(51,
                        GmParlorObservedFact.CardPlacedWithoutPause)),
                LegacyState(
                    new GmParlorObservedFactData(52,
                        GmParlorObservedFact.CardPlacedWithoutPause),
                    new GmParlorObservedFactData(52,
                        GmParlorObservedFact.CardPlacedWithoutPause)),
                LegacyState(new GmParlorObservedFactData(53,
                    (GmParlorObservedFact)999)),
            };
            foreach (GmParlorPresentationState state in corrupt)
            {
                GmSaveData save = GmRunStore.ToSaveData();
                save.parlorPresentation = state;
                GmRunStore.LoadFromSaveData(save);
                Assert.That(GmRunStore.ParlorPresentationRestoreError, Is.Not.Empty);
                Assert.That(GmRunStore.GetParlorPresentationState().observedFacts, Is.Empty);
            }
        }
        finally { Object.DestroyImmediate(graph.Root); }
    }

    [Test]
    public void RestoreImplementationCannotCallCanonicalTransitionMethodsOrReadHiddenTruth()
    {
        string coordinator = File.ReadAllText(Path.Combine(Application.dataPath,
            "Scripts/Scenes/parlor/GmParlorPresentationCoordinator.cs"));
        string evidence = File.ReadAllText(Path.Combine(Application.dataPath,
            "Scripts/Scenes/parlor/GmParlorEvidenceLog.cs"));
        StringAssert.DoesNotContain(".PlayPlayerCard(", coordinator);
        StringAssert.DoesNotContain(".ContinueJudgement(", coordinator);
        StringAssert.DoesNotContain(".StartRematch(", coordinator);
        StringAssert.DoesNotContain("AldricCheated", evidence);
        StringAssert.DoesNotContain("AldricCheatKind", evidence);
    }

    [TestCase("PlayerLeads")]
    [TestCase("PlayerFollowsAldricLead")]
    [TestCase("AwaitingHonestJudgement")]
    [TestCase("AwaitingCheatJudgement")]
    [TestCase("TrickResult")]
    [TestCase("RoundResult")]
    [TestCase("MatchResult")]
    [TestCase("DeferredReadMatchResult")]
    [TestCase("DeferredReadRematch")]
    public void EveryVisiblePhaseRoundTripsThroughDiskAndRecreatesTheSameTableAndEvidence(
        string fixtureName)
    {
        RuntimeGraph producer = ProduceGraphFixture(fixtureName);
        GmParlorMatchSnapshot snapshot = producer.Rules.Match.ExportSnapshot();
        GmParlorCardBinding[] expectedBindings = CaptureBindings(producer.Binder);
        GmParlorPresentationState expectedEvidence = producer.Evidence.ExportState();
        int uninterruptedCues = producer.Events.Cues;
        int uninterruptedOutcomes = producer.Events.Outcomes;
        int uninterruptedGames = producer.Events.Games;
        if (snapshot.phase == GmParlorMatchPhase.AwaitingAldricJudgement)
            Assert.That(uninterruptedCues, Is.EqualTo(1));
        Assert.That(GmSaveSystem.Flush(), Is.True);
        Assert.That(File.Exists(savePath), Is.True);
        Object.DestroyImmediate(producer.Root);
        GmRunStore.BeginNewRun();
        Assert.That(GmSaveSystem.Load(), Is.True);
        Assert.That(GmRunStore.HasParlorMatch, Is.True,
            "real disk load must repopulate the canonical match before scene recreation");

        RuntimeGraph restored = BuildGraphCore();
        var events = new RuntimeEventCounts(restored);
        Assert.That(restored.Rules.InitializeOrRestore(),
            Is.EqualTo(GmParlorInitializeResult.Restored));
        FinishGraph(restored);
        try
        {
            Assert.That(restored.Rules.LastInitializeResult,
                Is.EqualTo(GmParlorInitializeResult.Restored));
            Assert.That(restored.Controller.IsActivated, Is.False);
            Assert.That(restored.Controller.CanAcceptInput, Is.False);
            Assert.That(restored.Coordinator.IsBlocking, Is.False);
            Assert.That(restored.Coordinator.HasActiveCaption, Is.False,
                "Awake must clear transient captions");
            Assert.That(restored.Focus.IsOpen, Is.False);
            restored.Focus.Open();
            Assert.That(restored.Focus.IsOpen, Is.False,
                "focus is an input surface and cannot open before activation");
            Assert.That(restored.Controller.FocusedCardIndex, Is.Zero);
            restored.Focus.Move(1);
            restored.Focus.Select(2);
            Assert.That(restored.Controller.FocusedCardIndex, Is.Zero,
                "focus navigation cannot mutate pre-activation state");
            AssertBindings(expectedBindings, restored.Binder);
            AssertEvidence(expectedEvidence, restored.Evidence);

            int tableGamesBefore = GmRunStore.TableGameIndex;
            Assert.That(restored.Controller.Activate(), Is.EqualTo(GmParlorActionError.None));
            Assert.That(restored.Controller.IsActivated, Is.True);
            Assert.That(restored.Controller.CanAcceptInput, Is.True);
            Assert.That(restored.Controller.FocusedCardIndex, Is.Zero);
            Assert.That(restored.Focus.IsOpen, Is.False);
            Assert.That(restored.Coordinator.IsBlocking, Is.False);
            AssertBindings(expectedBindings, restored.Binder);
            AssertEvidence(expectedEvidence, restored.Evidence);
            Assert.That(events.Cues, Is.Zero);
            Assert.That(events.Outcomes, Is.Zero);
            Assert.That(events.Games, Is.Zero);
            Assert.That(events.Cues, Is.LessThanOrEqualTo(uninterruptedCues));
            Assert.That(events.Outcomes, Is.LessThanOrEqualTo(uninterruptedOutcomes));
            Assert.That(events.Games, Is.LessThanOrEqualTo(uninterruptedGames));
            if (snapshot.phase == GmParlorMatchPhase.AwaitingAldricJudgement)
            {
                Assert.That(restored.Presenter.LastCue.Observation,
                    Is.EqualTo(snapshot.tellObservation));
                Assert.That(restored.Presenter.LastCue.CommandId,
                    Is.EqualTo(GmParlorPresentationJournal.RestoreOpenJudgement(snapshot).Id));
            }
            if (snapshot.phase == GmParlorMatchPhase.MatchResult)
                Assert.That(GmRunStore.TableGameIndex, Is.EqualTo(Math.Max(1, tableGamesBefore)));
            else
                Assert.That(GmRunStore.TableGameIndex, Is.EqualTo(tableGamesBefore));

            Assert.That(restored.Controller.Activate(), Is.EqualTo(GmParlorActionError.None));
            Assert.That(events.Cues, Is.Zero);
            Assert.That(events.Outcomes, Is.Zero);
            Assert.That(events.Games, Is.Zero);
            AssertEvidence(expectedEvidence, restored.Evidence);
        }
        finally { Object.DestroyImmediate(restored.Root); }
    }

    [Test]
    public void RestoredAwaitingCaptionIsSilentReadableAndExpiresWithoutChangingEvidence()
    {
        GmAccessibilitySettings.SetCaptions(true);
        RuntimeGraph producer = ProduceGraphFixture("AwaitingCheatJudgement");
        GmParlorPresentationState expectedEvidence = producer.Evidence.ExportState();
        Assert.That(expectedEvidence.observedFacts, Is.Not.Empty,
            "the producer graph must observe its own live tell before checkpointing");
        Assert.That(GmSaveSystem.Flush(), Is.True);
        Object.DestroyImmediate(producer.Root);
        GmRunStore.BeginNewRun();
        Assert.That(GmSaveSystem.Load(), Is.True);
        GmAccessibilitySettings.SetCaptions(true);

        RuntimeGraph restored = BuildGraphCore();
        var events = new RuntimeEventCounts(restored);
        Assert.That(restored.Rules.InitializeOrRestore(),
            Is.EqualTo(GmParlorInitializeResult.Restored));
        FinishGraph(restored);
        VisualElement hud = restored.Hud.BuildForTests();
        Label caption = hud.Q<Label>("ParlorCaptionSurface");
        try
        {
            int evidenceAfterConfigure = events.EvidenceChanges;
            Assert.That(restored.Controller.CanAcceptInput, Is.False);
            Assert.That(restored.Coordinator.HasActiveCaption, Is.False);
            Assert.That(caption.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(events.Cues, Is.Zero);
            Assert.That(events.CaptionEffects, Is.Zero);
            Assert.That(events.CaptionReconstructions, Is.Zero);

            Assert.That(restored.Controller.Activate(), Is.EqualTo(GmParlorActionError.None));
            Assert.That(restored.Controller.CanAcceptInput, Is.True,
                "a silent restored caption cannot block the current decision");
            Assert.That(events.Cues, Is.Zero);
            Assert.That(events.CaptionEffects, Is.Zero,
                "restored state is not a replayed caption effect");
            Assert.That(events.CaptionReconstructions, Is.EqualTo(1));
            Assert.That(events.EvidenceChanges, Is.EqualTo(evidenceAfterConfigure));
            Assert.That(restored.Coordinator.HasActiveCaption, Is.True);
            Assert.That(caption.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(caption.text, Is.Not.Empty);

            float readableSeconds = restored.Presenter.LastCue.MinimumReadableSeconds;
            restored.Coordinator.Advance(Mathf.Max(0f, readableSeconds - 0.01f));
            Assert.That(restored.Coordinator.HasActiveCaption, Is.True);
            restored.Coordinator.Advance(0.02f);
            Assert.That(restored.Coordinator.HasActiveCaption, Is.False);
            Assert.That(caption.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(events.CaptionEffects, Is.Zero);
            Assert.That(events.CaptionReconstructions, Is.EqualTo(2));
            Assert.That(events.EvidenceChanges, Is.EqualTo(evidenceAfterConfigure));
            AssertEvidence(expectedEvidence, restored.Evidence);
        }
        finally { Object.DestroyImmediate(restored.Root); }
    }

    [Test]
    public void DurablePendingCaughtOutcomeWaitsForActivationAndAppliesOnceAcrossTwoReloads()
    {
        var backend = new GraphFailureBackend();
        GmSaveSystem.ConfigureForTests(savePath, backend);
        GmRunStore.BeginNewRun();
        JudgementFixture fixture = FindJudgementFixture(cheated: true);
        RuntimeGraph producer = StartProducer(fixture.Seed, 4, read: true);
        SelectCard(producer, fixture.CardIndex);
        RuntimeEventCounts producerEvents = producer.Events;
        int catchesBefore = GmRunStore.CheatsCaughtCount;
        int corruptionBefore = GmRunStore.CorruptionTier;
        float sanityBefore = GmRunStore.Sanity;
        int defianceBefore = GmRunStore.Defiance;
        int complianceBefore = GmRunStore.Compliance;
        GmParlorPresentationState expectedEvidence = producer.Evidence.ExportState();
        int writesBeforeFailure = backend.WriteCount;
        backend.FailAtWrite = writesBeforeFailure + 2;
        LogAssert.Expect(LogType.Error,
            new System.Text.RegularExpressions.Regex("injected graph acknowledgement failure"));
        Assert.That(producer.Controller.CallRead(),
            Is.EqualTo(GmParlorActionError.PersistenceFailed));
        Assert.That(producer.Rules.Match.TryPeekOutcome(out GmParlorOutcome outcome,
            out ulong sequence), Is.True);
        Assert.That(outcome.Kind, Is.EqualTo(GmParlorOutcomeKind.CheatCaught));
        Assert.That(outcome.CatchDelta, Is.GreaterThan(0));
        Assert.That(producer.Rules.Match.HighestDurableOutcomeSequence,
            Is.EqualTo(sequence));
        Assert.That(producer.Rules.Match.HighestAcknowledgedOutcomeSequence,
            Is.LessThan(sequence));
        Assert.That(producerEvents.Outcomes, Is.Zero);
        Assert.That(producerEvents.Tricks, Is.Zero);
        Assert.That(producerEvents.Rounds, Is.Zero);
        Assert.That(producerEvents.Games, Is.Zero);
        Object.DestroyImmediate(producer.Root);

        backend.FailAtWrite = -1;
        GmRunStore.BeginNewRun();
        LogAssert.Expect(LogType.Error,
            new System.Text.RegularExpressions.Regex("injected graph acknowledgement failure"));
        Assert.That(GmSaveSystem.Load(), Is.True,
            "the previous atomic file must contain the durable-pending checkpoint");
        Assert.That(GmRunStore.CheatsCaughtCount, Is.EqualTo(catchesBefore));
        Assert.That(GmRunStore.ParlorAppliedOutcomeSequence, Is.LessThan(sequence));

        RuntimeGraph restored = BuildGraphCore();
        var restoredEvents = new RuntimeEventCounts(restored);
        Assert.That(restored.Rules.InitializeOrRestore(),
            Is.EqualTo(GmParlorInitializeResult.Restored));
        FinishGraph(restored);
        int evidenceAfterConfigure = restoredEvents.EvidenceChanges;
        Assert.That(restored.Controller.IsActivated, Is.False);
        Assert.That(restored.Controller.CanAcceptInput, Is.False);
        Assert.That(restoredEvents.Outcomes, Is.Zero);
        Assert.That(restoredEvents.Cues, Is.Zero);
        Assert.That(restoredEvents.CaptionEffects, Is.Zero);
        AssertEvidence(expectedEvidence, restored.Evidence);

        Assert.That(restored.Controller.Activate(), Is.EqualTo(GmParlorActionError.None));
        Assert.That(restoredEvents.Outcomes, Is.EqualTo(1));
        Assert.That(restoredEvents.Tricks, Is.Zero,
            "the failed original boundary never emitted and restore delivers only the outcome");
        Assert.That(restoredEvents.Rounds, Is.Zero);
        Assert.That(restoredEvents.Games, Is.Zero);
        Assert.That(restoredEvents.Cues, Is.Zero);
        Assert.That(restoredEvents.CaptionEffects, Is.Zero);
        Assert.That(restoredEvents.EvidenceChanges, Is.EqualTo(evidenceAfterConfigure));
        Assert.That(GmRunStore.CheatsCaughtCount,
            Is.EqualTo(catchesBefore + outcome.CatchDelta));
        Assert.That(GmRunStore.CorruptionTier,
            Is.EqualTo(Mathf.Clamp(corruptionBefore + outcome.CorruptionDelta,
                GmRunStore.MinCorruptionTier, GmRunStore.MaxCorruptionTier)));
        Assert.That(GmRunStore.Sanity,
            Is.EqualTo(Mathf.Clamp01(sanityBefore + outcome.SanityDelta / 100f))
                .Within(0.0001f));
        Assert.That(GmRunStore.Defiance,
            Is.EqualTo(Mathf.Max(0, defianceBefore + outcome.DefianceDelta)));
        Assert.That(GmRunStore.Compliance,
            Is.EqualTo(Mathf.Max(0, complianceBefore + outcome.ComplianceDelta)));
        Assert.That(GmRunStore.ParlorAppliedOutcomeSequence, Is.EqualTo(sequence));
        Assert.That(restored.Rules.Match.HighestAcknowledgedOutcomeSequence,
            Is.EqualTo(sequence));
        Assert.That(restored.Controller.Activate(), Is.EqualTo(GmParlorActionError.None));
        Assert.That(restoredEvents.Outcomes, Is.EqualTo(1));
        AssertEvidence(expectedEvidence, restored.Evidence);
        int catchesAfterAck = GmRunStore.CheatsCaughtCount;
        int defianceAfterAck = GmRunStore.Defiance;
        Object.DestroyImmediate(restored.Root);

        GmRunStore.BeginNewRun();
        Assert.That(GmSaveSystem.Load(), Is.True);
        RuntimeGraph secondRestore = BuildGraphCore();
        var secondEvents = new RuntimeEventCounts(secondRestore);
        Assert.That(secondRestore.Rules.InitializeOrRestore(),
            Is.EqualTo(GmParlorInitializeResult.Restored));
        FinishGraph(secondRestore);
        try
        {
            Assert.That(secondRestore.Controller.Activate(),
                Is.EqualTo(GmParlorActionError.None));
            Assert.That(secondEvents.Outcomes, Is.Zero);
            Assert.That(secondEvents.Tricks, Is.Zero);
            Assert.That(secondEvents.Rounds, Is.Zero);
            Assert.That(secondEvents.Games, Is.Zero);
            Assert.That(secondEvents.Cues, Is.Zero);
            Assert.That(secondEvents.CaptionEffects, Is.Zero);
            Assert.That(GmRunStore.CheatsCaughtCount, Is.EqualTo(catchesAfterAck));
            Assert.That(GmRunStore.Defiance, Is.EqualTo(defianceAfterAck));
            Assert.That(GmRunStore.ParlorAppliedOutcomeSequence, Is.EqualTo(sequence));
            AssertEvidence(expectedEvidence, secondRestore.Evidence);
        }
        finally { Object.DestroyImmediate(secondRestore.Root); }
    }

    [Test]
    public void MatchCompletionIsNotReappliedAcrossTwoFullGraphReloads()
    {
        RuntimeGraph producer = ProduceGraphFixture("MatchResult");
        Assert.That(GmRunStore.TableGameIndex, Is.EqualTo(1));
        Assert.That(GmSaveSystem.Flush(), Is.True);
        Object.DestroyImmediate(producer.Root);

        for (int reload = 0; reload < 2; reload++)
        {
            GmRunStore.BeginNewRun();
            Assert.That(GmSaveSystem.Load(), Is.True);
            RuntimeGraph restored = BuildGraphCore();
            var events = new RuntimeEventCounts(restored);
            Assert.That(restored.Rules.InitializeOrRestore(),
                Is.EqualTo(GmParlorInitializeResult.Restored));
            FinishGraph(restored);
            Assert.That(restored.Controller.IsActivated, Is.False);
            Assert.That(events.Games, Is.Zero);
            Assert.That(restored.Controller.Activate(), Is.EqualTo(GmParlorActionError.None));
            Assert.That(events.Games, Is.Zero);
            Assert.That(events.Outcomes, Is.Zero);
            Assert.That(GmRunStore.TableGameIndex, Is.EqualTo(1));
            Object.DestroyImmediate(restored.Root);
        }
    }

    static GameObject BuildPresentationRig(out GmParlorPropBinder binder,
        out GmParlorPresentationCoordinator coordinator, out GmParlorAldricPresenter presenter,
        out GmParlorEvidenceLog evidence)
    {
        var root = new GameObject("ParlorRestoreRig");
        foreach (GmSuit suit in new[] { GmSuit.Flames, GmSuit.Eyes, GmSuit.Teeth, GmSuit.Bones })
        {
            for (int rank = 1; rank <= 7; rank++)
            {
                var card = new GameObject($"{suit}_{rank}");
                card.transform.SetParent(root.transform, false);
                card.AddComponent<GmParlorCardView>().Configure(new GmCard(suit, rank));
            }
        }
        binder = root.AddComponent<GmParlorPropBinder>();
        Assert.That(binder.TryConfigure(root.GetComponentsInChildren<GmParlorCardView>(true),
            out string bindError), Is.True, bindError);
        evidence = root.AddComponent<GmParlorEvidenceLog>();
        var hand = new GameObject("RightHandCue");
        hand.transform.SetParent(root.transform, false);
        var contact = new GameObject("CardContactCue");
        contact.transform.SetParent(root.transform, false);
        var sleeve = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sleeve.name = "SleeveCue";
        sleeve.transform.SetParent(root.transform, false);
        presenter = root.AddComponent<GmParlorAldricPresenter>();
        Assert.That(presenter.TryConfigure(hand.transform, sleeve.GetComponent<Renderer>(),
            contact.transform, evidence, out string presenterError), Is.True, presenterError);
        coordinator = root.AddComponent<GmParlorPresentationCoordinator>();
        Assert.That(coordinator.TryConfigure(binder, presenter,
            GmParlorAccessibilityProfile.FromGlobal(), out string coordinatorError),
            Is.True, coordinatorError);
        return root;
    }

    static GameObject BuildFullRuntime(out GmParlorRules rules,
        out GmParlorPropBinder binder, out GmParlorPresentationCoordinator coordinator,
        out GmParlorAldricPresenter presenter, out GmParlorEvidenceLog evidence,
        out GmParlorController controller, out GmParlorFocusView focus)
    {
        GameObject root = BuildPresentationRig(out binder, out coordinator, out presenter,
            out evidence);
        rules = root.AddComponent<GmParlorRules>();
        if (rules.Match == null)
            Assert.That(rules.InitializeOrRestore(), Is.EqualTo(GmParlorInitializeResult.Restored));
        controller = root.AddComponent<GmParlorController>();
        Assert.That(controller.TryConfigure(rules, binder, coordinator,
            out string controllerError), Is.True, controllerError);
        focus = root.AddComponent<GmParlorFocusView>();
        Assert.That(focus.TryConfigure(rules, controller, evidence, out string focusError),
            Is.True, focusError);
        var hud = root.AddComponent<GmParlorHud>();
        Assert.That(hud.TryConfigure(focus, coordinator, out string hudError), Is.True, hudError);
        return root;
    }

    static RuntimeGraph BuildGraphCore()
    {
        GameObject root = BuildPresentationRig(out GmParlorPropBinder binder,
            out GmParlorPresentationCoordinator coordinator,
            out GmParlorAldricPresenter presenter, out GmParlorEvidenceLog evidence);
        return new RuntimeGraph
        {
            Root = root,
            Rules = root.AddComponent<GmParlorRules>(),
            Binder = binder,
            Coordinator = coordinator,
            Presenter = presenter,
            Evidence = evidence,
        };
    }

    static void FinishGraph(RuntimeGraph graph)
    {
        graph.Controller = graph.Root.AddComponent<GmParlorController>();
        Assert.That(graph.Controller.TryConfigure(graph.Rules, graph.Binder,
            graph.Coordinator, out string controllerError), Is.True, controllerError);
        graph.Focus = graph.Root.AddComponent<GmParlorFocusView>();
        Assert.That(graph.Focus.TryConfigure(graph.Rules, graph.Controller, graph.Evidence,
            out string focusError), Is.True, focusError);
        graph.Hud = graph.Root.AddComponent<GmParlorHud>();
        Assert.That(graph.Hud.TryConfigure(graph.Focus, graph.Coordinator,
            out string hudError), Is.True, hudError);
    }

    static RuntimeGraph StartProducer(int seed, int tier, bool read)
    {
        RuntimeGraph graph = BuildGraphCore();
        Assert.That(graph.Rules.StartGame(seed, tier, 3, read, forceRestart: true),
            Is.EqualTo(GmParlorInitializeResult.StartedNew));
        FinishGraph(graph);
        graph.Events = new RuntimeEventCounts(graph);
        Assert.That(graph.Controller.Activate(), Is.EqualTo(GmParlorActionError.None));
        return graph;
    }

    static RuntimeGraph ProduceGraphFixture(string fixtureName)
    {
        RuntimeGraph graph;
        switch (fixtureName)
        {
            case "AwaitingHonestJudgement":
            case "AwaitingCheatJudgement":
            case "TrickResult":
                JudgementFixture judgement = FindJudgementFixture(
                    fixtureName != "AwaitingHonestJudgement");
                graph = StartProducer(judgement.Seed, 4, read: true);
                SelectCard(graph, judgement.CardIndex);
                if (fixtureName == "TrickResult")
                {
                    Assert.That(graph.Controller.ConfirmFocusedAction(),
                        Is.EqualTo(GmParlorActionError.None));
                    DrainPresentation(graph);
                }
                return graph;
            case "DeferredReadMatchResult":
            case "DeferredReadRematch":
                DeferredFixture deferred = FindDeferredFixture();
                graph = StartProducer(deferred.Seed, deferred.Tier, read: false);
                DriveUntil(graph, GmParlorMatchPhase.MatchResult);
                Assert.That(graph.Rules.Match.ReadTestDeferred, Is.True);
                if (fixtureName == "DeferredReadRematch")
                {
                    Assert.That(graph.Controller.ConfirmFocusedAction(),
                        Is.EqualTo(GmParlorActionError.None));
                    DrainPresentation(graph);
                }
                return graph;
            default:
                GmParlorMatchPhase target = (GmParlorMatchPhase)Enum.Parse(
                    typeof(GmParlorMatchPhase), fixtureName);
                int seed = target == GmParlorMatchPhase.PlayerLeads
                    ? 117 : FindSeedForPhase(target);
                graph = StartProducer(seed, 4, read: true);
                DriveUntil(graph, target);
                return graph;
        }
    }

    static void SelectCard(RuntimeGraph graph, int cardIndex)
    {
        graph.Controller.SetFocusedCardIndex(cardIndex);
        Assert.That(graph.Controller.ConfirmFocusedAction(),
            Is.EqualTo(GmParlorActionError.None));
        DrainPresentation(graph);
    }

    static void DriveUntil(RuntimeGraph graph, GmParlorMatchPhase target)
    {
        int guard = 400;
        while (graph.Rules.Match.Phase != target && guard-- > 0)
        {
            switch (graph.Rules.Match.Phase)
            {
                case GmParlorMatchPhase.PlayerLeads:
                case GmParlorMatchPhase.PlayerFollowsAldricLead:
                    int legal = Enumerable.Range(0, graph.Rules.PlayerHand.Count)
                        .First(index => graph.Rules.GetPlayerCardError(index) ==
                            GmParlorActionError.None);
                    graph.Controller.SetFocusedCardIndex(legal);
                    Assert.That(graph.Controller.ConfirmFocusedAction(),
                        Is.EqualTo(GmParlorActionError.None));
                    break;
                case GmParlorMatchPhase.AwaitingAldricJudgement:
                case GmParlorMatchPhase.TrickResult:
                case GmParlorMatchPhase.RoundResult:
                    Assert.That(graph.Controller.ConfirmFocusedAction(),
                        Is.EqualTo(GmParlorActionError.None));
                    break;
                default:
                    throw new AssertionException("Cannot graph-drive phase " +
                        graph.Rules.Match.Phase);
            }
            DrainPresentation(graph);
        }
        Assert.That(graph.Rules.Match.Phase, Is.EqualTo(target));
    }

    static void DrainPresentation(RuntimeGraph graph)
    {
        int guard = 100;
        while (graph.Coordinator.IsBlocking && guard-- > 0)
            graph.Coordinator.Advance(60f);
        Assert.That(graph.Coordinator.IsBlocking, Is.False);
    }

    static int FindSeedForPhase(GmParlorMatchPhase phase)
    {
        for (int seed = 1; seed <= 300; seed++)
        {
            GmParlorMatch match = Started(seed, true);
            int guard = 300;
            while (guard-- > 0)
            {
                if (match.Phase == phase) return seed;
                if (match.Phase == GmParlorMatchPhase.MatchResult) break;
                Step(match);
            }
        }
        throw new AssertionException("No seed found for phase " + phase);
    }

    static JudgementFixture FindJudgementFixture(bool cheated)
    {
        for (int seed = 1; seed < 10000; seed++)
        {
            GmParlorMatch match = Started(seed, true);
            for (int index = 0; index < match.PlayerHand.Count; index++)
            {
                Assert.That(GmParlorMatch.TryRestore(match.ExportSnapshot(),
                    out GmParlorMatch candidate, out string error), Is.True, error);
                if (candidate.PlayPlayerCard(index) == GmParlorActionError.None &&
                    candidate.AldricCheated == cheated)
                    return new JudgementFixture(seed, index);
            }
        }
        throw new AssertionException("No graph judgement fixture found");
    }

    static DeferredFixture FindDeferredFixture()
    {
        for (int tier = 1; tier <= 4; tier++)
        for (int seed = 1; seed <= 300; seed++)
        {
            GmParlorMatch match = Started(seed, false, tier);
            int guard = 300;
            while (match.Phase != GmParlorMatchPhase.MatchResult && guard-- > 0)
                Step(match);
            if (match.Phase == GmParlorMatchPhase.MatchResult && match.ReadTestDeferred)
                return new DeferredFixture(seed, tier);
        }
        throw new AssertionException("No deferred graph fixture found");
    }

    sealed class RuntimeGraph
    {
        public GameObject Root;
        public GmParlorRules Rules;
        public GmParlorPropBinder Binder;
        public GmParlorPresentationCoordinator Coordinator;
        public GmParlorAldricPresenter Presenter;
        public GmParlorEvidenceLog Evidence;
        public GmParlorController Controller;
        public GmParlorFocusView Focus;
        public GmParlorHud Hud;
        public RuntimeEventCounts Events;
    }

    sealed class RuntimeEventCounts
    {
        public int Outcomes;
        public int Tricks;
        public int Rounds;
        public int Games;
        public int Cues;
        public int CaptionEffects;
        public int CaptionReconstructions;
        public int EvidenceChanges;

        public RuntimeEventCounts(RuntimeGraph graph)
        {
            graph.Rules.OnOutcomeReady += (_, __) => Outcomes++;
            graph.Rules.OnTrickCompleted += _ => Tricks++;
            graph.Rules.OnRoundCompleted += _ => Rounds++;
            graph.Rules.OnGameCompleted += _ => Games++;
            graph.Presenter.OnCuePresented += _ => Cues++;
            graph.Coordinator.OnActiveCaptionChanged += () => CaptionEffects++;
            graph.Coordinator.OnCaptionStateReconstructed += () => CaptionReconstructions++;
            graph.Evidence.OnChanged += () => EvidenceChanges++;
        }
    }

    readonly struct JudgementFixture
    {
        public readonly int Seed;
        public readonly int CardIndex;

        public JudgementFixture(int seed, int cardIndex)
        {
            Seed = seed;
            CardIndex = cardIndex;
        }
    }

    readonly struct DeferredFixture
    {
        public readonly int Seed;
        public readonly int Tier;

        public DeferredFixture(int seed, int tier)
        {
            Seed = seed;
            Tier = tier;
        }
    }

    static void AssertBindings(GmParlorCardBinding[] expected, GmParlorPropBinder binder)
    {
        GmParlorCardView[] actual = binder.GetComponentsInChildren<GmParlorCardView>(true);
        Assert.That(actual, Has.Length.EqualTo(28));
        foreach (GmParlorCardBinding binding in expected)
            Assert.That(actual.Single(view => view.PhysicalCard == binding.PhysicalCard).Binding,
                    Is.EqualTo(binding), binding.PhysicalCard.ToString());
    }

    static GmParlorCardBinding[] CaptureBindings(GmParlorPropBinder binder)
    {
        GmParlorCardView[] views = binder.GetComponentsInChildren<GmParlorCardView>(true);
        Assert.That(views, Has.Length.EqualTo(28));
        var bindings = new GmParlorCardBinding[views.Length];
        for (int index = 0; index < views.Length; index++) bindings[index] = views[index].Binding;
        return bindings;
    }

    static void AssertEvidence(GmParlorPresentationState expected, GmParlorEvidenceLog actual)
    {
        Assert.That(actual.Facts, Has.Count.EqualTo(expected.observedFacts.Count));
        for (int index = 0; index < expected.observedFacts.Count; index++)
        {
            Assert.That(actual.Facts[index].CommandId,
                Is.EqualTo(expected.observedFacts[index].commandId));
            Assert.That(actual.Facts[index].Fact,
                Is.EqualTo(expected.observedFacts[index].fact));
            StringAssert.DoesNotContain("cheat", actual.Facts[index].Text.ToLowerInvariant());
        }
    }

    static GmParlorPresentationState EvidenceFor(GmParlorMatchSnapshot snapshot)
    {
        var state = GmParlorPresentationState.Empty();
        SetPresentationIdentity(state, snapshot.seed, GmRunStore.ParlorOutcomeNamespace);
        if (snapshot.phase == GmParlorMatchPhase.AwaitingAldricJudgement)
        {
            ulong commandId = GmParlorPresentationJournal.RestoreOpenJudgement(snapshot).Id;
            if (snapshot.tellObservation == GmTellObservation.Suspicious)
            {
                state.observedFacts.Add(new GmParlorObservedFactData(commandId,
                    GmParlorObservedFact.RightHandPausedAboveDeck));
                state.observedFacts.Add(new GmParlorObservedFactData(commandId,
                    GmParlorObservedFact.CardContactBroke));
            }
            else
            {
                state.observedFacts.Add(new GmParlorObservedFactData(commandId,
                    GmParlorObservedFact.CardPlacedWithoutPause));
            }
        }
        else
        {
            state.observedFacts.Add(new GmParlorObservedFactData(991,
                GmParlorObservedFact.SleeveBrushedTable));
        }
        return state;
    }

    static GmParlorPresentationState BoundCalmState(int seed, ulong outcomeNamespace,
        ulong commandId)
    {
        GmParlorPresentationState state = GmParlorPresentationState.Empty();
        SetPresentationIdentity(state, seed, outcomeNamespace);
        state.observedFacts.Add(new GmParlorObservedFactData(commandId,
            GmParlorObservedFact.CardPlacedWithoutPause));
        return state;
    }

    static GmParlorPresentationState LegacyState(
        params GmParlorObservedFactData[] facts)
    {
        GmParlorPresentationState state = GmParlorPresentationState.Empty();
        state.version = 1;
        state.observedFacts.AddRange(facts);
        return state;
    }

    static void SetPresentationIdentity(GmParlorPresentationState state, int seed,
        ulong outcomeNamespace)
    {
        var seedField = typeof(GmParlorPresentationState).GetField("matchSeed");
        var namespaceField = typeof(GmParlorPresentationState).GetField("matchOutcomeNamespace");
        var presentField = typeof(GmParlorPresentationState).GetField("hasMatchIdentity");
        Assert.That(seedField, Is.Not.Null,
            "presentation DTO has no semantic match seed identity");
        Assert.That(namespaceField, Is.Not.Null,
            "presentation DTO has no Parlor outcome namespace identity");
        seedField.SetValue(state, seed);
        namespaceField.SetValue(state, outcomeNamespace);
        presentField?.SetValue(state, true);
    }

    static GmParlorMatch Started(int seed, bool read, int tier = 4)
    {
        var match = new GmParlorMatch(seed, tier, 3, read);
        Assert.That(match.Start(), Is.EqualTo(GmParlorActionError.None));
        return match;
    }

    static void Step(GmParlorMatch match)
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
                throw new AssertionException("Cannot step phase " + match.Phase);
        }
        Acknowledge(match);
    }

    static void Acknowledge(GmParlorMatch match)
    {
        if (!match.TryPeekOutcome(out _, out ulong sequence)) return;
        Assert.That(match.MarkOutcomeDurable(sequence), Is.True);
        Assert.That(match.AcknowledgeOutcome(sequence), Is.True);
    }

    static GmParlorMatch FindAwaitingJudgement(bool cheated)
    {
        for (int seed = 1; seed < 10000; seed++)
        {
            var match = new GmParlorMatch(seed, 4, 3, true);
            match.Start();
            for (int index = 0; index < match.PlayerHand.Count; index++)
            {
                GmParlorMatch candidate;
                Assert.That(GmParlorMatch.TryRestore(match.ExportSnapshot(), out candidate,
                    out string error), Is.True, error);
                if (candidate.PlayPlayerCard(index) == GmParlorActionError.None &&
                    candidate.AldricCheated == cheated)
                    return candidate;
            }
        }
        throw new AssertionException("No judgement fixture found");
    }

    sealed class CountingBackend : IGmAtomicSaveBackend
    {
        public bool Fail;
        public int WriteCount;

        public void WriteAtomic(string target, string json)
        {
            WriteCount++;
            if (Fail) throw new IOException("injected presentation write failure");
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllText(target, json);
        }
    }

    sealed class GraphFailureBackend : IGmAtomicSaveBackend
    {
        public int FailAtWrite = -1;
        public int WriteCount;

        public void WriteAtomic(string target, string json)
        {
            WriteCount++;
            if (WriteCount == FailAtWrite)
                throw new IOException("injected graph acknowledgement failure");
            string saveDirectory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(saveDirectory)) Directory.CreateDirectory(saveDirectory);
            string temporary = target + ".tmp";
            File.WriteAllText(temporary, json);
            if (File.Exists(target)) File.Replace(temporary, target, null);
            else File.Move(temporary, target);
        }
    }
}
