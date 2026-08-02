using System;
using System.Collections.Generic;
using UnityEngine;

public enum GmCraftDepthLayer
{
    Foreground,
    Middle,
    Background
}

[Serializable]
public sealed class GmCraftZoneRule
{
    [SerializeField] string zoneId;
    [SerializeField, TextArea] string visualThesis;
    [SerializeField, TextArea] string storyState;
    [SerializeField] string focalElementId;
    [SerializeField] string[] foregroundElementIds = Array.Empty<string>();
    [SerializeField] string[] middleElementIds = Array.Empty<string>();
    [SerializeField] string[] backgroundElementIds = Array.Empty<string>();
    [SerializeField, Range(0f, 1f)] float maximumEmptyGroundShare = 0.46f;
    [SerializeField, Range(1, 5)] int maximumRepeatedSilhouetteRun = 3;

    public string ZoneId => zoneId;
    public string VisualThesis => visualThesis;
    public string StoryState => storyState;
    public string FocalElementId => focalElementId;
    public IReadOnlyList<string> ForegroundElementIds => foregroundElementIds;
    public IReadOnlyList<string> MiddleElementIds => middleElementIds;
    public IReadOnlyList<string> BackgroundElementIds => backgroundElementIds;
    public float MaximumEmptyGroundShare => maximumEmptyGroundShare;
    public int MaximumRepeatedSilhouetteRun => maximumRepeatedSilhouetteRun;

    public GmCraftZoneRule(string id, string thesis, string story, string focal,
        string[] foreground, string[] middle, string[] background,
        float maximumEmptyGround = 0.46f, int maximumRepeatedRun = 3)
    {
        zoneId = id;
        visualThesis = thesis;
        storyState = story;
        focalElementId = focal;
        foregroundElementIds = foreground ?? Array.Empty<string>();
        middleElementIds = middle ?? Array.Empty<string>();
        backgroundElementIds = background ?? Array.Empty<string>();
        maximumEmptyGroundShare = Mathf.Clamp01(maximumEmptyGround);
        maximumRepeatedSilhouetteRun = Mathf.Clamp(maximumRepeatedRun, 1, 5);
    }
}

/// <summary>
/// Human-authored visual contract for a scene. This is deliberately descriptive rather than
/// generative: the builder must satisfy it, audits can challenge it, and review can approve it,
/// but the component never scatters props or claims that a scene is beautiful.
/// </summary>
[DisallowMultipleComponent]
public sealed class GmSceneCraftProfile : MonoBehaviour
{
    [SerializeField] string sceneId;
    [SerializeField, TextArea] string artDirection;
    [SerializeField] int deterministicSeed;
    [SerializeField] GmCraftZoneRule[] zones = Array.Empty<GmCraftZoneRule>();

    public string SceneId => sceneId;
    public string ArtDirection => artDirection;
    public int DeterministicSeed => deterministicSeed;
    public IReadOnlyList<GmCraftZoneRule> Zones => zones;

    public void Configure(string id, string direction, int seed, GmCraftZoneRule[] rules)
    {
        sceneId = id;
        artDirection = direction;
        deterministicSeed = seed;
        zones = rules ?? Array.Empty<GmCraftZoneRule>();
    }
}

