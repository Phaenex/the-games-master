// Play-mode screenshot tour: captures the review set (spawn, drive, gate look-back, cemetery,
// chapel, garden, coach yard, porch) to Screens/<scene>/ for the visual review panel.
//
// Capture goes through an explicit RenderTexture rather than ScreenCapture.CaptureScreenshot.
// CaptureScreenshot reads the Game view backbuffer, which does not exist in a script-driven
// editor session, so it silently wrote nothing for all 12 waypoints. Rendering a camera into a
// RenderTexture we own works regardless of what the editor is showing, and pins the output at a
// fixed resolution instead of inheriting whatever size the Game view happens to be.
//
// Twelve canonical shots plus six close/rare-event diagnostics. Each shot logs its mean luminance
// so a white/black render is visible from the log alone --
// HDRP renders white in -batchmode, which is why this path is GUI-only (see GmProbe).
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public sealed class GmShotTour : GmSceneReviewTour
{
    // The waypoint yaws below are the web build's, and three.js's camera faces -Z at rotation.y=0
    // while Unity's faces +Z. Ported verbatim, every shot pointed 180 degrees backwards: the "porch"
    // shot photographed the coach house 80 units UP the drive while the mansion stood behind the
    // camera, which read for hours as "the mansion is missing". Convert at use, so the table below
    // stays directly comparable to the web source it came from.
    const float YawToUnity = 180f;

    static readonly GmReviewShot[] Waypoints =
    {
        new GmReviewShot("01-spawn",       new Vector3(0, 1.7f, 72),    0,   2),
        // estateCar sits at (-4.8, 76.5); the old pos/yaw here (-3,76 @ -125) pointed the camera
        // toward unity-yaw 55 deg (mostly +X), which faces AWAY from the car at unity-yaw ~-41 deg
        // (mostly -X) -- confirmed empty in tour-02-car.png. Pulled back to a clean 7.3-unit
        // establishing distance (the old spot was 1.9 units from the car, too tight to safely frame
        // a 4.6-unit-long object) and re-aimed: yaw/pitch solved from atan2 against the car's actual
        // design-data position, not guessed.
        new GmReviewShot("02-car",         new Vector3(0, 1.7f, 71),  -221.1f, 7),
        new GmReviewShot("03-gate",        new Vector3(0, 1.7f, 67),    0,   2),
        new GmReviewShot("04-lookback",    new Vector3(0, 1.7f, 55),    180, 2),
        new GmReviewShot("05-middrive",    new Vector3(0, 1.7f, 20),    0,   2),
        // The old x=7 camera stood 1.7m from the tapered cross-path endpoint. Even a perfectly flat
        // ribbon then filled the lower frame and read as a freestanding boulder. Preserve the same
        // entry axis with enough standoff to judge the cemetery room instead of one ground patch.
        new GmReviewShot("06-cem-path",    new Vector3(4.5f, 1.7f, 28.5f), -84, 2),
        // The authored cemetery settles into a 1.82m hollow. Review cameras must stand at player
        // eye height over terrain, not at the old flat-world y=1.7 plane floating above the plot.
        new GmReviewShot("07-cem-inside",  new Vector3(15, -0.12f, 26),  -55,  3),
        // Was (26,1.7,30) @ -88: that stands 3.1 units off estateChapelDoor (x=29.1) and 7 off the
        // chapel body (x=33), so the frame was one flat plank wall -- it proved the material was
        // textured and said nothing about the building. SM_Church is 11.5 tall; framing it whole
        // needs ~15 units of standoff (visible height = 2*d*tan(30deg)), and a three-quarter angle
        // reads as architecture where square-on reads as wallpaper. This vantage stands on the
        // cemetery side and sights through the east boundary's authored 25.6-34.2 opening, so the
        // low stone wall and graves become foreground instead of an occluder.
        new GmReviewShot("08-chapel",      new Vector3(19, -0.20f, 24), -113, -12),
        // Frame the garden from outside its south breach. At z=20.5 this camera was only a metre
        // from the cross-path edge, turning a floor mark into a full-width foreground cutout.
        new GmReviewShot("09-gdn-inside",  new Vector3(-10.8f, 1.7f, 18.0f), 153, 10),
        // The former camera at (-18,22) looked north-west past the well into the coach house, while
        // the garden's own shed sat behind it. Review the authored work chain from outside the east
        // boundary instead: shed/cart -> well -> fallen containers -> covered bed all fit within one
        // player-height frame, and the coach loading light no longer substitutes for garden proof.
        new GmReviewShot("10-well-shed",   new Vector3(-7.8f, 1.7f, 21.0f),  85f, 5f),
        new GmReviewShot("11-coach-yard",  new Vector3(-19.0f, 1.7f, 36.5f), 133.5f, 0),
        new GmReviewShot("12-porch",       new Vector3(0, 1.7f, -30),   0,   4),
        // Sight the disturbed grave from north of both path ribbons. The former z=30.5 position was
        // only two metres beyond the cross-path, so its flat edge occupied the lower-right third.
        new GmReviewShot("13-cem-detail",  new Vector3(16.2f, 0.25f, 32.3f), -20, 9),
        // Inspect the row families diagonally from outside the east fence. The former south-axis
        // view put every fine plant against the same ground value seven to fourteen metres away;
        // this position brings the nearest failed row to 4.5m, layers the other three behind it,
        // and remains more than 2.75m from both invisible path-guide bounds.
        new GmReviewShot("14-gdn-detail",  new Vector3(-5.8f, 1.7f, 25.3f), 110.5f, 4),
        new GmReviewShot("15-figure-far",  new Vector3(0f, 1.7f, 44f), 0, -6),
        new GmReviewShot("16-figure-gone", new Vector3(0f, 1.7f, 15f), 0, -10),
        // Exact same-camera transition pair. The old far/near shots proved two endpoint states but
        // hid whether removing the special glow produces a visible pop. These two frames hold every
        // camera variable constant near the real z=18 cutoff and change only the figure rig state.
        new GmReviewShot("17-figure-cutoff-on",  new Vector3(0f, 1.7f, 19.5f), 0, -7),
        new GmReviewShot("18-figure-cutoff-off", new Vector3(0f, 1.7f, 19.5f), 0, -7),
    };

    protected override IReadOnlyList<GmReviewShot> ReviewShots => Waypoints;
    public static IReadOnlyList<GmReviewShot> AuthoringShots => Waypoints;
    protected override float ReviewYawOffset => YawToUnity;
    protected override string ReviewLogTag => "GmShotTour";

    GmRareEvents rareEvents;
    Color32[] figureOnRegion;
    RectInt figureRegion;

    protected override void BeforeTour()
    {
        // Canonical evidence always measures the authored grade, never the operator's persisted
        // display preference. Normal play keeps that preference; this in-memory reset is not saved.
        FindAnyObjectByType<GmDisplayCalibration>()?.SetLevel(0, false);
        rareEvents = FindAnyObjectByType<GmRareEvents>();
        figureOnRegion = null;
        figureRegion = default;
        // Shots 01-14 always remain figure-free; later hooks force the exact diagnostic states.
        rareEvents?.HideFigureForReview();
    }

    protected override void BeforeShot(GmReviewShot shot)
    {
        if (shot.Name == "15-figure-far" || shot.Name == "17-figure-cutoff-on")
            rareEvents?.ForceFigureForReview();
        if (shot.Name == "16-figure-gone")
        {
            rareEvents?.ForceFigureForReview();
            rareEvents?.EvaluateFigureAtZ(shot.Position.z);
        }
        if (shot.Name == "18-figure-cutoff-off")
        {
            rareEvents?.ForceFigureForReview();
            // Read the threshold off the instance, not the estate const: the village remaps it, and
            // the const is tens of metres outside that scene's world.
            if (rareEvents != null) rareEvents.EvaluateFigureAtZ(rareEvents.FigureGoneBelow - 0.1f);
        }
    }

    protected override string ValidateCapturedShot(GmReviewShot shot, Color32[] pixels)
    {
        if (shot.Name == "08-chapel")
        {
            long sum = 0;
            int architecturalDetail = 0;
            foreach (Color32 pixel in pixels)
            {
                int value = (pixel.r + pixel.g + pixel.b) / 3;
                sum += value;
                if (value >= 10) architecturalDetail++;
            }
            float mean = sum / (float)pixels.Length;
            float readableFraction = architecturalDetail / (float)pixels.Length;
            if (mean < 9.5f || readableFraction < 0.24f)
                return $"chapel architecture collapses below the display-robust floor " +
                    $"(mean {mean:F1}, pixels>=10 {readableFraction:P1})";
        }
        if (shot.Name == "17-figure-cutoff-on")
        {
            GameObject rig = FindSceneRoot("WindowFigureRig");
            Camera camera = FindAnyObjectByType<GmPlayer>()?.GetComponentInChildren<Camera>() ?? Camera.main;
            if (rig == null || camera == null) return "figure comparison could not locate rig/camera";
            Renderer[] renderers = rig.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return "figure rig has no renderers to project";
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            figureRegion = ProjectBounds(camera, bounds, shotWidth, shotHeight, 5);
            if (figureRegion.width < 4 || figureRegion.height < 4)
                return $"projected figure region is only {figureRegion.width}x{figureRegion.height}px";
            figureOnRegion = CopyRegion(pixels, figureRegion, shotWidth);
            Debug.Log($"[GmShotTour] figure comparison ROI={figureRegion.x},{figureRegion.y} " +
                $"{figureRegion.width}x{figureRegion.height}");
            return null;
        }
        if (shot.Name != "18-figure-cutoff-off") return null;
        if (figureOnRegion == null || figureOnRegion.Length == 0)
            return "figure-on reference pixels were not captured";

        Color32[] off = CopyRegion(pixels, figureRegion, shotWidth);
        long absolute = 0;
        int materiallyChanged = 0;
        for (int i = 0; i < off.Length; i++)
        {
            int delta = Mathf.Abs(figureOnRegion[i].r - off[i].r) +
                Mathf.Abs(figureOnRegion[i].g - off[i].g) +
                Mathf.Abs(figureOnRegion[i].b - off[i].b);
            absolute += delta;
            if (delta >= 18) materiallyChanged++;
        }
        float meanChannelDelta = absolute / (float)(off.Length * 3);
        float changedFraction = materiallyChanged / (float)off.Length;
        Debug.Log($"[GmShotTour] figure on/off perceptual delta mean={meanChannelDelta:F2} " +
            $"changed={changedFraction:P1} ROI={figureRegion.width}x{figureRegion.height}");
        if (meanChannelDelta < 2.0f || changedFraction < 0.055f)
            return $"figure is not perceptible at cutoff (mean delta {meanChannelDelta:F2}, " +
                $"changed {changedFraction:P1})";
        if (meanChannelDelta > 72f || changedFraction > 0.78f)
            return $"figure transition is too dominant (mean delta {meanChannelDelta:F2}, " +
                $"changed {changedFraction:P1})";
        return null;
    }

    static RectInt ProjectBounds(Camera camera, Bounds bounds, int width, int height, int padding)
    {
        Vector3 min = bounds.min, max = bounds.max;
        float minX = 1f, minY = 1f, maxX = 0f, maxY = 0f;
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 world = new Vector3((corner & 1) == 0 ? min.x : max.x,
                (corner & 2) == 0 ? min.y : max.y, (corner & 4) == 0 ? min.z : max.z);
            Vector3 viewport = camera.WorldToViewportPoint(world);
            if (viewport.z <= 0f) continue;
            minX = Mathf.Min(minX, viewport.x); minY = Mathf.Min(minY, viewport.y);
            maxX = Mathf.Max(maxX, viewport.x); maxY = Mathf.Max(maxY, viewport.y);
        }
        int x0 = Mathf.Clamp(Mathf.FloorToInt(minX * width) - padding, 0, width - 1);
        int y0 = Mathf.Clamp(Mathf.FloorToInt(minY * height) - padding, 0, height - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt(maxX * width) + padding, x0 + 1, width);
        int y1 = Mathf.Clamp(Mathf.CeilToInt(maxY * height) + padding, y0 + 1, height);
        return new RectInt(x0, y0, x1 - x0, y1 - y0);
    }

    static Color32[] CopyRegion(Color32[] source, RectInt region, int sourceWidth)
    {
        var copy = new Color32[region.width * region.height];
        int destination = 0;
        for (int y = region.yMin; y < region.yMax; y++)
            for (int x = region.xMin; x < region.xMax; x++)
                copy[destination++] = source[y * sourceWidth + x];
        return copy;
    }

    static GameObject FindSceneRoot(string name)
    {
        foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == name) return root;
        return null;
    }
}

#if UNITY_EDITOR
public static class GmShotTourMenu
{
    const string WendHill = GmSceneCatalog.WendHillPath;

    [MenuItem("GamesMaster/Arm Shot Tour + Play")]
    public static void ArmAndPlay() => Run(WendHill);

    /// Tours whatever scene is already open. The waypoints are Wend Hill coordinates, so this is
    /// only meaningful in a scene that shares that layout.
    public static void ArmAndPlayCurrentScene() => Run(null);

    static void Run(string scenePath)
    {
        // Never tour "whatever the editor restored from last session" -- that silently produced a
        // Showcase-scene run when Wend Hill was the target.
        if (scenePath != null)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogError($"[GmShotTour] FAILED: {scenePath} does not exist -- build it first");
                EditorApplication.Exit(1);
                return;
            }
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        // SessionState arming only fires if something in the scene runs GmShotTour.Start. The builder
        // adds one; if it is missing, say so rather than entering play mode and letting the CLI sit
        // there until its 20-minute timeout wondering why no shots appeared.
        var tour = Object.FindAnyObjectByType<GmShotTour>();
        if (tour == null)
        {
            Debug.LogError("[GmShotTour] FAILED: no GmShotTour component in the scene — rebuild it first");
            EditorApplication.Exit(1);
            return;
        }

        // SessionState is the persisted belt, but set the in-memory component as suspenders. On
        // some windowed command-line launches SessionState was cleared during the play-mode domain
        // transition: play started successfully, GmShotTour.Start saw false, and the editor slept
        // forever with an empty Screens directory. A direct field assignment does not call
        // SetDirty/Undo or save the scene; the play-mode clone inherits it, while WendHill.unity on
        // disk remains runOnPlay: 0.
        UnityEditor.SessionState.SetBool(GmShotTour.ArmKey, true);
        UnityEditor.EditorPrefs.SetString(GmShotTour.ArmKey, System.DateTime.UtcNow.Ticks.ToString());
        tour.runOnPlay = true;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        Debug.Log($"[GmShotTour] armed in scene '{scene.name}', entering play mode");
        EditorApplication.isPlaying = true;
    }
}
#endif

