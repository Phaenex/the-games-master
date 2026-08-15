#!/usr/bin/env node
import { spawn } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { loadSceneRegistry } from './unity-scene-registry.mjs';

const REPO_ROOT = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
const unityRoot = process.env.GM_UNITY_PROJECT || path.join(REPO_ROOT, 'unity-project');
const scene = 'wend-hill-prologue';

// The tour writes to Screens/<sceneName>, and sceneName is the registry's to change. Hardcoding
// "WendHill_Prologue" in gate 12 agreed with the registry only by coincidence: rename sceneName and
// the tour writes a new folder, unity-cli only clears the current one so the old folder survives,
// and gate 12 would rescan those stale frames and report clean. Derive it, like unityRoot already is.
const sceneName = loadSceneRegistry().scenes.find((entry) => entry.id === scene)?.sceneName;
if (!sceneName) {
  console.error(`✗ scene '${scene}' has no sceneName in the registry — gate 12 has nothing to scan`);
  process.exit(1);
}
// Whether a failure here makes every LATER gate's evidence worthless.
//
// The pipeline used to stop dead at the first failure, on the reasoning that every later gate
// depends on the earlier artifact. That is true of the gates that BUILD things and false of the
// gates that JUDGE them, and the difference costs real coverage: gate 8's perf budget has been
// missed all day, so gates 9, 10, 11 and 12 have not run once. The house-entry proof, the
// full-route proof, the boundary-wall proof and the render-defect scan produced no evidence at all,
// because a frame-time percentile was 4ms high.
//
// This project has already paid for this exact shape once. Court's tests failed by design behind a
// content lock, gate 2 broke on first failure, and gates 3-12 were structurally unreachable for
// weeks -- recorded in TESTING.md as "the gate-2 deadlock". Same trap, one gate along.
//
// BLOCKING stays the default and every non-blocking gate carries its reason. The run still FAILS,
// loudly, on any failure; it just stops throwing away the evidence that would have followed.
const BLOCKING = true;
const CONTINUES = false;

const gates = [
  ['portable source and archive checks', 'npm', ['run', 'verify:portable'], BLOCKING],
  ['Unity EditMode', 'npm', ['run', 'test:unity'], BLOCKING],
  ['canonical Unity PlayMode', 'node', ['scripts/unity-cli.mjs', 'playtest'], BLOCKING],
  // Builds the scene every gate below reads. A stale or half-written scene poisons all of them.
  ['canonical scene rebuild', 'node', ['scripts/unity-cli.mjs', 'rebuild', scene], BLOCKING],
  // A verdict ON the scene, not a producer of anything. Its failure is worth knowing alongside the
  // others rather than instead of them.
  ['saved-scene contract', 'node', ['scripts/unity-cli.mjs', 'audit', scene], CONTINUES],
  ['player-camera visual tour', 'node', ['scripts/unity-cli.mjs', 'tour', scene], CONTINUES],
  // Produces the .app that gates 8-11 all run. Nothing below this means anything without it.
  ['canonical macOS build', 'node', ['scripts/unity-cli.mjs', 'build-mac', scene], BLOCKING],
  // These four run the app and each writes its own frames. They do not read each other's verdicts,
  // and gate 12 scans whatever frames exist -- it already rejects a missing or empty directory, so
  // a proof that died before writing anything is still caught, by the gate built to catch it.
  ['standalone story/input/audio proof', 'node', ['scripts/unity-cli.mjs', 'standalone-proof', scene], CONTINUES],
  ['house entry and first-game proof', 'node', ['scripts/unity-cli.mjs', 'house-proof', scene], CONTINUES],
  ['full-route 1080p performance proof', 'node', ['scripts/unity-cli.mjs', 'walk-proof', scene], CONTINUES],
  ['standalone boundary-wall proof', 'node', ['scripts/unity-cli.mjs', 'wall-proof', scene], CONTINUES],
  // Runs LAST because it reads what every gate above produced. Percentile luminance proves
  // brightness and nothing else: a magenta object survived every green gate run on 2026-08-03 by
  // hiding in screenshots behind an opaque UI panel, while black-void gate piers scored "ok".
  // Split in two because the two halves are judged by different rules and one process gets one set
  // of flags. Outdoors a warm cast means the night has no cool source left; indoors a room lit by
  // oil lamps genuinely has one colour of light, and the exterior band condemns good shots. Both
  // still scan for magenta, near-black, blown and flat.
  ['captured-frame render defects (exterior)', 'node', ['scripts/scan-frame-defects.mjs',
    `${unityRoot}/Library/GmSceneIntelligence/standalone-proof/${scene}`,
    `${unityRoot}/Library/GmSceneIntelligence/player-probes/${scene}/walk`,
    `${unityRoot}/Library/GmSceneIntelligence/player-probes/${scene}/walk-review`,
    `${unityRoot}/Library/GmSceneIntelligence/player-probes/${scene}/walls`,
    `${unityRoot}/Screens/${sceneName}`, '--night'], CONTINUES],
  ['captured-frame render defects (interior)', 'node', ['scripts/scan-frame-defects.mjs',
    `${unityRoot}/Library/GmSceneIntelligence/player-probes/${scene}/house`, '--interior'], CONTINUES],
];

function run(command, args) {
  return new Promise((resolve) => {
    const started = Date.now();
    // Pinned to the repo root: every gate's argv is a relative path (scripts/unity-cli.mjs), so
    // invoking this pipeline from anywhere else killed 11 of 12 gates on module-not-found.
    const child = spawn(command, args,
      { stdio: ['ignore', 'pipe', 'pipe'], detached: true, cwd: REPO_ROOT });
    let tail = [];
    const keep = (chunk) => {
      process.stdout.write(chunk);
      tail = tail.concat(chunk.toString().split('\n')).slice(-40);
    };
    child.stdout.on('data', keep);
    child.stderr.on('data', keep);
    child.on('error', (error) => resolve({ code: 1, seconds: 0, tail: [error.message] }));
    child.on('close', (code) => {
      try { process.kill(-child.pid, 'SIGTERM'); } catch (error) { if (error.code !== 'ESRCH') tail.push(error.message); }
      resolve({ code: code ?? 1, seconds: Math.round((Date.now() - started) / 1000), tail });
    });
  });
}

const results = [];
let blockedBy = null;
for (const [name, command, args, blocking] of gates) {
  if (blockedBy) { results.push({ name, code: null, seconds: 0, tail: [] }); continue; }
  console.log(`\n══ ${name} ══`);
  const result = await run(command, args);
  results.push({ name, ...result });
  if (result.code !== 0 && blocking) {
    blockedBy = name;
    console.error(`\n✗ ${name} failed and every later gate reads what it produces — stopping here.`);
  }
}

console.log('\nOpening verification summary');
for (const result of results) {
  // "did not run" is a THIRD state and must never be printed as a pass or read as one. The old
  // summary simply omitted the gates it never reached, so a run that covered seven of twelve looked
  // the same as one that covered twelve minus one failure.
  if (result.code === null) { console.log(`· ${result.name} — DID NOT RUN (blocked by ${blockedBy})`); continue; }
  console.log(`${result.code === 0 ? '✓' : '✗'} ${result.name} (${result.seconds}s)`);
}

const ran = results.filter((result) => result.code !== null);
const failed = ran.filter((result) => result.code !== 0);
const skipped = results.length - ran.length;
console.log(`\n${ran.length - failed.length}/${gates.length} gate(s) passed · ` +
  `${failed.length} failed · ${skipped} did not run`);
if (failed.length || skipped) {
  console.error('\nOPENING VERIFICATION FAILED — no release claim may be made from this run.');
  for (const result of failed) console.error(`   ✗ ${result.name}`);
  process.exit(1);
}
console.log('\nOPENING VERIFIED: canonical scene, story flows, visual evidence, build, route, walls and 1080p/60 gate all passed.');
