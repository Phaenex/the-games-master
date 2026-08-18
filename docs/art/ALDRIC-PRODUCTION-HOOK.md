# Aldric production hook

This is the asset-independent boundary for the final Aldric and connected player-body work. It does
not close the character-art gate. There is still no qualifying character prefab, authored motion
set, facial performance, voice, foley, or live IK rig in the shipping Parlor.

## Package pin

The project pins `com.unity.animation.rigging` **1.4.1** in both Unity manifests, both lock files,
and `unity/required-packages.json`. The version was checked against two official sources on August
17, 2026:

- Unity 6000.5.3f1 bundles `com.unity.animation.rigging-1.4.1.tgz` inside the editor. Its
  `package.json` requires Unity 6000.0.23f1 or newer.
- Unity's registry metadata at `https://packages.unity.com/com.unity.animation.rigging` identifies
  1.4.1 as the current 6000.0-compatible release. Its changelog includes the deprecated InstanceID
  API fix that matters to current Unity 6 editors.

📝 DECISION: Pin 1.4.1, not 1.4.0 | WHY: it is the exact package bundled with this 6000.5.3f1 editor
and removes a known obsolete API path | ALT: guessing from older Unity 6.0 manual pages would pin the
superseded 1.4.0 release.

## Asset layout and validator call

Keep untouched vendor files here:

```text
Assets/ThirdParty/<Vendor>/<Product>/
```

Keep owned derivatives here:

```text
Assets/GamesMaster/Characters/Aldric/
Assets/GamesMaster/Characters/Player/
```

That split is load-bearing. `GmModelImportSettings` forces a static-prop scale policy on models under
`Assets/GamesMaster/`. A correctly authored vendor humanoid belongs under `ThirdParty`, where that
postprocessor cannot rewrite its units.

Before the builder is allowed to instantiate either final prefab, call:

```csharp
GmCharacterAssetContractInspector.ValidateFromPaths(
    GmCharacterAssetRole.Aldric,
    rawModelPath,
    derivedPrefabPath,
    provenanceJsonPath);
```

The build must stop if the result contains any issue. It must not fall back to the current proxy.
The same call with `GmCharacterAssetRole.PlayerBody` owns the connected player-body gate.

The provenance JSON beside each owned prefab must contain these fields, filled with the selected
asset's real values:

```json
{
  "vendor": "",
  "product": "",
  "version": "",
  "licenseName": "",
  "licenseUrl": "",
  "sourceFile": "Assets/ThirdParty/<Vendor>/<Product>/<retained-source>",
  "redistributionTerms": "",
  "commercialUseAllowed": false
}
```

The inspector measures the real importer and prefab. It rejects a non-Humanoid importer, invalid or
non-human Avatar, broken shoulder/wrist chains, incomplete fingers, missing face controls, implausible
scale or bounds, missing LOD0/1/2, missing grips/gaze/seated sockets, missing controller or mask,
missing Animation Rigging root, non-HDRP materials, over-budget textures, absent retained source, and
license gaps. Aldric's LOD0 is capped at 120,000 triangles, 6 skinned renderers, and 8 materials. The visible
player body's LOD0 is capped at 80,000 triangles and must keep connected shoulders, arms, hands, and body
anchor.

## Runtime integration seam

`GmAldricPerformancePlanner` freezes pre-judgement performance from public observation, public action
role, trick ordinal, and stable command ID. The hidden cheat result, cheat kind, legality, pending
outcome, and future winner cannot enter its request. Correct-Read and false-accusation reactions are
added only when the public outcome exists.

`GmSemanticActionExecutor` is the shared manual clock for the later Animator, IK, and card driver. It
owns the five phases, hand/card claims, contact attach/release, pause, 0x to 4x speed, reduced motion,
skip, failure recovery, circuit breaker, and silent restore snap. A final adapter still needs to bind
the existing card motion and the selected rig to that interface. Animator events stay preview-only.
Its `Cancel` adapter must detach in a `finally` block before it propagates an exception; the executor
can contain a bad adapter call, but it cannot reach inside an adapter to repair state the adapter owns.

Restore calls only `RestorePublicPose`. It clears queued actions, claims, contact ownership, and audio
markers first. It never enters a performance phase or calls completion, so evidence, outcomes, voice,
foley, and reactions have no replay path.

## External blockers

- Select and legally approve the final Aldric and connected player-body assets.
- Retain real source files and complete the commercial license/provenance records.
- Author the production prefabs, controllers, masks, LODs, sockets, HDRP materials, and constraints.
- Author bespoke seated card actions, tells, catches, losses, wins, stand, and exit clips.
- Record and integrate real Aldric voice and table/cloth/card foley through an owned mixer.
- Bind the final rig and existing card motion to the shared executor, then prove every restore phase.
- Qualify character CPU, GPU, draw, memory, and zero-GC budgets in the built player.
- Review player, side, overhead, shadow, and reflection footage. The empty chair stays honest until
  all of this is real.

Do not create `ALDRIC-ASSET-REVIEW.md` until an actual candidate has been selected and measured. The
review must record the real vendor, product, version, license, source, rig, face, LOD, material, and
performance evidence. A blank approval document would only disguise the blocker.
