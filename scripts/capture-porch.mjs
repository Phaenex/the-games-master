// Close-up porch / door shots after sconce pass. Writes docs/playtest/screenshots/nite-*.png
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';

const ROOT = process.cwd(), PORT = 3811;
const MIME = { '.html':'text/html','.js':'text/javascript','.json':'application/json','.glb':'model/gltf-binary','.gltf':'model/gltf+json','.bin':'application/octet-stream','.png':'image/png','.jpg':'image/jpeg','.ogg':'audio/ogg' };
const server = createServer(async (req, res) => {
  try {
    let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html';
    const buf = await readFile(join(ROOT, p));
    res.writeHead(200, { 'Content-Type': MIME[extname(p)] || 'application/octet-stream' });
    res.end(buf);
  } catch { res.writeHead(404); res.end('nf'); }
});
await new Promise((r) => server.listen(PORT, r));

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
try {
const errs = [];
page.on('pageerror', (e) => { errs.push(e.message); console.log('[pageerror]', e.message); });
await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Prologue.dc.html`, { waitUntil: 'load' });
await page.waitForFunction(() => window.__GM && window.__GMC && window.__GMC._ruinsBuilt && window.__GMC._porchSconcesBuilt, null, { timeout: 120000 });
await page.evaluate(() => {
  window.__GM.goTo('walk');
  const C = window.__GMC;
  C.walkEnabled = false;
  C._passedGate = true;
  C._gateLockFired = true;
});
await page.waitForTimeout(800);

async function pose(name, fn) {
  await page.evaluate(fn);
  await page.waitForTimeout(700);
  const path = `docs/playtest/screenshots/${name}.png`;
  await page.screenshot({ path });
  const snap = await page.evaluate(() => window.__GM.getState());
  console.log('shot', name, 'porchSconces=', snap.porchSconces, 'errs=', snap.errors);
}

await pose('nite-04-porch-brackets', () => {
  const C = window.__GMC;
  // mid-porch: door + both sconces in frame (door wall ~z=-52.6)
  C.cam.position.set(0, 2.35, -46.5);
  C.yaw = 0; C.pitch = -0.06;
  C.cam.rotation.order = 'YXZ';
  C.cam.rotation.x = C.pitch; C.cam.rotation.y = C.yaw; C.cam.rotation.z = 0;
});
await pose('nite-05-door-close', () => {
  const C = window.__GMC;
  C.cam.position.set(0, 2.5, -49.8);
  C.yaw = 0; C.pitch = -0.04;
  C.cam.rotation.order = 'YXZ';
  C.cam.rotation.x = C.pitch; C.cam.rotation.y = C.yaw; C.cam.rotation.z = 0;
});
await pose('nite-06-door-ajar', () => {
  const C = window.__GMC;
  if (C.doorL) C.doorL.rotation.y = -1.15;
  if (C.doorR) C.doorR.rotation.y = 1.15;
  if (C.doorDark) C.doorDark.visible = false;
  if (C.chandGlow) C.chandGlow.intensity = 0.85;
  if (C.doorInner) C.doorInner.intensity = 0.4;
  C.cam.position.set(0, 2.45, -49.5);
  C.yaw = 0; C.pitch = -0.05;
  C.cam.rotation.order = 'YXZ';
  C.cam.rotation.x = C.pitch; C.cam.rotation.y = C.yaw; C.cam.rotation.z = 0;
});

console.log('pageerrors', errs.length ? errs : 0);
// the per-shot logs above are only evidence if a bad one fails the run
const fin = await page.evaluate(() => window.__GM.getState());
if (errs.length) throw new Error(`page errors: ${errs.join(' | ')}`);
if (fin.errors !== 0) throw new Error(`in-page errors recorded during porch capture: ${fin.errors}`);
console.log('done');
} finally {
  await browser.close();
  server.close();
}
