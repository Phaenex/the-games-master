import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, readdirSync, statSync, existsSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

const unity = process.env.UNITY_EDITOR_PATH ??
  '/Applications/Unity/Hub/Editor/6000.5.3f1/Unity.app';
const monoRoot = join(unity, 'Contents/Resources/Scripting/MonoBleedingEdge/bin');
const mcs = join(monoRoot, 'mcs');
const mono = join(monoRoot, 'mono');
assert.ok(existsSync(mcs) && existsSync(mono), 'Unity Mono toolchain is required');

const root = mkdtempSync(join(tmpdir(), 'gm-house-process-'));
const executable = join(root, 'house-process-helper.exe');
const source = resolve('scripts/house-process-helper.cs');
const managed = join(unity, 'Contents/Resources/Scripting/Managed/UnityEngine');
const productionSources = [
  resolve('unity/scene-system/Runtime/GmHouseMemory.cs'),
  resolve('unity/scene-system/Runtime/GmParlorAdaptation.cs'),
  resolve('unity/scene-system/Runtime/GmParlorCore.cs'),
];
const compiled = spawnSync(mcs, [
  '-langversion:latest',
  '-out:' + executable,
  source,
  ...productionSources,
  '-r:' + join(managed, 'UnityEngine.CoreModule.dll'),
  '-r:' + join(managed, 'UnityEngine.JSONSerializeModule.dll'),
], { encoding: 'utf8' });
assert.equal(compiled.status, 0, compiled.stderr || compiled.stdout);

const domain = join(root, 'domain');
const lease = join(domain, '.house-memory.lease');
const ready = join(root, 'holder.ready');
const deniedMutation = join(domain, 'denied.mutation');
const acquiredMutation = join(domain, 'acquired.mutation');
const holder = spawn(mono, [executable, 'hold', lease, ready], { stdio: 'ignore' });

const deadline = Date.now() + 5000;
while (!existsSync(ready) && Date.now() < deadline)
  await new Promise(resolveWait => setTimeout(resolveWait, 10));
assert.ok(existsSync(ready), 'holder process never acquired the lease');

function snapshot(path) {
  if (!existsSync(path)) return [];
  return readdirSync(path).sort().flatMap(name => {
    const full = join(path, name);
    return statSync(full).isDirectory()
      ? snapshot(full).map(item => [name + '/' + item[0], item[1]])
      : [[name, readFileSync(full).toString('hex')]];
  });
}

const beforeDenied = snapshot(domain);
const denied = spawnSync(mono, [executable, 'mutate', lease, deniedMutation]);
assert.equal(denied.status, 73, 'second OS process unexpectedly acquired held lease');
assert.deepEqual(snapshot(domain), beforeDenied, 'denied process changed the House domain');

holder.kill('SIGKILL');
await new Promise(resolveWait => holder.once('exit', resolveWait));
assert.ok(existsSync(lease), 'SIGKILL test improperly deleted the persistent lock file');
const acquired = spawnSync(mono, [executable, 'mutate', lease, acquiredMutation]);
assert.equal(acquired.status, 0, 'lease was not released by OS after SIGKILL');
assert.equal(readFileSync(acquiredMutation, 'utf8'), 'acquired');
console.log('✓ House lease: real second process denied byte-identically, then acquired after SIGKILL');

function runHelper(args, options = {}) {
  const result = spawnSync(mono, [executable, ...args], {
    encoding: 'utf8',
    ...options,
  });
  assert.equal(result.status, 0,
    `helper ${args[0]} failed (${result.status}): ${result.stderr || result.stdout}`);
  return result.stdout.trim();
}

async function killAtDurableArtifact(domainPath, command, point, runId = '') {
  const marker = join(root, `${command}-${point}-${Date.now()}.ready`);
  const child = spawn(mono,
    [executable, command, domainPath, runId, point, marker], { stdio: 'ignore' });
  const readyDeadline = Date.now() + 5000;
  while (!existsSync(marker) && Date.now() < readyDeadline)
    await new Promise(resolveWait => setTimeout(resolveWait, 10));
  assert.ok(existsSync(marker), `${command}/${point} never reached the real durability edge`);
  assert.equal(readFileSync(marker, 'utf8'), `${command}:${point}:ReopenValidation`);
  child.kill('SIGKILL');
  await new Promise(resolveWait => child.once('exit', resolveWait));
}

function inspect(domainPath, runId = '') {
  const fields = runHelper(['inspect', domainPath, runId]).split('\t');
  assert.equal(fields.length, 8, `invalid inspection: ${fields.join('|')}`);
  return {
    rootGeneration: Number(fields[0]),
    nextOrdinal: Number(fields[1]),
    allocations: Number(fields[2]),
    profileGeneration: Number(fields[3]),
    receipts: Number(fields[4]),
    distinctReceipts: Number(fields[5]),
    runGeneration: Number(fields[6]),
    runStage: fields[7],
  };
}

for (const point of ['RootGeneration', 'RootCommitRecord']) {
  const transactionDomain = join(root, `root-${point}`);
  const firstRunId = runHelper(['bootstrap', transactionDomain]);
  await killAtDurableArtifact(transactionDomain, 'root-allocate-pause', point);

  const afterKill = inspect(transactionDomain, firstRunId);
  assert.ok(
    (afterKill.allocations === 1 && afterKill.nextOrdinal === 2) ||
    (afterKill.allocations === 2 && afterKill.nextOrdinal === 3),
    `${point} exposed a hybrid root allocation`);
  assert.equal(afterKill.receipts, 0);
  runHelper(['allocate', transactionDomain]);
  const afterRetry = inspect(transactionDomain, firstRunId);
  assert.equal(afterRetry.nextOrdinal, afterRetry.allocations + 1,
    `${point} reused or skipped a committed ordinal`);
}
console.log('✓ House root transaction: generation/commit SIGKILL reopens as coherent old or new state');

const terminalWindows = [
  ['prepare', 'prepare-pause', ['RunGeneration', 'RunCommitRecord']],
  ['profile', 'profile-pause', ['ProfileGeneration', 'ProfileCommitRecord']],
  ['ack', 'ack-pause', ['RunGeneration', 'RunCommitRecord']],
];

for (const [phase, command, points] of terminalWindows) {
  for (const point of points) {
    const transactionDomain = join(root, `${phase}-${point}`);
    const runId = runHelper(['bootstrap', transactionDomain]);
    if (phase === 'profile' || phase === 'ack')
      runHelper(['prepare', transactionDomain, runId]);
    if (phase === 'ack')
      runHelper(['apply', transactionDomain, runId]);

    await killAtDurableArtifact(transactionDomain, command, point, runId);
    const afterKill = inspect(transactionDomain, runId);
    assert.equal(afterKill.receipts, afterKill.distinctReceipts,
      `${phase}/${point} exposed duplicate receipt identities`);
    if (phase === 'prepare') {
      assert.ok(['Active', 'Prepared'].includes(afterKill.runStage),
        `${phase}/${point} exposed hybrid stage ${afterKill.runStage}`);
      assert.equal(afterKill.receipts, 0);
    } else if (phase === 'profile') {
      assert.equal(afterKill.runStage, 'Prepared');
      assert.ok(afterKill.receipts === 0 || afterKill.receipts === 1,
        `${phase}/${point} exposed hybrid receipt count`);
    } else {
      assert.ok(['Prepared', 'Acknowledged'].includes(afterKill.runStage),
        `${phase}/${point} exposed hybrid stage ${afterKill.runStage}`);
      assert.equal(afterKill.receipts, 1);
    }

    runHelper(['complete', transactionDomain, runId]);
    const recovered = inspect(transactionDomain, runId);
    assert.equal(recovered.runStage, 'Acknowledged', `${phase}/${point} did not recover`);
    assert.equal(recovered.receipts, 1, `${phase}/${point} did not apply exactly once`);
    assert.equal(recovered.distinctReceipts, 1, `${phase}/${point} duplicated receipt identity`);
    assert.equal(recovered.profileGeneration, 2,
      `${phase}/${point} advanced the profile more than once`);
  }
}
console.log('✓ House terminal transaction: real prepare/profile/ack processes survive SIGKILL exactly once');
