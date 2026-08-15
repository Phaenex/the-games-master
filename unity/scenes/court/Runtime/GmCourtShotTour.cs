using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class GmCourtShotTour : GmSceneReviewTour
{
    static readonly GmReviewShot[] Shots =
    {
        new GmReviewShot("01-court-overview", new Vector3(0f, 1.6f, -4.0f), 0f, 10f),
        new GmReviewShot("02-jury-wall-perspective", new Vector3(-1.5f, 1.8f, 0f), -90f, 10f),
        new GmReviewShot("03-witness-spotlight", new Vector3(0f, 1.5f, -2.8f), 0f, 15f),
        new GmReviewShot("04-evidence-table", new Vector3(0f, 1.4f, 1.2f), 0f, 25f),
        new GmReviewShot("05-gavel-tarnish-tell", new Vector3(0.25f, 2.3f, 4.8f), 0f, 22f),
        new GmReviewShot("06-shard2-placement", new Vector3(0.6f, 1.4f, 1.7f), 15f, 25f),
        new GmReviewShot("07-bench-elevation", new Vector3(0f, 0.8f, 3.8f), 0f, -12f),
        new GmReviewShot("08-defense-stand", new Vector3(0f, 1.5f, 0.8f), 180f, 12f)
    };

    protected override IReadOnlyList<GmReviewShot> ReviewShots => Shots;
}

#if UNITY_EDITOR
public static class GmCourtShotTourMenu
{
    [MenuItem("GamesMaster/Scenes/Review Tour Court")]
    public static void ArmAndPlay()
    {
        GmSceneReviewTourMenu.ArmAndPlay<GmCourtShotTour>("Assets/Scenes/Court.unity");
    }
}
#endif
