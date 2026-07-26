// Authored perceptual and experience contracts. These components describe what a scene must read
// like from declared review positions. They never move objects or choose a candidate.
using System;
using System.Collections.Generic;
using UnityEngine;

public enum GmEra
{
    Unspecified,
    Contemporary,
    Victorian,
    Medieval,
    TimelessNatural
}

[Serializable]
public sealed class GmStoryRevealStep
{
    [SerializeField] string shotName;
    [SerializeField] string[] requiredElementIds = Array.Empty<string>();

    public string ShotName => shotName;
    public IReadOnlyList<string> RequiredElementIds => requiredElementIds;

    public GmStoryRevealStep(string reviewShotName, params string[] elementIds)
    {
        shotName = reviewShotName;
        requiredElementIds = elementIds == null ? Array.Empty<string>() : (string[])elementIds.Clone();
    }
}

[Serializable]
public sealed class GmStyleShotBudget
{
    [SerializeField] string shotName;
    [SerializeField, Range(0.001f, 1f)] float maximumViewportArea = 0.12f;

    public string ShotName => shotName;
    public float MaximumViewportArea => maximumViewportArea;

    public GmStyleShotBudget(string reviewShotName, float maximumArea)
    {
        shotName = reviewShotName;
        maximumViewportArea = Mathf.Clamp(maximumArea, 0.001f, 1f);
    }
}

[Serializable]
public sealed class GmSurfacePaletteRule
{
    [SerializeField] string elementId;
    [SerializeField, Range(0f, 1f)] float minimumTintLuminance;
    [SerializeField, Range(0f, 1f)] float maximumTintLuminance = 1f;
    [SerializeField, Range(0f, 1f)] float maximumSaturation = 1f;
    [SerializeField] bool requireWarmBias;
    [SerializeField] bool requireInspectableTint = true;
    [SerializeField] float maximumBrightnessMultiplier = 1f;

    public string ElementId => elementId;
    public float MinimumTintLuminance => minimumTintLuminance;
    public float MaximumTintLuminance => maximumTintLuminance;
    public float MaximumSaturation => maximumSaturation;
    public bool RequireWarmBias => requireWarmBias;
    public bool RequireInspectableTint => requireInspectableTint;
    public float MaximumBrightnessMultiplier => maximumBrightnessMultiplier;

    public GmSurfacePaletteRule(string subjectElementId, float minimumLuminance,
        float maximumLuminance, float maximumColourSaturation, bool warmBias,
        bool inspectableTint = true, float maximumBrightness = 1f)
    {
        elementId = subjectElementId;
        minimumTintLuminance = Mathf.Clamp01(minimumLuminance);
        maximumTintLuminance = Mathf.Clamp(maximumLuminance, minimumTintLuminance, 1f);
        maximumSaturation = Mathf.Clamp01(maximumColourSaturation);
        requireWarmBias = warmBias;
        requireInspectableTint = inspectableTint;
        maximumBrightnessMultiplier = Mathf.Max(0f, maximumBrightness);
    }
}

[Serializable]
public sealed class GmLandscapeShotRequirement
{
    [SerializeField] string shotName;
    [SerializeField] string[] foregroundElementIds = Array.Empty<string>();
    [SerializeField] string[] middleElementIds = Array.Empty<string>();
    [SerializeField] string[] farElementIds = Array.Empty<string>();
    [SerializeField, Range(0f, 1f)] float minimumFarHorizontalCoverage = 0.55f;
    [SerializeField, Range(0f, 1f)] float maximumFarHorizontalCoverage = 0.85f;

    public string ShotName => shotName;
    public IReadOnlyList<string> ForegroundElementIds => foregroundElementIds;
    public IReadOnlyList<string> MiddleElementIds => middleElementIds;
    public IReadOnlyList<string> FarElementIds => farElementIds;
    public float MinimumFarHorizontalCoverage => minimumFarHorizontalCoverage;
    public float MaximumFarHorizontalCoverage => maximumFarHorizontalCoverage;

    public GmLandscapeShotRequirement(string reviewShotName, string[] foregroundIds,
        string[] middleIds, string[] farIds, float minimumFarCoverage, float maximumFarCoverage)
    {
        shotName = reviewShotName;
        foregroundElementIds = foregroundIds == null ? Array.Empty<string>() : (string[])foregroundIds.Clone();
        middleElementIds = middleIds == null ? Array.Empty<string>() : (string[])middleIds.Clone();
        farElementIds = farIds == null ? Array.Empty<string>() : (string[])farIds.Clone();
        minimumFarHorizontalCoverage = Mathf.Clamp01(minimumFarCoverage);
        maximumFarHorizontalCoverage = Mathf.Clamp(maximumFarCoverage,
            minimumFarHorizontalCoverage, 1f);
    }
}

[Serializable]
public sealed class GmPacingCandidate
{
    [SerializeField] string candidateId;
    [SerializeField] float firstTollDelay;
    [SerializeField] float tollInterval;

    public string CandidateId => candidateId;
    public float FirstTollDelay => firstTollDelay;
    public float TollInterval => tollInterval;
    public float TotalDuration => firstTollDelay + tollInterval * 8f;

    public GmPacingCandidate(string id, float firstDelay, float interval)
    {
        candidateId = id;
        firstTollDelay = Mathf.Max(0f, firstDelay);
        tollInterval = Mathf.Max(0f, interval);
    }
}

[Serializable]
public sealed class GmPacingScenario
{
    [SerializeField] string scenarioId;
    [SerializeField] float walkingMetersPerSecond = 2.5f;
    [SerializeField] Vector3[] waypoints = Array.Empty<Vector3>();
    [SerializeField] float[] dwellSeconds = Array.Empty<float>();

    public string ScenarioId => scenarioId;
    public float WalkingMetersPerSecond => walkingMetersPerSecond;
    public IReadOnlyList<Vector3> Waypoints => waypoints;
    public IReadOnlyList<float> DwellSeconds => dwellSeconds;

    public GmPacingScenario(string id, float speed, Vector3[] routeWaypoints, float[] dwellAtWaypoints = null)
    {
        scenarioId = id;
        walkingMetersPerSecond = Mathf.Max(0.1f, speed);
        waypoints = routeWaypoints == null ? Array.Empty<Vector3>() : (Vector3[])routeWaypoints.Clone();
        dwellSeconds = dwellAtWaypoints == null ? Array.Empty<float>() : (float[])dwellAtWaypoints.Clone();
    }
}

public static class GmPerceptualAuthoring
{
    static T AddOrGet<T>(GameObject owner) where T : Component =>
        owner.GetComponent<T>() ?? owner.AddComponent<T>();

    public static GmRepetitionIntent Repetition(GameObject owner, string id, string rationale,
        string[] elements, int minimumSignatures, float maximumShare, int maximumRun)
    {
        var value = AddOrGet<GmRepetitionIntent>(owner);
        value.Configure(id, rationale, elements, minimumSignatures, maximumShare, maximumRun);
        return value;
    }

    public static GmEnvironmentalStoryIntent Story(GameObject owner, string id, string zone,
        string inference, string anchorId, string[] traces, GmStoryRevealStep[] steps)
    {
        var value = AddOrGet<GmEnvironmentalStoryIntent>(owner);
        value.Configure(id, zone, inference, anchorId, traces, steps);
        return value;
    }

    public static GmStyleIntent Style(GameObject owner, string id, string elementId, GmEra era,
        GmEra contextEra, bool deliberateContrast, string rationale, float maximumSmoothness,
        GmStyleShotBudget[] budgets)
    {
        var value = AddOrGet<GmStyleIntent>(owner);
        value.Configure(id, elementId, era, contextEra, deliberateContrast, rationale,
            maximumSmoothness, budgets);
        return value;
    }

    public static GmSurfacePaletteIntent SurfacePalette(GameObject owner, string id,
        string rationale, GmSurfacePaletteRule[] rules)
    {
        var value = AddOrGet<GmSurfacePaletteIntent>(owner);
        value.Configure(id, rationale, rules);
        return value;
    }

    public static GmLandscapeDepthIntent Landscape(GameObject owner, string id, string rationale,
        GmLandscapeShotRequirement[] shots)
    {
        var value = AddOrGet<GmLandscapeDepthIntent>(owner);
        value.Configure(id, rationale, shots);
        return value;
    }

    public static GmSoundscapeIntent Soundscape(GameObject owner, string id, string character,
        int maximumBeds, float minimumSilence, float maximumToneDb,
        string[] continuousClipResourceNames = null)
    {
        var value = AddOrGet<GmSoundscapeIntent>(owner);
        value.Configure(id, character, maximumBeds, minimumSilence, maximumToneDb,
            true, continuousClipResourceNames);
        return value;
    }

    public static GmPacingIntent Pacing(GameObject owner, string id, string rationale,
        int tollCount, int firstSymptom, int blackout, float maximumQuiet,
        GmPacingCandidate[] candidates, GmPacingScenario[] scenarios = null)
    {
        var value = AddOrGet<GmPacingIntent>(owner);
        value.Configure(id, rationale, tollCount, firstSymptom, blackout, maximumQuiet, candidates);
        value.ConfigureScenarios(scenarios);
        return value;
    }
}
