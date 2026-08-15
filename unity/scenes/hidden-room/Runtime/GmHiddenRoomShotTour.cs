using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class GmHiddenRoomShotTour : GmSceneReviewTour
{
    static readonly GmReviewShot[] Shots =
    {
        // Camera positions below are verified against GmSceneCompositionAudit's actual viewport-height
        // and visibility math (WorldToViewportPoint against each element's real combined bounds), not
        // eyeballed, after the composition markers were re-linked to real geometry.
        new GmReviewShot("01-room-entrance", new Vector3(1.0f, 1.5f, -2.0f), -20f, 10f),
        new GmReviewShot("02-desk-invitation", new Vector3(0f, 1.1f, 1.2f), 0f, 35f),
        new GmReviewShot("03-mirror-shard3", new Vector3(-1.0f, 1.2f, 0f), -90f, 12f),
        new GmReviewShot("04-journal-archives", new Vector3(-1.0f, 1.5f, 0f), 90f, 0f),
        new GmReviewShot("05-mirror-reconstructed", new Vector3(0.2f, 1.2f, 0f), -90f, 0f),
        new GmReviewShot("06-room-wide", new Vector3(2.4f, 1.7f, 1.5f), -100f, 10f)
    };

    protected override IReadOnlyList<GmReviewShot> ReviewShots => Shots;
}

#if UNITY_EDITOR
public static class GmHiddenRoomShotTourMenu
{
    [MenuItem("GamesMaster/Scenes/Review Tour Hidden Room")]
    public static void ArmAndPlay()
    {
        GmSceneReviewTourMenu.ArmAndPlay<GmHiddenRoomShotTour>("Assets/Scenes/HiddenRoom.unity");
    }
}
#endif
