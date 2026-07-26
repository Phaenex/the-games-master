// Verifies that WendHill_Prologue.unity ON DISK is actually the committed night.
//
// The existing GmVillageStandaloneBuild guard checks that required ROOT OBJECTS are present, because
// the failure it was written for was a crashed builder leaving a raw copy of the purchased showcase
// with no estate and no player. That check does not transfer to this scene. GmWendBuilder's night is
// made of VALUES, not roots: it re-aims the pack's own sun, retints the pack's own volume profile and
// repoints the pack's own materials. A crashed run leaves a scene that has every root the purchased
// pack ships, opens fine, builds fine, and is broad daylight.
//
// So this audits the night itself. Nine checks, each one a thing that a stale, half-built or
// reverted-on-reload scene fails:
//
//   1. the player exists and has an eye to render from
//   2. the lighting census still matches what the pack shipped
//   3. the directional light is the moon and not the 2000 lux sun
//   4. the scene's volume points at OUR profile copy, not the purchased one
//   5. that profile carries a Fixed exposure at the committed EV
//   6. exactly one live camera, and it is the player's
//   7. nothing in the scene still uses an ungated emissive foliage material
//   8. the map edge is closed, by four walls and a configured catch height
//   9. exactly one live AudioListener, and it is the player's
//
// Check 7 is the one that catches the specific way this could silently regress. The foliage fix
// repoints scene renderers and terrain tree prototypes in memory; if either failed to serialize, the
// scene would reopen with self-lit grass, and because HDRP's automatic exposure re-meters around
// anything bright, a reverted scene can still produce a frame that looks broadly plausible. That is
// exactly how the bug survived four attempts.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public static class GmWendSceneContract
{
    const string LogTag = "GmWendContract";

    /// Throws with EVERY failure listed, not just the first. A build guard that reports one problem at
    /// a time turns one wrong run into several.
    public static void AssertBuilt()
    {
        List<string> failures = Audit();
        if (failures.Count > 0)
            throw new System.InvalidOperationException(
                $"{GmWendBuilder.ScenePath} is not the committed night. " +
                $"{failures.Count} check(s) failed:\n  - {string.Join("\n  - ", failures)}\n" +
                "Re-run GamesMaster/Wend/3. Build the committed night.");
    }

    [MenuItem("GamesMaster/Wend/Audit the saved scene")]
    public static void AuditMenu()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            GmWendBuilder.ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
        List<string> failures = Audit();
        if (failures.Count == 0) Debug.Log($"[{LogTag}] PASS: all 9 checks green");
        else Debug.LogError($"[{LogTag}] FAIL:\n  - {string.Join("\n  - ", failures)}");
        EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
    }

    /// Audits the currently open scene. Returns an empty list when it is the committed night.
    public static List<string> Audit()
    {
        // Pick up any bracket overrides FIRST, so the audit compares the scene against the recipe this
        // run actually used rather than against the defaults.
        //
        // Without this a bracket is unbuildable: the night build and the app build are separate Unity
        // processes, the statics reset in between, and the contract correctly reports a 4 lux moon
        // against a 1 lux recipe and refuses to build. That is the guard doing its job, so the fix
        // belongs here rather than in the guard's strictness.
        GmWendNight.ReadBisectOverrides();

        var failures = new List<string>();

        // 1. Player.
        GameObject player = GameObject.Find(GmWendBuilder.PlayerName);
        if (player == null) failures.Add($"no '{GmWendBuilder.PlayerName}' root");
        else if (player.transform.Find("PlayerCamera") == null)
            failures.Add($"'{GmWendBuilder.PlayerName}' has no PlayerCamera child to render from");

        // 2. Census. The pack's lighting must still be all there.
        GmWendBuilder.LightingCensus census = GmWendBuilder.TakeCensus();
        if (census.directional != 1 || census.practical != 24 || census.volumes != 30)
            failures.Add($"lighting census is {census}, expected 1 directional, 24 practical, 30 volumes");

        // 3. The moon. A daylight sun here means the night never applied, or applied and was lost.
        Light sun = Object.FindObjectsByType<Light>(FindObjectsInactive.Include)
            .FirstOrDefault(l => l.type == LightType.Directional);
        if (sun == null) failures.Add("no directional light");
        else
        {
            var hd = sun.GetComponent<HDAdditionalLightData>();
            if (hd == null) failures.Add($"directional '{sun.name}' has no HDAdditionalLightData");
            else if (hd.lightUnit != LightUnit.Lux)
                failures.Add($"moon is in {hd.lightUnit}, expected Lux");
            else if (Mathf.Abs(hd.intensity - GmWendNight.MoonLux) > 0.01f)
                failures.Add($"moon is {hd.intensity:0.##} lux, expected {GmWendNight.MoonLux:0.##}. " +
                             "Either the scene predates the current recipe or the recipe changed without a rebuild");
        }

        // 4 and 5. The owned profile, and the exposure on it.
        Volume host = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include)
            .FirstOrDefault(v => v.sharedProfile != null);
        if (host == null) failures.Add("no volume carries a profile");
        else
        {
            string path = AssetDatabase.GetAssetPath(host.sharedProfile);
            if (path != GmWendNight.OwnedProfilePath)
                failures.Add($"volume '{host.name}' uses profile {path}, expected {GmWendNight.OwnedProfilePath}. " +
                             "A purchased path here means the scene reverted to the pack's own look on reload");

            if (!host.sharedProfile.TryGet(out Exposure exposure))
                failures.Add($"profile {path} has no Exposure override, so the scene is on automatic exposure");
            else if (exposure.mode.value != ExposureMode.Fixed)
                failures.Add($"exposure mode is {exposure.mode.value}, expected Fixed");
            else if (Mathf.Abs(exposure.fixedExposure.value - GmWendNight.CommittedExposureEV) > 0.001f)
                failures.Add($"exposure is EV {exposure.fixedExposure.value:0.##}, " +
                             $"expected the committed {GmWendNight.CommittedExposureEV:0.##}");
        }

        // 6. Exactly one live camera, and it is the player's.
        //
        // This check exists because its absence shipped a wrong app. The original version of this audit
        // checked that PlayerCamera EXISTS, which it did, while the pack's 'Camera06' sat enabled at the
        // same depth and owned the built player's backbuffer. Presence is not the same as being the one
        // that renders, and no editor frame can tell the difference, because the review rigs render
        // cameras they create themselves.
        Camera[] live = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include)
            .Where(c => c.enabled && c.gameObject.activeInHierarchy)
            .ToArray();
        if (live.Length != 1)
            failures.Add($"{live.Length} live camera(s): {string.Join(", ", live.Select(c => $"'{c.name}' depth={c.depth}"))}. " +
                         "Expected exactly 1. The pack ships 30 cameras, and a second live one at equal " +
                         "depth can own the backbuffer in a built player while the editor looks fine");
        else if (live[0].transform.parent == null || live[0].transform.parent.name != GmWendBuilder.PlayerName)
            failures.Add($"the only live camera is '{live[0].name}', which is not under " +
                         $"'{GmWendBuilder.PlayerName}'");

        // 7. Foliage emission, by both routes.
        Material[] emitters = GmWendFoliage.RemainingEmitters();
        if (emitters.Length > 0)
            failures.Add($"{emitters.Length} foliage material(s) still emit: " +
                         $"{string.Join(", ", emitters.Select(m => m.name))}. " +
                         "The scene would light its own grass and re-meter the whole frame around it");

        // 8. The map edge.
        //
        // Audited by VALUE like everything else here, not by the root merely existing. A boundary root
        // with no walls under it, or a catch plane left at its default height of zero, is a scene that
        // opens fine and still lets the player walk off the world. Zero is called out specifically
        // because it is what an unconfigured component serializes as, and on this terrain it sits
        // above the ground rather than under it, so it would fire constantly instead of never.
        GameObject bounds = GameObject.Find(GmWendBounds.RootName);
        if (bounds == null)
            failures.Add($"no '{GmWendBounds.RootName}' root: the map edge is open and a walk has " +
                         "already fallen 690m off it");
        else
        {
            int walls = bounds.GetComponentsInChildren<BoxCollider>(true).Length;
            if (walls != 4) failures.Add($"boundary has {walls} wall(s), expected 4");

            var catcher = bounds.GetComponent<GmWendCatchPlane>();
            if (catcher == null)
                failures.Add($"'{GmWendBounds.RootName}' has no GmWendCatchPlane, so a gap in the " +
                             "walls would be an unrecoverable fall");
            else if (Mathf.Approximately(catcher.CatchHeight, 0f))
                failures.Add("catch height is 0, which is the unconfigured default rather than a " +
                             "height derived from the terrain");
        }

        // 9. Exactly one live ear, and it is the player's.
        //
        // The same failure as check 6, one component over. Disabling a Camera does not disable an
        // AudioListener sitting on the same GameObject, so the pack's 30 rigs can leave a second ear
        // enabled after the camera fix has run and reported success. Unity chooses one listener and
        // warns about the others, which means the mix would come from a showcase rig parked in the
        // village while every frame still rendered correctly from the player's eye.
        AudioListener[] ears = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include)
            .Where(a => a.enabled && a.gameObject.activeInHierarchy)
            .ToArray();
        if (ears.Length != 1)
            failures.Add($"{ears.Length} live AudioListener(s): " +
                         $"{string.Join(", ", ears.Select(a => $"'{a.name}'"))}. Expected exactly 1, " +
                         "or the scene mixes its audio from somewhere that is not the player's head");
        else if (ears[0].GetComponentInParent<Transform>() == null ||
                 ears[0].transform.parent == null ||
                 ears[0].transform.parent.name != GmWendBuilder.PlayerName)
            failures.Add($"the only live AudioListener is '{ears[0].name}', which is not under " +
                         $"'{GmWendBuilder.PlayerName}'");

        if (failures.Count == 0)
            Debug.Log($"[{LogTag}] 9/9 checks pass: player, census ({census}), moon " +
                      $"{GmWendNight.MoonLux} lux, owned profile, fixed EV " +
                      $"{GmWendNight.CommittedExposureEV}, one live camera, zero foliage emitters, " +
                      "closed map edge, one live ear");

        return failures;
    }
}
