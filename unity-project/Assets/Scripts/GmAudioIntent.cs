using UnityEngine;

[DisallowMultipleComponent]
public sealed class GmAudioIntent : MonoBehaviour
{
    [SerializeField] string intentId;
    [SerializeField] GmAudioIntentKind kind;
    [SerializeField] GmAudioLoopPolicy loopPolicy;
    [SerializeField] string sourceElementId;
    [SerializeField, TextArea] string rationale;

    public string IntentId => intentId;
    public GmAudioIntentKind Kind => kind;
    public GmAudioLoopPolicy LoopPolicy => loopPolicy;
    public string SourceElementId => sourceElementId;
    public string Rationale => rationale;

    public void Configure(string id, GmAudioIntentKind intentKind, GmAudioLoopPolicy loop,
        string why, string sourceId = "")
    {
        intentId = id;
        kind = intentKind;
        loopPolicy = loop;
        rationale = why;
        sourceElementId = sourceId ?? "";
    }
}
