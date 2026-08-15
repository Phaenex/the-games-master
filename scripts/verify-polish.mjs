// Verifies the "remaining exterior polish" for Phase 0 actually WORKS at runtime, not just in source:
//  1. Falling leaves genuinely drift (sample y over time -> must change), and each leaf respawns.
//  2. Reduce-motion freezes the leaves (same sample -> must NOT change).
//  3. Crickets bed is armed on startAmbience (ambOn true, crickets track present & not paused-forever).
//  4. Owl one-shot fires on its timer (spy playSfx; force the timer to elapse and confirm the call).
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';
const ROOT = process.cwd(), PORT = 3799;
const MIME = { '.html':'text/html','.js':'text/javascript','.json':'application/json','.glb':'model/gltf-binary','.gltf':'model/gltf+json','.bin':'application/octet-stream','.png':'image/png','.ogg':'audio/ogg' };
const server = createServer(async (req,res)=>{ try{ let p=decodeURIComponent(req.url.split('?')[0]); if(p==='/')p='/index.html'; const fp=join(ROOT,p); const buf=await readFile(fp); res.writeHead(200,{'Content-Type':MIME[extname(fp)]||'application/octet-stream'}); res.end(buf);}catch{res.writeHead(404);res.end('nf');} });
await new Promise(r=>server.listen(PORT,r));
const browser = await chromium.launch();
const page = await browser.newPage({ viewport:{ width:1280, height:720 } });
let pass = false;
try {
const errs=[]; page.on('pageerror', e=>{ errs.push(e.message); console.log('[pageerror]', e.message); });
await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Prologue.dc.html`,{ waitUntil:'load' });
await page.waitForFunction(()=>window.__GMC && window.__GMC.scene && window.__GMC.leaves, null, { timeout:120000 });
await page.waitForTimeout(1200);

const results = [];
const check = (name, ok, detail)=>{ results.push({name, ok, detail}); console.log((ok?'PASS':'FAIL'), name, '-', detail); };

// enter walk with RAF live
await page.evaluate(()=>{ window.__GM.goTo('walk'); const C=window.__GMC; C.walkEnabled=true; C.yaw=0; C.pitch=0; if(window.GMSettings) GMSettings.set({reduceMotion:false}); });
await page.waitForTimeout(300);

// --- 1. leaves drift ---
const sample = ()=>page.evaluate(()=>window.__GMC.leaves.slice(0,12).map(l=>+l.position.y.toFixed(3)));
const a = await sample();
await page.waitForTimeout(900);
const b = await sample();
const moved = a.filter((y,i)=>Math.abs(y-b[i])>0.02).length;
check('leaves-drift', moved >= 8, `${moved}/12 leaves changed y over ~0.9s`);

// respawn: any leaf that fell below floor should have jumped back up (no leaf stuck < 0.12)
const belowFloor = await page.evaluate(()=>window.__GMC.leaves.filter(l=>l.position.y<0.12).length);
check('leaves-respawn', belowFloor === 0, `${belowFloor} leaves stuck below floor`);

// --- 2. reduce motion freezes leaves ---
await page.evaluate(()=>{ if(window.GMSettings) GMSettings.set({reduceMotion:true}); });
await page.waitForTimeout(150);
const c = await sample();
await page.waitForTimeout(900);
const d = await sample();
const stillMoved = c.filter((y,i)=>Math.abs(y-d[i])>0.02).length;
check('leaves-reduce-motion', stillMoved === 0, `${stillMoved}/12 leaves moved with reduceMotion ON (want 0)`);
await page.evaluate(()=>{ if(window.GMSettings) GMSettings.set({reduceMotion:false}); });

// --- 3. crickets bed armed ---
const amb = await page.evaluate(()=>{ const C=window.__GMC; return { ambOn:!!C._ambOn, hasCrickets:!!(C._audio&&C._audio.crickets), paused: C._audio&&C._audio.crickets? C._audio.crickets.paused : null, baseVol: C._audio&&C._audio.crickets? C._audio.crickets._baseVol : null }; });
// paused must be asserted, not just printed — a bed that never actually started is silent audio
check('crickets-armed', amb.ambOn && amb.hasCrickets && amb.paused === false, `ambOn=${amb.ambOn} crickets=${amb.hasCrickets} paused=${amb.paused} baseVol=${amb.baseVol}`);

// --- 4. owl one-shot fires ---
// spy playSfx, force the owl timer to elapse, tick once
const owl = await page.evaluate(async ()=>{
  const C=window.__GMC; const calls=[]; const orig=C.playSfx.bind(C);
  C.playSfx=(url,vol)=>{ calls.push(url); return orig(url,vol); };
  C._owlTimer = 0.0001; C._ambOn=true; C.mode='walk';
  await new Promise(r=>setTimeout(r,400));
  C.playSfx=orig;
  return { calls, firedOwl: calls.some(u=>/amb_owl/.test(u)) };
});
check('owl-oneshot', owl.firedOwl, `sfx calls during tick: ${JSON.stringify(owl.calls)}`);

pass = results.every(r=>r.ok) && errs.length===0;
console.log('\n=== POLISH VERIFY:', pass?'ALL PASS':'FAILURES PRESENT', '=== pageerrors:', errs.length);
} finally {
  await browser.close();
  server.close();
}
process.exit(pass?0:1);
