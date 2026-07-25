# Wend Hill night: the ladder restart, and the bug that was making everything else unreadable

Written 2026-07-25. Everything here is in `Assets/Scenes/WendHill_Prologue.unity`, built by
`GmWendBuilder` from the purchased Abandoned Village. Nothing is committed. Nothing is persisted to
the scene file yet either, which is covered under Still open.

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

Not verified, and this list matters more than the one above:

- **Whether the NavMesh actually improves the walk.** The bake covers 67279m^2 and the probe compiles
  against it, but no walk has been run since. The 188m of 580m number is UNCHANGED until someone builds
  the player and walks it. Pathing that bakes and never gets walked is a plausible fix, not a fix.
- **Whether the walls hold.** They exist in the saved scene by value. Nobody has walked into one.
- **Anything about frame time.** The walk probe now samples traversal pacing and writes
  `walk-performance.json`, but it has not been run, so there are no numbers. The perf harness exists;
  the perf pass does not.
- **Anything by ear.** Check 9 fixes WHICH listener is live. It says nothing about whether the scene
  makes a sound.
- Whether transmission through a neutral diffusion profile makes backlit leaves too bright is still
  open, since the leaf highlights do still clip at EV -2, and that can only be judged at whichever
  exposure gets chosen.

## Still open

- **The walk still reads 188m of a 580m route with 9 stalls, because it has not been re-run.** The
  NavMesh is baked and the probe paths through it, so the number is expected to move, but expected is
  not measured. THE NEXT THING TO DO IS BUILD THE PLAYER AND WALK IT. That single run also produces the
  first frame-time numbers and the first distance-named frames, so it closes three open items at once.
- **The boundary is untested by a player.** Four walls and a catch height are in the saved scene and the
  contract passes on them by value. Nobody has walked into a wall or fallen through a gap.
- **Saves are not wired into this scene.** `GmVillageSave` is added by `GmVillageBuilder` only, which is
  the retired village builder, so no save component exists in the prologue at all. Its restore decision
  is now pure and tested, both refusals included, because both fail silently: a save from another scene
  drops the player at coordinates that mean something else here, and a save taken at the spawn moves
  nobody while looking like it loaded. Whether the prologue SHOULD have saves yet is a design call and
  has not been made.
- **The spawn moved.** It is now the start of the route, at the sparse end, walking into the village. It
  used to be the centroid of the road network, which is not a place. Every frame in this document that
  predates that change was shot from the old spawn.
- **The player is verified for ONE frame, at spawn.** Whole-frame luma 0.059 against the editor's 0.059,
  ratio 1.01, band ratios 0.88 to 1.08. What has not been checked is the rest of the walk, anything in
  motion, or any other vantage. One frame proves the camera and the exposure reach the player; it does
  not prove the scene holds up for 180m.

  Capture it with the app's own probe, never with `screencapture`:

      "Builds/macOS-Wend/Wend Hill Prologue.app/Contents/MacOS/The Games Master" \
        -gmVillageSelfProbe /tmp/shot.png

  `GmVillageSelfProbe` reads the app's own backbuffer and needs no screen-recording permission. Its
  header already documented that macOS `screencapture` cannot capture third-party window content in this
  automation environment, and that was rediscovered the hard way over three wasted attempts. The name
  says Village because it predates this scene; it is generic and it works here unchanged.
- **The forward-view review reading** is unexplained, per the section above.
- **The lamp line** may want its own rung. It is the loudest feature in every bracket frame.
- **No perf pass, but the harness is in place.** The walk probe samples traversal frame pacing and
  writes `walk-performance.json` alongside the frames, excluding screenshot and sidestep intervals
  because those measure the harness rather than the game. Genuine hitches are deliberately not filtered
  by magnitude. It produces nothing until a walk runs. Still no memory or culling measurement.
- **No audio verified by ear.** The scene now mixes from the player's ear rather than one of 30
  listeners, which is a different claim and a smaller one.
- **The prologue's systems are not in this scene.** It is the purchased village plus a player plus the
  night. The rare events, the estate and the beats still live in the retired builder's scene.
- **The Wend sources are in the repository but not committed.** They are tracked files under
  `unity/scenes/wend-hill-prologue/` now rather than loose on disk, and `npm run unity:scene:check`
  passes on 61 files. The repo working tree also carries a large amount of unrelated uncommitted work,
  so nothing was swept into a commit.
- **`GmVillageSave.cs` is still outside version control.** It lives in `Assets/Scripts/` in the Unity
  project, which is not part of the synced source set, so the restore-decision extraction made to it
  this session is unversioned even though its tests are not.
