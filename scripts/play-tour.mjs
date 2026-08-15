// Live full walk of the opening: spawn -> car look -> down the drive to the door, with side-looks at
// the cemetery and garden. Movement is real (RAF live, real clamp/beats). Captures labeled frames and
// logs the game's devSnapshot so we can eyeball every stretch of the approach for issues.
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';
const ROOT = process.cwd(), PORT = 3798;
const MIME = { '.html':'text/html','.js':'text/javascript','.json':'application/json','.glb':'model/gltf-binary','.gltf':'model/gltf+json','.bin':'application/octet-stream','.png':'image/png','.ogg':'audio/ogg' };
const server = createServer(async (req,res)=>{ try{ let p=decodeURIComponent(req.url.split('?')[0]); if(p==='/')p='/index.html'; const fp=join(ROOT,p); const buf=await readFile(fp); res.writeHead(200,{'Content-Type':MIME[extname(fp)]||'application/octet-stream'}); res.end(buf);}catch{res.writeHead(404);res.end('nf');} });
await new Promise(r=>server.listen(PORT,r));
const browser = await chromium.launch();
const page = await browser.newPage({ viewport:{ width:1280, height:720 } });
try {
const pageErrors = [];
page.on('pageerror', e=>{ pageErrors.push(e.message); console.log('[pageerror]', e.message); });
await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Prologue.dc.html`,{ waitUntil:'load' });
await page.waitForFunction(()=>window.__GMC && window.__GMC.scene && window.__GMC.gateLeafL, null, { timeout:120000 });
await page.waitForFunction(()=>window.__GMC && window.__GMC._ruinsBuilt===true, null, { timeout:60000 });
await page.waitForTimeout(1500);

const dir = 'docs/playtest/screenshots';
const z = ()=>page.evaluate(()=>+window.__GMC.cam.position.z.toFixed(1));
const look = (yaw,pitch=0.0)=>page.evaluate(([y,p])=>{ window.__GMC.yaw=y; window.__GMC.pitch=p; }, [yaw,pitch]);
const shot = async (n)=>{ await page.screenshot({ path:`${dir}/${n}.png`, timeout:60000 }); console.log('shot', n, 'z=', await z()); };
// A leg that never reaches its target is the finding, not the weather: shooting the next frame
// anyway labels a mid-drive frame 'front-door'. The 12s bound this used to carry was wall-clock,
// and movement is dt-scaled — headless software rendering accrues that at a fraction of wall-clock
// (docs/TESTING.md rule 3) — so the bound matches play-full's proven 240s and the timeout is fatal.
const walkTo = async (targetZ, timeout=240000)=>{
  await page.keyboard.down('w');
  try {
    await page.waitForFunction((tz)=>window.__GMC.cam.position.z <= tz, targetZ, { timeout });
  } catch (err) {
    const diag = await page.evaluate(()=>{ const C=window.__GMC; return {
      z:C.cam.position.z, x:C.cam.position.x, yaw:C.yaw, mode:C.mode,
      walkEnabled:C.walkEnabled, keyW:!!C.keys.w, gateLockFired:C._gateLockFired
    }; });
    console.error('walkTo timeout', JSON.stringify({ targetZ, ...diag }));
    throw err;
  } finally {
    await page.keyboard.up('w');
  }
};

await page.evaluate(()=>{ window.__GM.goTo('walk'); const C=window.__GMC; C.walkEnabled=true; C.yaw=0; C.pitch=0.0; });
await page.waitForTimeout(400);

await shot('tour-01-spawn-forward');
await look(Math.PI, 0.02); await page.waitForTimeout(150); await shot('tour-02-spawn-lookback-car');
await look(0, 0.0);

await walkTo(58); await shot('tour-03-just-inside-gate');
await walkTo(40);
await look(-1.25, 0.0); await page.waitForTimeout(150); await shot('tour-04-cemetery-left');
await look(1.25, 0.0); await page.waitForTimeout(150); await shot('tour-05-garden-right');
await look(0, 0.0);

await walkTo(20); await shot('tour-06-midway');
await walkTo(-2); await shot('tour-07-past-fountain');
await walkTo(-26); await shot('tour-08-approach-porch');
// End of the live walk. The drive rect stops at z=-36.5 and arrival fires at z<=-36, so anything
// past here is the arrival ease, not walking — the trap beat after it is play-door's gate.
await walkTo(-34);
await look(0, 0.12); await page.waitForTimeout(150); await shot('tour-09-front-door');
await look(0, 0.5); await page.waitForTimeout(150); await shot('tour-10-facade-up');

// overview from above to judge overall layout/emptiness
await page.evaluate(()=>{ const C=window.__GMC; C.walkEnabled=false; C.cam.position.set(0,17,40); C.yaw=0; C.pitch=0.5; C.cam.rotation.set(C.pitch,C.yaw,0,'YXZ'); C.renderer.render(C.scene,C.cam); });
await page.waitForTimeout(200); await shot('tour-11-overview');

const fin = await page.evaluate(()=>window.__GM.getState());
console.log('FINAL', JSON.stringify(fin));
if (pageErrors.length) throw new Error(`page errors: ${pageErrors.join(' | ')}`);
if (fin.errors !== 0) throw new Error(`unexpected final state: ${JSON.stringify(fin)}`);
console.log('done');
} finally {
  await browser.close();
  server.close();
}
