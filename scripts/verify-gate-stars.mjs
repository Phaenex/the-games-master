// Verify closed gate blocks retreat through bars + stars exist all around.
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';

const ROOT = process.cwd(), PORT = 3818;
const MIME = { '.html':'text/html','.js':'text/javascript','.json':'application/json','.glb':'model/gltf-binary','.gltf':'model/gltf+json','.bin':'application/octet-stream','.png':'image/png','.ogg':'audio/ogg','.jpg':'image/jpeg' };
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
page.on('pageerror', (e) => console.log('PAGEERR', e.message));
await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Prologue.dc.html`, { waitUntil: 'load' });
await page.waitForFunction(() => window.__GMC && window.__GMC.scene && window.__GMC.starField, null, { timeout: 120000 });
await page.evaluate(() => { window.__GM.goTo('walk'); });
await page.waitForTimeout(800);

const stars = await page.evaluate(() => {
  const s = window.__GMC.starField.geometry.attributes.position.count;
  const b = window.__GMC.starFieldBright ? window.__GMC.starFieldBright.geometry.attributes.position.count : 0;
  // sample average |x|,|z| spread
  const arr = window.__GMC.starField.geometry.attributes.position.array;
  let maxX = 0, maxZ = 0, minZ = 0;
  for (let i = 0; i < arr.length; i += 3) {
    maxX = Math.max(maxX, Math.abs(arr[i]));
    maxZ = Math.max(maxZ, Math.abs(arr[i + 2]));
    minZ = Math.min(minZ, arr[i + 2]);
  }
  return { s, b, maxX, maxZ, minZ };
});
console.log('stars', stars);
if (stars.s < 2000) throw new Error('too few stars ' + stars.s);
if (stars.maxX < 100 || stars.maxZ < 100) throw new Error('stars not full-sky ' + JSON.stringify(stars));

await page.evaluate(() => {
  const C = window.__GMC;
  C._passedGate = true;
  C.triggerGateLock();
  C._gateLockFired = true;
  C.gateClosed = 1;
  if (C.gateLeafL) C.gateLeafL.rotation.y = C._gateShutL;
  if (C.gateLeafR) C.gateLeafR.rotation.y = C._gateShutR;
  C.cam.position.set(0, 1.7, C.gateZ - 2.5);
  C.yaw = Math.PI; // face the gate (look toward +Z / car)
  C.pitch = 0;
  C.walkEnabled = true;
  C.mode = 'walk';
  C.keys = { s: true }; // walk toward +Z into the closed gate
});
await page.waitForTimeout(2200);
const gate = await page.evaluate(() => {
  const C = window.__GMC;
  const z = C.cam.position.z;
  return { z, gateZ: C.gateZ, blocked: z <= C.gateZ - 0.75 };
});
console.log('gate block', gate);
if (!gate.blocked) throw new Error('gate walk-through still possible ' + JSON.stringify(gate));
await page.evaluate(() => { window.__GMC.keys = {}; });

// look left / back sky shots
await page.evaluate(() => {
  const C = window.__GMC;
  C.cam.position.set(0, 1.7, 40);
  C.yaw = Math.PI / 2; C.pitch = 0.35;
  C.cam.rotation.set(C.pitch, C.yaw, 0, 'YXZ');
  if (C.starField) C.starField.position.set(C.cam.position.x, 0, C.cam.position.z);
  if (C.starFieldBright) C.starFieldBright.position.set(C.cam.position.x, 0, C.cam.position.z);
});
await page.waitForTimeout(400);
await page.screenshot({ path: 'docs/playtest/screenshots/sky-01-look-left.png' });
await page.evaluate(() => {
  const C = window.__GMC;
  C.yaw = Math.PI; C.pitch = 0.45;
  C.cam.rotation.set(C.pitch, C.yaw, 0, 'YXZ');
});
await page.waitForTimeout(400);
await page.screenshot({ path: 'docs/playtest/screenshots/sky-02-look-back.png' });
await page.evaluate(() => {
  const C = window.__GMC;
  C.cam.position.set(0, 1.7, C.gateZ - 1.0);
  C.yaw = 0; C.pitch = 0;
  C.cam.rotation.set(0, 0, 0, 'YXZ');
  if (C.gateLeafL) C.gateLeafL.rotation.y = C._gateShutL;
  if (C.gateLeafR) C.gateLeafR.rotation.y = C._gateShutR;
});
await page.waitForTimeout(400);
await page.screenshot({ path: 'docs/playtest/screenshots/gate-block-closed.png' });

console.log('PASS gate+stars');
await browser.close();
server.close();
