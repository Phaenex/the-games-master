using NUnit.Framework;
using UnityEngine;

public sealed class GmGroundsExplorationTests
{
    GameObject root;
    GmGroundsExploration exploration;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("test-grounds-exploration");
        exploration = root.AddComponent<GmGroundsExploration>();
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
    }

    [Test]
    public void OwedAccumulatesPositiveDeltas()
    {
        exploration.AddOwed(3f);
        exploration.AddOwed(2f);
        Assert.AreEqual(5f, exploration.Owed);
    }

    [Test]
    public void OwedRejectsNegativeDeltas()
    {
        exploration.AddOwed(5f);
        exploration.AddOwed(-100f);
        Assert.AreEqual(5f, exploration.Owed, "a negative delta must never lower Owed -- nothing the player does buys time back.");
    }

    [Test]
    public void OwedRejectsNaNDeltas()
    {
        exploration.AddOwed(5f);
        exploration.AddOwed(float.NaN);
        Assert.AreEqual(5f, exploration.Owed);
    }

    [Test]
    public void OwedStartsAtZeroWithZeroPressure()
    {
        Assert.AreEqual(0f, exploration.Owed);
        Assert.AreEqual(0f, exploration.Pressure);
    }
}
