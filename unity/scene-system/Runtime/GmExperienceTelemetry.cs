// Development-only experience timeline. Shipping builds retain a no-op API, while editor and
// development builds can emit exact event gaps and player positions for pacing review.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public sealed class GmExperienceEvent
{
    public float seconds;
    public string eventName;
    public string detail;
    public Vector3 playerPosition;
}

[Serializable]
public sealed class GmExperienceTimeline
{
    public int schemaVersion = 1;
    public string sceneName;
    public string candidateProfileId;
    public float durationSeconds;
    public float maximumEventGapSeconds;
    public GmExperienceEvent[] events;
}

[DefaultExecutionOrder(-200)]
public sealed class GmExperienceTelemetry : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    static GmExperienceTelemetry current;
    readonly List<GmExperienceEvent> events = new List<GmExperienceEvent>();
    float startedAt;
    bool written;

    void Awake()
    {
        current = this;
        startedAt = Time.unscaledTime;
        RecordInternal("session-start", SceneManager.GetActiveScene().name);
    }

    void OnDestroy()
    {
        if (current == this) current = null;
    }

    void OnApplicationQuit() => WriteNow();
#endif

    public static void Record(string eventName, string detail = "")
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (current == null) current = FindAnyObjectByType<GmExperienceTelemetry>();
        current?.RecordInternal(eventName, detail);
#endif
    }

    public void WriteNow()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (written || events.Count == 0) return;
        written = true;
        float maximumGap = 0f;
        for (int i = 1; i < events.Count; i++)
            maximumGap = Mathf.Max(maximumGap, events[i].seconds - events[i - 1].seconds);
        string profile = $"wind:{ProfileId("GmAmbience")}|pacing:{ProfileId("GmBellSummons")}";
        var timeline = new GmExperienceTimeline {
            sceneName = SceneManager.GetActiveScene().name,
            candidateProfileId = profile,
            durationSeconds = Time.unscaledTime - startedAt,
            maximumEventGapSeconds = maximumGap,
            events = events.ToArray(),
        };
        string directory = OutputDirectory();
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory,
            $"timeline-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Math.Abs(profile.GetHashCode()):x8}.json");
        File.WriteAllText(file, JsonUtility.ToJson(timeline, true) + "\n");
        Debug.Log($"[GmExperienceTelemetry] wrote {events.Count} events gap={maximumGap:F1}s -> {file}");
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    void RecordInternal(string eventName, string detail)
    {
        if (string.IsNullOrWhiteSpace(eventName)) return;
        Camera camera = Camera.main;
        events.Add(new GmExperienceEvent {
            seconds = Time.unscaledTime - startedAt,
            eventName = eventName.Trim(),
            detail = (detail ?? "").Trim(),
            playerPosition = camera == null ? Vector3.zero : camera.transform.position,
        });
    }

    static string OutputDirectory()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-gmTelemetryDir");
        if (index >= 0 && index + 1 < args.Length && !string.IsNullOrWhiteSpace(args[index + 1]))
            return args[index + 1];
        return Path.Combine(Application.persistentDataPath, "GmSceneIntelligence", "pacing");
    }

    static string ProfileId(string componentTypeName)
    {
        foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,
            FindObjectsSortMode.None))
        {
            if (behaviour == null || behaviour.GetType().Name != componentTypeName) continue;
            var property = behaviour.GetType().GetProperty("ReviewProfileId");
            object value = property?.GetValue(behaviour);
            return value == null ? "unknown" : value.ToString();
        }
        return "unknown";
    }
#endif
}
