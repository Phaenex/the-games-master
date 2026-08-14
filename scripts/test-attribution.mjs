#!/usr/bin/env node
// Attribution gate. CC-BY permits commercial use ONLY with attribution, so a build that ships the
// gravyart mansion (or any other CC-BY asset) without a credit line is a licence violation, not a
// polish item. This fails on unresolved attribution promises and on CC-BY entries with no author.
//
// It deliberately checks the SOURCE records rather than a rendered credits screen: the screen can
// be rebuilt, but if the underlying record says "credit here when landed" then nobody knows who to
// credit in the first place.

import { readFileSync, existsSync } from 'node:fs';
import path from 'node:path';

// Root override so the gate can be pointed at a fixture and proven to still fail on bad input --
// same escape hatch sync-unity-scenes.mjs already takes with GM_UNITY_PROJECT. A licence check
// nobody can test is a licence check nobody knows still works.
const ROOT = process.env.GM_REPO_ROOT || process.cwd();

const RECORDS = [
  path.join(ROOT, 'assets/models/sourced/CREDITS.txt'),
  path.join(ROOT, 'assets/sfx/license.txt'),
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

// An entry in these records is a bullet item, or a paragraph between blank lines, and its sentences
// wrap onto as many lines as they need. The author name and the policy pointer that legitimately
// excuses a bare CC-BY mention land wherever the sentence reaches — routinely not the line carrying
// the "CC-BY" token itself. Reading that line alone failed the gate on CREDITS.txt:24, a record that
// is in fact complete: exactly the "trains people to ignore it" outcome described above. Bullets and
// blank lines close an entry so one asset's credit can never excuse the next asset's silence.
function entryScopes(lines) {
  const scopes = new Array(lines.length).fill('');
  let start = null;
  const close = (end) => {
    if (start === null) return;
    const text = lines.slice(start, end).join('\n');
    for (let i = start; i < end; i++) scopes[i] = text;
    start = null;
  };
  lines.forEach((line, index) => {
    if (line.trim() === '') { close(index); return; }
    if (/^\s*[-*]\s/.test(line)) close(index);
    if (start === null) start = index;
  });
  close(lines.length);
  return scopes;
}

let failures = 0;
let ccByLines = 0;

for (const record of RECORDS) {
  if (!existsSync(record)) {
    console.log(`  ✗ ${record} is missing — a sourced-asset record cannot be optional`);
    failures++;
    continue;
  }
  const lines = readFileSync(record, 'utf8').split('\n');
  const scopes = entryScopes(lines);
  let pending = false;
  lines.forEach((line, index) => {
    const where = `${record}:${index + 1}`;
    const entry = scopes[index] || line;
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
      // Named credits are read across the entry, but never across a line break: `by` + `[ \t]` keeps
      // "CC-BY\nlow-poly" from manufacturing an author out of a wrapped line. The bare parenthetical
      // stays line-local — "(drive lampposts)" on an entry header is a location note, not a credit.
      const names = /by[ \t]+[A-Z]|"[^"\n]+"[ \t]+by/i.test(entry) || /\(([^)]+)\)/.test(line);
      const isPolicy = /must stay credited|attribution required|see .*license|unless a per-model|license badges/i.test(entry);
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
