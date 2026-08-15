using System;
using System.Collections.Generic;
using UnityEngine;

// One review claim says what a screenshot must prove. Foreground, support and background IDs are
// deliberate elements, not automatic counts; a scene author has to name the depth hierarchy.
public sealed class GmReviewCompositionClaim : MonoBehaviour
{
    [SerializeField] string shotName;
    [SerializeField, TextArea] string claim;
    [SerializeField] string primaryElementId;
    [SerializeField] string[] foregroundElementIds = Array.Empty<string>();
    [SerializeField] string[] supportingElementIds = Array.Empty<string>();
    [SerializeField] string[] backgroundElementIds = Array.Empty<string>();
    [SerializeField] Vector2 targetViewport = new Vector2(0.5f, 0.5f);
    [SerializeField] Vector2 viewportTolerance = new Vector2(0.35f, 0.35f);
    [SerializeField] float minimumViewportHeight = 0.03f;
    [SerializeField] float maximumViewportHeight = 0.9f;

    public string ShotName => shotName;
    public string Claim => claim;
    public string PrimaryElementId => primaryElementId;
    public IReadOnlyList<string> ForegroundElementIds => foregroundElementIds;
    public IReadOnlyList<string> SupportingElementIds => supportingElementIds;
    public IReadOnlyList<string> BackgroundElementIds => backgroundElementIds;
    public Vector2 TargetViewport => targetViewport;
    public Vector2 ViewportTolerance => viewportTolerance;
    public float MinimumViewportHeight => minimumViewportHeight;
    public float MaximumViewportHeight => maximumViewportHeight;

    public void Configure(string reviewShotName, string visualClaim, string primaryId,
        string[] foregroundIds, string[] supportingIds, string[] backgroundIds,
        Vector2 target, Vector2 tolerance, float minViewportHeight = 0.03f,
        float maxViewportHeight = 0.9f)
    {
        shotName = reviewShotName;
        claim = visualClaim;
        primaryElementId = primaryId;
        foregroundElementIds = foregroundIds == null ? Array.Empty<string>() : (string[])foregroundIds.Clone();
        supportingElementIds = supportingIds == null ? Array.Empty<string>() : (string[])supportingIds.Clone();
        backgroundElementIds = backgroundIds == null ? Array.Empty<string>() : (string[])backgroundIds.Clone();
        targetViewport = target;
        viewportTolerance = new Vector2(Mathf.Clamp01(tolerance.x), Mathf.Clamp01(tolerance.y));
        minimumViewportHeight = Mathf.Clamp(minViewportHeight, 0.001f, 1f);
        maximumViewportHeight = Mathf.Clamp(maxViewportHeight, minimumViewportHeight, 1f);
    }
}
