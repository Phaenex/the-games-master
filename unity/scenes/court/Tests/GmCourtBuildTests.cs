using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

public class GmCourtBuildTests
{
    [OneTimeSetUp]
    public void BuildOnce() => GmCourtBuilder.Build();

    // Build() leaves its built scene active (NewSceneMode.Single) with no teardown of its own.
    // Without this, its colliders/renderers survive into every EditMode fixture that runs after
    // it in the same batch, corrupting unrelated raycasts (e.g. GmWendRouteGroundTests).
    [OneTimeTearDown]
    public void TearDownOnce() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

    [Test]
    public void SceneContractPasses()
    {
        var issues = GmCourtQualityAudit.ValidateOpenScene();
        Assert.IsEmpty(issues, "Court scene contract failed:\n- " + string.Join("\n- ", issues));
    }

    [Test]
    public void ReviewTourHasNoPlaceholderShotNames()
    {
        var tour = Object.FindAnyObjectByType<GmCourtShotTour>();
        Assert.IsNotNull(tour, "review tour component is missing");
        Assert.IsFalse(tour.HasPlaceholderShots,
            "replace every generated '*-replace-me' waypoint before this scene can pass");
    }

    [Test]
    public void AuthoredCompositionContractPasses()
    {
        var issues = GmSceneCompositionAudit.ValidateOpenScene(
            GmCourtBuilder.SceneId, Object.FindAnyObjectByType<GmCourtShotTour>(), Camera.main);
        Assert.IsEmpty(issues, "Court composition failed:\n- " + string.Join("\n- ", issues));
    }

    [Test]
    public void TheJudgeCanActuallySitBehindTheBench()
    {
        // An adversarial read flagged this as the tightest surviving margin after Court's 15 fixes:
        // bench centred z=6.0, chair centred z=6.8, both FBX footprints unmeasured. A tucked chair
        // overlapping a desk is normal furniture, so "no overlap" would be a false alarm. What must
        // be true is that a body fits: clear floor between the back of the bench and the north wall,
        // with the chair inside it and not inside the wall.
        const float NorthWallInnerFace = 7.85f;
        var bench = GameObject.Find("JudgeBenchDesk");
        var chair = GameObject.Find("JudgeChair");
        Assert.IsNotNull(bench, "JudgeBenchDesk is missing");
        Assert.IsNotNull(chair, "JudgeChair is missing");

        Bounds benchBounds = WorldBounds(bench);
        Bounds chairBounds = WorldBounds(chair);
        Debug.Log($"[GmCourtMeasure] bench z {benchBounds.min.z:F3}..{benchBounds.max.z:F3} " +
            $"(w {benchBounds.size.x:F3}) | chair z {chairBounds.min.z:F3}..{chairBounds.max.z:F3} " +
            $"(w {chairBounds.size.x:F3}) | wall inner face {NorthWallInnerFace:F3} | " +
            $"alcove {NorthWallInnerFace - benchBounds.max.z:F3}m");

        Assert.Less(chairBounds.max.z, NorthWallInnerFace,
            $"the judge's chair reaches z={chairBounds.max.z:F3}, inside the north wall at {NorthWallInnerFace}");
        Assert.Greater(NorthWallInnerFace - benchBounds.max.z, 0.6f,
            "less than 0.6m of floor between the back of the bench and the wall — nobody fits behind it");
    }

    static Bounds WorldBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        Assert.IsNotEmpty(renderers, $"{root.name} has no renderer to measure");
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }
}
