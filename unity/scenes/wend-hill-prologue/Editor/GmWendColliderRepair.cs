using System;
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

    public static int Apply()
    {
        GameObject stale = GameObject.Find(RootName);
        if (stale != null) UnityEngine.Object.DestroyImmediate(stale);
        var root = new GameObject(RootName);
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
            var proxyObject = new GameObject($"Proxy_{repaired:D3}_{source.name}");
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

        Debug.Log($"[GmWendColliderRepair] repaired {repaired} active negative-scale BoxCollider(s)");
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
