using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public enum GmEnvironmentRole
{
    Canopy,
    WetCanopy,
    Snag,
    Herb,
    Shrub,
    GroundCover,
    LeafLitter,
    Debris,
    Geology,
}

public readonly struct GmEnvironmentSourceSpec
{
    public readonly string Path;
    public readonly GmEnvironmentRole Role;
    public readonly float MinPrimarySize;
    public readonly float MaxPrimarySize;
    public readonly float Weight;
    public readonly string Silhouette;
    public readonly string FamilyId;

    public GmEnvironmentSourceSpec(string path, GmEnvironmentRole role, float minPrimarySize,
        float maxPrimarySize, float weight, string silhouette, string familyId = null)
    {
        Path = path;
        Role = role;
        MinPrimarySize = minPrimarySize;
        MaxPrimarySize = maxPrimarySize;
        Weight = weight;
        Silhouette = silhouette;
        FamilyId = string.IsNullOrEmpty(familyId) ? silhouette : familyId;
    }
}

/// <summary>
/// Exact, calibrated inventory shared by the environment lab and future estate composers. Asset
/// names are not semantics: every admitted source has an explicit role, target range, selection
/// weight and silhouette class. Validation is read-only and never changes a purchased asset.
/// </summary>
public static class GmOwnedEnvironmentGrammar
{
    const string Witch = "Assets/LeartesStudios/WitchVillage/HDRP/Art/Prefabs/";
    const string Haunted = "Assets/LeartesStudios/HauntedVillage/Art/Prefabs/";
    const string Abandoned = "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Prefabs/";

    static readonly GmEnvironmentSourceSpec[] Catalog = {
        new("Assets/Scenes/Tests/EnvironmentCypressEstate.prefab", GmEnvironmentRole.WetCanopy,
            4.5f, 14.5f, 1.00f, "high_sparse_conifer", "bald_cypress"),
        // Project-owned material variants preserve the source mesh/LODs while calibrating its
        // neon donor palette and enabling restrained wind. They remain one topology family and
        // must never be counted as three kinds of tree.
        new("Assets/Scenes/Tests/EnvironmentPineEstateCool.prefab", GmEnvironmentRole.Canopy,
            4.3f, 14.5f, 0.38f, "dense_conifer", "regular_pine"),
        new("Assets/Scenes/Tests/EnvironmentPineEstateDry.prefab", GmEnvironmentRole.Canopy,
            4.3f, 14.5f, 0.34f, "dense_conifer", "regular_pine"),
        new("Assets/Scenes/Tests/EnvironmentPineEstateShade.prefab", GmEnvironmentRole.Canopy,
            4.3f, 14.5f, 0.28f, "dense_conifer", "regular_pine"),
        // Mesh-warped RegularPine derivatives failed the player-capture identity test: the eye
        // still reads the donor topology. Generated assets remain inspectable in Tests, but are
        // quarantined from the automatic catalog and cannot masquerade as new families.
        // Abandoned Village Tree_01_Foliage and Tree_02 Foilage pass geometry checks but render as
        // luminous hanging strings/dark cards at player distance. Turntable + route proof override
        // their names: both remain quarantined outside this automatic catalog.
        // Neutral turntable proof showed that the Haunted "tree" family is leafless. It belongs
        // to snag structure, never the canopy pool.
        new("Assets/Scenes/Tests/EnvironmentSnagGnarled.prefab", GmEnvironmentRole.Snag,
            8.0f, 11.5f, 0.35f, "gnarled_snag"),
        new("Assets/Scenes/Tests/EnvironmentSnagTall.prefab", GmEnvironmentRole.Snag,
            10.0f, 15.0f, 0.25f, "tall_branch_snag"),
        new("Assets/Scenes/Tests/EnvironmentSnagSilver.prefab", GmEnvironmentRole.Snag,
            6.5f, 9.5f, 0.20f, "silver_branch_snag"),
        new("Assets/Scenes/Tests/EnvironmentSnagThin.prefab", GmEnvironmentRole.Snag,
            4.0f, 5.5f, 0.12f, "thin_snag"),
        new("Assets/Scenes/Tests/EnvironmentSnagForked.prefab", GmEnvironmentRole.Snag,
            3.5f, 5.0f, 0.08f, "forked_snag"),

        new(Witch + "SM_TallGrass_Clump1.prefab", GmEnvironmentRole.Herb, 0.20f, 0.52f, 0.02f, "broad_tuft"),
        new(Witch + "SM_TallGrass_Clump2.prefab", GmEnvironmentRole.Herb, 0.18f, 0.48f, 0.02f, "broad_tuft"),
        new(Witch + "SM_TallGrass_Clump3.prefab", GmEnvironmentRole.Herb, 0.22f, 0.56f, 0.02f, "broad_tuft"),
        new(Witch + "SM_TallGrass_Clump4.prefab", GmEnvironmentRole.Herb, 0.18f, 0.46f, 0.02f, "narrow_tuft"),
        new(Haunted + "SM_Grass_01.prefab", GmEnvironmentRole.Herb, 0.16f, 0.42f, 0.07f, "fine_grass"),
        new(Haunted + "SM_Grass_03.prefab", GmEnvironmentRole.Herb, 0.16f, 0.44f, 0.07f, "fine_grass"),
        new(Haunted + "SM_Grass_04.prefab", GmEnvironmentRole.Herb, 0.18f, 0.46f, 0.07f, "fine_grass"),
        new(Haunted + "SM_Grass_05.prefab", GmEnvironmentRole.Herb, 0.16f, 0.42f, 0.07f, "fine_grass"),
        new(Haunted + "SM_Grass_06.prefab", GmEnvironmentRole.Herb, 0.18f, 0.48f, 0.07f, "fine_grass"),
        new(Haunted + "SM_Grass_08.prefab", GmEnvironmentRole.Herb, 0.18f, 0.48f, 0.07f, "fine_grass"),
        new(Haunted + "SM_Grass_10.prefab", GmEnvironmentRole.Herb, 0.18f, 0.46f, 0.07f, "fine_grass"),
        new(Haunted + "SM_Grass_11.prefab", GmEnvironmentRole.Herb, 0.16f, 0.44f, 0.07f, "fine_grass"),

        // Neutral native-scale captures admit these as sparse ecotone structure, not hedge rows.
        new(Abandoned + "SM_DeadBush_01.prefab", GmEnvironmentRole.Shrub, 0.75f, 1.15f, 0.18f, "dead_bush_01"),
        new(Abandoned + "SM_DeadBush_02.prefab", GmEnvironmentRole.Shrub, 0.70f, 1.05f, 0.14f, "dead_bush_02"),
        new(Abandoned + "SM_Wheat_Grass.prefab", GmEnvironmentRole.Shrub, 0.80f, 1.30f, 0.34f, "wheat_mass"),
        new(Abandoned + "SM_Wild_Grass.prefab", GmEnvironmentRole.Shrub, 0.70f, 1.18f, 0.34f, "wild_grass_mass"),

        // Real bark geometry admitted only for causal fall zones and wet-pocket events.
        new(Haunted + "SM_Branch_01.prefab", GmEnvironmentRole.Debris, 0.58f, 0.86f, 0.18f, "fallen_branch"),
        new(Haunted + "SM_Branch_02.prefab", GmEnvironmentRole.Debris, 0.58f, 0.88f, 0.18f, "fallen_branch"),
        new(Haunted + "SM_Branch_03.prefab", GmEnvironmentRole.Debris, 0.58f, 0.90f, 0.18f, "fallen_branch"),
        new(Haunted + "SM_Branch_04.prefab", GmEnvironmentRole.Debris, 0.62f, 0.98f, 0.18f, "fallen_branch"),
        new(Haunted + "SM_Branch_05.prefab", GmEnvironmentRole.Debris, 0.85f, 1.18f, 0.18f, "forked_branch"),
        // Round-17 native-scale captures admitted a small ground-story set. These have distinct
        // causal roles and never enter generic scatter pools.
        new(Witch + "SM_SmallRocks_1.prefab", GmEnvironmentRole.Debris, 0.24f, 0.48f, 0.24f, "small_rock"),
        new(Witch + "SM_SmallRocks_2.prefab", GmEnvironmentRole.Debris, 0.22f, 0.46f, 0.24f, "small_rock"),
        new(Witch + "SM_SmallRocks_3.prefab", GmEnvironmentRole.Debris, 0.24f, 0.50f, 0.26f, "small_rock"),
        new(Witch + "SM_SmallRocks_4.prefab", GmEnvironmentRole.Debris, 0.22f, 0.46f, 0.26f, "small_rock"),
        new(Witch + "SM_TwistedBranch2.prefab", GmEnvironmentRole.Debris, 3.2f, 4.4f, 0.42f, "fallen_limb"),
        new(Witch + "SM_DriedLeaves1.prefab", GmEnvironmentRole.LeafLitter, 2.8f, 4.6f, 0.58f, "leaf_drift"),
        new(Witch + "SM_DriedLeaves3.prefab", GmEnvironmentRole.LeafLitter, 2.6f, 4.8f, 0.42f, "leaf_drift"),
        new(Witch + "SM_MossClump_1.prefab", GmEnvironmentRole.GroundCover, 0.62f, 1.05f, 0.56f, "moss_mass"),
        new(Witch + "SM_MossClump_3.prefab", GmEnvironmentRole.GroundCover, 0.58f, 1.12f, 0.44f, "moss_mass"),
        new(Abandoned + "SM_Rock.prefab", GmEnvironmentRole.Geology, 1.8f, 2.8f, 1.00f, "mossy_boulder"),
        // The Witch floating trunk has sound geometry but failed neutral color integration as a
        // chalk-white event. It remains validation-only until a derived material passes.

    };

    public static IReadOnlyList<GmEnvironmentSourceSpec> Specs => Catalog;

    public static List<GameObject> LoadRole(GmEnvironmentRole role)
    {
        var result = new List<GameObject>();
        foreach (GmEnvironmentSourceSpec spec in Catalog)
        {
            if (spec.Role != role) continue;
            GameObject prefab = LoadValidated(spec);
            if (prefab != null) result.Add(prefab);
        }
        if (result.Count == 0)
            throw new InvalidOperationException($"Environment catalog has no validated sources for role {role}");
        return result;
    }

    public static GmEnvironmentSourceSpec SpecFor(GameObject prefab)
    {
        string path = AssetDatabase.GetAssetPath(prefab);
        foreach (GmEnvironmentSourceSpec spec in Catalog)
            if (string.Equals(path, spec.Path, StringComparison.Ordinal)) return spec;
        throw new InvalidOperationException($"Prefab is not in the calibrated environment catalog: {path}");
    }

    public static GameObject PickWeighted(List<GameObject> pool, System.Random random)
    {
        float total = 0f;
        foreach (GameObject prefab in pool) total += SpecFor(prefab).Weight;
        float choice = (float)random.NextDouble() * total;
        foreach (GameObject prefab in pool)
        {
            choice -= SpecFor(prefab).Weight;
            if (choice <= 0f) return prefab;
        }
        return pool[pool.Count - 1];
    }

    static GameObject LoadValidated(GmEnvironmentSourceSpec spec)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.Path);
        if (prefab == null) throw new InvalidOperationException($"Missing calibrated source: {spec.Path}");
        GameObject probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Renderer[] renderers = probe.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = RenderableBounds(probe);
        float footprint = Mathf.Max(bounds.size.x, bounds.size.z);
        bool finite = IsFinite(bounds.size.x) && IsFinite(bounds.size.y) && IsFinite(bounds.size.z);
        bool footprintRole = spec.Role == GmEnvironmentRole.GroundCover ||
            spec.Role == GmEnvironmentRole.LeafLitter ||
            (spec.Role == GmEnvironmentRole.Debris && spec.Silhouette != "small_rock");
        float nativePrimary = footprintRole ? footprint : bounds.size.y;
        float minScale = nativePrimary > 0.0001f ? spec.MinPrimarySize / nativePrimary : float.PositiveInfinity;
        float maxScale = nativePrimary > 0.0001f ? spec.MaxPrimarySize / nativePrimary : float.PositiveInfinity;
        bool materialsValid = true;
        int renderableCount = 0;
        foreach (Renderer renderer in renderers)
        {
            // Several purchased prefabs carry an orphan MeshRenderer on their collider root. It
            // has no MeshFilter and a null material, so it cannot draw and is not a material fault.
            if (!HasRenderableGeometry(renderer)) continue;
            renderableCount++;
            foreach (Material material in renderer.sharedMaterials)
                materialsValid &= material != null && material.shader != null &&
                    material.shader.name != "Standard" &&
                    material.shader.name.IndexOf("InternalError", StringComparison.OrdinalIgnoreCase) < 0;
        }
        materialsValid &= renderableCount > 0;
        bool roleShapeValid = !footprintRole || (footprint >= 0.05f && bounds.size.y / footprint <= 0.65f);
        bool scaleValid = minScale >= 0.025f && maxScale <= 12f;
        UnityEngine.Object.DestroyImmediate(probe);
        if (!finite || bounds.size.y <= 0.005f || footprint <= 0.005f || !materialsValid ||
            !roleShapeValid || !scaleValid)
            throw new InvalidOperationException($"Calibrated source failed validation: {spec.Path} role={spec.Role} " +
                $"bounds={bounds.size} materials={materialsValid} scale={minScale:F3}..{maxScale:F3}");
        Debug.Log($"[GmEnvironmentCatalog] ACCEPT role={spec.Role} source={prefab.name} " +
            $"target={spec.MinPrimarySize:F2}..{spec.MaxPrimarySize:F2} silhouette={spec.Silhouette} " +
            $"family={spec.FamilyId}");
        return prefab;
    }

    public static Bounds RenderableBounds(GameObject owner)
    {
        Renderer[] renderers = owner.GetComponentsInChildren<Renderer>(true);
        Renderer first = null;
        foreach (Renderer renderer in renderers)
            if (HasRenderableGeometry(renderer)) { first = renderer; break; }
        if (first == null) return new Bounds(owner.transform.position, Vector3.zero);
        Bounds bounds = first.bounds;
        foreach (Renderer renderer in renderers)
            if (renderer != first && HasRenderableGeometry(renderer)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    static bool HasRenderableGeometry(Renderer renderer)
    {
        if (renderer is SkinnedMeshRenderer skinned) return skinned.sharedMesh != null;
        if (renderer is MeshRenderer)
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            return filter != null && filter.sharedMesh != null;
        }
        return true;
    }

    static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

