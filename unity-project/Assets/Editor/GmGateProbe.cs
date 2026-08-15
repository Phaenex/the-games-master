// Diagnostic: the gate renders as two pillars with visible hinge brackets and nothing hung between
// them. The Threshold Refusal beat needs the gate to read as a barrier that LOCKS behind the player,
// which two free-standing posts cannot do. Before authoring a replacement, find out what the
// purchased asset actually contains -- the leaves may exist but be disabled, mis-scaled, or named
// such that they were never instantiated.
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class GmGateProbe
{
    [MenuItem("GamesMaster/Village/Probe Gate Asset")]
    public static void Run()
    {
        foreach (string name in new[] { "graveyard_gate" })
        {
            string[] guids = AssetDatabase.FindAssets($"{name} t:model")
                .Concat(AssetDatabase.FindAssets($"{name} t:prefab")).Distinct().ToArray();
            Debug.Log($"[GmGateProbe] '{name}': {guids.Length} candidate asset(s)");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;

                var sb = new StringBuilder();
                sb.AppendLine($"[GmGateProbe] {path}");
                Dump(go.transform, sb, 1);

                Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
                Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds();
                foreach (Renderer r in rs) b.Encapsulate(r.bounds);
                sb.AppendLine($"  TOTAL renderers={rs.Length} bounds size=({b.size.x:0.##}, {b.size.y:0.##}, {b.size.z:0.##})");
                Debug.Log(sb.ToString());
            }
        }
        EditorApplication.Exit(0);
    }

    static void Dump(Transform t, StringBuilder sb, int depth)
    {
        var mr = t.GetComponent<MeshRenderer>();
        var mf = t.GetComponent<MeshFilter>();
        string mesh = mf != null && mf.sharedMesh != null
            ? $" mesh={mf.sharedMesh.name} verts={mf.sharedMesh.vertexCount}" : "";
        string bounds = mr != null
            ? $" size=({mr.bounds.size.x:0.##},{mr.bounds.size.y:0.##},{mr.bounds.size.z:0.##})" : "";
        sb.AppendLine($"  {new string(' ', depth * 2)}{t.name} active={t.gameObject.activeSelf}{mesh}{bounds}");
        foreach (Transform c in t) Dump(c, sb, depth + 1);
    }
}

