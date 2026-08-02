using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GmInteractionScanner : MonoBehaviour
{
    [SerializeField, Min(1f)] float maximumScanRange = 5f;
    [SerializeField, Range(0.01f, 0.2f)] float sphereRadius = 0.07f;
    [SerializeField, Min(0f)] float focusHysteresisSeconds = 0.10f;
    Camera viewCamera;
    GmDesignRuntime runtime;
    GmInteractable focused;
    float focusLostAt = -1f;
    RaycastHit[] hitBuffer = new RaycastHit[32];

    public GmInteractable Focused => focused;
    public bool HasFocus => focused != null;
    public string PromptText => focused == null ? "" : focused.Prompt;

    void Awake()
    {
        viewCamera = GetComponentInChildren<Camera>();
    }

    void Update()
    {
        if (runtime == null) runtime = FindAnyObjectByType<GmDesignRuntime>();
        Scan();
    }

    public void Scan()
    {
        if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
        if (viewCamera == null) { SetFocus(null); return; }

        Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
        int hitCount;
        while (true)
        {
            hitCount = Physics.SphereCastNonAlloc(ray, sphereRadius, hitBuffer, maximumScanRange,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (hitCount < hitBuffer.Length || hitBuffer.Length >= 256) break;
            Array.Resize(ref hitBuffer, hitBuffer.Length * 2);
        }

        GmInteractable candidate = null;
        float candidateScore = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hitBuffer[i];
            GmInteractable target = hit.collider.GetComponentInParent<GmInteractable>();
            if (target == null || !target.enabled || hit.distance > target.Range ||
                !IsWithinFocus(target, hit.point, ray.direction) || !HasLineOfSight(target, hit.point)) continue;
            float score = Score(target, hit.point, ray.direction);
            if (score >= candidateScore) continue;
            candidate = target;
            candidateScore = score;
        }

        if (candidate != null)
        {
            focusLostAt = -1f;
            SetFocus(candidate);
        }
        else if (focused != null)
        {
            if (focusLostAt < 0f) focusLostAt = Time.unscaledTime;
            if (Time.unscaledTime - focusLostAt >= focusHysteresisSeconds) SetFocus(null);
        }
    }

    public bool TryInteract()
    {
        if (focused == null) return false;
        bool result = focused.Interact(runtime);
        GmExperienceTelemetry.Record(result ? "interaction" : "interaction-failed", focused.InteractionId);
        return result;
    }

    bool IsWithinFocus(GmInteractable target, Vector3 point, Vector3 forward)
    {
        Vector3 direction = point - viewCamera.transform.position;
        return direction.sqrMagnitude > 0.0001f && Vector3.Angle(forward, direction) <= target.FocusAngle;
    }

    bool HasLineOfSight(GmInteractable target, Vector3 point)
    {
        Vector3 origin = viewCamera.transform.position;
        Vector3 direction = point - origin;
        float distance = direction.magnitude;
        if (distance <= 0.001f) return true;
        if (!Physics.Raycast(origin, direction / distance, out RaycastHit first, distance + 0.04f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
        return first.collider.GetComponentInParent<GmInteractable>() == target;
    }

    float Score(GmInteractable target, Vector3 point, Vector3 forward)
    {
        Vector3 delta = point - viewCamera.transform.position;
        return delta.magnitude + Vector3.Angle(forward, delta) * 0.18f + (target == focused ? -0.35f : 0f);
    }

    void SetFocus(GmInteractable next)
    {
        if (focused == next) return;
        if (focused != null) GmExperienceTelemetry.Record("focus-exit", focused.InteractionId);
        focused = next;
        if (focused != null) GmExperienceTelemetry.Record("focus-enter", focused.InteractionId);
    }
}
