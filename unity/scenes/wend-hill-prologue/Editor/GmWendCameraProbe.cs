// Why the built player does not look like the editor.
//
// Every review frame in this project was rendered by a camera the harness CREATED and pointed by hand.
// The built player renders with whatever camera the SCENE contains. Those are not the same thing, and
// the pack ships its own camera rig: 29 of its 30 volumes are empty leftovers from it. So the player
// could be rendering through a camera, and a volume stack, that no editor frame ever used.
//
// Measured from a screenshot of the running app: whole-frame luma 0.477 against the editor's 0.026 to
// 0.074 at the committed EV, with grass clipping at 255,255,255. The sky is still dark, so the night
// fog and sky retint ARE applied. That combination points at the Exposure override specifically not
// reaching the camera, rather than the whole profile being ignored.
//
// Read-only. Reports every camera, what it renders, and the volume plumbing that decides which
// overrides it actually receives.
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public static class GmWendCameraProbe
{
    const string LogTag = "GmWendCamProbe";

    [MenuItem("GamesMaster/Wend/Probe cameras and volume plumbing")]
    public static void Run()
    {
        EditorSceneManager.OpenScene(GmWendBuilder.ScenePath, OpenSceneMode.Single);
        var sb = new StringBuilder();

        Camera[] cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
        sb.AppendLine($"{cams.Length} camera(s) in the scene:");
        foreach (Camera c in cams.OrderByDescending(c => c.depth))
        {
            var hd = c.GetComponent<HDAdditionalCameraData>();
            sb.AppendLine($"  '{c.name}' tag={c.tag} enabled={c.enabled} activeInHierarchy={c.gameObject.activeInHierarchy} " +
                          $"depth={c.depth} layer={LayerMask.LayerToName(c.gameObject.layer)}({c.gameObject.layer})");
            if (hd == null) { sb.AppendLine("      NO HDAdditionalCameraData"); continue; }
            sb.AppendLine($"      volumeLayerMask={hd.volumeLayerMask.value} " +
                          $"anchorOverride={(hd.volumeAnchorOverride != null ? hd.volumeAnchorOverride.name : "none")} " +
                          $"clearMode={hd.clearColorMode} antialiasing={hd.antialiasing} " +
                          $"customFrameSettings={hd.customRenderingSettings}");

            // The exposure override only reaches this camera if the volume carrying it is on a layer
            // this mask includes. A mask that excludes it is silent: the frame just renders on
            // automatic exposure and looks like a plausible daylight.
            foreach (Volume v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include)
                         .Where(v => v.sharedProfile != null))
            {
                bool inMask = (hd.volumeLayerMask.value & (1 << v.gameObject.layer)) != 0;
                sb.AppendLine($"      profile volume '{v.name}' layer=" +
                              $"{LayerMask.LayerToName(v.gameObject.layer)}({v.gameObject.layer}) " +
                              $"global={v.isGlobal} priority={v.priority} weight={v.weight} " +
                              $"enabled={v.enabled} activeInHierarchy={v.gameObject.activeInHierarchy} " +
                              $"IN THIS CAMERA'S MASK={inMask}");
            }
        }
        Debug.Log($"[{LogTag}] CAMERAS\n{sb}");

        // What the Exposure override actually says on the asset, and whether it would survive a build.
        sb.Clear();
        Volume host = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include)
            .FirstOrDefault(v => v.sharedProfile != null);
        if (host == null) sb.AppendLine("no volume carries a profile");
        else
        {
            VolumeProfile p = host.sharedProfile;
            sb.AppendLine($"profile {AssetDatabase.GetAssetPath(p)} has {p.components.Count} component(s):");
            foreach (VolumeComponent comp in p.components)
                sb.AppendLine($"  {comp.GetType().Name} active={comp.active} hideFlags={comp.hideFlags}");

            if (p.TryGet(out Exposure e))
                sb.AppendLine($"Exposure: active={e.active} hideFlags={e.hideFlags} " +
                              $"mode={e.mode.value}/override={e.mode.overrideState} " +
                              $"fixedExposure={e.fixedExposure.value}/override={e.fixedExposure.overrideState}");
            else sb.AppendLine("Exposure: ABSENT");
        }
        Debug.Log($"[{LogTag}] PROFILE\n{sb}");

        EditorApplication.Exit(0);
    }
}
