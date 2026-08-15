import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';
const ROOT = process.cwd(), PORT = 3805;
const MIME = { '.html':'text/html','.js':'text/javascript','.json':'application/json','.glb':'model/gltf-binary','.gltf':'model/gltf+json','.bin':'application/octet-stream','.png':'image/png','.jpg':'image/jpeg','.ogg':'audio/ogg' };
const server = createServer(async (req,res)=>{ try{ let p=decodeURIComponent(req.url.split('?')[0]); if(p==='/')p='/index.html'; const fp=join(ROOT,p); const buf=await readFile(fp); res.writeHead(200,{'Content-Type':MIME[extname(fp)]||'application/octet-stream'}); res.end(buf);}catch{res.writeHead(404);res.end('nf');} });
await new Promise(r=>server.listen(PORT,r));
const browser = await chromium.launch();
const page = await browser.newPage({ viewport:{ width:1280, height:720 } });
try {
const errs=[]; page.on('pageerror', e=>{ errs.push(e.message); console.log('[pageerror]', e.message); });
const notReady=[];
await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Entry%20Hall.dc.html`,{ waitUntil:'load' });
await page.waitForFunction(()=>window.__GMC && window.__GMC.scene, null, { timeout:60000 }).catch(()=>{ notReady.push('window.__GMC.scene never built — initScene() did not complete'); });
await page.waitForTimeout(4000);
const info = await page.evaluate(()=>({ text:(document.body?document.body.innerText:'').slice(0,400), hasGMC:!!window.__GMC, hasGM:!!window.__GM, canvases:document.querySelectorAll('canvas').length }));
console.log('entry hall text:', JSON.stringify(info.text));
console.log('hasGMC=',info.hasGMC,'hasGM=',info.hasGM,'canvases=',info.canvases,'errs=',errs.length);
await page.screenshot({ path:'docs/playtest/screenshots/handoff-03-entryhall-boot.png' });
// this is a check, not a readout: a boot that produced none of the above must exit nonzero
const problems = [...notReady];
if (!info.hasGMC) problems.push('window.__GMC missing — the hall controller never mounted');
if (!info.hasGM) problems.push('window.__GM missing — no dev bridge to drive the hall');
if (info.canvases < 1) problems.push('no <canvas> in the document — nothing rendered');
if (errs.length) problems.push(`page errors: ${errs.join(' | ')}`);
console.log(problems.length ? 'ENTRY HALL BOOT FAIL' : 'ENTRY HALL BOOT PASS');
if (problems.length) { for (const p of problems) console.error(`  ✗ ${p}`); throw new Error(`entry hall boot: ${problems.length} problem(s)`); }
} finally {
  await browser.close(); server.close();
}
