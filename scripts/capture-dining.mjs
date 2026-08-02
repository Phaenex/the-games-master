// Entry Hall dining wing reference shots for Court redress.
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';

const ROOT = process.cwd(), PORT = 3812;
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
const errs = [];
page.on('pageerror', (e) => errs.push(e.message));
await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Entry%20Hall.dc.html`, { waitUntil: 'load' });
await page.waitForFunction(() => window.__GM && window.__GMC && window.__GMC.scene, null, { timeout: 45000 });
await page.evaluate(() => {
  window.__GM.goTo('hall');
  const C = window.__GMC;
  C.walkEnabled = false;
  C._beat = 99;
  (C.pois || []).forEach((p) => { p.said = true; });
  C.setState({ beatOn: false, promptOn: false, hintOn: false, fade: 0 });
});
await page.waitForTimeout(1200);

async function shot(name, x, y, z, yaw, pitch) {
  await page.evaluate(({ x, y, z, yaw, pitch }) => {
    const C = window.__GMC;
    C.setState({ beatOn: false, promptOn: false });
    C.cam.position.set(x, y, z);
    C.yaw = yaw; C.pitch = pitch || 0;
    C.cam.rotation.order = 'YXZ';
    C.cam.rotation.x = C.pitch; C.cam.rotation.y = C.yaw; C.cam.rotation.z = 0;
  }, { x, y, z, yaw, pitch });
  await page.waitForTimeout(700);
  const path = `docs/playtest/screenshots/${name}.png`;
  await page.screenshot({ path });
  console.log('shot', name);
}

// diningSet(-16,-14.5) inside roomShell(-24,-8.5,-24,-5)
// yaw=0 looks -Z (into house); yaw=PI looks +Z (toward hall)
await shot('dining-01-from-hall', -7.2, 1.7, -14.5, -Math.PI / 2, -0.05);
await shot('dining-02-table', -16, 1.85, -9.8, 0, -0.18);
await shot('dining-03-head', -16, 1.85, -19.2, Math.PI, -0.12);
await shot('dining-04-sideboard', -21.5, 1.7, -14.5, Math.PI / 2, -0.05);

console.log('pageerrors', errs.length ? errs : 0);
await browser.close();
server.close();
