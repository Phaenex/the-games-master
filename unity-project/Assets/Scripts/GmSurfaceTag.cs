using UnityEngine;

public enum GmSurfaceKind
{
    Unknown,
    PackedMud,
    WetMud,
    Gravel,
    DeadGrass,
    LeafLitter,
    Stone,
    Wood
}

/// <summary>Semantic ground ownership shared by footsteps, placement audits and future scenes.</summary>
[DisallowMultipleComponent]
public sealed class GmSurfaceTag : MonoBehaviour
{
    [SerializeField] GmSurfaceKind surface = GmSurfaceKind.Unknown;
    [SerializeField, TextArea] string rationale;

    public GmSurfaceKind Surface => surface;
    public string Rationale => rationale;

    public void Configure(GmSurfaceKind kind, string why)
    {
        surface = kind;
        rationale = why;
    }
}

