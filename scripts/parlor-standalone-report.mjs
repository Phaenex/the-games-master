import { createHash } from 'node:crypto';
import { readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';

export const REQUIRED_PARLOR_STANDALONE_SHOTS = [
  '01-ready',
  '02-card-motion',
  '03-suspicious-caption',
  '04-focus-evidence',
  '05-correct-read-result',
  '06-match-result',
  '07-rematch-ready',
  '08-pause-settings',
];

export const REQUIRED_PARLOR_COVERAGE_SHOTS = Object.freeze({
  'honest-baseline': Object.freeze([
    '01-honest-suspicious-caption',
    '02-honest-focus-evidence',
    '03-false-read-result',
  ]),
  'honest-hc200': Object.freeze([
    '01-hc200-settings',
    '02-honest-suspicious-caption-hc200',
    '03-honest-focus-evidence-hc200',
    '04-false-read-result-hc200',
  ]),
});

const SHA = /^[a-f0-9]{64}$/;
const REQUIRED_PHASE_SAMPLES = new Map([
  ['card-motion', 20],
  ['tell-caption', 20],
  ['focus', 120],
  ['result', 120],
  ['pause', 120],
]);
const fail = (message) => { throw new Error(`Parlor standalone report: ${message}`); };

export function sha256File(file) {
  return createHash('sha256').update(readFileSync(file)).digest('hex');
}

export function validateParlorStandaloneReport(report, options = {}) {
  if (!report || typeof report !== 'object') fail('missing report');
  if (report.schemaVersion !== 1 || report.seedCatalogVersion !== 1)
    fail('unsupported report or seed catalog version');
  if (report.evidenceKind !== 'macos-release-player-backbuffer')
    fail('evidence kind must identify a macOS release-player backbuffer');
  if (!Number.isInteger(report.repetition) || report.repetition < 1 || report.repetition > 3)
    fail('invalid repetition');
  if (report.resolution?.width !== 1920 || report.resolution?.height !== 1080)
    fail('proof must run at 1920x1080');
  const environment = report.environment;
  if (!environment || environment.applicationIsEditor !== false ||
      environment.batchMode !== false)
    fail('proof did not identify a non-editor, non-batch release player');
  if (environment.scene !== 'Parlor') fail(`runtime scene was ${environment.scene ?? 'missing'}`);
  if (!environment.qualityLevel || !environment.graphicsDevice || !environment.unityVersion ||
      !Number.isFinite(environment.renderScale) || environment.renderScale <= 0)
    fail('runtime quality/render environment is incomplete');
  for (const field of ['buildSha256', 'sceneSha256', 'contentSha256',
    'packageManifestSha256'])
    if (!SHA.test(report[field] ?? '')) fail(`${field} is missing or malformed`);
  const generated = Date.parse(report.generatedUtc);
  const age = (options.now ?? Date.now()) - generated;
  if (!Number.isFinite(generated) || age < -60_000 || age > 10 * 60_000)
    fail('report is stale or future-dated');

  const expectedCase = ['cheat-true-tell-eyes', 8, 4, 'Eyes', true, 'Suspicious',
    'RenegedWithHeldFlame', 'CorrectRead', 'Aldric'];
  const seed = report.seedCase;
  if (JSON.stringify([seed?.id, seed?.seed, seed?.tier, seed?.suit,
    seed?.expectedCheat, seed?.expectedTell, seed?.cheatFamily, seed?.decision,
    seed?.expectedWinner]) !==
    JSON.stringify(expectedCase)) fail('literal suspicious seed case drifted');
  if (report.controllerOnly !== true) fail('controller-only proof flag is absent');
  if (report.directStateMutationDetected !== false) fail('direct state mutation detected');
  if (report.hiddenTruthLeakageDetected !== false) fail('hidden truth leaked into player UI');

  if (!Array.isArray(report.shots) ||
      report.shots.length !== REQUIRED_PARLOR_STANDALONE_SHOTS.length)
    fail(`shot sequence is incomplete: ${report.shots?.length ?? 0}/${REQUIRED_PARLOR_STANDALONE_SHOTS.length}`);
  const screenshotHashes = options.screenshotHashes ?? {};
  const seen = new Set();
  report.shots.forEach((shot, index) => {
    const required = REQUIRED_PARLOR_STANDALONE_SHOTS[index];
    if (shot.name !== required) fail(`shot sequence differs at ${index}`);
    if (!SHA.test(shot.sha256 ?? '') || screenshotHashes[shot.name] !== shot.sha256)
      fail(`shot hash mismatch for ${shot.name}`);
    if (seen.has(shot.sha256)) fail(`reused shot bytes for ${shot.name}`);
    seen.add(shot.sha256);
    for (const field of ['publicStateSha256', 'cardStateSha256', 'evidenceSha256'])
      if (!SHA.test(shot[field] ?? '')) fail(`${shot.name} lacks ${field}`);
    if (!Array.isArray(shot.observedEvidence)) fail(`${shot.name} lacks evidence rows`);
  });
  const byName = new Map(report.shots.map((shot) => [shot.name, shot]));
  if (!(byName.get('03-suspicious-caption').caption ?? '').trim())
    fail('suspicious tell has no caption');
  if (!byName.get('04-focus-evidence').focusOpen ||
      byName.get('04-focus-evidence').observedEvidence.length < 2)
    fail('focus evidence is missing');
  if (!/caught/i.test(byName.get('05-correct-read-result').feedback ?? ''))
    fail('correct Read result is not public');
  if (byName.get('06-match-result').phase !== 'MatchResult')
    fail('match result shot is not a result');
  if (byName.get('07-rematch-ready').phase === 'MatchResult')
    fail('rematch did not leave MatchResult');
  if (!byName.get('08-pause-settings').paused)
    fail('settings shot is not paused');

  const match = report.match;
  if (!match?.suspiciousTellObserved || !match?.correctReadResolved)
    fail('suspicious Read path was not completed');
  const resultShot = byName.get('06-match-result');
  const scoreText = resultShot.uiText?.find((row) => /Matches\s+\d+-\d+/.test(row)) ?? '';
  const score = scoreText.match(/Matches\s+(\d+)-(\d+)/);
  if (!score) fail('result score is missing from the public result state');
  const scoreWinner = Number(score[1]) > Number(score[2]) ? 'Player'
    : Number(score[2]) > Number(score[1]) ? 'Aldric' : '';
  if (!scoreWinner || match?.resultWinner !== seed.expectedWinner ||
      match.resultWinner !== scoreWinner)
    fail('result winner disagrees with the frozen seed or public result score');
  if (match.resultPublicStateSha256 !== resultShot.publicStateSha256)
    fail('latched result public-state hash disagrees with the result shot');
  if (match.matchCompletionEvents !== 1) fail('match completion event count is not exactly one');
  if (!match.rematchStarted || match.rematchStartEvents !== 1)
    fail('rematch did not start exactly once');
  if (match.rematchActionCompleted !== true)
    fail('rematch action was not completed through gameplay');
  if (!match.quitRequested) fail('quit was not requested through the player');
  for (const field of ['ruleStateEvents', 'outcomeEvents', 'durableWrites'])
    if (!Number.isInteger(match[field]) || match[field] < 1) fail(`${field} was not measured`);

  const perf = report.performance;
  if (!perf || perf.warmupSeconds < 10 || perf.warmupFrames < 1 || perf.sampleFrames < 1800)
    fail('performance sample is incomplete');
  if (!perf.frameTimingAvailable || !perf.gcRecorderAvailable)
    fail('frame timing or GC recorder was unavailable');
  if (perf.unavailableFrameTimingSamples !== 0)
    fail('unavailable frame timing samples were observed');
  if (!Number.isInteger(perf.pendingFrameTimingPolls) || perf.pendingFrameTimingPolls < 0)
    fail('pending frame timing poll count is missing');
  if (perf.allocationMeasurement !== 'main-thread-cumulative-allocation-counter')
    fail('allocation measurement is not the release-player main-thread counter');
  if (perf.mainThreadMeasurement !== 'main-thread-frame-minus-present-wait')
    fail('main thread measurement did not exclude present wait');
  if (!(perf.p95HalfSpreadPercent <= 25)) fail('split-half p95 spread exceeded 25%');
  const rawNames = ['rawTotalMilliseconds', 'rawMainThreadMilliseconds',
    'rawCpuMainThreadFrameMilliseconds', 'rawMainThreadPresentWaitMilliseconds',
    'rawGpuMilliseconds', 'rawGcAllocatedBytes', 'rawPhase'];
  for (const field of rawNames)
    if (!Array.isArray(perf[field]) || perf[field].length !== perf.sampleFrames)
      fail(`raw ${field} sample count differs from ${perf.sampleFrames}`);
  if (perf.rawTotalMilliseconds.some((value) => !Number.isFinite(value) || value <= 0) ||
      perf.rawMainThreadMilliseconds.some((value) => !Number.isFinite(value) || value < 0) ||
      perf.rawCpuMainThreadFrameMilliseconds.some(
        (value) => !Number.isFinite(value) || value <= 0) ||
      perf.rawMainThreadPresentWaitMilliseconds.some(
        (value) => !Number.isFinite(value) || value < 0) ||
      perf.rawGpuMilliseconds.some((value) => !Number.isFinite(value) || value <= 0) ||
      perf.rawGcAllocatedBytes.some((value) => !Number.isFinite(value) || value < 0))
    fail('raw performance samples contain unavailable values');
  if (perf.captureQuarantineTimingRows !== 16)
    fail('capture timing quarantine did not match the frozen 16-row contract');
  if (!perf.captureFramesExcluded || !perf.ioFramesExcluded)
    fail('capture and I/O frames must be outside timing windows');
  if (!(perf.p95Milliseconds < 34)) fail('total p95 missed 34ms');
  if (!(perf.mainThreadP95Milliseconds <= 34)) fail('main thread p95 missed 34ms');
  if (!(perf.gpuP95Milliseconds <= 20)) fail('GPU p95 missed 20ms');
  if (!(perf.p99Milliseconds < 80)) fail('p99 missed 80ms');
  if (!(perf.maximumMilliseconds <= 500)) fail('maximum non-load frame exceeded 500ms');
  if (perf.gcAllocatedBytes !== 0) fail('steady-state GC allocation is nonzero');
  const phases = new Map((perf.phases ?? []).map((item) => [item.name, item]));
  for (const [name, minimum] of REQUIRED_PHASE_SAMPLES) {
    const item = phases.get(name);
    if (!item || item.samples < minimum) fail(`${name} has insufficient samples`);
    if (!(item.p95Milliseconds < 34) || !(item.p99Milliseconds < 80) ||
        !(item.maximumMilliseconds <= 500)) fail(`${name} missed frame budget`);
    if (!(item.mainThreadP95Milliseconds <= 34)) fail(`${name} main thread missed budget`);
    if (!(item.gpuP95Milliseconds <= 20)) fail(`${name} GPU missed budget`);
    if (item.gcAllocatedBytes !== 0) fail(`${name} GC allocation is nonzero`);
    const rawCount = perf.rawPhase.filter((value) => value === name).length;
    if (rawCount !== item.samples) fail(`${name} aggregate does not match raw samples`);
  }

  const host = report.host;
  const ignoreHostLoad = Boolean(options.ignoreHostLoad);
  if (!host || !Number.isFinite(host.loadPerCore) || (!ignoreHostLoad && host.loadPerCore > 1.0) ||
      !Number.isInteger(host.cores) || host.cores < 1 || !Number.isFinite(host.freeMemoryMB))
    fail('host load was untrusted or missing');

  const forbidden = /GmRunStore|CompleteRoom|TryImport|TrySetParlorMatch|CommitParlor|RestoreParlorSnapshotForTransaction|\.Match\.(?:Play|Continue|Read|StartRematch)|controller\.(?:ConfirmFocusedAction|CallRead|ApplyNavigation|SetFocusedCardIndex)|presentation\.(?:Advance|FastForward|ResetTransientState|TryRestore)/;
  if (forbidden.test(options.source ?? ''))
    fail('controller-only source contains direct canonical or presentation mutation');
  if (report.cli?.exitCode !== 0) fail(`child exit was ${report.cli?.exitCode ?? 'missing'}`);
  if (!Number.isInteger(report.cli?.pid) || report.cli.pid <= 0) fail('child pid is missing');
  if (!SHA.test(report.cli?.logSha256 ?? '') || !report.cli?.logPath)
    fail('player log attribution is missing');
  const log = options.log ?? '';
  const passAt = log.lastIndexOf(`[GmParlorStandaloneProbe] PASS repetition=${report.repetition}`);
  if (passAt < 0 || !log.includes('[GmPlayer] quit requested from pause menu'))
    fail('PASS or clean player quit marker is missing');
  if (/(?:Exception:|NullReferenceException|\bFAILED\b|\bError:)/
      .test(log.slice(passAt + 1))) fail('exception or error found after PASS');
  return true;
}

export function validateParlorStandaloneSeries(reports) {
  if (!Array.isArray(reports) || reports.length !== 3)
    fail('three consecutive clean repetitions are required');
  if (reports.map((report) => report.repetition).join(',') !== '1,2,3')
    fail('repetitions must be ordered 1,2,3');
  if (new Set(reports.map((report) => report.buildSha256)).size !== 1)
    fail('three repetitions did not use the same app SHA');
  const roots = reports.map((report) => report.shots[0]?.file?.split('/').slice(0, -1).join('/'));
  if (new Set(roots).size !== 3) fail('repetitions reused an output directory');
  return true;
}

export function validateParlorCoverageReport(report, options = {}) {
  if (!report || typeof report !== 'object') fail('missing coverage report');
  const mode = report.coverageMode;
  const required = REQUIRED_PARLOR_COVERAGE_SHOTS[mode];
  if (!required) fail(`unsupported coverage mode ${mode ?? 'missing'}`);
  if (report.schemaVersion !== 1 || report.seedCatalogVersion !== 1 ||
      report.evidenceKind !== 'macos-release-player-parlor-coverage')
    fail('coverage evidence kind or version is invalid');
  if (report.resolution?.width !== 1920 || report.resolution?.height !== 1080 ||
      report.environment?.applicationIsEditor !== false ||
      report.environment?.batchMode !== false || report.environment?.scene !== 'Parlor')
    fail('coverage did not identify a 1920x1080 non-editor Parlor player');
  for (const field of ['buildSha256', 'sceneSha256', 'contentSha256',
    'packageManifestSha256'])
    if (!SHA.test(report[field] ?? '')) fail(`coverage ${field} is missing`);
  const generated = Date.parse(report.generatedUtc);
  const age = (options.now ?? Date.now()) - generated;
  if (!Number.isFinite(generated) || age < -60_000 || age > 10 * 60_000)
    fail('coverage report is stale or future-dated');
  const seed = report.seedCase;
  const expected = ['honest-false-tell-teeth', 27, 4, 'Teeth', false, 'Suspicious',
    'None', 'FalseRead', 'FalseReadPenalty'];
  if (JSON.stringify([seed?.id, seed?.seed, seed?.tier, seed?.suit,
    seed?.expectedCheat, seed?.expectedTell, seed?.cheatFamily, seed?.decision,
    seed?.expectedOutcome]) !== JSON.stringify(expected))
    fail('frozen honest false-Read seed drifted');
  if (report.controllerOnly !== true || report.directStateMutationDetected !== false)
    fail('coverage is not controller-only');
  if (report.hiddenTruthLeakageDetected !== false)
    fail('honest truth leaked before the Read resolved');

  if (!Array.isArray(report.shots) || report.shots.length !== required.length)
    fail('coverage shot sequence is incomplete');
  const screenshotHashes = options.screenshotHashes ?? {};
  const seen = new Set();
  report.shots.forEach((shot, index) => {
    if (shot.name !== required[index]) fail(`coverage shot sequence differs at ${index}`);
    if (!SHA.test(shot.sha256 ?? '') || screenshotHashes[shot.name] !== shot.sha256)
      fail(`coverage shot hash mismatch for ${shot.name}`);
    if (seen.has(shot.sha256)) fail(`coverage reused shot bytes for ${shot.name}`);
    seen.add(shot.sha256);
    for (const field of ['publicStateSha256', 'cardStateSha256', 'evidenceSha256'])
      if (!SHA.test(shot[field] ?? '')) fail(`${shot.name} lacks ${field}`);
  });
  const result = report.shots.at(-1);
  const preResolution = report.shots.slice(0, -1)
    .flatMap((shot) => [...(shot.uiText ?? []), shot.feedback ?? '', shot.caption ?? ''])
    .join('\n');
  if (/\bhonest\b|expectedCheat|FalseReadPenalty/i.test(preResolution))
    fail('honest truth leaked into pre-resolution coverage UI');
  if (result.phase !== 'TrickResult' || !/Aldric was honest/i.test(result.feedback ?? ''))
    fail('false Read lacks truthful post-resolution player copy');
  if (!report.shots.some((shot) => /focus-evidence/.test(shot.name) && shot.focusOpen))
    fail('honest coverage lacks a real focus/evidence view');

  const coverage = report.coverage;
  if (!coverage?.falseReadResolved || coverage.resolvedOutcomeKind !== 'FalseReadPenalty')
    fail('false Read path did not resolve as the frozen outcome');
  if (coverage.canonicalPublicStateSha256 !== result.publicStateSha256 ||
      coverage.canonicalCardStateSha256 !== result.cardStateSha256)
    fail('coverage canonical result hashes do not match its result shot');
  for (const field of ['ruleStateEvents', 'outcomeEvents', 'durableWrites'])
    if (!Number.isInteger(coverage[field]) || coverage[field] < 1)
      fail(`coverage ${field} was not measured`);
  const accessibility = coverage.accessibility;
  if (mode === 'honest-hc200') {
    const hc200 = accessibility?.captions === true && accessibility?.reducedMotion === true &&
      accessibility?.vibration === false && accessibility?.monoAudio === true &&
      accessibility?.highContrast === true && accessibility?.textScale === 2;
    if (!hc200 || coverage.settingsChangedThroughController !== true ||
        coverage.preferencesPersisted !== true || !SHA.test(coverage.preferencesSha256 ?? ''))
      fail('HC200 settings or controller/persistence proof is fake or incomplete');
    const settings = report.shots[0];
    if (!settings.paused || !/hc200-settings/.test(settings.name))
      fail('HC200 Settings screenshot is missing');
  } else if (!accessibility || accessibility.captions !== true ||
      accessibility.reducedMotion !== false || accessibility.vibration !== true ||
      accessibility.monoAudio !== false || accessibility.highContrast !== false ||
      accessibility.textScale !== 1) fail('baseline accessibility state drifted');

  const forbidden = /GmRunStore|CompleteRoom|TryImport|TrySetParlorMatch|CommitParlor|RestoreParlorSnapshotForTransaction|\.Match\.(?:Play|Continue|Read|StartRematch)|controller\.(?:ConfirmFocusedAction|CallRead|ApplyNavigation|SetFocusedCardIndex)|presentation\.(?:Advance|FastForward|ResetTransientState|TryRestore)|GmAccessibilitySettings\.Set/;
  if (forbidden.test(options.source ?? ''))
    fail('coverage source contains direct gameplay or accessibility mutation');
  if (report.cli?.exitCode !== 0 || !Number.isInteger(report.cli?.pid) ||
      report.cli.pid <= 0 || !SHA.test(report.cli?.logSha256 ?? ''))
    fail('coverage child/process log attribution is invalid');
  const log = options.log ?? '';
  const passAt = log.lastIndexOf(`[GmParlorStandaloneProbe] COVERAGE PASS mode=${mode}`);
  if (passAt < 0 || !log.includes('[GmPlayer] quit requested from pause menu'))
    fail('coverage PASS or player quit marker is missing');
  if (/(?:Exception:|NullReferenceException|\bFAILED\b|\bError:)/.test(log.slice(passAt + 1)))
    fail('coverage logged an exception after PASS');
  return true;
}

export function validateParlorCoveragePair(baseline, hc200) {
  if (baseline?.coverageMode !== 'honest-baseline' || hc200?.coverageMode !== 'honest-hc200')
    fail('baseline and HC200 coverage pair is incomplete');
  if (baseline.buildSha256 !== hc200.buildSha256)
    fail('coverage pair did not use the same app');
  if (baseline.coverage?.resolvedOutcomeKind !== hc200.coverage?.resolvedOutcomeKind ||
      baseline.coverage?.canonicalPublicStateSha256 !==
        hc200.coverage?.canonicalPublicStateSha256 ||
      baseline.coverage?.canonicalCardStateSha256 !== hc200.coverage?.canonicalCardStateSha256)
    fail('HC200 changed the canonical result');
  return true;
}

export function validateParlorCurrentProvenance(series, reports, coverageReports,
  { currentSceneSha256, currentBuildSha256 } = {}) {
  if (!SHA.test(currentSceneSha256 ?? '')) fail('current Parlor scene hash is missing');
  if (!SHA.test(currentBuildSha256 ?? '')) fail('current release build hash is missing');
  const allReports = [...(reports ?? []), ...(coverageReports ?? [])];
  if (allReports.length !== 5)
    fail('current provenance requires three performance and two coverage reports');
  if (allReports.some((report) => report?.sceneSha256 !== currentSceneSha256))
    fail('current Parlor scene differs from the qualified evidence');
  if (allReports.some((report) => report?.buildSha256 !== currentBuildSha256))
    fail('current release build differs from the qualified evidence');
  if (series?.appSha256 !== currentBuildSha256)
    fail('series app SHA differs from the current release build');
  return true;
}

export function normalizeParlorShotFile(projectRoot, reportPath, shotFile) {
  const reportDirectory = path.dirname(reportPath);
  const absolute = path.resolve(reportDirectory, shotFile);
  if (path.dirname(absolute) !== reportDirectory)
    fail('screenshot path escapes repetition directory');
  return { absolute, file: path.relative(projectRoot, absolute) };
}

export function finalizeParlorStandaloneReport(reportPath,
  { projectRoot, repoRoot, logPath, exitCode, pid, binaryPath, host }) {
  const report = JSON.parse(readFileSync(reportPath, 'utf8'));
  report.buildSha256 = sha256File(binaryPath);
  report.sceneSha256 = sha256File(path.join(projectRoot, 'Assets', 'Scenes', 'Parlor.unity'));
  const sourceRoot = path.join(repoRoot, 'unity', 'scenes', 'parlor');
  const content = [];
  const walk = (directory) => {
    for (const entry of readdirSync(directory, { withFileTypes: true })) {
      const file = path.join(directory, entry.name);
      if (entry.isDirectory()) walk(file);
      else if (entry.name.endsWith('.cs')) content.push(file);
    }
  };
  walk(sourceRoot);
  const contentHash = createHash('sha256');
  for (const file of content.sort()) {
    contentHash.update(path.relative(repoRoot, file));
    contentHash.update(readFileSync(file));
  }
  report.contentSha256 = contentHash.digest('hex');
  report.packageManifestSha256 = sha256File(path.join(projectRoot, 'Packages', 'manifest.json'));
  report.host = host;
  report.cli = {
    exitCode,
    pid,
    logPath: path.relative(projectRoot, logPath),
    logSha256: sha256File(logPath),
  };
  const screenshotHashes = {};
  for (const shot of report.shots) {
    const normalized = normalizeParlorShotFile(projectRoot, reportPath, shot.file);
    shot.file = normalized.file;
    screenshotHashes[shot.name] = sha256File(normalized.absolute);
  }
  writeFileSync(reportPath, `${JSON.stringify(report, null, 2)}\n`);
  return { report, screenshotHashes };
}

export function finalizeParlorCoverageReport(reportPath,
  { projectRoot, repoRoot, logPath, exitCode, pid, binaryPath }) {
  const report = JSON.parse(readFileSync(reportPath, 'utf8'));
  report.buildSha256 = sha256File(binaryPath);
  report.sceneSha256 = sha256File(path.join(projectRoot, 'Assets', 'Scenes', 'Parlor.unity'));
  const sourceRoot = path.join(repoRoot, 'unity', 'scenes', 'parlor');
  const content = [];
  const walk = (directory) => {
    for (const entry of readdirSync(directory, { withFileTypes: true })) {
      const file = path.join(directory, entry.name);
      if (entry.isDirectory()) walk(file);
      else if (entry.name.endsWith('.cs')) content.push(file);
    }
  };
  walk(sourceRoot);
  const contentHash = createHash('sha256');
  for (const file of content.sort()) {
    contentHash.update(path.relative(repoRoot, file));
    contentHash.update(readFileSync(file));
  }
  report.contentSha256 = contentHash.digest('hex');
  report.packageManifestSha256 = sha256File(path.join(projectRoot, 'Packages', 'manifest.json'));
  report.cli = {
    exitCode, pid, logPath: path.relative(projectRoot, logPath), logSha256: sha256File(logPath),
  };
  const screenshotHashes = {};
  for (const shot of report.shots) {
    const normalized = normalizeParlorShotFile(projectRoot, reportPath, shot.file);
    shot.file = normalized.file;
    screenshotHashes[shot.name] = sha256File(normalized.absolute);
  }
  writeFileSync(reportPath, `${JSON.stringify(report, null, 2)}\n`);
  return { report, screenshotHashes };
}
