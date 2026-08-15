// Save plumbing for the village prologue.
//
// The project had none: a search for PlayerPrefs/save handling across Assets/Scripts turned up only
// GmDisplayCalibration storing a text-size preference. So this is a new system rather than a port,
// and it is deliberately conservative -- a save that half-restores a horror prologue is worse than
// no save, because the player cannot tell which beats they actually saw.
//
// What it restores: where you were standing and which way you faced, whether the gate has locked,
// and whether the bell is counting. What it does NOT restore is how far the count had got --
// GmBellSummons.Toll has no setter, and where a resumed count should pick up is a pacing call, so a
// resumed save re-arms the bell from toll zero. Nor does it restore which prose you have already
// read; beats and examines re-arm on load. Re-reading a paragraph is a much smaller failure than
// silently skipping one, and GmDesignRuntime's fired-flags are private runtime state with no accessor.
//
// Writes to Application.persistentDataPath, autosaves on a timer and on quit, and refuses to restore
// a save from a different scene.
using System;
using System.IO;
using UnityEngine;

public sealed class GmVillageSave : MonoBehaviour
{
    const string FileName = "wendhill-village-save.json";
    const float AutosaveSeconds = 20f;
    const float MinRestoreDistance = 3f;   // ignore a save taken essentially at the spawn

    [Serializable]
    class State
    {
        public string scene;
        public string savedAtUtc;
        public float x, y, z;
        public float yaw;
        public bool gateLocked;
        public int bellToll;
        public int version = 1;
    }

    static string Path_ => System.IO.Path.Combine(Application.persistentDataPath, FileName);

    /// Why a save was or was not restored.
    public enum RestoreVerdict
    {
        Restore,
        WrongScene,     // a save from another scene would teleport the player into geometry
        AtTheSpawn,     // nothing meaningful to restore
    }

    /// The restore decision, separated from the scene so it can be tested.
    ///
    /// Both refusals are silent by nature: restoring a save from another scene drops the player into
    /// whatever happens to be at those coordinates here, and restoring one taken at the spawn moves
    /// nobody while looking like it worked. Neither shows up as an error, which is exactly why the
    /// decision is worth pinning down in a test rather than reading and trusting.
    public static RestoreVerdict Decide(
        string saveScene, string currentScene, Vector3 saved, Vector3 spawn, float minDistance)
    {
        if (string.IsNullOrEmpty(saveScene) || saveScene != currentScene) return RestoreVerdict.WrongScene;
        if (Vector3.Distance(saved, spawn) < minDistance) return RestoreVerdict.AtTheSpawn;
        return RestoreVerdict.Restore;
    }

    Transform player;
    GmBellSummons bell;
    GmGateLeaves gate;
    float nextSaveAt;
    bool restored;
    bool armBellOnResume;

    void Start()
    {
        GameObject go = GameObject.Find("Player");
        player = go != null ? go.transform : null;
        bell = FindFirstObjectByType<GmBellSummons>();
        gate = FindFirstObjectByType<GmGateLeaves>();
        nextSaveAt = Time.time + AutosaveSeconds;

        // Opt-in. Automated runs (the walk test, the review probe) must always start from the
        // authored spawn, or a stale save would silently move the thing under test.
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-gmVillageResume") >= 0) Restore();
        else if (File.Exists(Path_))
            Debug.Log($"[GmVillageSave] a save exists at {Path_}; pass -gmVillageResume to load it");
    }

    void Update()
    {
        // Held over from Restore so GmBellSummons.Start has run first: Arm() snapshots
        // firstTollDelay into its next-toll time, so arming ahead of it would schedule the first
        // toll off the default cadence instead of the profile actually in force.
        if (armBellOnResume)
        {
            armBellOnResume = false;
            bell?.Arm();
        }

        if (Time.time < nextSaveAt) return;
        nextSaveAt = Time.time + AutosaveSeconds;
        Save();
    }

    void OnApplicationQuit() => Save();

    public void Save()
    {
        if (player == null) return;
        try
        {
            var s = new State
            {
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                savedAtUtc = DateTime.UtcNow.ToString("o"),
                x = player.position.x,
                y = player.position.y,
                z = player.position.z,
                yaw = player.rotation.eulerAngles.y,
                gateLocked = gate != null && gate.IsClosed,
                bellToll = bell != null ? bell.Toll : 0,
            };
            File.WriteAllText(Path_, JsonUtility.ToJson(s, true));
        }
        catch (Exception e)
        {
            // A failed save must never take the game down mid-walk.
            Debug.LogWarning($"[GmVillageSave] could not write {Path_}: {e.Message}");
        }
    }

    void Restore()
    {
        if (restored || player == null || !File.Exists(Path_)) return;
        try
        {
            var s = JsonUtility.FromJson<State>(File.ReadAllText(Path_));
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            var target = s == null ? Vector3.zero : new Vector3(s.x, s.y, s.z);

            switch (Decide(s?.scene, scene, target, player.position, MinRestoreDistance))
            {
                case RestoreVerdict.WrongScene:
                    Debug.Log($"[GmVillageSave] save is for '{s?.scene}', current scene is '{scene}'; ignoring");
                    return;
                case RestoreVerdict.AtTheSpawn:
                    Debug.Log("[GmVillageSave] save is at the spawn; nothing to restore");
                    restored = true;
                    return;
            }

            // The CharacterController owns the transform and will fight a direct assignment, so it
            // is disabled across the teleport and re-enabled after.
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.position = target;
            player.rotation = Quaternion.Euler(0f, s.yaw, 0f);
            if (cc != null) cc.enabled = true;

            // Snap, not swing: on a restore the gate locked in the previous session, so animating it
            // shut now would replay a beat the player already had.
            if (s.gateLocked) gate?.SetClosedImmediate();

            // The gate lock and the bell's arming are one event in GmThreshold, so a save with the
            // gate shut was taken with the count already running. Re-closing the gate without
            // re-arming the bell leaves the hour that ends the prologue unreachable for the rest of
            // the session -- the player walks a village that can no longer take them.
            if (s.gateLocked || s.bellToll > 0) armBellOnResume = true;

            restored = true;
            string bellState = armBellOnResume
                ? $"re-arming, count restarts from 0 (was toll {s.bellToll})"
                : "not counting";
            Debug.Log($"[GmVillageSave] restored to {target} yaw={s.yaw:0.#} " +
                      $"gateLocked={s.gateLocked} bell={bellState} (saved {s.savedAtUtc})");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GmVillageSave] could not read {Path_}: {e.Message}");
        }
    }
}
