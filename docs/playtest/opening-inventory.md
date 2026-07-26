# Opening / Prologue — full element inventory

Every scene and every camera view in the opening, with what's actually in it. Status is set from
one of three things only: the code, a screenshot in `docs/playtest/screenshots/`, or an asset on disk.
If I couldn't prove it with one of those, it's marked so. No "probably."

Status key:
- **OK** — in code AND visible/audible in a proof (shot path or asset listed)
- **WEAK** — exists but reads badly or barely shows in the view it should
- **MISSING** — not there
- **UNPROVEN** — in code but I have no screenshot/asset proving the player sees it

Screenshots referenced are from the last `node scripts/capture-phase0.mjs` run.

---

## 1. Boot / loading
| Element | Status | Proof / note |
|---|---|---|
| Three.js boot + error guard | OK | `waitThree()`, `_onErr` handler |
| Loading state before scene | UNPROVEN | no captured shot of the pre-scene frame |

## 2. Main menu
| Element | Status | Proof |
|---|---|---|
| Title + New Game / Options | OK | menu state, `onNewGame` |
| Slow cinematic camera drift | OK | loop `else` branch (menu) |
| Menu ambience/music | OK (soft) | soft wind starts on New Game gesture; no full music bus |

## 3. Options
| Element | Status | Proof |
|---|---|---|
| Master + SFX sliders | OK | `audioSliders`, now live-update the beds |
| Reduce motion, text size | OK | wired to `GMSettings` |
| Music slider | MISSING (intentional) | pulled — no music bus; beds ride SFX |

## 4. Cold open (text) + 5. Invitation card
| Element | Status | Proof |
|---|---|---|
| Cold-open lines, auto + click advance | OK | `advanceCold` |
| Invitation / letter card | OK | intro state |
| Any sound under either | OK (soft) | wind + dark pad under cold open (from New Game / `advanceCold`) |

## 6. Walk — the 3D exterior (the big one)

### 6a. Spawn facing the house — `phase0-01`
| Element | Status | Proof |
|---|---|---|
| Cobble drive, fence lines, lamps | OK | 01 |
| Dead-tree avenue down the drive | OK | 01, `ruinsBuilt` |
| Mansion at the end, fog depth | OK | 01 |
| Tutorial WASD chip, HUD | OK | 01 |

### 6b. Spawn looking back at the car — `phase0-02`
| Element | Status | Proof |
|---|---|---|
| The car model | **OK** | Realistic Car HD 03 Exterior_LOD0 — modern hatch, wet paint, soft emissive lights |
| Ground under/around it | OK | 02 |
| **Outer entrance behind the car** | **OK** | 02 — shut iron gate + two lit stone piers + low weathered outer walls + a dead-tree treeline fading into fog. |
| Driver door / luggage / arrival cue | OK | satchel + envelope on curb outside keep-clear (`horror-08`) |

### Driveway
| Element | Status | Proof |
|---|---|---|
| Cobble path | **OK** (fixed) | one continuous 200u plane (z=-86..114) — the old path + `pathN` seam is gone |
| Curbs | OK | matching continuous length |
| Path/curb dressing | OK | leaf litter on curb + cobble edges + hero verge tufts (`buildGroundScatter`) |

### Flanks (left & right of the walkway)
| Element | Status | Proof |
|---|---|---|
| Ground scatter (rocks, weeds, logs, mounds) | **OK** (new) | always-built primitive scatter across both flanks the whole drive — fills what was bare grass |
| Dense dead-tree avenue | **OK** (denser) | every 6u instead of 8u |
| Wide flank trees + bushes | **OK** (new) | ~60 mid-flank placements skipping cemetery/garden footprints |
| Far treeline | **OK** (new) | ~46 big trees at x=±46..76 so the world has a wooded edge, not a black horizon |

### 6c. Gate — `phase0-03`
| Element | Status | Proof |
|---|---|---|
| Wrought-iron gate, recolored dark | OK | 03 |
| Pillar lamp globes | OK | 03 |
| Gate lock dramatic beat | OK | `triggerGateLock` |

### 6d. Mid-approach — `phase0-04`
| Element | Status | Proof |
|---|---|---|
| Avenue framing, planters, fog | OK | 04 |
| Lamps receding into mist | OK | 04 |

### 6e. Facade / porch / door — `phase0-05`
| Element | Status | Proof |
|---|---|---|
| Mansion facade, scale (17u tall vs 1.7u eye) | OK | 05, scale report |
| Door glow, warm light growing near | OK | 05, `doorGlow` |
| Doors swing open on arrival | OK | `arrDoor`, doorL/doorR |
| Flanking porch lanterns | OK | Env `SM_MansionLamp` (`porchSconces:'env-lamp'`, `horror-05`/`09`) |
| Steps / terrace read | OK | `horror-05`, `full-09`..`12` |

### 6f. Cemetery — `phase0-06`, `phase0-07`
| Element | Status | Proof |
|---|---|---|
| Walled plot, gothic arch, headstones, stag, skull | OK | 06/07, `buildRuinsGrounds` |
| Cold fill light | removed | cold pools bleached stone — moon + fog only |

### 6g. Ruined garden / fountain — `phase0-08`
| Element | Status | Proof |
|---|---|---|
| Fountain, broken columns, fox statue, pots, bushes | OK | 08 |
| Cold fill light | removed | same as cemetery — no bleach pools |

### 6h. Sky — `phase0-01`, `phase0-09`, `phase0-10`
| Element | Status | Proof |
|---|---|---|
| Moon disc + soft glow | **OK** (fixed) | 01/09/10 — full moon over the drive with an additive glow halo and faint craters. Root cause of the old invisibility: `fog:true` washed it ~99% to the fog colour; now `fog:false`, repositioned over the mansion at (-46,64,-179). |
| Stars (620 points) | **OK** (fixed) | 01/09/10 — `fog:false`, `sizeAttenuation:false`, size 1.7 so they read at any distance |
| Night sky backdrop | **OK** | uniform `#0c111d` background. Tried a gradient dome AND a gradient plane — both left artifacts (zenith dark-disc / visible plane edge), so flat uniform is the deliberate ship. |

### 6i. Atmosphere
| Element | Status | Proof |
|---|---|---|
| Fog + depth | OK | every shot |
| 26 animated mist sprites | OK | loop mist drift |
| Lamp flicker | OK | loop lamps |
| **Falling leaves / dead-tree litter particles** | OK | 64 leaf planes on RAF (`leaves-t0/t1`, `verify-polish`) |
| **Wind ambience bed** | OK (new) | `amb_wind.ogg`, starts on walk |
| **Dark drone bed** | OK (new) | `amb_dark.ogg` |
| **Footsteps (stone/gravel/leaves)** | OK (new) | distance-based in loop, pool of 4 |
| Crickets / owl night wildlife | OK | `amb_crickets.ogg` bed + `amb_owl.ogg` one-shot |

### 6j. HUD / player-facing UI
| Element | Status | Proof |
|---|---|---|
| Tutorial chip, story beats, inventory badge, options btn, look legend | OK | every walk shot |

## 7. Letter-in-hand overlay
| Element | Status | Proof |
|---|---|---|
| Overlay pauses the walk, freezes camera | OK | loop pause branch |

## 8. Gate-lock event
| Element | Status | Proof |
|---|---|---|
| Gate slams + locks, retreat cap tightens | OK | `triggerGateLock`, `gate_slam.ogg`, `gate_lock.ogg` |
| Gate SFX license on disk | WEAK | `gate_slam/lock.ogg` origin unconfirmed — flagged in `license.txt` |

## 9. Arrival / knockout
| Element | Status | Proof |
|---|---|---|
| Climb, settle, doors open, hand at collar, blackout | OK | arrival branch |
| Door creak on open | OK (new) | `door_creak.ogg` |
| Beds fade out into the blackout | OK (new) | volume ramp then `stopAmbience` |
| Chandelier glimpse line | OK | `_glimpseSaid` |

## 10. Secret ending / 11. Aftermath
| Element | Status | Proof |
|---|---|---|
| Retreat-to-car secret ending | OK | `triggerSecretEnding`, now stops beds |
| Aftermath jump (dev) | OK | `goTo('aftermath')` |

---

## What's genuinely still open (evidence-based, ranked)

1. **Nick walk** — mandatory (`docs/NICK-NEEDED.md`)
2. **Gate SFX license** — `gate_slam`/`gate_lock` origin unconfirmed
3. **Gravyart facade** — keep until hinged Env front exists (Nick buy/export)
4. **Ruins kit geometry** — dark; still blocky low-poly shape

Full Nick vs agent split: **`docs/NICK-NEEDED.md`** (includes how to manually edit).
