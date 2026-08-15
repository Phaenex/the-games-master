using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class GmEntryHallShotTour : GmSceneReviewTour
{
    static readonly GmReviewShot[] Shots =
    {
        // Re-derived straight from WakeSettee's authored transform (0,0.25,-8): aiming yaw/pitch at
        // its exact center from a further-back post (was -9.4, now -12) keeps the settee comfortably
        // inside the frustum with real margin instead of a near-edge fit, and still holds wake-lamp
        // (-1,1.55,-8.1) well inside frame off to one side.
        new GmReviewShot("01-wake-vestibule", new Vector3(0f, 1.5f, -12.0f), 0f, 17.5f),
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
        new GmReviewShot("06-percival-shard", new Vector3(-4.5f, 1.8f, 5.5f), -60f, 10f),
        // GrandStaircase's rotated AABB (Editor/GmEntryHallBuilder) spans roughly y[-0.55,3.55]
        // z[6.45,10.55]; ChandelierFixture sits at (0,4.8,0). A level, further-back post (0,3,-6)
        // keeps the staircase's full vertical span inside frame with real headroom on both edges
        // while the chandelier lands comfortably in the upper third of the same frame.
        new GmReviewShot("07-staircase-landing", new Vector3(0f, 3.0f, -6.0f), 0f, 0f),
        // Aimed dead-center at ConsoleTable's combined bounds (table + GuestLedger + LedgerQuill
        // children, center ~(0,0.49,-1)) from further into the east corner (was 4.5,-2.0, now
        // 5.5,-3.5) than the previous oblique guess. Still documents the solid east wall boundary
        // (no doorway to Aldric's parlor is built here) while keeping the grand staircase inside the
        // support-element frustum margin off to the far side.
        new GmReviewShot("08-parlor-door", new Vector3(5.5f, 1.9f, -3.5f), -65.6f, 13.1f)
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
