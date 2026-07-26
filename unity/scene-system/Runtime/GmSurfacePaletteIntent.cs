using System;
using System.Collections.Generic;
using UnityEngine;

/// Declares measurable material-family limits for story-critical surfaces. This catches imported
/// ShaderGraph materials whose custom tint/brightness controls silently ignore a generic palette
/// pass, while leaving final taste and candidate selection to the screenshot review.
[DisallowMultipleComponent]
public sealed class GmSurfacePaletteIntent : MonoBehaviour
{
    [SerializeField] string intentId;
    [SerializeField, TextArea] string rationale;
    [SerializeField] GmSurfacePaletteRule[] rules = Array.Empty<GmSurfacePaletteRule>();

    public string IntentId => intentId;
    public string Rationale => rationale;
    public IReadOnlyList<GmSurfacePaletteRule> Rules => rules;

    public void Configure(string id, string why, GmSurfacePaletteRule[] paletteRules)
    {
        intentId = id;
        rationale = why;
        rules = paletteRules == null ? Array.Empty<GmSurfacePaletteRule>() :
            (GmSurfacePaletteRule[])paletteRules.Clone();
    }
}
