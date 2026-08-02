#!/usr/bin/env node
// Transparent, deterministic review memory for Unity scene composition. This is deliberately a
// structured ledger, not model training: Nick's decisions remain inspectable and always outrank
// agent observations and objective automation findings.
import crypto from 'node:crypto';
import {
  existsSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  renameSync,
  unlinkSync,
  writeFileSync,
} from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

export const SCHEMA_VERSION = 1;
export const REVIEW_SESSION_SCHEMA_VERSION = 2;
export const REPO_ROOT = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
export const DEFAULT_KNOWLEDGE_ROOT = path.join(REPO_ROOT, 'unity', 'scene-system', 'knowledge');

const REVIEWER_KINDS = new Set(['nick', 'agent', 'automation']);
const VERDICTS = new Set(['keep', 'change', 'reject']);
const CATEGORIES = new Set([
  'visual', 'asset', 'lighting', 'audio', 'navigation', 'ui', 'performance', 'pacing',
]);
const REQUIRED_FILES = {
  manifest: 'knowledge-manifest.json',
  historical: 'historical-observations.json',
  rules: 'approved-rules.json',
  resolutions: 'defect-resolutions.json',
  baselines: 'baselines.json',
  overrides: 'adaptive-overrides.json',
  assetPreferences: 'derived/asset-preferences.json',
  defects: 'derived/defects.json',
  candidates: 'derived/rule-candidates.json',
};

function fail(message) {
  throw new Error(`scene knowledge: ${message}`);
}

function assertObject(value, label) {
  if (!value || typeof value !== 'object' || Array.isArray(value)) fail(`${label} must be an object`);
}

function assertKeys(value, allowed, label) {
  for (const key of Object.keys(value)) if (!allowed.has(key)) fail(`${label} has unknown field '${key}'`);
}

function text(value, label, { optional = false, minimum = 1 } = {}) {
  if (optional && value === undefined) return;
  if (typeof value !== 'string' || value.trim() !== value || value.length < minimum) {
    fail(`${label} must be a trimmed string of at least ${minimum} character(s)`);
  }
}

function stringArray(value, label, { required = false } = {}) {
  if (!Array.isArray(value) || (required && value.length === 0)) fail(`${label} must be a${required ? ' non-empty' : 'n'} array`);
  const seen = new Set();
  for (const item of value) {
    text(item, `${label} item`);
    if (seen.has(item)) fail(`${label} contains duplicate '${item}'`);
    seen.add(item);
  }
}

function isoDate(value, label) {
  text(value, label);
  if (Number.isNaN(Date.parse(value)) || new Date(value).toISOString() !== value) fail(`${label} must be a canonical ISO timestamp`);
}

export function stableValue(value) {
  if (Array.isArray(value)) return value.map(stableValue);
  if (value && typeof value === 'object') {
    return Object.fromEntries(Object.keys(value).sort().map((key) => [key, stableValue(value[key])]));
  }
  return value;
}

export function canonicalJson(value) {
  return `${JSON.stringify(stableValue(value), null, 2)}\n`;
}

export function stableId(prefix, value) {
  return `${prefix}-${crypto.createHash('sha256').update(canonicalJson(value)).digest('hex').slice(0, 16)}`;
}

export function normalizeReviewSession(input) {
  if (!input || typeof input !== 'object' || Array.isArray(input)) return input;
  const session = JSON.parse(JSON.stringify(input));
  const removeEmpty = (value, keys) => {
    if (!value || typeof value !== 'object') return;
    for (const key of keys) if (value[key] === '') delete value[key];
  };
  removeEmpty(session.reviewer, ['displayName']);
  removeEmpty(session.evidence, ['buildFingerprint', 'captureSet', 'notes']);
  for (const verdict of session.verdicts ?? []) {
    removeEmpty(verdict, ['note']);
    removeEmpty(verdict.context, ['shotName', 'zoneId', 'clusterId', 'elementId', 'assetGuid', 'assetFamily', 'assetRole', 'candidateProfileId', 'intentId']);
  }
  for (const finding of session.objectiveFindings ?? []) {
    removeEmpty(finding, ['note', 'category', 'severity', 'subjectId', 'evidencePath']);
    removeEmpty(finding.context, ['shotName', 'zoneId', 'clusterId', 'elementId', 'assetGuid', 'assetFamily', 'assetRole', 'candidateProfileId', 'intentId']);
  }
  return session;
}

export function validateReviewSession(input) {
  const session = normalizeReviewSession(input);
  assertObject(session, 'review session');
  assertKeys(session, new Set(['schemaVersion', 'sessionId', 'sceneId', 'createdAt', 'reviewer', 'evidence', 'verdicts', 'objectiveFindings']), 'review session');
  if (![1, REVIEW_SESSION_SCHEMA_VERSION].includes(session.schemaVersion))
    fail(`unsupported review session schemaVersion '${session.schemaVersion}'`);
  text(session.sessionId, 'sessionId', { minimum: 4 });
  text(session.sceneId, 'sceneId', { minimum: 2 });
  isoDate(session.createdAt, 'createdAt');
  assertObject(session.reviewer, 'reviewer');
  assertKeys(session.reviewer, new Set(['kind', 'id', 'displayName']), 'reviewer');
  if (!REVIEWER_KINDS.has(session.reviewer.kind)) fail(`reviewer.kind must be nick, agent, or automation`);
  text(session.reviewer.id, 'reviewer.id');
  text(session.reviewer.displayName, 'reviewer.displayName', { optional: true });
  if (session.evidence !== undefined) {
    assertObject(session.evidence, 'evidence');
    assertKeys(session.evidence, new Set(['buildFingerprint', 'captureSet', 'notes']), 'evidence');
    text(session.evidence.buildFingerprint, 'evidence.buildFingerprint', { optional: true });
    text(session.evidence.captureSet, 'evidence.captureSet', { optional: true });
    text(session.evidence.notes, 'evidence.notes', { optional: true });
  }
  if (!Array.isArray(session.verdicts)) fail('verdicts must be an array');
  if (session.reviewer.kind !== 'automation' && session.verdicts.length === 0)
    fail('human and agent verdicts must be a non-empty array');
  const verdictIds = new Set();
  for (const verdict of session.verdicts) validateVerdict(verdict, session.reviewer.kind, verdictIds);
  const findings = session.objectiveFindings ?? [];
  if (!Array.isArray(findings)) fail('objectiveFindings must be an array');
  if (findings.length && session.reviewer.kind !== 'automation') fail('only automation sessions may contain objectiveFindings');
  if (session.reviewer.kind === 'automation' && findings.length === 0)
    fail('automation sessions must contain objectiveFindings');
  const findingIds = new Set();
  for (const finding of findings) validateFinding(finding, findingIds, session.schemaVersion);
  return session;
}

function validateContext(context, label) {
  assertObject(context, label);
  assertKeys(context, new Set(['shotName', 'zoneId', 'clusterId', 'elementId', 'assetGuid', 'assetFamily', 'assetRole', 'candidateProfileId', 'intentId']), label);
  for (const [key, value] of Object.entries(context)) text(value, `${label}.${key}`);
}

function validateVerdict(verdict, reviewerKind, ids) {
  assertObject(verdict, 'verdict');
  assertKeys(verdict, new Set(['id', 'category', 'verdict', 'context', 'tags', 'note']), 'verdict');
  text(verdict.id, 'verdict.id');
  if (ids.has(verdict.id)) fail(`duplicate verdict id '${verdict.id}'`);
  ids.add(verdict.id);
  if (!CATEGORIES.has(verdict.category)) fail(`verdict '${verdict.id}' has invalid category '${verdict.category}'`);
  if (!VERDICTS.has(verdict.verdict)) fail(`verdict '${verdict.id}' must be keep, change, or reject`);
  validateContext(verdict.context ?? {}, `verdict '${verdict.id}' context`);
  stringArray(verdict.tags ?? [], `verdict '${verdict.id}' tags`, { required: verdict.verdict !== 'keep' });
  text(verdict.note, `verdict '${verdict.id}' note`, { optional: true, minimum: 0 });
  if (reviewerKind === 'automation') fail(`automation may report objectiveFindings but may not author taste verdict '${verdict.id}'`);
}

function validateFinding(finding, ids, schemaVersion) {
  assertObject(finding, 'objective finding');
  assertKeys(finding, new Set(['id', 'metric', 'status', 'actual', 'expected', 'context', 'note',
    'category', 'severity', 'subjectId', 'evidencePath']), 'objective finding');
  text(finding.id, 'finding.id');
  if (ids.has(finding.id)) fail(`duplicate finding id '${finding.id}'`);
  ids.add(finding.id);
  text(finding.metric, `finding '${finding.id}' metric`);
  if (!['pass', 'warn', 'fail'].includes(finding.status)) fail(`finding '${finding.id}' status must be pass, warn, or fail`);
  if (!['string', 'number', 'boolean'].includes(typeof finding.actual)) fail(`finding '${finding.id}' actual must be scalar`);
  if (!['string', 'number', 'boolean'].includes(typeof finding.expected)) fail(`finding '${finding.id}' expected must be scalar`);
  validateContext(finding.context ?? {}, `finding '${finding.id}' context`);
  text(finding.note, `finding '${finding.id}' note`, { optional: true });
  text(finding.category, `finding '${finding.id}' category`, { optional: schemaVersion === 1 });
  if (finding.severity !== undefined && !['info', 'warning', 'error'].includes(finding.severity))
    fail(`finding '${finding.id}' severity must be info, warning, or error`);
  if (schemaVersion >= 2 && finding.severity === undefined)
    fail(`finding '${finding.id}' severity is required in schema v2`);
  text(finding.subjectId, `finding '${finding.id}' subjectId`, { optional: true });
  text(finding.evidencePath, `finding '${finding.id}' evidencePath`, { optional: true });
}

export function eventKey(session, verdict) {
  return `${session.sessionId}:${verdict.id}`;
}

function contextKey(sceneId, category, context = {}) {
  return [
    sceneId,
    category,
    context.zoneId ?? '*',
    context.assetGuid ?? context.assetFamily ?? '*',
    context.assetRole ?? '*',
  ].join('|');
}

function signatureOf(verdict) {
  const tag = [...(verdict.tags ?? [])].sort()[0] ?? verdict.verdict;
  return `${verdict.category}|${verdict.context?.assetRole ?? '*'}|${verdict.context?.assetFamily ?? '*'}|${tag}`;
}

function sessionEvents(sessions, historical) {
  const events = [];
  const seen = new Set();
  for (const input of [...sessions, ...historical]) {
    const session = validateReviewSession(input);
    for (const verdict of session.verdicts) {
      const key = eventKey(session, verdict);
      if (seen.has(key)) continue;
      seen.add(key);
      events.push({ session, verdict, key });
    }
  }
  return events.sort((a, b) => a.key.localeCompare(b.key));
}

export function aggregateKnowledge({ sessions = [], historical = [], resolutions = [], rules = [] } = {}) {
  const preferences = new Map();
  const defects = [];
  const candidates = new Map();
  for (const { session, verdict, key } of sessionEvents(sessions, historical)) {
    const reviewerWeight = session.reviewer.kind === 'nick' ? 1 : 0.25;
    const direction = verdict.verdict === 'keep' ? 1 : -1;
    const preferenceKey = contextKey(session.sceneId, verdict.category, verdict.context);
    const preference = preferences.get(preferenceKey) ?? {
      key: preferenceKey,
      sceneId: session.sceneId,
      category: verdict.category,
      context: stableValue(verdict.context ?? {}),
      affinity: 0,
      evidence: [],
      contextualExclusion: false,
    };
    preference.affinity += reviewerWeight * direction;
    preference.evidence.push(key);
    if (session.reviewer.kind === 'nick' && verdict.verdict === 'reject') preference.contextualExclusion = true;
    preferences.set(preferenceKey, preference);

    if (verdict.verdict !== 'keep') {
      defects.push({
        defectId: stableId('defect', { key, tags: verdict.tags }),
        eventKey: key,
        sceneId: session.sceneId,
        category: verdict.category,
        severity: verdict.verdict === 'reject' ? 'high' : 'medium',
        reviewerKind: session.reviewer.kind,
        context: stableValue(verdict.context ?? {}),
        tags: [...verdict.tags].sort(),
        note: verdict.note ?? '',
        status: 'open',
      });
    }

    const signature = signatureOf(verdict);
    const candidate = candidates.get(signature) ?? {
      candidateId: stableId('candidate', signature),
      signature,
      scenes: [],
      evidence: [],
      nickRejectEvidence: [],
      eligibleForPromotion: false,
      promotionRequiresNick: true,
    };
    if (!candidate.scenes.includes(session.sceneId)) candidate.scenes.push(session.sceneId);
    candidate.evidence.push(key);
    if (session.reviewer.kind === 'nick' && verdict.verdict === 'reject') candidate.nickRejectEvidence.push(key);
    candidate.eligibleForPromotion = candidate.nickRejectEvidence.length > 0 || candidate.scenes.length >= 2;
    candidates.set(signature, candidate);
  }
  const assetPreferences = [...preferences.values()].map((entry) => ({
    ...entry,
    affinity: Number(entry.affinity.toFixed(2)),
    evidence: entry.evidence.sort(),
  })).sort((a, b) => a.key.localeCompare(b.key));
  const approvedCandidateIds = new Set(rules.map((rule) => rule.candidateId));
  const ruleCandidates = [...candidates.values()].map((entry) => ({
    ...entry,
    scenes: entry.scenes.sort(),
    evidence: entry.evidence.sort(),
    nickRejectEvidence: entry.nickRejectEvidence.sort(),
  })).filter((entry) => entry.eligibleForPromotion && !approvedCandidateIds.has(entry.candidateId))
    .sort((a, b) => a.signature.localeCompare(b.signature));
  const resolutionByDefect = new Map(resolutions.map((entry) => [entry.defectId, entry]));
  const resolvedDefects = defects.map((entry) => {
    const resolution = resolutionByDefect.get(entry.defectId);
    return resolution == null ? entry : {
      ...entry,
      status: 'resolved',
      resolvedAt: resolution.resolvedAt,
      resolvedBy: resolution.resolvedBy,
    };
  });
  return {
    assetPreferences,
    defects: resolvedDefects.sort((a, b) => a.defectId.localeCompare(b.defectId)),
    ruleCandidates,
  };
}

function readJson(file, label = file) {
  try {
    return JSON.parse(readFileSync(file, 'utf8'));
  } catch (error) {
    fail(`cannot read ${label}: ${error.message}`);
  }
}

export function writeJsonAtomic(file, value) {
  mkdirSync(path.dirname(file), { recursive: true });
  const temporary = path.join(path.dirname(file), `.${path.basename(file)}.${process.pid}.${crypto.randomUUID()}.tmp`);
  try {
    writeFileSync(temporary, canonicalJson(value), { encoding: 'utf8', flag: 'wx' });
    renameSync(temporary, file);
  } finally {
    if (existsSync(temporary)) unlinkSync(temporary);
  }
}

export function writeReviewSessionAtomic(root, session) {
  session = validateReviewSession(session);
  const dir = path.join(root, 'review-sessions');
  const file = path.join(dir, `${session.sessionId}.json`);
  const body = canonicalJson(session);
  mkdirSync(dir, { recursive: true });
  if (existsSync(file)) {
    if (readFileSync(file, 'utf8') === body) return { file, duplicate: true };
    fail(`immutable session '${session.sessionId}' already exists with different content`);
  }
  const temporary = `${file}.${process.pid}.${crypto.randomUUID()}.tmp`;
  try {
    writeFileSync(temporary, body, { encoding: 'utf8', flag: 'wx' });
    renameSync(temporary, file);
  } finally {
    if (existsSync(temporary)) unlinkSync(temporary);
  }
  return { file, duplicate: false };
}

export function migrateKnowledge(value, kind) {
  if (!value || typeof value !== 'object') fail(`${kind} is not a JSON object`);
  if (value.schemaVersion === SCHEMA_VERSION) return value;
  if (value.schemaVersion === undefined && Array.isArray(value.items)) return { schemaVersion: 1, items: value.items };
  fail(`no ${kind} migration exists from schemaVersion '${value.schemaVersion}'`);
}

export function loadKnowledge(root = DEFAULT_KNOWLEDGE_ROOT) {
  const manifest = migrateKnowledge(readJson(path.join(root, REQUIRED_FILES.manifest), 'knowledge manifest'), 'manifest');
  if (manifest.schemaVersion !== SCHEMA_VERSION) fail(`manifest version mismatch`);
  const historicalDocument = migrateKnowledge(readJson(path.join(root, REQUIRED_FILES.historical), 'historical observations'), 'historical observations');
  const historical = (historicalDocument.items ?? []).map(validateReviewSession);
  const rulesDocument = migrateKnowledge(readJson(path.join(root, REQUIRED_FILES.rules), 'approved rules'), 'approved rules');
  const resolutionDocument = migrateKnowledge(readJson(path.join(root, REQUIRED_FILES.resolutions), 'defect resolutions'), 'defect resolutions');
  const baselineDocument = migrateKnowledge(readJson(path.join(root, REQUIRED_FILES.baselines), 'visual baselines'), 'visual baselines');
  const overrideDocument = migrateKnowledge(readJson(path.join(root, REQUIRED_FILES.overrides), 'adaptive overrides'), 'adaptive overrides');
  const rules = rulesDocument.items ?? [];
  const resolutions = resolutionDocument.items ?? [];
  const baselines = baselineDocument.items ?? [];
  const overrides = overrideDocument.items ?? [];
  if (!Array.isArray(rules)) fail('approved rules items must be an array');
  if (!Array.isArray(resolutions)) fail('defect resolutions items must be an array');
  if (!Array.isArray(baselines)) fail('visual baselines items must be an array');
  if (!Array.isArray(overrides)) fail('adaptive overrides items must be an array');
  const ruleIds = new Set(), ruleCandidates = new Set();
  for (const rule of rules) {
    assertObject(rule, 'approved rule');
    assertKeys(rule, new Set(['ruleId', 'candidateId', 'signature', 'scenes', 'evidence', 'promotedAt', 'promotedBy']), 'approved rule');
    text(rule.ruleId, 'approved rule ruleId');
    text(rule.candidateId, 'approved rule candidateId');
    text(rule.signature, 'approved rule signature');
    stringArray(rule.scenes, 'approved rule scenes', { required: true });
    stringArray(rule.evidence, 'approved rule evidence', { required: true });
    isoDate(rule.promotedAt, 'approved rule promotedAt');
    if (rule.promotedBy !== 'nick') fail('approved rules must be explicitly promoted by Nick');
    if (ruleIds.has(rule.ruleId)) fail(`duplicate approved rule '${rule.ruleId}'`);
    if (ruleCandidates.has(rule.candidateId)) fail(`candidate '${rule.candidateId}' was promoted twice`);
    ruleIds.add(rule.ruleId);
    ruleCandidates.add(rule.candidateId);
  }
  const resolvedIds = new Set();
  for (const resolution of resolutions) {
    assertObject(resolution, 'defect resolution');
    assertKeys(resolution, new Set(['defectId', 'resolvedAt', 'resolvedBy']), 'defect resolution');
    text(resolution.defectId, 'defect resolution defectId');
    isoDate(resolution.resolvedAt, 'defect resolution resolvedAt');
    if (resolution.resolvedBy !== 'nick') fail('defect resolutions must be explicitly authored by Nick');
    if (resolvedIds.has(resolution.defectId)) fail(`duplicate resolution for '${resolution.defectId}'`);
    resolvedIds.add(resolution.defectId);
  }
  const sessionDir = path.join(root, 'review-sessions');
  const sessions = existsSync(sessionDir) ? readdirSync(sessionDir)
    .filter((name) => name.endsWith('.json'))
    .sort()
    .map((name) => validateReviewSession(readJson(path.join(sessionDir, name), `review session ${name}`))) : [];
  return { root, manifest, historical, sessions, rules, resolutions, baselines, overrides };
}

export function rebuildDerived(root = DEFAULT_KNOWLEDGE_ROOT) {
  const loaded = loadKnowledge(root);
  const derived = aggregateKnowledge(loaded);
  writeJsonAtomic(path.join(root, REQUIRED_FILES.assetPreferences), { schemaVersion: 1, items: derived.assetPreferences });
  writeJsonAtomic(path.join(root, REQUIRED_FILES.defects), { schemaVersion: 1, items: derived.defects });
  writeJsonAtomic(path.join(root, REQUIRED_FILES.candidates), { schemaVersion: 1, items: derived.ruleCandidates });
  return { ...derived, sessionCount: loaded.sessions.length, historicalCount: loaded.historical.length };
}

function confirmedTimestamp(now) {
  const value = typeof now === 'string' ? now : new Date().toISOString();
  isoDate(value, 'action timestamp');
  return value;
}

export function promoteCandidate(root, candidateId, { nickConfirmed = false, now } = {}) {
  if (!nickConfirmed) fail('rule promotion requires explicit Nick confirmation');
  text(candidateId, 'candidateId');
  const loaded = loadKnowledge(root);
  const existing = loaded.rules.find((entry) => entry.candidateId === candidateId);
  if (existing != null) return { rule: existing, duplicate: true };
  const derived = aggregateKnowledge(loaded);
  const candidate = derived.ruleCandidates.find((entry) => entry.candidateId === candidateId);
  if (candidate == null) fail(`promotable candidate '${candidateId}' does not exist`);
  const rule = {
    ruleId: stableId('rule', candidate.signature),
    candidateId: candidate.candidateId,
    signature: candidate.signature,
    scenes: candidate.scenes,
    evidence: candidate.evidence,
    promotedAt: confirmedTimestamp(now),
    promotedBy: 'nick',
  };
  writeJsonAtomic(path.join(root, REQUIRED_FILES.rules), {
    schemaVersion: SCHEMA_VERSION,
    items: [...loaded.rules, rule].sort((a, b) => a.ruleId.localeCompare(b.ruleId)),
  });
  rebuildDerived(root);
  return { rule, duplicate: false };
}

export function resolveDefect(root, defectId, { nickConfirmed = false, now } = {}) {
  if (!nickConfirmed) fail('defect resolution requires explicit Nick confirmation');
  text(defectId, 'defectId');
  const loaded = loadKnowledge(root);
  const derived = aggregateKnowledge(loaded);
  const defect = derived.defects.find((entry) => entry.defectId === defectId);
  if (defect == null) fail(`derived defect '${defectId}' does not exist`);
  const existing = loaded.resolutions.find((entry) => entry.defectId === defectId);
  if (existing != null) return { resolution: existing, duplicate: true };
  const resolution = {
    defectId,
    resolvedAt: confirmedTimestamp(now),
    resolvedBy: 'nick',
  };
  writeJsonAtomic(path.join(root, REQUIRED_FILES.resolutions), {
    schemaVersion: SCHEMA_VERSION,
    items: [...loaded.resolutions, resolution].sort((a, b) => a.defectId.localeCompare(b.defectId)),
  });
  rebuildDerived(root);
  return { resolution, duplicate: false };
}

export function ensureKnowledgeRoot(root = DEFAULT_KNOWLEDGE_ROOT) {
  for (const directory of ['review-sessions', 'derived', 'schemas']) mkdirSync(path.join(root, directory), { recursive: true });
  return root;
}
