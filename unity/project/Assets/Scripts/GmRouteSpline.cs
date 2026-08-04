using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GmRouteSpline : MonoBehaviour
{
    [SerializeField] Vector3[] points = Array.Empty<Vector3>();
    [SerializeField] float length;

    public IReadOnlyList<Vector3> Points => points;
    public float Length => length;

    public void Configure(IReadOnlyList<Vector3> worldPoints)
    {
        if (worldPoints == null || worldPoints.Count < 2)
            throw new ArgumentException("a route spline needs at least two points", nameof(worldPoints));
        points = worldPoints.ToArray();
        length = 0f;
        for (int i = 1; i < points.Length; i++) length += HorizontalDistance(points[i - 1], points[i]);
    }

    public Vector3 PointAt(float metres)
    {
        if (points == null || points.Length == 0) return transform.position;
        float remaining = Mathf.Clamp(metres, 0f, length);
        for (int i = 1; i < points.Length; i++)
        {
            float segment = HorizontalDistance(points[i - 1], points[i]);
            if (remaining <= segment || i == points.Length - 1)
                return Vector3.Lerp(points[i - 1], points[i], segment <= 0.001f ? 0f : remaining / segment);
            remaining -= segment;
        }
        return points[^1];
    }

    public Vector3 TangentAt(float metres)
    {
        Vector3 a = PointAt(Mathf.Max(0f, metres - 1f));
        Vector3 b = PointAt(Mathf.Min(length, metres + 1f));
        Vector3 tangent = b - a;
        tangent.y = 0f;
        return tangent.sqrMagnitude > 0.001f ? tangent.normalized : Vector3.forward;
    }

    public float ProjectDistance(Vector3 world)
    {
        if (points == null || points.Length < 2) return 0f;
        float bestSquared = float.MaxValue;
        float bestMetres = 0f;
        float travelled = 0f;
        Vector2 target = new Vector2(world.x, world.z);
        for (int i = 1; i < points.Length; i++)
        {
            Vector2 a = new Vector2(points[i - 1].x, points[i - 1].z);
            Vector2 b = new Vector2(points[i].x, points[i].z);
            Vector2 delta = b - a;
            float segment = delta.magnitude;
            float t = segment <= 0.001f ? 0f : Mathf.Clamp01(Vector2.Dot(target - a, delta) / delta.sqrMagnitude);
            float squared = (target - Vector2.Lerp(a, b, t)).sqrMagnitude;
            if (squared < bestSquared)
            {
                bestSquared = squared;
                bestMetres = travelled + segment * t;
            }
            travelled += segment;
        }
        return bestMetres;
    }

    static float HorizontalDistance(Vector3 a, Vector3 b) =>
        Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
}
