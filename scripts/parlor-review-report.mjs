import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';

export const REQUIRED_PARLOR_REVIEW_SHOTS = [
  '01-player-hand-ready', '02-empty-host-chair-framing',
  '03-player-lead-and-aldric-follow', '04-aldric-lead-player-follow',
  '05-true-suspicious-contact', '06-false-suspicious-contact',
  '07-focus-true-observed-facts', '08-focus-false-observed-facts',
  '09-correct-read-result', '10-false-read-result', '11-missed-cheat-result',
  '12-locked-read-feedback', '13-late-read-feedback', '14-trick-result',
  '15-round-result', '16-player-match-win', '17-aldric-match-win',
  '18-rematch-ready', '19-pause-journal', '20-settings-default',
  '21-settings-high-contrast-200',
  '22-restore-before', '23-restore-after', '24-restore-focus',
];

const CASES = new Map([
  ['honest-calm-flames', [1, 1, 'Flames', false, 'Calm', 'None', 'Accept']],
  ['cheat-true-tell-eyes', [8, 4, 'Eyes', true, 'Suspicious', 'RenegedWithHeldFlame', 'CorrectRead']],
  ['honest-false-tell-teeth', [27, 4, 'Teeth', false, 'Suspicious', 'None', 'FalseRead']],
  ['cheat-calm-bones', [152, 4, 'Bones', true, 'Calm', 'ImpossibleEighthRank', 'Accept']],
  ['cheat-late-read-flames', [13, 4, 'Flames', true, 'Suspicious', 'ImpossibleEighthRank', 'LateRead']],
  ['honest-locked-read-eyes', [1, 1, 'Eyes', false, 'Calm', 'None', 'LockedRead']],
  ['bones-player-win-rematch', [21, 2, 'Bones', false, 'Calm', 'None', 'Accept']],
  ['aldric-win-restore', [27, 3, 'Flames', true, 'Calm', 'ImpossibleEighthRank', 'Accept']],
]);

const SHOT_CASE = new Map([
  ['05-true-suspicious-contact', 'cheat-true-tell-eyes'],
  ['06-false-suspicious-contact', 'honest-false-tell-teeth'],
  ['07-focus-true-observed-facts', 'cheat-true-tell-eyes'],
  ['08-focus-false-observed-facts', 'honest-false-tell-teeth'],
  ['09-correct-read-result', 'cheat-true-tell-eyes'],
  ['10-false-read-result', 'honest-false-tell-teeth'],
  ['11-missed-cheat-result', 'cheat-calm-bones'],
  ['12-locked-read-feedback', 'honest-locked-read-eyes'],
  ['13-late-read-feedback', 'cheat-late-read-flames'],
  ['16-player-match-win', 'bones-player-win-rematch'],
  ['17-aldric-match-win', 'aldric-win-restore'],
  ['18-rematch-ready', 'bones-player-win-rematch'],
  ['22-restore-before', 'aldric-win-restore'],
  ['23-restore-after', 'aldric-win-restore'],
  ['24-restore-focus', 'aldric-win-restore'],
]);

const SHA = /^[a-f0-9]{64}$/;
const fail = (message) => { throw new Error(`Parlor review report: ${message}`); };

export function validateParlorReviewReport(report, options = {}) {
  if (!report || typeof report !== 'object') fail('missing report');
  if (report.schemaVersion !== 1 || report.seedCatalogVersion !== 1)
    fail('unsupported report or seed catalog version');
  if (report.evidenceKind !== 'editor-runtime-backbuffer-not-built-player')
    fail('evidence kind must state that this is not built-player proof');
  for (const field of ['sceneSha256', 'contentSha256', 'packageManifestSha256'])
    if (!SHA.test(report[field] ?? '')) fail(`${field} is missing or malformed`);
  const generated = Date.parse(report.generatedUtc);
  const age = (options.now ?? Date.now()) - generated;
  if (!Number.isFinite(generated) || age < -60_000 || age > 10 * 60_000)
    fail('report is stale or future-dated');

  if (!Array.isArray(report.cases) || report.cases.length !== CASES.size)
    fail(`case matrix is incomplete: ${report.cases?.length ?? 0}/${CASES.size}`);
  const ids = new Set();
  for (const item of report.cases) {
    if (ids.has(item.id)) fail(`duplicate case ${item.id}`);
    ids.add(item.id);
    const expected = CASES.get(item.id);
    if (!expected) fail(`unknown case ${item.id}`);
    const actual = [item.seed, item.tier, item.suit, item.expectedCheat, item.expectedTell,
      item.cheatFamily, item.decision];
    if (JSON.stringify(actual) !== JSON.stringify(expected))
      fail(`truth or literal seed drift in ${item.id}`);
  }
  if (![...CASES.values()].some((item) => item[5] === 'RenegedWithHeldFlame') ||
      ![...CASES.values()].some((item) => item[5] === 'ImpossibleEighthRank'))
    fail('both cheat families are required');

  if (!Array.isArray(report.shots) || report.shots.length !== REQUIRED_PARLOR_REVIEW_SHOTS.length)
    fail(`shot sequence is incomplete: ${report.shots?.length ?? 0}/${REQUIRED_PARLOR_REVIEW_SHOTS.length}`);
  const screenshotHashes = options.screenshotHashes ?? {};
  const seenHashes = new Set();
  report.shots.forEach((shot, index) => {
    const required = REQUIRED_PARLOR_REVIEW_SHOTS[index];
    if (shot.name !== required) fail(`shot sequence differs at ${index}: ${shot.name}/${required}`);
    if (!SHA.test(shot.sha256 ?? '') || screenshotHashes[shot.name] !== shot.sha256)
      fail(`shot hash mismatch for ${shot.name}`);
    if (seenHashes.has(shot.sha256)) fail(`reused shot bytes for ${shot.name}`);
    seenHashes.add(shot.sha256);
    if (!SHA.test(shot.publicStateSha256 ?? '') ||
        !SHA.test(shot.cardStateSha256 ?? '') || !shot.phase)
      fail(`shot ${shot.name} lacks a live public phase/hash oracle`);
    if (!Array.isArray(shot.observedEvidence) ||
        shot.observedEvidenceCount !== shot.observedEvidence.length)
      fail(`shot ${shot.name} evidence count is not bound to its live visible rows`);
    if (new Set(shot.observedEvidence).size !== shot.observedEvidence.length)
      fail(`shot ${shot.name} displays duplicate observation lines`);
    const expectedCase = SHOT_CASE.get(shot.name) ?? 'honest-calm-flames';
    if (shot.caseId !== expectedCase) fail(`shot ${shot.name} used ${shot.caseId}, expected ${expectedCase}`);
  });

  const byName = new Map(report.shots.map((shot) => [shot.name, shot]));
  for (const name of ['05-true-suspicious-contact', '06-false-suspicious-contact'])
    if (!byName.get(name).captions || !(byName.get(name).activeCaption ?? '').trim())
      fail(`${name} has no player-facing observed cue`);
  for (const name of ['07-focus-true-observed-facts', '08-focus-false-observed-facts'])
    if (!(byName.get(name).activeCaption ?? '').trim())
      fail(`${name} has no live caption bound to the focus evidence frame`);
  for (const name of ['07-focus-true-observed-facts', '08-focus-false-observed-facts']) {
    const shot = byName.get(name);
    if (!shot.focusOpen || shot.observedEvidenceCount < 1)
      fail(`${name} lacks observed focus/evidence`);
  }
  if (!/locked/i.test(byName.get('12-locked-read-feedback').feedback ?? ''))
    fail('locked Read has no public feedback');
  if (!/closed/i.test(byName.get('13-late-read-feedback').feedback ?? ''))
    fail('late Read has no public feedback');
  const correctRead = byName.get('09-correct-read-result').feedback ?? '';
  const falseRead = byName.get('10-false-read-result').feedback ?? '';
  if (!/caught/i.test(correctRead) || !/honest/i.test(falseRead) ||
      correctRead === falseRead || /cheat/i.test(falseRead))
    fail('correct and false Read frames lack distinct truthful result wording');
  if (byName.get('16-player-match-win').phase !== 'MatchResult' ||
      byName.get('17-aldric-match-win').phase !== 'MatchResult')
    fail('match result frames did not complete matches');
  if (byName.get('18-rematch-ready').phase !== 'PlayerLeads') fail('rematch frame is not ready');
  const high = byName.get('21-settings-high-contrast-200');
  if (!high.highContrast || high.textScale !== 2) fail('high contrast / 200% frame is not live settings state');

  const before = byName.get('22-restore-before');
  const after = byName.get('23-restore-after');
  const focus = byName.get('24-restore-focus');
  if (before.focusOpen || after.focusOpen || !focus.focusOpen)
    fail('restore frames do not prove closed/closed/real-input focus semantics');
  if (before.publicStateSha256 !== after.publicStateSha256 ||
      before.publicStateSha256 !== focus.publicStateSha256 ||
      before.cardStateSha256 !== after.cardStateSha256 ||
      before.cardStateSha256 !== focus.cardStateSha256)
    fail('restore frame public/card state hashes do not match');
  const restore = report.restore;
  if (!restore || restore.cardsCompared !== 28) fail('restore verification is missing or incomplete');
  if (!/^Library\/GmSceneIntelligence\/parlor-review-tour\//.test(restore.isolatedSavePath ?? ''))
    fail('restore proof did not use its isolated save path');
  for (const field of ['firstReloadDeltaCount', 'secondReloadDeltaCount', 'cueEvents',
    'evidenceEvents', 'outcomeEvents', 'trickEvents', 'roundEvents', 'matchEvents',
    'runDeltaCount'])
    if (restore[field] !== 0) fail(`restore ${field} must be zero`);
  for (const family of ['PublicState', 'CardState', 'Evidence', 'RunState']) {
    const baseline = restore[`baseline${family}Sha256`];
    const restored = restore[`restored${family}Sha256`];
    const second = restore[`secondReload${family}Sha256`];
    if (!SHA.test(baseline ?? '') || baseline !== restored || baseline !== second)
      fail(`restore ${family} hashes do not match across both reloads`);
  }
  if (before.publicStateSha256 !== restore.baselinePublicStateSha256 ||
      before.cardStateSha256 !== restore.baselineCardStateSha256)
    fail('restore screenshots are not bound to the verified baseline state');

  const forbidden = /TryImport|TrySetParlorMatch|RestoreParlorSnapshotForTransaction|CommitParlor|SnapAvailable|TryRestoreCanonicalState|System\.Reflection|\.Match\.(?:Play|Continue|Read)|ExportSnapshot\(\).*=/;
  if (forbidden.test(options.probeSource ?? '')) fail('forbidden direct mutation/import in probe source');
  if (report.cli?.exitCode !== 0) fail(`Unity exit was ${report.cli?.exitCode ?? 'missing'}`);
  if (!SHA.test(report.cli?.logSha256 ?? '') || !report.cli?.logPath)
    fail('CLI log attribution is missing');
  const log = options.log ?? '';
  const passAt = log.lastIndexOf('[GmSceneReviewTour] TOUR COMPLETE');
  if (passAt < 0 || !log.includes('[GmParlorReviewProbe] RESTORE PASS') ||
      !log.includes('[GmParlorReviewProbe] REPORT 24/24'))
    fail('log is missing review/report completion');
  if (/(?:^|\n)(?:[A-Za-z0-9_.]*Exception:|[^\n]*\bFAILED\b|[^\n]*\bError:)/
      .test(log.slice(passAt + 1)))
    fail('post-pass exception/error found after tour completion');
  return true;
}

export function sha256File(file) {
  return createHash('sha256').update(readFileSync(file)).digest('hex');
}

export function finalizeParlorReviewReport(reportPath, { projectRoot, logPath, exitCode }) {
  const report = JSON.parse(readFileSync(reportPath, 'utf8'));
  report.cli = {
    exitCode,
    logPath: path.relative(projectRoot, logPath),
    logSha256: sha256File(logPath),
  };
  writeFileSync(reportPath, `${JSON.stringify(report, null, 2)}\n`);
  const screenshotHashes = Object.fromEntries(report.shots.map((shot) =>
    [shot.name, sha256File(path.join(projectRoot, shot.file))]));
  return { report, screenshotHashes };
}
