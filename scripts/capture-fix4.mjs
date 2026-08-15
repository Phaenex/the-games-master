#!/usr/bin/env node
/** Visual gate for the four polish fixes: car clear, hall frames, court/stb light, env porch. */
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';

const ROOT = process.cwd(), PORT = 3840, OUT = 'docs/playtest/screenshots';
const MIME = {
  '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript',
  '.json': 'application/json', '.glb': 'model/gltf-binary', '.gltf': 'model/gltf+json',
  '.bin': 'application/octet-stream', '.png': 'image/png', '.jpg': 'image/jpeg',
  '.ogg': 'audio/ogg', '.css': 'text/css',
};
const server = createServer(async (req, res) => {
  try {
    let p = decodeURIComponent(req.url.split('?')[0]);
    if (p === '/') p = '/index.html';
    const buf = await readFile(join(ROOT, p));
    res.writeHead(200, { 'Content-Type': MIME[extname(p)] || 'application/octet-stream' });
    res.end(buf);
  } catch { res.writeHead(404); res.end('nf'); }
});
await new Promise((r) => server.listen(PORT, r));
let browser = null, pass = false;
const errs = [];
try {
browser = await chromium.launch();
// Every page gets an error listener — a scene that throws on load otherwise shoots the same
// filenames as a clean run and reports nothing.
const openPage = async (label) => {
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  page.on('pageerror', (e) => { errs.push(`${label}: ${e.message}`); console.log('[pageerror]', label, e.message); });
  return page;
};
const shot = async (page, name) => {
  try {
    await page.locator('canvas').first().screenshot({ path: `${OUT}/${name}.png`, timeout: 30000 });
    console.log('shot', name);
  } catch (e) {
    // No canvas to shoot means the scene never came up. Keep a full-page frame as evidence,
    // but never let it pass for a clean canvas capture.
    errs.push(`${name}: canvas screenshot failed — ${e.message}`);
    console.log('FAIL shot', name, '—', e.message);
    await page.screenshot({ path: `${OUT}/${name}.png` }).catch(() => {});
  }
};

// Car keep-clear + Env porch
{
  const page = await openPage('prologue');
  await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Prologue.dc.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__GMC?.carModel && window.__GMC?.mansionModel, null, { timeout: 120000 });
  // Env lamp is 8.7MB — wait for it (or bracket after long timeout). fix-02 shoots the porch,
  // so an unbuilt fixture is a failed gate, not a silent shrug.
  const porchOK = await page.waitForFunction(() => window.__GMC?._porchSconcesBuilt === 'env-lamp' || window.__GMC?._porchSconcesBuilt === 'bracket', null, { timeout: 90000 }).then(() => true).catch(() => false);
  console.log(`porch sconces built: ${porchOK ? 'PASS' : 'FAIL'}`);
  if (!porchOK) errs.push('prologue: porch sconces never built (env-lamp|bracket)');
  await page.waitForTimeout(2800);
  const info = await page.evaluate(() => {
    const C = window.__GMC;
    window.__GM.goTo('walk');
    C.setState({ hintOn: false, beatOn: false, tutStep: 99, fade: 0, cinematic: false });
    const box = new THREE.Box3().setFromObject(C.carModel);
    const pad = new THREE.Box3(
      new THREE.Vector3(box.min.x - 2.4, -0.5, box.min.z - 3.2),
      new THREE.Vector3(box.max.x + 2.4, 2.4, box.max.z + 2.8)
    );
    let clutter = 0;
    const wp = new THREE.Vector3();
    C.scene.traverse((o) => {
      if (!o.isMesh) return;
      if (C.carModel.getObjectById(o.id) || o === C.carModel) return;
      o.getWorldPosition(wp);
      if (!pad.containsPoint(wp)) return;
      const kind = o.userData?.gmKind;
      if (kind === 'outerPier' || kind === 'outerWall' || kind === 'carDress') return;
      if (kind === 'rock' || kind === 'tuft' || kind === 'litter' || kind === 'mound' || kind === 'log' || wp.y < 1.2) clutter++;
    });
    return { porch: C._porchSconcesBuilt, cols: !!C._envPorchCols, clutter, killed: C._carClearKilled || 0 };
  });
  console.log('prologue', info);
  await page.evaluate(() => {
    const C = window.__GMC;
    const box = new THREE.Box3().setFromObject(C.carModel);
    const cx = (box.min.x + box.max.x) / 2;
    const frontZ = box.min.z;
    C.walkEnabled = false;
    C.cam.position.set(cx - 3.8, 1.55, frontZ - 2.8);
    C.yaw = -2.45; C.pitch = 0.05;
    C.cam.rotation.set(C.pitch, C.yaw, 0, 'YXZ');
    if (C._raf) { cancelAnimationFrame(C._raf); C._raf = null; }
    C.renderer.render(C.scene, C.cam);
  });
  await shot(page, 'fix-01-car-clear');
  await page.evaluate(() => {
    const C = window.__GMC;
    C.cam.position.set(0, 2.15, -46.5);
    C.yaw = 0; C.pitch = 0.08;
    C.cam.rotation.set(0.08, 0, 0, 'YXZ');
    C.renderer.render(C.scene, C.cam);
  });
  await shot(page, 'fix-02-porch-env');
  await page.close();
}

// Hall frames
{
  const page = await openPage('entry-hall');
  await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Entry%20Hall.dc.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__GMC?.scene, null, { timeout: 60000 });
  await page.evaluate(() => {
    const C = window.__GMC;
    if (C._t0) { clearTimeout(C._t0); C._t0 = null; }
    window.__GM.goTo('hall');
    C.walkEnabled = false;
    C._beat = 99;
    (C.pois || []).forEach((p) => { p.said = true; });
    C.setState({ beatOn: false, promptOn: false, hintOn: false, fade: 0 });
  });
  await page.waitForTimeout(2800);
  await page.evaluate(() => {
    const C = window.__GMC;
    // Face left wall portraits mid-hall
    C.cam.position.set(-2.2, 3.6, 8);
    C.yaw = Math.PI / 2; C.pitch = 0.12;
    C.cam.rotation.set(0.12, Math.PI / 2, 0, 'YXZ');
    if (C._raf) { cancelAnimationFrame(C._raf); C._raf = null; }
    C.renderer.render(C.scene, C.cam);
  });
  await shot(page, 'fix-03-hall-frames');
  await page.evaluate(() => {
    const C = window.__GMC;
    C.cam.position.set(0, 1.7, 10);
    C.yaw = 0; C.pitch = 0.05;
    C.cam.rotation.set(0.05, 0, 0, 'YXZ');
    C.renderer.render(C.scene, C.cam);
  });
  await shot(page, 'fix-04-hall-forward');
  await page.close();
}

// Court + STB
{
  const page = await openPage('court');
  await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Court.dc.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__GMC?.scene, null, { timeout: 60000 });
  await page.waitForTimeout(2800);
  await page.evaluate(() => {
    if (typeof window.__GM.goTo === 'function') window.__GM.goTo('bar');
    const C = window.__GMC;
    C.walkEnabled = false;
    C.setState?.({ beatOn: false, fade: 0, promptOn: false });
    C.cam.position.set(0, 1.7, 6);
    C.yaw = 0; C.pitch = 0.05;
    C.cam.rotation.set(0.05, 0, 0, 'YXZ');
    if (C._raf) { cancelAnimationFrame(C._raf); C._raf = null; }
    C.renderer.render(C.scene, C.cam);
  });
  await shot(page, 'fix-05-court-lit');
  await page.evaluate(() => {
    const C = window.__GMC;
    C.cam.position.set(-4.5, 2.2, 0);
    C.yaw = Math.PI / 2; C.pitch = 0.05;
    C.cam.rotation.set(0.05, Math.PI / 2, 0, 'YXZ');
    C.renderer.render(C.scene, C.cam);
  });
  await shot(page, 'fix-06-court-wall');
  await page.close();
}
{
  const page = await openPage('shut-the-box');
  await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Shut%20the%20Box.dc.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__GMC?.scene, null, { timeout: 60000 });
  await page.waitForTimeout(2800);
  await page.evaluate(() => {
    const C = window.__GMC;
    C.setState?.({ beatOn: false, fade: 0 });
    C.cam.position.set(0, 1.65, 3.1);
    C.pitch = -0.28;
    C.cam.rotation.set(C.pitch, 0, 0, 'YXZ');
    if (C._raf) { cancelAnimationFrame(C._raf); C._raf = null; }
    C.renderer.render(C.scene, C.cam);
  });
  await shot(page, 'fix-07-stb-lit');
  await page.evaluate(() => {
    const C = window.__GMC;
    C.cam.position.set(-2.5, 1.7, 1.0);
    C.yaw = Math.PI / 2; C.pitch = 0.05;
    C.cam.rotation.set(0.05, Math.PI / 2, 0, 'YXZ');
    C.renderer.render(C.scene, C.cam);
  });
  await shot(page, 'fix-08-stb-wall');
  await page.close();
}

console.log('page errors:', errs.length, errs.slice(0, 5));
pass = errs.length === 0;
console.log(pass ? 'FIX GATE PASS' : 'FIX GATE FAIL');
} catch (e) {
  console.log('FIX GATE FAIL — threw:', (e && e.message) || e);
} finally {
  if (browser) await browser.close().catch(() => {});
  server.close();
}
process.exit(pass ? 0 : 1);
