import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';
const ROOT = process.cwd(), PORT = 3792;
const MIME = { '.html':'text/html','.js':'text/javascript','.mjs':'text/javascript','.json':'application/json','.glb':'model/gltf-binary','.gltf':'model/gltf+json','.bin':'application/octet-stream','.png':'image/png','.jpg':'image/jpeg','.ogg':'audio/ogg' };
const server = createServer(async (req,res)=>{ try{ let p=decodeURIComponent(req.url.split('?')[0]); if(p==='/')p='/index.html'; const fp=join(ROOT,p); const buf=await readFile(fp); res.writeHead(200,{'Content-Type':MIME[extname(fp)]||'application/octet-stream'}); res.end(buf);}catch{res.writeHead(404);res.end('nf');} });
await new Promise(r=>server.listen(PORT,r));
const url = `http://localhost:${PORT}/The%20Games%20Master%20-%20Prologue.dc.html`;
let browser = null, pass = false;
try {
browser = await chromium.launch();
const page = await browser.newPage({ viewport:{ width:1280, height:720 } });
const errs = [];
page.on('pageerror', e=>{ errs.push(e.message); console.log('[pageerror]', e.message); });
await page.goto(url,{ waitUntil:'load' });
await page.waitForFunction(()=>window.__GMC && window.__GMC.scene, null, { timeout:30000 });
await page.waitForTimeout(2500);
// Enter walk, freeze player movement but DO NOT cancel the RAF, so mist + leaves keep animating.
await page.evaluate(()=>{ const C=window.__GMC; window.__GM.goTo('walk'); C.walkEnabled=false; C.cam.position.set(0,1.7,52); C.yaw=0; C.pitch=0.06; C.cam.rotation.set(C.pitch,C.yaw,0,'YXZ'); });
await page.waitForTimeout(400);
// sample leaf y-positions to prove motion between two frames
const y1 = await page.evaluate(()=>window.__GMC.leaves.slice(0,6).map(l=>+l.position.y.toFixed(3)));
await page.screenshot({ path:'docs/playtest/screenshots/leaves-t0.png' });
await page.waitForTimeout(1100);
const y2 = await page.evaluate(()=>window.__GMC.leaves.slice(0,6).map(l=>+l.position.y.toFixed(3)));
await page.screenshot({ path:'docs/playtest/screenshots/leaves-t1.png' });
const count = await page.evaluate(()=>window.__GMC.leaves.length);
// Reduce Motion parks the leaf update on purpose, so report it — a still frame under that
// setting is the game behaving, not the loop dying.
const reduceMotion = await page.evaluate(()=>!!(window.GMSettings && GMSettings.get().reduceMotion));
console.log('leaf count:', count);
console.log('y @ t0:', JSON.stringify(y1));
console.log('y @ t1:', JSON.stringify(y2));
const moved = y1.some((v,i)=>Math.abs(v-y2[i])>0.05);
console.log('moved:', moved, '| reduceMotion:', reduceMotion);
// This capture exists to prove the RAF-driven leaf update still runs, so a still frame is the
// failure it is looking for — assert it instead of logging it.
console.log(`leaf motion: ${moved && count > 0 ? 'PASS' : 'FAIL'} (${count} leaves)`);
pass = errs.length === 0 && count > 0 && moved;
console.log(pass ? 'LEAVES CAPTURE PASS' : 'LEAVES CAPTURE FAIL');
} catch (e) {
  console.log('LEAVES CAPTURE FAIL — threw:', (e && e.message) || e);
} finally {
  if (browser) await browser.close().catch(()=>{});
  server.close();
}
process.exit(pass ? 0 : 1);
