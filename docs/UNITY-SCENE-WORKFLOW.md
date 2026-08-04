# Unity scene production workflow

This is the repeatable scene factory for The Games Master. It exists so Entry Hall, Parlor, Court,
Shut the Box, the hidden room, the Labyrinth, and the endings do not each invent a different build
and review process.

The factory standardizes mechanics and authored visual intent, not taste. It can prove that a scene
is the right scene, builds deterministically, contains required systems, routes correctly, uses
named visual clusters instead of orphan scatter, produces real images, and has not drifted from
repository source. It cannot decide whether a room is beautiful, frightening, legible, or paced
well. Those calls remain screenshot review plus Nick's live walk. The visual contract is documented
in `docs/UNITY-COMPOSITION-ENGINE.md`.

## The source-of-truth chain

1. `unity/scene-system/scene-registry.json` owns each scene's ID, path, phase, build method, audit
   method, tour method, success markers, and required screenshot count.
2. New scene C# begins below `unity/scenes/<scene-id>/`. That repository copy is authoritative.
3. `scripts/sync-unity-scenes.mjs` copies registered C# into `~/GamesMaster-Unity` and detects drift.
4. The generated `.unity` file is build output. The deterministic builder is the authored source.
5. A `GmSceneIdentity` inside the built scene proves that its serialized content matches the
   registered scene ID and schema.

Wend Hill predates this layout, so its existing source remains in the Unity project for now. It is
registered and follows the same command, identity, audit, test, and tour contract. Do not move its
large builder during Phase 0 review; that would add refactor risk without improving the opening.

## Commands

Run these from `~/Projects/games/the-games-master` with Unity closed.

```bash
npm run unity:scene:registry
npm run unity:scene:check
npm run test:scene-system
npm run test:scene-system:csharp

# Preview a new source pack. Dry-run is the default and changes nothing.
node scripts/scaffold-unity-scene.mjs --id entry-hall --phase 1 --shots 8

# After reviewing names and scope, create it once.
node scripts/scaffold-unity-scene.mjs --id entry-hall --phase 1 --shots 8 --write
npm run unity:scene:sync

# Every scene-aware command accepts the registered scene ID.
node scripts/unity-cli.mjs rebuild entry-hall
node scripts/unity-cli.mjs audit entry-hall
node scripts/unity-cli.mjs tour entry-hall

# Tests are project-wide so cross-scene regressions cannot be filtered away accidentally.
node scripts/unity-cli.mjs test
node scripts/unity-cli.mjs playtest

# Produce a native-resolution fullscreen macOS review app.
npm run unity:build:mac
npm run unity:proof:mac

# Supervised memory, imported asset evidence, and temporary alternatives.
npm run unity:learning
npm run unity:assets:index
npm run unity:variants
```

For a human walk without arming or running the automated screenshot tour, launch the editor with
`-executeMethod GmSceneBuildUtility.LaunchPlayReviewFromCommandLine` and pass the registered scene
path after `-gmReviewScene`. Inside an already open editor, use
**GamesMaster > Play Current Scene (No Tour)**. Both routes clear stale tour arms, select Game view,
and enter Play mode without changing the serialized scene.

The macOS build command requires Unity to be closed, verifies Wend Hill is the startup scene, and
writes `~/GamesMaster-Unity/Builds/macOS/The Games Master.app`. This is an unsigned local review
build, not a Steam depot or a notarized public release. The CLI deletes only that generated app
bundle before rebuilding it and refuses to pass unless Unity reports success and the executable is
present inside the bundle.

`npm run unity:proof:mac` launches that built app with an opt-in seven-frame player probe. It verifies
the cold-open UI, spawn, look path, drive depth, cemetery, garden, and porch from the real player
backbuffer, requires zero runtime errors, and rejects a bundle containing UnityEditor assemblies.
Evidence lands under `~/GamesMaster-Unity/Library/GmSceneIntelligence/standalone-proof/`.

Open **Games Master > Scene Intelligence > Review Window** for supervised feedback and visual
evidence. The window can record explicit Nick verdicts, refresh knowledge, reindex imported assets,
generate temporary variants, arm the tour, start a walk, evaluate a baseline, and perform separately
confirmed rule-promotion or defect-resolution actions. It never auto-selects a variant or fabricates
a human verdict. Full behavior is in `docs/UNITY-SCENE-INTELLIGENCE.md`.

`npm run unity:scene:check` is read-only. It validates the registry and fails if a repository-owned
scene source is missing or different in Unity. `npm run unity:scene:sync` performs the explicit copy.
It never deletes unknown Unity files.

The scaffold refuses duplicate IDs, duplicate scene names, unsafe paths, invalid C# method names,
invalid phases, and existing destination files. It generates six items:

- a deterministic builder;
- an authored composition plan that starts deliberately red;
- a structural quality audit;
- a scene-specific screenshot tour based on the shared capture engine;
- minimum EditMode tests, including a placeholder-shot failure;
- a room README with the exact gates.

The composition intent and every generated screenshot are named `replace-me`. Generated tests stay
red until the plan contains real zones, anchored clusters, elements, routes, negative space,
motivated lights, and one deliberate claim per review shot. This prevents a compiling scaffold from
being reported as a visually reviewed room.

## Scene lifecycle

### Gate 0: design freeze

Before scaffolding, write down:

- the room's story job and entry/exit state;
- the mechanics the player must understand here;
- required canon objects and forbidden contradictions;
- the player route and optional exploration pockets;
- the asset families allowed in the room;
- its zones, dominant anchors, story clusters, protected routes, and intentional negative space;
- the visual and audio questions only Nick can answer.

Do not build a room whose story job is still changing. A builder makes iteration cheap, but it does
not make conflicting direction free.

### Gate 1: scaffold and registry

Run the scaffold as a dry run first. Confirm:

- scene ID is lowercase kebab-case;
- Unity scene name and C# prefix are stable and unsurprising;
- phase matches `docs/STEAM-TRACKER.md`;
- shot count covers the room rather than an arbitrary round number;
- no second version of the same room is being created.

Then use `--write`, sync, and immediately run `npm run unity:scene:check` plus
`npm run test:scene-system`.

### Gate 2: navigable blockout

The first real builder pass creates only what is needed to judge space:

- final-scale floor, ceiling, walls, doors, stairs, and major columns;
- player spawn and exit anchors;
- collision that matches the visible architecture;
- one lighting direction sufficient to read the route;
- review shots for entrance, exit, major axis, every mechanic station, and every blind corner.

At this gate, prove dimensions and traversal. Do not bury a bad room shape under props. Use simple
materials only when they render correctly in HDRP; built-in Standard materials are not acceptable
temporary content because they turn magenta and poison every screenshot.

Required checks:

```bash
node scripts/unity-cli.mjs rebuild <scene-id>
node scripts/unity-cli.mjs audit <scene-id>
node scripts/unity-cli.mjs test
node scripts/unity-cli.mjs playtest
node scripts/unity-cli.mjs tour <scene-id>
```

### Gate 3: architectural composition

Replace blockout surfaces with a small, coherent asset vocabulary. For each major placement, check:

- real-world scale at 1.7 meter eye height;
- floor contact from rendered bounds, not pivot assumptions;
- doors and stairs align with collision and route width;
- repeated modular pieces do not show obvious seams or texture phase jumps;
- silhouette remains readable at the authored exposure;
- no prop becomes accidental architecture;
- no purchased demonstration scene is imported wholesale;
- no second mansion or other canon-breaking shell appears.

Author the same decisions in `<Scene>CompositionPlan.cs`. Every major visible object belongs to a
named cluster and has a short rationale. Every cluster has one anchor plus support and detail roles.
Reserve the player route and the clear volumes that give silhouettes breathing room. The shared
composition audit must pass before detail dressing begins.

Take screenshots before detail dressing. If the room does not read with architecture and light, more
objects will make the diagnosis harder.

### Gate 4: gameplay stations and story evidence

Add interactables in route order. Each station needs:

- a reason to notice it before interaction;
- clear reach and facing distance;
- collision that cannot trap or snag the player;
- one authoritative runtime owner;
- testable state transitions;
- story text sourced from the current canon document;
- persistence keys when the consequence crosses scenes.

Tests must include the happy path, refusal/invalid input, repeated interaction, leaving and returning,
and any state that can unlock an ending. A log line is evidence only when a test asserts the state it
claims.

### Gate 5: lighting and audio

Lighting is reviewed as compositions, not isolated values. Each gameplay station should have a
motivated hierarchy: route read, subject read, background separation, then detail. Verify from the
player camera at final exposure. Do not use automatic exposure to rescue a dark room.

Audio receives the same discipline:

- one intentional continuous bed at most, unless the room design explicitly needs more;
- no short texture loop that reveals its seam;
- spatial sources placed at believable emitters;
- low-frequency beds checked for engine or spaceship character;
- silence used deliberately;
- an A/B control when the taste choice is uncertain.

Buying another pack is a Nick gate. First prove the current library cannot supply, edit, layer, or
filter the required sound.

### Gate 6: authored detail pass

Props support use, history, and route hierarchy. Every cluster should answer at least one question:

- Who used this?
- What happened here?
- What should the player look at next?
- What mechanic does this frame or support?

Scatter that answers none of them is noise. Keep traversal lanes visibly cleaner than dead corners.
Vary repeated kit pieces by role and composition, not random rotation alone. Decorative meshes must
not carry collision unless touching them changes play.

### Gate 7: adversarial verification

A room is not ready because one run passed. Minimum final evidence is:

1. cold rebuild from builder;
2. structural audit;
3. second cold rebuild;
4. identical layout fingerprint or an explained intentional difference;
5. all EditMode tests;
6. all relevant PlayMode route tests;
7. complete screenshot count with no tiny, white, or black frames;
8. every screenshot inspected at full size;
9. post-tour audit to prove review hooks did not dirty authored state;
10. repository-to-Unity source check;
11. no temporary C# scripts, recovery scenes, or duplicate room files;
12. Nick live walk for art, audio, readability, and pacing.
13. native player build plus `npm run unity:proof:mac` when the scene is in the startup path.

The CLI may retry one diagnosed zero-artifact GUI idle stall. A partial capture, reported code defect,
test failure, or unexpected exit is a failure. Retry evidence remains in separate logs and a retry
pass is reported as flaky, not silently green.

## Required screenshot set

Shot count is scene-specific, but coverage is not optional. Include:

- first player view;
- reverse view toward the entrance;
- main route wide;
- each gameplay station at approach and interaction distance;
- optional route entrance and deepest point;
- exit or handoff;
- at least one stress view for occlusion, repeated assets, or state transition;
- exact same-camera before/after pairs for visible state changes.

Name shots by what they prove, not by camera number alone. `07-ledger-readable` is useful evidence;
`07-angle-b` is not.

Each shot also needs a `GmReviewCompositionClaim`. The claim names its primary element and any
deliberate foreground, support, and background elements, plus acceptable framing and apparent-size
ranges. The audit confirms those subjects are actually in frame and in the declared depth order.

## Audit boundaries

The shared `GmSceneContractAudit` proves:

- exact active scene path;
- exactly one durable identity with matching ID, display name, and schema;
- unique root names;
- required roots;
- exactly one active enabled MainCamera;
- no missing script components;
- exactly one enabled build-settings entry.

The shared `GmSceneCompositionAudit` proves:

- one non-placeholder visual-intent manifest for the registered scene;
- named zones with anchored story clusters and no orphan elements;
- cluster radius, member caps, support/detail floors, and asset-family variation;
- visible renderer ownership, grounding, declared spatial relationships, and no exact overlaps;
- reserved route clearance and negative-space preservation;
- visible motivation for every enabled local light;
- one measurable subject/framing/depth claim per review shot.

The reusable tour also rejects captures whose percentile range and clipping fractions indicate an
effectively black, white, or flat image. This closes the old loophole where a plausible mean
luminance could hide an unusable frame.

Room audits must add their own objective claims: route clearance, required stations, material pipeline,
grounding, collision ownership, lighting source count, audio loop policy, persistence wiring, and
mechanic-specific invariants. Do not turn taste into fake numeric certainty. Screenshot and live-play
gates own composition, fear, beauty, subtlety, and pace.

## Failure and recovery rules

- Preserve the first failing log before a diagnostic rerun.
- Never convert an unexpected Unity exit into a pass.
- Never accept a zero-test XML file.
- Never accept screenshots from a prior run; the target folder is cleared before capture.
- Never accept a tour with the wrong count, all tiny files, all-white luminance, or all-black
  luminance.
- Never run two Unity editors against the project.
- Never edit or delete unrelated dirty worktree files.
- Never push, commit, buy assets, or change Nick-owned taste calls without asking.

If the host is too resource-starved for Unity to initialize reliably, stop launching more editors,
record the failed log, continue read-only or repository work, and retry after capacity returns. A
startup that never reached compilation says nothing about code quality.

## Handoff format

Every scene handoff reports:

- exact registry ID and scene path;
- current phase/status and tracker percentage;
- files changed in repository and Unity;
- build/audit/test/tour commands run;
- counts and fingerprints;
- screenshot path and visual findings;
- remaining objective defects;
- Nick-only taste questions;
- whether any run passed only on retry;
- explicit confirmation of no commit, push, or purchase.

The next agent should be able to reproduce the scene without relying on the last editor session or a
verbal claim.
