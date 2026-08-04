using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GmWorldAnchor : MonoBehaviour
{
    [SerializeField] string anchorId;
    [SerializeField] float routeMetres;

    public string AnchorId => anchorId;
    public float RouteMetres => routeMetres;

    public void Configure(string id, float metres)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("anchor id is required", nameof(id));
        anchorId = id.Trim();
        routeMetres = Mathf.Max(0f, metres);
    }

    public static GmWorldAnchor Find(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return UnityEngine.Object.FindObjectsByType<GmWorldAnchor>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).FirstOrDefault(anchor => anchor.anchorId == id);
    }

    public static List<string> ValidateScene()
    {
        var issues = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (GmWorldAnchor anchor in UnityEngine.Object.FindObjectsByType<GmWorldAnchor>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (string.IsNullOrWhiteSpace(anchor.anchorId)) issues.Add($"anchor '{anchor.name}' has no id");
            else if (!seen.Add(anchor.anchorId)) issues.Add($"duplicate anchor id '{anchor.anchorId}'");
        }
        return issues;
    }
}
