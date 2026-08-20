using UnityEngine;

public sealed class GmStudyDustField : MonoBehaviour
{
    [SerializeField] Vector3 boardCenter;
    [SerializeField] float exclusionRadius = 1.35f;
    public bool IsOutsideBoardCone => Vector2.Distance(
        new Vector2(transform.position.x, transform.position.z),
        new Vector2(boardCenter.x, boardCenter.z)) >= exclusionRadius;
    public void Configure(Vector3 center, float radius) { boardCenter = center; exclusionRadius = radius; }
}
