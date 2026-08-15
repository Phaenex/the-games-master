#!/usr/bin/env node
// Drives the Unity project at ./unity-project (override with GM_UNITY_PROJECT) from the terminal so
// the editor-menu steps (Setup HDRP Pipeline, Rebuild Wend Hill) don't need a human clicking them.
//
//   node scripts/unity-cli.mjs pipeline    -> GmPipelineSetup.Apply      (batchmode)
//   node scripts/unity-cli.mjs rebuild [id] -> registered scene builder  (batchmode)
//   node scripts/unity-cli.mjs audit [id]   -> registered scene audit    (batchmode)
//   node scripts/unity-cli.mjs report      -> Wend Hill structured audit (batchmode)
//   node scripts/unity-cli.mjs pacing      -> Wend Hill cadence evidence (batchmode)
//   node scripts/unity-cli.mjs setup       -> pipeline then rebuild
//   node scripts/unity-cli.mjs tour [id]    -> registered review tour    (GUI, opens a window)
//   node scripts/unity-cli.mjs build-mac    -> standalone macOS review app (batchmode)
//   node scripts/unity-cli.mjs standalone-proof -> seven player-backbuffer frames from built app
//   node scripts/unity-cli.mjs scenes       -> list registry (no Unity launch)
//
// HDRP will not render in batchmode on this project (GmProbe proved it: white frames), so the
// tour deliberately launches a windowed editor. Everything else stays headless.

import { spawn, spawnSync, execFileSync } from 'node:child_process';
import { existsSync, readFileSync, writeFileSync, mkdirSync, statSync, readdirSync, rmSync, copyFileSync } from 'node:fs';
import { loadavg, cpus, freemem } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  loadSceneRegistry,
  markerRegex,
  printSceneRegistry,
  resolveScene,
} from './unity-scene-registry.mjs';
import { assertHostCapacity, currentHostCapacity, MAX_LOAD_PER_CORE } from './unity-host-health.mjs';
import { findRuntimeIntegrityDefects } from './unity-runtime-integrity.mjs';
const REPO_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const PROJECT = process.env.GM_UNITY_PROJECT || path.join(REPO_ROOT, 'unity-project');
const UNITY_ENV = { ...process.env, GM_REPO_ROOT: REPO_ROOT, GM_UNITY_PROJECT: PROJECT };
const LOG_DIR = path.join(PROJECT, 'Logs');
const TIMEOUT_MS = 20 * 60 * 1000;
const IDLE_MS = 150 * 1000;   // no log growth / no artifact for this long after boot => likely a dialog
const REGISTRY = loadSceneRegistry();
const requested = process.argv[2];
const requestedSceneId = process.argv[3] ?? REGISTRY.defaultScene;
let SCENE;
try {
  SCENE = resolveScene(REGISTRY, requestedSceneId);
} catch (error) {
  console.error(`✗ ${error.message}`);
  process.exit(2);
}

/** Unity refuses to open a project with a different patch version, so read the pinned one. */
function resolveEditor() {
  const versionFile = path.join(PROJECT, 'ProjectSettings', 'ProjectVersion.txt');
  if (!existsSync(versionFile)) throw new Error(`no ProjectVersion.txt at ${versionFile}`);
  const match = readFileSync(versionFile, 'utf8').match(/m_EditorVersion:\s*(\S+)/);
  if (!match) throw new Error('could not parse m_EditorVersion');
  const version = match[1];
  const binary = `/Applications/Unity/Hub/Editor/${version}/Unity.app/Contents/MacOS/Unity`;
  if (!existsSync(binary)) throw new Error(`project pins ${version} but ${binary} is not installed`);
  return { version, binary };
}

/**
 * Unity holds an exclusive lock; running over an open editor corrupts the Library. The lockfile
 * alone isn't proof though — a killed editor leaves one behind, and other Unity projects running
 * concurrently are irrelevant. Confirm against the live process list before believing it.
 */
function projectEditorIsRunning() {
  const procs = execFileSync('ps', ['-Ao', 'args='], { encoding: 'utf8' });
  return procs.split('\n').some((line) => {
    if (!/Unity\.app\/Contents\/MacOS\/Unity/.test(line)) return false;
    if (!line.toLowerCase().includes(PROJECT.toLowerCase())) return false;
    // Unity's own helpers run the SAME binary with the SAME -projectPath and outlive the editor by
    // design. AssetImportWorker in particular lingers for ages at 0% CPU. Counting them as "the
    // editor is open" makes this refuse to run against a project nobody has open.
    return !/-adb2|AssetImportWorker|UnityShaderCompiler|UnityPackageManager|-name\s+AssetImport/.test(line);
  });
}

function assertEditorClosed() {
  const lock = path.join(PROJECT, 'Temp', 'UnityLockfile');
  const held = projectEditorIsRunning();

  if (held) {
    throw new Error(`Unity has ${PROJECT} open. Close the editor first.`);
  }
  if (!existsSync(lock)) return;
  console.log('  ! stale UnityLockfile (no process holds this project) — clearing it');
  rmSync(lock, { force: true });
}

async function waitForEditorRelease(timeoutMs = 30_000) {
  const deadline = Date.now() + timeoutMs;
  while (projectEditorIsRunning()) {
    if (Date.now() >= deadline) {
      throw new Error(`Unity did not release ${PROJECT} within ${timeoutMs / 1000}s after the stalled attempt was terminated`);
    }
    await new Promise((resolve) => setTimeout(resolve, 500));
  }
  assertEditorClosed();
}

const TOUR_SCENE = SCENE.sceneName;
const TEST_RESULTS = path.join(LOG_DIR, 'editmode-results.xml');
const PLAYTEST_RESULTS = path.join(LOG_DIR, 'playmode-results.xml');
const MAC_APP = SCENE.standalone ? path.join(PROJECT, SCENE.standalone.appPath) : null;
const MAC_BINARY = SCENE.standalone ? path.join(MAC_APP, SCENE.standalone.executablePath) : null;
const ASSET_INDEX = path.join(PROJECT, 'Library', 'GmSceneIntelligence', 'asset-index.json');
const VARIANT_ROOT = path.join(PROJECT, 'Library', 'GmSceneIntelligence', 'variants', 'wend-hill');
const STANDALONE_PROOF_ROOT = path.join(PROJECT, 'Library', 'GmSceneIntelligence', 'standalone-proof', SCENE.id);
const AUDIT_REPORT = path.join(PROJECT, 'Library', 'GmSceneIntelligence', 'audits', 'wend-hill-audit.json');
const PACING_REPORT = path.join(PROJECT, 'Library', 'GmSceneIntelligence', 'pacing', 'wend-hill-simulation.json');

const TASKS = {
  pipeline: { method: 'GmPipelineSetup.Apply', gui: false, done: /\[GmPipelineSetup\] HDRP assigned/ },
  rebuild: {
    method: SCENE.build.method,
    gui: false,
    sceneScoped: true,
    done: markerRegex(SCENE.build.success),
  },
  audit: {
    method: SCENE.audit.method,
    gui: false,
    sceneScoped: true,
    done: markerRegex(SCENE.audit.success),
  },
  'audit-saved': {
    method: 'GmEstateQualityAudit.RunSavedScene',
    gui: false,
    sceneScoped: true,
    done: /\[GmEstateAudit\] SAVED PASS:/,
  },
  test: {
    gui: false,
    // -runTests exits on its own; pairing it with -quit truncates the run before results are
    // written. EditMode tests never render, so unlike the tour this stays headless.
    args: ['-runTests', '-testPlatform', 'EditMode', '-testResults', TEST_RESULTS],
    quit: false,
    ownsVerdict: true,
    done: /\[GmTest\] never-matches/,   // completion is the results file, not a log line
    artifacts: () => (existsSync(TEST_RESULTS) ? [{ name: 'results.xml', bytes: statSync(TEST_RESULTS).size }] : []),
    clean: () => rmSync(TEST_RESULTS, { force: true }),
    expected: 1,
  },
  playtest: {
    gui: false,
    args: ['-runTests', '-testPlatform', 'PlayMode', '-testResults', PLAYTEST_RESULTS],
    quit: false,
    ownsVerdict: true,
    done: /\[GmTest\] never-matches/,
    artifacts: () => (existsSync(PLAYTEST_RESULTS) ? [{ name: 'playmode-results.xml', bytes: statSync(PLAYTEST_RESULTS).size }] : []),
    clean: () => rmSync(PLAYTEST_RESULTS, { force: true }),
    expected: 1,
  },
  'build-mac': {
    method: SCENE.standalone?.method,
    gui: false,
    sceneScoped: true,
    // Player builds need the graphics device available while shaders and HDRP data are compiled.
    useGraphics: true,
    done: SCENE.standalone ? markerRegex(SCENE.standalone.success) : /never-matches/,
    artifacts: () => (MAC_BINARY && existsSync(MAC_BINARY) ? [{ name: path.basename(MAC_APP), bytes: statSync(MAC_BINARY).size }] : []),
    clean: () => { if (MAC_APP) rmSync(MAC_APP, { recursive: true, force: true }); },
    expected: 1,
  },
  'asset-index': {
    method: 'GmAssetIntelligence.ReindexMenu',
    gui: false,
    useGraphics: true,
    done: /\[GmAssetIntelligence\] PASS/,
    artifacts: () => (existsSync(ASSET_INDEX) ? [{ name: 'asset-index.json', bytes: statSync(ASSET_INDEX).size }] : []),
    clean: () => rmSync(ASSET_INDEX, { force: true }),
    expected: 1,
  },
  report: {
    method: 'GmSceneReportWriter.WriteWendHillReport',
    gui: false,
    done: /\[GmSceneReport\] WEND HILL PASS/,
    artifacts: () => existsSync(AUDIT_REPORT) ? [{ name: 'wend-hill-audit.json', bytes: statSync(AUDIT_REPORT).size }] : [],
    clean: () => rmSync(AUDIT_REPORT, { force: true }),
    expected: 1,
  },
  pacing: {
    method: 'GmPacingSimulator.WriteWendHillReport',
    gui: false,
    done: /\[GmPacingSimulator\] WEND HILL PASS/,
    artifacts: () => existsSync(PACING_REPORT) ? [{ name: 'wend-hill-simulation.json', bytes: statSync(PACING_REPORT).size }] : [],
    clean: () => rmSync(PACING_REPORT, { force: true }),
    expected: 1,
  },
  variants: {
    method: 'GmGuardedVariantPreview.GenerateWendHillBatch',
    gui: false,
    useGraphics: true,
    done: /\[GmGuardedVariant\] PASS previews=\d+ selected=none/,
    artifacts: () => {
      if (!existsSync(VARIANT_ROOT)) return [];
      return readdirSync(VARIANT_ROOT, { recursive: true, withFileTypes: true })
        .filter((entry) => entry.isFile() && entry.name.endsWith('.png'))
        .map((entry) => ({ name: entry.name, bytes: statSync(path.join(entry.parentPath, entry.name)).size }));
    },
    clean: () => rmSync(VARIANT_ROOT, { recursive: true, force: true }),
    expected: 0,
  },
  tour: {
    method: SCENE.tour.method,
    gui: true,
    sceneScoped: true,
    // The first real GPU frame after a material/shader change can block while Unity imports HDRP
    // variants without growing the editor log. Tests/rebuilds keep the tighter 150s modal guard.
    idleMs: 300 * 1000,
    done: markerRegex(SCENE.tour.success),
    // Unity flushes its log lazily, so TOUR COMPLETE can show up minutes after the capture really
    // happened. Watch for the PNGs landing instead and treat the log as the slow confirmation.
    artifacts: () => shotsIn(TOUR_SCENE),
    // Watching for artifacts means a previous run's PNGs would satisfy the poll instantly, so the
    // output directory has to be empty before Unity starts. Same trap as the stale log file.
    clean: () => {
      rmSync(path.join(PROJECT, 'Screens', TOUR_SCENE), { recursive: true, force: true });
      // A previously killed windowed editor can leave a recovery scene. Unity may restore that
      // backup before the explicitly opened WendHill scene and then spend the tour serializing a
      // stale dirty clone. Generated Temp backups are safe to discard before an automated run.
      rmSync(path.join(PROJECT, 'Temp', '__Backupscenes'), { recursive: true, force: true });
    },
    expected: SCENE.tour.shots,
  },
  'diagnose-stall': {
    method: 'GmWendStallDiagnostic.RunMenu',
    gui: false,
    sceneScoped: true,
    done: /\[GmWendStallDiag\] DIAGNOSE COMPLETE/,
  },
};

function shotsIn(scene) {
  const dir = path.join(PROJECT, 'Screens', scene);
  if (!existsSync(dir)) return [];
  return readdirSync(dir)
    .filter((f) => f.endsWith('.png'))
    .map((f) => ({ name: f, bytes: statSync(path.join(dir, f)).size }));
}

/**
 * The "rebuild the Library?" / API-updater / assembly-updater modal dialogs only ever appear in a
 * WINDOWED editor. Batchmode suppresses every dialog and rebuilds the Library non-interactively, so
 * a headless open-and-quit pass forces the Library fully consistent BEFORE the windowed tour opens
 * — after which the window has nothing to prompt about. This is the fix for a tour hanging on a
 * "rebuild lib?" popup: eliminate the condition rather than try to click the dialog.
 */
/**
 * Stamps machine state onto a performance report and says out loud whether the timing can be
 * trusted. A p95 measured on a loaded, swapping box is not evidence about the game: on 2026-08-03
 * the same route measured 8.71ms and 17.24ms with no meaningful code difference between the runs,
 * and two separate investigations were spent arguing about which number was real. Record the
 * conditions with the measurement so that argument is never had again.
 */
/**
 * The conditions a timing was taken under, rendered next to the timing itself.
 *
 * A p95 quoted bare is unfalsifiable. On 2026-08-15 gate 8 read 19.25ms and answering "is that a
 * regression?" meant excavating a historical range from a tracker doc -- during which the obvious
 * suspect (216,820 triangles of new mansion collision) looked guilty and was innocent. Numbers that
 * travel without their conditions get a cause supplied by the reader, and the supplied cause is
 * usually wrong. This makes that impossible to do accidentally.
 */
function describeConditions(perf) {
  const parts = [];
  if (Number.isFinite(perf.processorCount)) parts.push(`${perf.processorCount} cores`);
  if (Number.isFinite(perf.systemMemoryMegabytes)) parts.push(`${perf.systemMemoryMegabytes}MB RAM`);
  // Written by stampHostState, which runs before this -- host-side conditions Unity cannot know.
  if (Number.isFinite(perf.hostOneMinuteLoad)) parts.push(`load ${perf.hostOneMinuteLoad.toFixed(2)}`);
  if (Number.isFinite(perf.hostLoadPerCore)) {
    // Above 1.0/core the harness already warns the number is indicative; say so where it is quoted.
    parts.push(`${perf.hostLoadPerCore.toFixed(2)}/core${perf.hostLoadPerCore > 1 ? ' ⚠loaded' : ''}`);
  }
  if (perf.graphicsDevice) parts.push(perf.graphicsDevice);
  if (perf.batchMode === true) parts.push('BATCHMODE');
  return parts.length ? `[${parts.join(' · ')}]` : '[conditions unrecorded]';
}

function stampHostState(reportPath) {
  if (!existsSync(reportPath)) return null;
  const report = JSON.parse(readFileSync(reportPath, 'utf8'));
  const cores = cpus().length;
  const load = loadavg()[0];
  const perCore = cores > 0 ? load / cores : null;
  report.hostOneMinuteLoad = Number(load.toFixed(2));
  report.hostCores = cores;
  report.hostLoadPerCore = perCore == null ? null : Number(perCore.toFixed(2));
  report.hostFreeMemoryMB = Math.round(freemem() / (1024 * 1024));
  writeFileSync(reportPath, `${JSON.stringify(report, null, 4)}\n`);
  const trustworthy = perCore != null && perCore <= 1.0;
  console.log(`  host at measurement: ${load.toFixed(1)} load / ${cores} cores = ` +
    `${perCore?.toFixed(2)} per core, ${report.hostFreeMemoryMB}MB free` +
    (trustworthy ? '' : '  ⚠ timing measured on a loaded host — treat p95 as indicative, not a verdict'));
  return report;
}

function warmLibrary(binary, suffix = '', stem = 'cli-warm') {
  const warmLog = path.join(LOG_DIR, `${stem}${suffix}.log`);
  rmSync(warmLog, { force: true });
  console.log('  ⋯ warming Library headless (prevents the windowed rebuild-library dialog)');
  const r = spawnSync(
    binary,
    ['-batchmode', '-nographics', '-quit', '-accept-apiupdate', '-projectPath', PROJECT, '-logFile', warmLog],
    { stdio: 'ignore', timeout: TIMEOUT_MS, env: UNITY_ENV }
  );
  const log = existsSync(warmLog) ? readFileSync(warmLog, 'utf8') : '';
  if (!/Exiting batchmode successfully/.test(log)) {
    // Not fatal — the tour may still work — but the operator should know the Library wasn't cleanly
    // warmed, which is exactly when a dialog can still slip through.
    console.warn(`  ⚠ Library warm pass did not report clean exit (code ${r.status}); the tour may prompt`);
  }
}

class UnityRunError extends Error {
  constructor(message, { kind = 'run-failure', artifacts = 0, logFile = null } = {}) {
    super(message);
    this.name = 'UnityRunError';
    this.kind = kind;
    this.artifacts = artifacts;
    this.logFile = logFile;
  }
}

function isRetryableGuiFailure(error) {
  return error instanceof UnityRunError &&
    error.kind === 'gui-idle-stall' &&
    error.artifacts === 0;
}

function taskLogStem(name) {
  const task = TASKS[name];
  if (task.sceneScoped && SCENE.id !== REGISTRY.defaultScene) return `cli-${SCENE.id}-${name}`;
  return `cli-${name}`;
}

function warmLogStem(name) {
  const task = TASKS[name];
  if (task.sceneScoped && SCENE.id !== REGISTRY.defaultScene) return `cli-${SCENE.id}-warm`;
  return 'cli-warm';
}

function run(name, attempt = 1, attempts = 1) {
  const task = TASKS[name];
  assertHostCapacity();
  const { binary, version } = resolveEditor();
  assertEditorClosed();
  mkdirSync(LOG_DIR, { recursive: true });
  const numberedAttempt = task.gui && attempts > 1;
  const attemptSuffix = numberedAttempt ? `-attempt-${attempt}` : '';
  const logFile = path.join(LOG_DIR, `${taskLogStem(name)}${attemptSuffix}.log`);
  // Unity only truncates this once it boots, which takes far longer than the first poll tick.
  // Leaving the previous run's log in place makes the poller match ITS completion marker and kill
  // the new editor a second after spawn.
  rmSync(logFile, { force: true });
  task.clean?.();

  // Windowed tasks get a headless Library warm first so the window can't hit a rebuild dialog.
  if (task.gui) warmLibrary(binary, attemptSuffix, warmLogStem(name));

  const args = ['-projectPath', PROJECT, '-logFile', logFile, '-accept-apiupdate'];
  args.push(...(task.args ?? ['-executeMethod', task.method]));
  const extraArgs = process.argv.slice(3).filter((a) => a !== SCENE.id);
  if (extraArgs.length > 0) args.push(...extraArgs);
  // Batchmode normally quits itself via -quit. The GUI tour drops out of play mode on its own but
  // leaves the editor up, so we watch the log and close it once the capture lands.
  if (!task.gui) args.unshift('-batchmode', ...(task.useGraphics ? [] : ['-nographics']), ...(task.quit === false ? [] : ['-quit']));

  const scope = task.sceneScoped ? `${SCENE.id}/` : '';
  console.log(`\n▶ ${scope}${name}: ${task.method ?? task.args.join(' ')}  (Unity ${version}${task.gui ? ', windowed' : ', batchmode'})`);
  console.log(`  log: ${logFile}`);

  return new Promise((resolve, reject) => {
    const child = spawn(binary, args, { stdio: 'ignore', env: UNITY_ENV });
    let settled = false;
    let lastSize = 0;
    let lastProgressAt = Date.now();   // last time the log grew OR an artifact appeared
    let artifactsSeen = 0;
    let artifactsComplete = false;
    let doneSeen = false;

    const finish = (fn, arg) => {
      if (settled) return;
      settled = true;
      clearInterval(poll);
      clearTimeout(deadline);
      fn(arg);
    };

    // A blocked editor (modal dialog, licence prompt) sits at ~0% CPU forever. Without this the
    // script waits with it indefinitely.
    const deadline = setTimeout(() => {
      child.kill('SIGTERM');
      finish(reject, new UnityRunError(
        `${name}: no completion after ${TIMEOUT_MS / 60000}min — Unity may be blocked on a dialog.\n  log: ${logFile}`,
        { kind: 'deadline', artifacts: artifactsSeen, logFile }));
    }, TIMEOUT_MS);

    const poll = setInterval(() => {
      // The artifacts are the real signal. Unity's log can lag the work by minutes, which once
      // left this script watching a finished tour for ten more minutes.
      const nArtifacts = task.artifacts ? task.artifacts().length : 0;
      if (nArtifacts > artifactsSeen) { artifactsSeen = nArtifacts; lastProgressAt = Date.now(); }
      if (task.artifacts && nArtifacts >= task.expected) {
        if (!artifactsComplete) console.log(`  ✓ ${name}: ${task.expected} artifacts on disk`);
        artifactsComplete = true;
        // GmShotTour exits the editor cleanly after its final frame. Killing the process the instant
        // the PNG appears leaves recovery scenes and makes the next tour restore stale state.
        if (!task.gui) return finish(resolve, logFile);
      }

      // Idle-hang backstop, faster than the 20-min deadline: if the log has grown at least once
      // (Unity booted) but nothing — log growth OR a new artifact — has happened for IDLE_MS, it is
      // almost certainly parked on a modal dialog. Kill and say so, rather than waiting out the hour.
      const idleMs = task.idleMs ?? IDLE_MS;
      if (lastSize > 0 && Date.now() - lastProgressAt > idleMs) {
        child.kill('SIGTERM');
        return finish(reject, new UnityRunError(
          `${name}: no progress for ${Math.round(idleMs / 1000)}s after boot — Unity is likely parked on a modal ` +
          `dialog (rebuild-Library / API-update / recovery). Warm pass should prevent this; check ${logFile}.`,
          { kind: 'gui-idle-stall', artifacts: artifactsSeen, logFile }));
      }

      if (!existsSync(logFile)) return;
      const size = statSync(logFile).size;
      if (size === lastSize) return;
      lastSize = size;
      lastProgressAt = Date.now();
      const log = readFileSync(logFile, 'utf8');

      // Match only real aborts. Don't pattern-match loose words like "missing" — the builders
      // print benign tallies ("15 placed, 0 missing") that would read as failures.
      //
      // A TEST run is different in kind and must not use the [Gm*] FAILED half of this. Tests
      // deliberately drive failure paths -- GmBootMenuTests proves a corrupt save is REFUSED, and
      // the refusal logs "[GmBoot] FAILED: ..." because a player losing a run deserves a reason in
      // the log. Scanning for that killed the run mid-flight and reported the passing test's own
      // evidence as the failure. For tests, NUnit's XML is the verdict (verifyTests reads it and
      // already treats zero tests as a failure); compile errors and batchmode aborts still abort
      // early here, because those produce no XML to read at all.
      const abortOnly = /^.*(?:Compilation failed|error CS\d+|Aborting batchmode due to failure|^\w*Exception: ).*$/m;
      const anyReportedFailure = /^.*(?:\[Gm\w+\][^\r\n]*\bFAILED\b|Compilation failed|error CS\d+|Aborting batchmode due to failure|^\w*Exception: ).*$/m;
      const failure = log.match(task.ownsVerdict ? abortOnly : anyReportedFailure);
      if (failure) {
        child.kill('SIGTERM');
        return finish(reject, new UnityRunError(
          `${name} failed: ${failure[0].trim()}\n  full log: ${logFile}`,
          { kind: 'reported-failure', artifacts: artifactsSeen, logFile }));
      }

      if (task.done.test(log)) {
        if (task.gui) {
          if (!doneSeen) console.log(`  ✓ ${name} reported complete; waiting for clean editor exit`);
          doneSeen = true;
          return;
        }
        // batchmode: let -quit exit on its own, the exit handler confirms
      }
    }, 1000);

    child.on('error', (err) => finish(reject, new UnityRunError(
      `${name}: failed to launch Unity: ${err.message}`,
      { kind: 'spawn-error', artifacts: artifactsSeen, logFile })));
    child.on('exit', (code) => {
      const log = existsSync(logFile) ? readFileSync(logFile, 'utf8') : '';
      if (task.done.test(log) || (task.gui && artifactsComplete)) return finish(resolve, logFile);
      // Unity exits non-zero when tests fail, and -runTests writes no completion line. A results
      // file means the run happened; whether it passed is verifyTests' call, not the exit code's.
      if (task.artifacts?.().length >= task.expected) return finish(resolve, logFile);
      finish(reject, new UnityRunError(
        `${name}: Unity exited ${code} without reporting success.\n  log: ${logFile}`,
        { kind: 'unexpected-exit', artifacts: task.artifacts?.().length ?? 0, logFile }));
    });
  });
}

/**
 * A tour that logs TOUR COMPLETE but writes nothing is a failure, and an earlier version of this
 * script happily exited 0 on zero PNGs. Refuse to pass on empty output, on files too small to be a
 * real 1920x1080 render, or on frames the renderer blanked.
 *
 * The luminance check is the one that matters: HDRP renders pure white in -batchmode, and a broken
 * volume profile pins every frame to the same value regardless of scene lighting. Both look like
 * success from the log alone. GmShotTour reports meanLum per shot precisely so this can be judged
 * mechanically rather than by asking a human to eyeball a number.
 */
export function verifyShots(scene, log, logTag, expectedShots = null) {
  const shots = shotsIn(scene).sort((a, b) => a.name.localeCompare(b.name));
  console.log(`\n  Screens/${scene}: ${shots.length} png`);
  if (shots.length === 0) {
    throw new Error(`tour wrote 0 screenshots to Screens/${scene} — capture path is broken`);
  }
  if (expectedShots !== null && shots.length !== expectedShots)
    throw new Error(`tour wrote ${shots.length}/${expectedShots} screenshots`);

  const metrics = new Map();
  const escapedTag = logTag.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const evidencePattern = new RegExp(
    `\\[${escapedTag}\\] (\\S+) meanLum=(\\d+) p05=(\\d+) p95=(\\d+) black=([\\d.]+)\\s*% white=([\\d.]+)\\s*%`, 'g');
  for (const m of log.matchAll(evidencePattern)) metrics.set(m[1], {
    mean: Number(m[2]), p05: Number(m[3]), p95: Number(m[4]), black: Number(m[5]), white: Number(m[6]),
  });

  for (const s of shots) {
    const name = s.name.replace(/^tour-|\.png$/g, '');
    const evidence = metrics.get(name);
    if (!evidence) throw new Error(`${s.name} has no complete luminance/clipping evidence in the tour log`);
    if (s.bytes < 10_000) throw new Error(`${s.name} is only ${s.bytes} bytes and may be blank`);
    if (evidence.p95 - evidence.p05 < 6 || evidence.black > 97 || evidence.white > 25)
      throw new Error(`${s.name} is invalid visual evidence: ${JSON.stringify(evidence)}`);
    console.log(`    ${s.name}  ${(s.bytes / 1024).toFixed(0)}KB  mean=${evidence.mean} ` +
      `p05=${evidence.p05} p95=${evidence.p95} black=${evidence.black}% white=${evidence.white}%`);
  }

  if (metrics.size !== shots.length)
    throw new Error(`tour logged ${metrics.size} evidence row(s) for ${shots.length} screenshots`);
  return shots;
}

async function runStandaloneProof() {
  // Same pre-flight the editor tasks get. A p95 measured against a budget on a loaded box is not
  // evidence about the game, and finding that out after the run costs the whole run.
  assertHostCapacity();
  if (!SCENE.standalone) throw new Error(`scene '${SCENE.id}' has no standalone configuration`);
  if (!existsSync(MAC_BINARY)) throw new Error(`standalone app is missing; run build-mac first: ${MAC_APP}`);
  const managed = path.join(MAC_APP, 'Contents', 'Resources', 'Data', 'Managed');
  const editorAssemblies = existsSync(managed)
    ? readdirSync(managed).filter((name) => /^UnityEditor(?:\.|-)/i.test(name)) : [];
  if (editorAssemblies.length) throw new Error(`player contains editor assembly: ${editorAssemblies.join(', ')}`);
  rmSync(STANDALONE_PROOF_ROOT, { recursive: true, force: true });
  mkdirSync(STANDALONE_PROOF_ROOT, { recursive: true });
  const logFile = path.join(STANDALONE_PROOF_ROOT, 'player.log');
  const budget = SCENE.performance ?? { width: 1280, height: 720, vSync: 1, p95Milliseconds: 33.4 };
  const args = [
    '-screen-fullscreen', '0', '-screen-width', String(budget.width), '-screen-height', String(budget.height),
    '-gmReviewCaptureDir', STANDALONE_PROOF_ROOT, '-gmReviewAutoExit',
    '-gmReviewWarmupSeconds', String(budget.warmupSeconds ?? 10), '-logFile', logFile,
  ];
  if (budget.vSync === 0) args.push('-gmReviewNoVSync');
  console.log(`\n▶ ${SCENE.id}/standalone-proof: seven player-backbuffer frames`);
  console.log(`  app: ${MAC_APP}`);
  console.log(`  output: ${STANDALONE_PROOF_ROOT}`);
  const exitCode = await new Promise((resolve, reject) => {
    const child = spawn(MAC_BINARY, args, { stdio: 'ignore' });
    const deadline = setTimeout(() => {
      child.kill('SIGTERM');
      reject(new Error('standalone proof exceeded 120 seconds'));
    }, 120_000);
    child.on('error', (error) => { clearTimeout(deadline); reject(error); });
    child.on('exit', (code) => { clearTimeout(deadline); resolve(code); });
  });
  const log = existsSync(logFile) ? readFileSync(logFile, 'utf8') : '';
  const shots = existsSync(STANDALONE_PROOF_ROOT) ? readdirSync(STANDALONE_PROOF_ROOT)
    .filter((name) => /^0[1-7]-.*\.png$/.test(name)).sort() : [];
  const controllerShots = existsSync(STANDALONE_PROOF_ROOT) ? readdirSync(STANDALONE_PROOF_ROOT)
    .filter((name) => /^controller-0[1-2]-.*\.png$/.test(name)).sort() : [];
  if (exitCode !== 0) throw new Error(`standalone proof exited ${exitCode}; inspect ${logFile}`);
  const renderIntegrityWarnings = findRuntimeIntegrityDefects(log);
  if (renderIntegrityWarnings.length)
    throw new Error(`standalone proof logged ${renderIntegrityWarnings.length} render-integrity warning(s): ${renderIntegrityWarnings.join(' | ')}`);
  if (!/\[GmStandaloneProbe\] PASS: 7\/7 player-backbuffer frames, performance sampled, zero runtime errors or render-integrity warnings/.test(log))
    throw new Error(`standalone proof did not report PASS; inspect ${logFile}`);
  if (!/\[GmStandaloneProbe\] AUDIO PASS: 5\/5 final Ninth Bell clips loaded and decoded/.test(log))
    throw new Error(`standalone proof did not decode all five final Ninth Bell clips; inspect ${logFile}`);
  if (!/\[GmStandaloneProbe\] CONTROLLER PASS: cold-open, move=.* interact, wind, brightness, pause\/resume, controller UI/.test(log))
    throw new Error(`standalone proof did not exercise the complete virtual-controller path; inspect ${logFile}`);
  if (shots.length !== 7) throw new Error(`standalone proof wrote ${shots.length}/7 PNGs`);
  if (controllerShots.length !== 2)
    throw new Error(`standalone proof wrote ${controllerShots.length}/2 controller UI PNGs`);
  for (const name of shots) {
    const bytes = statSync(path.join(STANDALONE_PROOF_ROOT, name)).size;
    if (bytes < 50_000) throw new Error(`${name} is only ${bytes} bytes and may be blank`);
    console.log(`    ${name}  ${(bytes / 1024).toFixed(0)}KB`);
  }
  for (const name of controllerShots) {
    const bytes = statSync(path.join(STANDALONE_PROOF_ROOT, name)).size;
    if (bytes < 50_000) throw new Error(`${name} is only ${bytes} bytes and may be blank`);
    console.log(`    ${name}  ${(bytes / 1024).toFixed(0)}KB`);
  }
  const performancePath = path.join(STANDALONE_PROOF_ROOT, 'performance.json');
  if (!existsSync(performancePath)) throw new Error('standalone proof did not write performance.json');
  const performance = stampHostState(performancePath);
  if (performance.schemaVersion !== 2 || performance.sampleFrames < 180)
    throw new Error(`invalid standalone performance sample: ${JSON.stringify(performance)}`);
  if (performance.width !== budget.width || performance.height !== budget.height ||
      performance.vSyncCount !== budget.vSync)
    throw new Error(`standalone performance conditions drifted: ${JSON.stringify(performance)}`);
  if (performance.internalRenderScale < 0.669 || performance.internalRenderScale > 0.671 ||
      performance.internalWidth < 1280 || performance.internalHeight < 720 ||
      performance.upscaleFilter !== 'EdgeAdaptiveScalingUpres' ||
      performance.lodBias < 0.99 || performance.lodBias > 1.01 || performance.lodCrossFade !== false ||
      performance.maxQueuedFrames !== 1 || performance.targetFrameRate !== 120)
    throw new Error(`standalone internal-render contract drifted: ${JSON.stringify(performance)}`);
  if (performance.p95Milliseconds > budget.p95Milliseconds) {
    // A failing p95 quoted bare invites the reader to supply a cause, and the supplied cause is
    // usually wrong -- on 2026-08-15 the obvious suspect (216k triangles of new mansion collision)
    // was innocent, and only the historical range proved it. Carry the conditions with the number.
    throw new Error(`standalone missed ${budget.p95Milliseconds.toFixed(1)}ms p95 budget: ` +
      `p95=${performance.p95Milliseconds.toFixed(2)}ms ${describeConditions(performance)}`);
  }
  console.log(`  ✓ frame pacing ${performance.width}x${performance.height} output / ` +
    `${performance.internalWidth}x${performance.internalHeight} internal (${(performance.internalRenderScale * 100).toFixed(0)}% ${performance.upscaleFilter}): ` +
    `${performance.sampleFrames} frames, ` +
    `mean=${performance.meanMilliseconds.toFixed(2)}ms p50=${performance.p50Milliseconds.toFixed(2)}ms ` +
    `p95=${performance.p95Milliseconds.toFixed(2)}ms p99=${performance.p99Milliseconds.toFixed(2)}ms ` +
    `max=${performance.maximumMilliseconds.toFixed(2)}ms ${describeConditions(performance)}`);
  console.log('  ✓ build contains no UnityEditor assembly');
  console.log('  ✓ built player loaded and decoded all five final Ninth Bell clips');
  console.log('  ✓ built player exercised cold-open, movement, look, interaction, wind, display calibration and pause through a virtual gamepad');
  console.log('  ✓ built player captured 2/2 controller-specific UI frames');
  console.log('  ✓ standalone player reported zero runtime errors or render-integrity warnings');
}

async function runConfiguredProbe(name) {
  // walk-proof is gate 10 and runs up to ten minutes before it reaches the p95 comparison. Refuse
  // the launch on an overloaded host rather than spending that time to produce a contaminated verdict.
  assertHostCapacity();
  const probe = SCENE.probes?.[name];
  if (!probe) throw new Error(`scene '${SCENE.id}' has no '${name}' probe configuration`);
  if (!MAC_BINARY || !existsSync(MAC_BINARY))
    throw new Error(`standalone app is missing; run build-mac first: ${MAC_APP}`);
  const output = path.join(PROJECT, 'Library', 'GmSceneIntelligence', 'player-probes', SCENE.id, name);
  rmSync(output, { recursive: true, force: true });
  mkdirSync(output, { recursive: true });
  const logFile = path.join(output, 'player.log');
  const budget = SCENE.performance ?? { width: 1280, height: 720, vSync: 1 };
  // `measured > undefined` is false, so a scene registered with a probe report but no p95 budget
  // would pass the frame-time half of the contract check below no matter how bad the number was.
  // Refuse to run rather than hand back a verdict that cannot fail.
  if (probe.report && !Number.isFinite(budget.p95Milliseconds))
    throw new Error(`scene '${SCENE.id}' registers a '${name}' probe report but no performance.p95Milliseconds budget to judge it against`);
  const args = ['-screen-fullscreen', '0', '-screen-width', String(budget.width),
    '-screen-height', String(budget.height), probe.flag, output, '-logFile', logFile];
  if (name === 'walk' && budget.vSync === 0)
    args.push('-gmWendNoVSync', '-gmWendPerformanceGate');
  console.log(`\n▶ ${SCENE.id}/${name}-proof`);
  console.log(`  app: ${MAC_APP}`);
  console.log(`  output: ${output}`);
  const timeoutMs = name === 'walk' ? 10 * 60 * 1000 : 3 * 60 * 1000;
  const exitCode = await new Promise((resolve, reject) => {
    const child = spawn(MAC_BINARY, args, { stdio: 'ignore' });
    const deadline = setTimeout(() => {
      child.kill('SIGTERM');
      reject(new Error(`${name} probe exceeded ${timeoutMs / 60000} minutes`));
    }, timeoutMs);
    child.on('error', (error) => { clearTimeout(deadline); reject(error); });
    child.on('exit', (code) => { clearTimeout(deadline); resolve(code); });
  });
  const log = existsSync(logFile) ? readFileSync(logFile, 'utf8') : '';
  // Stamp BEFORE any validation throws. A failing probe is precisely the run whose host conditions
  // matter, and stamping only on the success path left every failed gate with no context at all.
  if (probe.report) stampHostState(path.join(output, probe.report));
  if (exitCode !== 0) throw new Error(`${name} probe exited ${exitCode}; inspect ${logFile}`);
  if (!markerRegex(probe.success).test(log))
    throw new Error(`${name} probe did not report '${probe.success}'; inspect ${logFile}`);
  const integrity = findRuntimeIntegrityDefects(log);
  if (integrity.length) throw new Error(`${name} probe logged ${integrity.length} integrity defect(s): ${integrity.join(' | ')}`);
  const pngs = readdirSync(output).filter((file) => file.endsWith('.png'));
  if (pngs.length === 0) throw new Error(`${name} probe produced no screenshots`);
  if (probe.report) {
    const reportPath = path.join(output, probe.report);
    if (!existsSync(reportPath)) throw new Error(`${name} probe did not write ${probe.report}`);
    const report = stampHostState(reportPath);
    if (report.sceneId !== SCENE.id || report.completedFlow !== true || report.stalls !== 0 ||
        report.navFallbacks !== 0 || report.missingScreenshots !== 0 || report.runtimeDefects !== 0)
      throw new Error(`${name} probe report is not clean: ${JSON.stringify(report)}`);
    if (report.width !== budget.width || report.height !== budget.height ||
        report.vSyncCount !== budget.vSync || report.p95Milliseconds > budget.p95Milliseconds ||
        report.schemaVersion !== 4 || report.coveredRouteMetres < report.routeLengthMetres - 0.5 ||
        report.internalRenderScale < 0.669 ||
        report.internalRenderScale > 0.671 || report.upscaleFilter !== 'EdgeAdaptiveScalingUpres' ||
        report.lodBias < 0.99 || report.lodBias > 1.01 || report.lodCrossFade !== false ||
        report.maxQueuedFrames !== 1 || report.targetFrameRate !== 120)
      throw new Error(`${name} probe missed performance contract: ${JSON.stringify(report)}`);
    console.log(`  ✓ ${report.walkedMetres.toFixed(0)}m, ${report.width}x${report.height} output / ` +
      `${report.internalWidth}x${report.internalHeight} internal, p95=${report.p95Milliseconds.toFixed(2)}ms, ` +
      `${report.peakRendererCount} active renderers`);
  }
  console.log(`  ✓ ${pngs.length} screenshot(s), zero route/runtime integrity failures`);
}

if (requested === 'registry-check') {
  console.log(`✓ scene registry valid: ${REGISTRY.scenes.length} scene(s), default '${REGISTRY.defaultScene}'`);
  process.exit(0);
}

if (requested === 'scenes') {
  console.log(printSceneRegistry(REGISTRY));
  process.exit(0);
}

if (requested === 'host-health') {
  const state = currentHostCapacity();
  console.log(`${state.healthy ? '✓' : '✗'} Unity host capacity: ${state.reason}; limit ${MAX_LOAD_PER_CORE.toFixed(1)} per core`);
  process.exit(state.healthy ? 0 : 1);
}

if (requested === 'retry-selftest') {
  const stall = new UnityRunError('stall', { kind: 'gui-idle-stall', artifacts: 0 });
  const partial = new UnityRunError('partial', { kind: 'gui-idle-stall', artifacts: 1 });
  const defect = new UnityRunError('defect', { kind: 'reported-failure', artifacts: 0 });
  if (!isRetryableGuiFailure(stall) || isRetryableGuiFailure(partial) || isRetryableGuiFailure(defect)) {
    throw new Error('retry policy self-test failed');
  }
  console.log('✓ retry policy self-test: only a diagnosed GUI idle stall with zero artifacts is retryable');
  process.exit(0);
}

if (requested === 'standalone-proof') {
  try { await runStandaloneProof(); }
  catch (error) { console.error(`\n✗ ${error.message}`); process.exit(1); }
  console.log('\n✓ done: standalone-proof');
  process.exit(0);
}

if (requested === 'walk-proof' || requested === 'wall-proof' || requested === 'house-proof') {
  const name = requested === 'walk-proof' ? 'walk' : requested === 'wall-proof' ? 'walls' : 'house';
  try { await runConfiguredProbe(name); }
  catch (error) { console.error(`\n✗ ${error.message}`); process.exit(1); }
  console.log(`\n✓ done: ${requested}`);
  process.exit(0);
}

const queue = requested === 'setup' ? ['pipeline', 'rebuild'] : [requested];
if (!queue.every((t) => TASKS[t])) {
  console.error(`usage: node scripts/unity-cli.mjs <${Object.keys(TASKS).join('|')}|standalone-proof|walk-proof|wall-proof|house-proof|setup|registry-check|scenes|host-health|retry-selftest> [scene-id]`);
  console.error(`registered scenes: ${REGISTRY.scenes.map((scene) => scene.id).join(', ')}`);
  process.exit(2);
}

const sceneScopedRequest = requested === 'setup' || requested === 'standalone-proof' ||
  requested === 'walk-proof' || requested === 'wall-proof' || TASKS[requested]?.sceneScoped;
if (process.argv[3] && !sceneScopedRequest) {
  console.error(`✗ '${requested}' is project-wide and does not accept a scene id`);
  process.exit(2);
}

/**
 * NUnit's XML is the source of truth for the test run. A results file that exists but reports
 * failures, or reports zero tests (assembly never compiled, filter matched nothing), is a failure --
 * "Unity ran" is not "the tests passed".
 */
/**
 * Tests allowed to fail, each with the reason and the condition that retires it.
 *
 * This exists to break a real deadlock, not to hide red. Gate 2 of the opening pipeline runs the
 * ENTIRE EditMode suite and the runner breaks on first failure, so Court's two failures -- which are
 * failing on purpose, because Court's content is hard-locked behind Nick's Phase 0 walk -- made
 * gates 3-12 permanently unreachable. The opening could not be verified until Court was fixed, and
 * Court could not be touched until after the walk that verification was supposed to precede.
 *
 * The exclusion is deliberately brittle in the safe direction. An excluded test that starts PASSING
 * fails the run, because a stale exclusion is how a suite quietly stops meaning anything. So does
 * any failure outside this list. The only thing tolerated is exactly the documented, expected red.
 */
// Empty, and that is the goal state. Court's two tests lived here from 2026-08-15 until the lock on
// its content was lifted the same day; the rule then reported itself stale on the very next run,
// which is the behaviour that makes an allowlist survivable. Add entries only with a reason and a
// retirement condition, and expect the suite to evict them for you.
const EXPECTED_FAILURES = [];

function verifyTests(resultsPath, requiredFixture = null) {
  if (!existsSync(resultsPath)) throw new Error(`no test results at ${resultsPath} — the run produced nothing`);
  const xml = readFileSync(resultsPath, 'utf8');
  const attr = (k) => Number(xml.match(new RegExp(`\\b${k}="(\\d+)"`))?.[1] ?? 0);
  const [total, passed, failed] = [attr('total'), attr('passed'), attr('failed')];
  console.log(`\n  Unity tests: ${passed}/${total} passed, ${failed} failed`);
  if (total === 0) throw new Error('0 tests ran — the test assembly likely failed to compile or was not discovered');

  // Full name is what the allowlist matches against; NUnit puts it on the test-case element.
  const caseRe = /<test-case[^>]*?\bfullname="([^"]+)"[^>]*?\bresult="(Passed|Failed)"/g;
  const cases = [...xml.matchAll(caseRe)].map((m) => ({ name: m[1], result: m[2] }));
  const expectedFor = (name) => EXPECTED_FAILURES.find((e) => e.match.test(name));

  const unexpected = [];
  const tolerated = [];
  for (const c of cases) {
    if (c.result !== 'Failed') continue;
    const rule = expectedFor(c.name);
    if (rule) tolerated.push(c.name); else unexpected.push(c.name);
  }

  // A stale exclusion is worse than no exclusion: it silently shrinks the suite. If an excluded
  // test has started passing, the rule has outlived its reason and must be deleted.
  const staleRules = EXPECTED_FAILURES.filter((rule) =>
    cases.some((c) => rule.match.test(c.name) && c.result === 'Passed') &&
    !cases.some((c) => rule.match.test(c.name) && c.result === 'Failed'));

  for (const name of tolerated) console.log(`    ⚠ ${name} — failing as expected`);
  for (const name of unexpected) console.log(`    ✗ ${name}`);

  if (staleRules.length) {
    for (const rule of staleRules) console.error(`  ✗ stale expected-failure rule ${rule.match} — these tests now PASS`);
    throw new Error(`${staleRules.length} expected-failure rule(s) are stale — delete them from EXPECTED_FAILURES`);
  }
  if (unexpected.length) throw new Error(`${unexpected.length} Unity test(s) failed — see ${resultsPath}`);
  if (failed > 0 && tolerated.length === 0)
    throw new Error(`${failed} Unity test(s) failed but none were identified by name — see ${resultsPath}`);
  if (tolerated.length) {
    console.log(`  ⚠ ${tolerated.length} expected failure(s) tolerated:`);
    for (const rule of EXPECTED_FAILURES) console.log(`      ${rule.match} — ${rule.why} Retires when: ${rule.retireWhen}`);
  }

  if (requiredFixture && !xml.includes(requiredFixture))
    throw new Error(`Unity test run omitted required fixture '${requiredFixture}'`);
  return { total, passed, tolerated: tolerated.length };
}

/**
 * Opening a real editor window is not perfectly reliable. An independent review run stalled during
 * project load -- past licensing, before play mode -- and the idle guard correctly killed it with
 * zero shots; the identical command then succeeded on the next attempt. Retry ONLY that diagnosed
 * zero-artifact idle stall. Reported failures, partial captures, deadlines, spawn errors, and
 * unexpected exits remain first-attempt failures and are never papered over.
 *
 * Each GUI attempt writes a distinct log, and the successful attempt is copied to the historical
 * cli-tour.log path. A flaky green run therefore retains both the failure evidence and the success
 * evidence instead of deleting the first log on retry. Batchmode tasks are never retried.
 */
async function runWithRetry(name) {
  const attempts = TASKS[name].gui ? 2 : 1;
  const failures = [];
  if (TASKS[name].gui) {
    rmSync(path.join(LOG_DIR, `${taskLogStem(name)}.log`), { force: true });
    for (let attempt = 1; attempt <= attempts; attempt++) {
      rmSync(path.join(LOG_DIR, `${taskLogStem(name)}-attempt-${attempt}.log`), { force: true });
      rmSync(path.join(LOG_DIR, `${warmLogStem(name)}-attempt-${attempt}.log`), { force: true });
    }
  }

  for (let attempt = 1; attempt <= attempts; attempt++) {
    try {
      const logFile = await run(name, attempt, attempts);
      if (TASKS[name].gui) copyFileSync(logFile, path.join(LOG_DIR, `${taskLogStem(name)}.log`));
      if (attempt > 1) {
        console.warn(`\n  ⚠ ${name} PASSED ON RETRY after ${failures[0].kind}; run is green but flaky`);
        console.warn(`  preserved failure log: ${failures[0].logFile}`);
        console.warn(`  preserved success log: ${logFile}`);
      }
      return logFile;
    } catch (err) {
      failures.push(err);
      console.warn(`\n  ⚠ ${name} attempt ${attempt}/${attempts} failed [${err.kind ?? 'unknown'}]: ${err.message}`);
      if (attempt >= attempts || !isRetryableGuiFailure(err)) throw err;
      console.warn('  ↻ retrying once: diagnosed windowed-editor idle stall with zero artifacts');
      await waitForEditorRelease();
    }
  }
  throw failures[failures.length - 1];
}

for (const task of queue) {
  try {
    const logFile = await runWithRetry(task);
    if (task === 'tour') {
      // Read the log at verify time rather than trusting what the poller happened to have seen.
      verifyShots(TOUR_SCENE, existsSync(logFile) ? readFileSync(logFile, 'utf8') : '',
        SCENE.tour.logTag, SCENE.tour.shots);
    }
    if (task === 'test') verifyTests(TEST_RESULTS);
    if (task === 'playtest') verifyTests(PLAYTEST_RESULTS, 'GmWendCanonicalPlayModeTests');
  } catch (err) {
    console.error(`\n✗ ${err.message}`);
    process.exit(1);
  }
}
console.log(`\n✓ done: ${queue.join(' → ')}`);
