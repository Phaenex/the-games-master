// Hard audit of the mansion asset + our door overlay. Captures multiple angles and dumps
// native mesh names near the entrance so we can tell "bad mansion" vs "bad overlay".
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';
const ROOT = process.cwd(), PORT = 3811, DIR = 'docs/playtest/screenshots';
const MIME = { '.html':'text/html','.js':'text/javascript','.json':'application/json','.glb':'model/gltf-binary','.gltf':'model/gltf+json','.bin':'application/octet-stream','.png':'image/png','.jpg':'image/jpeg','.ogg':'audio/ogg' };
const server = createServer(async (req,res)=>{ try{ let p=decodeURIComponent(req.url.split('?')[0]); if(p==='/')p='/index.html'; const fp=join(ROOT,p); const buf=await readFile(fp); res.writeHead(200,{'Content-Type':MIME[extname(fp)]||'application/octet-stream'}); res.end(buf);}catch{res.writeHead(404);res.end('nf');} });
await new Promise(r=>server.listen(PORT,r));
const browser = await chromium.launch();
const page = await browser.newPage({ viewport:{ width:1280, height:720 } });
page.on('pageerror', e=>console.log('[pageerror]', e.message));
await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Prologue.dc.html`,{ waitUntil:'load' });
await page.waitForFunction(()=>window.__GMC && window.__GMC.scene && window.__GMC.mansionModel, null, { timeout:120000 });
await page.waitForTimeout(2000);

const pose = async (x,y,z,yaw,pitch=0)=>{ await page.evaluate(([px,py,pz,yw,pi])=>{ const C=window.__GMC; window.__GM.goTo('walk'); C.walkEnabled=false; if(C._raf){cancelAnimationFrame(C._raf);C._raf=null;} C.cam.position.set(px,py,pz); C.yaw=yw; C.pitch=pi; C.cam.rotation.set(pi,yw,0,'YXZ'); C.renderer.render(C.scene,C.cam); }, [x,y,z,yaw,pitch]); await page.waitForTimeout(220); };
const shot = async (n)=>{ await page.screenshot({ path:`${DIR}/${n}.png`, timeout:60000 }); console.log('shot', n); };

// inspect native mansion: mesh names, any door-like pieces, bbox of model
const inspect = await page.evaluate(()=>{
  const m = window.__GMC.mansionModel; const THREE=window.THREE;
  m.updateMatrixWorld(true);
  const box = new THREE.Box3().setFromObject(m);
  const names=[]; let doorish=[];
  m.traverse(o=>{ if(!o.isMesh) return; names.push(o.name||'(unnamed)');
    const n=((o.name||'')+' '+((o.material&&o.material.name)||'')).toLowerCase();
    if(/door|entrance|porch|step|stair|knob|frame|threshold/.test(n)) doorish.push({name:o.name, mat:(o.material&&o.material.name)||'', verts:o.geometry?.attributes?.position?.count||0});
  });
  // closest meshes to world door opening (~0, 2.75, -52)
  const near=[]; const tgt=new THREE.Vector3(0,2.75,-52.1);
  m.traverse(o=>{ if(!o.isMesh) return; const c=new THREE.Box3().setFromObject(o).getCenter(new THREE.Vector3());
    const d=c.distanceTo(tgt); if(d<8) near.push({name:o.name||'(unnamed)', d:+d.toFixed(2), pos:[+c.x.toFixed(1),+c.y.toFixed(1),+c.z.toFixed(1)], size: (()=>{const b=new THREE.Box3().setFromObject(o); return [+(b.max.x-b.min.x).toFixed(1),+(b.max.y-b.min.y).toFixed(1),+(b.max.z-b.min.z).toFixed(1)];})() });
  });
  near.sort((a,b)=>a.d-b.d);
  return {
    meshCount: names.length,
    size:[+(box.max.x-box.min.x).toFixed(1),+(box.max.y-box.min.y).toFixed(1),+(box.max.z-box.min.z).toFixed(1)],
    doorish,
    nearEntrance: near.slice(0,18),
    sampleNames: names.filter(n=>n&&n!=='(unnamed)').slice(0,40),
    hasVestibule: !!window.__GMC.vestibule,
    hasDoorL: !!window.__GMC.doorL,
  };
});
console.log('INSPECT', JSON.stringify(inspect,null,2));

// hide OUR door overlay + vestibule + any non-mansion meshes planted on the doorway so we can see
// what the native Haunted Victorian House actually has at the entrance (the real question)
await page.evaluate(()=>{
  const C=window.__GMC; const THREE=window.THREE;
  [C.doorL,C.doorR,C.doorDark,C.doorGlow,C.vestibule].forEach(o=>{ if(o) o.visible=false; });
  C._auditHidden = [];
  C.scene.traverse(o=>{
    if(!o.isMesh) return;
    let p=o; let underMansion=false;
    while(p){ if(p===C.mansionModel){ underMansion=true; break; } p=p.parent; }
    if(underMansion) return;
    const c=new THREE.Box3().setFromObject(o).getCenter(new THREE.Vector3());
    // our frame/doors/vest sit around the doorway band
    if (Math.abs(c.x)<4 && c.z<-48 && c.z>-55 && c.y>1 && c.y<6){ C._auditHidden.push(o); o.visible=false; }
  });
});

// Shot set A: NATIVE mansion only (our overlay hidden) — tells us if the asset itself is the problem
await pose(0, 1.7, -10, 0, 0.08); await shot('audit-01-native-mid');
await pose(0, 2.2, -38, 0, 0.12); await shot('audit-02-native-porch');
await pose(0, 3.4, -47, 0, -0.05); await shot('audit-03-native-door-close');
await pose(-6, 3.0, -45, 0.7, 0.05); await shot('audit-04-native-3q');
await pose(0, 1.7, 20, 0, 0.1); await shot('audit-05-native-far');

// restore OUR overlay
await page.evaluate(()=>{
  const C=window.__GMC;
  (C._auditHidden||[]).forEach(o=>{ o.visible=true; });
  [C.doorL,C.doorR,C.doorDark,C.doorGlow,C.vestibule].forEach(o=>{ if(o) o.visible=true; });
  if(C.doorL) C.doorL.rotation.y=0; if(C.doorR) C.doorR.rotation.y=0;
  if(C.doorDark) C.doorDark.visible=true;
  if(C.doorGlow) C.doorGlow.material.opacity=0;
  C.renderer.render(C.scene,C.cam);
});

// Shot set B: with our overlay closed
await pose(0, 2.2, -38, 0, 0.12); await shot('audit-06-ours-porch-closed');
await pose(0, 3.4, -47, 0, -0.05); await shot('audit-07-ours-door-closed');

// Shot set C: with our overlay open
await page.evaluate(()=>{
  const C=window.__GMC;
  if(C.doorL) C.doorL.rotation.y=1.52; if(C.doorR) C.doorR.rotation.y=-1.52;
  if(C.doorDark) C.doorDark.visible=false;
  if(C.doorInner) C.doorInner.intensity=5.5;
  if(C.chandGlow) C.chandGlow.intensity=2.2;
  C.renderer.render(C.scene,C.cam);
});
await pose(0, 3.4, -47.2, 0, -0.12); await shot('audit-08-ours-door-open');
await pose(0, 3.55, -47.4, 0, -0.17); await shot('audit-09-ours-inside-frame');

// Shot set D: porch flanks of mansion body (model quality aside from door)
await pose(-8, 2.5, -48, 0.9, 0.1); await shot('audit-10-left-facade');
await pose(8, 2.5, -48, -0.9, 0.1); await shot('audit-11-right-facade');
await pose(0, 6, -40, 0, 0.55); await shot('audit-12-look-up-tower');

console.log('done');
await browser.close(); server.close();
