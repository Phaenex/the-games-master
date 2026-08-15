// Assets/Editor/GmMansion.cs
// The gravyart "Haunted Victorian House" shell (CC-BY-4.0, credit in Exterior/license.txt).
// Kept out of GmEstateBuilderV2 because every constant here is specific to THIS model: the
// transform was measured against its native bbox, and Cube043 is its own sealed-door slab.
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

public static class GmMansion
{
    const string Fbx = "Assets/GamesMaster/Exterior/haunted_victorian_house.fbx";

    // Measured in the web build against the model's native bbox (x -8.04..7.96, y -1.43..15.69,
    // z -6.07..7.11). Do not re-derive: these are visually verified at 1.7-unit eye height.
    const float Scale = 1.0f;
    const float PosX  = 0.52f;   // recenters local Z onto the path at world x=0
    const float PosY  = 1.43f;   // lifts the foundation to ground
    // The web build uses -90 (native front is local +X; -90 brings it to world +Z). Our FBX arrives
    // rotated 180 about X relative to native, which moves which face ends up down the drive, so the
    // yaw that pairs with RotX below is +90, verified by looking: -90 showed a windowless back wall.
    const float RotY  = 90f;

    // Our Blender glTF->FBX conversion lands the model rotated 180 degrees about X relative to its
    // native orientation: local Y comes in as -15.69..1.43, the exact negation of the source glTF's
    // -1.43..15.69. Un-rotating here rather than in the exporter because the same script also feeds
    // the gate and car, and their correct orientation is not yet established -- one model's fix
    // should not silently re-orient the others. Revisit if the exporter is ever corrected.
    const float RotX  = 180f;

    public static GameObject Build(float mansionZ, Transform parent)
    {
        EnsureExternalMaterials();

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        if (prefab == null)
        {
            Debug.LogError($"[GmMansion] FAILED: {Fbx} missing — run scripts/gltf-to-fbx-blender.py first");
            return null;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = "Mansion (gravyart Haunted Victorian House)";
        go.transform.position = new Vector3(PosX * Scale, PosY * Scale, mansionZ);
        go.transform.rotation = Quaternion.Euler(RotX, RotY, 0);
        go.transform.localScale = Vector3.one * Scale;

        // Prefab source provenance disappears after a complete unpack. This marker is authored onto
        // the scene root so it survives duplicate + rename + unpack and keeps the exactly-one-mansion
        // audit enforceable in that state too.
        if (go.GetComponent<GmMansionIdentity>() == null) go.AddComponent<GmMansionIdentity>();

        SealTheDoors(go);
        VaryTheWindows(go);
        DressFacadeForNight(go);
        SeatOnGround(go, mansionZ);
        MakeSolid(go);
        return go;
    }

    /// Gives the house collision. Until 2026-08-15 it had none at all -- the FBX imports with
    /// addColliders: 0, nothing added one, and BuildManor's footprint pass disables any collider it
    /// finds in the area -- so a player could walk to the porch, keep walking, and pass through the
    /// front wall, the interior, and out the back into the field. GmThreshold went on printing "The
    /// doors did not open" from route-distance projection while they stood in the drawing room.
    ///
    /// That is not a cosmetic bug. Threshold Refusal is the whole opening: the house's own front
    /// doors never open and the player is taken by the ninth bell rather than admitted. A house you
    /// can stroll through defeats the premise in about four seconds.
    ///
    /// Mesh colliders rather than a box shell, deliberately. A box would need hand-placed extents
    /// and would either block the porch the player must reach or leave a gap somewhere along a
    /// facade nobody measured; the mesh is already the correct shape, and it keeps the porch steps
    /// walkable without anyone guessing coordinates. These are static and non-convex, so the cost is
    /// bake-time rather than per-frame, but the triangle total is logged because this scene is
    /// carrying a perf regression (LANE A1) and a number nobody printed is a number nobody can weigh.
    static void MakeSolid(GameObject go)
    {
        var filters = go.GetComponentsInChildren<MeshFilter>(true);
        int added = 0;
        long triangles = 0;
        foreach (var filter in filters)
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null) continue;
            // A renderer that was hidden to seal the doorway must still stop the player -- that slab
            // is exactly where someone would otherwise walk in.
            if (filter.GetComponent<MeshCollider>() != null) continue;
            var collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = false;
            added++;
            triangles += mesh.triangles.Length / 3;
        }

        if (added == 0)
        {
            Debug.LogError("[GmMansion] FAILED: no mesh colliders added — the house is still walk-through");
            return;
        }
        Debug.Log($"[GmMansion] solid: {added} mesh collider(s), {triangles} triangle(s) of static collision");
    }

    /// The source house is calibrated for its daylight showcase and its pale plaster catches the
    /// moon as chalk-white. Keep every texture and the deliberately varied window emission, but
    /// pull the opaque shell into the same low-reflectance night palette as the estate buildings.
    static void DressFacadeForNight(GameObject root)
    {
        var cache = new Dictionary<Material, Material>();
        int rendererCount = 0;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                var source = materials[i];
                if (source == null || source.name.StartsWith("Gm_Window", StringComparison.OrdinalIgnoreCase)) continue;
                if (!cache.TryGetValue(source, out var dressed))
                {
                    dressed = new Material(source) { name = source.name + "_WendHillNight" };
                    if (dressed.HasProperty("_BaseColor"))
                    {
                        Color c = dressed.GetColor("_BaseColor");
                        dressed.SetColor("_BaseColor", new Color(c.r * 0.72f, c.g * 0.67f, c.b * 0.62f, c.a));
                    }
                    if (dressed.HasProperty("_Smoothness"))
                        dressed.SetFloat("_Smoothness", Mathf.Min(0.18f, dressed.GetFloat("_Smoothness")));
                    cache[source] = dressed;
                }
                materials[i] = dressed;
                changed = true;
            }
            if (changed)
            {
                renderer.sharedMaterials = materials;
                rendererCount++;
            }
        }
        Debug.Log($"[GmMansion] night facade: {cache.Count} opaque material(s) across {rendererCount} renderer(s)");
    }

    /// The FBX ships its materials EMBEDDED as import sub-assets, not standalone .mat files. A prior
    /// fix here mutated those embedded materials in place (SetColor + SetDirty + AssetDatabase.
    /// SaveAssets) and logged "warmed N material(s), saved to disk" -- true in the sense that the
    /// call succeeded and the log line printed, but false as evidence of anything durable. Verified
    /// by a same-session diagnostic (GmMansionDiag, since removed) that opened the saved scene in a
    /// SEPARATE Unity process and read every "Glass" material's _EmissiveColor back as (0,0,0,0):
    /// script edits to a model's embedded sub-asset properties do not survive an import-artifact
    /// reload the way edits to a real, independently-serialized asset do. Same family of bug as the
    /// VolumeProfile trap on BuildLightingAndSky's AddOverride<T> -- an in-memory-only mutation that
    /// looks correct in the process that made it and evaporates in the next one. The fix there was
    /// AssetDatabase.AddObjectToAsset; the fix here is the same idea aimed at materials instead of
    /// volume overrides: flip the importer's material storage to real, independently-serialized
    /// files. The first attempt at this reached for `ModelImporter.ExtractMaterials(path)` by analogy
    /// with GmModelImportSettings' texture-extraction fix -- that method does not exist on this Unity
    /// version (confirmed by reflecting typeof(ModelImporter) with a throwaway probe: no method of
    /// that name, only the materialLocation/materialImportMode property pair). Flipping
    /// materialLocation to External and reimporting is what actually works here: it writes real .mat
    /// files into a "Materials" folder next to the model and remaps the import automatically (the
    /// property is flagged obsolete in this Editor version's tooltip -- "no longer supported" refers
    /// to a future removal, not current non-function; confirmed working by reading the resulting
    /// Glass.mat file's serialized _EmissiveColor directly off disk). Idempotent -- checks the current
    /// location before touching the importer, so repeated rebuilds don't re-trigger a reimport.
    static void EnsureExternalMaterials()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(Fbx);
        if (importer == null)
        {
            Debug.LogError($"[GmMansion] FAILED: no ModelImporter at {Fbx} — cannot extract materials");
            return;
        }
        if (importer.materialLocation == ModelImporterMaterialLocation.External)
        {
            Debug.Log("[GmMansion] materials already external, skipping reimport");
            return;
        }

        importer.materialLocation = ModelImporterMaterialLocation.External;
        importer.SaveAndReimport();
        AssetDatabase.Refresh();

        int n = AssetDatabase.FindAssets("t:material", new[] { "Assets/GamesMaster/Exterior" }).Length;
        Debug.Log($"[GmMansion] flipped materials to external, {n} material asset(s) now on disk under Assets/GamesMaster/Exterior");
        if (n == 0)
            Debug.LogWarning("[GmMansion] materialLocation=External reimport produced 0 external material assets — extraction did not work");
    }

    /// PosY = 1.43 assumes the model's foundation sits at local y = -1.43, which is true of the
    /// SOURCE glTF but not of the FBX our Blender conversion produces: it lands with local y running
    /// -15.69..1.43, the exact negation, so the house buries itself and only the roof clears the
    /// ground. Rather than hardcode a second magic offset that silently rots the next time the
    /// exporter changes, seat it on measured bounds. Self-correcting: if the foundation is already
    /// at ground level this is a no-op.
    static void SeatOnGround(GameObject go, float mansionZ)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) { Debug.LogError("[GmMansion] FAILED: no renderers — cannot seat on ground"); return; }
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);

        float lift = -b.min.y;
        go.transform.position += Vector3.up * lift;
        go.transform.position = new Vector3(go.transform.position.x, go.transform.position.y, mansionZ);

        // Re-measure rather than trust the arithmetic.
        b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        Debug.Log($"[GmMansion] seated: lift={lift:F2} bounds size={b.size} min.y={b.min.y:F2} (want ~0)");
        if (b.size.y < 10f)
            Debug.LogError($"[GmMansion] FAILED: height {b.size.y:F2} — expected ~17. Import scale is wrong, not the seating.");
    }

    /// Threshold Refusal is locked canon: the front doors never open. Cube043 is the model's own
    /// door slab; hiding it means there is no leaf to swing and no "how I got inside" to explain.
    ///
    /// The real node names carry a literal dot: "Cube.043", "Cube.043_Columns_0",
    /// "Cube.043_Columns2_0" (verified directly off the Unity-imported FBX; the source glTF has the
    /// dot too). Three.js's GLTFLoader silently sanitizes node names via
    /// PropertyBinding.sanitizeNodeName(), which strips the dot, so the web build's "Cube043" check
    /// only ever matched there. Unity's FBX importer preserves node names as authored, so a
    /// dot-stripped comparison is required here or this selector matches zero objects. Normalise by
    /// stripping dots on both sides before comparing so it works regardless of which form a future
    /// re-export produces.
    static void SealTheDoors(GameObject root)
    {
        int hidden = 0;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            string flat = t.name.Replace(".", "");
            if (flat != "Cube043" && !flat.StartsWith("Cube043_")) continue;
            var r = t.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
            else t.gameObject.SetActive(false);
            hidden++;
        }
        Debug.Log($"[GmMansion] sealed door slab: {hidden} Cube043 object(s) hidden");
    }

    /// "A house this size lit up like a birthday, and not one sound coming off it." The window
    /// materials arrive as dark PBR with zero emissive and read as a dead box from the gate. Warm
    /// ONLY the windows -- warming the facade bleaches it to limestone, the "clean marble" tell.
    ///
    /// Three bugs have lived here across two sessions, found by reading the actual imported material
    /// set and actual on-disk state rather than trusting a method name or a log line:
    ///
    /// 1. NAME FILTER matched the wrong material. The model ships two distinct materials that both
    ///    contain "window": "Glass" (the transparent panes -- the real target) and "Windows" (a
    ///    baked drapery/curtain texture behind the panes, verified by opening
    ///    Textures/Windows_baseColor.png -- it's a maroon fabric weave, not glass). Matching
    ///    ".Contains(\"window\")" warmed the curtains along with the panes. Only "Glass" is a window.
    ///
    /// 2. THE KEYWORD WAS A RED HERRING. HDRP's Lit shader adds _EmissiveColor to the surface
    ///    unconditionally (see LitBuiltinData.hlsl GetEmissiveColor) -- no keyword gates emission
    ///    itself. "_EMISSIVE_COLOR_MAP" only toggles whether an *emissive map texture* gets sampled
    ///    and multiplied in; its default fallback (Lit.shader: `_EmissiveColorMap(...) = "white" {}`)
    ///    makes enabling it here a no-op, not a fix. Removed rather than left as misleading dead code.
    ///
    /// 3. A prior fix (session 2) diagnosed the failure as a missing AssetDatabase.SaveAssets() call
    ///    and added one, plus a log line claiming "warmed N material(s), saved to disk". The call
    ///    succeeded and the line was technically true -- and it was STILL wrong, because SaveAssets()
    ///    cannot make an embedded FBX sub-asset's property edits outlive the process that made them.
    ///    Proven wrong empirically (session 3) with a throwaway diagnostic that opened the saved scene
    ///    in a FRESH Unity process and read every "Glass" material's _EmissiveColor back as
    ///    (0,0,0,0) -- exactly its unmodified import default. `EnsureExternalMaterials()` above is
    ///    the actual fix: extract the materials to real, independently-serialized .mat assets BEFORE
    ///    mutating them, via the same ModelImporter API this project already uses (see
    ///    GmModelImportSettings) for the parallel embedded-texture problem. Only a genuinely
    ///    persistent asset makes SetDirty + SaveAssets mean anything.
    static void VaryTheWindows(GameObject root)
    {
        var warm = new Color(1f, 0.38f, 0.07f);
        var slots = new List<(Renderer renderer, int materialIndex, Material source, uint order)>();
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            var materials = r.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                var m = materials[i];
                if (m == null || !m.name.ToLower().Contains("glass")) continue;
                if (!m.HasProperty("_EmissiveColor")) continue;
                slots.Add((r, i, m, StableHash(TransformPath(r.transform) + ":" + i)));
            }
        }

        slots.Sort((a, b) => a.order.CompareTo(b.order));
        int darkCount = slots.Count == 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(slots.Count * 0.15f));
        int dimCount = Mathf.RoundToInt(slots.Count * 0.35f);
        int dark = 0, dim = 0, lit = 0;
        for (int s = 0; s < slots.Count; s++)
        {
            string state;
            float emission;
            if (s < darkCount) { state = "Dark"; emission = 0f; dark++; }
            else if (s < darkCount + dimCount) { state = "Dim"; emission = 0.055f; dim++; }
            else { state = "Lit"; emission = 0.16f; lit++; }

            var slot = slots[s];
            var materials = slot.renderer.sharedMaterials;
            materials[slot.materialIndex] = WindowVariant(slot.source, state, warm * emission);
            slot.renderer.sharedMaterials = materials;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[GmMansion] varied {slots.Count} glass slot(s): {dark} dark, {dim} dim, {lit} lit");
        if (slots.Count == 0)
            Debug.LogWarning("[GmMansion] no glass material matched — the house will read as a dead box from the gate");
    }

    static Material WindowVariant(Material source, string state, Color emissive)
    {
        const string dir = "Assets/Scenes/MansionWindows";
        EnsureFolder(dir);
        string safe = source.name.Replace(" ", "_").Replace(".", "_");
        string path = $"{dir}/Gm_Window{state}_{safe}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(source) { name = $"Gm_Window{state}_{safe}" };
            AssetDatabase.CreateAsset(material, path);
        }
        else material.CopyPropertiesFromMaterial(source);

        material.name = $"Gm_Window{state}_{safe}";
        if (material.HasProperty("_EmissiveColor")) material.SetColor("_EmissiveColor", emissive);
        if (material.HasProperty("_UseEmissiveIntensity")) material.SetFloat("_UseEmissiveIntensity", 0f);
        material.globalIlluminationFlags = state == "Dark"
            ? MaterialGlobalIlluminationFlags.EmissiveIsBlack
            : MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(material);
        return material;
    }

    /// Canonical one-in-three figure rig. It owns no light or replacement pane: only the occluded
    /// human shape enters and leaves the already-authored facade.
    public static GameObject BuildWindowFigureRig(GameObject mansion)
    {
        if (mansion == null)
        {
            Debug.LogError("[GmMansion] cannot seat window figure: mansion build failed");
            return null;
        }

        // Seat the silhouette against a window that the facade already owns. Renderer bounds are
        // used here instead of a duplicate glowing pane, so a future mansion transform/material
        // pass cannot leave the event floating beside the architecture. Prefer an upper-floor lit
        // slot left of centre: it remains readable from the drive without becoming a centred face.
        Renderer chosenPane = null;
        float bestScore = float.PositiveInfinity;
        foreach (var renderer in mansion.GetComponentsInChildren<Renderer>(true))
        {
            bool isLitPane = false;
            foreach (var material in renderer.sharedMaterials)
            {
                if (material != null && material.name.StartsWith("Gm_WindowLit", StringComparison.OrdinalIgnoreCase))
                {
                    isLitPane = true;
                    break;
                }
            }
            if (!isLitPane || renderer.bounds.center.y < 6f) continue;

            Vector3 c = renderer.bounds.center;
            float driveFacadeZ = mansion.transform.position.z + 5.9f;
            float score = Mathf.Abs(c.x + 2.2f) + Mathf.Abs(c.y - 10.5f) * 0.35f
                + Mathf.Abs(renderer.bounds.max.z - driveFacadeZ) * 4f;
            if (score >= bestScore) continue;
            chosenPane = renderer;
            bestScore = score;
        }

        Vector3 seat = chosenPane != null
            // This FBX combines several glass panes into one renderer. Its aggregate bounds centre
            // lands on a stone mullion, not inside a pane. The lit upper-right opening is 0.46m left
            // of that aggregate centre; keep Y/Z derived from the actual facade so the event still
            // follows a future mansion transform or rescale.
            ? new Vector3(chosenPane.bounds.center.x - 0.46f, chosenPane.bounds.center.y,
                chosenPane.bounds.max.z + 0.22f)
            : new Vector3(-2.86f, 10.5f, mansion.transform.position.z + 6.05f);
        var root = new GameObject("WindowFigureRig");
        root.transform.position = seat;

        // A shoulder-and-head read must survive the real 80m cutoff camera. It remains matte black,
        // owns no glow and sits slightly off-centre, but no longer hides almost entirely behind the
        // pane mullion. At first glance it can still be curtain/furniture; comparison must reveal a
        // human proportion without requiring a pixel diff.
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "FigureBody";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, -0.23f, 0.035f);
        body.transform.localScale = new Vector3(0.54f, 0.84f, 0.14f);
        Renderer bodyRenderer = body.GetComponent<Renderer>();
        bodyRenderer.sharedMaterial = FigureMaterial(false);
        KeepDistantFigureRenderable(bodyRenderer);
        UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());

        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "FigureHead";
        head.transform.SetParent(root.transform, false);
        head.transform.localPosition = new Vector3(0f, 0.73f, 0.04f);
        head.transform.localScale = Vector3.one * 0.40f;
        Renderer headRenderer = head.GetComponent<Renderer>();
        headRenderer.sharedMaterial = FigureMaterial(false);
        KeepDistantFigureRenderable(headRenderer);
        UnityEngine.Object.DestroyImmediate(head.GetComponent<Collider>());

        var shoulders = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shoulders.name = "FigureShoulders";
        shoulders.transform.SetParent(root.transform, false);
        shoulders.transform.localPosition = new Vector3(0f, 0.35f, 0.045f);
        shoulders.transform.localScale = new Vector3(0.76f, 0.25f, 0.15f);
        Renderer shoulderRenderer = shoulders.GetComponent<Renderer>();
        shoulderRenderer.sharedMaterial = FigureMaterial(false);
        KeepDistantFigureRenderable(shoulderRenderer);
        UnityEngine.Object.DestroyImmediate(shoulders.GetComponent<Collider>());

        root.SetActive(false);
        string pane = chosenPane != null ? TransformPath(chosenPane.transform) : "measured facade fallback";
        string paneBounds = chosenPane != null
            ? $"center={chosenPane.bounds.center} size={chosenPane.bounds.size}"
            : "no pane bounds";
        Debug.Log($"[GmMansion] window figure rig seated at {root.transform.position} against {pane} " +
            $"({paneBounds}), inactive until armed");
        return root;
    }

    static void KeepDistantFigureRenderable(Renderer renderer)
    {
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.allowOcclusionWhenDynamic = false;
        // Unity 6/HDRP enables per-renderer small-mesh culling on new primitives. At the authored
        // 80m cutoff camera that silently removed every figure renderer while activeSelf remained
        // true, so behavioral tests passed and the on/off images were identical. This serialized
        // editor property is the actual switch used by HDRP's culling path.
        var serialized = new SerializedObject(renderer);
        SerializedProperty smallMeshCulling = serialized.FindProperty("m_SmallMeshCulling");
        if (smallMeshCulling != null) smallMeshCulling.boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static Material FigureMaterial(bool glow)
    {
        const string dir = "Assets/Scenes/MansionWindows";
        EnsureFolder(dir);
        string path = $"{dir}/Gm_Figure{(glow ? "Glow" : "Silhouette")}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            var shader = Shader.Find("HDRP/Lit");
            material = new Material(shader) { name = glow ? "Gm_FigureGlow" : "Gm_FigureSilhouette" };
            AssetDatabase.CreateAsset(material, path);
        }
        // Match an ordinary warm pane. The event earns its visibility through silhouette contrast,
        // placement and duration, never by making this one window the brightest object on the house.
        // A trace of warm reflected value keeps the silhouette renderable against the lit pane
        // without turning it into a coloured figure. It remains non-emissive, rough and nearly
        // black; the deterministic on/off ROI gate below the tour now enforces the actual read.
        Color baseColor = glow ? new Color(0.12f, 0.045f, 0.010f) : new Color(0.018f, 0.004f, 0.002f);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", baseColor);
        if (material.HasProperty("_EmissiveColor"))
            material.SetColor("_EmissiveColor", glow ? new Color(0.16f, 0.061f, 0.011f) : Color.black);
        if (material.HasProperty("_UseEmissiveIntensity")) material.SetFloat("_UseEmissiveIntensity", 0f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", glow ? 0.12f : 0f);
        if (material.HasProperty("_DoubleSidedEnable")) material.SetFloat("_DoubleSidedEnable", 1f);
        if (material.HasProperty("_TransmissionEnable")) material.SetFloat("_TransmissionEnable", 0f);
        material.EnableKeyword("_DOUBLESIDED_ON");
        EditorUtility.SetDirty(material);
        return material;
    }

    static void EnsureFolder(string dir)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Scenes", "MansionWindows");
    }

    static string TransformPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
        return path;
    }

    static uint StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in value) { hash ^= c; hash *= 16777619; }
            return hash;
        }
    }
}

