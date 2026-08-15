// The gate that actually closes.
//
// Assets/GamesMaster/Props/graveyard_gate.fbx is a SINGLE static mesh (GraveYard_Door_Cube.001,
// 1536 verts) with its leaves already modelled swung open -- confirmed by GmGateProbe. There is no
// leaf transform to rotate and no closed-state geometry anywhere in the asset. But Threshold Refusal
// requires the gate to shut behind the player: "Something slammed shut behind me. When I looked
// back, the gate was closed -- and the lock, somehow, had already turned."
//
// Splitting the purchased mesh by connected components would be fragile (it may well be one welded
// shell, and it is not ours to modify). Instead the leaves are built as their own geometry, hinged
// at the pillars, and swung shut on the lock beat. That also means the close is animated rather than
// a mesh swap, which is what the beat describes.
//
// GmThreshold calls Close() when it fires the gate lock. Safe to have none of these in a scene --
// the call there is null-conditional, so the estate scene is unaffected.
using UnityEngine;

public sealed class GmGateLeaves : MonoBehaviour
{
    [SerializeField] Transform leftPivot;
    [SerializeField] Transform rightPivot;

    [Tooltip("Yaw of each leaf when the gate stands open, in degrees, mirrored across the two sides.")]
    [SerializeField] float openYaw = 96f;
    [SerializeField] float closeSeconds = 0.55f;

    [SerializeField] Collider barrierCollider;

    bool closing;
    float t;

    public bool IsClosed { get; private set; }

    public void Configure(Transform left, Transform right, float open, float seconds, Collider barrier = null)
    {
        leftPivot = left;
        rightPivot = right;
        openYaw = open;
        closeSeconds = Mathf.Max(0.05f, seconds);
        if (barrier != null) barrierCollider = barrier;
        if (barrierCollider != null) barrierCollider.enabled = IsClosed;
        ApplyYaw(openYaw);
    }

    void Awake()
    {
        if (barrierCollider != null) barrierCollider.enabled = IsClosed;
        if (!IsClosed && !closing) ApplyYaw(openYaw);
    }

    /// Swing shut. Idempotent: a second call while already closing or closed does nothing, so a
    /// repeated lock event cannot restart the animation halfway.
    public void Close()
    {
        if (closing || IsClosed) return;
        closing = true;
        t = 0f;
        if (barrierCollider != null) barrierCollider.enabled = true;
    }

    /// Snap shut with no animation. Used by the lighting lab to render the closed state for review
    /// without entering play mode, and by GmVillageSave when restoring a session where the gate had
    /// already locked -- in both cases the swing is not wanted, only the end state.
    public void SetClosedImmediate()
    {
        closing = false;
        IsClosed = true;
        if (barrierCollider != null) barrierCollider.enabled = true;
        ApplyYaw(0f);
    }

    /// Reopen, for review tooling that renders both states in one pass.
    public void SetOpenImmediate()
    {
        closing = false;
        IsClosed = false;
        if (barrierCollider != null) barrierCollider.enabled = false;
        ApplyYaw(openYaw);
    }

    void Update()
    {
        if (!closing) return;
        t += Time.deltaTime / closeSeconds;
        // Eased so the leaves accelerate into the jamb rather than gliding to a polite stop; the
        // audio on this beat is a slam.
        float k = Mathf.Clamp01(t);
        ApplyYaw(Mathf.Lerp(openYaw, 0f, k * k));
        if (k >= 1f)
        {
            closing = false;
            IsClosed = true;
        }
    }

    void ApplyYaw(float yaw)
    {
        if (leftPivot != null) leftPivot.localRotation = Quaternion.Euler(0f, -yaw, 0f);
        if (rightPivot != null) rightPivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
    }
}

