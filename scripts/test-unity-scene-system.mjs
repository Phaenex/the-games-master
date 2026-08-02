#!/usr/bin/env node
import assert from 'node:assert/strict';
import {
  appendFileSync,
  cpSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import test from 'node:test';
import {
  loadSceneRegistry,
  markerRegex,
  REGISTRY_PATH,
  resolveScene,
  validateSceneRegistry,
} from './unity-scene-registry.mjs';
import {
  createSceneSpec,
  REPO_ROOT,
  renderSceneFiles,
  scaffoldScene,
} from './scaffold-unity-scene.mjs';
import { buildSyncPlan, syncUnityScenes } from './sync-unity-scenes.mjs';
import { evaluateHostCapacity } from './unity-host-health.mjs';

function clone(value) {
  return JSON.parse(JSON.stringify(value));
}

test('production registry validates and resolves its default scene', () => {
  const registry = loadSceneRegistry();
  assert.equal(resolveScene(registry).id, 'wend-hill-prologue');
  assert.equal(resolveScene(registry).tour.shots, 8);
  assert.equal(resolveScene(registry).performance.p95Milliseconds, 16.7);
});

test('runtime Wend Hill catalog stays aligned with the command registry', () => {
  const scene = resolveScene(loadSceneRegistry());
  const catalog = readFileSync(path.join(
    path.dirname(REGISTRY_PATH), 'Runtime', 'GmSceneIdentity.cs',
  ), 'utf8');
  assert.match(catalog, new RegExp(`WendHillId = "${scene.id}"`));
  assert.match(catalog, new RegExp(`WendHillName = "${scene.displayName}"`));
  assert.match(catalog, new RegExp(`WendHillPath = "${scene.scenePath.replaceAll('.', '\\.')}`));
});

test('registry rejects duplicate ids', () => {
  const registry = loadSceneRegistry();
  registry.scenes.push(clone(registry.scenes[0]));
  assert.throws(() => validateSceneRegistry(registry), /duplicate scene id/);
});

test('registry rejects traversal and scene-name/path drift', () => {
  const traversal = loadSceneRegistry();
  traversal.scenes[0].scenePath = 'Assets/Scenes/../Stolen.unity';
  assert.throws(() => validateSceneRegistry(traversal), /safe path/);

  const mismatch = loadSceneRegistry();
  mismatch.scenes[0].scenePath = 'Assets/Scenes/OtherName.unity';
  assert.throws(() => validateSceneRegistry(mismatch), /basename must match sceneName/);
});

test('registry rejects invalid execute methods and unregistered defaults', () => {
  const method = loadSceneRegistry();
  method.scenes[0].build.method = 'not a method';
  assert.throws(() => validateSceneRegistry(method), /invalid value/);

  const missingDefault = loadSceneRegistry();
  missingDefault.defaultScene = 'missing-scene';
  assert.throws(() => validateSceneRegistry(missingDefault), /is not registered/);

  const unknown = loadSceneRegistry();
  unknown.surprise = true;
  assert.throws(() => validateSceneRegistry(unknown), /unknown field/);

  const shortMarker = loadSceneRegistry();
  shortMarker.scenes[0].audit.success = 'ok';
  assert.throws(() => validateSceneRegistry(shortMarker), /non-empty, trimmed string/);
});

test('success markers compile as literal regular expressions', () => {
  const marker = markerRegex('[Builder] DONE + exact');
  assert.equal(marker.test('log [Builder] DONE + exact now'), true);
  assert.equal(marker.test('log Builder DONEEE exact now'), false);
});

test('host-capacity gate scales load against CPU count', () => {
  assert.equal(evaluateHostCapacity({ oneMinuteLoad: 20, coreCount: 10 }).healthy, true);
  assert.equal(evaluateHostCapacity({ oneMinuteLoad: 31, coreCount: 10 }).healthy, false);
  assert.equal(evaluateHostCapacity({ oneMinuteLoad: Number.NaN, coreCount: 0 }).healthy, true);
});

test('scaffold derives consistent names and generates all mandatory gates', () => {
  const spec = createSceneSpec({ id: 'entry-hall', phase: 1, shots: 5 });
  assert.equal(spec.className, 'GmEntryHall');
  assert.equal(spec.sceneName, 'EntryHall');
  const files = renderSceneFiles(spec);
  assert.deepEqual([...files.keys()], [
    'Editor/GmEntryHallBuilder.cs',
    'Editor/GmEntryHallCompositionPlan.cs',
    'Editor/GmEntryHallQualityAudit.cs',
    'Runtime/GmEntryHallShotTour.cs',
    'Tests/GmEntryHallBuildTests.cs',
    'README.md',
  ]);
  assert.match(files.get('Runtime/GmEntryHallShotTour.cs'), /01-replace-me/);
  assert.doesNotMatch(files.get('Runtime/GmEntryHallShotTour.cs'), /GmEntryHallBuilder/);
  assert.match(files.get('Runtime/GmEntryHallShotTour.cs'), /Assets\/Scenes\/EntryHall\.unity/);
  assert.match(files.get('Tests/GmEntryHallBuildTests.cs'), /HasPlaceholderShots/);
  assert.match(files.get('Tests/GmEntryHallBuildTests.cs'), /GmSceneCompositionAudit/);
  assert.match(files.get('Editor/GmEntryHallCompositionPlan.cs'), /replace-me: describe the visual hierarchy/);
  assert.match(files.get('Editor/GmEntryHallQualityAudit.cs'), /GmSceneCompositionAudit/);
  assert.match(files.get('Editor/GmEntryHallQualityAudit.cs'), /GmSceneContractAudit/);
});

test('scaffold writes repo source, updates registry once, and sync detects drift', () => {
  const tempRoot = mkdtempSync(path.join(tmpdir(), 'gm-scene-system-'));
  try {
    const registryDir = path.join(tempRoot, 'unity', 'scene-system');
    const registryPath = path.join(registryDir, 'scene-registry.json');
    mkdirSync(registryDir, { recursive: true });
    cpSync(path.join(path.dirname(REGISTRY_PATH), 'Editor'), path.join(registryDir, 'Editor'), { recursive: true });
    cpSync(path.join(path.dirname(REGISTRY_PATH), 'Runtime'), path.join(registryDir, 'Runtime'), { recursive: true });
    cpSync(path.join(path.dirname(REGISTRY_PATH), 'Tests'), path.join(registryDir, 'Tests'), { recursive: true });
    writeFileSync(registryPath, readFileSync(REGISTRY_PATH));

    const spec = createSceneSpec({ id: 'entry-hall', phase: 1, shots: 5 });
    const result = scaffoldScene(spec, { write: true, repoRoot: tempRoot, registryPath });
    assert.equal(result.wrote, true);
    const updated = loadSceneRegistry(registryPath);
    assert.equal(resolveScene(updated, 'entry-hall').sourceDir, 'unity/scenes/entry-hall');
    assert.throws(
      () => scaffoldScene(spec, { write: true, repoRoot: tempRoot, registryPath }),
      /already registered/,
    );

    const unityRoot = path.join(tempRoot, 'UnityProject');
    const before = syncUnityScenes(updated, {
      check: true,
      repoRoot: tempRoot,
      unityRoot,
      sceneIds: ['entry-hall'],
    });
    assert.ok(before.plan.length >= 27, 'shared system and generated scene files are all planned');
    assert.equal(before.drift.length, before.plan.length);

    const written = syncUnityScenes(updated, {
      repoRoot: tempRoot,
      unityRoot,
      sceneIds: ['entry-hall'],
    });
    assert.equal(written.copied, before.plan.length);
    const clean = syncUnityScenes(updated, {
      check: true,
      repoRoot: tempRoot,
      unityRoot,
      sceneIds: ['entry-hall'],
    });
    assert.equal(clean.clean, true);

    appendFileSync(clean.plan[0].destination, '// drift\n');
    const drifted = syncUnityScenes(updated, {
      check: true,
      repoRoot: tempRoot,
      unityRoot,
      sceneIds: ['entry-hall'],
    });
    assert.equal(drifted.drift.length, 1);
  } finally {
    rmSync(tempRoot, { recursive: true, force: true });
  }
});

test('sync refuses a registered scene whose source directory is absent', () => {
  const registry = loadSceneRegistry();
  const missing = clone(registry.scenes[0]);
  missing.id = 'missing-room';
  missing.displayName = 'Missing Room';
  missing.sceneName = 'MissingRoom';
  missing.scenePath = 'Assets/Scenes/MissingRoom.unity';
  missing.sourceDir = 'unity/scenes/missing-room';
  missing.build.method = 'GmMissingRoomBuilder.Build';
  missing.audit.method = 'GmMissingRoomQualityAudit.Run';
  missing.tour.method = 'GmMissingRoomShotTourMenu.ArmAndPlay';
  registry.scenes.push(missing);
  validateSceneRegistry(registry);
  assert.throws(
    () => buildSyncPlan(registry, { repoRoot: REPO_ROOT, sceneIds: ['missing-room'] }),
    /registered scene source is missing/,
  );
});
