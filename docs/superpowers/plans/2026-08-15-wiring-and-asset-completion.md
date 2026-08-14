# Wiring & asset completion plan — post-audit

Written 2026-08-15, after a full-session audit (5 parallel research passes + hands-on verification)
of everything sitting uncommitted on `wend-prologue-boundary-navmesh-harnesses`, plus wiring
`GmPauseMenu` into `GmPlayer`'s pause binding. This is the punch list that audit produced, ranked
and scoped so the next session (human or agent) can pick items up without re-deriving the context.

Five commits landed this session (`b6d3195` → `59136ed`): the corruption-ceiling closure, dead-file
cleanup, the run-state/ending/save/scene-transition core, the `GmPauseMenu` wiring + `GmHudTheme.tss`
sourcing fix, the `GmWendEstateForest` warning fix, and the guest-portrait tracking fix. Everything
below is what's left, in the state it was found.

## What's real and needs a decision, not more code

### D1 — Boot/title scene design (blocks GmSceneDirector, GmSaveSystem.Load, endings resolving)
`GmEndingManager.ResolveEnding()` is only called from `GmSceneDirector.ResolveAndShowEnding()`,
which has zero callers. `GmSceneDirector` is never instantiated anywhere (checked by GUID across
every scene/prefab). `GmSaveSystem.Load()` has zero callers anywhere. None of this is a bug in the
code itself — all three systems are real, tested, and correct — it's that nothing in the repo ever
calls them, because there's no boot/title scene to call them from. **Needs Nick:** where does a
title/boot scene fit in the current flow (does the Prologue become the "continue" target, is there
a separate menu scene, does `Application.isEditor` skip it in dev builds)? This is real feature
work — a whole new scene plus its own build-settings entry — not a wiring fix.

### D2 — GmPrologueHud vs GmPauseMenu, long-term
Two working pause systems now exist by design, not by accident: `GmPrologueHud` (bespoke,
Prologue-only, tested, shows Resume/Quit/run-state/brightness) and `GmPauseMenu` (generic 4-tab
dossier, now correctly wired but suppresses itself in any scene with a `GmPrologueHud`, so it has
nowhere to actually appear yet since the other 6 scenes have no player rig). **Needs Nick:** is this
split permanent (Prologue keeps its bespoke card, later chapters get the dossier), or should the
Prologue eventually adopt the dossier too? Not decided here — flagged in the wiring commit
(`4c7077a`) rather than guessed at.

### D3 — Corruption bridge schedule (already flagged, unchanged)
`GmRunStore.RaiseCorruption` exists, is called from Parlor/Court/Shut the Box, but WHAT raises it
and how often is still explicitly deferred to Nick per commit `cff4a35`. No change since the last
report.

## What's real, committed, and just needs someone to give the 6 scenes a player

All 6 scaffold scenes (Court, Parlor, Shut the Box, Hidden Room, Labyrinth, Entry Hall) have real,
tested gameplay controllers — Court's evidence trial, Parlor's trick-taking card game with a
cheating-AI opponent, Shut the Box's dice/tile rules, Hidden Room's shard mechanic with re-entry
safety, Labyrinth's chase AI. **None of the six have a `GmPlayer`/camera rig placed in them at all**
— that's why none of their controllers are reachable from input. This is the single biggest lever
for "get all things online": placing a player rig (however wend-hill-prologue's `GmWendBuilder`
does it) into each of the six scaffold builders is what would make `GmPauseMenu` actually visible
somewhere, and what would make every one of those five real mechanics playable for the first time.
Not attempted in this pass — it's a per-scene builder change with real design implications (where
does the player spawn, what does "leaving" the room do), not a mechanical fix.

## Asset gaps that were just filing, not sourcing (done 2026-08-15)

`assets/sfx/`'s 18 audio files were all already fully documented in the sibling `license.txt` —
tracked as-is, `node scripts/test-attribution.mjs` still passes clean. Committed in `c0d742d`.

## Corrected: the ~54 footstep/wind clips are NOT a filing fix — provenance unknown

Original framing here was wrong. Investigated before touching anything: `foot_dirt_01..08`,
`foot_grass_*`, `foot_gravel_*`, `foot_leaves_*`, `foot_stone_*`, `foot_wood_*`, `wind_local_01..04`,
`amb_wind_natural`, `amb_wind_hybrid` exist **only** inside `unity-project/Resources/Sfx/` — searched
every vendor pack under `assets/models/unity/` and `assets/models/unity-import/` (including the
`unity-learn-foundations-of-real-time-audio-urp` pack, the obvious first guess given the filename
style) and found zero matches anywhere. `docs/WEND-NIGHT-LADDER.md` confirms they're live and wired
("40 footstep clip(s)... wired") but says nothing about where they came from. No `.meta` file, no
doc, no script anywhere names a source or license for these ~54 files.

This is not a "copy it over" fix — it's the same class of problem the attribution gate exists to
catch, and fabricating a license entry to make it look filed would be worse than leaving it
uncommitted. **Needs Nick**: does he know where these came from (a specific purchase, a personal
recording session, an AI tool)? Until then these stay uncommitted rather than guessed at.

## Tooling gaps that are just filing, not sourcing (do these next, cheap and safe)

| Asset | Where it actually is | Fix |
|---|---|---|
| `scripts/sync-unity-scenes.mjs` | Untracked, never committed on this branch at all | First-ever commit of the project's core sync tool — needs its own dedicated review pass, not a drive-by. Also carries my small `GmHudTheme.tss` ownership-check addition, currently sitting in the working tree unattached to any commit. |
| `scripts/sync-victorian-proof-assets.mjs` | Untracked, never committed | Same as above — the tool that gets the portrait/vendor assets into Unity has itself never been in git. |

## Real gaps that need sourcing (not purchases — these are pieces of already-owned packs or CC0 kit)

| Asset | Status | Note |
|---|---|---|
| `Cobweb_02.fbx`, `Cobweb_03.fbx` (Hidden Room dressing) | Exist only in `unity-project/Assets/GamesMaster/Props/`, no source anywhere in `assets/` | Same class of fix as the portraits — find them, file them into `assets/models/`, track, re-point the builder if the path changes. |

### `SM_Wood_02.prefab` / `SM_Bucket.prefab` — solved, not a sourcing gap (2026-08-15)

Confirmed NOT missing from the owned pack: both sit in the raw import at
`assets/models/unity-import/leartes-abandoned-village/LeartesStudios/Abandoned Village/HDRP/Art/Prefabs/`
— the exact relative path `GmWendEstateForest.cs`'s constants expect, alongside 217 sibling prefabs
from the same pack that already work correctly in the live Unity project. This is an incomplete
import of 2 files out of an otherwise-complete 219-prefab pack, not a missing asset.

**Not fixed here on purpose**: copying `.prefab`/`.meta` files by hand risks wrong GUIDs and broken
mesh/material references that only Unity's own AssetDatabase import resolves correctly — exactly
the "never hand-edit serialized Unity assets, changes go through Editor scripts using the Unity API"
rule this project's CLAUDE.md states directly. The actual fix is trivial in the Editor itself: open
the project, re-import (or drag in) those 2 specific files from the source pack the same way the
other 217 already got there. Left for whoever next has Unity open.
| ~54 footstep/wind clips | Exist only in `unity-project/Resources/Sfx/`, zero trace anywhere else on disk | **Needs Nick** — provenance genuinely unknown, not a filing task (see above). |

## Ranked punch list

1. **D1/D2 answers from Nick** — everything downstream of "no ending can resolve" waits on this.
2. **Give the 6 scaffold scenes a player rig** — highest-value single change; unlocks 5 real,
   already-tested mechanics plus makes `GmPauseMenu` visible somewhere.
3. ~~File the sfx clips + `assets/sfx/` tracking~~ — done (`c0d742d`). The ~54 footstep/wind
   clips turned out to need Nick's input on provenance, not filing — see above.
4. **Commit `sync-unity-scenes.mjs` and `sync-victorian-proof-assets.mjs`** — foundational tooling
   gap; do this as its own reviewed pass, not folded into a feature commit.
5. **Cobweb FBX + the 2 missing Leartes prefabs** — small, bounded, check the owned pack first.
6. **The 14 ambiguous `capture-*.mjs` scripts** (from the earlier dead-file sweep) — fix-in-place or
   retire, still needs a call from whoever owns the 2026-08-13 audit's follow-through.

## What this session did NOT touch, on purpose

`GmAudioManager.cs`, `GmCreditsUI.cs`, `GmGameHud.cs` — all real, reviewed, and `GmAudioManager`'s
`GmHudTheme.tss` blocker is now resolved, but `GmCreditsUI` depends on `GmCreditsCatalog.cs`, which
is itself uncommitted and unreviewed. Committing `GmAudioManager` alone was in scope but held back
to keep this session's commits scoped to what was actually asked (the pause menu); it's ready
whenever the next pass picks it up.
