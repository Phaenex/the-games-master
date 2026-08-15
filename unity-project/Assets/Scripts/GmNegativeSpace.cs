using UnityEngine;

[DisallowMultipleComponent]
public sealed class GmNegativeSpace : MonoBehaviour
{
    [SerializeField] string spaceId;
    [SerializeField, TextArea] string purpose;
    [SerializeField] Vector3 size = new Vector3(2f, 2f, 2f);
    [SerializeField] Vector3 localCenterOffset;
    [SerializeField] string allowedElementId;

    public string SpaceId => spaceId;
    public string Purpose => purpose;
    public Vector3 Size => size;
    public Vector3 LocalCenterOffset => localCenterOffset;
    public string AllowedElementId => allowedElementId;

    public void Configure(string id, string why, Vector3 boundsSize, string allowedId = "",
        Vector3 centerOffset = default)
    {
        spaceId = id;
        purpose = why;
        size = new Vector3(Mathf.Max(0.1f, boundsSize.x), Mathf.Max(0.1f, boundsSize.y),
            Mathf.Max(0.1f, boundsSize.z));
        allowedElementId = allowedId;
        localCenterOffset = centerOffset;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.35f, 0.52f);
        Matrix4x4 before = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(localCenterOffset, size);
        Gizmos.matrix = before;
    }
}
