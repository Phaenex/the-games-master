# Wend Hill night: the ladder restart, and the bug that was making everything else unreadable

Written 2026-07-25. Everything here is in `Assets/Scenes/WendHill_Prologue.unity`, built by
`GmWendBuilder` from the purchased Abandoned Village. The sources are now committed, under
`unity/scenes/wend-hill-prologue/` in the games-master repo; the scene itself is a generated artifact
and deliberately is not.

Supersedes `docs/VILLAGE-ATMOSPHERE-PASS.md` for the night look. That pass is still worth reading for
fog albedo, `affectsVolumetric` and the EV sign, but its scene and its numbers are retired.

## Why the restart

The previous four attempts were authoring a night on Haunted Village. Counted, not guessed:

| | Haunted (old) | Abandoned (now) |
|---|---|---|
| buildings | 7 | 38 |
| road meshes | 13 | 57 |
| practical lights | 0 | 24 |
| lamp / fire props | 0 | 22 |

Haunted Village ships zero practical lights. That single number is most of the postmortem. Every lamp
in a night scene had to be invented, so every judgement after step one was about lighting that had been
written rather than lighting the pack shipped, and it went wrong four different ways: black, washed
out, too dark, blown white.

Abandoned Village was already lit by its artist. The job became moving it to night instead of
inventing it, and the rule in `GmWendBuilder` is now explicit: never delete a light or a volume the
pack shipped, edit them.

## The harness

`GmWendLadder` renders the conversion as a cumulative ladder, one rung per individual change, two
opposite camera directions per rung. Each rung rebuilds from the purchased source first, so rung N is
exactly "the shipped scene plus the first N changes" and not "whatever rung N-1 left behind". Every
rung asserts the lighting census, currently 1 directional, 24 practical, 30 volumes. A step that
changes those counts is a bug, not a lighting choice.

Rungs 0 through 2 have now reproduced their luma to three decimals across three separate runs, so the
rebuild is genuinely deterministic and the frames are comparable between sessions.

The reason it exists: the previous attempt moved six lighting values at once and could not attribute
any result to any cause. With a ladder, the culprit for a change is whatever moved on that rung.

## What R4 actually was

The frames were correct everywhere except the foliage, which glowed white-green while the rest of the
scene was properly black. This was originally diagnosed as an unregistered HDRP diffusion profile,
because the grass materials are `_MaterialID: 1` with `_TransmissionEnable: 1`. That diagnosis was
wrong, and worth recording as wrong: those materials reference no diffusion profile at all, and
`_RequireSplitLighting` is 0, so subsurface scattering was never in the path.

The real cause is an ungated emission term. `S_Wind.shadergraph` multiplies `_Emmisive` (a texture) by
`_Emmisive_Tint` and `_Emmisive_Intensity` straight into Emission with no on/off switch, and on all
four materials that use it the texture slot is empty, so it samples white and the whole blade emits its
tint:

| material | intensity | tint | reaches the scene as |
|---|---|---|---|
| `M_grass` | 2.21 | 0.96, 1.00, 0.72 | 640 renderers + 2 tree prototypes |
| `M_grass 1` | 3.93 | | 2 tree prototypes |
| `M_grass 2` | 2.60 | | 88 renderers |
| `M_Leaf` | 2.78 | 0.38, 0.42, 0.00 | `SM_Tree_02 Foilage` prototype |

Under the pack's own 2000 lux daylight that is a rounding error, which is why the artist never saw it.

The colour is what proves it, not the brightness. The only light in the scene is a blue moon at
0.62, 0.70, 0.92. The pre-fix canopy measured 44, 54, 6, with essentially no blue in it at all.
Scattered or transmitted moonlight cannot come out warmer and greener than the moon. The grass
measured 215, 220, 188, a ratio of 0.98 : 1 : 0.86, which is `M_grass`'s own emissive tint.

After the fix, at the same EV and the same camera, the colour changes hands:

| EV -2, mean of region | pre-fix | post-fix |
|---|---|---|
| foliage | 194, 189, 168 | 62, 50, 39 |
| canopy | 114, 119, **97** | 48, 45, **51** |
| leaf clusters | 197, 202, 180 | 96, 94, 97 |

Green-dominant becomes blue-dominant. The upper half of the frame is lit by a blue moon now instead of
glowing green. That is a change of mechanism, not a change of brightness, and it is the reason to
believe the fix rather than just prefer the picture.

## The part that mattered more than the grass

Turning the emission off dropped mean luma from 0.525 to 0.059 at auto exposure. One change, nine
times darker.

Which explains a result from earlier in the session that had been filed as "auto exposure
compensates". The sun was cut from 2000 lux to 3.4, a factor of 588, and mean luma moved from 0.438 to
0.435. The real reason is worse than compensation: HDRP's automatic exposure was metering the self-lit
grass, and emission does not care what the sun is doing. The scene's exposure was being set by a bug,
so no amount of dimming the sun could have moved it.

Any exposure decision made before this was measuring the glow. The first bracket ran
{ 2, 0, -2, -4, -6 } and read 0.096 / 0.229 / 0.560 / 0.877 / 0.985, which said EV +2 was the night
register. With the emission off the same five values read 0.001 / 0.016 / 0.199 / 0.652 / 0.915. EV +2
is now pure black. A bracket is only as good as the frame it meters.

## ⚠ EV -1.0 IS VOID, AND SO IS THE SECTION BELOW

Read this before the bracket table. Two things found later in the same day invalidated it.

**It was metered outside the village.** The spawn was the centroid of 57 scattered road meshes, which put
it on a hillside away from the lamps. EV -1.0 was correct there, and HDRP's automatic exposure agreed with
it there to three decimals, which is exactly how it survived review. On the actual village street the same
fixed exposure measured 0.343 to 0.881 across a 188m walk, against a 0.03 to 0.06 target. Six to fifteen
times too bright.

**The scene's light levels were still daylight levels.** The moon was cut from 2000 lux to 3.4, a factor
of 588, and nothing else was touched: twelve point lights at 1549 lumens, four at 3173 to 3725, and two
spots at 6957 and 62903. A 3.4 lux sky and a 63000 lumen spotlight cannot share an exposure. That is why
three separate brackets each worked at one vantage and failed everywhere else.

The practicals now scale to night levels on their own ladder rung, 104907 lumens down to 2475, and the lamp
glass drops from 21.95 to 1.5 the way the grass dropped from 2.21 to 0. The moon is 0.5 lux, because
Unity's own reference puts clear-night moonlight under 1 lux and 3.4 was never physical either.

The lesson, stated plainly because it cost a day: **two agreeing measurements of the wrong vantage agree
perfectly and tell you nothing.** The bracket method was sound every time. What was never checked was
whether the place being metered was the place the game happens, or whether the lights in it were night
lights.

## The exposure, re-bracketed (SUPERSEDED, see above)

Half stops across the window the coarse pass had narrowed to:

| EV | luma 000 | luma 180 | reads as |
|---|---|---|---|
| 0.0 | 0.016 | 0.020 | only the lamp line survives |
| -0.5 | 0.031 | 0.039 | night, concealing, hillside barely present |
| **-1.0** | **0.059** | **0.074** | night, hillside legible, road readable |
| -1.5 | 0.111 | 0.136 | distant hill going pale, depth flattening |
| -2.0 | 0.198 | 0.245 | closer to dusk than night |

**EV -1.0 is the committed value**, picked by Nick on 2026-07-25, and it is defensible rather than a
taste call. HDRP's own automatic exposure independently settled the corrected scene at 0.059 / 0.074,
which a fixed EV -1.0 matches to three decimals in both directions. So it is the engine's own metering
of the fixed scene, made deterministic. Automatic exposure is not shippable here, because re-metering
around anything bright is precisely what hid the foliage bug for four attempts.

Highlight headroom at the committed value: 0.000% of pixels fully clipped front, 0.006% rear, and that
0.006% is the lamp filaments. So the leaf clipping that was open at EV -2 does not exist at EV -1.0. It
was two stops of exposure, not a diffusion profile, and there is nothing there to fix.

Above EV -1.5 the distant rocky hill lifts to a pale grey and takes the depth with it.

One thing to look at before committing either: the pack's lamp line reads as a broad warm band across
the field at every exposure in the bracket, and by EV -1.5 it starts to look like a flow of light
rather than a row of lamps. That is 21 lamp posts at emissive 21.95 plus their practical lights. It is
the pack's intentional lit path and it is deliberately untouched, but it is the loudest feature in the
frame and it may want its own rung.

## What changed

New file `Assets/Editor/GmWendFoliage.cs`. Two entry points, `Survey` which only looks, and `Apply`
which is now rung 3 of the ladder, sitting before the exposure bracket because emission does not care
what the exposure is.

Purchased assets are never edited. Affected materials are copied into
`Assets/Scenes/WendHill_Night/` with both emissive knobs zeroed, and the scene is repointed, the same
way `GmWendNight` already owns the volume profile. Both knobs rather than just the intensity, because
the graph's wiring is not worth trusting on read: if emission is a product then either one is enough,
and if it is a lerp toward the tint then only the tint kills it.

Two routes needed repointing, and missing either would have left half the frame glowing:

1. **Scene renderers.** 640 of them on `M_grass`, 88 on `M_grass 2`.
2. **Terrain tree prototypes.** 5 of the 7. This is where all of `M_grass 1` and all of `M_Leaf` live,
   which is to say the glowing canopy was never reachable by repointing renderers.

The terrain's `TerrainData` is embedded in the scene rather than being a separate purchased asset, so
repointing its prototypes only ever writes to our own scene copy. The prototype prefabs themselves are
purchased, so those get owned copies too, and prototype order and count are preserved because every
placed tree refers to its prototype by index. Verified after the fact: the owned prefabs have the same
GameObject, MeshRenderer, MeshFilter and LODGroup counts as the purchased originals, so no vegetation
moved or changed shape.

Deliberately left alone: the pack's other emissive shader has an `_Emissive_1` on/off float, and its 21
lamp posts set it to 1 at intensity 21.95. Those are meant to glow. Every other gated material in the
pack has the switch at 0 and is inert, and no material in the pack has a non-black `_EmissiveColor`, so
the ungated `S_Wind` family was the entire problem.

`Apply` returns a count and rung 3 throws if it is zero. Silently finding nothing is the failure that
would matter, because the frame would still glow and the cause would look like something else. It also
refuses to reuse an owned prototype prefab that still contains an emitter, since a stale asset that
looks fine has already cost this project one build.

## Committing it, and the build path

The ladder deliberately never saves, because every rung has to start from the purchased source. That is
right for review and useless for shipping, and it is easy to mistake a good ladder frame for a scene
that is night on disk. Four things now close that gap.

**`GmWendNight.BuildCommittedNight`** (menu Wend/3) builds the base, applies the whole night at the
committed EV, saves the scene, then goes through an empty scene and reopens from disk before auditing.
The trip through an empty scene matters: reopening a path that is already open is not a reliable reload,
and a reload that did not really happen would audit memory and pass for the wrong reason.

**`GmWendSceneContract`** audits the night by value, not by root objects. The existing village guard
checks that required roots are present, which was right for its failure, a crashed builder leaving a raw
copy of the purchased showcase. It does not transfer here. This night is made of values: the pack's own
sun re-aimed, the pack's own profile retinted, the pack's own materials repointed. A crashed run leaves a
scene with every root the pack ships, which opens fine, builds fine, and is broad daylight. Six checks:
player and eye, census, moon in lux at the recipe's value, the owned profile actually in use, a Fixed
exposure at the committed EV, and zero remaining foliage emitters.

That last check is the one that matters most, and it passed after a real reload, which is the evidence
that the renderer overrides and the terrain prototype swaps genuinely serialized into the scene file
rather than living in memory. Check 6 shares its detection code with the fix itself rather than
reimplementing it, so the guard cannot drift away from what it guards.

**`GmWendStandaloneBuild`** (menu Wend/4) builds a macOS app from that scene alone, to
`Builds/macOS-Wend/`, which cannot collide with the real game or the retired village walk. The contract
runs BEFORE the build, because the failure being prevented is a five minute build of the wrong thing.

**The scene is registered.** `unity/scene-system/scene-registry.json` now carries `wend-hill-prologue`
with its build, audit and tour methods, so the existing CLI can drive it instead of it being a pile of
menu items. Registering it required renaming the review output from `Screens/Wend` to
`Screens/WendHill_Prologue`, because unity-cli derives a tour's screenshot directory from the registry
`sceneName`, and a mismatch there means the CLI polls an empty folder until it times out.

`GmVillageStandaloneBuild` is marked retired in its header. It still builds the Haunted Village scene,
and an out of date build entry point is the same "silently the wrong game" hazard its own guard was
written to prevent, one level up.

## The built app rendered the wrong camera, and no editor check could have caught it

The first prologue app built clean, passed its contract, and rendered the wrong thing. Whole-frame luma
0.477 against the editor's 0.026 to 0.074 at the same committed EV, with foreground grass clipping at
255,255,255. It looked like the night had broken.

The night had not broken. The sky was still dark in that frame, at 57,49,48, so the fog and sky retint
were applied, and the Exposure override on the profile was verifiably correct: active, Fixed, EV -1,
with both parameter override states set. What was wrong was the camera.

The purchased scene ships **thirty cameras**, a showcase camera plus animation rigs, and it leaves
`Camera06` enabled and active at depth 0. A new camera inherits depth 0. So the built scene had two live
cameras at the same depth, both rendering, and whichever drew last owned the backbuffer. The app was
rendering the pack's composed beauty shot of a lamp-lit street, at close range to a 21.95 emissive lamp,
which is why the foreground grass blew out.

Why no editor frame showed it: every review harness in this project renders a camera it CREATES and
points by hand, `GmWendLadderRig` and `GmWendReviewRig`. Not one editor frame in the entire session went
through a scene camera. The editor and the player were never rendering the same thing, and only the
player's opinion counts.

Why the contract did not catch it: check 1 verified that `PlayerCamera` EXISTS, which it did. Presence is
not the same as being the camera that renders. That is now check 6, which requires exactly one live camera
in the scene and requires it to hang under the player.

`GmWendBuilder.SoloPlayerCamera` disables every other camera, 29 of them, and gives the player's camera an
explicit depth of 10 rather than letting it inherit 0 and depend on being the only one left.

The general lesson, which is the reason this section exists: a build that compiles, passes a guard and
produces a bundle has demonstrated nothing about what it renders. The app has to be launched and the
frame has to be looked at. This one was signed off before that happened, and it was wrong.

After the fix, the player's own backbuffer reads whole-frame luma 0.059 against the editor's 0.059, a
ratio of 1.01, with band ratios from 0.88 to 1.08. Before it, the same measurement read 0.477.

## An open anomaly in the review rig, written down rather than smoothed over

The review of the saved scene reads mean luma 0.026 on the player's forward look, where the ladder reads
0.059 at the same EV from the same position and rotation in the same scene. The rear look agrees between
the two harnesses to within about 1% on per-band means. One view off by 2.3x while another agrees is not
settling noise.

One hypothesis was tried and disproven. HDRP volumetric fog accumulates temporally per render, and this
rig renders a disabled camera by hand at sixteen passes per shot, so the first shot of a run has far less
history than the third. Raising the first shot to 80 passes changed the reading not at all, 0.026 both
times, so accumulated render history is not the cause. The change was reverted rather than left in,
because code that claims a fix it does not deliver is worse than no code. The 0.026 does reproduce across
runs while the pixels differ slightly, which fits `S_Wind` animating the grass on shader time.

What it does not mean: this is a property of the rig, not the scene. The saved scene passes all six
contract checks after a genuine reload, and its other seven vantages land where the bracket predicts.
What is genuinely unknown is which of the two forward readings is truthful, because the ladder's forward
frame also came seven rungs into a warm session. So judge the forward look from a shot that is not first
in its run, and do not quote `eye-000` alone until someone finds the cause.

Correcting an overstatement made while chasing this: the warm frames were described mid-session as
bit-identical between the two harnesses. They are not. Their per-band means agree to a ratio of 1.00,
which is close agreement and not the same claim. No two renders of this scene are byte-equal, because the
grass moves.

## What to look at

- `Screens/Review/wend-fine-bracket-000.png` and `-180.png` are the exposure contact sheets, five
  frames each with the EV and luma burned in.
- `Screens/WendLadder/` is the current run, 9 rungs, two directions each.
- `Screens/WendLadder-preFoliageFix/` is the glowing version, kept for comparison. Same EVs, offset by
  one rung: pre-fix step3 pairs with post-fix step4.
- `Screens/WendLadder-coarseEV/` is the first post-fix bracket, { 2, 0, -2, -4, -6 }.
- `Screens/WendHill_Prologue/` is the committed scene, rendered from the SAVED file at EV -1.0: the
  player's four cardinal looks and four road vantages. Read the caveat about `eye-000` above.
- `Builds/macOS-Wend/Wend Hill Prologue.app` is the app. Unlaunched, see Still open.

## The scene was mixing its audio from the wrong place, and it is the camera bug again

Added later on 2026-07-25, while closing the map edge.

`GmWendBuilder.SoloPlayerCamera` disables the Camera COMPONENT on the pack's other 29 cameras. An
`AudioListener` on that same GameObject is a different component, and nothing was touching it. So the
camera fix ran, reported success, and left the scene with **30 live AudioListeners**. Measured, not
guessed: the build now prints `29 pack listener(s) disabled`.

Unity picks one listener when several are enabled and warns about the rest. Which one it picks is not
something to rely on, so the prologue's audio was being mixed from a showcase rig parked somewhere in
the village while every frame still rendered correctly from the player's eye. Exactly the shape of the
camera failure: presence is not the same as being the one that counts, the editor cannot show it, and
it survived the fix written for its twin one component over.

`SoloPlayerListener` disables the others, and contract check 9 requires exactly one live listener under
the player. Nothing has been verified BY EAR, and this does not change that. It fixes which ear the mix
comes from; whether the scene sounds like anything is still untested.

## The map edge is closed

Walls plus a failsafe, which is the shape Nick picked over a bare kill plane. `GmWendBounds` derives the
playable box from the terrain every build and raises four invisible box colliders around it, each side
overhanging by one wall thickness so the corners seal. Measured on the current scene: 4 walls around
315x378m, catch height -102, which is 30m under the terrain's lowest point.

`GmWendCatchPlane` is the failsafe under that, and it compares a float every frame rather than using a
trigger volume, because a falling body accelerates and a trigger thin enough to place is thin enough to
tunnel through. When it fires it recovers the player to the nearest route waypoint in XZ, measured from
the last place the ground actually held them rather than from the bottom of the fall, and it logs an
error. The loud log is deliberate: the walk probe reports a fall as a defect and standalone-proof fails
on any runtime error, so a hole in the boundary cannot be quietly rescued into looking like a clean run.

## The NavMesh, and where the bake does not go

`com.unity.ai.navigation` was not in the project. It is now, at **2.0.13**, which is the version this
editor's own package manifest pins for 6000.5.3f1. 2.0.8 was tried first and fails to compile against
this editor, because it calls `Object.GetInstanceID()` which is now obsolete-as-error. The runtime query
API was always present via `com.unity.modules.ai`; only baking needed the package.

`GmWendNavMesh` bakes a corridor derived from the route rather than the whole map. Measured:
**67279m^2, 7999 triangles, over a 210x331m corridor from 11 waypoints**, at agent radius 0.35 to match
the player's CharacterController. A mesh baked for a narrower agent than the body that walks it is worse
than none, because it returns paths through gaps the controller cannot fit and the player jams while
standing on a route that claims to be clear.

The bake is called from `ApplyCommitted` and deliberately NOT from `GmWendBuilder.BuildBase`, because
the ladder calls BuildBase once per rung, ten times a run, to render stills from a rig that never paths
anywhere. It would be ten bakes for nothing.

`GmWendWalkProbe` now steers through `NavMesh.CalculatePath` corners instead of straight at the waypoint.
The sidesteps are kept: the mesh knows about colliders at bake time and does not know about the
controller's skin width, so a stall AFTER a path was returned is the interesting case. Whether this
actually moves the 41% is UNMEASURED, see below.

## Two harness defects, and two more found while fixing them

The ladder watchdog counted FRAMES. A frame counter only advances when `Tick` runs, so it can only catch
a ladder that is alive and slow, never the failure that happened: a domain reload dropped the update
subscription, the run died, nothing incremented the counter, and it sat at zero for 48 minutes. It is
wall clock now, per-rung and per-run, and an `[InitializeOnLoadMethod]` reports an interrupted run using
a `SessionState` flag, which is the only part of a run that outlives the reload that kills everything
else. Every rung logs its own elapsed time, so the deadlines can be tightened against measured numbers.

Walk frames were numbered by capture index, so `walk-05` meant "the sixth shot of whatever run this was".
They are named by distance now, and fired on fixed milestones rather than an accumulator that resets to
zero, so `walk-0045m.png` is the same stretch of road in every run. Zero padding is load bearing or the
contact sheet sorts 300m before 45m.

Two more turned up while in there. `Finish` printed `LADDER COMPLETE` on the watchdog path as well as the
success path, so a timed-out run and a finished one ended identically in the log. And `MaxCaptures` was
32, which at 15m spacing silently truncates at 465m of a 580m route; it is 48 now and the walk says out
loud when a cap or the time limit stopped it.

Also deleted: a private `BuildRoute` in the walk probe, 47 lines, referenced by nothing. It implemented
the global principal axis method that `GmWendRoute`'s own header records as tried and rejected for
leaving 177m to 944m gaps between consecutive waypoints. A superseded route builder sitting inside the
walker is the setup for the exact bug that file's header warns about.

## The sources were in no repository at all

Worth correcting plainly, because the previous handoff said "nothing is committed to git, that's one
command whenever you want it". There was no repository at `~/GamesMaster-Unity` to commit to, and the
Wend sources were not part of the one that does exist: `unity:scene:check` reported the prologue as
"existing Unity-only source, nothing to sync". 2302 lines in exactly one place on disk.

They live in `unity/scenes/wend-hill-prologue/{Editor,Runtime,Tests}` now, registered with a `sourceDir`
so `sync-unity-scenes.mjs` copies them into Unity the same way it already handled 44 scene-system files.
The `.meta` files moved with the sources so the GUIDs are preserved. Nine of the ten classes are static
and the tenth is created at runtime, so nothing was GUID-referenced from a scene anyway.

The scene file itself stays out of git and should. It is generated from the purchased source on every
build, and it is BINARY despite the project being set to ForceText, because `GmWendBuilder` creates it
with `AssetDatabase.CopyAsset` from a purchased scene that ships binary, and a byte copy inherits the
format. The other two scenes in the project are text. Nothing depends on it being diffable, but do not
expect to read a diff of it.

## The walk was run, and it found that the night does not survive an open view

2026-07-25, first walk in the built player since the NavMesh. Numbers the run printed:

| | before | after |
|---|---|---|
| distance | 188m of 580m | **379m of 580m** |
| stalls | 9 | **2** |
| waypoints pathed | n/a | 7 of 11 on the NavMesh, 4 walked straight |
| catch plane fires | n/a | 0 |
| runtime errors | n/a | 0 |

So the NavMesh roughly doubled coverage and cut stalls from nine to two, and the distance-named
frames land on exact 15m milestones from `walk-0000m.png` to `walk-0375m.png`. Four waypoints still
could not be pathed, and stall 2 was 134m short of waypoint 10, so the last third of the route is
still not reachable. That is the next pathfinding question, not a solved one.

**The lighting finding is bigger than the pathing one.** Whole-frame luma across the 26 walk frames
runs **0.024 to 0.556, mean 0.166, with 2 of 26 inside the 0.03 to 0.06 target.** It climbs steadily
along the route and peaks at 0.556 around 330m.

Two frames were LOOKED AT, not just measured, because a luma number alone has already fooled this
project twice:

- `walk-0000m.png` at 0.024 is a real night. Dark, close forest, one distant warm lamp.
- `walk-0330m.png` at 0.556 is not a bright night. It is pale blue-white **foggy daylight**, with a
  single lit window the only thing in frame reading as night at all.

That is a change of register, not of exposure, so no EV fixes it.

One hypothesis was tested and DISPROVEN, recorded because it is the obvious one: the pack's other
volumes are not reasserting a daylight look. All 30 volumes are global at priority 1 weight 1, and 29
carry `profile=NONE`, so they contribute nothing. `GmWendNight.Inspect` prints this.

What the evidence points at instead, stated as the hypothesis it is: the fog is lit by AMBIENT and its
contribution accumulates with view depth. The night profile has fog albedo 0.035, 0.04, 0.055, which
is properly dark, but `globalLightProbeDimmer` is **1** and `meanFreePath` is 110m. At 0m the camera
is inside dense forest with geometry a few metres away, so almost no fog accumulates and the frame is
night. At 330m the view is an open street running hundreds of metres, so the fog integrates over that
whole depth and the ambient term takes the frame.

If that is right it also explains the older puzzles in this document: why brackets shot from different
vantages never agreed, and why "the distant rocky hill lifts to a pale grey and takes the depth with
it" above EV -1.5. It has NOT been tested. Nobody has changed the dimmer and re-measured.

It also inverts the assumed next lever. The previous handoff said the remaining unevenness was dark
stretches with no lamp in them, wanting ambient FILL. The measured spread is a factor of 23 and its
loud end is open ground going pale, so the ambient path is already dominating rather than missing.

## The bisect: it is the fog's ambient, and it is not the lamps

Two rungs, one value each, against the walk as the measurement rather than a single vantage. Both ran
unattended: night build with an override, app build from the saved scene, walk, measure. The overrides
are command-line flags (`-gmPracticalScale`, `-gmFogDimmer`) so a rung never needs a code edit, which
is what stops a campaign losing track of which change made which frame.

Whole-frame luma at matched distances. Milestone naming is what makes the columns comparable at all:

| distance | view | baseline | practicals x0.5 | fog ambient x0.5 |
|---|---|---|---|---|
| 0m | enclosed forest | 0.024 | 0.024 | 0.019 |
| 150m | mid, near lamps | 0.230 | 0.229 | 0.225 |
| 285m | opening out | 0.239 | 0.240 | 0.199 |
| 300m | open street | 0.402 | 0.395 | **0.282** |
| 315m | open street | 0.535 | 0.536 | **0.313** |
| 330m | open street | 0.556 | 0.555 | **0.326** |
| 345m | open street | 0.326 | 0.327 | **0.189** |
| 405m | enclosed | n/a | 0.020 | 0.014 |

A third rung took the dimmer to 0, and it lands the worst frame in the scene inside the target:

| distance | baseline | fog x0.5 | **fog x0** |
|---|---|---|---|
| 150m | 0.230 | 0.225 | 0.183 |
| 300m | 0.402 | 0.282 | 0.190 |
| 315m | 0.535 | 0.313 | 0.113 |
| 330m | 0.556 | 0.326 | **0.030** |
| 345m | 0.326 | 0.189 | 0.053 |
| 375-480m | n/a | 0.014-0.030 | 0.013-0.031 |

330m goes from six times over target to inside it, from one value, and the enclosed stretch is
untouched. That is as clean a single-cause result as this scene has produced. What it does NOT settle
is the middle: 150m is still 0.183 and 285m to 360m still runs 0.11 to 0.19, so the fog was the whole
story for the worst frame and only part of it elsewhere.

Confirmed by eye as well as by number, at the same camera position that produced the foggy-daylight
frame: dark sky, dim green-grey plaster, a warm lit window, road receding into black. The fog still
scatters direct lamp light, so zeroing the AMBIENT dimmer removes the daylight wash without removing
the halo the fog was ported for.

## Two more rungs, and the finding that global dimming makes the spread worse

The sky was the remaining suspect for the middle of the route, being the ambient source that lights
SURFACES rather than fog. Bracketing it settled that and something more useful.

| variant | in 0.03-0.06 | in 0.02-0.10 | near-black | spread | ratio |
|---|---|---|---|---|---|
| baseline | 2/26 | 12 | 0 | 0.024-0.556 | 23x |
| **fog ambient 0** | **7/35** | **22** | **0** | 0.013-0.190 | **14x** |
| sky -8.5 stops | 5/35 | 10 | **19** | 0.000-0.149 | 149x |
| fog 0 + sky -6.5 | 7/35 | 16 | 11 | 0.002-0.137 | 56x |

The sky IS the master ambient dial and it moves everything: three more stops took 150m from 0.230 to
0.058, straight into target. It also took nineteen of thirty-five frames to near-black. Combining a
gentler sky trim with the fog fix was worse than the fog fix alone on every measure except the narrow
target count.

**The structural finding, which is worth more than the numbers: global dimming increases the spread.**
Lamp-lit ground has a floor that does not scale with ambient, so turning ambient down drives the dark
stretches to zero faster than it brings the lit ones into range. Baseline sits at 23x max-to-min, the
fog fix improves it to 14x, and every further dimming rung made it worse, up to 149x.

That is the measured version of what the previous handoff guessed at when it said exposure slides the
window and cannot compress the spread. The instinct was right and the proposed remedy was backwards:
what is left after the fog fix is not too much light to be removed, it is too little in the stretches
between lamps. Raising the floor there is level work, more or better placed practicals, not a dial.

**The practicals are not the cause.** Halving all 24 of them moved every matched frame by less than
0.01, against a fog rung that moved single frames by 0.23. No run-to-run variance was measured for
this scene, so those small deltas are not formally attributable to noise; what can be said is that
they are more than an order of magnitude below the effect the fog rung produced. The scene is not
lit by its own lamps in any meaningful sense, and the warm cast at 150m does not come from them either.
That is worth stating flatly because the previous session cut them 104907 -> 2475 lumens and treated
that as the fix for brightness.

**The fog's ambient term is the cause of the blown open views.** Halving `globalLightProbeDimmer` cut
330m by 41% while moving the enclosed frames by 0.005. The effect scales with how open the view is,
which is the signature the depth hypothesis predicted: fog integrates along the view ray, so a street
running hundreds of metres accumulates the ambient term and a forest with geometry at 10m does not.

Two things it does NOT fix, both measured rather than assumed:

1. **0.5 is not low enough.** 315m to 360m still reads 0.31 to 0.33 against a 0.03 to 0.06 target. The
   next rung is a lower dimmer, and 0 is worth trying as a bracket end rather than a guess.
2. **There is a third cause in the middle of the route.** 150m barely moved under either rung, and it
   is 0.225 with a properly dark sky and warm-lit geometry. Not the lamps, not the fog. The remaining
   suspect is the ambient/sky term lighting SURFACES rather than the fog, which no rung has touched.

## The route wades into a lake at 506m. It was never a hole, and that claim is retracted

Recorded as a correction rather than quietly edited, because it was written up as a reproducible world
defect and it is not one.

Two runs reported FELL OUT OF THE WORLD at 506m, and the captured frame showed a flat tan plane with a
hard horizontal edge filling the lower half and rocks beyond. The water-plane reading of that frame was
right. The conclusion drawn from it was wrong: the player was SUBMERGED and standing on the lake bed,
not falling through anything.

What settled it is the next run. With the floor test corrected to terrain-surface relative, the walk
went straight past that point and finished at **517m with zero falls and zero catch-plane fires**. A
player 2m under a water surface while standing on the ground is not falling, and the old test could not
tell those apart because it measured against the SPAWN: this route descends 22m from its first waypoint
to its last, so it fired at 25m below spawn while the ground was 2m away.

`GmWendRoute` had already said so in its own report, which nobody had read closely:
`cluster of 12 piece(s): 2325 settlement prop(s) within 50m, overlaps water`. The road cluster the
route picks overlaps water, and its final waypoint is at y=-22.80. So this is a ROUTE quality finding,
the prologue's walk ends in a lake, and not a world integrity one.

**The catch plane flaw it exposed was real regardless.** It fired at 30m below the terrain's LOWEST
point, which put it at -102, and nothing in the middle of the map could ever reach that. Being above the
lowest point of a landscape says nothing about being above the ground you are standing on.

It is terrain-surface relative now: fallen means more than 10m below the surface sampled at the
player's own XZ, with the absolute height kept as the backstop for when the player is off the terrain
entirely and there is no surface to be under.

The walk probe's own detector had the mirror-image bug and it was luckier rather than righter. It
tested "25m below the SPAWN", and this route descends 22m from its first waypoint to its last, so a
clean walk to the end would have reported itself as falling out of the world with 3m to spare. Both
now share `GmWendCatchPlane.IsBelowWorld`, so the probe and the failsafe cannot disagree about what
falling means.

## Verified, and not

Verified by running it this session and reading the real output: the four materials and five prototype
prefabs that were repointed, the census holding at every rung, the luma at every rung of three ladder
runs, the pixel measurements in the tables above, the purchased originals still carrying their old
timestamps, and the owned prefabs matching the originals structurally.

Verified later the same day, by running it and reading the output: **125/125 EditMode tests pass**, up
from 103, with 22 added for frame naming, boundary geometry, NavMesh corridor and area, and the save
restore decision. A full `BuildCommittedNight` runs headless and **contract check 9/9 passes after a
genuine reload**, which is what proves the walls, the catch height and the single listener actually
serialized into the scene file rather than living in memory. The 29 disabled listeners, the 4 walls, the
-102 catch height and the 67279m^2 bake are all numbers that build printed.

Verified by the walk itself, in the built player: 379m of 580m over 26 milestone frames, 2 stalls,
7 of 11 waypoints pathed on the NavMesh, zero catch-plane fires and zero runtime errors, and the luma
spread and the two frames described above.

First frame-time numbers for this scene, from `walk-performance.json`, 5185 sampled frames over 379m
at 1600x900: **mean 32.20ms, p50 32.80ms, p95 50.54ms, p99 59.60ms, max 91.79ms**. Read that with the
caveat that matters: **vSyncCount is 1**, so these are delivered cadence quantised to the display's
interval, not raw frame cost. A 32.8ms median is two intervals, which is the 30fps step, so the scene
is missing the 60Hz deadline essentially all the time and dropping to a third of it by p95. Getting
true GPU cost needs a run with vSync off, which has not been done.

Not verified, and this list matters more than the one above:

- **Why 4 of 11 waypoints cannot be pathed**, and whether the corridor bake simply does not reach
  them. Stall 2 was 134m short of waypoint 10, so the far third of the route is still unwalked.
- **The fog hypothesis above.** The dimmer has not been changed and nothing has been re-measured.
- **Whether the walls hold.** Nothing fell, but nothing walked into a wall either. Zero catch-plane
  fires over a route that never reached the map edge is weak evidence, not a test.
- **Anything by ear.** Check 9 fixes WHICH listener is live. It says nothing about whether the scene
  makes a sound.
- **Memory and culling.** Frame pacing is measured now; neither of those is.
- Whether transmission through a neutral diffusion profile makes backlit leaves too bright is still
  open, since the leaf highlights do still clip at EV -2, and that can only be judged at whichever
  exposure gets chosen.

## The starfield sky is a three-part recipe, not a sky swap

Not done, and this is the reason rather than an excuse.

`GmVillageSky` bakes a seeded starfield and drives an HDRISky with it, and porting that to the prologue
looks like a one-line asset swap. It is not, because the HDRI sky is this scene's ONLY ambient source.
The prologue lights its surfaces from the pack's daytime HDRI taken down 5.5 stops; a starfield is
darker than that by a wide margin.

The village makes it work by paying for it in two places the prologue does not have:

| | village, with starfield | prologue, now |
|---|---|---|
| sky | starfield, exposure 0 | pack HDRI at -5.5 stops |
| moon | 4.6 lux | 1.0 lux |
| fill light | 0.35 lux, dedicated | none |

That is a lighting recipe, not a texture. And the cost of getting it wrong is already measured on this
scene rather than guessed: the sky bracket at -8.5 stops put 19 of 35 walk frames under 0.01, and a
starfield sits below that. Dropping it in without also porting the brighter moon and the fill would
predictably black out the night that four rungs just settled.

So it needs its own bracket, run the same way, and it should happen when the light DISTRIBUTION work
happens, because the fill light is the same lever that problem needs.

## Submersion is decided at runtime, because at runtime there is nothing to predict

Three attempts to truncate the route at the water failed at BUILD time, each on a different surface
signal, and all three are recorded in `GmWendRoute.TruncateAtWater`, which is kept tested and unwired
because the analysis outlived the code. The short version: waypoints are road-mesh bounding-box
centres, the village clears the water by about half a metre so any margin swallows it, and the terrain
itself dips under the water plane while the player walks on meshes above it. There is no reliable
"walkable surface height" to consult before the walk starts.

At runtime the question answers itself. `GmWendWalkProbe` looks up the water surface once, through
`GmWendRoute.WaterSurfaceY` so water stays defined in one place, and stops the walk the frame the EYE
goes under it. Not the player root: the root is at the feet, and wading is not drowning.

Measured: the walk now stops at **439m** rather than 515m, and reports STOPPED AT THE WATER. That is
better data, not worse. From about 450m the previous run was already at the water's edge and then in
it, and those frames are a golden band of refracted light that went into the luma statistics as though
they were views of a village. So the dry prologue is about 439m of a 580m route and the missing 141m is
lake, which is a level decision rather than a pathing failure.

The frame captured at the stop is a good one: a dark vista with the lamp line curving away below and
trees against the sky. Worth knowing, because it means the walk ends somewhere that looks deliberate.

## Eight levers, seven negligible: the lighting cannot be finished with dials

Run as one-variable rungs against the walk, scored on a written standard rather than on a single
number. Every rung is a full night build, app build and walk.

| lever | tested | effect on the walk |
|---|---|---|
| fog ambient dimmer | 1 / 0.5 / 0 | THE fix. 330m 0.556 -> 0.030. Committed at 0 |
| practicals | 0.5x and 5x | none. p5 unmoved at 0.012 in BOTH directions |
| moon intensity | 1 / 4 / 8 lux | none |
| moon elevation | 24 / 55 deg | best spread, 28x -> 21x. Still no visible moonlight |
| indirect diffuse | 1 / 3 | none. There is no GI in this scene to multiply |
| lamp glass emissive | 1.5 / 0.5 | none |
| sky exposure | -5.5 / -7 / -8.5 | scales everything uniformly. -8.5 blacks out 19 of 35 frames |
| SSR | not judged | no metric measures specular; see below |

**The number that settles it is p5**, the darkest frames. It sits at 0.012 through every rung,
including a FIVE TIMES increase in practical intensity. Light you turn up does not reach places that
have no light in them. The dark stretches do not have a weak lamp, they have no lamp, and that is the
measured version of what an earlier handoff guessed at.

It also means the premise this whole approach rests on is only half true. "Abandoned Village was
already lit by its artist" is right about the daytime scene; at night-scaled values those 24 practicals
light their own pools and nothing else, and what actually lights the walk is a dimmed daytime HDRI.
That is why every frame is warm and none is moonlit.

## The moon is decorative, measured at pixel level

Zero of 30 frames read as moonlit at any tested value. That was first measured on frame MEANS, which
cannot distinguish "no moonlight" from "moonlight next to brighter lamplight", so it was re-measured as
the fraction of VISIBLE pixels that are blue dominant:

| | blue fraction of visible pixels | best single frame |
|---|---|---|
| moon 24 deg, 1 lux (committed) | 0.2% | 4.6% |
| moon 55 deg, 4 lux | 0.4% | 8.3% |
| moon 8 lux, sky -7 | 0.0% | 0.0% |

The last row is the instructive one: dropping ambient to make room for the moon produced BLACK, not
moonlight. The sky is what makes surfaces visible at all and the moon cannot substitute for it.

## What whole-frame luma was missing

The first standard scored mean luma and fully-clipped pixels, and it passed frames it should not have.
Nick asked whether it was catching oversaturation, overbrightness and wrong colour. It was catching
none of them.

- **Local blowout.** 285m and 300m have 3 to 6 percent of their pixels above 0.80 luma, visibly blown,
  while the standard reported no clipping because nothing reached 254. A whole-frame mean cannot see a
  blown wall beside a lamp.
- **Colour.** The standard discarded colour entirely, which is indefensible in a document whose central
  bug was diagnosed BY colour ratio. Added: a green-cast check, since the scene's only sources are 2000K
  lamps and a blue moon and nothing in it can legitimately be green dominant.
- **Saturation, with a caveat that matters.** p95 saturation reads 1.0 on the darkest frames, and that
  is an artefact rather than a finding: a pixel like (3,1,0) is fully saturated by definition. It needs
  weighting by luma before it means anything, and it is not currently trusted.

SSR is left undone for the same reason rather than a different one: it changes specular response, and
none of the three metrics measures specular. Adding it would be shipping a feature with no instrument
pointed at it.

## Where the lighting actually stands

Scored 2 of 4 on the standard. Passing: nothing is unreadable black, nothing clips to 254. Failing:
two frames above the daylight threshold, and the body of the walk runs p5 0.012 to p95 0.213 against a
0.01 to 0.15 band.

Looked at rather than only measured, across all 30 frames: 105m to 225m is a genuine horror village at
night, and 270m to 300m reads well. Four defects are visible in the sheet: the 285m cottage windows
blowing, 315m and 405m near black, several motion blurred close ups where the walk brushes a trunk, and
360m, which is the brightest frame in the walk AND a colour outlier, a mint green wall in an otherwise
entirely warm scene, lit by a practical at about a metre.

None of that is reachable from a dial. It is lamp placement, and it needs someone to decide where the
light in this village comes from.

## Sky up 1.5 stops, EV +0.30: promoted, off a full walk this time

The previous section's "ambient UP" finding was voided as a promotion candidate the moment it was
checked: it was measured over 0-210m because that run was interrupted, and the interruption turned out
to be self-inflicted rather than external. Three walk attempts in a row died at exactly 15 frames /
210m, consistently enough to suspect something was killing them on purpose. It was not. The walk's own
internal cap is 480 seconds; the shell command running the built app had no timeout override and was
hitting a 2-minute default, killing the player mid-route before it ever reached `Finish()` or wrote
`walk-performance.json`. Once the app was launched with a 10-minute timeout instead, it completed to
the water at 438-439m on every attempt, the same place every full walk before it stopped.

With that fixed, the pair was measured full-route, twice: once at sky-drop 4.0 / EV +0.30 against a
freshly rebuilt control at the old sky-drop 5.5 / EV -1.35, both walked start to water, both scored by
the same script over the same 30 `walk-*.png` frames (stall and submerged tags excluded, as before).

|                     | mean  | p5    | p50   | p95   | p99   | spread | band fails | worst blowout | worst green cast |
|---------------------|-------|-------|-------|-------|-------|--------|------------|----------------|-------------------|
| control (5.5/-1.35) | 0.080 | 0.016 | 0.070 | 0.221 | 0.297 | 26.0x  | 4/30       | 3.68%          | 51.09%            |
| sky 4.0 / EV +0.30   | 0.048 | 0.012 | 0.043 | 0.108 | 0.243 | 33.3x  | 3/30       | 0.06%          | 41.99%            |

This is not the 4/4 the interrupted half-walk predicted. p5 does not move (0.016 -> 0.012, both already
failing dark, within run-to-run flicker noise) and the spread figure gets nominally worse, because that
ratio is dominated by whichever single frame is darkest and the darkest frame barely moved. What the
full walk shows is narrower and real: the three frames that were reading as daylight leaking through
(150m at 0.221, 285m at 3.68% local blowout, 300m at 1.90%) all resolve, and worst-case local blowout
across the whole route drops by two orders of magnitude. Confirmed by eye, not just by number: 150m and
300m read as a proper dark village street in both builds, and the difference the numbers describe (a
cottage window going from visibly blown to merely bright) is there on screen, not just in the histogram.

The two remaining defects are the same two the previous section already named, and they moved the way a
non-dial problem should: 315m stayed near black (0.011 -> 0.007, i.e. it did not get better and may be
marginally worse) and 360m's mint-green wall improved but did not resolve (51.09% -> 41.99% green-cast
pixels, still visibly green up close). Both are confirmed by eye in this session, at the promoted
values: 315m is a genuinely underlit stretch with one faint lamp in the far distance, and 360m is a
motion-blurred close pass on a wall lit by a single practical from about a metre, which is a colour and
placement problem the sky cannot reach no matter which way it is dialed.

**Promoted.** `DefaultSkyExposureDrop` is now 4.0 (was 5.5) and `DefaultExposureEV` is now 0.30 (was
-1.35), in `GmWendNight.cs`. Verified after promotion, not just before it: a `BuildCommittedNight` run
with NO CLI overrides reproduces sky-drop 4/EV 0.3 and passes 9/9 contract checks; 147/147 EditMode
tests pass; the standalone app built from that scene walks the full route to the water and reproduces
the table above within noise (mean 0.048, p95 0.108, 3/30 band fails, worst blowout 0.06%).

## The two hardest remaining frames were never lighting defects

Chased one more lever before accepting the 3/30 fails as the floor: `GmWendLamps.Gaps()` only ever
checked the route's 11 sparse waypoints (~58m apart on average) for coverage, never anything between
them. Waypoint 9 to waypoint 10 is a single ~152m leg with no NavMesh bake reaching it, and every one of
`walk-0360m.png` through `walk-0435m.png` sits somewhere on it -- both endpoints individually read
"covered" by the 30m reach check, so the whole dark middle was invisible to it.

Fixed properly: `GmWendRoute.Densify(route, 15f)` interpolates the route to the SAME 15m resolution the
walk probe photographs it at, matching `GmWendWalkProbe.CaptureEveryMeters`, before `Gaps()` ever runs.
Tested (a regression test reproduces the exact bug: two waypoints 150m apart, each individually within
reach of a light, densifying is what makes `Gaps()` find the 82m gap in the middle). Built: 3 gap lamps
now, up from 2, the new ones correctly sitting on the intended waypoint-9-to-10 line.

**It did not move the numbers at all.** Same walk, same script, only the gap lamps changed:

|                    | before densify | after densify |
|--------------------|----------------|----------------|
| band fails         | 3/30           | 3/30           |
| walk-0315m mean    | 0.007          | 0.007          |
| walk-0360m mean / green% | 0.243 / 41.99% | 0.244 / 40.48% |
| walk-0405m mean    | 0.010          | 0.009          |

Looked at why, by eye, at the actual walked coordinates rather than the route's intended ones, and the
diagnosis these three frames have carried all session turns out to be wrong for two of the three:

- **`walk-0360m.png` is not a lighting defect.** It is the camera jammed directly against a wall's
  collision mesh -- motion-blurred, filling the entire frame with one texture at point-blank range,
  reading green because that is the wall material's base colour under ambient light with no direct
  source reaching a surface the camera is embedded in. No lamp, no colour temperature, no intensity
  changes what a camera stuck inside geometry renders. This is what the character does after the
  approach to waypoint 9 stalls and sidesteps twice (see the NavMesh section below) -- a collision
  outcome, not a light placement.
- **`walk-0315m.png` is arguably not failing at all.** Looked at rather than only measured: it shows a
  village street receding into darkness with TWO lamps visibly lit in frame, one near, one at distance,
  and a building silhouette -- a genuinely composed, atmospheric night shot. It fails the 0.01 floor
  because the frame is mostly dark road and sky BY DESIGN, and a single mean-luma number cannot tell
  "atmospheric with real light sources in it" from "nothing lit." This is closer to a metric limitation
  than a scene defect.
- **`walk-0405m.png` is the one genuine dark gap left**, and the gap lamps placed on the waypoint-9-to-10
  line did not reach it because the character never reliably travels that line to begin with: the walk
  log shows it BLOCKED 152.5m short of waypoint 10, twice, meaning most of the "route" the lamps were
  placed along is never actually walked. A lamp placed where the route says the player should be does
  nothing for where the player actually ends up when navigation fails first.

**Conclusion:** the lighting recipe itself -- sky, exposure, fog, moon, practicals, gap coverage on the
route the character can actually reach -- is not leaving anything on the table. 27 of 30 walk-probe
frames pass a strict written standard, and of the 3 that do not, one is a metric artefact, one is a
collision/camera bug, and only one is a genuine coverage gap sitting on a stretch the NavMesh does not
reach. None of the three is fixable by another lighting lever; they are pathing and metric problems
wearing a lighting costume, and pathing is explicitly out of scope for this pass. The gap-lamp densify
fix is kept regardless -- it is a real, tested correction to a real bug in the coverage check, verified
not to regress anything (155 -> 164 EditMode tests, 9/9 contract, full walk still completes to the
water) -- it is just not the fix for these three frames, because these three frames were never what it
diagnosed them as.

**Half right, corrected the same session.** "reading green because that is the wall material's base
colour under ambient light" above was too quick. Re-walked and looked at `walk-0330m.png` and
`walk-0345m.png` -- normal viewing distance, no collision, no motion blur -- and the same building's
wall reads green there too. Not just a camera-in-geometry artefact; a real material defect. Traced it:
`M_Wall_02`, the single most used wall material in the village at 153 renderers, carries
`_BaseTint = (0.596, 0.635, 0.525, 0)`, green channel highest, a bias small enough (~6%) to vanish under
the pack's 2000 lux daylight and large enough to read as coloured stone at this scene's night exposure --
the same mechanism as the emissive-foliage bug, on a different property. `GmWendWallTint.cs` surveys
every material with a `_BaseTint` and neutralises any with a measurable green skew to a luma-preserving
grey, DERIVED from the colour rather than naming `M_Wall_02`: it caught 5 materials, not 1
(`M_Bottom_Cover_01`, `M_Wall_02`, `M_Wall_02 1`, `M_Wall_02b`, `M_Wall_04`), 263 renderer slots total,
none of them hand-picked. Verified full-walk, same route, only this changed:

|                        | before | after |
|------------------------|--------|-------|
| band fails             | 3/30   | 2/30  |
| worst green cast       | 41.99% | 0.01% |
| walk-0360m green%      | 40-42% | 0.00% |
| walk-0405m mean         | 0.009-0.010 (fail) | 0.010 (pass) |

`walk-0360m.png` still fails, now correctly for the reason first suspected: it is still a camera jammed
against a wall, still bright and motion-blurred, but the wall is neutral grey now (saturation 0.234 ->
0.078), not coloured. That residual is the collision bug, unchanged, and still out of scope. Confirmed
by eye: `walk-0330m.png` and `walk-0345m.png` now show a normal aged white-plaster wall with visible
brick weathering, and `walk-0360m.png`'s close pass is a neutral grey blur instead of a mint-green one.

## "It still doesn't feel right": the grass was the bigger problem

Every number above was passing. Told to keep looking anyway rather than trust the written standard, and
a plain re-read of frames the standard had already cleared found it: `walk-0105m.png`, `walk-0135m.png`,
`walk-0180m.png` and `walk-0210m.png` all show the SAME deeply saturated blood-red grass filling the
lower third to half of the frame, in villages shots that have nothing to do with the 360m collision
event. None of the written standard's numbers (luma band, green-cast, blowout) are built to catch an
oversaturated ORANGE foreground, which is exactly why it survived every measurement while being the
first thing a person looks at.

Traced to `M_grass` and `M_grass 2` (Shader Graphs/S_Wind, the same shader `GmWendFoliage.cs` already
owns copies of to kill their emission): both carry an `_Albedo_Tint` biased off neutral, and `M_grass`
additionally carries `_Albedo_Intensity = 2.2`, more than doubling its albedo contribution. Same
mechanism as every other defect in this document -- invisible against the pack's 2000 lux daylight,
dominant at this scene's night exposure -- on a THIRD property family this time (tint was walls,
intensity is new).

**Built the short-walk tool first, because guessing wrong here would repeat the starfield mistake.**
`-gmWendWalkMaxMetres <n>` stops the walk early (150m instead of the full ~439m, 78 seconds instead of
several minutes) specifically so a hypothesis can be tested against the actual frames it should change
before paying for a full walk. It earned its cost immediately:

- **Round 1**, tint neutralised alone (`GmWendGrassTone`, same luma-preserving neutralise as the wall
  fix, generalised to catch bias in ANY direction since `M_grass 2`'s bias is green while `M_grass 1`'s
  is warm): rebuilt, short-walked to 150m, looked at walk-0105m/135m. Unchanged. Still deeply saturated
  orange. Tint was not the driver.
- **Round 2**, `_Albedo_Intensity` reset from 2.2 to 1.0 as well: rebuilt, short-walked the same 150m
  segment. Confirmed by eye immediately -- the foreground grass went from an overwhelming red-orange
  mass to a properly dark, naturalistic silhouette with warmth only where a lamp actually lights it.

Full walk after both fixes together, same route, same script:

|                   | before | after |
|-------------------|--------|-------|
| band fails        | 2/30   | 2/30 (same two: 315m metric, 360m collision) |
| walk-0105m/135m/180m/210m grass | deeply saturated orange | dark, naturalistic |

The band-fail count does not move, and should not: neither of the two failing frames was ever about
grass. What moved is everything the written standard was never measuring -- confirmed by eye across the
whole walk, not by a new number.

**The two loose ends closed the same session.** The walk-0210m residual warmth: widened the spot-check
past 8m and found it is `M_grass` at 9.6-10.6m, already owned and already at intensity 1 -- correctly-fixed
grass sitting near a lamp, not an unfixed material. Real light, not a bug.

`M_grass 1` (tree-attached undergrowth, reaches the scene only through terrain tree prototypes) carried
the same measured bias as the renderer materials -- tint (0.679,0.565,0.516) spread 0.163, intensity 2.0
-- so `GmWendGrassTone` gained a second path mirroring `GmWendFoliage.OwnedPrefab`: the prototype prefab
is owned and its renderers repointed through the SAME `OwnedNeutral` decision already used for scene
renderers, so a material is fixed by the same rule regardless of which of the two paths it reaches the
scene through. `M_Leaf` (canopy) is checked by the identical rule and left alone by the identical rule:
its tint is already neutral and its intensity sits BELOW 1, the opposite direction from every confirmed
defect, not a hand-picked exclusion. Verified full-walk after: band fails 2/30 -> 3/30, but the added
fail is `walk-0405m` at 0.009, a frame that has sat within noise of the 0.01 floor (0.009-0.010) across
every run this session regardless of what changed -- not a regression, the same known boundary case.
Confirmed by eye across the route: no new defects, several genuinely well-composed frames (walk-0165m,
walk-0225m) with no foliage colour issues remaining.

## Three more attempts at the water cut, and why none of them are wired in

`TruncateAtWater` and `RaycastGroundY` already existed; this section is attempts five and six trying to
actually wire the cut in, plus what each one found on the way to giving up on it.

**Attempt 4, RaycastGroundY, wired straight in.** A physics raycast instead of a terrain sample, on the
theory that the player stands on whatever collider is really there, not on the terrain heightmap. Wiring
it into `Build()` cut 7 of 11 waypoints at waypoint 4, taking the route from 580m to 154m -- the SAME
failure the terrain-height attempts hit before this session, reproduced through a new code path. Waypoint
4 sits at terrain y=-1.46, 0.66m under the lake's absolute y=-0.8, and the walk crosses it dry every time
it runs.

**Attempt 5, GroundNearWater.** Requires an actual water COLLIDER in the same vertical column before
comparing depths, so a dry low spot with no water anywhere near it can never be cut. Fixed waypoint 4
correctly (own EditMode tests confirm it). Checked against the one real data point available -- the exact
position a live walk recorded itself as SUBMERGED, `(-18.89, -2.51, 81.26)` -- and returned null there
too. The reason: `PlaneWater`, the only water-named renderer this scene ships, has no Collider component
at all. `Physics.RaycastAll` can never see it, at any point in the scene. Correct code aimed at a signal
that does not exist here, and wiring it in produced "route kept whole" -- not because the route is
actually safe, but because the check can never fire.

**Attempt 6, GroundInFootprint.** Checks the water's RENDERED bounds instead of a collider, since
`PlaneWater` has no collider but does have a Renderer. This is where the investigation stopped being
about the code and started being about the scene. Two things fell out of checking it against real
coordinates instead of trusting the theory:

- `PlaneWater`'s own bounds centre is `(-20.75, -0.81, -27.79)` -- which is waypoint 5, exactly. The
  water plane is not just near the route, it IS one of the route's own waypoints, caught by the same
  "Lane" substring collision documented below.
- The real submersion point, `(-18.89, -2.51, 81.26)`, is 60m from anything named "Water" and everything
  else nearby too: the nearest renderers are `SM_Cliff_01`/`SM_Cliff_03` and `SM_Rock`, none of which
  have a Collider either. There is no lake mesh at the far end of this route. What actually ends the walk
  there is the terrain descending into a canyon below y=-0.8, the same global height the small decorative
  pond happens to share -- not a body of water in any sense `GroundInFootprint` could check.

A shape-accurate footprint (a real water mask, not a bounding box) would still need to solve attempt 1's
original problem -- `PlaneWater`'s rectangular AABB also covers waypoint 4's dry ground, so a footprint
check using it re-cuts the same false positive attempt 5 fixed.

**Conclusion, and it has not moved in six attempts:** there is no single static signal on this scene that
separates a dry dip from a real drop, because the pack did not build one. No water collider, no lake mesh
at the place that matters. `GmWendWalkProbe`'s runtime stop is not a fallback for this -- it is the only
thing that has ever correctly answered the question, because it asks about the ACTUAL path at the moment
of walking it rather than guessing from 11 static points beforehand. `RaycastGroundY`, `GroundNearWater`
and `GroundInFootprint` stay in `GmWendRoute.cs`, tested (155/155), unused, same as `TruncateAtWater`
before them: the analysis is worth more than the code, and the next person reaching for "just raycast it"
should find this instead of re-walking the same three-hop dead end.

**Also discovered along the way, worth its own line:** every one of the 57 "road pieces" this project has
called road meshes since the very first attempt is an object literally named `Plane` (56 of them) or
`PlaneWater` (1). `RoadPattern`'s `Lane` term matches the substring inside `Plane` by accident. It has
apparently been finding the right objects the entire time, because this pack's road decals really are
named `Plane` by default rather than anything descriptive -- but it is luck, not intent, and it is why a
generic ground plane anywhere else in a future scene would silently join the route.

## True GPU cost, vSync off

Every pacing number before this one was taken with vSync on, so it measured DELIVERED CADENCE quantised
to the display's refresh interval, not what the scene actually costs. Same walk, same build, `-gmWendNoVSync`:

| | vSync on | vSync off |
|---|---|---|
| mean | 20.21ms | 20.71ms |
| p50 | 16.75ms | 12.98ms |
| p95 | 29.35ms | 50.00ms |
| p99 | 33.72ms | 59.65ms |
| max | 67.08ms | 74.68ms |

The mean barely moves, which is a coincidence of averaging, not agreement: p50 drops (12.98ms, ~77fps,
the scene's real steady-state cost) while p95 and p99 get markedly WORSE (50ms/~20fps and 59.65ms/~17fps)
once they are not being rounded up to the nearest display interval. Read together, that is a scene that
runs comfortably most of the time and hitches hard on a real minority of frames -- exactly what vSync-on
numbers cannot show, because both a 17ms frame and a 32ms frame land on the same delivered step. What
causes the worst 5% has not been profiled; this only proves the hitch is real and roughly how big it is.

## Still open

Rewritten after the walk ran. The previous version of this list had gone stale in the worst way: it
still said the walk had not been re-run and the sources were not committed, both of which had been
done further up the same document. A list of open items that contradicts the record above it is worse
than no list, because it is the part people read first.

**Decided: saves stay deferred, on purpose, not by omission**

`GmVillageSave` would wire into this scene without error -- it null-checks `GmBellSummons` and
`GmGateLeaves` before touching them, so adding the component here would just degrade to saving and
restoring player position and rotation. That is exactly why it should not be added yet. Its own header
says what it is FOR: resuming a horror prologue's beats without silently skipping ones the player never
saw. This scene has no beats, no gate, no bell -- "the prologue's systems are not in this scene," per
the housekeeping note below -- so a save here would remember only a standing position and nothing about
the experience, which is worse than no save: it would tell a player their progress is preserved when
none of the progress that would matter exists yet to preserve. Revisit when the beats/gate/bell systems
are ported in; the component is already written, tested, and scene-agnostic, so reusing it then is a
one-line `AddComponent`, not new work.

**Lighting itself: closed out for this pass**

- **Light distribution / placement.** Done to the extent lighting alone can do it. `GmWendLamps` now
  checks coverage at walk-probe resolution (`GmWendRoute.Densify`, 15m) instead of at the 11 sparse
  route waypoints, closing a real 152m blind spot in the coverage check. It does not move the walk's
  three failing frames, and by design: those three turned out to be a camera/collision artefact, a
  metric limitation, and a gap the character never reliably reaches (see "The two hardest remaining
  frames were never lighting defects" above), not lamp placement. 27 of 30 frames pass the written
  standard and nothing found this session says another lighting lever would raise that further.
- **The lamp line** may still want its own rung if picked up again, but is no longer blocking anything.
- **The forward-view review anomaly** is still unexplained: the review rig reads 0.026 where the ladder
  reads 0.059 from the same position and rotation, while the rear look agrees to about 1%. One
  hypothesis, accumulated render history, was tested and disproven.

**Testable, just not tested yet**

- **Why waypoint 2 fails to path.** Narrowed, not solved. Of the 3 waypoints that still fail, 2 are now
  explained: waypoint 9 and waypoint 10 sit at or past the canyon the route runs into (see the water-cut
  section above), which the NavMesh bake correctly does not cover. Waypoint 2 sits in a tight walled
  courtyard corner -- `SM_Wall_Corner_300x100`, a wood fence, and pipe props all within a couple of
  metres -- and the path calculated toward it is `PathPartial`, stopping 1.6m short in Z with both ends
  confirmed on the mesh. Checked whether any of that geometry is a real physical obstacle: NONE of it has
  a Collider, wall, fence or pipe alike, which the CharacterController could not be blocked by even
  though the real walk stalls 18m short of the same waypoint. That gap between "nothing here can block a
  body" and "something here blocks the body" is the actual open question, and it points at the terrain
  mesh or a NavMesh bake seam at that corner rather than at any prop. Not followed further: fixing a bake
  seam by tuning agent parameters risks the 7 waypoints that already path correctly, and this is one
  stall the walk already handles gracefully (sidesteps, then moves on).
- **The route's last ~140m lead into a canyon with no lake mesh in it.** Investigated exhaustively this
  session (see above): there is no static signal that can tell this apart from a dry dip, so it stays
  unfixed at the route level on purpose. The walk stops itself at the water and says so, at 438-440m every
  time this session, so no measurement is polluted by it. Whether the prologue should end at the shore,
  turn before it, or go somewhere else entirely is still a level decision, now backed by six documented
  dead ends instead of three.
- **The boundary walls have never been walked into.** Four walls are in the saved scene and the contract
  passes on them by value. Zero catch-plane fires over 440m is weak evidence, not a test: the route
  never goes near the map edge.
- **Memory and culling.** Frame pacing is measured now; neither of those is.
- **The starfield sky port from `GmVillageSky`** has not been started.

**Cannot be tested here**

- **Anything by ear.** Checked what there actually is to hear, since this environment has no speakers or
  ears to judge it with regardless: zero. `FindObjectsByType<AudioSource>` over the built scene returns
  **0**, including inactive ones. Check 9 fixes which of the 30 `AudioListener`s is live, but there is
  nothing playing into it. This is not "nobody has listened yet" so much as "there is no sound design in
  this scene yet" -- no ambient bed, no footsteps, no lamp hum, nothing. The pack's own ambient loops
  from `assets/sfx/` (crickets, owl, wind, gravel and leaf steps -- already in the repo, unused here) are
  the obvious next step, but adding them and judging the result both need a human ear this session does
  not have.

**Housekeeping**

- **`Packages/manifest.json` and `Assets/Scripts/GmVillageSave.cs` are unversioned.** The navigation
  package addition and the restore-decision extraction both live only in the Unity project, which is
  not a git repository and is not part of the synced source set.
- **The rest of `unity/` is untracked.** Only `unity/scenes/wend-hill-prologue/` and the registry were
  committed. The 44 shared scene-system sources the registry depends on are still untracked from
  before, so a fresh clone would fail `unity:scene:sync`.
- **The scene and app on disk are an experimental variant**, whatever the last bisect rung built. A
  flagless `BuildCommittedNight` restores the documented control.
- **The prologue's systems are not in this scene.** It is the purchased village plus a player plus the
  night. The rare events, the estate and the beats still live in the retired builder's scene.
- **Running the walk needs a shell timeout longer than 2 minutes.** Three attempts in a row died at
  exactly 15 frames / 210m before this was diagnosed: the walk's own cap is 480s and the pipeline
  (settle + traversal + 30-some screenshot readbacks) routinely runs past a 2-minute default, so the
  shell kills the player mid-route rather than the walk finishing on its own. Both full walks this
  session used a 600s timeout on the direct binary launch and completed cleanly to the water every time.
