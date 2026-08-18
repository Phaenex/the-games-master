using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class GmParlorPresentationCoordinatorTests
{
    GameObject root;
    GmParlorPropBinder binder;
    GmParlorCardView[] views;

    [SetUp]
    public void SetUp()
    {
        ResetAccessibility();
        root = new GameObject("ParlorPresentationTest");
        views = CreateViews(root.transform);
        binder = root.AddComponent<GmParlorPropBinder>();
        Assert.That(binder.TryConfigure(views, out string error), Is.True, error);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        ResetAccessibility();
    }

    [Test]
    public void CardMotionTraversesFivePhasesAndSettlesAtCanonicalPose()
    {
        GmParlorMatch match = Started(117);
        GmParlorMatchSnapshot before = match.ExportSnapshot();
        Assert.That(binder.TryApply(before, out string bindError), Is.True, bindError);
        GmCard played = match.PlayerHand[0];
        Assert.That(match.PlayPlayerCard(0), Is.EqualTo(GmParlorActionError.None));
        GmParlorMatchSnapshot after = match.ExportSnapshot();
        GmParlorCardBinding target = GmParlorTableLayout.Build(after)
            .Single(binding => binding.PhysicalCard == played);
        var motion = new GmParlorCardMotion();
        GmParlorCardView view = views.Single(card => card.PhysicalCard == played);

        motion.Begin(view, target, reducedMotion: false);
        Assert.That(motion.Phase, Is.EqualTo(GmParlorMotionPhase.Approach));
        motion.Advance(GmParlorCardMotion.ApproachSeconds);
        Assert.That(motion.Phase, Is.EqualTo(GmParlorMotionPhase.Contact));
        motion.Advance(GmParlorCardMotion.ContactSeconds);
        Assert.That(motion.Phase, Is.EqualTo(GmParlorMotionPhase.Manipulate));
        motion.Advance(GmParlorCardMotion.ManipulateSeconds);
        Assert.That(motion.Phase, Is.EqualTo(GmParlorMotionPhase.Release));
        motion.Advance(GmParlorCardMotion.ReleaseSeconds);
        Assert.That(motion.Phase, Is.EqualTo(GmParlorMotionPhase.Settle));
        motion.Advance(GmParlorCardMotion.SettleSeconds);

        Assert.That(motion.Phase, Is.EqualTo(GmParlorMotionPhase.Complete));
        Assert.That(view.Binding, Is.EqualTo(target));
        Assert.That(view.transform.localPosition,
            Is.EqualTo(GmParlorTableLayout.LocalPosition(target)));
    }

    [TestCase(GmParlorMotionPhase.Approach)]
    [TestCase(GmParlorMotionPhase.Contact)]
    [TestCase(GmParlorMotionPhase.Manipulate)]
    [TestCase(GmParlorMotionPhase.Release)]
    [TestCase(GmParlorMotionPhase.Settle)]
    public void FastForwardFromEveryPhaseRestoresTheExactCanonicalBinding(GmParlorMotionPhase phase)
    {
        GmParlorMatch match = Started(314);
        Assert.That(binder.TryApply(match.ExportSnapshot(), out string bindError), Is.True, bindError);
        GmCard played = match.PlayerHand[0];
        Assert.That(match.PlayPlayerCard(0), Is.EqualTo(GmParlorActionError.None));
        GmParlorMatchSnapshot after = match.ExportSnapshot();
        GmParlorCardBinding target = GmParlorTableLayout.Build(after)
            .Single(binding => binding.PhysicalCard == played);
        GmParlorCardView view = views.Single(card => card.PhysicalCard == played);
        var motion = new GmParlorCardMotion();
        motion.Begin(view, target, reducedMotion: false);
        AdvanceTo(motion, phase);

        motion.FastForward();

        Assert.That(motion.Phase, Is.EqualTo(GmParlorMotionPhase.Complete));
        Assert.That(view.Binding, Is.EqualTo(target));
        Assert.That(view.transform.localPosition,
            Is.EqualTo(GmParlorTableLayout.LocalPosition(target)));
    }

    [Test]
    public void CoordinatorQueuesSemanticCommandsAndBlocksUntilCanonicalSettle()
    {
        GmParlorMatch match = Started(9001);
        GmParlorMatchSnapshot before = match.ExportSnapshot();
        Assert.That(binder.TryApply(before, out string bindError), Is.True, bindError);
        Assert.That(match.PlayPlayerCard(0), Is.EqualTo(GmParlorActionError.None));
        GmParlorMatchSnapshot after = match.ExportSnapshot();
        var coordinator = root.AddComponent<GmParlorPresentationCoordinator>();
        Assert.That(coordinator.TryConfigure(binder, out string configureError), Is.True, configureError);

        Assert.That(coordinator.TryEnqueue(
            GmParlorPresentationJournal.Build(before, after), after, out string queueError),
            Is.True, queueError);
        Assert.That(coordinator.IsBlocking, Is.True);

        for (int frame = 0; frame < 200 && coordinator.IsBlocking; frame++)
            coordinator.Advance(1f / 60f);

        Assert.That(coordinator.IsBlocking, Is.False);
        Assert.That(coordinator.LastError, Is.Empty);
        foreach (GmParlorCardBinding expected in GmParlorTableLayout.Build(after))
            Assert.That(views.Single(view => view.PhysicalCard == expected.PhysicalCard).Binding,
                Is.EqualTo(expected));
    }

    [Test]
    public void OpenJudgementInvokesObservedPresenterAfterPhysicalCardsSettle()
    {
        GmParlorMatch match = Started(731);
        GmParlorMatchSnapshot before = match.ExportSnapshot();
        Assert.That(binder.TryApply(before, out string bindError), Is.True, bindError);
        Assert.That(match.PlayPlayerCard(0), Is.EqualTo(GmParlorActionError.None));
        GmParlorMatchSnapshot after = match.ExportSnapshot();
        after.tellObservation = GmTellObservation.Suspicious;
        GmParlorAldricPresenter presenter = CreatePresenter(out GmParlorEvidenceLog log);
        var coordinator = root.AddComponent<GmParlorPresentationCoordinator>();
        Assert.That(coordinator.TryConfigure(binder, presenter,
            GmParlorAccessibilityProfile.Default, out string configureError), Is.True,
            configureError);

        Assert.That(coordinator.TryEnqueue(
            GmParlorPresentationJournal.Build(before, after), after, out string queueError),
            Is.True, queueError);
        for (int frame = 0; frame < 240 && coordinator.IsBlocking; frame++)
            coordinator.Advance(1f / 60f);

        Assert.That(coordinator.IsBlocking, Is.False);
        Assert.That(presenter.LastCue.Observation, Is.EqualTo(GmTellObservation.Suspicious));
        Assert.That(log.Facts, Has.Count.EqualTo(2));
        Assert.That(coordinator.LastError, Is.Empty);
    }

    [Test]
    public void GlobalAccessibilityChangesPropagateWithoutReconfiguringOrMutatingMatch()
    {
        GmParlorMatch match = Started(741);
        string canonicalBefore = match.PublicStateBytes;
        var coordinator = root.AddComponent<GmParlorPresentationCoordinator>();
        Assert.That(coordinator.TryConfigure(binder, out string error), Is.True, error);
        Assert.That(coordinator.ReducedMotion, Is.False);

        GmAccessibilitySettings.SetCaptions(true);
        GmAccessibilitySettings.SetReducedMotion(true);
        GmAccessibilitySettings.SetVibration(false);
        GmAccessibilitySettings.SetMonoAudio(true);

        Assert.That(coordinator.ReducedMotion, Is.True,
            "Parlor cached the configure-time profile instead of following global changes");
        Assert.That(coordinator.Accessibility.Captions, Is.True);
        Assert.That(coordinator.Accessibility.ReducedMotion, Is.True);
        Assert.That(coordinator.Accessibility.Vibration, Is.False);
        Assert.That(coordinator.Accessibility.MonoAudio, Is.True);
        Assert.That(match.PublicStateBytes, Is.EqualTo(canonicalBefore));
    }

    [Test]
    public void EnablingReducedMotionSnapsActiveCardAndStartsQueuedMotionReduced()
    {
        GmParlorMatch match = Started(751);
        GmParlorMatchSnapshot before = match.ExportSnapshot();
        Assert.That(binder.TryApply(before, out string bindError), Is.True, bindError);
        Assert.That(match.PlayPlayerCard(0), Is.EqualTo(GmParlorActionError.None));
        string canonicalAfter = match.PublicStateBytes;
        GmParlorMatchSnapshot after = match.ExportSnapshot();
        GmParlorPresentationCommand[] commands = GmParlorPresentationJournal.Build(before, after);
        var coordinator = root.AddComponent<GmParlorPresentationCoordinator>();
        Assert.That(coordinator.TryConfigure(binder, out string configureError), Is.True,
            configureError);
        Assert.That(coordinator.TryEnqueue(commands, after, out string queueError), Is.True,
            queueError);
        coordinator.Advance(0.04f);
        GmParlorPresentationCommand first = commands.First(command =>
            command.Action == GmParlorPresentationAction.PlayerCardToLead);
        GmParlorCardView firstView = views.Single(view => view.PhysicalCard == first.Card.Value);
        GmParlorCardBinding firstTarget = GmParlorTableLayout.Build(after).Single(binding =>
            binding.PhysicalCard == first.Card.Value && binding.Zone == GmParlorCardZone.Lead);
        Assert.That(firstView.Binding, Is.Not.EqualTo(firstTarget));

        GmAccessibilitySettings.SetReducedMotion(true);

        Assert.That(firstView.Binding, Is.EqualTo(firstTarget),
            "the in-flight full-motion card did not finish immediately");
        Assert.That(coordinator.CurrentMotionPhase, Is.EqualTo(GmParlorMotionPhase.Approach),
            "the later queued card did not begin after the snap");
        PropertyInfo approach = typeof(GmParlorPresentationCoordinator).GetProperty(
            "CurrentMotionApproachHeight");
        Assert.That(approach, Is.Not.Null,
            "the coordinator exposes no proof that the later motion used reduced behavior");
        Assert.That((float)approach.GetValue(coordinator), Is.LessThan(0.02f));
        Assert.That(match.PublicStateBytes, Is.EqualTo(canonicalAfter));
    }

    [Test]
    public void EnablingReducedMotionDropsActiveAldricMotionWithoutRepeatingEvidence()
    {
        GmParlorMatch match = Started(761);
        GmParlorMatchSnapshot before = match.ExportSnapshot();
        Assert.That(binder.TryApply(before, out string bindError), Is.True, bindError);
        Assert.That(match.PlayPlayerCard(0), Is.EqualTo(GmParlorActionError.None));
        string canonicalAfter = match.PublicStateBytes;
        GmParlorMatchSnapshot after = match.ExportSnapshot();
        after.tellObservation = GmTellObservation.Suspicious;
        GmParlorAldricPresenter presenter = CreatePresenter(out GmParlorEvidenceLog log);
        Transform hand = presenter.transform.Find("RightHandCue");
        Vector3 neutral = hand.localPosition;
        int cueEvents = 0;
        presenter.OnCuePresented += _ => cueEvents++;
        var coordinator = root.AddComponent<GmParlorPresentationCoordinator>();
        Assert.That(coordinator.TryConfigure(binder, presenter,
            GmParlorAccessibilityProfile.Default, out string configureError), Is.True,
            configureError);
        Assert.That(coordinator.TryEnqueue(GmParlorPresentationJournal.Build(before, after),
            after, out string queueError), Is.True, queueError);
        for (int step = 0; step < 10 && presenter.LastCue.CommandId == 0; step++)
            coordinator.Advance(1f);
        Assert.That(presenter.LastCue.CommandId, Is.Not.Zero);
        Assert.That(hand.localPosition, Is.Not.EqualTo(neutral));
        Assert.That(cueEvents, Is.EqualTo(1));
        Assert.That(log.Facts, Has.Count.EqualTo(2));

        GmAccessibilitySettings.SetReducedMotion(true);

        Assert.That(hand.localPosition, Is.EqualTo(neutral),
            "active Aldric hand motion remained after reduced motion was enabled");
        Assert.That(cueEvents, Is.EqualTo(1), "refresh replayed the semantic cue event");
        Assert.That(log.Facts, Has.Count.EqualTo(2), "refresh duplicated observed evidence");
        CollectionAssert.DoesNotContain(presenter.LastCue.Channels,
            GmParlorEvidenceChannel.HandMotion,
            "public cue metadata still advertises motion after motion was removed");
        Assert.That(match.PublicStateBytes, Is.EqualTo(canonicalAfter));
    }

    [Test]
    public void ActiveCaptionExpiresWithItsCueWhileEvidenceJournalRemains()
    {
        GmAccessibilitySettings.SetCaptions(true);
        GmParlorMatch match = Started(771);
        GmParlorMatchSnapshot before = match.ExportSnapshot();
        Assert.That(binder.TryApply(before, out string bindError), Is.True, bindError);
        Assert.That(match.PlayPlayerCard(0), Is.EqualTo(GmParlorActionError.None));
        GmParlorMatchSnapshot after = match.ExportSnapshot();
        after.tellObservation = GmTellObservation.Suspicious;
        GmParlorAldricPresenter presenter = CreatePresenter(out GmParlorEvidenceLog log);
        var coordinator = root.AddComponent<GmParlorPresentationCoordinator>();
        Assert.That(coordinator.TryConfigure(binder, presenter,
            GmParlorAccessibilityProfile.Default, out string configureError), Is.True,
            configureError);
        Assert.That(coordinator.TryEnqueue(GmParlorPresentationJournal.Build(before, after),
            after, out string queueError), Is.True, queueError);
        for (int step = 0; step < 10 && !Read<bool>(coordinator, "HasActiveCaption"); step++)
            coordinator.Advance(1f);

        Assert.That(Read<bool>(coordinator, "HasActiveCaption"), Is.True);
        Assert.That(Read<ulong>(coordinator, "ActiveCaptionCommandId"), Is.Not.Zero);
        Assert.That(Read<string>(coordinator, "ActiveCaptionText"), Does.Not.Contain("cheat").IgnoreCase);
        int journalCount = log.Facts.Count;

        coordinator.Advance(1f);

        Assert.That(Read<bool>(coordinator, "HasActiveCaption"), Is.False,
            "caption persisted after its readable cue completed");
        Assert.That(log.Facts, Has.Count.EqualTo(journalCount),
            "expiring transient caption deleted persistent observed evidence");
    }

    [Test]
    public void RestoredAwaitingObservationRecomputesCaptionOnEveryAccessibilityEnable()
    {
        GmAccessibilitySettings.SetCaptions(false);
        GmParlorMatch match = Started(781);
        Assert.That(match.PlayPlayerCard(0), Is.EqualTo(GmParlorActionError.None));
        GmParlorMatchSnapshot awaiting = match.ExportSnapshot();
        awaiting.tellObservation = GmTellObservation.Suspicious;
        string canonical = match.PublicStateBytes;
        GmParlorAldricPresenter presenter = CreatePresenter(out GmParlorEvidenceLog log);
        var coordinator = root.AddComponent<GmParlorPresentationCoordinator>();
        Assert.That(coordinator.TryConfigure(binder, presenter,
            GmParlorAccessibilityProfile.Default, out string configureError), Is.True,
            configureError);
        int cues = 0;
        int evidenceChanges = 0;
        int reconstructed = 0;
        presenter.OnCuePresented += _ => cues++;
        log.OnChanged += () => evidenceChanges++;
        coordinator.OnCaptionStateReconstructed += () => reconstructed++;
        Assert.That(coordinator.TryRestoreCanonicalState(awaiting, out string restoreError),
            Is.True, restoreError);
        int evidenceAfterRestore = evidenceChanges;
        Assert.That(coordinator.HasActiveCaption, Is.False);
        Assert.That(coordinator.IsBlocking, Is.False);

        GmAccessibilitySettings.SetCaptions(true);
        Assert.That(coordinator.HasActiveCaption, Is.True);
        Assert.That(coordinator.ActiveCaptionText, Does.Contain("right hand").IgnoreCase);
        Assert.That(coordinator.IsBlocking, Is.False);
        Assert.That(cues, Is.Zero);
        Assert.That(evidenceChanges, Is.EqualTo(evidenceAfterRestore));
        Assert.That(reconstructed, Is.EqualTo(1));
        coordinator.Advance(0.74f);
        Assert.That(coordinator.HasActiveCaption, Is.True,
            "caption lost the fresh 0.75 second readable interval");
        GmAccessibilitySettings.SetCaptions(false);
        Assert.That(coordinator.HasActiveCaption, Is.False);
        GmAccessibilitySettings.SetCaptions(true);
        Assert.That(coordinator.HasActiveCaption, Is.True);
        coordinator.Advance(0.74f);
        Assert.That(coordinator.HasActiveCaption, Is.True,
            "re-enable reused the prior timer instead of granting a fresh interval");
        coordinator.Advance(0.02f);
        Assert.That(coordinator.HasActiveCaption, Is.False);
        Assert.That(cues, Is.Zero);
        Assert.That(evidenceChanges, Is.EqualTo(evidenceAfterRestore));
        Assert.That(match.PublicStateBytes, Is.EqualTo(canonical));
    }

    [Test]
    public void LiveAwaitingObservationCanEnableCaptionAfterTheOriginalCueExpired()
    {
        GmAccessibilitySettings.SetCaptions(false);
        GmParlorMatch match = Started(791);
        GmParlorMatchSnapshot before = match.ExportSnapshot();
        Assert.That(binder.TryApply(before, out string bindError), Is.True, bindError);
        Assert.That(match.PlayPlayerCard(0), Is.EqualTo(GmParlorActionError.None));
        GmParlorMatchSnapshot awaiting = match.ExportSnapshot();
        awaiting.tellObservation = GmTellObservation.Calm;
        GmParlorAldricPresenter presenter = CreatePresenter(out GmParlorEvidenceLog log);
        int cues = 0;
        int changes = 0;
        int captionEffects = 0;
        presenter.OnCuePresented += _ => cues++;
        log.OnChanged += () => changes++;
        var coordinator = root.AddComponent<GmParlorPresentationCoordinator>();
        coordinator.OnActiveCaptionChanged += () => captionEffects++;
        Assert.That(coordinator.TryConfigure(binder, presenter,
            GmParlorAccessibilityProfile.Default, out string configureError), Is.True,
            configureError);
        Assert.That(coordinator.TryEnqueue(GmParlorPresentationJournal.Build(before, awaiting),
            awaiting, out string queueError), Is.True, queueError);
        for (int guard = 0; coordinator.IsBlocking && guard < 20; guard++)
            coordinator.Advance(1f);
        Assert.That(coordinator.IsBlocking, Is.False);
        Assert.That(coordinator.HasActiveCaption, Is.False);
        Assert.That(cues, Is.EqualTo(1));
        Assert.That(captionEffects, Is.Zero,
            "caption-disabled observation emitted a caption effect lifecycle");
        int changesAfterCue = changes;

        GmAccessibilitySettings.SetCaptions(true);
        Assert.That(coordinator.HasActiveCaption, Is.True);
        Assert.That(coordinator.ActiveCaptionText, Does.Contain("without pausing").IgnoreCase);
        coordinator.Advance(0.74f);
        Assert.That(coordinator.HasActiveCaption, Is.True);
        coordinator.Advance(0.02f);
        Assert.That(coordinator.HasActiveCaption, Is.False);
        Assert.That(cues, Is.EqualTo(1));
        Assert.That(changes, Is.EqualTo(changesAfterCue));
    }

    [Test]
    public void DisablingCaptionDuringLiveCuePreservesOriginalSemanticHold()
    {
        GmAccessibilitySettings.SetCaptions(true);
        GmParlorMatch match = Started(797);
        GmParlorMatchSnapshot before = match.ExportSnapshot();
        Assert.That(binder.TryApply(before, out string bindError), Is.True, bindError);
        Assert.That(match.PlayPlayerCard(0), Is.EqualTo(GmParlorActionError.None));
        GmParlorMatchSnapshot awaiting = match.ExportSnapshot();
        GmParlorAldricPresenter presenter = CreatePresenter(out GmParlorEvidenceLog log);
        int cues = 0;
        int evidenceChanges = 0;
        presenter.OnCuePresented += _ => cues++;
        log.OnChanged += () => evidenceChanges++;
        var coordinator = root.AddComponent<GmParlorPresentationCoordinator>();
        Assert.That(coordinator.TryConfigure(binder, presenter,
            GmParlorAccessibilityProfile.Default, out string configureError), Is.True,
            configureError);
        Assert.That(coordinator.TryEnqueue(GmParlorPresentationJournal.Build(before, awaiting),
            awaiting, out string queueError), Is.True, queueError);
        for (int guard = 0; !coordinator.HasActiveCaption && guard < 100; guard++)
            coordinator.Advance(0.05f);
        Assert.That(coordinator.HasActiveCaption, Is.True);
        Assert.That(coordinator.IsBlocking, Is.True);
        Assert.That(cues, Is.EqualTo(1));
        int evidenceAtCue = evidenceChanges;

        coordinator.Advance(0.25f);
        GmAccessibilitySettings.SetCaptions(false);

        Assert.That(coordinator.HasActiveCaption, Is.False);
        Assert.That(coordinator.IsBlocking, Is.True,
            "hiding caption text released the live semantic/input hold");
        Assert.That(cues, Is.EqualTo(1));
        Assert.That(evidenceChanges, Is.EqualTo(evidenceAtCue));
        coordinator.Advance(presenter.LastCue.MinimumReadableSeconds - 0.26f);
        Assert.That(coordinator.IsBlocking, Is.True,
            "live hold expired before its original readable boundary");
        coordinator.Advance(0.02f);
        Assert.That(coordinator.IsBlocking, Is.False);
        Assert.That(cues, Is.EqualTo(1));
        Assert.That(evidenceChanges, Is.EqualTo(evidenceAtCue));
    }

    [Test]
    public void ReentrantCaptionSettingListenerSettlesAtomicallyWithoutReplayOrBlock()
    {
        GmAccessibilitySettings.SetCaptions(false);
        GmParlorMatch match = Started(801);
        Assert.That(match.PlayPlayerCard(0), Is.EqualTo(GmParlorActionError.None));
        GmParlorMatchSnapshot awaiting = match.ExportSnapshot();
        GmParlorAldricPresenter presenter = CreatePresenter(out GmParlorEvidenceLog log);
        var coordinator = root.AddComponent<GmParlorPresentationCoordinator>();
        Assert.That(coordinator.TryConfigure(binder, presenter,
            GmParlorAccessibilityProfile.Default, out string configureError), Is.True,
            configureError);
        Assert.That(coordinator.TryRestoreCanonicalState(awaiting, out string restoreError),
            Is.True, restoreError);
        int cues = 0;
        int changes = 0;
        presenter.OnCuePresented += _ => cues++;
        log.OnChanged += () => changes++;
        coordinator.OnCaptionStateReconstructed += () =>
        {
            if (GmAccessibilitySettings.Captions)
                GmAccessibilitySettings.SetCaptions(false);
        };

        Assert.DoesNotThrow(() => GmAccessibilitySettings.SetCaptions(true));
        Assert.That(GmAccessibilitySettings.Captions, Is.False);
        Assert.That(coordinator.HasActiveCaption, Is.False);
        Assert.That(coordinator.IsBlocking, Is.False);
        Assert.That(cues, Is.Zero);
        Assert.That(changes, Is.Zero);
    }

    [Test]
    public void ReentrantCueListenerCannotInvalidateCoordinatorQueue()
    {
        GmParlorMatch match = Started(811);
        GmParlorMatchSnapshot before = match.ExportSnapshot();
        Assert.That(binder.TryApply(before, out string bindError), Is.True, bindError);
        Assert.That(match.PlayPlayerCard(0), Is.EqualTo(GmParlorActionError.None));
        GmParlorMatchSnapshot awaiting = match.ExportSnapshot();
        GmParlorAldricPresenter presenter = CreatePresenter(out _);
        var coordinator = root.AddComponent<GmParlorPresentationCoordinator>();
        Assert.That(coordinator.TryConfigure(binder, presenter,
            GmParlorAccessibilityProfile.Default, out string configureError), Is.True,
            configureError);
        presenter.OnCuePresented += _ => coordinator.FastForwardToCanonicalState();
        Assert.That(coordinator.TryEnqueue(GmParlorPresentationJournal.Build(before, awaiting),
            awaiting, out string queueError), Is.True, queueError);

        Assert.DoesNotThrow(() =>
        {
            for (int guard = 0; coordinator.IsBlocking && guard < 20; guard++)
                coordinator.Advance(1f);
        });
        Assert.That(coordinator.IsBlocking, Is.False);
        Assert.That(coordinator.LastError, Is.Empty);
        foreach (GmParlorCardBinding expected in GmParlorTableLayout.Build(awaiting))
            Assert.That(views.Single(view => view.PhysicalCard == expected.PhysicalCard).Binding,
                Is.EqualTo(expected));
    }

    [Test]
    public void ThrowingCueListenerIsContainedWithDeterministicPresentationError()
    {
        GmParlorMatch match = Started(821);
        GmParlorMatchSnapshot before = match.ExportSnapshot();
        Assert.That(binder.TryApply(before, out string bindError), Is.True, bindError);
        Assert.That(match.PlayPlayerCard(0), Is.EqualTo(GmParlorActionError.None));
        GmParlorMatchSnapshot awaiting = match.ExportSnapshot();
        GmParlorAldricPresenter presenter = CreatePresenter(out _);
        var coordinator = root.AddComponent<GmParlorPresentationCoordinator>();
        Assert.That(coordinator.TryConfigure(binder, presenter,
            GmParlorAccessibilityProfile.Default, out string configureError), Is.True,
            configureError);
        presenter.OnCuePresented += _ => throw new InvalidOperationException("cue observer exploded");
        Assert.That(coordinator.TryEnqueue(GmParlorPresentationJournal.Build(before, awaiting),
            awaiting, out string queueError), Is.True, queueError);

        Assert.DoesNotThrow(() =>
        {
            for (int guard = 0; coordinator.IsBlocking && guard < 20; guard++)
                coordinator.Advance(1f);
        });
        Assert.That(coordinator.IsBlocking, Is.False);
        Assert.That(coordinator.LastError, Does.Contain("cue observer exploded"));
        foreach (GmParlorCardBinding expected in GmParlorTableLayout.Build(awaiting))
            Assert.That(views.Single(view => view.PhysicalCard == expected.PhysicalCard).Binding,
                Is.EqualTo(expected));
    }

    [Test]
    public void MissingViewFailsClosedSnapsRemainingCardsAndUnblocksInput()
    {
        GmParlorMatch match = Started(117);
        GmParlorMatchSnapshot before = match.ExportSnapshot();
        Assert.That(binder.TryApply(before, out string bindError), Is.True, bindError);
        GmCard played = match.PlayerHand[0];
        Assert.That(match.PlayPlayerCard(0), Is.EqualTo(GmParlorActionError.None));
        GmParlorMatchSnapshot after = match.ExportSnapshot();
        Object.DestroyImmediate(views.Single(view => view.PhysicalCard == played).gameObject);
        var coordinator = root.AddComponent<GmParlorPresentationCoordinator>();
        Assert.That(coordinator.TryConfigure(binder, out string configureError), Is.True, configureError);
        LogAssert.Expect(LogType.Error,
            $"[GmParlorPresentation] Missing physical card view for PlayerCardToLead {played}");

        Assert.That(coordinator.TryEnqueue(
            GmParlorPresentationJournal.Build(before, after), after, out string error), Is.False);
        StringAssert.Contains("missing", error.ToLowerInvariant());
        Assert.That(coordinator.IsBlocking, Is.False);
        Assert.That(coordinator.LastError, Is.Not.Empty);
    }

    [Test]
    public void ReducedMotionPreservesLifecycleAndCutsTotalDuration()
    {
        GmParlorMatch match = Started(71);
        Assert.That(binder.TryApply(match.ExportSnapshot(), out string bindError), Is.True, bindError);
        GmCard played = match.PlayerHand[0];
        Assert.That(match.PlayPlayerCard(0), Is.EqualTo(GmParlorActionError.None));
        GmParlorCardBinding target = GmParlorTableLayout.Build(match.ExportSnapshot())
            .Single(binding => binding.PhysicalCard == played);
        GmParlorCardView view = views.Single(card => card.PhysicalCard == played);
        var motion = new GmParlorCardMotion();

        motion.Begin(view, target, reducedMotion: true);

        Assert.That(motion.TotalDuration, Is.LessThan(GmParlorCardMotion.FullDuration * 0.5f));
        Assert.That(motion.ApproachHeight, Is.LessThan(0.02f));
        motion.Advance(motion.TotalDuration);
        Assert.That(motion.Phase, Is.EqualTo(GmParlorMotionPhase.Complete));
        Assert.That(view.Binding, Is.EqualTo(target));
    }

    [Test]
    public void ActiveMotionAdvanceAllocatesNoManagedMemoryAfterWarmup()
    {
        GmParlorMatch match = Started(117);
        Assert.That(binder.TryApply(match.ExportSnapshot(), out string bindError), Is.True, bindError);
        GmCard played = match.PlayerHand[0];
        Assert.That(match.PlayPlayerCard(0), Is.EqualTo(GmParlorActionError.None));
        GmParlorCardBinding target = GmParlorTableLayout.Build(match.ExportSnapshot())
            .Single(binding => binding.PhysicalCard == played);
        var motion = new GmParlorCardMotion();
        motion.Begin(views.Single(card => card.PhysicalCard == played), target, reducedMotion: false);
        motion.Advance(0.001f);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int frame = 0; frame < 20; frame++) motion.Advance(0.001f);
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.That(after - before, Is.EqualTo(0),
            "steady-state card motion must not allocate between semantic boundaries");
    }

    static void AdvanceTo(GmParlorCardMotion motion, GmParlorMotionPhase target)
    {
        while (motion.Phase < target)
        {
            switch (motion.Phase)
            {
                case GmParlorMotionPhase.Approach:
                    motion.Advance(GmParlorCardMotion.ApproachSeconds);
                    break;
                case GmParlorMotionPhase.Contact:
                    motion.Advance(GmParlorCardMotion.ContactSeconds);
                    break;
                case GmParlorMotionPhase.Manipulate:
                    motion.Advance(GmParlorCardMotion.ManipulateSeconds);
                    break;
                case GmParlorMotionPhase.Release:
                    motion.Advance(GmParlorCardMotion.ReleaseSeconds);
                    break;
            }
        }
    }

    static void ResetAccessibility()
    {
        typeof(GmRunStore).Assembly.GetType("GmAccessibilitySettings")?.GetMethod(
            "ResetToDefaultsForTests", BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Static)?.Invoke(null, null);
    }

    static T Read<T>(object target, string property)
    {
        PropertyInfo info = target.GetType().GetProperty(property,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(info, Is.Not.Null, $"{target.GetType().Name} has no {property}");
        return (T)info.GetValue(target);
    }

    static GmParlorMatch Started(int seed)
    {
        var match = new GmParlorMatch(seed, 2, 0, true);
        Assert.That(match.Start(), Is.EqualTo(GmParlorActionError.None));
        return match;
    }

    static GmParlorCardView[] CreateViews(Transform parent)
    {
        return (from suit in new[] { GmSuit.Flames, GmSuit.Eyes, GmSuit.Teeth, GmSuit.Bones }
                from rank in Enumerable.Range(1, GmParlorCore.RanksPerSuit)
                let go = new GameObject($"{suit}_{rank}")
                let ignored = go.transform.SetParentAndReturn(parent)
                let view = go.AddComponent<GmParlorCardView>()
                select Configure(view, new GmCard(suit, rank))).ToArray();
    }

    static GmParlorCardView Configure(GmParlorCardView view, GmCard card)
    {
        view.Configure(card);
        return view;
    }

    GmParlorAldricPresenter CreatePresenter(out GmParlorEvidenceLog log)
    {
        var presenterRoot = new GameObject("AldricPresenter");
        presenterRoot.transform.SetParent(root.transform, false);
        log = presenterRoot.AddComponent<GmParlorEvidenceLog>();
        var hand = new GameObject("RightHandCue");
        hand.transform.SetParent(presenterRoot.transform, false);
        var contact = new GameObject("CardContactCue");
        contact.transform.SetParent(presenterRoot.transform, false);
        var glove = GameObject.CreatePrimitive(PrimitiveType.Cube);
        glove.name = "GloveCue";
        glove.transform.SetParent(hand.transform, false);
        var sleeve = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sleeve.name = "SleeveCue";
        sleeve.transform.SetParent(presenterRoot.transform, false);
        GmParlorAldricPresenter presenter = presenterRoot.AddComponent<GmParlorAldricPresenter>();
        Assert.That(presenter.TryConfigure(hand.transform, sleeve.GetComponent<Renderer>(),
            contact.transform, log, out string error), Is.True, error);
        return presenter;
    }
}

static class GmParlorPresentationTestTransformExtensions
{
    public static Transform SetParentAndReturn(this Transform transform, Transform parent)
    {
        transform.SetParent(parent, false);
        return transform;
    }
}
