#!/usr/bin/env node

import { existsSync, mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const root = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
const unityHubRoot = '/Applications/Unity/Hub/Editor';
const configuredEditor = process.env.GM_UNITY_EDITOR;
const unityProject = process.env.GM_UNITY_PROJECT || path.join(root, 'unity-project');
// A compiler that hangs — an accidental infinite loop in ShutBoxRules.cs is enough — otherwise
// blocks this script and every npm chain above it forever, with no exit code ever returned.
const RUN_TIMEOUT_MS = 5 * 60 * 1000;

// The pinned editor, never the newest installed one. Park an upgrade candidate next to the pinned
// version in the Hub and "newest" silently validates a different Unity's C# toolchain, so this suite
// reports green for a compiler the project does not ship on. Same resolution as unity-cli.mjs.
function findEditor() {
  if (configuredEditor) return configuredEditor;
  const versionFile = path.join(unityProject, 'ProjectSettings', 'ProjectVersion.txt');
  if (!existsSync(versionFile)) {
    throw new Error(`no ProjectVersion.txt at ${versionFile}; set GM_UNITY_EDITOR to the Unity editor root.`);
  }
  const version = readFileSync(versionFile, 'utf8').match(/m_EditorVersion:\s*(\S+)/)?.[1];
  if (!version) throw new Error(`could not parse m_EditorVersion from ${versionFile}`);
  const editor = path.join(unityHubRoot, version, 'Unity.app');
  if (!existsSync(editor)) throw new Error(`project pins ${version} but ${editor} is not installed`);
  return editor;
}

function run(command, args, label) {
  const result = spawnSync(command, args, {
    cwd: root,
    encoding: 'utf8',
    maxBuffer: 20 * 1024 * 1024,
    timeout: RUN_TIMEOUT_MS,
  });
  if (result.stdout) process.stdout.write(result.stdout);
  if (result.stderr) process.stderr.write(result.stderr);
  if (result.error?.code === 'ETIMEDOUT' || result.signal) {
    throw new Error(`${label} was killed after ${RUN_TIMEOUT_MS / 1000}s (${result.signal ?? result.error.code})`);
  }
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`${label} failed with exit code ${result.status}`);
}

let buildDir;
try {
  const editor = findEditor();
  const scripting = path.join(editor, 'Contents/Resources/Scripting');
  const mono = path.join(scripting, 'MonoBleedingEdge/bin/mono');
  const compiler = path.join(scripting, 'MonoBleedingEdge/lib/mono/4.5/csc.exe');
  const nunit = path.join(
    editor,
    'Contents/Resources/PackageManager/BuiltInPackages/com.unity.ext.nunit/net472/unity-custom/nunit.framework.dll',
  );
  for (const required of [mono, compiler, nunit]) {
    if (!existsSync(required)) throw new Error(`Required Unity tool is missing: ${required}`);
  }

  buildDir = mkdtempSync(path.join(tmpdir(), 'gm-shutbox-csharp-'));
  const runtimeSource = path.join(root, 'unity/shut-the-box/Runtime/ShutBoxRules.cs');
  const nunitSource = path.join(root, 'unity/shut-the-box/Tests/ShutBoxRulesTests.cs');
  const runnerSource = path.join(root, 'unity/shut-the-box/Tests/ShutBoxParityRunner.cs');
  const runtimeDll = path.join(buildDir, 'GamesMaster.ShutTheBox.dll');
  const testsDll = path.join(buildDir, 'GamesMaster.ShutTheBox.Tests.dll');
  const runnerExe = path.join(buildDir, 'GamesMaster.ShutTheBox.Parity.exe');
  const compilerPrefix = [compiler, '-nologo', '-warnaserror+', '-langversion:latest'];

  run(mono, [...compilerPrefix, '-target:library', `-out:${runtimeDll}`, runtimeSource], 'runtime compile');
  run(
    mono,
    [
      ...compilerPrefix,
      '-target:library',
      `-out:${testsDll}`,
      `-reference:${runtimeDll}`,
      `-reference:${nunit}`,
      nunitSource,
      runnerSource,
    ],
    'NUnit compile',
  );
  run(
    mono,
    [
      ...compilerPrefix,
      '-target:exe',
      `-out:${runnerExe}`,
      `-reference:${runtimeDll}`,
      runnerSource,
    ],
    'parity runner compile',
  );
  run(mono, [runnerExe], 'C# parity suite');
} catch (error) {
  console.error(`C# Shut-the-Box tests failed: ${error.message}`);
  process.exitCode = 1;
} finally {
  if (buildDir) rmSync(buildDir, { recursive: true, force: true });
}
