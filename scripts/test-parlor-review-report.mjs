import assert from 'node:assert/strict';
import {
  REQUIRED_PARLOR_REVIEW_SHOTS,
  validateParlorReviewReport,
} from './parlor-review-report.mjs';

assert.deepEqual(REQUIRED_PARLOR_REVIEW_SHOTS.slice(-3),
  ['22-restore-before', '23-restore-after', '24-restore-focus']);

const canonicalCases = [
  ['honest-calm-flames', 1, 1, 'Flames', false, 'Calm', 'None', 'Accept'],
  ['cheat-true-tell-eyes', 8, 4, 'Eyes', true, 'Suspicious', 'RenegedWithHeldFlame', 'CorrectRead'],
  ['honest-false-tell-teeth', 27, 4, 'Teeth', false, 'Suspicious', 'None', 'FalseRead'],
  ['cheat-calm-bones', 152, 4, 'Bones', true, 'Calm', 'ImpossibleEighthRank', 'Accept'],
  ['cheat-late-read-flames', 13, 4, 'Flames', true, 'Suspicious', 'ImpossibleEighthRank', 'LateRead'],
  ['honest-locked-read-eyes', 1, 1, 'Eyes', false, 'Calm', 'None', 'LockedRead'],
  ['bones-player-win-rematch', 21, 2, 'Bones', false, 'Calm', 'None', 'Accept'],
  ['aldric-win-restore', 27, 3, 'Flames', true, 'Calm', 'ImpossibleEighthRank', 'Accept'],
].map(([id, seed, tier, suit, expectedCheat, expectedTell, cheatFamily, decision]) => ({
  id, seed, tier, suit, expectedCheat, expectedTell, cheatFamily, decision,
}));

const shotCases = {
  '05-true-suspicious-contact': 'cheat-true-tell-eyes',
  '06-false-suspicious-contact': 'honest-false-tell-teeth',
  '07-focus-true-observed-facts': 'cheat-true-tell-eyes',
  '08-focus-false-observed-facts': 'honest-false-tell-teeth',
  '09-correct-read-result': 'cheat-true-tell-eyes',
  '10-false-read-result': 'honest-false-tell-teeth',
  '11-missed-cheat-result': 'cheat-calm-bones',
  '12-locked-read-feedback': 'honest-locked-read-eyes',
  '13-late-read-feedback': 'cheat-late-read-flames',
  '16-player-match-win': 'bones-player-win-rematch',
  '17-aldric-match-win': 'aldric-win-restore',
  '18-rematch-ready': 'bones-player-win-rematch',
  '22-restore-before': 'aldric-win-restore',
  '23-restore-after': 'aldric-win-restore',
  '24-restore-focus': 'aldric-win-restore',
};

function fixture() {
  const hashes = Object.fromEntries(REQUIRED_PARLOR_REVIEW_SHOTS.map((name, index) =>
    [name, `${(index + 1).toString(16).padStart(64, '0')}`]));
  return {
    report: {
      schemaVersion: 1,
      seedCatalogVersion: 1,
      evidenceKind: 'editor-runtime-backbuffer-not-built-player',
      generatedUtc: new Date().toISOString(),
      sceneSha256: 'a'.repeat(64), contentSha256: 'b'.repeat(64),
      packageManifestSha256: 'c'.repeat(64),
      cases: canonicalCases.map((item) => ({ ...item })),
      shots: REQUIRED_PARLOR_REVIEW_SHOTS.map((name) => ({
        name, file: `Screens/Parlor/tour-${name}.png`, sha256: hashes[name],
        caseId: shotCases[name] ?? 'honest-calm-flames',
        phase: name.includes('match-win') ? 'MatchResult' : 'PlayerLeads',
        publicStateSha256: 'd'.repeat(64), cardStateSha256: 'f'.repeat(64),
        observedTell: 'Calm',
        focusOpen: name.includes('focus'), observedEvidenceCount: name.includes('focus') ? 1 : 0,
        observedEvidence: name.includes('focus') ? ['Observed contact.'] : [],
        captions: name.includes('suspicious-contact'), highContrast: name.includes('high-contrast'),
        activeCaption: /(?:suspicious-contact|focus-(?:true|false))/.test(name)
          ? 'Observed contact.' : '',
        textScale: name.includes('200') ? 2 : 1,
        feedback: name.includes('correct-read') ? 'You caught Aldric cheating.' :
          name.includes('false-read') ? 'Aldric was honest.' :
          name.includes('locked-read') ? 'The Read is locked.' :
          name.includes('late-read') ? 'The Read window has closed.' : '',
      })),
      restore: {
        cardsCompared: 28, firstReloadDeltaCount: 0, secondReloadDeltaCount: 0,
        cueEvents: 0, evidenceEvents: 0, outcomeEvents: 0, trickEvents: 0,
        roundEvents: 0, matchEvents: 0, runDeltaCount: 0,
        isolatedSavePath: 'Library/GmSceneIntelligence/parlor-review-tour/run-save.json',
        baselinePublicStateSha256: 'd'.repeat(64),
        restoredPublicStateSha256: 'd'.repeat(64),
        secondReloadPublicStateSha256: 'd'.repeat(64),
        baselineCardStateSha256: 'f'.repeat(64),
        restoredCardStateSha256: 'f'.repeat(64),
        secondReloadCardStateSha256: 'f'.repeat(64),
        baselineEvidenceSha256: '1'.repeat(64),
        restoredEvidenceSha256: '1'.repeat(64),
        secondReloadEvidenceSha256: '1'.repeat(64),
        baselineRunStateSha256: '2'.repeat(64),
        restoredRunStateSha256: '2'.repeat(64),
        secondReloadRunStateSha256: '2'.repeat(64),
      },
      cli: { exitCode: 0, logPath: 'Logs/cli-parlor-tour.log', logSha256: 'e'.repeat(64) },
    },
    hashes,
    source: 'controller.CallRead(); controller.ConfirmFocusedAction();',
    log: '[GmParlorReviewProbe] RESTORE PASS\n[GmParlorReviewProbe] REPORT 24/24\n' +
      '[GmSceneReviewTour] TOUR COMPLETE 24/24',
  };
}

function rejected(mutator, pattern) {
  const item = fixture();
  mutator(item);
  assert.throws(() => validateParlorReviewReport(item.report, {
    screenshotHashes: item.hashes, probeSource: item.source, log: item.log,
  }), pattern);
}

{
  const item = fixture();
  assert.equal(validateParlorReviewReport(item.report, {
    screenshotHashes: item.hashes, probeSource: item.source, log: item.log,
  }), true);
}
rejected(({ report }) => report.cases.pop(), /case matrix/i);
rejected((item) => { item.report = null; }, /missing report/i);
rejected(({ report }) => { report.shots[0].name = report.shots[1].name; }, /shot sequence|duplicate/i);
rejected(({ report, hashes }) => {
  report.shots[0].sha256 = report.shots[1].sha256;
  hashes[report.shots[0].name] = report.shots[1].sha256;
}, /reused shot/i);
rejected(({ report }) => { report.cases[1].expectedCheat = false; }, /truth/i);
rejected(({ report }) => {
  const shot = report.shots.find((item) => item.name === '07-focus-true-observed-facts');
  shot.observedEvidence.push(shot.observedEvidence[0]);
  shot.observedEvidenceCount++;
}, /duplicate observation/i);
rejected(({ report }) => {
  report.shots.find((item) => item.name === '10-false-read-result').feedback =
    'Your Read was accepted.';
}, /distinct truthful/i);
rejected(({ report }) => {
  report.shots.find((shot) => shot.name === '06-false-suspicious-contact').activeCaption = '';
}, /player-facing observed cue/i);
rejected(({ report }) => { report.generatedUtc = '2000-01-01T00:00:00.000Z'; }, /stale/i);
rejected(({ report }) => { report.shots.pop(); }, /shot sequence/i);
rejected((item) => { item.source = 'GmRunStore.TrySetParlorMatch(snapshot, out error);'; }, /direct mutation|forbidden/i);
rejected((item) => { item.source = 'binder.SnapAvailable(snapshot);'; }, /direct mutation|forbidden/i);
rejected(({ report }) => { report.restore.isolatedSavePath = 'normal-save.json'; }, /isolated save/i);
rejected(({ report }) => { report.cli.exitCode = 1; }, /exit/i);
rejected(({ hashes }) => { hashes[REQUIRED_PARLOR_REVIEW_SHOTS[0]] = 'f'.repeat(64); }, /hash/i);
rejected((item) => { item.log += '\nNullReferenceException: after pass'; }, /after.*pass|post-pass/i);
rejected(({ report }) => { delete report.restore; }, /restore verification/i);
rejected(({ report }) => { report.restore.secondReloadDeltaCount = 1; }, /secondReloadDeltaCount/i);
rejected(({ report }) => {
  report.shots.find((shot) => shot.name === '23-restore-after').publicStateSha256 = '3'.repeat(64);
}, /restore frame.*hash/i);
rejected(({ report, hashes }) => {
  const before = report.shots.find((shot) => shot.name === '22-restore-before');
  const after = report.shots.find((shot) => shot.name === '23-restore-after');
  after.sha256 = before.sha256;
  hashes[after.name] = before.sha256;
}, /reused shot/i);

console.log('✓ parlor review report validator negative controls');
