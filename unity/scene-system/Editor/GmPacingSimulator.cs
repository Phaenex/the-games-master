// Deterministic route/timeline comparison for candidate cadences. It measures opportunity and
// quiet gaps; it does not choose which duration feels tense.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

[Serializable]
public sealed class GmPacingSimulationRow
{
    public string candidateId;
    public string scenarioId;
    public float blackoutSeconds;
    public float routeCompletionSeconds;
    public int opportunitiesBeforeFirstSymptom;
    public float maximumEventGapSeconds;
    public bool routeCompletesBeforeBlackout;
    public bool exceedsQuietBudget;
}

[Serializable]
public sealed class GmPacingSimulationReport
{
    public int schemaVersion = 1;
    public string sceneId;
    public GmPacingSimulationRow[] rows;
}

public static class GmPacingSimulator
{
    public const string WendHillReportPath = "Library/GmSceneIntelligence/pacing/wend-hill-simulation.json";

    public static GmPacingSimulationReport Simulate(string sceneId, GmPacingIntent intent)
    {
        if (intent == null) throw new ArgumentNullException(nameof(intent));
        var rows = new List<GmPacingSimulationRow>();
        foreach (GmPacingCandidate candidate in intent.Candidates)
            foreach (GmPacingScenario scenario in intent.Scenarios)
                rows.Add(Simulate(candidate, scenario, intent.MaximumUnintendedQuietSeconds,
                    intent.CanonicalTollCount, intent.FirstSymptomToll));
        return new GmPacingSimulationReport {
            sceneId = sceneId,
            rows = rows.ToArray(),
        };
    }

    public static GmPacingSimulationRow Simulate(GmPacingCandidate candidate,
        GmPacingScenario scenario, float maximumQuietSeconds, int canonicalTollCount, int firstSymptomToll)
    {
        var opportunityTimes = new List<float>();
        float elapsed = 0f;
        for (int i = 1; i < scenario.Waypoints.Count; i++)
        {
            elapsed += Vector3.Distance(scenario.Waypoints[i - 1], scenario.Waypoints[i]) /
                scenario.WalkingMetersPerSecond;
            if (i - 1 < scenario.DwellSeconds.Count) elapsed += Mathf.Max(0f, scenario.DwellSeconds[i - 1]);
            opportunityTimes.Add(elapsed);
        }
        float routeCompletion = elapsed;
        float blackout = candidate.TotalDuration;
        var events = new List<float> { 0f };
        events.AddRange(opportunityTimes.Where(value => value <= blackout));
        // The toll count comes from the intent. Canon is nine and GmPerceptualAudit is where that is
        // asserted, but this path writes evidence without going through the audit, so a hardcoded
        // nine here would have described a timeline the scene does not play.
        int tolls = Mathf.Max(1, canonicalTollCount);
        for (int toll = 1; toll <= tolls; toll++)
            events.Add(candidate.FirstTollDelay + (toll - 1) * candidate.TollInterval);
        events.Sort();
        float maximumGap = 0f;
        for (int i = 1; i < events.Count; i++) maximumGap = Mathf.Max(maximumGap, events[i] - events[i - 1]);
        float symptomTime = candidate.FirstTollDelay + (firstSymptomToll - 1) * candidate.TollInterval;
        return new GmPacingSimulationRow {
            candidateId = candidate.CandidateId,
            scenarioId = scenario.ScenarioId,
            blackoutSeconds = blackout,
            routeCompletionSeconds = routeCompletion,
            opportunitiesBeforeFirstSymptom = opportunityTimes.Count(value => value < symptomTime),
            maximumEventGapSeconds = maximumGap,
            routeCompletesBeforeBlackout = routeCompletion <= blackout,
            exceedsQuietBudget = maximumGap > maximumQuietSeconds + 0.0001f,
        };
    }

    public static string WriteOpenSceneReport()
    {
        GmSceneComposition composition = UnityEngine.Object.FindAnyObjectByType<GmSceneComposition>();
        GmPacingIntent intent = UnityEngine.Object.FindAnyObjectByType<GmPacingIntent>();
        string sceneId = composition != null ? composition.SceneId : "wend-hill";
        if (intent == null)
        {
            var go = new GameObject("GmPacingFallback");
            intent = go.AddComponent<GmPacingIntent>();
            intent.Configure("wend-hill-pacing", "Canonical Ninth Bell 285s countdown",
                9, 4, 9, 35f, new[] {
                    new GmPacingCandidate("tight-195", 35f, 20f),
                    new GmPacingCandidate("middle-240", 40f, 25f),
                    new GmPacingCandidate("control-285", 45f, 30f),
                });
            intent.ConfigureScenarios(new[] {
                new GmPacingScenario("steady-walk", 3.4f,
                    new[] { Vector3.zero, new Vector3(0f, 0f, 100f), new Vector3(0f, 0f, 250f), new Vector3(0f, 0f, 435f) },
                    new[] { 3f, 5f, 4f, 0f }),
                new GmPacingScenario("exploratory-walk", 2.2f,
                    new[] { Vector3.zero, new Vector3(0f, 0f, 50f), new Vector3(0f, 0f, 150f), new Vector3(0f, 0f, 300f), new Vector3(0f, 0f, 435f) },
                    new[] { 6f, 8f, 6f, 5f, 0f })
            });
        }
        GmPacingSimulationReport report = Simulate(sceneId, intent);
        string directory = Path.Combine(GmSceneIntelligencePaths.LibraryRoot, "pacing");
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, $"{sceneId}-simulation.json");
        File.WriteAllText(file, JsonUtility.ToJson(report, true) + "\n");
        Debug.Log($"[GmPacingSimulator] PASS rows={report.rows.Length} -> {file}");
        return file;
    }

    public static void WriteWendHillReport()
    {
        EditorSceneManager.OpenScene(GmSceneCatalog.WendHillPath, OpenSceneMode.Single);
        string file = WriteOpenSceneReport();
        Debug.Log($"[GmPacingSimulator] WEND HILL PASS -> {file}");
    }
}
