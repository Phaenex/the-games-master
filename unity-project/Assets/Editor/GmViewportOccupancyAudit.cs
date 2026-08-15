// Shot-level obstruction evidence for generated scenes. A scene can satisfy object counts and
// composition anchors while one accidental scale or near-camera prop consumes most of the frame.
// This audit keeps that failure explainable by recording the renderers with the largest clipped
// viewport bounds for every deterministic review shot. It reports evidence only; it never changes
// the scene or silently turns an aesthetic judgment into a pass/fail rule.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class GmViewportOccupancyRow
{
    public string shotName;
    public string rendererPath;
    public float clippedCoverage;
    public float projectedCoverage;
    public float centerDepth;
    public Vector3 boundsSize;
}

[Serializable]
public sealed class GmViewportFocusHit
{
    public string shotName;
    public Vector2 viewportSample;
    public string colliderPath;
    public float distance;
    public string rendererBoundsPath;
    public float rendererBoundsDistance;
    public string meshSurfacePath;
    public float meshSurfaceDistance;
}

[Serializable]
public sealed class GmViewportOccupancyReport
{
    public int schemaVersion = 2;
    public string sceneId;
    public string fingerprint;
    public int maximumRowsPerShot;
    public List<GmViewportOccupancyRow> rows = new List<GmViewportOccupancyRow>();
    public List<GmViewportFocusHit> focusHits = new List<GmViewportFocusHit>();
}

public static class GmViewportOccupancyAudit
{
    // A dozen rows is enough for architecture but not a prop-rich scene where terrain and LOD
    // groups crowd out the small near-camera object that actually causes a bad read. Sixty-four
    // stays compact in JSON while preserving the evidence needed to diagnose close dressing.
    public const int DefaultMaximumRowsPerShot = 64;

    sealed class Subject
    {
        public string path;
        public Bounds bounds;
        public Renderer[] renderers;
    }

    sealed class MeshData
    {
        public Vector3[] vertices;
        public int[] triangles;
    }

    public static GmViewportOccupancyReport Analyze(string sceneId, GmSceneReviewTour tour,
        Camera camera, int maximumRowsPerShot = DefaultMaximumRowsPerShot)
    {
        if (tour == null) throw new ArgumentNullException(nameof(tour));
        if (camera == null) throw new ArgumentNullException(nameof(camera));
        if (maximumRowsPerShot < 1) throw new ArgumentOutOfRangeException(nameof(maximumRowsPerShot));

        var report = new GmViewportOccupancyReport {
            sceneId = sceneId,
            fingerprint = GmSceneFingerprint.Current(),
            maximumRowsPerShot = maximumRowsPerShot,
        };
        Subject[] subjects = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)
            .Where(item => item.enabled && item.gameObject.scene == tour.gameObject.scene)
            .GroupBy(item => {
                LODGroup lod = item.GetComponentInParent<LODGroup>();
                return lod == null ? item.transform : lod.transform;
            })
            .Select(group => {
                Renderer[] renderers = group.ToArray();
                return new Subject {
                    path = GmSceneFingerprint.PathOf(group.Key),
                    bounds = CombinedBounds(renderers),
                    renderers = renderers,
                };
            }).ToArray();
        var meshCache = new Dictionary<Mesh, MeshData>();

        Vector3 oldPosition = camera.transform.position;
        Quaternion oldRotation = camera.transform.rotation;
        float oldAspect = camera.aspect;
        try
        {
            camera.aspect = tour.ShotHeight == 0 ? 16f / 9f : tour.ShotWidth / (float)tour.ShotHeight;
            foreach (GmReviewShot shot in tour.ShotsForAudit)
            {
                camera.transform.position = shot.Position;
                camera.transform.rotation = Quaternion.Euler(shot.Pitch,
                    shot.Yaw + tour.ReviewYawOffsetForAudit, 0f);
                Physics.SyncTransforms();
                Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
                report.rows.AddRange(subjects
                    .Select(subject => Measure(camera, planes, shot.Name, subject))
                    .Where(row => row != null && row.clippedCoverage >= 0.0025f)
                    .OrderByDescending(row => row.clippedCoverage)
                    .ThenBy(row => row.centerDepth)
                    .ThenBy(row => row.rendererPath, StringComparer.Ordinal)
                    .Take(maximumRowsPerShot));
                // Include the lower and outer thirds: foreground failures commonly enter from an
                // edge while leaving the classic centre 3x3 samples untouched.
                foreach (float sampleY in new[] { 0.15f, 0.30f, 0.50f, 0.70f, 0.85f })
                    foreach (float sampleX in new[] { 0.15f, 0.30f, 0.50f, 0.70f, 0.85f })
                    {
                        Vector2 sample = new Vector2(sampleX, sampleY);
                        Ray ray = camera.ViewportPointToRay(sample);
                        bool hasCollider = Physics.Raycast(ray, out RaycastHit hit,
                            2000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                        Subject rendered = null;
                        float renderedDistance = float.PositiveInfinity;
                        foreach (Subject subject in subjects)
                            if (subject.bounds.IntersectRay(ray, out float distance) && distance >= 0f &&
                                distance < renderedDistance)
                            {
                                rendered = subject;
                                renderedDistance = distance;
                            }
                        Renderer meshSurface = null;
                        float meshSurfaceDistance = float.PositiveInfinity;
                        foreach (Subject subject in subjects)
                        {
                            if (!subject.bounds.IntersectRay(ray)) continue;
                            foreach (Renderer renderer in subject.renderers)
                            {
                                Mesh mesh = MeshFor(renderer);
                                if (mesh == null || !renderer.bounds.IntersectRay(ray)) continue;
                                if (!TryIntersectRayMesh(ray, mesh, renderer.transform.localToWorldMatrix,
                                    meshCache, out float surfaceDistance)) continue;
                                if (surfaceDistance < 0f || surfaceDistance >= meshSurfaceDistance) continue;
                                meshSurface = renderer;
                                meshSurfaceDistance = surfaceDistance;
                            }
                        }
                        if (!hasCollider && rendered == null && meshSurface == null) continue;
                        report.focusHits.Add(new GmViewportFocusHit {
                            shotName = shot.Name,
                            viewportSample = sample,
                            colliderPath = hasCollider ? GmSceneFingerprint.PathOf(hit.collider.transform) : "",
                            distance = hasCollider ? hit.distance : 0f,
                            rendererBoundsPath = rendered == null ? "" : rendered.path,
                            rendererBoundsDistance = rendered == null ? 0f : renderedDistance,
                            meshSurfacePath = meshSurface == null ? "" :
                                GmSceneFingerprint.PathOf(meshSurface.transform),
                            meshSurfaceDistance = meshSurface == null ? 0f : meshSurfaceDistance,
                        });
                    }
            }
        }
        finally
        {
            camera.transform.position = oldPosition;
            camera.transform.rotation = oldRotation;
            camera.aspect = oldAspect;
        }
        return report;
    }

    public static string Write(string sceneId, GmSceneReviewTour tour, Camera camera)
    {
        GmViewportOccupancyReport report = Analyze(sceneId, tour, camera);
        string directory = Path.Combine(GmSceneIntelligencePaths.LibraryRoot, "audits");
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, $"{sceneId}-viewport-occupancy.json");
        File.WriteAllText(file, JsonUtility.ToJson(report, true) + "\n");
        foreach (IGrouping<string, GmViewportOccupancyRow> shot in report.rows.GroupBy(row => row.shotName))
        {
            string leaders = string.Join("; ", shot.Take(4).Select(row =>
                $"{row.rendererPath}={row.clippedCoverage:P0}@{row.centerDepth:F1}m"));
            Debug.Log($"[GmViewportOccupancy] {shot.Key}: {leaders}");
        }
        foreach (IGrouping<string, GmViewportFocusHit> shot in report.focusHits.GroupBy(hit => hit.shotName))
        {
            string hits = string.Join("; ", shot.Select(hit =>
                $"{hit.viewportSample.x:F2},{hit.viewportSample.y:F2}:" +
                $"col={hit.colliderPath}@{hit.distance:F1}m " +
                $"bounds={hit.rendererBoundsPath}@{hit.rendererBoundsDistance:F1}m " +
                $"mesh={hit.meshSurfacePath}@{hit.meshSurfaceDistance:F1}m"));
            Debug.Log($"[GmViewportFocus] {shot.Key}: {hits}");
        }
        Debug.Log($"[GmViewportOccupancy] PASS: {report.rows.Count} ranked rows -> {file}");
        return file;
    }

    static Bounds CombinedBounds(IEnumerable<Renderer> renderers)
    {
        using IEnumerator<Renderer> iterator = renderers.GetEnumerator();
        if (!iterator.MoveNext()) return new Bounds();
        Bounds bounds = iterator.Current.bounds;
        while (iterator.MoveNext()) bounds.Encapsulate(iterator.Current.bounds);
        return bounds;
    }

    static Mesh MeshFor(Renderer renderer)
    {
        if (renderer is SkinnedMeshRenderer skinned) return skinned.sharedMesh;
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        return filter == null ? null : filter.sharedMesh;
    }

    // Renderer bounds are deliberately only a broad phase: a long path or an L-shaped prop can
    // enclose the focus ray without drawing a single pixel there. Generated composition meshes are
    // readable, so an exact ray/triangle test can name the actual surface behind a bad screenshot.
    // Imported meshes that disable Read/Write remain represented by the conservative bounds field.
    static bool TryIntersectRayMesh(Ray worldRay, Mesh mesh, Matrix4x4 localToWorld,
        Dictionary<Mesh, MeshData> cache, out float nearestDistance)
    {
        nearestDistance = float.PositiveInfinity;
        if (mesh == null || !mesh.isReadable) return false;
        if (!cache.TryGetValue(mesh, out MeshData data))
        {
            data = new MeshData { vertices = mesh.vertices, triangles = mesh.triangles };
            cache.Add(mesh, data);
        }
        Matrix4x4 worldToLocal = localToWorld.inverse;
        Vector3 localOrigin = worldToLocal.MultiplyPoint3x4(worldRay.origin);
        // Ray normalizes its direction. Keep the unnormalized inverse-transformed direction for
        // triangle math: its parameter then remains the original world-ray distance even under
        // heavily non-uniform plane/terrain scaling.
        Vector3 localDirection = worldToLocal.MultiplyVector(worldRay.direction);
        var localRay = new Ray(localOrigin, localDirection);
        if (!mesh.bounds.IntersectRay(localRay)) return false;

        Vector3 origin = localOrigin, direction = localDirection;
        const float epsilon = 0.000001f;
        for (int i = 0; i + 2 < data.triangles.Length; i += 3)
        {
            Vector3 a = data.vertices[data.triangles[i]];
            Vector3 edge1 = data.vertices[data.triangles[i + 1]] - a;
            Vector3 edge2 = data.vertices[data.triangles[i + 2]] - a;
            Vector3 p = Vector3.Cross(direction, edge2);
            float determinant = Vector3.Dot(edge1, p);
            if (Mathf.Abs(determinant) < epsilon) continue;
            float inverse = 1f / determinant;
            Vector3 fromA = origin - a;
            float u = Vector3.Dot(fromA, p) * inverse;
            if (u < 0f || u > 1f) continue;
            Vector3 q = Vector3.Cross(fromA, edge1);
            float v = Vector3.Dot(direction, q) * inverse;
            if (v < 0f || u + v > 1f) continue;
            float distance = Vector3.Dot(edge2, q) * inverse;
            if (distance >= 0f && distance < nearestDistance) nearestDistance = distance;
        }
        return !float.IsInfinity(nearestDistance);
    }

    static GmViewportOccupancyRow Measure(Camera camera, Plane[] planes, string shotName,
        Subject subject)
    {
        Bounds bounds = subject.bounds;
        if (!GeometryUtility.TestPlanesAABB(planes, bounds)) return null;

        float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
        foreach (Vector3 corner in Corners(bounds))
        {
            Vector3 viewport = camera.WorldToViewportPoint(corner);
            if (viewport.z <= camera.nearClipPlane) continue;
            minX = Mathf.Min(minX, viewport.x);
            minY = Mathf.Min(minY, viewport.y);
            maxX = Mathf.Max(maxX, viewport.x);
            maxY = Mathf.Max(maxY, viewport.y);
        }
        if (float.IsInfinity(minX)) return null;

        float projectedWidth = Mathf.Max(0f, maxX - minX);
        float projectedHeight = Mathf.Max(0f, maxY - minY);
        float clippedWidth = Mathf.Max(0f, Mathf.Min(1f, maxX) - Mathf.Max(0f, minX));
        float clippedHeight = Mathf.Max(0f, Mathf.Min(1f, maxY) - Mathf.Max(0f, minY));
        float clippedCoverage = clippedWidth * clippedHeight;
        if (clippedCoverage <= 0f) return null;

        return new GmViewportOccupancyRow {
            shotName = shotName,
            rendererPath = subject.path,
            clippedCoverage = clippedCoverage,
            projectedCoverage = projectedWidth * projectedHeight,
            centerDepth = camera.WorldToViewportPoint(bounds.center).z,
            boundsSize = bounds.size,
        };
    }

    static IEnumerable<Vector3> Corners(Bounds bounds)
    {
        Vector3 min = bounds.min, max = bounds.max;
        yield return new Vector3(min.x, min.y, min.z);
        yield return new Vector3(min.x, min.y, max.z);
        yield return new Vector3(min.x, max.y, min.z);
        yield return new Vector3(min.x, max.y, max.z);
        yield return new Vector3(max.x, min.y, min.z);
        yield return new Vector3(max.x, min.y, max.z);
        yield return new Vector3(max.x, max.y, min.z);
        yield return new Vector3(max.x, max.y, max.z);
    }
}
