#!/usr/bin/env node
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import test from 'node:test';
import {
  aggregateKnowledge,
  canonicalJson,
  loadKnowledge,
  migrateKnowledge,
  normalizeReviewSession,
  promoteCandidate,
  rebuildDerived,
  resolveDefect,
  validateReviewSession,
  writeJsonAtomic,
  writeReviewSessionAtomic,
} from './scene-learning-store.mjs';

function verdict(id, value, sceneId = 'wend-hill', reviewer = 'nick', overrides = {}) {
  return {
    schemaVersion: 1,
    sessionId: `${reviewer}-${sceneId}-${id}`,
    sceneId,
    createdAt: '2026-07-19T12:00:00.000Z',
    reviewer: { kind: reviewer, id: reviewer },
    evidence: { buildFingerprint: 'cb198edc4905ac2e' },
    verdicts: [{
      id,
      category: 'visual',
      verdict: value,
      context: { zoneId: 'cemetery', assetFamily: 'grave-marker', assetRole: 'Detail' },
      tags: value === 'keep' ? [] : ['repetition'],
      ...overrides,
    }],
    objectiveFindings: [],
  };
}

function seedRoot() {
  const root = mkdtempSync(path.join(tmpdir(), 'gm-knowledge-'));
  for (const relative of ['review-sessions', 'derived']) mkdirSync(path.join(root, relative), { recursive: true });
  writeJsonAtomic(path.join(root, 'knowledge-manifest.json'), { schemaVersion: 1, name: 'test' });
  writeJsonAtomic(path.join(root, 'historical-observations.json'), { schemaVersion: 1, items: [] });
  writeJsonAtomic(path.join(root, 'approved-rules.json'), { schemaVersion: 1, items: [] });
  writeJsonAtomic(path.join(root, 'defect-resolutions.json'), { schemaVersion: 1, items: [] });
  writeJsonAtomic(path.join(root, 'baselines.json'), { schemaVersion: 1, items: [] });
  writeJsonAtomic(path.join(root, 'adaptive-overrides.json'), { schemaVersion: 1, items: [] });
  return root;
}

test('review schema rejects invalid verdicts, untagged changes, and automation taste', () => {
  assert.throws(() => validateReviewSession(verdict('bad', 'maybe')), /must be keep, change, or reject/);
  assert.throws(() => validateReviewSession(verdict('bare', 'change', 'wend-hill', 'nick', { tags: [] })), /non-empty array/);
  assert.throws(() => validateReviewSession(verdict('robot', 'keep', 'wend-hill', 'automation')), /may not author taste verdict/);
});

test('v2 automation sessions carry structured evidence without impersonating taste', () => {
  const session = {
    schemaVersion: 2,
    sessionId: 'automation-wend-hill-perceptual',
    sceneId: 'wend-hill',
    createdAt: '2026-07-19T12:00:00.000Z',
    reviewer: { kind: 'automation', id: 'gm-perceptual-audit' },
    evidence: { buildFingerprint: 'candidate-fingerprint' },
    verdicts: [],
    objectiveFindings: [{
      id: 'cemetery-repetition', metric: 'maximum-signature-share', status: 'fail',
      actual: '44%', expected: '<= 25%', category: 'repetition', severity: 'error',
      subjectId: 'cemetery-graves', evidencePath: 'Screens/WendHill/tour-05-cemetery.png',
      context: { zoneId: 'cemetery', intentId: 'grave-rhythm', candidateProfileId: 'environment-b' },
    }],
  };
  assert.equal(validateReviewSession(session).schemaVersion, 2);
  assert.throws(() => validateReviewSession({ ...session, objectiveFindings: [] }),
    /must contain objectiveFindings/);
  assert.throws(() => validateReviewSession({ ...session, objectiveFindings: [{
    ...session.objectiveFindings[0], severity: 'cruel-opinion',
  }] }), /severity must be info, warning, or error/);
});

test('Nick outweighs agents and only Nick reject creates contextual exclusion', () => {
  const result = aggregateKnowledge({ sessions: [
    verdict('n-keep', 'keep'),
    verdict('a-change', 'change', 'wend-hill', 'agent'),
    verdict('a-reject', 'reject', 'wend-hill', 'agent'),
  ] });
  assert.equal(result.assetPreferences[0].affinity, 0.5);
  assert.equal(result.assetPreferences[0].contextualExclusion, false);
  const rejected = aggregateKnowledge({ sessions: [verdict('n-reject', 'reject')] });
  assert.equal(rejected.assetPreferences[0].affinity, -1);
  assert.equal(rejected.assetPreferences[0].contextualExclusion, true);
  assert.equal(rejected.defects[0].severity, 'high');
});

test('duplicate events are idempotent and immutable sessions cannot be replaced', () => {
  const root = seedRoot();
  try {
    const session = verdict('keep-1', 'keep');
    const first = writeReviewSessionAtomic(root, session);
    const second = writeReviewSessionAtomic(root, session);
    assert.equal(first.duplicate, false);
    assert.equal(second.duplicate, true);
    assert.equal(aggregateKnowledge({ sessions: [session, session] }).assetPreferences[0].affinity, 1);
    assert.throws(() => writeReviewSessionAtomic(root, { ...session, sceneId: 'other-scene' }), /immutable session/);
  } finally { rmSync(root, { recursive: true, force: true }); }
});

test('candidate rules need Nick rejection or recurrence across two scenes and never auto-approve', () => {
  const oneAgent = aggregateKnowledge({ sessions: [verdict('one', 'change', 'wend-hill', 'agent')] });
  assert.equal(oneAgent.ruleCandidates.length, 0);
  const recurrent = aggregateKnowledge({ sessions: [
    verdict('one', 'change', 'wend-hill', 'agent'),
    verdict('two', 'change', 'entry-hall', 'agent'),
  ] });
  assert.equal(recurrent.ruleCandidates.length, 1);
  assert.equal(recurrent.ruleCandidates[0].promotionRequiresNick, true);
  assert.equal(recurrent.ruleCandidates[0].scenes.length, 2);
  const nickReject = aggregateKnowledge({ sessions: [verdict('reject', 'reject')] });
  assert.equal(nickReject.ruleCandidates.length, 1);
});

test('derived files rebuild identically and corrupt JSON fails closed', () => {
  const root = seedRoot();
  try {
    writeReviewSessionAtomic(root, verdict('change-1', 'change'));
    rebuildDerived(root);
    const file = path.join(root, 'derived', 'defects.json');
    const first = readFileSync(file, 'utf8');
    rebuildDerived(root);
    assert.equal(readFileSync(file, 'utf8'), first);
    writeFileSync(path.join(root, 'historical-observations.json'), '{broken');
    assert.throws(() => loadKnowledge(root), /cannot read historical observations/);
  } finally { rmSync(root, { recursive: true, force: true }); }
});

test('atomic writes leave canonical complete JSON and no temporary file', () => {
  const root = seedRoot();
  try {
    const target = path.join(root, 'derived', 'sample.json');
    writeJsonAtomic(target, { z: 2, a: 1 });
    assert.equal(readFileSync(target, 'utf8'), canonicalJson({ z: 2, a: 1 }));
    assert.equal(existsSync(`${target}.tmp`), false);
  } finally { rmSync(root, { recursive: true, force: true }); }
});

test('legacy item containers migrate to v1 while unknown versions fail', () => {
  assert.deepEqual(migrateKnowledge({ items: [1] }, 'test'), { schemaVersion: 1, items: [1] });
  assert.throws(() => migrateKnowledge({ schemaVersion: 99 }, 'test'), /no test migration exists/);
});

test('Unity optional empty strings are removed before schema validation and persistence', () => {
  const root = seedRoot();
  try {
    const unitySession = verdict('unity-empty', 'keep');
    unitySession.evidence.captureSet = '';
    unitySession.evidence.notes = '';
    unitySession.verdicts[0].context.shotName = '';
    unitySession.verdicts[0].context.assetGuid = '';
    unitySession.verdicts[0].note = '';
    const normalized = normalizeReviewSession(unitySession);
    assert.equal('shotName' in normalized.verdicts[0].context, false);
    validateReviewSession(unitySession);
    const result = writeReviewSessionAtomic(root, unitySession);
    const stored = readFileSync(result.file, 'utf8');
    assert.equal(stored.includes('"shotName": ""'), false);
    assert.equal(stored.includes('"captureSet": ""'), false);
  } finally { rmSync(root, { recursive: true, force: true }); }
});

test('draft rules can only be promoted by an explicit Nick confirmation', () => {
  const root = seedRoot();
  try {
    writeReviewSessionAtomic(root, verdict('one', 'change', 'wend-hill', 'agent'));
    writeReviewSessionAtomic(root, verdict('two', 'change', 'entry-hall', 'agent'));
    const candidate = rebuildDerived(root).ruleCandidates[0];
    assert.throws(() => promoteCandidate(root, candidate.candidateId), /explicit Nick confirmation/);
    const first = promoteCandidate(root, candidate.candidateId, {
      nickConfirmed: true,
      now: '2026-07-19T13:00:00.000Z',
    });
    const second = promoteCandidate(root, candidate.candidateId, { nickConfirmed: true });
    assert.equal(first.duplicate, false);
    assert.equal(second.duplicate, true);
    assert.equal(loadKnowledge(root).rules[0].promotedBy, 'nick');
    assert.equal(rebuildDerived(root).ruleCandidates.length, 0);
  } finally { rmSync(root, { recursive: true, force: true }); }
});

test('defect resolution is confirmation-gated, append-only, and reflected in derived state', () => {
  const root = seedRoot();
  try {
    writeReviewSessionAtomic(root, verdict('change-1', 'change'));
    const defect = rebuildDerived(root).defects[0];
    assert.throws(() => resolveDefect(root, defect.defectId), /explicit Nick confirmation/);
    const first = resolveDefect(root, defect.defectId, {
      nickConfirmed: true,
      now: '2026-07-19T14:00:00.000Z',
    });
    const second = resolveDefect(root, defect.defectId, { nickConfirmed: true });
    assert.equal(first.duplicate, false);
    assert.equal(second.duplicate, true);
    assert.equal(rebuildDerived(root).defects[0].status, 'resolved');
    assert.equal(loadKnowledge(root).resolutions[0].resolvedBy, 'nick');
  } finally { rmSync(root, { recursive: true, force: true }); }
});

test('corrupt gated ledgers and non-Nick approved rules fail closed', () => {
  const root = seedRoot();
  try {
    writeJsonAtomic(path.join(root, 'approved-rules.json'), { schemaVersion: 1, items: [{
      ruleId: 'rule-test', candidateId: 'candidate-test', signature: 'visual|Detail|stone|repeat',
      scenes: ['wend-hill'], evidence: ['session:event'],
      promotedAt: '2026-07-19T15:00:00.000Z', promotedBy: 'agent',
    }] });
    assert.throws(() => loadKnowledge(root), /explicitly promoted by Nick/);
    writeJsonAtomic(path.join(root, 'approved-rules.json'), { schemaVersion: 1, items: [] });
    writeFileSync(path.join(root, 'baselines.json'), '{broken');
    assert.throws(() => loadKnowledge(root), /cannot read visual baselines/);
  } finally { rmSync(root, { recursive: true, force: true }); }
});
