using UnityEngine;

[DisallowMultipleComponent]
public sealed class GmCompositionZone : MonoBehaviour
{
    [SerializeField] string zoneId;
    [SerializeField, TextArea] string purpose;
    [SerializeField] Vector3 size = new Vector3(8f, 4f, 8f);
    [SerializeField] Vector3 localCenterOffset;
    [SerializeField] int minimumClusters = 1;
    [SerializeField] int minimumElements = 3;

    public string ZoneId => zoneId;
    public string Purpose => purpose;
    public Vector3 Size => size;
    public Vector3 LocalCenterOffset => localCenterOffset;
    public Vector3 WorldCenter => transform.TransformPoint(localCenterOffset);
    public int MinimumClusters => minimumClusters;
    public int MinimumElements => minimumElements;

    public void Configure(string id, string why, Vector3 boundsSize, int minClusters = 1,
        int minElements = 3, Vector3 centerOffset = default)
    {
        zoneId = id;
        purpose = why;
        size = new Vector3(Mathf.Max(0.1f, boundsSize.x), Mathf.Max(0.1f, boundsSize.y),
            Mathf.Max(0.1f, boundsSize.z));
        localCenterOffset = centerOffset;
        minimumClusters = Mathf.Max(1, minClusters);
        minimumElements = Mathf.Max(1, minElements);
    }

    public bool Contains(Vector3 worldPoint, float padding = 0f)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint) - localCenterOffset;
        Vector3 half = size * 0.5f + Vector3.one * padding;
        return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.42f);
        Matrix4x4 before = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(localCenterOffset, size);
        Gizmos.matrix = before;
    }
}
