using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class GmWendOpeningTests
{
    GameObject root;

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
    }

    [Test]
    public void EstateRouteStopsAtTheDryManorEndpoint()
    {
        var raw = new[] {
            Vector3.zero,
            new Vector3(0f, 0f, 300f),
            new Vector3(0f, -20f, 580f),
        };
        var estate = GmWendRoute.TakeThroughDistance(raw, GmWendRoute.EstateRouteMetres);
        Assert.That(GmWendRoute.RouteLength(estate), Is.EqualTo(435f).Within(0.01f));
        Assert.That(estate[^1].z, Is.EqualTo(435f).Within(0.01f));
        Assert.That(estate[^1].y, Is.GreaterThan(-11f), "canyon tail must not become the manor endpoint");
    }

    [Test]
    public void RouteSplineProjectsProgressWithoutDependingOnWorldAxis()
    {
        root = new GameObject("route");
        var route = root.AddComponent<GmRouteSpline>();
        route.Configure(new[] { Vector3.zero, new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, -10f) });
        Assert.That(route.Length, Is.EqualTo(20f).Within(0.01f));
        Assert.That(route.ProjectDistance(new Vector3(10f, 0f, -6f)), Is.EqualTo(16f).Within(0.01f));
        Assert.That(Vector3.Distance(route.PointAt(15f), new Vector3(10f, 0f, -5f)), Is.LessThan(0.01f));
    }

    [Test]
    public void RouteCapsuleIntersectionIncludesLowAndChestHeightBlockers()
    {
        root = new GameObject("route-capsule-test");
        var route = root.AddComponent<GmRouteSpline>();
        route.Configure(new[] { Vector3.zero, new Vector3(10f, 0f, 0f) });

        Assert.That(GmWendPerformance.IntersectsRouteCapsule(
            new Bounds(new Vector3(5f, 0.1f, 0.4f), new Vector3(0.1f, 0.2f, 0.1f)), route), Is.True);
        Assert.That(GmWendPerformance.IntersectsRouteCapsule(
            new Bounds(new Vector3(5f, 1f, 0.4f), new Vector3(0.1f, 2f, 0.1f)), route), Is.True);
        Assert.That(GmWendPerformance.IntersectsRouteCapsule(
            new Bounds(new Vector3(5f, 1f, 3f), new Vector3(0.1f, 2f, 0.1f)), route), Is.False);
    }

    [Test]
    public void AnchorValidationRejectsDuplicateStableIds()
    {
        root = new GameObject("anchors");
        root.AddComponent<GmWorldAnchor>().Configure("gate", 10f);
        var duplicate = new GameObject("duplicate");
        duplicate.transform.SetParent(root.transform);
        duplicate.AddComponent<GmWorldAnchor>().Configure("gate", 20f);
        CollectionAssert.Contains(GmWorldAnchor.ValidateScene(), "duplicate anchor id 'gate'");
    }

    [Test]
    public void CanonicalDesignHasExactStoryCountsAndOnlySemanticTriggers()
    {
        string path = Path.Combine(Application.dataPath, "StreamingAssets", GmWendOpening.DesignFile);
        Assert.That(File.Exists(path), Is.True, path);
        string json = File.ReadAllText(path);
        Assert.That(Count(json, "\"anchorId\""), Is.EqualTo(25)); // 13 POIs + 5 main + 7 branch
        Assert.That(Count(json, "\"id\""), Is.EqualTo(13));
        Assert.That(Count(json, "\"main\""), Is.EqualTo(12));
        Assert.That(Count(json, "\"rect\""), Is.Zero);
        Assert.That(Count(json, "\"z\""), Is.Zero);
    }

    [Test]
    public void NegativeScaleColliderIsDetectedBeforeRepair()
    {
        root = GameObject.CreatePrimitive(PrimitiveType.Cube);
        root.transform.localScale = new Vector3(-1f, 2f, 3f);
        Assert.That(GmWendColliderRepair.HasNegativeLossyScale(root.transform), Is.True);
    }

    [Test]
    public void DoubleReflectionInColliderAncestryIsStillRejected()
    {
        root = new GameObject("root");
        root.transform.localScale = new Vector3(-1f, 1f, 1f);
        var child = new GameObject("child");
        child.transform.SetParent(root.transform, false);
        child.transform.localScale = new Vector3(-1f, 1f, 1f);
        BoxCollider collider = child.AddComponent<BoxCollider>();
        Assert.That(child.transform.lossyScale.x, Is.GreaterThan(0f), "two reflections cancel in lossyScale");
        Assert.That(GmWendColliderRepair.HasUnsupportedBoxTransform(collider), Is.True);
    }

    [Test]
    public void BuiltPlayerRenderBudgetIsExplicitAndNotSub720pInternally()
    {
        Assert.That(GmWendRenderBudget.InternalRenderPercentage, Is.EqualTo(67f));
        Assert.That(Mathf.CeilToInt(GmWendRenderBudget.InternalRenderPercentage * 1080f / 100f),
            Is.GreaterThanOrEqualTo(720f));
        Assert.That(GmWendRenderBudget.UpscaleFilter,
            Is.EqualTo(DynamicResUpscaleFilter.EdgeAdaptiveScalingUpres));
    }

    [Test]
    public void VictorianProofKitIsInstalledAndBuildsMappedHdrpMaterials()
    {
        GmVictorianInteriorKit.Prepare();
        Assert.That(GmVictorianInteriorKit.Ready, Is.True,
            "run `npm run unity:assets:victorian` before rebuilding the opening");
        Material material = GmVictorianInteriorKit.Surface("chair", "test-chair", Vector2.one);
        Assert.That(material.shader.name, Is.EqualTo("HDRP/Lit"));
        Assert.That(material.GetTexture("_BaseColorMap"), Is.Not.Null);
        Assert.That(material.GetTexture("_NormalMap"), Is.Not.Null);
        Object.DestroyImmediate(material);
    }

    static int Count(string input, string value) =>
        input.Split(new[] { value }, System.StringSplitOptions.None).Length - 1;
}
