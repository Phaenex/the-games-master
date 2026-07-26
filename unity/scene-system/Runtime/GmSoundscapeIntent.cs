using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GmSoundscapeIntent : MonoBehaviour
{
    [SerializeField] string intentId;
    [SerializeField, TextArea] string desiredCharacter;
    [SerializeField] int maximumContinuousBeds = 1;
    [SerializeField, Range(0f, 1f)] float minimumSilenceShare = 0.2f;
    [SerializeField] float maximumPersistentToneDb = 8f;
    [SerializeField] bool forbidMachineLikeTonality = true;
    [SerializeField] string[] continuousClipResourceNames = Array.Empty<string>();

    public string IntentId => intentId;
    public string DesiredCharacter => desiredCharacter;
    public int MaximumContinuousBeds => maximumContinuousBeds;
    public float MinimumSilenceShare => minimumSilenceShare;
    public float MaximumPersistentToneDb => maximumPersistentToneDb;
    public bool ForbidMachineLikeTonality => forbidMachineLikeTonality;
    public IReadOnlyList<string> ContinuousClipResourceNames => continuousClipResourceNames;

    public void Configure(string id, string character, int maximumBeds, float minimumSilence,
        float maximumToneDb, bool forbidMachineTonality = true, string[] continuousClips = null)
    {
        intentId = id;
        desiredCharacter = character;
        maximumContinuousBeds = Mathf.Max(0, maximumBeds);
        minimumSilenceShare = Mathf.Clamp01(minimumSilence);
        maximumPersistentToneDb = Mathf.Max(0f, maximumToneDb);
        forbidMachineLikeTonality = forbidMachineTonality;
        continuousClipResourceNames = continuousClips == null ? Array.Empty<string>() :
            (string[])continuousClips.Clone();
    }
}
