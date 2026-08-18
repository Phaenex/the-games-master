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
    const float SecondFloorSurfaceY = 3.4f;

    public static void Author(GameObject owner, IReadOnlyDictionary<string, GameObject> built)
    {
        GmCompositionAuthoring.Begin(owner, SceneId,
            "A cavernous, decaying Victorian entry hall where nine portraits and a guest ledger reveal the host's trapped cycle.",
            minZones: 6, minClusters: 7, minElements: 20);

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

        GmCompositionAuthoring.Element(Built(built, "brass-skeleton-key"), "brass-skeleton-key",
            "wake-cluster", "evidence-props",
            "Brass skeleton key left on the wake table, cut for the library lock.",
            GmCompositionRole.Gameplay, surfaceY: 0.5f);

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
            new Vector3(12f, 8f, 8f), minClusters: 1, minElements: 2);

        // Recentered onto the midpoint of the staircase and the hall chandelier it sits under: the
        // chandelier lights the whole vaulted ceiling, not just the stair landing, so it genuinely
        // sits ~9m from the zone's nominal center -- past the default cluster radius.
        var stairCluster = new GameObject("StairCluster");
        stairCluster.transform.SetParent(stairZone.transform, false);
        stairCluster.transform.position = new Vector3(0f, 3.0f, 4.5f);
        GmCompositionAuthoring.Cluster(stairCluster, "stair-cluster", "stair-zone",
            "Central red-carpeted staircase sweeping upward under the iron chandelier.", "grand-staircase");

        // The imported stair is fitted from its renderer bounds and then grounded to the actual hall
        // floor. The old blockout was a tilted box whose corner sat below zero; preserving that
        // measured defect here would make a floating replacement pass and a grounded one fail.
        GmCompositionAuthoring.Element(Built(built, "grand-staircase"), "grand-staircase", "stair-cluster",
            "hall-architecture", "The grand oak staircase dominating the north wall.",
            GmCompositionRole.Anchor, surfaceY: 0f);

        GmCompositionAuthoring.Element(Built(built, "hall-chandelier"), "hall-chandelier", "stair-cluster",
            "hall-lighting", "Large iron chandelier hanging from the vaulted ceiling.",
            GmCompositionRole.Support, GmSpatialRelation.Suspended);

        GmCompositionAuthoring.Element(Built(built, "stair-newel"), "stair-newel", "stair-cluster",
            "hall-architecture", "Carved newel post anchoring the base of the staircase balustrade.",
            GmCompositionRole.Detail);

        GmCompositionAuthoring.Element(Built(built, "stair-sconce"), "stair-sconce", "stair-cluster",
            "hall-lighting", "East-wall gas sconce revealing the upper stair and balustrade.",
            GmCompositionRole.Detail, GmSpatialRelation.Suspended);

        GmCompositionAuthoring.Element(Built(built, "stair-west-sconce"), "stair-west-sconce", "stair-cluster",
            "hall-lighting", "West-wall gas sconce balancing the second stair flight.",
            GmCompositionRole.Detail, GmSpatialRelation.Suspended);

        GmCompositionAuthoring.Element(Built(built, "parlor-door-sconce"), "parlor-door-sconce", "stair-cluster",
            "hall-lighting", "Gas sconce marking the turn toward the open parlor doorway.",
            GmCompositionRole.Detail, GmSpatialRelation.Suspended);

        GmCompositionAuthoring.Element(Built(built, "parlor-doorway"), "parlor-doorway", "stair-cluster",
            "hall-architecture", "Open double doors at the north-east turn leading into Aldric's parlor.",
            GmCompositionRole.Support, surfaceY: 0f);

        GmCompositionAuthoring.Element(Built(built, "cellar-panel"), "cellar-panel", "stair-cluster",
            "hall-architecture", "Secret under-stair panel. It will not open without the bookcase lever.",
            GmCompositionRole.Gameplay, surfaceY: 0f);

        GmCompositionAuthoring.Element(Built(built, "north-moon-window-west"), "north-moon-window-west",
            "stair-cluster", "hall-windows",
            "High west window admitting the cool night shaft behind the stair.",
            GmCompositionRole.Detail, GmSpatialRelation.AgainstBoundary, "grand-staircase", 8f,
            blocksRoutes: false);
        GmCompositionAuthoring.Element(Built(built, "north-moon-window-east"), "north-moon-window-east",
            "stair-cluster", "hall-windows",
            "High east window balancing the cool stair silhouette against the gas sconces.",
            GmCompositionRole.Detail, GmSpatialRelation.AgainstBoundary, "grand-staircase", 8f,
            blocksRoutes: false);

        // Every enabled local light needs a visible, nearby source or the audit's light-motivation
        // gate fails it. Every runtime practical the builder creates is covered here.
        GmCompositionAuthoring.Motivate(Built(built, "chandelier-light"), "hall-chandelier-light",
            "hall-chandelier", "Warm downlight thrown by the iron chandelier over the stair landing.");

        GmCompositionAuthoring.Motivate(Built(built, "ledger-lamp-light"), "ledger-lamp-light",
            "ledger-lamp", "Focused green-glass downlight illuminating the guest ledger.");

        GmCompositionAuthoring.Motivate(Built(built, "gallery-sconce-1-light"), "gallery-sconce-1-light",
            "gallery-sconce-1", "Warm wall-sconce glow on the south end of the portrait gallery.");

        GmCompositionAuthoring.Motivate(Built(built, "gallery-sconce-2-light"), "gallery-sconce-2-light",
            "gallery-sconce-2", "Warm wall-sconce glow on the north end of the portrait gallery.");

        GmCompositionAuthoring.Motivate(Built(built, "wake-lamp-light"), "wake-lamp-light",
            "wake-lamp", "Low dying gaslight revealing the settee where the player wakes.");

        GmCompositionAuthoring.Motivate(Built(built, "stair-sconce-light"), "stair-sconce-light",
            "stair-sconce", "Warm east-wall light separating the stair from the north-wall darkness.");

        GmCompositionAuthoring.Motivate(Built(built, "stair-west-sconce-light"), "stair-west-sconce-light",
            "stair-west-sconce", "Warm west-wall light separating the second flight and its balustrade.");

        GmCompositionAuthoring.Motivate(Built(built, "parlor-door-sconce-light"), "parlor-door-sconce-light",
            "parlor-door-sconce", "Threshold light marking the open double doors into the parlor.");

        var libraryZone = new GameObject("LibraryZone");
        libraryZone.transform.SetParent(owner.transform, false);
        libraryZone.transform.position = new Vector3(-6.7f, 1.5f, 13.2f);
        GmCompositionAuthoring.Zone(libraryZone, "library-zone",
            "North library past the keyed door: stacks, reading table, and the weighted east shelf.",
            new Vector3(10f, 4f, 8f), minClusters: 2, minElements: 6);

        var libraryCluster = new GameObject("LibraryCluster");
        libraryCluster.transform.SetParent(libraryZone.transform, false);
        libraryCluster.transform.position = new Vector3(-6.7f, 1.4f, 13.2f);
        GmCompositionAuthoring.Cluster(libraryCluster, "library-cluster", "library-zone",
            "Door threshold, north stacks, reading table, and the sconce that marks the way in.",
            "library-door", maxRadius: 12f);

        GmCompositionAuthoring.Element(Built(built, "library-door"), "library-door", "library-cluster",
            "hall-architecture", "Locked north library door that yields to the brass skeleton key.",
            GmCompositionRole.Anchor, surfaceY: 0f);

        GmCompositionAuthoring.Element(Built(built, "library-stub-shelf"), "library-stub-shelf",
            "library-cluster", "hall-furniture",
            "North-wall bookcase seen through the library door, the far stacks of the room.",
            GmCompositionRole.Support);

        GmCompositionAuthoring.Element(Built(built, "library-stub-lamp"), "library-stub-lamp",
            "library-cluster", "hall-lighting",
            "Gas sconce on the library side of the door so the threshold is not a silhouette.",
            GmCompositionRole.Detail, GmSpatialRelation.Suspended);

        GmCompositionAuthoring.Element(Built(built, "library-reading-table"), "library-reading-table",
            "library-cluster", "hall-furniture",
            "Reading table in the west aisle, clear of the door line so the room stays walkable.",
            GmCompositionRole.Support, surfaceY: 0f);

        GmCompositionAuthoring.Element(Built(built, "library-west-bay"), "library-west-bay",
            "library-cluster", "hall-furniture",
            "West-wall stacks with a rolling ladder and Eleanor's margin note.",
            GmCompositionRole.Detail);

        GmCompositionAuthoring.Element(Built(built, "library-ladder"), "library-ladder",
            "library-cluster", "hall-furniture",
            "Rolling library ladder parked against the west stacks.",
            GmCompositionRole.Detail, surfaceY: 0f);

        GmCompositionAuthoring.Element(Built(built, "library-reading-lamp"), "library-reading-lamp",
            "library-cluster", "hall-lighting",
            "Lamp on the reading table so the west aisle is not lit only by spill.",
            GmCompositionRole.Detail, GmSpatialRelation.Suspended);

        var shelfCluster = new GameObject("WeightedShelfCluster");
        shelfCluster.transform.SetParent(libraryZone.transform, false);
        shelfCluster.transform.position = new Vector3(-2.8f, 1.4f, 13.2f);
        GmCompositionAuthoring.Cluster(shelfCluster, "weighted-shelf-cluster", "library-zone",
            "Sagging east rail, five titled volumes, and the sconce that shows the numerals.",
            "library-weighted-shelf", maxRadius: 6f);

        GmCompositionAuthoring.Element(Built(built, "library-weighted-shelf"), "library-weighted-shelf",
            "weighted-shelf-cluster", "evidence-props",
            "The weighted shelf: five books on a notched brass rail, a lock shaped like an order.",
            GmCompositionRole.Gameplay, surfaceY: 1.18f, groundTolerance: 0.35f);

        GmCompositionAuthoring.Element(Built(built, "library-inscription"), "library-inscription",
            "weighted-shelf-cluster", "evidence-props",
            "Carved frame: Every game has an order. Even this one.",
            GmCompositionRole.Support, GmSpatialRelation.AgainstBoundary, "library-weighted-shelf", 1.5f);

        GmCompositionAuthoring.Element(Built(built, "library-eleanor-note"), "library-eleanor-note",
            "library-cluster", "evidence-props",
            "Eleanor's margin note naming the east shelf as a lock.",
            GmCompositionRole.Gameplay, surfaceY: 1.4f);

        GmCompositionAuthoring.Element(Built(built, "library-shelf-lamp"), "library-shelf-lamp",
            "weighted-shelf-cluster", "hall-lighting",
            "East-wall sconce so the Roman numerals are readable.",
            GmCompositionRole.Detail, GmSpatialRelation.Suspended);

        var upperZone = new GameObject("UpperZone");
        upperZone.transform.SetParent(owner.transform, false);
        upperZone.transform.position = new Vector3(0f, 4.8f, 13.2f);
        GmCompositionAuthoring.Zone(upperZone, "upper-zone",
            "Second-floor gallery reached by walking the grand staircase.",
            new Vector3(16f, 5f, 10f), minClusters: 2, minElements: 6);

        var landingCluster = new GameObject("UpperLandingCluster");
        landingCluster.transform.SetParent(upperZone.transform, false);
        landingCluster.transform.position = new Vector3(0f, 4.6f, 13.2f);
        GmCompositionAuthoring.Cluster(landingCluster, "upper-landing-cluster", "upper-zone",
            "Walkable 2F landing, open door into Percival's room, and the overhead fixture.",
            "second-floor-landing", maxRadius: 10f);

        GmCompositionAuthoring.Element(Built(built, "second-floor-landing"), "second-floor-landing",
            "upper-landing-cluster", "hall-architecture",
            "Second-floor gallery floor beyond the north arch.",
            GmCompositionRole.Anchor, surfaceY: SecondFloorSurfaceY);

        GmCompositionAuthoring.Element(Built(built, "percival-door"), "percival-door",
            "upper-landing-cluster", "hall-architecture",
            "Percival's door standing open onto a real bedroom.",
            GmCompositionRole.Support, surfaceY: SecondFloorSurfaceY);

        GmCompositionAuthoring.Element(Built(built, "landing-upper-lamp"), "landing-upper-lamp",
            "upper-landing-cluster", "hall-lighting",
            "Overhead fixture over the 2F gallery so the landing is not a black slot.",
            GmCompositionRole.Detail, GmSpatialRelation.Suspended);

        var upperDoorCluster = new GameObject("UpperDoorCluster");
        upperDoorCluster.transform.SetParent(upperZone.transform, false);
        upperDoorCluster.transform.position = new Vector3(0.8f, 4.8f, 15.4f);
        GmCompositionAuthoring.Cluster(upperDoorCluster, "upper-door-cluster", "upper-zone",
            "Locked, barred, and hatch doors that refuse the rest of the wing.",
            "marr-door", maxRadius: 10f);

        GmCompositionAuthoring.Element(Built(built, "marr-door"), "marr-door", "upper-door-cluster",
            "hall-architecture", "Lady Marr's study, locked, no key in this hall yet.",
            GmCompositionRole.Anchor, surfaceY: SecondFloorSurfaceY);

        GmCompositionAuthoring.Element(Built(built, "barred-guest-door"), "barred-guest-door",
            "upper-door-cluster", "hall-architecture",
            "A guest room barred from the other side.",
            GmCompositionRole.Support, surfaceY: SecondFloorSurfaceY);

        GmCompositionAuthoring.Element(Built(built, "attic-hatch"), "attic-hatch", "upper-door-cluster",
            "hall-architecture", "Locked attic hatch in the gallery vault.",
            GmCompositionRole.Detail, GmSpatialRelation.Suspended);

        var percivalZone = new GameObject("PercivalZone");
        percivalZone.transform.SetParent(owner.transform, false);
        percivalZone.transform.position = new Vector3(-8.6f, 4.8f, 13.2f);
        GmCompositionAuthoring.Zone(percivalZone, "percival-zone",
            "Percival's open guest bedroom, proof that the upstairs is a room and not a painted wall.",
            new Vector3(8f, 4f, 7f), minClusters: 1, minElements: 3);

        var percivalCluster = new GameObject("PercivalCluster");
        percivalCluster.transform.SetParent(percivalZone.transform, false);
        percivalCluster.transform.position = new Vector3(-8.6f, 4.6f, 13.2f);
        GmCompositionAuthoring.Cluster(percivalCluster, "percival-cluster", "percival-zone",
            "Gothic bed, writing desk, and the sconce that lights them.", "percival-bed");

        GmCompositionAuthoring.Element(Built(built, "percival-bed"), "percival-bed", "percival-cluster",
            "hall-furniture", "Percival's four-poster, the reason his door is worth opening.",
            GmCompositionRole.Anchor, surfaceY: SecondFloorSurfaceY);

        GmCompositionAuthoring.Element(Built(built, "percival-desk"), "percival-desk", "percival-cluster",
            "hall-furniture", "A small writing desk against the south wall of Percival's room.",
            GmCompositionRole.Support, surfaceY: SecondFloorSurfaceY);

        GmCompositionAuthoring.Element(Built(built, "percival-lamp"), "percival-lamp", "percival-cluster",
            "hall-lighting", "Sconce over Percival's bed so the room is not lit only by spill.",
            GmCompositionRole.Detail, GmSpatialRelation.Suspended);

        GmCompositionAuthoring.Element(Built(built, "front-doors"), "front-doors", "wake-cluster",
            "hall-architecture", "Barred front doors. Threshold Refusal holds indoors.",
            GmCompositionRole.Support, surfaceY: 0f);

        GmCompositionAuthoring.Element(Built(built, "conservatory-door"), "conservatory-door",
            "wake-cluster", "hall-architecture",
            "Conservatory door barred from the other side.",
            GmCompositionRole.Detail, surfaceY: 0f);

        GmCompositionAuthoring.Motivate(Built(built, "library-stub-lamp-light"), "library-stub-lamp-light",
            "library-stub-lamp", "Warm sconce over the library door threshold.");

        GmCompositionAuthoring.Motivate(Built(built, "library-reading-lamp-light"), "library-reading-lamp-light",
            "library-reading-lamp", "Table lamp lighting the west reading aisle.");

        GmCompositionAuthoring.Motivate(Built(built, "library-shelf-lamp-light"), "library-shelf-lamp-light",
            "library-shelf-lamp", "East-wall sconce on the weighted shelf numerals.");

        GmCompositionAuthoring.Motivate(Built(built, "landing-upper-lamp-light"), "landing-upper-lamp-light",
            "landing-upper-lamp", "Overhead light on the second-floor gallery.");

        GmCompositionAuthoring.Motivate(Built(built, "percival-lamp-light"), "percival-lamp-light",
            "percival-lamp", "Sconce lighting Percival's bed and desk.");

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

        GmCompositionAuthoring.ReviewClaim(owner, "07-staircase-landing", "grand-staircase", "stair-sconce",
            "stair-cluster", "stair-zone", new Vector2(0.5f, 0.6f), 0.55f,
            "Proves the staircase landing, balustrades, and the practical revealing the dark upper turn.");

        GmCompositionAuthoring.ReviewClaim(owner, "08-parlor-door", "parlor-doorway", "parlor-door-sconce",
            "stair-cluster", "stair-zone", new Vector2(0.55f, 0.50f), 0.45f,
            "Proves the open double doors, modeled leaves, and lit threshold leading into Aldric's parlor.");

        GmCompositionAuthoring.ReviewClaim(owner, "09-library-door", "library-door", "library-stub-shelf",
            "library-cluster", "library-zone", new Vector2(0.45f, 0.5f), 0.50f,
            "Proves the locked library door and the north stacks through the opening.");

        GmCompositionAuthoring.ReviewClaim(owner, "10-stair-ascent", "grand-staircase", "second-floor-landing",
            "stair-cluster", "stair-zone", new Vector2(0.5f, 0.55f), 0.50f,
            "Proves the staircase is a climb, not a solid north-wall box.");

        GmCompositionAuthoring.ReviewClaim(owner, "11-second-floor", "percival-door", "marr-door",
            "upper-landing-cluster", "upper-zone", new Vector2(0.5f, 0.5f), 0.50f,
            "Proves the 2F gallery, Percival's open door, and the doors that refuse the rest.");

        GmCompositionAuthoring.ReviewClaim(owner, "12-percival-room", "percival-bed", "percival-desk",
            "percival-cluster", "percival-zone", new Vector2(0.5f, 0.5f), 0.50f,
            "Proves Percival's bedroom is a furnished room, not an empty box behind an open door.");

        GmCompositionAuthoring.ReviewClaim(owner, "13-library-interior", "library-reading-table",
            "library-stub-shelf", "library-cluster", "library-zone", new Vector2(0.5f, 0.5f), 0.50f,
            "Proves the north library is a walkable room of stacks and a reading table, not a stub.");

        GmCompositionAuthoring.ReviewClaim(owner, "14-weighted-shelf", "library-weighted-shelf",
            "library-inscription", "weighted-shelf-cluster", "library-zone", new Vector2(0.55f, 0.5f),
            0.50f, "Proves the sagging east rail, five titled spines, and the carved order.");
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
