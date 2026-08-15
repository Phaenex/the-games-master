---
name: unity-scene-master
description: Master workflow for compiling, synchronizing, rebuilding, and autonomously proving Unity HDRP scenes in The Games Master
---

# Unity Scene Master Skill

Use this skill when making scene modifications, rebuilding C# scene generators, building standalone players, or executing automated walk and house proofs.

## Asset-First Prop Placement (MANDATORY)

Before placing ANY visible prop in a scene builder:

1. **Search available asset packs** for a suitable 3D mesh:
   - `Assets/ThirdParty/MetalManVictorianInteriors/` — Victorian furniture (chairs, tables, bookshelves, lamps, mirrors, mantels, doors, couches, carpets, stairs)
   - `Assets/LeartesStudios/WitchVillage/HDRP/Art/Meshes/` — Gothic/rustic props (candles, desks, fireplaces, cabinets, bottles, books, scrolls, stools, shelves, lanterns, brasiers)
   - `Assets/LeartesStudios/HauntedVillage/Art/` — Village props (benches, chains, doors, handles, carts, buckets, churches)
   - `Assets/GamesMaster/Props/` — Custom project props (bed, clock, cobwebs, car, gate, armor)

2. **Load the mesh** using `AssetDatabase.LoadAssetAtPath<GameObject>(path)` and `PrefabUtility.InstantiatePrefab()` or `Object.Instantiate()`.

3. **Apply PBR material** from the same pack's Materials folder, or create an HDRP/Lit material with the pack's Albedo + Normal textures.

4. **ONLY use `CreatePrimitive`** for:
   - Invisible collision volumes and trigger zones
   - Architectural room shells (floor/wall/ceiling slabs) that will be fully covered by wallpaper/floor PBR materials
   - Temporary placeholders that are **explicitly commented as `// PLACEHOLDER — needs real mesh`** in code

5. **Flag gaps**: If no suitable mesh exists in the project, add a code comment `// ASSET-NEEDED: <description>` and report it to the user as a required acquisition.

## Rebuild & Verification Sequence

```bash
# 1. Sync authored repo files to local unity-project
npm run unity:scene:sync

# 2. Rebuild the scene in Unity batchmode
node scripts/unity-cli.mjs rebuild wend-hill-prologue

# 3. Compile the standalone macOS player
npm run unity:build:mac

# 4. Execute the standalone 435m walk proof (checks stalls, fps, 30 milestone screenshots)
npm run unity:proof:walk

# 5. Execute the indoor house proof (wake room, entry hall, card table)
node scripts/unity-cli.mjs house-proof

# 6. Run visual review tour
node scripts/unity-cli.mjs tour wend-hill-prologue

# 7. Run full portable test suite
npm run test:all
```

## Critical Checks
- **Stall Count**: Must be exactly 0 stalls on `unity:proof:walk`.
- **Active Renderers**: Outdoor estate must retain 2,500+ active renderers (no aggressive distance culling).
- **Frametimes**: p95 frametime must remain below 16.7ms (60 FPS target).
- **Screenshots**: Review generated images in `unity-project/Library/GmSceneIntelligence/player-probes/` with `view_file` to verify lighting, shadow fidelity, and prop grounding.

## Screenshot Review Honesty (MANDATORY)
When reviewing proof screenshots:
- **Describe what you actually see** in the image, not what the code says the object should be.
- A brown cube is a "brown cube", not a "carved mahogany table".
- A white sphere is a "white sphere", not a "porcelain mask".
- Flag any visible primitives as **visual defects** that need real mesh replacement.
- Never score visual quality above 70/100 if primitives are visible as player-facing props.
