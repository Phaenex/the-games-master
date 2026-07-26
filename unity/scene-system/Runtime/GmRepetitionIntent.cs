using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GmRepetitionIntent : MonoBehaviour
{
    [SerializeField] string intentId;
    [SerializeField, TextArea] string rationale;
    [SerializeField] string[] elementIds = Array.Empty<string>();
    [SerializeField] int minimumDistinctSignatures = 3;
    [SerializeField, Range(0.05f, 1f)] float maximumSignatureShare = 0.4f;
    [SerializeField] int maximumNearIdenticalRun = 2;

    public string IntentId => intentId;
    public string Rationale => rationale;
    public IReadOnlyList<string> ElementIds => elementIds;
    public int MinimumDistinctSignatures => minimumDistinctSignatures;
    public float MaximumSignatureShare => maximumSignatureShare;
    public int MaximumNearIdenticalRun => maximumNearIdenticalRun;

    public void Configure(string id, string why, string[] elements, int minimumSignatures,
        float maximumShare, int maximumRun)
    {
        intentId = id;
        rationale = why;
        elementIds = elements == null ? Array.Empty<string>() : (string[])elements.Clone();
        minimumDistinctSignatures = Mathf.Max(2, minimumSignatures);
        maximumSignatureShare = Mathf.Clamp(maximumShare, 0.05f, 1f);
        maximumNearIdenticalRun = Mathf.Max(1, maximumRun);
    }
}
