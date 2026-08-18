using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class GmEntryHallShotTour : GmSceneReviewTour
{
    static readonly GmReviewShot[] Shots =
    {
        // The previous post at z=-12 was outside the south wall at z=-10, so its green composition
        // result was a shot through solid architecture. This oblique post is inside the vestibule,
        // looking across both the settee and its practical without occupying either prop.
        new GmReviewShot("01-wake-vestibule", new Vector3(2.0f, 1.4f, -9.15f), -55f, 18f),
        new GmReviewShot("02-hall-overview", new Vector3(0f, 1.7f, -6f), 0f, 10f),
        new GmReviewShot("03-ledger-table", new Vector3(0f, 1.4f, -2.5f), 0f, 25f),
        // Re-aimed dead-center at GuestLedger's authored world position (0,0.945,-1) from a step
        // further back (was -1.6, now -2.2); LedgerLampFixture (0.4,1.05,-1) sits well inside frame
        // to one side instead of riding the frustum edge.
        new GmReviewShot("04-ledger-closeup", new Vector3(0f, 1.2f, -2.2f), 0f, 12f),
        // Percival's frame (z=+6) and Marr's frame (z=-6) sit 12m apart on the same wall. Standing at
        // the gallery's own y-height (2.2) with pitch 0 removes vertical offset entirely, and backing
        // the camera up to x=5.5 (just inside the east wall at x=6) widens the angular margin to both
        // frames versus the previous x=3 post, which put one of the two nearer the frustum edge.
        new GmReviewShot("05-portrait-gallery", new Vector3(5.5f, 2.2f, 0f), -90f, 0f),
        new GmReviewShot("06-percival-shard", new Vector3(-3.5f, 1.9f, 5.5f), -77f, 6f),
        // Close enough for the modeled balustrade to resolve under its east-wall practical, while
        // still retaining the chandelier and both flights in the wider composition.
        new GmReviewShot("07-staircase-landing", new Vector3(0f, 1.8f, 1.5f), 0f, 5f),
        // The doorway now exists. This post faces the actual north-east opening and its threshold
        // sconce, instead of preserving the obsolete solid-wall proof that predated the doors.
        new GmReviewShot("08-parlor-door", new Vector3(0f, 1.6f, 3.5f), 70f, 2f),
        new GmReviewShot("09-library-door", new Vector3(-2.55f, 1.55f, 7.15f), -32f, 3f),
        new GmReviewShot("10-stair-ascent", new Vector3(0f, 1.85f, 2.2f), 0f, 14f),
        new GmReviewShot("11-second-floor", new Vector3(0.2f, 4.9f, 11.3f), -48f, 2f),
        new GmReviewShot("12-percival-room", new Vector3(-6.55f, 4.8f, 13.2f), -92f, 6f),
        new GmReviewShot("13-library-interior", new Vector3(-5.25f, 1.52f, 13.2f), -90f, 12f),
        new GmReviewShot("14-weighted-shelf", new Vector3(-5.15f, 1.55f, 13.2f), 90f, 6f),
        new GmReviewShot("15-upper-gallery", new Vector3(2.35f, 4.95f, 13.2f), 90f, 4f),
        new GmReviewShot("16-marr-study", new Vector3(-2.4f, 4.92f, 17.15f), 0f, 8f),
        new GmReviewShot("17-barred-guest", new Vector3(2.4f, 4.92f, 17.15f), 0f, 8f),
        new GmReviewShot("18-attic-hatch", new Vector3(2.15f, 4.95f, 12.05f), 38f, -22f),
        new GmReviewShot("19-attic-loft", new Vector3(0.15f, 7.62f, 13.45f), -108f, 12f),
        new GmReviewShot("20-attic-shard", new Vector3(0.95f, 7.28f, 14.35f), -8f, 4f),
        new GmReviewShot("21-cellar-panel", new Vector3(5.42f, 1.58f, 8.35f), -90f, 2f),
        new GmReviewShot("22-cellar-descent", new Vector3(2.07f, 0.48f, 9.15f), 192f, 30f),
        new GmReviewShot("23-cellar-vault", new Vector3(3.15f, -1.46f, 5.55f), 165f, 4f)
    };

    protected override IReadOnlyList<GmReviewShot> ReviewShots => Shots;
}

#if UNITY_EDITOR
public static class GmEntryHallShotTourMenu
{
    [MenuItem("GamesMaster/Scenes/Review Tour Entry Hall")]
    public static void ArmAndPlay()
    {
        GmSceneReviewTourMenu.ArmAndPlay<GmEntryHallShotTour>("Assets/Scenes/EntryHall.unity");
    }
}
#endif
