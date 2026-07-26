# Horror Environments Bundle — selective intake

Purchased by Nick for $50 on 2026-07-15. The bundle is a source library, not permission to replace the mansion shell or ship nine complete scenes.

## Download status

The cached `Horror Environments Bundle 9 Packs.unitypackage` is a 5.4 KB entitlement/lite package. Its included readme says each environment must be downloaded from its own Package Content link.

Run:

```bash
npm run assets:horror:check
```

Historical payload status: **9/9 downloaded** during intake. Current storage no longer contains a
retained Asset Store package cache, so `npm run assets:horror:check` cannot reproduce that result.
It now reports the three packs provably imported in `GamesMaster-Unity` and exits nonzero rather than
claiming missing source packages are present.

In Unity Package Manager → My Assets, download these individually. Download only; importing every pack into one Unity project is unnecessary.

- Haunted Village Environment / Horror Village Environment
- Haunted Prison Environment
- Demonic Village Environment
- The Aftermath Environment
- Historical Museum
- Abandoned Horror Mansion Interior
- Witch Village Environment
- Sorcerer's Hut
- Abandoned Village Environment

## First-pass room map

| Game area | Primary packs to mine | Candidate asset classes |
|-----------|-----------------------|-------------------------|
| Opening grounds | Witch Village; Haunted Village; Abandoned Village; Aftermath | dead trees, willow/cypress, grass, branches, ground, walls, carts, lanterns, ruined clutter |
| Porch / Entry Hall | Abandoned Horror Mansion Interior; Historical Museum | trim, lamps, furniture, paintings, frames, display pieces, decay decals |
| Parlor | Mansion Interior; Museum; Sorcerer's Hut | chairs, tables, shelves, books, bottles, small narrative props |
| Court | Historical Museum; Haunted Prison; Mansion Interior | rails, benches, display cases, lights, ironwork, institutional furniture |
| Shut the Box | Mansion Interior; Sorcerer's Hut; Haunted Prison | table dressing, stools/chairs, club scars, chains, room-number and notice framing |
| Hidden room | Sorcerer's Hut; Demonic Village; Haunted Prison | ritual objects, journals, candles, restraints, occult fragments |
| Labyrinth | Haunted Prison; Demonic Village; Aftermath | modular stone/brick, iron doors, grilles, drains, ruin fragments |
| Endings | All, selectively | alternate decay states, hero props, silhouette and lighting variants |

This map is provisional until the real filenames, materials, LODs and bounds are inventoried.

## Intake gates

1. Extract each package outside the served game tree.
2. Inventory meshes, textures, prefabs, LODs and material dependencies.
3. Shortlist by room and reject duplicates/style mismatches.
4. Convert only shortlisted FBX files to GLB.
5. Prefer a suitable authored LOD over brute-force high-poly exports.
6. Downscale 4K textures to 1K/2K as framing permits; combine maps only when visually safe.
7. Add a small number of hero assets first and keep existing cheap fill at fog distance.
8. Run live walk, screenshot, performance and error gates before expanding usage.

Current selective Unity imports: Haunted Village, Witch Village and Abandoned Village. The accepted
opening uses their selected dead-willow, grave, wall, vegetation, cart, trellis and story-prop assets;
the full demo scenes remain unserved. Future rooms should re-download only the owned payload they
need from Package Manager > My Assets.

## Locks

- Existing mansion facade stays; no second mansion shell.
- Threshold Refusal remains closed-door through knockout.
- Do not serve Unity demo scenes or whole package directories.
- No further asset purchase until this bundle is mined and a specific gap survives testing.
