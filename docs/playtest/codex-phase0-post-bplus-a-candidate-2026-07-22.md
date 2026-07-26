# Phase 0 post-B+ A-candidate closure

Date: 2026-07-22  
Estate fingerprint: `2c9c97b0db8dc80b`  
Composition/variant fingerprint: `460221080faabbff`  
Codex verdict: **A candidate, not A+; ready for adversarial Claude review and Nick's sensory walk**

## What changed after Claude's B+

Claude's finding was not treated as four bad prefabs. It was treated as a missing project contract.

- `GmTerrainPrototypeAudit` now rejects child-only Terrain roots, missing root/LOD mesh data, invalid
  prototype references and insignificant populations for every future scene.
- Wend Hill's scene audit and EditMode test consume that shared module.
- `GmRuntimeIntegrityPolicy` catches the failure in the player as it happens.
- `scripts/unity-runtime-integrity.mjs` scans the complete player log, including warnings emitted
  before the probe subscribed. Four tests pin Terrain and shader failures and exclude finalization
  chatter.
- Objective results were appended as automation evidence. No Nick verdict, defect resolution, rule
  promotion, adaptive selection or baseline approval was fabricated.

## Art/readability pass

- The SUV's near-black body was raised to restrained oxblood and given enough rough reflection for
  bonnet, door and wheel-arch planes to read. Headlights remain off; no fictional light was added.
- A new chapel pixel gate requires mean luminance at least 9.5 and at least 24% of pixels at value 10
  or higher. Two candidates failed at mean 8.9/9.0. The retained cold, bounded `CompositionFill`
  reaches the three-quarter facade and passes at logged mean 10.
- Fixed exposure remains -2.55 and authored post exposure remains 0. The global night was not lifted.
- `GmDisplayCalibration` gives normal players five persisted levels from -0.5 to +0.5 stops through
  Left/Right Arrow or controller D-pad from pause. Automated evidence forces in-memory level 0 and
  cannot overwrite the preference.

## Proof-quality pass

The first rebuilt player passed technically but was rejected on inspection: the controller target's
interaction text, triggered story cards, controls legend and wind toast remained over later scene
frames. The probe now clears only its transient instrumentation between the dedicated UI phase and
composition phase. Final native frames 02-07 are clean; controller frames 01-02 still prove cold-open
and pause/calibration UI.

## Final evidence

- Shared-source sync: **44/44**
- Repository: **23/23 C# rules**, **12/12 learning**, **10/10 scene system**, **4/4 runtime
  integrity**, **23/23 JavaScript rules**, **47/47 browser harness**
- Unity: **103/103 EditMode**, **5/5 PlayMode**
- Saved estate audit: two cold passes, `2c9c97b0db8dc80b`
- Structured report: **0 errors, 0 warnings, 0 findings**
- Pacing: **15/15** route/scenario rows
- Guarded variants: **6/6**, `selected=none`, exact composition restore at `460221080faabbff`
- HDRP editor tour: **18/18**, including chapel and figure perceptual gates
- Fresh macOS player: **7/7 clean scene frames**, **2/2 controller UI frames**, no UnityEditor
  assembly, **5/5 final audio clips decoded**, zero runtime or render-integrity findings
- Virtual controller: cold-open advance/skip, left-stick and D-pad movement, right-stick look,
  focused interaction, wind switching, brightness adjustment and pause/resume all passed
- Native 1280x720, 240 frames: mean **11.76ms**, p50 **10.46ms**, p95 **14.89ms**, p99
  **15.52ms**, max **15.61ms**
- Full archaeology runner: **9/9 gates green**

## Honest grade

| Area | Grade | Reason |
|---|---:|---|
| Scene engine | A+ | Reusable structure, intent, Terrain, display and runtime-integrity contracts |
| Verification | A+ | Editor, native, controller, audio, pacing, variants, logs and clean frames agree |
| Mansion and drive | A | Strong hierarchy, continuous ecology and readable arrival/threshold |
| Cemetery | A- candidate | Family plots and excavation read; repetition tolerance is still a taste call |
| Garden and service yards | A | Work chain, cultivation pattern, local light and ecology read coherently |
| Acreage | A- candidate | Continuous and layered; far darkness remains display-dependent |
| Vehicle presentation | A- candidate | Readable and integrated; generic modern model remains the weakest art fit |
| Audio | A provisional | Objective/provenance/runtime proof is strong; Nick's ears still decide character |
| Phase 0 overall | **A candidate** | No known objective B-grade defect remains; sensory and taste gates remain human |

This is not an A+ claim. Nick still must test a physical controller, judge level-0 darkness and the
calibration range on his display, listen to wind and the full Ninth Bell mix, decide whether the
figure is perceptible but deniable, and walk the uninterrupted 4m45 approach. Claude should attack
the candidate using `docs/CLAUDE-PHASE0-A-CANDIDATE-REVIEW-2026-07-22.md` before Court begins.

No purchase, commit or push was made. Purchased source assets remain untouched. Threshold Refusal
remains closed-door, no second mansion exists, and Court remains locked.
