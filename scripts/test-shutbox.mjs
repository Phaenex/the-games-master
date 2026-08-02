#!/usr/bin/env node
// Pure-Node unit tests for gm-shutbox-logic.js (no Playwright, no Three).
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const require = createRequire(import.meta.url);
const ROOT = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
const SB = require(path.join(ROOT, 'gm-shutbox-logic.js'));

let passed = 0, failed = 0;
function assert(name, cond, detail) {
  if (cond) { passed++; console.log('✓', name); }
  else { failed++; console.log('✗', name, detail || ''); }
}

// fresh box
{
  const b = SB.freshBox();
  assert('freshBox opens 1–9', SB.TILES.every((n) => b.open[n] === true));
  assert('freshBox sum 45', b.sum === 45 && SB.openSum(b) === 45);
}

// legalMoves for roll 7
{
  const b = SB.freshBox();
  const moves = SB.legalMoves(b, 7);
  const keys = moves.map((m) => m.join(',')).sort();
  assert('legalMoves(7) includes [7]', keys.includes('7'));
  assert('legalMoves(7) includes [1,6]', keys.includes('1,6'));
  assert('legalMoves(7) includes [2,5]', keys.includes('2,5'));
  assert('legalMoves(7) includes [3,4]', keys.includes('3,4'));
  assert('legalMoves(7) includes [1,2,4]', keys.includes('1,2,4'));
  assert('no illegal 7+something', !keys.some((k) => k.split(',').map(Number).reduce((a, c) => a + c, 0) !== 7));
}

// applyMove
{
  const b = SB.freshBox();
  const r = SB.applyMove(b, [1, 6]);
  assert('applyMove [1,6] ok', r.ok === true && r.sum === 38);
  assert('tiles shut', b.open[1] === false && b.open[6] === false && b.open[2] === true);
  const bad = SB.applyMove(b, [1]);
  assert('cannot re-shut', bad.ok === false && bad.reason === 'tile-shut');
}

// pickLegalMove greedy
{
  const b = SB.freshBox();
  const pick = SB.pickLegalMove(b, 9);
  assert('pickLegalMove returns array', Array.isArray(pick) && pick.length >= 1);
  assert('pick sums to 9', pick.reduce((a, c) => a + c, 0) === 9);
  // shutting 9 alone leaves sum 36; shutting 1+8 also 36 — either is fine for greedy-on-sum
  const trial = { open: Object.assign({}, b.open), locked: false };
  SB.applyMove(trial, pick);
  assert('greedy leaves open sum <= 36', SB.openSum(trial) <= 36);
}

// Hold: palm
{
  const hit = SB.evaluateHold('palm', {}, { didPalm: true });
  const miss = SB.evaluateHold('palm', {}, { didPalm: false });
  assert('Hold palm correct', hit.correct && !hit.penalty);
  assert('Hold palm false = penalty', !miss.correct && miss.penalty);
}

// Hold: falseCall
{
  const legal = SB.legalMoves(SB.freshBox(), 6);
  const badCall = SB.evaluateHold('falseCall', { tiles: [5] }, { legalForRoll: legal });
  const goodAnn = SB.evaluateHold('falseCall', { tiles: [6] }, { legalForRoll: legal });
  assert('falseCall catch on illegal shut', badCall.correct && !badCall.penalty);
  assert('falseCall miss when announced legal', !goodAnn.correct && goodAnn.penalty);
}

// Hold: tamper + tile 9 door
{
  const door = SB.evaluateHold('tamper', { tile: 9 }, { tile: 9, wasShut: true, isOpenNow: true });
  const noDoor = SB.evaluateHold('tamper', { tile: 3 }, { tile: 3, wasShut: true, isOpenNow: true });
  const fake = SB.evaluateHold('tamper', { tile: 9 }, { tile: 9, wasShut: false, isOpenNow: true });
  assert('tile-9 Hold opens door', door.correct && door.opensHiddenDoor === true);
  assert('other tile Hold no door', noDoor.correct && !noDoor.opensHiddenDoor);
  assert('fake tamper penalties', !fake.correct && fake.penalty);
}

// ledger map
assert('guestForTile(1)=Marr', SB.guestForTile(1) === 'Marr');
assert('guestForTile(9)=Percival', SB.guestForTile(9) === 'Percival');

console.log(`\nShut the Box logic: ${passed} passed, ${failed} failed`);
process.exitCode = failed ? 1 : 0;
