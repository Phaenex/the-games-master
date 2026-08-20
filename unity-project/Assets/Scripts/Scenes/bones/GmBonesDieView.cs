using System;
using UnityEngine;

public static class GmPhysicalDieFaces
{
    public readonly struct Face
    {
        public readonly int value;
        public readonly Vector3 normal;
        public Face(int faceValue, Vector3 faceNormal) { value = faceValue; normal = faceNormal; }
    }

    public static readonly Face[] All =
    {
        new Face(1, Vector3.up), new Face(6, Vector3.down),
        new Face(2, Vector3.forward), new Face(5, Vector3.back),
        new Face(3, Vector3.right), new Face(4, Vector3.left)
    };

    public static int Opposite(int value)
    {
        if (value < 1 || value > 6) throw new ArgumentOutOfRangeException(nameof(value));
        return 7 - value;
    }

    public static Vector3 NormalForValue(int value)
    {
        foreach (Face face in All) if (face.value == value) return face.normal;
        throw new ArgumentOutOfRangeException(nameof(value));
    }

    public static int ValueForNormal(Vector3 normal)
    {
        foreach (Face face in All) if (Vector3.Dot(face.normal, normal.normalized) > 0.999f) return face.value;
        throw new ArgumentException("normal is not a standard die face", nameof(normal));
    }
}

public sealed class GmPhysicalDiePip : MonoBehaviour
{
    public int FaceValue { get; private set; }
    public Vector3 FaceNormal { get; private set; }
    public void Configure(int face, Vector3 normal) { FaceValue = face; FaceNormal = normal; }
}

public sealed class GmBonesDieView : MonoBehaviour
{
    [SerializeField] MeshFilter authoredBody;
    [SerializeField] GameObject changedEvidence;

    public int Face { get; private set; } = 1;
    public bool IsChangedEvidenceVisible => changedEvidence != null && changedEvidence.activeSelf;
    public int ChangedEvidenceRendererCount => changedEvidence == null
        ? 0 : changedEvidence.GetComponentsInChildren<Renderer>(true).Length;

    public void Configure(MeshFilter body, GameObject evidence)
    {
        authoredBody = body;
        changedEvidence = evidence;
    }

    public void SetFace(int face, bool changed, bool reducedMotion)
    {
        Quaternion target = RotationForFace(face);
        transform.localRotation = target;
        Face = face;
        if (changedEvidence != null) changedEvidence.SetActive(changed);
    }

    public static Quaternion RotationForFace(int face)
    {
        Vector3 normal = GmPhysicalDieFaces.NormalForValue(face);
        return Quaternion.FromToRotation(normal, Vector3.up);
    }
}
