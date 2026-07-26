// Perceptual counterchecks for authored intent. Measurements are deterministic and explainable;
// they reject known failure shapes without pretending to decide whether a scene is beautiful.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GmAuditSeverity
{
    Info,
    Warning,
    Error
}

[Serializable]
public sealed class GmSceneAuditFinding
{
    public string category;
    public GmAuditSeverity severity;
    public string subjectId;
    public string message;
    public string actual;
    public string expected;
}

[Serializable]
public sealed class GmSceneAuditReport
{
    public int schemaVersion = 1;
    public string sceneId;
    public string fingerprint;
    public List<GmSceneAuditFinding> findings = new List<GmSceneAuditFinding>();
    public bool Passed => findings.All(item => item.severity != GmAuditSeverity.Error);

    public void Add(string category, GmAuditSeverity severity, string subject, string message,
        string actual = "", string expected = "")
    {
        findings.Add(new GmSceneAuditFinding {
            category = category,
            severity = severity,
            subjectId = subject,
            message = message,
            actual = actual,
            expected = expected,
        });
    }
}

public static class GmPerceptualAudit
{
    public static void Analyze(Scene scene, GmSceneReviewTour tour, Camera camera,
        GmSceneAuditReport report)
    {
        var elements = Find<GmCompositionElement>(scene)
            .Where(item => !string.IsNullOrWhiteSpace(item.ElementId))
            .GroupBy(item => item.ElementId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var shots = tour == null ? new Dictionary<string, GmReviewShot>(StringComparer.Ordinal) :
            tour.ShotsForAudit.GroupBy(shot => shot.Name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (GmRepetitionIntent intent in Find<GmRepetitionIntent>(scene))
            AnalyzeRepetition(intent, elements, tour, camera, report);
        foreach (GmEnvironmentalStoryIntent intent in Find<GmEnvironmentalStoryIntent>(scene))
            AnalyzeStory(intent, elements, shots, tour, camera, report);
        foreach (GmStyleIntent intent in Find<GmStyleIntent>(scene))
            AnalyzeStyle(intent, elements, shots, tour, camera, report);
        foreach (GmSurfacePaletteIntent intent in Find<GmSurfacePaletteIntent>(scene))
            AnalyzeSurfacePalette(intent, elements, report);
        foreach (GmLandscapeDepthIntent intent in Find<GmLandscapeDepthIntent>(scene))
            AnalyzeLandscape(intent, elements, shots, tour, camera, report);
        foreach (GmSoundscapeIntent intent in Find<GmSoundscapeIntent>(scene))
            AnalyzeSoundscape(intent, scene, report);
        foreach (GmPacingIntent intent in Find<GmPacingIntent>(scene))
            AnalyzePacing(intent, report);
        foreach (GmClearedPathGuide guide in Find<GmClearedPathGuide>(scene))
            AnalyzeClearedPath(guide, report);
        GmCraftQualityAudit.Analyze(scene, report);
    }

    static List<T> Find<T>(Scene scene) where T : Component =>
        UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include)
            .Where(item => item.gameObject.scene == scene).ToList();

    static void AnalyzeClearedPath(GmClearedPathGuide guide, GmSceneAuditReport report)
    {
        if (guide.Length <= 0.1f || guide.Width <= 0.1f)
            report.Add("cleared-path", GmAuditSeverity.Error, guide.name,
                "negative-space path has invalid dimensions",
                $"{guide.Width:F2}x{guide.Length:F2}", "> 0.1m on both axes");
        int renderers = guide.GetComponentsInChildren<Renderer>(true).Length;
        if (renderers > 0)
            report.Add("cleared-path", GmAuditSeverity.Error, guide.name,
                "negative-space path owns visible geometry and can return as a painted floor strip",
                renderers.ToString(), "0 renderers");
        int colliders = guide.GetComponentsInChildren<Collider>(true).Length;
        if (colliders > 0)
            report.Add("cleared-path", GmAuditSeverity.Error, guide.name,
                "negative-space path owns collision and can alter or snag the authored route",
                colliders.ToString(), "0 colliders");
    }

    static void AnalyzeRepetition(GmRepetitionIntent intent,
        IReadOnlyDictionary<string, GmCompositionElement> elements, GmSceneReviewTour tour,
        Camera camera, GmSceneAuditReport report)
    {
        ValidateText(intent.IntentId, "repetition", intent.IntentId, "intent id", report);
        ValidateText(intent.Rationale, "repetition", intent.IntentId, "rationale", report, 12);
        var members = new List<GmCompositionElement>();
        foreach (string id in intent.ElementIds)
        {
            if (!elements.TryGetValue(id, out GmCompositionElement element))
                report.Add("repetition", GmAuditSeverity.Error, intent.IntentId,
                    $"repetition intent references missing element '{id}'");
            else members.Add(element);
        }
        if (members.Count == 0)
        {
            report.Add("repetition", GmAuditSeverity.Error, intent.IntentId,
                "repetition intent has no resolvable elements");
            return;
        }

        var signatures = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (GmCompositionElement element in members)
        {
            string signature = Signature(element);
            signatures.TryGetValue(signature, out int count);
            signatures[signature] = count + 1;
        }
        if (signatures.Count < intent.MinimumDistinctSignatures)
            report.Add("repetition", GmAuditSeverity.Error, intent.IntentId,
                "visible set has too few perceptually distinct signatures",
                signatures.Count.ToString(), intent.MinimumDistinctSignatures.ToString());
        int dominant = signatures.Values.Max();
        float share = dominant / (float)members.Count;
        if (share > intent.MaximumSignatureShare + 0.0001f)
            report.Add("repetition", GmAuditSeverity.Error, intent.IntentId,
                "one perceptual signature dominates the authored set",
                share.ToString("P0"), intent.MaximumSignatureShare.ToString("P0"));

        GmCompositionElement[] ordered = members.OrderBy(item => item.transform.position.z)
            .ThenBy(item => item.transform.position.x).ToArray();
        int run = 1, maximumRun = 1;
        for (int i = 1; i < ordered.Length; i++)
        {
            if (Signature(ordered[i]) == Signature(ordered[i - 1])) run++;
            else run = 1;
            maximumRun = Mathf.Max(maximumRun, run);
        }
        if (maximumRun > intent.MaximumNearIdenticalRun)
            report.Add("repetition", GmAuditSeverity.Error, intent.IntentId,
                "near-identical elements form an obvious consecutive run",
                maximumRun.ToString(), intent.MaximumNearIdenticalRun.ToString());

        if (SpacingRegularity(ordered) > 0.92f && ordered.Length >= 6)
            report.Add("repetition", GmAuditSeverity.Error, intent.IntentId,
                "placement spacing remains machine-regular despite surface variation",
                SpacingRegularity(ordered).ToString("F2"), "<= 0.92");

        AnalyzeScreenSpaceRuns(intent, members, tour, camera, report);
    }

    // World-space variety can still collapse into a visible row from the authored camera. Project
    // each member into every review frame and reject same-signature horizontal runs in screen space.
    static void AnalyzeScreenSpaceRuns(GmRepetitionIntent intent,
        IReadOnlyList<GmCompositionElement> members, GmSceneReviewTour tour, Camera camera,
        GmSceneAuditReport report)
    {
        if (tour == null || camera == null) return;
        Vector3 oldPosition = camera.transform.position;
        Quaternion oldRotation = camera.transform.rotation;
        float oldAspect = camera.aspect;
        try
        {
            camera.aspect = tour.ShotHeight == 0 ? 16f / 9f : tour.ShotWidth / (float)tour.ShotHeight;
            foreach (GmReviewShot shot in tour.ShotsForAudit)
            {
                camera.transform.position = shot.Position;
                camera.transform.rotation = Quaternion.Euler(shot.Pitch,
                    shot.Yaw + tour.ReviewYawOffsetForAudit, 0f);
                var visible = members.Select(member => new
                    {
                        member,
                        point = camera.WorldToViewportPoint(CombinedBounds(
                            member.GetComponentsInChildren<Renderer>(true)).center)
                    })
                    .Where(item => item.point.z > 0f && item.point.x >= 0f && item.point.x <= 1f &&
                        item.point.y >= 0f && item.point.y <= 1f)
                    .OrderBy(item => item.point.x).ToArray();
                if (visible.Length < 3) continue;
                int run = 1, maximum = 1;
                for (int i = 1; i < visible.Length; i++)
                {
                    if (Signature(visible[i].member) == Signature(visible[i - 1].member)) run++;
                    else run = 1;
                    maximum = Mathf.Max(maximum, run);
                }
                if (maximum > intent.MaximumNearIdenticalRun)
                    report.Add("repetition-screen", GmAuditSeverity.Error,
                        $"{intent.IntentId}/{shot.Name}",
                        "authored camera compresses near-identical silhouettes into an obvious screen-space run",
                        maximum.ToString(), intent.MaximumNearIdenticalRun.ToString());
            }
        }
        finally
        {
            camera.transform.position = oldPosition;
            camera.transform.rotation = oldRotation;
            camera.aspect = oldAspect;
        }
    }

    static Bounds CombinedBounds(Renderer[] renderers)
    {
        if (renderers == null || renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.zero);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static string Signature(GmCompositionElement element)
    {
        var meshNames = new SortedSet<string>(StringComparer.Ordinal);
        var materialNames = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Renderer renderer in element.GetComponentsInChildren<Renderer>(true))
        {
            Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh :
                renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh != null) meshNames.Add(mesh.name);
            foreach (Material material in renderer.sharedMaterials)
                if (material != null) materialNames.Add(MaterialFamily(material.name));
        }
        Vector3 rotation = element.transform.eulerAngles;
        Vector3 scale = element.transform.lossyScale;
        return string.Join("+", meshNames) + "|" + string.Join("+", materialNames) + "|" +
            $"{QuantizeAngle(rotation.x, 5f)},{QuantizeAngle(rotation.z, 5f)}|" +
            $"{Quantize(scale.x, 0.08f)},{Quantize(scale.y, 0.08f)},{Quantize(scale.z, 0.08f)}";
    }

    static string MaterialFamily(string name)
    {
        int marker = name.IndexOf(" (Instance)", StringComparison.Ordinal);
        if (marker >= 0) name = name.Substring(0, marker);
        return name.Replace("_DeadGarden", "");
    }

    static float Quantize(float value, float step) => Mathf.Round(value / step) * step;
    static float QuantizeAngle(float value, float step)
    {
        if (value > 180f) value -= 360f;
        return Quantize(value, step);
    }

    static float SpacingRegularity(IReadOnlyList<GmCompositionElement> ordered)
    {
        if (ordered.Count < 3) return 0f;
        var distances = new List<float>();
        for (int i = 1; i < ordered.Count; i++)
            distances.Add(Vector3.Distance(ordered[i - 1].transform.position,
                ordered[i].transform.position));
        float mean = distances.Average();
        if (mean <= 0.0001f) return 1f;
        float variance = distances.Sum(value => (value - mean) * (value - mean)) / distances.Count;
        float coefficient = Mathf.Sqrt(variance) / mean;
        return 1f / (1f + coefficient);
    }

    static void AnalyzeStory(GmEnvironmentalStoryIntent intent,
        IReadOnlyDictionary<string, GmCompositionElement> elements,
        IReadOnlyDictionary<string, GmReviewShot> shots, GmSceneReviewTour tour, Camera camera,
        GmSceneAuditReport report)
    {
        ValidateText(intent.IntentId, "story", intent.IntentId, "intent id", report);
        ValidateText(intent.IntendedInference, "story", intent.IntentId, "intended inference", report, 20);
        if (!elements.ContainsKey(intent.AnchorElementId))
            report.Add("story", GmAuditSeverity.Error, intent.IntentId,
                $"story anchor '{intent.AnchorElementId}' is missing");
        if (intent.TraceElementIds.Count < 3)
            report.Add("story", GmAuditSeverity.Error, intent.IntentId,
                "environmental story needs at least three authored traces",
                intent.TraceElementIds.Count.ToString(), ">= 3");
        foreach (string id in intent.TraceElementIds)
            if (!elements.ContainsKey(id))
                report.Add("story", GmAuditSeverity.Error, intent.IntentId,
                    $"story trace '{id}' is missing");
        if (intent.RevealSteps.Count < 2)
            report.Add("story", GmAuditSeverity.Error, intent.IntentId,
                "environmental story needs at least two staged review reveals");
        if (camera == null || tour == null) return;
        foreach (GmStoryRevealStep step in intent.RevealSteps)
        {
            if (!shots.TryGetValue(step.ShotName, out GmReviewShot shot))
            {
                report.Add("story", GmAuditSeverity.Error, intent.IntentId,
                    $"story reveal references missing shot '{step.ShotName}'");
                continue;
            }
            WithShot(camera, tour, shot, () =>
            {
                foreach (string id in step.RequiredElementIds)
                {
                    if (!elements.TryGetValue(id, out GmCompositionElement element)) continue;
                    if (!TryViewportRect(camera, BoundsOf(element), out _))
                        report.Add("story", GmAuditSeverity.Error, intent.IntentId,
                            $"story reveal '{step.ShotName}' cannot see required trace '{id}'");
                }
            });
        }
    }

    static void AnalyzeStyle(GmStyleIntent intent,
        IReadOnlyDictionary<string, GmCompositionElement> elements,
        IReadOnlyDictionary<string, GmReviewShot> shots, GmSceneReviewTour tour, Camera camera,
        GmSceneAuditReport report)
    {
        ValidateText(intent.IntentId, "style", intent.IntentId, "intent id", report);
        if (!elements.TryGetValue(intent.ElementId, out GmCompositionElement element))
        {
            report.Add("style", GmAuditSeverity.Error, intent.IntentId,
                $"style subject '{intent.ElementId}' is missing");
            return;
        }
        if (intent.ElementEra != GmEra.Unspecified && intent.ContextEra != GmEra.Unspecified &&
            intent.ElementEra != intent.ContextEra)
        {
            if (!intent.DeliberateContrast)
                report.Add("style", GmAuditSeverity.Error, intent.IntentId,
                    $"{intent.ElementEra} subject conflicts with {intent.ContextEra} context without an exception");
            ValidateText(intent.Rationale, "style", intent.IntentId,
                "deliberate contrast rationale", report, 24);
        }
        float maximumSmoothness = 0f;
        foreach (Renderer renderer in element.GetComponentsInChildren<Renderer>(true))
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null) continue;
                if (material.HasProperty("_Smoothness"))
                    maximumSmoothness = Mathf.Max(maximumSmoothness, material.GetFloat("_Smoothness"));
                else if (material.HasProperty("_Glossiness"))
                    maximumSmoothness = Mathf.Max(maximumSmoothness, material.GetFloat("_Glossiness"));
            }
        if (maximumSmoothness > intent.MaximumMaterialSmoothness + 0.0001f)
            report.Add("style", GmAuditSeverity.Error, intent.IntentId,
                "subject material is too pristine for its authored condition",
                maximumSmoothness.ToString("F2"), intent.MaximumMaterialSmoothness.ToString("F2"));
        if (camera == null || tour == null) return;
        foreach (GmStyleShotBudget budget in intent.ShotBudgets)
        {
            if (!shots.TryGetValue(budget.ShotName, out GmReviewShot shot))
            {
                report.Add("style", GmAuditSeverity.Error, intent.IntentId,
                    $"style budget references missing shot '{budget.ShotName}'");
                continue;
            }
            WithShot(camera, tour, shot, () =>
            {
                if (!TryViewportRect(camera, BoundsOf(element), out Rect rect)) return;
                float area = Mathf.Clamp01(rect.width) * Mathf.Clamp01(rect.height);
                if (area > budget.MaximumViewportArea + 0.0001f)
                    report.Add("style", GmAuditSeverity.Error, intent.IntentId,
                        $"subject dominates establishing shot '{budget.ShotName}'",
                        area.ToString("P1"), budget.MaximumViewportArea.ToString("P1"));
            });
        }
    }

    static void AnalyzeSurfacePalette(GmSurfacePaletteIntent intent,
        IReadOnlyDictionary<string, GmCompositionElement> elements, GmSceneAuditReport report)
    {
        ValidateText(intent.IntentId, "surface-palette", intent.IntentId, "intent id", report);
        ValidateText(intent.Rationale, "surface-palette", intent.IntentId, "rationale", report, 12);
        if (intent.Rules.Count == 0)
        {
            report.Add("surface-palette", GmAuditSeverity.Error, intent.IntentId,
                "surface palette intent has no material rules");
            return;
        }

        foreach (GmSurfacePaletteRule rule in intent.Rules)
        {
            if (!elements.TryGetValue(rule.ElementId, out GmCompositionElement element))
            {
                report.Add("surface-palette", GmAuditSeverity.Error, intent.IntentId,
                    $"surface palette references missing element '{rule.ElementId}'");
                continue;
            }
            int inspected = 0;
            // Nested composition elements own their own surface semantics. Without this boundary,
            // a deliberate dark recess embedded in a warm soil bank is measured as if both were
            // one material family and either the lip or the void must fail. The nearest authored
            // element owns each renderer; parent elements do not silently absorb child palettes.
            foreach (Renderer renderer in element.GetComponentsInChildren<Renderer>(true)
                .Where(item => item.GetComponentInParent<GmCompositionElement>() == element))
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null) continue;
                    // Shader families often retain unused compatibility aliases at default white.
                    // Inspect the controls that actually feed that family instead of penalising a
                    // dormant `_Color` when HDRP/Lit renders `_BaseColor`, for example.
                    var properties = new List<string>();
                    if (material.HasProperty("_BaseColor")) properties.Add("_BaseColor");
                    else if (material.HasProperty("_BaseColor_Value")) properties.Add("_BaseColor_Value");
                    else
                    {
                        if (material.HasProperty("_Albedo_Tint")) properties.Add("_Albedo_Tint");
                        if (material.HasProperty("_ColorGrass")) properties.Add("_ColorGrass");
                        if (properties.Count == 0 && material.HasProperty("_Color")) properties.Add("_Color");
                    }
                    foreach (string property in properties)
                    {
                        inspected++;
                        Color colour = material.GetColor(property);
                        float luminance = colour.r * 0.2126f + colour.g * 0.7152f + colour.b * 0.0722f;
                        float high = Mathf.Max(colour.r, Mathf.Max(colour.g, colour.b));
                        float low = Mathf.Min(colour.r, Mathf.Min(colour.g, colour.b));
                        float saturation = high <= 0.0001f ? 0f : (high - low) / high;
                        if (luminance < rule.MinimumTintLuminance - 0.0001f ||
                            luminance > rule.MaximumTintLuminance + 0.0001f)
                            report.Add("surface-palette", GmAuditSeverity.Error, rule.ElementId,
                                $"material '{material.name}' property '{property}' is outside its authored luminance band",
                                luminance.ToString("F2"),
                                $"{rule.MinimumTintLuminance:F2}..{rule.MaximumTintLuminance:F2}");
                        if (saturation > rule.MaximumSaturation + 0.0001f)
                            report.Add("surface-palette", GmAuditSeverity.Error, rule.ElementId,
                                $"material '{material.name}' property '{property}' is too saturated for its surface family",
                                saturation.ToString("F2"), $"<= {rule.MaximumSaturation:F2}");
                        if (rule.RequireWarmBias && (colour.r + 0.015f < colour.g || colour.r < colour.b))
                            report.Add("surface-palette", GmAuditSeverity.Error, rule.ElementId,
                                $"material '{material.name}' property '{property}' is not warm-biased",
                                $"{colour.r:F2},{colour.g:F2},{colour.b:F2}", "red >= green and blue");
                    }
                    if (material.HasProperty("_Brightness") &&
                        material.GetFloat("_Brightness") > rule.MaximumBrightnessMultiplier + 0.0001f)
                        report.Add("surface-palette", GmAuditSeverity.Error, rule.ElementId,
                            $"material '{material.name}' ignores its authored brightness ceiling",
                            material.GetFloat("_Brightness").ToString("F2"),
                            $"<= {rule.MaximumBrightnessMultiplier:F2}");
                }
            if (rule.RequireInspectableTint && inspected == 0)
                report.Add("surface-palette", GmAuditSeverity.Error, rule.ElementId,
                    "no supported tint property could be inspected; add this shader family before approval");
        }
    }

    static void AnalyzeLandscape(GmLandscapeDepthIntent intent,
        IReadOnlyDictionary<string, GmCompositionElement> elements,
        IReadOnlyDictionary<string, GmReviewShot> shots, GmSceneReviewTour tour, Camera camera,
        GmSceneAuditReport report)
    {
        ValidateText(intent.IntentId, "landscape", intent.IntentId, "intent id", report);
        ValidateText(intent.Rationale, "landscape", intent.IntentId, "rationale", report, 12);
        if (camera == null || tour == null) return;
        foreach (GmLandscapeShotRequirement requirement in intent.Shots)
        {
            if (!shots.TryGetValue(requirement.ShotName, out GmReviewShot shot))
            {
                report.Add("landscape", GmAuditSeverity.Error, intent.IntentId,
                    $"landscape requirement references missing shot '{requirement.ShotName}'");
                continue;
            }
            WithShot(camera, tour, shot, () =>
            {
                float foregroundDepth = VisibleDepth(requirement.ForegroundElementIds, elements, camera,
                    intent.IntentId, requirement.ShotName, "foreground", report);
                float middleDepth = VisibleDepth(requirement.MiddleElementIds, elements, camera,
                    intent.IntentId, requirement.ShotName, "middle", report);
                float farDepth = VisibleDepth(requirement.FarElementIds, elements, camera,
                    intent.IntentId, requirement.ShotName, "far", report);
                if (foregroundDepth > 0f && middleDepth > 0f && farDepth > 0f &&
                    !(foregroundDepth < middleDepth && middleDepth < farDepth))
                    report.Add("landscape", GmAuditSeverity.Error, intent.IntentId,
                        $"shot '{requirement.ShotName}' depth bands are not ordered front-to-back",
                        $"{foregroundDepth:F1}/{middleDepth:F1}/{farDepth:F1}", "foreground < middle < far");
                float coverage = HorizontalCoverage(requirement.FarElementIds, elements, camera);
                if (coverage < requirement.MinimumFarHorizontalCoverage ||
                    coverage > requirement.MaximumFarHorizontalCoverage)
                    report.Add("landscape", GmAuditSeverity.Error, intent.IntentId,
                        $"shot '{requirement.ShotName}' far horizon is empty or wall-like",
                        coverage.ToString("P0"),
                        $"{requirement.MinimumFarHorizontalCoverage:P0}..{requirement.MaximumFarHorizontalCoverage:P0}");
            });
        }
    }

    static float VisibleDepth(IReadOnlyList<string> ids,
        IReadOnlyDictionary<string, GmCompositionElement> elements, Camera camera, string intentId,
        string shot, string label, GmSceneAuditReport report)
    {
        var depths = new List<float>();
        foreach (string id in ids)
        {
            if (!elements.TryGetValue(id, out GmCompositionElement element))
            {
                report.Add("landscape", GmAuditSeverity.Error, intentId,
                    $"{label} band references missing element '{id}'");
                continue;
            }
            foreach (Renderer renderer in element.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    !TryViewportRect(camera, renderer.bounds, out _)) continue;
                depths.Add(Vector3.Distance(camera.transform.position, renderer.bounds.center));
            }
        }
        if (depths.Count == 0)
            report.Add("landscape", GmAuditSeverity.Error, intentId,
                $"shot '{shot}' has no visible {label} depth band");
        return depths.Count == 0 ? -1f : depths.Average();
    }

    static float HorizontalCoverage(IReadOnlyList<string> ids,
        IReadOnlyDictionary<string, GmCompositionElement> elements, Camera camera)
    {
        var intervals = new List<Vector2>();
        foreach (string id in ids)
            if (elements.TryGetValue(id, out GmCompositionElement element))
                foreach (Renderer renderer in element.GetComponentsInChildren<Renderer>(true))
                    if (renderer.enabled && renderer.gameObject.activeInHierarchy &&
                        TryViewportRect(camera, renderer.bounds, out Rect rect))
                        intervals.Add(new Vector2(Mathf.Clamp01(rect.xMin), Mathf.Clamp01(rect.xMax)));
        if (intervals.Count == 0) return 0f;
        intervals.Sort((a, b) => a.x.CompareTo(b.x));
        float total = 0f, start = intervals[0].x, end = intervals[0].y;
        for (int i = 1; i < intervals.Count; i++)
        {
            if (intervals[i].x <= end) end = Mathf.Max(end, intervals[i].y);
            else { total += end - start; start = intervals[i].x; end = intervals[i].y; }
        }
        return Mathf.Clamp01(total + end - start);
    }

    static void AnalyzeSoundscape(GmSoundscapeIntent intent, Scene scene,
        GmSceneAuditReport report)
    {
        ValidateText(intent.IntentId, "soundscape", intent.IntentId, "intent id", report);
        ValidateText(intent.DesiredCharacter, "soundscape", intent.IntentId,
            "desired character", report, 20);
        AudioSource[] sources = Find<AudioSource>(scene).Where(source => source.enabled &&
            source.gameObject.activeInHierarchy).ToArray();
        AudioSource[] beds = sources.Where(source => source.loop).ToArray();
        if (beds.Length > intent.MaximumContinuousBeds)
            report.Add("soundscape", GmAuditSeverity.Error, intent.IntentId,
                "too many continuous audio beds", beds.Length.ToString(),
                $"<= {intent.MaximumContinuousBeds}");
        var clips = new List<AudioClip>();
        foreach (AudioSource bed in beds)
            if (bed.clip != null && !clips.Contains(bed.clip)) clips.Add(bed.clip);
        foreach (string resourceName in intent.ContinuousClipResourceNames)
        {
            if (string.IsNullOrWhiteSpace(resourceName)) continue;
            AudioClip clip = Resources.Load<AudioClip>($"Sfx/{resourceName}");
            if (clip == null)
                report.Add("soundscape", GmAuditSeverity.Error, intent.IntentId,
                    $"declared continuous clip '{resourceName}' is missing from Resources/Sfx");
            else if (!clips.Contains(clip)) clips.Add(clip);
        }
        foreach (AudioClip clip in clips)
        {
            GmAudioClipMetrics metrics = GmAudioAnalysis.Measure(clip);
            if (intent.ForbidMachineLikeTonality && metrics.spaceshipRisk)
                // This remains a warning while the authored F8 comparison has no Nick verdict.
                // The analyser may reject a source, but it may not silently choose taste for him.
                report.Add("soundscape", GmAuditSeverity.Warning, intent.IntentId,
                    $"continuous bed '{clip.name}' risks stationary machine-like character; compare the sparse and breathing candidates",
                    $"tone={metrics.persistentToneDb:F1}dB stationarity={metrics.stationarity:F2} " +
                    $"silence={metrics.silenceShare:P0} seam={metrics.loopDiscontinuity:F2}",
                    $"tone <= {intent.MaximumPersistentToneDb:F1}dB or an irregular/sparse envelope");
        }
    }

    static void AnalyzePacing(GmPacingIntent intent, GmSceneAuditReport report)
    {
        ValidateText(intent.IntentId, "pacing", intent.IntentId, "intent id", report);
        ValidateText(intent.Rationale, "pacing", intent.IntentId, "rationale", report, 20);
        if (intent.CanonicalTollCount != 9 || intent.FirstSymptomToll != 4 || intent.BlackoutToll != 9)
            report.Add("pacing", GmAuditSeverity.Error, intent.IntentId,
                "Wend Hill canonical count, symptom onset, or blackout toll changed",
                $"{intent.CanonicalTollCount}/{intent.FirstSymptomToll}/{intent.BlackoutToll}", "9/4/9");
        if (intent.Candidates.Count == 0 || intent.Candidates.Count > 3)
            report.Add("pacing", GmAuditSeverity.Error, intent.IntentId,
                "pacing review requires one to three bounded candidates",
                intent.Candidates.Count.ToString(), "1..3");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (GmPacingCandidate candidate in intent.Candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.CandidateId) || !ids.Add(candidate.CandidateId))
                report.Add("pacing", GmAuditSeverity.Error, intent.IntentId,
                    $"pacing candidate id '{candidate.CandidateId}' is empty or duplicated");
            if (candidate.FirstTollDelay < 20f || candidate.TollInterval < 15f)
                report.Add("pacing", GmAuditSeverity.Error, intent.IntentId,
                    $"pacing candidate '{candidate.CandidateId}' is test-speed",
                    $"{candidate.FirstTollDelay:F0}+8x{candidate.TollInterval:F0}", "first >=20s, interval >=15s");
        }
        if (intent.Scenarios.Count == 0)
            report.Add("pacing", GmAuditSeverity.Error, intent.IntentId,
                "pacing intent has no deterministic player-route scenarios");
        var scenarioIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (GmPacingScenario scenario in intent.Scenarios)
        {
            if (string.IsNullOrWhiteSpace(scenario.ScenarioId) || !scenarioIds.Add(scenario.ScenarioId))
                report.Add("pacing", GmAuditSeverity.Error, intent.IntentId,
                    $"pacing scenario id '{scenario.ScenarioId}' is empty or duplicated");
            if (scenario.Waypoints.Count < 2)
                report.Add("pacing", GmAuditSeverity.Error, intent.IntentId,
                    $"pacing scenario '{scenario.ScenarioId}' needs at least two waypoints");
        }
        if (intent.Candidates.Count > 0 && intent.Scenarios.Count > 0)
        {
            GmPacingSimulationReport simulation = GmPacingSimulator.Simulate("audit", intent);
            foreach (GmPacingSimulationRow row in simulation.rows)
            {
                if (!row.routeCompletesBeforeBlackout)
                    report.Add("pacing", GmAuditSeverity.Error, intent.IntentId,
                        $"scenario '{row.scenarioId}' cannot complete under '{row.candidateId}'",
                        row.routeCompletionSeconds.ToString("F1"), $"<= {row.blackoutSeconds:F1}s");
                if (row.exceedsQuietBudget)
                    report.Add("pacing", GmAuditSeverity.Error, intent.IntentId,
                        $"scenario '{row.scenarioId}' has an unintended event desert under '{row.candidateId}'",
                        row.maximumEventGapSeconds.ToString("F1"),
                        $"<= {intent.MaximumUnintendedQuietSeconds:F1}s");
            }
        }
    }

    static void WithShot(Camera camera, GmSceneReviewTour tour, GmReviewShot shot, Action action)
    {
        Vector3 oldPosition = camera.transform.position;
        Quaternion oldRotation = camera.transform.rotation;
        float oldAspect = camera.aspect;
        try
        {
            camera.transform.position = shot.Position;
            camera.transform.rotation = Quaternion.Euler(shot.Pitch,
                shot.Yaw + tour.ReviewYawOffsetForAudit, 0f);
            camera.aspect = tour.ShotHeight == 0 ? 16f / 9f : tour.ShotWidth / (float)tour.ShotHeight;
            action();
        }
        finally
        {
            camera.transform.position = oldPosition;
            camera.transform.rotation = oldRotation;
            camera.aspect = oldAspect;
        }
    }

    static Bounds BoundsOf(Component component)
    {
        Renderer[] renderers = component.GetComponentsInChildren<Renderer>(true)
            .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy).ToArray();
        if (renderers.Length == 0) return new Bounds(component.transform.position, Vector3.zero);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static bool TryViewportRect(Camera camera, Bounds bounds, out Rect rect)
    {
        rect = default;
        if (!GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera), bounds)) return false;
        float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
        foreach (Vector3 corner in Corners(bounds))
        {
            Vector3 point = camera.WorldToViewportPoint(corner);
            if (point.z <= 0f) continue;
            minX = Mathf.Min(minX, point.x); minY = Mathf.Min(minY, point.y);
            maxX = Mathf.Max(maxX, point.x); maxY = Mathf.Max(maxY, point.y);
        }
        if (float.IsInfinity(minX)) return false;
        rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return rect.xMax >= 0f && rect.xMin <= 1f && rect.yMax >= 0f && rect.yMin <= 1f;
    }

    static IEnumerable<Vector3> Corners(Bounds bounds)
    {
        Vector3 min = bounds.min, max = bounds.max;
        yield return new Vector3(min.x, min.y, min.z);
        yield return new Vector3(min.x, min.y, max.z);
        yield return new Vector3(min.x, max.y, min.z);
        yield return new Vector3(min.x, max.y, max.z);
        yield return new Vector3(max.x, min.y, min.z);
        yield return new Vector3(max.x, min.y, max.z);
        yield return new Vector3(max.x, max.y, min.z);
        yield return new Vector3(max.x, max.y, max.z);
    }

    static void ValidateText(string value, string category, string subject, string label,
        GmSceneAuditReport report, int minimumLength = 1)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim() != value || value.Length < minimumLength)
            report.Add(category, GmAuditSeverity.Error, subject,
                $"{label} must be a deliberate trimmed value of at least {minimumLength} characters");
    }
}
