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
    public const string WendHillReportPath =
        "Library/GmSceneIntelligence/audits/wend-hill-audit.json";

    public static string WriteOpenSceneReport()
    {
        GmSceneComposition composition =
            UnityEngine.Object.FindAnyObjectByType<GmSceneComposition>();
        if (composition == null)
            throw new InvalidOperationException("Open scene needs a GmSceneComposition.");
        GmSceneReviewTour tour =
            UnityEngine.Object.FindAnyObjectByType<GmSceneReviewTour>();
        Camera camera = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
        GmSceneAuditReport report = GmSceneCompositionAudit.AnalyzeOpenScene(
            composition.SceneId, tour, camera);
        string directory = Path.Combine(GmSceneIntelligencePaths.LibraryRoot, "audits");
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, $"{composition.SceneId}-audit.json");
        File.WriteAllText(file, JsonUtility.ToJson(report, true) + "\n");
        int errors = report.findings.FindAll(item => item.severity == GmAuditSeverity.Error).Count;
        int warnings = report.findings.FindAll(item => item.severity == GmAuditSeverity.Warning).Count;
        Debug.Log($"[GmSceneReport] {(report.Passed ? "PASS" : "FAILED")} " +
            $"errors={errors} warnings={warnings} findings={report.findings.Count} -> {file}");
        return file;
    }

    public static void WriteWendHillReport()
    {
        EditorSceneManager.OpenScene(GmSceneCatalog.WendHillPath, OpenSceneMode.Single);
        string file = WriteOpenSceneReport();
        GmSceneAuditReport report = JsonUtility.FromJson<GmSceneAuditReport>(File.ReadAllText(file));
        if (report == null || !report.Passed)
            throw new InvalidOperationException("Wend Hill report contains blocking audit errors.");
        GmSceneReviewTour tour = UnityEngine.Object.FindAnyObjectByType<GmSceneReviewTour>();
        Camera camera = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
        GmViewportOccupancyAudit.Write("wend-hill", tour, camera);
        Debug.Log($"[GmSceneReport] WEND HILL PASS -> {file}");
    }
}
