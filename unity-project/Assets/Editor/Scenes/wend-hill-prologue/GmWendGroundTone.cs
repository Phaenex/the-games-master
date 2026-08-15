// Owns the purchased landscape material for the committed night.
//
// The drive's ground rendered as molten orange through every green gate. Four suspects were cleared
// by measurement first -- the lamps (2000K vs 2700K moved the frame cast 2.03 to 2.13, nothing), the
// TerrainLayer diffuse remaps (a near-neutral 0.72/0.68/0.60), the cliff tint (0.62/0.64/0.60) and
// the moon, which turned out to be a real but separate problem. Each was found by reading the code
// that writes it, and each was about a surface the camera was not actually looking at.
//
// Interrogating the scene instead found it in one pass. The route runs on a Terrain whose material
// is the pack's M_New_Land, on a custom Shader Graph (S_Landscape), and a custom graph is free to
// ignore TerrainLayer diffuse remaps entirely -- which is exactly why neutralising the layers
// changed nothing. Its mud tint is:
//
//     _Mud_Tint = (0.576, 0.380, 0.000)
//
// Blue is not low. Blue is ZERO. A surface with no blue channel cannot be lit cool by anything: the
// moon can be any brightness you like and the mud will still return pure orange, because there is
// nothing there for cool light to reflect. That is why raising the moon fixed the background and
// made the ground worse -- it lit a surface that can only answer in one colour.
//
// It also explains the binary pattern across the tour: mud frames measured 2.9, grass frames 0.7,
// because _Grass_Tint is a neutral 0.855 grey and only the mud is poisoned. The drive is mud.
//
// The pack asset is left untouched and every scene use is repointed to an owned copy, the same way
// GmWendRockTone handles the cliffs.
using System;
using UnityEditor;
using UnityEngine;

public static class GmWendGroundTone
{
    public const string SourceMaterialPath =
        "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Materials/M_New_Land.mat";
    public const string OwnedMaterialPath = "Assets/Scenes/WendHill_Night/Gm_Landscape_Night.mat";

    /// Wet earth at night. Warm in hue, because it is still mud, but with a real blue channel so
    /// moonlight has something to land on. Deliberately not grey: the goal is for the lamps to read
    /// as warm ACCENTS against it, which needs the ground to be able to go cool where they do not
    /// reach. Red:blue 1.4 against the original's infinity.
    public static readonly Color NightMud = new Color(0.230f, 0.196f, 0.165f, 1f);

    /// Grass is already neutral in the pack (0.855 grey). Brought down rather than re-hued, so the
    /// night is dark without inventing a colour the artist did not choose.
    public static readonly Color NightGrass = new Color(0.300f, 0.310f, 0.290f, 1f);

    public static int Apply()
    {
        Material source = AssetDatabase.LoadAssetAtPath<Material>(SourceMaterialPath);
        if (source == null) throw new InvalidOperationException($"missing landscape material {SourceMaterialPath}");

        Material owned = AssetDatabase.LoadAssetAtPath<Material>(OwnedMaterialPath);
        if (owned == null)
        {
            owned = new Material(source) { name = "Gm_Landscape_Night" };
            Configure(owned);
            AssetDatabase.CreateAsset(owned, OwnedMaterialPath);
        }
        else
        {
            owned.CopyPropertiesFromMaterial(source);
            Configure(owned);
            EditorUtility.SetDirty(owned);
        }

        int terrains = 0;
        foreach (Terrain terrain in UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include))
        {
            Material current = terrain.materialTemplate;
            if (current != owned &&
                (current == source || AssetDatabase.GetAssetPath(current) == SourceMaterialPath))
            {
                terrain.materialTemplate = owned;
                EditorUtility.SetDirty(terrain);
                terrains++;
            }
        }

        // Meshes can use it too -- the pack dresses some ground patches as geometry rather than
        // terrain, and a patch left on the original tint would be a saffron puddle in a fixed scene.
        int slots = 0;
        foreach (Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
        {
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != source && AssetDatabase.GetAssetPath(materials[i]) != SourceMaterialPath) continue;
                materials[i] = owned;
                changed = true;
                slots++;
            }
            if (!changed) continue;
            renderer.sharedMaterials = materials;
            EditorUtility.SetDirty(renderer);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[GmWendGroundTone] repointed {terrains} terrain(s) and {slots} mesh slot(s) to " +
                  $"{OwnedMaterialPath}; mud tint {Describe(source, "_Mud_Tint")} -> {Describe(owned, "_Mud_Tint")}");
        return terrains + slots;
    }

    static void Configure(Material material)
    {
        if (material.HasProperty("_Mud_Tint")) material.SetColor("_Mud_Tint", NightMud);
        if (material.HasProperty("_Grass_Tint")) material.SetColor("_Grass_Tint", NightGrass);
    }

    static string Describe(Material material, string property)
    {
        if (material == null || !material.HasProperty(property)) return "n/a";
        Color c = material.GetColor(property);
        return $"({c.r:0.000},{c.g:0.000},{c.b:0.000})";
    }
}
