// G2 runtime proofs: chapel bell one-shot fires in the forecourt (and not on the drive), and
// drive lamp #2 actually behaves like a dying light (deep dips / blackouts vs its steady peers).
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';
const ROOT = process.cwd(), PORT = 3823;
const MIME = { '.html':'text/html','.js':'text/javascript','.json':'application/json','.glb':'model/gltf-binary','.gltf':'model/gltf+json','.bin':'application/octet-stream','.png':'image/png','.ogg':'audio/ogg' };
const server = createServer(async (req,res)=>{ try{ let p=decodeURIComponent(req.url.split('?')[0]); if(p==='/')p='/index.html'; const fp=join(ROOT,p); const buf=await readFile(fp); res.writeHead(200,{'Content-Type':MIME[extname(fp)]||'application/octet-stream'}); res.end(buf);}catch{res.writeHead(404);res.end('nf');} });
await new Promise(r=>server.listen(PORT,r));
const browser = await chromium.launch();
const page = await browser.newPage({ viewport:{ width:1280, height:720 } });
const errs=[]; page.on('pageerror', e=>{ errs.push(e.message); console.log('[pageerror]', e.message); });
await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Prologue.dc.html`,{ waitUntil:'load' });
await page.waitForFunction(()=>window.__GMC && window.__GMC.scene && window.__GMC._ruinsBuilt, null, { timeout:120000 });
await page.waitForTimeout(1500);

const result = await page.evaluate(async ()=>{
  const C = window.__GMC;
  C._sfxSpy = [];
  const orig = C.playSfx.bind(C);
  C.playSfx = (url, vol)=>{ C._sfxSpy.push(url); return orig(url, vol); };
  window.__GM.goTo('walk'); C.walkEnabled = true;
  const sleep = (ms)=>new Promise(r=>setTimeout(r, ms));
  // 1. on the DRIVE: force the bell timer low — it must NOT fire here (forecourt-gated)
  C.cam.position.set(0, 1.7, 40); C._bellTimer = 0.01;
  await sleep(700);
  const firedOnDrive = C._sfxSpy.some(u=>/chapel_bell/.test(u));
  // 2. in the FORECOURT: force it — must fire
  C.cam.position.set(28, 1.7, 31); C._bellTimer = 0.01;
  await sleep(900);
  const firedInCourt = C._sfxSpy.some(u=>/chapel_bell/.test(u));
  // 3. dying lamp vs steady lamp: sample ~1.2s. Distinguish by MEAN brightness (dying swings low)
  // and by floor (nothing may go negative — that was a real pre-existing bug this harness caught).
  const s1 = [], s2 = [];
  for (let i = 0; i < 24; i++) { s1.push(C.lamps[1].intensity / C.lamps[1]._base); s2.push(C.lamps[2].intensity / C.lamps[2]._base); await sleep(50); }
  const mean = (a)=>a.reduce((x,y)=>x+y,0)/a.length;
  // 4. chapel answers on the third toll (force two more tolls; knock is a delayed ko_thud)
  C._bellTimer = 0.01; await sleep(700);
  C._bellTimer = 0.01; await sleep(2400);
  const knock = !!C._chapelKnocked && C._sfxSpy.filter(u=>/ko_thud/.test(u)).length >= 1;
  // 5. the window figure: force on at the gate range → visible; cross z<18 → gone for good
  C._figureForce = true; C.cam.position.set(0, 1.7, 40); await sleep(300);
  const figVisibleFar = !!(C._figure && C._figure.visible);
  C.cam.position.set(0, 1.7, 15); await sleep(300);
  C.cam.position.set(0, 1.7, 40); await sleep(300);
  const figGoneAfter = !(C._figure && C._figure.visible) && !!C._figureGone;
  C._figureForce = null;
  // 6. two-layer examine: second press digs deeper
  C.cam.position.set(19.0, 1.7, 23.4);
  C.tryExamine(); const ex1 = C.state.examineText;
  C.tryExamine(); const ex2 = C.state.examineText;
  const twoLayer = ex1 !== ex2 && /kept ready/i.test(ex2);
  return { firedOnDrive, firedInCourt, mean1: +mean(s1).toFixed(3), mean2: +mean(s2).toFixed(3),
    min1: +Math.min(...s1).toFixed(3), min2: +Math.min(...s2).toFixed(3),
    knock, figVisibleFar, figGoneAfter, twoLayer, errors: (C._errs||[]).length };
});
console.log(JSON.stringify(result));
const bellOK = !result.firedOnDrive && result.firedInCourt;
const lampOK = result.mean2 < result.mean1 - 0.2 && result.min2 <= 0.1 && result.min1 >= 0;
const layersOK = result.knock && result.figVisibleFar && result.figGoneAfter && result.twoLayer;
console.log(`bell gating: ${bellOK ? 'PASS' : 'FAIL'} · dying lamp: ${lampOK ? 'PASS' : 'FAIL'} · layers (knock/figure/two-press): ${layersOK ? 'PASS' : 'FAIL'} (knock:${result.knock} figFar:${result.figVisibleFar} figGone:${result.figGoneAfter} twoLayer:${result.twoLayer})`);
const pass = bellOK && lampOK && layersOK && errs.length === 0 && result.errors === 0;
console.log(pass ? 'VERIFY G2 PASS' : 'VERIFY G2 FAIL');
await browser.close(); server.close();
process.exit(pass ? 0 : 1);
