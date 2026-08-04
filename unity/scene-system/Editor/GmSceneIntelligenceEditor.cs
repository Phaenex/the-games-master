// Human-facing review console plus guarded, temporary variant rendering. Any variant mutation is
// restored in finally, never saved, and fingerprint checked before the result is accepted.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

public static class GmSceneIntelligencePaths
{
    public static string FindRepoRoot()
    {
        string configured = Environment.GetEnvironmentVariable("GM_REPO_ROOT");
        if (IsRepo(configured)) return configured;
        string cursor = Directory.GetParent(Application.dataPath)?.FullName;
        for (int i = 0; i < 5 && !string.IsNullOrEmpty(cursor); i++)
        {
            if (IsRepo(cursor)) return cursor;
            cursor = Directory.GetParent(cursor)?.FullName;
        }
        foreach (string candidate in ConventionalRoots())
            if (IsRepo(candidate)) return candidate;
        throw new DirectoryNotFoundException(
            "Cannot find the-games-master repo. Searched GM_REPO_ROOT, five parents of " +
            Application.dataPath + ", and the Projects tree. Set GM_REPO_ROOT.");
    }

    // The repo has already moved once (~/Projects -> ~/Projects/games), so probe a level of
    // grouping directories instead of pinning one path that the next move would break again.
    static IEnumerable<string> ConventionalRoots()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string projects = Path.Combine(home, "Projects");
        yield return Path.Combine(projects, "the-games-master");
        string[] groups;
        try { groups = Directory.GetDirectories(projects); }
        catch (Exception) { yield break; }
        Array.Sort(groups, StringComparer.Ordinal);
        foreach (string group in groups)
            yield return Path.Combine(group, "the-games-master");
    }

    public static string KnowledgeRoot => Path.Combine(FindRepoRoot(), "unity", "scene-system", "knowledge");
    public static string LibraryRoot => Path.Combine(Directory.GetCurrentDirectory(), "Library", "GmSceneIntelligence");

    static bool IsRepo(string candidate) => !string.IsNullOrEmpty(candidate) &&
        File.Exists(Path.Combine(candidate, "scripts", "scene-learning.mjs"));
}

public static class GmSceneFingerprint
{
    public static string Current()
    {
        var rows = new List<string>();
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) return "invalid-scene";
        foreach (GameObject root in scene.GetRootGameObjects()) Collect(root.transform, rows);
        rows.Sort(StringComparer.Ordinal);
        unchecked
        {
            ulong hash = 14695981039346656037UL;
            foreach (string row in rows)
                foreach (char c in row) { hash ^= c; hash *= 1099511628211UL; }
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }
    }

    static void Collect(Transform transform, List<string> rows)
    {
        Vector3 p = transform.position, r = transform.eulerAngles, s = transform.lossyScale;
        rows.Add($"{PathOf(transform)}|{p.x:F3},{p.y:F3},{p.z:F3}|{r.x:F2},{r.y:F2},{r.z:F2}|{s.x:F3},{s.y:F3},{s.z:F3}");
        foreach (Transform child in transform) Collect(child, rows);
    }

    public static string PathOf(Transform transform)
    {
        string value = transform.name;
        while (transform.parent != null) { transform = transform.parent; value = transform.name + "/" + value; }
        return value;
    }
}

public static class GmKnowledgeBridge
{
    public static string Run(params string[] arguments)
    {
        string repo = GmSceneIntelligencePaths.FindRepoRoot();
        string script = Path.Combine(repo, "scripts", "scene-learning.mjs");
        var parts = new List<string> { Quote(script) };
        parts.AddRange(arguments.Select(Quote));
        parts.Add("--root");
        parts.Add(Quote(GmSceneIntelligencePaths.KnowledgeRoot));
        var start = new ProcessStartInfo {
            FileName = "node",
            Arguments = string.Join(" ", parts),
            WorkingDirectory = repo,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        using (Process process = Process.Start(start))
        {
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit(30000);
            if (!process.HasExited) { process.Kill(); throw new TimeoutException("scene-learning CLI timed out"); }
            if (process.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? output : error);
            return output.Trim();
        }
    }

    static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}

[Serializable]
sealed class GmReviewSessionDocument
{
    public int schemaVersion = 2;
    public string sessionId;
    public string sceneId;
    public string createdAt;
    public GmReviewerDocument reviewer;
    public GmEvidenceDocument evidence;
    public GmVerdictDocument[] verdicts;
    public GmObjectiveFindingDocument[] objectiveFindings = Array.Empty<GmObjectiveFindingDocument>();
}

[Serializable] sealed class GmReviewerDocument { public string kind; public string id; public string displayName; }
[Serializable] sealed class GmEvidenceDocument { public string buildFingerprint; public string captureSet; public string notes; }
[Serializable] sealed class GmVerdictDocument
{
    public string id;
    public string category;
    public string verdict;
    public GmReviewContextDocument context;
    public string[] tags;
    public string note;
}
[Serializable] sealed class GmObjectiveFindingDocument
{
    public string id;
    public string metric;
    public string status;
    public string actual;
    public string expected;
    public string category;
    public string severity;
    public string subjectId;
    public string evidencePath;
    public string note;
    public GmReviewContextDocument context;
}
[Serializable] sealed class GmRuleCandidateListDocument { public GmRuleCandidateSummaryDocument[] items = Array.Empty<GmRuleCandidateSummaryDocument>(); }
[Serializable] sealed class GmRuleCandidateSummaryDocument { public string candidateId; public string signature; }
[Serializable] sealed class GmDefectListDocument { public GmDefectSummaryDocument[] items = Array.Empty<GmDefectSummaryDocument>(); }
[Serializable] sealed class GmDefectSummaryDocument { public string defectId; public string sceneId; public string category; public string status; }
[Serializable] sealed class GmReviewContextDocument
{
    public string shotName;
    public string zoneId;
    public string clusterId;
    public string elementId;
    public string assetGuid;
    public string assetFamily;
    public string assetRole;
    public string candidateProfileId;
    public string intentId;
}

public static class GmKnowledgeReviewActions
{
    public static string[] CandidateIds(out string[] labels)
    {
        string file = Path.Combine(GmSceneIntelligencePaths.KnowledgeRoot, "derived", "rule-candidates.json");
        GmRuleCandidateListDocument document = Read<GmRuleCandidateListDocument>(file);
        GmRuleCandidateSummaryDocument[] items = document.items ?? Array.Empty<GmRuleCandidateSummaryDocument>();
        labels = items.Select(item => $"{item.candidateId}  {item.signature}").ToArray();
        return items.Select(item => item.candidateId).ToArray();
    }

    public static string[] OpenDefectIds(out string[] labels)
    {
        string file = Path.Combine(GmSceneIntelligencePaths.KnowledgeRoot, "derived", "defects.json");
        GmDefectListDocument document = Read<GmDefectListDocument>(file);
        GmDefectSummaryDocument[] items = (document.items ?? Array.Empty<GmDefectSummaryDocument>())
            .Where(item => item.status == "open").ToArray();
        labels = items.Select(item => $"{item.defectId}  {item.sceneId}/{item.category}").ToArray();
        return items.Select(item => item.defectId).ToArray();
    }

    public static string Promote(string candidateId) =>
        GmKnowledgeBridge.Run("promote", candidateId, "--confirm-nick");

    public static string Resolve(string defectId) =>
        GmKnowledgeBridge.Run("resolve", defectId, "--confirm-nick");

    static T Read<T>(string file)
    {
        if (!File.Exists(file)) throw new FileNotFoundException("Knowledge file is missing. Rebuild derived knowledge first.", file);
        T document = JsonUtility.FromJson<T>(File.ReadAllText(file));
        if (document == null) throw new InvalidDataException($"Knowledge file could not be parsed: {file}");
        return document;
    }
}

public static class GmReviewRecorder
{
    public static string RecordNickVerdict(string category, string verdict, string tag, string note)
    {
        if (verdict != "keep" && string.IsNullOrWhiteSpace(tag))
            throw new ArgumentException("Change and Reject require a defect tag.");
        GmSceneComposition manifest = Object.FindAnyObjectByType<GmSceneComposition>();
        if (manifest == null) throw new InvalidOperationException("The open scene has no GmSceneComposition.");
        GmCompositionElement element = Selection.activeGameObject == null ? null :
            Selection.activeGameObject.GetComponentInParent<GmCompositionElement>();
        GmCompositionCluster cluster = element == null ? null : FindById<GmCompositionCluster>(element.ClusterId, value => value.ClusterId);
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        string shortId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var document = new GmReviewSessionDocument {
            sessionId = $"nick-{manifest.SceneId}-{DateTime.UtcNow:yyyyMMddHHmmss}-{shortId}",
            sceneId = manifest.SceneId,
            createdAt = timestamp,
            reviewer = new GmReviewerDocument { kind = "nick", id = "nick", displayName = "Nick" },
            evidence = new GmEvidenceDocument {
                buildFingerprint = GmSceneFingerprint.Current(),
                captureSet = Path.Combine("Screens", SceneManager.GetActiveScene().name),
                notes = "Recorded explicitly in the Unity Scene Intelligence review window.",
            },
            verdicts = new[] {
                new GmVerdictDocument {
                    id = $"{category}-{verdict}-{shortId}", category = category, verdict = verdict,
                    tags = verdict == "keep" ? Array.Empty<string>() : new[] { tag.Trim() },
                    note = (note ?? "").Trim(),
                    context = new GmReviewContextDocument {
                        elementId = element?.ElementId,
                        clusterId = element?.ClusterId,
                        zoneId = cluster?.ZoneId,
                        assetFamily = element?.AssetFamily,
                        assetRole = element?.Role.ToString(),
                    },
                },
            },
        };
        string directory = GmSceneIntelligencePaths.LibraryRoot;
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, document.sessionId + ".json");
        File.WriteAllText(temporary, JsonUtility.ToJson(document, true) + "\n");
        try { return GmKnowledgeBridge.Run("record", temporary); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static string RecordObjectiveAudit()
    {
        GmSceneComposition manifest = Object.FindAnyObjectByType<GmSceneComposition>();
        if (manifest == null) throw new InvalidOperationException("The open scene has no GmSceneComposition.");
        GmSceneReviewTour tour = Object.FindAnyObjectByType<GmSceneReviewTour>();
        Camera camera = Camera.main ?? Object.FindAnyObjectByType<Camera>();
        GmSceneAuditReport report = GmSceneCompositionAudit.AnalyzeOpenScene(manifest.SceneId, tour, camera);
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        string shortId = Guid.NewGuid().ToString("N").Substring(0, 8);
        GmObjectiveFindingDocument[] findings = report.findings.Select((finding, index) =>
            new GmObjectiveFindingDocument {
                id = $"{finding.category}-{index + 1:000}",
                metric = finding.message,
                status = finding.severity == GmAuditSeverity.Error ? "fail" :
                    finding.severity == GmAuditSeverity.Warning ? "warn" : "pass",
                actual = string.IsNullOrWhiteSpace(finding.actual) ? "reported" : finding.actual,
                expected = string.IsNullOrWhiteSpace(finding.expected) ? "no finding" : finding.expected,
                category = finding.category,
                severity = finding.severity.ToString().ToLowerInvariant(),
                subjectId = string.IsNullOrWhiteSpace(finding.subjectId) ? manifest.SceneId : finding.subjectId,
                evidencePath = Path.Combine("Screens", SceneManager.GetActiveScene().name),
                note = finding.message,
                context = new GmReviewContextDocument { intentId = finding.subjectId },
            }).ToArray();
        if (findings.Length == 0)
            findings = new[] { new GmObjectiveFindingDocument {
                id = "audit-pass-001", metric = "perceptual-and-structural-audit", status = "pass",
                actual = "pass", expected = "pass", category = "audit", severity = "info",
                subjectId = manifest.SceneId, evidencePath = Path.Combine("Screens", SceneManager.GetActiveScene().name),
                note = "No structural or perceptual findings were produced.",
                context = new GmReviewContextDocument(),
            } };
        var document = new GmReviewSessionDocument {
            sessionId = $"automation-{manifest.SceneId}-{DateTime.UtcNow:yyyyMMddHHmmss}-{shortId}",
            sceneId = manifest.SceneId,
            createdAt = timestamp,
            reviewer = new GmReviewerDocument {
                kind = "automation", id = "gm-perceptual-audit", displayName = "Scene Intelligence"
            },
            evidence = new GmEvidenceDocument {
                buildFingerprint = report.fingerprint,
                captureSet = Path.Combine("Screens", SceneManager.GetActiveScene().name),
                notes = "Objective audit evidence only. No taste verdict was inferred.",
            },
            verdicts = Array.Empty<GmVerdictDocument>(),
            objectiveFindings = findings,
        };
        string directory = GmSceneIntelligencePaths.LibraryRoot;
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, document.sessionId + ".json");
        File.WriteAllText(temporary, JsonUtility.ToJson(document, true) + "\n");
        try { return GmKnowledgeBridge.Run("record", temporary); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    static T FindById<T>(string id, Func<T, string> selector) where T : Component
    {
        foreach (T item in Object.FindObjectsByType<T>(FindObjectsInactive.Include))
            if (selector(item) == id) return item;
        return null;
    }
}

public static class GmGuardedVariantPreview
{
    public const string OutputRoot = "Library/GmSceneIntelligence/variants";

    struct TransformSnapshot
    {
        public Transform transform;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
        public bool active;
    }

    [MenuItem("Games Master/Scene Intelligence/Generate Guarded Variant Previews")]
    public static void GenerateAllMenu()
    {
        int count = GenerateAll();
        EditorUtility.DisplayDialog("Guarded variants", $"Generated {count} temporary preview(s). No variant was selected and the scene was not saved.", "OK");
    }

    public static int GenerateAll(Action injectedAfterMutation = null)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path)) throw new InvalidOperationException("Open a saved scene first.");
        if (scene.isDirty) throw new InvalidOperationException("Save or discard current scene edits before generating temporary variants.");
        GmSceneComposition initialManifest = Object.FindAnyObjectByType<GmSceneComposition>();
        if (initialManifest == null) throw new InvalidOperationException("No GmSceneComposition in the open scene.");
        var jobs = new List<string[]>();
        foreach (GmAdaptiveSlot slot in FindSceneObjects<GmAdaptiveSlot>()
            .OrderBy(value => value.SlotId, StringComparer.Ordinal))
            foreach (GmAdaptiveCandidate candidate in slot.Candidates)
                jobs.Add(new[] { slot.SlotId, candidate.CandidateId });
        string beforeFingerprint = GmSceneFingerprint.Current();
        int written = 0;
        foreach (string[] job in jobs)
        {
            GmSceneComposition manifest = Object.FindAnyObjectByType<GmSceneComposition>();
            Camera camera = Camera.main ?? Object.FindAnyObjectByType<Camera>();
            GmSceneReviewTour tour = Object.FindAnyObjectByType<GmSceneReviewTour>();
            GmAdaptiveSlot slot = FindSceneObjects<GmAdaptiveSlot>()
                .FirstOrDefault(value => value.SlotId == job[0]);
            GmAdaptiveCandidate candidate = slot?.Candidates.FirstOrDefault(value => value.CandidateId == job[1]);
            if (manifest == null || camera == null || slot == null || candidate == null)
                throw new InvalidOperationException($"variant job '{job[0]}/{job[1]}' could not be restored after scene reload");
            RenderOne(manifest, tour, camera, slot, candidate, injectedAfterMutation);
            written++;
            if (GmSceneFingerprint.Current() != beforeFingerprint)
                throw new InvalidOperationException($"variant '{candidate.CandidateId}' failed exact scene restoration");
        }
        return written;
    }

    public static void GenerateWendHillBatch()
    {
        EditorSceneManager.OpenScene(GmSceneCatalog.WendHillPath, OpenSceneMode.Single);
        int count = GenerateAll();
        Debug.Log($"[GmGuardedVariant] PASS previews={count} selected=none fingerprint={GmSceneFingerprint.Current()}");
    }

    static void RenderOne(GmSceneComposition manifest, GmSceneReviewTour tour, Camera camera,
        GmAdaptiveSlot slot, GmAdaptiveCandidate candidate, Action injectedAfterMutation)
    {
        GmCompositionElement original = slot.GetComponent<GmCompositionElement>();
        if (original == null) original = FindElement(slot.AllowedSwapElementIds.FirstOrDefault());
        if (original == null)
        {
            string requested = slot.AllowedSwapElementIds.FirstOrDefault() ?? "<none>";
            string localComponents = string.Join(",", slot.gameObject.GetComponents<Component>()
                .Select(component => component == null ? "<missing-script>" : component.GetType().Name));
            string knownElements = string.Join(",", Resources.FindObjectsOfTypeAll<GmCompositionElement>()
                .Where(element => element != null && element.gameObject.scene == SceneManager.GetActiveScene())
                .Select(element => element.ElementId).Take(12));
            throw new InvalidOperationException($"slot '{slot.SlotId}' has no resolvable swap element '{requested}'; local=[{localComponents}] known=[{knownElements}]");
        }
        if (IsExcluded(candidate.AssetGuid, manifest.SceneId, slot.ZoneId))
            throw new InvalidOperationException($"candidate '{candidate.CandidateId}' is contextually excluded by Nick");
        string assetPath = AssetDatabase.GUIDToAssetPath(candidate.AssetGuid);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null) throw new InvalidOperationException($"candidate '{candidate.CandidateId}' GUID is not an imported GameObject");
        foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null || material.shader == null)
                    throw new InvalidOperationException($"candidate '{candidate.CandidateId}' has a missing material or shader");
                string shaderPath = AssetDatabase.GetAssetPath(material.shader);
                if (string.IsNullOrEmpty(shaderPath) || shaderPath.StartsWith("Resources/") || shaderPath.StartsWith("Library/"))
                    throw new InvalidOperationException($"candidate '{candidate.CandidateId}' uses non-HDRP shader '{material.shader.name}'");
            }

        Scene scene = SceneManager.GetActiveScene();
        bool initiallyDirty = scene.isDirty;
        Transform cameraTransform = camera.transform;
        var snapshots = new[] {
            Snapshot(original.transform), Snapshot(cameraTransform),
        };
        Renderer[] originalRenderers = original.GetComponentsInChildren<Renderer>(true);
        bool[] rendererStates = originalRenderers.Select(renderer => renderer.enabled).ToArray();
        Collider[] originalColliders = original.GetComponentsInChildren<Collider>(true);
        bool[] colliderStates = originalColliders.Select(collider => collider.enabled).ToArray();
        GameObject temporary = null;
        try
        {
            GameObject owner = original.gameObject;
            foreach (Renderer renderer in originalRenderers) renderer.enabled = false;
            foreach (Collider collider in originalColliders) collider.enabled = false;
            temporary = (GameObject)PrefabUtility.InstantiatePrefab(prefab, owner.transform);
            temporary.name = $"__GmVariant_{slot.SlotId}_{candidate.CandidateId}";
            temporary.transform.localPosition = candidate.LocalOffset;
            temporary.transform.localRotation = Quaternion.Euler(0f, candidate.YawOffset, 0f);
            temporary.transform.localScale = Vector3.Scale(prefab.transform.localScale, candidate.ScaleMultiplier);
            MatchAuthoredSurface(originalRenderers, temporary);
            Renderer[] candidateRenderers = temporary.GetComponentsInChildren<Renderer>(true);
            if (candidateRenderers.Length == 0) throw new InvalidOperationException("candidate has no renderer");
            Bounds candidateBounds = candidateRenderers[0].bounds;
            for (int i = 1; i < candidateRenderers.Length; i++) candidateBounds.Encapsulate(candidateRenderers[i].bounds);
            float groundingCorrection = original.DeclaredSurfaceY - candidateBounds.min.y;
            if (Mathf.Abs(groundingCorrection) > slot.GroundingTolerance)
                throw new InvalidOperationException($"candidate '{candidate.CandidateId}' needs {groundingCorrection:F2}m grounding correction, limit {slot.GroundingTolerance:F2}m");
            temporary.transform.position += Vector3.up * groundingCorrection;
            Physics.SyncTransforms();
            RejectNewAuditFailures(manifest, tour, camera);
            injectedAfterMutation?.Invoke();
            Frame(camera, temporary);
            Capture(camera, manifest.SceneId, slot.SlotId, candidate.CandidateId);
        }
        finally
        {
            if (temporary != null) Object.DestroyImmediate(temporary);
            for (int i = 0; i < originalRenderers.Length; i++)
                if (originalRenderers[i] != null) originalRenderers[i].enabled = rendererStates[i];
            for (int i = 0; i < originalColliders.Length; i++)
                if (originalColliders[i] != null) originalColliders[i].enabled = colliderStates[i];
            foreach (TransformSnapshot snapshot in snapshots) Restore(snapshot);
            Physics.SyncTransforms();
            if (!initiallyDirty && scene.isDirty)
                EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
        }
    }

    /// Variant previews compare silhouette and placement, not the raw demo-scene palette shipped
    /// by an asset pack. Clone candidate materials and carry the replaced detail's authored surface
    /// controls across before auditing. Source assets remain untouched, while a bright green grass
    /// prefab cannot bypass the same dry-night palette that constrains its in-scene counterpart.
    static void MatchAuthoredSurface(Renderer[] originalRenderers, GameObject candidate)
    {
        Material[] references = originalRenderers
            .SelectMany(renderer => renderer.sharedMaterials)
            .Where(material => material != null)
            .Distinct().ToArray();
        if (references.Length == 0) return;

        string[] colours = { "_BaseColor", "_BaseColor_Value", "_Albedo_Tint", "_ColorGrass", "_Color" };
        string[] floats = { "_Smoothness", "_Glossiness", "_Brightness", "_Saturation",
            "_Albedo_Saturation", "_Albedo_Saturation_1", "_Color_Variation" };
        foreach (Renderer renderer in candidate.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int index = 0; index < materials.Length; index++)
            {
                Material source = materials[index];
                if (source == null) continue;
                Material reference = references.FirstOrDefault(item => item.shader == source.shader) ??
                    references.FirstOrDefault(item => colours.Any(property =>
                        item.HasProperty(property) && source.HasProperty(property))) ?? references[0];
                var clone = new Material(source) { name = source.name + "_GmVariantPalette" };
                string fallbackColour = colours.FirstOrDefault(reference.HasProperty);
                foreach (string property in colours)
                {
                    if (!clone.HasProperty(property)) continue;
                    if (reference.HasProperty(property))
                        clone.SetColor(property, reference.GetColor(property));
                    else if (!string.IsNullOrEmpty(fallbackColour))
                    {
                        Color tint = reference.GetColor(fallbackColour);
                        Color old = clone.GetColor(property);
                        clone.SetColor(property, new Color(tint.r, tint.g, tint.b, old.a));
                    }
                }
                foreach (string property in floats)
                    if (clone.HasProperty(property) && reference.HasProperty(property))
                        clone.SetFloat(property, reference.GetFloat(property));
                materials[index] = clone;
            }
            renderer.sharedMaterials = materials;
        }
    }

    static TransformSnapshot Snapshot(Transform transform) => new TransformSnapshot {
        transform = transform, localPosition = transform.localPosition, localRotation = transform.localRotation,
        localScale = transform.localScale, active = transform.gameObject.activeSelf,
    };

    static void Restore(TransformSnapshot snapshot)
    {
        if (snapshot.transform == null) return;
        snapshot.transform.localPosition = snapshot.localPosition;
        snapshot.transform.localRotation = snapshot.localRotation;
        snapshot.transform.localScale = snapshot.localScale;
        snapshot.transform.gameObject.SetActive(snapshot.active);
    }

    static GmCompositionElement FindElement(string id) => Object.FindObjectsByType<GmCompositionElement>(FindObjectsInactive.Include)
        .FirstOrDefault(element => element.ElementId == id);

    static IEnumerable<T> FindSceneObjects<T>() where T : Component
    {
        Scene active = SceneManager.GetActiveScene();
        return Resources.FindObjectsOfTypeAll<T>().Where(component => component != null &&
            component.gameObject.scene == active);
    }

    static bool IsExcluded(string guid, string sceneId, string zoneId)
    {
        string file = Path.Combine(GmSceneIntelligencePaths.KnowledgeRoot, "derived", "asset-preferences.json");
        if (!File.Exists(file)) return false;
        string json = File.ReadAllText(file);
        // The derived file is deterministic and small. Match only an entry containing this exact
        // GUID/context and a true exclusion; agent rejections never set the exclusion bit.
        foreach (string block in json.Split(new[] { "\"key\"" }, StringSplitOptions.None))
            if (block.Contains("\"assetGuid\": \"" + guid + "\"") &&
                block.Contains("\"sceneId\": \"" + sceneId + "\"") &&
                (string.IsNullOrEmpty(zoneId) || block.Contains("\"zoneId\": \"" + zoneId + "\"")) &&
                block.Contains("\"contextualExclusion\": true")) return true;
        return false;
    }

    static void RejectNewAuditFailures(GmSceneComposition manifest, GmSceneReviewTour tour, Camera camera)
    {
        List<string> issues = GmSceneCompositionAudit.ValidateOpenScene(manifest.SceneId, tour, camera);
        if (issues.Count > 0)
            throw new InvalidOperationException("variant violates scene guards: " + string.Join(" | ", issues.Take(5)));
    }

    static void Frame(Camera camera, GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException("variant has no renderers");
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        Vector3 direction = camera.transform.position - bounds.center;
        direction.y = Mathf.Max(0.2f, direction.y);
        if (direction.sqrMagnitude < 0.1f) direction = new Vector3(1f, 0.35f, 1f);
        direction.Normalize();
        float distance = Mathf.Max(3f, bounds.extents.magnitude * 2.8f);
        camera.transform.position = bounds.center + direction * distance;
        camera.transform.LookAt(bounds.center);
    }

    static void Capture(Camera camera, string sceneId, string slotId, string candidateId)
    {
        const int width = 1280, height = 720;
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        RenderTexture previous = RenderTexture.active;
        RenderTexture cameraTarget = camera.targetTexture;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            string directory = Path.Combine(OutputRoot, sceneId, slotId);
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, candidateId + ".png"), texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = cameraTarget;
            RenderTexture.active = previous;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(texture);
        }
    }
}

public sealed class GmSceneIntelligenceWindow : EditorWindow
{
    static readonly string[] Categories = { "visual", "asset", "lighting", "audio", "navigation", "ui", "performance", "pacing" };
    int category;
    string defectTag = "";
    string note = "";
    int candidateIndex;
    int defectIndex;
    Vector2 scroll;
    string status = "Ready. Nick feedback is never inferred or pre-filled.";

    [MenuItem("Games Master/Scene Intelligence/Review Window")]
    public static void ShowWindow() => GetWindow<GmSceneIntelligenceWindow>("Scene Intelligence");

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        GmSceneComposition manifest = Object.FindAnyObjectByType<GmSceneComposition>();
        EditorGUILayout.LabelField("Supervised scene review", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Variants are temporary previews. This window never selects one automatically, and agent observations never impersonate your feedback.", MessageType.Info);
        EditorGUILayout.LabelField("Scene", manifest == null ? "No composition manifest" : manifest.SceneId);
        EditorGUILayout.LabelField("Transform fingerprint", GmSceneFingerprint.Current());
        EditorGUILayout.LabelField("Selected review subject", Selection.activeGameObject == null ? "Whole scene" : GmSceneFingerprint.PathOf(Selection.activeGameObject.transform));
        int claims = Object.FindObjectsByType<GmReviewCompositionClaim>(FindObjectsInactive.Include).Length;
        int slots = Object.FindObjectsByType<GmAdaptiveSlot>(FindObjectsInactive.Include).Length;
        int perceptual = Object.FindObjectsByType<GmRepetitionIntent>(FindObjectsInactive.Include).Length +
            Object.FindObjectsByType<GmEnvironmentalStoryIntent>(FindObjectsInactive.Include).Length +
            Object.FindObjectsByType<GmStyleIntent>(FindObjectsInactive.Include).Length +
            Object.FindObjectsByType<GmSurfacePaletteIntent>(FindObjectsInactive.Include).Length +
            Object.FindObjectsByType<GmLandscapeDepthIntent>(FindObjectsInactive.Include).Length +
            Object.FindObjectsByType<GmSoundscapeIntent>(FindObjectsInactive.Include).Length +
            Object.FindObjectsByType<GmPacingIntent>(FindObjectsInactive.Include).Length;
        EditorGUILayout.LabelField("Evidence", $"{claims} shot claim(s), {slots} guarded slot(s), {perceptual} perceptual contract(s)");

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Nick verdict", EditorStyles.boldLabel);
        category = EditorGUILayout.Popup("Category", category, Categories);
        defectTag = EditorGUILayout.TextField("Defect tag", defectTag);
        note = EditorGUILayout.TextField("Optional note", note);
        using (new EditorGUI.DisabledScope(manifest == null))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Keep")) Record("keep");
            if (GUILayout.Button("Change")) Record("change");
            if (GUILayout.Button("Reject")) Record("reject");
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Evidence and tools", EditorStyles.boldLabel);
        if (GUILayout.Button("Analyze structural + perceptual contracts")) Run(() => {
            if (manifest == null) throw new InvalidOperationException("No scene composition manifest.");
            GmSceneAuditReport report = GmSceneCompositionAudit.AnalyzeOpenScene(manifest.SceneId,
                Object.FindAnyObjectByType<GmSceneReviewTour>(), Camera.main ?? Object.FindAnyObjectByType<Camera>());
            int errors = report.findings.Count(item => item.severity == GmAuditSeverity.Error);
            int warnings = report.findings.Count(item => item.severity == GmAuditSeverity.Warning);
            status = $"Audit {(errors == 0 ? "PASS" : "FAIL")}: {errors} error(s), {warnings} warning(s), fingerprint {report.fingerprint}.";
        });
        if (GUILayout.Button("Record objective audit evidence (no verdict)"))
            Run(() => status = GmReviewRecorder.RecordObjectiveAudit());
        if (GUILayout.Button("Write pacing comparison report (select none)"))
            Run(() => status = $"Wrote pacing evidence to {GmPacingSimulator.WriteOpenSceneReport()}; selected none.");
        if (GUILayout.Button("Reindex imported assets + contact manifests")) Run(() => GmAssetIntelligence.Reindex(true));
        if (GUILayout.Button("Generate guarded variant previews (select none)")) Run(() => status = $"Generated {GmGuardedVariantPreview.GenerateAll()} preview(s); selected none.");
        if (GUILayout.Button("Capture canonical review tour")) Run(ArmTour);
        if (GUILayout.Button("Walk current scene")) EditorApplication.isPlaying = true;
        if (GUILayout.Button("Evaluate captured frames against baseline")) Run(() => {
            GmBaselineEvaluation evaluation = GmVisualBaseline.Evaluate(GmVisualBaseline.MeasureOpenScene());
            status = evaluation.HasApprovedBaseline
                ? $"Baseline {(evaluation.Pass ? "PASS" : "FAIL")}: {string.Join("; ", evaluation.Failures)}"
                : string.Join("; ", evaluation.Warnings);
        });
        if (GUILayout.Button("Approve visual baseline (Nick gate)")) Run(() => {
            GmSceneComposition active = Object.FindAnyObjectByType<GmSceneComposition>();
            if (active == null) throw new InvalidOperationException("No scene composition manifest.");
            List<string> blockers = GmVisualBaseline.ApprovalBlockers(active.SceneId);
            if (blockers.Count > 0) throw new InvalidOperationException(string.Join("; ", blockers));
            if (!EditorUtility.DisplayDialog("Approve baseline", "Lock the current captures and transform fingerprint as Nick's approved baseline?", "Approve", "Cancel")) return;
            GmVisualBaseline.ApproveCurrent();
            status = "Approved visual baseline.";
        });
        if (GUILayout.Button("Refresh derived knowledge")) Run(() => status = GmKnowledgeBridge.Run("rebuild"));

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Explicit knowledge gates", EditorStyles.boldLabel);
        string knowledgeError = null;
        string[] candidateIds = Array.Empty<string>(), candidateLabels = Array.Empty<string>();
        string[] defectIds = Array.Empty<string>(), defectLabels = Array.Empty<string>();
        try
        {
            candidateIds = GmKnowledgeReviewActions.CandidateIds(out candidateLabels);
            defectIds = GmKnowledgeReviewActions.OpenDefectIds(out defectLabels);
        }
        catch (Exception error) { knowledgeError = error.Message; }
        if (knowledgeError != null) EditorGUILayout.HelpBox(knowledgeError, MessageType.Error);
        candidateIndex = Mathf.Clamp(candidateIndex, 0, Mathf.Max(0, candidateIds.Length - 1));
        defectIndex = Mathf.Clamp(defectIndex, 0, Mathf.Max(0, defectIds.Length - 1));
        if (candidateIds.Length > 0) candidateIndex = EditorGUILayout.Popup("Draft rule", candidateIndex, candidateLabels);
        else EditorGUILayout.LabelField("Draft rule", "No promotable candidates");
        using (new EditorGUI.DisabledScope(candidateIds.Length == 0 || knowledgeError != null))
            if (GUILayout.Button("Promote selected rule (Nick gate)")) PromoteRule(candidateIds[candidateIndex]);
        if (defectIds.Length > 0) defectIndex = EditorGUILayout.Popup("Open defect", defectIndex, defectLabels);
        else EditorGUILayout.LabelField("Open defect", "None");
        using (new EditorGUI.DisabledScope(defectIds.Length == 0 || knowledgeError != null))
            if (GUILayout.Button("Mark selected defect resolved (Nick gate)")) ResolveDefect(defectIds[defectIndex]);
        if (GUILayout.Button("Open knowledge folder")) EditorUtility.RevealInFinder(GmSceneIntelligencePaths.KnowledgeRoot);

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(status, status.StartsWith("Error", StringComparison.Ordinal) ? MessageType.Error : MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    void Record(string verdict)
    {
        Run(() => {
            if (verdict != "keep" && string.IsNullOrWhiteSpace(defectTag))
                throw new InvalidOperationException("Change and Reject require a defect tag.");
            string prompt = $"Record this as Nick's {verdict.ToUpperInvariant()} verdict for {Categories[category]}?";
            if (!EditorUtility.DisplayDialog("Confirm Nick verdict", prompt, "Record", "Cancel")) return;
            status = GmReviewRecorder.RecordNickVerdict(Categories[category], verdict, defectTag, note);
            if (verdict != "keep") defectTag = "";
            note = "";
        });
    }

    void ArmTour()
    {
        GmSceneReviewTour tour = Object.FindAnyObjectByType<GmSceneReviewTour>();
        if (tour == null) throw new InvalidOperationException("No review tour exists in the open scene.");
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save or discard scene edits before capture.");
        SessionState.SetBool(GmSceneReviewTour.ArmKey, true);
        EditorPrefs.SetString(GmSceneReviewTour.ArmKey, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
        tour.runOnPlay = true;
        status = $"Armed {tour.ShotCount} canonical shots.";
        EditorApplication.isPlaying = true;
    }

    void PromoteRule(string candidateId)
    {
        Run(() => {
            if (!EditorUtility.DisplayDialog("Promote draft rule",
                $"Approve {candidateId} as a durable scene rule? This records an explicit Nick decision.",
                "Promote", "Cancel")) return;
            status = GmKnowledgeReviewActions.Promote(candidateId);
        });
    }

    void ResolveDefect(string defectId)
    {
        Run(() => {
            if (!EditorUtility.DisplayDialog("Resolve defect",
                $"Mark {defectId} resolved? Do this only after inspecting the repaired scene evidence.",
                "Resolve", "Cancel")) return;
            status = GmKnowledgeReviewActions.Resolve(defectId);
        });
    }

    void Run(Action action)
    {
        try { action(); if (string.IsNullOrWhiteSpace(status)) status = "Complete."; }
        catch (Exception error) { status = "Error: " + error.Message; Debug.LogException(error); }
        Repaint();
    }
}
