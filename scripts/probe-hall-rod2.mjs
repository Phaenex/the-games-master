// Hide-and-seek: find which scene object owns the dark diagonal rod pixels in the Entry Hall
// (raycast missed it — likely a Line object or raycast-transparent mesh).
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';
const ROOT = process.cwd(), PORT = 3818;
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
const out = await page.evaluate(()=>{
  const C = window.__GMC, THREE = window.THREE;
  if (C._raf) { cancelAnimationFrame(C._raf); C._raf = null; }
  const W=1280,H=720, rt = new THREE.WebGLRenderTarget(W,H), buf = new Uint8Array(4);
  const sample=(x,y)=>{ C.renderer.setRenderTarget(rt); C.renderer.render(C.scene,C.cam); C.renderer.readRenderTargetPixels(rt,x,H-y,1,1,buf); C.renderer.setRenderTarget(null); return [buf[0],buf[1],buf[2]]; };
  const lum=(c)=>(c[0]+c[1]+c[2])/3;
  const res = [];
  // scan across the rod's expected x range at a few heights to land exactly on it (it's dark vs lit bg)
  for (const y of [120, 180, 240]) {
    let rodX = null, best = 999;
    for (let x = 500; x <= 640; x += 2) { const v = lum(sample(x, y)); if (v < best) { best = v; rodX = x; } }
    const base = sample(rodX, y);
    let node = C.scene, found = null, chain = [];
    for (let depth = 0; depth < 8; depth++) {
      let culprit = null;
      for (const child of node.children) {
        if (!child.visible) continue;
        child.visible = false;
        const v = sample(rodX, y);
        child.visible = true;
        if (lum(v) > lum(base) * 1.8 + 6) { culprit = child; break; } // rod is DARK: hiding it brightens
      }
      if (!culprit) break;
      found = culprit; chain.push(`${culprit.type}:${culprit.name||'·'}`);
      node = culprit;
      if (!culprit.children || !culprit.children.length) break;
    }
    let desc = null;
    if (found) {
      const box = new THREE.Box3().setFromObject(found); const size = box.getSize(new THREE.Vector3());
      const wp = new THREE.Vector3(); found.getWorldPosition(wp);
      const m = found.material && (Array.isArray(found.material)?found.material[0]:found.material);
      desc = { chain: chain.join('>'), type: found.type, geo: found.geometry ? found.geometry.type : null,
        mat: m?`${m.type}#${m.color?m.color.getHexString():'?'} map:${!!m.map}`:null,
        pos:[+wp.x.toFixed(2),+wp.y.toFixed(2),+wp.z.toFixed(2)], rot:[+found.rotation.x.toFixed(2),+found.rotation.y.toFixed(2),+found.rotation.z.toFixed(2)],
        size:[+size.x.toFixed(2),+size.y.toFixed(2),+size.z.toFixed(2)] };
    }
    res.push({ y, rodX, rodRGB: base, found: desc });
  }
  rt.dispose();
  return res;
});
for (const r of out) console.log(JSON.stringify(r));
await browser.close(); server.close();
