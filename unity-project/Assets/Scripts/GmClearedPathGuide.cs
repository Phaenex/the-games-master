// A path can be composed as intentional negative space instead of an opaque floor ribbon. The
// guide gives builders, audits and review-camera clearance checks measurable route geometry while
// the surrounding fence openings, grave spacing, beds and edge debris provide the visible read.
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GmClearedPathGuide : MonoBehaviour
{
    [SerializeField, Min(0.1f)] float length = 1f;
    [SerializeField, Min(0.1f)] float width = 1f;
    [SerializeField] bool alongX;

    public float Length => length;
    public float Width => width;
    public bool AlongX => alongX;
    public Bounds WorldBounds => new Bounds(transform.position,
        alongX ? new Vector3(length, 0.05f, width) : new Vector3(width, 0.05f, length));

    public void Configure(float routeLength, float routeWidth, bool routeRunsAlongX)
    {
        length = Mathf.Max(0.1f, routeLength);
        width = Mathf.Max(0.1f, routeWidth);
        alongX = routeRunsAlongX;
    }
}
