// Porch flank + mansion surround poses after the flank-fill pass.
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';
const ROOT = process.cwd(), PORT = 3803;
const MIME = { '.html':'text/html','.js':'text/javascript','.json':'application/json','.glb':'model/gltf-binary','.gltf':'model/gltf+json','.bin':'application/octet-stream','.png':'image/png','.ogg':'audio/ogg' };
const server = createServer(async (req,res)=>{ try{ let p=decodeURIComponent(req.url.split('?')[0]); if(p==='/')p='/index.html'; const fp=join(ROOT,p); const buf=await readFile(fp); res.writeHead(200,{'Content-Type':MIME[extname(fp)]||'application/octet-stream'}); res.end(buf);}catch{res.writeHead(404);res.end('nf');} });
await new Promise(r=>server.listen(PORT,r));
const browser = await chromium.launch();
const page = await browser.newPage({ viewport:{ width:1280, height:720 } });
try {
const errs=[]; page.on('pageerror', e=>{ errs.push(e.message); console.log('[pageerror]', e.message); });
await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Prologue.dc.html`,{ waitUntil:'load' });
await page.waitForFunction(()=>window.__GMC && window.__GMC._ruinsBuilt, null, { timeout:120000 });
await page.waitForTimeout(2000);
const pose = async (x,y,z,yaw,pitch=0)=>{ await page.evaluate(([px,py,pz,y,p])=>{ const C=window.__GMC; window.__GM.goTo('walk'); C.walkEnabled=false; C.cam.position.set(px,py,pz); C.yaw=y; C.pitch=p; C.cam.rotation.set(p,y,0,'YXZ'); C.renderer.render(C.scene,C.cam); }, [x,y,z,yaw,pitch]); await page.waitForTimeout(180); };
const shots = [
  ['mansion-porch-left',  -2.8,1.7,-38,-1.35,0.05],
  ['mansion-porch-right',  2.8,1.7,-38, 1.35,0.05],
  ['mansion-porch-door',   0,  1.7,-38, 0,    0.22],
  ['mansion-cemetery',   -2.5,1.7, 22,-1.25,0.04],
  ['mansion-garden',       2.5,1.7, 20, 1.25,0.04],
  ['mansion-left-flank',  -2.8,1.7,-20,-1.45,0.08],
  ['mansion-lookback',     0,  1.7, 15, Math.PI,0.02],
];
for (const [n,...p] of shots) { await pose(...p); await page.screenshot({ path:`docs/playtest/screenshots/${n}.png`, timeout:60000 }); console.log('shot', n); }
// a posed shot of a scene that threw is not evidence — fail the run, don't just print 'done'
if (errs.length) throw new Error(`page errors: ${errs.join(' | ')}`);
console.log('done');
} finally {
  await browser.close();
  server.close();
}
