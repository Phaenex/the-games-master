// Threshold presence layer: prove the porch_breath one-shot actually fires on the
// "Something moved in the porch dark" beat (and the latch tick still fires with it).
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';
const ROOT = process.cwd(), PORT = 3814;
const MIME = { '.html':'text/html','.js':'text/javascript','.json':'application/json','.glb':'model/gltf-binary','.gltf':'model/gltf+json','.bin':'application/octet-stream','.png':'image/png','.ogg':'audio/ogg' };
const server = createServer(async (req,res)=>{ try{ let p=decodeURIComponent(req.url.split('?')[0]); if(p==='/')p='/index.html'; const fp=join(ROOT,p); const buf=await readFile(fp); res.writeHead(200,{'Content-Type':MIME[extname(fp)]||'application/octet-stream'}); res.end(buf);}catch{res.writeHead(404);res.end('nf');} });
await new Promise(r=>server.listen(PORT,r));
const browser = await chromium.launch();
const page = await browser.newPage({ viewport:{ width:1280, height:720 } });
let pass = false;
try {
const pageErrors = [];
const failed = [];
page.on('pageerror', e=>{ pageErrors.push(e.message); console.log('[pageerror]', e.message); });
page.on('requestfailed', r=>failed.push(r.url()));
await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Prologue.dc.html`,{ waitUntil:'load' });
await page.waitForFunction(()=>window.__GMC && window.__GMC.scene && window.__GMC._ruinsBuilt, null, { timeout:120000 });
await page.waitForTimeout(1500);

await page.evaluate(()=>{
  const C = window.__GMC;
  C._sfxSpy = [];
  const orig = C.playSfx.bind(C);
  C.playSfx = (url, vol)=>{ C._sfxSpy.push(url); return orig(url, vol); };
  window.__GM.goTo('walk');
  C.walkEnabled=true; C.yaw=0; C.pitch=0.1;
  C.cam.position.set(0,1.7,-34.5);
  C._passedGate=true; C._gateLockFired=true;
});
await page.keyboard.down('w');
await page.waitForFunction(()=>window.__GMC.mode==='arrival', null, { timeout:60000 });
await page.keyboard.up('w');
// Arrival mode starts before the slow headless glide has actually settled at the porch. Waiting on
// arrHold here used to time out because that timer correctly remains stopped during the glide. The
// exhaustive agent walk uses the same settle condition; this gate tests the breath/latch beat, not
// software-renderer pacing.
await page.waitForFunction(()=>window.__GMC.cam.position.z < -48.8, null, { timeout:90000 });
await page.evaluate(()=>{ const C=window.__GMC; C.arrHold = Math.max(C.arrHold||0, 3.1); });
await page.waitForFunction(()=>window.__GMC._thresholdSaid===true, null, { timeout:60000 });
await page.waitForTimeout(600);

const spy = await page.evaluate(()=>window.__GMC._sfxSpy);
const breath = spy.some(u=>/porch_breath\.ogg/.test(u));
const latch = spy.some(u=>/gate_lock\.ogg/.test(u));
const breath404 = failed.some(u=>/porch_breath/.test(u));
console.log('sfx spy:', JSON.stringify(spy));
console.log(`breath fired: ${breath} · latch fired: ${latch} · breath request failed: ${breath404} · page errors: ${pageErrors.length}`);
pass = breath && latch && !breath404 && pageErrors.length === 0;
console.log(pass ? 'VERIFY BREATH PASS' : 'VERIFY BREATH FAIL');
} finally {
  await browser.close();
  server.close();
}
process.exit(pass ? 0 : 1);
