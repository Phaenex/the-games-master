using UnityEngine;

/// <summary>
/// Restrained whole-tree movement for calibrated estate canopies. The purchased foliage shader
/// produced severe moon-white cards, so Wend uses a predictable HDRP/Lit cutout and moves the tree
/// by a fraction of a degree at its authored root. The displacement is centimetres at crown height,
/// deterministic per placed tree, and keeps every purchased mesh/material untouched.
/// </summary>
[DisallowMultipleComponent]
public sealed class GmCanopySway : MonoBehaviour
{
    [SerializeField, Range(0.05f, 0.22f)] float amplitudeDegrees = 0.14f;
    [SerializeField, Range(0.035f, 0.11f)] float primaryFrequencyHz = 0.07f;

    Quaternion restRotation;
    bool hasRestRotation;

    public float AmplitudeDegrees => amplitudeDegrees;
    public float PrimaryFrequencyHz => primaryFrequencyHz;
    public float MaximumAngleDegrees => amplitudeDegrees * 1.28f;

    public void Configure(float amplitude, float frequencyHz)
    {
        amplitudeDegrees = Mathf.Clamp(amplitude, 0.05f, 0.22f);
        primaryFrequencyHz = Mathf.Clamp(frequencyHz, 0.035f, 0.11f);
    }

    void OnEnable()
    {
        restRotation = transform.localRotation;
        hasRestRotation = true;
    }

    void LateUpdate()
    {
        if (!hasRestRotation)
        {
            restRotation = transform.localRotation;
            hasRestRotation = true;
        }
        Vector2 offset = EvaluateOffsetDegrees(Time.time);
        transform.localRotation = restRotation * Quaternion.Euler(offset.x, 0f, offset.y);
    }

    void OnDisable()
    {
        if (hasRestRotation) transform.localRotation = restRotation;
        hasRestRotation = false;
    }

    /// <summary>Pure deterministic sample used by the structural audit as well as runtime motion.</summary>
    public Vector2 EvaluateOffsetDegrees(float timeSeconds)
    {
        float phase = PlacementPhase();
        float primary = timeSeconds * primaryFrequencyHz * Mathf.PI * 2f + phase;
        float secondary = timeSeconds * primaryFrequencyHz * 2.37f * Mathf.PI * 2f + phase * 0.61f;
        float x = Mathf.Sin(primary) * amplitudeDegrees +
            Mathf.Sin(secondary) * amplitudeDegrees * 0.28f;
        float z = Mathf.Cos(primary * 0.83f + 1.7f) * amplitudeDegrees * 0.72f +
            Mathf.Sin(secondary * 0.71f) * amplitudeDegrees * 0.19f;
        return new Vector2(x, z);
    }

    float PlacementPhase()
    {
        unchecked
        {
            uint hash = 2166136261u;
            Vector3 p = transform.position;
            hash = (hash ^ (uint)Mathf.RoundToInt((p.x + 256f) * 100f)) * 16777619u;
            hash = (hash ^ (uint)Mathf.RoundToInt((p.z + 256f) * 100f)) * 16777619u;
            for (int i = 0; i < gameObject.name.Length; i++)
                hash = (hash ^ gameObject.name[i]) * 16777619u;
            return (hash & 0x00ffffffu) / 16777215f * Mathf.PI * 2f;
        }
    }
}

