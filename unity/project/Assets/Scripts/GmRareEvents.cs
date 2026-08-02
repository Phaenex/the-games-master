// The layered rare events. Hooks for the window figure + dying lamp (armed once the mansion +
// drive lamps exist here).
//
// The chapel bell used to be a rare event that knocked you back on the third toll. It is now the
// house's clock and belongs to GmBellSummons -- see the ninth-bell spec. Shipping both would ring
// the same bell for two different reasons.
using UnityEngine;
using UnityEngine.SceneManagement;

public class GmRareEvents : MonoBehaviour
{
    /// The ESTATE's threshold, kept as a const because existing callers and tests reference it
    /// statically. It is a world-Z coordinate, so it is only meaningful in the estate scene.
    public const float FigureGoneBelowZ = 18f;

    /// Per-scene threshold. The village runs at completely different world Z (roughly -281..+4, so
    /// the estate's +18 sits off the north end of that entire map, and the figure would vanish the
    /// instant it armed). The village builder remaps this through GmVillageEstate.EstateZToVillageZ,
    /// exactly as gateZ, arrivalZ and the bell's chapel position are. Defaults to the estate value
    /// so that scene is unchanged.
    [SerializeField] float figureGoneBelowZ = FigureGoneBelowZ;

    public float FigureGoneBelow
    {
        get => figureGoneBelowZ;
        set => figureGoneBelowZ = value;
    }

    public static bool ForceFigureForTests;

    GmAmbience amb;
    Transform player;
    GameObject figureRig;
    bool figureArmed, figureGone;

    public bool FigureArmed => figureArmed;
    public bool FigureVisible => figureRig != null && figureRig.activeSelf;

    void Start()
    {
        amb = FindFirstObjectByType<GmAmbience>();
        var p = FindFirstObjectByType<GmPlayer>();
        if (p != null) player = p.transform;
        figureRig = FindSceneRoot("WindowFigureRig");
        figureArmed = ForceFigureForTests || Random.value < (1f / 3f);
        if (figureRig != null) figureRig.SetActive(figureArmed);
        else Debug.LogWarning("[GmRareEvents] WindowFigureRig missing — one-in-three figure disabled");
    }

    void Update()
    {
        if (player == null) return;
        EvaluateFigureAtZ(player.position.z);
    }

    /// Review-only counterpart to ForceFigureForReview. The deterministic tour hides the figure for
    /// the fourteen shots that are not about it, so those frames stop inheriting the one-in-three
    /// gameplay roll and reproduce bit-for-bit between runs.
    ///
    /// Deliberately NOT solved by seeding the roll: a fixed seed would make every playthrough
    /// identical and destroy the "about one walk in three" canon. The randomness is the feature; only
    /// the review capture needs to be deterministic.
    public void HideFigureForReview()
    {
        if (figureRig == null) figureRig = FindSceneRoot("WindowFigureRig");
        figureArmed = false;
        figureGone = false;
        if (figureRig != null) figureRig.SetActive(false);
    }

    /// Public only for the deterministic visual tour and EditMode lifecycle test. Normal gameplay
    /// still arms from the one-in-three roll in Start; no player-facing control calls this.
    public void ForceFigureForReview()
    {
        if (figureRig == null) figureRig = FindSceneRoot("WindowFigureRig");
        figureGone = false;
        figureArmed = true;
        if (figureRig != null) figureRig.SetActive(true);
    }

    public void EvaluateFigureAtZ(float playerZ)
    {
        if (!figureArmed || figureGone || playerZ >= figureGoneBelowZ) return;
        figureGone = true;
        if (figureRig != null) figureRig.SetActive(false);
        Debug.Log("[GmRareEvents] upper-window figure gone for this walk");
    }

    static GameObject FindSceneRoot(string rootName)
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) return null;
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == rootName) return root;
        return null;
    }
}

