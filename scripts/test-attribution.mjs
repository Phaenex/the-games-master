#!/usr/bin/env node
// Attribution gate. CC-BY permits commercial use ONLY with attribution, so a build that ships the
// gravyart mansion (or any other CC-BY asset) without a credit line is a licence violation, not a
// polish item. This fails on unresolved attribution promises and on CC-BY entries with no author.
//
// It deliberately checks the SOURCE records rather than a rendered credits screen: the screen can
// be rebuilt, but if the underlying record says "credit here when landed" then nobody knows who to
// credit in the first place.

import { readFileSync, existsSync } from 'node:fs';

const RECORDS = [
  'assets/models/sourced/CREDITS.txt',
  'assets/sfx/license.txt',
];

// Phrases that mean "attribution is still owed". Found in a live record on 2026-08-04.
const UNRESOLVED = [
  /credit here when landed/i,
  /\bTODO\b/,
  /\bTBD\b/,
  /credit pending/i,
  /attribution pending/i,
  /license unknown/i,
];

let failures = 0;
let ccByLines = 0;

for (const record of RECORDS) {
  if (!existsSync(record)) {
    console.log(`  ✗ ${record} is missing — a sourced-asset record cannot be optional`);
    failures++;
    continue;
  }
  const lines = readFileSync(record, 'utf8').split('\n');
  let pending = false;
  lines.forEach((line, index) => {
    const where = `${record}:${index + 1}`;
    // A PENDING block describes an asset that was never acquired — the Court gavel's CDN fetch
    // failed and the record says "do not invent a file". No credit is owed for something not in
    // the game. A gate that fails on those trains people to ignore it, which is worse than no gate.
    if (/^\s*PENDING\b/i.test(line)) { pending = true; return; }
    if (pending && /^\S/.test(line)) pending = false;
    if (pending) return;
    for (const pattern of UNRESOLVED) {
      if (pattern.test(line)) {
        console.log(`  ✗ ${where} attribution still owed: ${line.trim().slice(0, 90)}`);
        failures++;
        return;
      }
    }
    // A CC-BY line has to name somebody. "CC-BY" alone credits no one.
    if (/CC[-\s]?BY/i.test(line)) {
      ccByLines++;
      const names = /by\s+[A-Z]|\(([^)]+)\)|"[^"]+"\s+by/i.test(line);
      const isPolicy = /must stay credited|attribution required|see .*license|unless a per-model|license badges/i.test(line);
      if (!names && !isPolicy) {
        console.log(`  ✗ ${where} CC-BY entry names no author: ${line.trim().slice(0, 90)}`);
        failures++;
      }
    }
  });
}

console.log(failures === 0
  ? `✓ attribution: ${ccByLines} CC-BY entr(ies) accounted for, none outstanding`
  : `✗ attribution: ${failures} unresolved credit(s) — shipping these is a licence violation`);
process.exit(failures === 0 ? 0 : 1);
