using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class GmLabyrinthBuildTests
{
    [OneTimeSetUp]
    public void BuildOnce() => GmLabyrinthBuilder.Build();

    // Build() leaves its built scene active (NewSceneMode.Single) with no teardown of its own.
    // Without this, its colliders/renderers survive into every EditMode fixture that runs after
    // it in the same batch, corrupting unrelated raycasts (e.g. GmWendRouteGroundTests).
    [OneTimeTearDown]
    public void TearDownOnce() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

    [Test]
    public void SceneContractPasses()
    {
        var issues = GmLabyrinthQualityAudit.ValidateOpenScene();
        Assert.IsEmpty(issues, "Labyrinth scene contract failed:\n- " + string.Join("\n- ", issues));
    }

    [Test]
    public void ReviewTourHasNoPlaceholderShotNames()
    {
        var tour = Object.FindAnyObjectByType<GmLabyrinthShotTour>();
        Assert.IsNotNull(tour, "review tour component is missing");
        Assert.IsFalse(tour.HasPlaceholderShots,
            "replace every generated '*-replace-me' waypoint before this scene can pass");
    }

    [Test]
    public void AuthoredCompositionContractPasses()
    {
        var issues = GmSceneCompositionAudit.ValidateOpenScene(
            GmLabyrinthBuilder.SceneId, Object.FindAnyObjectByType<GmLabyrinthShotTour>(), Camera.main);
        Assert.IsEmpty(issues, "Labyrinth composition failed:\n- " + string.Join("\n- ", issues));
    }

    [Test]
    public void TheAuthoredExitGateResolvesTheRunInsteadOfLeadingIntoASolidBoundary()
    {
        var ending = Object.FindAnyObjectByType<GmEndingTrigger>(FindObjectsInactive.Include);
        Assert.IsNotNull(ending, "the maze has an exit prop but no end-of-run trigger");
        Collider volume = ending.GetComponent<Collider>();
        Assert.IsNotNull(volume);
        Assert.IsTrue(volume.isTrigger);
        Assert.That(Vector3.Distance(volume.bounds.center, new Vector3(15f, 1.2f, 17f)),
            Is.LessThan(1.5f), "the ending trigger is not at the authored exit gate");
    }

    [Test]
    public void EveryAuthoredNightLightCarriesItsHdrpLumenValue()
    {
        var expectedLumens = new System.Collections.Generic.Dictionary<string, float>
        {
            ["MoonbeamAltarLight"] = 800f,
            ["EntranceTorchLightWest"] = 60f,
            ["EntranceTorchLightEast"] = 60f,
            ["HuntsmanLanternLight"] = 60f,
            ["ExitLanternLightWest"] = 60f,
            ["ExitLanternLightEast"] = 60f
        };
        foreach (var pair in expectedLumens)
        {
            GameObject lightObject = GameObject.Find(pair.Key);
            Assert.IsNotNull(lightObject, $"{pair.Key} is missing");
            Light light = lightObject.GetComponent<Light>();
            Assert.AreEqual("Lumen", light.lightUnit.ToString(), $"{pair.Key} fell back to an implicit HDRP unit");
            Assert.AreEqual(pair.Value, light.intensity, 0.01f,
                $"{pair.Key} was silently collapsed to a default or interior ceiling");
        }

        Light moon = GameObject.Find("LabyrinthMoon").GetComponent<Light>();
        Assert.AreEqual("Lux", moon.lightUnit.ToString());
        Assert.AreEqual(GmLabyrinthNightAtmosphere.MoonLux, moon.intensity, 0.01f);

        Volume volume = Object.FindAnyObjectByType<Volume>();
        Assert.IsNotNull(volume);
        Assert.AreEqual("Assets/Scenes/Generated/GmNight_labyrinth.asset",
            AssetDatabase.GetAssetPath(volume.sharedProfile),
            "the outdoor maze is still using the windowless interior profile");
        Assert.IsTrue(volume.sharedProfile.TryGet(out GradientSky _), "the outdoor maze has no authored night sky");
    }

    [Test]
    public void MazeWallsUseOwnedFoliageOverInvisiblePhysicalBlockers()
    {
        GameObject hedges = GameObject.Find("MazeHedges");
        Assert.IsNotNull(hedges);
        foreach (Transform hedge in hedges.transform)
        {
            Assert.IsNotNull(hedge.GetComponent<BoxCollider>(), $"{hedge.name} has no physical blocker");
            Assert.IsNull(hedge.GetComponent<MeshRenderer>(), $"{hedge.name} still exposes a primitive cube wall");
            Assert.IsNotEmpty(hedge.GetComponentsInChildren<Renderer>(true),
                $"{hedge.name} has no owned foliage dressing");
            int directStoneSegments = 0;
            foreach (Transform child in hedge)
                if (child.name.Contains("_Stone")) directStoneSegments++;
            Assert.AreEqual(2, directStoneSegments,
                $"{hedge.name} is still pretending sparse shrubs form a solid maze wall");
        }
    }

    [Test]
    public void EntranceAndExitOpeningsArePhysicalAndTheShrineContainsTheAssembledMirror()
    {
        Assert.IsNull(GameObject.Find("SouthOuterWall"), "the entrance arch still stands in front of a solid boundary");
        Assert.IsNotNull(GameObject.Find("SouthOuterWall_West"));
        Assert.IsNotNull(GameObject.Find("SouthOuterWall_East"));
        Assert.IsNotNull(GameObject.Find("AssembledMirror"), "the mirror shrine is still an empty cylinder");
        Assert.IsNull(GameObject.Find("ExitGroundMist"), "the authored fog is still represented by an opaque cube");
    }

    [Test]
    public void ShrineUsesOwnedAltarArtAndARealVolumetricBeam()
    {
        GameObject pedestal = GameObject.Find("MirrorShrinePedestal");
        Assert.IsNotNull(pedestal);
        Assert.IsNull(pedestal.GetComponent<MeshFilter>(), "the shrine pedestal is still a primitive cylinder");
        Assert.IsNotEmpty(pedestal.GetComponentsInChildren<Renderer>(true));

        GameObject shaft = GameObject.Find("MoonbeamShaft");
        Assert.IsNotNull(shaft);
        Renderer glow = shaft.GetComponent<Renderer>();
        Assert.IsNotNull(glow, "the composition contract has no visible moonlight support");
        Assert.AreEqual((int)RenderQueue.Transparent, glow.sharedMaterial.renderQueue,
            "the moonlight support reverted to an opaque surface");
        Assert.IsNull(shaft.GetComponent<Collider>(), "the moonlight glow blocks the clearing");
        Assert.IsTrue(GameObject.Find("MoonbeamAltarLight").GetComponent<HDAdditionalLightData>()
            .affectsVolumetric);
    }
}
