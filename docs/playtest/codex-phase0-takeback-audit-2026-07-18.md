# Codex Phase 0 takeback cold audit

Date: 2026-07-18

Scope: the current Unity/Steam Wend Hill Prologue after Claude's six delta changes and Codex's
independent delta review. This audit was written before any Phase 0 repair code was edited.

## Baseline verdict

**Product B+, Unity/Steam Phase 0 84%, ready for Nick's judgment walk but not signed off.** The scene
is mechanically stable and visually coherent. The remaining path to closure has two separate lanes:
objective tooling defects the agent can repair now, and visual/audio/pacing decisions only Nick's
walk can clear.

Current verified baseline:

- estate audit twice: `objects=3968 renderers=2949 colliders=496 lights=9`, fingerprint
  `4707410a08b94635` both times
- Unity EditMode: 41/41
- Unity PlayMode: 3/3
- HDRP tour: 16/16, all images inspected at full resolution
- browser runner: 9/9 in one uninterrupted invocation
- Unity source hashes unchanged during the delta review
- no commit, push, purchase, second mansion, or open Threshold Refusal

## Audit table

| ID | Finding | Severity | Evidence | Owner / disposition |
|---|---|---|---|---|
| P0-T01 | Side-zone test claims seven wall pressures but records only final containment. Chapel-east cannot fail with its wall absent; garden west/south hit collidable fences first. | **BLOCKING** for trustworthy automation | `codex-delta-review-2026-07-18.md`, perimeter table; `GmPrologueRouteTests.cs` | **AGENT**: replace ambiguous pushes with endpoint/contact-aware tests that name the intended `WalkBounds` collider and use clear approach lanes. |
| P0-T02 | Single-mansion audit loses provenance after an unpack-and-rename duplicate. | **BLOCKING** for the one-mansion automated floor | `ValidateSingleMansion`; prefab check skips `IsPartOfPrefabInstance == false` | **AGENT**: add a durable authored identity marker or scene/component signature that survives prefab unpacking, then test the counterexample. No second mansion will be added to the shipped scene. |
| P0-T03 | GUI retry catches every run failure, deletes the first detailed log on retry, and can return green with only ephemeral console evidence. | **POLISH**, high tooling priority | `unity-cli.mjs` `runWithRetry`; `run()` removes `cli-tour.log` every attempt | **AGENT**: retry only diagnosed zero-artifact idle/open stalls; preserve attempt-numbered logs and print a final flaky-pass summary. |
| P0-T04 | Figure tour proves static present/gone states from different cameras, not whether the one-frame rig removal pops during movement. | **POLISH** | tour shots 15 at `z=44` and 16 at `z=15`; `EvaluateFigureAtZ` disables the whole rig | **AGENT**: add a same-camera or short-sequence review capture around the cutoff. Do not seed gameplay randomness or retune taste before Nick sees it. |
| P0-A01 | Window pane is now obvious, but the silhouette reads more like a glyph than a person. | **NICK** | full-resolution shot 15 and 1:1 crop | **BOTH** after walk: Nick calls too loud/too vague; agent adjusts pose and contrast, not canon. |
| P0-A02 | Kitchen garden remains the weakest zone and may read as an empty field. | **NICK** | shots 09, 10, 14; Claude B-; Codex final B | **BOTH** after walk. Do not begin an eighth speculative rebuild before Nick's motion read. |
| P0-A03 | Cemetery remains visually repetitive at detail distance. | **NICK** | shots 06, 07, 13; 3 cross models and one repeated wall module | **BOTH** after walk. Existing bundle can supply variants if Nick rejects it; no purchase. |
| P0-A04 | Fixed EV -3, vehicle art match, filtered wind A/B, and 4m45 nine-bell pacing remain human-only calls. | **NICK** and Phase 0 exit gate | `NICK-NEEDED.md`; Steam tracker | **NICK**: guided Unity walk and ear test. Agent implements only the resulting punch list. |
| P0-P01 | Chapel waypoint 08 now reads as a building and passes through the authored opening. | **PASS** | fresh shot 08 plus source geometry | Closed unless Nick dislikes it in motion. |
| P0-P02 | Review figure determinism is correctly isolated from one-in-three gameplay randomness. | **PASS** | `HideFigureForReview`, `ForceFigureForReview`, gameplay `Random.value < 1/3` | Preserve. Do not seed the gameplay roll. |
| P0-W01 | Gate lock has no physical backtrack barrier. | **WONTFIX** by canon | `GmThreshold` is narrative/audio only; pre-lock retreat is secret-ending canon | Do not invent a containment rule. |
| P0-W02 | Garden/cemetery automation cannot certify visual composition. | **WONTFIX** as a test claim | Counts and transforms prove structure, not art quality | Keep visual and human gates explicit rather than faking an A-grade assertion. |

## Execution order

1. Repair P0-T01 through P0-T04 without touching garden, cemetery, figure taste, exposure, vehicle,
   wind mix, or pacing.
2. Rebuild and rerun the full Unity audit, EditMode, PlayMode, and visual-tour stack.
3. Inspect the focused evidence and prepare one guided Nick walk.
4. Apply only Nick's observed punch list.
5. Rerun all Unity evidence and the uninterrupted nine-gate browser suite.
6. Close Phase 0 only after Nick says the opening stands.

## Locks

No purchase, commit, push, second mansion, seeded gameplay figure roll, opened Threshold Refusal,
or speculative garden/cemetery rebuild is authorized by this audit.
