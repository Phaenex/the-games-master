using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GmLandscapeDepthIntent : MonoBehaviour
{
    [SerializeField] string intentId;
    [SerializeField, TextArea] string rationale;
    [SerializeField] GmLandscapeShotRequirement[] shots = Array.Empty<GmLandscapeShotRequirement>();

    public string IntentId => intentId;
    public string Rationale => rationale;
    public IReadOnlyList<GmLandscapeShotRequirement> Shots => shots;

    public void Configure(string id, string why, GmLandscapeShotRequirement[] requirements)
    {
        intentId = id;
        rationale = why;
        shots = requirements == null ? Array.Empty<GmLandscapeShotRequirement>() :
            (GmLandscapeShotRequirement[])requirements.Clone();
    }
}
