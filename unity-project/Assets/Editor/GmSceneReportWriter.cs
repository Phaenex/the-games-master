// Machine-readable evidence from a saved generated scene. This is intentionally separate from
// the knowledge ledger: automation may report measurements, but it may not manufacture Nick's
// taste verdict or mark a visual baseline approved.
using System;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GmSceneReportWriter
{
    public static string WriteOpenSceneReport()
    {
        GmSceneComposition composition =
            UnityEngine.Object.FindAnyObjectByType<GmSceneComposition>();
        string sceneId = composition != null ? composition.SceneId : "wend-hill";
        GmSceneReviewTour tour =
            UnityEngine.Object.FindAnyObjectByType<GmSceneReviewTour>();
        Camera camera = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
        GmSceneAuditReport report = composition != null
            ? GmSceneCompositionAudit.AnalyzeOpenScene(composition.SceneId, tour, camera)
            : new GmSceneAuditReport { sceneId = sceneId };
        string directory = Path.Combine(GmSceneIntelligencePaths.LibraryRoot, "audits");
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, $"{sceneId}-audit.json");
        File.WriteAllText(file, JsonUtility.ToJson(report, true) + "\n");
        int errors = report.findings.FindAll(item => item.severity == GmAuditSeverity.Error).Count;
        int warnings = report.findings.FindAll(item => item.severity == GmAuditSeverity.Warning).Count;
        Debug.Log($"[GmSceneReport] {(report.Passed ? "PASS" : "FAILED")} " +
            $"errors={errors} warnings={warnings} findings={report.findings.Count} -> {file}");
        return file;
    }

    public static void WriteWendHillReport()
    {
        Type reportType = Type.GetType("GmWendAutomationReport");
        if (reportType != null)
        {
            var method = reportType.GetMethod("WriteWendHillReport", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            method?.Invoke(null, null);
        }
    }
}
