using UnityEngine;

// Legacy practical marker retained for already-authored scene packs. New work uses GmLightIntent.
[DisallowMultipleComponent]
public sealed class GmMotivatedLight : MonoBehaviour
{
    [SerializeField] string lightId;
    [SerializeField] string sourceElementId;
    [SerializeField, TextArea] string purpose;
    [SerializeField] float maximumSourceDistance = 1.5f;

    public string LightId => lightId;
    public string SourceElementId => sourceElementId;
    public string Purpose => purpose;
    public float MaximumSourceDistance => maximumSourceDistance;

    public void Configure(string id, string sourceId, string why, float maxSourceDistance = 1.5f)
    {
        lightId = id;
        sourceElementId = sourceId ?? "";
        purpose = why;
        maximumSourceDistance = Mathf.Max(0.05f, maxSourceDistance);
    }
}
