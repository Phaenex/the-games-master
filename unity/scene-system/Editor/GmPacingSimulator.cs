// Deterministic route/timeline comparison for candidate cadences. It measures opportunity and
// quiet gaps; it does not choose which duration feels tense.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
                    intent.FirstSymptomToll));
        return new GmPacingSimulationReport {
            sceneId = sceneId,
            rows = rows.ToArray(),
        };
    }

    public static GmPacingSimulationRow Simulate(GmPacingCandidate candidate,
        GmPacingScenario scenario, float maximumQuietSeconds, int firstSymptomToll)
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
        for (int toll = 1; toll <= 9; toll++)
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
        if (composition == null || intent == null)
            throw new InvalidOperationException("Open scene needs composition and pacing intent.");
        GmPacingSimulationReport report = Simulate(composition.SceneId, intent);
        string directory = Path.Combine(GmSceneIntelligencePaths.LibraryRoot, "pacing");
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, $"{composition.SceneId}-simulation.json");
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
