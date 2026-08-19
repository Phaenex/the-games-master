# GameCraft engine separation plan

The Games Master already has the beginning of a reusable game-building engine. It can scaffold
scenes, rebuild them deterministically, describe composition intent, index owned assets, preserve
review evidence, run physical proofs, and retain approved findings. It is not yet a reusable Unity
engine package. Most of that machinery compiles together with game-specific systems under the same
`Gm*` naming and source tree.

This plan keeps the shipping game moving while separating those responsibilities in controlled
steps. The target package name is `com.nyx.gamecraft`. That name is provisional until Nick chooses
the public name.

## What the audit found

The runtime source currently has 61 files and no assembly definition around the shared scene
system. The boundary manifest classifies them as:

| Class | Count | Meaning |
|---|---:|---|
| Portable now | 24 | Closed composition, intent, placement, lighting-budget, route, and integrity code that has no dependency on game-owned runtime types. |
| Adapter needed | 17 | Useful engine behavior that still knows about a Games Master player, save shape, UI, cue catalog, scene catalog, or named presentation resource. |
| Game-owned | 20 | Aldric, Parlor, endings, estate progression, House Memory, the run store, and other content or rules that should remain in this game. |

`npm run unity:engine:audit` now enforces this classification. A new runtime file fails the normal
fast test until its owner is declared. A portable candidate also fails if it starts referring to a
game-owned runtime type or a named piece of Games Master canon.

The lack of assembly definitions is real debt, but adding one around the current mixed folder would
break compilation. Unity assemblies cannot reach back into the predefined game assembly without
recreating the coupling we are trying to remove. The code must move in dependency order, preserving
Unity `.meta` GUIDs, before the compiler boundary is switched on.

## What this engine should know

The engine should know how to build and test a game, not decide what a good game means on Nick's
behalf.

It should provide:

- a game definition containing scenes, build entry points, quality budgets, save adapters, input
  adapters, and platform targets;
- deterministic scene authoring with zones, clusters, anchors, routes, negative space, motivated
  light, interaction intent, soundscape intent, and review claims;
- asset indexing and legal provenance checks before a builder places visible content;
- tunable feel data kept outside behavior code;
- reusable interaction, accessibility, save, transition, pause, audio, and telemetry contracts;
- EditMode, PlayMode, built-player, physical bypass, image, accessibility, and performance proofs;
- append-only review evidence and conservative rule proposals that require named human approval;
- a clean sample game that proves the package works without any Games Master class or asset.

It should not invent story, approve taste, turn one project's horror rules into universal law, or
claim a game is fun because its audits are green.

## The game-quality model

The engine's quality gates need to cover the parts players actually feel.

| Player-facing question | Engine evidence | Human evidence |
|---|---|---|
| Is the promise clear? | scene and interaction intent are present | the opening establishes the fantasy without an explanation dump |
| Are decisions readable? | focus, input, state, consequence, and recovery paths are tested | the player understands what changed and why |
| Is challenge fair? | deterministic rules, legal actions, tell windows, and loss recovery are tested | pressure feels earned rather than arbitrary |
| Does movement feel right? | speeds, camera limits, route clearance, collision, and controller paths are measured | a complete walk feels deliberate |
| Does the world have authored hierarchy? | anchors, layers, routes, negative space, and motivated lights pass composition audit | the frame has a subject and the room tells a story |
| Does atmosphere serve play? | light, dust, flicker, sound zones, captions, reduced motion, and contrast have budgets | fear, quiet, and surprise land without hiding required information |
| Does the game respect time? | pacing simulations, checkpoints, load transitions, and resume paths are tested | repetition and downtime still feel intentional |
| Does it survive failure? | saves are atomic, corrupt data fails safely, and recovery is proven | recovery wording and consequences make sense |
| Does it run where promised? | built-player frame time, stalls, memory conditions, and platform build are recorded | the final build is walked on the target display and controls |
| Did the engine learn honestly? | evidence is immutable, automation cannot issue taste verdicts, and rules need approval | Nick owns every promoted taste rule |

This is the main lesson from the project so far: a good game is not one system. The basement
masonry, a flickering hall lamp, a fair cheat tell, the pause menu, a corrupted save, and the p95
frame time all affect the same player experience. The engine has to keep those checks connected
without pretending they are interchangeable.

## Lessons already worth carrying forward

- Green logic tests can coexist with a physically bypassable gate. Physical adversary proofs are a
  separate release gate.
- A renderer count can coexist with a broken frame. Review captures need named claims and direct
  inspection.
- Performance numbers without host conditions create false diagnoses. Reports must carry load,
  memory, device, sample stability, and build identity.
- Dimming one light does not create night. Exposure, fog, practical sources, surface response, and
  sightline depth must be authored as one system.
- Continuous synthetic ambience can erase the setting. Sound needs a physical source model and a
  human listening pass.
- Asset variety is not authored composition. A cluster needs an anchor, support, detail, and a reason
  to exist.
- Feel values need one tunable data source. Contract thresholds and measured geometry are not feel
  dials.
- Automation may collect evidence and propose rules. It cannot approve taste or silently rewrite a
  cold deterministic build.
- A generated scene is not finished when it exists. It is finished after rebuild, audit, tests,
  review tour, built-player proof, and a real walk.

## Extraction sequence

```
E0  Boundary manifest and ratchet       [████████████████████] 100%
E1  Namespaces and assembly definitions [░░░░░░░░░░░░░░░░░░░░]   0%
E2  Unity package foundation            [████████████████████] 100%
E3  Games Master adapter and data        [░░░░░░░░░░░░░░░░░░░░]   0%
E4  Clean-room sample game              [░░░░░░░░░░░░░░░░░░░░]   0%
E5  Separate versioned repository       [██████████████░░░░░░]  70%
```

### E1, establish compiler boundaries

Move the 24 closed runtime files first, preserving their Unity GUIDs. Put them under a neutral
namespace and a runtime assembly. Move their tests into a matching test assembly. Then extract the
shared editor composition and audit code into an editor-only assembly that references the runtime
assembly. Do not rename serialized MonoBehaviours and move their files in the same commit.

### E2, create the embedded package

Use Unity's current package layout with `package.json`, `Runtime`, `Editor`, `Tests/Runtime`,
`Tests/Editor`, `Documentation~`, and `Samples~`. The package owns no Games Master assets, scene IDs,
cue names, or story text.

The standalone repository now exists at `/Users/damato/Projects/gamecraft-engine` and is installed as
the local `com.nyx.gamecraft` package in both tracked Unity manifests. Its neutral runtime, editor,
test, schema, documentation, CI, and Minimal Game foundations compile inside The Games Master. The
24 closed legacy candidates and their editor consumers remain E1 work. They must move without
breaking serialized scene references.

### E3, replace knowledge with adapters

Define small contracts for player control, interaction content, save storage, scene transitions,
audio cues, UI presentation, and telemetry. The Games Master implements those contracts in its own
assembly. `GmFeelConfig`, `GmRunStore`, House Memory, Parlor, endings, and estate progression stay in
the game.

Scene registry data should become one game-definition asset or validated JSON document. The command
runner reads that definition instead of carrying fixed Wend Hill and Parlor artifact paths.

### E4, prove reuse

Build a tiny clean-room sample with a different name, tone, scene list, input adapter, save payload,
and visual grammar. It must scaffold, build, audit, test, tour, and produce a standalone player
without importing a Games Master assembly. That is the point where “build any game” becomes a tested
claim instead of a hope.

### E5, separate the repository

Nick directed the repository split before the clean-room sample was complete. The private repository
is now at [Phaenex/gamecraft-engine](https://github.com/Phaenex/gamecraft-engine), with `v0.1.0`,
migration notes, and a changelog. The game still uses the sibling checkout so local Unity builds do
not depend on GitHub credentials. E5 closes after a clean consumer installs the private tag and the
game deliberately switches from the local development link to a pinned release.

## Current Unity alignment

The project is pinned to Unity `6000.5.3f1`. Its required packages match the repository contract.
No package was upgraded during this audit because a passing pin is safer than an untested version
bump.

The extraction direction matches current Unity guidance: custom packages use separate runtime,
editor, and test assemblies; package tests live under dedicated test folders; ScriptableObjects are
appropriate for shared authored data but not deployed save writes; and the Unity Performance
Testing API is available when the current custom proof reports are ready to become package-level
benchmarks.

Sources checked 2026-08-19:

- [Unity package layout](https://docs.unity3d.com/6000.0/Documentation/Manual/cus-layout.html)
- [Assembly definitions and packages](https://docs.unity3d.com/6000.0/Documentation/Manual/cus-asmdef.html)
- [Creating custom packages](https://docs.unity3d.com/6000.0/Documentation/Manual/CustomPackages.html)
- [Unity Performance Testing API](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.test-framework.performance.html)
- [ScriptableObject](https://docs.unity3d.com/6000.1/Documentation/Manual/class-ScriptableObject.html)

## Next safe move

Extract the closed composition and intent types into a runtime assembly in small batches. The first
batch should contain only plain data components with no saved-scene instances, then run source sync,
EditMode, PlayMode, every registered audit, and the full game build. Components already serialized
into scenes move later with GUID preservation and a scene reopen check.
