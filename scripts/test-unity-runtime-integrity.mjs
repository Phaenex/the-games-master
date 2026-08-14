#!/usr/bin/env node
import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { findRuntimeIntegrityDefects, RENDER_INTEGRITY_PATTERNS } from './unity-runtime-integrity.mjs';

const root = path.dirname(path.dirname(fileURLToPath(import.meta.url)));

// Every node pattern is a plain literal today, so the C# fragment it must have a twin for can be
// derived from it. One that stops being literal has to be mapped by hand — say so loudly rather than
// comparing a mangled string.
function nodeFragments() {
  return RENDER_INTEGRITY_PATTERNS.map((pattern) => {
    const bare = pattern.source.replace(/\\[^\w\s]/g, '');
    assert.ok(!/[\\^$*+?.()|[\]{}]/.test(bare),
      `/${pattern.source}/ is not a plain literal; map its player fragment by hand`);
    return pattern.source.replace(/\\([^\w\s])/g, '$1').toLowerCase();
  }).sort();
}

function playerFragments() {
  const csharp = readFileSync(path.join(root,
    'unity/scene-system/Runtime/GmRuntimeIntegrityPolicy.cs'), 'utf8');
  const block = csharp.match(/RenderFailureFragments\s*=\s*\{([\s\S]*?)\};/);
  assert.ok(block, 'GmRuntimeIntegrityPolicy.RenderFailureFragments is no longer a literal array');
  return [...block[1].matchAll(/"([^"]*)"/g)].map((match) => match[1].toLowerCase()).sort();
}

test('terrain child-renderer rejection is a fatal render-integrity defect', () => {
  const log = "The tree WendHill_EstateFoliage_Reed01 couldn't be instanced because the prefab contains no valid mesh renderer.";
  assert.deepEqual(findRuntimeIntegrityDefects(log), [log]);
});

test('unsupported and fallback shaders are fatal render-integrity defects', () => {
  const log = [
    "Shader is not supported on this GPU (none of subshaders/fallbacks are suitable)",
    "Material will use fallback shader 'Hidden/InternalErrorShader'",
  ].join('\n');
  assert.equal(findRuntimeIntegrityDefects(log).length, 2);
});

test('known shutdown and thread-finalize chatter is not mislabeled as missing content', () => {
  const log = 'Thread is not attached to scripting runtime during finalization.\nUnloading 6 unused Assets';
  assert.deepEqual(findRuntimeIntegrityDefects(log), []);
});

test('negative-scale BoxCollider warnings are fatal collision-integrity defects', () => {
  const log = 'BoxCollider does not support negative scale or size. The effective box size has been forced positive.';
  assert.deepEqual(findRuntimeIntegrityDefects(log), [log]);
});

test('a non-string log is refused instead of scanning nothing', () => {
  // Without the guard a Buffer, or a spawn result that came back null, scans clean and reports a
  // build with missing content as intact.
  assert.throws(() => findRuntimeIntegrityDefects(null), TypeError);
  assert.throws(() => findRuntimeIntegrityDefects(Buffer.from("couldn't be instanced")), TypeError);
});

test('node and player policies retain the same required failure fragments', () => {
  // Derived from the node policy, not restated from it: a hand-kept second copy of the list only
  // proves parity on the day it was typed. A seventh node pattern with no C# twin used to leave
  // this test passing, which is the drift it exists to catch.
  assert.deepEqual(playerFragments(), nodeFragments());
});
