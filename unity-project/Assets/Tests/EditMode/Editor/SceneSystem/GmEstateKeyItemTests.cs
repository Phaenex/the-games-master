using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class GmEstateKeyItemTests
{
    GameObject root;

    [SetUp]
    public void SetUp()
    {
        GmRunStore.BeginNewRun();
        root = new GameObject("Key");
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
    }

    [Test]
    public void TakingTheKeyBanksTheClueAndHidesTheObject()
    {
        var item = root.AddComponent<GmEstateKeyItem>();
        item.Configure("key:brass_skeleton_key", "Brass Skeleton Key",
            "Acquired the Brass Skeleton Key.", "An ornate tarnished key.");
        Awake(item);

        Assert.IsTrue(root.activeSelf);
        item.OnGmInteraction(null);
        Assert.IsTrue(GmRunStore.HasClue("key:brass_skeleton_key"));
        Assert.IsFalse(root.activeSelf);
    }

    [Test]
    public void AKeyAlreadyHeldDoesNotStayInTheWorld()
    {
        GmRunStore.RecordClue("key:brass_skeleton_key");
        var item = root.AddComponent<GmEstateKeyItem>();
        item.Configure("key:brass_skeleton_key", "Brass Skeleton Key",
            "Acquired the Brass Skeleton Key.", "An ornate tarnished key.");
        Awake(item);
        Assert.IsFalse(root.activeSelf, "a collected key respawned on rebuild");
    }

    static void Awake(GmEstateKeyItem item)
    {
        MethodInfo awake = typeof(GmEstateKeyItem).GetMethod("Awake",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(awake);
        awake.Invoke(item, null);
    }
}
