using NUnit.Framework;
using UnityEngine;

public class GmEndingTests
{
    [SetUp]
    public void SetUp()
    {
        GmRunStore.BeginNewRun();
    }

    [Test]
    public void DefaultRunResolvesTrappedLoop()
    {
        GmEndingType ending = GmEndingManager.ResolveEnding();
        Assert.AreEqual(GmEndingType.TrappedLoop, ending);
    }

    [Test]
    public void ZeroSanityResolvesMadness()
    {
        GmRunStore.ApplySanityDelta(-1.0f);
        Assert.AreEqual(0.0f, GmRunStore.Sanity);
        Assert.AreEqual(GmEndingType.Madness, GmEndingManager.ResolveEnding());
    }

    [Test]
    public void TierFiveCorruptionResolvesCorruptedHost()
    {
        for (int i = 0; i < 6; i++) GmRunStore.RaiseCorruption("Max corruption test");
        Assert.AreEqual(5, GmRunStore.CorruptionTier);
        Assert.AreEqual(GmEndingType.CorruptedHost, GmEndingManager.ResolveEnding());
    }

    [Test]
    public void AllShardsAndEightCatchesResolvesTrueEscape()
    {
        GmRunStore.CollectShard(0);
        GmRunStore.CollectShard(1);
        GmRunStore.CollectShard(2);

        for (int i = 1; i <= 8; i++)
        {
            GmRunStore.RecordCatch($"catch-{i}");
        }

        Assert.IsTrue(GmRunStore.AllShardsCollected);
        Assert.AreEqual(8, GmRunStore.CheatsCaughtCount);
        Assert.AreEqual(GmEndingType.TrueEscape, GmEndingManager.ResolveEnding());
    }

    [Test]
    public void HighDefianceResolvesDefiantSacrifice()
    {
        GmRunStore.RecordDefiance();
        GmRunStore.RecordDefiance();
        Assert.AreEqual(2, GmRunStore.DefianceCount);
        Assert.AreEqual(0, GmRunStore.ComplianceCount);
        Assert.AreEqual(GmEndingType.DefiantSacrifice, GmEndingManager.ResolveEnding());
    }

    [Test]
    public void HighComplianceResolvesHostSuccession()
    {
        GmRunStore.RecordCompliance();
        GmRunStore.RecordCompliance();
        Assert.AreEqual(0, GmRunStore.DefianceCount);
        Assert.AreEqual(2, GmRunStore.ComplianceCount);
        Assert.AreEqual(GmEndingType.HostSuccession, GmEndingManager.ResolveEnding());
    }

    // The six tests above each trigger one condition alone, which proves every branch is reachable
    // and nothing about the order they are asked in. A run can satisfy several at once, and the
    // authored chain is Madness > CorruptedHost > TrueEscape > DefiantSacrifice > HostSuccession.

    [Test]
    public void MadnessOutranksEveryOtherSatisfiedEnding()
    {
        GoldenRun();
        for (int i = 0; i < 6; i++) GmRunStore.RaiseCorruption("priority test");
        GmRunStore.RecordDefiance();
        GmRunStore.ApplySanityDelta(-1.0f);

        Assert.AreEqual(0.0f, GmRunStore.Sanity);
        Assert.AreEqual(5, GmRunStore.CorruptionTier);
        Assert.IsTrue(GmRunStore.AllShardsCollected);
        Assert.AreEqual(GmEndingType.Madness, GmEndingManager.ResolveEnding(),
            "a lost mind was outvoted by a later branch");
    }

    [Test]
    public void CorruptedHostOutranksATrueEscapeEarnedInTheSameRun()
    {
        GoldenRun();
        for (int i = 0; i < 6; i++) GmRunStore.RaiseCorruption("priority test");

        Assert.AreEqual(5, GmRunStore.CorruptionTier);
        Assert.AreEqual(8, GmRunStore.CheatsCaughtCount);
        Assert.IsTrue(GmRunStore.AllShardsCollected);
        Assert.AreEqual(GmEndingType.CorruptedHost, GmEndingManager.ResolveEnding(),
            "a fully corrupted host still walked out through the true ending");
    }

    [Test]
    public void TrueEscapeOutranksBothStanceTallies()
    {
        GoldenRun();
        GmRunStore.RecordDefiance();
        GmRunStore.RecordDefiance();

        Assert.Greater(GmRunStore.DefianceCount, GmRunStore.ComplianceCount);
        Assert.AreEqual(GmEndingType.TrueEscape, GmEndingManager.ResolveEnding());

        // And the same run read from the other stance.
        GmRunStore.RecordCompliance();
        GmRunStore.RecordCompliance();
        GmRunStore.RecordCompliance();
        Assert.Greater(GmRunStore.ComplianceCount, GmRunStore.DefianceCount);
        Assert.AreEqual(GmEndingType.TrueEscape, GmEndingManager.ResolveEnding());
    }

    [Test]
    public void StanceEndingsFollowTheLeadingTallyAndATieFallsBackToTheLoop()
    {
        GmRunStore.RecordDefiance();
        GmRunStore.RecordDefiance();
        GmRunStore.RecordCompliance();
        Assert.AreEqual(GmEndingType.DefiantSacrifice, GmEndingManager.ResolveEnding());

        GmRunStore.RecordCompliance();
        Assert.AreEqual(2, GmRunStore.DefianceCount);
        Assert.AreEqual(2, GmRunStore.ComplianceCount);
        Assert.AreEqual(GmEndingType.TrappedLoop, GmEndingManager.ResolveEnding(),
            "an even run picked a stance it never took");

        GmRunStore.RecordCompliance();
        Assert.AreEqual(GmEndingType.HostSuccession, GmEndingManager.ResolveEnding());
    }

    [Test]
    public void EveryEndingCarriesItsOwnTitle()
    {
        var titles = new System.Collections.Generic.HashSet<string>();
        foreach (GmEndingType ending in System.Enum.GetValues(typeof(GmEndingType)))
        {
            string title = GmEndingManager.GetEndingTitle(ending);
            Assert.IsFalse(string.IsNullOrWhiteSpace(title), $"{ending} has no title");
            Assert.IsTrue(titles.Add(title), $"{ending} reuses the title '{title}'");
        }
        Assert.AreEqual(6, titles.Count);
    }

    // All three shards and the eight catches the true ending requires, and nothing else.
    static void GoldenRun()
    {
        GmRunStore.CollectShard(0);
        GmRunStore.CollectShard(1);
        GmRunStore.CollectShard(2);
        for (int i = 1; i <= 8; i++) GmRunStore.RecordCatch($"catch-{i}");
    }
}
