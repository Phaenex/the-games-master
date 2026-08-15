using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class GmHiddenRoomQualityAudit
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

    [MenuItem("GamesMaster/Scenes/Audit Hidden Room")]
    public static void Run()
    {
        GmHiddenRoomBuilder.Build();
        List<string> issues = ValidateOpenScene();
        if (issues.Count > 0)
        {
            foreach (string issue in issues) Debug.LogError("[GmHiddenRoomAudit] FAILED: " + issue);
            return;
        }
        Debug.Log("[GmHiddenRoomAudit] PASS: scene contract, desk, mirror frame, journals, and Shard #3 verified");
    }

    public static List<string> ValidateOpenScene()
    {
        var issues = GmSceneContractAudit.ValidateOpenScene(
            GmHiddenRoomBuilder.SceneId, GmHiddenRoomBuilder.DisplayName, GmHiddenRoomBuilder.ScenePath, RequiredRoots);

        // Room-specific checks
        if (GameObject.Find("RolltopDesk") == null)
            issues.Add("RolltopDesk object is missing");

        if (GameObject.Find("InvitationLetter") == null)
            issues.Add("InvitationLetter object is missing");

        if (GameObject.Find("StandingMirrorFrame") == null)
            issues.Add("StandingMirrorFrame object is missing");

        if (GameObject.Find("MirrorShard_3") == null)
            issues.Add("MirrorShard_3 prop is missing");

        if (GameObject.Find("ArchiveShelf") == null)
            issues.Add("ArchiveShelf object is missing");

        var systems = GameObject.Find("SceneSystems");
        if (systems == null)
        {
            issues.Add("SceneSystems root is missing");
        }
        else if (systems.GetComponent<GmHiddenRoomController>() == null)
        {
            issues.Add("GmHiddenRoomController component is missing from SceneSystems");
        }

        issues.AddRange(GmSceneCompositionAudit.ValidateOpenScene(
            GmHiddenRoomBuilder.SceneId, Object.FindAnyObjectByType<GmHiddenRoomShotTour>(), Camera.main));

        return issues;
    }
}
