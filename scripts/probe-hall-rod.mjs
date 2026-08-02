// Identify the black diagonal rods floating across the Entry Hall staircase (seen in
// hallwake-portraits.png) by raycasting through their screen positions.
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';
const ROOT = process.cwd(), PORT = 3817;
const MIME = { '.html':'text/html','.js':'text/javascript','.json':'application/json','.glb':'model/gltf-binary','.gltf':'model/gltf+json','.bin':'application/octet-stream','.png':'image/png','.jpg':'image/jpeg','.ogg':'audio/ogg' };
const server = createServer(async (req,res)=>{ try{ let p=decodeURIComponent(req.url.split('?')[0]); if(p==='/')p='/index.html'; const fp=join(ROOT,p); const buf=await readFile(fp); res.writeHead(200,{'Content-Type':MIME[extname(fp)]||'application/octet-stream'}); res.end(buf);}catch{res.writeHead(404);res.end('nf');} });
await new Promise(r=>server.listen(PORT,r));
const browser = await chromium.launch();
const page = await browser.newPage({ viewport:{ width:1280, height:720 } });
await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Entry%20Hall.dc.html`,{ waitUntil:'load' });
await page.waitForFunction(()=>window.__GMC && window.__GMC.scene, null, { timeout:120000 });
await page.waitForTimeout(5000);
await page.evaluate(()=>window.__GM.goTo('portraits'));
await page.waitForTimeout(900);
const hits = await page.evaluate(()=>{
  const C = window.__GMC, THREE = window.THREE;
  if (C._raf) { cancelAnimationFrame(C._raf); C._raf = null; }
  C.renderer.render(C.scene, C.cam);
  const ray = new THREE.Raycaster(); ray.far = 200;
  const out = [];
  // pixel coords along the two rods (1280x720) -> NDC
  const px = [[575,95],[565,140],[555,200],[548,250],[540,300]];
  for (const [x, y] of px) {
    ray.setFromCamera(new THREE.Vector2((x/1280)*2-1, -(y/720)*2+1), C.cam);
    const hit = ray.intersectObjects(C.scene.children, true).find(h=>h.object.visible && h.object.isMesh);
    if (!hit) { out.push({ px:[x,y], hit:null }); continue; }
    const o = hit.object;
    const names = []; for (let p=o,i=0; p&&i<7; p=p.parent,i++) names.push(p.name || p.userData?.gmKind || p.type || '·');
    const m = Array.isArray(o.material) ? o.material[0] : o.material;
    const wp = new THREE.Vector3(); o.getWorldPosition(wp);
    const box = new THREE.Box3().setFromObject(o); const size = box.getSize(new THREE.Vector3());
    const root = (() => { let p = o; while (p.parent && p.parent.type !== 'Scene') p = p.parent; return p; })();
    const rbox = new THREE.Box3().setFromObject(root); const rsize = rbox.getSize(new THREE.Vector3());
    out.push({ px:[x,y], dist:+hit.distance.toFixed(1), path:names.join('>'), mat:m?`${m.type}#${m.color?m.color.getHexString():'?'} map:${!!m.map}`:null,
      rot:[+o.rotation.x.toFixed(2),+o.rotation.y.toFixed(2),+o.rotation.z.toFixed(2)],
      worldPos:[+wp.x.toFixed(1),+wp.y.toFixed(1),+wp.z.toFixed(1)], size:[+size.x.toFixed(2),+size.y.toFixed(2),+size.z.toFixed(2)],
      rootType:root.type, rootName:root.name||'(unnamed)', rootKids:root.children.length,
      rootPos:[+root.position.x.toFixed(1),+root.position.y.toFixed(1),+root.position.z.toFixed(1)],
      rootSize:[+rsize.x.toFixed(2),+rsize.y.toFixed(2),+rsize.z.toFixed(2)] });
  }
  return out;
});
for (const h of hits) console.log(JSON.stringify(h));
await browser.close(); server.close();
