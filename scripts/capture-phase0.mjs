// One-off Phase 0 visual-gate capture. Drives the Prologue with Playwright (Node-side, so no 5s
// MCP screenshot cap), poses the camera at each required view, and writes PNGs to docs/playtest/screenshots.
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';

const ROOT = process.cwd();
const PORT = 3789;
const MIME = { '.html':'text/html', '.js':'text/javascript', '.mjs':'text/javascript', '.json':'application/json', '.glb':'model/gltf-binary', '.gltf':'model/gltf+json', '.bin':'application/octet-stream', '.png':'image/png', '.jpg':'image/jpeg', '.jpeg':'image/jpeg', '.ogg':'audio/ogg' };

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
await new Promise(r => server.listen(PORT, r));

const url = `http://localhost:${PORT}/The%20Games%20Master%20-%20Prologue.dc.html`;
const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
await page.goto(url, { waitUntil: 'load' });
await page.waitForFunction(() => window.__GMC && window.__GMC.scene, null, { timeout: 15000 });
// let GLTFs (car, gate, fence, mansion) finish, and give ruins_pack grounds time to build
await page.waitForFunction(() => window.__GMC && window.__GMC.carModel && window.__GMC.mansionModel, null, { timeout: 20000 }).catch(()=>{});
await page.waitForFunction(() => window.__GMC && window.__GMC._ruinsBuilt !== undefined, null, { timeout: 25000 }).catch(()=>{});
await page.waitForTimeout(2500);

const shots = [
  { name: 'phase0-01-spawn-facing-house', pose: { x:0, y:1.7, z:72, yaw:0, pitch:0.02 } },
  { name: 'phase0-02-spawn-looking-at-car', pose: { x:0, y:1.7, z:73, yaw:Math.PI, pitch:0.0 } },
  { name: 'phase0-03-gate', pose: { x:0, y:1.7, z:71, yaw:0, pitch:0.05 } },
  { name: 'phase0-04-mid-approach', pose: { x:0, y:1.7, z:-38, yaw:0, pitch:0.06 } },
  { name: 'phase0-05-facade-door', pose: { x:0, y:1.7, z:-46, yaw:0, pitch:0.12 } },
  { name: 'phase0-06-cemetery', pose: { x:0, y:1.7, z:30, yaw:-1.2, pitch:0.02 } },
  { name: 'phase0-07-cemetery-close', pose: { x:6, y:1.7, z:34, yaw:-0.5, pitch:0.0 } },
  { name: 'phase0-08-garden-fountain', pose: { x:0, y:1.7, z:26, yaw:1.2, pitch:0.02 } },
  { name: 'phase0-09-overview', pose: { x:0, y:16, z:52, yaw:0, pitch:0.42 } },
  { name: 'phase0-10-sky-moon', pose: { x:0, y:1.7, z:45, yaw:0.4, pitch:0.5 } },
  { name: 'phase0-11-behind-mansion', pose: { x:0, y:1.7, z:-40, yaw:Math.PI, pitch:0.03 } },
];

for (const s of shots) {
  await page.evaluate((pose) => {
    const C = window.__GMC;
    window.__GM.goTo('walk');
    C.walkEnabled = false; // freeze so the pose sticks for the shot
    if (C._raf) { cancelAnimationFrame(C._raf); C._raf = null; }
    C.cam.position.set(pose.x, pose.y, pose.z);
    C.yaw = pose.yaw; C.pitch = pose.pitch;
    C.cam.rotation.set(C.pitch, C.yaw, 0, 'YXZ');
    C.renderer.render(C.scene, C.cam);
  }, s.pose);
  await page.waitForTimeout(250);
  await page.screenshot({ path: `docs/playtest/screenshots/${s.name}.png`, timeout: 60000 });
  console.log('captured', s.name);
}

// scale report — world-space bbox of every tagged estate object + the hero singles, vs 1.7u eye height
const report = await page.evaluate(() => {
  const THREE = window.THREE, C = window.__GMC, S = C.scene;
  const box = (o) => { const b = new THREE.Box3().setFromObject(o); const s = b.getSize(new THREE.Vector3()); return [+s.x.toFixed(2), +s.y.toFixed(2), +s.z.toFixed(2)]; };
  const kinds = {};
  S.traverse(o => { if (o.userData && o.userData.gmKind) { const k = o.userData.gmKind; (kinds[k] = kinds[k] || []).push(box(o)); } });
  const summ = {};
  for (const k in kinds) { const hs = kinds[k].map(a => a[1]).sort((a, b) => a - b); summ[k] = { n: kinds[k].length, hMin: hs[0], hMed: hs[Math.floor(hs.length/2)], hMax: hs[hs.length-1], sample: kinds[k][0] }; }
  const singles = {};
  if (C.mansionModel) singles.mansion = box(C.mansionModel);
  if (C.gateModel) singles.gate = box(C.gateModel);
  if (C.carModel) singles.car = box(C.carModel);
  if (C.fenceModel) singles.fencePanel = box(C.fenceModel);
  return { eyeHeight: 1.7, ruinsBuilt: C._ruinsBuilt, kinds: summ, singles };
});
console.log('SCALE REPORT\n' + JSON.stringify(report, null, 2));

await browser.close();
server.close();
console.log('done');
