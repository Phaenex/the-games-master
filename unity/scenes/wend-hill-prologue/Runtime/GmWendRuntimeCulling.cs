using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages interior visibility for HouseBeginning. All outdoor estate renderers, landscape,
/// and practical lights remain fully active and loaded, allowing HDRP's native GPU culling
/// to handle frustum and occlusion without CPU renderer toggling or pop-in.
/// </summary>
public sealed class GmWendRuntimeCulling : MonoBehaviour
{
    const float InteriorMargin = 6f;
    const int InteriorPerFrame = 64;

    readonly List<Renderer> interiorRenderers = new List<Renderer>();
    readonly List<Light> interiorLights = new List<Light>();
    GmHouseBeginning house;
    Bounds interiorBounds;
    bool interiorBoundsValid;
    bool interiorVisible = false;
    int interiorCursor;
    Transform player;

    void Start()
    {
        GmPlayer owner = FindAnyObjectByType<GmPlayer>();
        player = owner != null ? owner.transform : null;
        house = FindAnyObjectByType<GmHouseBeginning>();

        foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (renderer == null || !renderer.enabled || renderer.GetComponent<Terrain>() != null) continue;
            if (renderer.transform.root.name == "HouseBeginning")
            {
                interiorRenderers.Add(renderer);
                if (interiorBoundsValid) interiorBounds.Encapsulate(renderer.bounds);
                else { interiorBounds = renderer.bounds; interiorBoundsValid = true; }
            }
        }

        foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (light == null || !light.enabled || light.type == LightType.Directional) continue;
            if (light.transform.root.name == "HouseBeginning")
            {
                interiorLights.Add(light);
            }
        }

        ApplyInteriorVisibility(player != null ? player.position : Vector3.zero, int.MaxValue);
        Debug.Log($"[GmWendRuntimeCulling] outdoor estate fully active and loaded; interior {(interiorVisible ? "shown" : "hidden")} ({interiorRenderers.Count} renderers, {interiorLights.Count} lights)");
    }

    void Update()
    {
        if (player == null) return;
        ApplyInteriorVisibility(player.position);
    }

    public void CullNowForReview(Vector3 reviewPosition)
    {
        ApplyInteriorVisibility(reviewPosition, int.MaxValue);
    }

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
            interiorCursor = 0;
        }

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
}
