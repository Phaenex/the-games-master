import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';
const ROOT = process.cwd(), PORT = 3793;
const MIME = { '.html':'text/html','.js':'text/javascript','.mjs':'text/javascript','.json':'application/json','.glb':'model/gltf-binary','.gltf':'model/gltf+json','.bin':'application/octet-stream','.png':'image/png','.jpg':'image/jpeg','.ogg':'audio/ogg' };
const server = createServer(async (req,res)=>{ try{ let p=decodeURIComponent(req.url.split('?')[0]); if(p==='/')p='/index.html'; const fp=join(ROOT,p); const buf=await readFile(fp); res.writeHead(200,{'Content-Type':MIME[extname(fp)]||'application/octet-stream'}); res.end(buf);}catch{res.writeHead(404);res.end('nf');} });
await new Promise(r=>server.listen(PORT,r));
const url = `http://localhost:${PORT}/The%20Games%20Master%20-%20Prologue.dc.html`;
let browser = null, pass = false;
try {
browser = await chromium.launch();
const page = await browser.newPage({ viewport:{ width:1280, height:720 } });
const errs = [];
page.on('console', m=>console.log('[page]', m.type(), m.text()));
page.on('pageerror', e=>{ errs.push(e.message); console.log('[pageerror]', e.message); });
await page.goto(url,{ waitUntil:'load' });
await page.waitForFunction(()=>window.__GMC && window.__GMC.scene, null, { timeout:120000 });
// No gate leaves, no gate shots worth keeping — and --lock calls triggerGateLock() on them.
const gateOK = await page.waitForFunction(()=>window.__GMC && window.__GMC.gateLeafL, null, { timeout:60000 }).then(()=>true).catch(()=>false);
console.log(`gate leaves built: ${gateOK ? 'PASS' : 'FAIL'}`);
await page.waitForTimeout(2500);

const lock = process.argv.includes('--lock');
if (lock && gateOK) { await page.evaluate(()=>{ window.__GMC._passedGate = true; window.__GMC._gateLockFired = true; window.__GMC.triggerGateLock(); }); await page.waitForTimeout(900); }
const tag = lock ? 'locked' : 'open';

const shots = gateOK ? [
  { name:`gate-approach-${tag}`, pose:{ x:0,  y:1.7, z:72, yaw:0,       pitch:0.02 } },
  { name:`gate-turned-${tag}`,   pose:{ x:0,  y:1.7, z:60, yaw:Math.PI, pitch:0.02 } },
  { name:`gate-edge-${tag}`,     pose:{ x:2.8,y:1.7, z:70, yaw:-0.25,   pitch:0.0 } },
] : [];
// With no gate in the scene these frames would be junk filed under the baseline names — and a
// --lock run that never locked would file them as 'locked'. Skip them; the run fails either way.
if (!gateOK) console.log('skipping gate shots — no gate leaves to capture');
for (const s of shots) {
  await page.evaluate((pose)=>{
    const C=window.__GMC; window.__GM.goTo('walk'); C.walkEnabled=false;
    if(C._raf){ cancelAnimationFrame(C._raf); C._raf=null; }
    C.cam.position.set(pose.x,pose.y,pose.z); C.yaw=pose.yaw; C.pitch=pose.pitch;
    C.cam.rotation.set(C.pitch,C.yaw,0,'YXZ'); C.renderer.render(C.scene,C.cam);
  }, s.pose);
  await page.waitForTimeout(150);
  await page.screenshot({ path:`docs/playtest/screenshots/${s.name}.png`, timeout:60000 });
  console.log('captured', s.name);
}
console.log('page errors:', errs.length, errs.slice(0,3));
pass = errs.length === 0 && gateOK;
console.log(pass ? 'GATE CAPTURE PASS' : 'GATE CAPTURE FAIL');
} catch (e) {
  console.log('GATE CAPTURE FAIL — threw:', (e && e.message) || e);
} finally {
  if (browser) await browser.close().catch(()=>{});
  server.close();
}
process.exit(pass ? 0 : 1);
