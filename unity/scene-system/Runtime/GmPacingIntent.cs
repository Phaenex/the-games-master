using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GmPacingIntent : MonoBehaviour
{
    [SerializeField] string intentId;
    [SerializeField, TextArea] string rationale;
    [SerializeField] int canonicalTollCount = 9;
    [SerializeField] int firstSymptomToll = 4;
    [SerializeField] int blackoutToll = 9;
    [SerializeField] float maximumUnintendedQuietSeconds = 35f;
    [SerializeField] GmPacingCandidate[] candidates = Array.Empty<GmPacingCandidate>();
    [SerializeField] GmPacingScenario[] scenarios = Array.Empty<GmPacingScenario>();

    public string IntentId => intentId;
    public string Rationale => rationale;
    public int CanonicalTollCount => canonicalTollCount;
    public int FirstSymptomToll => firstSymptomToll;
    public int BlackoutToll => blackoutToll;
    public float MaximumUnintendedQuietSeconds => maximumUnintendedQuietSeconds;
    public IReadOnlyList<GmPacingCandidate> Candidates => candidates;
    public IReadOnlyList<GmPacingScenario> Scenarios => scenarios;

    public void Configure(string id, string why, int tollCount, int symptomToll, int finalToll,
        float maximumQuietSeconds, GmPacingCandidate[] cadenceCandidates)
    {
        intentId = id;
        rationale = why;
        canonicalTollCount = Mathf.Max(1, tollCount);
        firstSymptomToll = Mathf.Clamp(symptomToll, 1, canonicalTollCount);
        blackoutToll = Mathf.Clamp(finalToll, firstSymptomToll, canonicalTollCount);
        maximumUnintendedQuietSeconds = Mathf.Max(1f, maximumQuietSeconds);
        candidates = cadenceCandidates == null ? Array.Empty<GmPacingCandidate>() :
            (GmPacingCandidate[])cadenceCandidates.Clone();
    }

    public void ConfigureScenarios(GmPacingScenario[] routeScenarios)
    {
        scenarios = routeScenarios == null ? Array.Empty<GmPacingScenario>() :
            (GmPacingScenario[])routeScenarios.Clone();
    }
}
