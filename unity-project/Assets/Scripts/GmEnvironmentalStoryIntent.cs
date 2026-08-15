using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GmEnvironmentalStoryIntent : MonoBehaviour
{
    [SerializeField] string intentId;
    [SerializeField] string zoneId;
    [SerializeField, TextArea] string intendedInference;
    [SerializeField] string anchorElementId;
    [SerializeField] string[] traceElementIds = Array.Empty<string>();
    [SerializeField] GmStoryRevealStep[] revealSteps = Array.Empty<GmStoryRevealStep>();

    public string IntentId => intentId;
    public string ZoneId => zoneId;
    public string IntendedInference => intendedInference;
    public string AnchorElementId => anchorElementId;
    public IReadOnlyList<string> TraceElementIds => traceElementIds;
    public IReadOnlyList<GmStoryRevealStep> RevealSteps => revealSteps;

    public void Configure(string id, string zone, string inference, string anchorId,
        string[] traceIds, GmStoryRevealStep[] steps)
    {
        intentId = id;
        zoneId = zone;
        intendedInference = inference;
        anchorElementId = anchorId;
        traceElementIds = traceIds == null ? Array.Empty<string>() : (string[])traceIds.Clone();
        revealSteps = steps == null ? Array.Empty<GmStoryRevealStep>() : (GmStoryRevealStep[])steps.Clone();
    }
}
