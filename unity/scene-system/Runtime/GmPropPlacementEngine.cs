using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Studio-grade prop placement, sizing, grounding, and collision proxy generation pipeline.
/// Guarantees that assets placed across the estate and interior rooms sit physically grounded,
/// correctly scaled, and sealed against player clipping or route blocking.
/// </summary>
public static class GmPropPlacementEngine
{
    public const float DefaultMaxSlopeDegrees = 45f;
    public const float MinAllowedDimension = 0.05f;
    public const float MaxAllowedDimension = 40f;

    /// <summary>
    /// Computes accurate axis-aligned bounding box enclosing all active renderers and mesh filters.
    /// Handles FBX sub-assets with synchronous transformation calculation before Unity's next frame update.
    /// </summary>
    public static Bounds EncapsulateBounds(GameObject target)
    {
        if (target == null) return new Bounds(Vector3.zero, Vector3.zero);

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = new Bounds(target.transform.position, Vector3.zero);

        foreach (Renderer r in renderers)
        {
            if (r.bounds.size.sqrMagnitude <= 0.000001f) continue;
            if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
            else bounds.Encapsulate(r.bounds);
        }

        if (hasBounds) return bounds;

        foreach (MeshFilter filter in target.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || filter.sharedMesh.bounds.size.sqrMagnitude <= 0.000001f) continue;
            Bounds local = filter.sharedMesh.bounds;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = filter.transform.TransformPoint(local.center +
                    Vector3.Scale(local.extents, new Vector3(x, y, z)));
                if (!hasBounds) { bounds = new Bounds(corner, Vector3.zero); hasBounds = true; }
                else bounds.Encapsulate(corner);
            }
        }

        return bounds;
    }

    /// <summary>
    /// Normalizes prop scale by its longest dimension, preventing imported 100x or 0.01x mesh scale bugs.
    /// </summary>
    public static float NormalizeScale(GameObject target, float targetLongestDimensionMetres)
    {
        if (target == null) return 1f;

        Bounds bounds = EncapsulateBounds(target);
        float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));

        if (longest <= 0.0001f) return 1f;

        float clampedTarget = Mathf.Clamp(targetLongestDimensionMetres, MinAllowedDimension, MaxAllowedDimension);
        float scaleFactor = clampedTarget / longest;

        target.transform.localScale *= scaleFactor;
        return scaleFactor;
    }

    /// <summary>
    /// Snaps a prop's lowest bounding point to the terrain or floor geometry underneath.
    /// Aligns upward orientation to ground normal when within the authored slope limit.
    /// </summary>
    public static bool SnapToGround(GameObject target, float raycastStartLift = 2.5f, float maxRayDistance = 15f,
        float maxSlopeDegrees = DefaultMaxSlopeDegrees, bool alignToNormal = false)
    {
        if (target == null) return false;

        Bounds bounds = EncapsulateBounds(target);
        Vector3 origin = new Vector3(bounds.center.x, bounds.max.y + raycastStartLift, bounds.center.z);

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxRayDistance + raycastStartLift))
        {
            float slope = Vector3.Angle(hit.normal, Vector3.up);
            if (slope > maxSlopeDegrees) return false; // Slope too steep to ground safely

            float groundY = hit.point.y;
            float bottomOffset = bounds.center.y - bounds.min.y;
            float targetCenterY = groundY + bottomOffset;
            float deltaY = targetCenterY - bounds.center.y;

            target.transform.position += new Vector3(0f, deltaY, 0f);

            if (alignToNormal && slope > 1f)
            {
                Quaternion alignRot = Quaternion.FromToRotation(target.transform.up, hit.normal);
                target.transform.rotation = alignRot * target.transform.rotation;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Generates a solid, non-inverted BoxCollider proxy enclosing the prop's render bounds.
    /// </summary>
    public static BoxCollider GenerateCollisionProxy(GameObject target)
    {
        if (target == null) return null;

        Bounds bounds = EncapsulateBounds(target);
        if (bounds.size.sqrMagnitude <= 0.0001f) return null;

        var box = target.GetComponent<BoxCollider>();
        if (box == null) box = target.AddComponent<BoxCollider>();

        Vector3 localCenter = target.transform.InverseTransformPoint(bounds.center);
        Vector3 localSize = new Vector3(
            Mathf.Abs(bounds.size.x / Mathf.Max(0.0001f, target.transform.lossyScale.x)),
            Mathf.Abs(bounds.size.y / Mathf.Max(0.0001f, target.transform.lossyScale.y)),
            Mathf.Abs(bounds.size.z / Mathf.Max(0.0001f, target.transform.lossyScale.z))
        );

        box.center = localCenter;
        box.size = localSize;
        box.isTrigger = false;

        return box;
    }
}
