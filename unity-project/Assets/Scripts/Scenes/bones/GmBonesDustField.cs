using UnityEngine;

public sealed class GmBonesDustField : MonoBehaviour
{
    [SerializeField] Vector3 diceCenter;
    [SerializeField] float exclusionRadius = 1.35f;
    public bool IsOutsideDiceCone => Vector2.Distance(
        new Vector2(transform.position.x, transform.position.z),
        new Vector2(diceCenter.x, diceCenter.z)) >= exclusionRadius;
    public void Configure(Vector3 center, float radius) { diceCenter = center; exclusionRadius = radius; }
}
