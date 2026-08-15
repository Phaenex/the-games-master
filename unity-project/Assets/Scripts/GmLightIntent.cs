using UnityEngine;

[DisallowMultipleComponent]
public sealed class GmLightIntent : MonoBehaviour
{
    [SerializeField] string intentId;
    [SerializeField] GmLightIntentKind kind;
    [SerializeField] string sourceElementId;
    [SerializeField] string subjectElementId;
    [SerializeField, TextArea] string rationale;
    [SerializeField] float maximumSourceDistance = 1.5f;
    [SerializeField] float maximumSubjectDistance = 16f;

    public string IntentId => intentId;
    public GmLightIntentKind Kind => kind;
    public string SourceElementId => sourceElementId;
    public string SubjectElementId => subjectElementId;
    public string Rationale => rationale;
    public float MaximumSourceDistance => maximumSourceDistance;
    public float MaximumSubjectDistance => maximumSubjectDistance;

    public void Configure(string id, GmLightIntentKind intentKind, string why,
        string sourceId = "", string subjectId = "", float maxSourceDistance = 1.5f,
        float maxSubjectDistance = 16f)
    {
        intentId = id;
        kind = intentKind;
        rationale = why;
        sourceElementId = sourceId ?? "";
        subjectElementId = subjectId ?? "";
        maximumSourceDistance = Mathf.Max(0.05f, maxSourceDistance);
        maximumSubjectDistance = Mathf.Max(0.05f, maxSubjectDistance);
    }
}
