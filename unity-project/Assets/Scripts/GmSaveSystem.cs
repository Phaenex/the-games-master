using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public interface IGmAtomicSaveBackend
{
    void WriteAtomic(string target, string json);
}

public sealed class GmFileAtomicSaveBackend : IGmAtomicSaveBackend
{
    public void WriteAtomic(string target, string json)
    {
        string directory = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        string temporary = target + ".tmp";
        File.WriteAllText(temporary, json);
        if (File.Exists(target)) File.Replace(temporary, target, null);
        else File.Move(temporary, target);
    }
}

/// <summary>One serialized writer. Pending generations collapse to the newest immutable JSON.</summary>
public sealed class GmCoalescingSaveWriter
{
    readonly object gate = new object();
    readonly IGmAtomicSaveBackend backend;
    string pendingPath;
    string pendingJson;
    long pendingGeneration;
    long latestGeneration;
    long completedGeneration;
    long durableGeneration;
    bool running;
    string lastError = string.Empty;

    public GmCoalescingSaveWriter(IGmAtomicSaveBackend backend)
    {
        this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public long DurableGeneration { get { lock (gate) return durableGeneration; } }
    public string LastError { get { lock (gate) return lastError; } }

    public long Queue(string path, string json)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Save path is required", nameof(path));
        if (json == null) throw new ArgumentNullException(nameof(json));
        lock (gate)
        {
            latestGeneration++;
            pendingGeneration = latestGeneration;
            pendingPath = path;
            pendingJson = json;
            if (!running)
            {
                running = true;
                Task.Run(WriteLoop);
            }
            return latestGeneration;
        }
    }

    public bool Flush(out string error)
    {
        lock (gate)
        {
            long target = latestGeneration;
            while (completedGeneration < target) Monitor.Wait(gate);
            bool success = durableGeneration >= target;
            error = success ? string.Empty : lastError;
            return success;
        }
    }

    void WriteLoop()
    {
        while (true)
        {
            string path;
            string json;
            long generation;
            lock (gate)
            {
                path = pendingPath;
                json = pendingJson;
                generation = pendingGeneration;
                pendingPath = null;
                pendingJson = null;
            }

            string failure = string.Empty;
            try { backend.WriteAtomic(path, json); }
            catch (Exception ex) { failure = ex.Message; }

            lock (gate)
            {
                completedGeneration = Math.Max(completedGeneration, generation);
                if (failure.Length == 0)
                {
                    durableGeneration = Math.Max(durableGeneration, generation);
                    lastError = string.Empty;
                }
                else lastError = failure;

                if (pendingJson == null)
                {
                    running = false;
                    Monitor.PulseAll(gate);
                    return;
                }
                Monitor.PulseAll(gate);
            }
        }
    }
}

/// <summary>Serializes on the main thread and coalesces atomic file IO on one background writer.</summary>
public static class GmSaveSystem
{
    static string pathOverride;
    static string preferencesPathOverride;
    static IGmAtomicSaveBackend runBackend = new GmFileAtomicSaveBackend();
    static IGmAtomicSaveBackend preferenceBackend = new GmFileAtomicSaveBackend();
    static GmCoalescingSaveWriter writer = new GmCoalescingSaveWriter(runBackend);
    static GmCoalescingSaveWriter preferencesWriter =
        new GmCoalescingSaveWriter(preferenceBackend);
    static string lastError = string.Empty;

    public static string SavePath => pathOverride ??
        Path.Combine(Application.persistentDataPath, "the_games_master_save.json");
    public static string PreferencesPath => preferencesPathOverride ??
        Path.Combine(Application.persistentDataPath, "the_games_master_preferences.json");
    public static string LastError => lastError.Length > 0 ? lastError :
        preferencesWriter.LastError.Length > 0 ? preferencesWriter.LastError : writer.LastError;
    public static long DurableGeneration => writer.DurableGeneration;

    public static void ConfigureForTests(string path, IGmAtomicSaveBackend backend = null)
    {
        string preferencePath = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty,
            "preferences.json");
        ConfigureForTests(path, backend, preferencePath, backend);
    }

    public static void ConfigureForTests(string runPath, IGmAtomicSaveBackend runBackend,
        string preferencePath, IGmAtomicSaveBackend preferenceBackend)
    {
        FlushAllSilently();
        pathOverride = runPath ?? throw new ArgumentNullException(nameof(runPath));
        preferencesPathOverride = preferencePath ??
            throw new ArgumentNullException(nameof(preferencePath));
        GmSaveSystem.runBackend = runBackend ?? new GmFileAtomicSaveBackend();
        GmSaveSystem.preferenceBackend = preferenceBackend ?? new GmFileAtomicSaveBackend();
        writer = new GmCoalescingSaveWriter(GmSaveSystem.runBackend);
        preferencesWriter = new GmCoalescingSaveWriter(GmSaveSystem.preferenceBackend);
        lastError = string.Empty;
    }

    public static IDisposable BeginTemporaryConfiguration(string runPath,
        IGmAtomicSaveBackend temporaryRunBackend = null)
    {
        string preferencePath = Path.Combine(Path.GetDirectoryName(runPath) ?? string.Empty,
            "preferences.json");
        return new TemporaryConfigurationScope(runPath, temporaryRunBackend,
            preferencePath, temporaryRunBackend);
    }

    public static void ResetTestConfiguration()
    {
        FlushAllSilently();
        pathOverride = null;
        preferencesPathOverride = null;
        runBackend = new GmFileAtomicSaveBackend();
        preferenceBackend = new GmFileAtomicSaveBackend();
        writer = new GmCoalescingSaveWriter(runBackend);
        preferencesWriter = new GmCoalescingSaveWriter(preferenceBackend);
        lastError = string.Empty;
    }

    sealed class TemporaryConfigurationScope : IDisposable
    {
        readonly string previousRunPath;
        readonly string previousPreferencePath;
        readonly IGmAtomicSaveBackend previousRunBackend;
        readonly IGmAtomicSaveBackend previousPreferenceBackend;
        bool disposed;

        public TemporaryConfigurationScope(string runPath, IGmAtomicSaveBackend temporaryRunBackend,
            string preferencePath, IGmAtomicSaveBackend temporaryPreferenceBackend)
        {
            previousRunPath = pathOverride;
            previousPreferencePath = preferencesPathOverride;
            previousRunBackend = runBackend;
            previousPreferenceBackend = preferenceBackend;
            ConfigureForTests(runPath, temporaryRunBackend, preferencePath,
                temporaryPreferenceBackend);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            FlushAllSilently();
            if (previousRunPath == null)
            {
                pathOverride = null;
                preferencesPathOverride = null;
                runBackend = previousRunBackend;
                preferenceBackend = previousPreferenceBackend;
                writer = new GmCoalescingSaveWriter(runBackend);
                preferencesWriter = new GmCoalescingSaveWriter(preferenceBackend);
                lastError = string.Empty;
                return;
            }
            ConfigureForTests(previousRunPath, previousRunBackend,
                previousPreferencePath, previousPreferenceBackend);
        }
    }

    public static bool HasSave() => File.Exists(SavePath);
    public static bool SaveGame() => Save();
    public static bool LoadGame() => Load();

    public static bool QueueSave(out long generation)
    {
        return QueueSaveData(GmRunStore.ToSaveData(), out generation);
    }

    internal static bool QueueSaveData(GmSaveData data, out long generation)
    {
        generation = 0;
        if (GmHousePersistenceCoordinator.IsRecollectionActive)
        {
            generation = writer.DurableGeneration;
            lastError = string.Empty;
            return true;
        }
        try
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            string json = data.ToJson(true);
            generation = writer.Queue(SavePath, json);
            lastError = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            lastError = ex.Message;
            Debug.LogError($"[GmSaveSystem] Failed to queue save: {ex.Message}");
            return false;
        }
    }

    public static bool Flush()
    {
        if (GmHousePersistenceCoordinator.IsRecollectionActive)
        {
            lastError = string.Empty;
            return true;
        }
        if (writer.Flush(out string error))
        {
            lastError = string.Empty;
            return true;
        }
        lastError = string.IsNullOrEmpty(error) ? "unknown writer failure" : error;
        Debug.LogError($"[GmSaveSystem] Failed to save game: {lastError}");
        return false;
    }

    public static bool Save() => QueueSave(out _) && Flush();

    public static bool QueueAccessibilityPreferences(out long generation)
    {
        generation = 0;
        try
        {
            string json = JsonUtility.ToJson(GmAccessibilitySettings.ToPreferencesData(), true);
            generation = preferencesWriter.Queue(PreferencesPath, json);
            lastError = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            lastError = ex.Message;
            Debug.LogError($"[GmSaveSystem] Failed to queue preferences: {ex.Message}");
            return false;
        }
    }

    public static bool FlushAccessibilityPreferences()
    {
        if (preferencesWriter.Flush(out string error))
        {
            lastError = string.Empty;
            return true;
        }
        lastError = string.IsNullOrEmpty(error) ? "unknown preference writer failure" : error;
        Debug.LogError($"[GmSaveSystem] Failed to save preferences: {lastError}");
        return false;
    }

    public static bool Load()
    {
        try
        {
            // A failed newest generation never invalidates the previous atomic file. Surface the
            // error, then allow an explicit load to recover that last durable checkpoint.
            Flush();
            string path = SavePath;
            if (!File.Exists(path))
            {
                Debug.LogWarning("[GmSaveSystem] No save file found.");
                return false;
            }
            GmSaveData data = GmSaveData.FromJson(File.ReadAllText(path));
            if (data == null)
            {
                Debug.LogError("[GmSaveSystem] Corrupt save file.");
                return false;
            }
            GmRunStore.LoadFromSaveData(data);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GmSaveSystem] Failed to load game: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Imports player preferences without loading any run state. New Run uses this after a cold app
    /// start so accessibility survives while catches, corruption and the saved scene stay behind.
    /// Missing or corrupt data is a recoverable default, never a reason to block a fresh run.
    /// </summary>
    public static bool TryLoadAccessibilityPreferences()
    {
        try
        {
            // Live dirty preferences are newer than any file on disk. Try to make that generation
            // durable first; if IO still fails, keep it live and dirty rather than applying stale
            // disk data over the player's most recent choices. New Run treats false as recoverable.
            if (GmAccessibilitySettings.HasPendingSave)
                return GmAccessibilitySettings.FlushPendingSave();

            preferencesWriter.Flush(out _);
            if (File.Exists(PreferencesPath))
            {
                GmAccessibilityPreferencesData preferences =
                    JsonUtility.FromJson<GmAccessibilityPreferencesData>(
                        File.ReadAllText(PreferencesPath));
                return GmAccessibilitySettings.TryLoadPreferences(preferences);
            }

            FlushSilently();
            if (!File.Exists(SavePath)) return false;
            GmSaveData data = GmSaveData.FromJson(File.ReadAllText(SavePath));
            if (data == null) return false;
            if (!GmAccessibilitySettings.TryLoadFrom(data)) return false;
            GmAccessibilitySettings.MarkPersistenceDirty();
            // Migration failure is recoverable: the imported values remain live and dirty so the
            // next lifecycle boundary retries without touching the run file.
            GmAccessibilitySettings.FlushPendingSave();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool DeleteSave()
    {
        try
        {
            if (!Flush()) return false;
            if (File.Exists(SavePath)) File.Delete(SavePath);
            if (File.Exists(SavePath + ".tmp")) File.Delete(SavePath + ".tmp");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GmSaveSystem] Failed to delete save: {ex.Message}");
            return false;
        }
    }

    static void FlushSilently() => writer.Flush(out _);
    static void FlushAllSilently()
    {
        writer.Flush(out _);
        preferencesWriter.Flush(out _);
    }
}
