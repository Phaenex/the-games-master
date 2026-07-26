# Unity project scene-quality settings

These are project contracts, not workstation preferences. Future scenes inherit them unless a scene
spec documents and tests a deliberate exception.

## Rendering and display

- Unity editor: 6000.5.3f1, HDRP.
- Authored night exposure is fixed. Wend Hill uses fixed EV -2.55; automatic exposure is forbidden
  because it erases authored darkness and makes display-to-display review irreproducible.
- `ColorAdjustments.postExposure = 0` is the persisted authored baseline.
- `GmDisplayCalibration` owns the player preference. It offers five levels from -0.5 to +0.5 stops
  in 0.25-stop steps, available from the pause menu on keyboard arrows and controller D-pad.
- Automated tours and standalone proofs force level 0 in memory and never overwrite the player's
  saved preference. A review screenshot therefore always measures the authored grade.
- Do not solve a local readability problem with global exposure. Use a visible practical or an
  explicitly classified, bounded `CompositionFill`, then add a shot-level pixel gate.

## Imported geometry and Terrain

- Purchased source prefabs and materials are immutable. Project-owned variants may reuse their
  licensed meshes and textures without rewriting the imported source.
- A Unity Terrain tree prototype is valid only when its prefab root owns a real mesh renderer, or
  owns an `LODGroup` whose referenced renderers all contain real mesh data. A child-only renderer is
  not sufficient and will be rejected by the built player.
- `GmTerrainPrototypeAudit` is the shared pre-build contract. Scene audits call it with a meaningful
  minimum population so an unused registered prototype cannot inflate a variety claim.
- Scene-specific material, reflectance, ecology and route constraints belong above that shared
  structural audit, not inside it.

## Player/runtime integrity

- `GmRuntimeIntegrityPolicy` is the in-player warning classifier.
- `scripts/unity-runtime-integrity.mjs` is the post-run player-log classifier. Its parity test keeps
  the required fragments aligned with the C# policy.
- Missing Terrain instances, invalid root renderers, unsupported shaders, unassigned shaders and
  the internal error fallback are fatal proof defects even if Unity logs only a warning and exits 0.
- Known shutdown/thread-finalization chatter is not classified as missing visual content.
- Standalone composition frames must be free of probe-created interaction text, story cards,
  controls legends and review toasts. Dedicated cold-open and pause frames own UI proof.

## Input and accessibility

- One Input System action map owns keyboard, mouse and gamepad. Gameplay code consumes actions rather
  than device-specific polling.
- Every shipping pause-screen setting needs both keyboard and controller bindings and a native-player
  proof. Current display calibration uses Left/Right Arrow and D-pad Left/Right.
- Virtual-controller proof is mandatory but does not claim physical controller feel. Nick still
  tests a real Xbox or PlayStation-compatible device.

## Evidence and learning

- Generic review gates reject blank, clipped and near-black frames. Scene-specific subclasses add
  semantic gates such as figure visibility and chapel architectural readability.
- Objective automation findings may be appended to `unity/scene-system/knowledge/review-sessions/`.
  They cannot issue taste verdicts, resolve defects, approve rules or freeze a visual baseline.
- Agent verdicts remain low-weight advice. Nick alone approves baselines, promotes rules, resolves
  taste defects, buys assets, commits and pushes.
- Current objective learning evidence is
  `docs/playtest/scene-learning-phase0-a-candidate-2026-07-22.json`.

## Future-scene acceptance checklist

1. Author zones, clusters, routes, negative space, light ownership and review claims before polish.
2. Run the shared composition, craft, Terrain and runtime-integrity contracts.
3. Capture the canonical editor tour at display level 0 and inspect every frame.
4. Reject visibly bad candidates even when audits pass; record why.
5. Build the real player and inspect clean native composition plus dedicated keyboard/controller UI.
6. Record objective evidence. Leave taste approval and durable rule promotion for Nick.
