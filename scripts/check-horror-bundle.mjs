#!/usr/bin/env node

import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const REPO_ROOT = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
const storeCandidates = [
  process.env.UNITY_ASSET_STORE_CACHE,
  path.join(os.homedir(), 'Library', 'Unity', 'Asset Store-5.x'),
  path.join(os.homedir(), 'Library', 'Application Support', 'Unity', 'Asset Store-5.x'),
  path.join(os.homedir(), 'Library', 'Caches', 'com.unity3d.UnityEditor', 'Asset Store-5.x'),
].filter(Boolean);
const store = storeCandidates.find((candidate) => fs.existsSync(candidate));
const unityProject = process.env.GM_UNITY_PROJECT || path.join(REPO_ROOT, 'unity-project');

const expected = [
  { label: 'Haunted Village Environment', tokens: ['haunted', 'village'] },
  { label: 'Haunted Prison Environment', tokens: ['haunted', 'prison'] },
  { label: 'Demonic Village Environment', tokens: ['demonic', 'village'] },
  { label: 'The Aftermath Environment', tokens: ['aftermath'] },
  { label: 'Historical Museum', tokens: ['historical', 'museum'] },
  { label: 'Abandoned Horror Mansion Interior', tokens: ['abandoned', 'horror', 'mansion'] },
  { label: 'Witch Village Environment', tokens: ['witch', 'village'] },
  { label: "Sorcerer's Hut", tokens: ['sorcerer', 'hut'] },
  { label: 'Abandoned Village Environment', tokens: ['abandoned', 'village'] },
];

function normalize(value) {
  return value
    .toLowerCase()
    .normalize('NFKD')
    .replace(/[^a-z0-9]+/g, ' ')
    .trim();
}

function collectPackages(root) {
  const packages = [];
  const stack = [root];
  while (stack.length) {
    const dir = stack.pop();
    let entries = [];
    try {
      entries = fs.readdirSync(dir, { withFileTypes: true });
    } catch {
      continue;
    }
    for (const entry of entries) {
      const absolute = path.join(dir, entry.name);
      if (entry.isDirectory()) stack.push(absolute);
      else if (entry.name.toLowerCase().endsWith('.unitypackage')) packages.push(absolute);
    }
  }
  return packages;
}

if (!store) {
  const leartesRoot = path.join(unityProject, 'Assets', 'LeartesStudios');
  const importedNames = fs.existsSync(leartesRoot)
    ? fs.readdirSync(leartesRoot, { withFileTypes: true })
        .filter((entry) => entry.isDirectory())
        .map((entry) => normalize(entry.name))
    : [];
  console.error('Unity Asset Store payload cache not found in any known macOS location:');
  for (const candidate of storeCandidates) console.error(`  ${candidate}`);
  console.error('');
  console.error(`Selective project import inventory: ${leartesRoot}`);
  for (const item of expected) {
    const imported = importedNames.some((name) =>
      item.tokens.every((token) => name.includes(token)));
    console.error(`${imported ? 'IMPORTED' : 'ABSENT  '}  ${item.label}`);
  }
  console.error('');
  console.error('The current project proves selective imports, not that all nine source payloads are still cached.');
  console.error('Open Unity Package Manager > My Assets to re-download any payload needed for future rooms,');
  console.error('or set UNITY_ASSET_STORE_CACHE to a retained cache directory and rerun this check.');
  process.exit(2);
}

const packages = collectPackages(store).map((absolute) => ({
  absolute,
  name: path.basename(absolute, '.unitypackage'),
  normalized: normalize(path.basename(absolute, '.unitypackage')),
  bytes: fs.statSync(absolute).size,
}));

const wrapper = packages.find((pkg) =>
  pkg.normalized.includes('horror environments bundle') && pkg.normalized.includes('9 packs'));

console.log('Horror Environments Bundle intake');
console.log('----------------------------------');
if (wrapper) {
  console.log(`Entitlement wrapper: PRESENT (${(wrapper.bytes / 1024).toFixed(1)} KB)`);
} else {
  console.log('Entitlement wrapper: MISSING');
}
console.log('');

let missing = 0;
for (const item of expected) {
  const matches = packages
    .filter((pkg) => item.tokens.every((token) => pkg.normalized.includes(token)))
    .filter((pkg) => !pkg.normalized.includes('bundle'))
    .sort((a, b) => b.bytes - a.bytes);
  const match = matches[0];
  if (!match || match.bytes < 1024 * 1024) {
    missing++;
    console.log(`MISSING  ${item.label}`);
    continue;
  }
  console.log(`READY    ${item.label} — ${(match.bytes / (1024 ** 3)).toFixed(2)} GB`);
  console.log(`         ${match.absolute}`);
}

console.log('');
console.log(`${expected.length - missing}/${expected.length} payload packages ready`);
if (missing) {
  console.log('In Unity Package Manager → My Assets, download each missing pack separately.');
  console.log('Download is sufficient; do not import all nine into a Unity project.');
  process.exit(2);
}
