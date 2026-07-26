# Codex Adversarial Review — Wend Hill Prologue

Date: 2026-07-17  
Reviewer: Codex  
Scope: fresh `rebuild` → fresh 12-shot post-rebuild `tour` → EditMode tests → combined web tests → static regression checks.

## Bottom line

**Grade: D+** for “would a player believe this is a real horror game's opening?”

It is a functioning, textured prototype with a readable route and recognizable landmarks. It is not a convincing shipped horror opening. The cobalt-blue sky, flat black horizon, empty acreage, fence spam, low-poly leafy shard trees, clean modern SUV, uniformly overexposed mansion windows, and weak terrain composition make it read like an asset-placement test scene.

## The 12 shots

1. **01 spawn — FAIL:** the destination reads, but the black gate slabs, enormous foreground trunk, blue sky, flat horizon, and empty flanks announce “prototype,” not horror.
2. **02 car — FAIL:** the car clears the fence, but a spotless modern white SUV dropped into a period-horror estate destroys the fiction immediately.
3. **03 gate — FAIL:** the framing is usable, but the gate is a featureless black grid swallowing the frame; it does not read as authored wrought iron.
4. **04 lookback — FAIL:** the car is visible, but the ruler-flat horizon and mostly vacant terrain make the estate feel unfinished rather than isolated.
5. **05 middrive — FAIL:** this should be the money shot; instead it shows jagged leafy trees, repeated fence segments, obvious empty planes, blue dusk, fog blanket, and a glowing dollhouse.
6. **06 cemetery path — BORDERLINE/FAIL:** it unmistakably reads as a graveyard, but the crosses look scattered, the chapel crushes dark, and a giant block-leaf tree dominates the composition.
7. **07 cemetery inside — FAIL:** grave props and an open grave exist, but the flat ground, thin dressing, even spacing, and empty horizon prevent a believable burial ground.
8. **08 chapel — FAIL:** the camera is indeed nose-to-wall, but that does not excuse the nearly black material response; both the framing and lighting fail the review purpose.
9. **09 garden — FAIL:** a scarecrow, well, and shed are not enough; it reads as three props on an empty plane, not a ruined garden.
10. **10 well/shed — FAIL:** the shed is legible, but the well is chalk-bright, the staging is vacant, and the horizon line remains brutally artificial.
11. **11 coach yard — FAIL:** texture is visible, but the camera presents a dark wall, trough, and empty dirt; it looks like an asset inspection frame.
12. **12 porch — FAIL:** the mansion is upright and textured, but the facade and every window are blasted white against a cobalt sky; it resembles a lit dollhouse/exposure test, not a threatening threshold.

## Claims graded

1. **Gate dark wrought iron — PARTIAL.** It is dark, not white plastic. Visually it reads as a black block/grid rather than wrought iron. Rebuild also logs `graveyard_gate: FAILED — 0 materials matched` because `HDRP_WroughtIron` is not recognized by the converter; the CLI failure matcher misses this form.
2. **Clean tiling mud ground; no atlas patches or giant leaf planes — CONFIRMED, with a quality reservation.** No hard-edged atlas/z-fighting leaf sheets are visible. The replacement is extremely flat and repetitive.
3. **Tiling muddy road; no 142-unit smear — CONFIRMED, with a quality reservation.** The log selects `T_Muddy_Road_01_Albedo`, and the road no longer looks like one stretched bitmap. Repetition and weak edge blending are obvious.
4. **Cemetery reads as graveyard with ~29 crosses/open grave — CONFIRMED literally.** Rebuild reports 29 crosses and the shots read as a graveyard. It still looks like props scattered across a level plane rather than a composed cemetery.
5. **Bare/dead tree avenue; no blocky green-leaf shard trees — FALSE.** Several prominent trees retain coarse polygon leaves. The largest examples dominate shots 05 and 06 and are exactly the alleged failure mode.
6. **Chapel, coach house, shed not crushed black — PARTIAL.** Shed and coach house retain some texture. Chapel shot mean luminance is 23 and the facade is almost black; the close camera worsens but does not create the underlying lighting failure.
7. **Night dark/moody, not washed gray — FALSE.** The sky is saturated cobalt blue, open ground is blue-gray/lavender, and the horizon is a hard black band. It reads as unfinished dusk lighting, not night.
8. **Mansion textured, lit, upright, sealed — PARTIAL.** Upright and textured are confirmed. Doors appear sealed. Lighting is not successful: nearly every window and the entrance clip to white, flattening the facade and removing menace.
9. **Car clear of fence — CONFIRMED.** It does not visibly intersect the fence. The vehicle itself is a fiction-breaking modern white SUV and remains a major art problem beyond the narrow claim.
10. **Ground mist rolls low — PARTIAL.** A low fog layer is clearly visible and rebuild logs a volumetric noise box. In stills it reads as a uniform gray blanket; the “rolls” claim was not demonstrated by this tour.
11. **All tests pass — PARTIAL.** Unity EditMode: **34/34**. C# parity: **23/23**. JS logic: **23/23**. Browser harness: **only 4/47 completed**, then timed out after 240 seconds while stuck after Prologue. Therefore the advertised full suite did not pass.
12. **No magenta — CONFIRMED.** Fresh successful rebuild has `EXPECT MAGENTA` count 0 and none of the 12 fresh shots contains a magenta surface.

## Static regression checks

- Placements: **17 placed, 0 missing** — confirmed.
- WitchVillage untouched: **141** missing-shader materials — confirmed near the expected count.
- Exposure: `NightExposureEV = -3f` — unchanged.
- Moon: `MoonLux = 1.7f` — unchanged.
- Temp editor scripts: **0** `Assets/Editor/GmTemp*.cs` — confirmed.
- Tour modal: no rebuild-Library dialog appeared. However the first cold rebuild hit Package Manager IPC trouble and later launches spent up to ~193 seconds initializing.
- Rebuild/tour CLI reliability: weak. The review exposed early-return/orphan-process behavior under the command runner, and the rebuild success parser does not treat `[GmHdrpConvert] graveyard_gate: FAILED` as failure.

## Ranked problems and specific fixes

1. **Replace the blue “night” and hard horizon.** Art-direct HDRP sky, fog, and exposure together. Use a near-black desaturated sky, moon direction, height/volumetric fog, and distant terrain/treeline silhouettes that occlude the horizon seam. EV -3 is not sacred; tune from a calibrated display with histogram and human eyes.
2. **Build terrain, not a plane.** Add shallow grade changes, road crown/ruts, drainage ditches, berms, cemetery mound variation, garden walls/hedges, and terrain blending. The current flatness makes every prop placement look synthetic.
3. **Redo mansion lighting.** Light perhaps 20–35% of windows, vary temperature/intensity, keep most panes dark, prevent clipping, add controlled porch pools and side/rim light, and let the central door remain readable without glowing white.
4. **Remove the modern SUV.** Replace it with a period-appropriate car or hide the vehicle beyond the gate until a correct asset exists. Its current shape and showroom-white material sabotage the premise.
5. **Purge the shard-leaf trees.** Enforce the bare-tree filter on rendered output, not prefab naming. Reject coarse leaf-card LODs at review distance. Use fewer, better hero silhouettes and clustered distant woodland instead of evenly scattered specimens.
6. **Fix the gate as an object, not merely a color.** Resolve the converter mismatch, make the CLI fail on the logged conversion failure, verify material breakup/specular response, and frame the gate so its iron detail is visible instead of solid black.
7. **Compose the cemetery and garden.** Create paths, clusters, age hierarchy, sinking/leaning graves, vegetation pockets, retaining edges, and focal lighting. The garden needs a recognizable boundary and cultivated remnants; the cemetery needs believable rows disrupted by age, not random scatter.
8. **Break fence repetition.** Use continuous logical boundaries, damaged sections with structural cause, vegetation at transitions, and fewer identical panels. The current fence corridor resembles level-blockout breadcrumbs.
9. **Improve outbuilding lighting and cameras.** Chapel needs readable shadow detail; well stone needs less fill/albedo; tour frames 08 and 11 must present whole structures and context.
10. **Harden automation.** Make `unity-cli.mjs` surface child lifecycle correctly in every runner, match any `[Gm*] ... FAILED`, report cold-start duration, and make the browser harness identify the exact scene/test that blocks rather than waiting 240 seconds with only `RUNNING`.

## Commit statement

No code was changed and nothing was committed or pushed. This file is the review deliverable; fresh Unity scene/log/screenshot artifacts were produced by the requested audit commands.

