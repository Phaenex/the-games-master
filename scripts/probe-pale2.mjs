// Hide-and-seek probe: sample real rendered pixels at pale screen points, then recursively
// hide scene subtrees until the pale pixel goes dark. Names the exact object painting each
// pale region — no raycast ambiguity, no material guesses.
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';
const ROOT = process.cwd(), PORT = 3812;
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

// pose + pixel points chosen from the pale regions in pale-01/pale-03 renders (1280x720)
const jobs = [
  { name:'cemetery cream boxes',    pose:{ x:3, y:1.7, z:28, yaw:-0.9, pitch:0.02 }, px:[ [490,470], [620,480], [860,420], [1050,420] ] },
  { name:'spawn flank cream rows',  pose:{ x:0, y:1.65, z:72, yaw:0,   pitch:0.02 }, px:[ [150,420], [1120,430] ] },
];

const result = await page.evaluate(async (jobs)=>{
  const C = window.__GMC, THREE = window.THREE;
  if (window.__GM && window.__GM.goTo) { window.__GM.goTo('walk'); C.walkEnabled = false; }
  if (C._raf) { cancelAnimationFrame(C._raf); C._raf = null; }
  const W = 1280, H = 720;
  const rt = new THREE.WebGLRenderTarget(W, H);
  const buf = new Uint8Array(4);
  const sample = (x, y) => {
    C.renderer.setRenderTarget(rt);
    C.renderer.render(C.scene, C.cam);
    C.renderer.readRenderTargetPixels(rt, x, H - y, 1, 1, buf);
    C.renderer.setRenderTarget(null);
    return [buf[0], buf[1], buf[2]];
  };
  const lum = (rgb) => (rgb[0] + rgb[1] + rgb[2]) / 3;
  const describe = (node) => {
    const names = [];
    for (let p = node, i = 0; p && i < 6; p = p.parent, i++) names.push(p.name || p.userData?.gmKind || p.type || '·');
    let kind = null;
    for (let p = node; p; p = p.parent) if (p.userData?.gmKind) { kind = p.userData.gmKind; break; }
    let matInfo = [];
    node.traverse((o) => {
      if (o.isMesh && o.material && matInfo.length < 4) {
        const ms = Array.isArray(o.material) ? o.material : [o.material];
        for (const m of ms) matInfo.push(`${m.type}#${m.color ? m.color.getHexString() : '?'} fog:${m.fog !== false} map:${!!m.map} vtxCol:${!!m.vertexColors} tone:${m.toneMapped !== false}`);
      }
    });
    return { path: names.join('>'), kind, mats: [...new Set(matInfo)].slice(0, 4) };
  };
  const out = [];
  for (const job of jobs) {
    C.cam.position.set(job.pose.x, job.pose.y, job.pose.z); C.yaw = job.pose.yaw; C.pitch = job.pose.pitch;
    C.cam.rotation.set(C.pitch, C.yaw, 0, 'YXZ');
    for (const [px, py] of job.px) {
      const base = sample(px, py);
      if (lum(base) < 6) { out.push({ job: job.name, px: [px, py], base, verdict: 'already near-black' }); continue; }
      // recursive bisect: find deepest node whose hiding halves the pixel luminance
      let node = C.scene, found = null;
      for (let depth = 0; depth < 8; depth++) {
        let culprit = null;
        for (const child of node.children) {
          if (!child.visible) continue;
          child.visible = false;
          const v = sample(px, py);
          child.visible = true;
          if (lum(v) < lum(base) * 0.5) { culprit = child; break; }
        }
        if (!culprit) break;
        found = culprit;
        node = culprit;
        if (!culprit.children || !culprit.children.length) break;
      }
      out.push({ job: job.name, px: [px, py], base, found: found ? describe(found) : null });
    }
  }
  rt.dispose();
  return out;
}, jobs);
for (const r of result) {
  console.log(`\n[${r.job}] px(${r.px}) rgb=${r.base}${r.verdict ? ' — ' + r.verdict : ''}`);
  if (r.found) {
    console.log(`  culprit: ${r.found.path}  kind=${r.found.kind}`);
    for (const m of r.found.mats) console.log(`    mat: ${m}`);
  }
}
await browser.close(); server.close(); console.log('\ndone');
