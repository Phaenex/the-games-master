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
let browser = null, pass = false;
try {
browser = await chromium.launch();
const page = await browser.newPage({ viewport:{ width:1280, height:720 } });
const errs=[]; page.on('pageerror', e=>{ errs.push(e.message); console.log('[pageerror]', e.message); });
const fails=[];
await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Entry%20Hall.dc.html`,{ waitUntil:'load' });
await page.waitForFunction(()=>window.__GMC && window.__GMC.scene, null, { timeout:120000 });
await page.waitForTimeout(5000); // let GLB furniture/portrait mounts land

const phases = ['wake','start','hall','portraits','stairs','door'];
for (const ph of phases) {
  await page.evaluate((n)=>window.__GM.goTo(n), ph);
  if (ph === 'wake') {
    // The wake-up is a scripted schedule of six state changes, so a wall-clock wait reshoots a
    // different beat the moment that schedule is retuned, with no signal that it moved. Wait for
    // the sequence to arm (fade held at 1 on the marble), then for its first fade step — the
    // same beat the old fixed wait landed on, but pinned to the sequence instead of the clock.
    const armed = await page.waitForFunction(()=>window.__GMC.waking === true && window.__GMC.state.fade === 1, null, { timeout:10000 }).then(()=>true).catch(()=>false);
    const stepped = armed && await page.waitForFunction(()=>window.__GMC.waking === true && window.__GMC.state.fade < 1, null, { timeout:20000 }).then(()=>true).catch(()=>false);
    if (!stepped) { fails.push(`wake sequence never reached its first fade step (armed=${armed})`); console.log('FAIL wake sequence never reached its first fade step'); }
  }
  await page.waitForTimeout(900);
  await page.screenshot({ path:`${DIR}/hallwake-${ph}.png`, timeout:60000 });
  console.log('shot hallwake-' + ph, JSON.stringify(await page.evaluate(()=>window.__GM.getState())).slice(0,180));
  // devSnapshot() carries no beat text, so record which wake beat the frame actually caught.
  if (ph === 'wake') console.log('  wake beat captured:', JSON.stringify(await page.evaluate(()=>window.__GMC.state.beatMain)));
}
console.log('errors:', errs.length, errs.slice(0,3));
if (fails.length) console.log('failures:', fails);
pass = errs.length === 0 && fails.length === 0;
console.log(pass ? 'HALL WAKE CAPTURE PASS' : 'HALL WAKE CAPTURE FAIL');
} catch (e) {
  console.log('HALL WAKE CAPTURE FAIL — threw:', (e && e.message) || e);
} finally {
  if (browser) await browser.close().catch(()=>{});
  server.close();
}
process.exit(pass ? 0 : 1);
