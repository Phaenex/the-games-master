#!/usr/bin/env node

// Baseline/inventory probe for the opening visual rebuild. It deliberately reports
// suspicious material and group counts without pretending those are a visual pass.
import { chromium } from 'playwright';
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { extname, join } from 'node:path';

const root = process.cwd();
const server = createServer(async (req, res) => {
  try {
    const requestPath = decodeURIComponent((req.url || '/').split('?')[0]);
    const file = join(root, requestPath === '/' ? 'index.html' : requestPath);
    const body = await readFile(file);
    const type = { '.html': 'text/html', '.js': 'text/javascript', '.glb': 'model/gltf-binary', '.gltf': 'model/gltf+json', '.bin': 'application/octet-stream', '.png': 'image/png', '.jpg': 'image/jpeg', '.webp': 'image/webp', '.ogg': 'audio/ogg' }[extname(file)] || 'application/octet-stream';
    res.writeHead(200, { 'Content-Type': type });
    res.end(body);
  } catch {
    res.writeHead(404);
    res.end('not found');
  }
});
await new Promise((done) => server.listen(0, '127.0.0.1', done));
const port = server.address().port;

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
const errors = [];
const failed = [];
page.on('pageerror', (error) => errors.push(error.message));
page.on('requestfailed', (request) => failed.push(`${request.url()} :: ${request.failure()?.errorText || 'failed'}`));
await page.goto(`http://127.0.0.1:${port}/The%20Games%20Master%20-%20Prologue.dc.html`, { waitUntil: 'load', timeout: 120000 });
await page.waitForFunction(() => window.__GMC?.scene && window.__GMC?.mansionModel, null, { timeout: 120000 });
await page.waitForFunction(() => window.__GMC?._leartesTreesReady && window.__GMC?._leartesGroundsReady, null, { timeout: 120000 });

const report = await page.evaluate(() => {
  const C = window.__GMC;
  const THREE = window.THREE;
  const groups = {};
  const meshes = [];
  const bright = [];
  const mansionBright = [];
  C.scene.traverse((object) => {
    const kind = object.userData?.gmKind || '(untagged)';
    groups[kind] = (groups[kind] || 0) + 1;
    if (!object.isMesh || !object.visible) return;
    const materials = Array.isArray(object.material) ? object.material : [object.material];
    const colors = materials.filter(Boolean).map((material) => material.color ? material.color.getHexString() : null).filter(Boolean);
    const avg = materials.length ? materials.reduce((sum, material) => sum + (material?.color ? (material.color.r + material.color.g + material.color.b) / 3 : 0), 0) / materials.length : 0;
    const name = `${object.name || ''} ${materials.map((material) => material?.name || '').join(' ')}`.toLowerCase();
    const ancestry = [];
    for (let parent = object; parent && ancestry.length < 5; parent = parent.parent) ancestry.push(parent.name || parent.userData?.gmKind || '(unnamed)');
    const entry = { name: object.name || '(unnamed)', ancestry, kind, colors: colors.slice(0, 4), avg: Number(avg.toFixed(3)), map: materials.some((material) => !!material?.map) };
    meshes.push(entry);
    if (avg > 0.52 && !/window|glass|pane|light|lamp|emissive/.test(name)) bright.push(entry);
    if (C.mansionModel?.getObjectById(object.id) && avg > 0.35) mansionBright.push(entry);
  });
  return {
    snapshot: C.devSnapshot(),
    stateWarm: C.state?.warm ?? null,
    warmOverlay: document.querySelector('[style*="radial-gradient"]')?.style.opacity ?? null,
    sceneChildren: C.scene.children.length,
    meshCount: meshes.length,
    renderer: C.renderer?.info?.render ? { calls: C.renderer.info.render.calls, triangles: C.renderer.info.render.triangles, geometries: C.renderer.info.memory.geometries, textures: C.renderer.info.memory.textures } : null,
    groups,
    brightNonWindow: bright.slice(0, 80),
    brightNonWindowCount: bright.length,
    mansionBright: mansionBright.slice(0, 100),
    meshSample: meshes.slice(0, 25),
  };
});

console.log(JSON.stringify({ errors, failedRequests: failed.slice(0, 40), report }, null, 2));
await browser.close();
server.close();
process.exit(errors.length || failed.length ? 1 : 0);
