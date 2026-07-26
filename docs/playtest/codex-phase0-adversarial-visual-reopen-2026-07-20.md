# Phase 0 Adversarial Visual Reopen

Date: 2026-07-20  
Decision: previous B+ / A- visual grade withdrawn  
Current environment grade: D+  
Status: not ready for Nick's acceptance walk

## Verdict

Nick's rejection is confirmed. The current exported player is technically functional but visually
reads as a grey test arena containing disconnected asset islands. Three independent audits and a
fresh native capture reached the same result.

The previous claim that Wend Hill was built on an owned environment was materially misleading. It
cloned Witch Village TerrainData, then discarded the donor scene's dense vegetation, decals, water,
forest composition, fog relationships and edge transitions. Counting 695 grass objects did not
produce continuous visible ecology.

No new environment purchase is justified. The owned scenes already demonstrate the missing quality.

## Cold visual audit table

| ID | Finding | Severity | Evidence | Owner |
|---|---|---|---|---|
| V-01 | The estate is an enormous nearly uniform floor with sparse prop islands. | BLOCKING | `standalone-proof/02-spawn-facing-mansion.png`, `04-middrive-depth.png` | Agent |
| V-02 | The drive has no convincing crown, rut, puddle, shoulder, ditch or encroaching verge. | BLOCKING | `tour-03-gate.png`, `tour-05-middrive.png` | Agent |
| V-03 | Trees repeat at similar spacing and contrast; the horizon exposes procedural placement. | BLOCKING | `tour-04-lookback.png`, `tour-15-figure-far.png` | Agent |
| V-04 | Cemetery buildings, walls, graves and plants do not form one burial ground. | BLOCKING | `tour-06-cem-path.png`, `tour-07-cem-inside.png`, `tour-13-cem-detail.png` | Agent |
| V-05 | The garden is the clearest room-with-stuff composition and uses shrub rows as obstacles. | BLOCKING | `tour-09-gdn-inside.png`, `tour-14-gdn-detail.png` | Agent |
| V-06 | The coach house reads as a prefab preview with four props on an empty floor. | BLOCKING | `tour-11-coach-yard.png` | Agent |
| V-07 | The mansion is a centered dollhouse backdrop with an empty apron and no foundation transition. | BLOCKING | `tour-01-spawn.png`, `tour-12-porch.png` | Agent |
| V-08 | The SUV is stranded in open acreage and loose rectangular marks look like debug tiles. | POLISH | `tour-02-car.png` | Agent |
| V-09 | Night values collapse into near-black, nearly grayscale fields; darkness conceals rather than shapes depth. | BLOCKING | Native frames average 26.9-33.1 luminance and 3.1-4.4 saturation out of 255. | Agent |
| V-10 | Native art proof is covered by synthetic interaction, wind, control and story overlays. | BLOCKING | `standalone-proof/02` through `07` | Agent |
| V-11 | Existing visual gates prove file existence and transform counts, not rendered quality. | BLOCKING | `unity-cli.mjs`, `GmStandaloneReviewProbe.cs`, `GmEstateQualityAudit.cs` | Agent |
| V-12 | Review cameras influence tree placement, creating sterile bubbles around the evidence cameras. | BLOCKING | `GmEstateBuilderV2.cs` tree exclusions against review poses | Agent |
| V-13 | Composition claims accept nearly any subject size and leave foreground/support/background empty. | BLOCKING | `GmEstateBuilderV2.cs` review claim setup | Agent |
| V-14 | Current stored audit and occupancy evidence are stale and do not match the current scene fingerprint. | BLOCKING | stored `5ef26a...` versus current `ba3c79...` | Agent |
| V-15 | The tracker reports 15.03ms mean, while the latest native proof reports 16.67ms mean / 17.44ms p95. | BLOCKING | `standalone-proof/performance.json` | Agent |
| V-16 | Figure visibility, wind character and approach pacing require a human walk after art remediation. | NICK | sensory decision | Nick |
| V-17 | Threshold Refusal remains closed-door and the estate retains one mansion. | WONTFIX | locked canon | Canon |

## Owned reference proof

Comparison contact sheet: `/tmp/gm-owned-reference-vs-wend.png`

Primary donor scenes:

1. `Assets/LeartesStudios/WitchVillage/HDRP/Scene/HDRP_WitchVillage.unity`
2. `Assets/LeartesStudios/Abandoned Village/HDRP/Scene/HDRP_Abandoned_Village.unity`
3. `Assets/LeartesStudios/HauntedVillage/Scene/Showcase.unity`

Witch Village provides the strongest continuous natural substrate: dense tall grass, moss, rocks,
fallen timber, overlapping forest layers and low haze. Abandoned Village provides the strongest
route grammar: embedded wheel ruts, shoulders, functional clutter and multiple depth planes. Haunted
Village provides the strongest dead hedgerow and wet-road vocabulary.

Important unused owned families include Witch moss clumps, white moss, small rocks, branch banks,
logs, ivy, lianas, cypress, mangrove and pine; Haunted Village bushes 01-07, grasses 01 and 03-11,
and puddle decals; and Abandoned Village hills, cliffs, dead bushes and broken fence variants.

## Required rebuild sequence

1. Split clean native art capture from controller and interaction testing.
2. Bind captures, reports, occupancy data, scene fingerprint and player build hash into one evidence
   manifest; stale evidence must fail.
3. Build a donor-scene inspector and contact-sheet workflow so owned reference composition is visible
   during authoring.
4. Rebuild the estate from Witch ecosystem grammar, Abandoned route grammar and Haunted understory,
   preserving one mansion and the locked threshold.
5. Establish a complete drive cross-section: narrow track, crown, ruts, water, shoulders, drainage,
   encroachment and layered tree/hedge walls.
6. Replace transform-count ecology with visible multi-height coverage and deliberate clearings.
7. Rebuild cemetery, garden, coach yard, porch foundation and car arrival as connected places with
   ground consequences and functional circulation.
8. Tune exposure, fog and lights only after geometry reads in a neutral diagnostic pass.
9. Capture clean native route frames and 360-degree station sweeps, then run independent environment,
   proof-system and UX reviews.
10. Ask Nick to walk only after the blocking visual findings are closed.

## Grade and gate

- Technical execution: A
- Verification credibility: D until stale and self-certifying evidence is removed
- Environment art: D+
- Phase 0 status: active art remediation
- Court: locked

