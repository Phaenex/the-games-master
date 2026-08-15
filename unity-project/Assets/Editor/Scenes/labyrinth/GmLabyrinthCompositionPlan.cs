using UnityEngine;

/// <summary>
/// The labyrinth geometry the composition plan marks up. The builder fills this in while it builds,
/// so every element marker lands on the object that actually renders: the audit measures renderer
/// bounds, and a marker on a bare GameObject asserts nothing a reviewer could ever see.
/// </summary>
public sealed class GmLabyrinthSceneParts
{
    public GameObject CryptArch;
    public GameObject TorchSconce;
    public GameObject GravelPath;
    public GameObject MirrorPedestal;
    public GameObject ShrinePillars;
    public GameObject MoonbeamShaft;
    public GameObject HedgeWalls;
    public GameObject HuntsmanLantern;
    public GameObject PatrolTrack;
    public GameObject BoneTotem;
    public GameObject ExitGate;
    public GameObject StonePiers;
    public GameObject ExitMist;
    public Light MoonbeamLight;
    public Light TorchLight;
    public Light HuntsmanLanternLight;
}

/// <summary>
/// Composition plan for the Labyrinth scene (Phase 7). Defines the zones, clusters, motivated lighting,
/// and review claims for the 7x7 hedge/stone maze, mirror shrine, and the Huntsman's patrol route.
/// Zones and clusters are logical volumes and own their own markers; elements are marked on the
/// builder's geometry, so the Composition root holds only the manifest, the zones and the clusters.
/// </summary>
public static class GmLabyrinthCompositionPlan
{
    public const string SceneId = "labyrinth";

    public static void Author(GameObject owner, GmLabyrinthSceneParts parts)
    {
        GmCompositionAuthoring.Begin(owner, SceneId,
            "A fog-shrouded 7x7 labyrinth beneath the moonlight where the Huntsman stalks the corridors between the mirror shrine and the final iron gate.",
            minZones: 4, minClusters: 4, minElements: 12);

        // Zone 1: Maze Entrance Zone
        var entranceZone = new GameObject("EntranceZone");
        entranceZone.transform.SetParent(owner.transform, false);
        entranceZone.transform.position = new Vector3(-15f, 1.5f, -15f);
        GmCompositionAuthoring.Zone(entranceZone, "entrance-zone",
            "Stone crypt archway where the player enters the 7x7 maze.",
            new Vector3(8f, 5f, 8f), minClusters: 1, minElements: 3);

        var entranceCluster = new GameObject("EntranceCluster");
        entranceCluster.transform.SetParent(entranceZone.transform, false);
        entranceCluster.transform.position = new Vector3(-15f, 0f, -15f);
        GmCompositionAuthoring.Cluster(entranceCluster, "entrance-cluster", "entrance-zone",
            "Stone archway flanked by rusted iron torch sconces.", "crypt-arch");

        GmCompositionAuthoring.Element(parts.CryptArch, "crypt-arch", "entrance-cluster", "maze-architecture",
            "The heavy stone entrance archway.", GmCompositionRole.Anchor);

        // The sconce hangs on the arch face, so it is measured against the arch rather than the ground.
        GmCompositionAuthoring.Element(parts.TorchSconce, "entrance-torch", "entrance-cluster", "maze-lighting",
            "Amber wall torch casting flickering light on the wet cobbles.", GmCompositionRole.Support,
            GmSpatialRelation.AgainstBoundary, "crypt-arch");

        GmCompositionAuthoring.Element(parts.GravelPath, "gravel-path", "entrance-cluster", "maze-props",
            "Crunching gravel pathway.", GmCompositionRole.Detail);

        // Zone 2: Mirror Shrine Zone (Center)
        var shrineZone = new GameObject("ShrineZone");
        shrineZone.transform.SetParent(owner.transform, false);
        shrineZone.transform.position = new Vector3(0f, 2f, 0f);
        GmCompositionAuthoring.Zone(shrineZone, "shrine-zone",
            "Central open courtyard where the assembled mirror shines under moonlight.",
            new Vector3(10f, 6f, 10f), minClusters: 1, minElements: 3);

        var shrineCluster = new GameObject("ShrineCluster");
        shrineCluster.transform.SetParent(shrineZone.transform, false);
        shrineCluster.transform.position = new Vector3(0f, 0f, 0f);
        GmCompositionAuthoring.Cluster(shrineCluster, "shrine-cluster", "shrine-zone",
            "Carved stone pedestal surrounded by four ancient stone pillars.", "mirror-pedestal");

        GmCompositionAuthoring.Element(parts.MirrorPedestal, "mirror-pedestal", "shrine-cluster", "maze-architecture",
            "The octagonal stone altar holding the mirror shards.", GmCompositionRole.Anchor);

        GmCompositionAuthoring.Element(parts.ShrinePillars, "shrine-pillars", "shrine-cluster", "maze-architecture",
            "Four weathered gothic stone pillars.", GmCompositionRole.Support);

        GmCompositionAuthoring.Element(parts.MoonbeamShaft, "moonlight-glow", "shrine-cluster", "maze-lighting",
            "Cool blue volumetric moonbeam spotlight centered on the altar.", GmCompositionRole.Detail,
            GmSpatialRelation.Suspended);

        // Zone 3: Huntsman Stalk Zone
        var stalkZone = new GameObject("StalkZone");
        stalkZone.transform.SetParent(owner.transform, false);
        stalkZone.transform.position = new Vector3(5f, 2f, -5f);
        GmCompositionAuthoring.Zone(stalkZone, "stalk-zone",
            "Deep foggy corridor where the Huntsman patrols.",
            new Vector3(8f, 4f, 14f), minClusters: 1, minElements: 3);

        var stalkCluster = new GameObject("StalkCluster");
        stalkCluster.transform.SetParent(stalkZone.transform, false);
        stalkCluster.transform.position = new Vector3(5f, 0f, -5f);
        GmCompositionAuthoring.Cluster(stalkCluster, "stalk-cluster", "stalk-zone",
            "Dense hedge walls concealing the Huntsman's lantern.", "hedge-walls");

        // Family is "maze-hedge", not "maze-architecture": hedges are planting, not carved stone.
        // The audit's duplicate-placement guard is deliberately family-scoped, and sharing a family
        // with shrine-pillars made the two elements' container roots -- both legitimately at the
        // world origin, since their children carry absolute positions -- hash to the same key and
        // trip it. Nothing was ever misplaced; retagging is the honest fix, where nudging an
        // invisible parent to dodge a hash collision would not be.
        GmCompositionAuthoring.Element(parts.HedgeWalls, "hedge-walls", "stalk-cluster", "maze-hedge",
            "Three-meter tall dense dark hedges.", GmCompositionRole.Anchor);

        // The rationale describes the built prop, not an aspiration: a static warm-toned cube with
        // no Light component and no swing script. Swing motion and an actual glow are unauthored;
        // whether the Huntsman's lantern should move or emit light is Nick's call, not invented here.
        GmCompositionAuthoring.Element(parts.HuntsmanLantern, "huntsman-lantern", "stalk-cluster", "maze-lighting",
            "The Huntsman's warm-toned lantern prop marking his patrol corner.", GmCompositionRole.Gameplay,
            GmSpatialRelation.Suspended);

        // The cluster's Gameplay lantern does not count as support, so the corridor needs the cue
        // that actually tells the player which way the Huntsman walks.
        GmCompositionAuthoring.Element(parts.PatrolTrack, "patrol-track", "stalk-cluster", "maze-ground",
            "Beaten earth down the corridor, worn by the Huntsman's circuit.", GmCompositionRole.RouteCue);

        GmCompositionAuthoring.Element(parts.BoneTotem, "bone-totem", "stalk-cluster", "maze-props",
            "Crude bone markers warning of the Huntsman's territory.", GmCompositionRole.Detail);

        // Zone 4: Maze Exit Zone
        var exitZone = new GameObject("ExitZone");
        exitZone.transform.SetParent(owner.transform, false);
        exitZone.transform.position = new Vector3(15f, 2f, 15f);
        GmCompositionAuthoring.Zone(exitZone, "exit-zone",
            "The far gate opening onto the Wend Hill moors.",
            new Vector3(8f, 5f, 8f), minClusters: 1, minElements: 3);

        var exitCluster = new GameObject("ExitCluster");
        exitCluster.transform.SetParent(exitZone.transform, false);
        exitCluster.transform.position = new Vector3(15f, 0f, 15f);
        GmCompositionAuthoring.Cluster(exitCluster, "exit-cluster", "exit-zone",
            "Wrought iron gate set between mossy stone piers.", "exit-gate");

        GmCompositionAuthoring.Element(parts.ExitGate, "exit-gate", "exit-cluster", "maze-architecture",
            "Massive black wrought iron estate gate.", GmCompositionRole.Anchor);

        GmCompositionAuthoring.Element(parts.StonePiers, "stone-piers", "exit-cluster", "maze-architecture",
            "Twin carved stone piers bearing stag crests.", GmCompositionRole.Support);

        GmCompositionAuthoring.Element(parts.ExitMist, "exit-mist", "exit-cluster", "maze-lighting",
            "Dense ground fog illuminated by the full moon beyond.", GmCompositionRole.Detail);

        // Both local lights carry authored intent. The torch is a practical and names the sconce the
        // player can see; the moonbeam is environmental and has no fixture to stand next to.
        GmAdaptiveIntentAuthoring.Light(parts.TorchLight.gameObject, "entrance-torch-practical",
            GmLightIntentKind.Practical,
            "The amber pool on the threshold comes from the sconce on the arch, not from nowhere.",
            sourceElementId: "entrance-torch");

        GmAdaptiveIntentAuthoring.Light(parts.MoonbeamLight.gameObject, "altar-moonbeam",
            GmLightIntentKind.Environmental,
            "Moonlight drops into the one open cell of the maze; the shrine owns no lamp of its own.");

        // The lantern burns. An older comment here said whether it should was undecided and that the
        // prop was a plain cube -- both were stale: the prop is the SM_Lantern mesh and the builder
        // already creates and enables a 200-intensity light at it. So this documents what the scene
        // actually does rather than leaving the brightest light in the maze unexplained.
        GmAdaptiveIntentAuthoring.Light(parts.HuntsmanLanternLight.gameObject, "huntsman-lantern-practical",
            GmLightIntentKind.Practical,
            "The warm pool in the stalk corridor is the Huntsman's own lantern — the prop the player tracks.",
            sourceElementId: "huntsman-lantern");

        // 8 Review claims.
        // The trailing float reads as a symmetric framing tolerance through the interim ReviewClaim
        // adapter, and that reading is NOT confirmed (docs/audit/F1-review-claim-decision.md). Several
        // of these values are wider than the acceptance window the audit's visibility test already
        // applies, so a green run here is not evidence that any of these shots is framed correctly.
        // Numbers stay exactly as authored until Nick rules on what they mean.
        GmCompositionAuthoring.ReviewClaim(owner, "01-crypt-entrance", "crypt-arch", "entrance-torch",
            "entrance-cluster", "entrance-zone", new Vector2(0.5f, 0.5f), 0.50f,
            "Entering the dark maze from the crypt threshold.");

        GmCompositionAuthoring.ReviewClaim(owner, "02-hedge-corridor", "hedge-walls", "gravel-path",
            "stalk-cluster", "stalk-zone", new Vector2(0.5f, 0.4f), 0.60f,
            "Long claustrophobic view down the narrow hedge passageway.");

        GmCompositionAuthoring.ReviewClaim(owner, "03-mirror-shrine", "mirror-pedestal", "shrine-pillars",
            "shrine-cluster", "shrine-zone", new Vector2(0.5f, 0.55f), 0.70f,
            "Central courtyard view framing the stone altar and cool moonbeam.");

        GmCompositionAuthoring.ReviewClaim(owner, "04-huntsman-patrol", "huntsman-lantern", "hedge-walls",
            "stalk-cluster", "stalk-zone", new Vector2(0.5f, 0.5f), 0.55f,
            "Corner view showing the Huntsman's lantern in the mist.");

        GmCompositionAuthoring.ReviewClaim(owner, "05-bone-totem", "bone-totem", "hedge-walls",
            "stalk-cluster", "stalk-zone", new Vector2(0.6f, 0.5f), 0.40f,
            "Close inspection of the bone marker warning.");

        GmCompositionAuthoring.ReviewClaim(owner, "06-moonlight-clearing", "shrine-pillars", "moonlight-glow",
            "shrine-cluster", "shrine-zone", new Vector2(0.5f, 0.6f), 0.50f,
            "Upward looking wide framing of the moonlit clearing.");

        GmCompositionAuthoring.ReviewClaim(owner, "07-exit-gate", "exit-gate", "stone-piers",
            "exit-cluster", "exit-zone", new Vector2(0.5f, 0.5f), 0.65f,
            "Framing the iron exit gate and distant misty hills.");

        GmCompositionAuthoring.ReviewClaim(owner, "08-maze-wide", "crypt-arch", "exit-gate",
            "entrance-zone", "exit-zone", new Vector2(0.5f, 0.5f), 0.35f,
            "Elevated wide overview of the 7x7 hedge maze layout.");
    }
}
