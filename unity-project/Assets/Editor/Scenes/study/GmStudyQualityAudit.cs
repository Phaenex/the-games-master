using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class GmStudyQualityAudit
{
    static readonly string[] Roots = { "SceneSystems", "SharedAudio", "Environment", "Gameplay", "Lighting", "Composition", "Player" };
    static readonly string[] MajorProps =
    {
        "StudyTable", "PlayerChair", "AldricChair", "StudyCarpet", "TableCandles",
    };
    static readonly HashSet<string> AllowedArchitecturePrimitives = new HashSet<string>(StringComparer.Ordinal)
    {
        "StudyFloor", "NorthWall", "SouthWall", "WestWall", "EastWall", "StudyCeiling",
    };
    static readonly HashSet<string> UnityPrimitiveMeshes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Cube", "Sphere", "Capsule", "Cylinder", "Plane", "Quad",
    };
    static readonly GmChessPieceType[] RequiredPieceTypes =
    {
        GmChessPieceType.King, GmChessPieceType.Queen, GmChessPieceType.Rook, GmChessPieceType.Pawn,
    };

    [MenuItem("GamesMaster/Scenes/Audit Study")]
    public static void Run()
    {
        GmStudyBuilder.Build();
        List<string> issues = ValidateOpenScene();
        if (issues.Count == 0) Debug.Log("[GmStudyAudit] PASS: physical board, pieces, light, dust, host and direct-review contract verified");
        else foreach (string issue in issues) Debug.LogError("[GmStudyAudit] FAILED: " + issue);
    }

    public static List<string> ValidateOpenScene()
    {
        List<string> issues = GmSceneContractAudit.ValidateOpenScene(
            GmStudyBuilder.SceneId, GmStudyBuilder.DisplayName, GmStudyBuilder.ScenePath, Roots);

        GmStudyBoardTile[] tiles = Object.FindObjectsByType<GmStudyBoardTile>(FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        if (tiles.Length != 64) issues.Add($"expected exactly 64 board tiles, found {tiles.Length}");
        else
        {
            var squares = new HashSet<(int, int)>();
            foreach (GmStudyBoardTile tile in tiles) squares.Add((tile.File, tile.Rank));
            if (squares.Count != 64) issues.Add("board tiles do not cover 64 distinct squares");
        }

        GmStudyPieceView[] pieces = Object.FindObjectsByType<GmStudyPieceView>(FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        if (pieces.Length != 5) issues.Add($"expected exactly 5 physical pieces, found {pieces.Length}");
        foreach (GmChessPieceType required in RequiredPieceTypes)
            if (!pieces.Any(piece => piece.PieceType == required))
                issues.Add($"no authored physical piece exists for {required}");
        if (pieces.Count(piece => piece.PieceType == GmChessPieceType.King) != 2)
            issues.Add("expected exactly two king pieces (one per side)");

        if (Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude).Length != 1)
            issues.Add("scene needs exactly one active camera");
        if (Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude).Length != 1)
            issues.Add("scene needs exactly one active listener");
        GmStudySceneHost host = Object.FindAnyObjectByType<GmStudySceneHost>();
        if (host == null || !host.IsConfigured) issues.Add("Study host is missing or unconfigured");
        else if (!host.IsDirectReviewOnly) issues.Add("Study scaffold host is not marked direct-review-only");
        GmStudyInput input = Object.FindAnyObjectByType<GmStudyInput>();
        GmStudyHud hud = Object.FindAnyObjectByType<GmStudyHud>();
        GmStudyPresenter presenter = Object.FindAnyObjectByType<GmStudyPresenter>();
        GmStudyAudio audio = Object.FindAnyObjectByType<GmStudyAudio>();
        if (input == null) issues.Add("configured Study input is missing");
        if (hud == null) issues.Add("configured Study HUD is missing");
        if (presenter == null || !presenter.IsConfigured)
            issues.Add("configured Study presentation-model presenter is missing");
        else if (!presenter.SupportsAccessibility)
            issues.Add("Study presenter does not declare reduced-motion/high-contrast support");
        GmAudioManager sharedAudio = Object.FindAnyObjectByType<GmAudioManager>();
        if (audio == null || !audio.UsesSharedAudioManager || sharedAudio == null)
            issues.Add("Study piece-move cue is not routed through the shared audio manager/SFX settings");
        else if (sharedAudio.GetComponent<GmAudioIntent>() == null)
            issues.Add("Study shared-manager piece-move cue has no authored audio intent");

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
        GameObject light = GameObject.Find("StudyTableTaskLight");
        if (light == null || light.GetComponent<GmPeriodLampFlicker>() != null)
            issues.Add("stable board task light is missing or flickering");
        if (Object.FindObjectsByType<GmStudyDustField>(FindObjectsInactive.Include)
            .Any(field => !field.IsOutsideBoardCone)) issues.Add("dust enters the board readability cone");
        GmStudyShotTour tour = Object.FindAnyObjectByType<GmStudyShotTour>();
        if (tour == null || tour.ShotCount != 10 || tour.HasPlaceholderShots)
            issues.Add("ten-shot non-placeholder Study tour is missing");
        GameObject clearance = GameObject.Find("PlayerTableClearance");
        if (clearance == null || Physics.OverlapBox(clearance.transform.position,
                clearance.transform.localScale * 0.49f, Quaternion.identity)
            .Any(collider => !collider.transform.IsChildOf(clearance.transform)))
            issues.Add("player-to-table approach clearance is missing or obstructed");
        issues.AddRange(GmSceneCompositionAudit.ValidateOpenScene(GmStudyBuilder.SceneId, tour, Camera.main));
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
