#!/usr/bin/env node
// Convert one or more FBX files to GLB via Playwright + Three FBXLoader/GLTFExporter.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright';
import { createServer } from 'node:http';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, '..');

const inputs = process.argv.slice(2);
if (!inputs.length) {
  console.error('Usage: node scripts/fbx-to-glb.mjs <file.fbx> [more.fbx...]');
  console.error('       Writes sibling .glb next to each input (or --out dir as first arg after flags).');
  process.exit(1);
}

let outDir = null;
const files = [];
for (let i = 0; i < inputs.length; i++) {
  if (inputs[i] === '--out') {
    outDir = path.resolve(inputs[++i]);
    continue;
  }
  files.push(path.resolve(inputs[i]));
}
if (outDir) fs.mkdirSync(outDir, { recursive: true });

const PIXEL_PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==',
  'base64',
);

function contentType(p) {
  if (p.endsWith('.html')) return 'text/html';
  if (p.endsWith('.js')) return 'text/javascript';
  if (p.endsWith('.fbx') || p.endsWith('.FBX')) return 'application/octet-stream';
  if (p.endsWith('.png')) return 'image/png';
  if (p.endsWith('.jpg') || p.endsWith('.jpeg')) return 'image/jpeg';
  return 'application/octet-stream';
}

// Index every texture under unity-import once (basename → absolute path).
const TEX_INDEX = new Map();
// Basenames already reported as stubbed, so one missing texture isn't logged per request.
const STUBBED_TEX = new Set();
function buildTexIndex() {
  const importRoot = path.join(root, 'assets/models/unity-import');
  if (!fs.existsSync(importRoot)) return;
  const stack = [importRoot];
  while (stack.length) {
    const dir = stack.pop();
    let ents;
    try { ents = fs.readdirSync(dir, { withFileTypes: true }); } catch { continue; }
    for (const e of ents) {
      const p = path.join(dir, e.name);
      if (e.isDirectory()) {
        if (e.name === 'Library' || e.name === 'node_modules') continue;
        stack.push(p);
      } else if (/\.(png|jpe?g|tga|webp|bmp)$/i.test(e.name)) {
        const key = e.name.toLowerCase();
        if (!TEX_INDEX.has(key)) TEX_INDEX.set(key, p);
      }
    }
  }
  console.log(`Texture index: ${TEX_INDEX.size} files under unity-import`);
}
buildTexIndex();

function resolveTexture(urlPath) {
  const base = path.basename(urlPath);
  if (!/\.(png|jpe?g|tga|webp|bmp)$/i.test(base)) return null;
  const direct = path.join(root, urlPath.replace(/^\//, ''));
  if (direct.startsWith(root) && fs.existsSync(direct) && fs.statSync(direct).isFile()) return direct;
  return TEX_INDEX.get(base.toLowerCase()) || null;
}

const server = createServer((req, res) => {
  const urlPath = decodeURIComponent((req.url || '/').split('?')[0]);
  let fp = path.join(root, urlPath.replace(/^\//, ''));
  if (!fp.startsWith(root) || !fs.existsSync(fp) || fs.statSync(fp).isDirectory()) {
    const tex = resolveTexture(urlPath);
    if (tex) {
      res.writeHead(200, { 'Content-Type': contentType(tex), 'Access-Control-Allow-Origin': '*' });
      res.end(fs.readFileSync(tex));
      return;
    }
    if (/\.(png|jpe?g|tga|webp|bmp)$/i.test(urlPath) || /checkered/i.test(urlPath)) {
      // A 1px stand-in keeps the load alive, but a mesh baked with it is not the mesh
      // anyone asked for — say so once per texture instead of substituting in silence.
      const key = path.basename(urlPath);
      if (!STUBBED_TEX.has(key)) {
        STUBBED_TEX.add(key);
        console.warn(`  ! texture not found, serving 1x1 placeholder: ${key}`);
      }
      res.writeHead(200, { 'Content-Type': 'image/png', 'Access-Control-Allow-Origin': '*' });
      res.end(PIXEL_PNG);
      return;
    }
    res.writeHead(404); res.end('missing'); return;
  }
  res.writeHead(200, { 'Content-Type': contentType(fp), 'Access-Control-Allow-Origin': '*' });
  res.end(fs.readFileSync(fp));
});
await new Promise((r) => server.listen(0, '127.0.0.1', r));
const port = server.address().port;

const PAGE_URL = `http://127.0.0.1:${port}/scripts/fbx-to-glb.html`;
const LIB_HELP = 'the page pulls three.js/FBXLoader/GLTFExporter from cdn.jsdelivr.net, so this converter needs outbound access to that host';

let ok = 0, fail = 0;
const pageErrors = [];
let browser = null;
try {
  browser = await chromium.launch();
  const page = await browser.newPage();
  page.on('console', (m) => {
    if (m.type() === 'error') console.error('[page]', m.text());
  });
  // An uncaught page exception means the conversion ran on a broken page; it must not pass silently.
  page.on('pageerror', (e) => {
    const msg = e && e.message ? e.message : String(e);
    pageErrors.push(msg);
    console.error('[pageerror]', msg);
  });

  try {
    await page.goto(PAGE_URL, { waitUntil: 'networkidle', timeout: 60000 });
  } catch (e) {
    throw new Error(`could not load ${PAGE_URL}: ${e.message || e} — ${LIB_HELP}`);
  }
  // Fail on a named missing library rather than stalling on an unexplained readiness wait.
  await page.waitForFunction(
    () => window.__LIBS_FAILED.length || (window.THREE && window.__FBX2GLB && THREE.FBXLoader && THREE.GLTFExporter),
    null,
    { timeout: 60000 },
  ).catch(() => { throw new Error(`converter page never became ready within 60s — ${LIB_HELP}`); });
  const libsFailed = await page.evaluate(() => window.__LIBS_FAILED);
  if (libsFailed.length) throw new Error(`library load failed (${libsFailed.join(', ')}) — ${LIB_HELP}`);

  for (const fbx of files) {
    if (!fs.existsSync(fbx)) {
      console.error('SKIP missing', fbx);
      fail++;
      continue;
    }
    const rel = path.relative(root, fbx).split(path.sep).join('/');
    const base = path.basename(fbx, path.extname(fbx));
    const dest = outDir
      ? path.join(outDir, base + '.glb')
      : path.join(path.dirname(fbx), base + '.glb');
    process.stdout.write(`→ ${rel} … `);
    const t0 = Date.now();
    try {
      await page.evaluate((url) => window.__FBX2GLB.convert(url), '/' + rel);
      await page.waitForFunction(() => !window.__FBX2GLB.busy, null, { timeout: 180000 });
      const result = await page.evaluate(() => ({
        error: window.__FBX2GLB.error,
        byteLength: window.__FBX2GLB.result && window.__FBX2GLB.result.byteLength,
        meshCount: window.__FBX2GLB.result && window.__FBX2GLB.result.meshCount,
        names: window.__FBX2GLB.result && window.__FBX2GLB.result.names,
        base64: window.__FBX2GLB.result && window.__FBX2GLB.result.base64,
      }));
      if (result.error || !result.base64) throw new Error(result.error || 'no result');
      fs.writeFileSync(dest, Buffer.from(result.base64, 'base64'));
      console.log(`OK ${((Date.now() - t0) / 1000).toFixed(1)}s  ${result.meshCount} meshes  ${(result.byteLength / 1024).toFixed(0)}KB → ${path.relative(root, dest)}`);
      ok++;
    } catch (e) {
      console.log('FAIL', e.message || e);
      fail++;
    }
  }
} catch (e) {
  // Startup failed outright — no file was converted, so the run is a failure, not an empty success.
  console.error('FAIL', e.message || e);
  fail++;
} finally {
  // Without this a thrown startup error left a Chromium process and a bound port behind.
  if (browser) await browser.close().catch(() => {});
  server.close();
  server.closeAllConnections?.();
}

if (STUBBED_TEX.size) console.log(`Stubbed textures: ${STUBBED_TEX.size} (${[...STUBBED_TEX].join(', ')})`);
if (pageErrors.length) console.error(`Uncaught page errors: ${pageErrors.length}`);
console.log(`Done: ${ok} ok, ${fail} fail`);
process.exit(fail || pageErrors.length ? 1 : 0);
