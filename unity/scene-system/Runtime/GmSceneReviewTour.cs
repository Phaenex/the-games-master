// Reusable, deterministic screenshot capture for future rooms. Scene-specific subclasses supply
// deliberate camera compositions and optional state hooks; this class owns rendering, luminance
// evidence, output naming, one-shot arming, clean editor shutdown and placeholder detection.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public readonly struct GmReviewShot
{
    public readonly string Name;
    public readonly Vector3 Position;
    public readonly float Yaw;
    public readonly float Pitch;

    public GmReviewShot(string name, Vector3 position, float yaw, float pitch)
    {
        Name = name;
        Position = position;
        Yaw = yaw;
        Pitch = pitch;
    }
}

// Procedural dressing must protect review/playtest viewpoints just as deliberately as walkable
// rectangles. A tree can stand outside a route collider and still put the camera inside its crown.
// Builders can use this scene-agnostic helper before placement; the occupancy report then verifies
// the result from the rendered camera's point of view.
public static class GmReviewShotProtection
{
    public static float HorizontalDistanceToBounds(Vector3 position, Bounds bounds)
    {
        float dx = position.x < bounds.min.x ? bounds.min.x - position.x :
            position.x > bounds.max.x ? position.x - bounds.max.x : 0f;
        float dz = position.z < bounds.min.z ? bounds.min.z - position.z :
            position.z > bounds.max.z ? position.z - bounds.max.z : 0f;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    public static bool IsInsideHorizontalClearance(Vector3 candidate,
        IReadOnlyList<GmReviewShot> shots, float clearance)
    {
        if (shots == null || clearance < 0f) return false;
        float clearanceSquared = clearance * clearance;
        foreach (GmReviewShot shot in shots)
        {
            float dx = candidate.x - shot.Position.x;
            float dz = candidate.z - shot.Position.z;
            if (dx * dx + dz * dz < clearanceSquared) return true;
        }
        return false;
    }
}

public abstract class GmSceneReviewTour : MonoBehaviour
{
    public bool runOnPlay;
    public float settleSeconds = 1.2f;
    public int shotWidth = 1920;
    public int shotHeight = 1080;
    public int minimumLuminanceRange = 6;
    [Range(0f, 1f)] public float maximumNearBlackFraction = 0.97f;
    [Range(0f, 1f)] public float maximumNearWhiteFraction = 0.25f;

    public const string ArmKey = "GmSceneReviewTour.armed";
    protected abstract IReadOnlyList<GmReviewShot> ReviewShots { get; }
    protected virtual float ReviewYawOffset => 0f;
    protected virtual string ReviewLogTag => "GmSceneReviewTour";

    public int ShotCount => ReviewShots?.Count ?? 0;
    public int ShotWidth => shotWidth;
    public int ShotHeight => shotHeight;
    public IReadOnlyList<GmReviewShot> ShotsForAudit => ReviewShots;
    public float ReviewYawOffsetForAudit => ReviewYawOffset;
    public bool HasPlaceholderShots
    {
        get
        {
            if (ReviewShots == null) return true;
            foreach (var shot in ReviewShots)
                if (string.IsNullOrWhiteSpace(shot.Name) || shot.Name.Contains("replace-me")) return true;
            return false;
        }
    }

    protected virtual void BeforeTour() { }
    protected virtual void BeforeShot(GmReviewShot shot) { }
    /// Scene-specific perceptual gates can compare the actual captured pixels after the generic
    /// blank/exposure checks. Return a concise failure reason or null when the shot is acceptable.
    protected virtual string ValidateCapturedShot(GmReviewShot shot, Color32[] pixels) => null;

    protected virtual void Start()
    {
        if (runOnPlay) { StartCoroutine(Tour()); return; }
#if UNITY_EDITOR
        bool sessionArmed = SessionState.GetBool(ArmKey, false);
        string persistedArm = EditorPrefs.GetString(ArmKey, "");
        EditorPrefs.DeleteKey(ArmKey);
        bool persistedArmed = long.TryParse(persistedArm, out long ticks) &&
            (System.DateTime.UtcNow - new System.DateTime(ticks, System.DateTimeKind.Utc)).TotalMinutes < 5.0;
        if (sessionArmed || persistedArmed)
        {
            SessionState.SetBool(ArmKey, false);
            StartCoroutine(Tour());
        }
#endif
    }

    IEnumerator Tour()
    {
        var shots = ReviewShots;
        if (shots == null || shots.Count == 0)
        {
            Debug.LogError($"[{ReviewLogTag}] FAILED: no review shots are defined");
            EndPlay(1);
            yield break;
        }

        var names = new HashSet<string>();
        foreach (var shot in shots)
        {
            if (string.IsNullOrWhiteSpace(shot.Name) || shot.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                Debug.LogError($"[{ReviewLogTag}] FAILED: invalid shot name '{shot.Name}'");
                EndPlay(1);
                yield break;
            }
            if (!names.Add(shot.Name))
            {
                Debug.LogError($"[{ReviewLogTag}] FAILED: duplicate shot name '{shot.Name}'");
                EndPlay(1);
                yield break;
            }
        }

        var player = FindAnyObjectByType<GmPlayer>();
        Camera camera = player != null ? player.GetComponentInChildren<Camera>() : Camera.main;
        if (camera == null)
        {
            Debug.LogError($"[{ReviewLogTag}] FAILED: no camera in scene, captured nothing");
            EndPlay(1);
            yield break;
        }

        string sceneName = gameObject.scene.name;
        string dir = Path.Combine(Directory.GetCurrentDirectory(), "Screens", sceneName);
        Directory.CreateDirectory(dir);
        yield return new WaitForSecondsRealtime(settleSeconds);
        BeforeTour();

        var target = new RenderTexture(shotWidth, shotHeight, 24, RenderTextureFormat.ARGB32);
        var texture = new Texture2D(shotWidth, shotHeight, TextureFormat.RGBA32, false);
        int written = 0;
        int invalidVisualEvidence = 0;
        foreach (var shot in shots)
        {
            BeforeShot(shot);
            if (player != null && camera.transform.IsChildOf(player.transform))
            {
                float eyeHeight = camera.transform.localPosition.y;
                player.transform.position = shot.Position - Vector3.up * eyeHeight;
                player.transform.rotation = Quaternion.Euler(0f, shot.Yaw + ReviewYawOffset, 0f);
                camera.transform.localRotation = Quaternion.Euler(shot.Pitch, 0f, 0f);
            }
            else
            {
                camera.transform.position = shot.Position;
                camera.transform.rotation = Quaternion.Euler(shot.Pitch, shot.Yaw + ReviewYawOffset, 0f);
            }

            Physics.SyncTransforms();
            camera.targetTexture = target;
            for (int i = 0; i < 10; i++) camera.Render();
            camera.targetTexture = null;

            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, shotWidth, shotHeight), 0, 0);
            texture.Apply();
            RenderTexture.active = null;

            string file = Path.Combine(dir, $"tour-{shot.Name}.png");
            File.WriteAllBytes(file, texture.EncodeToPNG());
            written++;

            long sum = 0;
            int nearBlack = 0;
            int nearWhite = 0;
            var pixels = texture.GetPixels32();
            var luminance = new byte[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                byte value = (byte)((pixels[i].r + pixels[i].g + pixels[i].b) / 3);
                luminance[i] = value;
                sum += value;
                if (value <= 2) nearBlack++;
                if (value >= 253) nearWhite++;
            }
            System.Array.Sort(luminance);
            int p05 = luminance[Mathf.Clamp(Mathf.FloorToInt(luminance.Length * 0.05f), 0, luminance.Length - 1)];
            int p95 = luminance[Mathf.Clamp(Mathf.FloorToInt(luminance.Length * 0.95f), 0, luminance.Length - 1)];
            float blackFraction = nearBlack / (float)pixels.Length;
            float whiteFraction = nearWhite / (float)pixels.Length;
            int range = p95 - p05;
            bool valid = range >= minimumLuminanceRange &&
                blackFraction <= maximumNearBlackFraction && whiteFraction <= maximumNearWhiteFraction;
            if (!valid)
            {
                invalidVisualEvidence++;
                Debug.LogError($"[{ReviewLogTag}] FAILED VISUAL EVIDENCE {shot.Name}: p05={p05} p95={p95} " +
                    $"range={range} black={blackFraction:P1} white={whiteFraction:P1}");
            }
            string sceneSpecificFailure = ValidateCapturedShot(shot, pixels);
            if (!string.IsNullOrEmpty(sceneSpecificFailure))
            {
                invalidVisualEvidence++;
                Debug.LogError($"[{ReviewLogTag}] FAILED PERCEPTUAL EVIDENCE {shot.Name}: " +
                    sceneSpecificFailure);
            }
            Debug.Log($"[{ReviewLogTag}] {shot.Name} meanLum={sum / pixels.Length} p05={p05} p95={p95} " +
                $"black={blackFraction:P1} white={whiteFraction:P1} -> {file}");
        }

        camera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(texture);
        target.Release();
        Destroy(target);
        if (invalidVisualEvidence > 0)
        {
            Debug.LogError($"[{ReviewLogTag}] FAILED: {invalidVisualEvidence}/{shots.Count} captures were blank, clipped or too flat to review");
            EndPlay(1);
        }
        else
        {
            Debug.Log($"[{ReviewLogTag}] TOUR COMPLETE {written}/{shots.Count} -> {dir}");
            EndPlay(0);
        }
    }

    static void EndPlay(int exitCode)
    {
#if UNITY_EDITOR
        EditorApplication.Exit(exitCode);
#endif
    }
}

#if UNITY_EDITOR
public static class GmSceneReviewTourMenu
{
    public static void ArmAndPlay<T>(string scenePath) where T : GmSceneReviewTour
    {
        if (!File.Exists(scenePath))
        {
            Debug.LogError($"[GmSceneReviewTour] FAILED: {scenePath} does not exist; build it first");
            EditorApplication.Exit(1);
            return;
        }
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var tour = Object.FindAnyObjectByType<T>();
        if (tour == null)
        {
            Debug.LogError($"[GmSceneReviewTour] FAILED: no {typeof(T).Name} component in the scene");
            EditorApplication.Exit(1);
            return;
        }
        SessionState.SetBool(GmSceneReviewTour.ArmKey, true);
        EditorPrefs.SetString(GmSceneReviewTour.ArmKey, System.DateTime.UtcNow.Ticks.ToString());
        tour.runOnPlay = true;
        Debug.Log($"[GmSceneReviewTour] armed {tour.ShotCount} shots in '{tour.gameObject.scene.name}'");
        EditorApplication.isPlaying = true;
    }
}
#endif
