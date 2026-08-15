using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class GmLabyrinthQualityAudit
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

    [MenuItem("GamesMaster/Scenes/Audit Labyrinth")]
    public static void Run()
    {
        GmLabyrinthBuilder.Build();
        List<string> issues = ValidateOpenScene();
        if (issues.Count > 0)
        {
            foreach (string issue in issues) Debug.LogError("[GmLabyrinthAudit] FAILED: " + issue);
            return;
        }
        Debug.Log("[GmLabyrinthAudit] PASS: scene contract, 7x7 hedges, mirror shrine, and exit gate verified");
    }

    public static List<string> ValidateOpenScene()
    {
        var issues = GmSceneContractAudit.ValidateOpenScene(
            GmLabyrinthBuilder.SceneId, GmLabyrinthBuilder.DisplayName, GmLabyrinthBuilder.ScenePath, RequiredRoots);

        // Room-specific checks
        if (GameObject.Find("EntranceCryptArch") == null)
            issues.Add("EntranceCryptArch object is missing");

        if (GameObject.Find("MirrorShrinePedestal") == null)
            issues.Add("MirrorShrinePedestal object is missing");

        if (GameObject.Find("ExitWroughtGate") == null)
            issues.Add("ExitWroughtGate object is missing");

        if (GameObject.Find("HuntsmanLantern") == null)
            issues.Add("HuntsmanLantern prop is missing");

        var systems = GameObject.Find("SceneSystems");
        if (systems == null)
        {
            issues.Add("SceneSystems root is missing");
        }
        else
        {
            if (systems.GetComponent<GmLabyrinthGenerator>() == null)
                issues.Add("GmLabyrinthGenerator component is missing from SceneSystems");
            if (systems.GetComponent<GmHuntsmanAI>() == null)
                issues.Add("GmHuntsmanAI component is missing from SceneSystems");
        }

        issues.AddRange(GmSceneCompositionAudit.ValidateOpenScene(
            GmLabyrinthBuilder.SceneId, Object.FindAnyObjectByType<GmLabyrinthShotTour>(), Camera.main));

        return issues;
    }
}
