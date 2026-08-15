// Pose the camera along the whole approach (walking is too slow in headless GL) to check the fence,
// mist/fog, and the mansion facade lighting after the tour-found fixes. Static poses = fine for judging
// geometry + lighting; movement itself is verified separately by play-gate.mjs.
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';
const ROOT = process.cwd(), PORT = 3799;
const MIME = { '.html':'text/html','.js':'text/javascript','.json':'application/json','.glb':'model/gltf-binary','.gltf':'model/gltf+json','.bin':'application/octet-stream','.png':'image/png','.ogg':'audio/ogg' };
const server = createServer(async (req,res)=>{ try{ let p=decodeURIComponent(req.url.split('?')[0]); if(p==='/')p='/index.html'; const fp=join(ROOT,p); const buf=await readFile(fp); res.writeHead(200,{'Content-Type':MIME[extname(fp)]||'application/octet-stream'}); res.end(buf);}catch{res.writeHead(404);res.end('nf');} });
server.on('error', (e)=>{ console.error(`FATAL: static server on :${PORT} — ${e.code||e.message}`); process.exit(1); });
await new Promise(r=>server.listen(PORT,r));
const browser = await chromium.launch();
const page = await browser.newPage({ viewport:{ width:1280, height:720 } });
// fails[] is what makes this script able to say no: an unready gate/mansion or a thrown page
// error used to produce the same six "shot app-*" lines as a healthy run.
const fails = [], errs = [];
page.on('pageerror', e=>{ errs.push(e.message); console.log('[pageerror]', e.message); });

const shots = [
  { name:'app-gate-inside',  pose:{ x:0, y:1.7, z:60,  yaw:Math.PI, pitch:0.02 } },
  { name:'app-drive-mid',    pose:{ x:0, y:1.7, z:40,  yaw:0,       pitch:0.05 } },
  { name:'app-mansion-far',  pose:{ x:0, y:1.7, z:5,   yaw:0,       pitch:0.06 } },
  { name:'app-mansion-mid',  pose:{ x:0, y:1.7, z:-22, yaw:0,       pitch:0.09 } },
  { name:'app-porch',        pose:{ x:0, y:1.7, z:-42, yaw:0,       pitch:0.13 } },
  { name:'app-overview',     pose:{ x:0, y:20,  z:24,  yaw:0,       pitch:-0.62 } },
];
try {
  await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Prologue.dc.html`,{ waitUntil:'load' });
  const ready = await page.waitForFunction(()=>window.__GMC && window.__GMC.scene && window.__GMC.gateLeafL && window.__GMC.mansionModel, null, { timeout:120000 }).then(()=>true).catch(()=>false);
  if (!ready) fails.push('scene/gateLeafL/mansionModel not on __GMC within 120s — the fence + facade shots are of nothing');
  const ruins = await page.waitForFunction(()=>window.__GMC && window.__GMC._ruinsBuilt!==undefined, null, { timeout:60000 }).then(()=>true).catch(()=>false);
  if (!ruins) fails.push('__GMC._ruinsBuilt never resolved within 60s');
  await page.waitForTimeout(2000);
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
