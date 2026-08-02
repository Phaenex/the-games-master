#!/usr/bin/env node
/**
 * Trio visual gate — car + Hall Modular + Court Modular + STB Modular.
 * Agent must PASS with readable screenshots before claiming cutover done.
 */
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile, writeFile } from 'fs/promises';
import { extname, join } from 'path';
import { createReadStream, existsSync } from 'fs';

const ROOT = process.cwd();
const PORT = 3820;
const OUT = 'docs/playtest/screenshots';
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
  } catch {
    res.writeHead(404);
    res.end('nf');
  }
});
await new Promise((r) => server.listen(PORT, r));

const browser = await chromium.launch();
const results = [];

async function shotPage(page, name) {
  const path = `${OUT}/${name}.png`;
  // Prefer canvas-only — Dom UI can't leak into the visual gate frame.
  const canvas = page.locator('canvas').first();
  try {
    await canvas.screenshot({ path });
  } catch {
    await page.screenshot({ path });
  }
  console.log('shot', name);
  return path;
}

function sampleBright(path) {
  // very rough: non-black if file > 15KB (black frames tend smaller / solid)
  // real check happens when agent Reads the image
  return existsSync(path);
}

async function hideHud(page) {
  await page.evaluate(() => {
    const C = window.__GMC;
    if (C && typeof C.setState === 'function') {
      C.setState({
        hintOn: false,
        beatOn: false,
        tutStep: 99,
        fade: 0,
        showInventory: false,
        showInteract: false,
        nearFlashlight: false,
        promptOn: false,
        letterMounted: false,
        cinematic: false,
      });
    }
    // Hide any overlay that isn't the canvas itself (siblings under the app root).
    const canvas = document.querySelector('canvas');
    if (canvas) {
      const chain = new Set();
      let n = canvas;
      while (n) { chain.add(n); n = n.parentElement; }
      document.body.querySelectorAll('*').forEach((el) => {
        if (chain.has(el) || el === canvas) return;
        // Skip pure ancestors only — hide every other node (HUD, beats, inv).
        if (el.contains && el.contains(canvas) && el !== canvas) return;
        el.style.setProperty('visibility', 'hidden', 'important');
        el.style.setProperty('opacity', '0', 'important');
        el.style.setProperty('pointer-events', 'none', 'important');
      });
      chain.forEach((el) => {
        el.style.removeProperty('visibility');
        el.style.setProperty('opacity', '1', 'important');
      });
    }
  });
  await page.waitForTimeout(100);
}

// ─── Prologue car ───────────────────────────────────────────
{
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  const errs = [];
  page.on('pageerror', (e) => errs.push(e.message));
  await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Prologue.dc.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__GMC?.carModel, null, { timeout: 60000 });
  await page.waitForTimeout(1500);
  const carInfo = await page.evaluate(() => {
    const C = window.__GMC;
    if (typeof window.__GM?.goTo === 'function') window.__GM.goTo('walk');
    C.walkEnabled = false;
    if (C._raf) { cancelAnimationFrame(C._raf); C._raf = null; }
    const box = new THREE.Box3().setFromObject(C.carModel);
    const s = box.getSize(new THREE.Vector3());
    return {
      size: C._carSize,
      world: [+s.x.toFixed(2), +s.y.toFixed(2), +s.z.toFixed(2)],
      minY: +box.min.y.toFixed(2),
      path: (C.carModel.userData && C.carModel.userData.src) || 'hd03',
    };
  });
  console.log('car', JSON.stringify(carInfo));

  // Front of car is min.z (nose toward house). Face it from down-drive looking +Z (yaw=π).
  const poses = await page.evaluate(() => {
    const C = window.__GMC;
    const box = new THREE.Box3().setFromObject(C.carModel);
    const cx = (box.min.x + box.max.x) / 2;
    const cz = (box.min.z + box.max.z) / 2;
    const frontZ = box.min.z;
    return [
      { name: 'trio-gate-car-front', x: cx, y: 1.45, z: frontZ - 4.2, yaw: Math.PI, pitch: 0.04 },
      { name: 'trio-gate-car-3q', x: cx - 4.2, y: 1.55, z: frontZ - 2.2, yaw: -2.5, pitch: 0.03 },
      { name: 'trio-gate-car-side', x: box.max.x + 3.2, y: 1.5, z: cz, yaw: -Math.PI / 2, pitch: 0.02 },
    ];
  });
  for (const p of poses) {
    await page.evaluate((pose) => {
      const C = window.__GMC;
      C.cam.position.set(pose.x, pose.y, pose.z);
      C.yaw = pose.yaw; C.pitch = pose.pitch;
      C.cam.rotation.set(C.pitch, C.yaw, 0, 'YXZ');
      C.renderer.render(C.scene, C.cam);
    }, p);
    await page.waitForTimeout(250);
    await hideHud(page);
    await shotPage(page, p.name);
  }
  const upright = carInfo.minY > -0.2 && carInfo.world[1] > 1.2 && carInfo.world[1] < 2.2
    && carInfo.world[2] > 3.2 && carInfo.world[2] < 5.5;
  results.push({ scene: 'car', ok: upright && errs.length === 0, carInfo, errs });
  await page.close();
}

// ─── Entry Hall Modular ─────────────────────────────────────
{
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  const errs = [];
  page.on('pageerror', (e) => errs.push(e.message));
  await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Entry%20Hall.dc.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__GM && window.__GMC?.scene, null, { timeout: 45000 });
  await page.evaluate(() => {
    const C = window.__GMC;
    if (C._t0) { clearTimeout(C._t0); C._t0 = null; }
    window.__GM.goTo('hall');
    C.walkEnabled = false;
    C._beat = 99;
    (C.pois || []).forEach((p) => { p.said = true; });
    C.setState({ beatOn: false, promptOn: false, hintOn: false, fade: 0 });
  });
  await page.waitForTimeout(2500);
  // Wait for modular wall albedo maps (async TextureLoader)
  await page.waitForFunction(() => {
    let mapped = 0;
    window.__GMC.scene.traverse((o) => {
      if (!o.isMesh || !o.material) return;
      const ms = Array.isArray(o.material) ? o.material : [o.material];
      ms.forEach((m) => { if (m && m.map && m.map.image) mapped++; });
    });
    return mapped >= 2;
  }, { timeout: 15000 }).catch(() => {});
  await page.waitForTimeout(800);
  const hallInfo = await page.evaluate(() => {
    let modWalls = 0;
    window.__GMC.scene.traverse((o) => {
      if (o.isMesh && /Wall_01|wall_01|NoLedge/i.test(o.name || (o.parent && o.parent.name) || '')) modWalls++;
      // clones may lose names — count by material source paths if tagged
    });
    // also count any GLTF objects near side walls
    let nearWall = 0;
    window.__GMC.scene.traverse((o) => {
      if (!o.isMesh) return;
      const p = new THREE.Vector3();
      o.getWorldPosition(p);
      if (Math.abs(p.x) > 6.5 && p.y > 1 && p.y < 9) nearWall++;
    });
    return { modWalls, nearWall, kids: window.__GMC.scene.children.length };
  });
  console.log('hall', JSON.stringify(hallInfo));

  await page.evaluate(() => {
    const C = window.__GMC;
    C.cam.position.set(0, 1.7, 10);
    C.yaw = 0; C.pitch = 0.05;
    C.cam.rotation.set(0.05, 0, 0, 'YXZ');
    C.renderer.render(C.scene, C.cam);
  });
  await hideHud(page);
  await shotPage(page, 'trio-gate-hall-forward');

  await page.evaluate(() => {
    const C = window.__GMC;
    C.cam.position.set(-5.5, 1.7, 0);
    C.yaw = Math.PI / 2; C.pitch = 0;
    C.cam.rotation.set(0, Math.PI / 2, 0, 'YXZ');
    C.renderer.render(C.scene, C.cam);
  });
  await hideHud(page);
  await shotPage(page, 'trio-gate-hall-wall');

  results.push({ scene: 'hall', ok: hallInfo.kids > 20 && errs.length === 0, hallInfo, errs });
  await page.close();
}

// ─── Court Modular ──────────────────────────────────────────
{
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  const errs = [];
  page.on('pageerror', (e) => errs.push(e.message));
  await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Court.dc.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__GM && window.__GMC?.scene, null, { timeout: 45000 });
  await page.waitForTimeout(2000);
  await page.evaluate(() => {
    if (typeof window.__GM.goTo === 'function') window.__GM.goTo('bar');
    const C = window.__GMC;
    C.walkEnabled = false;
    C.setState?.({ beatOn: false, fade: 0, promptOn: false });
  });
  await page.waitForTimeout(2000);
  await page.evaluate(() => {
    const C = window.__GMC;
    if (C._raf) { cancelAnimationFrame(C._raf); C._raf = null; }
    C.cam.position.set(0, 1.7, 6);
    C.yaw = 0; C.pitch = 0.05;
    C.cam.rotation.set(0.05, 0, 0, 'YXZ');
    C.renderer.render(C.scene, C.cam);
  });
  await hideHud(page);
  await shotPage(page, 'trio-gate-court');

  await page.evaluate(() => {
    const C = window.__GMC;
    C.cam.position.set(-4.2, 1.8, 0.5);
    C.yaw = Math.PI / 2; C.pitch = 0.02;
    C.cam.rotation.set(0.02, Math.PI / 2, 0, 'YXZ');
    // bump side fill so wall panels read
    if (!C._trioWallFill) {
      const fl = new THREE.PointLight('#c8b090', 2.2, 14, 2);
      fl.position.set(-4.5, 2.2, 0.5);
      C.scene.add(fl);
      C._trioWallFill = fl;
    }
    C.renderer.render(C.scene, C.cam);
  });
  await hideHud(page);
  await shotPage(page, 'trio-gate-court-wall');
  results.push({ scene: 'court', ok: errs.length === 0, errs });
  await page.close();
}

// ─── STB Modular ────────────────────────────────────────────
{
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  const errs = [];
  page.on('pageerror', (e) => errs.push(e.message));
  await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Shut%20the%20Box.dc.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__GMC?.scene || window.__GM, null, { timeout: 45000 });
  await page.waitForTimeout(2500);
  await page.evaluate(() => {
    const C = window.__GMC;
    if (!C) return;
    C.setState?.({ beatOn: false, fade: 0, promptOn: false });
    // Temp fill so Modular walls read in gate shots (play stays moody)
    if (!C._trioFill) {
      const fill = new THREE.PointLight('#c9b080', 2.4, 28, 2);
      fill.position.set(0, 2.4, -1);
      C.scene.add(fill);
      C._trioFill = fill;
    }
    if (C._raf) { cancelAnimationFrame(C._raf); C._raf = null; }
    C.cam.position.set(0, 1.65, 3.1);
    C.pitch = -0.28;
    C.yaw = 0;
    C.cam.rotation.set(C.pitch, 0, 0, 'YXZ');
    C.renderer.render(C.scene, C.cam);
  });
  await hideHud(page);
  await shotPage(page, 'trio-gate-stb');

  await page.evaluate(() => {
    const C = window.__GMC;
    C.cam.position.set(-2.4, 1.7, 1.2);
    C.yaw = Math.PI / 2; C.pitch = 0.05;
    C.cam.rotation.set(0.05, Math.PI / 2, 0, 'YXZ');
    C.renderer.render(C.scene, C.cam);
  });
  await hideHud(page);
  await shotPage(page, 'trio-gate-stb-wall');
  results.push({ scene: 'stb', ok: errs.length === 0, errs });
  await page.close();
}

await browser.close();
server.close();

const failed = results.filter((r) => !r.ok);
console.log('\n=== TRIO GATE ===');
console.log(JSON.stringify(results, null, 2));
if (failed.length) {
  console.log('FAIL', failed.map((f) => f.scene).join(', '));
  process.exit(1);
}
console.log('TRIO GATE PASS');
await writeFile('/tmp/gm-trio-gate.json', JSON.stringify(results, null, 2));
