# Phase 0 Natural-Estate Rebuild

> **Superseded on 2026-07-20.** The B+ / A- judgment below was withdrawn after an adversarial review
> of all native and editor frames. Current verdict: D+. See
> `docs/playtest/codex-phase0-adversarial-visual-reopen-2026-07-20.md`.

Date: 2026-07-20  
Status: automated candidate complete; Nick walk/listen required  
Visual grade: B+ / A- candidate  
Verification grade: A+

## Verdict

No new environment pack is needed for Wend Hill. The owned Horror Environments bundle already
contains a suitable terrain base, ground textures, vegetation and architectural reference. The
failure was composition and integration, not a missing purchase.

Nick's criticism was correct. The previous build passed technical gates but still read as a large
room with themed objects placed inside it. Terrain, road, foliage, cemetery and garden each existed,
but they did not form one continuous ecology at player height.

## What changed

- Wend Hill now uses a cloned Witch Village TerrainData asset. Purchased source assets remain
  untouched.
- The authored clone carries real relief and four painted layers for mud, wet wheel tracks, leaves
  and moss.
- Broad deterministic noise fields create macro ground regions instead of blending every surface
  almost uniformly.
- The road is a narrower 5 to 6 meter tread painted into the terrain. Broken twin track masks supply
  direction without repeating the pack's crosswise road photograph.
- Terrain relief resumes immediately outside the tread. The old semantic road renderer remains
  hidden.
- `EstateUnderstory` adds 695 owned grass clumps across twelve avenue communities, eight cemetery
  communities and six garden communities.
- Six owned grass source families vary height, width, depth and rotation. Identical dry foliage
  shares instanced HDRP materials rather than generating hundreds of one-off materials.
- The drive center, cemetery cross/spine and garden entry stay explicit negative space. Decorative
  colliders are removed.
- Cemetery vegetation now belongs to plot corners. Garden vegetation belongs to failed bed ends,
  fence breaks and shed margins. Neither room is filled like a uniform lawn.

## Rejected iteration

A denser middle and distant woodland was tested. The mid-drive frame became a tree wall and failed
the horizon occupancy gate at 91 percent against an 85 percent maximum. The density change was
reverted. The acceptance threshold was not weakened.

A warmer lighting experiment was also reviewed and rejected because it collapsed the blue and amber
night contrast into sepia. The cooler moon, fog and practical-light relationship was restored.

## Final evidence

- Estate audit: PASS
- Scene fingerprint: `ba3c79d6929fde11`
- Scene inventory: 6,810 objects, 4,638 renderers, 281 colliders, 21 lights
- EditMode: 94/94
- PlayMode: 5/5
- HDRP review tour: 18/18
- Built-player composition frames: 7/7
- Built-player controller frames: 2/2
- Native runtime errors: 0
- Performance: 15.03ms mean, 15.97ms p50, 18.55ms p95, 18.94ms p99, 19.43ms max
- All five Ninth Bell clips decode in the built app
- Virtual gamepad proof covers cold open, movement, look, interaction, wind comparison and pause

## Honest remaining grade cap

This is no longer the same bare-room scene, but automated proof does not make it A+ artwork.

- The mansion's centered symmetry remains visually rigid, even though it supports the narrative.
- Two safe bare-tree source families still make some woodland silhouettes repeat.
- Some side-acreage views remain sparse.
- The garden remains the weakest visual room and may still feel staged.
- Locked night exposure makes the new macro terrain variation deliberately subtle.
- Review/debug overlays obscure parts of the native proof frames.
- Figure visibility, wind character and the 4 minute 45 second approach need human eyes and ears.

Court remains locked. Nick must play the exported build and accept the opening before Phase 1 work
begins.
