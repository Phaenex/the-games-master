using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GmRouteReservation : MonoBehaviour
{
    [SerializeField] string routeId;
    [SerializeField, TextArea] string purpose;
    [SerializeField] Vector3[] localPoints = Array.Empty<Vector3>();
    [SerializeField] float halfWidth = 0.7f;

    public string RouteId => routeId;
    public string Purpose => purpose;
    public IReadOnlyList<Vector3> LocalPoints => localPoints;
    public float HalfWidth => halfWidth;

    public void Configure(string id, string why, Vector3[] points, float clearanceHalfWidth = 0.7f)
    {
        routeId = id;
        purpose = why;
        localPoints = points == null ? Array.Empty<Vector3>() : (Vector3[])points.Clone();
        halfWidth = Mathf.Max(0.1f, clearanceHalfWidth);
    }

    public Vector3 WorldPoint(int index) => transform.TransformPoint(localPoints[index]);

    void OnDrawGizmosSelected()
    {
        if (localPoints == null || localPoints.Length < 2) return;
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.7f);
        for (int i = 1; i < localPoints.Length; i++) Gizmos.DrawLine(WorldPoint(i - 1), WorldPoint(i));
    }
}
