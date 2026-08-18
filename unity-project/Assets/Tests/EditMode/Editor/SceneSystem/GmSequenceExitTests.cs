using NUnit.Framework;
using UnityEngine;

public sealed class GmSequenceExitTests
{
    [SetUp]
    public void SetUp() => GmRunStore.BeginNewRun();

    [Test]
    public void ExitStaysPhysicallyShutUntilItsRoomIsCompleteThenUnlocksOnce()
    {
        var root = new GameObject("Exit");
        var triggerObject = new GameObject("Transition");
        triggerObject.transform.SetParent(root.transform, false);
        var triggerCollider = triggerObject.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        var trigger = triggerObject.AddComponent<GmSceneTransitionTrigger>();
        var blocker = root.AddComponent<BoxCollider>();
        var left = new GameObject("LeftHinge").transform;
        left.SetParent(root.transform, false);
        var right = new GameObject("RightHinge").transform;
        right.SetParent(root.transform, false);

        try
        {
            var exit = root.AddComponent<GmSequenceExit>();
            exit.Configure("parlor", trigger, triggerCollider, blocker, left, right,
                new Vector3(0f, -92f, 0f), new Vector3(0f, 92f, 0f));

            Assert.IsFalse(exit.IsUnlocked);
            Assert.IsTrue(blocker.enabled, "the locked leaves have no physical barrier");
            Assert.IsFalse(triggerCollider.enabled, "walking into a locked door still leaves the room");

            Assert.IsTrue(GmRunStore.CompleteRoom("parlor", countsAsTableGame: true));

            Assert.IsTrue(exit.IsUnlocked);
            Assert.IsFalse(blocker.enabled, "the finished match still leaves an invisible wall");
            Assert.IsTrue(triggerCollider.enabled, "the open doorway has no live transition");
            Assert.AreEqual(Quaternion.Euler(0f, -92f, 0f), left.localRotation);
            Assert.AreEqual(Quaternion.Euler(0f, 92f, 0f), right.localRotation);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void ALoadedCompletionStartsOpenInsteadOfRelockingThePlayerInTheRoom()
    {
        GmRunStore.CompleteRoom("parlor", countsAsTableGame: true);
        var root = new GameObject("Exit");
        var trigger = root.AddComponent<GmSceneTransitionTrigger>();
        var triggerCollider = root.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        var blockerObject = new GameObject("Blocker");
        blockerObject.transform.SetParent(root.transform, false);
        var blocker = blockerObject.AddComponent<BoxCollider>();
        try
        {
            var exit = root.AddComponent<GmSequenceExit>();
            exit.Configure("parlor", trigger, triggerCollider, blocker, null, null,
                Vector3.zero, Vector3.zero);
            Assert.IsTrue(exit.IsUnlocked);
            Assert.IsTrue(triggerCollider.enabled);
            Assert.IsFalse(blocker.enabled);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void ReviewSnapCannotBypassTheAuthoredRequirement()
    {
        var root = new GameObject("Exit");
        var trigger = root.AddComponent<GmSceneTransitionTrigger>();
        var triggerCollider = root.AddComponent<BoxCollider>();
        var blockerObject = new GameObject("Blocker");
        blockerObject.transform.SetParent(root.transform, false);
        var blocker = blockerObject.AddComponent<BoxCollider>();
        var hinge = new GameObject("Hinge").transform;
        hinge.SetParent(root.transform, false);
        try
        {
            var exit = root.AddComponent<GmSequenceExit>();
            exit.Configure("parlor", trigger, triggerCollider, blocker, hinge, null,
                new Vector3(0f, 90f, 0f), Vector3.zero);

            Assert.IsFalse(exit.SnapOpenForReview());
            Assert.IsTrue(blocker.enabled);
            Assert.IsFalse(triggerCollider.enabled);
            Assert.AreEqual(Quaternion.identity, hinge.localRotation);

            GmRunStore.CompleteRoom("parlor", countsAsTableGame: true);
            Assert.IsTrue(exit.SnapOpenForReview());
            Assert.IsFalse(blocker.enabled);
            Assert.IsTrue(triggerCollider.enabled);
            Assert.Less(Quaternion.Angle(Quaternion.Euler(0f, 90f, 0f), hinge.localRotation), 0.01f);
        }
        finally { Object.DestroyImmediate(root); }
    }
}
