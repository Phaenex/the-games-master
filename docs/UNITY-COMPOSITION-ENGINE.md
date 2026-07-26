# Authored Unity composition engine

This layer turns scene intent into a build-time contract. It exists to prevent a room from passing
because it compiles, contains enough props, and produces screenshots. Every important visible object
must belong to a named story cluster, every cluster must have a dominant subject, and every review
shot must say what it is proving.

The engine does not generate taste. It makes missing intent and common placement failures visible
early enough that a human can spend review time on mood, beauty, fear, subtlety, and pacing.

## Open-source research and decision

No third-party code or Unity package was imported in this pass. The useful patterns were small enough
to implement against the project's existing Unity 6000.5 APIs, so the shipping project gained no new
dependency or license obligation.

| Project | Useful pattern | License / fit | Decision |
|---|---|---|---|
| [Procedural Toolkit](https://github.com/Syomus/ProceduralToolkit) | Deterministic procedural geometry as a library, separate from scene authorship | MIT; current Unity support | Keep as a reference if Court or Labyrinth needs custom mesh generation. Do not install for placement. |
| [Free Prefab Painter](https://github.com/alexanderameye/prefab-painter) | Palettes, surface projection, brush masks, density controls | MIT; tested on an old Unity release | Adopt the palette and mask concepts, not the old package. A brush can dress a surface but cannot explain a scene. |
| [Prefab Painter](https://github.com/Orange-Panda/Prefab-Painter) | Small package structure for direct prefab placement | MIT; very small project | No import. It does not supply story clusters, route reservations, or validation. |
| [WaveFunctionCollapse](https://github.com/mxgmn/WaveFunctionCollapse) | Adjacency constraints and contradiction detection | MIT; general algorithm, not a Unity room-authoring system | Reserve for local modular problems such as Labyrinth cells. Do not use it to invent authored story rooms. |
| [Unity Viewpoint Computation](https://github.com/robertoranon/Unity-ViewpointComputation) | Describe camera requirements using subject visibility, size, and screen position | Apache-2.0; built for Unity 5 | Reimplemented the small relevant idea in the review-claim audit. Do not import an obsolete solver. |
| [Unity Splines](https://docs.unity3d.com/Manual/com.unity.splines.html) | Author and evaluate routes and object placement along curves | Official Unity package for Unity 6 | Candidate for curved exterior paths later. Not needed for Court, Entry Hall, or other rectilinear rooms. |
| [Unity Graphics Test Framework](https://docs.unity3d.com/Packages/com.unity.testframework.graphics@9.0/manual/index.html) | Reference-image comparison and graphics regression testing | Official Unity package; Unity 6000 compatible | Consider after Nick approves a visual baseline. Do not freeze an unapproved look into golden images. |

The core lesson is consistent across these tools: automation is strongest at constraints, repeatable
placement, and evidence. It is weakest at deciding what a room means. The engine therefore requires
authored meaning before it checks geometry.

## Composition grammar

Each generated scene owns one `GmSceneComposition` and builds its plan from a dedicated
`<Scene>CompositionPlan.cs` file.

The plan contains:

- **Zones:** bounded parts of the scene with a stated dramatic or navigational purpose.
- **Clusters:** small visual sentences inside a zone. Each has one named anchor, support pieces, and
  details. A cluster is rejected when it is only undifferentiated scatter.
- **Elements:** visible authored objects with stable IDs, asset families, roles, rationales, and
  spatial relationships.
- **Routes:** explicit player lanes with clearance widths.
- **Negative space:** volumes that must remain visually clear for silhouettes, movement, or dread.
- **Motivated lights:** local lights tied to visible source elements and a stated purpose.
- **Review claims:** one declaration per screenshot naming the primary subject, foreground,
  supporting layer, background, expected screen position, and apparent size.

Element roles are `Anchor`, `Gameplay`, `Support`, `Detail`, `Boundary`, `Background`, `Ground`, and
`RouteCue`. Spatial relations are `Grounded`, `AgainstBoundary`, `FlanksAnchor`, `LeadsToAnchor`,
`FramesRoute`, `BackgroundLayer`, `Suspended`, and `Embedded`.

This vocabulary is deliberately plain. A reviewer should be able to inspect a hierarchy and answer
why an object exists without reverse-engineering a random seed.

## Builder pattern

The scaffold creates a red placeholder instead of inventing a room:

```csharp
public static void Author(GameObject owner)
{
    GmCompositionAuthoring.Begin(owner, SceneId,
        "The jury wall compresses the player toward one isolated witness chair.",
        minZones: 2, minClusters: 3, minElements: 14);

    var zone = new GameObject("WitnessZone");
    zone.transform.position = new Vector3(0f, 1.5f, 5f);
    GmCompositionAuthoring.Zone(zone, "witness-zone",
        "Makes the exposed chair readable before evidence interaction.",
        new Vector3(8f, 4f, 7f), minClusters: 1, minElements: 5);

    var cluster = new GameObject("WitnessCluster");
    cluster.transform.position = new Vector3(0f, 0f, 5f);
    GmCompositionAuthoring.Cluster(cluster, "witness", "witness-zone",
        "One exposed chair opposed by the denser jury wall.", "witness-chair");

    GmCompositionAuthoring.Element(chair, "witness-chair", "witness", "court-furniture",
        "The isolated gameplay anchor and emotional target.", GmCompositionRole.Gameplay);
    GmCompositionAuthoring.Element(table, "evidence-table", "witness", "court-furniture",
        "Creates a lower support mass without competing with the chair.", GmCompositionRole.Support);
    GmCompositionAuthoring.Element(droppedCard, "dropped-card", "witness", "paper-traces",
        "A human trace that pulls the eye into interaction distance.", GmCompositionRole.Detail);
}
```

The real plan also reserves routes and sightlines, motivates local lights, and declares every review
shot. IDs are stable contracts. Object names may change without silently invalidating a test.

## What the audit rejects

`GmSceneCompositionAudit` reports a failure for:

1. a missing, duplicate, wrong-scene, or placeholder composition manifest;
2. too few zones, clusters, or elements for the scene's declared floor;
3. missing IDs, duplicate IDs, placeholder text, or empty rationales;
4. orphan elements and clusters outside their named zones;
5. clusters without their declared anchor, support layer, or detail layer;
6. clusters that exceed their authored radius or member cap;
7. palette monoculture beyond the cluster's declared dominant-family limit;
8. visible elements with no renderer;
9. grounded elements whose rendered base misses their declared surface;
10. broken spatial relationships or references to missing routes;
11. exact overlapping element transforms;
12. occupied negative-space volumes;
13. tagged scene elements blocking an authored route;
14. enabled local lights without a visible source and purpose;
15. review claims with missing subjects, invisible subjects, wrong screen position, wrong apparent
    scale, or foreground/background IDs at the wrong depth;
16. a tour capture that is effectively black, white, or too flat to inspect, using percentile and
    clipping metrics rather than mean luminance alone.

These checks deliberately operate on tagged authored elements. Imported prefab children inherit the
meaning of the marked prefab root, so a complex model does not need hundreds of marker components.

## What remains a human gate

The audit cannot prove that a room is frightening, period-correct, elegant, or emotionally coherent.
It also cannot tell whether a legal asset is the right asset, whether repetition is visible in a
specific texture, whether audio sounds like a spaceship, or whether five minutes feels too slow.

A room still needs:

1. cold deterministic rebuild;
2. structural and composition audits;
3. EditMode and route PlayMode tests;
4. the complete HDRP review tour;
5. full-size inspection of every frame;
6. a standalone player walk with final input, UI, rendering, and audio;
7. Nick's taste call before approval.

The correct failure mode is explicit: tests may say the scene is structurally reviewable while the
human verdict still says it looks bad. The tools must never turn a numeric pass into an A grade.

## Adoption rule

All newly scaffolded scenes use this contract immediately. Wend Hill now has a metadata-only retrofit
covering 8 zones, 15 clusters, 57 elements, 5 routes, 5 negative-space reservations, 17 classified
lights, 18 review claims, and 2 guarded adaptive slots. The retrofit survived scene close/reopen and
preserved fingerprint `cb198edc4905ac2e`; it did not move or replace visible content.

The first from-scratch production use remains Entry Hall. It is small enough to prove the complete
workflow and important enough that the composition contract will catch a weak room before Court
inherits its mistakes. The supervised memory, asset index, variants, and baseline policy are in
`docs/UNITY-SCENE-INTELLIGENCE.md`.
