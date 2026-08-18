using System;
using System.Collections.Generic;
using System.Linq;
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
        SetCoachInteriorForShot(shot);
    }

    public override void PrepareShotForAudit(GmReviewShot shot) => SetCoachInteriorForShot(shot);

    public override void RestoreAfterAuditShot(GmReviewShot shot)
    {
        GameObject coachHouse = GameObject.Find("CoachHouse");
        Transform interior = coachHouse != null ? coachHouse.transform.Find("Interior") : null;
        if (interior != null) interior.gameObject.SetActive(false);
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
        AddArrivalShot();
        AddRoute("02-gate", 10f, "gate", -4f, 1.8f);
        AddRoute("03-lookback", 28f, "arrival-car", 2f, 1.2f);
        Vector3 chapelTarget = ChapelRendererBounds().center;
        AddRouteTarget("04-route", 180f, chapelTarget, -2f);
        GmRouteSpline route = FindAnyObjectByType<GmRouteSpline>();
        if (route != null)
        {
            Vector3 chapelPosition = PositionAlongReveal(
                route.PointAt(180f) + Vector3.up * 1.7f, chapelTarget, 12f);
            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null)
                chapelPosition.y = terrain.SampleHeight(chapelPosition) + terrain.transform.position.y + 1.7f;
            shots.Add(LookAt("05-chapel", chapelPosition, chapelTarget, -3f));
        }
        AddRoute("06-grounds", 270f, "garden-basin", -2f, 1.8f);
        AddRoute("07-manor", 390f, "manor-porch", -8f, 7f);
        AddRoute("08-porch", 426f, "manor-porch", -3f, 2f);
        AddCoachHouseShots();
    }

    void AddArrivalShot()
    {
        GmRouteSpline route = FindAnyObjectByType<GmRouteSpline>();
        GmWorldAnchor car = GmWorldAnchor.Find("arrival-car");
        GameObject carVisual = car != null && car.transform.parent != null
            ? car.transform.parent.Find("ArrivalCar")?.gameObject
            : null;
        Renderer[] carRenderers = carVisual != null
            ? carVisual.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.GetComponentInParent<Light>() == null)
                .ToArray()
            : Array.Empty<Renderer>();
        if (route == null || car == null || carRenderers.Length == 0)
        {
            Debug.LogError("[GmWendReview] missing route, arrival-car anchor or rendered car for arrival shot");
            return;
        }

        Bounds bounds = carRenderers[0].bounds;
        foreach (Renderer renderer in carRenderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        // Face the lit front of the vehicle from a raised three-quarter angle. The former lateral
        // verge camera was below the car beltline and filled 85% of the frame with an unlit slope;
        // the car technically intersected the frustum but read as four black circles. Deriving this
        // from the real post-pivot bounds keeps the purchased model framed after future rebuilds.
        Vector3 forward = route.TangentAt(0f).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 position = bounds.center + forward * 7f + right * 2.6f;
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y + 1.9f;
        else
            position.y = bounds.center.y + 0.55f;
        position.y = Mathf.Max(position.y, bounds.center.y + 0.55f);
        shots.Add(LookAt("01-arrival", position, bounds.center + Vector3.up * 0.08f, 1f));
    }

    static void SetCoachInteriorForShot(GmReviewShot shot)
    {
        GameObject coachHouse = GameObject.Find("CoachHouse");
        Transform interior = coachHouse != null ? coachHouse.transform.Find("Interior") : null;
        if (interior != null) interior.gameObject.SetActive(shot.Name == "10-coach-interior");
    }

    static Bounds ChapelRendererBounds()
    {
        GmWorldAnchor anchor = GmWorldAnchor.Find("chapel");
        Renderer[] church = FindObjectsByType<Renderer>(FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Where(candidate => candidate.enabled && candidate.name.StartsWith("SM_Church", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (church.Length == 0)
        {
            Vector3 fallback = anchor != null ? anchor.transform.position + Vector3.up * 3f : Vector3.zero;
            Debug.LogError("[GmWendReview] no rendered SM_Church geometry for chapel shots");
            return new Bounds(fallback, Vector3.one);
        }

        Renderer nearest = anchor == null
            ? church[0]
            : church.OrderBy(candidate => candidate.bounds.SqrDistance(anchor.transform.position)).First();
        return nearest.bounds;
    }

    void AddCoachHouseShots()
    {
        GmWorldAnchor door = GmWorldAnchor.Find("coach-doors");
        GameObject coachHouse = GameObject.Find("CoachHouse");
        if (door == null || coachHouse == null)
        {
            Debug.LogError("[GmWendReview] missing coach house or coach-doors anchor");
            return;
        }

        Transform room = coachHouse.transform;
        Vector3 approach = door.transform.position - room.forward * 6.5f + room.right * 2.4f + Vector3.up * 2.0f;
        Vector3 facadeTarget = door.transform.position + Vector3.up * 2.1f;
        shots.Add(LookAt("09-coach-approach", approach, facadeTarget, -2f));

        Vector3 interior = door.transform.position + room.forward * 1.8f + Vector3.up * 1.7f;
        Vector3 stallTarget = door.transform.position + room.forward * 6.4f + room.right * 1.2f + Vector3.up * 1.3f;
        shots.Add(LookAt("10-coach-interior", interior, stallTarget, -3f));
    }

    static GmReviewShot LookAt(string name, Vector3 position, Vector3 target, float pitch)
    {
        Vector3 direction = target - position;
        direction.y = 0f;
        float yaw = direction.sqrMagnitude < 0.001f
            ? 0f
            : Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        return new GmReviewShot(name, position, yaw, pitch);
    }

    public static Vector3 PositionAlongReveal(Vector3 routeReveal, Vector3 target, float standOff)
    {
        if (standOff <= 0f) throw new ArgumentOutOfRangeException(nameof(standOff));
        Vector3 towardReveal = routeReveal - target;
        towardReveal.y = 0f;
        if (towardReveal.sqrMagnitude < 0.0001f)
            throw new ArgumentException("route reveal and target need distinct horizontal positions");
        Vector3 position = target + towardReveal.normalized * standOff;
        position.y = routeReveal.y;
        return position;
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

    void AddRouteTarget(string name, float atMetres, Vector3 target, float pitch)
    {
        GmRouteSpline route = FindAnyObjectByType<GmRouteSpline>();
        if (route == null)
        {
            Debug.LogError($"[GmWendReview] missing route for shot {name}");
            return;
        }
        Vector3 position = route.PointAt(atMetres) + Vector3.up * 1.7f;
        Vector3 direction = target - position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) direction = route.TangentAt(atMetres);
        float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        shots.Add(new GmReviewShot(name, position, yaw, pitch));
    }
}
