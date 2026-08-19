using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class GmCourtQualityAudit
{
    static readonly string[] RequiredRoots =
    {
        // "Player", not "ReviewCamera": this room now contains a body, and the review tour already
        // prefers the player's own camera when one exists (GmSceneReviewTour picks
        // player.GetComponentInChildren<Camera>() over Camera.main). A standalone ReviewCamera
        // alongside it would be a second live camera and a second AudioListener -- the exact
        // fault that made the first prologue build render a purchased pack's beauty shot
        // instead of the player's eye.
        "SceneSystems", "Environment", "Gameplay", "Lighting", "Composition", "Player"
    };

    [MenuItem("GamesMaster/Scenes/Audit Court")]
    public static void Run()
    {
        GmCourtBuilder.Build();
        List<string> issues = ValidateOpenScene();
        if (issues.Count > 0)
        {
            foreach (string issue in issues) Debug.LogError("[GmCourtAudit] FAILED: " + issue);
            return;
        }
        Debug.Log("[GmCourtAudit] PASS: scene contract, 9 jury chairs, evidence table, and gavel verified");
    }

    public static List<string> ValidateOpenScene()
    {
        var issues = GmSceneContractAudit.ValidateOpenScene(
            GmCourtBuilder.SceneId, GmCourtBuilder.DisplayName, GmCourtBuilder.ScenePath, RequiredRoots);

        // Room-specific checks
        GameObject juryBox = GameObject.Find("JuryBox");
        if (juryBox == null) issues.Add("JuryBox object is missing");
        else if (juryBox.transform.childCount < 9)
            issues.Add($"JuryBox has {juryBox.transform.childCount} chairs, expected 9");

        if (GameObject.Find("WitnessDock") == null)
            issues.Add("WitnessDock object is missing");

        if (GameObject.Find("EvidenceTable") == null)
            issues.Add("EvidenceTable object is missing");

        if (GameObject.Find("BrassGavel") == null)
            issues.Add("BrassGavel prop is missing");

        if (GameObject.Find("MirrorShard_2") == null)
            issues.Add("MirrorShard_2 prop is missing");

        var systems = GameObject.Find("SceneSystems");
        if (systems == null)
        {
            issues.Add("SceneSystems root is missing");
        }
        else if (systems.GetComponent<GmCourtController>() == null)
        {
            issues.Add("GmCourtController component is missing from SceneSystems");
        }
        else
        {
            if (systems.GetComponent<GmCourtHud>() == null)
                issues.Add("GmCourtHud is missing; the hearing has no player-facing evidence surface");
            if (systems.GetComponent<GmCourtInput>() == null)
                issues.Add("GmCourtInput is missing; controller/keyboard evidence input is not wired");
            if (systems.GetComponent<GmCourtPresenter>() == null)
                issues.Add("GmCourtPresenter is missing; seals, role light and gavel tell cannot move");
        }

        issues.AddRange(GmSceneCompositionAudit.ValidateOpenScene(
            GmCourtBuilder.SceneId, Object.FindAnyObjectByType<GmCourtShotTour>(), Camera.main));

        return issues;
    }
}
