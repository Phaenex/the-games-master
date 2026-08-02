export const meta = {
  name: 'the-ninth-bell',
  description: 'Build + admin-review The Ninth Bell: 9 tolls, symptoms, crossing, wake room, branch beats',
  phases: [
    { title: 'House02 magenta' },
    { title: 'Bell audio' },
    { title: 'GmBellSummons' },
    { title: 'Symptoms' },
    { title: 'Threshold rewrite' },
    { title: 'The crossing' },
    { title: 'Wake room' },
    { title: 'Branch beats' },
    { title: 'Invitation' },
    { title: 'Bell tests' },
    { title: 'Final sweep' },
  ],
}

const PREAMBLE = `
PROJECT: The Games Master — Unity 6000.5.3f1 / HDRP Steam game at /Users/damato/GamesMaster-Unity.
Web repo (design source, scripts, docs) at /Users/damato/Projects/the-games-master.

READ FIRST:
  docs/superpowers/specs/2026-07-17-the-ninth-bell.md   (WHY — canon)
  docs/superpowers/plans/2026-07-17-the-ninth-bell.md   (HOW — your task's section)

THE DESIGN IN ONE LINE: the invitation says nine o'clock; the chapel bell tolls nine; on the ninth
toll the player is taken wherever they stand and comes to inside the house as a longcase clock
finishes its own ninth chime. The bell and the clock are the same nine. Nine is the house COUNTING
(nine names in the ledger, nine tiles in Shut the Box, nine portraits in Court, COUNTED OUT on the
child's marker).

HARD RULES — violating any is a failure:
1. NEVER COMMIT. NEVER git add. Standing owner rule. The plan contains commit steps — IGNORE THEM.
2. SANDBOX: /Users/damato/GamesMaster-Unity is OUTSIDE the Bash sandbox write allowlist. EVERY Unity
   command and EVERY Blender/python-writing-there command needs dangerouslyDisableSandbox: true.
3. UNITY IS SINGLE-INSTANCE. Only ever drive it via 'node scripts/unity-cli.mjs <task>' from
   /Users/damato/Projects/the-games-master. Never launch two at once.
4. EVIDENCE BEFORE CLAIMS. Only say something works if you RAN it and READ real output. Never claim a
   visual result without opening the PNG with the Read tool and looking. Say so when you cannot verify.
5. Do not touch Assets/GamesMaster/ShutTheBox/. Do not weaken a test to make it pass.

COMMANDS (from /Users/damato/Projects/the-games-master, sandbox OFF):
  node scripts/unity-cli.mjs rebuild | tour | test
  npm run test:all
Logs: ~/GamesMaster-Unity/Logs/cli-<task>.log

HARD-WON CONTEXT — do not re-derive, do not re-break:
- SAVE-OR-IT-DIDN'T-HAPPEN: editor code that mutates an asset (material, volume profile, importer)
  MUST call AssetDatabase.SaveAssets() or the change lives only in that batchmode process and dies.
  This bit us twice: the night volume (components:[]) and the mansion's warmed windows.
- unity-cli's editor-open guard ignores Unity's helper processes (-adb2 / AssetImportWorker /
  ShaderCompiler). They linger for hours at 0% CPU and are NOT an open editor.
- three.js faces -Z at yaw 0, Unity faces +Z. GmShotTour applies +180 YawToUnity. Don't "fix" the table.
- GmMansion's transform constants (RotX=180, RotY=90, PosX/PosY, SeatOnGround) are CORRECT. Leave them.
- Leartes ships built-in AND HDRP twins; built-in = Standard shader = magenta under HDRP. Go through
  GmEstateBuilderV2.FindAssetPrefab/PreferHdrp. NEVER mass-convert the packs (WitchVillage has 141
  built-in twins whose HDRP originals are in use — converting them wrecks the pack).
- Fixed exposure is deliberate: NightExposureEV=-3, MoonLux=1.7. NEVER switch to automatic — it
  re-brightens authored darkness, the exact bug the web build spent three days on.
`

const VERDICT = {
  type: 'object',
  properties: {
    pass: { type: 'boolean', description: 'true ONLY if you independently verified every acceptance check' },
    problems: { type: 'string', description: 'Specific actionable defects. Empty if pass.' },
    evidence: { type: 'string', description: 'What you actually ran/read and the real output you saw.' },
  },
  required: ['pass', 'problems', 'evidence'],
}

const TASKS = [
  {
    id: 'house02', title: 'House02 texture',
    build: `SM_House_02 (the coach house at z=50) is NO LONGER magenta (a prior pass converted its one
material to HDRP) but it now renders FLAT WHITE / untextured — visible in tour-09/10/11. Finish the job.
An editor script is ready: Assets/Editor/GmConvertHouse02.cs — READ it. It authors HDRP/Lit materials
with the pack's real T_WoodWall wood textures (read from DISK, because a flat already-converted material
has no _MainTex to copy) and binds them to THIS FBX ONLY via externalObjects. It does NOT touch shared
pack materials.
RUN IT (sandbox off):
  /Applications/Unity/Hub/Editor/6000.5.3f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -quit \\
    -projectPath /Users/damato/GamesMaster-Unity -executeMethod GmConvertHouse02.Run \\
    -logFile /Users/damato/GamesMaster-Unity/Logs/cli-house02.log
Grep that log for [GmHouse02] — it should log a base texture name per slot, not "will read flat". If it
fails or reads flat, FIX THE SCRIPT (never fall back to a bulk pack convert).
Then: node scripts/unity-cli.mjs rebuild — 'grep -c "EXPECT MAGENTA"' must be 0.
Then tour and LOOK at tour-11-coach-yard.png: the coach house must read as textured matte WOOD, not
flat white and not magenta.`,
    verify: `ADMIN REVIEW. Do not trust the builder. The bar here is TEXTURED, not merely non-magenta.
1. node scripts/unity-cli.mjs rebuild; grep -c 'EXPECT MAGENTA' must be 0.
2. node scripts/unity-cli.mjs tour; OPEN tour-11-coach-yard.png and LOOK.
3. CRITICAL — confirm the packs were NOT mass-converted:
   grep -rl 'guid: 0000000000000000f000000000000000' --include='*.mat' ~/GamesMaster-Unity/Assets/LeartesStudios/WitchVillage | wc -l
   Must still be ~141. A drop = the converter ran too wide = automatic FAIL.
PASS ONLY IF: 0 EXPECT MAGENTA, the coach house is not magenta in the shot, and WitchVillage's twin
count is unchanged. Report the numbers.`,
  },
  {
    id: 'audio', title: 'Bell audio',
    build: `Task 1 of the ninth-bell plan: generate the 5 placeholder sounds.
Create scripts/gen-bell-audio.py EXACTLY as the plan specifies (complete python is in the plan) and run:
  python3 scripts/gen-bell-audio.py ~/GamesMaster-Unity/Assets/Resources/Sfx
If Assets/Resources/Sfx does not exist yet (the intro plan's Task 11 may not have landed), find where
GmAmbience.Find() currently resolves clips and write there instead — then SAY SO in your report.
Then verify they are not silent, per the plan's Step 3 (ffmpeg volumedetect). Any max_volume near
-91 dB means a silent file: the generator ran and produced nothing, which the file's existence hides.`,
    verify: `ADMIN REVIEW of the bell audio.
1. Confirm heartbeat.ogg, ear_whine.ogg, clock_chime.ogg, whisper_bed.ogg exist where GmAmbience can
   find them (read GmAmbience.Find to see where that actually is — do not assume).
2. For EACH, run: ffmpeg -i <f> -af volumedetect -f null - 2>&1 | grep max_volume
   PASS needs each between about -1 dB and -14 dB. -91 dB (silence) is a FAIL.
3. Confirm durations are sane (heartbeat ~1.2s, whine ~4s, chime ~5.5s, whisper ~8s).
Report the real max_volume numbers.`,
  },
  {
    id: 'bell', title: 'GmBellSummons',
    build: `Task 2 of the ninth-bell plan: GmBellSummons + retire the old bell rare event.
Create Assets/Scripts/GmBellSummons.cs per the plan, add the GmAmbience.Clip() public accessor,
DELETE the chapel-bell rare event from GmRareEvents (it is superseded — the bell is now the clock,
shipping both rings the same bell for two reasons), and attach GmBellSummons in
GmEstateBuilderV2.BuildPlayerAndSystems before GmThreshold.
NOTE: GmBellSummons calls threshold.BeginCrossing() which does not exist yet (Task 'threshold'
creates it). If that breaks compilation, add a temporary no-op BeginCrossing() to GmThreshold now and
say so — do NOT delete the call.
Rebuild; zero 'error CS'.`,
    verify: `ADMIN REVIEW of GmBellSummons.
1. node scripts/unity-cli.mjs rebuild; grep 'error CS' — must be empty.
2. Read GmBellSummons.cs: confirm ONE authority owns the count (public int Toll), cadence is
   firstTollDelay=45 / tollInterval=30 (NOT test values), and the bell AudioSource is 3D
   (spatialBlend=1) positioned at the chapel (~28.9, ~30).
3. Confirm the old chapel-bell rare event is GONE from GmRareEvents (grep for bellTimer/bellTolls).
4. Confirm GmBellSummons is attached in GmEstateBuilderV2.
PASS ONLY IF all four hold.`,
  },
  {
    id: 'symptoms', title: 'Symptoms',
    build: `Task 3 of the ninth-bell plan: GmSymptoms.
Create Assets/Scripts/GmSymptoms.cs per the plan and add GmAmbience.SetBedVolume(float). READ
GmAmbience first to find its real bed AudioSource field names — do not guess them.
Attach GmSymptoms after GmBellSummons.
Then PROVE the curve fires (plan Step 4): temporarily set firstTollDelay=2, tollInterval=2, rebuild,
tour, and grep for '[GmBell] toll' and '[GmSymptoms] toll'. You must see tolls 1..9 in order with
symptoms rising monotonically and TAKEN at 9.
THEN PUT THE REAL VALUES BACK (45/30) and rebuild. A shipped test cadence rings nine bells in 18
seconds and destroys the scene's pace.`,
    verify: `ADMIN REVIEW of the symptoms.
1. Read GmSymptoms.cs: values must be a pure function of Toll (monotonic, cannot recover between
   tolls). Confirm it reads GmBellSummons.Toll and does NOT run its own timer.
2. CRITICAL: read GmEstateBuilderV2/GmBellSummons and confirm the cadence is back to
   firstTollDelay=45 and tollInterval=30. Test values (2/2) left in = automatic FAIL.
3. node scripts/unity-cli.mjs rebuild; grep 'error CS' — empty.
4. Confirm the SymptomVolume uses a RUNTIME profile (vol.profile, not an .asset) — if it creates an
   asset it must SaveAssets or it will silently do nothing, the trap that bit the night volume twice.
PASS ONLY IF all four hold. Report the actual cadence values you read.`,
  },
  {
    id: 'threshold', title: 'Threshold rewrite',
    build: `Task 4 of the ninth-bell plan: rewrite GmThreshold.
KEEP: the gate lock (first backward move past gateZ, gate_slam + gate_lock one-shots, the beat).
ADD: bell.Arm() at the gate lock — the bell can only count someone the house already has, so the
     seventh state (retreating to the car before the gate) still beats it outright.
ADD: public void BeginCrossing() — called by GmBellSummons on toll 9; starts GmCrossing.
DELETE: the arrivalZ auto-settle glide, glimpseSaid/thresholdSaid/knockStarted, the collar beats,
     ko_thud, and the camera-roll knockdown. The porch KO is retired.
KEEP the porch as a BEAT not an ending (the plan has the exact copy): the doors still never open —
that is locked canon — you just stand there and the hour arrives anyway, which is worse.
Rebuild; zero 'error CS'.`,
    verify: `ADMIN REVIEW of the threshold rewrite.
1. Read GmThreshold.cs. Confirm: gate lock intact (gate_slam + gate_lock still referenced);
   bell.Arm() called at the gate lock; BeginCrossing() exists; the collar/ko_thud/settle-glide KO is
   GONE; the porch still fires a beat and the doors still never open.
2. node scripts/unity-cli.mjs rebuild; grep 'error CS' — empty.
3. Confirm nothing calls ko_thud any more (grep the whole Assets/Scripts).
PASS ONLY IF all three hold.`,
  },
  {
    id: 'crossing', title: 'The crossing',
    build: `Task 5 of the ninth-bell plan: GmCrossing.
Create Assets/Scripts/GmCrossing.cs per the plan and wire GmThreshold.BeginCrossing() to start it.
Attach GmCrossing in the builder.
Sequence discipline (this is the most important seam in the Prologue — get it wrong and it is a
loading screen): CUT to black (not a fade), ~2s of nothing, hearing back wrong-ended, whispers in the
dark, sight irising in badly, and the clock's NINTH chime resolving as vision does. The bell's ninth
toll and the clock's ninth chime are the same nine in two rooms — they must OVERLAP, not queue.
Verify with the 2s test cadence, then RESTORE 45/30.`,
    verify: `ADMIN REVIEW of the crossing.
1. Read GmCrossing.cs: confirm it CUTS to black (fade=1 immediately, not a lerp), has dead air, plays
   whisper_bed then clock_chime, irises sight back in, and re-enables the player at the end.
2. A crossing that never re-enables GmPlayer softlocks the game — verify it does.
3. node scripts/unity-cli.mjs rebuild; grep 'error CS' — empty.
4. Confirm the cadence is 45/30, not test values.
PASS ONLY IF all four hold.`,
  },
  {
    id: 'wake-room', title: 'Wake room',
    build: `Task 6 of the ninth-bell plan: the wake room (Entry Hall STUB — not the Entry Hall).
Create Assets/Editor/GmWakeRoom.cs. Build a small dark interior far from the estate (z ~ +400 so its
walls cannot leak into the grounds' shots), with a floor he wakes on, ONE warm light, and a LONGCASE
CLOCK, prominent and visible from the wake pose. Use GmEstateBuilderV2.FindAssetPrefab (prefers HDRP).
Search the owned library for a clock:
  find /Users/damato/Projects/the-games-master/assets/models/unity -iname '*clock*'
If nothing suitable exists, build a placeholder from primitives and LOG LOUDLY that it is a
placeholder. Do not quietly ship a box and call it a clock.
Create a 'WakeRoom/WakePose' GameObject at eye height facing the clock, and teleport the player there
in GmCrossing during the dead air (the plan has the code — note CharacterController must be disabled
around the teleport or it fights it).
DO NOT use the line "Cold. The marble had my cheek." — that beat belongs to Phase 1's real Entry Hall.
Verify by measurement: temp editor script logging WakePose's world position and confirming a floor
renderer below it. DELETE the temp script after.`,
    verify: `ADMIN REVIEW of the wake room.
1. node scripts/unity-cli.mjs rebuild; grep '[GmWakeRoom]' and 'error CS'.
2. Confirm GameObject 'WakeRoom/WakePose' exists in the built scene (write your own temp editor check
   if needed, and delete it after).
3. Confirm the room is far from the estate (z ~ +400) so it cannot appear in the grounds' tour shots.
4. Run the tour and confirm the 12 grounds shots are UNCHANGED by this — no interior walls leaking in.
5. Confirm whether the clock is a real model or a primitive placeholder, and that the log says which.
PASS ONLY IF the pose exists, the room is isolated, and the tour is unaffected. Report which the clock is.`,
  },
  {
    id: 'branch-beats', title: 'Branch beats',
    build: `Task 7 of the ninth-bell plan: branch beats.
Add the 7 branchBeats to BOTH design-data copies (unity/design-data/prologue-design.json AND
~/GamesMaster-Unity/Assets/StreamingAssets/prologue-design.json) — the plan has the exact JSON — then
diff to prove identical.
Hydrate them in GmDesignRuntime and fire each once on rect entry (reuse ShowBeat).
CRITICAL TRAP the plan flags: GmDesignRuntime.ParseWalkRects uses a regex over 4-float arrays, which
will now ALSO match branchBeats[].rect. If unscoped, walk bounds silently gains 7 extra boxes. The log
must still read rects=8 — if it reads 15, scope the parse to the walkRects key.
Rebuild + tour; grep [GmDesignRuntime]. Expect rects=8 and branchBeats=7.`,
    verify: `ADMIN REVIEW of the branch beats.
1. diff the two design-data copies — MUST be identical.
2. node scripts/unity-cli.mjs rebuild + tour; grep '[GmDesignRuntime]'.
   MUST read rects=8 (NOT 15) and branchBeats=7. rects=15 means the walk-rect regex ate the branch
   rects and the walk bounds are now wrong — automatic FAIL.
3. grep '[GmV2] walk bounds' — the wall count must not have jumped (it was 32 from 8 rects).
PASS ONLY IF rects=8, branchBeats=7, and the bounds are unchanged. Report the real log line.`,
  },
  {
    id: 'invitation', title: 'Invitation',
    build: `Task 8 of the ninth-bell plan: the invitation card.
Add the 5th coldOpen entry to BOTH design-data copies — the plan has the exact string. Canon
(house-history.md): NEVER a street address, never a family name for the house. "Wend" is an old word
for a turn in a road, hence "Take the old road. It will turn you." No "do not be late" — the house
never names the compulsion. "— a friend" is the letter's three-stage-reveal signature (story bible
§6) and is NOT "the friend in the walls".
diff both copies, rebuild + tour, grep [GmDesignRuntime] — expect coldOpen=5.`,
    verify: `ADMIN REVIEW of the invitation.
1. diff the two design-data copies — identical.
2. rebuild + tour; grep '[GmDesignRuntime]' — MUST read coldOpen=5.
3. Read the card text. Confirm: no street address, no house family name, no "do not be late",
   signed "— a friend", contains "Nine o'clock".
PASS ONLY IF all three hold.`,
  },
  {
    id: 'bell-tests', title: 'Bell tests',
    build: `Task 9 of the ninth-bell plan: add the 3 bell tests to
Assets/Tests/EditMode/GmEstateBuildTests.cs — BellSummonsAndCrossingExist, BellCadenceIsShippable,
WakeRoomExistsWithAPose. The plan has the complete C#.
BellCadenceIsShippable is the important one: it catches a test cadence (2s) left shipped, which would
ring nine bells in 18 seconds and is invisible in any log.
Run: node scripts/unity-cli.mjs test — expect 35/35 (23 Shut the Box + 9 estate + 3 bell).
Do NOT weaken any assertion to force a pass. If a test fails, fix the CODE.`,
    verify: `ADMIN REVIEW of the bell tests.
1. node scripts/unity-cli.mjs test yourself. Read the real count. Expect 35/35.
2. Read GmEstateBuildTests.cs. Confirm no assertion was weakened, deleted or commented out — compare
   against the plan's stated intent. In particular BellCadenceIsShippable must still require >= 20s.
3. Confirm GmEstateBuilderV2's AddOverride<T> is intact (not left reverted from any bite-test).
PASS ONLY IF the count is real, assertions intact, and the builder is not left broken.`,
  },
  {
    id: 'defects', title: 'Grounds defects',
    build: `Two real placement defects the overnight review found by eye (not magenta, not lighting):

1. THE CAR CLIPS INTO THE ROADSIDE FENCE. tour-02-car.png and tour-04-lookback.png show the car's
   hood/bumper embedded in the wooden fence — reads as a glitch, not "abandoned across the verge". The
   car is at design-data (x=-4.8, z=76.5). The fence run (BuildFenceRun in GmEstateBuilderV2) places
   posts at x=+/-3.9. So the car at x=-4.8 overlaps the x=-3.9 fence line. FIX: either nudge the car
   clear of the fence (more negative x, e.g. -6.5, updating BOTH design-data copies), or add a gap in
   the fence run around the car's z (like the existing side-path openings in BuildFenceRun). Prefer the
   fence gap — the car's x=-4.8 is authored and other things may depend on it. Whichever you pick,
   verify by opening tour-02-car.png and confirming the car is clear of the fence.

2. A FLOATING BLACK SLAB DOMINATES THE CEMETERY. tour-06-cem-path.png and tour-07-cem-inside.png show a
   large flat black horizontal slab at ~eye height, unlit, no support. The review confirmed it is NOT
   the invisible walk-bounds collider (those are BoxColliders with no renderer). Find the real prop:
   check BuildPlacements and the cemetery-area assets (the open grave SM_Pit, monument, headstones) for
   one that imported mis-scaled or mis-rotated (possibly the same 180-about-X FBX issue the mansion had,
   if it came through the Blender pipeline). Identify it by position (cemetery is x 8.6..24.6, z 12.6..40.6),
   fix its transform, and confirm by opening tour-06 and tour-07 that the slab is gone.

Do NOT guess — for each, identify the actual offending object (write a temp diagnostic editor script
that logs positions/bounds in the cemetery region if needed, and DELETE it after). Rebuild + tour +
LOOK after each fix.`,
    verify: `ADMIN REVIEW of the grounds defects.
1. node scripts/unity-cli.mjs rebuild + tour yourself.
2. OPEN tour-02-car.png AND tour-04-lookback.png — the car must be clear of the fence, not embedded.
3. OPEN tour-06-cem-path.png AND tour-07-cem-inside.png — no floating black slab.
4. Confirm placements is still '17 placed, 0 missing' (a fix must not drop a prop) and any design-data
   edit kept BOTH copies identical (diff them).
PASS ONLY IF the car is clear, the slab is gone, 17/0 holds, and data copies match. Report what you saw
in each of the 4 shots.`,
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
      PREAMBLE + '\n\nYou are the ADMIN REVIEWER. You did not do this work and must not trust it. ' +
      'Re-run every check yourself and read real output before judging. A blocked check is NOT a pass.\n\n' + t.verify,
      { label: `admin:${t.id}#${round}`, phase: t.title, model: 'sonnet', schema: VERDICT }
    )
    if (!verdict) { log(`${t.id}: admin died round ${round}`); break }
    if (verdict.pass) { log(`${t.id}: PASS round ${round}`); break }
    log(`${t.id}: REJECTED round ${round}: ${String(verdict.problems).slice(0, 160)}`)
    if (round === 3) break
    await agent(
      PREAMBLE + '\n\nThe admin reviewer REJECTED your work. Fix it.\n\nPROBLEMS:\n' + verdict.problems +
      '\n\nEVIDENCE THEY SAW:\n' + verdict.evidence + '\n\nORIGINAL TASK:\n' + t.build +
      '\n\nFix, re-verify yourself, report what you changed.',
      { label: `fix:${t.id}#${round}`, phase: t.title, model: 'sonnet' }
    )
  }
  results.push({ id: t.id, pass: verdict ? !!verdict.pass : false, problems: verdict ? verdict.problems : 'admin died' })
}

phase('Final sweep')
const sweep = await agent(PREAMBLE + `
FINAL VERIFICATION SWEEP for The Ninth Bell. Verify end to end and report the truth, however unwelcome.

1. npm run test:all                    (expect C# 23, JS 23, browser 47)
2. node scripts/unity-cli.mjs test     (expect 35/35)
3. node scripts/unity-cli.mjs rebuild — grep for: 'placements:' (want 17 placed, 0 missing),
   'EXPECT MAGENTA' (want 0), 'night volume' (want 4 overrides, moon 1.7 Lux, EV -3),
   'walk bounds', '[GmMansion]', '[GmWakeRoom]'
4. grep [GmDesignRuntime] from a tour log — want pois=13 beats=5 rects=8 coldOpen=5 branchBeats=7
5. CONFIRM THE SHIPPING CADENCE: read GmBellSummons — firstTollDelay MUST be 45 and tollInterval MUST
   be 30. Test values left in would ring nine bells in 18 seconds. This is the single easiest thing to
   leave broken and the hardest to notice.
6. node scripts/unity-cli.mjs tour, then OPEN ALL TWELVE PNGs and LOOK at each. Not a sample.

Judge honestly, as if the owner walks this in minutes:
- Lit, occupied house at the end of the drive? Gate a real threshold? Car abandoned-looking?
- Anything magenta, floating, buried, inverted, absurdly scaled?
- Night dark but navigable, or mud?
- Does it read as a horror opening or a kit-bash field with props on it?

Report every command's real numbers, a per-shot verdict for all 12, and a RANKED list of what is
still wrong. Do not claim anything you did not observe. Confirm you committed nothing.`,
  { label: 'final-sweep', phase: 'Final sweep', model: 'sonnet' })

return { results, sweep }
