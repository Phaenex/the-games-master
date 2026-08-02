#!/usr/bin/env node
// Full interactive + visual gate for owned Unity integrations across all live scenes.
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile, mkdir } from 'fs/promises';
import { extname, join } from 'path';

const ROOT = process.cwd();
const PORT = 3820;
const DIR = 'docs/playtest/screenshots';
const MIME = {
  '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json',
  '.glb': 'model/gltf-binary', '.gltf': 'model/gltf+json', '.bin': 'application/octet-stream',
  '.png': 'image/png', '.jpg': 'image/jpeg', '.jpeg': 'image/jpeg', '.ogg': 'audio/ogg', '.css': 'text/css',
};
await mkdir(DIR, { recursive: true });
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
const base = `http://127.0.0.1:${PORT}`;
const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
const errors = [];
page.on('pageerror', (e) => errors.push(e.message));

const results = { prologue: {}, hall: {}, court: {}, stb: {}, errors: 0 };
const shot = async (name) => {
  await page.screenshot({ path: `${DIR}/${name}.png` });
  console.log('shot', name);
};

async function freezePose(x, y, z, yaw, pitch) {
  await page.evaluate(([px, py, pz, yw, pi]) => {
    const C = window.__GMC;
    if (window.__GM && window.__GM.goTo) {
      try { window.__GM.goTo('walk'); } catch (_) {}
    }
    C.walkEnabled = false;
    if (C._raf) { cancelAnimationFrame(C._raf); C._raf = null; }
    C.cam.position.set(px, py, pz);
    C.yaw = yw; C.pitch = pi;
    C.cam.rotation.set(pi, yw, 0, 'YXZ');
    C.renderer.render(C.scene, C.cam);
  }, [x, y, z, yaw, pitch]);
  await page.waitForTimeout(200);
}

// ===== PROLOGUE: doors load + open interactive beat =====
await page.goto(`${base}/The%20Games%20Master%20-%20Prologue.dc.html`, { waitUntil: 'load', timeout: 120000 });
await page.waitForFunction(() => window.__GMC && window.__GMC.scene && window.THREE, null, { timeout: 120000 });
// Prologue facade uses matched painted leaves (Unity Door_Double removed — style clash).
await page.waitForFunction(() => window.__GMC && window.__GMC.mansionModel && window.__GMC.doorL && window.__GMC.doorR, null, { timeout: 120000 });
results.prologue.unityDoors = await page.evaluate(() => {
  const C = window.__GMC; const THREE = window.THREE;
  const lb = new THREE.Box3().setFromObject(C.doorL);
  const s = lb.getSize(new THREE.Vector3());
  return { flag: !!C._unityFrontDoors, painted: !C._unityFrontDoors, h: +s.y.toFixed(2), w: +s.x.toFixed(2) };
});
await page.evaluate(() => {
  window.__GM.goTo('walk');
  const C = window.__GMC;
  C.walkEnabled = true; C.yaw = 0; C.pitch = 0.1;
  C.cam.position.set(0, 1.7, -37.5);
  C._passedGate = true; C._gateLockFired = true;
});
await page.waitForTimeout(500);
await page.keyboard.down('w');
await page.waitForFunction(() => window.__GMC.mode === 'arrival', null, { timeout: 20000 }).catch(() => {});
await page.keyboard.up('w');
await page.waitForFunction(() => window.__GMC._glimpseSaid === true || window.__GMC.knockStarted === true, null, { timeout: 25000 }).catch(() => {});
await page.waitForTimeout(400);
results.prologue.trap = await page.evaluate(() => ({
  glimpse: !!window.__GMC._glimpseSaid,
  knock: !!window.__GMC.knockStarted,
  doorRot: {
    L: +(window.__GMC.doorL.rotation.y || 0).toFixed(2),
    R: +(window.__GMC.doorR.rotation.y || 0).toFixed(2),
  },
}));
// Closed-door trap: leaves must stay ~0 during hold/knock (no open fashion show)
await page.evaluate(() => {
  const C = window.__GMC;
  C.walkEnabled = false;
  if (C._raf) { cancelAnimationFrame(C._raf); C._raf = null; }
  if (C.doorL) C.doorL.rotation.y = 0;
  if (C.doorR) C.doorR.rotation.y = 0;
  C.cam.position.set(0, 2.4, -48.8);
  C.yaw = 0; C.pitch = 0.08;
  C.cam.rotation.set(0.08, 0, 0, 'YXZ');
  C.renderer.render(C.scene, C.cam);
});
await shot('unity-full-01-prologue-doors');
console.log('prologue', JSON.stringify(results.prologue));

// ===== ENTRY HALL: metalman furniture + parlor doors =====
await page.goto(`${base}/The%20Games%20Master%20-%20Entry%20Hall.dc.html`, { waitUntil: 'load', timeout: 120000 });
await page.waitForFunction(() => window.__GMC && window.__GMC.scene, null, { timeout: 90000 });
await page.waitForTimeout(3500);
results.hall = await page.evaluate(() => {
  const C = window.__GMC; const THREE = window.THREE;
  let meshCount = 0;
  C.scene.traverse((o) => { if (o.isMesh) meshCount++; });
  const leftH = C.doorL ? new THREE.Box3().setFromObject(C.doorL).getSize(new THREE.Vector3()).y : 0;
  return {
    unityParlor: !!C._unityParlorDoors,
    meshCount,
    doorH: +leftH.toFixed(2),
    doorRot0: C.doorL ? +C.doorL.rotation.y.toFixed(2) : null,
  };
});
await freezePose(-16, 1.7, 6, -0.15, 0);
await shot('unity-full-02-hall-library');
await freezePose(16, 1.7, 5, 0.2, 0);
await shot('unity-full-03-hall-drawing');
// interactive: swing parlor doors
await page.evaluate(() => {
  const C = window.__GMC;
  if (C._raf) { cancelAnimationFrame(C._raf); C._raf = null; }
  if (C.doorL) C.doorL.rotation.y = 1.2;
  if (C.doorR) C.doorR.rotation.y = -1.2;
  C.cam.position.set(0, 1.7, -24);
  C.yaw = 0; C.pitch = 0.05;
  C.cam.rotation.set(0.05, 0, 0, 'YXZ');
  C.renderer.render(C.scene, C.cam);
});
await page.waitForTimeout(250);
results.hall.doorOpen = await page.evaluate(() => ({
  L: +(window.__GMC.doorL.rotation.y || 0).toFixed(2),
  R: +(window.__GMC.doorR.rotation.y || 0).toFixed(2),
}));
await shot('unity-full-04-hall-parlor-open');
console.log('hall', JSON.stringify(results.hall));

// ===== COURT: walk + evidence present =====
await page.goto(`${base}/The%20Games%20Master%20-%20Court.dc.html`, { waitUntil: 'load', timeout: 120000 });
await page.waitForFunction(() => window.__GMC && window.__GMC.scene, null, { timeout: 90000 });
await page.waitForTimeout(2500);
await page.evaluate(() => {
  if (window.__GM && window.__GM.goTo) window.__GM.goTo('bar');
});
await page.waitForTimeout(800);
results.court = await page.evaluate(() => {
  const C = window.__GMC; const THREE = window.THREE;
  let chairs = 0, tables = 0;
  C.scene.traverse((o) => {
    if (!o.isMesh) return;
    const n = (o.name || '') + (o.parent && o.parent.name || '');
    if (/chair/i.test(n)) chairs++;
    if (/table|dinning/i.test(n)) tables++;
  });
  return { chairs, tables, gavel: !!C.gavel, meshCount: (() => { let n = 0; C.scene.traverse((o) => { if (o.isMesh) n++; }); return n; })() };
});
await freezePose(0, 1.7, 4.2, 0, -0.08);
await shot('unity-full-05-court-bar');
// interactive evidence via bridge (keys can miss focus in headless)
await page.evaluate(() => {
  const GM = window.__GM;
  if (typeof GM.setRigged === 'function') GM.setRigged(true);
  if (typeof GM.goTo === 'function') GM.goTo('rigged');
  if (window.__GMC) { window.__GMC._presented = []; window.__GMC._tarnishCur = 0; window.__GMC._tarnishTarget = 0; }
  if (typeof GM.presentTrue === 'function') GM.presentTrue();
});
await page.waitForTimeout(700);
results.court.afterPresent = await page.evaluate(() => {
  const st = window.__GM.getState();
  return {
    presented: (st.presented && st.presented.length) || 0,
    tarnish: +(st.gavelTarnish || 0).toFixed(2),
    seals: st.sealsCracked || 0,
  };
});
await shot('unity-full-06-court-evidence');
console.log('court', JSON.stringify(results.court));

// ===== STB: boards + demoShut9 =====
await page.goto(`${base}/The%20Games%20Master%20-%20Shut%20the%20Box.dc.html`, { waitUntil: 'load', timeout: 120000 });
await page.waitForFunction(() => window.__GMC && window.__GMC._sceneReady, null, { timeout: 90000 });
await page.waitForTimeout(2000);
results.stb = await page.evaluate(() => {
  const C = window.__GMC;
  let meshCount = 0;
  C.scene.traverse((o) => { if (o.isMesh) meshCount++; });
  return {
    boards: C.match ? 2 : 0,
    unityTable: !!C._unityTable,
    meshCount,
    playerSum: C.playerBox && window.GMShutBox ? GMShutBox.openSum(C.playerBox) : null,
  };
});
await page.evaluate(() => {
  const C = window.__GMC;
  if (C._raf) { cancelAnimationFrame(C._raf); C._raf = null; }
  C.cam.position.set(0, 1.65, 3.1);
  C.yaw = 0; C.pitch = -0.42;
  C.cam.rotation.set(-0.42, 0, 0, 'YXZ');
  C.renderer.render(C.scene, C.cam);
});
await shot('unity-full-07-stb');
await page.evaluate(() => { if (window.__GMC.demoShut9) window.__GMC.demoShut9(); });
await page.waitForTimeout(800);
results.stb.afterShut9 = await page.evaluate(() => {
  const C = window.__GMC;
  return {
    playerSum: C.playerBox && window.GMShutBox ? GMShutBox.openSum(C.playerBox) : null,
    tile9: C.playerBox ? !!C.playerBox[9] : null,
  };
});
await shot('unity-full-08-stb-shut9');
console.log('stb', JSON.stringify(results.stb));

results.errors = errors.length;
if (errors.length) console.log('PAGEERRORS', errors.slice(0, 8));

await browser.close();
server.close();

const pass =
  results.prologue.unityDoors && results.prologue.unityDoors.flag && results.prologue.unityDoors.h > 1.8 &&
  Math.abs(results.prologue.doorRot.L) > 0.4 &&
  results.hall.unityParlor && results.hall.meshCount > 80 &&
  results.hall.doorOpen && Math.abs(results.hall.doorOpen.L) > 0.5 &&
  results.court.gavel && results.court.meshCount > 40 &&
  results.court.afterPresent && results.court.afterPresent.tarnish > 0.4 &&
  results.stb.boards === 2 && results.stb.unityTable &&
  results.stb.afterShut9 && results.stb.afterShut9.playerSum === 36 &&
  results.errors === 0;

console.log(pass ? 'UNITY FULL GATE PASS' : 'UNITY FULL GATE FAIL', JSON.stringify(results, null, 2));
process.exit(pass ? 0 : 1);
