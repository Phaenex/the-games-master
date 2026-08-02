// Door / trap beat: porch threshold → closed-door hold → knockout (doors stay shut) → aftermath.
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';
const ROOT = process.cwd(), PORT = 3802, DIR = 'docs/playtest/screenshots';
const MIME = { '.html':'text/html','.js':'text/javascript','.json':'application/json','.glb':'model/gltf-binary','.gltf':'model/gltf+json','.bin':'application/octet-stream','.png':'image/png','.ogg':'audio/ogg' };
const server = createServer(async (req,res)=>{ try{ let p=decodeURIComponent(req.url.split('?')[0]); if(p==='/')p='/index.html'; const fp=join(ROOT,p); const buf=await readFile(fp); res.writeHead(200,{'Content-Type':MIME[extname(fp)]||'application/octet-stream'}); res.end(buf);}catch{res.writeHead(404);res.end('nf');} });
await new Promise(r=>server.listen(PORT,r));
const browser = await chromium.launch();
const page = await browser.newPage({ viewport:{ width:1280, height:720 } });
const pageErrors = [];
page.on('pageerror', e=>{ pageErrors.push(e.message); console.log('[pageerror]', e.message); });
await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Prologue.dc.html`,{ waitUntil:'load' });
await page.waitForFunction(()=>window.__GMC && window.__GMC.scene && window.__GMC._ruinsBuilt, null, { timeout:120000 });
await page.waitForTimeout(2000);

const state = ()=>page.evaluate(()=>window.__GM.getState());
const shot = async (n)=>{ await page.screenshot({ path:`${DIR}/${n}.png`, timeout:60000 }); console.log('shot', n, JSON.stringify(await state())); };

// start at porch threshold facing the door, movement live
await page.evaluate(()=>{
  window.__GM.goTo('walk');
  const C=window.__GMC;
  C.walkEnabled=true; C.yaw=0; C.pitch=0.1;
  C.cam.position.set(0,1.7,-34.5);
  C._passedGate=true; C._gateLockFired=true;
});
await page.waitForTimeout(400);
await shot('door-01-threshold');

// one step forward triggers arrival (z <= arrivalZ = -36)
await page.keyboard.down('w');
await page.waitForFunction(()=>window.__GMC.mode==='arrival', null, { timeout:60000 });
await page.keyboard.up('w');
await shot('door-02-arrival-start');
// the settle glide is dt-scaled camera animation — jump it near the settle plane instead of
// waiting wall-clock for fps-starved lerps (the lerp continues from here; 'settled' < -48.8)
await page.evaluate(()=>{ const C=window.__GMC; if (C.cam.position.z > -48.5) C.cam.position.z = -48.5; });

await page.waitForFunction(()=>window.__GMC._glimpseSaid===true, null, { timeout:60000 });
await page.waitForTimeout(400);
await shot('door-03-closed-hold');

// Headless hold-time accrues at a fraction of wall-clock (dt clamp × software fps) — advance the
// timer to just under each beat threshold; the beat code still has to fire. Real-time pacing is
// the human gate's territory (Nick's walk), not a flaky headless wait.
await page.evaluate(()=>{ const C=window.__GMC; C.arrHold = Math.max(C.arrHold||0, 3.1); });
await page.waitForFunction(()=>window.__GMC._thresholdSaid===true, null, { timeout:30000 });
await page.waitForTimeout(350);
await shot('door-03b-porch-dark');

await page.evaluate(()=>{ const C=window.__GMC; C.arrHold = Math.max(C.arrHold||0, 6.3); });
await page.waitForFunction(()=>window.__GMC.knockStarted===true, null, { timeout:30000 });
await page.waitForTimeout(200);
await shot('door-04-knock-start');

await page.waitForTimeout(400);
await shot('door-05-knockout');

await page.waitForFunction(()=>window.__GM.getState().phase==='aftermath', null, { timeout:30000 });
await page.waitForTimeout(1500);
await shot('door-06-aftermath');

const fin = await state();
const doorState = await page.evaluate(()=>({ left:window.__GMC.doorL?.rotation.y || 0, right:window.__GMC.doorR?.rotation.y || 0 }));
console.log('FINAL', JSON.stringify(fin), 'DOORS', JSON.stringify(doorState));
await browser.close(); server.close();
if (pageErrors.length) throw new Error(`page errors: ${pageErrors.join(' | ')}`);
if (fin.phase !== 'aftermath' || fin.errors !== 0) throw new Error(`unexpected final state: ${JSON.stringify(fin)}`);
if (Math.abs(doorState.left) > 0.001 || Math.abs(doorState.right) > 0.001) throw new Error(`Threshold Refusal doors moved: ${JSON.stringify(doorState)}`);
console.log('done');
