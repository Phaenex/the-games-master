# Supervised Unity scene intelligence

This is the adaptive layer above the authored composition engine. It remembers review evidence,
indexes imported assets, proposes tightly bounded alternatives, and detects visual drift. It does
not train a model, rewrite a scene, select an asset, approve a baseline, or claim taste on Nick's
behalf.

The safety rule is simple: automation may measure, agents may recommend, and Nick approves taste.
Every durable decision is inspectable JSON. A cold rebuild can reproduce the authored scene without
the adaptive layer, and temporary previews must restore the exact transform fingerprint before they
pass.

## Knowledge store

The repository-owned store is `unity/scene-system/knowledge/`:

| Path | Ownership | Purpose |
|---|---|---|
| `review-sessions/` | append-only | Explicit review events. Existing session IDs cannot be replaced with different content. |
| `historical-observations.json` | curated migration evidence | Honest known findings from earlier reviews, clearly marked as agent evidence. |
| `approved-rules.json` | Nick gate | Draft rules promoted by an explicit confirmation only. |
| `defect-resolutions.json` | Nick gate | Append-only record of defects Nick has inspected and marked resolved. |
| `baselines.json` | Nick gate | Approved visual baselines. Empty until Nick accepts a scene candidate. |
| `adaptive-overrides.json` | Nick gate | Selected adaptive candidates. Empty during proof and review. |
| `derived/` | rebuildable | Asset preferences, open/resolved defects, and promotable rule candidates. |
| `schemas/` | repository | Review-session interchange contract. |

Review-memory writes are atomic and canonical. Rebuilding derived files is deterministic. Corrupt JSON, an unknown
schema version, an automated taste verdict, a duplicate resolution, or an attempt to mutate an
immutable session fails closed.

Review weights are deliberately conservative:

- Nick Keep contributes `+1`.
- Nick Change or Reject contributes `-1`.
- Agent Keep contributes `+0.25`; agent Change or Reject contributes `-0.25`.
- Automation can record objective findings but cannot issue Keep, Change, or Reject.
- Only Nick Reject creates a contextual asset exclusion.
- A draft rule needs either a Nick rejection or the same signature in at least two scenes.
- Eligibility does not approve a rule. Promotion is still a separate Nick-confirmed action.

Commands:

```bash
npm run test:scene-learning
npm run unity:learning
node scripts/scene-learning.mjs rebuild

# These intentionally fail unless the final confirmation flag is present.
node scripts/scene-learning.mjs promote <candidate-id> --confirm-nick
node scripts/scene-learning.mjs resolve <defect-id> --confirm-nick
```

Use the Unity review window for normal promotion and resolution. Its confirmation dialog supplies
the same gate without asking Nick to copy IDs.

## Composition and intent metadata

Every adapted production scene owns one `GmSceneComposition`. Its zones, clusters, elements,
reserved routes, negative space, motivated lights, and review claims describe why the scene exists.
The adaptive extension adds:

- `GmAdaptiveSlot`: a stable local question with at most three imported candidate assets;
- `GmSceneAdaptiveIntent`: scene status, deterministic behavior, and candidate policy;
- `GmAudioIntent`: Bed, Diegetic, Stinger, Foley, or UI ownership plus loop policy;
- `GmLightIntent`: Practical, Environmental, or CompositionFill ownership;
- `GmVisualBaseline`: frame metrics and transform fingerprint for a future approved baseline.

Wend Hill now proves the full serialized contract without changing its visual placement. It has 8
zones, 15 clusters, 82 tagged elements, 5 routes, 5 negative-space reservations, 18 review claims,
17 classified lights, and 2 guarded adaptive slots. The serialized scene retains those components
after close and reopen. Its composition/variant fingerprint is `460221080faabbff`; the deeper Wend
Hill estate/material/Terrain fingerprint is `2c9c97b0db8dc80b`.

CompositionFill is allowed, but it must be honest. A light with no visible lamp cannot pretend to be
practical. This keeps useful fill while exposing it for the later visual pass.

## Terrain and runtime integrity

`GmTerrainPrototypeAudit` is shared editor policy for every registered scene. A populated Terrain
prototype must expose a real renderer on its prefab root or a root `LODGroup` whose renderers all
reference real meshes. Child-only meshes are rejected because Unity Terrain can accept them in an
asset audit and then refuse to instance them in the built player. Callers also set a minimum real
population so registration alone cannot inflate a variety claim.

`GmRuntimeIntegrityPolicy` enforces the same class of failure while the player runs. The Node-side
`unity-runtime-integrity.mjs` scans the complete player log, including warnings emitted before the
runtime subscriber existed. Tests keep their required Terrain/shader fragments aligned and prove
thread-finalization chatter does not become a false visual failure.

## Display calibration

`GmDisplayCalibration` leaves the persisted authored `ColorAdjustments.postExposure` at 0 and applies
only a runtime preference from -0.5 to +0.5 stops. Keyboard arrows and controller D-pad operate it
from pause. Tours and standalone proofs force level 0 without saving, so evidence remains comparable
while players on darker displays retain a narrow accessibility control. Project-wide details live in
`docs/UNITY-PROJECT-SETTINGS.md`.

## Imported asset intelligence

`GmAssetIntelligence` indexes only assets already imported into the Unity project. It records GUID,
path, source pack, render pipeline, dependency hash, rendered bounds, pivot and scale hints, renderer
and material counts, collider and LOD coverage, triangle count, shader families, flatness, semantic
tags, and audio duration/channel/rate/RMS/loop evidence. Audio schema v3 separates the first/last
window texture comparison from the actual boundary sample jump, slope change and normal sample-step
energy. It also records raw narrow-tone risk, intentional tonal context and environmental risk, so
a tinnitus symptom cannot be silently approved as ambience or falsely rejected as a broken loop.
Known bad or incompatible entries carry an explicit rejection reason.

The index lives under Unity's `Library/GmSceneIntelligence/`, so it is disposable local evidence,
not a second asset database committed to the game. A repeated index run must be byte-identical for
the same import state. Contact sheets are real rendered PNGs with JSON manifests, grouped by role,
so asset selection starts with comparable evidence instead of folder-name guessing.

```bash
npm run unity:assets:index
```

The current project indexes 1,731 imported assets. The accepted schema-v3 content hash is
`6211277dd78c7fae47aea3f3d37e5f60`. It includes all five final Ninth Bell clips and classifies
`ear_whine` as raw tonal risk `true`, intentional internal tone `true`, environmental risk `false`,
and loop suitability `likely` from its boundary evidence.

## Guarded variants

Variants are review evidence, not scene edits. Each preview:

1. resolves one declared slot and one of at most three deterministic candidates;
2. refuses a Nick-excluded asset, missing prefab, bad shader/material, excessive grounding
   correction, or any new composition-audit failure;
3. disables only the allowed original renderer and collider set;
4. instantiates the candidate in memory;
5. frames and captures it;
6. restores in `finally`, including injected-exception paths;
7. reloads the saved scene if Unity marked it dirty;
8. verifies the exact pre-preview fingerprint;
9. never saves and never writes `adaptive-overrides.json`.

```bash
npm run unity:variants
```

Wend Hill currently produces six previews, three for cemetery grave detail and three for garden bed
growth. Proof ended with `selected=none`, composition fingerprint `460221080faabbff`, and an empty
override file. The separately computed estate fingerprint remained `2c9c97b0db8dc80b`.

## Visual baselines

The baseline layer measures perceptual hash, luminance histogram, clipping, edge density, and scene
fingerprint. These are regression signals, not an aesthetic score. Baseline approval is blocked while
the scene remains in review or has open defects. No golden image has been accepted for Wend Hill.

Unity's Graphics Test Framework is intentionally deferred until Nick approves a production look.
Freezing the current A- candidate as a golden image would turn known weaknesses into protected
behavior.

## Review window

Open **Games Master > Scene Intelligence > Review Window**. It provides:

- the active scene, selected review subject, fingerprint, claim count, and adaptive-slot count;
- explicit Keep, Change, and Reject recording with confirmation;
- imported-asset reindex and contact evidence;
- guarded variant preview generation with no selection;
- canonical review tour and live walk controls;
- baseline evaluation and separately gated baseline approval;
- derived-knowledge rebuild;
- explicit draft-rule promotion and defect resolution, each with a second Nick confirmation.

Change and Reject require a defect tag. The window never pre-fills Nick's verdict and never records
one on selection, capture, or exit.

## Full proof sequence

With Unity closed:

```bash
npm run test:scene-learning
npm run test:scene-system
npm run test:scene-system:csharp
npm run unity:scene:check
node scripts/unity-cli.mjs audit-saved
npm run unity:assets:index
npm run unity:variants
node scripts/unity-cli.mjs test
node scripts/unity-cli.mjs playtest
node scripts/unity-cli.mjs tour
npm run unity:build:mac
npm run unity:proof:mac
```

The knowledge suite currently passes 12/12, including Unity's optional-empty-string interoperability
case and fail-closed gated-ledger corruption. The runtime-integrity suite adds 4/4 Node tests and the
Unity suite adds shared Terrain/display tests. The final command launches the actual built player,
writes seven player-backbuffer frames under
`~/GamesMaster-Unity/Library/GmSceneIntelligence/standalone-proof/`, verifies none are tiny, requires
the runtime probe's 7/7 zero-error marker, and rejects a build containing a UnityEditor assembly.

## Boundaries

This system cannot prove fear, beauty, period fit, audio character, or pacing. It also does not make
an A scene into an A+ scene merely by describing it better. Wend Hill still needs Nick's walk and
ear gate; vehicle period fit, cemetery repetition tolerance, figure subtlety, wind choice and 4m45
pacing remain honest review items.

No further asset purchase is justified by this engine pass. The imported library is broad enough to
build the next scenes; the right next action is targeted use and human review, not another bundle.
