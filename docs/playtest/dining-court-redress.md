# Dining Wing → Court Redress Notes

Captured overnight 2026-07-14 against live Entry Hall. Shots: `dining-01..04` under `docs/playtest/screenshots/`.

## Where it lives

| Piece | World | Code |
|-------|------:|------|
| Room shell | x −24..−8.5, z −24..−5, open on hall (R) | `roomShell(-24,-8.5,-24,-5,'R')` |
| Dining set | (−16, −14.5) | `diningSet(-16,-14.5)` |
| Sideboard | (−23.2, −14) facing +x | `sideboard(-23.2,-14,Math.PI/2)` |
| Rug | 8×11 under table | `rug(-16,-14.5,8,11,'#241010')` |
| Room light | (−16, −14) | `roomLight(-16,-14,1.05)` |
| Walk block | table footprint | `[-18,-14,-17,-12]` in `this.blocks` |

`diningSet` builds: long table (2.2×5.4 local, grp scale 0.62), 6 table legs, 10 side chairs (box builders), 2 brass candelabra clusters on the table. **No hall GLTF props yet** — all box primitives.

Verified shots (2026-07-14 overnight): `dining-02-table` / `dining-03-head` show the full set + candy-cone flames on the rug; `dining-01-from-hall` is the doorway read Court should echo; sideboard is a low cabinet on the west wall (`dining-04`).

## Court redress map

| Dining (now) | Court (target) | Asset |
|--------------|----------------|-------|
| Long table | Bar / clerk table, player at near end | swap → `Table_Large.gltf` |
| Head-end chairs | Judge bench seat | `Chair_1.gltf` ×1–2, raised plinth optional |
| Side chairs ×10 | Jury gallery (cut to ~6–8 sighted) | `Chair_1` + portrait wall behind |
| Table candelabra (brass cones) | Bench practical that **swings** on role change | `candelabra.glb` |
| Sideboard | Evidence stand + gavel block | keep sideboard OR slim lectern; drop `GMCourtProps.buildGavel` |
| Blank end wall | 9 guest portraits as jury | clone Entry Hall portrait builder |
| Room light alone | Dim gaslamps + swinging key | PointLights + role-driven target |

## Keep / drop / change

**Keep as scaffolding:** `roomShell` proportions, walk region for dining, doorway from hall pattern, rug scale as warm floor cue.

**Drop for Court scene:** playable walk of full manor hall; wake/arrival modes; Entry Hall POIs. Court is its own `.dc.html`.

**Change early:** table/chair mesh quality (hall kit), replace candy-cone “candles” with sourced candelabra, add gavel tarnish RAF hook from `gm-court-props.js`.

## Open decisions (from plan)

1. Young-Aldric portrait: still home vs drop (not in dining today).
2. Whether Court keeps the sideboard silhouette or goes to a narrower evidence lectern.
3. Jury: all 9 portraits visible vs 3 lit / 6 dust-sheeted until seals crack.

## Script modules ready to `#include`

- `gm-court-props.js` — `buildGavel`, `buildWaxSeal`, `buildDustSheet`
- Evidence copy — `docs/superpowers/specs/2026-07-14-court-evidence-draft.md`
