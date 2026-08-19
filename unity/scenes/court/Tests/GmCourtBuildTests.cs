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
        Assert.IsTrue(tour.UsesBackbufferCaptureForAudit,
            "Court evidence includes UI Toolkit; Camera.Render screenshots would omit the hearing");
    }

    [Test]
    public void ShippingCourtHasPlayerFacingHearingInputAndPhysicalPresentation()
    {
        GameObject systems = GameObject.Find("SceneSystems");
        Assert.IsNotNull(systems);
        Assert.IsNotNull(systems.GetComponent<GmCourtHud>());
        Assert.IsNotNull(systems.GetComponent<GmCourtInput>());
        Assert.IsNotNull(systems.GetComponent<GmCourtPresenter>());
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

    [Test]
    public void ARealVerdictUnlocksTheOnwardDoorToShutTheBox()
    {
        var exit = Object.FindAnyObjectByType<GmSequenceExit>(FindObjectsInactive.Include);
        Assert.IsNotNull(exit, "the Court reaches a verdict and then strands the player");
        var trigger = exit.GetComponentInChildren<GmSceneTransitionTrigger>(true);
        Assert.IsNotNull(trigger);
        Assert.AreEqual(GmShutTheBoxBuilder.SceneId, trigger.TargetSceneId);
        Assert.AreEqual(GmShutTheBoxBuilder.ScenePath, trigger.TargetScenePath);
        Assert.IsFalse(trigger.GetComponent<Collider>().enabled,
            "the player can leave before the Court reaches a verdict");

        GmRunStore.CompleteRoom(GmCourtBuilder.SceneId, countsAsTableGame: false);
        Assert.IsTrue(exit.IsUnlocked);
        Assert.IsTrue(trigger.GetComponent<Collider>().enabled);
    }

    [Test]
    public void EvidencePropsUseHdrpMaterialsAndBothSpotlightsActuallyAimAtTheirSubjects()
    {
        foreach (string name in new[] { "JudgeBenchDesk", "CourtEvidenceCandle", "JudgeBenchCandles", "EvidenceDocket", "BrassGavel" })
        {
            GameObject prop = GameObject.Find(name);
            Assert.IsNotNull(prop, $"{name} is missing");
            foreach (Renderer renderer in prop.GetComponentsInChildren<Renderer>(true))
                foreach (Material material in renderer.sharedMaterials)
                {
                    Assert.IsNotNull(material, $"{name}/{renderer.name} has a null material");
                    Assert.IsNotNull(material.shader, $"{name}/{renderer.name} has a null shader");
                    Assert.AreEqual("HDRP/Lit", material.shader.name,
                        $"{name}/{renderer.name} would render with an incompatible material");
                }
        }

        AssertSpotAimsAt("EvidenceSpotlight", new Vector3(0f, 0.8f, 2.5f));
        AssertSpotAimsAt("JudgeChandelierLight", new Vector3(0f, 1.6f, 5.7f));
        foreach (string name in new[] { "EvidenceSpotlight", "JudgeChandelierLight", "JudgeBenchCandleLight", "JurySconceLight", "WitnessBacklight" })
            Assert.AreEqual("Lumen", GameObject.Find(name).GetComponent<Light>().lightUnit.ToString(),
                $"{name} has no explicit HDRP lumen contract");

        Assert.AreEqual(GmInteriorAtmosphere.PracticalCeilingLumens,
            GameObject.Find("JudgeBenchCandleLight").GetComponent<Light>().intensity, 0.01f,
            "the bench candle practical exceeds the shared period-light ceiling");

        Assert.AreEqual(50f, GameObject.Find("JudgeChandelierLight").GetComponent<Light>().spotAngle, 0.01f,
            "the bench practical spreads its period-limited output across the whole room");
        GameObject exitFixtures = GameObject.Find("VerdictDoorSconces");
        GameObject exitLight = GameObject.Find("VerdictDoorSconceLight");
        Assert.IsNotNull(exitFixtures, "the verdict passage has no visible period light source");
        Assert.AreEqual(2, exitFixtures.transform.childCount);
        Assert.IsNotNull(exitLight);
        Assert.AreEqual(GmInteriorAtmosphere.PracticalCeilingLumens,
            exitLight.GetComponent<Light>().intensity, 0.01f);
    }

    static void AssertSpotAimsAt(string name, Vector3 subject)
    {
        GameObject lightObject = GameObject.Find(name);
        Assert.IsNotNull(lightObject, $"{name} is missing");
        Vector3 expected = (subject - lightObject.transform.position).normalized;
        Assert.Greater(Vector3.Dot(lightObject.transform.forward, expected), 0.995f,
            $"{name} illuminates empty space instead of its authored subject");
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
