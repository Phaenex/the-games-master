using System;
using NUnit.Framework;

public sealed class GmSemanticActionExecutorTests
{
    [Test]
    public void ActionTraversesFivePhasesAndTransfersContactOwnership()
    {
        var driver = new RecordingDriver();
        var contact = new RecordingContact();
        var claims = new GmSemanticResourceClaims();
        var executor = new GmSemanticActionExecutor(4, driver, contact, claims);

        Assert.That(executor.TryEnqueue(Request(10), out string error), Is.True, error);
        executor.Advance(10f);

        Assert.That(driver.Trace, Is.EqualTo("Approach>Contact>Manipulate>Release>Settle>Snap>Complete>"));
        Assert.That(contact.AttachCount, Is.EqualTo(1));
        Assert.That(contact.ReleaseCount, Is.EqualTo(1));
        Assert.That(claims.Claimed, Is.EqualTo(GmSemanticResource.None));
        Assert.That(executor.IsIdle, Is.True);
    }

    [Test]
    public void SharedClaimsRejectASecondExecutorUsingTheHeldHandAndCard()
    {
        var claims = new GmSemanticResourceClaims();
        var first = new GmSemanticActionExecutor(2, new RecordingDriver(),
            new RecordingContact(), claims);
        var second = new GmSemanticActionExecutor(2, new RecordingDriver(),
            new RecordingContact(), claims);
        Assert.That(first.TryEnqueue(Request(1), out _), Is.True);
        Assert.That(second.TryEnqueue(Request(2), out string error), Is.False);
        StringAssert.Contains("claimed", error);
    }

    [Test]
    public void SharedClaimsDoNotTreatDuplicateStableActionIdsAsTheSameExecutor()
    {
        var claims = new GmSemanticResourceClaims();
        var first = new GmSemanticActionExecutor(2, new RecordingDriver(),
            new RecordingContact(), claims);
        var second = new GmSemanticActionExecutor(2, new RecordingDriver(),
            new RecordingContact(), claims);
        Assert.That(first.TryEnqueue(Request(77), out _), Is.True);
        Assert.That(second.TryEnqueue(Request(77), out string error), Is.False);
        StringAssert.Contains("claimed", error);
    }

    [Test]
    public void RequestRejectsUnknownResourceBits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GmSemanticActionRequest(
            501, (GmSemanticResource)(1 << 12), 1, 0.1f, 0.1f, 0.1f, 0.1f, 0.1f));
    }

    [Test]
    public void DefaultRequestIsRejectedWithoutPoisoningIdleOrQueuedExecution()
    {
        var executor = NewExecutor(out _, out _, out _);
        Assert.That(executor.TryEnqueue(default, out string idleError), Is.False);
        StringAssert.Contains("invalid", idleError);

        Assert.That(executor.TryEnqueue(Request(510), out _), Is.True);
        Assert.That(executor.TryEnqueue(default, out string queuedError), Is.False);
        StringAssert.Contains("invalid", queuedError);
        Assert.That(executor.PendingCount, Is.EqualTo(1));
        executor.Advance(10f);
        Assert.That(executor.IsIdle, Is.True);
    }

    [Test]
    public void QueuedClaimContentionWaitsWithoutCountingAsFailure()
    {
        var claims = new GmSemanticResourceClaims();
        var mainDiagnostics = new RecordingDiagnostics();
        var main = new GmSemanticActionExecutor(3, new RecordingDriver(),
            new RecordingContact(), claims, mainDiagnostics);
        var blocker = new GmSemanticActionExecutor(2, new RecordingDriver(),
            new RecordingContact(), claims);
        Assert.That(main.TryEnqueue(Request(601, GmSemanticResource.AldricLeftHand), out _), Is.True);
        Assert.That(main.TryEnqueue(Request(602, GmSemanticResource.AldricRightHand), out _), Is.True);
        Assert.That(blocker.TryEnqueue(Request(603, GmSemanticResource.AldricRightHand), out _), Is.True);

        main.Advance(10f);

        Assert.That(main.PendingCount, Is.EqualTo(1));
        Assert.That(main.Phase, Is.EqualTo(GmSemanticActionPhase.Complete));
        Assert.That(mainDiagnostics.Count, Is.Zero);
        Assert.That(main.IsCircuitOpen, Is.False);

        blocker.SkipCurrent();
        main.Advance(10f);
        Assert.That(main.IsIdle, Is.True);
        Assert.That(mainDiagnostics.Count, Is.Zero);
    }

    [Test]
    public void EnqueueBehindAContendedHeadPreservesFifoAndAcceptsTheNewTail()
    {
        var claims = new GmSemanticResourceClaims();
        var mainDriver = new RecordingDriver();
        var main = new GmSemanticActionExecutor(4, mainDriver,
            new RecordingContact(), claims);
        var blocker = new GmSemanticActionExecutor(2, new RecordingDriver(),
            new RecordingContact(), claims);
        main.TryEnqueue(Request(611, GmSemanticResource.AldricLeftHand), out _);
        main.TryEnqueue(Request(612, GmSemanticResource.AldricRightHand), out _);
        blocker.TryEnqueue(Request(613, GmSemanticResource.AldricRightHand), out _);
        main.Advance(10f);
        Assert.That(main.PendingCount, Is.EqualTo(1));

        Assert.That(main.TryEnqueue(Request(614, GmSemanticResource.AldricLeftHand),
            out string error), Is.True, error);
        Assert.That(main.PendingCount, Is.EqualTo(2));

        blocker.SkipCurrent();
        main.Advance(10f);
        Assert.That(main.IsIdle, Is.True);
        Assert.That(mainDriver.CompletedIds, Is.EqualTo("611,612,614,"));
    }

    [TestCase(ThrowSite.Enter)]
    [TestCase(ThrowSite.Attach)]
    [TestCase(ThrowSite.Release)]
    [TestCase(ThrowSite.Cancel)]
    [TestCase(ThrowSite.Snap)]
    [TestCase(ThrowSite.Complete)]
    [TestCase(ThrowSite.Restore)]
    public void AdapterExceptionsAreContainedAndReleaseAllTransientState(ThrowSite site)
    {
        var diagnostics = new RecordingDiagnostics();
        var driver = new RecordingDriver { ThrowSite = site };
        var contact = new RecordingContact { ThrowSite = site };
        var claims = new GmSemanticResourceClaims();
        var executor = new GmSemanticActionExecutor(2, driver, contact, claims, diagnostics);

        Assert.DoesNotThrow(() =>
        {
            bool enqueued = executor.TryEnqueue(Request(700), out _);
            if (site == ThrowSite.Restore || site == ThrowSite.Cancel)
            {
                if (enqueued) executor.Advance(0.21f);
                executor.RestoreSnap(22);
            }
            else if (enqueued)
            {
                executor.Advance(10f);
            }
        });

        Assert.That(executor.IsIdle, Is.True);
        Assert.That(executor.PendingCount, Is.Zero);
        Assert.That(executor.AudioMarkerCount, Is.Zero);
        Assert.That(contact.IsHeld, Is.False);
        Assert.That(claims.Claimed, Is.EqualTo(GmSemanticResource.None));
        Assert.That(diagnostics.Count, Is.EqualTo(1));
        StringAssert.Contains("injected", executor.LastFailure);
    }

    [Test]
    public void PauseSpeedAndReducedMotionUseTheExplicitClock()
    {
        var executor = NewExecutor(out RecordingDriver driver, out _, out _);
        executor.Paused = true;
        executor.TryEnqueue(Request(3), out _);
        executor.Advance(100f);
        Assert.That(executor.Phase, Is.EqualTo(GmSemanticActionPhase.Approach));
        Assert.That(driver.Trace, Is.EqualTo("Approach>"));

        executor.Paused = false;
        executor.Speed = 2f;
        executor.ReducedMotion = true;
        executor.Advance(1f);
        Assert.That(executor.IsIdle, Is.True);
    }

    [TestCase(0f, false)]
    [TestCase(0.25f, false)]
    [TestCase(1f, true)]
    [TestCase(4f, true)]
    public void AuthoredSpeedProfilesAreDeterministic(float speed, bool completesInOneSecond)
    {
        var executor = NewExecutor(out _, out _, out _);
        executor.Speed = speed;
        executor.TryEnqueue(Request(300), out _);
        executor.Advance(1f);
        Assert.That(executor.IsIdle, Is.EqualTo(completesInOneSecond));
    }

    [Test]
    public void SpeedOutsideZeroToFourIsRejected()
    {
        var executor = NewExecutor(out _, out _, out _);
        Assert.Throws<ArgumentOutOfRangeException>(() => executor.Speed = -0.01f);
        Assert.Throws<ArgumentOutOfRangeException>(() => executor.Speed = 4.01f);
    }

    [Test]
    public void ZeroSpeedFreezesEvenAnAllZeroDurationAction()
    {
        var executor = NewExecutor(out RecordingDriver driver, out _, out _);
        executor.Speed = 0f;
        executor.TryEnqueue(Request(301, duration: 0f), out _);
        executor.Advance(1f);
        Assert.That(executor.Phase, Is.EqualTo(GmSemanticActionPhase.Approach));
        Assert.That(driver.Trace, Is.EqualTo("Approach>"));
    }

    [Test]
    public void SkipReleasesHeldContactSnapsAndContinuesWithoutCanonicalMutation()
    {
        var executor = NewExecutor(out RecordingDriver driver,
            out RecordingContact contact, out GmSemanticResourceClaims claims);
        executor.TryEnqueue(Request(4), out _);
        executor.Advance(0.21f);
        Assert.That(contact.IsHeld, Is.True);

        executor.SkipCurrent();

        Assert.That(contact.IsHeld, Is.False);
        Assert.That(contact.ReleaseCount, Is.Zero,
            "skip must not use the normal effect-capable release path");
        Assert.That(contact.CancelCount, Is.EqualTo(1));
        Assert.That(driver.SnapCount, Is.EqualTo(1));
        Assert.That(claims.Claimed, Is.EqualTo(GmSemanticResource.None));
        Assert.That(executor.IsIdle, Is.True);
    }

    [Test]
    public void FailureReleasesSnapsLogsOnceAndOpensCircuitAfterThreeFailures()
    {
        var diagnostics = new RecordingDiagnostics();
        var driver = new RecordingDriver { FailPhase = GmSemanticActionPhase.Contact };
        var contact = new RecordingContact();
        var claims = new GmSemanticResourceClaims();
        var executor = new GmSemanticActionExecutor(2, driver, contact, claims, diagnostics);

        for (int i = 0; i < 3; i++)
        {
            Assert.That(executor.TryEnqueue(Request((ulong)(20 + i)), out _), Is.True);
            executor.Advance(1f);
        }

        Assert.That(diagnostics.Count, Is.EqualTo(3));
        Assert.That(diagnostics.LastPhase, Is.EqualTo(GmSemanticActionPhase.Contact));
        Assert.That(executor.IsCircuitOpen, Is.True);
        Assert.That(executor.TryEnqueue(Request(99), out string error), Is.False);
        StringAssert.Contains("circuit", error);
        Assert.That(contact.IsHeld, Is.False);
        Assert.That(claims.Claimed, Is.EqualTo(GmSemanticResource.None));
    }

    [Test]
    public void SuccessfulSkipResetsConsecutiveFailureBreaker()
    {
        var driver = new RecordingDriver { FailPhase = GmSemanticActionPhase.Contact };
        var executor = new GmSemanticActionExecutor(2, driver,
            new RecordingContact(), new GmSemanticResourceClaims());
        for (int i = 0; i < 2; i++)
        {
            executor.TryEnqueue(Request((ulong)(800 + i)), out _);
            executor.Advance(1f);
        }
        driver.FailPhase = null;
        executor.TryEnqueue(Request(802), out _);
        executor.SkipCurrent();
        Assert.That(executor.LastFailure, Is.Empty);

        driver.FailPhase = GmSemanticActionPhase.Contact;
        executor.TryEnqueue(Request(803), out _);
        executor.Advance(1f);
        Assert.That(executor.IsCircuitOpen, Is.False);
    }

    [Test]
    public void ApproachFailureLeavesAnEmptyUsableQueueAndReportsTheFailedPhase()
    {
        var diagnostics = new RecordingDiagnostics();
        var driver = new RecordingDriver { FailPhase = GmSemanticActionPhase.Approach };
        var executor = new GmSemanticActionExecutor(2, driver, new RecordingContact(),
            new GmSemanticResourceClaims(), diagnostics);

        Assert.That(executor.TryEnqueue(Request(88), out string error), Is.False);

        StringAssert.Contains("injected", error);
        Assert.That(executor.PendingCount, Is.Zero);
        Assert.That(executor.IsIdle, Is.True);
        Assert.That(diagnostics.Count, Is.EqualTo(1));
        Assert.That(diagnostics.LastPhase, Is.EqualTo(GmSemanticActionPhase.Approach));
    }

    [Test]
    public void RestoreClearsClaimsQueueAudioMarkersAndHeldCardWithoutReplayingEffects()
    {
        var executor = NewExecutor(out RecordingDriver driver,
            out RecordingContact contact, out GmSemanticResourceClaims claims);
        executor.TryEnqueue(Request(30), out _);
        executor.TryEnqueue(Request(31), out _);
        executor.Advance(0.21f);
        executor.MarkAudioMarker(7);
        int phaseEntriesBeforeRestore = driver.PhaseEntryCount;
        Assert.That(contact.IsHeld, Is.True);

        executor.RestoreSnap(publicPoseId: 441);

        Assert.That(executor.IsIdle, Is.True);
        Assert.That(executor.PendingCount, Is.Zero);
        Assert.That(executor.AudioMarkerCount, Is.Zero);
        Assert.That(contact.IsHeld, Is.False);
        Assert.That(contact.ReleaseCount, Is.Zero,
            "restore must not use the normal effect-capable release path");
        Assert.That(contact.CancelCount, Is.EqualTo(1));
        Assert.That(claims.Claimed, Is.EqualTo(GmSemanticResource.None));
        Assert.That(driver.RestorePoseId, Is.EqualTo(441));
        Assert.That(driver.PhaseEntryCount, Is.EqualTo(phaseEntriesBeforeRestore),
            "restore must not replay phase/cue/evidence effects");
        Assert.That(driver.CompleteCount, Is.Zero, "restore is reconstruction, not completion replay");
    }

    [Test]
    public void SteadyStateAdvanceAllocatesNoManagedMemoryAfterWarmup()
    {
        var executor = NewExecutor(out _, out _, out _);
        executor.TryEnqueue(Request(50, duration: 1000f), out _);
        executor.Advance(0.01f);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) executor.Advance(0.001f);
        long after = GC.GetAllocatedBytesForCurrentThread();
        Assert.That(after - before, Is.Zero);
    }

    static GmSemanticActionExecutor NewExecutor(out RecordingDriver driver,
        out RecordingContact contact, out GmSemanticResourceClaims claims)
    {
        driver = new RecordingDriver();
        contact = new RecordingContact();
        claims = new GmSemanticResourceClaims();
        return new GmSemanticActionExecutor(4, driver, contact, claims);
    }

    static GmSemanticActionRequest Request(ulong id, float duration = 0.1f)
    {
        return new GmSemanticActionRequest(id,
            GmSemanticResource.AldricRightHand | GmSemanticResource.TableCard,
            publicPoseId: (int)(id & 0x7fffffff), duration, duration, duration, duration, duration);
    }

    static GmSemanticActionRequest Request(ulong id, GmSemanticResource resources,
        float duration = 0.1f)
    {
        return new GmSemanticActionRequest(id, resources,
            publicPoseId: (int)(id & 0x7fffffff), duration, duration, duration, duration, duration);
    }

    public enum ThrowSite
    {
        None,
        Enter,
        Attach,
        Release,
        Cancel,
        Snap,
        Complete,
        Restore,
    }

    sealed class RecordingDriver : IGmSemanticActionDriver
    {
        public string Trace = string.Empty;
        public GmSemanticActionPhase? FailPhase;
        public int SnapCount;
        public int CompleteCount;
        public int PhaseEntryCount;
        public int RestorePoseId;
        public ThrowSite ThrowSite;
        public string CompletedIds = string.Empty;

        public bool TryEnterPhase(in GmSemanticActionRequest request,
            GmSemanticActionPhase phase, out string error)
        {
            if (ThrowSite == ThrowSite.Enter) throw new InvalidOperationException("injected enter throw");
            PhaseEntryCount++;
            Trace += phase + ">";
            if (FailPhase == phase)
            {
                error = "injected phase failure";
                return false;
            }
            error = string.Empty;
            return true;
        }

        public void SnapToSettled(in GmSemanticActionRequest request)
        {
            if (ThrowSite == ThrowSite.Snap) throw new InvalidOperationException("injected snap throw");
            SnapCount++;
            Trace += "Snap>";
        }

        public void Complete(in GmSemanticActionRequest request)
        {
            if (ThrowSite == ThrowSite.Complete) throw new InvalidOperationException("injected complete throw");
            CompleteCount++;
            CompletedIds += request.StableActionId + ",";
            Trace += "Complete>";
        }

        public void RestorePublicPose(int publicPoseId)
        {
            if (ThrowSite == ThrowSite.Restore) throw new InvalidOperationException("injected restore throw");
            RestorePoseId = publicPoseId;
        }
    }

    sealed class RecordingContact : IGmSemanticActionContact
    {
        public int AttachCount;
        public int ReleaseCount;
        public int CancelCount;
        public bool IsHeld;
        public ThrowSite ThrowSite;

        public bool TryAttach(in GmSemanticActionRequest request, out string error)
        {
            if (ThrowSite == ThrowSite.Attach) throw new InvalidOperationException("injected attach throw");
            AttachCount++;
            IsHeld = true;
            error = string.Empty;
            return true;
        }

        public void Release(in GmSemanticActionRequest request)
        {
            if (!IsHeld) return;
            if (ThrowSite == ThrowSite.Release) throw new InvalidOperationException("injected release throw");
            IsHeld = false;
            ReleaseCount++;
        }

        public void Cancel(in GmSemanticActionRequest request)
        {
            if (!IsHeld) return;
            IsHeld = false;
            if (ThrowSite == ThrowSite.Cancel) throw new InvalidOperationException("injected cancel throw");
            CancelCount++;
        }
    }

    sealed class RecordingDiagnostics : IGmSemanticActionDiagnostics
    {
        public int Count;
        public GmSemanticActionPhase LastPhase;
        public void ReportFailure(ulong actionId, GmSemanticActionPhase phase, string error)
        {
            Count++;
            LastPhase = phase;
        }
    }
}
