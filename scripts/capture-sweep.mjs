// Broad visual sweep of the whole opening to hunt remaining issues: car close, outer entrance behind
// spawn, avenue trees (verify darkened), inside the cemetery, inside the ruined garden/fountain, and
// the mansion. Static poses for judging geometry/lighting; movement verified by play-gate.mjs.
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';
const ROOT = process.cwd(), PORT = 3800;
const MIME = { '.html':'text/html','.js':'text/javascript','.json':'application/json','.glb':'model/gltf-binary','.gltf':'model/gltf+json','.bin':'application/octet-stream','.png':'image/png','.ogg':'audio/ogg' };
const server = createServer(async (req,res)=>{ try{ let p=decodeURIComponent(req.url.split('?')[0]); if(p==='/')p='/index.html'; const fp=join(ROOT,p); const buf=await readFile(fp); res.writeHead(200,{'Content-Type':MIME[extname(fp)]||'application/octet-stream'}); res.end(buf);}catch{res.writeHead(404);res.end('nf');} });
await new Promise(r=>server.listen(PORT,r));
const browser = await chromium.launch();
const page = await browser.newPage({ viewport:{ width:1280, height:720 } });
page.on('pageerror', e=>console.log('[pageerror]', e.message));
await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Prologue.dc.html`,{ waitUntil:'load' });
await page.waitForFunction(()=>window.__GMC && window.__GMC.scene && window.__GMC._ruinsBuilt, null, { timeout:120000 }).catch(()=>{});
await page.waitForTimeout(2000);

const shots = [
  { name:'sweep-car-3q',       pose:{ x:-9,  y:1.6, z:71,  yaw:-2.42,   pitch:0.03 } },
  { name:'sweep-outer-gate',   pose:{ x:0.5, y:1.7, z:82,  yaw:Math.PI, pitch:0.03 } },
  { name:'sweep-avenue-trees', pose:{ x:1,   y:1.7, z:30,  yaw:-1.15,   pitch:0.05 } },
  { name:'sweep-cemetery-in',  pose:{ x:15,  y:1.7, z:22,  yaw:0.5,     pitch:0.02 } },
  { name:'sweep-garden-in',    pose:{ x:-16, y:1.7, z:20,  yaw:0.2,     pitch:0.03 } },
  { name:'sweep-yard-clutter', pose:{ x:2,   y:1.7, z:64,  yaw:-0.7,    pitch:0.02 } },
  { name:'sweep-facade',       pose:{ x:0,   y:1.7, z:-40, yaw:0,       pitch:0.14 } },
];
for (const s of shots) {
  await page.evaluate((pose)=>{
    const C=window.__GMC; window.__GM.goTo('walk'); C.walkEnabled=false;
    if(C._raf){ cancelAnimationFrame(C._raf); C._raf=null; }
    C.cam.position.set(pose.x,pose.y,pose.z); C.yaw=pose.yaw; C.pitch=pose.pitch;
    C.cam.rotation.set(C.pitch,C.yaw,0,'YXZ'); C.renderer.render(C.scene,C.cam);
  }, s.pose);
  await page.waitForTimeout(150);
  await page.screenshot({ path:`docs/playtest/screenshots/${s.name}.png`, timeout:60000 });
  console.log('shot', s.name);
}
await browser.close(); server.close(); console.log('done');
