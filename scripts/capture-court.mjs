// Court + STB shell visual gate shots
import { chromium } from 'playwright';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { extname, join } from 'path';

const ROOT = process.cwd(), PORT = 3815;
const MIME = { '.html':'text/html','.js':'text/javascript','.json':'application/json','.glb':'model/gltf-binary','.gltf':'model/gltf+json','.bin':'application/octet-stream','.png':'image/png','.jpg':'image/jpeg','.ogg':'audio/ogg' };
const server = createServer(async (req, res) => {
  try {
    let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html';
    const buf = await readFile(join(ROOT, p));
    res.writeHead(200, { 'Content-Type': MIME[extname(p)] || 'application/octet-stream' });
    res.end(buf);
  } catch { res.writeHead(404); res.end('nf'); }
});
server.on('error', (e) => { console.error(`FATAL: static server on :${PORT} — ${e.code || e.message}`); process.exit(1); });
await new Promise((r) => server.listen(PORT, r));
const browser = await chromium.launch();

// a gate has to be able to fail: a scene that never becomes ready, a pose that throws, or a page
// error all land in fails[]/errs[] and set the exit code at the bottom.
const fails = [];

async function shotScene(file, waits, poses) {
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  const errs = [];
  page.on('pageerror', (e) => errs.push(e.message));
  try {
    await page.goto(`http://localhost:${PORT}/${encodeURIComponent(file)}`, { waitUntil: 'load' });
    await page.waitForFunction(waits, null, { timeout: 45000 });
    await page.waitForTimeout(900);
    for (const p of poses) {
      await page.evaluate(p.fn);
      await page.waitForTimeout(700);
      await page.screenshot({ path: `docs/playtest/screenshots/${p.name}.png` });
      const s = await page.evaluate(() => window.__GM.getState());
      console.log('shot', p.name, JSON.stringify(s), 'errs', errs.length);
    }
  } catch (e) {
    fails.push(`${file}: ${String(e.message || e).split('\n')[0]}`);
  } finally {
    await page.close().catch(() => {});
  }
  return errs;
}

let courtErrs = [], stbErrs = [];
try {
  courtErrs = await shotScene('The Games Master - Court.dc.html',
    () => window.__GM && window.__GM.getState().sceneReady && window.__GM.getState().gavel,
    [
      { name: 'court-01-entry', fn: () => { window.__GM.goTo('entry'); } },
      { name: 'court-02-bar', fn: () => { window.__GM.goTo('bar'); } },
      { name: 'court-03-bench', fn: () => { window.__GM.goTo('bench'); } },
      { name: 'court-04-tarnish', fn: () => {
        window.__GM.goTo('rigged');
        window.__GMC._presented = [];
        window.__GM.presentTrue();
      } },
    ]);

  stbErrs = await shotScene('The Games Master - Shut the Box.dc.html',
    () => window.__GM && window.__GM.getState().sceneReady && window.__GM.getState().boards === 2,
    [
      { name: 'stb-01-shell', fn: () => {} },
      { name: 'stb-02-tile9', fn: () => { window.__GM.demoShut9(); } },
    ]);
} finally {
  await browser.close().catch(() => {});
  await new Promise((r) => server.close(() => r()));
}

console.log('court pageerrors', courtErrs.length ? courtErrs : 0);
console.log('stb pageerrors', stbErrs.length ? stbErrs : 0);
for (const f of fails) console.log('FAIL', f);
const pageErrCount = courtErrs.length + stbErrs.length;
if (pageErrCount || fails.length) {
  console.log(`FAIL — ${pageErrCount} page error(s), ${fails.length} scene failure(s)`);
  process.exitCode = 1;
} else {
  console.log('done');
}
