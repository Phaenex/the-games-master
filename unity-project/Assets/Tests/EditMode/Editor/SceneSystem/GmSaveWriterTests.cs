using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public sealed class GmSaveWriterTests
{
    string directory;
    string path;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "gm-save-writer-" + Guid.NewGuid().ToString("N"));
        path = Path.Combine(directory, "save.json");
        GmRunStore.BeginNewRun();
    }

    [TearDown]
    public void TearDown()
    {
        GmSaveSystem.ResetTestConfiguration();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        GmRunStore.BeginNewRun();
    }

    [Test]
    public void RapidTransitionsCoalesceAndFlushWritesTheNewestGeneration()
    {
        var backend = new BlockingBackend();
        GmSaveSystem.ConfigureForTests(path, backend);

        Assert.That(GmSaveSystem.QueueSave(out long first), Is.True);
        Assert.That(backend.Started.WaitOne(2000), Is.True);
        GmRunStore.RaiseCorruption("second");
        Assert.That(GmSaveSystem.QueueSave(out long second), Is.True);
        GmRunStore.RaiseCorruption("third");
        Assert.That(GmSaveSystem.QueueSave(out long third), Is.True);
        backend.Release.Set();

        Assert.That(GmSaveSystem.Flush(), Is.True, GmSaveSystem.LastError);
        Assert.That(first, Is.LessThan(second));
        Assert.That(second, Is.LessThan(third));
        Assert.That(backend.Writes, Has.Count.EqualTo(2),
            "intermediate queued generations were not coalesced");
        StringAssert.Contains("\"corruptionTier\": 3", backend.Writes[1]);
        Assert.That(GmSaveSystem.DurableGeneration, Is.EqualTo(third));
    }

    [Test]
    public void FailedLatestGenerationSurfacesAndPriorAtomicFileRemainsValid()
    {
        var backend = new RecordingBackend();
        GmSaveSystem.ConfigureForTests(path, backend);
        Assert.That(GmSaveSystem.Save(), Is.True);
        string prior = backend.LastJson;

        backend.Fail = true;
        GmRunStore.RaiseCorruption("failure");
        Assert.That(GmSaveSystem.QueueSave(out _), Is.True);
        UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error,
            new System.Text.RegularExpressions.Regex("injected writer failure"));
        Assert.That(GmSaveSystem.Flush(), Is.False);
        StringAssert.Contains("injected", GmSaveSystem.LastError);
        Assert.That(backend.LastJson, Is.EqualTo(prior));

        backend.Fail = false;
        Assert.That(GmSaveSystem.QueueSave(out long recovered), Is.True);
        Assert.That(GmSaveSystem.Flush(), Is.True);
        Assert.That(GmSaveSystem.DurableGeneration, Is.EqualTo(recovered));
        StringAssert.Contains("\"corruptionTier\": 2", backend.LastJson);
    }

    [Test]
    public void FailedPreferenceWriterRetainsDirtyRetryWithoutTouchingRunWriter()
    {
        var runBackend = new RecordingBackend();
        var preferenceBackend = new RecordingBackend();
        string preferencePath = Path.Combine(directory, "preferences.json");
        MethodInfo configure = typeof(GmSaveSystem).GetMethods(BindingFlags.Public |
                BindingFlags.Static)
            .SingleOrDefault(method => method.Name == "ConfigureForTests" &&
                method.GetParameters().Length == 4);
        Assert.That(configure, Is.Not.Null,
            "tests cannot independently inject run and preference writers");
        configure.Invoke(null, new object[] { path, runBackend, preferencePath, preferenceBackend });
        GmRunStore.RecordCatch("advanced-run");
        Assert.That(GmSaveSystem.Save(), Is.True);
        string priorRun = runBackend.LastJson;

        preferenceBackend.Fail = true;
        GmAccessibilitySettings.SetCaptions(true);
        UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error,
            new System.Text.RegularExpressions.Regex("injected writer failure"));
        Assert.That(GmAccessibilitySettings.FlushPendingSave(), Is.False);
        Assert.That(GmAccessibilitySettings.HasPendingSave, Is.True);
        Assert.That(runBackend.LastJson, Is.EqualTo(priorRun),
            "failed preference write touched the durable run");

        preferenceBackend.Fail = false;
        Assert.That(GmAccessibilitySettings.FlushPendingSave(), Is.True);
        Assert.That(GmAccessibilitySettings.HasPendingSave, Is.False);
        StringAssert.Contains("\"captions\": true", preferenceBackend.LastJson);
        Assert.That(runBackend.LastJson, Is.EqualTo(priorRun));
    }

    sealed class BlockingBackend : IGmAtomicSaveBackend
    {
        public readonly ManualResetEvent Started = new ManualResetEvent(false);
        public readonly ManualResetEvent Release = new ManualResetEvent(false);
        public readonly List<string> Writes = new List<string>();

        public void WriteAtomic(string target, string json)
        {
            if (Writes.Count == 0)
            {
                Started.Set();
                Release.WaitOne(5000);
            }
            lock (Writes) Writes.Add(json);
        }
    }

    public sealed class RecordingBackend : IGmAtomicSaveBackend
    {
        public bool Fail;
        public string LastJson { get; private set; }

        public void WriteAtomic(string target, string json)
        {
            if (Fail) throw new IOException("injected writer failure");
            LastJson = json;
        }
    }
}
