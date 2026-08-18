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

        if (GameObject.Find("LibraryDoor") == null)
            issues.Add("LibraryDoor is missing");
        if (GameObject.Find("ConservatoryDoor") == null)
            issues.Add("ConservatoryDoor is missing");
        if (GameObject.Find("FrontDoors") == null)
            issues.Add("FrontDoors are missing");
        if (GameObject.Find("CellarPanel") == null)
            issues.Add("CellarPanel is missing");
        if (GameObject.Find("PercivalDoor") == null)
            issues.Add("PercivalDoor is missing");
        if (GameObject.Find("MarrDoor") == null)
            issues.Add("MarrDoor is missing");
        if (GameObject.Find("BarredGuestDoor") == null)
            issues.Add("BarredGuestDoor is missing");
        if (GameObject.Find("AtticHatch") == null)
            issues.Add("AtticHatch is missing");
        if (GameObject.Find("BrassSkeletonKey") == null)
            issues.Add("BrassSkeletonKey is missing");
        if (GameObject.Find("StairTreads") == null)
            issues.Add("walkable StairTreads are missing — the staircase is still a solid box");
        if (GameObject.Find("SecondFloorGallery") == null)
            issues.Add("SecondFloorGallery is missing");
        if (GameObject.Find("PercivalBedroom") == null)
            issues.Add("PercivalBedroom is missing");
        if (GameObject.Find("NorthLibrary") == null)
            issues.Add("NorthLibrary is missing");
        if (GameObject.Find("LibraryReadingTable") == null)
            issues.Add("LibraryReadingTable is missing");
        if (GameObject.Find("LibraryLadder") == null)
            issues.Add("LibraryLadder is missing");
        if (GameObject.Find("WeightedShelfCase") == null)
            issues.Add("WeightedShelfCase is missing");
        if (Object.FindAnyObjectByType<GmWeightedShelf>(FindObjectsInactive.Include) == null)
            issues.Add("GmWeightedShelf is missing");
        if (GameObject.Find("UpperDebtorGallery") == null)
            issues.Add("UpperDebtorGallery is missing");
        if (GameObject.Find("MarrStudy") == null)
            issues.Add("MarrStudy is missing");
        if (GameObject.Find("BarredGuestRoom") == null)
            issues.Add("BarredGuestRoom is missing");
        if (GameObject.Find("LadyMarrKey") == null)
            issues.Add("LadyMarrKey is missing");
        if (GameObject.Find("AtticLoft") == null)
            issues.Add("AtticLoft is missing");
        if (GameObject.Find("AtticHatchKey") == null)
            issues.Add("AtticHatchKey is missing");
        if (GameObject.Find("AtticLadder") == null)
            issues.Add("AtticLadder is missing");
        if (GameObject.Find("MirrorShard_2") == null)
            issues.Add("MirrorShard_2 prop is missing");

        issues.AddRange(GmSceneCompositionAudit.ValidateOpenScene(
            GmEntryHallBuilder.SceneId, Object.FindAnyObjectByType<GmEntryHallShotTour>(), Camera.main));

        return issues;
    }
}
