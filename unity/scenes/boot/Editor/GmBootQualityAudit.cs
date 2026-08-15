// Structural gate for Boot. Add room-specific, objective checks here; visual
// intent and spatial relationships use the shared composition audit; taste remains visual/human.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class GmBootQualityAudit
{
    static readonly string[] RequiredRoots =
    {
        "SceneSystems", "Environment", "Gameplay", "Lighting", "Composition", "ReviewCamera"
    };

    [MenuItem("GamesMaster/Scenes/Audit Boot")]
    public static void Run()
    {
        GmBootBuilder.Build();
        List<string> issues = ValidateOpenScene();
        if (issues.Count > 0)
        {
            foreach (string issue in issues) Debug.LogError("[GmBootAudit] FAILED: " + issue);
            return;
        }
        Debug.Log("[GmBootAudit] PASS: scene contract and required roots verified");
    }

    public static List<string> ValidateOpenScene()
    {
        var issues = GmSceneContractAudit.ValidateOpenScene(
            GmBootBuilder.SceneId, GmBootBuilder.DisplayName, GmBootBuilder.ScenePath, RequiredRoots);
        issues.AddRange(GmSceneCompositionAudit.ValidateOpenScene(
            GmBootBuilder.SceneId, Object.FindAnyObjectByType<GmBootShotTour>(), Camera.main));
        return issues;
    }
}
