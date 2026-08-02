#!/usr/bin/env node
/**
 * Physical + visual QA for opening after trio cutover.
 * Asserts: car upright, gate slam+block, lateral funnel, mansion load,
 * Modular hall textures, Court/STB boot. Canvas shots under qa-*.
 */
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile, writeFile } from 'fs/promises';
import { extname, join } from 'path';

const ROOT = process.cwd();
const PORT = 3831;
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
const fail = (scene, why) => { results.push({ scene, ok: false, why }); console.log('FAIL', scene, why); };
const pass = (scene, info) => { results.push({ scene, ok: true, info }); console.log('PASS', scene, info ? JSON.stringify(info) : ''); };

async function canvasShot(page, name) {
  const path = `${OUT}/${name}.png`;
  try {
    await page.locator('canvas').first().screenshot({ path, timeout: 30000 });
  } catch {
    await page.screenshot({ path, timeout: 30000 });
  }
  console.log('shot', name);
  return path;
}

// ─── Prologue physical ──────────────────────────────────────
{
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  const errs = [];
  page.on('pageerror', (e) => errs.push(e.message));
  await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Prologue.dc.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__GMC?.gateLeafL && window.__GMC?.carModel, null, { timeout: 120000 });
  await page.waitForFunction(() => window.__GMC?._ruinsBuilt !== undefined, null, { timeout: 60000 }).catch(() => {});
  await page.waitForFunction(() => window.__GMC?.mansionModel, null, { timeout: 90000 }).catch(() => {});
  await page.waitForTimeout(1500);

  // CAR physics
  const car = await page.evaluate(() => {
    const C = window.__GMC;
    const box = new THREE.Box3().setFromObject(C.carModel);
    const s = box.getSize(new THREE.Vector3());
    return {
      size: [+s.x.toFixed(2), +s.y.toFixed(2), +s.z.toFixed(2)],
      minY: +box.min.y.toFixed(3),
      maxY: +box.max.y.toFixed(3),
      cx: +((box.min.x + box.max.x) / 2).toFixed(2),
      cz: +((box.min.z + box.max.z) / 2).toFixed(2),
      reported: C._carSize,
    };
  });
  const carOk = car.minY > -0.15 && car.minY < 0.25
    && car.size[1] > 1.2 && car.size[1] < 2.2
    && car.size[2] > 3.0 && car.size[2] < 5.5
    && Math.abs(car.cx + 4.8) < 1.5;
  if (carOk && !errs.length) pass('car-phys', car);
  else fail('car-phys', { car, errs });

  await page.evaluate(() => {
    const C = window.__GMC;
    window.__GM.goTo('walk');
    C.walkEnabled = false;
    C.setState({ hintOn: false, beatOn: false, tutStep: 99, fade: 0, cinematic: false });
    const box = new THREE.Box3().setFromObject(C.carModel);
    const cx = (box.min.x + box.max.x) / 2;
    const frontZ = box.min.z;
    C.cam.position.set(cx - 3.5, 1.55, frontZ - 2.5);
    C.yaw = -2.4; C.pitch = 0.04;
    C.cam.rotation.set(C.pitch, C.yaw, 0, 'YXZ');
    C.renderer.render(C.scene, C.cam);
  });
  await canvasShot(page, 'qa-01-car');

  // Match play-gate.mjs: enter walk live, no camera teleport, walk forward through gate
  await page.evaluate(() => {
    const C = window.__GMC;
    window.__GM.goTo('walk');
    C.walkEnabled = true;
    C.yaw = 0; C.pitch = 0;
    C.setState({ hintOn: false, beatOn: false, tutStep: 99, fade: 0 });
    if (!C._raf) C._raf = requestAnimationFrame(() => C.loop());
  });
  await page.waitForTimeout(400);
  await canvasShot(page, 'qa-02-spawn');

  await page.keyboard.down('w');
  await page.waitForFunction(() => window.__GMC.cam.position.z < 66, null, { timeout: 30000 }).catch(() => {});
  await page.waitForFunction(() => window.__GMC.cam.position.z < 58, null, { timeout: 30000 }).catch(() => {});
  await page.keyboard.up('w');
  const thru = await page.evaluate(() => ({
    z: +window.__GMC.cam.position.z.toFixed(2),
    lock: !!window.__GM.getState().gateLockFired,
    passed: !!window.__GM.getState().passedGate,
  }));
  await canvasShot(page, 'qa-03-through-gate');
  if (thru.z < 62 && thru.passed) pass('gate-pass', thru);
  else fail('gate-pass', thru);

  // Turn around + walk back to slam
  await page.evaluate(() => { window.__GMC.yaw = Math.PI; });
  await page.waitForTimeout(250);
  await page.keyboard.down('w');
  await page.waitForFunction(() => window.__GM.getState().gateLockFired === true, null, { timeout: 30000 }).catch(() => {});
  await page.waitForTimeout(800);
  const locked = await page.evaluate(() => {
    const C = window.__GMC;
    return {
      lock: !!window.__GM.getState().gateLockFired,
      z: +C.cam.position.z.toFixed(2),
      leafL: C.gateLeafL ? +C.gateLeafL.rotation.y.toFixed(3) : null,
      leafR: C.gateLeafR ? +C.gateLeafR.rotation.y.toFixed(3) : null,
    };
  });
  await canvasShot(page, 'qa-04-gate-locked');

  await page.waitForTimeout(2000);
  await page.keyboard.up('w');
  const blocked = await page.evaluate(() => {
    const C = window.__GMC;
    return {
      z: +C.cam.position.z.toFixed(2),
      gateZ: C.gateZ,
      escapeAttempt: C.cam.position.z > C.gateZ + 5,
    };
  });
  await canvasShot(page, 'qa-05-gate-blocked');
  if (locked.lock && !blocked.escapeAttempt && blocked.z < blocked.gateZ + 3) pass('gate-block', { locked, blocked });
  else fail('gate-block', { locked, blocked, errs });

  // Lateral funnel: try walk around pier (|x| large) while facing house
  await page.evaluate(() => {
    const C = window.__GMC;
    C.yaw = 0;
    C.cam.position.set(0, 1.7, 55);
    C.walkEnabled = true;
  });
  await page.keyboard.down('d');
  await page.waitForTimeout(2000);
  await page.keyboard.up('d');
  const lat = await page.evaluate(() => ({
    x: +window.__GMC.cam.position.x.toFixed(2),
    z: +window.__GMC.cam.position.z.toFixed(2),
  }));
  // funnel should keep |x| modest near gate band
  if (Math.abs(lat.x) < 6.5) pass('lateral-funnel', lat);
  else fail('lateral-funnel', lat);
  await canvasShot(page, 'qa-06-lateral');

  // Mansion present?
  const house = await page.evaluate(() => {
    const C = window.__GMC;
    return {
      mansion: !!C.mansionModel,
      porch: C._porchSconcesBuilt || null,
      doors: !!(C.doorLeafL || C.doorL),
      ruins: !!C._ruinsBuilt,
      kids: C.scene.children.length,
    };
  });
  // walk toward porch for a facade shot (pose if too slow)
  await page.evaluate(() => {
    const C = window.__GMC;
    C.walkEnabled = false;
    C.cam.position.set(0, 2.0, -44);
    C.yaw = 0; C.pitch = 0.06;
    C.cam.rotation.set(0.06, 0, 0, 'YXZ');
    C.renderer.render(C.scene, C.cam);
  });
  await canvasShot(page, 'qa-07-porch');
  if (house.mansion && house.ruins) pass('facade', house);
  else fail('facade', house);

  if (errs.length) fail('prologue-errors', errs);
  else pass('prologue-errors', { n: 0 });
  await page.close();
}

// ─── Entry Hall Modular textures ────────────────────────────
{
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  const errs = [];
  page.on('pageerror', (e) => errs.push(e.message));
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
  await page.waitForTimeout(2000);
  await page.waitForFunction(() => {
    let mapped = 0;
    window.__GMC.scene.traverse((o) => {
      if (!o.isMesh || !o.material) return;
      const ms = Array.isArray(o.material) ? o.material : [o.material];
      ms.forEach((m) => { if (m && m.map && m.map.image) mapped++; });
    });
    return mapped >= 2;
  }, null, { timeout: 15000 }).catch(() => {});

  // walk forward a bit
  await page.evaluate(() => {
    const C = window.__GMC;
    C.cam.position.set(0, 1.7, 10);
    C.yaw = 0; C.pitch = 0.05;
    C.cam.rotation.set(0.05, 0, 0, 'YXZ');
    if (C._raf) { cancelAnimationFrame(C._raf); C._raf = null; }
    C.renderer.render(C.scene, C.cam);
  });
  await canvasShot(page, 'qa-08-hall');

  // physical: walk into side wall shouldn't clip out of world
  await page.evaluate(() => {
    const C = window.__GMC;
    C.walkEnabled = true;
    C.cam.position.set(0, 1.7, 4);
    C.yaw = Math.PI / 2;
  });
  await page.keyboard.down('w');
  await page.waitForTimeout(2500);
  await page.keyboard.up('w');
  const wallHit = await page.evaluate(() => ({
    x: +window.__GMC.cam.position.x.toFixed(2),
    z: +window.__GMC.cam.position.z.toFixed(2),
  }));
  await canvasShot(page, 'qa-09-hall-wall');
  const mapped = await page.evaluate(() => {
    let n = 0, white = 0;
    window.__GMC.scene.traverse((o) => {
      if (!o.isMesh || !o.material) return;
      const ms = Array.isArray(o.material) ? o.material : [o.material];
      ms.forEach((m) => {
        if (m && m.map && m.map.image) n++;
        if (m && m.color && m.color.r > 0.7 && m.color.g > 0.7 && m.color.b > 0.7 && !m.map) white++;
      });
    });
    return { mapped: n, whiteSlabs: white };
  });
  if (mapped.mapped >= 2 && mapped.whiteSlabs < 40 && !errs.length) pass('hall-visual', { mapped, wallHit });
  else fail('hall-visual', { mapped, wallHit, errs });
  await page.close();
}

// ─── Court boot + walk ──────────────────────────────────────
{
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  const errs = [];
  page.on('pageerror', (e) => errs.push(e.message));
  await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Court.dc.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__GMC?.scene, null, { timeout: 60000 });
  await page.waitForTimeout(2000);
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
  await canvasShot(page, 'qa-10-court');
  const kids = await page.evaluate(() => window.__GMC.scene.children.length);
  if (kids > 15 && !errs.length) pass('court', { kids });
  else fail('court', { kids, errs });
  await page.close();
}

// ─── STB boot ───────────────────────────────────────────────
{
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  const errs = [];
  page.on('pageerror', (e) => errs.push(e.message));
  await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Shut%20the%20Box.dc.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__GMC?.scene, null, { timeout: 60000 });
  await page.waitForTimeout(2500);
  await page.evaluate(() => {
    const C = window.__GMC;
    C.setState?.({ beatOn: false, fade: 0 });
    C.cam.position.set(0, 1.65, 3.1);
    C.pitch = -0.28;
    C.cam.rotation.set(C.pitch, 0, 0, 'YXZ');
    if (C._raf) { cancelAnimationFrame(C._raf); C._raf = null; }
    C.renderer.render(C.scene, C.cam);
  });
  await canvasShot(page, 'qa-11-stb');
  const boards = await page.evaluate(() => {
    let tiles = 0;
    window.__GMC.scene.traverse((o) => {
      if (o.isMesh && /tile|Tile|board/i.test(o.name || '')) tiles++;
    });
    return { kids: window.__GMC.scene.children.length, tiles };
  });
  if (boards.kids > 10 && !errs.length) pass('stb', boards);
  else fail('stb', { boards, errs });
  await page.close();
}

await browser.close();
server.close();

const failed = results.filter((r) => !r.ok);
console.log('\n=== QA OPENING PHYS ===');
console.log(JSON.stringify(results, null, 2));
await writeFile('/tmp/gm-qa-phys.json', JSON.stringify(results, null, 2));
if (failed.length) {
  console.log('FAIL', failed.map((f) => f.scene).join(', '));
  process.exit(1);
}
console.log('QA OPENING PHYS PASS');
process.exit(0);
