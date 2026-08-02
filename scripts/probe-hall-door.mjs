// Enumerate all meshes near the Entry Hall parlor doorway with their rotations/sizes —
// hunting a stray door leaf standing edge-on (the diagonal "rod" in the wake-path shots).
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';
const ROOT = process.cwd(), PORT = 3819;
const MIME = { '.html':'text/html','.js':'text/javascript','.json':'application/json','.glb':'model/gltf-binary','.gltf':'model/gltf+json','.bin':'application/octet-stream','.png':'image/png','.jpg':'image/jpeg','.ogg':'audio/ogg' };
const server = createServer(async (req,res)=>{ try{ let p=decodeURIComponent(req.url.split('?')[0]); if(p==='/')p='/index.html'; const fp=join(ROOT,p); const buf=await readFile(fp); res.writeHead(200,{'Content-Type':MIME[extname(fp)]||'application/octet-stream'}); res.end(buf);}catch{res.writeHead(404);res.end('nf');} });
await new Promise(r=>server.listen(PORT,r));
const browser = await chromium.launch();
const page = await browser.newPage({ viewport:{ width:1280, height:720 } });
await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Entry%20Hall.dc.html`,{ waitUntil:'load' });
await page.waitForFunction(()=>window.__GMC && window.__GMC.scene, null, { timeout:120000 });
await page.waitForTimeout(5000);
const rows = await page.evaluate(()=>{
  const C = window.__GMC, THREE = window.THREE;
  const out = [];
  C.scene.traverse((o)=>{
    if (!o.isMesh || !o.visible) return;
    const wp = new THREE.Vector3(); o.getWorldPosition(wp);
    if (Math.abs(wp.x) > 4 || wp.z < -35.5 || wp.z > -30 || wp.y > 8) return;
    const box = new THREE.Box3().setFromObject(o); const s = box.getSize(new THREE.Vector3());
    if (Math.max(s.x, s.y, s.z) < 1.2) return; // skip knobs/small trim
    const names = []; for (let p=o,i=0; p&&i<5; p=p.parent,i++) names.push(p.name || p.type);
    const wq = new THREE.Quaternion(); o.getWorldQuaternion(wq);
    const e = new THREE.Euler().setFromQuaternion(wq, 'YXZ');
    out.push({ path:names.join('>'), pos:[+wp.x.toFixed(2),+wp.y.toFixed(2),+wp.z.toFixed(2)],
      size:[+s.x.toFixed(2),+s.y.toFixed(2),+s.z.toFixed(2)],
      worldRot:[+e.x.toFixed(2),+e.y.toFixed(2),+e.z.toFixed(2)] });
  });
  return out;
});
for (const r of rows) console.log(JSON.stringify(r));
console.log('total:', rows.length);
await browser.close(); server.close();
