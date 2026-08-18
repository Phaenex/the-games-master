using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class GmAccessibilitySettingsTests
{
    Type settingsType;
    string directory;
    string savePath;

    [SetUp]
    public void SetUp()
    {
        settingsType = typeof(GmRunStore).Assembly.GetType("GmAccessibilitySettings");
        directory = Path.Combine(Path.GetTempPath(),
            "gm-accessibility-" + Guid.NewGuid().ToString("N"));
        savePath = Path.Combine(directory, "save.json");
        GmSaveSystem.ConfigureForTests(savePath);
        GmRunStore.BeginNewRun();
        Invoke("ResetToDefaultsForTests");
    }

    [TearDown]
    public void TearDown()
    {
        Invoke("ResetToDefaultsForTests");
        GmRunStore.BeginNewRun();
        GmSaveSystem.ResetTestConfiguration();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    [Test]
    public void AuthorityHasBackwardCompatibleDefaultsAndRaisesOneMutationEvent()
    {
        Assert.That(settingsType, Is.Not.Null,
            "there is no single global accessibility authority");
        Assert.That(Read<bool>("Captions"), Is.False);
        Assert.That(Read<bool>("ReducedMotion"), Is.False);
        Assert.That(Read<bool>("Vibration"), Is.True,
            "vibration must default on, including migrated old saves");
        Assert.That(Read<bool>("MonoAudio"), Is.False);
        Assert.That(Read<bool>("HighContrast"), Is.False);
        Assert.That(Read<float>("TextScale"), Is.EqualTo(1f).Within(0.001f));

        EventInfo changedEvent = settingsType.GetEvent("OnChanged",
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(changedEvent, Is.Not.Null);
        int changes = 0;
        Action changed = () => changes++;
        changedEvent.AddEventHandler(null, changed);
        try
        {
            Invoke("SetCaptions", true);
            Invoke("SetCaptions", true);
            Assert.That(changes, Is.EqualTo(1),
                "one real mutation should raise one event and an idempotent set should raise none");
        }
        finally
        {
            changedEvent.RemoveEventHandler(null, changed);
        }
    }

    [Test]
    public void OldSaveMigrationRestoresAllDefaultsIncludingVibrationOn()
    {
        const string oldJson = "{\"corruptionTier\":3,\"sanity\":0.75}";
        Invoke("SetCaptions", true);
        Invoke("SetVibration", false);
        Invoke("SetTextScale", 1.8f);

        Assert.That(InvokeWithResult("TryLoadFrom",
            JsonUtility.FromJson<GmSaveData>(oldJson)), Is.EqualTo(true));

        Assert.That(Read<bool>("Captions"), Is.False);
        Assert.That(Read<bool>("ReducedMotion"), Is.False);
        Assert.That(Read<bool>("Vibration"), Is.True);
        Assert.That(Read<bool>("MonoAudio"), Is.False);
        Assert.That(Read<bool>("HighContrast"), Is.False);
        Assert.That(Read<float>("TextScale"), Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void NewRunPreservesAccessibilityAndDoesNotChangeGameplayState()
    {
        Invoke("SetCaptions", true);
        Invoke("SetReducedMotion", true);
        Invoke("SetVibration", false);
        Invoke("SetMonoAudio", true);
        Invoke("SetHighContrast", true);
        Invoke("SetTextScale", 2f);
        GmSaveData before = GmRunStore.ToSaveData();

        GmRunStore.BeginNewRun();
        GmSaveData after = GmRunStore.ToSaveData();

        Assert.That(Read<bool>("Captions"), Is.True);
        Assert.That(Read<bool>("ReducedMotion"), Is.True);
        Assert.That(Read<bool>("Vibration"), Is.False);
        Assert.That(Read<bool>("MonoAudio"), Is.True);
        Assert.That(Read<bool>("HighContrast"), Is.True);
        Assert.That(Read<float>("TextScale"), Is.EqualTo(2f).Within(0.001f));
        Assert.That(after.corruptionTier, Is.EqualTo(GmRunStore.MinCorruptionTier));
        Assert.That(after.sanity, Is.EqualTo(1f));
        Assert.That(after.cheatsCaught, Is.Empty);
        Assert.That(before.corruptionTier, Is.EqualTo(after.corruptionTier),
            "accessibility mutation leaked into House Memory/gameplay adaptation state");
    }

    [Test]
    public void AccessibilityMutationsDoNotEnterHouseMemoryOrRaiseRunStateEvents()
    {
        GmRunStore.RaiseCorruption("sentinel");
        GmRunStore.RecordCatch("sentinel-catch");
        GmRunStore.RecordClue("sentinel-clue");
        GmSaveData before = GmRunStore.ToSaveData();
        int runEvents = 0;
        Action changed = () => runEvents++;
        GmRunStore.OnStateChanged += changed;
        try
        {
            Invoke("SetCaptions", true);
            Invoke("SetReducedMotion", true);
            Invoke("SetVibration", false);
            Invoke("SetMonoAudio", true);
            Invoke("SetHighContrast", true);
            Invoke("SetTextScale", 2f);
        }
        finally
        {
            GmRunStore.OnStateChanged -= changed;
        }
        GmSaveData after = GmRunStore.ToSaveData();

        Assert.That(runEvents, Is.Zero);
        Assert.That(after.corruptionTier, Is.EqualTo(before.corruptionTier));
        Assert.That(after.sanity, Is.EqualTo(before.sanity));
        Assert.That(after.defiance, Is.EqualTo(before.defiance));
        Assert.That(after.compliance, Is.EqualTo(before.compliance));
        CollectionAssert.AreEquivalent(before.cheatsCaught, after.cheatsCaught);
        CollectionAssert.AreEquivalent(before.discoveredClues, after.discoveredClues);
        CollectionAssert.AreEquivalent(before.completedRooms, after.completedRooms);
        CollectionAssert.AreEquivalent(before.completedTableGames, after.completedTableGames);
        CollectionAssert.AreEqual(before.mirrorShards, after.mirrorShards);
    }

    [Test]
    public void QueueFlushAndDiskLoadRoundTripAllSixSettings()
    {
        Invoke("SetCaptions", true);
        Invoke("SetReducedMotion", true);
        Invoke("SetVibration", false);
        Invoke("SetMonoAudio", true);
        Invoke("SetHighContrast", true);
        Invoke("SetTextScale", 1.8f);

        Assert.That((bool)InvokeWithResult("FlushPendingSave"), Is.True);
        Invoke("ResetToDefaultsForTests");
        Assert.That(GmSaveSystem.TryLoadAccessibilityPreferences(), Is.True);

        Assert.That(Read<bool>("Captions"), Is.True);
        Assert.That(Read<bool>("ReducedMotion"), Is.True);
        Assert.That(Read<bool>("Vibration"), Is.False);
        Assert.That(Read<bool>("MonoAudio"), Is.True);
        Assert.That(Read<bool>("HighContrast"), Is.True);
        Assert.That(Read<float>("TextScale"), Is.EqualTo(1.8f).Within(0.001f));
    }

    [Test]
    public void InvalidAndNonFiniteTextScaleFailSafeDeterministically()
    {
        Invoke("SetTextScale", float.NaN);
        Assert.That(Read<float>("TextScale"), Is.EqualTo(1f).Within(0.001f));
        Invoke("SetTextScale", float.PositiveInfinity);
        Assert.That(Read<float>("TextScale"), Is.EqualTo(1f).Within(0.001f));
        Invoke("SetTextScale", -20f);
        Assert.That(Read<float>("TextScale"), Is.EqualTo(0.8f).Within(0.001f));
        Invoke("SetTextScale", 20f);
        Assert.That(Read<float>("TextScale"), Is.EqualTo(2f).Within(0.001f));
    }

    [Test]
    public void LoadedNonFiniteScaleFallsBackWithoutDiscardingOtherCurrentSettings()
    {
        var data = new GmSaveData
        {
            accessibilitySettingsVersion = 1,
            accessibilityCaptions = true,
            accessibilityVibration = false,
            accessibilityTextScale = float.NaN,
        };

        Assert.That(InvokeWithResult("TryLoadFrom", data), Is.EqualTo(true));

        Assert.That(Read<float>("TextScale"), Is.EqualTo(1f).Within(0.001f));
        Assert.That(Read<bool>("Captions"), Is.True);
        Assert.That(Read<bool>("Vibration"), Is.False);
    }

    [TestCase(-1)]
    [TestCase(2)]
    [TestCase(99)]
    public void UnknownAccessibilityVersionsFailWithoutMutatingLivePreferences(int version)
    {
        Invoke("SetCaptions", true);
        Invoke("SetReducedMotion", true);
        Invoke("SetVibration", false);
        Invoke("SetMonoAudio", true);
        Invoke("SetHighContrast", true);
        Invoke("SetTextScale", 1.7f);
        var unknown = new GmSaveData
        {
            accessibilitySettingsVersion = version,
            accessibilityCaptions = false,
            accessibilityReducedMotion = false,
            accessibilityVibration = true,
            accessibilityMonoAudio = false,
            accessibilityHighContrast = false,
            accessibilityTextScale = 1f,
        };

        object result = InvokeWithResult("TryLoadFrom", unknown);

        Assert.That(result, Is.EqualTo(false), "unknown versions must report a deterministic failure");
        Assert.That(Read<bool>("Captions"), Is.True);
        Assert.That(Read<bool>("ReducedMotion"), Is.True);
        Assert.That(Read<bool>("Vibration"), Is.False);
        Assert.That(Read<bool>("MonoAudio"), Is.True);
        Assert.That(Read<bool>("HighContrast"), Is.True);
        Assert.That(Read<float>("TextScale"), Is.EqualTo(1.7f).Within(0.001f));
    }

    [Test]
    public void V0MigrationIsExplicitAndRestoresBackwardCompatibleDefaults()
    {
        Invoke("SetCaptions", true);
        Invoke("SetVibration", false);
        Invoke("SetTextScale", 2f);
        var v0 = new GmSaveData { accessibilitySettingsVersion = 0 };

        object result = InvokeWithResult("TryLoadFrom", v0);

        Assert.That(result, Is.EqualTo(true));
        Assert.That(Read<bool>("Captions"), Is.False);
        Assert.That(Read<bool>("Vibration"), Is.True);
        Assert.That(Read<float>("TextScale"), Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void StandalonePreferenceV0MigrationRestoresBackwardCompatibleDefaults()
    {
        Invoke("SetCaptions", true);
        Invoke("SetReducedMotion", true);
        Invoke("SetVibration", false);
        Invoke("SetMonoAudio", true);
        Invoke("SetHighContrast", true);
        Invoke("SetTextScale", 2f);
        Assert.That((bool)InvokeWithResult("FlushPendingSave"), Is.True,
            "fixture must begin from a clean durable generation before replacing the file with v0");
        Directory.CreateDirectory(directory);
        File.WriteAllText(ReadSaveSystemPath("PreferencesPath"), "{\"version\":0}");

        Assert.That(GmSaveSystem.TryLoadAccessibilityPreferences(), Is.True,
            "the independent preference format lost its explicit v0 migration");
        Assert.That(Read<bool>("Captions"), Is.False);
        Assert.That(Read<bool>("ReducedMotion"), Is.False);
        Assert.That(Read<bool>("Vibration"), Is.True);
        Assert.That(Read<bool>("MonoAudio"), Is.False);
        Assert.That(Read<bool>("HighContrast"), Is.False);
        Assert.That(Read<float>("TextScale"), Is.EqualTo(1f).Within(0.001f));
    }

    [TestCase(-1)]
    [TestCase(2)]
    public void UnknownStandalonePreferenceVersionsFailWithoutMutatingLivePreferences(int version)
    {
        Invoke("SetCaptions", true);
        Invoke("SetVibration", false);
        Invoke("SetTextScale", 1.7f);
        Assert.That((bool)InvokeWithResult("FlushPendingSave"), Is.True,
            "fixture must begin clean so the unknown disk version is actually interrogated");
        Directory.CreateDirectory(directory);
        File.WriteAllText(ReadSaveSystemPath("PreferencesPath"),
            $"{{\"version\":{version},\"captions\":false,\"vibration\":true,\"textScale\":1}}");

        Assert.That(GmSaveSystem.TryLoadAccessibilityPreferences(), Is.False);
        Assert.That(Read<bool>("Captions"), Is.True);
        Assert.That(Read<bool>("Vibration"), Is.False);
        Assert.That(Read<float>("TextScale"), Is.EqualTo(1.7f).Within(0.001f));
    }

    [Test]
    public void PreferenceFlushWithNoRunCannotCreateAFakeContinueSave()
    {
        Assert.That(GmSaveSystem.HasSave(), Is.False);
        Invoke("SetCaptions", true);

        Assert.That((bool)InvokeWithResult("FlushPendingSave"), Is.True);

        Assert.That(GmSaveSystem.HasSave(), Is.False,
            "player preferences created a run file and enabled Continue");
        Assert.That(File.Exists(GmSaveSystem.SavePath), Is.False);
        Assert.That(File.Exists(ReadSaveSystemPath("PreferencesPath")), Is.True,
            "preferences were not written to their independent durable path");
    }

    [Test]
    public void PreferenceFlushLeavesAdvancedRunBytesAndStateUntouched()
    {
        GmRunStore.RecordCatch("advanced-catch");
        GmRunStore.CollectShard(2);
        GmRunStore.RaiseCorruption("advanced-corruption");
        GmRunStore.CurrentSceneId = "court";
        Assert.That(GmSaveSystem.Save(), Is.True);
        byte[] before = File.ReadAllBytes(GmSaveSystem.SavePath);

        GmRunStore.BeginNewRun();
        Invoke("SetHighContrast", true);
        Assert.That((bool)InvokeWithResult("FlushPendingSave"), Is.True);

        CollectionAssert.AreEqual(before, File.ReadAllBytes(GmSaveSystem.SavePath),
            "cold Boot preference flush rewrote the advanced run bytes");
        Assert.That(GmSaveSystem.Load(), Is.True);
        Assert.That(GmRunStore.CheatsCaught, Does.Contain("advanced-catch"));
        Assert.That(GmRunStore.HasShard(2), Is.True);
        Assert.That(GmRunStore.CurrentSceneId, Is.EqualTo("court"));
    }

    [Test]
    public void PreferenceFileWinsAndDirectRunLoadCannotOverwriteIt()
    {
        Invoke("SetCaptions", false);
        Invoke("SetTextScale", 1.6f);
        Invoke("MarkPersistenceDirty");
        Assert.That((bool)InvokeWithResult("FlushPendingSave"), Is.True);

        var staleRun = new GmSaveData
        {
            accessibilitySettingsVersion = 1,
            accessibilityCaptions = true,
            accessibilityTextScale = 2f,
            currentSceneId = "court",
        };
        File.WriteAllText(GmSaveSystem.SavePath, JsonUtility.ToJson(staleRun, true));
        Invoke("ResetToDefaultsForTests");
        Assert.That(GmSaveSystem.TryLoadAccessibilityPreferences(), Is.True);
        Assert.That(Read<bool>("Captions"), Is.False);
        Assert.That(Read<float>("TextScale"), Is.EqualTo(1.6f).Within(0.001f));

        Assert.That(GmSaveSystem.Load(), Is.True);
        Assert.That(Read<bool>("Captions"), Is.False,
            "full run load replaced newer player preferences with stale embedded fields");
        Assert.That(Read<float>("TextScale"), Is.EqualTo(1.6f).Within(0.001f));
    }

    [Test]
    public void MissingPreferenceFileMigratesValidLegacyRunOnceWithoutChangingRunBytes()
    {
        var legacyRun = new GmSaveData
        {
            accessibilitySettingsVersion = 1,
            accessibilityCaptions = true,
            accessibilityVibration = false,
            accessibilityTextScale = 1.7f,
            currentSceneId = "parlor",
        };
        Directory.CreateDirectory(directory);
        File.WriteAllText(GmSaveSystem.SavePath, JsonUtility.ToJson(legacyRun, true));
        byte[] before = File.ReadAllBytes(GmSaveSystem.SavePath);
        Invoke("ResetToDefaultsForTests");

        Assert.That(GmSaveSystem.TryLoadAccessibilityPreferences(), Is.True);

        Assert.That(Read<bool>("Captions"), Is.True);
        Assert.That(Read<bool>("Vibration"), Is.False);
        Assert.That(Read<float>("TextScale"), Is.EqualTo(1.7f).Within(0.001f));
        Assert.That(File.Exists(ReadSaveSystemPath("PreferencesPath")), Is.True);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(GmSaveSystem.SavePath));
    }

    [Test]
    public void FailedNewerPreferenceWriteOutranksOlderDiskDuringNewRunAndRetriesLater()
    {
        Invoke("SetTextScale", 1.4f);
        Assert.That((bool)InvokeWithResult("FlushPendingSave"), Is.True);
        string preferencesPath = ReadSaveSystemPath("PreferencesPath");
        GmAccessibilityPreferencesData durableA =
            JsonUtility.FromJson<GmAccessibilityPreferencesData>(File.ReadAllText(preferencesPath));
        Assert.That(durableA.textScale, Is.EqualTo(1.4f).Within(0.001f));

        var preferenceBackend = new ToggleFileBackend { Fail = true };
        GmSaveSystem.ConfigureForTests(savePath, new GmFileAtomicSaveBackend(),
            preferencesPath, preferenceBackend);
        GmRunStore.RecordCatch("must-not-survive-new-run");
        Invoke("SetCaptions", true);
        Invoke("SetTextScale", 2f);
        UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
            new System.Text.RegularExpressions.Regex("injected preference failure"));
        Assert.That((bool)InvokeWithResult("FlushPendingSave"), Is.False);
        Assert.That(Read<bool>("HasPendingSave"), Is.True);

        UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
            new System.Text.RegularExpressions.Regex("injected preference failure"));
        Assert.That(GmSaveSystem.TryLoadAccessibilityPreferences(), Is.False,
            "a failed newer write allowed the older disk copy to be treated as authoritative");
        GmRunStore.BeginNewRun();

        Assert.That(Read<bool>("Captions"), Is.True,
            "New Run replaced newer dirty live preferences with older durable preferences");
        Assert.That(Read<float>("TextScale"), Is.EqualTo(2f).Within(0.001f));
        Assert.That(Read<bool>("HasPendingSave"), Is.True,
            "failed preferences lost their retry state");
        Assert.That(GmRunStore.CheatsCaught, Is.Empty,
            "preserving player preferences imported stale run state into New Run");
        Assert.That(GmRunStore.CurrentSceneId, Is.EqualTo("wend-hill-prologue"));

        preferenceBackend.Fail = false;
        Assert.That((bool)InvokeWithResult("FlushPendingSave"), Is.True);
        Invoke("ResetToDefaultsForTests");
        Assert.That(GmSaveSystem.TryLoadAccessibilityPreferences(), Is.True);
        Assert.That(Read<bool>("Captions"), Is.True);
        Assert.That(Read<float>("TextScale"), Is.EqualTo(2f).Within(0.001f));
    }

    T Read<T>(string property)
    {
        Assert.That(settingsType, Is.Not.Null,
            "there is no single global accessibility authority");
        PropertyInfo info = settingsType.GetProperty(property,
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(info, Is.Not.Null, $"accessibility authority has no {property} property");
        return (T)info.GetValue(null);
    }

    void Invoke(string method, params object[] arguments)
    {
        if (settingsType == null)
        {
            if (method == "ResetToDefaultsForTests") return;
            Assert.Fail("there is no single global accessibility authority");
        }
        MethodInfo info = settingsType.GetMethod(method,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(info, Is.Not.Null, $"accessibility authority has no {method} method");
        info.Invoke(null, arguments);
    }

    object InvokeWithResult(string method, params object[] arguments)
    {
        Assert.That(settingsType, Is.Not.Null);
        MethodInfo info = settingsType.GetMethod(method,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(info, Is.Not.Null, $"accessibility authority has no {method} method");
        return info.Invoke(null, arguments);
    }

    static string ReadSaveSystemPath(string property)
    {
        PropertyInfo info = typeof(GmSaveSystem).GetProperty(property,
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(info, Is.Not.Null, $"GmSaveSystem has no {property}");
        return (string)info.GetValue(null);
    }

    sealed class ToggleFileBackend : IGmAtomicSaveBackend
    {
        readonly GmFileAtomicSaveBackend file = new GmFileAtomicSaveBackend();
        public bool Fail;

        public void WriteAtomic(string target, string json)
        {
            if (Fail) throw new IOException("injected preference failure");
            file.WriteAtomic(target, json);
        }
    }
}
