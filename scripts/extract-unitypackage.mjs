#!/usr/bin/env node
// Extract a .unitypackage (gzip tar of guid/{pathname,asset,asset.meta}) into a folder
// with real relative paths restored. Streams decompression so multi-GB packs don't OOM.
import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';
import { execFileSync, spawnSync } from 'node:child_process';
import { pipeline } from 'node:stream/promises';

const pkg = process.argv[2];
const outRoot = process.argv[3] || path.join(process.cwd(), 'assets/models/unity-import', path.basename(pkg, '.unitypackage').replace(/[^\w.-]+/g, '_'));
if (!pkg) {
  console.error('Usage: node scripts/extract-unitypackage.mjs <file.unitypackage> [outdir]');
  process.exit(1);
}
if (!fs.existsSync(pkg)) {
  console.error('Missing package:', pkg);
  process.exit(1);
}
fs.mkdirSync(outRoot, { recursive: true });
const tmp = fs.mkdtempSync(path.join('/tmp', 'upkg-'));
const tarPath = path.join(tmp, 'pkg.tar');

const sizeMb = (fs.statSync(pkg).size / (1024 * 1024)).toFixed(0);
process.stdout.write(`Extracting ${path.basename(pkg)} (${sizeMb} MB) → ${outRoot} … `);

// Stream gunzip → tar file (avoids loading whole archive into RAM)
await pipeline(fs.createReadStream(pkg), zlib.createGunzip(), fs.createWriteStream(tarPath));
execFileSync('tar', ['-xf', tarPath, '-C', tmp], { stdio: 'ignore' });

const guids = fs.readdirSync(tmp).filter((d) => {
  try { return fs.statSync(path.join(tmp, d)).isDirectory(); } catch { return false; }
});
let n = 0;
for (const g of guids) {
  const dir = path.join(tmp, g);
  const pnFile = path.join(dir, 'pathname');
  const assetFile = path.join(dir, 'asset');
  if (!fs.existsSync(pnFile) || !fs.existsSync(assetFile)) continue;
  // pathname is one line; some packages append junk like "\n00" after the path
  let rel = fs.readFileSync(pnFile, 'utf8').replace(/\0/g, '').split(/\r?\n/)[0].trim();
  rel = rel.replace(/^Assets\//, '');
  if (!rel || rel.includes('..')) continue;
  const dest = path.join(outRoot, rel);
  fs.mkdirSync(path.dirname(dest), { recursive: true });
  fs.copyFileSync(assetFile, dest);
  const metaSrc = path.join(dir, 'asset.meta');
  if (fs.existsSync(metaSrc)) fs.copyFileSync(metaSrc, dest + '.meta');
  n++;
}
fs.rmSync(tmp, { recursive: true, force: true });
fs.writeFileSync(path.join(outRoot, '.gm-extracted'), `${new Date().toISOString()}\n${pkg}\n${n}\n`);
console.log(`${n} assets`);
