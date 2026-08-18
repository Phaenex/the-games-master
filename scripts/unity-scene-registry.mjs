import { existsSync, readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const ROOT = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
export const REGISTRY_PATH = path.join(ROOT, 'unity', 'scene-system', 'scene-registry.json');

const ID = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;
const SCENE_NAME = /^[A-Za-z][A-Za-z0-9_]*$/;
const METHOD = /^[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)+$/;
const LOG_TAG = /^[A-Za-z_][A-Za-z0-9_]*$/;
const STATUSES = new Set(['scaffold', 'blockout', 'in-progress', 'review', 'approved', 'legacy']);

function plainObject(value) {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

function requireString(value, label, pattern = null, minLength = 1) {
  if (typeof value !== 'string' || value.trim() !== value || value.length < minLength) {
    throw new Error(`${label} must be a non-empty, trimmed string`);
  }
  if (pattern && !pattern.test(value)) throw new Error(`${label} has invalid value '${value}'`);
}

function validateCommand(command, label, extraAllowed = []) {
  if (!plainObject(command)) throw new Error(`${label} must be an object`);
  const keys = Object.keys(command).sort();
  const allowed = label.endsWith('.tour')
    ? ['logTag', 'method', 'minimumLuminanceRange', 'shots', 'success']
    : ['method', 'success', ...extraAllowed];
  const extras = keys.filter((key) => !allowed.includes(key));
  if (extras.length) throw new Error(`${label} has unknown field(s): ${extras.join(', ')}`);
  requireString(command.method, `${label}.method`, METHOD);
  requireString(command.success, `${label}.success`, null, 4);
}

function requireSafeRelative(value, label) {
  requireString(value, label);
  if (path.posix.isAbsolute(value) || value.split('/').includes('..') || value.includes('\\')) {
    throw new Error(`${label} must be a safe POSIX-relative path`);
  }
}

function validateStandalone(standalone, label) {
  validateCommand(standalone, label, ['appPath', 'executablePath']);
  requireSafeRelative(standalone.appPath, `${label}.appPath`);
  requireSafeRelative(standalone.executablePath, `${label}.executablePath`);
  if (!standalone.appPath.endsWith('.app')) throw new Error(`${label}.appPath must end in .app`);
}

function validateProbe(probe, label) {
  if (!plainObject(probe)) throw new Error(`${label} must be an object`);
  const extras = Object.keys(probe).filter((key) => !['flag', 'success', 'report'].includes(key));
  if (extras.length) throw new Error(`${label} has unknown field(s): ${extras.join(', ')}`);
  requireString(probe.flag, `${label}.flag`, /^-[A-Za-z][A-Za-z0-9]+$/);
  requireString(probe.success, `${label}.success`, null, 4);
  if (probe.report !== undefined) requireSafeRelative(probe.report, `${label}.report`);
}

function validatePerformance(performance, label) {
  if (!plainObject(performance)) throw new Error(`${label} must be an object`);
  const required = ['width', 'height', 'vSync', 'warmupSeconds', 'p95Milliseconds'];
  const extras = Object.keys(performance).filter((key) => !required.includes(key));
  if (extras.length) throw new Error(`${label} has unknown field(s): ${extras.join(', ')}`);
  for (const key of required) {
    if (typeof performance[key] !== 'number' || !Number.isFinite(performance[key])) {
      throw new Error(`${label}.${key} must be a finite number`);
    }
  }
  if (!Number.isInteger(performance.width) || performance.width < 640 ||
      !Number.isInteger(performance.height) || performance.height < 360) {
    throw new Error(`${label} resolution is invalid`);
  }
  if (![0, 1].includes(performance.vSync) || performance.warmupSeconds < 0 ||
      performance.p95Milliseconds <= 0) throw new Error(`${label} budget is invalid`);
}

export function validateSceneRegistry(registry) {
  if (!plainObject(registry)) throw new Error('scene registry must be an object');
  const rootAllowed = new Set(['$schema', 'version', 'defaultScene', 'scenes']);
  const rootExtras = Object.keys(registry).filter((key) => !rootAllowed.has(key));
  if (rootExtras.length) throw new Error(`scene registry has unknown field(s): ${rootExtras.join(', ')}`);
  if (registry.$schema !== undefined) requireString(registry.$schema, '$schema');
  if (registry.version !== 2) throw new Error(`scene registry version must be 2, got '${registry.version}'`);
  requireString(registry.defaultScene, 'defaultScene', ID);
  if (!Array.isArray(registry.scenes) || registry.scenes.length === 0) {
    throw new Error('scene registry must contain at least one scene');
  }

  const ids = new Set();
  const names = new Set();
  const paths = new Set();
  for (let i = 0; i < registry.scenes.length; i++) {
    const scene = registry.scenes[i];
    const label = `scenes[${i}]`;
    if (!plainObject(scene)) throw new Error(`${label} must be an object`);
    const allowed = new Set([
      'id', 'displayName', 'phase', 'status', 'sceneName', 'scenePath', 'sourceDir',
      'build', 'audit', 'tour', 'standalone', 'probes', 'performance',
    ]);
    const extras = Object.keys(scene).filter((key) => !allowed.has(key));
    if (extras.length) throw new Error(`${label} has unknown field(s): ${extras.join(', ')}`);

    requireString(scene.id, `${label}.id`, ID);
    requireString(scene.displayName, `${label}.displayName`, null, 2);
    requireString(scene.sceneName, `${label}.sceneName`, SCENE_NAME);
    requireString(scene.scenePath, `${label}.scenePath`);
    if (!Number.isInteger(scene.phase) || scene.phase < 0 || scene.phase > 10) {
      throw new Error(`${label}.phase must be an integer from 0 to 10`);
    }
    if (!STATUSES.has(scene.status)) throw new Error(`${label}.status '${scene.status}' is not valid`);
    if (!scene.scenePath.startsWith('Assets/Scenes/') || !scene.scenePath.endsWith('.unity') ||
        scene.scenePath.includes('..') || path.posix.isAbsolute(scene.scenePath)) {
      throw new Error(`${label}.scenePath must be a safe path below Assets/Scenes ending in .unity`);
    }
    if (path.posix.basename(scene.scenePath, '.unity') !== scene.sceneName) {
      throw new Error(`${label}.scenePath basename must match sceneName '${scene.sceneName}'`);
    }
    if (scene.sourceDir !== undefined) {
      requireString(scene.sourceDir, `${label}.sourceDir`);
      if (scene.sourceDir !== `unity/scenes/${scene.id}`) {
        throw new Error(`${label}.sourceDir must be 'unity/scenes/${scene.id}'`);
      }
    }

    validateCommand(scene.build, `${label}.build`);
    validateCommand(scene.audit, `${label}.audit`);
    validateCommand(scene.tour, `${label}.tour`);
    requireString(scene.tour.logTag, `${label}.tour.logTag`, LOG_TAG);
    if (!Number.isInteger(scene.tour.shots) || scene.tour.shots < 1 || scene.tour.shots > 99) {
      throw new Error(`${label}.tour.shots must be an integer from 1 to 99`);
    }
    if (scene.tour.minimumLuminanceRange !== undefined &&
        (!Number.isInteger(scene.tour.minimumLuminanceRange) ||
         scene.tour.minimumLuminanceRange < 1 || scene.tour.minimumLuminanceRange > 255)) {
      throw new Error(`${label}.tour.minimumLuminanceRange must be an integer from 1 to 255`);
    }
    if (scene.standalone !== undefined) validateStandalone(scene.standalone, `${label}.standalone`);
    if (scene.probes !== undefined) {
      if (!plainObject(scene.probes) || Object.keys(scene.probes).length === 0)
        throw new Error(`${label}.probes must be a non-empty object`);
      for (const [name, probe] of Object.entries(scene.probes)) {
        if (!/^[a-z][a-z0-9-]*$/.test(name)) throw new Error(`${label}.probes has invalid name '${name}'`);
        validateProbe(probe, `${label}.probes.${name}`);
      }
    }
    if (scene.performance !== undefined) validatePerformance(scene.performance, `${label}.performance`);
    if (scene.id === registry.defaultScene &&
        (!scene.standalone || !scene.probes?.walk || !scene.probes?.walls || !scene.performance)) {
      throw new Error(`${label} is the default scene and must define standalone, walk/walls probes, and performance`);
    }

    if (ids.has(scene.id)) throw new Error(`duplicate scene id '${scene.id}'`);
    if (names.has(scene.sceneName)) throw new Error(`duplicate sceneName '${scene.sceneName}'`);
    if (paths.has(scene.scenePath)) throw new Error(`duplicate scenePath '${scene.scenePath}'`);
    ids.add(scene.id);
    names.add(scene.sceneName);
    paths.add(scene.scenePath);
  }

  if (!ids.has(registry.defaultScene)) {
    throw new Error(`defaultScene '${registry.defaultScene}' is not registered`);
  }
  return registry;
}

export function loadSceneRegistry(registryPath = REGISTRY_PATH) {
  if (!existsSync(registryPath)) throw new Error(`scene registry is missing: ${registryPath}`);
  let parsed;
  try {
    parsed = JSON.parse(readFileSync(registryPath, 'utf8'));
  } catch (error) {
    throw new Error(`scene registry is not valid JSON: ${error.message}`);
  }
  return validateSceneRegistry(parsed);
}

export function resolveScene(registry, id = registry.defaultScene) {
  const scene = registry.scenes.find((candidate) => candidate.id === id);
  if (!scene) {
    throw new Error(`unknown Unity scene '${id}'; registered scenes: ${registry.scenes.map((s) => s.id).join(', ')}`);
  }
  return scene;
}

export function markerRegex(marker) {
  const escaped = marker.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  return new RegExp(escaped);
}

export function printSceneRegistry(registry) {
  const rows = registry.scenes.map((scene) => {
    const isDefault = scene.id === registry.defaultScene ? ' (default)' : '';
    return `${scene.id}${isDefault}: Phase ${scene.phase}, ${scene.status}, ${scene.scenePath}, ${scene.tour.shots} shots`;
  });
  return [`Unity scene registry v${registry.version}`, ...rows].join('\n');
}
