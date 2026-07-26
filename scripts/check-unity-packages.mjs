#!/usr/bin/env node
// Verifies the Unity project carries the packages this repo's synced sources need to compile.
//
// The failure this prevents: the Unity project at ~/GamesMaster-Unity is not a git repository, so its
// Packages/manifest.json is not version controlled. A restored, rebuilt or freshly cloned project
// loses every package added by hand, and the first symptom is a wall of compile errors in a file that
// has not changed. Requirements live in unity/required-packages.json, which IS tracked.
import { existsSync, readFileSync } from 'node:fs';
import { homedir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const REPO_ROOT = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
const UNITY_ROOT = process.env.GM_UNITY_PROJECT || path.join(homedir(), 'GamesMaster-Unity');
const REQUIRED = path.join(REPO_ROOT, 'unity', 'required-packages.json');
const MANIFEST = path.join(UNITY_ROOT, 'Packages', 'manifest.json');

export function checkUnityPackages({ required = REQUIRED, manifest = MANIFEST } = {}) {
  if (!existsSync(required)) throw new Error(`missing requirements file: ${required}`);
  if (!existsSync(manifest)) throw new Error(`missing Unity manifest: ${manifest}`);

  const wanted = JSON.parse(readFileSync(required, 'utf8')).packages ?? {};
  const have = JSON.parse(readFileSync(manifest, 'utf8')).dependencies ?? {};

  const problems = [];
  for (const [name, spec] of Object.entries(wanted)) {
    if (!(name in have)) {
      problems.push(`${name} is MISSING; needed by ${spec.neededBy} (${spec.why})`);
      continue;
    }
    if (have[name] !== spec.version) {
      problems.push(`${name} is ${have[name]}, expected ${spec.version}. ${spec.note ?? ''}`.trim());
    }
  }
  return problems;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    const problems = checkUnityPackages();
    if (problems.length) {
      for (const p of problems) console.error(`  ✗ ${p}`);
      console.error(`✗ Unity packages: ${problems.length} problem(s)`);
      process.exit(1);
    }
    console.log('✓ Unity packages: all required packages present at the expected versions');
  } catch (error) {
    console.error(`✗ ${error.message}`);
    process.exit(2);
  }
}
