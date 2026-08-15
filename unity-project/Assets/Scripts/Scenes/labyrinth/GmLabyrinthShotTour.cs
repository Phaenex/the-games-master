using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class GmLabyrinthShotTour : GmSceneReviewTour
{
    static readonly GmReviewShot[] Shots =
    {
        // Camera positions verified against GmSceneCompositionAudit's actual viewport-height and
        // visibility math (WorldToViewportPoint against each element's real combined bounds).
        new GmReviewShot("01-crypt-entrance", new Vector3(-15f, 1.7f, -11f), 180f, 15f),
        new GmReviewShot("02-hedge-corridor", new Vector3(-17f, 1.7f, -17f), 30f, 5f),
        new GmReviewShot("03-mirror-shrine", new Vector3(0f, 1.8f, -4f), 0f, 15f),
        new GmReviewShot("04-huntsman-patrol", new Vector3(5f, 1.7f, -8f), 0f, 10f),
        // Backed off from z=-6.0: a 2.04m totem shot from 1.84m overflowed the frame top and bottom
        // (viewport height 1.00 against a 0.90 ceiling), so the review shot could not do the job a
        // review shot exists for. Nothing was misplaced; only the camera was too close. Open ground
        // -- the hedge blocks sit at x in [-2,2] and [8,12], so the sightline along x=4.5 is clear.
        new GmReviewShot("05-bone-totem", new Vector3(4.5f, 1.0f, -7.0f), 0f, 12f),
        new GmReviewShot("06-moonlight-clearing", new Vector3(0f, 1.5f, -9f), 0f, 5f),
        new GmReviewShot("07-exit-gate", new Vector3(15f, 1.8f, 10.5f), 0f, 8f),
        new GmReviewShot("08-maze-wide", new Vector3(-20f, 5f, -20f), 45f, 20f)
    };

    protected override IReadOnlyList<GmReviewShot> ReviewShots => Shots;
}

#if UNITY_EDITOR
public static class GmLabyrinthShotTourMenu
{
    [MenuItem("GamesMaster/Scenes/Review Tour Labyrinth")]
    public static void ArmAndPlay()
    {
        GmSceneReviewTourMenu.ArmAndPlay<GmLabyrinthShotTour>("Assets/Scenes/Labyrinth.unity");
    }
}
#endif
