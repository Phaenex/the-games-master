// Runtime host for the design data: POIs (two-layer examines), drive beats, and the walk-rect
// data (parsed manually — JsonUtility can't read nested arrays). Same contract as the web build.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

// Runs before every other Gm* system's Start() (all of which sit at Unity's default order, 0).
// GmColdOpen reads rt.coldOpen.Count synchronously in its own Start() to decide whether to disable
// the player -- without an explicit order, Unity does not guarantee this component's Start() (which
// does the file read + JsonUtility parse that populates coldOpen/pois/beats) runs first, and it was
// observed NOT to: GmColdOpen.Start() ran on an empty list and permanently no-opped every session.
[DefaultExecutionOrder(-100)]
public class GmDesignRuntime : MonoBehaviour
{
    [Serializable] public class Poi { public string id, verb, anchorId; public float x, z, radius; public string text, text2; [NonSerialized] public int seen; }
    [Serializable] public class Beat { public float z; public string anchorId, main, sub; [NonSerialized] public bool fired; }
    [Serializable] public class BranchBeat { public float[] rect; public string anchorId, main, sub; public float radius = 6f; [NonSerialized] public bool fired; }
    [Serializable] class PoiList { public Poi[] pois; public Beat[] beats; public string[] coldOpen; public BranchBeat[] branchBeats; }

    public List<Poi> pois = new List<Poi>();
    public List<Beat> beats = new List<Beat>();
    public List<string> coldOpen = new List<string>();
    public List<BranchBeat> branchBeats = new List<BranchBeat>();
    public List<float[]> walkRects = new List<float[]>();
    public string lastExamine = "";     // read by UI + tests
    public string activeBeatMain = "";
    public string activeBeatSub = "";
    float beatClearAt = -1f, examineClearAt = -1f;
    Transform player;
    readonly Dictionary<string, GmWorldAnchor> anchors = new Dictionary<string, GmWorldAnchor>(StringComparer.Ordinal);
    public int AnchorIssueCount { get; private set; }

    /// Which design document to load from StreamingAssets. Defaults to the estate's, so the existing
    /// Wend Hill scene is unaffected; the village scene sets this to "village-design.json", whose
    /// coordinates have been remapped from estate space into village world space. Additive on
    /// purpose -- this script is shared, and changing the default would silently repoint the estate
    /// scene at data authored for somewhere else.
    public string designFile = "prologue-design.json";

    void Start()
    {
        string path = Path.Combine(Application.streamingAssetsPath,
            string.IsNullOrWhiteSpace(designFile) ? "prologue-design.json" : designFile);
        if (!File.Exists(path))
        {
            Debug.LogError($"[GmDesignRuntime] FAILED: no design file at {path}");
            return;
        }
        string json = File.ReadAllText(path);
        var wrapped = JsonUtility.FromJson<PoiList>(json.Replace("\"walkRects\"", "\"_walkRectsRaw\""));
        if (wrapped != null)
        {
            if (wrapped.pois != null)
                foreach (var poi in wrapped.pois)
                {
                    poi.text = DecodeAuthoredText(poi.text);
                    poi.text2 = DecodeAuthoredText(poi.text2);
                    pois.Add(poi);
                }
            if (wrapped.beats != null)
                foreach (var b in wrapped.beats)
                {
                    b.main = DecodeAuthoredText(b.main);
                    b.sub = DecodeAuthoredText(b.sub);
                    beats.Add(b);
                }
            if (wrapped.coldOpen != null)
                foreach (string card in wrapped.coldOpen) coldOpen.Add(DecodeAuthoredText(card));
            if (wrapped.branchBeats != null)
                foreach (var beat in wrapped.branchBeats)
                {
                    beat.main = DecodeAuthoredText(beat.main);
                    beat.sub = DecodeAuthoredText(beat.sub);
                    branchBeats.Add(beat);
                }
        }
        // Scope the manual regex parse to the "walkRects" array's own text span. branchBeats[].rect
        // is also a bare 4-float array and would otherwise match too -- scanning the whole file gains
        // 7 phantom walk boxes the moment branchBeats exists. Bracket-match from the key's opening
        // '[' to its balanced close, then only regex inside that span.
        string walkRectsSpan = ExtractWalkRectsSpan(json);
        foreach (Match m in Regex.Matches(walkRectsSpan, "\\[\\s*(-?[\\d.]+),\\s*(-?[\\d.]+),\\s*(-?[\\d.]+),\\s*(-?[\\d.]+)\\s*\\]"))
        {
            var r = new float[4];
            for (int i = 0; i < 4; i++) r[i] = float.Parse(m.Groups[i + 1].Value, System.Globalization.CultureInfo.InvariantCulture);
            walkRects.Add(r);
        }
        var p = FindFirstObjectByType<GmPlayer>();
        if (p != null) player = p.transform;
        IndexAnchors();
        BindInteractables();
        Debug.Log($"[GmDesignRuntime] pois={pois.Count} beats={beats.Count} rects={walkRects.Count} coldOpen={coldOpen.Count} branchBeats={branchBeats.Count}");
    }

    void IndexAnchors()
    {
        anchors.Clear();
        AnchorIssueCount = 0;
        foreach (GmWorldAnchor anchor in FindObjectsByType<GmWorldAnchor>(FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (string.IsNullOrWhiteSpace(anchor.AnchorId) || !anchors.TryAdd(anchor.AnchorId, anchor))
            {
                AnchorIssueCount++;
                Debug.LogError($"[GmDesignRuntime] missing or duplicate world anchor '{anchor.AnchorId}'");
            }
        }
        foreach (Poi poi in pois) RequireAnchor(poi.anchorId, $"POI '{poi.id}'");
        foreach (Beat beat in beats) RequireAnchor(beat.anchorId, "main beat");
        foreach (BranchBeat beat in branchBeats) RequireAnchor(beat.anchorId, "branch beat");
    }

    void RequireAnchor(string id, string owner)
    {
        if (string.IsNullOrWhiteSpace(id)) return; // legacy coordinate document
        if (anchors.ContainsKey(id)) return;
        AnchorIssueCount++;
        Debug.LogError($"[GmDesignRuntime] {owner} references missing anchor '{id}'");
    }

    public static string DecodeAuthoredText(string input)
    {
        if (string.IsNullOrEmpty(input)) return input ?? "";
        string decoded = Regex.Replace(input, @"\\u([0-9a-fA-F]{4})", match =>
            ((char)System.Convert.ToInt32(match.Groups[1].Value, 16)).ToString());
        return decoded.Replace("\\n", "\n");
    }

    // Returns the substring of `json` spanning the "walkRects" key's array value, brackets included,
    // by counting bracket depth from the key's first '[' to its balanced match. Empty string if the
    // key is missing or malformed.
    static string ExtractWalkRectsSpan(string json)
    {
        int keyIdx = json.IndexOf("\"walkRects\"");
        if (keyIdx < 0) return "";
        int start = json.IndexOf('[', keyIdx);
        if (start < 0) return "";
        int depth = 0;
        for (int i = start; i < json.Length; i++)
        {
            if (json[i] == '[') depth++;
            else if (json[i] == ']')
            {
                depth--;
                if (depth == 0) return json.Substring(start, i - start + 1);
            }
        }
        return "";
    }

    void Update()
    {
        if (player == null) return;
        // Version-two designs fire from stable scene anchors. Legacy designs retain their Z fallback.
        foreach (var b in beats)
        {
            if (b.fired) continue;
            bool reached = !string.IsNullOrWhiteSpace(b.anchorId) && anchors.TryGetValue(b.anchorId, out GmWorldAnchor anchor)
                ? Vector2.Distance(new Vector2(player.position.x, player.position.z),
                    new Vector2(anchor.transform.position.x, anchor.transform.position.z)) <= 6f
                : player.position.z <= b.z;
            if (!reached) continue;
            b.fired = true;
            activeBeatMain = b.main; activeBeatSub = b.sub;
            beatClearAt = Time.time + BeatSecondsFor(b.main, b.sub);
            Debug.Log($"[Beat] {b.main}");
            GmExperienceTelemetry.Record("drive-beat", b.main);
            break;
        }
        // Branch anchors replace global rectangles in the canonical opening; rectangles remain only
        // for the archived coordinate document.
        var pos = player.position;
        foreach (var bb in branchBeats)
        {
            if (bb.fired) continue;
            bool reached = !string.IsNullOrWhiteSpace(bb.anchorId) && anchors.TryGetValue(bb.anchorId, out GmWorldAnchor anchor)
                ? Vector2.Distance(new Vector2(pos.x, pos.z),
                    new Vector2(anchor.transform.position.x, anchor.transform.position.z)) <= Mathf.Max(2f, bb.radius)
                : bb.rect != null && bb.rect.Length == 4 && pos.x >= bb.rect[0] && pos.x <= bb.rect[1] &&
                  pos.z >= bb.rect[2] && pos.z <= bb.rect[3];
            if (!reached) continue;
            bb.fired = true;
            GmExperienceTelemetry.Record("branch-enter", bb.main);
            ShowBeat(bb.main, bb.sub);
        }
        if (beatClearAt > 0 && Time.time > beatClearAt) { activeBeatMain = activeBeatSub = ""; beatClearAt = -1; }
        if (examineClearAt > 0 && Time.time > examineClearAt) { lastExamine = ""; examineClearAt = -1; }
    }

    /// How long a beat stays up, derived from how much there is to read rather than fixed.
    ///
    /// This was a hardcoded 4.2s against beats of 45-51 words, which is 640-730 words per minute.
    /// Comfortable adult silent reading is around 238 wpm and subtitle guidance sits at 160-180, so
    /// the premise of the game -- the debt, the daughter, Mara, the invitation -- was being shown
    /// roughly three times faster than a competent reader reads, once, in the dark, while walking,
    /// with no pause and no replay. Nothing in 332 EditMode tests has any concept of reading speed,
    /// so it went unnoticed until a panel measured it.
    ///
    /// ReadingWordsPerMinute is deliberately at the subtitle end rather than the silent-reading end:
    /// this text is read while moving, in low light, over ambience.
    public const float ReadingWordsPerMinute = 170f;
    public const float MinimumBeatSeconds = 4.2f;   // the old fixed value is now the floor
    public const float MaximumBeatSeconds = 14f;    // a long card should not strand the player

    public static float BeatSecondsFor(string main, string sub)
    {
        int words = CountWords(main) + CountWords(sub);
        float needed = words / ReadingWordsPerMinute * 60f;
        // A beat is glanced at, not studied -- the reader also has to notice it arrived.
        return Mathf.Clamp(needed + 0.9f, MinimumBeatSeconds, MaximumBeatSeconds);
    }

    static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return text.Split(new[] { ' ', '\n', '\t', '\r' }, System.StringSplitOptions.RemoveEmptyEntries).Length;
    }

    public void ShowBeat(string main, string sub)
    {
        activeBeatMain = main; activeBeatSub = sub;
        beatClearAt = Time.time + BeatSecondsFor(main, sub);
        Debug.Log($"[Beat] {main}");
        GmExperienceTelemetry.Record("story-beat", main);
    }

    string aftermathText = "";
    public string AftermathText => aftermathText;
    public void ShowAftermath(string text) { aftermathText = text; }

    // Automated captures exercise movement/story state before taking clean composition frames.
    // Normal play never calls this; the review harness must be able to clear the resulting card
    // without reaching into a private field or disabling the actual story systems under test.
    public void ClearAftermathForReview() { aftermathText = ""; }

    void BindInteractables()
    {
        var targets = FindObjectsByType<GmInteractable>(FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        var byId = new Dictionary<string, GmInteractable>(StringComparer.Ordinal);
        foreach (var target in targets)
        {
            if (string.IsNullOrWhiteSpace(target.InteractionId)) continue;
            if (!byId.TryAdd(target.InteractionId, target))
                Debug.LogError($"[GmDesignRuntime] duplicate interaction id '{target.InteractionId}'");
        }
        foreach (Poi poi in pois)
        {
            if (string.IsNullOrWhiteSpace(poi.id))
            {
                Debug.LogError("[GmDesignRuntime] POI is missing a stable id; coordinate interaction is retired");
                continue;
            }
            string targetId = string.IsNullOrWhiteSpace(poi.anchorId) ? poi.id : poi.anchorId;
            GmInteractable target = null;
            if (!string.IsNullOrWhiteSpace(targetId) && anchors.TryGetValue(targetId, out GmWorldAnchor anchor))
                target = anchor.GetComponent<GmInteractable>();
            if (target == null) byId.TryGetValue(poi.id, out target);
            if (target == null)
            {
                Debug.LogError($"[GmDesignRuntime] no visible interactable is bound to POI '{poi.id}'");
                continue;
            }
            // text2 is the discrepancy, and it now binds as a TELL rather than as a second Examine
            // line. Every one of these is shaped like a caught cheat -- coins all heads-down, one
            // mason's hand on stones a century apart, boots going to the shed and none coming back --
            // and handing them over for a second button press meant the narrator played the game's
            // core verb on the player's behalf for the whole opening. Examine now gives the
            // observation; the player has to call the tell to earn the catch.
            target.BindContent(poi.text, "");
            target.BindTell(poi.text2);
        }
    }

    public bool ShowExamine(string interactionId, string text)
    {
        if (string.IsNullOrWhiteSpace(interactionId) || string.IsNullOrWhiteSpace(text)) return false;
        lastExamine = text;
        examineClearAt = Time.time + 4.6f;
        Debug.Log($"[Examine:{interactionId}] {lastExamine}");
        GmExperienceTelemetry.Record("examine", interactionId);
        return true;
    }

}
