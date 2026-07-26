using NUnit.Framework;
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

    static Mesh Triangle()
    {
        var mesh = new Mesh { name = "TerrainAuditTriangle" };
        mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
        mesh.triangles = new[] { 0, 1, 2 };
        return mesh;
    }
}
