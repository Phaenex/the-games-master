// Shared structural gate for Unity Terrain tree/detail prefabs. Unity's Terrain renderer does not
// accept an arbitrary prefab merely because a child has a MeshRenderer: it requires a usable
// renderer on the prefab root or a root LODGroup that owns usable renderers. This catches that
// player-only rejection before a scene can advertise foliage populations that render as nothing.
using System.Collections.Generic;
using UnityEngine;

public static class GmTerrainPrototypeAudit
{
    public static bool HasPlayerInstancableRoot(GameObject prefab)
    {
        if (prefab == null) return false;
        if (HasMesh(prefab.GetComponent<Renderer>())) return true;

        LODGroup rootLod = prefab.GetComponent<LODGroup>();
        if (rootLod == null) return false;
        LOD[] lods = rootLod.GetLODs();
        if (lods == null || lods.Length == 0) return false;
        foreach (LOD lod in lods)
        {
            if (lod.renderers == null || lod.renderers.Length == 0) return false;
            foreach (Renderer renderer in lod.renderers)
                if (!HasMesh(renderer)) return false;
        }
        return true;
    }

    public static IReadOnlyList<string> Validate(Terrain terrain, int minimumPopulationPerPrototype = 1)
    {
        var issues = new List<string>();
        if (terrain == null)
        {
            issues.Add("terrain is missing");
            return issues;
        }
        TerrainData data = terrain.terrainData;
        if (data == null)
        {
            issues.Add($"terrain '{terrain.name}' has no TerrainData");
            return issues;
        }

        TreePrototype[] prototypes = data.treePrototypes;
        int[] populations = new int[prototypes.Length];
        foreach (TreeInstance instance in data.treeInstances)
        {
            if (instance.prototypeIndex < 0 || instance.prototypeIndex >= prototypes.Length)
                issues.Add($"terrain '{terrain.name}' has an instance with invalid prototype index {instance.prototypeIndex}");
            else populations[instance.prototypeIndex]++;
        }

        for (int i = 0; i < prototypes.Length; i++)
        {
            GameObject prefab = prototypes[i].prefab;
            string label = prefab == null ? "<null>" : prefab.name;
            if (prefab == null)
                issues.Add($"terrain '{terrain.name}' prototype {i} is null");
            else if (!HasPlayerInstancableRoot(prefab))
                issues.Add($"terrain '{terrain.name}' prototype {i} '{label}' has no valid root renderer/LODGroup and will be rejected by the built player");
            if (populations[i] < minimumPopulationPerPrototype)
                issues.Add($"terrain '{terrain.name}' prototype {i} '{label}' has population {populations[i]}; expected at least {minimumPopulationPerPrototype}");
        }
        return issues;
    }

    static bool HasMesh(Renderer renderer)
    {
        if (renderer == null) return false;
        if (renderer is SkinnedMeshRenderer skinned) return skinned.sharedMesh != null;
        return renderer.GetComponent<MeshFilter>()?.sharedMesh != null;
    }
}
