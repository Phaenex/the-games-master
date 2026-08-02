#!/usr/bin/env node
import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { findRuntimeIntegrityDefects } from './unity-runtime-integrity.mjs';

const root = path.dirname(path.dirname(fileURLToPath(import.meta.url)));

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

test('node and player policies retain the same required failure fragments', () => {
  const csharp = readFileSync(path.join(root,
    'unity/scene-system/Runtime/GmRuntimeIntegrityPolicy.cs'), 'utf8').toLowerCase();
  for (const fragment of [
    "couldn't be instanced", 'contains no valid mesh renderer',
    'shader is not supported on this gpu', 'has no shader assigned',
    "fallback shader 'hidden/internalerrorshader'",
    'boxcollider does not support negative scale or size',
  ]) assert.ok(csharp.includes(fragment), `C# player policy lost '${fragment}'`);
});
