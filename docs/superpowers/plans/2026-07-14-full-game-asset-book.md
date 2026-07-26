# Full-Game Asset Book — Modern Guest / Trapped House

> **For agentic workers:** sourcing + placement bible, not a coding sprint.
>
> **House history canon:** `docs/superpowers/specs/2026-07-14-house-history.md` — coaching inn → club → failed manor → frozen games house. **Wild West saloon is retired.**

**Goal:** Finished-game asset plan. Player = now / few years ago. House face = stuck in host-era invitation dress. Older site lives show as scars + host slips.

**Locks:** Story bible (Aldric guest→host, Court / STB / Labyrinth / hidden room / six endings). This doc adds time-layering + what to buy/source/tweak.

---

## 1. Timeline frame

| Who / what | When it “lives” | Feel |
|------------|-----------------|------|
| **Guest (you)** | ~2018–2026 | Modern car, outside mud, language of now |
| **House face** | Frozen late Victorian / Edwardian invitation house | Oil lamps, portraits, ledger — no working modern outlets lore |
| **Host memory** | Centuries in the chair; slips to older place-names | “Bar,” “tap,” “member,” “the desk” under Corruption |
| **Site history** | See house-history spec | Inn → club → private try → games house |

**Car:** Quiet used modern sedan/hatch (~2010–2020). Kill Model T. Clash with lamps is the premise.

---

## 2. Time layers (tags)

| Tag | Meaning | Examples |
|-----|---------|----------|
| `L0_modern` | Guest arrival only | Car, keys, mud print, contemporary invitation stock |
| `L1_gameshouse` | Current invitation dress | MetalMan Victorian, ledger, portraits, parlor, oil lamps |
| `L2_manor` | Failed private family try | Wallpaper scars, filled arch, servant-bell stub, sealed chamber |
| `L3_club` | Private members / numbered rooms | Brass room numbers, lobby desk footprint, evening-card notice fragment |
| `L4_coaching_inn` | Oldest readable public life | Taproom bar-rail scars, pewter rings, hitching/coach-lamp porch scar, public-room floor join / raised end |
| `L5_underside` | Labyrinth | Wrong masonry, drains, not on guest floorplan |

A stool from L4 in Entry Hall is a **clue**, not decorative noise.

---

## 3. Full-game asset matrix

**HAVE** = wired · **OWN** = Unity GLB ready · **BUY** = purchase · **PROC** = code/build · **OPEN** = undecided · **WRITE** = prose/VO

### A. Prologue

| Asset | Layer | Status | Notes |
|-------|-------|--------|-------|
| Modern night car | L0 | **LATER** | Model T OK for now. Optional HD 03 after house. |
| Tire / wet road polish | L0 | PROC | Optional |
| Invitation letter | L0 | HAVE | Three-stage doubt |
| Outer gate + fence | L1 | HAVE | Procedural gate + plaggy fence |
| Drive / ruins grounds | L1 | HAVE | ruins_pack |
| Mansion shell | L1 | HAVE / optional BUY | gravyart; Victorian Mansion Env ~$80 if porch still loses |
| Front doors | L1 | PROC painted | No Door_Double on facade |
| Closed-door KO beat | — | HAVE | |
| Hitching / coach-lamp scar | L4 | PROC | Porch earth pack |
| Crickets / wind / owl / steps | — | HAVE | |
| Key / phone examine | L0 | OPEN / PROC | Optional |

### B. Entry Hall

| Asset | Layer | Status | Notes |
|-------|-------|--------|-------|
| Hall shell / MetalMan dress | L1 | HAVE | |
| 9 portraits + frames | L1 | HAVE | |
| Ledger + nameplate wear | L1 | HAVE | |
| Plants / clock / shelves | L1 | OWN/wired | |
| Parlor doors | L1 | Door_Double OK interior | |
| Wallpaper → brass “3” | L3 | PROC / decal | Club number |
| Filled arch / different brick | L2/L4 | OWN alchemist / flooded OR PROC | |
| Mirror shard (Percival) | L1 | PROC | Phase 3 |
| Mud print at carpet edge | L0 | PROC | Optional |

### C. Parlor

| Asset | Layer | Status | Notes |
|-------|-------|--------|-------|
| Card table / chairs / UI | L1 | HAVE | |
| Host seat dress | L1 | HAVE | |
| Era-slip lines | — | WRITE | “back room,” not saloon |
| Stage/floor scratch under rug | L4 | PROC | Optional examine |

### D. Court

| Asset | Layer | Status | Notes |
|-------|-------|--------|-------|
| Table / chairs / sconces | L1 | HAVE / MetalMan | |
| Gavel | L1 | PROC | |
| Evidence / wax seals | L1 | PROC + UI | |
| Jury ×9 portraits | L1 | HAVE | |
| Witness rail = old **bar rail** | L4 | PROC / OWN tavern props | Inn scar |
| Floor join (raised public end) | L4 | PROC | |
| Role lights / tarnish | — | CODE | |

### E. Shut the Box

| Asset | Layer | Status | Notes |
|-------|-------|--------|-------|
| Dice / hinged tiles / table | L1 | HAVE + PROC | |
| Dust-sheet frames | L1 | PROC | |
| Seated Aldric silhouette | L1 | PROC first | Full body OPEN |
| Hidden panel door | L1→L2 | PROC + wall kit | |
| Brass room dig under sheet | L3 | PROC | |
| Club evening-notice fragment | L3 | PROC / 2D | Not a beer-hall poster |
| Tile→ledger map | — | CODE | |

### F. Hidden room

| Asset | Layer | Status | Notes |
|-------|-------|--------|-------|
| Small chamber | L2 | PROC / modular | |
| Older invitation | L1 guest-era | 2D | Distinct from player letter |
| Journal fragments | L2 | dark_book + UI | Never names the inn |
| Desk / chair | L1/L2 | HAVE / OWN | |
| Cork / bottle ring | L4 | BUY/OWN small | Unmarked |

### G. Labyrinth

| Asset | Layer | Status | Notes |
|-------|-------|--------|-------|
| Modular underside | L5 | OWN flooded/asylum + PROC | |
| Mirrors | L5 | HAVE + PROC | |
| Shards ×2 | L5 | PROC | |
| Huntsman suggestion | L5 | PROC shadow/SFX | No full character buy required |

### H. Persistence / endings

| Asset | Layer | Status | Notes |
|-------|-------|--------|-------|
| Continue / endings UI | L0 chrome OK | CODE | |
| Collection tenth tile | L1 | PROC | |
| Secret ending drive-away | L0 | Needs modern car | |
| Young-Aldric portrait | L1 | OPEN | Story bible |

### I. Host

| Asset | Status | Notes |
|-------|--------|-------|
| Full rigged body | OPEN / defer | Silhouette + hands first |
| Hands (dice/cards/gavel) | PROC | Priority |
| Wardrobe | L1 period | Never modern clothes |

---

## 4. Buy / source — **BUY NOW trio (~$145)**

Get it out of the way in one checkout (Unity Asset Store):

| # | Item | Est. | Link |
|---|------|------|------|
| 1 | **Modular Victorian Interior** | **$35** | [Store](https://assetstore.unity.com/packages/3d/environments/modular-victorian-interior-mansion-167750) |
| 2 | **Victorian Mansion Environment** | **$79.99** | [Store](https://assetstore.unity.com/packages/3d/environments/victorian-mansion-environment-269740) — **door-mesh trailer gate** |
| 3 | **Realistic Car HD 03** | **$30** | [Store](https://assetstore.unity.com/packages/3d/vehicles/land/realistic-car-hd-03-113200) (alt HD 02 $45) |

**Total ~$145** (or ~$115 if Env fails door check — still buy Modular + car).

Skip: KitBash, Western megas, Synty, Mystery Room overlapping Modular.

**Wire order:** convert all → Modular rooms → Env facade → HD 03 spawn/secret ending.

---

## 5. Clue list (matches house history)

See house-history §Environmental clues. Writing owns VO; this book owns meshes/decals.

---

## 6. Nick tweakability

### Placement overrides

```
assets/placements/
  prologue.json
  entry-hall.json
  court.json
  shut-the-box.json
  hidden-room.json
  labyrinth.json
```

```json
{
  "id": "frontDoorLeft",
  "src": "procedural:doorLeaf",
  "layer": "L1_gameshouse",
  "pos": [0, 2.9, -52.1],
  "rot": [0, 0, 0],
  "scale": [1, 1, 1],
  "visible": true,
  "notes": "flush to porch cut"
}
```

### Harness tool

`?place=1` — click mesh, nudge/yaw/height, **S** writes JSON. Layer filter hides L4 while dressing L1.

Blender/Unity only for new mesh authoring — not daily 2cm porch nudges.

---

## 7. Delivery order

1. Modern car + spawn dress  
2. Placement JSON + Prologue `?place=1`  
3. Shell: keep gravyart vs buy P1 (door mesh check)  
4. L4/L3 scar props (Court rail, brass #, porch hitch scar)  
5. Court / STB full dress  
6. Hidden room  
7. Labyrinth L5  
8. Endings / collection / young-Aldric decision  

---

## 8. Open for Nick

- [ ] Car vibe: hatch vs older sedan  
- [ ] Clue density: 3 quiet scars vs denser ghost building  
- [ ] Phone UI at all?  
- [ ] Young-Aldric portrait  
- [ ] Buy $80 shell now vs tweak gravyart first  

---

## 9. Checkpoints

- [x] House history written (inn/club/manor — saloon retired)  
- [ ] Timeline agreed with Nick walk  
- [ ] Modern car sourced + wired  
- [ ] `assets/placements/` + tweak tool  
- [ ] Matrix statuses truth-checked in catalog  
- [ ] ≥5 layered clues readable without lore dump  

---

*Related:* `docs/superpowers/specs/2026-07-14-house-history.md`, `docs/superpowers/specs/2026-07-12-story-bible-final.md`, `asset-catalog.html`, `assets/models/unity/OWNED-PACKS.md`.
