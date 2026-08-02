using UnityEngine;

/// <summary>
/// Explicit declaration that a prominent authored object has no interaction. Keeping this separate
/// from GmInteractable gives Unity a durable MonoScript identity when the scene is reloaded.
/// </summary>
[DisallowMultipleComponent]
public sealed class GmIntentionallySilent : MonoBehaviour
{
    [SerializeField, TextArea] string rationale;
    public string Rationale => rationale;
    public void Configure(string why) => rationale = why ?? "";
}

