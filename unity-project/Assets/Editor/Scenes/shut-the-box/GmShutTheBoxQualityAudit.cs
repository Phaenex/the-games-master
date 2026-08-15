using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class GmShutTheBoxQualityAudit
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

    [MenuItem("GamesMaster/Scenes/Audit Shut the Box")]
    public static void Run()
    {
        GmShutTheBoxBuilder.Build();
        List<string> issues = ValidateOpenScene();
        if (issues.Count > 0)
        {
            foreach (string issue in issues) Debug.LogError("[GmShutTheBoxAudit] FAILED: " + issue);
            return;
        }
        Debug.Log("[GmShutTheBoxAudit] PASS: scene contract, 2 boxes, 18 tiles, dice, and secret door verified");
    }

    public static List<string> ValidateOpenScene()
    {
        var issues = GmSceneContractAudit.ValidateOpenScene(
            GmShutTheBoxBuilder.SceneId, GmShutTheBoxBuilder.DisplayName, GmShutTheBoxBuilder.ScenePath, RequiredRoots);

        // Room-specific checks. These names resolve to one object each: the composition plan marks
        // the builder's geometry rather than standing up same-named empties beside it.
        GameObject playerBox = GameObject.Find("PlayerBox");
        if (playerBox == null) issues.Add("PlayerBox object is missing");
        else if (CountTiles(playerBox) < 9)
            issues.Add($"PlayerBox has {CountTiles(playerBox)} tiles, expected 9");

        GameObject hostBox = GameObject.Find("HostBox");
        if (hostBox == null) issues.Add("HostBox object is missing");
        else if (CountTiles(hostBox) < 9)
            issues.Add($"HostBox has {CountTiles(hostBox)} tiles, expected 9");

        if (GameObject.Find("DiceTray") == null)
            issues.Add("DiceTray prop is missing");

        if (GameObject.Find("PanelDoor") == null)
            issues.Add("PanelDoor secret exit is missing");

        var systems = GameObject.Find("SceneSystems");
        if (systems == null)
        {
            issues.Add("SceneSystems root is missing");
        }
        else if (systems.GetComponent<GmShutTheBoxController>() == null)
        {
            issues.Add("GmShutTheBoxController component is missing from SceneSystems");
        }

        issues.AddRange(GmSceneCompositionAudit.ValidateOpenScene(
            GmShutTheBoxBuilder.SceneId, Object.FindAnyObjectByType<GmShutTheBoxShotTour>(), Camera.main));

        return issues;
    }

    // Counts the engraved tiles wherever they hang, so regrouping them under a tile root cannot
    // quietly turn a nine-tile board into a one-child board as far as this audit is concerned.
    static int CountTiles(GameObject box)
    {
        int tiles = 0;
        foreach (Transform child in box.GetComponentsInChildren<Transform>(true))
            if (child != box.transform && child.name.StartsWith("Tile_")) tiles++;
        return tiles;
    }
}
