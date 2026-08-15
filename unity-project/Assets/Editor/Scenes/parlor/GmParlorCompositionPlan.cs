using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Composition plan for the Parlor scene. Defines the zones, clusters, motivated lighting,
/// and review claims for Aldric Voss's card table, hearth, and parlor drapes.
/// Zones and clusters are authored volumes and own their own transforms; every element marker goes
/// onto the object GmParlorBuilder actually built, because the audit measures elements through their
/// renderers and an element with none is an intent nothing can check.
/// </summary>
public static class GmParlorCompositionPlan
{
    public const string SceneId = "parlor";

    public static void Author(GameObject owner, IReadOnlyDictionary<string, GameObject> built)
    {
        GmCompositionAuthoring.Begin(owner, SceneId,
            "An intimate, claustrophobic Victorian card room where Aldric Voss sits across the green felt table, bound by cheating tells.",
            minZones: 4, minClusters: 4, minElements: 12);

        // Zone 1: Gaming Table Zone
        var tableZone = new GameObject("TableZone");
        tableZone.transform.SetParent(owner.transform, false);
        tableZone.transform.position = new Vector3(0f, 1.2f, 0f);
        GmCompositionAuthoring.Zone(tableZone, "table-zone",
            "The focal gaming table where the 4-suit card game and Read mechanics take place.",
            new Vector3(6f, 4f, 6f), minClusters: 1, minElements: 4);

        // Cluster origins sit on the geometry they gather, not on the zone's nominal centre: the
        // audit measures every member's renderer against this point.
        var tableCluster = new GameObject("CardTableCluster");
        tableCluster.transform.SetParent(tableZone.transform, false);
        tableCluster.transform.position = new Vector3(0f, 0.5f, 0f);
        GmCompositionAuthoring.Cluster(tableCluster, "table-cluster", "table-zone",
            "Green-felt card table flanked by two high-backed leather armchairs under a shaded banker's lamp.", "card-table");

        GmCompositionAuthoring.Element(Built(built, "card-table"), "card-table", "table-cluster",
            "parlor-furniture", "The octagonal green baize gaming table.", GmCompositionRole.Anchor);

        GmCompositionAuthoring.Element(Built(built, "aldric-chair"), "aldric-chair", "table-cluster",
            "parlor-furniture", "High-backed carved armchair where the host Aldric Voss is seated.",
            GmCompositionRole.Support);

        // Deck and lamp stand on the baize, so they declare the table top (y 0.76) as their surface
        // rather than the floor the default assumes.
        GmCompositionAuthoring.Element(Built(built, "card-deck"), "card-deck", "table-cluster",
            "gameplay-cards", "The 28-card deck displaying custom Flames, Eyes, Bones, and Teeth suits.",
            GmCompositionRole.Gameplay, surfaceY: 0.76f);

        GmCompositionAuthoring.Element(Built(built, "banker-lamp"), "banker-lamp", "table-cluster",
            "parlor-lighting", "Green glass banker's lamp casting focused yellow light onto the table.",
            GmCompositionRole.Detail, surfaceY: 0.76f);

        // Zone 2: Hearth Fire Zone
        var hearthZone = new GameObject("HearthZone");
        hearthZone.transform.SetParent(owner.transform, false);
        hearthZone.transform.position = new Vector3(4.5f, 1.5f, 0f);
        GmCompositionAuthoring.Zone(hearthZone, "hearth-zone",
            "Carved stone fireplace giving warm ambient flicker and shadow play.",
            new Vector3(4f, 5f, 6f), minClusters: 1, minElements: 3);

        var hearthCluster = new GameObject("HearthCluster");
        hearthCluster.transform.SetParent(hearthZone.transform, false);
        hearthCluster.transform.position = new Vector3(3.5f, 0.9f, 0f);
        GmCompositionAuthoring.Cluster(hearthCluster, "hearth-cluster", "hearth-zone",
            "Stone mantelpiece framing dying orange embers.", "stone-mantel");

        GmCompositionAuthoring.Element(Built(built, "stone-mantel"), "stone-mantel", "hearth-cluster",
            "parlor-architecture", "Carved Victorian stone mantelpiece.", GmCompositionRole.Anchor);

        GmCompositionAuthoring.Element(Built(built, "fire-embers"), "fire-embers", "hearth-cluster",
            "parlor-lighting", "Glowing coal embers emitting motivated warm firelight.",
            GmCompositionRole.Support, surfaceY: 0.2f);

        GmCompositionAuthoring.Element(Built(built, "stag-clock"), "stag-clock", "hearth-cluster",
            "parlor-decor", "Ornate brass mantel clock with a stag crest ticking in the silence.",
            GmCompositionRole.Detail, GmSpatialRelation.AgainstBoundary, "stone-mantel");

        // Zone 3: Cabinet & Spirits Zone
        var cabinetZone = new GameObject("CabinetZone");
        cabinetZone.transform.SetParent(owner.transform, false);
        cabinetZone.transform.position = new Vector3(-4.5f, 1.8f, 0f);
        GmCompositionAuthoring.Zone(cabinetZone, "cabinet-zone",
            "Dark corner cabinet with decanters and locked drawer.",
            new Vector3(4f, 5f, 6f), minClusters: 1, minElements: 2);

        var cabinetCluster = new GameObject("CabinetCluster");
        cabinetCluster.transform.SetParent(cabinetZone.transform, false);
        cabinetCluster.transform.position = new Vector3(-3.4f, 0.6f, 0f);
        GmCompositionAuthoring.Cluster(cabinetCluster, "cabinet-cluster", "cabinet-zone",
            "Sideboard holding crystal decanters and unopened letters.", "bar-cabinet");

        GmCompositionAuthoring.Element(Built(built, "bar-cabinet"), "bar-cabinet", "cabinet-cluster",
            "parlor-furniture", "Mahogany sideboard cabinet.", GmCompositionRole.Anchor);

        GmCompositionAuthoring.Element(Built(built, "crystal-decanter"), "crystal-decanter",
            "cabinet-cluster", "parlor-decor", "Amber liquor decanter catching room specular highlights.",
            GmCompositionRole.Detail, surfaceY: 1.1f);

        // The cluster's own purpose names the letters, and a cluster owes at least one support
        // element besides its anchor and its detail, so the sideboard's second half is authored
        // rather than the requirement lowered to match a half-dressed corner.
        GmCompositionAuthoring.Element(Built(built, "sealed-letters"), "sealed-letters",
            "cabinet-cluster", "parlor-paper", "Unopened correspondence stacked where the host never reads it.",
            GmCompositionRole.Support, surfaceY: 1.1f);

        // Zone 4: Entryway & Drapes Zone
        var entryZone = new GameObject("EntryZone");
        entryZone.transform.SetParent(owner.transform, false);
        entryZone.transform.position = new Vector3(0f, 1.5f, -4.5f);
        GmCompositionAuthoring.Zone(entryZone, "entry-zone",
            "Heavy velvet curtained entryway sealing the parlor from the Entry Hall.",
            new Vector3(6f, 4f, 4f), minClusters: 1, minElements: 3);

        var doorwayCluster = new GameObject("DoorwayCluster");
        doorwayCluster.transform.SetParent(entryZone.transform, false);
        doorwayCluster.transform.position = new Vector3(0f, 1.1f, -3.7f);
        GmCompositionAuthoring.Cluster(doorwayCluster, "doorway-cluster", "entry-zone",
            "Closed double doors flanked by heavy damask drapes.", "parlor-doors");

        GmCompositionAuthoring.Element(Built(built, "parlor-doors"), "parlor-doors", "doorway-cluster",
            "parlor-architecture", "Heavy paneled double doors leading back to the Entry Hall.",
            GmCompositionRole.Anchor);

        GmCompositionAuthoring.Element(Built(built, "damask-drapes"), "damask-drapes", "doorway-cluster",
            "parlor-textile", "Dark red velvet drapes muffling exterior sound.", GmCompositionRole.Support);

        GmCompositionAuthoring.Element(Built(built, "brass-handle"), "brass-handle", "doorway-cluster",
            "parlor-hardware", "Brass lock plate that latches shut when the game begins.",
            GmCompositionRole.Detail, GmSpatialRelation.AgainstBoundary, "parlor-doors");

        // Both practicals in this room have a fixture the player can see, which is what a motivated
        // light asserts. The ceiling fill has none, so claiming a source for it would be a lie about
        // the room; it is authored as the composition fill it actually is, bound to the table it shapes.
        GmCompositionAuthoring.Motivate(Built(built, "banker-lamp-light"), "banker-lamp-light",
            "banker-lamp", "Warm downlight thrown by the green glass shade standing on the baize.");

        GmCompositionAuthoring.Motivate(Built(built, "fireplace-light"), "fireplace-light",
            "fire-embers", "Amber flicker thrown by the dying coals in the hearth.");

        GmAdaptiveIntentAuthoring.Light(Built(built, "ambient-fill-light"), "parlor-ambient-fill",
            GmLightIntentKind.CompositionFill,
            "Cold ceiling fill keeping the room off pure black outside the lamp cone, without inventing a second fixture.",
            subjectElementId: "card-table", maximumSubjectDistance: 4f);

        // Author 10 explicit review claims.
        // The trailing float on every one of these is passed through the interim ReviewClaim adapter
        // as a symmetric viewport tolerance. That reading is NOT confirmed and it is Nick's to settle
        // (decision D1 in docs/audit/F1-review-claim-decision.md): the numbers run backwards for a
        // tolerance, and several are wider than the window the visibility test already enforces, so
        // those framing assertions cannot fail. A green composition audit is therefore not evidence
        // that these shots are framed. Do not retune these numbers to move a gate.
        GmCompositionAuthoring.ReviewClaim(owner, "01-table-perspective", "card-table", "aldric-chair",
            "table-zone", "hearth-zone", new Vector2(0.5f, 0.4f), 0.50f,
            "Player view across the green table directly facing Aldric's seat.");

        GmCompositionAuthoring.ReviewClaim(owner, "02-aldric-portrait-framing", "aldric-chair", "card-table",
            "table-cluster", "table-zone", new Vector2(0.5f, 0.6f), 0.60f,
            "Close portrait framing of Aldric Voss across the baize.");

        GmCompositionAuthoring.ReviewClaim(owner, "03-card-hand-layout", "card-deck", "card-table",
            "table-cluster", "table-zone", new Vector2(0.5f, 0.3f), 0.70f,
            "Legible card hand presentation with 4 distinct custom suits.");

        GmCompositionAuthoring.ReviewClaim(owner, "04-banker-lamp-focus", "banker-lamp", "card-deck",
            "table-cluster", "table-zone", new Vector2(0.6f, 0.5f), 0.45f,
            "Downlight cast and green glass reflection illuminating cards.");

        GmCompositionAuthoring.ReviewClaim(owner, "05-hearth-glow", "stone-mantel", "fire-embers",
            "hearth-cluster", "hearth-zone", new Vector2(0.5f, 0.5f), 0.55f,
            "Warm amber fireplace shadows playing against dark wood paneling.");

        GmCompositionAuthoring.ReviewClaim(owner, "06-clock-closeup", "stag-clock", "stone-mantel",
            "hearth-cluster", "hearth-zone", new Vector2(0.5f, 0.6f), 0.40f,
            "Mantel clock showing roman numerals and the broken stag crest.");

        GmCompositionAuthoring.ReviewClaim(owner, "07-cabinet-corner", "bar-cabinet", "crystal-decanter",
            "cabinet-cluster", "cabinet-zone", new Vector2(0.4f, 0.5f), 0.50f,
            "Atmospheric side cabinet corner with crystal reflections.");

        GmCompositionAuthoring.ReviewClaim(owner, "08-entry-drapes", "parlor-doors", "damask-drapes",
            "doorway-cluster", "entry-zone", new Vector2(0.5f, 0.55f), 0.50f,
            "Threshold view showing heavy velvet drapes and locked doors.");

        GmCompositionAuthoring.ReviewClaim(owner, "09-the-read-focus", "aldric-chair", "banker-lamp",
            "table-cluster", "table-zone", new Vector2(0.5f, 0.5f), 0.85f,
            "Close accusatory view during 'The Read' time-dilation mechanic.");

        GmCompositionAuthoring.ReviewClaim(owner, "10-room-wide", "card-table", "stone-mantel",
            "table-zone", "hearth-zone", new Vector2(0.5f, 0.5f), 0.40f,
            "Wide corner establishing shot showing complete parlor mood and lighting balance.");
    }

    // A composition marker belongs on the built object or it measures nothing. A missing key means
    // the builder and this plan have drifted apart, which is worth stopping the build for: quietly
    // authoring onto a fresh empty GameObject is the exact failure this lookup exists to prevent.
    static GameObject Built(IReadOnlyDictionary<string, GameObject> built, string id)
    {
        if (built == null || !built.TryGetValue(id, out GameObject go) || go == null)
            throw new InvalidOperationException(
                $"[GmParlor] composition element '{id}' has no object from GmParlorBuilder");
        return go;
    }
}
