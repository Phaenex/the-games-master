using UnityEngine;

[DisallowMultipleComponent]
public sealed class GmCompositionCluster : MonoBehaviour
{
    [SerializeField] string clusterId;
    [SerializeField] string zoneId;
    [SerializeField, TextArea] string purpose;
    [SerializeField] string anchorElementId;
    [SerializeField] Vector3 localCenterOffset;
    [SerializeField] int minimumSupports = 1;
    [SerializeField] int minimumDetails = 1;
    [SerializeField] int maximumMembers = 24;
    [SerializeField] float maximumRadius = 8f;
    [SerializeField] bool requireAssetFamilyVariation = true;
    [SerializeField, Range(0.5f, 1f)] float maximumDominantFamilyShare = 0.75f;

    public string ClusterId => clusterId;
    public string ZoneId => zoneId;
    public string Purpose => purpose;
    public string AnchorElementId => anchorElementId;
    public Vector3 LocalCenterOffset => localCenterOffset;
    public Vector3 WorldCenter => transform.TransformPoint(localCenterOffset);
    public int MinimumSupports => minimumSupports;
    public int MinimumDetails => minimumDetails;
    public int MaximumMembers => maximumMembers;
    public float MaximumRadius => maximumRadius;
    public bool RequireAssetFamilyVariation => requireAssetFamilyVariation;
    public float MaximumDominantFamilyShare => maximumDominantFamilyShare;

    public void Configure(string id, string zone, string why, string anchorId, int minSupports = 1,
        int minDetails = 1, int maxMembers = 24, float maxRadius = 8f,
        bool requireVariation = true, float maxFamilyShare = 0.75f, Vector3 centerOffset = default)
    {
        clusterId = id;
        zoneId = zone;
        purpose = why;
        anchorElementId = anchorId;
        localCenterOffset = centerOffset;
        minimumSupports = Mathf.Max(0, minSupports);
        minimumDetails = Mathf.Max(0, minDetails);
        maximumMembers = Mathf.Max(1, maxMembers);
        maximumRadius = Mathf.Max(0.1f, maxRadius);
        requireAssetFamilyVariation = requireVariation;
        maximumDominantFamilyShare = Mathf.Clamp(maxFamilyShare, 0.5f, 1f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.7f, 0.18f, 0.5f);
        Gizmos.DrawWireSphere(WorldCenter, maximumRadius);
    }
}
