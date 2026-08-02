#!/usr/bin/env node
/** Short horror-atmosphere gate: key poses after fog/ambient pass. */
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';

const ROOT = process.cwd(), PORT = 3855, OUT = 'docs/playtest/screenshots';
const MIME = {
  '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript',
  '.glb': 'model/gltf-binary', '.gltf': 'model/gltf+json', '.bin': 'application/octet-stream',
  '.png': 'image/png', '.ogg': 'audio/ogg', '.css': 'text/css', '.jpg': 'image/jpeg',
};
const server = createServer(async (req, res) => {
  try {
    let p = decodeURIComponent(req.url.split('?')[0]);
    const buf = await readFile(join(ROOT, p));
    res.writeHead(200, { 'Content-Type': MIME[extname(p)] || 'application/octet-stream' });
    res.end(buf);
  } catch { res.writeHead(404); res.end('nf'); }
});
await new Promise((r) => server.listen(PORT, r));
const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Prologue.dc.html`, { waitUntil: 'load' });
await page.waitForFunction(() => window.__GMC?.carModel && window.__GMC?.mansionModel && window.__GMC?._ruinsBuilt, null, { timeout: 120000 });
await page.waitForFunction(() => window.__GMC?._porchSconcesBuilt === 'env-lamp' || window.__GMC?._porchSconcesBuilt === 'bracket', null, { timeout: 90000 }).catch(() => {});
await page.waitForTimeout(1500);

const shot = async (name, fn) => {
  await page.evaluate((code) => {
    const C = window.__GMC;
    window.__GM.goTo('walk');
    C.setState({ hintOn: false, beatOn: false, tutStep: 99, fade: 0, cinematic: false });
    C.walkEnabled = false;
    if (C._raf) { cancelAnimationFrame(C._raf); C._raf = null; }
  });
  await page.evaluate(fn);
  await page.waitForTimeout(120);
  await page.locator('canvas').first().screenshot({ path: `${OUT}/${name}.png` });
  console.log('shot', name);
};

await shot('horror-01-spawn', () => {
  const C = window.__GMC;
  C.cam.position.set(0, 1.7, 72); C.yaw = 0; C.pitch = 0.02;
  C.cam.rotation.set(0.02, 0, 0, 'YXZ');
  C.renderer.render(C.scene, C.cam);
});
await shot('horror-02-cemetery', () => {
  const C = window.__GMC;
  C.cam.position.set(0, 1.7, 28); C.yaw = -1.15; C.pitch = 0.05;
  C.cam.rotation.set(0.05, -1.15, 0, 'YXZ');
  C.renderer.render(C.scene, C.cam);
});
await shot('horror-03-mid', () => {
  const C = window.__GMC;
  C.cam.position.set(0, 1.7, 8); C.yaw = 0; C.pitch = 0.04;
  C.cam.rotation.set(0.04, 0, 0, 'YXZ');
  C.renderer.render(C.scene, C.cam);
});
await shot('horror-04-porch', () => {
  const C = window.__GMC;
  C.cam.position.set(0, 2.0, -42); C.yaw = 0; C.pitch = 0.1;
  C.cam.rotation.set(0.1, 0, 0, 'YXZ');
  C.renderer.render(C.scene, C.cam);
});
await shot('horror-05-door', () => {
  const C = window.__GMC;
  C.cam.position.set(0, 2.2, -48); C.yaw = 0; C.pitch = 0.08;
  C.cam.rotation.set(0.08, 0, 0, 'YXZ');
  C.renderer.render(C.scene, C.cam);
});
await shot('horror-06-gate-back', () => {
  const C = window.__GMC;
  C.cam.position.set(0, 1.7, 63); C.yaw = Math.PI; C.pitch = 0.02;
  C.cam.rotation.set(0.02, Math.PI, 0, 'YXZ');
  C.renderer.render(C.scene, C.cam);
});

await shot('horror-07-cemetery-close', () => {
  const C = window.__GMC;
  C.cam.position.set(4.5, 1.65, 26); C.yaw = -1.05; C.pitch = 0.08;
  C.cam.rotation.set(0.08, -1.05, 0, 'YXZ');
  C.renderer.render(C.scene, C.cam);
});
await shot('horror-08-car-arrival', () => {
  const C = window.__GMC;
  const box = new THREE.Box3().setFromObject(C.carModel);
  const cx = (box.min.x + box.max.x) / 2;
  C.cam.position.set(cx - 3.2, 1.5, box.min.z - 3.0);
  C.yaw = -2.5; C.pitch = 0.05;
  C.cam.rotation.set(0.05, -2.5, 0, 'YXZ');
  C.renderer.render(C.scene, C.cam);
});
await shot('horror-09-porch-dress', () => {
  const C = window.__GMC;
  C.cam.position.set(0, 2.4, -49.5); C.yaw = 0; C.pitch = 0.12;
  C.cam.rotation.set(0.12, 0, 0, 'YXZ');
  C.renderer.render(C.scene, C.cam);
});

const info = await page.evaluate(() => {
  const C = window.__GMC;
  let basicWalls = 0, stdWalls = 0;
  C.scene.traverse((o) => {
    if (!o.isMesh || !o.material) return;
    const m = Array.isArray(o.material) ? o.material[0] : o.material;
    const n = (o.name || '').toLowerCase();
    if (!/wall|column|arch|pot|barrel|crate/.test(n + (o.parent?.name || ''))) return;
    if (m.isMeshBasicMaterial) basicWalls++;
    else stdWalls++;
  });
  return {
    fog: C.scene.fog?.density,
    ambient: C.scene.children.find((o) => o.isAmbientLight)?.intensity,
    porch: C._porchSconcesBuilt,
    cols: !!C._envPorchCols,
    basicWalls, stdWalls,
  };
});
console.log('info', info);
await browser.close();
server.close();
console.log('HORROR GATE DONE');
