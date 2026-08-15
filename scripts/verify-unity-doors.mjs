// Visual gate: Unity Door_Double upright + porch closed/open shots (RAF paused for freeze poses).
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile, mkdir } from 'fs/promises';
import { extname, join } from 'path';

const ROOT = process.cwd(), PORT = 3811, DIR = 'docs/playtest/screenshots';
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
// Printing a page error and still exiting 0 is the same as not looking — collect and gate on it.
const errs = [];
page.on('pageerror', (e) => { errs.push(e.message); console.error('PAGEERROR', e.message); });

let info;
try {
await page.goto(`http://127.0.0.1:${PORT}/The%20Games%20Master%20-%20Prologue.dc.html`, { waitUntil: 'load', timeout: 120000 });
// both leaves — this is Door_Double, and a missing doorR is exactly what the size check below catches
await page.waitForFunction(() => window.__GMC && window.__GMC.scene && window.__GMC.mansionModel && window.__GMC.doorL && window.__GMC.doorR && window.__GMC._unityFrontDoors, null, { timeout: 120000 });

info = await page.evaluate(() => {
  const C = window.__GMC; const THREE = window.THREE;
  const L = C.doorL, R = C.doorR;
  const lb = L ? new THREE.Box3().setFromObject(L) : null;
  const rb = R ? new THREE.Box3().setFromObject(R) : null;
  const sz = (b) => b ? [+(b.max.x - b.min.x).toFixed(2), +(b.max.y - b.min.y).toFixed(2), +(b.max.z - b.min.z).toFixed(2)] : null;
  return {
    unity: !!C._unityFrontDoors,
    painted: !C._unityFrontDoors,
    axis: C._unityDoorAxis || null,
    leftSize: sz(lb),
    rightSize: sz(rb),
    leftY: L && +L.position.y.toFixed(2),
    rightY: R && +R.position.y.toFixed(2),
  };
});
console.log('doors', JSON.stringify(info));
if (info.painted) console.warn('WARN: painted facade leaves still active (expected Door_Double)');

const pose = async (x, y, z, yaw, pitch, open) => {
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
  await page.waitForTimeout(250);
};

await pose(0, 2.4, -46.5, 0, 0.12, false);
await page.screenshot({ path: `${DIR}/unity-door-01-closed.png` });
console.log('shot unity-door-01-closed');

await pose(0, 2.5, -49.5, 0, 0.1, true);
await page.screenshot({ path: `${DIR}/unity-door-02-open.png` });
console.log('shot unity-door-02-open');

await page.goto(`http://127.0.0.1:${PORT}/The%20Games%20Master%20-%20Court.dc.html`, { waitUntil: 'load', timeout: 120000 });
await page.waitForFunction(() => window.__GMC && window.__GMC.scene, null, { timeout: 90000 });
await page.waitForTimeout(2000);
await page.evaluate(() => {
  if (window.__GM && window.__GM.goTo) window.__GM.goTo('bar');
  const C = window.__GMC;
  if (C && C.cam) {
    C.walkEnabled = false;
    if (C._raf) { cancelAnimationFrame(C._raf); C._raf = null; }
    C.cam.position.set(0, 1.7, 4.5);
    C.yaw = 0; C.pitch = -0.05;
    C.cam.rotation.set(C.pitch, C.yaw, 0, 'YXZ');
    C.renderer.render(C.scene, C.cam);
  }
});
await page.waitForTimeout(800);
await page.screenshot({ path: `${DIR}/unity-court-01.png` });
console.log('shot unity-court-01');

} finally {
  await browser.close();
  server.close();
}

// Door_Double hinged leaves on Prologue facade (night MeshBasic). Both leaves are measured, so both
// are asserted — the leaves are mirrored, and a degenerate right one would otherwise sail through.
const leafOK = (s) => !!s && s[1] > 1.8 && s[1] >= s[0] * 0.9;
const ok = !!info && info.unity && leafOK(info.leftSize) && leafOK(info.rightSize) && errs.length === 0;
console.log(ok ? 'VERIFY PASS (Door_Double facade leaves)' : 'VERIFY FAIL', info, 'pageerrors:', errs.length);
process.exit(ok ? 0 : 1);
