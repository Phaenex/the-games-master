using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class GmEstateDoorTests
{
    GameObject root;
    Transform leaf;

    [SetUp]
    public void SetUp()
    {
        GmRunStore.BeginNewRun();
        root = new GameObject("DoorRoot");
        var leafObject = new GameObject("DoorLeaf");
        leafObject.transform.SetParent(root.transform, false);
        leaf = leafObject.transform;
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
    }

    [Test]
    public void ALockedDoorBlocksTheOpeningUntilTheKeyIsUsed()
    {
        GmEstateDoor door = BuildDoor(locked: true, barred: false, secret: false,
            requiredKey: "key:brass_skeleton_key", keyName: "Brass Skeleton Key");
        Awake(door);

        Assert.IsTrue(door.BlocksPassage, "a locked leaf left a hole a CharacterController can walk");
        Assert.IsFalse(door.Barrier.isTrigger, "the barrier is a trigger — it does not stop the body");

        door.OnGmInteraction(null);
        Assert.IsTrue(door.IsLocked, "the lock yielded without the key");
        Assert.IsTrue(door.BlocksPassage);

        GmRunStore.RecordClue("key:brass_skeleton_key");
        door.OnGmInteraction(null);

        Assert.IsFalse(door.IsLocked);
        Assert.IsTrue(door.IsOpen);
        Assert.IsFalse(door.BlocksPassage, "the unlocked leaf still occupies the opening");
        Assert.IsTrue(GmRunStore.HasClue("unlocked:door_library"));
        door.RelockClosed();
        Assert.IsTrue(door.IsLocked);
        Assert.IsFalse(door.IsOpen);
        Assert.IsTrue(door.BlocksPassage);
    }

    [Test]
    public void ABarredDoorRattlesAndNeverOpens()
    {
        GmEstateDoor door = BuildDoor(locked: false, barred: true, secret: false);
        Awake(door);

        door.OnGmInteraction(null);
        Assert.IsTrue(door.IsBarred);
        Assert.IsFalse(door.IsOpen);
        Assert.IsTrue(door.BlocksPassage, "a barred door is only a prompt; the opening is walkable");
    }

    [Test]
    public void ASecretPanelStaysShutWithoutTheClueAndOpensWithIt()
    {
        GmEstateDoor door = BuildDoor(locked: true, barred: false, secret: true,
            requiredKey: "clue:library_lever", keyName: "Bookcase lever");
        Awake(door);

        door.OnGmInteraction(null);
        Assert.IsTrue(door.IsLocked);
        Assert.IsTrue(door.BlocksPassage);

        GmRunStore.RecordClue("clue:library_lever");
        door.OnGmInteraction(null);
        Assert.IsFalse(door.IsLocked);
        Assert.IsTrue(door.IsOpen);
        Assert.IsFalse(door.BlocksPassage);
    }

    [Test]
    public void AnUnlockedClosedDoorBlocksUntilItIsOpened()
    {
        GmEstateDoor door = BuildDoor(locked: false, barred: false, secret: false);
        Awake(door);

        Assert.IsFalse(door.IsOpen);
        Assert.IsTrue(door.BlocksPassage, "a shut unlocked door is a painted plane");

        door.OnGmInteraction(null);
        Assert.IsTrue(door.IsOpen);
        Assert.IsFalse(door.BlocksPassage);
    }

    [Test]
    public void ConfigureWritesTheSecretFlagInsteadOfLeavingItDefault()
    {
        GmEstateDoor door = BuildDoor(locked: true, barred: false, secret: true,
            requiredKey: "clue:x");
        Assert.IsTrue(door.IsSecretMechanism);
        Assert.AreEqual("clue:x", door.RequiredKey);
    }

    [Test]
    public void APersistedUnlockClearsTheLockOnAwake()
    {
        GmRunStore.RecordClue("unlocked:door_library");
        GmEstateDoor door = BuildDoor(locked: true, barred: false, secret: false,
            requiredKey: "key:brass_skeleton_key");
        Awake(door);
        Assert.IsFalse(door.IsLocked);
        Assert.IsTrue(door.BlocksPassage, "remembering the key should not leave the leaf standing open");
    }

    [Test]
    public void ACeilingHatchSwingsOnItsHingeAxisInsteadOfYaw()
    {
        var door = root.AddComponent<GmEstateDoor>();
        door.Configure("door_attic_hatch", "Attic Hatch", "key:attic_hatch", "attic key",
            locked: false, barred: false, 90f, leaf, secret: false, openAtStart: false,
            barrierCollider: null, swingAxis: Vector3.right);
        door.EnsureBarrier(new Vector3(1.1f, 0.12f, 1.1f));
        Awake(door);
        Quaternion closed = leaf.localRotation;
        door.OnGmInteraction(null);
        Assert.IsTrue(door.IsOpen);
        Vector3 swung = (Quaternion.Inverse(closed) * leaf.localRotation).eulerAngles;
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(swung.x, 90f)), Is.LessThan(1f),
            $"hatch yawed or rolled instead of lifting: {swung}");
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(swung.y, 0f)), Is.LessThan(1f),
            $"hatch used the wall-door yaw axis: {swung}");
    }

    GmEstateDoor BuildDoor(bool locked, bool barred, bool secret,
        string requiredKey = "", string keyName = "")
    {
        var door = root.AddComponent<GmEstateDoor>();
        door.Configure("door_library", "Library Door", requiredKey, keyName,
            locked, barred, 90f, leaf, secret, openAtStart: false);
        door.EnsureBarrier(new Vector3(0.14f, 2.2f, 1.15f));
        return door;
    }

    static void Awake(GmEstateDoor door)
    {
        MethodInfo awake = typeof(GmEstateDoor).GetMethod("Awake",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(awake, "GmEstateDoor lost Awake — persisted unlocks would never apply");
        awake.Invoke(door, null);
    }
}
