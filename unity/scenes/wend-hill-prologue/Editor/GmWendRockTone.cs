// Owns the purchased moss-blend cliff material for the committed night.
//
// The 150m walk identified the cliff at route 131m, 6.8m from the path, as a pale sheet. The
// environment report traced every nearby cliff and rock to one shared purchased material whose rock
// tint is yellow-green. The pack asset remains untouched; all scene uses are repointed to this copy.
using System;
using UnityEditor;
using UnityEngine;

public static class GmWendRockTone
{
    public const string SourceMaterialPath =
        "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Materials/M_MossyRock.mat";
    public const string OwnedMaterialPath = "Assets/Scenes/WendHill_Night/Gm_MossyRock_Night.mat";

    public static int Apply()
    {
        Material source = AssetDatabase.LoadAssetAtPath<Material>(SourceMaterialPath);
        if (source == null) throw new InvalidOperationException($"missing cliff material {SourceMaterialPath}");
        Material owned = AssetDatabase.LoadAssetAtPath<Material>(OwnedMaterialPath);
        if (owned == null)
        {
            owned = MakeNightMaterial(source);
            AssetDatabase.CreateAsset(owned, OwnedMaterialPath);
        }
        else
        {
            owned.CopyPropertiesFromMaterial(source);
            Configure(owned);
            EditorUtility.SetDirty(owned);
        }

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
        Debug.Log($"[GmWendRockTone] repointed {slots} cliff/rock material slot(s) to {OwnedMaterialPath}");
        return slots;
    }

    public static Material MakeNightMaterial(Material source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        var material = new Material(source) { name = "Gm_MossyRock_Night" };
        Configure(material);
        return material;
    }

    static void Configure(Material material)
    {
        if (material.HasProperty("_Rock_Tint"))
            material.SetColor("_Rock_Tint", new Color(0.62f, 0.64f, 0.60f, 1f));
        if (material.HasProperty("_Moss_Tint"))
            material.SetColor("_Moss_Tint", new Color(0.48f, 0.51f, 0.45f, 1f));
        if (material.HasProperty("_Rock_Brightness")) material.SetFloat("_Rock_Brightness", 0.14f);
        if (material.HasProperty("_Moss_Brightness")) material.SetFloat("_Moss_Brightness", 0.22f);
        if (material.HasProperty("_TransmissionEnable")) material.SetFloat("_TransmissionEnable", 0f);
    }
}
