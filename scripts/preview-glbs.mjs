#!/usr/bin/env node

// Render one or more local GLB files into neutral comparison shots. This is an
// intake tool: candidates stay outside the served asset tree until a human has
// looked at the shot and judged the printed bounding box. There is no numeric
// size threshold in here — the exit code gates render failures only.
import { chromium } from 'playwright';
import { createServer } from 'node:http';
import { readFile, mkdir } from 'node:fs/promises';
import { basename, extname, join, resolve } from 'node:path';

const root = process.cwd();
const args = process.argv.slice(2);
const outIndex = args.indexOf('--out');
const outDir = resolve(root, outIndex >= 0 ? args[outIndex + 1] : 'docs/playtest/screenshots/glb-candidates');
const files = outIndex >= 0
  ? args.filter((arg, index) => index !== outIndex && index !== outIndex + 1)
  : args;

if (!files.length) {
  console.error('Usage: node scripts/preview-glbs.mjs [--out folder] file.glb [more.glb]');
  process.exit(1);
}

await mkdir(outDir, { recursive: true });

const mime = {
  '.html': 'text/html',
  '.js': 'text/javascript',
  '.glb': 'model/gltf-binary',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
};

const server = createServer(async (req, res) => {
  try {
    const requestPath = decodeURIComponent((req.url || '/').split('?')[0]);
    const absolute = resolve(root, requestPath.replace(/^\/+/, ''));
    if (!absolute.startsWith(root)) throw new Error('outside root');
    const body = await readFile(absolute);
    res.writeHead(200, { 'Content-Type': mime[extname(absolute).toLowerCase()] || 'application/octet-stream' });
    res.end(body);
  } catch {
    res.writeHead(404);
    res.end('not found');
  }
});
await new Promise((done) => server.listen(0, '127.0.0.1', done));
const port = server.address().port;

const html = `<!doctype html><meta charset="utf-8">
<style>html,body{margin:0;width:100%;height:100%;overflow:hidden;background:#080a0d}canvas{display:block}</style>
<script src="https://cdn.jsdelivr.net/npm/three@0.128.0/build/three.min.js"></script>
<script src="https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/loaders/GLTFLoader.js"></script>
<script>
window.__ready = false;
window.__error = null;
window.renderCandidate = function(url) {
  window.__ready = false;
  window.__error = null;
  document.querySelectorAll('canvas').forEach((node) => node.remove());
  const scene = new THREE.Scene();
  scene.background = new THREE.Color(0x080a0d);
  const camera = new THREE.PerspectiveCamera(36, innerWidth / innerHeight, 0.01, 1000);
  const renderer = new THREE.WebGLRenderer({ antialias: true, preserveDrawingBuffer: true });
  renderer.setPixelRatio(1);
  renderer.setSize(innerWidth, innerHeight);
  renderer.outputEncoding = THREE.sRGBEncoding;
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.2;
  document.body.appendChild(renderer.domElement);
  scene.add(new THREE.HemisphereLight(0xb9c8df, 0x21180f, 1.3));
  const key = new THREE.DirectionalLight(0xffe0b0, 2.4);
  key.position.set(4, 7, 6);
  scene.add(key);
  const rim = new THREE.DirectionalLight(0x7299cc, 1.2);
  rim.position.set(-5, 4, -3);
  scene.add(rim);
  new THREE.GLTFLoader().load(url, (gltf) => {
    const model = gltf.scene;
    model.updateMatrixWorld(true);
    let box = new THREE.Box3().setFromObject(model);
    const size = box.getSize(new THREE.Vector3());
    const center = box.getCenter(new THREE.Vector3());
    const maxDim = Math.max(size.x, size.y, size.z) || 1;
    const scale = 5 / maxDim;
    model.scale.setScalar(scale);
    model.position.set(-center.x * scale, -box.min.y * scale - 2.35, -center.z * scale);
    model.traverse((object) => {
      if (!object.isMesh) return;
      object.castShadow = true;
      object.receiveShadow = true;
      const materials = Array.isArray(object.material) ? object.material : [object.material];
      for (const material of materials) {
        if (!material) continue;
        material.side = THREE.DoubleSide;
        if (!material.map && material.color && material.color.getHex() > 0xeeeeee) material.color.set(0x77706a);
      }
    });
    scene.add(model);
    const ground = new THREE.Mesh(
      new THREE.CircleGeometry(4.5, 64),
      new THREE.MeshStandardMaterial({ color: 0x171a19, roughness: 0.95 })
    );
    ground.rotation.x = -Math.PI / 2;
    ground.position.y = -2.36;
    scene.add(ground);
    camera.position.set(6.4, 3.6, 7.6);
    camera.lookAt(0, 0.1, 0);
    renderer.render(scene, camera);
    window.__bounds = [size.x, size.y, size.z].map((value) => Number(value.toFixed(3)));
    window.__ready = true;
  }, undefined, (error) => {
    window.__error = String(error);
    window.__ready = true;
  });
};
</script>`;

const slug = (text) => text.replace(/[^a-z0-9_-]+/gi, '-').toLowerCase();
const taken = new Map();
let failures = 0;

const browser = await chromium.launch();
try {
  const page = await browser.newPage({ viewport: { width: 1000, height: 800 }, deviceScaleFactor: 1 });
  const pageErrors = [];
  page.on('pageerror', (e) => { pageErrors.push(e.message); console.error('[pageerror]', e.message); });
  await page.route(`http://127.0.0.1:${port}/preview`, (route) => route.fulfill({ contentType: 'text/html', body: html }));
  await page.goto(`http://127.0.0.1:${port}/preview`, { waitUntil: 'networkidle' });

  for (const input of files) {
    const absolute = resolve(root, input);
    const relative = absolute.slice(root.length + 1).split('\\').join('/');
    let stem = slug(basename(input, extname(input)));
    // Same basename from two directories would otherwise overwrite one shot with the other.
    if (taken.has(stem) && taken.get(stem) !== relative) {
      const fromPath = slug(relative.replace(/\.[^.]+$/, ''));
      let unique = fromPath;
      for (let n = 2; taken.has(unique) && taken.get(unique) !== relative; n++) unique = `${fromPath}-${n}`;
      console.error(`NOTE ${input}: ${stem}.png already taken by ${taken.get(stem)}; writing ${unique}.png`);
      stem = unique;
    }
    taken.set(stem, relative);
    await page.evaluate((url) => window.renderCandidate(url), `http://127.0.0.1:${port}/${relative}`);
    await page.waitForFunction(() => window.__ready, null, { timeout: 60000 });
    const result = await page.evaluate(() => ({ error: window.__error, bounds: window.__bounds }));
    if (result.error) {
      console.error(`FAIL ${input}: ${result.error}`);
      failures++;
      continue;
    }
    const output = join(outDir, `${stem}.png`);
    await page.screenshot({ path: output });
    console.log(`${input} bounds=${JSON.stringify(result.bounds)} -> ${output}`);
  }
  failures += pageErrors.length;
} finally {
  await browser.close();
  server.close();
}

if (failures) {
  console.error(`preview-glbs: ${failures} failure(s) across ${files.length} candidate(s)`);
  process.exitCode = 1;
}
