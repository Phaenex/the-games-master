// Every-screen/every-state capture for the Prologue: menu, options (+text sizes), all four
// cold-open cards, letter front/back, tutorial steps, all five drive beats in place, gate lock,
// secret ending, plus a small-viewport sanity pair. Complements play-door/play-full (porch/KO/
// aftermath) — together the two cover every reachable state in the opening.
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';
const ROOT = process.cwd(), PORT = 3815, DIR = 'docs/playtest/screenshots';
const MIME = { '.html':'text/html','.js':'text/javascript','.json':'application/json','.glb':'model/gltf-binary','.gltf':'model/gltf+json','.bin':'application/octet-stream','.png':'image/png','.ogg':'audio/ogg' };
const server = createServer(async (req,res)=>{ try{ let p=decodeURIComponent(req.url.split('?')[0]); if(p==='/')p='/index.html'; const fp=join(ROOT,p); const buf=await readFile(fp); res.writeHead(200,{'Content-Type':MIME[extname(fp)]||'application/octet-stream'}); res.end(buf);}catch{res.writeHead(404);res.end('nf');} });
await new Promise(r=>server.listen(PORT,r));
const browser = await chromium.launch();
const page = await browser.newPage({ viewport:{ width:1280, height:720 } });
let failed = false;
try {
const pageErrors = [];
page.on('pageerror', e=>{ pageErrors.push(e.message); console.log('[pageerror]', e.message); });
// A state that never arrived still gets screenshotted under its label, and none of that increments
// pageErrors — so a swallowed wait is recorded here and folded into the verdict at the bottom.
const missed = [];
const ready = async (label, fn, timeout=120000)=>{ await page.waitForFunction(fn, null, { timeout }).catch(()=>{ missed.push(`${label} (waited ${timeout}ms)`); console.log('[not-ready]', label); }); };
await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Prologue.dc.html`,{ waitUntil:'load' });
await page.waitForFunction(()=>window.__GMC && window.__GMC.scene && window.__GMC._ruinsBuilt, null, { timeout:120000 });
await ready('leartes trees + grounds', ()=>window.__GMC._leartesTreesReady && window.__GMC._leartesGroundsReady);
await page.waitForTimeout(2000);

const shot = async (n)=>{ await page.waitForTimeout(260); await page.screenshot({ path:`${DIR}/${n}.png`, timeout:60000 }); console.log('shot', n); };
const go = (name)=>page.evaluate((n)=>window.__GM.goTo(n), name);

// 1. menu
await go('menu'); await shot('state-01-menu');

// 2. options + text size cycle (capture default and largest)
await go('options'); await shot('state-02-options');
await page.evaluate(()=>{ window.GMSettings.set({ textSize:'large' }); window.__GMC.forceUpdate(); });
await shot('state-03-options-textsize-large');
await page.evaluate(()=>{ window.GMSettings.set({ textSize:'default' }); window.__GMC.forceUpdate(); });

// 3. cold open — each card
await go('coldopen');
for (let i = 0; i < 4; i++) {
  await ready(`coldopen phase for card ${i+1}`, ()=>window.__GM.getState().phase==='coldopen', 10000);
  // phase stays 'coldopen' across all four cards, so it cannot tell whether nextCold() advanced.
  // _coldI is the card the screen is actually showing; a mismatch means the PNG below is mislabelled.
  const card = await page.evaluate(()=>window.__GMC._coldI);
  if (card !== i) missed.push(`coldopen card ${i+1} labelled from _coldI=${card}`);
  await shot(`state-04${'abcd'[i]}-coldopen-${i+1}`);
  if (i < 3) await page.evaluate(()=>window.__GMC.nextCold());
}

// 4. invitation letter — front and back
await go('invitation');
// not letterMounted: that flag belongs to the in-hand letter (onRaiseLetter), and devGoTo('invitation')
// clears it — the wait it replaces could never come true, so it timed out silently on every run.
await ready('invitation screen', ()=>window.__GM.getState().phase==='invitation', 10000);
await page.waitForTimeout(900);
await shot('state-05-letter-front');
await page.evaluate(()=>{ const C=window.__GMC; (C.onFlip||C.onFlipLetter||function(){}).call(C); });
await page.waitForTimeout(1100);
await shot('state-06-letter-back');

// 5. walk + tutorial steps (0 fresh, then simulate progress for 1 and 2)
await go('walk');
await page.waitForTimeout(400);
await shot('state-07-walk-tut0');
await page.evaluate(()=>window.__GMC.setState({ tutStep:1 })); await shot('state-08-walk-tut1');
await page.evaluate(()=>window.__GMC.setState({ tutStep:2 })); await shot('state-09-walk-tut2');

// 6. all five drive beats, camera parked at each beat's z so the backdrop matches the moment
const beats = await page.evaluate(()=>window.__GMC.beats.map(b=>({ z:b.z, main:b.main, sub:b.sub })));
for (let i = 0; i < beats.length; i++) {
  const b = beats[i];
  await page.evaluate((args)=>{
    const C=window.__GMC;
    C.cam.position.set(0, 1.7, args.z - 0.5); C.yaw = 0; C.pitch = 0.02;
    C.setState({ beatMain: args.main, beatSub: args.sub, beatOn: true, hintOn: false, tutStep: 3 });
  }, b);
  await shot(`state-10${'abcde'[i]}-beat-${i+1}`);
}

// 7. gate-lock beat in place (looking back at the shut gate)
await page.evaluate(()=>{
  const C=window.__GMC;
  C.cam.position.set(0, 1.7, 58); C.yaw = Math.PI; C.pitch = 0.02;
  C.setState({ beatMain:'Something slammed shut behind me.', beatSub:'When I looked back, the gate was closed — and the lock, somehow, had already turned.', beatOn:true, hintOn:false });
});
await shot('state-11-gatelock-beat');

// 8. letter re-read mid-walk — front, then flipped to the back (the in-hand letter is the
// flippable one; the intro-screen letter is a different, non-flip layout)
await page.evaluate(()=>{ const C=window.__GMC; C.setState({ beatOn:false }); C.onRaiseLetter && C.onRaiseLetter(); });
await page.waitForTimeout(900);
await shot('state-12-letter-midwalk');
await page.evaluate(()=>{ const C=window.__GMC; C.onFlip && C.onFlip(); });
await page.waitForTimeout(1100);
await shot('state-12b-letter-midwalk-back');
await page.evaluate(()=>{ const C=window.__GMC; C.onFlip && C.onFlip(); });
await page.waitForTimeout(1100);
await page.evaluate(()=>{ const C=window.__GMC; C.onLowerLetter && C.onLowerLetter(); });

// 9. secret ending
await go('secretEnding');
await page.waitForTimeout(600);
await shot('state-13-secret-ending');

// 10. small-viewport sanity: menu + walk HUD at 1024x640
await page.setViewportSize({ width: 1024, height: 640 });
await go('menu'); await shot('state-14-menu-1024');
await go('walk'); await page.waitForTimeout(400); await shot('state-15-walk-1024');

console.log('page errors:', pageErrors.length, pageErrors.slice(0,3));
if (missed.length) for (const m of missed) console.log('  ✗ missed state:', m);
failed = pageErrors.length !== 0 || missed.length !== 0;
console.log(failed ? 'CAPTURE STATES FAIL' : 'CAPTURE STATES PASS');
} finally {
  await browser.close(); server.close();
}
process.exit(failed ? 1 : 0);
