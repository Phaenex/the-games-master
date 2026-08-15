using System.Linq;
using UnityEditor;
using UnityEngine;

public static class GmWendColliderRepair
{
    public const string RootName = "GmWendColliderProxies";

    public static bool HasNegativeLossyScale(Transform transform)
    {
        Vector3 scale = transform.lossyScale;
        return scale.x < 0f || scale.y < 0f || scale.z < 0f;
    }

    public static bool HasUnsupportedBoxTransform(BoxCollider collider)
    {
        if (collider.size.x < 0f || collider.size.y < 0f || collider.size.z < 0f) return true;
        for (Transform current = collider.transform; current != null; current = current.parent)
        {
            Vector3 local = current.localScale;
            if (local.x < 0f || local.y < 0f || local.z < 0f) return true;
        }
        return false;
    }

    /// Replaces every broken BoxCollider with a positive-size proxy, and returns how many it replaced.
    ///
    /// Reuses an existing proxy root rather than clearing it, because clearing it destroyed the only
    /// copy of the collision it was holding: the broken sources a rerun would need to rebuild those
    /// proxies from were destroyed by the first run, so the second run deleted working colliders,
    /// found nothing left to repair, reported 0 and left that geometry with no collision at all --
    /// worse than not running. Every shipping path starts from a fresh copy of the purchased scene, so
    /// in practice the root is absent and the sources are back; this is about what happens when it is
    /// called twice against one scene state.
    public static int Apply()
    {
        GameObject root = GameObject.Find(RootName);
        if (root == null) root = new GameObject(RootName);
        int kept = root.GetComponentsInChildren<GmWendColliderProxy>(true).Length;
        int repaired = 0;

        BoxCollider[] colliders = UnityEngine.Object.FindObjectsByType<BoxCollider>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (BoxCollider source in colliders)
        {
            if (!HasUnsupportedBoxTransform(source)) continue;

            if (!source.enabled || !source.gameObject.activeInHierarchy)
            {
                UnityEngine.Object.DestroyImmediate(source);
                continue;
            }

            Bounds before = source.bounds;
            string sourcePath = AnimationUtility.CalculateTransformPath(source.transform, null);
            var proxyObject = new GameObject($"Proxy_{kept + repaired:D3}_{source.name}");
            proxyObject.layer = source.gameObject.layer;
            proxyObject.transform.SetParent(root.transform, true);
            proxyObject.transform.SetPositionAndRotation(source.transform.TransformPoint(source.center),
                source.transform.rotation);
            proxyObject.transform.localScale = Vector3.one;

            Vector3 scale = source.transform.lossyScale;
            var proxy = proxyObject.AddComponent<BoxCollider>();
            proxy.center = Vector3.zero;
            proxy.size = new Vector3(Mathf.Abs(scale.x * source.size.x),
                Mathf.Abs(scale.y * source.size.y), Mathf.Abs(scale.z * source.size.z));
            proxy.isTrigger = source.isTrigger;
            proxy.sharedMaterial = source.sharedMaterial;
            proxy.contactOffset = source.contactOffset;
            proxyObject.AddComponent<GmWendColliderProxy>().Configure(sourcePath, before.center, before.size);
            // Disabled invalid colliders still emit Unity's negative-size warning while the scene is
            // deserialized in a player. The positive proxy is now authoritative, so remove only this
            // scene instance's broken component; the purchased prefab asset remains untouched.
            UnityEngine.Object.DestroyImmediate(source);
            repaired++;
        }

        Debug.Log($"[GmWendColliderRepair] repaired {repaired} active negative-scale BoxCollider(s), " +
                  $"kept {kept} proxy/proxies from an earlier pass");
        return repaired;
    }

    public static string[] ActiveNegativeColliderPaths() =>
        UnityEngine.Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(c => c.enabled && c.gameObject.activeInHierarchy && HasUnsupportedBoxTransform(c))
            .Select(c => AnimationUtility.CalculateTransformPath(c.transform, null)).ToArray();

    public static string[] AllNegativeColliderPaths() =>
        UnityEngine.Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(HasUnsupportedBoxTransform)
            .Select(c => AnimationUtility.CalculateTransformPath(c.transform, null)).ToArray();
}
