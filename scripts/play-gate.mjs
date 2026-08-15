// Actually PLAYS the opening: enters walk mode with movement live (RAF running, real clamp + beat
// logic), holds forward to walk down the drive through the gate, turns around, walks back to trigger
// the lock naturally, and captures live frames + the game's own devSnapshot at each step.
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';
const ROOT = process.cwd(), PORT = 3797;
const MIME = { '.html':'text/html','.js':'text/javascript','.json':'application/json','.glb':'model/gltf-binary','.gltf':'model/gltf+json','.bin':'application/octet-stream','.png':'image/png','.ogg':'audio/ogg' };
const server = createServer(async (req,res)=>{ try{ let p=decodeURIComponent(req.url.split('?')[0]); if(p==='/')p='/index.html'; const fp=join(ROOT,p); const buf=await readFile(fp); res.writeHead(200,{'Content-Type':MIME[extname(fp)]||'application/octet-stream'}); res.end(buf);}catch{res.writeHead(404);res.end('nf');} });
await new Promise(r=>server.listen(PORT,r));
const browser = await chromium.launch();
const page = await browser.newPage({ viewport:{ width:1280, height:720 } });
const pageErrors = [];
page.on('pageerror', e=>{ pageErrors.push(e.message); console.log('[pageerror]', e.message); });
await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Prologue.dc.html`,{ waitUntil:'load' });
await page.waitForFunction(()=>window.__GMC && window.__GMC.scene && window.__GMC.gateLeafL, null, { timeout:120000 });
await page.waitForFunction(()=>window.__GMC && window.__GMC._ruinsBuilt===true, null, { timeout:60000 });
await page.waitForTimeout(1500);

const dir = 'docs/playtest/screenshots';
const state = ()=>page.evaluate(()=>window.__GM.getState());
const shot = async (n)=>{ await page.screenshot({ path:`${dir}/${n}.png`, timeout:60000 }); console.log('shot', n, JSON.stringify(await state())); };

// enter walk with movement LIVE (do not freeze RAF)
await page.evaluate(()=>{ window.__GM.goTo('walk'); const C=window.__GMC; C.walkEnabled=true; C.yaw=0; C.pitch=0.0; });
await page.waitForTimeout(400);
await shot('play-01-spawn');

// walk forward (toward the house, -z) through the gate
await page.keyboard.down('w');
await page.waitForFunction(()=>window.__GMC.cam.position.z < 66, null, { timeout:8000 });
await shot('play-02-at-gate');
await page.waitForFunction(()=>window.__GMC.cam.position.z < 58, null, { timeout:8000 });
await page.keyboard.up('w');
await shot('play-03-through-gate');

// turn around (mouse-look equivalent) to face back at the gate, then walk back toward it
await page.evaluate(()=>{ window.__GMC.yaw = Math.PI; });
await page.waitForTimeout(250);
await shot('play-04-turned');

await page.keyboard.down('w'); // yaw=PI => forward is +z, back toward the gate
// catch the slam mid-swing
await page.waitForFunction(()=>window.__GM.getState().gateLockFired===true, null, { timeout:8000 });
await page.waitForTimeout(160);
await shot('play-05-slam');
await page.waitForTimeout(600);
await shot('play-06-locked');

// keep pushing back — confirm the shut gate actually blocks (z clamps, can't leave)
await page.waitForTimeout(1500);
await page.keyboard.up('w');
const fin = await state();
const blockedZ = await page.evaluate(()=>+window.__GMC.cam.position.z.toFixed(2));
const retreatCap = await page.evaluate(()=>window.__GMC.gateZ+2);
console.log('FINAL', JSON.stringify(fin));
console.log('blocked at z =', blockedZ, '(retreat cap = gateZ+2 =', retreatCap, ')');
await shot('play-07-blocked');

await browser.close(); server.close();
if (pageErrors.length) throw new Error(`page errors: ${pageErrors.join(' | ')}`);
if (!fin.gateLockFired || fin.errors !== 0) throw new Error(`unexpected final state: ${JSON.stringify(fin)}`);
if (blockedZ > retreatCap + 0.1) throw new Error(`gate failed to block: z=${blockedZ}, cap=${retreatCap}`);
console.log('done');
