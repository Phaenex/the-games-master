using System.Linq;
using System.Reflection;
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
    public void ExistingCourtSceneControllerCanBootstrapPlayerFacingComponents()
    {
        var courtObj = new GameObject("TestCourt");
        try
        {
            GmCourtController court = courtObj.AddComponent<GmCourtController>();

            court.EnsurePresentationComponents();
            court.EnsurePresentationComponents();

            Assert.That(courtObj.GetComponents<GmCourtHud>(), Has.Length.EqualTo(1));
            Assert.That(courtObj.GetComponents<GmCourtInput>(), Has.Length.EqualTo(1));
            Assert.That(courtObj.GetComponents<GmCourtPresenter>(), Has.Length.EqualTo(1));
        }
        finally { Object.DestroyImmediate(courtObj); }
    }

    [Test]
    public void HearingOwnsPointerAndMovementUntilEitherVerdict()
    {
        var playerObj = new GameObject("Player");
        var courtObj = new GameObject("TestCourt");
        try
        {
            GmPlayer player = playerObj.AddComponent<GmPlayer>();
            GmCourtController court = courtObj.AddComponent<GmCourtController>();

            court.StartHearing();
            Assert.IsTrue(player.ControlBlocked);
            court.PresentEvidence("one", false);
            court.PresentEvidence("two", false);
            court.PresentEvidence("three", false);

            Assert.AreEqual(GmCourtPhase.Verdict, court.Phase);
            Assert.IsFalse(player.ControlBlocked);
        }
        finally
        {
            Object.DestroyImmediate(courtObj);
            Object.DestroyImmediate(playerObj);
        }
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
    public void PlayerFacingThreeArgumentHearingIsWinnableWithDistinctEvidence()
    {
        var courtObj = new GameObject("TestCourt");
        var court = courtObj.AddComponent<GmCourtController>();
        try
        {
            court.StartHearing();
            court.SelectEvidence(2); // Percival register
            Assert.IsTrue(court.PresentSelectedEvidence());
            court.SelectEvidence(1); // matching wax fault
            Assert.IsTrue(court.PresentSelectedEvidence());
            court.SelectEvidence(4); // impossible latch schedule
            Assert.IsTrue(court.PresentSelectedEvidence());

            Assert.AreEqual(GmCourtPhase.Verdict, court.Phase);
            Assert.AreEqual(0, court.WaxSealsRemaining);
            Assert.IsTrue(GmRunStore.IsRoomComplete("court"));
        }
        finally { Object.DestroyImmediate(courtObj); }
    }

    [Test]
    public void DecoyCostsPressureAndCannotBeEnteredTwice()
    {
        var courtObj = new GameObject("TestCourt");
        var court = courtObj.AddComponent<GmCourtController>();
        try
        {
            court.StartHearing();
            float before = court.PressureTimeRemaining;
            court.SelectEvidence(0);
            Assert.IsFalse(court.PresentSelectedEvidence());
            Assert.AreEqual(before - 12f, court.PressureTimeRemaining, 0.001f);
            Assert.IsFalse(court.PresentSelectedEvidence());
            Assert.AreEqual(before - 12f, court.PressureTimeRemaining, 0.001f,
                "re-entering one decoy charged the pressure penalty twice");
        }
        finally { Object.DestroyImmediate(courtObj); }
    }

    [Test]
    public void EvidenceRejectedTooEarlyRemainsAvailableForItsActualArgument()
    {
        var courtObj = new GameObject("TestCourt");
        var court = courtObj.AddComponent<GmCourtController>();
        try
        {
            court.StartHearing();
            court.SelectEvidence(1); // Wax belongs to argument two, not argument one.
            Assert.IsFalse(court.PresentSelectedEvidence());

            court.SelectEvidence(2);
            Assert.IsTrue(court.PresentSelectedEvidence());
            court.SelectEvidence(1);
            Assert.IsTrue(court.PresentSelectedEvidence(),
                "a premature attempt permanently consumed evidence needed by a later argument");
            court.SelectEvidence(4);
            Assert.IsTrue(court.PresentSelectedEvidence());
            Assert.AreEqual(GmCourtPhase.Verdict, court.Phase);
        }
        finally { Object.DestroyImmediate(courtObj); }
    }

    [Test]
    public void ReactiveRiggingWaitsUntilAldricIsAboutToLoseAndStillAllowsTheVerdict()
    {
        var courtObj = new GameObject("TestCourt");
        var court = courtObj.AddComponent<GmCourtController>();
        try
        {
            GmRunStore.RaiseCorruption("test tier 2");
            GmRunStore.RaiseCorruption("test tier 3");
            court.StartHearing();
            court.SelectEvidence(2); court.PresentSelectedEvidence();
            Assert.IsFalse(court.GavelIsTarnished);
            court.SelectEvidence(1); court.PresentSelectedEvidence();
            Assert.IsFalse(court.GavelIsTarnished);
            court.SelectEvidence(4); court.PresentSelectedEvidence();

            Assert.IsTrue(court.GavelIsTarnished);
            Assert.AreEqual(GmCourtPhase.Verdict, court.Phase);
            Assert.IsTrue(GmRunStore.HasCatch("court-rigged-evidence-tell"));
        }
        finally { Object.DestroyImmediate(courtObj); }
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
        Assert.IsTrue(GmRunStore.IsRoomComplete("court"));
        Assert.AreEqual(0, GmRunStore.TableGameIndex,
            "the Court is a trial, not one of the seven table games");

        Object.DestroyImmediate(courtObj);
    }

    [Test]
    public void LosingTheHearingStillCompletesTheTrialInsteadOfSoftLockingTheNight()
    {
        var courtObj = new GameObject("TestCourt");
        var court = courtObj.AddComponent<GmCourtController>();
        try
        {
            court.StartHearing();
            FieldInfo clock = typeof(GmCourtController).GetField("<PressureTimeRemaining>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(clock);
            clock.SetValue(court, 0f);
            MethodInfo update = typeof(GmCourtController).GetMethod("Update",
                BindingFlags.Instance | BindingFlags.NonPublic);
            update.Invoke(court, null);

            Assert.AreEqual(GmCourtPhase.Verdict, court.Phase);
            Assert.IsTrue(GmRunStore.IsRoomComplete("court"));
            Assert.AreEqual(0, GmRunStore.TableGameIndex);
        }
        finally { Object.DestroyImmediate(courtObj); }
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
