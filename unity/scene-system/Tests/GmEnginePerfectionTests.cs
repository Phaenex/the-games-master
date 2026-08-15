using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class GmEnginePerfectionTests
{
    GameObject root;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("EnginePerfectionFixture");
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
    }

    [Test]
    public void PropPlacementEngineNormalizesScaleCorrectly()
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(root.transform);
        cube.transform.localScale = new Vector3(10f, 5f, 2f);

        float factor = GmPropPlacementEngine.NormalizeScale(cube, 2.5f);
        Bounds bounds = GmPropPlacementEngine.EncapsulateBounds(cube);
        float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));

        Assert.That(longest, Is.EqualTo(2.5f).Within(0.01f), "Longest dimension must equal target dimension");
    }

    [Test]
    public void PropPlacementEngineGeneratesAccurateCollisionProxy()
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(root.transform);
        cube.transform.localScale = new Vector3(2f, 3f, 4f);

        BoxCollider proxy = GmPropPlacementEngine.GenerateCollisionProxy(cube);
        Assert.That(proxy, Is.Not.Null);
        Assert.That(proxy.isTrigger, Is.False);
        Assert.That(proxy.size.x, Is.GreaterThan(0.5f));
    }

    [Test]
    public void LightingEngineEnforcesShadowBudget()
    {
        var lightList = new List<Light>();
        for (int i = 0; i < 8; i++)
        {
            var go = new GameObject($"Light_{i}");
            go.transform.SetParent(root.transform);
            go.transform.position = new Vector3(0f, 0f, i * 5f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.Soft;
            lightList.Add(light);
        }

        int activeCasters = GmLightingEngine.EnforceShadowBudget(Vector3.zero, lightList, 3);
        Assert.That(activeCasters, Is.EqualTo(3), "Must clamp active shadow casters to budget");

        int totalSoft = 0;
        foreach (var l in lightList)
            if (l.shadows == LightShadows.Soft) totalSoft++;

        Assert.That(totalSoft, Is.EqualTo(3));
    }

    [Test]
    public void AcousticZoneEngineReturnsValidAcousticSettings()
    {
        var chapel = GmAcousticZoneEngine.GetSettings(GmAcousticEnvironment.StoneChapelCrypt);
        Assert.That(chapel.decayTime, Is.GreaterThan(2.0f), "Chapel crypt must have long reverb decay");

        var cell = GmAcousticZoneEngine.GetSettings(GmAcousticEnvironment.ClaustrophobicCoachHouse);
        Assert.That(cell.decayTime, Is.LessThan(1.0f), "Coach house must have short decay time");
        Assert.That(cell.lowPassCutoffHz, Is.LessThan(15000f), "Coach house must muffle high frequencies");
    }

    [Test]
    public void InteractionEngineFocusesTargetWithinCone()
    {
        var targetGo = new GameObject("InteractTarget");
        targetGo.transform.SetParent(root.transform);
        targetGo.transform.position = new Vector3(0f, 0f, 2f);
        var interactable = targetGo.AddComponent<GmInteractable>();
        interactable.Configure("target-poi", "Examine", 3.5f, 15f);

        var list = new List<GmInteractable> { interactable };
        var result = GmInteractionEngine.ScanForTarget(Vector3.zero, Vector3.forward, list, ~0);

        Assert.That(result.bestTarget, Is.EqualTo(interactable));
        Assert.That(result.hasLineOfSight, Is.True);
        Assert.That(result.distance, Is.EqualTo(2f).Within(0.01f));
    }
}
