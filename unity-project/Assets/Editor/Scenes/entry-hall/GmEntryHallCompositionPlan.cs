using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Composition plan for the Entry Hall. Defines the zones, clusters, motivated lighting,
/// and review claims for the 9-portrait gallery, guest ledger, and staircase.
/// Zones and clusters are authored volumes and own their own transforms; every element marker goes
/// onto the object GmEntryHallBuilder actually built, because the audit measures elements through
/// their renderers and an element with none is an intent nothing can check.
/// </summary>
public static class GmEntryHallCompositionPlan
{
    public const string SceneId = "entry-hall";

    public static void Author(GameObject owner, IReadOnlyDictionary<string, GameObject> built)
    {
        GmCompositionAuthoring.Begin(owner, SceneId,
            "A cavernous, decaying Victorian entry hall where nine portraits and a guest ledger reveal the host's trapped cycle.",
            minZones: 4, minClusters: 4, minElements: 12);

        // Zone 1: Wake Vestibule
        var wakeZone = new GameObject("WakeZone");
        wakeZone.transform.SetParent(owner.transform, false);
        wakeZone.transform.position = new Vector3(0f, 1.5f, -8f);
        GmCompositionAuthoring.Zone(wakeZone, "wake-zone",
            "Transition vestibule where the player awakens after the porch knockout.",
            new Vector3(6f, 4f, 6f), minClusters: 1, minElements: 3);

        var wakeCluster = new GameObject("WakeCluster");
        wakeCluster.transform.SetParent(wakeZone.transform, false);
        wakeCluster.transform.position = new Vector3(0f, 0f, -8f);
        GmCompositionAuthoring.Cluster(wakeCluster, "wake-cluster", "wake-zone",
            "Low velvet settee and small marble table beneath a dying gas wall sconce.", "wake-settee");

        GmCompositionAuthoring.Element(Built(built, "wake-settee"), "wake-settee", "wake-cluster",
            "hall-furniture", "The low settee where the player regains footing.", GmCompositionRole.Anchor);

        GmCompositionAuthoring.Element(Built(built, "wake-lamp"), "wake-lamp", "wake-cluster",
            "hall-lighting", "Flickering gas sconce casting warm light on the floor.",
            GmCompositionRole.Support, GmSpatialRelation.Suspended);

        GmCompositionAuthoring.Element(Built(built, "wake-table"), "wake-table", "wake-cluster",
            "hall-furniture", "Small marble table beside the settee, dressed for the wake vestibule.",
            GmCompositionRole.Detail);

        // Zone 2: Foyer & Ledger
        var foyerZone = new GameObject("FoyerZone");
        foyerZone.transform.SetParent(owner.transform, false);
        foyerZone.transform.position = new Vector3(0f, 1.5f, 0f);
        GmCompositionAuthoring.Zone(foyerZone, "foyer-zone",
            "Central hallway containing the console table and the critical Guest Ledger.",
            new Vector3(10f, 6f, 10f), minClusters: 1, minElements: 4);

        var ledgerCluster = new GameObject("LedgerCluster");
        ledgerCluster.transform.SetParent(foyerZone.transform, false);
        ledgerCluster.transform.position = new Vector3(0f, 0f, -1f);
        GmCompositionAuthoring.Cluster(ledgerCluster, "ledger-cluster", "foyer-zone",
            "Console table holding the open leather guest ledger under direct lamplight.", "console-table");

        GmCompositionAuthoring.Element(Built(built, "console-table"), "console-table", "ledger-cluster",
            "hall-furniture", "Carved dark mahogany console table centered in the hall.", GmCompositionRole.Anchor);

        // Ledger, lamp and quill all rest on the tabletop (y ~0.9), not the floor, so they declare
        // the table top as their surface rather than the default the floor items use.
        GmCompositionAuthoring.Element(Built(built, "ledger-book"), "ledger-book", "ledger-cluster",
            "evidence-props", "The 9-name guest ledger with Percival and the blank 10th host line.",
            GmCompositionRole.Gameplay, surfaceY: 0.9f);

        GmCompositionAuthoring.Element(Built(built, "ledger-lamp"), "ledger-lamp", "ledger-cluster",
            "hall-lighting", "Green-shaded reading lamp illuminating the ledger text.",
            GmCompositionRole.Support, surfaceY: 0.9f);

        GmCompositionAuthoring.Element(Built(built, "ledger-quill"), "ledger-quill", "ledger-cluster",
            "evidence-props", "Quill and inkwell resting where the last guest signed the ledger.",
            GmCompositionRole.Detail, surfaceY: 0.9f);

        // Zone 3: Portrait Gallery
        var galleryZone = new GameObject("GalleryZone");
        galleryZone.transform.SetParent(owner.transform, false);
        galleryZone.transform.position = new Vector3(-4.5f, 2.5f, 2f);
        GmCompositionAuthoring.Zone(galleryZone, "gallery-zone",
            "Long wall lined with 9 gilded portraits of previous guests and victims.",
            new Vector3(4f, 5f, 12f), minClusters: 1, minElements: 3);

        // Cluster origin sits on the gallery's actual centerline (the real portrait wall spans
        // z -6..+6 around x=-5.8), not the zone's nominal center: the audit measures every member's
        // renderer against this point, and the zone's own center sat ~8m from the farthest portrait.
        var portraitCluster = new GameObject("PortraitCluster");
        portraitCluster.transform.SetParent(galleryZone.transform, false);
        portraitCluster.transform.position = new Vector3(-5.8f, 2.2f, 0f);
        GmCompositionAuthoring.Cluster(portraitCluster, "portrait-cluster", "gallery-zone",
            "Arrangement of nine framed portraits centered on Percival's worn plate.", "percival-frame");

        GmCompositionAuthoring.Element(Built(built, "percival-frame"), "percival-frame", "portrait-cluster",
            "portraits", "Percival's portrait frame concealing Shard #1.",
            GmCompositionRole.Anchor, GmSpatialRelation.Suspended);

        GmCompositionAuthoring.Element(Built(built, "marr-frame"), "marr-frame", "portrait-cluster",
            "portraits", "Edwin Marr's portrait showing 'WATCH HIS HANDS' clue.",
            GmCompositionRole.Support, GmSpatialRelation.Suspended);

        // "Hidden behind Percival's canvas" is a spatial claim as well as a narrative one: the shard
        // is authored against Percival's frame so the audit checks the two are actually close.
        GmCompositionAuthoring.Element(Built(built, "shard-one"), "shard-one", "portrait-cluster",
            "shards", "Mirror Shard #1 hidden behind Percival's canvas.",
            GmCompositionRole.Gameplay, GmSpatialRelation.AgainstBoundary, "percival-frame",
            maxRelationDistance: 1.0f);

        GmCompositionAuthoring.Element(Built(built, "gallery-sconce-1"), "gallery-sconce-1", "portrait-cluster",
            "hall-lighting", "Wall sconce lighting the south end of the portrait gallery.",
            GmCompositionRole.Detail, GmSpatialRelation.Suspended);

        GmCompositionAuthoring.Element(Built(built, "gallery-sconce-2"), "gallery-sconce-2", "portrait-cluster",
            "hall-lighting", "Wall sconce lighting the north end of the portrait gallery.",
            GmCompositionRole.Detail, GmSpatialRelation.Suspended);

        // Zone 4: Grand Staircase
        var stairZone = new GameObject("StairZone");
        stairZone.transform.SetParent(owner.transform, false);
        stairZone.transform.position = new Vector3(0f, 3.0f, 8f);
        GmCompositionAuthoring.Zone(stairZone, "stair-zone",
            "Grand bifurcated staircase leading to upper gallery and darkened landings.",
            new Vector3(10f, 8f, 8f), minClusters: 1, minElements: 2);

        // Recentered onto the midpoint of the staircase and the hall chandelier it sits under: the
        // chandelier lights the whole vaulted ceiling, not just the stair landing, so it genuinely
        // sits ~9m from the zone's nominal center -- past the default cluster radius.
        var stairCluster = new GameObject("StairCluster");
        stairCluster.transform.SetParent(stairZone.transform, false);
        stairCluster.transform.position = new Vector3(0f, 3.0f, 4.5f);
        GmCompositionAuthoring.Cluster(stairCluster, "stair-cluster", "stair-zone",
            "Central red-carpeted staircase sweeping upward under the iron chandelier.", "grand-staircase");

        // The staircase blockout is a single box tilted 30 degrees around its center, so its lowest
        // rendered corner sits below y=0 even though the stairs conceptually rise from the floor.
        // Declared surface y is the measured value, not the nominal one.
        GmCompositionAuthoring.Element(Built(built, "grand-staircase"), "grand-staircase", "stair-cluster",
            "hall-architecture", "The grand oak staircase dominating the north wall.",
            GmCompositionRole.Anchor, surfaceY: -0.55f);

        GmCompositionAuthoring.Element(Built(built, "hall-chandelier"), "hall-chandelier", "stair-cluster",
            "hall-lighting", "Large iron chandelier hanging from the vaulted ceiling.",
            GmCompositionRole.Support, GmSpatialRelation.Suspended);

        GmCompositionAuthoring.Element(Built(built, "stair-newel"), "stair-newel", "stair-cluster",
            "hall-architecture", "Carved newel post anchoring the base of the staircase balustrade.",
            GmCompositionRole.Detail);

        // Every enabled local light needs a visible, nearby source or the audit's light-motivation
        // gate fails it. All four runtime lights the builder creates are covered here.
        GmCompositionAuthoring.Motivate(Built(built, "chandelier-light"), "hall-chandelier-light",
            "hall-chandelier", "Warm downlight thrown by the iron chandelier over the stair landing.");

        GmCompositionAuthoring.Motivate(Built(built, "ledger-lamp-light"), "ledger-lamp-light",
            "ledger-lamp", "Focused green-glass downlight illuminating the guest ledger.");

        GmCompositionAuthoring.Motivate(Built(built, "gallery-sconce-1-light"), "gallery-sconce-1-light",
            "gallery-sconce-1", "Warm wall-sconce glow on the south end of the portrait gallery.");

        GmCompositionAuthoring.Motivate(Built(built, "gallery-sconce-2-light"), "gallery-sconce-2-light",
            "gallery-sconce-2", "Warm wall-sconce glow on the north end of the portrait gallery.");

        // Author explicit review claims (8 shots).
        // The trailing float on every one of these is passed through the interim ReviewClaim adapter
        // as a symmetric viewport tolerance. That reading is NOT confirmed and it is Nick's to settle
        // (decision D1 in docs/audit/F1-review-claim-decision.md): the numbers run backwards for a
        // tolerance, and several are wider than the window the visibility test already enforces, so
        // those framing assertions cannot fail. A green composition audit is therefore not evidence
        // that these shots are framed. Do not retune these numbers to move a gate.
        GmCompositionAuthoring.ReviewClaim(owner, "01-wake-vestibule", "wake-settee", "wake-lamp",
            "wake-zone", "foyer-zone", new Vector2(0.5f, 0.4f), 0.35f,
            "Proves the player awakens on the settee with clear framing into the main hall.");

        GmCompositionAuthoring.ReviewClaim(owner, "02-hall-overview", "grand-staircase", "console-table",
            "foyer-zone", "stair-zone", new Vector2(0.5f, 0.6f), 0.45f,
            "Proves the hall's dramatic scale, marble floor, runner, and overhead chandelier.");

        GmCompositionAuthoring.ReviewClaim(owner, "03-ledger-table", "ledger-book", "console-table",
            "ledger-cluster", "foyer-zone", new Vector2(0.5f, 0.5f), 0.50f,
            "Proves the console table and leather guest ledger are crisp and inspectable.");

        GmCompositionAuthoring.ReviewClaim(owner, "04-ledger-closeup", "ledger-book", "ledger-lamp",
            "ledger-cluster", "foyer-zone", new Vector2(0.5f, 0.5f), 0.80f,
            "Proves the 9 ledger names, worn 9th entry, and blank line 10 are legible.");

        GmCompositionAuthoring.ReviewClaim(owner, "05-portrait-gallery", "percival-frame", "marr-frame",
            "portrait-cluster", "gallery-zone", new Vector2(0.4f, 0.5f), 0.60f,
            "Proves all nine portraits are hung in order with brass nameplates.");

        GmCompositionAuthoring.ReviewClaim(owner, "06-percival-shard", "shard-one", "percival-frame",
            "portrait-cluster", "gallery-zone", new Vector2(0.5f, 0.5f), 0.40f,
            "Proves Shard #1 is visible behind the loose corner of Percival's frame.");

        GmCompositionAuthoring.ReviewClaim(owner, "07-staircase-landing", "grand-staircase", "hall-chandelier",
            "stair-cluster", "stair-zone", new Vector2(0.5f, 0.6f), 0.55f,
            "Proves the staircase landing, balustrades, and dark upper archways.");

        // NOT a door shot: GmEntryHallBuilder builds no door geometry anywhere in the hall, so a
        // claim that this proves "closed double doors" would describe something not in the scene.
        // The camera (see GmEntryHallShotTour "08-parlor-door") stands near the east wall and turns
        // back across the foyer -- framing the console table and, further off, the grand staircase --
        // rather than facing the wall square-on, because a camera pointed due east at the wall put its
        // declared primary (console-table, to the west) entirely behind it. Standing at the boundary
        // and looking back still documents that nothing (no door) stands between camera and wall.
        // Where Aldric's parlor actually connects to this hall is a level-layout call for Nick, not
        // something to invent here. Text corrected to match what is actually built; deferred:
        // build the real doorway once that layout call is made.
        GmCompositionAuthoring.ReviewClaim(owner, "08-parlor-door", "console-table", "grand-staircase",
            "foyer-zone", "stair-zone", new Vector2(0.6f, 0.45f), 0.40f,
            "Proves the solid east wall boundary near the foyer; no doorway to Aldric's parlor is " +
            "built in this scene yet, so this shot does not prove a parlor entrance.");
    }

    // A composition marker belongs on the built object or it measures nothing. A missing key means
    // the builder and this plan have drifted apart, which is worth stopping the build for: quietly
    // authoring onto a fresh empty GameObject is the exact failure this lookup exists to prevent.
    static GameObject Built(IReadOnlyDictionary<string, GameObject> built, string id)
    {
        if (built == null || !built.TryGetValue(id, out GameObject go) || go == null)
            throw new InvalidOperationException(
                $"[GmEntryHall] composition element '{id}' has no object from GmEntryHallBuilder");
        return go;
    }
}
