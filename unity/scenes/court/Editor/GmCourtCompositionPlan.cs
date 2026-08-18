using UnityEngine;

/// <summary>
/// Composition plan for the Court scene. Defines the zones, clusters, motivated lighting,
/// and review claims for the 9-seat jury wall, witness dock, evidence bar, and tarnishing gavel.
/// </summary>
public static class GmCourtCompositionPlan
{
    public const string SceneId = "court";

    public sealed class SceneRefs
    {
        public GameObject juryTier;
        public GameObject juryChairs;
        public GameObject juryLightFixture;
        public GameObject juryLight;
        public GameObject witnessDock;
        public GameObject witnessChair;
        public GameObject dockRail;
        public GameObject witnessLightFixture;
        public GameObject witnessLight;
        public GameObject evidenceTable;
        public GameObject waxSeals;
        public GameObject shardTwo;
        public GameObject docketStand;
        public GameObject evidenceDocket;
        public GameObject judgeBench;
        public GameObject judgeChair;
        public GameObject gavel;
        public GameObject soundBlock;
        public GameObject benchCandles;
        public GameObject benchLightFixture;
        public GameObject benchLight;
        public GameObject benchDeskLight;
        public GameObject evidenceLight;
        public GameObject verdictDoors;
        public GameObject verdictSconces;
        public GameObject verdictSconceLight;
    }

    public static void Author(GameObject owner, SceneRefs refs)
    {
        GmCompositionAuthoring.Begin(owner, SceneId,
            "A decaying gothic courtroom where an imposing 9-seat jury watches from the gloom as evidence is presented against a ticking pressure clock.",
            minZones: 4, minClusters: 4, minElements: 12);

        // Zone 1: Jury Wall Zone
        var juryZone = new GameObject("JuryZone");
        juryZone.transform.SetParent(owner.transform, false);
        juryZone.transform.position = new Vector3(-5f, 2.5f, 0f);
        GmCompositionAuthoring.Zone(juryZone, "jury-zone",
            "Elevated 9-seat jury tier representing the nine trapped guests.",
            new Vector3(6f, 6f, 12f), minClusters: 1, minElements: 3);

        var juryCluster = new GameObject("JuryCluster");
        juryCluster.transform.SetParent(juryZone.transform, false);
        juryCluster.transform.position = new Vector3(-5f, 1.2f, 0f);
        GmCompositionAuthoring.Cluster(juryCluster, "jury-cluster", "jury-zone",
            "Two-tiered jury bench with nine high-backed dark oak chairs.", "jury-bench");

        GmCompositionAuthoring.Element(refs.juryTier, "jury-bench", "jury-cluster", "court-architecture",
            "Two-tier elevated wooden jury box structure.", GmCompositionRole.Anchor,
            GmSpatialRelation.Grounded, surfaceY: 0.0f);

        GmCompositionAuthoring.Element(refs.juryChairs, "jury-chairs", "jury-cluster", "court-furniture",
            "Nine imposing high-backed jury chairs in shadow.", GmCompositionRole.Support,
            GmSpatialRelation.Grounded, surfaceY: 1.2f);

        GmCompositionAuthoring.Element(refs.juryLightFixture, "jury-shadow-light", "jury-cluster", "court-lighting",
            "Low-angle rim light fixture creating sharp silhouettes of the jury.", GmCompositionRole.Detail,
            GmSpatialRelation.Suspended);

        // Zone 2: Witness Dock Zone
        var witnessZone = new GameObject("WitnessZone");
        witnessZone.transform.SetParent(owner.transform, false);
        witnessZone.transform.position = new Vector3(0f, 1.2f, -1f);
        GmCompositionAuthoring.Zone(witnessZone, "witness-zone",
            "Isolated central dock where the player stands under harsh swinging light.",
            new Vector3(5f, 5f, 5f), minClusters: 1, minElements: 3);

        var witnessCluster = new GameObject("WitnessCluster");
        witnessCluster.transform.SetParent(witnessZone.transform, false);
        witnessCluster.transform.position = new Vector3(0f, 1.0f, -1f);
        GmCompositionAuthoring.Cluster(witnessCluster, "witness-cluster", "witness-zone",
            "Single oak chair within a low wooden witness enclosure.", "witness-chair");

        GmCompositionAuthoring.Element(refs.witnessChair, "witness-chair", "witness-cluster", "court-furniture",
            "The isolated central witness chair.", GmCompositionRole.Anchor,
            GmSpatialRelation.Grounded, surfaceY: 0.5f);

        GmCompositionAuthoring.Element(refs.witnessLightFixture, "swinging-light", "witness-cluster", "court-lighting",
            "Swinging conical spotlight fixture alternating between accuser and defender.", GmCompositionRole.Support,
            GmSpatialRelation.Suspended);

        GmCompositionAuthoring.Element(refs.dockRail, "dock-rail", "witness-cluster", "court-architecture",
            "Worn brass witness railing.", GmCompositionRole.Detail,
            GmSpatialRelation.Suspended);

        // Zone 3: Evidence Bar Zone
        var evidenceZone = new GameObject("EvidenceZone");
        evidenceZone.transform.SetParent(owner.transform, false);
        evidenceZone.transform.position = new Vector3(0f, 1.2f, 2.5f);
        GmCompositionAuthoring.Zone(evidenceZone, "evidence-zone",
            "The bar table where evidence cards and wax-sealed dockets are placed.",
            new Vector3(6f, 4f, 4f), minClusters: 1, minElements: 4);

        var evidenceCluster = new GameObject("EvidenceCluster");
        evidenceCluster.transform.SetParent(evidenceZone.transform, false);
        evidenceCluster.transform.position = new Vector3(0f, 0.9f, 2.5f);
        GmCompositionAuthoring.Cluster(evidenceCluster, "evidence-cluster", "evidence-zone",
            "Long mahogany table displaying evidence cards, red wax seals, and Shard #2.", "evidence-table");

        GmCompositionAuthoring.Element(refs.evidenceTable, "evidence-table", "evidence-cluster", "court-furniture",
            "The heavy evidence presentation table.", GmCompositionRole.Anchor,
            GmSpatialRelation.Grounded, surfaceY: 0.0f);

        GmCompositionAuthoring.Element(refs.waxSeals, "wax-seals", "evidence-cluster", "court-props",
            "Three red wax seals representing juror consensus.", GmCompositionRole.Gameplay,
            GmSpatialRelation.Grounded, surfaceY: 0.9f);

        GmCompositionAuthoring.Element(refs.shardTwo, "shard-two", "evidence-cluster", "shards",
            "Mirror Shard #2 mislabeled among evidence files.", GmCompositionRole.Gameplay,
            GmSpatialRelation.Grounded, surfaceY: 0.9f);

        GmCompositionAuthoring.Element(refs.docketStand, "docket-stand", "evidence-cluster", "court-furniture",
            "Wooden dossier stand holding case filings.", GmCompositionRole.Support,
            GmSpatialRelation.Grounded, surfaceY: 0.9f);

        GmCompositionAuthoring.Element(refs.evidenceDocket, "evidence-docket", "evidence-cluster", "court-props",
            "Sealed parchment docket detailing the charges.", GmCompositionRole.Detail,
            GmSpatialRelation.Grounded, surfaceY: 0.9f);

        // Zone 4: Judicial Bench Zone
        var benchZone = new GameObject("BenchZone");
        benchZone.transform.SetParent(owner.transform, false);
        benchZone.transform.position = new Vector3(0f, 2.5f, 6.0f);
        GmCompositionAuthoring.Zone(benchZone, "bench-zone",
            "Elevated judge's podium holding the sound block and tarnishing brass gavel.",
            new Vector3(6f, 5f, 4f), minClusters: 1, minElements: 4);

        var benchCluster = new GameObject("BenchCluster");
        benchCluster.transform.SetParent(benchZone.transform, false);
        benchCluster.transform.position = new Vector3(0f, 1.8f, 6.0f);
        GmCompositionAuthoring.Cluster(benchCluster, "bench-cluster", "bench-zone",
            "Raised judicial bench and sound block.", "judge-bench");

        GmCompositionAuthoring.Element(refs.judgeBench, "judge-bench", "bench-cluster", "court-architecture",
            "Raised mahogany judicial dais.", GmCompositionRole.Anchor,
            GmSpatialRelation.Grounded, surfaceY: 0.95f);

        GmCompositionAuthoring.Element(refs.gavel, "gavel", "bench-cluster", "court-props",
            "Brass gavel that visibly tarnishes when rigged evidence is submitted.", GmCompositionRole.Gameplay,
            GmSpatialRelation.Grounded, surfaceY: 1.85f);

        GmCompositionAuthoring.Element(refs.judgeChair, "judge-chair", "bench-cluster", "court-furniture",
            "Imposing high-backed judicial chair overlooking the courtroom.", GmCompositionRole.Support,
            GmSpatialRelation.Grounded, surfaceY: 1.0f);

        GmCompositionAuthoring.Element(refs.soundBlock, "sound-block", "bench-cluster", "court-props",
            "Carved oak sound block.", GmCompositionRole.Detail,
            GmSpatialRelation.Grounded, surfaceY: 1.85f);

        GmCompositionAuthoring.Element(refs.benchCandles, "bench-candles", "bench-cluster", "court-lighting",
            "A low cluster of working candles beside the gavel, revealing the verdict surface.",
            GmCompositionRole.Support, GmSpatialRelation.Grounded, surfaceY: 1.85f);

        // The lantern the bench light actually comes from. It was built and stored on refs and then
        // never declared, so the intent below had nothing to name and pointed at the desk instead.
        // Suspended, like the shut-the-box wall sconce: a fixture hung over the dais is not standing
        // on anything, and a grounded check on it would be meaningless.
        GmCompositionAuthoring.Element(refs.benchLightFixture, "bench-lantern", "bench-cluster", "court-lighting",
            "Iron lantern hung over the dais, the warm source above the bench.",
            GmCompositionRole.Detail, GmSpatialRelation.Suspended);

        // Zone 5: Verdict Exit. This is a route cue, not courtroom dressing, and gets its own small
        // cluster so the composition audit measures the actual leaves at the south threshold.
        var exitZone = new GameObject("VerdictExitZone");
        exitZone.transform.SetParent(owner.transform, false);
        exitZone.transform.position = new Vector3(0f, 1.5f, -8f);
        GmCompositionAuthoring.Zone(exitZone, "verdict-exit-zone",
            "The double doors released by either Court verdict, leading into Shut the Box.",
            new Vector3(4f, 4f, 4f), minClusters: 1, minElements: 1);

        var exitCluster = new GameObject("VerdictExitCluster");
        exitCluster.transform.SetParent(exitZone.transform, false);
        exitCluster.transform.position = new Vector3(0f, 1.2f, -7.78f);
        GmCompositionAuthoring.Cluster(exitCluster, "verdict-exit-cluster", "verdict-exit-zone",
            "Paired oak leaves opening onto the next game passage.", "verdict-doors",
            minSupports: 1, minDetails: 0, requireVariation: false);
        GmCompositionAuthoring.Element(refs.verdictDoors, "verdict-doors", "verdict-exit-cluster",
            "court-architecture", "Physical verdict-gated double doors and their real hinge leaves.",
            GmCompositionRole.Anchor);
        GmCompositionAuthoring.Element(refs.verdictSconces, "verdict-sconces", "verdict-exit-cluster",
            "court-lighting", "Paired period wall lamps revealing the verdict-gated threshold.",
            GmCompositionRole.Support, GmSpatialRelation.Suspended);

        // Motivated Lighting
        refs.witnessLight.AddComponent<GmLightIntent>().Configure("court-witness-spot",
            GmLightIntentKind.Practical,
            "Swinging overhead spotlight illuminating the isolated witness dock.",
            sourceId: "swinging-light");

        refs.juryLight.AddComponent<GmLightIntent>().Configure("court-jury-rim",
            GmLightIntentKind.Practical,
            "Rim light defining the high-backed jury tier against the darkness.",
            sourceId: "jury-shadow-light");

        // Pointed at the lantern rather than the desk. The light IS the chandelier -- both sit at
        // (0, 4.5, 4) -- but the intent named the desk 2.17m below it, and the audit measures
        // point-to-box, so it read as an unexplained light. Un-parenting the desk widens that gap to
        // 2.93m, so this had to be fixed either way. Same shape as the Hidden Room shelf sconce: the
        // fixture existed on refs and was simply never declared as an element.
        refs.benchLight.AddComponent<GmLightIntent>().Configure("court-bench-lamp",
            GmLightIntentKind.Practical,
            "Iron lantern over the dais: the warm source above the sound block and the brass gavel.",
            sourceId: "bench-lantern");

        refs.benchDeskLight.AddComponent<GmLightIntent>().Configure("court-bench-candles",
            GmLightIntentKind.Practical,
            "Low candlelight motivated by the working candle cluster beside the gavel.",
            sourceId: "bench-candles");

        // Non-diegetic by admission. There is no fixture anywhere near (0, 3.5, 2.5) -- the nearest
        // candidate is a candle 2.85m away -- so calling this Practical would be inventing a source.
        // CompositionFill is what the contract has for exactly this, and it is the honest label.
        refs.evidenceLight.AddComponent<GmLightIntent>().Configure("court-evidence-fill",
            GmLightIntentKind.CompositionFill,
            "Overhead fill isolating the evidence bar so the juror-facing documents read against the gloom.",
            subjectId: "evidence-table");

        refs.verdictSconceLight.AddComponent<GmLightIntent>().Configure("court-verdict-sconces",
            GmLightIntentKind.Practical,
            "Paired wall lamps make the resolved-verdict passage readable at the shared fixed exposure.",
            sourceId: "verdict-sconces");

        // Review claims
        GmCompositionAuthoring.ReviewClaim(owner, "01-court-overview", "judge-bench", "witness-chair",
            "bench-zone", "witness-zone", new Vector2(0.5f, 0.55f), 0.50f,
            "Wide view from court entrance showing witness dock, evidence bar, and bench.");

        GmCompositionAuthoring.ReviewClaim(owner, "02-jury-wall-perspective", "jury-bench", "jury-chairs",
            "jury-cluster", "jury-zone", new Vector2(0.5f, 0.5f), 0.60f,
            "Imposing lateral perspective of the 9 high-backed jury seats.");

        GmCompositionAuthoring.ReviewClaim(owner, "03-witness-spotlight", "witness-chair", "dock-rail",
            "witness-cluster", "witness-zone", new Vector2(0.5f, 0.45f), 0.70f,
            "Focused spotlight beam illuminating the isolated witness dock.");

        GmCompositionAuthoring.ReviewClaim(owner, "04-evidence-table", "evidence-table", "wax-seals",
            "evidence-cluster", "evidence-zone", new Vector2(0.5f, 0.5f), 0.65f,
            "Close-up of evidence table showing documents and wax seals.");

        GmCompositionAuthoring.ReviewClaim(owner, "05-gavel-tarnish-tell", "gavel", "sound-block",
            "bench-cluster", "bench-zone", new Vector2(0.5f, 0.5f), 0.75f,
            "High-detail framing of the brass gavel revealing the surface tarnish tell.");

        GmCompositionAuthoring.ReviewClaim(owner, "06-shard2-placement", "shard-two", "evidence-table",
            "evidence-cluster", "evidence-zone", new Vector2(0.5f, 0.5f), 0.60f,
            "Evidence table view showing Mirror Shard #2 placed among case papers.");

        GmCompositionAuthoring.ReviewClaim(owner, "07-bench-elevation", "judge-bench", "gavel",
            "bench-cluster", "bench-zone", new Vector2(0.5f, 0.55f), 0.55f,
            "Upward looking dramatic framing of the judge dais and dark archways.");

        GmCompositionAuthoring.ReviewClaim(owner, "08-defense-stand", "witness-chair", "dock-rail",
            "witness-zone", "witness-cluster", new Vector2(0.5f, 0.5f), 0.60f,
            "Player view standing at the evidence bar looking directly toward the witness dock.");

        GmCompositionAuthoring.ReviewClaim(owner, "09-verdict-passage-open", "verdict-doors", "",
            "verdict-exit-cluster", "verdict-exit-zone", new Vector2(0.5f, 0.5f), 0.55f,
            "Resolved-verdict view proving the leaves clear the onward passage to Shut the Box.");
    }
}
