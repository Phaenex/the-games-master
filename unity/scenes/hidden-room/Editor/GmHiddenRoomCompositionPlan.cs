using UnityEngine;

/// <summary>
/// Composition plan for the Hidden Room (Phase 6). Defines the zones, clusters, motivated lighting,
/// and review claims for Aldric's original invitation, the journal archives, and Shard #3 in the mirror frame.
/// </summary>
public static class GmHiddenRoomCompositionPlan
{
    public const string SceneId = "hidden-room";

    // Zones and clusters are logical volumes and stay on their own marker objects. Elements do not:
    // every one of them is attached to the geometry GmHiddenRoomBuilder actually creates, because
    // GmSceneCompositionAudit resolves an element through the renderers below it and rejects a
    // marker that has none.
    public static void Author(GameObject owner, GmHiddenRoomProps props)
    {
        GmCompositionAuthoring.Begin(owner, SceneId,
            "A dust-choked secret chamber behind the wainscoting where Aldric's original invitation, the eight guest journals, and the fractured mirror frame are stored.",
            minZones: 4, minClusters: 4, minElements: 12);

        // Zone 1: Secret Desk Zone
        var deskZone = new GameObject("DeskZone");
        deskZone.transform.SetParent(owner.transform, false);
        deskZone.transform.position = new Vector3(0f, 1.2f, 2f);
        GmCompositionAuthoring.Zone(deskZone, "desk-zone",
            "Dusty rolltop desk containing Aldric's original invitation letter and wax inkpot.",
            new Vector3(4f, 3f, 4f), minClusters: 1, minElements: 3);

        var deskCluster = new GameObject("DeskCluster");
        deskCluster.transform.SetParent(deskZone.transform, false);
        deskCluster.transform.position = new Vector3(0f, 0f, 2f);
        GmCompositionAuthoring.Cluster(deskCluster, "desk-cluster", "desk-zone",
            "Rolltop desk illuminated by a low oil lantern.", "rolltop-desk");

        GmCompositionAuthoring.Element(props.RolltopDesk, "rolltop-desk", "desk-cluster", "hidden-furniture",
            "A heavy rolltop desk with pigeonholes stuffed with yellowed papers.", GmCompositionRole.Anchor);

        GmCompositionAuthoring.Element(props.InvitationLetter, "invitation-letter", "desk-cluster", "hidden-props",
            "Aldric Voss's original un-decayed invitation letter revealing the cycle contract.",
            GmCompositionRole.Gameplay, GmSpatialRelation.Grounded,
            surfaceY: GmHiddenRoomBuilder.DeskSurfaceY);

        GmCompositionAuthoring.Element(props.DeskLantern, "desk-lantern", "desk-cluster", "hidden-lighting",
            "Brass oil lantern casting warm yellow grazing light across desk documents.",
            GmCompositionRole.Support, GmSpatialRelation.Grounded,
            surfaceY: GmHiddenRoomBuilder.DeskSurfaceY);

        GmCompositionAuthoring.Element(props.WaxInkpot, "wax-inkpot", "desk-cluster", "hidden-props",
            "Sealed wax inkpot beside the letter, the detail the desk zone already names.",
            GmCompositionRole.Detail, GmSpatialRelation.Grounded,
            surfaceY: GmHiddenRoomBuilder.DeskSurfaceY);

        // Zone 2: Broken Mirror Zone
        var mirrorZone = new GameObject("MirrorZone");
        mirrorZone.transform.SetParent(owner.transform, false);
        mirrorZone.transform.position = new Vector3(-2.8f, 1.8f, 0f);
        GmCompositionAuthoring.Zone(mirrorZone, "mirror-zone",
            "Full-length standing mirror frame with slots for all three mirror shards.",
            new Vector3(3f, 4f, 4f), minClusters: 1, minElements: 3);

        var mirrorCluster = new GameObject("MirrorCluster");
        mirrorCluster.transform.SetParent(mirrorZone.transform, false);
        mirrorCluster.transform.position = new Vector3(-2.8f, 0f, 0f);
        GmCompositionAuthoring.Cluster(mirrorCluster, "mirror-cluster", "mirror-zone",
            "Gilded standing mirror frame missing its glass.", "mirror-frame");

        GmCompositionAuthoring.Element(props.MirrorFrame, "mirror-frame", "mirror-cluster", "hidden-furniture",
            "Ornate gilded standing mirror frame.", GmCompositionRole.Anchor);

        GmCompositionAuthoring.Element(props.ShardThree, "shard-three", "mirror-cluster", "shards",
            "Mirror Shard #3 resting inside the fractured wooden bezel.",
            GmCompositionRole.Gameplay, GmSpatialRelation.Embedded);

        GmCompositionAuthoring.Element(props.ShardReceptacle, "mirror-glow-light", "mirror-cluster", "hidden-lighting",
            "Pale ethereal blue highlight from the shard receptacle.",
            GmCompositionRole.Support, GmSpatialRelation.Embedded);

        GmCompositionAuthoring.Element(props.EmptyShardSlots, "shard-slots", "mirror-cluster", "hidden-decor",
            "The two empty slots above and below the shard, cut for glass that is still elsewhere.",
            GmCompositionRole.Detail, GmSpatialRelation.Embedded);

        // Zone 3: Archive Shelf Zone
        var shelfZone = new GameObject("ShelfZone");
        shelfZone.transform.SetParent(owner.transform, false);
        shelfZone.transform.position = new Vector3(2.8f, 1.8f, 0f);
        GmCompositionAuthoring.Zone(shelfZone, "shelf-zone",
            "Archive shelves storing the journals of the previous eight victims.",
            new Vector3(3f, 4f, 4f), minClusters: 1, minElements: 3);

        var shelfCluster = new GameObject("ShelfCluster");
        shelfCluster.transform.SetParent(shelfZone.transform, false);
        shelfCluster.transform.position = new Vector3(2.8f, 0f, 0f);
        GmCompositionAuthoring.Cluster(shelfCluster, "shelf-cluster", "shelf-zone",
            "Floor-to-ceiling wooden shelving stacked with leather journals.", "archive-shelf");

        GmCompositionAuthoring.Element(props.ArchiveShelf, "archive-shelf", "shelf-cluster", "hidden-furniture",
            "Dark pine archive shelf.", GmCompositionRole.Anchor);

        GmCompositionAuthoring.Element(props.JournalBinders, "journal-binder", "shelf-cluster", "hidden-props",
            "The eight leather-bound guest journals.",
            GmCompositionRole.Gameplay, GmSpatialRelation.Embedded);

        GmCompositionAuthoring.Element(props.Cobwebs, "cobwebs", "shelf-cluster", "hidden-decor",
            "Dense dusty webbing coating the upper ledges.",
            GmCompositionRole.Detail, GmSpatialRelation.Suspended);

        GmCompositionAuthoring.Element(props.StepStool, "step-stool", "shelf-cluster", "hidden-furniture",
            "Worn step stool standing where the top ledges are out of reach.", GmCompositionRole.Support);

        // Declared so the archive light below has a visible source to answer to. Suspended, like
        // shut-the-box's wall-sconce, because a fixture mounted up the wall is not standing on the
        // floor and a grounded check on it would be meaningless.
        GmCompositionAuthoring.Element(props.ShelfWallSconce, "shelf-sconce", "shelf-cluster", "hidden-lighting",
            "Gas wall sconce on the east wall, the fixture the archive light actually comes from.",
            GmCompositionRole.Support, GmSpatialRelation.Suspended);

        // Zone 4: Entry Recess Zone
        var recessZone = new GameObject("RecessZone");
        recessZone.transform.SetParent(owner.transform, false);
        recessZone.transform.position = new Vector3(0f, 1.5f, -2.5f);
        GmCompositionAuthoring.Zone(recessZone, "recess-zone",
            "The interior side of the secret panel door leading back to Shut the Box.",
            new Vector3(4f, 3f, 3f), minClusters: 1, minElements: 3);

        var recessCluster = new GameObject("RecessCluster");
        recessCluster.transform.SetParent(recessZone.transform, false);
        recessCluster.transform.position = new Vector3(0f, 0f, -2.5f);
        GmCompositionAuthoring.Cluster(recessCluster, "recess-cluster", "recess-zone",
            "Secret panel door with spring release and heavy brass latch bolt.", "recess-door");

        GmCompositionAuthoring.Element(props.RecessDoor, "recess-door", "recess-cluster", "hidden-architecture",
            "The rough back face of the secret wainscot panel door.", GmCompositionRole.Anchor);

        GmCompositionAuthoring.Element(props.BrassBolt, "brass-bolt", "recess-cluster", "hidden-decor",
            "Heavy spring bolt mechanism.", GmCompositionRole.Detail, GmSpatialRelation.Embedded);

        GmCompositionAuthoring.Element(props.DoorSconce, "door-sconce", "recess-cluster", "hidden-lighting",
            "Small gas wall light near the entrance.",
            GmCompositionRole.Support, GmSpatialRelation.FlanksAnchor, "recess-door");

        // Every local light answers to a visible source, which is what keeps the room from lighting
        // itself out of nowhere.
        GmCompositionAuthoring.Motivate(props.DoorSconceLight, "door-sconce-light", "door-sconce",
            "Amber practical for the open panel and the real return passage behind it.");

        GmCompositionAuthoring.Motivate(props.DeskLanternLight, "desk-lantern-light", "desk-lantern",
            "Warm practical for the desk: the lantern body on the desk top is the source on camera.");

        GmCompositionAuthoring.Motivate(props.MirrorGlowLight, "mirror-glow", "mirror-glow-light",
            "Cold practical for the frame: the shard receptacle is what the blue highlight comes from.");

        GmCompositionAuthoring.Motivate(props.ShelfSconceLight, "shelf-sconce-light", "shelf-sconce",
            "Amber practical for the archive: the wall sconce beside the shelves is the source on camera.");

        // Review Claims
        // NOTE: the trailing float on each call reaches GmReviewCompositionClaim as a SYMMETRIC
        // viewport tolerance through the interim ReviewClaim adapter, and that reading is not
        // confirmed (docs/audit/F1-review-claim-decision.md). Values here track shot intimacy rather
        // than framing slack, so a passing claim is not evidence the shot is framed as intended.
        // Do not tune these numbers to move the audit; they are Nick's ruling to make.
        GmCompositionAuthoring.ReviewClaim(owner, "01-room-entrance", "rolltop-desk", "mirror-frame",
            "recess-zone", "desk-zone", new Vector2(0.5f, 0.5f), 0.45f,
            "Entering the secret chamber seeing the dusty desk, mirror frame, and low lantern light.");

        GmCompositionAuthoring.ReviewClaim(owner, "02-desk-invitation", "invitation-letter", "rolltop-desk",
            "desk-cluster", "desk-zone", new Vector2(0.5f, 0.5f), 0.70f,
            "Close-up inspection of Aldric's original un-decayed invitation letter.");

        GmCompositionAuthoring.ReviewClaim(owner, "03-mirror-shard3", "shard-three", "mirror-frame",
            "mirror-cluster", "mirror-zone", new Vector2(0.5f, 0.5f), 0.65f,
            "Close view of Mirror Shard #3 lodged in the standing frame.");

        GmCompositionAuthoring.ReviewClaim(owner, "04-journal-archives", "journal-binder", "archive-shelf",
            "shelf-cluster", "shelf-zone", new Vector2(0.5f, 0.5f), 0.60f,
            "Framing the 8 guest journals on the dusty shelves.");

        GmCompositionAuthoring.ReviewClaim(owner, "05-mirror-reconstructed", "mirror-frame", "shard-three",
            "mirror-cluster", "mirror-zone", new Vector2(0.5f, 0.6f), 0.80f,
            "Dramatic blue luminescence framing the assembled mirror glass.");

        GmCompositionAuthoring.ReviewClaim(owner, "06-room-wide", "rolltop-desk", "recess-door",
            "desk-zone", "recess-zone", new Vector2(0.5f, 0.5f), 0.35f,
            "Wide corner shot showing the entire compact hidden chamber.");

        GmCompositionAuthoring.ReviewClaim(owner, "07-labyrinth-passage", "recess-door", "door-sconce",
            "recess-cluster", "recess-zone", new Vector2(0.5f, 0.5f), 0.55f,
            "Interior view proving the already-open panel frames a real return passage into the night.");
    }
}
