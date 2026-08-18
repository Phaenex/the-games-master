using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class GmParlorEvidenceLogTests
{
    GameObject root;
    GmParlorEvidenceLog log;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("EvidenceLogTest");
        log = root.AddComponent<GmParlorEvidenceLog>();
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
    }

    [Test]
    public void RecordsObservedFactsNeverConclusionsAndDeduplicatesCommandReplay()
    {
        Assert.That(log.Record(17, GmParlorObservedFact.RightHandPausedAboveDeck), Is.True);
        Assert.That(log.Record(17, GmParlorObservedFact.RightHandPausedAboveDeck), Is.False);
        Assert.That(log.Record(17, GmParlorObservedFact.CardContactBroke), Is.True);

        Assert.That(log.Facts, Has.Count.EqualTo(2));
        Assert.That(log.Facts.Select(fact => fact.CommandId), Is.All.EqualTo(17));
        foreach (GmParlorEvidenceFact fact in log.Facts)
        {
            string lower = fact.Text.ToLowerInvariant();
            StringAssert.DoesNotContain("cheat", lower);
            StringAssert.DoesNotContain("guilty", lower);
            StringAssert.DoesNotContain("honest", lower);
        }
    }

    [Test]
    public void KeepsBoundedRecentHistoryWithoutChangingFactIdentity()
    {
        for (ulong id = 1; id <= GmParlorEvidenceLog.Capacity + 3UL; id++)
            Assert.That(log.Record(id, GmParlorObservedFact.SleeveBrushedTable), Is.True);

        Assert.That(log.Facts, Has.Count.EqualTo(GmParlorEvidenceLog.Capacity));
        Assert.That(log.Facts[0].CommandId, Is.EqualTo(4));
        Assert.That(log.Facts[^1].CommandId,
            Is.EqualTo((ulong)GmParlorEvidenceLog.Capacity + 3UL));
        Assert.That(log.Facts, Has.All.Matches<GmParlorEvidenceFact>(fact =>
            fact.Fact == GmParlorObservedFact.SleeveBrushedTable));
    }
}
