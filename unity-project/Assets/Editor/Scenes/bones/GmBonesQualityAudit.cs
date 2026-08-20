using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class GmBonesQualityAudit
{
    static readonly string[] Roots = { "SceneSystems", "SharedAudio", "Environment", "Gameplay", "Lighting", "Composition", "Player" };
    static readonly string[] MajorProps =
    {
        "BonesTable", "PlayerChair", "AldricChair", "BonesCarpet", "TableCandles",
    };
    static readonly HashSet<string> AllowedArchitecturePrimitives = new HashSet<string>(StringComparer.Ordinal)
    {
        "BonesFloor", "NorthWall", "SouthWall", "WestWall", "EastWall", "BonesCeiling",
    };
    static readonly HashSet<string> UnityPrimitiveMeshes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Cube", "Sphere", "Capsule", "Cylinder", "Plane", "Quad",
    };

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
        else if (!host.IsDirectReviewOnly) issues.Add("Bones scaffold host is not marked direct-review-only");
        GmBonesInput input = Object.FindAnyObjectByType<GmBonesInput>();
        GmBonesHud hud = Object.FindAnyObjectByType<GmBonesHud>();
        GmBonesPresenter presenter = Object.FindAnyObjectByType<GmBonesPresenter>();
        GmBonesAudio audio = Object.FindAnyObjectByType<GmBonesAudio>();
        if (input == null) issues.Add("configured Bones input is missing");
        if (hud == null) issues.Add("configured Bones HUD is missing");
        if (presenter == null || !presenter.IsConfigured)
            issues.Add("configured Bones presentation-model presenter is missing");
        else if (!presenter.SupportsAccessibility)
            issues.Add("Bones presenter does not declare reduced-motion/high-contrast support");
        GmAudioManager sharedAudio = Object.FindAnyObjectByType<GmAudioManager>();
        if (audio == null || !audio.UsesSharedAudioManager || sharedAudio == null)
            issues.Add("Bones dice cue is not routed through the shared audio manager/SFX settings");
        else if (sharedAudio.GetComponent<GmAudioIntent>() == null)
            issues.Add("Bones shared-manager dice cue has no authored audio intent");

        foreach (string propName in MajorProps)
        {
            GameObject prop = GameObject.Find(GmOwnedPropFactory.VisualPrefix + propName);
            if (prop == null || PrefabUtility.GetCorrespondingObjectFromSource(prop) == null)
            {
                issues.Add($"major prop '{propName}' is missing its imported prefab/model source");
                continue;
            }
            Renderer[] renderers = prop.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0 || !renderers.SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null).Any(HasReadablePbrSurface))
                issues.Add($"major prop '{propName}' has no readable textured PBR surface");
        }

        foreach (string surfaceName in AllowedArchitecturePrimitives)
        {
            Renderer renderer = GameObject.Find(surfaceName)?.GetComponent<Renderer>();
            if (renderer == null || !renderer.sharedMaterials.Any(HasReadablePbrSurface))
                issues.Add($"room surface '{surfaceName}' is missing its Victorian PBR surface");
        }
        foreach (MeshFilter filter in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,
            FindObjectsSortMode.None))
        {
            if (filter.sharedMesh == null || !UnityPrimitiveMeshes.Contains(filter.sharedMesh.name)) continue;
            Renderer renderer = filter.GetComponent<Renderer>();
            if (renderer != null && renderer.enabled && !AllowedArchitecturePrimitives.Contains(filter.gameObject.name))
                issues.Add($"player-facing primitive '{filter.gameObject.name}' is prohibited outside covered architecture");
        }
        GameObject light = GameObject.Find("BonesTableTaskLight");
        if (light == null || light.GetComponent<GmPeriodLampFlicker>() != null)
            issues.Add("stable dice task light is missing or flickering");
        if (Object.FindObjectsByType<GmBonesDustField>(FindObjectsInactive.Include)
            .Any(field => !field.IsOutsideDiceCone)) issues.Add("dust enters the dice readability cone");
        GmBonesShotTour tour = Object.FindAnyObjectByType<GmBonesShotTour>();
        if (tour == null || tour.ShotCount != 10 || tour.HasPlaceholderShots)
            issues.Add("ten-shot non-placeholder Bones tour is missing");
        GameObject clearance = GameObject.Find("PlayerTableClearance");
        if (clearance == null || Physics.OverlapBox(clearance.transform.position,
                clearance.transform.localScale * 0.49f, Quaternion.identity)
            .Any(collider => !collider.transform.IsChildOf(clearance.transform)))
            issues.Add("player-to-table approach clearance is missing or obstructed");
        issues.AddRange(GmSceneCompositionAudit.ValidateOpenScene(GmBonesBuilder.SceneId, tour, Camera.main));
        return issues;
    }

    static bool HasReadablePbrSurface(Material material)
    {
        if (material == null || material.shader == null ||
            material.shader.name.IndexOf("InternalErrorShader", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;
        return material.HasProperty("_BaseColorMap") && material.GetTexture("_BaseColorMap") != null;
    }
}
