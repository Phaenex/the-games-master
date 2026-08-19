import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
const expectedVersion = '1.4.1';
const packageName = 'com.unity.animation.rigging';
const gamecraftName = 'com.nyx.gamecraft';
const gamecraftPaths = {
  'unity-project': 'file:../../../../gamecraft-engine',
  'unity/project': 'file:../../../../gamecraft-engine',
};

const required = JSON.parse(readFileSync(path.join(repoRoot, 'unity', 'required-packages.json'), 'utf8'));
assert.equal(required.packages?.[packageName]?.version, expectedVersion,
  `${packageName} must be pinned by the fresh-clone package guard`);

for (const projectRoot of ['unity-project', 'unity/project']) {
  const manifest = JSON.parse(readFileSync(path.join(repoRoot, projectRoot, 'Packages', 'manifest.json'), 'utf8'));
  const lock = JSON.parse(readFileSync(path.join(repoRoot, projectRoot, 'Packages', 'packages-lock.json'), 'utf8'));
  assert.equal(manifest.dependencies?.[packageName], expectedVersion,
    `${projectRoot} manifest must pin ${packageName}@${expectedVersion}`);
  assert.equal(lock.dependencies?.[packageName]?.version, expectedVersion,
    `${projectRoot} lock must resolve ${packageName}@${expectedVersion}`);
  assert.equal(lock.dependencies?.[packageName]?.depth, 0,
    `${projectRoot} lock must keep Animation Rigging as a direct dependency`);
  assert.equal(lock.dependencies?.[packageName]?.source, 'registry',
    `${projectRoot} lock must resolve Animation Rigging from Unity's registry`);
  assert.equal(manifest.dependencies?.[gamecraftName], gamecraftPaths[projectRoot],
    `${projectRoot} manifest must link the standalone GameCraft repository`);
  assert.ok(manifest.testables?.includes(gamecraftName),
    `${projectRoot} must run GameCraft package tests`);
  assert.equal(lock.dependencies?.[gamecraftName]?.version, gamecraftPaths[projectRoot],
    `${projectRoot} lock must resolve the expected GameCraft path`);
  assert.equal(lock.dependencies?.[gamecraftName]?.depth, 0,
    `${projectRoot} lock must keep GameCraft as a direct dependency`);
  assert.equal(lock.dependencies?.[gamecraftName]?.source, 'local',
    `${projectRoot} lock must resolve GameCraft from the sibling repository`);
}

console.log('Unity package contract tests passed (Animation Rigging and GameCraft pinned in both projects).');
