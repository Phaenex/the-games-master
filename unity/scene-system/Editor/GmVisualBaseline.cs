// Lightweight visual evidence and baseline gates. Approved frames can later feed Unity's Graphics
// Test Framework; review-status scenes stay in warning mode and cannot establish a false baseline.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[Serializable]
public sealed class GmFrameMetrics
{
    public string shot;
    public string perceptualHash;
    public float[] histogram;
    public float meanLuminance;
    public int p05;
    public int p95;
    public float nearBlackFraction;
    public float nearWhiteFraction;
    public float edgeDensity;
}

[Serializable]
public sealed class GmVisualBaselineRecord
{
    public string baselineId;
    public string sceneId;
    public string status;
    public string createdAt;
    public string transformFingerprint;
    public int zones;
    public int clusters;
    public int elements;
    public int routes;
    public int negativeSpaces;
    public GmFrameMetrics[] frames;
}

[Serializable]
sealed class GmVisualBaselineDocument
{
    public int schemaVersion = 1;
    public GmVisualBaselineRecord[] items = Array.Empty<GmVisualBaselineRecord>();
}

public sealed class GmBaselineEvaluation
{
    public readonly List<string> Warnings = new List<string>();
    public readonly List<string> Failures = new List<string>();
    public bool HasApprovedBaseline;
    public bool Pass => Failures.Count == 0;
}

public static class GmVisualBaseline
{
    public static GmVisualBaselineRecord MeasureOpenScene()
    {
        GmSceneComposition manifest = Object.FindAnyObjectByType<GmSceneComposition>();
        if (manifest == null) throw new InvalidOperationException("Open scene has no composition manifest.");
        string screenDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Screens",
            SceneManager.GetActiveScene().name);
        if (!Directory.Exists(screenDirectory)) throw new DirectoryNotFoundException($"No captures at {screenDirectory}");
        string[] files = Directory.GetFiles(screenDirectory, "*.png").OrderBy(file => file, StringComparer.Ordinal).ToArray();
        if (files.Length == 0) throw new InvalidOperationException("No PNG captures exist for the open scene.");
        return new GmVisualBaselineRecord {
            baselineId = $"{manifest.SceneId}-{DateTime.UtcNow:yyyyMMddHHmmss}",
            sceneId = manifest.SceneId,
            status = "in-progress",
            createdAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
            transformFingerprint = GmSceneFingerprint.Current(),
            zones = Object.FindObjectsByType<GmCompositionZone>(FindObjectsInactive.Include).Length,
            clusters = Object.FindObjectsByType<GmCompositionCluster>(FindObjectsInactive.Include).Length,
            elements = Object.FindObjectsByType<GmCompositionElement>(FindObjectsInactive.Include).Length,
            routes = Object.FindObjectsByType<GmRouteReservation>(FindObjectsInactive.Include).Length,
            negativeSpaces = Object.FindObjectsByType<GmNegativeSpace>(FindObjectsInactive.Include).Length,
            frames = files.Select(MeasureFrame).ToArray(),
        };
    }

    public static GmBaselineEvaluation Evaluate(GmVisualBaselineRecord current)
    {
        GmVisualBaselineRecord approved = LoadDocument().items.LastOrDefault(item =>
            item.sceneId == current.sceneId && item.status == "approved");
        var result = new GmBaselineEvaluation { HasApprovedBaseline = approved != null };
        if (approved == null)
        {
            result.Warnings.Add("No approved baseline. Visual differences warn but do not fail until Nick approves a review-ready scene.");
            return result;
        }
        if (approved.transformFingerprint != current.transformFingerprint)
            result.Failures.Add($"transform fingerprint changed: {approved.transformFingerprint} -> {current.transformFingerprint}");
        var currentFrames = current.frames.ToDictionary(frame => frame.shot, StringComparer.Ordinal);
        foreach (GmFrameMetrics expected in approved.frames)
        {
            if (!currentFrames.TryGetValue(expected.shot, out GmFrameMetrics actual))
            {
                result.Failures.Add($"missing approved shot '{expected.shot}'");
                continue;
            }
            int hashDistance = Hamming(expected.perceptualHash, actual.perceptualHash);
            float histogramDelta = HistogramDelta(expected.histogram, actual.histogram);
            float edgeDelta = Mathf.Abs(expected.edgeDensity - actual.edgeDensity) / Mathf.Max(0.01f, expected.edgeDensity);
            if (hashDistance > 12) result.Failures.Add($"shot '{expected.shot}' pHash distance {hashDistance} > 12");
            if (histogramDelta > 0.25f) result.Failures.Add($"shot '{expected.shot}' histogram delta {histogramDelta:F2} > 0.25");
            if (Mathf.Abs(expected.meanLuminance - actual.meanLuminance) > 15f)
                result.Failures.Add($"shot '{expected.shot}' mean luminance drift exceeds 15");
            if (edgeDelta > 0.25f) result.Failures.Add($"shot '{expected.shot}' edge density drift {edgeDelta:P0} > 25%");
            if (actual.nearBlackFraction > 0.97f || actual.nearWhiteFraction > 0.25f)
                result.Failures.Add($"shot '{expected.shot}' is clipped or blank");
        }
        return result;
    }

    public static List<string> ApprovalBlockers(string sceneId)
    {
        var blockers = new List<string>();
        string repo = GmSceneIntelligencePaths.FindRepoRoot();
        string registry = File.ReadAllText(Path.Combine(repo, "unity", "scene-system", "scene-registry.json"));
        int sceneAt = registry.IndexOf("\"id\": \"" + sceneId + "\"", StringComparison.Ordinal);
        string sceneBlock = sceneAt < 0 ? "" : registry.Substring(sceneAt, Math.Min(1800, registry.Length - sceneAt));
        if (sceneAt < 0) blockers.Add("scene is absent from the registry");
        else if (sceneBlock.Contains("\"status\": \"review\"")) blockers.Add("scene registry status is still review");
        string defects = File.ReadAllText(Path.Combine(GmSceneIntelligencePaths.KnowledgeRoot, "derived", "defects.json"));
        foreach (string block in defects.Split(new[] { "\"defectId\"" }, StringSplitOptions.None))
            if (block.Contains("\"sceneId\": \"" + sceneId + "\"") && block.Contains("\"severity\": \"high\"") &&
                block.Contains("\"status\": \"open\"")) { blockers.Add("one or more high-severity defects remain open"); break; }
        string sessions = Path.Combine(GmSceneIntelligencePaths.KnowledgeRoot, "review-sessions");
        bool nickKeep = Directory.Exists(sessions) && Directory.GetFiles(sessions, "*.json").Any(file => {
            string json = File.ReadAllText(file);
            return json.Contains("\"kind\": \"nick\"") && json.Contains("\"sceneId\": \"" + sceneId + "\"") &&
                json.Contains("\"verdict\": \"keep\"");
        });
        if (!nickKeep) blockers.Add("no explicit Nick Keep verdict has been recorded");
        return blockers;
    }

    public static void ApproveCurrent()
    {
        GmVisualBaselineRecord current = MeasureOpenScene();
        List<string> blockers = ApprovalBlockers(current.sceneId);
        if (blockers.Count > 0) throw new InvalidOperationException("Baseline approval blocked: " + string.Join("; ", blockers));
        current.status = "approved";
        GmVisualBaselineDocument document = LoadDocument();
        var items = document.items.ToList();
        items.Add(current);
        document.items = items.OrderBy(item => item.createdAt, StringComparer.Ordinal).ToArray();
        WriteAtomic(BaselinePath(), JsonUtility.ToJson(document, true) + "\n");
    }

    public static bool CanRunGraphicsRegression(string sceneId) => LoadDocument().items.Any(item =>
        item.sceneId == sceneId && item.status == "approved");

    static GmFrameMetrics MeasureFrame(string file)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!texture.LoadImage(File.ReadAllBytes(file), false)) throw new InvalidOperationException($"Cannot decode {file}");
            Color32[] pixels = texture.GetPixels32();
            var luma = new byte[pixels.Length];
            var histogram = new float[16];
            long sum = 0;
            int nearBlack = 0, nearWhite = 0, edges = 0, comparisons = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                byte value = (byte)((pixels[i].r * 54 + pixels[i].g * 183 + pixels[i].b * 19) >> 8);
                luma[i] = value;
                histogram[Mathf.Min(15, value / 16)]++;
                sum += value;
                if (value <= 2) nearBlack++;
                if (value >= 253) nearWhite++;
                int x = i % texture.width, y = i / texture.width;
                if (x > 0) { if (Mathf.Abs(value - luma[i - 1]) >= 24) edges++; comparisons++; }
                if (y > 0) { if (Mathf.Abs(value - luma[i - texture.width]) >= 24) edges++; comparisons++; }
            }
            byte[] sorted = (byte[])luma.Clone(); Array.Sort(sorted);
            for (int i = 0; i < histogram.Length; i++) histogram[i] /= pixels.Length;
            return new GmFrameMetrics {
                shot = Path.GetFileNameWithoutExtension(file),
                perceptualHash = PerceptualHash(luma, texture.width, texture.height),
                histogram = histogram,
                meanLuminance = sum / (float)pixels.Length,
                p05 = sorted[Mathf.Clamp(Mathf.FloorToInt(sorted.Length * 0.05f), 0, sorted.Length - 1)],
                p95 = sorted[Mathf.Clamp(Mathf.FloorToInt(sorted.Length * 0.95f), 0, sorted.Length - 1)],
                nearBlackFraction = nearBlack / (float)pixels.Length,
                nearWhiteFraction = nearWhite / (float)pixels.Length,
                edgeDensity = comparisons == 0 ? 0f : edges / (float)comparisons,
            };
        }
        finally { Object.DestroyImmediate(texture); }
    }

    static string PerceptualHash(byte[] luma, int width, int height)
    {
        var values = new float[64];
        float sum = 0f;
        for (int cellY = 0; cellY < 8; cellY++)
            for (int cellX = 0; cellX < 8; cellX++)
            {
                int x = Mathf.Clamp(Mathf.FloorToInt((cellX + 0.5f) * width / 8f), 0, width - 1);
                int y = Mathf.Clamp(Mathf.FloorToInt((cellY + 0.5f) * height / 8f), 0, height - 1);
                float value = luma[y * width + x];
                values[cellY * 8 + cellX] = value;
                sum += value;
            }
        float average = sum / 64f;
        ulong bits = 0;
        for (int i = 0; i < 64; i++) if (values[i] >= average) bits |= 1UL << i;
        return bits.ToString("x16", CultureInfo.InvariantCulture);
    }

    static int Hamming(string a, string b)
    {
        if (!ulong.TryParse(a, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong left) ||
            !ulong.TryParse(b, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong right)) return 64;
        ulong value = left ^ right;
        int count = 0;
        while (value != 0) { value &= value - 1; count++; }
        return count;
    }

    static float HistogramDelta(IReadOnlyList<float> a, IReadOnlyList<float> b)
    {
        if (a == null || b == null || a.Count != b.Count) return 1f;
        float result = 0f;
        for (int i = 0; i < a.Count; i++) result += Mathf.Abs(a[i] - b[i]);
        return result * 0.5f;
    }

    static string BaselinePath() => Path.Combine(GmSceneIntelligencePaths.KnowledgeRoot, "baselines.json");

    static GmVisualBaselineDocument LoadDocument()
    {
        string file = BaselinePath();
        if (!File.Exists(file)) return new GmVisualBaselineDocument();
        GmVisualBaselineDocument value = JsonUtility.FromJson<GmVisualBaselineDocument>(File.ReadAllText(file));
        return value ?? new GmVisualBaselineDocument();
    }

    static void WriteAtomic(string target, string body)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temporary, body);
        if (File.Exists(target)) File.Replace(temporary, target, null);
        else File.Move(temporary, target);
    }
}
