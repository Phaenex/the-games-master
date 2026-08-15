using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public sealed class GmDisplayCalibrationTests
{
    Volume gradeVolume;
    VolumeProfile sharedProfile;
    bool hadPreference;
    int savedPreference;

    // The component only ever changes what the player sees through a graded Volume, so every test
    // needs one standing in an isolated scene before the component exists: Awake applies immediately
    // and would otherwise log a hard failure about a scene this fixture never authored.
    [SetUp]
    public void SetUp()
    {
        hadPreference = PlayerPrefs.HasKey(GmDisplayCalibration.PreferenceKey);
        savedPreference = PlayerPrefs.GetInt(GmDisplayCalibration.PreferenceKey, 0);
        PlayerPrefs.DeleteKey(GmDisplayCalibration.PreferenceKey);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        sharedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        sharedProfile.Add<ColorAdjustments>();
        var volumeObject = new GameObject("GradeVolume");
        gradeVolume = volumeObject.AddComponent<Volume>();
        gradeVolume.isGlobal = true;
        gradeVolume.sharedProfile = sharedProfile;
    }

    // Automated runs never keep a calibration the player did not set. Restoring the real preference
    // is part of that rule, not politeness: these tests write the same key the pause menu writes.
    [TearDown]
    public void TearDown()
    {
        if (hadPreference) PlayerPrefs.SetInt(GmDisplayCalibration.PreferenceKey, savedPreference);
        else PlayerPrefs.DeleteKey(GmDisplayCalibration.PreferenceKey);
        PlayerPrefs.Save();
        // Volume never destroys the runtime copy it instantiates from a shared profile.
        if (gradeVolume != null && gradeVolume.HasInstantiatedProfile()) Release(gradeVolume.profile);
        Release(sharedProfile);
        sharedProfile = null;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [Test]
    public void BrightnessRangeStaysNarrowAndCentredOnAuthoredExposure()
    {
        Assert.AreEqual(-2, GmDisplayCalibration.ClampLevel(-100));
        Assert.AreEqual(0, GmDisplayCalibration.ClampLevel(0));
        Assert.AreEqual(2, GmDisplayCalibration.ClampLevel(100));
        Assert.AreEqual(-0.5f, GmDisplayCalibration.OffsetForLevel(-2));
        Assert.AreEqual(0f, GmDisplayCalibration.OffsetForLevel(0));
        Assert.AreEqual(0.5f, GmDisplayCalibration.OffsetForLevel(2));
    }

    [Test]
    public void ApplyWritesThePostExposureOffsetOntoTheGradedVolume()
    {
        GmDisplayCalibration calibration = NewCalibration();

        calibration.SetLevel(2, persist: false);
        Assert.AreEqual(2, calibration.Level);
        Assert.AreEqual(0.5f, PostExposure(), 0.0001f,
            "the level moved but the HDRP grade the player looks through did not");

        calibration.SetLevel(-2, persist: false);
        Assert.AreEqual(-0.5f, PostExposure(), 0.0001f);

        // Out of range collapses onto the authored ends rather than opening the stop range.
        calibration.SetLevel(9, persist: false);
        Assert.AreEqual(2, calibration.Level);
        Assert.AreEqual(0.5f, PostExposure(), 0.0001f);
    }

    [Test]
    public void StepMovesOneLevelAtATimeAndStopsAtTheAuthoredEnds()
    {
        GmDisplayCalibration calibration = NewCalibration();

        calibration.Step(1);
        Assert.AreEqual(1, calibration.Level);
        calibration.Step(-1);
        Assert.AreEqual(0, calibration.Level);
        // Magnitude is a direction, never a jump size.
        calibration.Step(7);
        Assert.AreEqual(1, calibration.Level);
        calibration.Step(0);
        Assert.AreEqual(1, calibration.Level);

        for (int i = 0; i < 6; i++) calibration.Step(1);
        Assert.AreEqual(GmDisplayCalibration.MaximumLevel, calibration.Level);
        Assert.AreEqual(0.5f, PostExposure(), 0.0001f);
        for (int i = 0; i < 12; i++) calibration.Step(-1);
        Assert.AreEqual(GmDisplayCalibration.MinimumLevel, calibration.Level);
        Assert.AreEqual(-0.5f, PostExposure(), 0.0001f);
    }

    [Test]
    public void SetLevelRaisesChangedAndPersistsOnlyWhenAsked()
    {
        GmDisplayCalibration calibration = NewCalibration();
        int changed = 0;
        calibration.Changed += () => changed++;

        calibration.SetLevel(1, persist: false);
        Assert.AreEqual(1, changed);
        Assert.AreEqual(0, PlayerPrefs.GetInt(GmDisplayCalibration.PreferenceKey, 0),
            "a non-persisting change still overwrote the player's saved calibration");

        calibration.SetLevel(-1, persist: true);
        Assert.AreEqual(2, changed);
        Assert.AreEqual(-1, PlayerPrefs.GetInt(GmDisplayCalibration.PreferenceKey, 0));

        // Re-selecting the level already applied is not a change: nothing to re-grade, nothing to save.
        calibration.SetLevel(-1, persist: true);
        Assert.AreEqual(2, changed);
    }

    [Test]
    public void AwakeRestoresThePersistedPreferenceAndGradesTheSceneFromIt()
    {
        PlayerPrefs.SetInt(GmDisplayCalibration.PreferenceKey, 2);
        PlayerPrefs.Save();

        // Awake is the production entry point; EditMode never runs it for a plain MonoBehaviour, so
        // this drives the same body the player's first frame does. Only the standalone proof passes
        // -gmReviewAutoExit, so an editor run always takes the player-preference branch.
        GmDisplayCalibration calibration = NewCalibration();
        Assert.AreEqual(2, calibration.Level, "the saved calibration did not survive the scene load");
        Assert.AreEqual(0.5f, calibration.PostExposureOffset, 0.0001f);
        Assert.AreEqual(0.5f, PostExposure(), 0.0001f);

        // A stored value outside the authored range is clamped on load, never trusted verbatim.
        PlayerPrefs.SetInt(GmDisplayCalibration.PreferenceKey, 40);
        PlayerPrefs.Save();
        GmDisplayCalibration reloaded = NewCalibration();
        Assert.AreEqual(GmDisplayCalibration.MaximumLevel, reloaded.Level);
    }

    [Test]
    public void MeterDrawsTheLevelInsideTheAuthoredRange()
    {
        GmDisplayCalibration calibration = NewCalibration();

        calibration.SetLevel(GmDisplayCalibration.MinimumLevel, persist: false);
        Assert.AreEqual("[|----]", calibration.Meter);
        calibration.SetLevel(0, persist: false);
        Assert.AreEqual("[--|--]", calibration.Meter);
        calibration.SetLevel(GmDisplayCalibration.MaximumLevel, persist: false);
        Assert.AreEqual("[----|]", calibration.Meter);
    }

    // A profile owns its overrides as separate objects; dropping the profile alone strands them.
    static void Release(VolumeProfile profile)
    {
        if (profile == null) return;
        foreach (VolumeComponent component in profile.components)
            if (component != null) Object.DestroyImmediate(component);
        profile.components.Clear();
        Object.DestroyImmediate(profile);
    }

    GmDisplayCalibration NewCalibration()
    {
        var owner = new GameObject("DisplayCalibration");
        var calibration = owner.AddComponent<GmDisplayCalibration>();
        MethodInfo awake = typeof(GmDisplayCalibration).GetMethod("Awake",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(awake, "GmDisplayCalibration lost the Awake that loads and applies calibration");
        awake.Invoke(calibration, null);
        return calibration;
    }

    float PostExposure()
    {
        Assert.IsTrue(gradeVolume.profile.TryGet(out ColorAdjustments colour),
            "the graded Volume lost its ColorAdjustments override");
        Assert.IsTrue(colour.postExposure.overrideState,
            "post exposure is no longer overridden, so the authored grade is whatever the profile shipped with");
        return colour.postExposure.value;
    }
}
