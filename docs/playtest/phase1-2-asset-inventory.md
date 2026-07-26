# Phase 1–2 Asset Inventory

Checked overnight 2026-07-14 against the live repo. Goal: walk into Court and Shut the Box knowing every prop we own, every gap, and what we can build procedurally so we don't stall on sourcing.

## Owned & ready to wire

### Court
| Asset | License | Path | Use |
|-------|---------|------|-----|
| Table_Large | CC0 | `assets/models/hall/Table_Large.gltf` | Bar / clerk table |
| Chair_1 | CC0 | `assets/models/hall/Chair_1.gltf` | Jury / gallery seats |
| Chandelier | CC0 | `assets/models/hall/Chandelier.gltf` | Optional loft fixture |
| Candelabra | CC-BY Don Carson | `assets/models/sourced/candelabra.glb` | Bench practical light |
| Portraits 1–9 | procedural in Entry Hall | clone in Court | Jury wall |
| Wood / stone / rug textures | in hall pack + `assets/tex/` | | Floor + furniture |

### Shut the Box
| Asset | License | Path | Use |
|-------|---------|------|-----|
| dice.glb | CC-BY Jarlan Perez | `assets/models/sourced/dice.glb` | Table dice (instance ×2) |
| Desk / table options | CC0/CC-BY | `desk_quaternius.glb`, hall Table_Large | Game table |
| Curtains | CC0 | `assets/models/sourced/curtains.glb` | End-of-hall dressing |
| Wall modular | CC0 | `wall_modular.glb` | Hidden door wall kit |
| Portrait frame pattern | Entry Hall code | | Dust-sheeted frames |

### Hidden room (Phase 4, gated by Phase 2)
| Asset | License | Path | Use |
|-------|---------|------|-----|
| dark_book.glb | CC-BY Justin Randall | `assets/models/sourced/dark_book.glb` | Journal fragments |
| desk_google / desk_quaternius | CC-BY / CC0 | sourced/ | Aldric's desk |
| mirror.glb | CC0 Isa Lousberg | sourced/ | Optional shard staging |

## Gaps

| Gap | Severity | Plan |
|-----|----------|------|
| **Gavel + sound block** | Ready (code) | `gm-court-props.js` → `buildGavel(THREE)` + `setTarnish(0..1)`. Wire when Court scene exists. Poly Pizza CDN still 403. |
| **Evidence card art** | Medium | UI cards (paper stock + wax); prose draft exists in evidence-draft spec. |
| **Wax seal HUD** | Ready (3D fallback) | `buildWaxSeal(THREE, cracked)` + plan still prefers 2D HUD cracks |
| **Bone/ivory tile inscriptions** | Ready (code) | `gm-shutbox-board.js` hinged tiles 1–9 + canvas numbers; rules in `gm-shutbox-logic.js` |
| **Dust sheets** | Ready (code) | `buildDustSheet(THREE, w, h)` linen plane |
| **Young-Aldric portrait** | Design open | Decide home or drop before STB ships |
| **Seated Aldric body** | Medium for STB | Silhouette + coat from procedural primitives first; no full character pipeline |

## Sourcing rules for this project

1. Prefer CC0 / CC-BY already in `assets/models/sourced/CREDITS.txt`.
2. Never invent license — if unknown, tag `LICENSE UNKNOWN` in catalog.
3. Hall pack GLBs stay on disk for Court/STB even though Entry Hall still uses boxes.
4. Procedural beats a style-mismatched download every time (lesson from entrance doors).

## Wire order when Phase 1 starts

1. Court scene shell using dining wing proportions + Table_Large + Chair_1 + candelabra
2. Procedural gavel + tarnish material (`color` lerp gold → dull bronze on RAF)
3. Evidence JSON + fan UI
4. Then STB: corridor clone + dice.glb + procedural boxes

## Credits to add when gavel lands

```
gavel.glb — [author], CC-BY-3.0, via Poly Pizza — Court judge's gavel
```

Until then Court uses procedural geometry and CREDITS stays accurate.
