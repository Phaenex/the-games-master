// Verifies the aftermath -> Continue link actually lands the player in the Entry Hall scene, boots it,
// and produces no page errors. Jumps to the 'aftermath' phase, clicks the Continue anchor, waits for nav.
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';
const ROOT = process.cwd(), PORT = 3803;
const MIME = { '.html':'text/html','.js':'text/javascript','.json':'application/json','.glb':'model/gltf-binary','.gltf':'model/gltf+json','.bin':'application/octet-stream','.png':'image/png','.jpg':'image/jpeg','.ogg':'audio/ogg' };
const server = createServer(async (req,res)=>{ try{ let p=decodeURIComponent(req.url.split('?')[0]); if(p==='/')p='/index.html'; const fp=join(ROOT,p); const buf=await readFile(fp); res.writeHead(200,{'Content-Type':MIME[extname(fp)]||'application/octet-stream'}); res.end(buf);}catch{res.writeHead(404);res.end('nf');} });
await new Promise(r=>server.listen(PORT,r));
const browser = await chromium.launch();
const page = await browser.newPage({ viewport:{ width:1280, height:720 } });
try {
const errs=[]; page.on('pageerror', e=>{ errs.push(e.message); console.log('[pageerror]', e.message); });
await page.goto(`http://localhost:${PORT}/The%20Games%20Master%20-%20Prologue.dc.html`,{ waitUntil:'load' });
await page.waitForFunction(()=>window.__GM && window.__GMC && window.__GMC.scene, null, { timeout:60000 });
await page.waitForTimeout(1000);

// jump to aftermath
await page.evaluate(()=>window.__GM.goTo('aftermath'));
await page.waitForTimeout(500);
const st = await page.evaluate(()=>window.__GM.getState());
console.log('aftermath state:', st.phase, 'isArrival(dev phase)=', st.phase==='aftermath');
await page.screenshot({ path:'docs/playtest/screenshots/handoff-01-aftermath.png', timeout:120000 });

// the continue anchor
const contHref = await page.evaluate(()=>{ const a=[...document.querySelectorAll('a')].find(a=>/continue/i.test(a.textContent)); return a?a.getAttribute('href'):null; });
console.log('continue href =', contHref);

// click it and wait for navigation to the Entry Hall
const cont = page.locator('a', { hasText: /continue/i }).first();
await cont.click();
await page.waitForURL(/Entry%20Hall|Entry Hall/, { timeout:60000 }).catch(()=>{});
await page.waitForTimeout(2500);
const url = page.url();
const title = await page.title().catch(()=>'');
const bodyLen = await page.evaluate(()=>document.body ? document.body.innerText.length : 0);
console.log('landed url =', decodeURIComponent(url));
console.log('title =', title, '| body text length =', bodyLen);
await page.screenshot({ path:'docs/playtest/screenshots/handoff-02-entry-hall.png', timeout:120000 });

const ok = /Entry%20Hall|Entry Hall/.test(url) && errs.length===0;
console.log('\n=== HANDOFF:', ok?'PASS':'FAIL', '=== pageerrors:', errs.length);
if (!ok) throw new Error(`Entry Hall handoff failed: url=${url}, pageerrors=${errs.join(' | ') || 'none'}`);
} finally {
  await browser.close();
  server.close();
}
