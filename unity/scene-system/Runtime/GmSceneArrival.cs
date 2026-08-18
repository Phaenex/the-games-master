// The other half of the ninth-bell crossing, in the room you wake up in.
//
// GmCrossing owns everything up to the black: the cut on the ninth toll, the outbuildings closing,
// the dead air, the whisper. It cannot own what comes after, because the scene load that carries
// the player inside destroys it. So the sequence is deliberately split at the one point where
// nothing is visible — the arriving room opens the eyes it is now responsible for.
//
// A room reached any other way must not be forced through a wake it never had, so this does nothing
// unless the curtain is actually raised. Entering the Entry Hall from a save, or from the boot menu
// during development, starts with sight and control already yours.
using System.Collections;
using UnityEngine;

public sealed class GmSceneArrival : MonoBehaviour
{
    [Tooltip("Seconds for sight to return. Matches GmCrossing.irisTime so both paths through the " +
             "crossing feel like one moment rather than two implementations of it.")]
    [Range(0.1f, 6f)] public float irisSeconds = 2.2f;

    [Tooltip("Seconds the closing card is held once sight is back.")]
    [Range(0.1f, 12f)] public float cardSeconds = 4.2f;

    [TextArea]
    [Tooltip("Arithmetic, not dialogue. The payoff the whole nine-count exists for.")]
    public string closingCard = "Nine o'clock. On the hour. For the first time in my life, I was not late.";

    /// Set once the arrival has run, so a re-entered scene cannot wake the player twice.
    public bool HasArrived { get; private set; }

    IEnumerator Start()
    {
        GmSceneCurtain curtain = GmSceneCurtain.Instance;
        if (curtain == null || !curtain.IsRaised)
        {
            // Not a crossing. Nothing to undo, and nothing to log as a fault: this is the normal
            // path for every other way into the room.
            HasArrived = true;
            yield break;
        }

        var player = FindAnyObjectByType<GmPlayer>();
        if (player != null) player.SetControlBlocked(true);
        Debug.Log("[GmSceneArrival] woke behind the curtain — opening on the room he was never let into");

        yield return curtain.Open(irisSeconds);

        if (!string.IsNullOrWhiteSpace(closingCard))
        {
            curtain.ShowCard(closingCard);
            yield return new WaitForSeconds(cardSeconds);
            curtain.ShowCard("");
        }

        if (player != null) player.SetControlBlocked(false);
        HasArrived = true;
        Debug.Log("[GmSceneArrival] ARRIVAL COMPLETE — player control restored inside the house");
    }
}
