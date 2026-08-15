using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class GmTerrainPrototypeAuditTests
{
    [Test]
    public void ChildOnlyMeshDoesNotPretendToBeAPlayerInstancableTerrainPrefab()
    {
        var root = new GameObject("ChildOnlyRoot");
        var child = new GameObject("VisibleChild");
        child.transform.SetParent(root.transform);
        child.AddComponent<MeshRenderer>();
        child.AddComponent<MeshFilter>().sharedMesh = Triangle();
        try { Assert.IsFalse(GmTerrainPrototypeAudit.HasPlayerInstancableRoot(root)); }
        finally
        {
            Object.DestroyImmediate(child.GetComponent<MeshFilter>().sharedMesh);
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void RootLodGroupMayOwnValidChildMeshes()
    {
        var root = new GameObject("LodRoot");
        var child = new GameObject("Lod0");
        child.transform.SetParent(root.transform);
        var renderer = child.AddComponent<MeshRenderer>();
        child.AddComponent<MeshFilter>().sharedMesh = Triangle();
        root.AddComponent<LODGroup>().SetLODs(new[] { new LOD(0.05f, new Renderer[] { renderer }) });
        try { Assert.IsTrue(GmTerrainPrototypeAudit.HasPlayerInstancableRoot(root)); }
        finally
        {
            Object.DestroyImmediate(child.GetComponent<MeshFilter>().sharedMesh);
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void RootRendererRequiresRealMeshData()
    {
        var root = new GameObject("EmptyRootRenderer");
        root.AddComponent<MeshRenderer>();
        root.AddComponent<MeshFilter>();
        try { Assert.IsFalse(GmTerrainPrototypeAudit.HasPlayerInstancableRoot(root)); }
        finally { Object.DestroyImmediate(root); }
    }

    // HasPlayerInstancableRoot is the predicate; Validate is the entry point the estate audit calls
    // to gate a build (GmEstateQualityAudit, population floor 50). The three tests above never reach it.

    [Test]
    public void ValidateRejectsATerrainThatIsNotThere()
    {
        Assert.That(GmTerrainPrototypeAudit.Validate(null), Has.Some.Contains("terrain is missing"));
    }

    [Test]
    public void ValidateNamesRejectedPrototypesAndPopulationsTooThinToRead()
    {
        const string folder = "Assets/Tests/GmTerrainPrototypeTemp";
        var terrainObject = new GameObject("AuditTerrain");
        TerrainData data = null;
        try
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Tests")) AssetDatabase.CreateFolder("Assets", "Tests");
                AssetDatabase.CreateFolder("Assets/Tests", "GmTerrainPrototypeTemp");
            }
            GameObject instancable = SavePrefab(RootMesh(), folder + "/RootMesh.prefab");
            GameObject childOnly = SavePrefab(ChildOnlyMesh(), folder + "/ChildOnly.prefab");

            data = new TerrainData { name = "AuditTerrainData" };
            data.treePrototypes = new[] {
                new TreePrototype { prefab = instancable },
                new TreePrototype { prefab = childOnly },
            };
            var instances = new List<TreeInstance>();
            for (int i = 0; i < 3; i++)
                instances.Add(new TreeInstance {
                    prototypeIndex = 0,
                    position = new Vector3(0.1f * (i + 1), 0f, 0.1f * (i + 1)),
                    widthScale = 1f,
                    heightScale = 1f,
                    rotation = 0f,
                    color = Color.white,
                    lightmapColor = Color.white,
                });
            data.SetTreeInstances(instances.ToArray(), false);

            var terrain = terrainObject.AddComponent<Terrain>();
            terrain.terrainData = data;

            IReadOnlyList<string> issues = GmTerrainPrototypeAudit.Validate(terrain, 2);
            Assert.That(issues, Has.Some.Contains("prototype 1 'ChildOnly' has no valid root renderer/LODGroup"),
                "a child-only prototype the built player silently drops was reported as shippable");
            Assert.That(issues, Has.Some.Contains("prototype 1 'ChildOnly' has population 0; expected at least 2"));
            Assert.That(issues, Has.None.Contains("prototype 0"),
                "a valid, populated prototype was reported as a problem");

            // Same terrain, stricter floor: a registered prototype nobody would notice is still a finding.
            Assert.That(GmTerrainPrototypeAudit.Validate(terrain, 4),
                Has.Some.Contains("prototype 0 'RootMesh' has population 3; expected at least 4"));
        }
        finally
        {
            Object.DestroyImmediate(terrainObject);
            if (data != null) Object.DestroyImmediate(data);
            AssetDatabase.DeleteAsset(folder);
        }
    }

    static GameObject RootMesh()
    {
        var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
        root.name = "RootMesh";
        return root;
    }

    static GameObject ChildOnlyMesh()
    {
        var root = new GameObject("ChildOnly");
        GameObject child = GameObject.CreatePrimitive(PrimitiveType.Cube);
        child.name = "VisibleChild";
        child.transform.SetParent(root.transform);
        return root;
    }

    static GameObject SavePrefab(GameObject source, string path)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, path);
        Object.DestroyImmediate(source);
        Assert.IsNotNull(prefab, $"could not author the fixture prefab at {path}");
        return prefab;
    }

    static Mesh Triangle()
    {
        var mesh = new Mesh { name = "TerrainAuditTriangle" };
        mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
        mesh.triangles = new[] { 0, 1, 2 };
        return mesh;
    }
}
