#!/usr/bin/env node
// Gate: Unity Door_Double + Env outer fence + door surround on Prologue.
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile, mkdir } from 'fs/promises';
import { extname, join } from 'path';

const ROOT = process.cwd(), PORT = 3812, DIR = 'docs/playtest/screenshots';
const MIME = { '.html':'text/html','.js':'text/javascript','.json':'application/json','.glb':'model/gltf-binary','.gltf':'model/gltf+json','.bin':'application/octet-stream','.png':'image/png','.jpg':'image/jpeg','.ogg':'audio/ogg','.css':'text/css' };
await mkdir(DIR, { recursive: true });
const server = createServer(async (req, res) => {
  try {
    let p = decodeURIComponent(req.url.split('?')[0]);
    if (p === '/') p = '/index.html';
    const fp = join(ROOT, p);
    const buf = await readFile(fp);
    res.writeHead(200, { 'Content-Type': MIME[extname(fp)] || 'application/octet-stream' });
    res.end(buf);
  } catch { res.writeHead(404); res.end('nf'); }
});
await new Promise((r) => server.listen(PORT, r));

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
const errs = [];
page.on('pageerror', (e) => errs.push(e.message));

await page.goto(`http://127.0.0.1:${PORT}/The%20Games%20Master%20-%20Prologue.dc.html`, { waitUntil: 'load', timeout: 120000 });
await page.waitForFunction(() => window.__GMC && window.__GMC.scene && window.__GMC.mansionModel && window.__GMC.doorL, null, { timeout: 120000 });
// Wait for async Door_Double + Env fences
await page.waitForFunction(() => {
  const C = window.__GMC;
  return C && C._unityFrontDoors && C._envOuterWalls;
}, null, { timeout: 90000 }).catch(() => {});

const info = await page.evaluate(() => {
  const C = window.__GMC; const THREE = window.THREE;
  const L = C.doorL, R = C.doorR;
  const lb = L ? new THREE.Box3().setFromObject(L) : null;
  const rb = R ? new THREE.Box3().setFromObject(R) : null;
  const sz = (b) => b ? [+(b.max.x - b.min.x).toFixed(2), +(b.max.y - b.min.y).toFixed(2), +(b.max.z - b.min.z).toFixed(2)] : null;
  let envFence = 0, outerBoxesVis = 0;
  C.scene.traverse((o) => {
    if (o.userData?.gmKind === 'envOuterFence' || o.userData?.gmKind === 'envOuterWall') envFence++;
    if (o.userData?.gmKind === 'outerWall' && o.visible) outerBoxesVis++;
  });
  return {
    unity: !!C._unityFrontDoors,
    envOuter: C._envOuterWalls || null,
    leftSize: sz(lb),
    rightSize: sz(rb),
    envFence,
    outerBoxesVis,
    snap: C.devSnapshot ? C.devSnapshot() : null,
  };
});
console.log('info', JSON.stringify(info));

const pose = async (x, y, z, yaw, pitch, open, name) => {
  await page.evaluate(([px, py, pz, yw, pi, openDoors]) => {
    const C = window.__GMC;
    window.__GM.goTo('walk');
    C.walkEnabled = false;
    if (C._raf) { cancelAnimationFrame(C._raf); C._raf = null; }
    C.cam.position.set(px, py, pz);
    C.yaw = yw; C.pitch = pi;
    C.cam.rotation.set(pi, yw, 0, 'YXZ');
    if (C.doorL) C.doorL.rotation.y = openDoors ? 1.52 : 0;
    if (C.doorR) C.doorR.rotation.y = openDoors ? -1.52 : 0;
    if (C.doorSeam) C.doorSeam.material.opacity = openDoors ? 0 : 0.38;
    if (C.doorDark) C.doorDark.visible = !openDoors;
    C.renderer.render(C.scene, C.cam);
  }, [x, y, z, yaw, pitch, open]);
  await page.waitForTimeout(200);
  await page.screenshot({ path: `${DIR}/${name}.png` });
  console.log('shot', name);
};

const oeZ = await page.evaluate(() => window.__GMC._oeZ || 90);
await pose(0, 1.7, oeZ - 8, Math.PI, 0.05, false, 'env-outgate-01-lookback');
await pose(5, 1.7, oeZ - 2, Math.PI * 0.5, 0.05, false, 'env-outgate-02-fence');
await pose(0, 2.4, -46.5, 0, 0.12, false, 'env-door-01-closed');
await pose(0, 2.5, -49.5, 0, 0.1, true, 'env-door-02-open');
await pose(0, 2.2, -50.8, 0, 0.08, true, 'env-door-03-threshold');

await browser.close();
server.close();

const ok =
  info.unity &&
  info.envOuter &&
  info.leftSize &&
  info.leftSize[1] > 1.8 &&
  info.outerBoxesVis === 0 &&
  errs.length === 0;
console.log(ok ? 'VERIFY PASS (owned Env fence + Door_Double wired)' : 'VERIFY FAIL', { errs: errs.slice(0, 5), info });
process.exit(ok ? 0 : 1);
