using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class GmBonesControllerTests
{
    string directory;
    string path;
    ToggleBackend backend;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "gm-bones-controller-" + Guid.NewGuid().ToString("N"));
        path = Path.Combine(directory, "save.json");
        backend = new ToggleBackend();
        GmSaveSystem.ConfigureForTests(path, backend);
        GmRunSeed.ForceForReview(7711);
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
    public void StartAndEveryChoiceAreDurableAndResumeExactly()
    {
        GmSaveSystem.ConfigureForTests(path);
        var controller = new GmBonesController();
        Assert.That(controller.InitializeOrRestore(), Is.EqualTo(GmBonesInitializeResult.StartedNew));
        AssertDiskResumes(controller);
        for (int choice = 0; choice < 3; choice++)
        {
            Assert.That(controller.ConfirmFocusedAction(), Is.EqualTo(GmBonesActionError.None));
            AssertDiskResumes(controller);
        }
    }

    [Test]
    public void FocusMapsBankAndThreePressSlotsAndWraps()
    {
        var controller = new GmBonesController();
        controller.InitializeOrRestore();
        Assert.That(controller.FocusIndex, Is.Zero);
        controller.MoveFocus(-1);
        Assert.That(controller.FocusIndex, Is.EqualTo(3));
        controller.MoveFocus(1);
        Assert.That(controller.FocusIndex, Is.Zero);
        controller.MoveFocus(2);
        Assert.That(controller.FocusIndex, Is.EqualTo(2));

        for (int focus = 1; focus <= 3; focus++)
        {
            GmRunStore.BeginNewRun();
            var focused = new GmBonesController();
            Assert.That(focused.InitializeOrRestore(), Is.EqualTo(GmBonesInitializeResult.StartedNew));
            focused.MoveFocus(focus);
            Assert.That(focused.ConfirmFocusedAction(), Is.EqualTo(GmBonesActionError.None));
            Assert.That(GmRunStore.GetBonesMatchSnapshot().actionJournal[0],
                Is.EqualTo($"round-1:press:{focus - 1}"));
        }
    }

    [Test]
    public void FocusEventFiresOnceOnlyForEffectiveMovesAndNeverPersists()
    {
        GmSaveSystem.ConfigureForTests(path);
        var controller = new GmBonesController();
        Assert.That(controller.InitializeOrRestore(), Is.EqualTo(GmBonesInitializeResult.StartedNew));
        string fingerprint = controller.Snapshot.stateFingerprint;
        string disk = File.ReadAllText(path);
        int events = 0;
        controller.OnFocusChanged += () => events++;

        controller.MoveFocus(0);
        controller.MoveFocus(4);
        Assert.That(events, Is.Zero);
        controller.MoveFocus(1);
        Assert.That(controller.FocusIndex, Is.EqualTo(1));
        Assert.That(events, Is.EqualTo(1));
        controller.MoveFocus(-5);
        Assert.That(controller.FocusIndex, Is.Zero);
        Assert.That(events, Is.EqualTo(2));
        Assert.That(controller.Snapshot.stateFingerprint, Is.EqualTo(fingerprint));
        Assert.That(GmRunStore.GetBonesMatchSnapshot().stateFingerprint, Is.EqualTo(fingerprint));
        Assert.That(File.ReadAllText(path), Is.EqualTo(disk));
    }

    [Test]
    public void FailedWriteRollsBackMatchStoreFocusAndEventsThenRetryCommitsOnce()
    {
        var controller = new GmBonesController();
        controller.InitializeOrRestore();
        controller.MoveFocus(2);
        string matchBefore = controller.Snapshot.stateFingerprint;
        string storeBefore = GmRunStore.GetBonesMatchSnapshot().stateFingerprint;
        int focusBefore = controller.FocusIndex;
        int stateEvents = 0;
        int completionEvents = 0;
        controller.OnStateChanged += () => stateEvents++;
        controller.OnCompleted += _ => completionEvents++;

        backend.Fail = true;
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("injected bones failure"));
        Assert.That(controller.ConfirmFocusedAction(), Is.EqualTo(GmBonesActionError.PersistenceFailed));
        Assert.That(controller.Snapshot.stateFingerprint, Is.EqualTo(matchBefore));
        Assert.That(GmRunStore.GetBonesMatchSnapshot().stateFingerprint, Is.EqualTo(storeBefore));
        Assert.That(controller.FocusIndex, Is.EqualTo(focusBefore));
        Assert.That(stateEvents, Is.Zero);
        Assert.That(completionEvents, Is.Zero);

        backend.Fail = false;
        Assert.That(controller.ConfirmFocusedAction(), Is.EqualTo(GmBonesActionError.None));
        Assert.That(stateEvents, Is.EqualTo(1));
        Assert.That(completionEvents, Is.Zero);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void LoadedSixChallengeAndProceedPersistExactlyWithoutCatches(bool challenge)
    {
        GmBonesMatch pending = PendingLoadedSix();
        GmRunStore.LoadFromSaveData(new GmSaveData { bonesMatch = pending.ExportSnapshot() });
        var controller = new GmBonesController();
        Assert.That(controller.InitializeOrRestore(), Is.EqualTo(GmBonesInitializeResult.Restored),
            controller.LastRestoreError);
        int catches = GmRunStore.CheatsCaughtCount;

        GmBonesActionError result = challenge
            ? controller.CallTell()
            : controller.ConfirmFocusedAction();
        Assert.That(result, Is.EqualTo(GmBonesActionError.None));
        Assert.That(GmRunStore.CheatsCaughtCount, Is.EqualTo(catches));
        Assert.That(GmRunStore.HasClue("bones-loaded-six-intervention"), Is.EqualTo(challenge));
        ReloadStoreFromDisk();
        Assert.That(GmRunStore.GetBonesMatchSnapshot().phase, Is.EqualTo(GmBonesMatchPhase.Complete));
        Assert.That(GmRunStore.HasClue("bones-loaded-six-intervention"), Is.EqualTo(challenge));
    }

    [TestCase(GmBonesMatchResult.PlayerWin, 2, 0, 0f)]
    [TestCase(GmBonesMatchResult.AldricWin, 0, 2, -0.05f)]
    [TestCase(GmBonesMatchResult.Tie, 1, 1, 0f)]
    public void FirstTerminalPersistenceAppliesOutcomeAndCompletionExactlyOnce(
        GmBonesMatchResult expected, int defiance, int compliance, float sanityDelta)
    {
        GmBonesMatch beforeFinal = MatchBeforeFinalChoice(expected);
        GmRunStore.LoadFromSaveData(new GmSaveData { bonesMatch = beforeFinal.ExportSnapshot() });
        var controller = new GmBonesController();
        Assert.That(controller.InitializeOrRestore(), Is.EqualTo(GmBonesInitializeResult.Restored),
            controller.LastRestoreError);
        float sanityBefore = GmRunStore.Sanity;
        Assert.That(controller.ConfirmFocusedAction(), Is.EqualTo(GmBonesActionError.None));
        Assert.That(controller.Result, Is.EqualTo(expected));
        Assert.That(GmRunStore.Defiance, Is.EqualTo(defiance));
        Assert.That(GmRunStore.Compliance, Is.EqualTo(compliance));
        Assert.That(GmRunStore.Sanity, Is.EqualTo(sanityBefore + sanityDelta).Within(0.001f));
        Assert.That(GmRunStore.IsRoomComplete("bones"), Is.True);
        Assert.That(GmRunStore.TableGameIndex, Is.EqualTo(1));

        var restored = new GmBonesController();
        Assert.That(restored.InitializeOrRestore(), Is.EqualTo(GmBonesInitializeResult.Restored),
            restored.LastRestoreError);
        Assert.That(GmRunStore.Defiance, Is.EqualTo(defiance));
        Assert.That(GmRunStore.Compliance, Is.EqualTo(compliance));
        Assert.That(GmRunStore.TableGameIndex, Is.EqualTo(1));
    }

    [Test]
    public void ExistingBonesRoomWithoutTableCompletionStillAppliesOutcomeOnce()
    {
        GmBonesMatch beforeFinal = MatchBeforeFinalChoice(GmBonesMatchResult.PlayerWin);
        var legacy = new GmSaveData { bonesMatch = beforeFinal.ExportSnapshot() };
        legacy.completedRooms.Add("bones");
        GmRunStore.LoadFromSaveData(legacy);
        var controller = new GmBonesController();
        Assert.That(controller.InitializeOrRestore(), Is.EqualTo(GmBonesInitializeResult.Restored),
            controller.LastRestoreError);

        Assert.That(controller.ConfirmFocusedAction(), Is.EqualTo(GmBonesActionError.None));
        Assert.That(GmRunStore.IsRoomComplete("bones"), Is.True);
        Assert.That(GmRunStore.TableGameIndex, Is.EqualTo(1));
        Assert.That(GmRunStore.Defiance, Is.EqualTo(2));

        var restored = new GmBonesController();
        Assert.That(restored.InitializeOrRestore(), Is.EqualTo(GmBonesInitializeResult.Restored));
        Assert.That(GmRunStore.TableGameIndex, Is.EqualTo(1));
        Assert.That(GmRunStore.Defiance, Is.EqualTo(2));
    }

    [Test]
    public void ControllerSourceCannotUseCatchOrSceneRouting()
    {
        string sourcePath = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../unity/scene-system/Runtime/GmBonesController.cs"));
        string source = File.ReadAllText(sourcePath);
        Assert.That(source, Does.Not.Contain("RecordCatch"));
        Assert.That(source, Does.Not.Contain("GmSceneDirector"));
        Assert.That(source, Does.Not.Contain("scene-registry"));
        string registryPath = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../unity/scene-system/scene-registry.json"));
        Assert.That(File.ReadAllText(registryPath), Does.Not.Contain("\"bones\""));
    }

    [Test]
    public void PublicSnapshotCannotAdvanceOrMutateControllerStoreDiskOrEvents()
    {
        var controller = new GmBonesController();
        Assert.That(controller.InitializeOrRestore(), Is.EqualTo(GmBonesInitializeResult.StartedNew));
        string diskBefore = File.Exists(path) ? File.ReadAllText(path) : backend.LastJson;
        string fingerprint = controller.Snapshot.stateFingerprint;
        int stateEvents = 0;
        int completionEvents = 0;
        controller.OnStateChanged += () => stateEvents++;
        controller.OnCompleted += _ => completionEvents++;

        GmBonesMatchSnapshot exposed = controller.Snapshot;
        exposed.stateFingerprint = "caller-forged";
        exposed.currentDice[0] = exposed.currentDice[0] == 6 ? 1 : 6;
        exposed.actionJournal = new[] { "round-1:bank" };

        Assert.That(typeof(GmBonesController).GetProperty("Match"), Is.Null);
        Assert.That(controller.Snapshot.stateFingerprint, Is.EqualTo(fingerprint));
        Assert.That(controller.Phase, Is.EqualTo(GmBonesMatchPhase.AwaitingPlayerChoice));
        Assert.That(controller.PlayerDecisionCount, Is.Zero);
        Assert.That(GmRunStore.GetBonesMatchSnapshot().stateFingerprint, Is.EqualTo(fingerprint));
        Assert.That(File.Exists(path) ? File.ReadAllText(path) : backend.LastJson, Is.EqualTo(diskBefore));
        Assert.That(stateEvents, Is.Zero);
        Assert.That(completionEvents, Is.Zero);
    }

    void AssertDiskResumes(GmBonesController controller)
    {
        string fingerprint = controller.Snapshot.stateFingerprint;
        ReloadStoreFromDisk();
        var restored = new GmBonesController();
        Assert.That(restored.InitializeOrRestore(), Is.EqualTo(GmBonesInitializeResult.Restored),
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

    static GmBonesMatch PendingLoadedSix()
    {
        int[] dice = { 6,6,6, 6,6,6, 6,6,6, 6,6,6, 6,6,5, 6,2,1, 1,2 };
        var match = new GmBonesMatch(3UL, dice);
        match.TryChoose(GmBonesChoice.Bank, -1, out _);
        match.TryChoose(GmBonesChoice.Bank, -1, out _);
        match.TryChoose(GmBonesChoice.Bank, -1, out _);
        return match;
    }

    static GmBonesMatch MatchBeforeFinalChoice(GmBonesMatchResult expected)
    {
        int[] dice = expected == GmBonesMatchResult.PlayerWin
            ? new[] { 6,6,6, 4,4,4, 4,4,4, 6,6,6, 6,6,6, 4,4,4 }
            : expected == GmBonesMatchResult.AldricWin
                ? new[] { 4,4,4, 6,6,6, 6,6,6, 4,4,4, 4,4,4, 6,6,6 }
                : new[] { 6,6,6, 6,6,6, 6,6,6, 6,6,6, 6,6,6, 6,6,6 };
        var match = new GmBonesMatch(5UL, dice);
        match.TryChoose(GmBonesChoice.Bank, -1, out _);
        match.TryChoose(GmBonesChoice.Bank, -1, out _);
        return match;
    }

    sealed class ToggleBackend : IGmAtomicSaveBackend
    {
        public bool Fail;
        public string LastJson { get; private set; }
        public void WriteAtomic(string target, string json)
        {
            if (Fail) throw new IOException("injected bones failure");
            LastJson = json;
        }
    }
}
