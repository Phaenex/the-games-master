using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class GmParlorShotTour : GmSceneReviewTour
{
    static readonly GmReviewShot[] Shots =
    {
        // Camera positions verified against GmSceneCompositionAudit's actual viewport-height and
        // visibility math (WorldToViewportPoint against each element's real combined bounds).
        new GmReviewShot("01-table-perspective", new Vector3(0f, 1.3f, -2.3f), 0f, 15f),
        new GmReviewShot("02-aldric-portrait-framing", new Vector3(0f, 1.3f, -1.6f), 0f, 10f),
        new GmReviewShot("03-card-hand-layout", new Vector3(0f, 1.35f, -0.9f), 0f, 40f),
        new GmReviewShot("04-banker-lamp-focus", new Vector3(0.2f, 1.1f, -0.4f), 45f, 20f),
        new GmReviewShot("05-hearth-glow", new Vector3(1.0f, 1.3f, 0f), 90f, 10f),
        new GmReviewShot("06-clock-closeup", new Vector3(2.5f, 1.6f, 0f), 90f, 5f),
        new GmReviewShot("07-cabinet-corner", new Vector3(-1.5f, 1.4f, 0f), -90f, 10f),
        new GmReviewShot("08-entry-drapes", new Vector3(0f, 1.4f, -1.0f), 180f, 5f),
        new GmReviewShot("09-the-read-focus", new Vector3(-0.2f, 1.0f, -0.6f), 20f, 5f),
        new GmReviewShot("10-room-wide", new Vector3(-2.8f, 2.0f, -2.8f), 45f, 15f)
    };

    protected override IReadOnlyList<GmReviewShot> ReviewShots => Shots;
}

#if UNITY_EDITOR
public static class GmParlorShotTourMenu
{
    [MenuItem("GamesMaster/Scenes/Review Tour Parlor")]
    public static void ArmAndPlay()
    {
        GmSceneReviewTourMenu.ArmAndPlay<GmParlorShotTour>("Assets/Scenes/Parlor.unity");
    }
}
#endif
