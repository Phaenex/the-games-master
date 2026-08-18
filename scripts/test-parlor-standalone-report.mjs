import assert from 'node:assert/strict';
import {
  REQUIRED_PARLOR_COVERAGE_SHOTS,
  REQUIRED_PARLOR_STANDALONE_SHOTS,
  normalizeParlorShotFile,
  validateParlorCurrentProvenance,
  validateParlorCoveragePair,
  validateParlorCoverageReport,
  validateParlorStandaloneReport,
  validateParlorStandaloneSeries,
} from './parlor-standalone-report.mjs';

assert.deepEqual(normalizeParlorShotFile(
  '/repo/unity-project',
  '/repo/unity-project/Library/GmSceneIntelligence/parlor-standalone/rep-1/report.json',
  '01-ready.png'), {
  absolute: '/repo/unity-project/Library/GmSceneIntelligence/parlor-standalone/rep-1/01-ready.png',
  file: 'Library/GmSceneIntelligence/parlor-standalone/rep-1/01-ready.png',
});
assert.throws(() => normalizeParlorShotFile(
  '/repo/unity-project',
  '/repo/unity-project/Library/GmSceneIntelligence/parlor-standalone/rep-1/report.json',
  '../../../01-ready.png'), /escapes repetition directory/i);

const SHA = 'a'.repeat(64);
const phase = (name, samples, p50 = 10, p95 = 10, p99 = 10, maximum = 10) => ({
  name, samples, p50Milliseconds: p50, p95Milliseconds: p95,
  p99Milliseconds: p99, maximumMilliseconds: maximum,
  mainThreadP95Milliseconds: 7, gpuP95Milliseconds: 8,
  gcAllocatedBytes: 0,
});

function fixture() {
  const hashes = Object.fromEntries(REQUIRED_PARLOR_STANDALONE_SHOTS.map((name, index) =>
    [name, (index + 1).toString(16).padStart(64, '0')]));
  return {
    report: {
      schemaVersion: 1,
      seedCatalogVersion: 1,
      evidenceKind: 'macos-release-player-backbuffer',
      generatedUtc: new Date().toISOString(),
      repetition: 1,
      resolution: { width: 1920, height: 1080 },
      environment: {
        applicationIsEditor: false, batchMode: false, scene: 'Parlor',
        qualityLevel: 'High Fidelity', renderScale: 1,
        graphicsDevice: 'Apple M5', unityVersion: '6000.5.3f1',
      },
      buildSha256: SHA,
      sceneSha256: 'b'.repeat(64),
      contentSha256: 'c'.repeat(64),
      packageManifestSha256: 'd'.repeat(64),
      controllerOnly: true,
      directStateMutationDetected: false,
      hiddenTruthLeakageDetected: false,
      seedCase: {
        id: 'cheat-true-tell-eyes', seed: 8, tier: 4, suit: 'Eyes',
        expectedCheat: true, expectedTell: 'Suspicious',
        cheatFamily: 'RenegedWithHeldFlame', decision: 'CorrectRead',
        expectedWinner: 'Aldric',
      },
      shots: REQUIRED_PARLOR_STANDALONE_SHOTS.map((name) => ({
        name, file: `Library/GmSceneIntelligence/parlor-standalone/rep-1/${name}.png`,
        sha256: hashes[name], phase: name.includes('match-result') ? 'MatchResult' : 'PlayerLeads',
        publicStateSha256: 'e'.repeat(64), cardStateSha256: 'f'.repeat(64),
        evidenceSha256: '1'.repeat(64),
        observedEvidence: name.includes('focus') ? ['Aldric touched his sleeve.',
          'The played card broke the visible suit obligation.'] : [],
        feedback: name.includes('correct-read') ? 'You caught Aldric cheating.' : '',
        caption: name.includes('suspicious-caption') ? 'Aldric touched his sleeve.' : '',
        uiText: name.includes('match-result')
          ? ['Round 2   Tricks 2-4   Matches 0-2', 'Leave for Court, or begin a rematch']
          : ['Round 1   Tricks 0-0   Matches 0-0'],
        focusOpen: name.includes('focus'), paused: name.includes('pause'),
      })),
      match: {
        suspiciousTellObserved: true, correctReadResolved: true,
        resultWinner: 'Aldric', resultPublicStateSha256: 'e'.repeat(64),
        matchCompletionEvents: 1, rematchStarted: true,
        rematchStartEvents: 1, rematchActionCompleted: true,
        quitRequested: true, ruleStateEvents: 34,
        outcomeEvents: 1, durableWrites: 40,
      },
      performance: {
        warmupSeconds: 10, warmupFrames: 1000, sampleFrames: 1800,
        p50Milliseconds: 10, p95Milliseconds: 10, p99Milliseconds: 10,
        maximumMilliseconds: 10, mainThreadP95Milliseconds: 7,
        gpuP95Milliseconds: 8, gcAllocatedBytes: 0,
        frameTimingAvailable: true, gcRecorderAvailable: true,
        allocationMeasurement: 'main-thread-cumulative-allocation-counter',
        mainThreadMeasurement: 'main-thread-frame-minus-present-wait',
        unavailableFrameTimingSamples: 0, pendingFrameTimingPolls: 400,
        p95FirstHalfMilliseconds: 10, p95SecondHalfMilliseconds: 10.5,
        p95HalfSpreadPercent: 5,
        rawTotalMilliseconds: Array(1800).fill(10),
        rawMainThreadMilliseconds: Array(1800).fill(7),
        rawCpuMainThreadFrameMilliseconds: Array(1800).fill(8),
        rawMainThreadPresentWaitMilliseconds: Array(1800).fill(1),
        rawGpuMilliseconds: Array(1800).fill(8),
        rawGcAllocatedBytes: Array(1800).fill(0),
        rawPhase: [
          ...Array(360).fill('card-motion'), ...Array(120).fill('tell-caption'),
          ...Array(440).fill('focus'), ...Array(440).fill('result'),
          ...Array(440).fill('pause'),
        ],
        captureQuarantineTimingRows: 16,
        captureFramesExcluded: true, ioFramesExcluded: true,
        phases: [phase('card-motion', 360), phase('tell-caption', 120),
          phase('focus', 440), phase('result', 440), phase('pause', 440)],
      },
      host: { oneMinuteLoad: 2, cores: 10, loadPerCore: 0.2, freeMemoryMB: 4096 },
      cli: { exitCode: 0, pid: 12345, logPath: 'player.log', logSha256: '2'.repeat(64) },
    },
    hashes,
    source: 'InputSystem.QueueStateEvent(gamepad, state);',
    log: '[GmParlorStandaloneProbe] PASS repetition=1\n' +
      '[GmPlayer] quit requested from pause menu',
  };
}

function validate(item = fixture()) {
  return validateParlorStandaloneReport(item.report, {
    screenshotHashes: item.hashes, source: item.source, log: item.log,
  });
}

function rejected(mutator, pattern) {
  const item = fixture();
  mutator(item);
  assert.throws(() => validate(item), pattern);
}

assert.equal(validate(), true);
{
  const item = fixture();
  item.report.performance.rawMainThreadMilliseconds[0] = 0;
  assert.equal(validate(item), true);
}
rejected((item) => { item.report = null; }, /missing report/i);
rejected(({ report }) => { report.generatedUtc = '2000-01-01T00:00:00Z'; }, /stale/i);
rejected(({ report }) => { report.shots.pop(); }, /shot.*incomplete/i);
rejected(({ report, hashes }) => {
  report.shots[1].sha256 = report.shots[0].sha256;
  hashes[report.shots[1].name] = report.shots[0].sha256;
}, /reused shot/i);
rejected(({ report }) => { report.cli.exitCode = 9; }, /exit/i);
rejected((item) => { item.log += '\nNullReferenceException: after PASS'; }, /after pass/i);
rejected(({ report }) => { report.performance.phases[0].samples = 2; }, /card-motion.*samples/i);
rejected(({ report }) => { report.performance.rawTotalMilliseconds.pop(); }, /raw.*sample/i);
rejected(({ report }) => {
  report.performance.rawMainThreadPresentWaitMilliseconds.pop();
}, /raw.*sample/i);
rejected(({ report }) => { report.performance.p95HalfSpreadPercent = 26; }, /split-half/i);
rejected(({ report }) => { report.host.loadPerCore = 1.01; }, /host load/i);
rejected(({ report }) => { report.environment.applicationIsEditor = true; }, /release player/i);
rejected(({ report }) => { report.environment.scene = 'Boot'; }, /scene/i);
rejected(({ report }) => { report.performance.p95Milliseconds = 34.1; }, /p95/i);
rejected(({ report }) => { report.performance.mainThreadP95Milliseconds = 34.1; }, /main thread/i);
rejected(({ report }) => { report.performance.gpuP95Milliseconds = 20.1; }, /gpu/i);
rejected(({ report }) => { report.performance.p99Milliseconds = 80.1; }, /p99/i);
rejected(({ report }) => { report.performance.maximumMilliseconds = 500.1; }, /maximum/i);
rejected(({ report }) => { report.performance.gcAllocatedBytes = 1; }, /GC/i);
rejected(({ report }) => { report.performance.unavailableFrameTimingSamples = 1; },
  /unavailable.*timing/i);
rejected(({ report }) => { report.performance.mainThreadMeasurement = 'cpu-frame-time'; },
  /main thread measurement/i);
rejected(({ report }) => { report.evidenceKind = 'editor-runtime-backbuffer-not-built-player'; },
  /evidence kind/i);
rejected(({ report }) => { report.directStateMutationDetected = true; }, /direct state/i);
rejected((item) => { item.source += '\ncontroller.ConfirmFocusedAction();'; }, /controller-only/i);
rejected(({ report }) => { report.hiddenTruthLeakageDetected = true; }, /hidden truth/i);
rejected(({ report }) => { report.match.matchCompletionEvents = 2; }, /completion/i);
rejected(({ report }) => { report.match.resultWinner = 'Player'; }, /result winner/i);
rejected(({ report }) => { report.match.rematchStartEvents = 0; }, /rematch/i);
rejected(({ report }) => { report.match.rematchActionCompleted = false; }, /rematch action/i);
rejected(({ report }) => { report.match.quitRequested = false; }, /quit/i);

{
  const items = [1, 2, 3].map((repetition) => {
    const item = fixture();
    item.report.repetition = repetition;
    item.report.shots.forEach((shot) => {
      shot.file = shot.file.replace('rep-1', `rep-${repetition}`);
    });
    item.log = item.log.replace('repetition=1', `repetition=${repetition}`);
    return item;
  });
  assert.equal(validateParlorStandaloneSeries(items.map((item) => item.report)), true);
  assert.throws(() => validateParlorStandaloneSeries(items.slice(0, 2).map((item) => item.report)),
    /three consecutive/i);
  items[2].report.buildSha256 = '9'.repeat(64);
  assert.throws(() => validateParlorStandaloneSeries(items.map((item) => item.report)),
    /same app/i);
}

{
  const reports = [1, 2, 3].map((repetition) => {
    const report = fixture().report;
    report.repetition = repetition;
    return report;
  });
  const coverage = [coverageFixture('honest-baseline').report,
    coverageFixture('honest-hc200').report];
  const series = { appSha256: SHA };
  assert.equal(validateParlorCurrentProvenance(series, reports, coverage, {
    currentSceneSha256: 'b'.repeat(64), currentBuildSha256: SHA,
  }), true);
  assert.throws(() => validateParlorCurrentProvenance(series, reports, coverage, {
    currentSceneSha256: '9'.repeat(64), currentBuildSha256: SHA,
  }), /current Parlor scene/i);
}

console.log('✓ Parlor standalone report validator negative controls');

function coverageFixture(mode = 'honest-baseline') {
  const highContrast = mode === 'honest-hc200';
  const names = REQUIRED_PARLOR_COVERAGE_SHOTS[mode];
  const hashes = Object.fromEntries(names.map((name, index) =>
    [name, `${index + 2}`.repeat(64)]));
  const report = {
    schemaVersion: 1, seedCatalogVersion: 1,
    evidenceKind: 'macos-release-player-parlor-coverage',
    generatedUtc: new Date().toISOString(), coverageMode: mode,
    resolution: { width: 1920, height: 1080 },
    environment: {
      applicationIsEditor: false, batchMode: false, scene: 'Parlor',
      qualityLevel: 'Ultra', renderScale: 1, graphicsDevice: 'Apple M5',
      unityVersion: '6000.5.3f1',
    },
    buildSha256: 'a'.repeat(64), sceneSha256: 'b'.repeat(64),
    contentSha256: 'c'.repeat(64), packageManifestSha256: 'd'.repeat(64),
    controllerOnly: true, directStateMutationDetected: false,
    hiddenTruthLeakageDetected: false,
    seedCase: {
      id: 'honest-false-tell-teeth', seed: 27, tier: 4, suit: 'Teeth',
      expectedCheat: false, expectedTell: 'Suspicious', cheatFamily: 'None',
      decision: 'FalseRead', expectedOutcome: 'FalseReadPenalty',
    },
    shots: names.map((name) => ({
      name, file: `Library/GmSceneIntelligence/parlor-standalone/${mode}/${name}.png`,
      sha256: hashes[name], phase: name.includes('false-read-result')
        ? 'TrickResult' : 'AwaitingAldricJudgement',
      publicStateSha256: name.includes('false-read-result') ? '4'.repeat(64) : '5'.repeat(64),
      cardStateSha256: name.includes('false-read-result') ? '6'.repeat(64) : '7'.repeat(64),
      evidenceSha256: '8'.repeat(64), observedEvidence: ['His hand stopped above the deck.'],
      feedback: name.includes('false-read-result')
        ? 'Aldric was honest. Your false Read costs you.' : '',
      caption: name.includes('caption') ? 'His hand stopped above the deck.' : '',
      uiText: name.includes('false-read-result')
        ? ['Aldric was honest. Your false Read costs you.'] : ['Read the evidence'],
      focusOpen: name.includes('focus'), paused: name.includes('settings'),
    })),
    coverage: {
      falseReadResolved: true, resolvedOutcomeKind: 'FalseReadPenalty',
      canonicalPublicStateSha256: '4'.repeat(64), canonicalCardStateSha256: '6'.repeat(64),
      settingsChangedThroughController: highContrast, preferencesPersisted: highContrast,
      preferencesSha256: highContrast ? '9'.repeat(64) : '',
      accessibility: highContrast ? {
        captions: true, reducedMotion: true, vibration: false,
        monoAudio: true, highContrast: true, textScale: 2,
      } : {
        captions: true, reducedMotion: false, vibration: true,
        monoAudio: false, highContrast: false, textScale: 1,
      },
      ruleStateEvents: 4, outcomeEvents: 1, durableWrites: highContrast ? 7 : 2,
    },
    cli: { exitCode: 0, pid: 123, logPath: 'Player.log', logSha256: 'f'.repeat(64) },
  };
  const log = `[GmParlorStandaloneProbe] COVERAGE PASS mode=${mode}\n` +
    '[GmPlayer] quit requested from pause menu\n';
  return { report, hashes, log, source: 'InputSystem.QueueStateEvent' };
}

for (const mode of ['honest-baseline', 'honest-hc200']) {
  const item = coverageFixture(mode);
  assert.equal(validateParlorCoverageReport(item.report, {
    screenshotHashes: item.hashes, log: item.log, source: item.source,
    now: Date.parse(item.report.generatedUtc) + 1000,
  }), true);
}

{
  const baseline = coverageFixture('honest-baseline');
  const hc200 = coverageFixture('honest-hc200');
  assert.equal(validateParlorCoveragePair(baseline.report, hc200.report), true);
  const missingFalse = structuredClone(baseline.report);
  missingFalse.coverage.falseReadResolved = false;
  assert.throws(() => validateParlorCoverageReport(missingFalse, {
    screenshotHashes: baseline.hashes, log: baseline.log, source: baseline.source,
    now: Date.parse(missingFalse.generatedUtc) + 1000,
  }), /false Read/i);
  const fakeHc = structuredClone(hc200.report);
  fakeHc.coverage.accessibility.highContrast = false;
  assert.throws(() => validateParlorCoverageReport(fakeHc, {
    screenshotHashes: hc200.hashes, log: hc200.log, source: hc200.source,
    now: Date.parse(fakeHc.generatedUtc) + 1000,
  }), /HC200/i);
  const mutated = structuredClone(hc200.report);
  mutated.coverage.canonicalPublicStateSha256 = '0'.repeat(64);
  assert.throws(() => validateParlorCoveragePair(baseline.report, mutated), /canonical result/i);
}

console.log('✓ Parlor built honest/HC200 coverage validator negative controls');
