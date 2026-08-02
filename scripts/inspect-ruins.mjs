// Loads ruins_pack.glb (and lantern) in a real headless browser via the same GLTFLoader the game uses,
// then logs every mesh child with its true world-space size + local origin offset, so I can place
// cemetery/estate pieces at correct scale.
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';

const ROOT = process.cwd();
const PORT = 3790;
const MIME = { '.html':'text/html','.js':'text/javascript','.mjs':'text/javascript','.json':'application/json','.glb':'model/gltf-binary','.gltf':'model/gltf+json','.bin':'application/octet-stream','.png':'image/png','.jpg':'image/jpeg' };
const server = createServer(async (req, res) => {
  try { let p = decodeURIComponent(req.url.split('?')[0]); if (p==='/') p='/x.html'; const buf = await readFile(join(ROOT, p)); res.writeHead(200,{'Content-Type':MIME[extname(p)]||'application/octet-stream'}); res.end(buf); }
  catch { res.writeHead(404); res.end('nf'); }
});
await new Promise(r => server.listen(PORT, r));

const html = `<!doctype html><html><head><meta charset=utf-8>
<script src="https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js"></script>
<script src="https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/loaders/GLTFLoader.js"></script>
</head><body><script>
window.__done=false; window.__out={};
const loader=new THREE.GLTFLoader();
function measure(url, key){ return new Promise(res=>{ loader.load(url, g=>{
  const root=g.scene; root.updateMatrixWorld(true);
  const whole=new THREE.Box3().setFromObject(root); const ws=whole.getSize(new THREE.Vector3());
  const parts=[];
  root.traverse(o=>{ if(o.isMesh){ const b=new THREE.Box3().setFromObject(o); const s=b.getSize(new THREE.Vector3()); parts.push({name:o.name, size:[+s.x.toFixed(2),+s.y.toFixed(2),+s.z.toFixed(2)]}); } });
  res({whole:[+ws.x.toFixed(2),+ws.y.toFixed(2),+ws.z.toFixed(2)], parts}); }, undefined, e=>res({error:String(e)})); }); }
(async()=>{ window.__out.ruins=await measure('assets/models/sourced/ruins_pack.glb'); window.__out.lantern=await measure('assets/models/sourced/lantern.glb'); window.__out.wall=await measure('assets/models/sourced/wall_modular.glb'); window.__done=true; })();
</script></body></html>`;

const browser = await chromium.launch();
const page = await browser.newPage();
await page.route('**/x.html', r => r.fulfill({ contentType:'text/html', body: html }));
await page.goto(`http://localhost:${PORT}/x.html`, { waitUntil:'load' });
await page.waitForFunction(() => window.__done, null, { timeout: 30000 });
const out = await page.evaluate(() => window.__out);
console.log('LANTERN whole:', JSON.stringify(out.lantern.whole));
console.log('WALL_MODULAR whole:', JSON.stringify(out.wall.whole));
console.log('RUINS whole:', JSON.stringify(out.ruins.whole));
console.log('RUINS parts:');
for (const p of out.ruins.parts || []) console.log('  ', p.name, JSON.stringify(p.size));
await browser.close();
server.close();
