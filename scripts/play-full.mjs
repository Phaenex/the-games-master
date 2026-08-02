// Full opening playthrough: spawn → gate lock → full walk to mansion → look around flanks/porch →
// closed-door refusal → knockout → aftermath. Live RAF/collision movement for walk segments;
// paused poses for look-around. Dedicated input gates cover browser keyboard dispatch; this long
// sequence drives the same controller key state directly so a loaded headless browser cannot lose a
// Playwright keydown between scenes.
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';
const ROOT = process.cwd(), PORT = 3801, DIR = 'docs/playtest/screenshots';
const MIME = { '.html':'text/html','.js':'text/javascript','.json':'application/json','.glb':'model/gltf-binary','.gltf':'model/gltf+json','.bin':'application/octet-stream','.png':'image/png','.ogg':'audio/ogg' };
const server = createServer(async (req,res)=>{ try{ let p=decodeURIComponent(req.url.split('?')[0]); if(p==='/')p='/index.html'; const fp=join(ROOT,p); const buf=await readFile(fp); res.writeHead(200,{'Content-Type':MIME[extname(fp)]||'application/octet-stream'}); res.end(buf);}catch{res.writeHead(404);res.end('nf');} });
await new Promise(r=>server.listen(PORT,r));
const browser = await chromium.launch();
const page = await browser.newPage({ viewport:{ width:1280, height:720 } });
try {
const pageErrors = [];
const assetFailures = [];
page.on('pageerror', e=>{ pageErrors.push(e.message); console.log('[pageerror]', e.message); });
page.on('requestfailed', req=>{ assetFailures.push(`${req.url()} :: ${req.failure()?.errorText || 'request failed'}`); });
page.on('response', res=>{ if(res.status() >= 400) assetFailures.push(`${res.status()} ${res.url()}`); });
await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Prologue.dc.html`,{ waitUntil:'load' });
await page.waitForFunction(()=>window.__GMC && window.__GMC.scene && window.__GMC.gateLeafL, null, { timeout:120000 });
await page.waitForFunction(()=>window.__GMC && window.__GMC._ruinsBuilt, null, { timeout:60000 });
await page.waitForFunction(()=>window.__GMC && window.__GMC._ownedDeadTreesReady===true, null, { timeout:120000 });
const bootState = await page.evaluate(()=>window.__GM.getState());
if (bootState.ownedTreeCount <= 0) {
  throw new Error(`owned-tree loaders completed with zero placements; asset failures: ${assetFailures.join(' | ') || 'none reported'}`);
}
await page.waitForTimeout(2000);

const state = ()=>page.evaluate(()=>window.__GM.getState());
const z = ()=>page.evaluate(()=>+window.__GMC.cam.position.z.toFixed(1));
const look = (yaw,pitch=0)=>page.evaluate(([y,p])=>{ const C=window.__GMC; C.yaw=y; C.pitch=p; }, [yaw,pitch]);
const pose = async (x,y,zp,yaw,pitch=0)=>{ await page.evaluate(([px,py,pz,y,p])=>{ const C=window.__GMC; C.walkEnabled=false; C.cam.position.set(px,py,pz); C.yaw=y; C.pitch=p; C.cam.rotation.set(p,C.yaw,0,'YXZ'); C.renderer.render(C.scene,C.cam); }, [x,y,zp,yaw,pitch]); await page.waitForTimeout(180); };
const shot = async (n)=>{ await page.screenshot({ path:`${DIR}/${n}.png`, timeout:120000 }); console.log('shot', n, 'z=', await z(), JSON.stringify(await state())); };
const walkTo = async (targetZ, timeout=240000)=>{
  await page.evaluate(()=>{ const C=window.__GMC; C.walkEnabled=true; C.keys.w=true; });
  try {
    await page.waitForFunction((tz)=>window.__GMC.cam.position.z <= tz, targetZ, { timeout });
  } catch (err) {
    const diag = await page.evaluate(()=>{ const C=window.__GMC; return {
      z:C.cam.position.z, x:C.cam.position.x, yaw:C.yaw, mode:C.mode,
      walkEnabled:C.walkEnabled, keyW:C.keys.w, gateLockFired:C._gateLockFired
    }; });
    console.error('walkTo timeout', JSON.stringify({ targetZ, ...diag }));
    throw err;
  } finally {
    // Never leak a held movement key when a diagnostic times out.
    await page.evaluate(()=>{ if(window.__GMC) window.__GMC.keys.w=false; });
  }
};

// --- START WALK ---
await page.evaluate(()=>{ window.__GM.goTo('walk'); const C=window.__GMC; C.walkEnabled=true; C.yaw=0; C.pitch=0; });
await page.waitForTimeout(400);
await shot('full-01-spawn');

// through gate
await walkTo(62, 180000);
await shot('full-02-through-gate');

// turn + lock
await look(Math.PI, 0.02);
await page.waitForTimeout(200);
await page.evaluate(()=>{ window.__GMC.keys.w=true; });
try {
  await page.waitForFunction(()=>window.__GM.getState().gateLockFired===true, null, { timeout:90000 });
} finally {
  await page.evaluate(()=>{ if(window.__GMC) window.__GMC.keys.w=false; });
}
await page.waitForTimeout(700);
const lockedSideZ = await page.evaluate(()=>window.__GMC.cam.position.z);
if(lockedSideZ >= 65) throw new Error(`gate lock stranded player outside estate at z=${lockedSideZ}`);
await shot('full-03-gate-locked');

// face house again, resume walk
await look(0, 0);
await page.waitForTimeout(200);
await walkTo(40);
await look(-1.2, 0.04); await page.waitForTimeout(150); await shot('full-04-cemetery-pass');
await look(1.2, 0.04); await page.waitForTimeout(150); await shot('full-05-garden-pass');
await look(0, 0);

await walkTo(10);
await shot('full-06-mid-drive');

await walkTo(-20);
await look(-1.45, 0.08); await page.waitForTimeout(150); await shot('full-07-left-flank');
await look(1.45, 0.08); await page.waitForTimeout(150); await shot('full-08-right-flank');
await look(0, 0);

await walkTo(-34);
await shot('full-09-at-porch');

// look around at the porch: left cemetery over fence, right garden, up at facade, door close
await pose(-2.8, 1.7, -38, -1.35, 0.05); await shot('full-10-porch-left');
await pose(2.8, 1.7, -38, 1.35, 0.05); await shot('full-11-porch-right');
await pose(0, 1.7, -38, 0, 0.22); await shot('full-12-door-close');
await pose(0, 1.7, -38, 0, 0.42); await shot('full-13-facade-up');

// behind-mansion sightline from far approach (player can't walk behind it)
await pose(0, 1.7, 20, Math.PI, 0.02); await shot('full-14-look-back-drive');

// trigger arrival by walking to threshold (must face the house — look-back pose leaves yaw=PI)
await look(0, 0.08);
await page.waitForTimeout(200);
await page.evaluate(()=>{ const C=window.__GMC; C.cam.position.set(0,1.7,-38); C.walkEnabled=true; });
await page.evaluate(()=>{ window.__GMC.keys.w=true; });
try {
  await page.waitForFunction(()=>window.__GM.getState().mode==='arrival' || window.__GMC.mode==='arrival', null, { timeout:180000 });
} finally {
  await page.evaluate(()=>{ if(window.__GMC) window.__GMC.keys.w=false; });
}
await shot('full-15-arrival-start');

// closed-door trap hold → knockout (no door-open fashion show)
// Arrival begins at z=-36, then the live controller eases the player up the porch to z=-49.6.
// Software rendering can advance that easing at only the clamped 0.05s/frame rate under a loaded
// headless run, so do not confuse "arrival mode entered" with "the closed-door hold has started."
await page.waitForFunction(()=>window.__GMC.cam.position.z < -48.8, null, { timeout:180000 });
await page.waitForFunction(()=>window.__GMC._glimpseSaid===true, null, { timeout:60000 });
await page.waitForTimeout(500);
await shot('full-16-closed-hold');

// Headless software rendering accrues arrHold at a small fraction of wall-clock (dt clamp 0.05 ×
// low fps), so waiting out the full 6.4s hold in real time is unbounded on a loaded machine.
// Jump the timer to just under the knock threshold (same technique verify-polish uses on the owl
// timer) — the code path still has to fire the knock itself, and the real-time pacing is already
// exercised by play-door.
await page.evaluate(()=>{ const C=window.__GMC; C.arrHold = Math.max(C.arrHold||0, 6.3); });
await page.waitForFunction(()=>window.__GMC.knockStarted===true, null, { timeout:120000 });
await page.waitForTimeout(350);
await shot('full-17-knockout');

// wait for aftermath
await page.waitForFunction(()=>window.__GM.getState().phase==='aftermath', null, { timeout:90000 });
await page.waitForTimeout(1200);
await shot('full-18-aftermath');

const fin = await state();
const doorState = await page.evaluate(()=>({ left:window.__GMC.doorL?.rotation.y || 0, right:window.__GMC.doorR?.rotation.y || 0 }));
console.log('FINAL', JSON.stringify(fin), 'DOORS', JSON.stringify(doorState));
if (pageErrors.length) throw new Error(`page errors: ${pageErrors.join(' | ')}`);
if (fin.phase !== 'aftermath' || fin.errors !== 0) throw new Error(`unexpected final state: ${JSON.stringify(fin)}`);
if (Math.abs(doorState.left) > 0.001 || Math.abs(doorState.right) > 0.001) throw new Error(`Threshold Refusal doors moved: ${JSON.stringify(doorState)}`);
console.log('done');
} finally {
  await browser.close();
  server.close();
}
