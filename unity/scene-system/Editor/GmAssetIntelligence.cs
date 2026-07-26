// Deterministic metadata index and contact sheets for imported Unity assets. Downloaded packages
// that are not imported cannot enter the index or adaptive candidates.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable]
public sealed class GmAssetProfile
{
    public string guid;
    public string path;
    public string assetType;
    public string pack;
    public string renderPipeline;
    public string dependencyHash;
    public Vector3 boundsSize;
    public Vector3 pivot;
    public Vector3 declaredScale;
    public int rendererCount;
    public int materialCount;
    public int colliderCount;
    public int lodGroupCount;
    public long triangleEstimate;
    public float flatness;
    public string visualSignature;
    public Color dominantColor;
    public float maximumSmoothness;
    public string era;
    public string condition;
    public string[] shaders;
    public string[] tags;
    public string[] tagEvidence;
    public float audioDuration;
    public int audioChannels;
    public int audioSampleRate;
    public float audioRms;
    public float audioPeak;
    public float audioSilenceShare;
    public float audioSpectralCentroidHz;
    public float audioSpectralFlatness;
    public float audioPersistentToneDb;
    public float audioStationarity;
    public float audioLoopDiscontinuity;
    public float audioLoopSimilarity;
    public float audioBoundaryJump;
    public float audioBoundarySlopeJump;
    public float audioSampleStepRms;
    public float audioBoundaryJumpRatio;
    public bool audioSpaceshipRisk;
    public bool audioIntentionallyTonal;
    public bool audioEnvironmentalRisk;
    public string loopSuitability;
    public bool priorAmbienceRejection;
}

[Serializable]
public sealed class GmAssetIndex
{
    public int schemaVersion = 3;
    public string unityVersion;
    public string contentHash;
    public GmAssetProfile[] assets;
}

[Serializable]
sealed class GmAssetTagOverride
{
    public string pathContains;
    public string era;
    public string condition;
    public string[] addTags;
}

[Serializable]
sealed class GmAssetTagOverrideDocument
{
    public int schemaVersion;
    public GmAssetTagOverride[] items;
}

public static class GmAssetIntelligence
{
    public const string OutputDirectory = "Library/GmSceneIntelligence";
    public const string IndexPath = OutputDirectory + "/asset-index.json";
    static readonly string[] Roles = { "Anchor", "Support", "Detail", "Boundary", "Ground", "RouteCue", "Audio" };

    [MenuItem("Games Master/Scene Intelligence/Reindex Imported Assets")]
    public static void ReindexMenu() => Reindex(true);

    public static GmAssetIndex Reindex(bool generateContactSheets = false)
    {
        Directory.CreateDirectory(OutputDirectory);
        string[] guids = AssetDatabase.FindAssets("t:GameObject t:Material t:AudioClip", new[] { "Assets" });
        var profiles = new List<GmAssetProfile>();
        foreach (string guid in guids.Distinct().OrderBy(value => value, StringComparer.Ordinal))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath) || Directory.Exists(assetPath)) continue;
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (!(asset is GameObject) && !(asset is Material) && !(asset is AudioClip)) continue;
            profiles.Add(Profile(guid, assetPath, asset));
        }
        profiles.Sort((a, b) => string.CompareOrdinal(a.path, b.path));
        var index = new GmAssetIndex {
            unityVersion = Application.unityVersion,
            assets = profiles.ToArray(),
        };
        index.contentHash = Hash128.Compute(JsonUtility.ToJson(index, false)).ToString();
        WriteAtomic(IndexPath, JsonUtility.ToJson(index, true) + "\n");
        if (generateContactSheets) GenerateContactSheets(index);
        Debug.Log($"[GmAssetIntelligence] PASS {index.assets.Length} imported assets hash={index.contentHash}");
        return index;
    }

    static GmAssetProfile Profile(string guid, string assetPath, UnityEngine.Object asset)
    {
        var result = new GmAssetProfile {
            guid = guid,
            path = assetPath,
            assetType = asset is AudioClip ? "AudioClip" : asset is Material ? "Material" :
                (assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ? "Prefab" : "Model"),
            pack = PackName(assetPath),
            dependencyHash = AssetDatabase.GetAssetDependencyHash(assetPath).ToString(),
            shaders = Array.Empty<string>(),
            tags = InferTags(assetPath),
            tagEvidence = new[] { "filename-hint" },
            declaredScale = Vector3.one,
            loopSuitability = "not-audio",
            era = "unspecified",
            condition = "unspecified",
        };
        if (asset is GameObject go) ProfileGameObject(result, go, assetPath);
        else if (asset is Material material)
        {
            result.materialCount = 1;
            result.shaders = new[] { material.shader == null ? "<missing>" : material.shader.name };
        }
        else if (asset is AudioClip clip) ProfileAudio(result, clip, assetPath);
        ApplyTagOverride(result);
        result.visualSignature = Hash128.Compute(string.Join("|", new[] {
            result.assetType, result.boundsSize.ToString("F3"), result.rendererCount.ToString(),
            result.materialCount.ToString(), result.triangleEstimate.ToString(),
            string.Join(",", result.shaders), result.maximumSmoothness.ToString("F3"),
            result.dominantColor.ToString(),
        })).ToString();
        result.renderPipeline = Pipeline(assetPath, result.shaders);
        return result;
    }

    static void ProfileGameObject(GmAssetProfile result, GameObject go, string assetPath)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        result.rendererCount = renderers.Length;
        result.colliderCount = go.GetComponentsInChildren<Collider>(true).Length;
        result.lodGroupCount = go.GetComponentsInChildren<LODGroup>(true).Length;
        var materials = new HashSet<Material>();
        var shaders = new HashSet<string>(StringComparer.Ordinal);
        var meshes = new HashSet<Mesh>();
        Color accumulatedColor = Color.black;
        int colorCount = 0;
        Bounds? combined = null;
        foreach (Renderer renderer in renderers)
        {
            foreach (Material material in renderer.sharedMaterials)
                if (material != null)
                {
                    materials.Add(material);
                    shaders.Add(material.shader == null ? "<missing>" : material.shader.name);
                    if (material.HasProperty("_BaseColor"))
                    { accumulatedColor += material.GetColor("_BaseColor"); colorCount++; }
                    else if (material.HasProperty("_Color"))
                    { accumulatedColor += material.GetColor("_Color"); colorCount++; }
                    if (material.HasProperty("_Smoothness"))
                        result.maximumSmoothness = Mathf.Max(result.maximumSmoothness, material.GetFloat("_Smoothness"));
                    else if (material.HasProperty("_Glossiness"))
                        result.maximumSmoothness = Mathf.Max(result.maximumSmoothness, material.GetFloat("_Glossiness"));
                }
            Mesh mesh = null;
            if (renderer is SkinnedMeshRenderer skinned) mesh = skinned.sharedMesh;
            else
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter != null) mesh = filter.sharedMesh;
            }
            if (mesh == null || !meshes.Add(mesh)) continue;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                result.triangleEstimate += (long)mesh.GetIndexCount(submesh) / 3L;
            Bounds rootBounds = TransformBounds(mesh.bounds, go.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix);
            if (!combined.HasValue) combined = rootBounds;
            else { Bounds value = combined.Value; value.Encapsulate(rootBounds); combined = value; }
        }
        result.materialCount = materials.Count;
        result.dominantColor = colorCount == 0 ? Color.clear : accumulatedColor / colorCount;
        result.shaders = shaders.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (combined.HasValue)
        {
            result.boundsSize = combined.Value.size;
            result.pivot = -combined.Value.center;
            float largest = Mathf.Max(result.boundsSize.x, result.boundsSize.y, result.boundsSize.z);
            float smallest = Mathf.Min(result.boundsSize.x, result.boundsSize.y, result.boundsSize.z);
            result.flatness = largest <= 0.0001f ? 0f : smallest / largest;
        }
        if (AssetImporter.GetAtPath(assetPath) is ModelImporter importer)
            result.declaredScale = Vector3.one * importer.globalScale;
    }

    static void ApplyTagOverride(GmAssetProfile profile)
    {
        string file;
        try { file = Path.Combine(GmSceneIntelligencePaths.FindRepoRoot(), "unity", "scene-system", "asset-tag-overrides.json"); }
        catch { return; }
        if (!File.Exists(file)) return;
        GmAssetTagOverrideDocument document;
        try { document = JsonUtility.FromJson<GmAssetTagOverrideDocument>(File.ReadAllText(file)); }
        catch (Exception error)
        {
            throw new InvalidDataException($"Asset tag override file is invalid: {error.Message}", error);
        }
        if (document == null || document.schemaVersion != 1 || document.items == null)
            throw new InvalidDataException("Asset tag override file must use schemaVersion 1 and an items array.");
        foreach (GmAssetTagOverride item in document.items)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.pathContains) ||
                profile.path.IndexOf(item.pathContains, StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (!string.IsNullOrWhiteSpace(item.era)) profile.era = item.era.Trim();
            if (!string.IsNullOrWhiteSpace(item.condition)) profile.condition = item.condition.Trim();
            var tags = new SortedSet<string>(profile.tags ?? Array.Empty<string>(), StringComparer.Ordinal);
            foreach (string tag in item.addTags ?? Array.Empty<string>())
                if (!string.IsNullOrWhiteSpace(tag)) tags.Add(tag.Trim());
            profile.tags = tags.ToArray();
            profile.tagEvidence = new[] { "filename-hint", $"manual:{item.pathContains}" };
        }
    }

    static void ProfileAudio(GmAssetProfile result, AudioClip clip, string assetPath)
    {
        result.audioDuration = clip.length;
        result.audioChannels = clip.channels;
        result.audioSampleRate = clip.frequency;
        GmAudioClipMetrics metrics = GmAudioAnalysis.Measure(clip);
        result.audioRms = metrics.rms;
        result.audioPeak = metrics.peak;
        result.audioSilenceShare = metrics.silenceShare;
        result.audioSpectralCentroidHz = metrics.spectralCentroidHz;
        result.audioSpectralFlatness = metrics.spectralFlatness;
        result.audioPersistentToneDb = metrics.persistentToneDb;
        result.audioStationarity = metrics.stationarity;
        result.audioLoopDiscontinuity = metrics.loopDiscontinuity;
        result.audioLoopSimilarity = metrics.loopSimilarity;
        result.audioBoundaryJump = metrics.boundaryJump;
        result.audioBoundarySlopeJump = metrics.boundarySlopeJump;
        result.audioSampleStepRms = metrics.sampleStepRms;
        result.audioBoundaryJumpRatio = metrics.boundaryJumpRatio;
        result.audioSpaceshipRisk = metrics.spaceshipRisk;
        string lower = assetPath.ToLowerInvariant();
        bool internalTone = lower.Contains("ear_whine") || lower.Contains("tinnitus") ||
            lower.Contains("internal_tone");
        result.audioIntentionallyTonal = internalTone;
        result.audioEnvironmentalRisk = metrics.spaceshipRisk && !internalTone;
        bool ambience = lower.Contains("amb") || lower.Contains("wind") || lower.Contains("drone") || lower.Contains("roomtone");
        bool boundarySafe = metrics.boundaryJump <= 0.04f || metrics.boundaryJumpRatio <= 3f;
        bool textureMatch = metrics.loopDiscontinuity <= 0.08f || metrics.loopSimilarity >= 0.72f;
        bool seamlessEnough = boundarySafe && textureMatch;
        result.loopSuitability = internalTone && clip.length >= 4f && boundarySafe &&
            metrics.boundarySlopeJump <= 0.12f ? "likely" :
            ambience && clip.length >= 8f && seamlessEnough && !metrics.spaceshipRisk ? "likely" :
            clip.length >= 2f && !metrics.spaceshipRisk ? "possible" : "unlikely";
        result.priorAmbienceRejection = lower.Contains("amb_dark") || lower.Contains("drone") || lower.Contains("spaceship");
    }

    static Bounds TransformBounds(Bounds source, Matrix4x4 matrix)
    {
        Vector3 center = matrix.MultiplyPoint3x4(source.center);
        Vector3 extents = source.extents;
        Vector3 axisX = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
        Vector3 axisY = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
        Vector3 axisZ = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));
        extents = new Vector3(
            Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
            Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
            Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));
        return new Bounds(center, extents * 2f);
    }

    static string PackName(string assetPath)
    {
        string[] parts = assetPath.Split('/');
        if (parts.Length >= 3 && parts[1] == "LeartesStudios") return parts[2];
        return parts.Length >= 2 ? parts[1] : "Project";
    }

    static string Pipeline(string assetPath, IReadOnlyList<string> shaders)
    {
        if (assetPath.IndexOf("/HDRP/", StringComparison.OrdinalIgnoreCase) >= 0 ||
            shaders.Any(shader => shader.IndexOf("HDRP", StringComparison.OrdinalIgnoreCase) >= 0)) return "HDRP";
        if (assetPath.IndexOf("/URP/", StringComparison.OrdinalIgnoreCase) >= 0 ||
            shaders.Any(shader => shader.IndexOf("Universal Render Pipeline", StringComparison.OrdinalIgnoreCase) >= 0)) return "URP";
        return "Unspecified";
    }

    static string[] InferTags(string assetPath)
    {
        string lower = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
        var tags = new SortedSet<string>(StringComparer.Ordinal);
        AddTag(tags, lower, "wall", "boundary");
        AddTag(tags, lower, "fence", "boundary");
        AddTag(tags, lower, "gate", "route-cue");
        AddTag(tags, lower, "path", "ground");
        AddTag(tags, lower, "road", "ground");
        AddTag(tags, lower, "ground", "ground");
        AddTag(tags, lower, "tree", "background");
        AddTag(tags, lower, "building", "anchor");
        AddTag(tags, lower, "house", "anchor");
        AddTag(tags, lower, "chapel", "anchor");
        AddTag(tags, lower, "door", "support");
        AddTag(tags, lower, "grave", "detail");
        AddTag(tags, lower, "prop", "detail");
        AddTag(tags, lower, "audio", "audio");
        AddTag(tags, lower, "amb", "audio");
        AddTag(tags, lower, "heartbeat", "internal-symptom");
        AddTag(tags, lower, "ear_whine", "internal-symptom");
        AddTag(tags, lower, "whisper", "story-audio");
        AddTag(tags, lower, "chime", "story-audio");
        if (tags.Count == 0) tags.Add("unclassified");
        return tags.ToArray();
    }

    static void AddTag(ISet<string> tags, string lower, string needle, string tag)
    {
        if (lower.Contains(needle)) tags.Add(tag);
    }

    static void GenerateContactSheets(GmAssetIndex index)
    {
        string directory = Path.Combine(OutputDirectory, "contact-sheets");
        Directory.CreateDirectory(directory);
        foreach (string role in Roles)
        {
            string tag = role.ToLowerInvariant();
            GmAssetProfile[] selected = index.assets.Where(asset => asset.tags.Contains(tag))
                .OrderBy(asset => asset.path, StringComparer.Ordinal).Take(12).ToArray();
            WriteAtomic(Path.Combine(directory, $"{role}.json"), JsonUtility.ToJson(new ContactSheetManifest {
                role = role,
                assetPaths = selected.Select(asset => asset.path).ToArray(),
            }, true) + "\n");
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                WriteContactSheetPng(Path.Combine(directory, $"{role}.png"), selected);
        }
    }

    static void WriteContactSheetPng(string file, IReadOnlyList<GmAssetProfile> selected)
    {
        const int columns = 4, rows = 3, tileWidth = 200, tileHeight = 150;
        var target = new RenderTexture(columns * tileWidth, rows * tileHeight, 0, RenderTextureFormat.ARGB32);
        var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            RenderTexture.active = target;
            GL.Clear(true, true, new Color(0.045f, 0.05f, 0.06f, 1f));
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, target.width, 0, target.height);
            for (int i = 0; i < selected.Count; i++)
            {
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(selected[i].path);
                Texture2D ownedPreview = asset is GameObject gameObject ? RenderPrefabPreview(gameObject,
                    tileWidth - 16, tileHeight - 16) : null;
                Texture preview = ownedPreview ?? AssetPreview.GetMiniThumbnail(asset);
                if (preview == null) continue;
                int column = i % columns;
                int row = rows - 1 - i / columns;
                var rect = new Rect(column * tileWidth + 8, row * tileHeight + 8,
                    tileWidth - 16, tileHeight - 16);
                Graphics.DrawTexture(rect, preview);
                if (ownedPreview != null) UnityEngine.Object.DestroyImmediate(ownedPreview);
            }
            GL.PopMatrix();
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(file, texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    static Texture2D RenderPrefabPreview(GameObject prefab, int width, int height)
    {
        var utility = new PreviewRenderUtility();
        RenderTexture copyTarget = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            GameObject instance = utility.InstantiatePrefabInScene(prefab);
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.enabled).ToArray();
            if (renderers.Length == 0) return null;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            float radius = Mathf.Max(0.1f, bounds.extents.magnitude);
            Vector3 direction = new Vector3(1f, 0.55f, -1f).normalized;
            utility.cameraFieldOfView = 32f;
            utility.camera.nearClipPlane = Mathf.Max(0.01f, radius * 0.02f);
            utility.camera.farClipPlane = radius * 12f + 10f;
            utility.camera.transform.position = bounds.center + direction * (radius * 3.2f + 0.4f);
            utility.camera.transform.LookAt(bounds.center);
            utility.camera.clearFlags = CameraClearFlags.SolidColor;
            utility.camera.backgroundColor = new Color(0.045f, 0.05f, 0.06f, 1f);
            utility.lights[0].intensity = 1.4f;
            utility.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            utility.lights[1].intensity = 0.65f;
            utility.lights[1].transform.rotation = Quaternion.Euler(340f, 210f, 0f);
            utility.ambientColor = new Color(0.28f, 0.30f, 0.34f);
            utility.BeginPreview(new Rect(0, 0, width, height), GUIStyle.none);
            utility.camera.Render();
            Texture rendered = utility.EndPreview();
            copyTarget = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(rendered, copyTarget);
            RenderTexture.active = copyTarget;
            var copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            copy.Apply();
            return copy;
        }
        catch (Exception error)
        {
            Debug.LogWarning($"[GmAssetIntelligence] preview failed for {prefab.name}: {error.Message}");
            return null;
        }
        finally
        {
            RenderTexture.active = previous;
            if (copyTarget != null) RenderTexture.ReleaseTemporary(copyTarget);
            utility.Cleanup();
        }
    }

    static void WriteAtomic(string target, string body)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        string temporary = target + "." + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ".tmp";
        File.WriteAllText(temporary, body);
        if (File.Exists(target)) File.Replace(temporary, target, null);
        else File.Move(temporary, target);
    }

    [Serializable]
    sealed class ContactSheetManifest
    {
        public string role;
        public string[] assetPaths;
    }
}
