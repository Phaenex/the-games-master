using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class GmBonesQualityAudit
{
    static readonly string[] Roots = { "SceneSystems", "Environment", "Gameplay", "Lighting", "Composition", "Player" };

    [MenuItem("GamesMaster/Scenes/Audit Bones")]
    public static void Run()
    {
        GmBonesBuilder.Build();
        List<string> issues = ValidateOpenScene();
        if (issues.Count == 0) Debug.Log("[GmBonesAudit] PASS: physical table, dice, light, dust, host and direct-review contract verified");
        else foreach (string issue in issues) Debug.LogError("[GmBonesAudit] FAILED: " + issue);
    }

    public static List<string> ValidateOpenScene()
    {
        List<string> issues = GmSceneContractAudit.ValidateOpenScene(
            GmBonesBuilder.SceneId, GmBonesBuilder.DisplayName, GmBonesBuilder.ScenePath, Roots);
        GmBonesDieView[] dice = Object.FindObjectsByType<GmBonesDieView>(FindObjectsInactive.Include);
        if (dice.Length != 3) issues.Add($"expected exactly 3 physical dice, found {dice.Length}");
        int pips = dice.Sum(die => die.GetComponentsInChildren<GmPhysicalDiePip>(true).Length);
        if (pips != 63) issues.Add($"expected exactly 63 separately modeled pips, found {pips}");
        if (Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude).Length != 1)
            issues.Add("scene needs exactly one active camera");
        if (Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude).Length != 1)
            issues.Add("scene needs exactly one active listener");
        GmBonesSceneHost host = Object.FindAnyObjectByType<GmBonesSceneHost>();
        if (host == null || !host.IsConfigured) issues.Add("Bones host is missing or unconfigured");
        GameObject light = GameObject.Find("BonesTableTaskLight");
        if (light == null || light.GetComponent<GmPeriodLampFlicker>() != null)
            issues.Add("stable dice task light is missing or flickering");
        if (Object.FindObjectsByType<GmBonesDustField>(FindObjectsInactive.Include)
            .Any(field => !field.IsOutsideDiceCone)) issues.Add("dust enters the dice readability cone");
        GmBonesShotTour tour = Object.FindAnyObjectByType<GmBonesShotTour>();
        if (tour == null || tour.ShotCount != 10 || tour.HasPlaceholderShots)
            issues.Add("ten-shot non-placeholder Bones tour is missing");
        issues.AddRange(GmSceneCompositionAudit.ValidateOpenScene(GmBonesBuilder.SceneId, tour, Camera.main));
        return issues;
    }
}
