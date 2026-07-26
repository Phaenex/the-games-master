# Wend Hill Village: lighting and atmosphere pass

> **⚠ SUPERSEDED 2026-07-25 → `docs/WEND-NIGHT-LADDER.md`.**
> This pass authored a night look on Haunted Village, which ships **zero practical lights**, so every
> lamp in it had to be invented. That is why it took four attempts and why the result kept reading
> wrong. The work restarted on the purchased Abandoned Village (24 practical lights, 22 lamp props)
> with a render-every-change ladder. The findings below about fog albedo, `affectsVolumetric` and the
> EV sign are still true and still worth reading. The scene, the builder and the numbers are not
> current: `WendHillVillage.unity` and `GmVillageBuilder` are retired in favour of
> `WendHill_Prologue.unity` and `GmWendBuilder`.

Written overnight 2026-07-24/25. Everything here is in the village scene
(`Assets/Scenes/WendHillVillage.unity`), built by `GmVillageBuilder`. Nothing is committed.

## The problem this pass was fixing

The previous look got rejected as "super over exposed and bright and just looks off instead of
creepy atmosphere", with the fog specifically suspected.

The fog suspicion was right, and it was the main cause. Three things were lifting the whole frame:

1. **The moon was lighting the volumetric fog.** `affectsVolumetric` was on for the directional
   moonlight, so the fog scattered moonlight and became a light source in its own right. Distance
   turned into glare instead of into concealment. That is the thing that made it read wrong.
2. **Fog albedo was too bright.** Even without the moon, a bright albedo makes fog scatter rather
   than absorb.
3. **Fill light was cancelling the shadows.** 0.9 lux from an opposing angle removed the moon's
   shadow rake, so nothing in frame had real black in it.

Exposure was a factor but a smaller one than it looked. Worth knowing: the rejected look and the
current one are BOTH at EV -7.0. Same number, and the current frame measures about eight times
darker, because the glow was coming from the fog, not the stop.

## What changed

**Lighting recipe** (`GmVillageNightRecipe.Base()`)

| | before | after |
|---|---|---|
| exposure EV | -7.0 | -7.0 |
| moon | 5.5 lux, volumetric ON | 4.6 lux, volumetric OFF |
| fill | 0.9 lux | 0.35 lux |
| fog albedo | 0.050, 0.056, 0.075 | 0.028, 0.032, 0.044 |
| fog mean free path | 78m | 62m |
| contrast / saturation | 6 / -5 | 11 / -9 |
| vignette / bloom / AO | 0.12 / 0.10 / 0.50 | 0.26 / 0.06 / 0.62 |
| practicals | 110 lm, 14m range | 74 lm, 11m range, deeper amber |
| gate lanterns | 45 lm | 26 lm |

Mean frame luminance at the village vantage went from 0.211 to about 0.027.

**New: star sky.** The sky was a `GradientSky`, three colours blended vertically. Fine as an ambient
source and dead to look at, which matters in a game whose whole prologue is outdoors at night with
the player repeatedly looking up at a roofline, a church tower and a bell. `GmVillageSky` now bakes a
2048x1024 equirectangular starfield (5200 stars, brightness distribution weighted so most are faint
and a few are genuinely bright, plus a faint tilted galactic band) and imports it as a latlong
cubemap driving an `HDRISky`. Deterministic seed, so review frames stay comparable between runs.

Star directions are sampled uniformly on the sphere and then converted to UV. Picking UV uniformly
instead piles thousands of stars into the poles, which is the usual giveaway of a faked procedural
sky.

**New: ground mist.** The global fog override is uniform, so it thins everything equally with
distance and reads as haze rather than weather. Eight `LocalVolumetricFog` volumes now lie along the
walked road at knee height, denser than the global fog, with soft fades on every axis so walking into
one is a gradient and not a pop. Real night fog pools in hollows and along roads and stops at your
waist, and that is what gives a night scene depth.

**New: fog quality.** These were sitting at defaults. `anisotropy` 0.62 scatters light forward, so a
lamp seen through mist gets a directional halo instead of an even glow, which is most of what makes
fog read as air. `depthExtent` raised to 110m because the default 64m stopped volumetric fog well
short of the 180m walk, leaving the far half of the road on flat distance fog. Also
`sliceDistributionUniformity` and `multipleScatteringIntensity`.

The HDRP pipeline asset itself was deliberately NOT edited. It is purchased Leartes content shared
with other scenes.

**New: motion.** Two components existed in the project and were unused in the village.
`GmLightFlicker` now drives every practical, seeded off world position so no two windows breathe in
sync (synchronised flicker is worse than none, it announces itself). `GmCanopySway` moves 61 trees
near the road by a fraction of a degree at the root, which is centimetres at crown height. Enough
that the treeline is never quite still in peripheral vision without anything reading as animation.
Scoped to trees near the walked road, since sub-degree motion is invisible on a distant fogged
silhouette and would be pure cost.

## Bugs found and fixed on the way

**`GmRareEvents.FigureGoneBelowZ` was a hardcoded estate coordinate.** Same class of bug as the bell
tolling from an empty field. It decides when the mansion window figure disappears, compared against
player world Z, and estate z=+18 sits off the north end of the entire village map. The figure would
have vanished the instant it armed. Now remapped through `GmVillageEstate.EstateZToVillageZ` to
-99.3, with the estate const kept intact so existing tests and `GmShotTour` still compile.

**The window figure was never appearing at all.** The rig gets parented under the estate root so it
travels with the mansion when the whole assembly is moved into the village. But `GmRareEvents` finds
it with `FindSceneRoot("WindowFigureRig")`, which only searches scene ROOTS, so as a child it was
invisible to the system that owns it. The built app was logging "WindowFigureRig missing, one-in-three
figure disabled" every run. It is now detached back to a scene root after the move, with
`SetParent(null, true)` preserving the world transform it just inherited.

**A crashed build silently left the wrong scene on disk.** The Burst compiler crashed mid-build once.
`GmVillageBuilder` starts by deleting the village scene and copying the purchased Showcase scene over
it, so a crash between that and the final save leaves a pristine copy of the purchased showcase: no
lighting, no estate, no systems, no player. It opens fine, it builds fine, and the app is silently
the wrong game. The mtime does not even give it away, because `CopyAsset` inherits the source file's
timestamp (which is how it was spotted: the scene claimed to be from Jul 20).

`GmVillageStandaloneBuild` now refuses to build unless the scene contains the five roots the builder
is contractually required to leave behind. A five-minute build of the wrong thing became an immediate
specific error. The deterministic full-regeneration design is what made the crash recoverable at all:
re-running the builder restored everything exactly.

## What to look at

- `Screens/Review/bracket-A-hero.png` and `bracket-C-eye.png` and `bracket-D-mansion-approach.png`
  are exposure contact sheets. Six exposures, left to right then top to bottom, EV -6.1 (darkest)
  through -7.6 (lightest). Everything else is identical across all six. Current default is -7.0,
  which is bottom-left.
- `Screens/ExposureBracket/` holds all 54 full-size frames if you want to look closely.
- `Screens/Video/wend-hill-village-walk.mp4` is the walk.

If the exposure is wrong for you, it is one constant: `exposureEV` in `GmVillageNightRecipe.Base()`.
Pick a frame from the bracket and I will set it.

## Still open

- No perf or culling pass yet, and no frame-time measurement on the dense scene. That is M4.
- The save system writes correctly but the quit-and-resume restore path has not been round-tripped.
- Ambient audio (`GmAmbience`) is attached and the clip library is there (crickets, owl, wind,
  ear-whine, per-surface footsteps) but I have not verified by ear what actually plays during a walk.
