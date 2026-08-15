using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class GmCourtEvidenceTests
{
    [SetUp]
    public void SetUp()
    {
        GmRunStore.BeginNewRun();
    }

    [Test]
    public void StartHearingInitializesThreeSealsAndFullClock()
    {
        var courtObj = new GameObject("TestCourt");
        var court = courtObj.AddComponent<GmCourtController>();

        court.StartHearing();
        Assert.AreEqual(3, court.WaxSealsRemaining);
        Assert.AreEqual(90.0f, court.PressureTimeRemaining);
        Assert.IsFalse(court.GavelIsTarnished);
        Assert.AreEqual(GmCourtPhase.PlayerDefense, court.Phase);

        Object.DestroyImmediate(courtObj);
    }

    [Test]
    public void TrueEvidenceCracksWaxSeal()
    {
        var courtObj = new GameObject("TestCourt");
        var court = courtObj.AddComponent<GmCourtController>();

        court.StartHearing();
        bool ok = court.PresentEvidence("alibi-ledger-page", false);
        Assert.IsTrue(ok);
        Assert.AreEqual(2, court.WaxSealsRemaining);
        Assert.IsFalse(court.GavelIsTarnished);

        Object.DestroyImmediate(courtObj);
    }

    [Test]
    public void RiggedEvidenceTarnishesGavelAndRecordsCatch()
    {
        var courtObj = new GameObject("TestCourt");
        var court = courtObj.AddComponent<GmCourtController>();

        court.StartHearing();
        bool ok = court.PresentEvidence("forged-will", true);
        Assert.IsTrue(ok);
        Assert.IsTrue(court.GavelIsTarnished);
        Assert.IsTrue(GmRunStore.CheatsCaught.Contains("court-rigged-evidence-tell"));

        Object.DestroyImmediate(courtObj);
    }

    [Test]
    public void ThreeTrueEvidenceSubmissionsWinsHearingAndRecordsDefiance()
    {
        var courtObj = new GameObject("TestCourt");
        var court = courtObj.AddComponent<GmCourtController>();

        court.StartHearing();
        court.PresentEvidence("evidence-1", false);
        court.PresentEvidence("evidence-2", false);
        court.PresentEvidence("evidence-3", false);

        Assert.AreEqual(0, court.WaxSealsRemaining);
        Assert.AreEqual(GmCourtPhase.Verdict, court.Phase);
        Assert.IsTrue(GmRunStore.CheatsCaught.Contains("court-verdict-cleared"));
        Assert.AreEqual(1, GmRunStore.DefianceCount);

        Object.DestroyImmediate(courtObj);
    }

    [Test]
    public void CollectingShardTwoUpdatesRunStore()
    {
        var courtObj = new GameObject("TestCourt");
        var court = courtObj.AddComponent<GmCourtController>();

        court.StartHearing();
        bool collected = court.CollectEvidenceShard();
        Assert.IsTrue(collected);
        Assert.IsTrue(GmRunStore.MirrorShards[1]);

        // Attempting to collect again returns false (idempotent)
        Assert.IsFalse(court.CollectEvidenceShard());

        Object.DestroyImmediate(courtObj);
    }
}
