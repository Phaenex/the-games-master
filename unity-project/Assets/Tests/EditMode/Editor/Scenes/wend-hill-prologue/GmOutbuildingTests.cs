using NUnit.Framework;
using UnityEngine;

public sealed class GmOutbuildingTests
{
    GameObject buildingGo;
    GameObject interiorGo;

    [TearDown]
    public void TearDown()
    {
        if (buildingGo != null) Object.DestroyImmediate(buildingGo);
        if (interiorGo != null) Object.DestroyImmediate(interiorGo);
        GmOutbuilding.ResetRegistryForTests();
    }

    [Test]
    public void ConfigureSetsTheBuildingId()
    {
        interiorGo = new GameObject("interior");
        buildingGo = new GameObject("building");
        GmOutbuilding outbuilding = buildingGo.AddComponent<GmOutbuilding>();
        outbuilding.Configure("coach-house", interiorGo);

        Assert.AreEqual("coach-house", outbuilding.BuildingId);
    }

    [Test]
    public void CloseAllForCrossingDeactivatesEveryRegisteredInterior()
    {
        interiorGo = new GameObject("interior");
        interiorGo.SetActive(true);
        buildingGo = new GameObject("building");
        GmOutbuilding outbuilding = buildingGo.AddComponent<GmOutbuilding>();
        outbuilding.Configure("coach-house", interiorGo);

        GmOutbuilding.CloseAllForCrossing();

        Assert.IsFalse(interiorGo.activeSelf, "the crossing must never leave a lit interior active behind the black.");
    }

    [Test]
    public void DestroyedOutbuildingsLeaveTheRegistry()
    {
        interiorGo = new GameObject("interior");
        buildingGo = new GameObject("building");
        buildingGo.AddComponent<GmOutbuilding>().Configure("coach-house", interiorGo);

        Object.DestroyImmediate(buildingGo);
        buildingGo = null;

        // Must not throw even though the outbuilding above no longer exists.
        Assert.DoesNotThrow(() => GmOutbuilding.CloseAllForCrossing());
    }

    [Test]
    public void RequiresABoxColliderComponent()
    {
        buildingGo = new GameObject("building");
        buildingGo.AddComponent<GmOutbuilding>();

        Assert.IsNotNull(buildingGo.GetComponent<BoxCollider>(),
            "[RequireComponent(typeof(BoxCollider))] must add one automatically.");
    }
}
