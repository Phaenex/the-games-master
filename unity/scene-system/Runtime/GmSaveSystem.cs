using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Handles atomic save/load serialization for the persistent run store.
/// </summary>
public static class GmSaveSystem
{
    public static string SavePath => Path.Combine(Application.persistentDataPath, "the_games_master_save.json");

    public static bool HasSave()
    {
        return File.Exists(SavePath);
    }

    public static bool SaveGame() => Save();
    public static bool LoadGame() => Load();

    public static bool Save()
    {
        try
        {
            string path = SavePath;
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            GmSaveData data = GmRunStore.ToSaveData();
            string json = JsonUtility.ToJson(data, true);
            string tmpPath = path + ".tmp";
            File.WriteAllText(tmpPath, json);
            // Swap, never delete-then-move. An interruption in the gap between the Delete and the
            // Move leaves nothing at path at all: the old save is gone and the new one is orphaned
            // at .tmp, where HasSave()/Load() cannot see it, so the run comes back as a fresh boot.
            // File.Replace is a rename, so the destination is either the old file or the new one.
            if (File.Exists(path)) File.Replace(tmpPath, path, null);
            else File.Move(tmpPath, path);
            Debug.Log($"[GmSaveSystem] Saved game to {path}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GmSaveSystem] Failed to save game: {ex.Message}");
            return false;
        }
    }

    public static bool Load()
    {
        try
        {
            string path = SavePath;
            if (!File.Exists(path))
            {
                Debug.LogWarning("[GmSaveSystem] No save file found.");
                return false;
            }

            string json = File.ReadAllText(path);
            GmSaveData data = JsonUtility.FromJson<GmSaveData>(json);
            if (data == null)
            {
                Debug.LogError("[GmSaveSystem] Corrupt save file.");
                return false;
            }

            GmRunStore.LoadFromSaveData(data);
            Debug.Log($"[GmSaveSystem] Loaded game from {path} (Tier {GmRunStore.CorruptionTier}, {GmRunStore.CheatsCaughtCount} catches, {GmRunStore.ShardsCount} shards)");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GmSaveSystem] Failed to load game: {ex.Message}");
            return false;
        }
    }

    public static bool DeleteSave()
    {
        try
        {
            string path = SavePath;
            if (File.Exists(path)) File.Delete(path);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GmSaveSystem] Failed to delete save: {ex.Message}");
            return false;
        }
    }
}
