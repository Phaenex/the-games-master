using System.Collections.Generic;
using UnityEngine;

public sealed class GmWendStoryTour : GmSceneReviewTour
{
    readonly List<GmReviewShot> shots = new List<GmReviewShot>();
    protected override IReadOnlyList<GmReviewShot> ReviewShots
    {
        get
        {
            if (shots.Count == 0) BuildShots();
            return shots;
        }
    }

    protected override string ReviewLogTag => "GmWendReview";

    protected override void BeforeShot(GmReviewShot shot)
    {
        FindAnyObjectByType<GmWendRuntimeCulling>()?.CullNowForReview(shot.Position);
    }

    protected override string ValidateCapturedShot(GmReviewShot shot, Color32[] pixels)
    {
        long sum = 0;
        int nearBlack = 0;
        foreach (Color32 pixel in pixels)
        {
            int luminance = (pixel.r + pixel.g + pixel.b) / 3;
            sum += luminance;
            if (luminance <= 2) nearBlack++;
        }
        float mean = sum / (float)pixels.Length;
        float blackFraction = nearBlack / (float)pixels.Length;
        if (mean < 8f) return $"mean luminance {mean:0.0} is too dark to inspect";
        if (blackFraction > 0.72f) return $"{blackFraction:P1} of the frame is near-black";
        return null;
    }

    void BuildShots()
    {
        shots.Clear();
        AddRoute("01-arrival", 20f, "arrival-car", 3f, 1.1f);
        AddRoute("02-gate", 10f, "gate", -4f, 1.8f);
        AddRoute("03-lookback", 28f, "arrival-car", 2f, 1.2f);
        AddRoute("04-route", 180f, "chapel", -2f, 2.5f);
        AddRoute("05-chapel", 208f, "chapel", -4f, 3f);
        AddRoute("06-grounds", 270f, "garden-shed", -2f, 2f);
        AddRoute("07-manor", 390f, "manor-porch", -8f, 7f);
        AddRoute("08-porch", 426f, "manor-porch", -3f, 2f);
    }

    void AddRoute(string name, float atMetres, string lookId, float pitch, float targetHeight)
    {
        GmWorldAnchor look = GmWorldAnchor.Find(lookId);
        GmRouteSpline route = FindAnyObjectByType<GmRouteSpline>();
        if (route == null || look == null)
        {
            Debug.LogError($"[GmWendReview] missing shot route/anchor: {lookId}");
            return;
        }
        Vector3 target = look.transform.position + Vector3.up * targetHeight;
        Vector3 position = route.PointAt(atMetres) + Vector3.up * 1.7f;
        Vector3 direction = target - position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) direction = route.TangentAt(atMetres);
        float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        shots.Add(new GmReviewShot(name, position, yaw, pitch));
    }
}
