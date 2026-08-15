// The trigger's only real entry path was dead.
//
// OnTriggerEnter read CompareTag("Player") and nothing in this project ever set that tag --
// GmPlayerRig tags the CAMERA MainCamera and leaves the body untagged. Every existing test called
// TriggerTransition() directly, so the component was written, covered, and unreachable by a human.
//
// These drive the branch that was broken, through the same private entry point Unity calls.
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class GmSceneTransitionTriggerTests
{
    static void EnterVolume(GmSceneTransitionTrigger trigger, Collider who)
    {
        MethodInfo enter = typeof(GmSceneTransitionTrigger)
            .GetMethod("OnTriggerEnter", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(enter, "OnTriggerEnter is gone; this test is guarding nothing");
        enter.Invoke(trigger, new object[] { who });
    }

    [Test]
    public void APlayerWalkingInFiresIt()
    {
        var volume = new GameObject("Volume");
        var body = new GameObject("Body");
        try
        {
            var trigger = volume.AddComponent<GmSceneTransitionTrigger>();
            body.AddComponent<GmPlayer>();
            var collider = body.AddComponent<CapsuleCollider>();

            EnterVolume(trigger, collider);
            Assert.IsTrue(trigger.IsTriggered,
                "a real player walked into the exit and nothing happened — this is the tag bug back");
        }
        finally { Object.DestroyImmediate(body); Object.DestroyImmediate(volume); }
    }

    [Test]
    public void APlayerIsFoundFromAChildCollider()
    {
        // The rig puts the camera and other parts under the body, and a hit can land on any of them.
        var volume = new GameObject("Volume");
        var body = new GameObject("Body");
        try
        {
            var trigger = volume.AddComponent<GmSceneTransitionTrigger>();
            body.AddComponent<GmPlayer>();
            var child = new GameObject("Knee");
            child.transform.SetParent(body.transform, false);
            var collider = child.AddComponent<BoxCollider>();

            EnterVolume(trigger, collider);
            Assert.IsTrue(trigger.IsTriggered, "a hit on part of the player did not count as the player");
        }
        finally { Object.DestroyImmediate(body); Object.DestroyImmediate(volume); }
    }

    [Test]
    public void SomethingThatIsNotThePlayerDoesNotFireIt()
    {
        // Without this the fix would be "fire for anything", which is worse than the tag bug: a
        // rolling prop or a dropped object would evict the player from the room.
        var volume = new GameObject("Volume");
        var prop = new GameObject("Chair");
        try
        {
            var trigger = volume.AddComponent<GmSceneTransitionTrigger>();
            var collider = prop.AddComponent<BoxCollider>();

            EnterVolume(trigger, collider);
            Assert.IsFalse(trigger.IsTriggered, "a chair walked into the Parlor");
        }
        finally { Object.DestroyImmediate(prop); Object.DestroyImmediate(volume); }
    }
}
