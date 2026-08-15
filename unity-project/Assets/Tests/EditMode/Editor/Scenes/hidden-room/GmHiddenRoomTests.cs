using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class GmHiddenRoomTests
{
    [SetUp]
    public void SetUp()
    {
        GmRunStore.BeginNewRun();
    }

    [Test]
    public void ReadOriginalInvitationRecordsCatchAndSanity()
    {
        var roomObj = new GameObject("TestHiddenRoom");
        var room = roomObj.AddComponent<GmHiddenRoomController>();

        // A fresh run starts at full sanity and ApplySanityDelta clamps at 1, so the reward is
        // invisible unless the run has something to recover. Take a hit first, then measure.
        GmRunStore.ApplySanityDelta(-0.5f);
        float initialSanity = GmRunStore.Sanity;
        bool ok = room.ReadOriginalInvitation();

        Assert.IsTrue(ok);
        Assert.IsTrue(room.InvitationRead);
        Assert.IsTrue(GmRunStore.CheatsCaught.Contains("hidden-room-original-invitation"));
        Assert.Greater(GmRunStore.Sanity, initialSanity);

        // Subsequent read is idempotent
        Assert.IsFalse(room.ReadOriginalInvitation());

        Object.DestroyImmediate(roomObj);
    }

    [Test]
    public void CollectShardThreeUpdatesRunStore()
    {
        var roomObj = new GameObject("TestHiddenRoom");
        var room = roomObj.AddComponent<GmHiddenRoomController>();

        bool ok = room.CollectShardThree();
        Assert.IsTrue(ok);
        Assert.IsTrue(room.ShardThreeCollected);
        Assert.IsTrue(GmRunStore.MirrorShards[2]); // Shard 3 is at index 2

        Object.DestroyImmediate(roomObj);
    }

    [Test]
    public void CollectingAllThreeShardsAssemblesMirror()
    {
        var roomObj = new GameObject("TestHiddenRoom");
        var room = roomObj.AddComponent<GmHiddenRoomController>();

        // Collect shards 1 and 2 first
        GmRunStore.CollectShard(0);
        GmRunStore.CollectShard(1);
        Assert.IsFalse(room.MirrorAssembled);

        // Collect shard 3
        room.CollectShardThree();
        Assert.IsTrue(room.MirrorAssembled);
        Assert.IsTrue(GmRunStore.AllShardsCollected);
        Assert.IsTrue(GmRunStore.CheatsCaught.Contains("mirror-fully-assembled"));

        Object.DestroyImmediate(roomObj);
    }

    [Test]
    public void ReEnteringTheRoomDoesNotRepeatRewardsOrCompletion()
    {
        GmRunStore.ApplySanityDelta(-0.5f);
        var firstVisit = new GameObject("TestHiddenRoom");
        var first = firstVisit.AddComponent<GmHiddenRoomController>();
        GmRunStore.CollectShard(0);
        GmRunStore.CollectShard(1);
        Assert.IsTrue(first.ReadOriginalInvitation());
        Assert.IsTrue(first.InspectJournal(3));
        Assert.IsTrue(first.CollectShardThree());
        float sanityAfterFirstVisit = GmRunStore.Sanity;
        Object.DestroyImmediate(firstVisit);

        // Leaving and re-entering loads the scene again, which builds a new controller against the
        // same run. Nothing it owns may be earned twice.
        var secondVisit = new GameObject("TestHiddenRoomAgain");
        var second = secondVisit.AddComponent<GmHiddenRoomController>();
        int completions = 0;
        second.OnMirrorCompleted += () => completions++;

        Assert.IsTrue(second.InvitationRead, "the run already holds the invitation catch");
        Assert.IsTrue(second.ShardThreeCollected, "the run already holds shard #3");
        Assert.AreEqual(1, second.JournalsInspected, "journal progress belongs to the run, not the visit");
        Assert.IsFalse(second.ReadOriginalInvitation());
        Assert.IsFalse(second.InspectJournal(3));
        Assert.IsFalse(second.CollectShardThree());
        Assert.AreEqual(sanityAfterFirstVisit, GmRunStore.Sanity, 0.0001f,
            "re-entry must not pay the sanity reward a second time");
        Assert.AreEqual(0, completions, "the true-escape unlock must not re-fire on re-entry");

        Object.DestroyImmediate(secondVisit);
    }

    [Test]
    public void InspectJournalsTracksEightVictims()
    {
        var roomObj = new GameObject("TestHiddenRoom");
        var room = roomObj.AddComponent<GmHiddenRoomController>();

        for (int i = 1; i <= 8; i++)
        {
            Assert.IsTrue(room.InspectJournal(i));
            Assert.IsTrue(GmRunStore.CheatsCaught.Contains($"hidden-room-journal-{i}"));
        }

        Assert.AreEqual(8, room.JournalsInspected);
        Assert.IsFalse(room.InspectJournal(9)); // only 8 previous victims (9 is Percival)

        Object.DestroyImmediate(roomObj);
    }
}
