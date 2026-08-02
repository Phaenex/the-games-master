#!/usr/bin/env node

import { existsSync, mkdtempSync, readdirSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const root = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
const unityHubRoot = '/Applications/Unity/Hub/Editor';
const configuredEditor = process.env.GM_UNITY_EDITOR;

function findEditor() {
  if (configuredEditor) return configuredEditor;
  if (!existsSync(unityHubRoot)) {
    throw new Error('Unity Hub editor directory is missing; set GM_UNITY_EDITOR to the Unity editor root.');
  }

  const versions = readdirSync(unityHubRoot, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
    .sort((a, b) => b.localeCompare(a, undefined, { numeric: true }));
  if (!versions.length) throw new Error('No Unity editor installation was found.');
  return path.join(unityHubRoot, versions[0], 'Unity.app');
}

function run(command, args, label) {
  const result = spawnSync(command, args, { cwd: root, encoding: 'utf8' });
  if (result.stdout) process.stdout.write(result.stdout);
  if (result.stderr) process.stderr.write(result.stderr);
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
