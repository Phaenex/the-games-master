using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Interaction scanner and ray-casting engine for player interaction cones.
/// Handles directional dot-product focus, distance attenuation, obstacle occlusion,
/// and interaction execution with audio/visual feedback.
/// </summary>
public static class GmInteractionEngine
{
    public struct ScanResult
    {
        public GmInteractable bestTarget;
        public float distance;
        public float angleDegrees;
        public bool hasLineOfSight;
    }

    /// <summary>
    /// Scans surrounding interactable objects within player's view cone and line of sight.
    /// </summary>
    public static ScanResult ScanForTarget(Vector3 cameraPosition, Vector3 cameraForward, IEnumerable<GmInteractable> interactables, LayerMask occlusionMask)
    {
        GmInteractable best = null;
        float bestScore = float.MaxValue;
        float bestDistance = 0f;
        float bestAngle = 0f;
        bool bestLos = false;

        if (interactables == null) return default;

        foreach (GmInteractable candidate in interactables)
        {
            if (candidate == null || !candidate.gameObject.activeInHierarchy || !candidate.enabled) continue;

            Vector3 targetPos = candidate.transform.position;
            Vector3 toTarget = targetPos - cameraPosition;
            float dist = toTarget.magnitude;

            if (dist > candidate.Range || dist < 0.05f) continue;

            Vector3 dir = toTarget / dist;
            float dot = Vector3.Dot(cameraForward, dir);
            float angle = Vector3.Angle(cameraForward, dir);

            if (angle > candidate.FocusAngle) continue;

            // Line of sight raycast
            bool occluded = Physics.Raycast(cameraPosition, dir, out RaycastHit hit, dist, occlusionMask) &&
                            hit.collider != null && hit.collider.gameObject != candidate.gameObject &&
                            !hit.collider.transform.IsChildOf(candidate.transform);

            if (occluded) continue;

            // Score favoring lower angles and closer proximity
            float score = angle * 2.0f + dist;
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
                bestDistance = dist;
                bestAngle = angle;
                bestLos = true;
            }
        }

        return new ScanResult
        {
            bestTarget = best,
            distance = bestDistance,
            angleDegrees = bestAngle,
            hasLineOfSight = bestLos
        };
    }
}
