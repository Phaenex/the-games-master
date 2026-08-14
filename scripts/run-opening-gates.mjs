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
const gates = [
  ['portable source and archive checks', 'npm', ['run', 'verify:portable']],
  ['Unity EditMode', 'npm', ['run', 'test:unity']],
  ['canonical Unity PlayMode', 'node', ['scripts/unity-cli.mjs', 'playtest']],
  ['canonical scene rebuild', 'node', ['scripts/unity-cli.mjs', 'rebuild', scene]],
  ['saved-scene contract', 'node', ['scripts/unity-cli.mjs', 'audit', scene]],
  ['player-camera visual tour', 'node', ['scripts/unity-cli.mjs', 'tour', scene]],
  ['canonical macOS build', 'node', ['scripts/unity-cli.mjs', 'build-mac', scene]],
  ['standalone story/input/audio proof', 'node', ['scripts/unity-cli.mjs', 'standalone-proof', scene]],
  ['house entry and first-game proof', 'node', ['scripts/unity-cli.mjs', 'house-proof', scene]],
  ['full-route 1080p performance proof', 'node', ['scripts/unity-cli.mjs', 'walk-proof', scene]],
  ['standalone boundary-wall proof', 'node', ['scripts/unity-cli.mjs', 'wall-proof', scene]],
  // Runs LAST because it reads what every gate above produced. Percentile luminance proves
  // brightness and nothing else: a magenta object survived every green gate run on 2026-08-03 by
  // hiding in screenshots behind an opaque UI panel, while black-void gate piers scored "ok".
  ['captured-frame render defects', 'node', ['scripts/scan-frame-defects.mjs',
    `${unityRoot}/Library/GmSceneIntelligence/standalone-proof/${scene}`,
    `${unityRoot}/Library/GmSceneIntelligence/player-probes/${scene}`,
    `${unityRoot}/Screens/${sceneName}`]],
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
for (const [name, command, args] of gates) {
  console.log(`\n══ ${name} ══`);
  const result = await run(command, args);
  results.push({ name, ...result });
  if (result.code !== 0) break; // every later gate depends on this artifact being trustworthy
}

console.log('\nOpening verification summary');
for (const result of results)
  console.log(`${result.code === 0 ? '✓' : '✗'} ${result.name} (${result.seconds}s)`);
if (results.length !== gates.length || results.some((result) => result.code !== 0)) {
  console.error('\nOPENING VERIFICATION FAILED — no release claim may be made from this run.');
  process.exit(1);
}
console.log('\nOPENING VERIFIED: canonical scene, story flows, visual evidence, build, route, walls and 1080p/60 gate all passed.');
