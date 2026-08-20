using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class GmStudyControllerTests
{
    string directory;
    string path;
    ToggleBackend backend;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "gm-study-controller-" + Guid.NewGuid().ToString("N"));
        path = Path.Combine(directory, "save.json");
        backend = new ToggleBackend();
        GmSaveSystem.ConfigureForTests(path, backend);
        GmRunSeed.ForceForReview(4409);
        GmRunStore.BeginNewRun();
    }

    [TearDown]
    public void TearDown()
    {
        GmSaveSystem.ResetTestConfiguration();
        GmRunSeed.ResetForTests();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        GmRunStore.BeginNewRun();
    }

    [Test]
    public void StartAndEveryMoveAreDurableAndResumeExactly()
    {
        GmSaveSystem.ConfigureForTests(path);
        var controller = new GmStudyController();
        Assert.That(controller.InitializeOrRestore(), Is.EqualTo(GmStudyInitializeResult.StartedNew));
        AssertDiskResumes(controller);
        controller.MoveFocus(1);
        for (int move = 0; move < 3; move++)
        {
            Assert.That(controller.ConfirmFocusedAction(), Is.EqualTo(GmStudyActionError.None));
            AssertDiskResumes(controller);
        }
        Assert.That(controller.Result, Is.EqualTo(GmStudyMatchResult.AldricWin));
    }

    [Test]
    public void FocusWrapsAcrossThreeMoveCardsAndSelectsThePositionCard()
    {
        var controller = new GmStudyController();
        controller.InitializeOrRestore();
        Assert.That(controller.FocusIndex, Is.Zero);
        controller.MoveFocus(-1);
        Assert.That(controller.FocusIndex, Is.EqualTo(2));
        controller.MoveFocus(1);
        Assert.That(controller.FocusIndex, Is.Zero);
        controller.MoveFocus(2);
        Assert.That(controller.FocusIndex, Is.EqualTo(2));

        for (int focus = 0; focus < 3; focus++)
        {
            GmRunStore.BeginNewRun();
            var focused = new GmStudyController();
            Assert.That(focused.InitializeOrRestore(), Is.EqualTo(GmStudyInitializeResult.StartedNew));
            focused.MoveFocus(focus);
            Assert.That(focused.ConfirmFocusedAction(), Is.EqualTo(GmStudyActionError.None));
            string expected = GmStudyRules.GetPosition(0).cards[focus].actionId;
            Assert.That(GmRunStore.GetStudyMatchSnapshot().actionJournal[0], Is.EqualTo(expected));
        }
    }

    [Test]
    public void FocusEventFiresOnceOnlyForEffectiveMovesAndNeverPersists()
    {
        GmSaveSystem.ConfigureForTests(path);
        var controller = new GmStudyController();
        Assert.That(controller.InitializeOrRestore(), Is.EqualTo(GmStudyInitializeResult.StartedNew));
        string fingerprint = controller.Snapshot.stateFingerprint;
        string disk = File.ReadAllText(path);
        int events = 0;
        controller.OnFocusChanged += () => events++;

        controller.MoveFocus(0);
        controller.MoveFocus(3);
        Assert.That(events, Is.Zero);
        controller.MoveFocus(1);
        Assert.That(controller.FocusIndex, Is.EqualTo(1));
        Assert.That(events, Is.EqualTo(1));
        controller.MoveFocus(-4);
        Assert.That(controller.FocusIndex, Is.Zero);
        Assert.That(events, Is.EqualTo(2));
        Assert.That(controller.Snapshot.stateFingerprint, Is.EqualTo(fingerprint));
        Assert.That(GmRunStore.GetStudyMatchSnapshot().stateFingerprint, Is.EqualTo(fingerprint));
        Assert.That(File.ReadAllText(path), Is.EqualTo(disk));
    }

    [Test]
    public void FailedWriteRollsBackMatchStoreFocusAndEventsThenRetryCommitsOnce()
    {
        var controller = new GmStudyController();
        controller.InitializeOrRestore();
        controller.MoveFocus(1);
        string matchBefore = controller.Snapshot.stateFingerprint;
        string storeBefore = GmRunStore.GetStudyMatchSnapshot().stateFingerprint;
        int focusBefore = controller.FocusIndex;
        int stateEvents = 0;
        int completionEvents = 0;
        controller.OnStateChanged += () => stateEvents++;
        controller.OnCompleted += _ => completionEvents++;

        backend.Fail = true;
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("injected study failure"));
        Assert.That(controller.ConfirmFocusedAction(), Is.EqualTo(GmStudyActionError.PersistenceFailed));
        Assert.That(controller.Snapshot.stateFingerprint, Is.EqualTo(matchBefore));
        Assert.That(GmRunStore.GetStudyMatchSnapshot().stateFingerprint, Is.EqualTo(storeBefore));
        Assert.That(controller.FocusIndex, Is.EqualTo(focusBefore));
        Assert.That(stateEvents, Is.Zero);
        Assert.That(completionEvents, Is.Zero);

        backend.Fail = false;
        Assert.That(controller.ConfirmFocusedAction(), Is.EqualTo(GmStudyActionError.None));
        Assert.That(stateEvents, Is.EqualTo(1));
        Assert.That(completionEvents, Is.Zero);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void InterventionChallengeAndProceedPersistExactlyWithoutCatches(bool challenge)
    {
        GmStudyMatch pending = PendingIntervention();
        GmRunStore.LoadFromSaveData(new GmSaveData { studyMatch = pending.ExportSnapshot() });
        var controller = new GmStudyController();
        Assert.That(controller.InitializeOrRestore(), Is.EqualTo(GmStudyInitializeResult.Restored),
            controller.LastRestoreError);
        int catches = GmRunStore.CheatsCaughtCount;

        GmStudyActionError result = challenge
            ? controller.Challenge()
            : controller.ConfirmFocusedAction();
        Assert.That(result, Is.EqualTo(GmStudyActionError.None));
        Assert.That(GmRunStore.CheatsCaughtCount, Is.EqualTo(catches));
        Assert.That(GmRunStore.HasClue("study-arbiter-override-intervention"), Is.EqualTo(challenge));
        if (challenge)
        {
            Assert.That(controller.Result, Is.EqualTo(GmStudyMatchResult.PlayerWin));
            Assert.That(GmRunStore.Defiance, Is.EqualTo(2));
            Assert.That(GmRunStore.IsRoomComplete("study"), Is.True);
            Assert.That(GmRunStore.TableGameIndex, Is.EqualTo(1));
        }
        ReloadStoreFromDisk();
        Assert.That(GmRunStore.HasClue("study-arbiter-override-intervention"), Is.EqualTo(challenge));
        Assert.That(GmRunStore.GetStudyMatchSnapshot().phase, Is.EqualTo(
            challenge ? GmStudyMatchPhase.Complete : GmStudyMatchPhase.AwaitingMove));
        if (challenge)
        {
            Assert.That(GmRunStore.Defiance, Is.EqualTo(2));
            Assert.That(GmRunStore.TableGameIndex, Is.EqualTo(1));
        }
    }

    [Test]
    public void TerminalPersistenceAppliesAldricWinOutcomeExactlyOnce()
    {
        GmStudyMatch beforeFinal = MatchBeforeFinalWrongAnswer();
        GmRunStore.LoadFromSaveData(new GmSaveData { studyMatch = beforeFinal.ExportSnapshot() });
        var controller = new GmStudyController();
        Assert.That(controller.InitializeOrRestore(), Is.EqualTo(GmStudyInitializeResult.Restored),
            controller.LastRestoreError);
        float sanityBefore = GmRunStore.Sanity;
        controller.MoveFocus(1);
        Assert.That(controller.ConfirmFocusedAction(), Is.EqualTo(GmStudyActionError.None));
        Assert.That(controller.Result, Is.EqualTo(GmStudyMatchResult.AldricWin));
        Assert.That(GmRunStore.Defiance, Is.Zero);
        Assert.That(GmRunStore.Compliance, Is.EqualTo(2));
        Assert.That(GmRunStore.Sanity, Is.EqualTo(sanityBefore - 0.05f).Within(0.001f));
        Assert.That(GmRunStore.IsRoomComplete("study"), Is.True);
        Assert.That(GmRunStore.TableGameIndex, Is.EqualTo(1));
        Assert.That(GmRunStore.Sovereigns, Is.EqualTo(1));

        var restored = new GmStudyController();
        Assert.That(restored.InitializeOrRestore(), Is.EqualTo(GmStudyInitializeResult.Restored),
            restored.LastRestoreError);
        Assert.That(GmRunStore.Compliance, Is.EqualTo(2));
        Assert.That(GmRunStore.Sovereigns, Is.EqualTo(1),
            "restoring an already-completed match must not grant a second sovereign");
        Assert.That(GmRunStore.TableGameIndex, Is.EqualTo(1));
    }

    [Test]
    public void ExistingStudyRoomWithoutTableCompletionStillAppliesOutcomeOnce()
    {
        GmStudyMatch beforeFinal = MatchBeforeFinalWrongAnswer();
        var legacy = new GmSaveData { studyMatch = beforeFinal.ExportSnapshot() };
        legacy.completedRooms.Add("study");
        GmRunStore.LoadFromSaveData(legacy);
        var controller = new GmStudyController();
        Assert.That(controller.InitializeOrRestore(), Is.EqualTo(GmStudyInitializeResult.Restored),
            controller.LastRestoreError);

        controller.MoveFocus(1);
        Assert.That(controller.ConfirmFocusedAction(), Is.EqualTo(GmStudyActionError.None));
        Assert.That(GmRunStore.IsRoomComplete("study"), Is.True);
        Assert.That(GmRunStore.TableGameIndex, Is.EqualTo(1));
        Assert.That(GmRunStore.Compliance, Is.EqualTo(2));

        var restored = new GmStudyController();
        Assert.That(restored.InitializeOrRestore(), Is.EqualTo(GmStudyInitializeResult.Restored));
        Assert.That(GmRunStore.TableGameIndex, Is.EqualTo(1));
        Assert.That(GmRunStore.Compliance, Is.EqualTo(2));
    }

    [Test]
    public void ControllerSourceCannotUseCatchOrSceneRouting()
    {
        string sourcePath = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../unity/scene-system/Runtime/GmStudyController.cs"));
        string source = File.ReadAllText(sourcePath);
        Assert.That(source, Does.Not.Contain("RecordCatch"));
        Assert.That(source, Does.Not.Contain("GmSceneDirector"));
        Assert.That(source, Does.Not.Contain("scene-registry"));

        string directorPath = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../unity/scene-system/Runtime/GmSceneDirector.cs"));
        Assert.That(File.ReadAllText(directorPath), Does.Not.Contain("Study"),
            "the durable Study controller must not silently create a campaign route");
    }

    [Test]
    public void PublicSnapshotCannotAdvanceOrMutateControllerStoreDiskOrEvents()
    {
        var controller = new GmStudyController();
        Assert.That(controller.InitializeOrRestore(), Is.EqualTo(GmStudyInitializeResult.StartedNew));
        string diskBefore = File.Exists(path) ? File.ReadAllText(path) : backend.LastJson;
        string fingerprint = controller.Snapshot.stateFingerprint;
        int stateEvents = 0;
        int completionEvents = 0;
        controller.OnStateChanged += () => stateEvents++;
        controller.OnCompleted += _ => completionEvents++;

        GmStudyMatchSnapshot exposed = controller.Snapshot;
        exposed.stateFingerprint = "caller-forged";
        exposed.currentFen = "caller-forged-fen";
        exposed.actionJournal = new[] { "captured-record:c8-c1" };

        Assert.That(typeof(GmStudyController).GetProperty("Match"), Is.Null);
        Assert.That(controller.Snapshot.stateFingerprint, Is.EqualTo(fingerprint));
        Assert.That(controller.Phase, Is.EqualTo(GmStudyMatchPhase.AwaitingMove));
        Assert.That(controller.PlayerDecisionCount, Is.Zero);
        Assert.That(GmRunStore.GetStudyMatchSnapshot().stateFingerprint, Is.EqualTo(fingerprint));
        Assert.That(File.Exists(path) ? File.ReadAllText(path) : backend.LastJson, Is.EqualTo(diskBefore));
        Assert.That(stateEvents, Is.Zero);
        Assert.That(completionEvents, Is.Zero);
    }

    void AssertDiskResumes(GmStudyController controller)
    {
        string fingerprint = controller.Snapshot.stateFingerprint;
        ReloadStoreFromDisk();
        var restored = new GmStudyController();
        Assert.That(restored.InitializeOrRestore(), Is.EqualTo(GmStudyInitializeResult.Restored),
            restored.LastRestoreError);
        Assert.That(restored.Snapshot.stateFingerprint, Is.EqualTo(fingerprint));
    }

    void ReloadStoreFromDisk()
    {
        string json = File.Exists(path) ? File.ReadAllText(path) : backend.LastJson;
        GmSaveData data = GmSaveData.FromJson(json);
        GmRunStore.BeginNewRun();
        GmRunStore.LoadFromSaveData(data);
    }

    static GmStudyMatch PendingIntervention()
    {
        var match = new GmStudyMatch(11UL);
        match.TryChoose(CorrectActionId(0), out _);
        match.TryChoose(CorrectActionId(1), out _);
        return match;
    }

    static GmStudyMatch MatchBeforeFinalWrongAnswer()
    {
        var match = new GmStudyMatch(13UL);
        match.TryChoose(WrongActionId(0), out _);
        match.TryChoose(WrongActionId(1), out _);
        return match;
    }

    static string CorrectActionId(int positionIndex) =>
        GmStudyRules.GetPosition(positionIndex).cards.First(card => card.isCorrect).actionId;

    static string WrongActionId(int positionIndex) =>
        GmStudyRules.GetPosition(positionIndex).cards.First(card => !card.isCorrect).actionId;

    sealed class ToggleBackend : IGmAtomicSaveBackend
    {
        public bool Fail;
        public string LastJson { get; private set; }
        public void WriteAtomic(string target, string json)
        {
            if (Fail) throw new IOException("injected study failure");
            LastJson = json;
        }
    }
}
