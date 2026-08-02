// Entry Hall wake-up + interior matrix: the aftermath lands here, so the first thing the player
// sees after the KO is part of the Phase 0 experience. Drives each dev phase and screenshots it.
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';
const ROOT = process.cwd(), PORT = 3816, DIR = 'docs/playtest/screenshots';
const MIME = { '.html':'text/html','.js':'text/javascript','.json':'application/json','.glb':'model/gltf-binary','.gltf':'model/gltf+json','.bin':'application/octet-stream','.png':'image/png','.jpg':'image/jpeg','.ogg':'audio/ogg' };
const server = createServer(async (req,res)=>{ try{ let p=decodeURIComponent(req.url.split('?')[0]); if(p==='/')p='/index.html'; const fp=join(ROOT,p); const buf=await readFile(fp); res.writeHead(200,{'Content-Type':MIME[extname(fp)]||'application/octet-stream'}); res.end(buf);}catch{res.writeHead(404);res.end('nf');} });
await new Promise(r=>server.listen(PORT,r));
const browser = await chromium.launch();
const page = await browser.newPage({ viewport:{ width:1280, height:720 } });
const errs=[]; page.on('pageerror', e=>{ errs.push(e.message); console.log('[pageerror]', e.message); });
await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Entry%20Hall.dc.html`,{ waitUntil:'load' });
await page.waitForFunction(()=>window.__GMC && window.__GMC.scene, null, { timeout:120000 });
await page.waitForTimeout(5000); // let GLB furniture/portrait mounts land

const phases = ['wake','start','hall','portraits','stairs','door'];
for (const ph of phases) {
  await page.evaluate((n)=>window.__GM.goTo(n), ph);
  await page.waitForTimeout(ph === 'wake' ? 4500 : 900); // wake has a fade-in animation
  await page.screenshot({ path:`${DIR}/hallwake-${ph}.png`, timeout:60000 });
  console.log('shot hallwake-' + ph, JSON.stringify(await page.evaluate(()=>window.__GM.getState())).slice(0,180));
}
console.log('errors:', errs.length, errs.slice(0,3));
console.log(errs.length === 0 ? 'HALL WAKE CAPTURE PASS' : 'HALL WAKE CAPTURE FAIL');
await browser.close(); server.close();
process.exit(errs.length === 0 ? 0 : 1);
