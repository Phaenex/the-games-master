export const meta = {
  name: 'prologue-polish',
  description: 'Art + defect polish and finish the bell — gated on how it LOOKS, judged by eye',
  phases: [
    { title: 'Coach house check' },
    { title: 'Car parking' },
    { title: 'Cemetery slab' },
    { title: 'Atmosphere' },
    { title: 'Flank density' },
    { title: 'Wake room' },
    { title: 'Branch beats' },
    { title: 'Invitation' },
    { title: 'Bell tests' },
    { title: 'Final sweep' },
  ],
}

const PREAMBLE = `
PROJECT: The Games Master — Unity 6000.5.3f1 / HDRP Steam horror game.
Repo (scripts, docs, authored C#) at /Users/damato/Projects/games/the-games-master. The Unity project
is INSIDE that repo at unity-project/ — there is no separate ~/GamesMaster-Unity tree. Authored source
lives in unity/ and syncs into unity-project/ (npm run unity:scene:sync). Edit the repo copy, never the
synced copy.

THE STANDARD FOR THIS RUN IS DIFFERENT: the owner said the scene "looks like ass". Correctness is not
the bar — it must LOOK like the opening of a real horror game. "It renders / 0 magenta / tests pass" is
necessary but NOT sufficient. Every art task is judged by OPENING THE SCREENSHOT AND LOOKING.

WHAT ALREADY LOOKS GOOD (do not regress these): the mansion stands textured and lit at the end of a
tree-lined drive; the gate is now dark wrought iron silhouetted against the night (a prior white-plastic
bug, fixed); the night reads dark-but-navigable. tour-01-spawn.png and tour-05-middrive.png are the
reference for "good" — your changes must keep those at least as strong.

HARD RULES — violating any is a failure:
1. NEVER COMMIT. NEVER git add. Standing owner rule. Ignore any commit steps in docs.
2. SANDBOX: unity-cli.mjs shells out to 'ps' for its editor-closed guard, and a sandbox that blocks
   process listing kills every Unity gate with no log (docs/TESTING.md). EVERY Unity command and EVERY
   Blender command needs dangerouslyDisableSandbox: true. Plain file writes are fine sandboxed.
3. UNITY IS SINGLE-INSTANCE. Only drive it via 'node scripts/unity-cli.mjs <task>' from the repo root.
   Never launch two at once. If it says the editor is open, STOP and report.
4. EVIDENCE BEFORE CLAIMS. Never claim a visual result without OPENING the PNG with the Read tool and
   looking. "meanLum is fine" is NOT a visual verdict. Say so when you cannot verify.
5. Do not touch Assets/GamesMaster/ShutTheBox/. Do not weaken a test to pass it.

COMMANDS (from the repo root, sandbox OFF):
  node scripts/unity-cli.mjs rebuild | tour | test
Screenshots land at unity-project/Screens/WendHill/tour-*.png. The tour waypoint table
(unity/project/Assets/Scripts/GmShotTour.cs) is 18 shots — tour-01-spawn through tour-18-figure-cutoff-off.
Logs at unity-project/Logs/.

HARD-WON CONTEXT — do not re-derive, do not re-break:
- SAVE-OR-IT-DIDN'T-HAPPEN: editor code that mutates an asset (material/volume profile/importer) MUST
  call AssetDatabase.SaveAssets() or the change dies with the batchmode process.
- Leartes packs ship built-in AND HDRP twins; built-in = Standard shader = magenta. Go through
  GmEstateBuilderV2.FindAssetPrefab/PreferHdrp. NEVER mass-convert the packs (WitchVillage's 141
  built-in twins are in use — converting them wrecks the pack).
- FBX from our Blender pipeline arrives rotated 180 about X and unit-scaled wrong; GmModelImportSettings
  forces useFileScale=false for Assets/GamesMaster/ and GmMansion applies the rotation. Props placed via
  the Blender pipeline may need the same rotation handling.
- Fixed exposure is deliberate: NightExposureEV=-3, MoonLux=1.7 in GmEstateBuilderV2. Do NOT switch to
  automatic and do NOT change these to "fix" a lighting problem in one prop — that darkens everything.
- unity-cli's editor-open guard already ignores Unity helper processes (-adb2/AssetImportWorker).
`

const VERDICT = {
  type: 'object',
  properties: {
    pass: { type: 'boolean', description: 'true ONLY if you independently verified every check AND (for art) it genuinely looks good by eye' },
    problems: { type: 'string', description: 'Specific actionable defects. Empty if pass.' },
    evidence: { type: 'string', description: 'What you ran/read/looked at and the real output/visual you saw.' },
  },
  required: ['pass', 'problems', 'evidence'],
}

const TASKS = [
  {
    id: 'coach', title: 'Coach house check',
    build: `The coach house SM_House_02 (z=50) had a flat-white-untextured bug. An editor script exists to fix
it: Assets/Editor/GmConvertHouse02.cs — it textures the walls with the pack's real wood maps AND disables
a stray convex-hull renderer that was rendering flat over the mesh. RUN it, then verify it looks right:
  /Applications/Unity/Hub/Editor/6000.5.3f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -quit \\
    -projectPath /Users/damato/Projects/games/the-games-master/unity-project -executeMethod GmConvertHouse02.Run \\
    -logFile /Users/damato/Projects/games/the-games-master/unity-project/Logs/cli-house02.log
grep [GmHouse02] in that log (want a base-texture name per slot, not "will read flat"). Then rebuild + tour
and OPEN tour-11-coach-yard.png and tour-10-well-shed.png: the coach house must read as textured matte
WOOD, not flat white, not magenta.`,
    verify: `ADMIN REVIEW. Bar is TEXTURED WOOD, not merely non-magenta.
1. rebuild yourself; grep -c 'EXPECT MAGENTA' must be 0.
2. tour yourself; OPEN tour-11-coach-yard.png AND tour-10-well-shed.png and LOOK.
3. Confirm WitchVillage still has ~141 built-in twins (no mass convert):
   grep -rl 'guid: 0000000000000000f000000000000000' --include='*.mat' unity-project/Assets/LeartesStudios/WitchVillage | wc -l
PASS ONLY IF the coach house is textured wood in both shots and WitchVillage is unchanged. Report what you saw.`,
  },
  {
    id: 'car', title: 'Car parking',
    build: `The car clips into the roadside fence — reads as a glitch, not "abandoned". The car is at design-data
(x=-4.8, z=76.5); the fence run in GmEstateBuilderV2.BuildFenceRun places posts at x=+/-3.9, so the car at
x=-4.8 overlaps the x=-3.9 fence line. FIX by carving a gap in the fence run around the car's z (same pattern
as the existing side-path openings in BuildFenceRun — look for the 'if (x > 0 && Mathf.Abs(z - ...' skips and
add one for the car). Do NOT move the car (x=-4.8 is authored). Rebuild + tour + OPEN tour-02-car.png and
tour-04-lookback.png: the car must sit clear of the fence, reading as abandoned on the verge.`,
    verify: `ADMIN REVIEW of the car.
1. rebuild + tour yourself; grep 'placements:' must still read '17 placed, 0 missing'.
2. OPEN tour-02-car.png AND tour-04-lookback.png and LOOK: the car is clear of the fence, not embedded.
PASS ONLY IF the car reads clean in both shots and 17/0 holds. Report what you saw.`,
  },
  {
    id: 'slab', title: 'Cemetery slab',
    build: `A large flat black slab floats at eye height in the cemetery (tour-06-cem-path.png, tour-07-cem-inside.png).
It is NOT a walk-bounds collider (those have no renderer). Find the real offending prop: it is in the cemetery
region (x 8.6..24.6, z 12.6..40.6). Likely a placement (SM_Pit open grave, monument, or a headstone) that
imported mis-scaled or mis-rotated. Write a TEMP editor script that logs the name/position/bounds of every
renderer whose center is in that region, run it, identify the slab by its oversized flat bounds, fix its
transform (or its targetHeight in the design data if that's the cause), and DELETE the temp script. Rebuild +
tour + OPEN tour-06 and tour-07: the slab must be gone and the cemetery readable.`,
    verify: `ADMIN REVIEW of the cemetery slab.
1. rebuild + tour yourself; OPEN tour-06-cem-path.png AND tour-07-cem-inside.png and LOOK.
2. Confirm no giant floating black slab; the graves/monument/headstones read as intended.
3. grep 'placements:' still '17 placed, 0 missing' (a fix must not drop a prop).
PASS ONLY IF the slab is gone in both shots and 17/0 holds. Report what you saw.`,
  },
  {
    id: 'atmosphere', title: 'Atmosphere',
    build: `Add horror ATMOSPHERE without washing out the scene. Currently the night is a clean blue gradient with
thin fog — it reads dark-but-flat. Goal: low GROUND MIST rolling across the drive and grounds for mood, and a
touch more depth haze, while keeping the mansion's warm windows and the dark gate silhouette exactly as strong
as they are now (tour-01/05 are the reference for "already good").
The fog is in GmEstateBuilderV2.BuildLightingAndSky (HDRP Fog volume override: meanFreePath=55, albedo cool
blue, baseHeight=0, maximumHeight=35). Options: lower maximumHeight so fog hugs the ground as mist (~8-14),
and/or add a HDRP Local Volumetric Fog volume along the drive for a rolling ground layer. Keep NightExposureEV=-3
and MoonLux=1.7 UNCHANGED. Iterate: change, rebuild, tour, OPEN tour-01-spawn / tour-05-middrive / tour-12-porch
and LOOK. If any of those three got worse (washed out, house windows dimmed, gate no longer reads), REVERT and
try gentler. The bar: moodier than now, NOT muddier.`,
    verify: `ADMIN REVIEW of the atmosphere — the highest-risk task for regressing the good look.
1. rebuild + tour yourself; OPEN tour-01-spawn.png, tour-05-middrive.png, tour-12-porch.png and LOOK at all three.
2. Confirm the mansion's warm windows still read strong, the dark iron gate still silhouettes, and the scene is
   NOT washed out/gray/muddy. Moodier ground mist = good; a bright gray haze over everything = FAIL.
3. Read GmEstateBuilderV2: NightExposureEV must still be -3 and MoonLux still 1.7 (changing them to compensate
   is an automatic FAIL).
PASS ONLY IF it looks moodier AND none of the three reference shots regressed AND the exposure constants are
untouched. Be strict — a wash-out here undoes the best part of the scene. Report what you saw in each shot.`,
  },
  {
    id: 'density', title: 'Flank density',
    build: `The land beyond the drive fence reads empty/flat — it fades to fog with sparse dead trees, which looks
under-dressed. Add density on the flanks WITHOUT cluttering the drive itself or the cemetery/garden play areas.
In GmEstateBuilderV2, the tree scatter is BuildTreeLines (avenue + mid-flank scatter of 46). Increase the flank
scatter count and/or add low groundcover/debris (owned Leartes props via FindAssetPrefab — bushes, rocks, dead
foliage, roots) in the empty regions OUTSIDE the walk rects, so the world feels inhabited-then-abandoned rather
than a flat plane with a few trees. Keep the drive sightline to the mansion CLEAR (do not block the z=72→-58 view
corridor). Rebuild + tour + OPEN tour-01, tour-05, tour-09-gdn-inside, tour-11-coach-yard and LOOK: fuller, still
navigable, mansion still visible down the drive.`,
    verify: `ADMIN REVIEW of the flank density.
1. rebuild + tour yourself; OPEN tour-01-spawn.png, tour-05-middrive.png, tour-11-coach-yard.png and LOOK.
2. Confirm the flanks read fuller/dressed, the mansion is STILL clearly visible down the drive (sightline not
   blocked), and the drive/paths are still walkable (not choked with props).
3. grep 'placements:' and the tree/scatter logs — nothing errored, no magenta introduced (grep EXPECT MAGENTA = 0).
PASS ONLY IF fuller AND mansion sightline preserved AND still navigable AND 0 magenta. Report what you saw.`,
  },
  {
    id: 'wake-room', title: 'Wake room',
    build: `Task 6 of docs/superpowers/plans/2026-07-17-the-ninth-bell.md: the wake room (Entry Hall STUB, NOT the
Entry Hall). Read that task. Create Assets/Editor/GmWakeRoom.cs: a small dark interior far from the estate
(z~+400 so it can't leak into grounds shots), a floor, ONE warm light, and a LONGCASE CLOCK visible from a
'WakeRoom/WakePose' GameObject at eye height. Use GmEstateBuilderV2.FindAssetPrefab. Search owned assets for a
clock: find assets/models/unity -iname '*clock*' from the repo root. If none, build a primitive
placeholder and LOG that it's a placeholder. Do NOT use "Cold. The marble had my cheek." (that's Phase 1).
Verify by measurement (temp script logging WakePose position + a floor renderer below it; delete after) and by a
tour confirming the 18 grounds shots are UNCHANGED (room isolated).`,
    verify: `ADMIN REVIEW of the wake room.
1. rebuild + tour yourself; confirm 'WakeRoom/WakePose' exists (write your own temp check, delete after).
2. Confirm the room is at z~+400 and the 18 grounds shots are UNCHANGED (no interior walls leaking in).
3. Report whether the clock is a real model or a primitive placeholder.
PASS ONLY IF the pose exists, the room is isolated, and the grounds tour is unaffected.`,
  },
  {
    id: 'branch-beats', title: 'Branch beats',
    build: `Task 7 of the ninth-bell plan: branch beats. Add the 7 branchBeats to BOTH design-data copies
(unity/design-data/prologue-design.json AND unity-project/Assets/StreamingAssets/prologue-design.json —
the plan has the exact JSON), diff to prove identical, hydrate in GmDesignRuntime, fire each once on rect entry.
CRITICAL TRAP: GmDesignRuntime.ParseWalkRects regex matches 4-float arrays and will also match branchBeats[].rect
— the walk-bounds log must still read rects=8, NOT 15. If it reads 15, scope the walk-rect parse to the walkRects
key. Rebuild + tour; grep [GmDesignRuntime] — expect rects=8 and branchBeats=7.`,
    verify: `ADMIN REVIEW of branch beats.
1. diff the two design-data copies — identical.
2. rebuild + tour; grep '[GmDesignRuntime]' — MUST read rects=8 (NOT 15) and branchBeats=7.
3. grep '[GmV2] walk bounds' — wall count not wildly changed (was ~32 from 8 rects).
PASS ONLY IF rects=8, branchBeats=7, bounds sane, copies identical.`,
  },
  {
    id: 'invitation', title: 'Invitation',
    build: `Task 8 of the ninth-bell plan: add the 5th coldOpen entry (the invitation card) to BOTH design-data
copies — the plan has the exact string. Canon: NO street address, no house family name, no "do not be late",
signed "— a friend", contains "Nine o'clock". diff both copies, rebuild + tour, grep [GmDesignRuntime] — expect
coldOpen=5.`,
    verify: `ADMIN REVIEW of the invitation.
1. diff the two copies — identical.
2. rebuild + tour; grep '[GmDesignRuntime]' — coldOpen=5.
3. Read the card: no street address, no family name, no "do not be late", signed "— a friend", has "Nine o'clock".
PASS ONLY IF all three hold.`,
  },
  {
    id: 'bell-tests', title: 'Bell tests',
    build: `Task 9 of the ninth-bell plan: add the 3 bell tests to Assets/Tests/EditMode/Editor/GmEstateBuildTests.cs
(BellSummonsAndCrossingExist, BellCadenceIsShippable, WakeRoomExistsWithAPose) — the plan has the C#. Note: the
bell scripts (GmBellSummons, GmSymptoms, GmCrossing, GmThreshold rewrite) from the prior run are already on disk;
confirm they're attached in GmEstateBuilderV2.BuildPlayerAndSystems and compile. BellCadenceIsShippable must
require tollInterval>=20 (catches a 2s test cadence). Run: node scripts/unity-cli.mjs test — report the real count.
Do NOT weaken assertions.`,
    verify: `ADMIN REVIEW of the bell tests.
1. node scripts/unity-cli.mjs test yourself; read the real count.
2. Read GmEstateBuildTests.cs: no assertion weakened/deleted; BellCadenceIsShippable requires >=20s.
3. Confirm GmBellSummons/GmCrossing exist and are attached (grep GmEstateBuilderV2).
PASS ONLY IF tests are green (or honestly report which fail and why), assertions intact, bell attached.`,
  },
]

const results = []
for (const t of TASKS) {
  phase(t.title)
  log(`starting ${t.id}`)
  await agent(PREAMBLE + '\n\nYOUR TASK:\n' + t.build, { label: `build:${t.id}`, phase: t.title, model: 'sonnet' })

  let verdict = null
  for (let round = 1; round <= 3; round++) {
    verdict = await agent(
      PREAMBLE + '\n\nYou are the ADMIN REVIEWER. You did not do this work and must not trust it. Re-run every ' +
      'check yourself and LOOK at the real screenshots before judging. A blocked or unlooked check is NOT a pass.\n\n' + t.verify,
      { label: `admin:${t.id}#${round}`, phase: t.title, model: 'sonnet', schema: VERDICT }
    )
    if (!verdict) { log(`${t.id}: admin died round ${round}`); break }
    if (verdict.pass) { log(`${t.id}: PASS round ${round}`); break }
    log(`${t.id}: REJECTED round ${round}: ${String(verdict.problems).slice(0, 150)}`)
    if (round === 3) break
    await agent(
      PREAMBLE + '\n\nThe admin reviewer REJECTED your work. Fix it.\n\nPROBLEMS:\n' + verdict.problems +
      '\n\nEVIDENCE THEY SAW:\n' + verdict.evidence + '\n\nORIGINAL TASK:\n' + t.build +
      '\n\nFix, re-verify yourself (LOOK at the shots), report what you changed.',
      { label: `fix:${t.id}#${round}`, phase: t.title, model: 'sonnet' }
    )
  }
  results.push({ id: t.id, pass: verdict ? !!verdict.pass : false, problems: verdict ? verdict.problems : 'admin died' })
}

phase('Final sweep')
const sweep = await agent(PREAMBLE + `
FINAL VERIFICATION SWEEP. Verify end to end and report the truth as if the owner walks it in minutes and is
already skeptical about how it looks.
1. node scripts/unity-cli.mjs test — report the real count.
2. node scripts/unity-cli.mjs rebuild — grep: 'placements:' (want 17/0), 'EXPECT MAGENTA' (want 0),
   'night volume' (want 4 overrides, EV -3, moon 1.7).
3. node scripts/unity-cli.mjs tour, then OPEN ALL EIGHTEEN PNGs and LOOK at each.
Judge each shot HONESTLY for whether it looks like a real horror opening: gate (dark iron, ominous?), house
(lit, occupied?), drive (moody, navigable?), grounds (dressed or empty?), coach house (textured or flat?),
cemetery (readable, no floating slab?), car (abandoned or glitched?). Rank what's still wrong, worst first.
Report every command's real numbers, a per-shot verdict for all 18, and the ranked list. Confirm nothing committed.`,
  { label: 'final-sweep', phase: 'Final sweep', model: 'sonnet' })

return { results, sweep }
