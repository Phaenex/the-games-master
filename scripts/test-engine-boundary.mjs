#!/usr/bin/env node
import assert from 'node:assert/strict';
import { cpSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { auditEngineBoundary, REPO_ROOT } from './engine-boundary-audit.mjs';

function fixture() {
  const root = mkdtempSync(path.join(tmpdir(), 'gm-engine-boundary-'));
  cpSync(path.join(REPO_ROOT, 'unity', 'scene-system'), path.join(root, 'unity', 'scene-system'), {
    recursive: true,
  });
  return root;
}

test('production runtime has one explicit owner per source file', () => {
  const result = auditEngineBoundary();
  assert.deepEqual(result.errors, []);
  assert.equal(result.counts.total, 67);
  assert.equal(result.counts.portableNow, 24);
});

test('a new runtime file cannot bypass the extraction decision', () => {
  const root = fixture();
  try {
    writeFileSync(path.join(root, 'unity', 'scene-system', 'Runtime', 'GmNewSystem.cs'),
      'public sealed class GmNewSystem {}\n');
    assert.match(auditEngineBoundary({ repoRoot: root }).errors.join('\n'),
      /unclassified runtime file GmNewSystem\.cs/);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test('portable candidates cannot quietly depend on game-owned code', () => {
  const root = fixture();
  try {
    const file = path.join(root, 'unity', 'scene-system', 'Runtime', 'GmLightingEngine.cs');
    writeFileSync(file, `${readFileSync(file, 'utf8')}\n// GmRunStore must never leak here.\n`);
    const errors = auditEngineBoundary({ repoRoot: root }).errors.join('\n');
    assert.match(errors, /GmLightingEngine\.cs depends on non-portable runtime type GmRunStore/);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test('the portable-term guard cannot be disabled by empty policy data', () => {
  const root = fixture();
  try {
    const file = path.join(root, 'unity', 'scene-system', 'engine-boundary.json');
    const manifest = JSON.parse(readFileSync(file, 'utf8'));
    manifest.forbiddenPortableTerms = [];
    writeFileSync(file, `${JSON.stringify(manifest, null, 2)}\n`);
    assert.match(auditEngineBoundary({ repoRoot: root }).errors.join('\n'),
      /forbiddenPortableTerms must be a non-empty array/);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});
