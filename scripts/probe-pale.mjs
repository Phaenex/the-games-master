// Diagnose the pale-cutout tell: re-render the poses from today's full-* shots, screenshot them
// fresh, then raycast through a screen grid and report exactly which object/material every pale
// region belongs to (name, gmKind ancestry, material type/color/fog/map). Evidence, not guesses.
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';
const ROOT = process.cwd(), PORT = 3811;
const OUT = process.env.PROBE_OUT || 'docs/playtest/screenshots';
const MIME = { '.html':'text/html','.js':'text/javascript','.json':'application/json','.glb':'model/gltf-binary','.gltf':'model/gltf+json','.bin':'application/octet-stream','.png':'image/png','.ogg':'audio/ogg' };
const server = createServer(async (req,res)=>{ try{ let p=decodeURIComponent(req.url.split('?')[0]); if(p==='/')p='/index.html'; const fp=join(ROOT,p); const buf=await readFile(fp); res.writeHead(200,{'Content-Type':MIME[extname(fp)]||'application/octet-stream'}); res.end(buf);}catch{res.writeHead(404);res.end('nf');} });
await new Promise(r=>server.listen(PORT,r));
const browser = await chromium.launch();
const page = await browser.newPage({ viewport:{ width:1280, height:720 } });
page.on('pageerror', e=>console.log('[pageerror]', e.message));
await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Prologue.dc.html`,{ waitUntil:'load' });
await page.waitForFunction(()=>window.__GMC && window.__GMC.scene && window.__GMC._ruinsBuilt, null, { timeout:120000 });
await page.waitForFunction(()=>window.__GMC._leartesTreesReady && window.__GMC._leartesGroundsReady && window.__GMC._ownedTreesReady, null, { timeout:120000 }).catch(()=>{});
await page.waitForTimeout(2500);

const poses = [
  { name:'pale-01-spawn',    pose:{ x:0,   y:1.65, z:72, yaw:0,    pitch:0.02 } },
  { name:'pale-02-middrive', pose:{ x:0,   y:1.7,  z:30, yaw:0,    pitch:0.02 } },
  { name:'pale-03-cemetery', pose:{ x:3,   y:1.7,  z:28, yaw:-0.9, pitch:0.02 } },
  { name:'pale-04-porch',    pose:{ x:0,   y:2.2,  z:-44,yaw:0,    pitch:0.0  } },
];

for (const s of poses) {
  const hits = await page.evaluate((pose)=>{
    const C = window.__GMC, THREE = window.THREE;
    if (window.__GM && window.__GM.goTo) { window.__GM.goTo('walk'); C.walkEnabled = false; }
    if (C._raf) { cancelAnimationFrame(C._raf); C._raf = null; }
    C.cam.position.set(pose.x, pose.y, pose.z); C.yaw = pose.yaw; C.pitch = pose.pitch;
    C.cam.rotation.set(C.pitch, C.yaw, 0, 'YXZ');
    C.renderer.render(C.scene, C.cam);
    const ray = new THREE.Raycaster();
    ray.far = 400;
    const out = [];
    for (let gy = -0.5; gy <= 0.6; gy += 0.22) {
      for (let gx = -0.9; gx <= 0.9; gx += 0.18) {
        ray.setFromCamera(new THREE.Vector2(gx, gy), C.cam);
        const hit = ray.intersectObjects(C.scene.children, true).find(h => h.object.visible && h.object.isMesh);
        if (!hit) continue;
        const o = hit.object;
        let kind = null;
        for (let p = o; p; p = p.parent) { if (p.userData && p.userData.gmKind) { kind = p.userData.gmKind; break; } }
        const names = [];
        for (let p = o, i = 0; p && i < 6; p = p.parent, i++) names.push(p.name || '·');
        const m = Array.isArray(o.material) ? o.material[0] : o.material;
        out.push({
          ndc: [Number(gx.toFixed(2)), Number(gy.toFixed(2))],
          dist: Number(hit.distance.toFixed(1)),
          kind: kind || '(none)',
          names: names.join('>'),
          mat: m ? { type: m.type, color: m.color ? m.color.getHexString() : null, fog: m.fog !== false, map: !!m.map, emissive: m.emissive ? m.emissive.getHexString() : null, emissiveIntensity: m.emissiveIntensity ?? null } : null,
        });
      }
    }
    return out;
  }, s.pose);
  await page.waitForTimeout(120);
  await page.screenshot({ path: `${OUT}/${s.name}.png`, timeout: 60000 });
  console.log(`=== ${s.name} ===`);
  // Group hits by material signature so the report is readable
  const seen = new Map();
  for (const h of hits) {
    const key = `${h.kind}|${h.mat?.type}|${h.mat?.color}|fog:${h.mat?.fog}|map:${h.mat?.map}`;
    if (!seen.has(key)) seen.set(key, { ...h, count: 0, samples: [] });
    const g = seen.get(key);
    g.count++;
    if (g.samples.length < 2) g.samples.push(`${h.names} @${h.dist}u ndc(${h.ndc})`);
  }
  for (const g of [...seen.values()].sort((a,b)=>b.count-a.count)) {
    console.log(` x${String(g.count).padStart(2)} kind=${g.kind} mat=${g.mat?.type} color=#${g.mat?.color} fog=${g.mat?.fog} map=${g.mat?.map} emissive=${g.mat?.emissive}/${g.mat?.emissiveIntensity}`);
    for (const smp of g.samples) console.log(`      ${smp}`);
  }
}
await browser.close(); server.close(); console.log('done');
