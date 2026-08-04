using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps the dense marketplace village bounded around the player at runtime. The far fog hides the
/// transition; the canonical opening never needs all five thousand renderers and every practical
/// light active simultaneously along a 435 metre route.
/// </summary>
public sealed class GmWendRuntimeCulling : MonoBehaviour
{
    const float RendererRange = 20f;
    const float LightRange = 35f;
    const float RefreshDistance = 7f;
    // Enabling/disabling 96 renderers in one moving frame repeatedly pushed the walk probe's p95
    // over budget even though its median stayed near 12ms. A 24-renderer slice still completes a
    // full 2,400-renderer sweep before the player travels the next refresh interval, but distributes
    // the hierarchy/render-state work instead of creating a regular hitch every seven metres.
    const int RenderersPerFrame = 24;
    // The authored interior sits inside the same scene as the 435m outdoor route. Distance culling is
    // the wrong instrument for it — a room is all-or-nothing, and its lights are soft-shadowed points
    // that cost far more than the 20m radius rule assumes. It is gated on the house phase instead,
    // with a bounds fallback so a review camera teleported inside still renders.
    const float InteriorMargin = 6f;
    const int InteriorPerFrame = 24;

    readonly List<Renderer> renderers = new List<Renderer>();
    readonly List<Light> lights = new List<Light>();
    readonly List<Renderer> interiorRenderers = new List<Renderer>();
    readonly List<Light> interiorLights = new List<Light>();
    GmHouseBeginning house;
    Bounds interiorBounds;
    bool interiorBoundsValid;
    bool interiorVisible = true;
    int interiorCursor;
    Transform player;
    Vector3 sweepPosition;
    Vector3 lastCompletedPosition;
    int rendererCursor;
    int lightCursor;
    bool sweeping;

    void Start()
    {
        GmPlayer owner = FindAnyObjectByType<GmPlayer>();
        player = owner != null ? owner.transform : null;
        house = FindAnyObjectByType<GmHouseBeginning>();
        foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude,
                     FindObjectsSortMode.None))
        {
            if (!renderer.enabled || renderer.GetComponent<Terrain>() != null) continue;
            if (renderer.transform.root.name == "HouseBeginning")
            {
                interiorRenderers.Add(renderer);
                if (interiorBoundsValid) interiorBounds.Encapsulate(renderer.bounds);
                else { interiorBounds = renderer.bounds; interiorBoundsValid = true; }
                continue;
            }
            if (renderer.transform.root.name == "GmWendOpening" ||
                renderer.transform.root.name == "WakeRoom" ||
                Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.z) > 250f) continue;
            renderers.Add(renderer);
        }
        foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!light.enabled || light.type == LightType.Directional) continue;
            if (light.transform.root.name == "HouseBeginning") { interiorLights.Add(light); continue; }
            if (light.transform.root.name == "GmWendOpening" || light.transform.root.name == "WakeRoom") continue;
            lights.Add(light);
        }
        BeginSweep();
        while (sweeping) CullSlice(int.MaxValue);
        int activeRenderers = 0;
        foreach (Renderer renderer in renderers)
            if (renderer != null && renderer.enabled) activeRenderers++;
        int activeLights = 0;
        foreach (Light light in lights)
            if (light != null && light.enabled) activeLights++;
        ApplyInteriorVisibility(player != null ? player.position : sweepPosition, int.MaxValue);
        Debug.Log($"[GmWendRuntimeCulling] initial sweep kept {activeRenderers}/{renderers.Count} " +
                  $"renderer(s) within {RendererRange:0}m and {activeLights}/{lights.Count} " +
                  $"light(s) within {LightRange:0}m; interior {(interiorVisible ? "shown" : "hidden")} " +
                  $"({interiorRenderers.Count} renderer(s), {interiorLights.Count} light(s))");
    }

    void Update()
    {
        if (player == null) return;
        ApplyInteriorVisibility(player.position);
        if (!sweeping && Vector2.Distance(new Vector2(player.position.x, player.position.z),
                new Vector2(lastCompletedPosition.x, lastCompletedPosition.z)) >= RefreshDistance)
            BeginSweep();
        if (sweeping) CullSlice(RenderersPerFrame);
    }

    /// <summary>
    /// A room is all-or-nothing: shown once the house phase says the player has crossed inside, or
    /// whenever the viewer is physically within its bounds, so a teleported review camera is never
    /// left staring at an invisible interior. Toggles only on change, so the steady state is free.
    /// </summary>
    void ApplyInteriorVisibility(Vector3 viewerPosition, int budget = InteriorPerFrame)
    {
        int total = interiorRenderers.Count + interiorLights.Count;
        if (total == 0) return;
        bool indoors = house != null && house.Phase != GmHousePhase.WaitingForCrossing;
        bool inside = interiorBoundsValid && interiorBounds.SqrDistance(viewerPosition)
            <= InteriorMargin * InteriorMargin;
        bool wanted = indoors || inside;
        if (wanted != interiorVisible)
        {
            interiorVisible = wanted;
            interiorCursor = 0;   // begin a sliced transition rather than paying it in one frame
        }
        // Flipping all 208 objects in a single moving frame cost a 123ms spike, which is the same
        // mistake the main sweep already learned: distribute the render-state churn.
        while (budget-- > 0 && interiorCursor < total)
        {
            if (interiorCursor < interiorRenderers.Count)
            {
                Renderer renderer = interiorRenderers[interiorCursor];
                if (renderer != null) renderer.enabled = interiorVisible;
            }
            else
            {
                Light light = interiorLights[interiorCursor - interiorRenderers.Count];
                if (light != null) light.enabled = interiorVisible;
            }
            interiorCursor++;
        }
    }

    void BeginSweep()
    {
        if (player == null) return;
        sweepPosition = player.position;
        rendererCursor = 0;
        lightCursor = 0;
        sweeping = true;
    }

    public void CullNowForReview(Vector3 reviewPosition)
    {
        // Review cameras teleport between distant compositions without yielding an Update between
        // shots. Force the same completed local sweep a walking player receives so a shot at the
        // manor is not still rendering the spawn neighbourhood (or hiding the manor neighbourhood).
        sweepPosition = reviewPosition;
        rendererCursor = 0;
        lightCursor = 0;
        sweeping = true;
        while (sweeping) CullSlice(int.MaxValue);
        // A review shot must never catch a half-revealed room, so it completes in one call.
        ApplyInteriorVisibility(reviewPosition, int.MaxValue);
    }

    void CullSlice(int budget)
    {
        int end = Mathf.Min(renderers.Count, rendererCursor + budget);
        for (; rendererCursor < end; rendererCursor++)
        {
            Renderer renderer = renderers[rendererCursor];
            if (renderer == null) continue;
            Bounds bounds = renderer.bounds;
            float allowance = RendererRange + Mathf.Max(bounds.extents.x, bounds.extents.z);
            Vector2 delta = new Vector2(bounds.center.x - sweepPosition.x, bounds.center.z - sweepPosition.z);
            renderer.enabled = delta.sqrMagnitude <= allowance * allowance;
        }
        if (rendererCursor < renderers.Count) return;
        for (; lightCursor < lights.Count; lightCursor++)
        {
            Light light = lights[lightCursor];
            if (light == null) continue;
            Vector2 delta = new Vector2(light.transform.position.x - sweepPosition.x,
                light.transform.position.z - sweepPosition.z);
            light.enabled = delta.sqrMagnitude <= LightRange * LightRange;
        }
        lastCompletedPosition = sweepPosition;
        sweeping = false;
    }
}
