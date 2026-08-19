#!/usr/bin/env node
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

export const REPO_ROOT = path.dirname(path.dirname(fileURLToPath(import.meta.url)));

const buckets = ['portableNow', 'adapterNeeded', 'gameOwned'];

function loadJson(file) {
  try {
    return JSON.parse(readFileSync(file, 'utf8'));
  } catch (error) {
    throw new Error(`cannot read engine boundary ${file}: ${error.message}`);
  }
}

function declaredTypes(source) {
  return new Set([...source.matchAll(/\b(?:class|struct|interface|enum)\s+(Gm[A-Za-z0-9_]+)/g)]
    .map((match) => match[1]));
}

export function auditEngineBoundary({ repoRoot = REPO_ROOT } = {}) {
  const systemRoot = path.join(repoRoot, 'unity', 'scene-system');
  const runtimeRoot = path.join(systemRoot, 'Runtime');
  const manifestPath = path.join(systemRoot, 'engine-boundary.json');
  const manifest = loadJson(manifestPath);
  const errors = [];

  if (manifest.version !== 1) errors.push(`unsupported boundary version ${manifest.version}`);
  if (typeof manifest.packageTarget !== 'string' ||
      !/^[a-z0-9._-]+$/.test(manifest.packageTarget)) {
    errors.push('packageTarget must be a lowercase Unity package name');
  }
  if (!Array.isArray(manifest.forbiddenPortableTerms) ||
      manifest.forbiddenPortableTerms.length === 0) {
    errors.push('forbiddenPortableTerms must be a non-empty array');
  } else {
    const terms = manifest.forbiddenPortableTerms;
    if (terms.some((term) => typeof term !== 'string' || term.length === 0)) {
      errors.push('forbiddenPortableTerms entries must be non-empty strings');
    }
    if (new Set(terms).size !== terms.length) {
      errors.push('forbiddenPortableTerms contains duplicates');
    }
  }
  if (!manifest.runtime || typeof manifest.runtime !== 'object') {
    errors.push('runtime classification is missing');
    return { errors, manifest, counts: {} };
  }

  const actual = readdirSync(runtimeRoot).filter((file) => file.endsWith('.cs')).sort();
  const owners = new Map();
  for (const bucket of buckets) {
    const files = manifest.runtime[bucket];
    if (!Array.isArray(files)) {
      errors.push(`runtime.${bucket} must be an array`);
      continue;
    }
    for (const file of files) {
      const claimed = owners.get(file) ?? [];
      claimed.push(bucket);
      owners.set(file, claimed);
      if (!existsSync(path.join(runtimeRoot, file))) errors.push(`${bucket} lists missing file ${file}`);
    }
  }

  for (const file of actual) {
    const claimed = owners.get(file) ?? [];
    if (claimed.length === 0) errors.push(`unclassified runtime file ${file}`);
    if (claimed.length > 1) errors.push(`${file} is classified more than once: ${claimed.join(', ')}`);
  }
  for (const file of owners.keys()) {
    if (!actual.includes(file)) errors.push(`classification has no runtime source: ${file}`);
  }

  const nonPortableTypes = new Set();
  for (const bucket of ['adapterNeeded', 'gameOwned']) {
    for (const file of manifest.runtime[bucket] ?? []) {
      const sourcePath = path.join(runtimeRoot, file);
      if (!existsSync(sourcePath)) continue;
      for (const type of declaredTypes(readFileSync(sourcePath, 'utf8'))) nonPortableTypes.add(type);
    }
  }

  for (const file of manifest.runtime.portableNow ?? []) {
    const sourcePath = path.join(runtimeRoot, file);
    if (!existsSync(sourcePath)) continue;
    const source = readFileSync(sourcePath, 'utf8');
    for (const term of manifest.forbiddenPortableTerms ?? []) {
      if (source.includes(term)) errors.push(`${file} contains game-owned term ${JSON.stringify(term)}`);
    }
    const ownTypes = declaredTypes(source);
    for (const type of nonPortableTypes) {
      if (ownTypes.has(type)) continue;
      if (new RegExp(`\\b${type}\\b`).test(source)) {
        errors.push(`${file} depends on non-portable runtime type ${type}`);
      }
    }
  }

  const counts = Object.fromEntries(buckets.map((bucket) =>
    [bucket, Array.isArray(manifest.runtime[bucket]) ? manifest.runtime[bucket].length : 0]));
  counts.total = actual.length;
  return { errors: [...new Set(errors)].sort(), manifest, counts };
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    const result = auditEngineBoundary();
    console.log(`Engine boundary: ${result.counts.portableNow ?? 0} portable now, ` +
      `${result.counts.adapterNeeded ?? 0} need adapters, ${result.counts.gameOwned ?? 0} game-owned ` +
      `(${result.counts.total ?? 0} runtime files)`);
    if (result.errors.length) {
      for (const error of result.errors) console.error(`  ✗ ${error}`);
      console.error(`✗ Engine boundary: ${result.errors.length} problem(s)`);
      process.exit(1);
    }
    console.log('✓ Engine boundary is complete and portable candidates do not import game-owned runtime code');
  } catch (error) {
    console.error(`✗ ${error.message}`);
    process.exit(2);
  }
}
