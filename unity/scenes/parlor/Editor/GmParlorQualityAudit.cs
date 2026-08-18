using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class GmParlorQualityAudit
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

    [MenuItem("GamesMaster/Scenes/Audit Parlor")]
    public static void Run()
    {
        GmParlorBuilder.Build();
        List<string> issues = ValidateOpenScene();
        if (issues.Count > 0)
        {
            foreach (string issue in issues) Debug.LogError("[GmParlorAudit] FAILED: " + issue);
            return;
        }
        Debug.Log("[GmParlorAudit] PASS: scene contract, card table, Aldric chair, and composition verified");
    }

    public static List<string> ValidateOpenScene()
    {
        var issues = GmSceneContractAudit.ValidateOpenScene(
            GmParlorBuilder.SceneId, GmParlorBuilder.DisplayName, GmParlorBuilder.ScenePath, RequiredRoots);

        // Room-specific checks
        if (GameObject.Find("CardTable") == null)
            issues.Add("CardTable object is missing");

        if (GameObject.Find("AldricChair") == null)
            issues.Add("AldricChair object is missing");

        if (GameObject.Find("BankerLamp") == null)
            issues.Add("BankerLamp object is missing");

        if (GameObject.Find("StoneMantel") == null)
            issues.Add("StoneMantel object is missing");

        var systems = GameObject.Find("SceneSystems");
        if (systems == null)
        {
            issues.Add("SceneSystems root is missing");
        }
        else
        {
            if (systems.GetComponent<GmParlorRules>() == null)
                issues.Add("GmParlorRules component is missing from SceneSystems");
            if (systems.GetComponent<GmParlorController>() == null)
                issues.Add("GmParlorController component is missing from SceneSystems");
            if (systems.GetComponent<GmParlorInput>() == null)
                issues.Add("GmParlorInput component is missing from SceneSystems");
            if (systems.GetComponent<GmParlorFocusView>() == null)
                issues.Add("GmParlorFocusView component is missing from SceneSystems");
            if (systems.GetComponent<GmParlorHud>() == null)
                issues.Add("GmParlorHud component is missing from SceneSystems");
            if (systems.GetComponent<GmHostAI>() != null)
                issues.Add("legacy GmHostAI component must not be shipping scene authority");
            if (HasComponentNamed(systems, "GmTheReadController"))
                issues.Add("legacy GmTheReadController bypasses canonical outcome persistence");
        }

        GmParlorCardView[] cards = Object.FindObjectsByType<GmParlorCardView>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (cards.Length != GmParlorCore.TotalCards)
            issues.Add($"physical Parlor card count is {cards.Length}, expected {GmParlorCore.TotalCards}");
        if (Object.FindAnyObjectByType<GmParlorPropBinder>() == null)
            issues.Add("GmParlorPropBinder component is missing");
        if (Object.FindAnyObjectByType<GmParlorPresentationCoordinator>() == null)
            issues.Add("GmParlorPresentationCoordinator component is missing");
        GmParlorAldricPresenter presenter = Object.FindAnyObjectByType<GmParlorAldricPresenter>();
        if (presenter == null || !presenter.IsConfigured)
            issues.Add("configured GmParlorAldricPresenter is missing");
        if (Object.FindAnyObjectByType<GmParlorEvidenceLog>() == null)
            issues.Add("GmParlorEvidenceLog component is missing");

        issues.AddRange(GmSceneCompositionAudit.ValidateOpenScene(
            GmParlorBuilder.SceneId, Object.FindAnyObjectByType<GmParlorShotTour>(), Camera.main));

        return issues;
    }

    static bool HasComponentNamed(GameObject target, string typeName)
    {
        foreach (Component component in target.GetComponents<Component>())
            if (component != null && component.GetType().Name == typeName) return true;
        return false;
    }
}
