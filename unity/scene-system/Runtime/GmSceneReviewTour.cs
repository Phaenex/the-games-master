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
    [Range(0f, 0.01f)] public float maximumSaturatedMagentaFraction = 0.0001f;

    public const string ArmKey = "GmSceneReviewTour.armed";
    protected abstract IReadOnlyList<GmReviewShot> ReviewShots { get; }
    protected virtual float ReviewYawOffset => 0f;
    protected virtual string ReviewLogTag => "GmSceneReviewTour";
    protected virtual bool CaptureReviewBackbuffer => false;
    // A stateful tour may expose its complete authored shot contract while deferring a tail of
    // captures to an object that survives a scene unload. Ordinary tours capture every shot.
    protected virtual int DirectCaptureShotCount => ReviewShots?.Count ?? 0;

    public int ShotCount => ReviewShots?.Count ?? 0;
    public int ShotWidth => shotWidth;
    public int ShotHeight => shotHeight;
    public IReadOnlyList<GmReviewShot> ShotsForAudit => ReviewShots;
    public float ReviewYawOffsetForAudit => ReviewYawOffset;
    public bool UsesBackbufferCaptureForAudit => CaptureReviewBackbuffer;
    // Composition validation runs in EditMode, outside the capture coroutine. Stateful shots still
    // need the same temporary geometry state they use when rendered (an opened exit, an activated
    // interior), and that state must be restored so an audit cannot dirty the shipping scene.
    public virtual void PrepareShotForAudit(GmReviewShot shot) { }
    public virtual void RestoreAfterAuditShot(GmReviewShot shot) { }
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
    // Stateful runtime overlays commit layout at the end of a frame. A scene may wait here after
    // staging its semantic state and before the backbuffer is sampled.
    protected virtual IEnumerator BeforeShotSettled(GmReviewShot shot) { yield break; }
    /// Scene-specific perceptual gates can compare the actual captured pixels after the generic
    /// blank/exposure checks. Return a concise failure reason or null when the shot is acceptable.
    protected virtual string ValidateCapturedShot(GmReviewShot shot, Color32[] pixels) => null;
    // Runs only after the PNG exists. Stateful tours use this to bind semantic state and a file
    // digest to the exact frame, rather than trusting a filename to describe what was captured.
    protected virtual void AfterShotCaptured(GmReviewShot shot, string file, Texture2D captured) { }
    // Return true only when a persistent owner has accepted responsibility for final validation,
    // completion logging and editor exit. This keeps the base tour from claiming success early.
    protected virtual bool TryBeginDeferredCompletion(Camera camera, string directory,
        int written, int invalidVisualEvidence) => false;

    protected virtual void Start()
    {
#if UNITY_EDITOR
        bool sessionArmed = SessionState.GetBool(ArmKey, false);
        string persistedArm = EditorPrefs.GetString(ArmKey, "");
        // Consume both arm channels even when runOnPlay survived the domain reload. Returning before
        // this cleanup leaked the global token into the next PlayMode scene for five minutes, where an
        // unrelated legacy tour could start and fail a canonical test run.
        SessionState.SetBool(ArmKey, false);
        EditorPrefs.DeleteKey(ArmKey);
        bool persistedArmed = long.TryParse(persistedArm, out long ticks) &&
            (System.DateTime.UtcNow - new System.DateTime(ticks, System.DateTimeKind.Utc)).TotalMinutes < 5.0;
        // In the Editor, only an explicit, freshly consumed arm token may launch capture. Serialized
        // runOnPlay values can survive a previous review in an unrelated scene and must not turn an
        // ordinary PlayMode test into a rendering job. Player builds retain runOnPlay below.
        if (sessionArmed || persistedArmed)
        {
            StartCoroutine(Tour());
        }
#else
        if (runOnPlay) StartCoroutine(Tour());
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
        int directCount = Mathf.Clamp(DirectCaptureShotCount, 0, shots.Count);
        foreach (GmReviewShot shot in shots)
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
        GmPlayer playerCtrl = player != null ? player.GetComponent<GmPlayer>() : null;
        if (playerCtrl != null) playerCtrl.SetControlBlocked(true);
        BeforeTour();

        RenderTexture target = CaptureReviewBackbuffer ? null :
            new RenderTexture(shotWidth, shotHeight, 24, RenderTextureFormat.ARGB32);
        Texture2D texture = CaptureReviewBackbuffer ? null :
            new Texture2D(shotWidth, shotHeight, TextureFormat.RGBA32, false);
        int written = 0;
        int invalidVisualEvidence = 0;
        for (int shotIndex = 0; shotIndex < directCount; shotIndex++)
        {
            GmReviewShot shot = shots[shotIndex];
            BeforeShot(shot);
            if (player != null && camera.transform.IsChildOf(player.transform))
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                float eyeHeight = camera.transform.localPosition.y;
                player.transform.position = shot.Position - Vector3.up * eyeHeight;
                player.transform.rotation = Quaternion.Euler(0f, shot.Yaw + ReviewYawOffset, 0f);
                camera.transform.localRotation = Quaternion.Euler(shot.Pitch, 0f, 0f);
                if (cc != null) cc.enabled = true;
            }
            else
            {
                camera.transform.position = shot.Position;
                camera.transform.rotation = Quaternion.Euler(shot.Pitch, shot.Yaw + ReviewYawOffset, 0f);
            }

            Physics.SyncTransforms();
            yield return BeforeShotSettled(shot);
            Texture2D captured = texture;
            if (CaptureReviewBackbuffer)
            {
                yield return new WaitForEndOfFrame();
                captured = ScreenCapture.CaptureScreenshotAsTexture();
                if (captured == null)
                {
                    Debug.LogError($"[{ReviewLogTag}] FAILED: backbuffer capture returned null for {shot.Name}");
                    invalidVisualEvidence++;
                    continue;
                }
            }
            else
            {
                camera.targetTexture = target;
                for (int i = 0; i < 10; i++) camera.Render();
                camera.targetTexture = null;

                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, shotWidth, shotHeight), 0, 0);
                texture.Apply();
                RenderTexture.active = null;
            }

            string file = Path.Combine(dir, $"tour-{shot.Name}.png");
            File.WriteAllBytes(file, captured.EncodeToPNG());
            written++;
            AfterShotCaptured(shot, file, captured);

            long sum = 0;
            int nearBlack = 0;
            int nearWhite = 0;
            var pixels = captured.GetPixels32();
            int magentaPixels = LogMagentaOccupants(camera, shot.Name, pixels,
                captured.width, captured.height);
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
            bool isHighContrastUi = whiteFraction >= 0.005f && blackFraction >= 0.90f;
            bool valid = (range >= minimumLuminanceRange || (isHighContrastUi && whiteFraction >= 0.005f)) &&
                (blackFraction <= maximumNearBlackFraction || isHighContrastUi) &&
                whiteFraction <= maximumNearWhiteFraction &&
                magentaPixels / (float)pixels.Length <= maximumSaturatedMagentaFraction;
            if (!valid)
            {
                invalidVisualEvidence++;
                Debug.LogError($"[{ReviewLogTag}] FAILED VISUAL EVIDENCE {shot.Name}: p05={p05} p95={p95} " +
                    $"range={range} black={blackFraction:P1} white={whiteFraction:P1} " +
                    $"magenta={magentaPixels / (float)pixels.Length:P3}");
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
            if (CaptureReviewBackbuffer) Destroy(captured);
        }

        camera.targetTexture = null;
        RenderTexture.active = null;
        if (texture != null) Destroy(texture);
        if (target != null)
        {
            target.Release();
            Destroy(target);
        }
        if (TryBeginDeferredCompletion(camera, dir, written, invalidVisualEvidence)) yield break;
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

    // A saturated magenta patch is almost always Unity's error shader. Name the renderer covering
    // its centroid while the scene and camera still exist, instead of spending another full capture
    // cycle guessing which of hundreds of renderers owns the pixels.
    static int LogMagentaOccupants(Camera camera, string shotName, Color32[] pixels, int width, int height)
    {
        long sumX = 0;
        long sumY = 0;
        int count = 0;
        for (int index = 0; index < pixels.Length; index++)
        {
            Color32 pixel = pixels[index];
            if (pixel.r < 180 || pixel.b < 180 || pixel.g > 70) continue;
            sumX += index % width;
            sumY += index / width;
            count++;
        }
        if (count < 24) return count;

        Vector2 point = new Vector2(sumX / (float)count, sumY / (float)count);
        var occupants = new List<string>();
        foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude,
                     FindObjectsSortMode.None))
        {
            if (!renderer.enabled) continue;
            Bounds bounds = renderer.bounds;
            Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 world = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                    (corner & 1) == 0 ? -1f : 1f,
                    (corner & 2) == 0 ? -1f : 1f,
                    (corner & 4) == 0 ? -1f : 1f));
                Vector3 screen = camera.WorldToScreenPoint(world);
                if (screen.z <= 0f) continue;
                min = Vector3.Min(min, screen);
                max = Vector3.Max(max, screen);
            }
            if (point.x < min.x || point.x > max.x || point.y < min.y || point.y > max.y) continue;
            Material material = renderer.sharedMaterial;
            occupants.Add($"{renderer.transform.name} material='{(material != null ? material.name : "NULL")}' " +
                $"shader='{(material != null && material.shader != null ? material.shader.name : "NULL")}'");
            if (occupants.Count >= 8) break;
        }
        Debug.LogWarning($"[{nameof(GmSceneReviewTour)}] MAGENTA {shotName}: {count} pixels around " +
            $"({point.x:0},{point.y:0}); renderers=[{string.Join(" | ", occupants)}]");
        return count;
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
