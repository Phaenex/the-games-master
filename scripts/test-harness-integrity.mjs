#!/usr/bin/env node
// The meta-gate: proves the verification harness can still fail.
//
// Every check in this project exists to catch something. A check that has quietly stopped being able
// to fail is worse than no check at all, because it reports success and nobody looks again. This
// repo has shipped three of those already: scan-frame-defects counted an existing-but-empty
// directory as clean, verify-gate-stars prints the word PASS unconditionally, and verify-unity-full
// asserts a value it wrote itself one line earlier.
//
// "Break it on purpose and watch it go red" was written in TESTING.md as advice. Advice is something
// you remember when you are not tired. This runs it.
//
// THE RULE EVERY CASE FOLLOWS: each guard needs BOTH a red case and a green case. A red case alone
// is satisfied by a script that always fails, which is its own kind of broken -- it trains people to
// ignore the gate. Both directions, or the case proves nothing.
import { spawnSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync, copyFileSync, existsSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const REPO_ROOT = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
const sandbox = mkdtempSync(path.join(tmpdir(), 'gm-harness-integrity-'));

let passed = 0;
const failures = [];

function run(script, args, env = {}) {
  const result = spawnSync('node', [path.join(REPO_ROOT, 'scripts', script), ...args], {
    cwd: REPO_ROOT, encoding: 'utf8', env: { ...process.env, ...env },
  });
  return { code: result.status ?? 1, out: `${result.stdout ?? ''}${result.stderr ?? ''}` };
}

/** The guard must REJECT this input. A zero exit here means the check has gone blind. */
function mustFail(label, script, args, env) {
  const { code, out } = run(script, args, env);
  if (code !== 0) { passed++; console.log(`  ✓ ${label} — correctly rejected`); return; }
  failures.push(`${label}: ${script} exited 0 on input it must reject — the check is blind here\n      ${out.trim().split('\n').slice(-3).join('\n      ')}`);
  console.log(`  ✗ ${label} — ACCEPTED bad input`);
}

/** The guard must ACCEPT this input. Without this direction, "always fails" would score as healthy. */
function mustPass(label, script, args, env) {
  const { code, out } = run(script, args, env);
  if (code === 0) { passed++; console.log(`  ✓ ${label} — correctly accepted`); return; }
  failures.push(`${label}: ${script} exited ${code} on input it must accept — the check cries wolf\n      ${out.trim().split('\n').slice(-3).join('\n      ')}`);
  console.log(`  ✗ ${label} — REJECTED good input`);
}

// ---------------------------------------------------------------------------------------------
// scan-frame-defects.mjs — gate 12. Reads what every other gate produced, so its blindness is
// inherited by the whole pipeline.
// ---------------------------------------------------------------------------------------------
console.log('\nscan-frame-defects.mjs');

const realFrame = path.join(REPO_ROOT, 'unity-project/Screens/WendHill_Prologue/tour-01-arrival.png');
const haveRealFrame = existsSync(realFrame);

const emptyDir = path.join(sandbox, 'exists-but-empty');
mkdirSync(emptyDir, { recursive: true });
const corruptDir = path.join(sandbox, 'corrupt');
mkdirSync(corruptDir, { recursive: true });
writeFileSync(path.join(corruptDir, 'truncated.png'), Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x00, 0x01]));
const goodDir = path.join(sandbox, 'good');
mkdirSync(goodDir, { recursive: true });
if (haveRealFrame) copyFileSync(realFrame, path.join(goodDir, 'frame-01.png'));

mustFail('missing target directory', 'scan-frame-defects.mjs', [path.join(sandbox, 'nope')]);
mustFail('directory that exists but captured nothing', 'scan-frame-defects.mjs', [emptyDir]);
mustFail('undecodable PNG counted as scanned', 'scan-frame-defects.mjs', [corruptDir]);
if (haveRealFrame) {
  mustPass('a real captured frame', 'scan-frame-defects.mjs', [goodDir]);
  // The regression that shipped: one good target masking a silent one.
  mustFail('good target paired with an empty one', 'scan-frame-defects.mjs', [goodDir, emptyDir]);
} else {
  console.log('  … skipped 2 positive cases — no captured frame on disk; run the tour first');
}

// ---------------------------------------------------------------------------------------------
// test-attribution.mjs — gate 0. A licence check that cannot fail is a legal exposure, not a gate.
// ---------------------------------------------------------------------------------------------
console.log('\ntest-attribution.mjs');

function attributionSandbox(name, credits, sfx = 'All CC0, no attribution owed.\n') {
  const root = path.join(sandbox, name);
  mkdirSync(path.join(root, 'assets/models/sourced'), { recursive: true });
  mkdirSync(path.join(root, 'assets/sfx'), { recursive: true });
  if (credits !== null) writeFileSync(path.join(root, 'assets/models/sourced/CREDITS.txt'), credits);
  writeFileSync(path.join(root, 'assets/sfx/license.txt'), sfx);
  return root;
}

// Deliberately NOT inside a PENDING block: the gate is supposed to forgive those (an asset whose
// fetch failed owes no credit), and a fixture that leaned on a PENDING block would have proven the
// opposite of what it claims.
const unresolved = attributionSandbox('attr-unresolved',
  'gavel.glb\n  Sourced for the Court scene. License unknown.\n');
const unnamed = attributionSandbox('attr-unnamed',
  'mirror.glb\n  Sourced from Poly Pizza, CC-BY.\n');
const clean = attributionSandbox('attr-clean',
  'lantern.glb\n  "Lantern" by Kay Lousberg, via Poly Pizza. License: CC0 (Public Domain).\n');
const missing = attributionSandbox('attr-missing', null);

mustFail('an unresolved "credit here when landed" promise', 'test-attribution.mjs', [], { GM_REPO_ROOT: unresolved });
mustFail('a CC-BY entry naming no author', 'test-attribution.mjs', [], { GM_REPO_ROOT: unnamed });
mustFail('a missing CREDITS record', 'test-attribution.mjs', [], { GM_REPO_ROOT: missing });
mustPass('a complete, credited record', 'test-attribution.mjs', [], { GM_REPO_ROOT: clean });

// ---------------------------------------------------------------------------------------------
// sync-unity-scenes.mjs --check — the drift gate. If it cannot see drift, the repo and the Unity
// project can disagree silently, which is the failure mode the whole ownership model exists to stop.
// ---------------------------------------------------------------------------------------------
console.log('\nsync-unity-scenes.mjs --check');

const driftProject = path.join(sandbox, 'drift-unity-project');
mkdirSync(driftProject, { recursive: true });
mustFail('a Unity project missing every synced file', 'sync-unity-scenes.mjs', ['--check'],
  { GM_UNITY_PROJECT: driftProject });

// ---------------------------------------------------------------------------------------------
console.log('\n' + '-'.repeat(78));
if (failures.length) {
  console.error(`✗ harness integrity: ${failures.length} guard(s) cannot do their job\n`);
  for (const f of failures) console.error(`    ${f}\n`);
  console.error('A check that cannot fail is not a check. Fix the guard, not this test.');
  rmSync(sandbox, { recursive: true, force: true });
  process.exit(1);
}
console.log(`✓ harness integrity: ${passed} guard case(s) proven — each rejects bad input and accepts good`);
rmSync(sandbox, { recursive: true, force: true });
