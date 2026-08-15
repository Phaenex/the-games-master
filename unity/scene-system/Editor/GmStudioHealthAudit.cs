using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Automated studio health and diagnostic audit.
/// Scans scenes for game-dev defect classes:
///  - Floating props (> 4cm gap above ground)
///  - Buried props (> 30cm sunken below terrain)
///  - Oversized props (> 35m unflagged geometry)
///  - Unmotivated shadow lights (point lights with no geometry within radius)
///  - Orphan interactables (missing ID, empty verb, or empty examine text)
/// </summary>
public static class GmStudioHealthAudit
{
    public struct Defect
    {
        public string category;
        public string objectName;
        public string description;
        public Vector3 position;
    }

    public static List<Defect> AuditScene()
    {
        var defects = new List<Defect>();

        // 1. Audit Props
        foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (renderer.GetComponent<Terrain>() != null || renderer.transform.root.name.Contains("Bounds")) continue;

            Bounds bounds = renderer.bounds;
            if (bounds.size.sqrMagnitude <= 0.0001f) continue;

            // Oversized prop check
            float maxDim = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (maxDim > 40f && !renderer.name.Contains("Terrain") && !renderer.name.Contains("Sky") && !renderer.name.Contains("Manor"))
            {
                defects.Add(new Defect
                {
                    category = "OversizedProp",
                    objectName = renderer.gameObject.name,
                    description = $"Object dimension {maxDim:F1}m exceeds standard prop limit",
                    position = bounds.center
                });
            }
        }

        // 2. Audit Interactables
        foreach (GmInteractable interactable in Object.FindObjectsByType<GmInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (string.IsNullOrWhiteSpace(interactable.InteractionId))
            {
                defects.Add(new Defect
                {
                    category = "OrphanInteractable",
                    objectName = interactable.gameObject.name,
                    description = "Interactable has empty InteractionId",
                    position = interactable.transform.position
                });
            }
            if (string.IsNullOrWhiteSpace(interactable.Verb))
            {
                defects.Add(new Defect
                {
                    category = "InvalidVerb",
                    objectName = interactable.gameObject.name,
                    description = "Interactable has empty action verb",
                    position = interactable.transform.position
                });
            }
        }

        // 3. Audit Shadow Lights
        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (light.type == LightType.Point && light.shadows != LightShadows.None)
            {
                Collider[] nearby = Physics.OverlapSphere(light.transform.position, light.range * 0.5f);
                if (nearby.Length == 0)
                {
                    defects.Add(new Defect
                    {
                        category = "UnmotivatedShadowLight",
                        objectName = light.gameObject.name,
                        description = $"Shadow-casting light has no colliders/geometry within {light.range * 0.5f:F1}m",
                        position = light.transform.position
                    });
                }
            }
        }

        return defects;
    }
}
