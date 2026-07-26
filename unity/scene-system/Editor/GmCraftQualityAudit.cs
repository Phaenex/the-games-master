using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GmCraftQualityAudit
{
    public static void Analyze(Scene scene, GmSceneAuditReport report)
    {
        var profiles = Find<GmSceneCraftProfile>(scene);
        if (profiles.Count == 0) return;
        if (profiles.Count != 1)
        {
            report.Add("scene-craft", GmAuditSeverity.Error, scene.name,
                "scene must own exactly one craft profile", profiles.Count.ToString(), "1");
            return;
        }
        GmSceneCraftProfile profile = profiles[0];
        if (profile.Zones.Count < 2)
            report.Add("scene-craft", GmAuditSeverity.Error, profile.SceneId,
                "craft profile does not prove multiple authored zones", profile.Zones.Count.ToString(), ">= 2");

        var elements = Find<GmCompositionElement>(scene)
            .Where(item => !string.IsNullOrWhiteSpace(item.ElementId))
            .GroupBy(item => item.ElementId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (GmCraftZoneRule zone in profile.Zones)
        {
            Validate(zone.ZoneId, "zone id", profile.SceneId, report);
            Validate(zone.VisualThesis, "visual thesis", zone.ZoneId, report);
            Validate(zone.StoryState, "story state", zone.ZoneId, report);
            if (elements.Count > 0)
            {
                RequireElement(zone.FocalElementId, "focal", zone.ZoneId, elements, report);
                foreach (string id in zone.ForegroundElementIds) RequireElement(id, "foreground", zone.ZoneId, elements, report);
                foreach (string id in zone.MiddleElementIds) RequireElement(id, "middle", zone.ZoneId, elements, report);
                foreach (string id in zone.BackgroundElementIds) RequireElement(id, "background", zone.ZoneId, elements, report);
            }
        }

        foreach (GmSurfaceTag tag in Find<GmSurfaceTag>(scene))
        {
            if (tag.Surface == GmSurfaceKind.Unknown)
                report.Add("surface", GmAuditSeverity.Error, tag.name, "surface is unclassified");
            Validate(tag.Rationale, "surface rationale", tag.name, report);
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (GmInteractable target in Find<GmInteractable>(scene))
        {
            if (string.IsNullOrWhiteSpace(target.InteractionId))
                report.Add("interaction", GmAuditSeverity.Error, target.name, "interactable has no stable id");
            else if (!ids.Add(target.InteractionId))
                report.Add("interaction", GmAuditSeverity.Error, target.InteractionId, "interaction id is duplicated");
            if (target.GetComponentsInChildren<Collider>(true).Length == 0)
                report.Add("interaction", GmAuditSeverity.Error, target.InteractionId, "interactable has no focus collider");
            if (!target.HasContent)
                report.Add("interaction", GmAuditSeverity.Error, target.InteractionId,
                    "interactable is not bound to authored content in the saved scene");
        }

        foreach (GmCompositionElement element in Find<GmCompositionElement>(scene))
        {
            bool classified = element.GetComponentInChildren<GmInteractable>(true) != null ||
                element.GetComponent<GmIntentionallySilent>() != null;
            if (!classified)
                report.Add("interaction", GmAuditSeverity.Error, element.ElementId,
                    "prominent composition element is neither interactable nor intentionally silent");
        }
    }

    static List<T> Find<T>(Scene scene) where T : Component =>
        UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(item => item.gameObject.scene == scene).ToList();

    static void RequireElement(string id, string layer, string zone,
        IReadOnlyDictionary<string, GmCompositionElement> elements, GmSceneAuditReport report)
    {
        if (string.IsNullOrWhiteSpace(id) || !elements.ContainsKey(id))
            report.Add("scene-craft", GmAuditSeverity.Error, zone,
                $"{layer} layer references missing element '{id}'");
    }

    static void Validate(string value, string field, string subject, GmSceneAuditReport report)
    {
        if (string.IsNullOrWhiteSpace(value))
            report.Add("scene-craft", GmAuditSeverity.Error, subject, $"{field} is empty");
    }
}
