# Full Grounds Plan — Wend Hill Estate (Prologue exterior)

Written 2026-07-16, after the estate rebuild pass (side paths + first nine Leartes buildings).
This is the master plan for the complete explorable grounds: every walkway, every destination,
every thing worth looking at, and where each asset comes from. Percentages and layout use the
live coordinate system (drive runs z=103 spawn-side → z=-36 arrival; mansion face at z≈-52).

## Design intent

The estate is the game's cold open — it must do three jobs before the player ever touches a card:
1. Sell the fiction: a coaching-inn-turned-manor that money abandoned ("the house on Wend Hill").
2. Reward disobedience: the invitation says walk to the house; every detour deepens the dread.
3. Plant payoffs: names, dates, and objects out here recur inside (portraits, host slips, endings).

Nothing out here blocks the critical path. All exploration is optional, diegetic, and quiet.

## Walkway network

```
                     N (z+, spawn/outer gate)
        coach house yard (W3b)
             |
  W3 garden path (z 22) ---- DRIVE (W1) ---- W2 cemetery path (z 28.5)
             |                  |                   |
        ruined garden        front gate         cemetery plot
        + shed yard          (z 65, locks)          |
                                |               W2b lychgate → chapel forecourt (E)
                             porch (z -36+)
```

| ID  | Route | Status |
|-----|-------|--------|
| W1  | Main drive, spawn → gate → porch | BUILT (original) |
| W2  | East gravel branch (z 28.5) → cemetery plot | BUILT 2026-07-16 |
| W2b | Cemetery east wall → lychgate → chapel forecourt (x 24.6→38, z 26–34) | **NEXT (G1)** |
| W3  | West gravel branch (z 22) → ruined garden + shed yard | BUILT 2026-07-16 |
| W3b | Garden north edge → coach-house yard (x −32..−24, z 38–56) | **NEXT (G1)** |
| W4  | Mansion side/porch flanks | DEFERRED — Threshold Refusal canon keeps the approach frontal |
| W5  | Spawn forecourt around the car | exists as clamp; polish only |

Movement is the walk-rect union + solid AABBs added in the estate pass — new walkways are new
rects plus their dressing. Keep every route ≥3u wide so the funnel never fights the player.

## Destinations & points of interest

Examine system (G1): press E near a POI → one beat-style line (reuses the beat text UI, no new
HUD). The legend already teaches "E interact"; today it does nothing in the Prologue — that gap
becomes the feature.

### The drive (W1)
- The car (exists): examine — engine still ticking. Anchor of the secret ending.
- Dropped invitation satchel (exists): examine.
- **Gate pier plaque (G1, new): "WEND HILL" cut into the stone — canonical name payoff.**
- Drive lanterns (exist): one flickers harder than the rest (RAF already flickers; pick one, amplify).

### Cemetery (W2)
- Headstones (26): 2–3 examinable. Weathered lines; ONE legible date that contradicts the house's
  apparent age (history layer L2/L3 tease). No names that collide with Entry Hall portraits yet —
  story bible owns that mapping.
- **Open grave + shovel (exists): examine — "Fresh. No name yet." The single scariest object.**
- Stag monument (exists): examine — the family crest beast, antler snapped.
- Leartes crosses (12), willow moss, bench: dressing, no text.

### Chapel forecourt (W2b — G1)
- Chapel (exists as landmark): becomes reachable. SM_Door_01 plank door mounted shut on its
  entrance + examine ("locked long before I was born"). Interior = never (window glow only, maybe).
- Lychgate at the cemetery's east wall (G1): timber posts + SM_DoorFrame or procedural.
- Bell? (Sorcerers Hut / Witch Village payloads have bells — G2 candidate; wind-caught single toll.)

### Ruined garden (W3)
- Scarecrow (exists): examine — its coat is too fine for a scarecrow (host-adjacent chill).
- Dry fountain (exists): examine — coins fused to the basin.
- Shed (exists): SM_Door_02 mounted shut + examine; bucket/shovel dressing placed.

### Coach-house yard (W3b — G1)
- Coach house (exists): reachable; big doors stay shut (examine — "wheel ruts, decades old").
- Cart (exists): dressing. Lantern on a post (G2).
- History payoff: this is the coaching-inn layer made physical (spec: house-history.md).

### Audio POIs (G2)
- Crow burst when first entering the cemetery plot (one-shot, Horror Elements has candidates).
- Chain/wood creak near the coach house.
- Single bell toll at the chapel on a long random timer.

## Asset sourcing (bundle payloads, mined → remaining)

| Payload | Mined so far | Grounds candidates remaining |
|---|---|---|
| Haunted Village (1.3G) | 16 FBX (church, houses, doors, fences, cart, pugalo, pit…) | trees 01–10, RoofWall/WoodWall modulars, Trube chimneys, bench variants |
| Witch Village (4.0G) | willow + maps | gallows?, well, hanging cages, lantern posts, crates |
| Abandoned Village (3.5G) | — | barn/well/fence variants, wagon, troughs |
| Aftermath (3.1G) | — | ruined walls, rubble piles (labyrinth later) |
| Haunted Prison / Demonic / Museum / Sorcerers Hut / Mansion Interior | — | interior phases, not grounds |

Rule stands: selective single-mesh GLBs ≤10MB, night-dressed in code, tri budget for the whole
exterior ≤1.5M rendered (currently 1.19M).

## Phasing

- **G1 — SHIPPED 2026-07-16 (same night):** examine system (10 POIs, E + proximity hint) · W2b
  lychgate + chapel forecourt · W3b connector + coach-house yard · WEND HILL pier plaque · chapel
  plank door. All verified: extended capture-estate gate (walk-in, examine, rect asserts) PASS,
  full battery PASS (tests 23+47, door, full, env, breath, handoff, polish).
- **G2 — SHIPPED 2026-07-16 (same night):** well (garden) + ember braziers (both junctions) +
  feeding trough (coach yard) + hand lantern (cemetery bench) mined by hash-targeted extraction
  from Abandoned/Witch Village · chapel bell one-shot (forecourt-gated, verify-g2 asserts it
  cannot fire on the drive) · dying drive lamp · 3 new POIs (13 total) · chapel base wall
  segments. Trunk totem converted, previewed, rejected. Bonus: fixed the pre-existing negative-
  intensity lamp flicker bug the new verifier caught. Headstone NAME mapping (vs story bible)
  remains deferred to the bible owner.
- **G3 (post-Nick-walk):** whatever the walk surfaces; density pass with Witch/Abandoned Village
  props; optional W4 side flank IF canon changes.

## Verification contract

Every tranche: capture-estate poses extended to new areas + live walk-proof into each new rect +
play-door/play-full/tests + baseline tri/call check + shots read by eye before claiming done.

**2026-07-17 addendum — the contract is now automated:** `npm run playtest:agent` plays the whole
opening (geometry audit → coverage grid → 18-stop walkthrough with real input + E at every POI +
frame metering + KO/aftermath → scene boots) and emits ranked findings. Green = HIGH:0. Run it
after ANY grounds change; treat its findings file (`docs/playtest/agent-report.json`) as the
punch list.
