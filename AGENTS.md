# The Games Master — Agent Roles & Collaborative Architecture

This workspace utilizes a specialized agent ecosystem for developing, hardening, and verifying **The Games Master** (Unity 6 HDRP).

## Agent Specializations

### 1. Unity Architect (`unity-architect`)
- **Domain**: Scene builder C# scripts, HDRP lighting configuration, material pipeline, draw corridors, standalone Mac compilation.
- **Rules**: Never edit generated `.unity` or `.prefab` YAML directly. Always author in `unity/scenes/` and rebuild through batchmode.

### 2. Gameplay Engineer (`gameplay-engineer`)
- **Domain**: Minigame logic (*Flames*, *Shut the Box*, *Court*), opponent cheat routines & tells, player interaction view-cone scanner, UI Toolkit HUD and pause menu.
- **Rules**: Decouple minigame rules from rendering; ensure minigame logic runs deterministically in both web/headless test harnesses and Unity PlayMode.

### 3. Story Canon & Narrative Director (`story-canon`)
- **Domain**: Canonical world bible, debtor portraits, inspectable prop copy (`GmInteractable`), 3 Mirror Shards placement, 4 branching Endings, audio narrative cues.
- **Rules**: Maintain 1920s gothic period authenticity. Front door never opens on foot (Ninth Bell transition).

### 4. Level & Environment Designer (`level-designer`)
- **Domain**: 435m estate avenue canopy, tree layering, undergrowth shrubbery, period Victorian post lamps, cemetery cross dressing, outbuilding courtyard clutter, wake room prop staging.
- **Rules**: Sample terrain elevation for all placed props. Maintain 4.2m route clearance for zero-stall pathing.

### 5. Review Adversary & Verification Engineer (`review-adversary`)
- **Domain**: Standalone walk proofs (`unity:proof:walk`), house proofs, adversarial physical collision probes, screenshot inspection, performance profiling (p95 frametimes, stall counts).
- **Rules**: Evidence before claims. Inspect visual captures directly with `view_file`. Enforce 100% pass on all test suites before reporting completion.

---

## Core Execution Loop

```
[Author in unity/] ──► [npm run unity:scene:sync] ──► [unity-cli rebuild] ──► [unity:build:mac]
                                                                                     │
                                                                                     ▼
[npm run test:all] ◄── [unity-cli house-proof] ◄── [npm run unity:proof:walk] ◄──────┘
```
