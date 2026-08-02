#!/usr/bin/env node
/**
 * Extract ALL Asset Store .unitypackages into assets/models/unity-import/<slug>/
 * then convert every .fbx / .FBX into assets/models/unity/<slug>/… .glb
 *
 * Usage:
 *   node scripts/unity-bulk-convert.mjs              # extract + convert
 *   node scripts/unity-bulk-convert.mjs --extract-only
 *   node scripts/unity-bulk-convert.mjs --convert-only
 *   node scripts/unity-bulk-convert.mjs --slug alchemist-house
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync, execFileSync } from 'node:child_process';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, '..');
const STORE = path.join(process.env.HOME || '', 'Library/Unity/Asset Store-5.x');
const IMPORT = path.join(root, 'assets/models/unity-import');
const OUT = path.join(root, 'assets/models/unity');
const MANIFEST = path.join(OUT, 'BULK-MANIFEST.md');

const args = process.argv.slice(2);
const EXTRACT_ONLY = args.includes('--extract-only');
const CONVERT_ONLY = args.includes('--convert-only');
const slugFilterIdx = args.indexOf('--slug');
const SLUG_FILTER = slugFilterIdx >= 0 ? args[slugFilterIdx + 1] : null;
const BATCH = Math.max(1, Number(args.find((a, i) => args[i - 1] === '--batch') || 40));

/** Known prior extract folder names → match by package basename */
const LEGACY_SLUG = {
  'victorian interior': 'metalman-victorian-interior',
  'lowpoly - victorian mansion pack': 'lowpoly-victorian-mansion',
  '3d stealth game haunted house': 'stealth-haunted-house',
  'free wood door pack': 'free-wood-door-pack',
  'door free pack aferar': 'door-free-pack-aferar',
  'horror starter pack free': 'horror-starter-free',
  'alchemist house': 'alchemist-house',
};

function slugify(pkgPath) {
  const name = path.basename(pkgPath, '.unitypackage');
  const key = name.toLowerCase().replace(/\s+/g, ' ').trim();
  if (LEGACY_SLUG[key]) return LEGACY_SLUG[key];
  // Prefer short basename slug; prefix publisher only on collision later
  const raw = name.toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 80);
  return raw || 'pack';
}

function findPackages() {
  if (!fs.existsSync(STORE)) {
    console.error('No Asset Store cache at', STORE);
    return [];
  }
  const out = [];
  const stack = [STORE];
  while (stack.length) {
    const dir = stack.pop();
    let ents;
    try { ents = fs.readdirSync(dir, { withFileTypes: true }); } catch { continue; }
    for (const e of ents) {
      const p = path.join(dir, e.name);
      if (e.isDirectory()) stack.push(p);
      else if (e.name.toLowerCase().endsWith('.unitypackage')) out.push(p);
    }
  }
  // Skip nested pipeline packages + editor-only noise? User asked for ALL — keep everything
  // except Unity PackageCache TMP packs that aren't in Asset Store (we don't scan PackageCache).
  out.sort((a, b) => fs.statSync(a).size - fs.statSync(b).size);
  return out;
}

function alreadyExtracted(dir) {
  return fs.existsSync(path.join(dir, '.gm-extracted'));
}

function extractOne(pkg, slug) {
  const dest = path.join(IMPORT, slug);
  if (alreadyExtracted(dest)) {
    console.log(`SKIP extract (done) ${slug}`);
    return { slug, skipped: true };
  }
  // If folder exists with lots of files but no marker (legacy), mark and skip re-extract
  if (fs.existsSync(dest)) {
    const hasFbx = spawnSync('find', [dest, '-iname', '*.fbx', '-print', '-quit'], { encoding: 'utf8' }).stdout.trim();
    if (hasFbx) {
      fs.writeFileSync(path.join(dest, '.gm-extracted'), `legacy\n${pkg}\n`);
      console.log(`SKIP extract (legacy has fbx) ${slug}`);
      return { slug, skipped: true };
    }
  }
  const r = spawnSync(process.execPath, [
    path.join(root, 'scripts/extract-unitypackage.mjs'),
    pkg,
    dest,
  ], { cwd: root, encoding: 'utf8', maxBuffer: 20 * 1024 * 1024 });
  process.stdout.write(r.stdout || '');
  if (r.stderr) process.stderr.write(r.stderr);
  if (r.status !== 0) {
    console.error(`FAIL extract ${slug} status=${r.status}`);
    return { slug, fail: true };
  }
  return { slug, ok: true };
}

function collectFbx(importDir) {
  const list = [];
  const stack = [importDir];
  while (stack.length) {
    const dir = stack.pop();
    let ents;
    try { ents = fs.readdirSync(dir, { withFileTypes: true }); } catch { continue; }
    for (const e of ents) {
      const p = path.join(dir, e.name);
      if (e.isDirectory()) {
        // Don't descend into nested .unitypackage extract of HDRP/STANDARD subpacks endlessly
        if (e.name === 'Packages' && /horrorstarter|hdrp|lwrp/i.test(dir)) continue;
        stack.push(p);
      } else if (/\.fbx$/i.test(e.name)) {
        list.push(p);
      }
    }
  }
  return list;
}

function destGlbFor(fbxAbs, slug) {
  const rel = path.relative(path.join(IMPORT, slug), fbxAbs);
  const noExt = rel.replace(/\.fbx$/i, '') + '.glb';
  return path.join(OUT, slug, noExt);
}

function convertBatch(fbxList) {
  if (!fbxList.length) return { ok: 0, fail: 0, skip: 0 };
  // Filter already converted
  const need = [];
  let skip = 0;
  for (const f of fbxList) {
    // slug is first segment under IMPORT
    const relImp = path.relative(IMPORT, f);
    const slug = relImp.split(path.sep)[0];
    const dest = destGlbFor(f, slug);
    if (fs.existsSync(dest) && fs.statSync(dest).size > 64) {
      skip++;
      continue;
    }
    fs.mkdirSync(path.dirname(dest), { recursive: true });
    need.push({ f, dest, slug });
  }
  if (!need.length) return { ok: 0, fail: 0, skip };

  // Group by slug so --out mirrors tree incorrectly if flat — convert one file at a time
  // with sibling out path via copying: fbx-to-glb writes to --out + basename only.
  // So we convert per-file with temporary --out then move, OR patch to preserve relative.
  // Easiest: call with no --out (writes sibling .glb next to fbx), then move into unity/<slug>/...
  let ok = 0, fail = 0;
  for (let i = 0; i < need.length; i += BATCH) {
    const chunk = need.slice(i, i + BATCH);
    const paths = chunk.map((c) => c.f);
    console.log(`\n=== Convert batch ${i / BATCH + 1} (${chunk.length} files) ===`);
    const r = spawnSync(process.execPath, [
      path.join(root, 'scripts/fbx-to-glb.mjs'),
      ...paths,
    ], { cwd: root, encoding: 'utf8', maxBuffer: 80 * 1024 * 1024, stdio: ['ignore', 'pipe', 'pipe'] });
    process.stdout.write(r.stdout || '');
    if (r.stderr) process.stderr.write(r.stderr);

    // Move sibling .glb next to each fbx into OUT tree
    for (const c of chunk) {
      const sibling = c.f.replace(/\.fbx$/i, '.glb');
      if (fs.existsSync(sibling) && fs.statSync(sibling).size > 64) {
        fs.mkdirSync(path.dirname(c.dest), { recursive: true });
        fs.renameSync(sibling, c.dest);
        ok++;
      } else {
        fail++;
      }
    }
  }
  return { ok, fail, skip };
}

function writeManifest(packages, stats) {
  const lines = [];
  lines.push('# Unity bulk convert manifest');
  lines.push('');
  lines.push(`Generated: ${new Date().toISOString()}`);
  lines.push(`Packages scanned: ${packages.length}`);
  lines.push(`Extract ok/skip/fail: ${stats.extractOk}/${stats.extractSkip}/${stats.extractFail}`);
  lines.push(`FBX convert ok/skip/fail: ${stats.convertOk}/${stats.convertSkip}/${stats.convertFail}`);
  lines.push('');
  lines.push('| Slug | Package | Size MB | Extracted | FBX | GLB |');
  lines.push('|------|---------|---------|-----------|-----|-----|');
  for (const p of packages) {
    const slug = slugify(p);
    const mb = (fs.statSync(p).size / (1024 * 1024)).toFixed(0);
    const imp = path.join(IMPORT, slug);
    const out = path.join(OUT, slug);
    const extracted = alreadyExtracted(imp) ? 'yes' : 'no';
    const fbxN = fs.existsSync(imp)
      ? spawnSync('find', [imp, '-iname', '*.fbx'], { encoding: 'utf8' }).stdout.trim().split('\n').filter(Boolean).length
      : 0;
    const glbN = fs.existsSync(out)
      ? spawnSync('find', [out, '-iname', '*.glb'], { encoding: 'utf8' }).stdout.trim().split('\n').filter(Boolean).length
      : 0;
    lines.push(`| \`${slug}\` | ${path.basename(p)} | ${mb} | ${extracted} | ${fbxN} | ${glbN} |`);
  }
  fs.mkdirSync(OUT, { recursive: true });
  fs.writeFileSync(MANIFEST, lines.join('\n') + '\n');
  console.log('\nWrote', path.relative(root, MANIFEST));
}

// --- main ---
let packages = findPackages();
if (SLUG_FILTER) {
  packages = packages.filter((p) => slugify(p) === SLUG_FILTER || slugify(p).includes(SLUG_FILTER));
  if (!packages.length) {
    console.error('No package matched --slug', SLUG_FILTER);
    process.exit(1);
  }
}
console.log(`Found ${packages.length} Asset Store packages`);
console.log(`Import → ${IMPORT}`);
console.log(`GLB out → ${OUT}`);

const stats = {
  extractOk: 0, extractSkip: 0, extractFail: 0,
  convertOk: 0, convertSkip: 0, convertFail: 0,
};

if (!CONVERT_ONLY) {
  for (const pkg of packages) {
    const slug = slugify(pkg);
    const r = extractOne(pkg, slug);
    if (r.fail) stats.extractFail++;
    else if (r.skipped) stats.extractSkip++;
    else stats.extractOk++;
  }
}

if (!EXTRACT_ONLY) {
  // Also convert already-imported legacy slugs not from Asset Store naming
  const slugDirs = fs.existsSync(IMPORT)
    ? fs.readdirSync(IMPORT, { withFileTypes: true }).filter((d) => d.isDirectory()).map((d) => d.name)
    : [];
  const want = SLUG_FILTER
    ? slugDirs.filter((s) => s === SLUG_FILTER || s.includes(SLUG_FILTER))
    : slugDirs;

  const allFbx = [];
  for (const slug of want) {
    const list = collectFbx(path.join(IMPORT, slug));
    console.log(`${slug}: ${list.length} fbx`);
    allFbx.push(...list);
  }
  console.log(`Total FBX to consider: ${allFbx.length}`);
  const cr = convertBatch(allFbx);
  stats.convertOk += cr.ok;
  stats.convertFail += cr.fail;
  stats.convertSkip += cr.skip;
}

writeManifest(packages.length ? packages : findPackages(), stats);
console.log('\nSTATS', stats);
process.exit(stats.extractFail || stats.convertFail ? 0 : 0); // don't fail hard — keep going was the point
