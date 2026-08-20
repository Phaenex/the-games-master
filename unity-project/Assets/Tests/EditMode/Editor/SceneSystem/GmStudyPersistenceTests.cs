using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class GmStudyPersistenceTests
{
    string directory;
    string path;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "gm-study-store-" + Guid.NewGuid().ToString("N"));
        path = Path.Combine(directory, "save.json");
        Directory.CreateDirectory(directory);
        GmSaveSystem.ConfigureForTests(path);
        GmRunSeed.ForceForReview(4433);
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
    public void OldJsonWithoutStudyLoadsAsNoMatchAndNewRunClearsStudy()
    {
        File.WriteAllText(path, "{\"corruptionTier\":3}");
        Assert.That(GmSaveSystem.Load(), Is.True, GmSaveSystem.LastError);
        Assert.That(GmRunStore.HasStudyMatch, Is.False);
        Assert.That(GmRunStore.StudyRestoreError, Is.Empty);

        var controller = new GmStudyController();
        Assert.That(controller.InitializeOrRestore(), Is.EqualTo(GmStudyInitializeResult.StartedNew));
        Assert.That(GmRunStore.HasStudyMatch, Is.True);
        GmRunStore.BeginNewRun();
        Assert.That(GmRunStore.HasStudyMatch, Is.False);
    }

    [Test]
    public void StoreAndSaveDataOwnIndependentStudySnapshots()
    {
        var controller = new GmStudyController();
        Assert.That(controller.InitializeOrRestore(), Is.EqualTo(GmStudyInitializeResult.StartedNew));
        GmStudyMatchSnapshot first = GmRunStore.GetStudyMatchSnapshot();
        string fingerprint = first.stateFingerprint;
        first.stateFingerprint = "caller-forged";
        Assert.That(GmRunStore.GetStudyMatchSnapshot().stateFingerprint, Is.EqualTo(fingerprint));

        GmSaveData save = GmRunStore.ToSaveData();
        save.studyMatch.stateFingerprint = "save-forged";
        Assert.That(GmRunStore.GetStudyMatchSnapshot().stateFingerprint, Is.EqualTo(fingerprint));
    }

    [Test]
    public void ModernDiskJsonDeclaresEnvelopeAndOmitsInventedNestedEvidence()
    {
        var controller = new GmStudyController();
        Assert.That(controller.InitializeOrRestore(), Is.EqualTo(GmStudyInitializeResult.StartedNew));
        string json = File.ReadAllText(path);

        StringAssert.Contains("\"studyEnvelopeVersion\": 1", json);
        StringAssert.Contains("\"studyPayloadPresent\": true", json);
        StringAssert.Contains("\"studyTurnEvidencePresent\": false", json);
        StringAssert.Contains("\"studySessionEventPresent\": false", json);
        StringAssert.DoesNotContain("\"interventionReceipt\"", json);
        StringAssert.DoesNotContain("\"intervention\"", json);
        Assert.That(GmSaveData.FromJson(json).studyMatch, Is.Not.Null);
    }

    [Test]
    public void CorruptStudySnapshotIsRetainedAndControllerRefusesFreshFallback()
    {
        var match = new GmStudyMatch(9UL);
        GmStudyMatchSnapshot corrupt = match.ExportSnapshot();
        corrupt.currentFen = string.Empty;
        GmRunStore.LoadFromSaveData(new GmSaveData { studyMatch = corrupt });
        Assert.That(GmRunStore.HasStudyMatch, Is.True);
        Assert.That(GmRunStore.GetStudyMatchSnapshot().currentFen, Is.Empty);
        Assert.That(GmRunStore.StudyRestoreError, Is.Not.Empty);

        var controller = new GmStudyController();
        Assert.That(controller.InitializeOrRestore(), Is.EqualTo(GmStudyInitializeResult.CorruptSavedState));
        Assert.That(controller.Snapshot, Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ExplicitEmptyStudyPayloadIsPresentAndCorrupt(bool modernEnvelope)
    {
        string envelope = modernEnvelope
            ? "\"studyEnvelopeVersion\":1,\"studyPayloadPresent\":true,"
            : string.Empty;
        File.WriteAllText(path, "{" + envelope + "\"studyMatch\":{}}");

        Assert.That(GmSaveSystem.Load(), Is.True, GmSaveSystem.LastError);
        Assert.That(GmRunStore.HasStudyMatch, Is.True);
        Assert.That(GmRunStore.StudyRestoreError, Is.Not.Empty);
        var controller = new GmStudyController();
        Assert.That(controller.InitializeOrRestore(),
            Is.EqualTo(GmStudyInitializeResult.CorruptSavedState));
    }

    [TestCase("interventionReceipt")]
    [TestCase("session-intervention")]
    public void ExplicitEmptyNestedStudyEvidenceRemainsCorruptThroughRealJson(string target)
    {
        var match = new GmStudyMatch(19UL);
        var data = new GmSaveData
        {
            studyEnvelopeVersion = 1,
            studyPayloadPresent = true,
            studyMatch = match.ExportSnapshot()
        };
        string json;
        if (target == "interventionReceipt")
        {
            data.studyTurnEvidencePresent = true;
            data.studyMatch.interventionReceipt = new GmStudyInterventionReceipt();
            json = data.ToJson();
        }
        else
        {
            json = data.ToJson().Replace("\"studySessionEventPresent\":false",
                "\"studySessionEventPresent\":true");
            int studyMatchStart = IndexOfToken(json, "studyMatch", 0);
            json = InsertObject(json, "session", "\"intervention\":{},", studyMatchStart);
        }
        File.WriteAllText(path, json);

        Assert.That(GmSaveSystem.Load(), Is.True, GmSaveSystem.LastError);
        Assert.That(GmRunStore.HasStudyMatch, Is.True);
        Assert.That(GmRunStore.StudyRestoreError, Is.Not.Empty);
    }

    [TestCase("payload-false-turn-bit")]
    [TestCase("payload-false-session-bit")]
    [TestCase("payload-false-outer-present")]
    [TestCase("payload-true-outer-absent")]
    [TestCase("turn-true-object-absent")]
    [TestCase("turn-false-object-present")]
    [TestCase("session-true-object-absent")]
    [TestCase("session-false-object-present")]
    public void ModernEnvelopeContradictionsFailClosedFromPhysicalJson(string scenario)
    {
        File.WriteAllText(path, ContradictoryJson(scenario));

        Assert.That(GmSaveSystem.Load(), Is.True, GmSaveSystem.LastError);
        StringAssert.Contains("envelope", GmRunStore.StudyRestoreError.ToLowerInvariant());
        var controller = new GmStudyController();
        Assert.That(controller.InitializeOrRestore(),
            Is.EqualTo(GmStudyInitializeResult.CorruptSavedState));
    }

    [TestCase("fresh")]
    [TestCase("pending")]
    [TestCase("resolved")]
    [TestCase("complete")]
    public void ValidModernEnvelopeRestoresEveryStudyLifecycleShape(string state)
    {
        GmStudyMatchSnapshot snapshot = ValidSnapshot(state);
        var data = new GmSaveData
        {
            studyEnvelopeVersion = 1,
            studyPayloadPresent = true,
            studyTurnEvidencePresent = snapshot.interventionReceipt != null,
            studySessionEventPresent = snapshot.session?.intervention != null,
            studyMatch = snapshot
        };
        File.WriteAllText(path, data.ToJson());

        Assert.That(GmSaveSystem.Load(), Is.True, GmSaveSystem.LastError);
        Assert.That(GmRunStore.StudyRestoreError, Is.Empty);
        Assert.That(GmRunStore.GetStudyMatchSnapshot().stateFingerprint,
            Is.EqualTo(snapshot.stateFingerprint));
    }

    static string ContradictoryJson(string scenario)
    {
        var data = new GmSaveData
        {
            studyEnvelopeVersion = 1,
            studyPayloadPresent = true,
            studyMatch = new GmStudyMatch(31UL).ExportSnapshot()
        };
        string json = data.ToJson();
        switch (scenario)
        {
            case "payload-false-turn-bit":
                return "{\"studyEnvelopeVersion\":1,\"studyPayloadPresent\":false," +
                    "\"studyTurnEvidencePresent\":true}";
            case "payload-false-session-bit":
                return "{\"studyEnvelopeVersion\":1,\"studyPayloadPresent\":false," +
                    "\"studySessionEventPresent\":true}";
            case "payload-false-outer-present":
                return json.Replace("\"studyPayloadPresent\":true",
                    "\"studyPayloadPresent\":false");
            case "payload-true-outer-absent":
                return "{\"studyEnvelopeVersion\":1,\"studyPayloadPresent\":true}";
            case "turn-true-object-absent":
                return json.Replace("\"studyTurnEvidencePresent\":false",
                    "\"studyTurnEvidencePresent\":true");
            case "turn-false-object-present":
                return InsertObject(json, "studyMatch", "\"interventionReceipt\":{},");
            case "session-true-object-absent":
                return json.Replace("\"studySessionEventPresent\":false",
                    "\"studySessionEventPresent\":true");
            case "session-false-object-present":
            {
                int studyMatchStart = IndexOfToken(json, "studyMatch", 0);
                return InsertObject(json, "session", "\"intervention\":{},", studyMatchStart);
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }
    }

    static string InsertObject(string json, string owner, string property, int searchFrom = 0)
    {
        string token = "\"" + owner + "\":{";
        int index = json.IndexOf(token, searchFrom, StringComparison.Ordinal);
        Assert.That(index, Is.GreaterThanOrEqualTo(0));
        return json.Insert(index + token.Length, property);
    }

    static int IndexOfToken(string json, string owner, int searchFrom)
    {
        string token = "\"" + owner + "\":{";
        int index = json.IndexOf(token, searchFrom, StringComparison.Ordinal);
        Assert.That(index, Is.GreaterThanOrEqualTo(0));
        return index;
    }

    static GmStudyMatchSnapshot ValidSnapshot(string state)
    {
        if (state == "fresh") return new GmStudyMatch(41UL).ExportSnapshot();
        if (state == "complete")
        {
            var complete = new GmStudyMatch(43UL);
            complete.TryChoose(WrongActionId(0), out _);
            complete.TryChoose(WrongActionId(1), out _);
            complete.TryChoose(WrongActionId(2), out _);
            return complete.ExportSnapshot();
        }

        var intervention = new GmStudyMatch(3UL);
        intervention.TryChoose(CorrectActionId(0), out _);
        intervention.TryChoose(CorrectActionId(1), out _);
        if (state == "resolved") intervention.TryResolveIntervention(true, out _);
        else if (state != "pending")
            throw new ArgumentOutOfRangeException(nameof(state), state, null);
        return intervention.ExportSnapshot();
    }

    static string CorrectActionId(int positionIndex) =>
        GmStudyRules.GetPosition(positionIndex).cards.First(card => card.isCorrect).actionId;

    static string WrongActionId(int positionIndex) =>
        GmStudyRules.GetPosition(positionIndex).cards.First(card => !card.isCorrect).actionId;
}
