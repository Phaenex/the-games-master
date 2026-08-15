using UnityEngine;

/// <summary>
/// Composition plan for the Shut the Box scene. Defines the zones, clusters, motivated lighting,
/// and review claims for the two 9-tile boards, dice tray, and Tile-9 secret door.
/// Zones and clusters are marker objects under the Composition root; every element marker is
/// attached to the geometry <see cref="GmShutTheBoxBuilder"/> actually creates, so the audit
/// measures real renderers rather than empties standing in for them.
/// </summary>
public static class GmShutTheBoxCompositionPlan
{
    public const string SceneId = "shut-the-box";

    /// <summary>Geometry handed over by the builder for the element markers to attach to.</summary>
    public sealed class SceneRefs
    {
        public GameObject playerBox;
        public GameObject playerTiles;
        public GameObject playerPivotRod;
        public GameObject playerNamePlate;
        public GameObject hostBox;
        public GameObject hostTiles;
        public GameObject hostPivotRod;
        public GameObject hostTile9Hinge;
        public GameObject diceTray;
        public GameObject boneDice;
        public GameObject diceRim;
        public GameObject tableLampFixture;
        public GameObject tableLampLight;
        public GameObject panelDoor;
        public GameObject brassSeam;
        public GameObject doorWainscot;
        public GameObject doorAccentLight;
        public GameObject bookshelf;
        public GameObject wallSconce;
        public GameObject wallSconceLight;
        public GameObject wallClock;
    }

    // Everything resting on the gaming table is grounded on the table top, not the floor.
    const float TableTopY = 0.8f;

    public static void Author(GameObject owner, SceneRefs refs)
    {
        GmCompositionAuthoring.Begin(owner, SceneId,
            "An oak gaming alcove where player and host compete on paired nine-tile Shut the Box boards, concealing the hidden room latch.",
            minZones: 4, minClusters: 4, minElements: 12);

        // Zone 1: Board Table Zone
        var tableZone = new GameObject("TableZone");
        tableZone.transform.SetParent(owner.transform, false);
        tableZone.transform.position = new Vector3(0f, 1.1f, 0f);
        GmCompositionAuthoring.Zone(tableZone, "table-zone",
            "Central mahogany gaming table holding the two nine-tile boards.",
            new Vector3(6f, 4f, 6f), minClusters: 2, minElements: 6);

        // Cluster 1: Player Board Cluster
        var playerCluster = new GameObject("PlayerBoardCluster");
        playerCluster.transform.SetParent(tableZone.transform, false);
        playerCluster.transform.position = new Vector3(0f, 0f, -0.6f);
        GmCompositionAuthoring.Cluster(playerCluster, "player-board-cluster", "table-zone",
            "Player's nine-tile wooden board with engraved guest names (1=Marr ... 9=Percival).", "player-box");

        GmCompositionAuthoring.Element(refs.playerBox, "player-box", "player-board-cluster", "stb-furniture",
            "The player's polished walnut nine-tile board.", GmCompositionRole.Anchor,
            GmSpatialRelation.Grounded, surfaceY: TableTopY);

        GmCompositionAuthoring.Element(refs.playerTiles, "player-tiles", "player-board-cluster", "stb-props",
            "The nine numbered wooden tiles with brass pivot rods.", GmCompositionRole.Gameplay,
            GmSpatialRelation.Grounded, surfaceY: TableTopY);

        GmCompositionAuthoring.Element(refs.playerPivotRod, "player-pivot-rod", "player-board-cluster", "stb-hardware",
            "The brass rod the player's tiles pivot on, holding the whole rank in one line.",
            GmCompositionRole.Support, GmSpatialRelation.Grounded, surfaceY: TableTopY);

        GmCompositionAuthoring.Element(refs.playerNamePlate, "player-name-plate", "player-board-cluster", "stb-engraving",
            "The engraved guest-name strip along the board's front edge, read from the player's seat.",
            GmCompositionRole.Detail, GmSpatialRelation.Grounded, surfaceY: TableTopY);

        // Cluster 2: Host Board Cluster
        var hostCluster = new GameObject("HostBoardCluster");
        hostCluster.transform.SetParent(tableZone.transform, false);
        hostCluster.transform.position = new Vector3(0f, 0f, 0.6f);
        GmCompositionAuthoring.Cluster(hostCluster, "host-board-cluster", "table-zone",
            "Aldric's opposing board with tampered Tile 9 hinge.", "host-box");

        GmCompositionAuthoring.Element(refs.hostBox, "host-box", "host-board-cluster", "stb-furniture",
            "Aldric's dark stained oak nine-tile board.", GmCompositionRole.Anchor,
            GmSpatialRelation.Grounded, surfaceY: TableTopY);

        GmCompositionAuthoring.Element(refs.hostTiles, "host-tiles", "host-board-cluster", "stb-props",
            "Aldric's tiles subject to false calls and palm cheats.", GmCompositionRole.Gameplay,
            GmSpatialRelation.Grounded, surfaceY: TableTopY);

        GmCompositionAuthoring.Element(refs.hostPivotRod, "host-pivot-rod", "host-board-cluster", "stb-hardware",
            "The matching brass rod on Aldric's side, so the tampering reads against a true one.",
            GmCompositionRole.Support, GmSpatialRelation.Grounded, surfaceY: TableTopY);

        GmCompositionAuthoring.Element(refs.hostTile9Hinge, "host-tile9-hinge", "host-board-cluster", "stb-hardware",
            "The thickened Tile 9 hinge on Aldric's board, the tell the Hold verb accuses.",
            GmCompositionRole.Detail, GmSpatialRelation.Grounded, surfaceY: TableTopY);

        // Zone 2: Dice Tray Zone
        var diceZone = new GameObject("DiceZone");
        diceZone.transform.SetParent(owner.transform, false);
        diceZone.transform.position = new Vector3(0f, 1.1f, 0f);
        GmCompositionAuthoring.Zone(diceZone, "dice-zone",
            "Central green felt dice rolling pit between the two boards.",
            new Vector3(4f, 3f, 4f), minClusters: 1, minElements: 3);

        var diceCluster = new GameObject("DiceCluster");
        diceCluster.transform.SetParent(diceZone.transform, false);
        diceCluster.transform.position = new Vector3(0f, 0f, 0f);
        GmCompositionAuthoring.Cluster(diceCluster, "dice-cluster", "dice-zone",
            "Leather dice rolling pit and two carved bone dice.", "dice-tray");

        GmCompositionAuthoring.Element(refs.diceTray, "dice-tray", "dice-cluster", "stb-props",
            "Octagonal green felt tray containing dice throws.", GmCompositionRole.Anchor,
            GmSpatialRelation.Grounded, surfaceY: TableTopY);

        GmCompositionAuthoring.Element(refs.boneDice, "bone-dice", "dice-cluster", "stb-dice",
            "Pair of aged bone six-sided dice.", GmCompositionRole.Gameplay,
            GmSpatialRelation.Grounded, surfaceY: TableTopY);

        GmCompositionAuthoring.Element(refs.tableLampFixture, "table-lamp", "dice-cluster", "stb-lighting",
            "Downlight lamp focused squarely on the dice tray.", GmCompositionRole.Support,
            GmSpatialRelation.Suspended);

        GmCompositionAuthoring.Element(refs.diceRim, "dice-rim", "dice-cluster", "stb-leather",
            "The leather rim of the rolling pit, worn where throws are gathered back in.",
            GmCompositionRole.Detail, GmSpatialRelation.Grounded, surfaceY: TableTopY);

        // Zone 3: Secret Door Zone
        var doorZone = new GameObject("DoorZone");
        doorZone.transform.SetParent(owner.transform, false);
        doorZone.transform.position = new Vector3(4f, 1.5f, 0f);
        GmCompositionAuthoring.Zone(doorZone, "door-zone",
            "East wall wood paneling containing the Tile-9 secret door to Phase 6.",
            new Vector3(3f, 4f, 4f), minClusters: 1, minElements: 2);

        var doorCluster = new GameObject("SecretDoorCluster");
        doorCluster.transform.SetParent(doorZone.transform, false);
        doorCluster.transform.position = new Vector3(3.8f, 0f, 0f);
        GmCompositionAuthoring.Cluster(doorCluster, "secret-door-cluster", "door-zone",
            "Concealed doorway in the wainscoting tied to Tile 9 Hold mechanic.", "panel-door");

        GmCompositionAuthoring.Element(refs.panelDoor, "panel-door", "secret-door-cluster", "stb-architecture",
            "The secret panel door that pops open upon holding Tile 9.", GmCompositionRole.Anchor,
            GmSpatialRelation.Grounded);

        GmCompositionAuthoring.Element(refs.doorWainscot, "door-wainscot", "secret-door-cluster", "stb-architecture",
            "The wainscoting run either side of the leaf, which is what makes the door read as wall.",
            GmCompositionRole.Support, GmSpatialRelation.Grounded);

        GmCompositionAuthoring.Element(refs.brassSeam, "brass-seam", "secret-door-cluster", "stb-decor",
            "Hairline seam catching raking light when unlatched.", GmCompositionRole.Detail,
            GmSpatialRelation.Grounded);

        // Zone 4: Background & Alcove Zone
        var alcoveZone = new GameObject("AlcoveZone");
        alcoveZone.transform.SetParent(owner.transform, false);
        alcoveZone.transform.position = new Vector3(0f, 2f, 3.5f);
        GmCompositionAuthoring.Zone(alcoveZone, "alcove-zone",
            "Shadowed bookshelf alcove framing the intimate table space.",
            new Vector3(8f, 4f, 4f), minClusters: 1, minElements: 2);

        var alcoveCluster = new GameObject("AlcoveCluster");
        alcoveCluster.transform.SetParent(alcoveZone.transform, false);
        alcoveCluster.transform.position = new Vector3(0f, 0f, 3.5f);
        GmCompositionAuthoring.Cluster(alcoveCluster, "alcove-cluster", "alcove-zone",
            "Dark oak bookshelves and wall clock.", "bookshelf");

        GmCompositionAuthoring.Element(refs.bookshelf, "bookshelf", "alcove-cluster", "stb-furniture",
            "Heavy carved bookcase.", GmCompositionRole.Anchor, GmSpatialRelation.Grounded);

        GmCompositionAuthoring.Element(refs.wallSconce, "wall-sconce", "alcove-cluster", "stb-lighting",
            "Dim amber wall sconce.", GmCompositionRole.Support, GmSpatialRelation.Suspended);

        GmCompositionAuthoring.Element(refs.wallClock, "wall-clock", "alcove-cluster", "stb-decor",
            "The alcove wall clock, the only thing in the room still keeping honest time.",
            GmCompositionRole.Detail, GmSpatialRelation.Suspended);

        AuthorLighting(refs);
        AuthorReviewClaims(owner);
    }

    // GmLightIntent has no GmCompositionAuthoring helper -- GmMotivatedLight is the legacy marker
    // and new work is meant to use the intent component, so these are configured directly.
    static void AuthorLighting(SceneRefs refs)
    {
        refs.tableLampLight.AddComponent<GmLightIntent>().Configure("stb-table-lamp",
            GmLightIntentKind.Practical,
            "The downlight over the pit is the reason the dice read at all; the shade above it is the visible source.",
            sourceId: "table-lamp");

        refs.wallSconceLight.AddComponent<GmLightIntent>().Configure("stb-wall-sconce",
            GmLightIntentKind.Practical,
            "Amber sconce lifting the alcove off black so the bookcase silhouette stays legible.",
            sourceId: "wall-sconce");

        // Deliberately unmotivated: nothing in the room emits this. It exists to rake the seam so a
        // player can find the door, which is what CompositionFill is for.
        refs.doorAccentLight.AddComponent<GmLightIntent>().Configure("stb-door-rake",
            GmLightIntentKind.CompositionFill,
            "Cheated raking accent that grazes the panel edge so the hairline seam is findable.",
            subjectId: "brass-seam");
    }

    // The trailing float on each claim is an INTERIM reading: GmCompositionAuthoring.ReviewClaim
    // widens it into a symmetric viewport tolerance, and that reading is NOT confirmed (see the
    // adapter comment in GmSceneComposition.cs and docs/audit/F1-review-claim-decision.md). The
    // numbers here track shot intimacy, which is backwards for a tolerance, so several of these
    // windows are wider than the audit's own visibility test and cannot fail. Do not read a passing
    // composition audit as evidence that these shots are framed correctly, and do not tune these
    // values to chase one -- they are Nick's call.
    static void AuthorReviewClaims(GameObject owner)
    {
        GmCompositionAuthoring.ReviewClaim(owner, "01-table-overview", "dice-tray", "player-box",
            "table-zone", "dice-zone", new Vector2(0.5f, 0.45f), 0.50f,
            "Player view looking over the player board and dice tray toward Aldric.");

        GmCompositionAuthoring.ReviewClaim(owner, "02-player-board-focus", "player-box", "player-tiles",
            "player-board-cluster", "table-zone", new Vector2(0.5f, 0.4f), 0.70f,
            "Close-up of the player's nine numbered wooden tiles.");

        GmCompositionAuthoring.ReviewClaim(owner, "03-host-board-focus", "host-box", "host-tiles",
            "host-board-cluster", "table-zone", new Vector2(0.5f, 0.6f), 0.65f,
            "Close-up of Aldric's opposing board and tampered Tile 9 hinge.");

        GmCompositionAuthoring.ReviewClaim(owner, "04-dice-tray-action", "dice-tray", "bone-dice",
            "dice-cluster", "dice-zone", new Vector2(0.5f, 0.5f), 0.60f,
            "Clear framing of the green felt dice tray with rolling bone dice.");

        GmCompositionAuthoring.ReviewClaim(owner, "05-tile9-door-seam", "panel-door", "brass-seam",
            "secret-door-cluster", "door-zone", new Vector2(0.6f, 0.5f), 0.55f,
            "Raking light highlighting the concealed panel door in the east wall.");

        GmCompositionAuthoring.ReviewClaim(owner, "06-hold-verb-framing", "player-tiles", "dice-tray",
            "player-board-cluster", "table-zone", new Vector2(0.5f, 0.5f), 0.80f,
            "Magnified interaction view when player holds a tile for cheat accusation.");

        GmCompositionAuthoring.ReviewClaim(owner, "07-alcove-mood", "bookshelf", "wall-sconce",
            "alcove-cluster", "alcove-zone", new Vector2(0.5f, 0.6f), 0.50f,
            "Atmospheric background framing with shadowed books and warm sconce glow.");

        GmCompositionAuthoring.ReviewClaim(owner, "08-room-wide", "player-box", "panel-door",
            "table-zone", "door-zone", new Vector2(0.5f, 0.5f), 0.40f,
            "Wide establishing shot showing the table, boards, and east wall secret door.");
    }
}
