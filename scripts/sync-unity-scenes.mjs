#!/usr/bin/env node
// Copies registered scene source packs from this repository into the Unity project. --check is
// read-only and fails on missing or drifted files, making repo-to-Unity divergence visible in CI.
import {
  copyFileSync,
  existsSync,
  mkdirSync,
  readFileSync,
  readdirSync,
} from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { loadSceneRegistry } from './unity-scene-registry.mjs';

export const REPO_ROOT = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
export const UNITY_ROOT = process.env.GM_UNITY_PROJECT || path.join(REPO_ROOT, 'unity-project');

const DESTINATIONS = {
  Editor: ['Assets', 'Editor', 'Scenes'],
  Runtime: ['Assets', 'Scripts', 'Scenes'],
  Tests: ['Assets', 'Tests', 'EditMode', 'Editor', 'Scenes'],
};

const SHARED_DESTINATIONS = {
  Editor: ['Assets', 'Editor'],
  Runtime: ['Assets', 'Scripts'],
  Tests: ['Assets', 'Tests', 'EditMode', 'Editor', 'SceneSystem'],
};

function walkFiles(dir, prefix = '', predicate = () => true) {
  if (!existsSync(dir)) return [];
  const files = [];
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const relative = path.join(prefix, entry.name);
    const absolute = path.join(dir, entry.name);
    if (entry.isDirectory()) files.push(...walkFiles(absolute, relative, predicate));
    else if (entry.isFile() && predicate(relative, entry.name)) files.push(relative);
  }
  return files.sort();
}

function walkCsharp(dir, prefix = '') {
  return walkFiles(dir, prefix, (_relative, name) => name.endsWith('.cs'));
}

function addPlanItem(plan, destinations, item) {
  const key = path.resolve(item.destination);
  const existing = destinations.get(key);
  if (existing) {
    throw new Error(`Unity sync destination is governed twice: ${item.destination} (${existing.sceneId}, ${item.sceneId})`);
  }
  destinations.set(key, item);
  plan.push(item);
}

function isProjectOwnedUnityFile(relative) {
  const normalized = relative.split(path.sep).join('/');
  return /^Assets\/(?:Editor|Scripts|Tests)\/.*\/Gm[^/]*\.cs$/.test(normalized) ||
    /^Assets\/(?:Editor|Scripts)\/Gm[^/]*\.cs$/.test(normalized) ||
    normalized === 'Assets/Resources/Input/GmControls.inputactions' ||
    // GmHudTheme.tss is every HUD/pause/credits UI Toolkit panel's shared theme (GmGameHud,
    // GmPauseMenu, GmCreditsUI, GmHouseHud). It has no scene owner of its own -- same class of
    // shared, non-scene-specific Resources asset as GmControls.inputactions above.
    normalized === 'Assets/Resources/GmHudTheme.tss' ||
    normalized === 'Assets/Tests/PlayMode/GamesMaster.PlayMode.Tests.asmdef' ||
    /^Assets\/StreamingAssets\/(?:prologue|village)-design\.json$/.test(normalized);
}

export function buildSyncPlan(registry, {
  repoRoot = REPO_ROOT,
  unityRoot = UNITY_ROOT,
  sceneIds = null,
} = {}) {
  const wanted = sceneIds ? new Set(sceneIds) : null;
  if (wanted) {
    for (const id of wanted) {
      if (!registry.scenes.some((scene) => scene.id === id)) throw new Error(`unknown Unity scene '${id}'`);
    }
  }
  const plan = [];
  const destinations = new Map();
  const skipped = [];
  const sharedRoot = path.join(repoRoot, 'unity', 'scene-system');
  for (const [bucket, destinationParts] of Object.entries(SHARED_DESTINATIONS)) {
    const bucketRoot = path.join(sharedRoot, bucket);
    const files = walkCsharp(bucketRoot);
    if (!existsSync(bucketRoot) || files.length === 0) {
      throw new Error(`shared scene-system source bucket is missing or empty: ${bucketRoot}`);
    }
    for (const relative of files) {
      addPlanItem(plan, destinations, {
        sceneId: 'scene-system',
        source: path.join(bucketRoot, relative),
        destination: path.join(unityRoot, ...destinationParts, relative),
      });
    }
  }
  for (const scene of registry.scenes) {
    if (wanted && !wanted.has(scene.id)) continue;
    if (!scene.sourceDir) { skipped.push(scene.id); continue; }
    const sourceRoot = path.join(repoRoot, scene.sourceDir);
    if (!existsSync(sourceRoot)) throw new Error(`registered scene source is missing: ${sourceRoot}`);
    for (const [bucket, destinationParts] of Object.entries(DESTINATIONS)) {
      const bucketRoot = path.join(sourceRoot, bucket);
      const files = walkCsharp(bucketRoot);
      if (!existsSync(bucketRoot) || files.length === 0) {
        throw new Error(`registered scene '${scene.id}' source bucket is missing or empty: ${bucketRoot}`);
      }
      for (const relative of files) {
        addPlanItem(plan, destinations, {
          sceneId: scene.id,
          source: path.join(bucketRoot, relative),
          destination: path.join(unityRoot, ...destinationParts, scene.id, relative),
        });
      }
    }
  }

  // Exact-path overlay for project-authored Unity files that are shared by scenes: gameplay
  // runtime, builders, PlayMode tests, input actions, design data, package pins and project settings.
  // Purchased assets stay outside this tree and are never copied into the repository.
  const projectOverlay = path.join(repoRoot, 'unity', 'project');
  for (const relative of walkFiles(projectOverlay, '', (_relative, name) => name !== '.DS_Store')) {
    addPlanItem(plan, destinations, {
      sceneId: 'project-overlay',
      source: path.join(projectOverlay, relative),
      destination: path.join(unityRoot, relative),
    });
  }

  // Shut-the-Box has its own assembly layout and intentionally does not live in the generic scene
  // buckets. It is still governed by the same drift contract.
  const shutBoxRoot = path.join(repoRoot, 'unity', 'shut-the-box');
  for (const relative of walkFiles(shutBoxRoot, '', (_relative, name) =>
    name.endsWith('.cs') || name.endsWith('.asmdef') || name.endsWith('.meta'))) {
    addPlanItem(plan, destinations, {
      sceneId: 'shut-the-box',
      source: path.join(shutBoxRoot, relative),
      destination: path.join(unityRoot, 'Assets', 'GamesMaster', 'ShutTheBox', relative),
    });
  }
  return { plan, skipped };
}

export function syncUnityScenes(registry, {
  check = false,
  repoRoot = REPO_ROOT,
  unityRoot = UNITY_ROOT,
  sceneIds = null,
} = {}) {
  const { plan, skipped } = buildSyncPlan(registry, { repoRoot, unityRoot, sceneIds });
  const drift = [];
  let copied = 0;
  for (const item of plan) {
    const sourceBytes = readFileSync(item.source);
    const matches = existsSync(item.destination) && readFileSync(item.destination).equals(sourceBytes);
    if (matches) continue;
    drift.push(item);
    if (!check) {
      mkdirSync(path.dirname(item.destination), { recursive: true });
      copyFileSync(item.source, item.destination);
      copied++;
    }
  }
  const governed = new Set(plan.map((item) => path.resolve(item.destination)));
  // Only Assets/ can ever match isProjectOwnedUnityFile, so only Assets/ is walked. Walking the
  // whole project instead reaches Library/ (40k+ files here) and costs ~0.5s on every --check.
  const assetsRoot = path.join(unityRoot, 'Assets');
  const unmanaged = existsSync(assetsRoot)
    ? walkFiles(assetsRoot, 'Assets').filter((relative) => isProjectOwnedUnityFile(relative) &&
      !governed.has(path.resolve(unityRoot, relative)))
    : [];
  return { plan, skipped, drift, unmanaged, copied, clean: drift.length === 0 && unmanaged.length === 0 };
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    const args = process.argv.slice(2);
    const check = args.includes('--check');
    const sceneIds = args.filter((arg) => arg !== '--check');
    const registry = loadSceneRegistry();
    const result = syncUnityScenes(registry, { check, sceneIds: sceneIds.length ? sceneIds : null });
    for (const id of result.skipped) console.log(`  - ${id}: existing Unity-only source, nothing to sync`);
    if (check && result.drift.length) {
      for (const item of result.drift) console.error(`  ✗ ${item.sceneId}: ${item.destination}`);
      console.error(`✗ Unity scene source drift: ${result.drift.length} file(s) missing or different`);
      process.exit(1);
    }
    if (check && result.unmanaged.length) {
      for (const relative of result.unmanaged) console.error(`  ✗ Unity-only project source: ${relative}`);
      console.error(`✗ Unity source ownership gap: ${result.unmanaged.length} project-authored file(s) exist only in Unity`);
      process.exit(1);
    }
    if (check) console.log(`✓ Unity scene sources match: ${result.plan.length} tracked file(s)`);
    else console.log(`✓ Unity scene sync: ${result.copied} copied, ${result.plan.length - result.drift.length} already current`);
  } catch (error) {
    console.error(`✗ ${error.message}`);
    process.exit(2);
  }
}
