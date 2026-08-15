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
import { mkdtempSync, writeFileSync, mkdirSync, rmSync } from 'node:fs';
import { deflateSync } from 'node:zlib';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const REPO_ROOT = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
const sandbox = mkdtempSync(path.join(tmpdir(), 'gm-harness-integrity-'));

// Cleanup on the way out, whatever the way out is. Both exit paths below used to call rmSync
// themselves, which left the fixtures on disk for any THIRD path -- a spawn that throws, a bad
// fixture write, an uncaught anything. Stale fixtures are worse here than elsewhere: the next run
// finds a populated directory where it expected to build one, and a gate whose whole job is
// detecting an existing-but-wrong directory is the last thing that should be fed one.
process.on('exit', () => rmSync(sandbox, { recursive: true, force: true }));

let passed = 0;
let attempted = 0;
const failures = [];

/**
 * A real, decodable PNG built here rather than borrowed from disk.
 *
 * The first version of this gate took its "accept good input" frame from
 * unity-project/Screens/, which is gitignored -- so on the ONLY CI job that runs today
 * (ubuntu-latest, no Unity, fresh checkout) that file never exists, two cases silently skipped, and
 * the run still printed a clean pass. The skipped pair included the case this gate exists for: a
 * good target masking an empty one, which is the exact defect that shipped. A meta-gate that
 * quietly covers less on the machine that matters most is the thing it was written to prevent.
 *
 * A horizontal grey ramp, so it clears every defect rule in scan-frame-defects deliberately rather
 * than by luck: not near-black (p90 well above 3), not blown (median far below 235), not flat
 * (p90-p5 spread far above 4), and no magenta (r == g == b can never satisfy g < r * 0.55).
 */
function writeGradientPng(file, size = 32) {
  const crcTable = Array.from({ length: 256 }, (_, n) => {
    let c = n;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    return c >>> 0;
  });
  const crc32 = (buf) => {
    let c = 0xffffffff;
    for (const byte of buf) c = crcTable[(c ^ byte) & 0xff] ^ (c >>> 8);
    return (c ^ 0xffffffff) >>> 0;
  };
  const chunk = (type, data) => {
    const len = Buffer.alloc(4); len.writeUInt32BE(data.length);
    const body = Buffer.concat([Buffer.from(type, 'ascii'), data]);
    const crc = Buffer.alloc(4); crc.writeUInt32BE(crc32(body));
    return Buffer.concat([len, body, crc]);
  };

  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(size, 0); ihdr.writeUInt32BE(size, 4);
  ihdr[8] = 8;    // bit depth
  ihdr[9] = 2;    // colour type 2 = RGB, which the scanner accepts
  ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;   // deflate / adaptive filtering / no interlace

  const raw = Buffer.alloc(size * (size * 3 + 1));
  let at = 0;
  for (let y = 0; y < size; y++) {
    raw[at++] = 0;   // filter type 0 (None) for this scanline
    for (let x = 0; x < size; x++) {
      const v = Math.round(12 + (x / (size - 1)) * 200);   // 12..212
      raw[at++] = v; raw[at++] = v; raw[at++] = v;
    }
  }

  writeFileSync(file, Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', ihdr),
    chunk('IDAT', deflateSync(raw)),
    chunk('IEND', Buffer.alloc(0)),
  ]));
}

function run(script, args, env = {}) {
  const result = spawnSync('node', [path.join(REPO_ROOT, 'scripts', script), ...args], {
    cwd: REPO_ROOT, encoding: 'utf8', env: { ...process.env, ...env },
  });
  return { code: result.status ?? 1, out: `${result.stdout ?? ''}${result.stderr ?? ''}` };
}

/** The guard must REJECT this input. A zero exit here means the check has gone blind. */
function mustFail(label, script, args, env) {
  attempted++;
  const { code, out } = run(script, args, env);
  if (code !== 0) { passed++; console.log(`  ✓ ${label} — correctly rejected`); return; }
  failures.push(`${label}: ${script} exited 0 on input it must reject — the check is blind here\n      ${out.trim().split('\n').slice(-3).join('\n      ')}`);
  console.log(`  ✗ ${label} — ACCEPTED bad input`);
}

/** The guard must ACCEPT this input. Without this direction, "always fails" would score as healthy. */
function mustPass(label, script, args, env) {
  attempted++;
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

const emptyDir = path.join(sandbox, 'exists-but-empty');
mkdirSync(emptyDir, { recursive: true });
const corruptDir = path.join(sandbox, 'corrupt');
mkdirSync(corruptDir, { recursive: true });
writeFileSync(path.join(corruptDir, 'truncated.png'), Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x00, 0x01]));
const goodDir = path.join(sandbox, 'good');
mkdirSync(goodDir, { recursive: true });
writeGradientPng(path.join(goodDir, 'frame-01.png'));

mustFail('missing target directory', 'scan-frame-defects.mjs', [path.join(sandbox, 'nope')]);
mustFail('directory that exists but captured nothing', 'scan-frame-defects.mjs', [emptyDir]);
mustFail('undecodable PNG counted as scanned', 'scan-frame-defects.mjs', [corruptDir]);
mustPass('a clean, decodable frame', 'scan-frame-defects.mjs', [goodDir]);
// The regression that shipped: one good target masking a silent one. This is the case the whole
// gate exists for, so it must run everywhere, not only where a prior tour left frames behind.
mustFail('good target paired with an empty one', 'scan-frame-defects.mjs', [goodDir, emptyDir]);

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
// Deliberately CC-BY, wrapped across lines, with the author's name on a DIFFERENT line from the
// licence token. A CC0 fixture here proved nothing: /CC[-\s]?BY/ never matches "CC0", so the
// entry-scoping and author-detection code -- the exact path that once wrongly rejected a complete
// record in CREDITS.txt -- went unexercised by the gate meant to keep it honest.
const clean = attributionSandbox('attr-clean',
  'lantern.glb  (drive lampposts)\n  "Lantern" by Kay Lousberg, via Poly Pizza.\n'
  + '  License: CC-BY 3.0.\n');
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
  process.exit(1);
}
// Report attempted as well as passed: a run that silently covered fewer cases must not read the
// same as a full one. That is precisely how the first version of this file hid its own gap.
console.log(`✓ harness integrity: ${passed}/${attempted} guard case(s) proven — each rejects bad input and accepts good`);
