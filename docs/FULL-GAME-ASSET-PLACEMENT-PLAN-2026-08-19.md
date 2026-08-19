# The Games Master: Full-Game Asset Placement Plan

Date: 2026-08-19  
Scope: the complete playable route, from the road to every ending  
Authority: this replaces the stale purchase and status assumptions in the 2026-07-14 asset book

## Current production truth

The project does not have a general asset shortage. It has a selection and deployment problem.
The local vault contains 117 Unity packs, about 7,100 prefabs, 5,700 FBXs, 7,300 PNGs, and nearly
2,000 audio files. The imported Unity project already contains the core Victorian furniture, the
estate exterior, the modern car, fog and fire effects, cobwebs, portraits, and the game-specific
procedural props. The three packs previously marked "buy now" are owned, imported, and in use.

The remaining genuine source gaps are narrow:

- final production Aldric body, face, hands, clothing, and animation set
- a hand-authored or properly licensed final gavel mesh if the current procedural gavel fails close-up
- optional sculpted bone dice if the current readable dice fail close-up
- final ending-specific hero art and Steam-facing key art

Everything else in this plan should be mined from owned material before another purchase.

## Status legend

| Mark | Meaning |
|---|---|
| `SHIP` | Present in the game and suitable for the current build |
| `WIRE` | Owned and imported, but must be placed or connected |
| `CURATE` | Several owned candidates exist; choose through a visual proof |
| `BUILD` | Project-authored geometry, material, VFX, UI, or code is the right answer |
| `SOURCE` | A focused external acquisition is still justified |
| `HUMAN` | Requires Nick's taste, performance, or final casting decision after an in-game comparison |

## Asset source palette

Use a tight palette so the house looks authored instead of like an Asset Store aisle.

| Family | Primary use | Rule |
|---|---|---|
| MetalMan Victorian Interiors | tables, chairs, shelves, doors, mirror | Main guest-floor furniture language |
| Modular Victorian Interior Mansion | walls, trim, sconces, chandeliers | Main guest-floor architectural language |
| Victorian Mansion Environment | exterior shell and facade support | Exterior only unless a matching piece is proven in context |
| Leartes Witch Village HDRP | dust, candle flame, smoke, fire, cellar clutter | VFX and rough service-space detail, never whole-room architecture upstairs |
| Abandoned Asylum | damaged mirror and restrained decay | Hidden room and basement accents only |
| Flooded Grounds / RPG Dungeon | stone, drains, rubble, crypt pieces | Labyrinth and deep basement only |
| GamesMaster authored assets | portraits, cobwebs, armor, evidence, shards | Story-critical and interaction-critical objects |

Never mix more than two architectural families in one visible room. Small props may cross families
after their materials are normalized. Rebuild through the scene builders. Do not edit scene or
prefab YAML by hand.

## Placement map

### Boot, title, settings, credits

| Need | Status | Source and placement | Implementation notes |
|---|---|---|---|
| Title silhouette | `BUILD` | Cropped estate facade behind title | One dark value mass, no generic foggy forest splash |
| Background motion | `BUILD` | Slow fog sheet and one distant window flicker | Disable with reduced motion |
| Menu audio | `CURATE` | Existing `amb_dark` at low level | Start only after user input where platform policy requires it |
| Credits | `SHIP` | Existing project credit system plus `assets/sfx/license.txt` | Attribution gate remains mandatory |

### Prologue: road, car, avenue, gate, cemetery, courtyard, porch

| Zone | Status | Exact asset direction | Placement and behavior |
|---|---|---|---|
| Road and arrival | `SHIP` | Realistic Car HD 03, authored road/terrain | Car remains the only modern object. Wet sheen belongs under headlights, not across the whole estate |
| Avenue canopy | `CURATE` | Existing estate tree placements plus owned vegetation | Three depth bands: trunk wall near route, canopy bridge overhead, sparse distant silhouette. Preserve 4.2m route clearance |
| Ground detail | `WIRE` | Witch Village/Flooded Grounds stones, twigs, shrubs | Cluster at bends and drainage edges. Do not salt-and-pepper scatter the whole 435m route |
| Fog | `SHIP` | Existing HDRP night volume | Keep path readable at navigation height. Fog sheets may sit in low hollows only |
| Lamps | `SHIP` | Existing period post lamps | Warm islands with dark travel between them. No synchronized flicker |
| Gate | `SHIP` | `Assets/GamesMaster/Props/graveyard_gate.fbx` plus authored fence | Hinges and lock get localized audio. Keep the center passage physically clear |
| Cemetery | `WIRE` | Owned graves, cross forms, rubble, dead growth | Dense on the outer edge, one open-grave story cluster, no collision snags beside the critical path |
| Courtyard | `WIRE` | Witch Village barrels, crates, bucket, bottles, lanterns | Put use-worn clusters against walls and outbuilding doors. Leave turning and camera sightlines clean |
| Porch | `SHIP` | Victorian Mansion facade plus authored front doors | Candle or lantern practical at each readable threshold. The front door still never opens on foot |
| Ninth Bell | `SHIP` | Curated bell, heartbeat, whisper, ear-whine cues | Audio owns the transition. VFX supports it and does not replace it |

### Wake room and Entry Hall

| Zone | Status | Exact asset direction | Placement and behavior |
|---|---|---|---|
| Wake room corners | `WIRE` | `Assets/LeartesStudios/WitchVillage/HDRP/Art/Particles/P_Dust.prefab`, `Assets/GamesMaster/Props/Cobweb_02.fbx`, `Cobweb_03.fbx` | One dust volume in the lamp beam, webs only in two ceiling corners and one furniture-wall junction |
| Hall furniture | `SHIP` | MetalMan shelves, tables, chairs, armor | Maintain a strong center axis from wake threshold to parlor doors |
| Nine portraits | `SHIP` | GamesMaster portrait images and authored frames | Portrait lights are stable gameplay signals, never flicker |
| Hall practicals | `SHIP` | Existing sconces/chandelier candidates | Named flame practicals use seeded `GmLightFlicker`; door, evidence, and navigation lights stay fixed |
| Time-layer scars | `BUILD` | Brass room number, filled arch, carpet-edge mud print | Put scars at eye or hand height and keep them sparse enough to read as clues |
| Attic access | `WIRE` | Rough crates, sheets, books, clock, cobwebs | Denser dust and cooler fill than the hall. Preserve ladder top and shard route clearance |
| Basement stair | `WIRE` | Chains, bottle debris, bucket, damp stone trim | Darkness should hide depth, not the next safe foot placement |
| Mirror shard #2 | `SHIP` | Court final seal rig | The retired attic duplicate must not return |

### Parlor: Flames

| Need | Status | Source and placement | Implementation notes |
|---|---|---|---|
| Table and chairs | `SHIP` | MetalMan table and chair family | Aldric's chair owns the far side, player approach remains open |
| Card handling | `SHIP` | Existing card presentation plus `parlor-card-snap.ogg` | New CC0 cue is 0.30s and wired to `PlayCardSnap()` |
| Fire | `WIRE` | `P_Fire.prefab`, `P_Candle_Flame.prefab`, restrained smoke | Fireplace is the dominant warm source. Candles support silhouettes, not flat room fill |
| Corners | `WIRE` | Dust prefab, one book stack, one covered frame | Keep detail out of the card/evidence read cone |
| Aldric | `SOURCE` | Final period host character | Prototype silhouette remains until one licensed candidate passes close, mid, and seated proofs |

### Court

| Need | Status | Source and placement | Implementation notes |
|---|---|---|---|
| Bench, witness rail, jury | `SHIP` | Existing authored courtroom and nine portrait jury | Keep the old bar-rail scar in the witness geometry |
| Gavel | `SHIP` / `HUMAN` | Current tarnished procedural gavel | It is mechanically complete. Replace only if a licensed mesh wins a close-up comparison |
| Gavel audio | `SHIP` | `court-gavel-strike.ogg`, CC0 wood hammer source | New 0.32s cue is wired to `PlayGavelStrike()` |
| Evidence and seals | `SHIP` | Five evidence cards, wax seals, reactive final-seal rig | Evidence light is stable. Final seal owns the dramatic pulse |
| Atmosphere | `WIRE` | Dust in side shafts, candle flames at bench, low smoke only near practicals | Never put opaque particles between the player and evidence text |
| Floor history | `BUILD` | Raised-end join and worn witness approach | Material variation, not a trip hazard or collider ridge |

### Shut the Box

| Need | Status | Source and placement | Implementation notes |
|---|---|---|---|
| Board, tiles, hinges | `SHIP` | Existing deterministic authored board | Gameplay geometry remains authored because labels, pivots, and colliders must match the rules |
| Dice | `SHIP` / `HUMAN` | Current authored readable dice, local `assets/models/sourced/dice.glb` as optional comparison | Do not trade correct face readability and deterministic scale for a prettier mesh |
| Dice audio | `SHIP` | `stb-bone-dice-roll.ogg`, CC0 wooden die recording | New 1.37s cue is wired to `PlayDiceRoll()` |
| Covered-room dressing | `WIRE` | Sheet forms, clock, frames, sparse cobwebs and dust | Build two large silhouettes, not twenty tiny covered props |
| Secret panel | `SHIP` | Existing authored panel and brass seam | Keep the seam readable only after the game state allows it |

### Hidden room

| Need | Status | Source and placement | Implementation notes |
|---|---|---|---|
| Desk cluster | `WIRE` | MetalMan table/desk candidate, books, scrolls, bottle, wax inkpot | Center at composition zone `(0, 0, 2)`. Invitation and journal remain interaction-first |
| Mirror cluster | `WIRE` | MetalMan `Mirror_1.fbx` or Abandoned Asylum damaged mirror | Center at `(-2.8, 0, 0)`. Use one hero mirror and two small support elements |
| Shelf cluster | `WIRE` | `BookShelf_1.fbx`, books, bottles, small box | Center at `(2.8, 0, 0)`. Uneven fill, with one deliberately empty shelf |
| Recess | `BUILD` | Wrong masonry, wax traces, shard response | Center at `(0, 0, -2.5)`. No generic skeleton jump scare |
| Dust | `WIRE` | One low-rate dust field in the desk light | Keep particle bounds inside the 6m room |

### Basement and Labyrinth

| Zone | Status | Exact asset direction | Placement and behavior |
|---|---|---|---|
| Basement threshold | `WIRE` | Witch Village chains, barrels, bucket, bottles and damp clutter | Build one believable former service cluster beside the route, not across it |
| Masonry transition | `CURATE` | Flooded Grounds or RPG Dungeon wall/crypt set | Material bridge from manor stone to impossible underside over 6 to 10m |
| Entrance | `WIRE` | Rubble, drain, paired sconces | Composition center `(-15, 0, -15)`. Teach the wall language before the first turn |
| Central shrine | `WIRE` | Crypt pieces, damaged mirror, candle/fire VFX | Composition center `(0, 0, 0)`. This is the one dense subterranean hero cluster |
| Stalk lane | `BUILD` | Sparse shadow occluders plus positional Huntsman audio | Composition center `(5, 0, -5)`. Suggest motion with occlusion and sound, not a cheap monster prefab |
| Exit | `WIRE` | Cleaner stone, gate/arch candidate, final practical | Composition center `(15, 0, 15)`. Visual relief without becoming bright or safe |
| Path safety | `SHIP` contract | Existing 36m layout and navigation proofs | No decorative collider may narrow a route below the established clearance |

### Endings and collection

| Need | Status | Source and placement | Implementation notes |
|---|---|---|---|
| Ending variants | `BUILD` | Recompose existing house, car, portraits, mirrors, and light states | Each ending changes one dominant image and one sound bed, not an unrelated asset set |
| Drive-away | `SHIP` asset | Realistic Car HD 03 | Reuse the arrival car so the escape image pays off the opening |
| Tenth tile collection | `BUILD` | Authored object and collection UI | Must match the game's physical brass/wax language |
| Young Aldric | `SOURCE` / `HUMAN` | Portrait or final character-derived render | Hold until character casting is settled so the face stays canonical |

## Cross-game atmosphere rules

### Dust and webs

- Guest rooms: one localized dust field per room, 10 to 25 particles visible at once.
- Attic and basement: two localized fields maximum, with lower speed and slightly larger motes.
- Webs live at structural junctions. Never place them in free air or evenly in all four corners.
- Dust is disabled or halved in low-power and reduced-motion profiles.
- Particle bounds must be explicit so off-room particles cull.

### Flicker and fire

- Existing `GmLightFlicker` owns seeded runtime variation for named flame practicals.
- Portrait, evidence, exit, mirror, and navigation lights never flicker.
- Every visible candle flame must correspond to a light or clearly read as an unlit prop.
- Flicker varies intensity softly. It does not switch a whole hallway on and off.

### Audio

- Table-game cues are dry and local: card contact, wooden dice, wooden gavel.
- Room ambience is continuous and quiet enough that interaction cues remain legible.
- Basement sound gets wetter and more directional as architecture becomes less domestic.
- One-shots use spatial placement where the source exists in the world. UI confirmation stays non-spatial.

### Collision and performance

- Decorative clutter defaults to no collider. Add simple proxy colliders only for objects that block the player visually.
- Story objects get dedicated interaction colliders, never mesh colliders by accident.
- Interiors target two particle systems and six shadow-casting local lights or fewer in the default view.
- Large imported prefabs must pass a material, missing-script, renderer-count, and collider audit before placement.
- Use LODs on exterior vegetation and ruins. Small interior props should cull by room or portal, not carry elaborate LOD chains.

## Acquisition decisions

### Acquired in this pass

| Asset | License | Use |
|---|---|---|
| BMacZero Playing Card Sounds, `contact2.wav` | CC0 | Parlor card contact |
| Wuzzy Wooden Dice on Wooden Table, take 3 | CC0 | Shut the Box roll |
| rubberduck 100 CC0 Metal and Wood SFX, `wood_hammer_01.ogg` | CC0 | Court gavel strike |

The transformed shipping files and hashes are recorded in `assets/sfx/license.txt`.

### Do not acquire yet

- No new environment mega-pack. Owned coverage is already excessive.
- No generic horror monster. The Huntsman works better as authored absence, occlusion, and sound.
- No new mansion shell. The owned facade and modular interior are already the project language.
- No gavel or dice purchase until the current props fail an actual close-up proof.

### Human-only decisions, moved to proof gates

Nick does not need to build rooms or place props by hand. Human input is limited to choosing between
rendered finalists:

1. Aldric casting: choose one of at most three licensed in-game character proofs.
2. Gavel close-up: keep procedural or select one licensed candidate after matching material and scale.
3. Dice close-up: keep authored readable dice or use the local sourced model if face readability survives.
4. Ending hero frame and key art: choose from finished compositions, not asset-store thumbnails.

Until those comparisons exist, the game continues with the current functional assets. None of these
choices blocks Court mechanics, full-game builds, or environment dressing.

## Execution order and progress

Progress after the first integration checkpoint: **45%**. Inventory and source audit are complete,
the three missing gameplay sounds are acquired and wired, bounded dust is live in every interior, and
the remaining work is deeper prop placement, character sourcing, ending art, and final proof.

- [x] Inventory owned packs and Unity imports
- [x] Search local Omnivore catalog for additional holdings
- [x] Correct stale buy-list and car status
- [x] Acquire and license card, dice, and gavel cues
- [x] Define room-by-room placement and asset-family rules
- [x] Add owned dust to interior builders with bounded placement and a checked-scene runtime fallback
- [ ] Curate exact attic, basement, hidden-room, and labyrinth prefab finalists from contact sheets
- [ ] Run candidate validation for missing scripts, materials, colliders, and renderer counts
- [ ] Rebuild affected generated scenes through Unity batchmode
- [ ] Capture wake room, hall, parlor, Court, Shut the Box, hidden room, basement, and labyrinth tours
- [ ] Verify no navigation regressions or evidence/UI occlusion
- [ ] Source and compare final Aldric candidates
- [ ] Produce ending hero-frame and key-art candidate proofs
- [ ] Run complete tests, attribution gate, macOS build, commit, and push

## Definition of done

This asset pass is complete only when every playable scene has a current visual tour, every imported
third-party item has durable provenance, the full route remains traversable, all gameplay signals stay
readable, performance evidence passes, and the macOS player builds from a clean checkout. A pack being
owned or a prefab merely existing in the project does not count as integration.
