// Finds any mesh in the scene whose material colour reads green-dominant (g clearly > r and b),
// near the front yard / cemetery, so we can identify the odd bright-green box seen in car-3q.
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';
const ROOT = process.cwd(), PORT = 3801;
const MIME = { '.html':'text/html','.js':'text/javascript','.json':'application/json','.glb':'model/gltf-binary','.gltf':'model/gltf+json','.bin':'application/octet-stream','.png':'image/png','.jpg':'image/jpeg','.ogg':'audio/ogg' };
const server = createServer(async (req,res)=>{ try{ let p=decodeURIComponent(req.url.split('?')[0]); if(p==='/')p='/index.html'; const fp=join(ROOT,p); const buf=await readFile(fp); res.writeHead(200,{'Content-Type':MIME[extname(fp)]||'application/octet-stream'}); res.end(buf);}catch{res.writeHead(404);res.end('nf');} });
await new Promise(r=>server.listen(PORT,r));
const browser = await chromium.launch();
const page = await browser.newPage({ viewport:{ width:1280, height:720 } });
await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Prologue.dc.html`,{ waitUntil:'load' });
await page.waitForFunction(()=>window.__GMC && window.__GMC.scene, null, { timeout:60000 });
await page.waitForFunction(()=>window.__GMC && window.__GMC._ruinsBuilt!==undefined, null, { timeout:60000 }).catch(()=>{});
await page.waitForTimeout(2000);
const greens = await page.evaluate(()=>{
  const THREE = window.THREE; const out=[];
  const box = new THREE.Box3(); const c=new THREE.Color();
  window.__GMC.scene.traverse(o=>{
    if(!o.isMesh || !o.material) return;
    const ms = Array.isArray(o.material)?o.material:[o.material];
    for(const m of ms){ if(!m.color) continue; const {r,g,b}=m.color;
      if (g>0.18 && g>r*1.4 && g>b*1.4){
        box.setFromObject(o); const ctr=box.getCenter(new THREE.Vector3());
        out.push({ name:o.name||'(unnamed)', matName:m.name||'', rgb:[+r.toFixed(2),+g.toFixed(2),+b.toFixed(2)], pos:[+ctr.x.toFixed(1),+ctr.y.toFixed(1),+ctr.z.toFixed(1)], size:[+(box.max.x-box.min.x).toFixed(1),+(box.max.y-box.min.y).toFixed(1),+(box.max.z-box.min.z).toFixed(1)] });
        break;
      }
    }
  });
  return out;
});
console.log('green-dominant meshes:', JSON.stringify(greens,null,2));
await browser.close(); server.close();
