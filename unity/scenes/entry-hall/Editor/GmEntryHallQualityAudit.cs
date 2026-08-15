using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class GmEntryHallQualityAudit
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

    [MenuItem("GamesMaster/Scenes/Audit Entry Hall")]
    public static void Run()
    {
        GmEntryHallBuilder.Build();
        List<string> issues = ValidateOpenScene();
        if (issues.Count > 0)
        {
            foreach (string issue in issues) Debug.LogError("[GmEntryHallAudit] FAILED: " + issue);
            return;
        }
        Debug.Log("[GmEntryHallAudit] PASS: scene contract, 9 portraits, ledger, and composition verified");
    }

    public static List<string> ValidateOpenScene()
    {
        var issues = GmSceneContractAudit.ValidateOpenScene(
            GmEntryHallBuilder.SceneId, GmEntryHallBuilder.DisplayName, GmEntryHallBuilder.ScenePath, RequiredRoots);

        // Room-specific checks
        GameObject gallery = GameObject.Find("PortraitGallery");
        if (gallery == null) issues.Add("PortraitGallery object is missing");
        else if (gallery.transform.childCount < 9)
            issues.Add($"PortraitGallery has {gallery.transform.childCount} children, expected at least 9 portraits");

        if (GameObject.Find("GuestLedger") == null)
            issues.Add("GuestLedger prop is missing");

        if (GameObject.Find("ConsoleTable") == null)
            issues.Add("ConsoleTable prop is missing");

        if (GameObject.Find("MirrorShard_1") == null)
            issues.Add("MirrorShard_1 prop is missing");

        issues.AddRange(GmSceneCompositionAudit.ValidateOpenScene(
            GmEntryHallBuilder.SceneId, Object.FindAnyObjectByType<GmEntryHallShotTour>(), Camera.main));

        return issues;
    }
}
