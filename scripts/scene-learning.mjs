#!/usr/bin/env node
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  DEFAULT_KNOWLEDGE_ROOT,
  loadKnowledge,
  promoteCandidate,
  rebuildDerived,
  resolveDefect,
  validateReviewSession,
  writeReviewSessionAtomic,
} from './scene-learning-store.mjs';

function usage() {
  console.log('Usage: node scripts/scene-learning.mjs <status|validate|record|rebuild|promote|resolve> [id-or-session.json] [--confirm-nick] [--root path]');
}

function parse(argv) {
  const rootIndex = argv.indexOf('--root');
  let root = DEFAULT_KNOWLEDGE_ROOT;
  if (rootIndex >= 0) {
    if (!argv[rootIndex + 1]) throw new Error('--root requires a path');
    root = path.resolve(argv[rootIndex + 1]);
    argv.splice(rootIndex, 2);
  }
  return { command: argv[0], file: argv[1], root, nickConfirmed: argv.includes('--confirm-nick') };
}

export function run(argv = process.argv.slice(2)) {
  const { command, file, root, nickConfirmed } = parse([...argv]);
  if (command === 'status') {
    const loaded = loadKnowledge(root);
    const derived = rebuildDerived(root);
    const openDefects = derived.defects.filter((entry) => entry.status === 'open').length;
    console.log(`Scene knowledge v${loaded.manifest.schemaVersion}: ${loaded.sessions.length} immutable review session(s), ${loaded.historical.length} historical observation(s), ${openDefects} open derived defect(s), ${derived.ruleCandidates.length} promotable draft rule(s), ${loaded.rules.length} approved rule(s).`);
    return;
  }
  if (command === 'validate') {
    if (!file || !existsSync(file)) throw new Error('validate requires an existing session JSON file');
    validateReviewSession(JSON.parse(readFileSync(file, 'utf8')));
    console.log(`✓ valid review session: ${path.resolve(file)}`);
    return;
  }
  if (command === 'record') {
    if (!file || !existsSync(file)) throw new Error('record requires an existing session JSON file');
    const result = writeReviewSessionAtomic(root, JSON.parse(readFileSync(file, 'utf8')));
    const derived = rebuildDerived(root);
    const openDefects = derived.defects.filter((entry) => entry.status === 'open').length;
    console.log(`${result.duplicate ? '✓ already recorded' : '✓ recorded'} ${path.basename(result.file)}; ${openDefects} open derived defect(s)`);
    return;
  }
  if (command === 'rebuild') {
    const derived = rebuildDerived(root);
    console.log(`✓ rebuilt deterministic knowledge: ${derived.assetPreferences.length} preference(s), ${derived.defects.length} defect(s), ${derived.ruleCandidates.length} candidate rule(s)`);
    return;
  }
  if (command === 'promote') {
    if (!file) throw new Error('promote requires a candidate id');
    const result = promoteCandidate(root, file, { nickConfirmed });
    console.log(`${result.duplicate ? '✓ already promoted' : '✓ promoted'} ${result.rule.ruleId} from ${result.rule.candidateId}`);
    return;
  }
  if (command === 'resolve') {
    if (!file) throw new Error('resolve requires a defect id');
    const result = resolveDefect(root, file, { nickConfirmed });
    console.log(`${result.duplicate ? '✓ already resolved' : '✓ resolved'} ${result.resolution.defectId}`);
    return;
  }
  usage();
  if (command) throw new Error(`unknown command '${command}'`);
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try { run(); }
  catch (error) { console.error(`✗ ${error.message}`); process.exit(1); }
}
