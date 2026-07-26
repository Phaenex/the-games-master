// Minimum objective health contract shared by every gameplay scene. Scene-specific audits layer on
// architecture, routing, mechanics and art checks; this catches identity/path/camera/root/build-list
// drift and missing script references before a visual tour is trusted.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GmSceneContractAudit
{
    public static List<string> ValidateOpenScene(
        string expectedId,
        string expectedDisplayName,
        string expectedPath,
        IReadOnlyList<string> requiredRoots)
    {
        var issues = new List<string>();
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            issues.Add("active scene is invalid");
            return issues;
        }
        if (scene.path != expectedPath)
            issues.Add($"active scene path is '{scene.path}', expected '{expectedPath}'");

        var roots = scene.GetRootGameObjects();
        var rootNames = new HashSet<string>(StringComparer.Ordinal);
        var identities = new List<GmSceneIdentity>();
        int activeMainCameras = 0;
        foreach (var root in roots)
        {
            if (!rootNames.Add(root.name)) issues.Add($"duplicate root name '{root.name}'");
            identities.AddRange(root.GetComponentsInChildren<GmSceneIdentity>(true));
            foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                if (camera.enabled && camera.gameObject.activeInHierarchy && camera.CompareTag("MainCamera"))
                    activeMainCameras++;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                foreach (var component in transform.GetComponents<Component>())
                    if (component == null) issues.Add($"{HierarchyPath(transform)} has a missing script component");
        }

        if (identities.Count != 1)
            issues.Add($"expected exactly one GmSceneIdentity, found {identities.Count}");
        else
        {
            var identity = identities[0];
            if (identity.SceneId != expectedId)
                issues.Add($"scene identity id is '{identity.SceneId}', expected '{expectedId}'");
            if (identity.DisplayName != expectedDisplayName)
                issues.Add($"scene identity name is '{identity.DisplayName}', expected '{expectedDisplayName}'");
            if (identity.SchemaVersion != GmSceneCatalog.SchemaVersion)
                issues.Add($"scene identity schema is {identity.SchemaVersion}, expected {GmSceneCatalog.SchemaVersion}");
            if (identity.gameObject.scene != scene)
                issues.Add("scene identity belongs to a different loaded scene");
        }

        if (activeMainCameras != 1)
            issues.Add($"expected exactly one active enabled MainCamera, found {activeMainCameras}");
        if (requiredRoots != null)
            foreach (string required in requiredRoots)
                if (!rootNames.Contains(required)) issues.Add($"required scene root '{required}' is missing");

        int enabledBuildEntries = 0;
        foreach (var buildScene in EditorBuildSettings.scenes)
            if (buildScene.enabled && buildScene.path == expectedPath) enabledBuildEntries++;
        if (enabledBuildEntries != 1)
            issues.Add($"expected one enabled build-settings entry for '{expectedPath}', found {enabledBuildEntries}");
        return issues;
    }

    static string HierarchyPath(Transform transform)
    {
        string result = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            result = transform.name + "/" + result;
        }
        return result;
    }
}
