# Phase 1 Court + Phase 2 Shut the Box — Build Plan & Asset Book

Overnight design pass. Status: **designed + assets inventoried. Scenes not stubbed yet** (Phase 0 Nick playtest still gates Court start). When Nick greenlights after opening walk, implement Court first.

Sources of truth already on disk:
- `docs/superpowers/specs/2026-07-12-per-game-cheats-design.md`
- `docs/superpowers/specs/2026-07-12-story-bible-final.md`
- `docs/superpowers/plans/2026-07-12-build-roadmap.md`
- `The Games Master - Art Direction.dc.html`

---

## Asset status at a glance

| Need | Have? | Path / note |
|------|-------|-------------|
| Dining table upgrade | YES (unused) | `assets/models/hall/Table_Large.gltf` CC0 |
| Chairs / jury seating | YES (unused) | `assets/models/hall/Chair_1.gltf` CC0 |
| Court candelabra / bench light | YES (unused) | `assets/models/sourced/candelabra.glb` CC-BY Don Carson |
| Chandelier (optional Court loft) | YES (unused) | `assets/models/hall/Chandelier.gltf` CC0 |
| Entry Hall 9 portraits (jury dress) | YES (procedural in Entry Hall) | Clone builder into Court — no new art |
| Evidence cards (text + UI) | NO | Design only — write as JSON/data in Court scene |
| Gavel + block (tarnish state) | YES (module) | `gm-court-props.js` `buildGavel` + `setTarnish` — Poly Pizza optional later |
| Bone / table dice | YES (unused) | `assets/models/sourced/dice.glb` CC-BY Jarlan Perez |
| Shut-the-Box tiles 1–9 | YES (module) | `gm-shutbox-logic.js` + `gm-shutbox-board.js` |
| Dust-sheeted portrait frames | YES (module) | `buildDustSheet` in `gm-court-props.js` + Entry Hall frame pattern |
| Hidden-room door (tile-9 panel) | PROCEDURAL | Wall panel that cracks open on correct Hold |
| Dark book / journal (hidden room) | YES (unused) | `assets/models/sourced/dark_book.glb` CC-BY |
| Young-Aldric secret portrait | **OPEN DESIGN** | Decide: examine in Shut the Box hall OR formally drop |
| Hall modular kit | YES (unused) | `assets/models/hall/*` CC0 — Court / STB upgrades |
| Shared ambience beds | YES | Reuse `amb_dark` / wood creaks from Prologue / Entry Hall |

---

## Phase 1 — The Court ("The Assize of One")

### Room geometry (cheapest path)

Fork Entry Hall's **dining wing** pattern (`diningSet` / `roomShell` around world `(-16,-14.5)` style), as a new scene:

`The Games Master - Court.dc.html`

Do **not** start from Parlor (2D). Do **not** wait for a courthouse kit.

Dressing map (Art Direction):

| Dining piece | Court role |
|--------------|------------|
| Long table | Bar / clerk table (player stands at near end) |
| Head chairs | Judge's bench (Aldric switches voices, same body) |
| Side chairs × N | Jury box backdrop — portrait frames of 9 guests facing in |
| Center candelabra | Bench light (swing when role changes) |
| Sideboard / empty wall | Evidence stand / gavel block |

Lighting beat: one practical warm key that **swings** when Aldric's speaking role changes (prosecutor → judge → bailiff → foreman). Gaslamps dim. Matches Art Direction Court study.

### Game data model (to author before code)

File suggestion: inline `EVIDENCE` array on the Court scene (same style as Entry Hall POIs), later extract if needed.

```
sealCount: 3                 // wax seals = 3 arguments to land
deck: [
  { id, title, body, isTrue, isShard? },
  ...
]
rigged: false by default     // becomes true only when corruption / revisit risk fires
gavelTarnish: 0..1           // lerps gold → dull on true-card present when rigged
```

**Fail-state (non-negotiable from cheats design):**
- First visit / low corruption: straight trial, no planted deck, no tarnish — **loseable**.
- Rigging + tarnish only after Aldric is at genuine risk of loss (reactive-only rule).

**Catch verb:** present true Evidence Card (not Parlor's Read). HUD: 3 wax seals crack as arguments stick; Evidence Cards fan at lower edge.

**cheatsCaught:** +1 per seal-ish catch, local for Phase 1 (reuse `GMProgress` / Parlor pattern if already easy; otherwise local counter + TODO for Phase 5).

### Build sequence (Court)

1. Scene shell: room + table + chairs + sconces/candelabra load + player wander clamp
2. Portrait jury backdrop (reuse Entry Hall portraits list / frames)
3. Evidence fan UI + present action
4. Straight-trial win/lose
5. Rigging branch + gavel gold→tarnish lerp (continuous, RAF)
6. Role lighting swing
7. Shard card (can land early with Phase 3, or stub)
8. Test Harness group: build, present-true, first-visit-loseable, rigging-then-tarnish

### Acceptance (Court)

- [ ] Scene boots in harness, zero page errors
- [ ] First visit can lose
- [ ] Later/high-corruption visit: true card → gavel tarnishes visibly
- [ ] Jury reads as the 9 named guests (same names as Entry Hall)
- [ ] Visual gate: bench, jury wall, bar close-up, seal HUD, tarnish before/after

---

## Phase 2 — Shut the Box

### Room geometry

New scene: `The Games Master - Shut the Box.dc.html`

Long dim hall patterned on Entry Hall's portrait corridor:
- Frames left/right, most picture lights **off**, dust sheets over several canvases
- One dark table mid-hall, two box boards facing each other
- Aldric seated opposite (simple seated silhouette + voice — no high-fid character mesh)
- Dice prop from `dice.glb` (or two die instances)
- Far wall: paneling that becomes the hidden door (tile-9 Hold)

### Mechanics summary (from cheats design — do not reinvent)

- Head-to-head, 9 tiles each, alternate rolls, voluntary stop allowed
- Tiles = ledger order: Marr…Percival
- Hold: live tally of both boards + roll history; wrong Hold costs
- Cheats (only when Aldric behind): palming, false calls, board tampering
- Hidden door: correct Hold on **tampering of tile 9** at Tier 3+

### AI scope

Match `gmFollow()` complexity in Parlor. Legal-move enumerations over open tiles + dice total. Cheat insertion only when his open-sum is worse than the player's and corruption tier allows. Do **not** build a search-theoretic engine.

### Build sequence (STB)

1. Hall shell + dust-sheet frames + seated Aldric placeholder + table
2. Procedural boxes (9 hinged tiles, numbers, open/shut pose)
3. Dice roll presentation + legal move highlight
4. Opponent legal AI (no cheats)
5. Hold tally UI + catch / wrong-Hold cost
6. Three cheat types + tiers
7. Tile-9 door open → stub load of Hidden Room (or fade stub)
8. Harness: both boxes resolve, Hold true/false, tile-9 gate only on correct condition

### Acceptance (STB)

- [ ] Full legal game completable vs AI with no cheats
- [ ] Each cheat type catchable by Hold
- [ ] Wrong Hold penalizes
- [ ] Tile-9 + tampering only opens door
- [ ] Visual gate: hall entrance, table mid, boxes close, Hold tally, door open

---

## Open design decisions (need Nick before / during Phase 2)

1. **Young-Aldric unmasked portrait** — home in Shut the Box dust-sheet row (recommended: one sheet half-fallen, only after first win in room) **or** drop. Flagged as open in cheats design §1.8.
2. **Court evidence prose** — needs writing pass (true card + decoys + one shard mislabel). Placeholder strings OK for first mechanical stub.
3. **Court entry from manor** — recommended door off Entry Hall dining wing ("the hearing is waiting") rather than jumping from menu only.

---

## Recommended overnight → morning order

1. Finish exterior entrance polish still in flight (sconce whites, door glow fade) — this file's parallel track.
2. Source / credit gavel GLB into `assets/models/sourced/` + CREDITS.
3. Wire Court + STB into `asset-catalog.html` with gap tags (Gavel GAP → OK once landed).
4. After Nick Phase 0 playtest: stub Court scene shell (empty walkable dining redress) before any HUD.

---

## What this plan deliberately does **not** do tonight

- Does not stub Court/STB HTML scenes (would tempt starting Court before Nick walks the opening).
- Does not write full evidence prose or journal fragments.
- Does not build Labyrinth (Phase 6) or six endings (Phase 7).
