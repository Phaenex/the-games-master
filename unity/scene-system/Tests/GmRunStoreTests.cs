using System;
using NUnit.Framework;
using UnityEngine;

public sealed class GmRunStoreTests
{
    [SetUp]
    public void SetUp()
    {
        GmRunStore.BeginNewRun();
    }

    [Test]
    public void FreshRunInitializesToFloorAndFullSanity()
    {
        Assert.AreEqual(1, GmRunStore.CorruptionTier);
        Assert.AreEqual(1.0f, GmRunStore.Sanity, 0.001f);
        Assert.AreEqual(0, GmRunStore.CheatsCaughtCount);
        Assert.AreEqual(0, GmRunStore.ShardsCount);
        Assert.AreEqual(0, GmRunStore.Defiance);
        Assert.AreEqual(0, GmRunStore.Compliance);
    }

    [Test]
    public void RecordingCatchIsIdempotent()
    {
        Assert.IsTrue(GmRunStore.RecordCatch("parlor-palm-trick-4"));
        Assert.AreEqual(1, GmRunStore.CheatsCaughtCount);

        // Duplicate clue should not double bank
        Assert.IsFalse(GmRunStore.RecordCatch("parlor-palm-trick-4"));
        Assert.AreEqual(1, GmRunStore.CheatsCaughtCount);

        // Second unique clue increments
        Assert.IsTrue(GmRunStore.RecordCatch("shutbox-false-call-tile7"));
        Assert.AreEqual(2, GmRunStore.CheatsCaughtCount);
    }

    [Test]
    public void CorruptionClampedBetweenOneAndFive()
    {
        // Cannot drop below 1
        GmRunStore.LowerCorruption("test");
        Assert.AreEqual(1, GmRunStore.CorruptionTier);

        // Raise up to ceiling
        for (int i = 0; i < 10; i++) GmRunStore.RaiseCorruption("test");
        Assert.AreEqual(5, GmRunStore.CorruptionTier);

        // Lower back down
        GmRunStore.LowerCorruption("test");
        Assert.AreEqual(4, GmRunStore.CorruptionTier);
    }

    [Test]
    public void SanityClampedBetweenZeroAndOne()
    {
        GmRunStore.ApplySanityDelta(-0.5f);
        Assert.AreEqual(0.5f, GmRunStore.Sanity, 0.001f);

        GmRunStore.ApplySanityDelta(-0.8f);
        Assert.AreEqual(0.0f, GmRunStore.Sanity, 0.001f);

        GmRunStore.ApplySanityDelta(1.5f);
        Assert.AreEqual(1.0f, GmRunStore.Sanity, 0.001f);
    }

    [Test]
    public void ShardCollectionTracksDistinctShards()
    {
        Assert.IsFalse(GmRunStore.HasShard(0));
        Assert.IsTrue(GmRunStore.CollectShard(0));
        Assert.IsTrue(GmRunStore.HasShard(0));
        Assert.AreEqual(1, GmRunStore.ShardsCount);

        // Duplicate shard collection
        Assert.IsFalse(GmRunStore.CollectShard(0));
        Assert.AreEqual(1, GmRunStore.ShardsCount);

        // Out of bounds shard
        Assert.IsFalse(GmRunStore.CollectShard(5));
        Assert.IsFalse(GmRunStore.HasShard(5));

        // Collect shards 1 and 2
        Assert.IsTrue(GmRunStore.CollectShard(1));
        Assert.IsTrue(GmRunStore.CollectShard(2));
        Assert.AreEqual(3, GmRunStore.ShardsCount);
    }

    // The Parlor, Shut the Box, Court and Hidden Room controllers all refresh off OnStateChanged.
    // Asserting the resulting property alone cannot see a mutator that stops raising it, which
    // leaves those four rooms displaying a run that has already moved on.
    [Test]
    public void EveryRealMutationTellsTheRoomsOnceThatTheRunMoved()
    {
        int raised = 0;
        Action listener = () => raised++;
        GmRunStore.OnStateChanged += listener;
        try
        {
            GmRunStore.RecordCatch("parlor-palm-trick-4");
            Assert.AreEqual(1, raised, "a banked catch never reached the room controllers");

            GmRunStore.CollectShard(1);
            Assert.AreEqual(2, raised, "a recovered shard never reached the room controllers");

            GmRunStore.RecordClue("ledger-margin-note");
            Assert.AreEqual(3, raised, "a discovered clue never reached the room controllers");

            GmRunStore.RaiseCorruption("state event test");
            Assert.AreEqual(4, raised);
            GmRunStore.LowerCorruption("state event test");
            Assert.AreEqual(5, raised);

            GmRunStore.ApplySanityDelta(-0.10f);
            Assert.AreEqual(6, raised);
            GmRunStore.RecordDefiance();
            Assert.AreEqual(7, raised);
            GmRunStore.RecordCompliance();
            Assert.AreEqual(8, raised);

            // A miss costs sanity through ApplySanityDelta, which already raises the event; a second
            // invoke here would double-refresh every subscriber for one miss.
            GmRunStore.RecordMiss();
            Assert.AreEqual(9, raised, "a miss fired the reactive event a different number of times than once");
            Assert.AreEqual(0.85f, GmRunStore.Sanity, 0.001f, "a miss cost the run the wrong amount of sanity");

            GmRunStore.BeginNewRun();
            Assert.AreEqual(10, raised, "a fresh run left the rooms showing the previous one");
        }
        finally
        {
            GmRunStore.OnStateChanged -= listener;
        }
    }

    [Test]
    public void RefusedMutationsLeaveSubscribersAlone()
    {
        GmRunStore.RecordCatch("parlor-palm-trick-4");
        GmRunStore.CollectShard(0);

        int raised = 0;
        Action listener = () => raised++;
        GmRunStore.OnStateChanged += listener;
        try
        {
            Assert.IsFalse(GmRunStore.RecordCatch("parlor-palm-trick-4"));
            Assert.IsFalse(GmRunStore.RecordCatch("   "));
            Assert.IsFalse(GmRunStore.CollectShard(0));
            Assert.IsFalse(GmRunStore.CollectShard(7));
            Assert.IsFalse(GmRunStore.RecordClue(null));
            GmRunStore.LowerCorruption("already at the floor");
            for (int i = 0; i < 4; i++) GmRunStore.RaiseCorruption("climb to the ceiling");
            int atCeiling = raised;
            GmRunStore.RaiseCorruption("already at the ceiling");

            Assert.AreEqual(1, GmRunStore.CheatsCaughtCount);
            Assert.AreEqual(1, GmRunStore.ShardsCount);
            Assert.AreEqual(GmRunStore.MaxCorruptionTier, GmRunStore.CorruptionTier);
            Assert.AreEqual(4, atCeiling, "a refused mutation still woke every subscriber");
            Assert.AreEqual(atCeiling, raised, "corruption re-announced a tier it never left");
        }
        finally
        {
            GmRunStore.OnStateChanged -= listener;
        }
    }

    [Test]
    public void CorruptionStartsAtTierOneAndNeverReturnsToZero()
    {
        Assert.AreEqual(1, GmRunStore.MinCorruptionTier);
        Assert.AreEqual(1, GmRunStore.CorruptionTier, "a fresh run began below the canonical Tier 1 floor");

        for (int i = 0; i < 5; i++) GmRunStore.LowerCorruption("floor test");
        Assert.AreEqual(1, GmRunStore.CorruptionTier);

        // A save that claims Tier 0 is a corrupt file, not a valid state to restore into.
        GmRunStore.LoadFromSaveData(new GmSaveData { corruptionTier = 0 });
        Assert.AreEqual(1, GmRunStore.CorruptionTier);
    }

    [Test]
    public void ClueLedgerStaysSeparateFromTheCatchTally()
    {
        Assert.IsTrue(GmRunStore.RecordClue("hall-portrait-ninth-guest"));
        Assert.IsFalse(GmRunStore.RecordClue("hall-portrait-ninth-guest"));
        Assert.IsTrue(GmRunStore.HasClue("  hall-portrait-ninth-guest  "));
        Assert.AreEqual(1, GmRunStore.DiscoveredCluesCount);
        Assert.AreEqual(0, GmRunStore.CheatsCaughtCount,
            "reading material counted toward the eight catches the true ending requires");

        Assert.IsTrue(GmRunStore.RecordCatch("hall-portrait-ninth-guest"));
        Assert.AreEqual(1, GmRunStore.CheatsCaughtCount);
        Assert.AreEqual(1, GmRunStore.DiscoveredCluesCount);
    }

    [Test]
    public void SaveDataRoundTripPreservesExactState()
    {
        GmRunStore.RaiseCorruption("test-raise");
        GmRunStore.RaiseCorruption("test-raise");
        GmRunStore.ApplySanityDelta(-0.35f);
        GmRunStore.RecordCatch("clue-1");
        GmRunStore.RecordCatch("clue-2");
        GmRunStore.CollectShard(0);
        GmRunStore.CollectShard(2);
        GmRunStore.RecordDefiance();
        GmRunStore.RecordCompliance();
        GmRunStore.CurrentSceneId = "entry-hall";

        GmSaveData saved = GmRunStore.ToSaveData();

        // Reset store
        GmRunStore.BeginNewRun();
        Assert.AreEqual(1, GmRunStore.CorruptionTier);
        Assert.AreEqual(0, GmRunStore.CheatsCaughtCount);

        // Restore
        GmRunStore.LoadFromSaveData(saved);
        Assert.AreEqual(3, GmRunStore.CorruptionTier);
        Assert.AreEqual(0.65f, GmRunStore.Sanity, 0.001f);
        Assert.AreEqual(2, GmRunStore.CheatsCaughtCount);
        Assert.AreEqual(2, GmRunStore.ShardsCount);
        Assert.IsTrue(GmRunStore.HasShard(0));
        Assert.IsFalse(GmRunStore.HasShard(1));
        Assert.IsTrue(GmRunStore.HasShard(2));
        Assert.AreEqual(1, GmRunStore.Defiance);
        Assert.AreEqual(1, GmRunStore.Compliance);
        Assert.AreEqual("entry-hall", GmRunStore.CurrentSceneId);
    }
}
