// Estate rebuild visual gate: flush doors, side paths, cemetery/garden interiors, chapel + coach
// house landmarks. Static poses + a live walk INTO the cemetery to prove the path is walkable.
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';
const ROOT = process.cwd(), PORT = 3821, DIR = 'docs/playtest/screenshots';
const MIME = { '.html':'text/html','.js':'text/javascript','.json':'application/json','.glb':'model/gltf-binary','.gltf':'model/gltf+json','.bin':'application/octet-stream','.png':'image/png','.ogg':'audio/ogg' };
const server = createServer(async (req,res)=>{ try{ let p=decodeURIComponent(req.url.split('?')[0]); if(p==='/')p='/index.html'; const fp=join(ROOT,p); const buf=await readFile(fp); res.writeHead(200,{'Content-Type':MIME[extname(fp)]||'application/octet-stream'}); res.end(buf);}catch{res.writeHead(404);res.end('nf');} });
await new Promise(r=>server.listen(PORT,r));
const browser = await chromium.launch();
const page = await browser.newPage({ viewport:{ width:1280, height:720 } });
const errs = [];
page.on('pageerror', e=>{ errs.push(e.message); console.log('[pageerror]', e.message); });
await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Prologue.dc.html`,{ waitUntil:'load' });
await page.waitForFunction(()=>window.__GMC && window.__GMC.scene && window.__GMC._ruinsBuilt, null, { timeout:120000 });
await page.waitForFunction(()=>window.__GMC._estateReady && window.__GMC._leartesGroundsReady, null, { timeout:120000 }).catch(()=>{ console.log('WARN estate/grounds ready wait timed out'); });
await page.waitForTimeout(2500);

const poses = [
  { name:'estate-01-door-close',     pose:{ x:0,   y:2.2, z:-42,  yaw:0,     pitch:0.04 } },
  { name:'estate-02-door-nose',      pose:{ x:0,   y:3.3, z:-47.5,yaw:0,     pitch:0.0  } },
  { name:'estate-03-cem-junction',   pose:{ x:2.0, y:1.7, z:28.5, yaw:-1.45, pitch:0.02 } },
  { name:'estate-04-cem-path',       pose:{ x:7.5, y:1.7, z:28.5, yaw:-1.2,  pitch:0.02 } },
  { name:'estate-05-cem-inside',     pose:{ x:14,  y:1.7, z:27,   yaw:-0.9,  pitch:0.04 } },
  { name:'estate-06-chapel',         pose:{ x:18,  y:1.7, z:30,   yaw:-1.5,  pitch:0.1  } },
  { name:'estate-07-gdn-junction',   pose:{ x:-2,  y:1.7, z:22,   yaw:1.45,  pitch:0.02 } },
  { name:'estate-08-gdn-inside',     pose:{ x:-12, y:1.7, z:24,   yaw:1.0,   pitch:0.03 } },
  { name:'estate-09-coach-house',    pose:{ x:-8,  y:1.7, z:38,   yaw:2.2,   pitch:0.04 } },
  { name:'estate-10-drive-overview', pose:{ x:0,   y:1.7, z:44,   yaw:-0.55, pitch:0.03 } },
  { name:'estate-12-gate-plaque',    pose:{ x:2.4, y:1.7, z:69.5, yaw:0.18,  pitch:0.0  } },
  { name:'estate-13-lychgate',       pose:{ x:21,  y:1.7, z:30,   yaw:-1.5,  pitch:0.03 } },
  { name:'estate-14-chapel-court',   pose:{ x:26.5,y:1.7, z:30,   yaw:-1.5,  pitch:0.08 } },
  { name:'estate-15-coach-yard',     pose:{ x:-25.5,y:1.7,z:43,   yaw:2.55,  pitch:0.03 } },
];
for (const s of poses) {
  await page.evaluate((pose)=>{
    const C=window.__GMC; window.__GM.goTo('walk'); C.walkEnabled=false;
    if(C._raf){ cancelAnimationFrame(C._raf); C._raf=null; }
    C.cam.position.set(pose.x,pose.y,pose.z); C.yaw=pose.yaw; C.pitch=pose.pitch;
    C.cam.rotation.set(C.pitch,C.yaw,0,'YXZ'); C._linearizeFlatColors(); C.renderer.render(C.scene,C.cam);
  }, s.pose);
  await page.waitForTimeout(140);
  await page.screenshot({ path:`${DIR}/${s.name}.png`, timeout:60000 });
  console.log('shot', s.name);
}

// live walk proof: start on the drive at the junction, walk east along the path into the plot.
// The static poses above killed the RAF loop — restart it or nothing moves.
await page.evaluate(()=>{ const C=window.__GMC; window.__GM.goTo('walk'); C.walkEnabled=true; C.yaw=-Math.PI/2; C.pitch=0.02; C.cam.position.set(0,1.7,28.5); C._passedGate=true; C._gateLockFired=true; if(!C._raf){ C.clock && C.clock.getDelta(); C._raf = requestAnimationFrame(()=>C.loop()); } });
await page.keyboard.down('w');
await page.waitForFunction(()=>window.__GMC.cam.position.x > 8.5, null, { timeout: 30000 }).catch(()=>{});
await page.keyboard.up('w');
const endPos = await page.evaluate(()=>({ x:+window.__GMC.cam.position.x.toFixed(1), z:+window.__GMC.cam.position.z.toFixed(1) }));
await page.screenshot({ path:`${DIR}/estate-11-walked-into-cemetery.png`, timeout:60000 });
const snap = await page.evaluate(()=>window.__GM.getState());
console.log('walked to', JSON.stringify(endPos), '| estateCount:', snap.estateCount, '| sidePaths:', snap.sidePaths, '| errors:', snap.errors);
const walkedIn = endPos.x > 8.5;
console.log(`walk-into-cemetery: ${walkedIn ? 'PASS' : 'FAIL'} (x=${endPos.x}, need > 8.5)`);

// examine POI: teleport beside the open grave, press the actual E path, assert the line shows
const exam = await page.evaluate(()=>{
  const C = window.__GMC;
  C.cam.position.set(19.0, 1.7, 23.4);
  C.tryExamine();
  return { on: C.state.examineOn, text: C.state.examineText };
});
await page.waitForTimeout(350);
await page.screenshot({ path:`${DIR}/estate-16-examine-grave.png`, timeout:60000 });
const examOK = exam.on && /Fresh-dug/.test(exam.text);
console.log(`examine-at-grave: ${examOK ? 'PASS' : 'FAIL'} (${JSON.stringify(exam)})`);

// new-rect reachability: assert the chapel forecourt and coach-house yard accept positions
const rects = await page.evaluate(()=>{
  const ok = (x,z)=> window.__GMC._walkRects.some(r=>x>=r[0]&&x<=r[1]&&z>=r[2]&&z<=r[3]);
  return { chapel: ok(30, 30), coachYard: ok(-28, 48), connector: ok(-24, 37) };
});
const rectsOK = rects.chapel && rects.coachYard && rects.connector;
console.log(`new walk rects: ${rectsOK ? 'PASS' : 'FAIL'} ${JSON.stringify(rects)}`);

const pass = errs.length === 0 && walkedIn && examOK && rectsOK;
console.log(pass ? 'ESTATE CAPTURE PASS' : 'ESTATE CAPTURE FAIL');
await browser.close(); server.close();
process.exit(pass ? 0 : 1);
