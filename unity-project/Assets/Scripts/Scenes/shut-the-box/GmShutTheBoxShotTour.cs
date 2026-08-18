using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class GmShutTheBoxShotTour : GmSceneReviewTour
{
    static readonly GmReviewShot[] Shots =
    {
        // Camera positions verified against GmSceneCompositionAudit's actual viewport-height and
        // visibility math (WorldToViewportPoint against each element's real combined bounds).
        new GmReviewShot("01-table-overview", new Vector3(0f, 1.4f, -1.2f), 0f, 25f),
        new GmReviewShot("02-player-board-focus", new Vector3(0f, 1.3f, -1.8f), 0f, 35f),
        new GmReviewShot("03-host-board-focus", new Vector3(0f, 1.1f, 0.2f), 0f, 40f),
        new GmReviewShot("04-dice-tray-action", new Vector3(0f, 1.4f, -1.0f), 0f, 45f),
        new GmReviewShot("05-tile9-door-seam", new Vector3(1.0f, 1.4f, 0f), 90f, 8f),
        new GmReviewShot("06-hold-verb-framing", new Vector3(0f, 1.1f, -1.3f), 0f, 15f),
        new GmReviewShot("07-alcove-mood", new Vector3(-1.4f, 1.6f, -1.5f), 30f, 0f),
        new GmReviewShot("08-room-wide", new Vector3(-2.5f, 1.8f, -2.5f), 45f, 15f),
        new GmReviewShot("09-hidden-passage-open", new Vector3(1.0f, 1.4f, 0f), 90f, 8f),
        new GmReviewShot("10-labyrinth-passage-open", new Vector3(0f, 2.4f, -1.1f), 180f, 23f)
    };

    protected override IReadOnlyList<GmReviewShot> ReviewShots => Shots;

    protected override void BeforeTour() => GmRunStore.BeginNewRun();

    protected override void BeforeShot(GmReviewShot shot)
    {
        if (shot.Name == "09-hidden-passage-open")
            GmRunStore.RecordCatch("stb-tile-9-door-latch");
        else if (shot.Name == "10-labyrinth-passage-open")
            GmRunStore.CompleteRoom("shut-the-box", countsAsTableGame: true);
        else return;

        foreach (var exit in FindObjectsByType<GmSequenceExit>(FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
            exit.SnapOpenForReview();
    }
}

#if UNITY_EDITOR
public static class GmShutTheBoxShotTourMenu
{
    [MenuItem("GamesMaster/Scenes/Review Tour Shut the Box")]
    public static void ArmAndPlay()
    {
        GmSceneReviewTourMenu.ArmAndPlay<GmShutTheBoxShotTour>("Assets/Scenes/ShutTheBox.unity");
    }
}
#endif
