import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';
const ROOT = process.cwd(), PORT = 3791;
const MIME = { '.html':'text/html','.js':'text/javascript','.mjs':'text/javascript','.json':'application/json','.glb':'model/gltf-binary','.gltf':'model/gltf+json','.bin':'application/octet-stream','.png':'image/png','.jpg':'image/jpeg','.ogg':'audio/ogg' };
const server = createServer(async (req,res)=>{ try{ let p=decodeURIComponent(req.url.split('?')[0]); if(p==='/')p='/index.html'; const fp=join(ROOT,p); const buf=await readFile(fp); res.writeHead(200,{'Content-Type':MIME[extname(fp)]||'application/octet-stream'}); res.end(buf);}catch{res.writeHead(404);res.end('nf');} });
server.on('error', (e)=>{ console.error(`FATAL: static server on :${PORT} — ${e.code||e.message}`); process.exit(1); });
await new Promise(r=>server.listen(PORT,r));
const url = `http://localhost:${PORT}/The%20Games%20Master%20-%20Prologue.dc.html`;
const browser = await chromium.launch();
const page = await browser.newPage({ viewport:{ width:1280, height:720 } });
// the car IS the subject here — a swallowed carModel wait meant a car-less scene shot seven times
// and reported exactly like a good run.
const fails = [], errs = [];
page.on('pageerror', e=>{ errs.push(e.message); console.log('[pageerror]', e.message); });
// car sits at cx=-4.8, cz=76.5, front toward -Z (house)
const shots = [
  { name:'car-front',  pose:{ x:-4.8, y:1.5, z:69,   yaw:Math.PI,   pitch:0.03 } },
  { name:'car-side',   pose:{ x:0,    y:1.6, z:76.5, yaw:Math.PI/2, pitch:0.03 } },
  { name:'car-3q',     pose:{ x:-9,   y:1.6, z:71,   yaw:-2.42,     pitch:0.03 } },
  { name:'car-arrival',pose:{ x:-2.0, y:1.52,z:78.2, yaw:0.42,      pitch:0.05 } },
  { name:'porch-door', pose:{ x:0,    y:1.7, z:-44,  yaw:0,         pitch:0.14 } },
  { name:'flank-left', pose:{ x:2,    y:1.7, z:40,   yaw:-1.15,     pitch:0.0  } },
  { name:'flank-right',pose:{ x:-2,   y:1.7, z:20,   yaw:1.15,      pitch:0.0  } },
];
try {
  await page.goto(url,{ waitUntil:'load' });
  await page.waitForFunction(()=>window.__GMC && window.__GMC.scene, null, { timeout:30000 });
  const carLoaded = await page.waitForFunction(()=>window.__GMC && window.__GMC.carModel, null, { timeout:30000 }).then(()=>true).catch(()=>false);
  if (!carLoaded) fails.push('__GMC.carModel never loaded within 30s — every car-* frame below is of an empty drive');
  await page.waitForTimeout(2500);
  for (const s of shots) {
    await page.evaluate((pose)=>{
      const C=window.__GMC; window.__GM.goTo('walk'); C.walkEnabled=false;
      if(C._raf){ cancelAnimationFrame(C._raf); C._raf=null; }
      C.cam.position.set(pose.x,pose.y,pose.z); C.yaw=pose.yaw; C.pitch=pose.pitch;
      C.cam.rotation.set(C.pitch,C.yaw,0,'YXZ'); C.renderer.render(C.scene,C.cam);
    }, s.pose);
    await page.waitForTimeout(200);
    await page.screenshot({ path:`docs/playtest/screenshots/${s.name}.png`, timeout:60000 });
    console.log('captured', s.name);
  }
} catch (e) {
  fails.push(`run threw: ${String(e.message||e).split('\n')[0]}`);
} finally {
  await browser.close().catch(()=>{});
  await new Promise(r=>server.close(()=>r()));
}
for (const f of fails) console.log('FAIL', f);
if (errs.length) console.log('FAIL page errors:', errs.slice(0,3).join(' | '));
if (fails.length || errs.length) { console.log(`done WITH FAILURES — ${fails.length} readiness/run, ${errs.length} page error(s)`); process.exitCode = 1; }
else console.log('done');
