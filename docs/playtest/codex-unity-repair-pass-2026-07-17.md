# Codex Unity repair pass — 2026-07-17

## Verdict

**B- / BORDERLINE PASS FOR NICK'S HUMAN REVIEW.** This is no longer the D+ asset-placement scene
from the adversarial review. It is ready for Nick to walk and judge. It is not a ship sign-off.

- Visual direction: **B-** (was D+)
- Functional/automation confidence: **A-**
- Exterior audio mix: **provisional C+** until heard in a real walk
- Buy call: **buy nothing yet**

The remaining visual ceiling is now concentrated in a few identifiable assets/compositions rather
than a scene-wide failure: the modern SUV, repeated cemetery stone module, sparse coach yard, close
garden readability, and uniform mansion window pattern.

## What changed after the failed review

- Replaced the cobalt physical sky with a controlled moonless HDRP gradient and ACES tonemapping.
- Added four non-colliding procedural terrain ridges outside the playable y=0 estate and lifted a
  dead-willow woodland onto them, breaking the ruler-straight horizon without changing story paths.
- Excluded both `/foliage/` and the pack's misspelled `/foilage/` tree paths. The final tour uses bare
  trunks/willows rather than the green shard-card trees called out in the review.
- Darkened and retiled mud/drive materials; narrowed/darkened the local fog layer.
- Moved/lowered/broke the estate fence run so it reads as a neglected boundary instead of editor
  breadcrumbs. Car and side-path openings remain clear.
- Authored the cemetery as a room: low dark stone boundary, west/east openings, cross-path, spine,
  grave clusters and perimeter scruff. Module repetition remains visible from some angles.
- Authored the kitchen garden: low fence, entry/work paths, six irregular soil rows, dead planting
  clusters and work debris. It needs a human darkness/readability judgment while moving.
- Corrected outbuilding fill lights against rendered bounds rather than unreliable imported pivots,
  then retuned them from the first overbright attempt to 12–24 lumens. Final tour luminance stays
  between 14 and 27 across all twelve frames.
- Reduced/saturated mansion window emission and added highlight rolloff. Windows now read amber
  instead of white; the shared pane material still makes the lighting pattern too uniform.
- Retinted the arrival SUV near-black oxblood with dark glass. This hides it better but does not make
  a modern SUV period-correct.
- Recognized the gate export's real `HDRP_WroughtIron` material. Rebuild now reports a real success
  instead of the old false `FAILED — 0 materials matched` line.

## Audio diagnosis and repair

The complaint that the background sounded like a spaceship was correct.

The old estate continuously stacked:

- `amb_wind` at 0.50;
- a synthetic/broadband `amb_dark` bed at 0.30; and
- a 3.326-second cricket clip looping at 0.26.

The active graph now has exactly one continuous exterior source: long wind modulated slowly between
roughly 0.07 and 0.12. The dark drone is not played outdoors. Crickets are a quiet intermittent
one-shot every 11–31 seconds; the owl is reduced and spaced 48–100 seconds apart. Story one-shots
reset pitch so wildlife randomization cannot corrupt a gate/bell/door cue.

The downloaded free **Horror Elements** package was inspected before import. Its ambience set includes
`Amb_Deep_space_1/2/3`, rumble, underwater, darkroom and other designed horror textures. That is useful
later for stingers/interiors, but it is the wrong source for a natural Wend Hill exterior. Do not buy
or import another generic horror bundle to solve this. If Nick still dislikes the quiet wind during
the walk, the correct future buy/search is a targeted natural night exterior field-recording pack
(wind through bare trees, sparse insects, distant birds), and it remains a Nick buy gate.

## Automation repairs

- Unity tour arming now survives Play-mode domain reload via a timestamped, one-shot editor preference
  that expires in five minutes. The saved scene remains `runOnPlay: 0`.
- Unity CLI failure matching now catches any `[Gm…] … FAILED` form, not only `[Gm…] FAILED:`.
- The browser harness prints scene/test progress and gives every test a named 30-second deadline.
- Headless runs unload each completed Three.js preview, releasing its RAF/WebGL load. Interactive use
  still keeps all live previews. This changed the harness from a 240-second partial timeout to a full
  47/47 pass in about one minute.
- Added Unity guards for GradientSky + ACES, four non-colliding backdrop ridges, composed cemetery/
  garden roots, and the one-loop/no-drone exterior audio graph.

## Verification

- Fresh Unity rebuild: **17 placed, 0 missing**; gate conversion **1 material force-set**; no build
  `FAILED` line.
- Final post-rebuild HDRP tour: **12/12 PNGs**, every frame nonblank, mean luminance **14–27**.
- Unity EditMode: **37/37 passed**.
- Browser live-scene harness: **47/47 passed**.
- C# Shut the Box parity: **23/23 passed**.
- JavaScript Shut the Box logic: **23/23 passed**.
- The foliage filter, fixed exposure, persistent volume overrides, walk bounds and no-built-in-shader
  conditions are covered by the real builder tests.

## Nick's pass should answer these, in order

1. Does the new sparse audio bed sound like wind/empty acreage rather than machinery?
2. Is the approach dark but navigable on Nick's monitor?
3. Do the cemetery and garden feel like places when walking, not just in fixed review shots?
4. Is the modern SUV acceptable for the protagonist, or does its silhouette break the estate's era?
5. Does 4m45s before the ninth toll feel tense or padded?

No purchase, commit, or push was made.
