using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// Converts a built-player .raw profiler capture into a reviewable JSON report. Frame pacing alone
/// can say that a hitch exists; this report names the main-thread markers that own the time.
/// </summary>
public static class GmWendProfilerReport
{
    const int TailFrameCount = 1800;
    const float SlowFrameMilliseconds = 16.7f;

    [Serializable]
    sealed class MarkerRow
    {
        public string marker;
        public float meanSelfMilliseconds;
        public float meanTotalMilliseconds;
        public float slowFrameSelfMilliseconds;
        public float slowFrameTotalMilliseconds;
        public int frameOccurrences;
        public int calls;
    }

    [Serializable]
    sealed class ProfileReport
    {
        public int schemaVersion = 1;
        public string source;
        public int firstCapturedFrame;
        public int lastCapturedFrame;
        public int analyzedFrames;
        public int slowFrames;
        public float meanMainThreadMilliseconds;
        public float p50MainThreadMilliseconds;
        public float p95MainThreadMilliseconds;
        public float p99MainThreadMilliseconds;
        public float meanGpuMilliseconds;
        public float p95GpuMilliseconds;
        public MarkerRow[] topSelfMarkers;
        public MarkerRow[] topTotalMarkers;
    }

    sealed class MarkerAggregate
    {
        public double Self;
        public double Total;
        public double SlowSelf;
        public double SlowTotal;
        public int Frames;
        public int Calls;
    }

    public static void WriteFromCommandLine()
    {
        try
        {
            string input = RequiredArgument("-gmProfileInput");
            string output = RequiredArgument("-gmProfileReport");
            Write(input, output);
            Debug.Log($"[GmWendProfilerReport] PASS: {output}");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    static void Write(string input, string output)
    {
        if (!File.Exists(input)) throw new FileNotFoundException("profile capture is missing", input);
        ProfilerDriver.ClearAllFrames();
        if (!ProfilerDriver.LoadProfile(input, false))
            throw new InvalidOperationException($"Unity could not load profiler capture: {input}");

        int first = ProfilerDriver.firstFrameIndex;
        int last = ProfilerDriver.lastFrameIndex;
        if (first < 0 || last < first)
            throw new InvalidOperationException($"profile capture contains no frames: first={first}, last={last}");

        var available = new List<int>();
        for (int frame = first; frame >= 0 && frame <= last; frame = ProfilerDriver.GetNextFrameIndex(frame))
        {
            available.Add(frame);
            if (frame == last) break;
            int next = ProfilerDriver.GetNextFrameIndex(frame);
            if (next <= frame) break;
        }
        int skip = Mathf.Max(0, available.Count - TailFrameCount);
        int[] frames = available.Skip(skip).ToArray();
        if (frames.Length < 300)
            throw new InvalidOperationException($"profile capture has only {frames.Length} analyzable frame(s)");

        var mainTimes = new List<float>(frames.Length);
        var gpuTimes = new List<float>(frames.Length);
        var markers = new Dictionary<string, MarkerAggregate>(StringComparer.Ordinal);
        int slowFrames = 0;
        foreach (int frame in frames)
        {
            float frameTime;
            using (RawFrameDataView raw = ProfilerDriver.GetRawFrameDataView(frame, 0))
            {
                if (!raw.valid) continue;
                frameTime = raw.frameTimeMs;
                mainTimes.Add(frameTime);
                if (raw.frameGpuTimeMs > 0f) gpuTimes.Add(raw.frameGpuTimeMs);
            }
            bool slow = frameTime > SlowFrameMilliseconds;
            if (slow) slowFrames++;

            using (HierarchyFrameDataView hierarchy = ProfilerDriver.GetHierarchyFrameDataView(
                       frame, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                       HierarchyFrameDataView.columnDontSort, false))
            {
                if (!hierarchy.valid) continue;
                var items = new List<int>();
                CollectItems(hierarchy, hierarchy.GetRootItemID(), items);
                foreach (int item in items)
                {
                    string name = hierarchy.GetItemName(item);
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    if (!markers.TryGetValue(name, out MarkerAggregate aggregate))
                    {
                        aggregate = new MarkerAggregate();
                        markers.Add(name, aggregate);
                    }
                    float self = hierarchy.GetItemColumnDataAsFloat(item,
                        HierarchyFrameDataView.columnSelfTime);
                    float total = hierarchy.GetItemColumnDataAsFloat(item,
                        HierarchyFrameDataView.columnTotalTime);
                    aggregate.Self += self;
                    aggregate.Total += total;
                    aggregate.Frames++;
                    if (int.TryParse(hierarchy.GetItemColumnData(item,
                            HierarchyFrameDataView.columnCalls), out int calls)) aggregate.Calls += calls;
                    if (slow)
                    {
                        aggregate.SlowSelf += self;
                        aggregate.SlowTotal += total;
                    }
                }
            }
        }
        if (mainTimes.Count < 300)
            throw new InvalidOperationException($"only {mainTimes.Count} main-thread frame(s) were readable");

        MarkerRow Row(KeyValuePair<string, MarkerAggregate> pair) => new MarkerRow
        {
            marker = pair.Key,
            meanSelfMilliseconds = (float)(pair.Value.Self / mainTimes.Count),
            meanTotalMilliseconds = (float)(pair.Value.Total / mainTimes.Count),
            slowFrameSelfMilliseconds = slowFrames == 0 ? 0f :
                (float)(pair.Value.SlowSelf / slowFrames),
            slowFrameTotalMilliseconds = slowFrames == 0 ? 0f :
                (float)(pair.Value.SlowTotal / slowFrames),
            frameOccurrences = pair.Value.Frames,
            calls = pair.Value.Calls,
        };

        var report = new ProfileReport
        {
            source = Path.GetFullPath(input),
            firstCapturedFrame = frames[0],
            lastCapturedFrame = frames[frames.Length - 1],
            analyzedFrames = mainTimes.Count,
            slowFrames = slowFrames,
            meanMainThreadMilliseconds = mainTimes.Average(),
            p50MainThreadMilliseconds = Percentile(mainTimes, 0.50f),
            p95MainThreadMilliseconds = Percentile(mainTimes, 0.95f),
            p99MainThreadMilliseconds = Percentile(mainTimes, 0.99f),
            meanGpuMilliseconds = gpuTimes.Count == 0 ? 0f : gpuTimes.Average(),
            p95GpuMilliseconds = gpuTimes.Count == 0 ? 0f : Percentile(gpuTimes, 0.95f),
            topSelfMarkers = markers.OrderByDescending(pair => pair.Value.SlowSelf)
                .ThenByDescending(pair => pair.Value.Self).Take(60).Select(Row).ToArray(),
            topTotalMarkers = markers.OrderByDescending(pair => pair.Value.SlowTotal)
                .ThenByDescending(pair => pair.Value.Total).Take(60).Select(Row).ToArray(),
        };
        string directory = Path.GetDirectoryName(output);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(output, JsonUtility.ToJson(report, true));
    }

    static void CollectItems(HierarchyFrameDataView view, int parent, List<int> output)
    {
        var children = new List<int>();
        view.GetItemChildren(parent, children);
        foreach (int child in children)
        {
            output.Add(child);
            CollectItems(view, child, output);
        }
    }

    static float Percentile(List<float> values, float percentile)
    {
        float[] sorted = values.ToArray();
        Array.Sort(sorted);
        int index = Mathf.Clamp(Mathf.CeilToInt((sorted.Length - 1) * percentile),
            0, sorted.Length - 1);
        return sorted[index];
    }

    static string RequiredArgument(string flag)
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, flag);
        if (index < 0 || index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
            throw new ArgumentException($"required command-line argument missing: {flag}");
        return Path.GetFullPath(args[index + 1]);
    }
}
