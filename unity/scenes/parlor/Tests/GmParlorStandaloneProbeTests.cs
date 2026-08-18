using System.IO;
using NUnit.Framework;
using UnityEditor;

public sealed class GmParlorStandaloneProbeTests
{
    [Test]
    public void ReleaseProofFreezesLiteralSeedAndPerformanceGateBeforeMeasurement()
    {
        GmParlorReviewCase item = GmParlorStandaloneProbe.ProofCase;

        Assert.That(item.Id, Is.EqualTo("cheat-true-tell-eyes"));
        Assert.That(item.Seed, Is.EqualTo(8));
        Assert.That(item.CorruptionTier, Is.EqualTo(4));
        Assert.That(item.Decision, Is.EqualTo(GmParlorReviewDecision.CorrectRead));
        Assert.That(GmParlorStandaloneProbe.OutputWidth, Is.EqualTo(1920));
        Assert.That(GmParlorStandaloneProbe.OutputHeight, Is.EqualTo(1080));
        Assert.That(GmParlorStandaloneProbe.TotalP95BudgetMilliseconds, Is.EqualTo(34d));
        Assert.That(GmParlorStandaloneProbe.MainThreadP95BudgetMilliseconds, Is.EqualTo(34d));
        Assert.That(GmParlorStandaloneProbe.GpuP95BudgetMilliseconds, Is.EqualTo(20d));
        Assert.That(GmParlorStandaloneProbe.P99BudgetMilliseconds, Is.EqualTo(80d));
        Assert.That(GmParlorStandaloneProbe.MaximumFrameBudgetMilliseconds, Is.EqualTo(500d));
    }

    [Test]
    public void ProbeSourceUsesInputEdgesAndContainsNoDirectGameplayMutation()
    {
        string source = File.ReadAllText(Path.Combine(System.Environment.GetEnvironmentVariable(
            "GM_REPO_ROOT") ?? Directory.GetCurrentDirectory(), "unity", "scenes", "parlor",
            "Runtime", "GmParlorStandaloneProbe.cs"));

        StringAssert.Contains("InputSystem.QueueStateEvent", source);
        StringAssert.DoesNotContain("GmRunStore", source);
        StringAssert.DoesNotContain("CompleteRoom", source);
        StringAssert.DoesNotContain("controller.ConfirmFocusedAction", source);
        StringAssert.DoesNotContain("controller.CallRead", source);
        StringAssert.DoesNotContain("controller.SetFocusedCardIndex", source);
        StringAssert.DoesNotContain("presentation.FastForward", source);
        StringAssert.DoesNotContain("Match.PlayPlayerCard", source);
        StringAssert.DoesNotContain("Match.Continue", source);
        StringAssert.DoesNotContain("Match.Read", source);
    }

    [Test]
    public void RematchPlaysOneControllerBoundCardBeforePause()
    {
        string source = File.ReadAllText(Path.Combine(System.Environment.GetEnvironmentVariable(
            "GM_REPO_ROOT") ?? Directory.GetCurrentDirectory(), "unity", "scenes", "parlor",
            "Runtime", "GmParlorStandaloneProbe.cs"));

        StringAssert.Contains("rematchActionCompleted", source);
        StringAssert.Contains("rematch card action made no progress", source);
        int readyCapture = source.IndexOf("Capture(\"07-rematch-ready\")",
            System.StringComparison.Ordinal);
        int rematchAction = source.IndexOf("rematchActionCompleted = rules.PlayerHand.Count",
            System.StringComparison.Ordinal);
        int pause = source.IndexOf("Press(GamepadButton.Start)", readyCapture,
            System.StringComparison.Ordinal);
        Assert.That(rematchAction, Is.GreaterThan(readyCapture));
        Assert.That(pause, Is.GreaterThan(rematchAction));
    }

    [Test]
    public void MatchWinnerAndPublicStateAreLatchedBeforeRematchResetsTheMatch()
    {
        string source = File.ReadAllText(Path.Combine(System.Environment.GetEnvironmentVariable(
            "GM_REPO_ROOT") ?? Directory.GetCurrentDirectory(), "unity", "scenes", "parlor",
            "Runtime", "GmParlorStandaloneProbe.cs"));

        int resultReached = source.IndexOf("rules.Phase != GmParlorMatchPhase.MatchResult",
            System.StringComparison.Ordinal);
        int winnerLatch = source.IndexOf("latchedResultWinner = rules.Match.MatchWinner.ToString()",
            resultReached, System.StringComparison.Ordinal);
        int stateLatch = source.IndexOf(
            "latchedResultPublicStateSha256 = TextHash(rules.Match.PublicStateBytes)",
            resultReached, System.StringComparison.Ordinal);
        int rematch = source.IndexOf("ConfirmThroughBindings(-1)", resultReached,
            System.StringComparison.Ordinal);
        Assert.That(winnerLatch, Is.GreaterThan(resultReached));
        Assert.That(stateLatch, Is.GreaterThan(winnerLatch));
        Assert.That(rematch, Is.GreaterThan(stateLatch));
        StringAssert.Contains("ProofExpectedWinner", source);
    }

    [Test]
    public void BuiltCoverageUsesFrozenHonestSeedAndControllerOnlyAccessibilityChanges()
    {
        string source = File.ReadAllText(Path.Combine(System.Environment.GetEnvironmentVariable(
            "GM_REPO_ROOT") ?? Directory.GetCurrentDirectory(), "unity", "scenes", "parlor",
            "Runtime", "GmParlorStandaloneProbe.cs"));

        StringAssert.Contains("honest-false-tell-teeth", source);
        StringAssert.Contains("FalseReadPenalty", source);
        StringAssert.Contains("RunCoverage", source);
        StringAssert.Contains("settingsChangedThroughController", source);
        StringAssert.Contains("preferencesPersisted", source);
        StringAssert.Contains("honest-hc200", source);
        StringAssert.DoesNotContain("GmAccessibilitySettings.SetHighContrast", source);
        StringAssert.DoesNotContain("GmAccessibilitySettings.SetTextScale", source);
    }

    [Test]
    public void ControllerLeavesSettingsFocusToFlushPreferencesBeforeLookingForTheFile()
    {
        string source = File.ReadAllText(Path.Combine(System.Environment.GetEnvironmentVariable(
            "GM_REPO_ROOT") ?? Directory.GetCurrentDirectory(), "unity", "scenes", "parlor",
            "Runtime", "GmParlorStandaloneProbe.cs"));

        StringAssert.Contains("B did not leave Settings controls before persistence", source);
        int cancel = source.IndexOf("Press(GamepadButton.East)", System.StringComparison.Ordinal);
        int persisted = source.IndexOf("File.Exists(requestedPreferencesPath)", cancel,
            System.StringComparison.Ordinal);
        Assert.That(cancel, Is.GreaterThanOrEqualTo(0));
        Assert.That(persisted, Is.GreaterThan(cancel));
    }

    [Test]
    public void CoveragePersistsTheControllerAuthoredValuesThroughTheCanonicalPreferencesWriter()
    {
        string source = File.ReadAllText(Path.Combine(System.Environment.GetEnvironmentVariable(
            "GM_REPO_ROOT") ?? Directory.GetCurrentDirectory(), "unity", "scenes", "parlor",
            "Runtime", "GmParlorStandaloneProbe.cs"));

        StringAssert.Contains("GmSaveSystem.QueueAccessibilityPreferences", source);
        StringAssert.Contains("GmSaveSystem.FlushAccessibilityPreferences", source);
        StringAssert.DoesNotContain("File.WriteAllText(requestedPreferencesPath", source);
    }

    [Test]
    public void AccessibilityUsesTheProvenSingleSubmitEdgeForEveryToggle()
    {
        string source = File.ReadAllText(Path.Combine(System.Environment.GetEnvironmentVariable(
            "GM_REPO_ROOT") ?? Directory.GetCurrentDirectory(), "unity", "scenes", "parlor",
            "Runtime", "GmParlorStandaloneProbe.cs"));

        StringAssert.Contains("caption setting did not accept A", source);
        int hcStart = source.IndexOf("IEnumerator ApplyHc200ThroughController()",
            System.StringComparison.Ordinal);
        int hcEnd = source.IndexOf("IEnumerator FlushSettingsThroughController()", hcStart,
            System.StringComparison.Ordinal);
        string hc = source.Substring(hcStart, hcEnd - hcStart);
        Assert.That(Count(hc, "Press(GamepadButton.South)"), Is.EqualTo(5));
        StringAssert.Contains("Press(GamepadButton.DpadRight)", hc);
        StringAssert.Contains("AdvanceSettingsFocusThroughController", hc);
        StringAssert.Contains("Press(GamepadButton.DpadDown)", source);
        StringAssert.Contains("caption toggle did not settle", hc);
        StringAssert.Contains("reduced-motion toggle did not settle", hc);
        StringAssert.Contains("vibration toggle did not settle", hc);
        StringAssert.Contains("mono-audio toggle did not settle", hc);
        StringAssert.Contains("high-contrast toggle did not settle", hc);
        StringAssert.Contains("text-scale increment did not settle", hc);
    }

    static int Count(string source, string token)
    {
        int count = 0;
        int start = 0;
        while ((start = source.IndexOf(token, start, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            start += token.Length;
        }
        return count;
    }

    [Test]
    public void ControllerEdgesTargetTheProbeOwnedVirtualGamepad()
    {
        string source = File.ReadAllText(Path.Combine(System.Environment.GetEnvironmentVariable(
            "GM_REPO_ROOT") ?? Directory.GetCurrentDirectory(), "unity", "scenes", "parlor",
            "Runtime", "GmParlorStandaloneProbe.cs"));

        StringAssert.Contains("InputSystem.QueueStateEvent(gamepad", source);
        StringAssert.DoesNotContain("Gamepad.current", source);
        StringAssert.Contains("VirtualButtonHoldFrames = 2", source);
        StringAssert.Contains("VirtualButtonReleaseFrames = 4", source);
        StringAssert.Contains("SettingsNavigationRetryLimit = 3", source);
    }

    [Test]
    public void FullGameSceneLoadCannotInstallASecondPersistentProbe()
    {
        string source = File.ReadAllText(Path.Combine(System.Environment.GetEnvironmentVariable(
            "GM_REPO_ROOT") ?? Directory.GetCurrentDirectory(), "unity", "scenes", "parlor",
            "Runtime", "GmParlorStandaloneProbe.cs"));

        StringAssert.Contains("static bool installed;", source);
        int installer = source.IndexOf("static void InstallWhenRequested()",
            System.StringComparison.Ordinal);
        int create = source.IndexOf("new GameObject(\"GmParlorStandaloneProbe\")", installer,
            System.StringComparison.Ordinal);
        int guard = source.IndexOf("if (string.IsNullOrEmpty(requestedDirectory) || installed) return;",
            installer, System.StringComparison.Ordinal);
        int latch = source.IndexOf("installed = true;", guard, System.StringComparison.Ordinal);
        Assert.That(guard, Is.GreaterThan(installer));
        Assert.That(latch, Is.GreaterThan(guard));
        Assert.That(create, Is.GreaterThan(latch));
    }

    [Test]
    public void SettingsEntryUsesTheShortestWraparoundPathAndRejectsDetachedFocus()
    {
        string source = File.ReadAllText(Path.Combine(System.Environment.GetEnvironmentVariable(
            "GM_REPO_ROOT") ?? Directory.GetCurrentDirectory(), "unity", "scenes", "parlor",
            "Runtime", "GmParlorStandaloneProbe.cs"));
        int start = source.IndexOf("IEnumerator EnterSettingsThroughController()",
            System.StringComparison.Ordinal);
        int end = source.IndexOf("void LateUpdate()", start, System.StringComparison.Ordinal);
        string enter = source.Substring(start, end - start);

        StringAssert.Contains("Press(GamepadButton.DpadLeft)", enter);
        StringAssert.DoesNotContain("Press(GamepadButton.DpadRight)", enter);
        StringAssert.Contains("pause.ActiveTab == GmPauseTab.Settings &&", enter);
        StringAssert.Contains("pause.SettingsFocusActive", enter);
    }

    [Test]
    public void SuspiciousCaptionIsCapturedBeforeTheBlockingPresentationCanExpireIt()
    {
        string source = File.ReadAllText(Path.Combine(System.Environment.GetEnvironmentVariable(
            "GM_REPO_ROOT") ?? Directory.GetCurrentDirectory(), "unity", "scenes", "parlor",
            "Runtime", "GmParlorStandaloneProbe.cs"));

        int caption = source.IndexOf(
            "WaitUntil(() => presentation.HasActiveCaption", System.StringComparison.Ordinal);
        int settled = source.IndexOf("first card presentation did not settle",
            System.StringComparison.Ordinal);
        Assert.That(caption, Is.GreaterThanOrEqualTo(0));
        Assert.That(settled, Is.GreaterThan(caption),
            "waiting for all blocking presentation first lets the 0.75s caption expire");
    }

    [Test]
    public void PerformanceSampleFloorHasABoundedFailureInsteadOfHangingThePlayer()
    {
        string source = File.ReadAllText(Path.Combine(System.Environment.GetEnvironmentVariable(
            "GM_REPO_ROOT") ?? Directory.GetCurrentDirectory(), "unity", "scenes", "parlor",
            "Runtime", "GmParlorStandaloneProbe.cs"));

        StringAssert.DoesNotContain("while (samples.Count < 1800) yield return null", source);
        StringAssert.Contains("performance sample floor was not reached", source);
    }

    [Test]
    public void ReleasePlayerMeasuresMainThreadAllocationWhenProfilerCounterIsUnavailable()
    {
        string source = File.ReadAllText(Path.Combine(System.Environment.GetEnvironmentVariable(
            "GM_REPO_ROOT") ?? Directory.GetCurrentDirectory(), "unity", "scenes", "parlor",
            "Runtime", "GmParlorStandaloneProbe.cs"));

        StringAssert.Contains("GC.GetAllocatedBytesForCurrentThread()", source);
        StringAssert.Contains("main-thread-cumulative-allocation-counter", source);
    }

    [Test]
    public void MainThreadGateUsesTheMainThreadFieldNotTotalCpuFrameTime()
    {
        string source = File.ReadAllText(Path.Combine(System.Environment.GetEnvironmentVariable(
            "GM_REPO_ROOT") ?? Directory.GetCurrentDirectory(), "unity", "scenes", "parlor",
            "Runtime", "GmParlorStandaloneProbe.cs"));

        StringAssert.Contains("cpuMainThreadFrameTime", source);
        StringAssert.DoesNotContain(".cpuFrameTime", source);
    }

    [Test]
    public void MainThreadWorkExcludesFrameLimiterPresentWait()
    {
        string source = File.ReadAllText(Path.Combine(System.Environment.GetEnvironmentVariable(
            "GM_REPO_ROOT") ?? Directory.GetCurrentDirectory(), "unity", "scenes", "parlor",
            "Runtime", "GmParlorStandaloneProbe.cs"));

        StringAssert.Contains("cpuMainThreadPresentWaitTime", source);
        StringAssert.Contains("main-thread-frame-minus-present-wait", source);
        StringAssert.Contains("rawCpuMainThreadFrameMilliseconds", source);
        StringAssert.Contains("rawMainThreadPresentWaitMilliseconds", source);
    }

    [Test]
    public void CaptureIoRemainsExcludedThroughTheFollowingLateUpdate()
    {
        string source = File.ReadAllText(Path.Combine(System.Environment.GetEnvironmentVariable(
            "GM_REPO_ROOT") ?? Directory.GetCurrentDirectory(), "unity", "scenes", "parlor",
            "Runtime", "GmParlorStandaloneProbe.cs"));

        int captureStart = source.IndexOf("IEnumerator Capture(string name)",
            System.StringComparison.Ordinal);
        int captureEnd = source.IndexOf("string[] CaptureUiText()", captureStart,
            System.StringComparison.Ordinal);
        string capture = source.Substring(captureStart, captureEnd - captureStart);
        int evidenceRecorded = capture.IndexOf("shots.Add", System.StringComparison.Ordinal);
        int excludedFrame = capture.IndexOf("yield return null", evidenceRecorded,
            System.StringComparison.Ordinal);
        int cleanBridgeFrame = capture.IndexOf("yield return null", excludedFrame + 1,
            System.StringComparison.Ordinal);
        int restored = capture.IndexOf("samplePhase = previous", evidenceRecorded,
            System.StringComparison.Ordinal);

        Assert.That(excludedFrame, Is.GreaterThan(evidenceRecorded));
        Assert.That(cleanBridgeFrame, Is.GreaterThan(excludedFrame));
        Assert.That(restored, Is.GreaterThan(cleanBridgeFrame),
            "deltaTime after the I/O frame must also pass through an unmeasured LateUpdate");
    }

    [Test]
    public void PerformanceRunUsesTheRepositorysVsyncOff120FpsCeiling()
    {
        string source = File.ReadAllText(Path.Combine(System.Environment.GetEnvironmentVariable(
            "GM_REPO_ROOT") ?? Directory.GetCurrentDirectory(), "unity", "scenes", "parlor",
            "Runtime", "GmParlorStandaloneProbe.cs"));

        StringAssert.Contains("QualitySettings.vSyncCount = 0", source);
        StringAssert.Contains("Application.targetFrameRate = 120", source);
        StringAssert.DoesNotContain("Application.targetFrameRate = 100", source);
        StringAssert.DoesNotContain("Application.targetFrameRate = -1", source);
    }

    [Test]
    public void FrameTimingCaptureContinuesWhileMeasurementIsDisarmed()
    {
        string source = File.ReadAllText(Path.Combine(System.Environment.GetEnvironmentVariable(
            "GM_REPO_ROOT") ?? Directory.GetCurrentDirectory(), "unity", "scenes", "parlor",
            "Runtime", "GmParlorStandaloneProbe.cs"));
        int lateUpdate = source.IndexOf("void LateUpdate()", System.StringComparison.Ordinal);
        int disarmed = source.IndexOf("if (samplePhase == PerfPhase.None)", lateUpdate,
            System.StringComparison.Ordinal);
        int capture = source.IndexOf("FrameTimingManager.CaptureFrameTimings()", lateUpdate,
            System.StringComparison.Ordinal);

        Assert.That(capture, Is.GreaterThan(lateUpdate));
        Assert.That(capture, Is.LessThan(disarmed),
            "capture must stay primed across screenshots and phase transitions");
    }

    [Test]
    public void PerformancePhasesWaitForValidRenderedSampleCounts()
    {
        string source = File.ReadAllText(Path.Combine(System.Environment.GetEnvironmentVariable(
            "GM_REPO_ROOT") ?? Directory.GetCurrentDirectory(), "unity", "scenes", "parlor",
            "Runtime", "GmParlorStandaloneProbe.cs"));

        StringAssert.Contains("CollectPhaseSamples(PerfPhase.Focus, FocusSamples", source);
        StringAssert.Contains("CollectPhaseSamples(PerfPhase.Result, ResultSamples", source);
        StringAssert.Contains("CollectPhaseSamples(PerfPhase.Pause, PauseSamples", source);
        StringAssert.DoesNotContain("yield return Frames(FocusSamples)", source);
        StringAssert.DoesNotContain("yield return Frames(ResultSamples)", source);
        StringAssert.DoesNotContain("yield return Frames(PauseSamples)", source);
    }

    [Test]
    public void PendingAsynchronousTimingPollsAreDisclosedSeparatelyFromUnavailableSamples()
    {
        string source = File.ReadAllText(Path.Combine(System.Environment.GetEnvironmentVariable(
            "GM_REPO_ROOT") ?? Directory.GetCurrentDirectory(), "unity", "scenes", "parlor",
            "Runtime", "GmParlorStandaloneProbe.cs"));

        StringAssert.Contains("pendingFrameTimingPolls", source);
        StringAssert.Contains("if (count == 0)", source);
        StringAssert.Contains("unavailableFrameTimingSamples == 0", source);
        StringAssert.Contains("if (rawMain <= 0d", source);
        StringAssert.DoesNotContain("if (main <= 0d || gpu <= 0d)", source);
    }

    [Test]
    public void ScreenshotTimingRowsAreQuarantinedBeforeMeasurementResumes()
    {
        string source = File.ReadAllText(Path.Combine(System.Environment.GetEnvironmentVariable(
            "GM_REPO_ROOT") ?? Directory.GetCurrentDirectory(), "unity", "scenes", "parlor",
            "Runtime", "GmParlorStandaloneProbe.cs"));

        StringAssert.Contains("CaptureQuarantineTimingRows = 16", source);
        StringAssert.Contains("discardedTimingRows - quarantineStart < CaptureQuarantineTimingRows",
            source);
    }

    [Test]
    public void ScreenshotRecordsAreRelativeToTheirOwnRepetitionDirectory()
    {
        string source = File.ReadAllText(Path.Combine(System.Environment.GetEnvironmentVariable(
            "GM_REPO_ROOT") ?? Directory.GetCurrentDirectory(), "unity", "scenes", "parlor",
            "Runtime", "GmParlorStandaloneProbe.cs"));

        StringAssert.Contains("file = name + \".png\"", source);
        StringAssert.DoesNotContain("Path.GetRelativePath(Directory.GetCurrentDirectory(), path)",
            source);
    }

    [Test]
    public void DisarmedFramesDrainCapturedTimingBeforeMeasurementResumes()
    {
        string source = File.ReadAllText(Path.Combine(System.Environment.GetEnvironmentVariable(
            "GM_REPO_ROOT") ?? Directory.GetCurrentDirectory(), "unity", "scenes", "parlor",
            "Runtime", "GmParlorStandaloneProbe.cs"));
        int disarmed = source.IndexOf("if (samplePhase == PerfPhase.None)",
            System.StringComparison.Ordinal);
        int disarmedReturn = source.IndexOf("return;", disarmed,
            System.StringComparison.Ordinal);
        string block = source.Substring(disarmed, disarmedReturn - disarmed);

        StringAssert.Contains("GetLatestTimings", block,
            "a screenshot timing left in the queue contaminates the next measured main/max sample");
    }

    [Test]
    public void CaptionWaitUsesTheDurationSafeGuardAt120Fps()
    {
        string source = File.ReadAllText(Path.Combine(System.Environment.GetEnvironmentVariable(
            "GM_REPO_ROOT") ?? Directory.GetCurrentDirectory(), "unity", "scenes", "parlor",
            "Runtime", "GmParlorStandaloneProbe.cs"));

        StringAssert.Contains("presentation.HasActiveCaption, GuardFrames", source);
        StringAssert.DoesNotContain("presentation.HasActiveCaption, 120", source);
    }

    [Test]
    public void FailedPerformanceGateRetainsRawDiagnosticInsteadOfOnlyOnePercentile()
    {
        string source = File.ReadAllText(Path.Combine(System.Environment.GetEnvironmentVariable(
            "GM_REPO_ROOT") ?? Directory.GetCurrentDirectory(), "unity", "scenes", "parlor",
            "Runtime", "GmParlorStandaloneProbe.cs"));

        StringAssert.Contains("failed-report.json", source);
        StringAssert.Contains("PERF DIAGNOSTIC", source);
    }

    [Test]
    public void ProofUsesTheFullGameReleaseBuildWithFrameTimingEnabled()
    {
        Assert.That(GmFullGameBuild.MacOutputPath,
            Is.EqualTo("Builds/macOS-Game/The Games Master.app"));
        Assert.That(GmFullGameBuild.EnableFrameTimingStatsForBuild, Is.True);
    }
}
