// Targeted plaque check: pose in front of the pier plate, screenshot, and dump the plaque's
// live material + measured screen pixels so "too pale" is a number, not an impression.
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';
const ROOT = process.cwd(), PORT = 3822, DIR = 'docs/playtest/screenshots';
const MIME = { '.html':'text/html','.js':'text/javascript','.json':'application/json','.glb':'model/gltf-binary','.gltf':'model/gltf+json','.bin':'application/octet-stream','.png':'image/png','.ogg':'audio/ogg' };
const server = createServer(async (req,res)=>{ try{ let p=decodeURIComponent(req.url.split('?')[0]); if(p==='/')p='/index.html'; const fp=join(ROOT,p); const buf=await readFile(fp); res.writeHead(200,{'Content-Type':MIME[extname(fp)]||'application/octet-stream'}); res.end(buf);}catch{res.writeHead(404);res.end('nf');} });
await new Promise(r=>server.listen(PORT,r));
const browser = await chromium.launch();
const page = await browser.newPage({ viewport:{ width:1280, height:720 } });
const errs=[]; page.on('pageerror', e=>{ errs.push(e.message); console.log('[pageerror]', e.message); });
await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Prologue.dc.html`,{ waitUntil:'load' });
await page.waitForFunction(()=>window.__GMC && window.__GMC.scene && window.__GMC._ruinsBuilt, null, { timeout:120000 });
await page.waitForTimeout(1500);
const info = await page.evaluate(()=>{
  const C = window.__GMC; let pl = null;
  C.scene.traverse(o=>{ if(o.userData && o.userData.gmKind==='gatePlaque') pl = o; });
  if (!pl) return { found:false };
  window.__GM.goTo('walk'); C.walkEnabled=false;
  if (C._raf) { cancelAnimationFrame(C._raf); C._raf=null; }
  C.cam.position.set(2.6, 1.85, 67.6); C.yaw = 0.08; C.pitch = 0.02;
  C.cam.rotation.set(C.pitch, C.yaw, 0, 'YXZ');
  C.renderer.render(C.scene, C.cam);
  const m = pl.material;
  return { found:true, pos: pl.position.toArray(), matColor: m.color.getHexString(), hasMap: !!m.map,
    mapEncoding: m.map ? m.map.encoding : null, toneMapped: m.toneMapped !== false, fog: m.fog !== false };
});
console.log('plaque:', JSON.stringify(info));
await page.waitForTimeout(150);
await page.screenshot({ path:`${DIR}/estate-12-gate-plaque.png`, timeout:60000 });
console.log('shot estate-12-gate-plaque | errors:', errs.length);
await browser.close(); server.close();
