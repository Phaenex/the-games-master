#!/usr/bin/env node
// Headless CI runner for The Games Master's existing browser-based Test Harness.
// Serves the repo locally, drives the harness in headless Chromium, and reports
// the same pass/fail data the harness already computes — no new test logic here.

import { chromium } from 'playwright';
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
const PORT = 8813;
const HARNESS_FILE = 'The Games Master - Test Harness.dc.html';
// 240s: denser estate (wide flank trees + ~500 ground-scatter meshes + far treeline) plus ruins_pack.glb
// + live scene renders + GLTF-dependent async assertions. Was 60 → 90 → 150 → 240 as the exterior densified.
const TIMEOUT_MS = Number(process.env.GM_HARNESS_TIMEOUT_MS || 240_000); // override for focused hang diagnosis

const MIME = { '.html': 'text/html', '.js': 'text/javascript', '.jpg': 'image/jpeg', '.json': 'application/json' };

function startServer() {
  const server = http.createServer((req, res) => {
    const reqPath = decodeURIComponent(req.url.split('?')[0]);
    const filePath = path.join(ROOT, reqPath === '/' ? '/index.html' : reqPath);
    fs.readFile(filePath, (err, data) => {
      if (err) { res.writeHead(404); res.end('not found'); return; }
      const ext = path.extname(filePath);
      res.writeHead(200, { 'Content-Type': MIME[ext] || 'application/octet-stream' });
      res.end(data);
    });
  });
  return new Promise((resolve) => server.listen(PORT, () => resolve(server)));
}

async function main() {
  const server = await startServer();
  let browser;
  try {
    browser = await chromium.launch();
    const page = await browser.newPage();
    const consoleErrors = [];
    // The harness page and the scenes it frames pull webfonts and a CDN copy of three from other
    // origins. Those must not decide the exit code: a sandboxed or offline runner has no egress, and
    // a fully-passing suite would go red for a reason that has nothing to do with the tests. They are
    // still printed, and a CDN that genuinely fails to deliver surfaces as failed assertions or a
    // page error anyway. Anything served from our own port is ours, and does gate.
    const foreignNoise = [];
    const ORIGIN = `http://localhost:${PORT}/`;
    const bucket = (url) => (url.startsWith(ORIGIN) ? consoleErrors : foreignNoise);
    page.on('console', (msg) => {
      // Route console errors by origin too, not just network events. The bucket() rule one line up
      // already declares a third-party request non-gating, and then this pushed the console error
      // that SAME failure produces straight into the gating list -- so a Google Fonts 404 failed
      // gate 1 and, with the pipeline stopping at the first failure, cost all twelve gates below it.
      // Observed 2026-08-15: "47 passed, 0 failed" alongside "2 third-party request problem(s)
      // (reported, not gating)" and a red run.
      //
      // A message with no location is ours by default. Guessing the other way would let a real
      // uncaught error escape because Playwright happened not to attribute it.
      if (msg.type() === 'error') bucket(msg.location()?.url ?? ORIGIN).push(msg.text());
      if (msg.text().startsWith('[Harness progress]') || msg.text().startsWith('[Harness test]')) console.log(msg.text());
    });
    page.on('pageerror', (err) => consoleErrors.push(`pageerror: ${err.message}`));
    page.on('response', (res) => {
      if (res.status() >= 400) bucket(res.url()).push(`response ${res.status()}: ${res.url()}`);
    });
    page.on('requestfailed', (req) => {
      bucket(req.url()).push(`requestfailed: ${req.url()} (${req.failure()?.errorText || 'unknown'})`);
    });

    // The interactive harness deliberately keeps every live preview visible. In headless CI that
    // means five permanent requestAnimationFrame/WebGL loops compete until timers barely advance.
    // The harness reads this flag and unloads each iframe only after its scene's tests finish.
    await page.addInitScript(() => { window.__GM_HEADLESS_HARNESS = true; });
    await page.goto(`http://localhost:${PORT}/${encodeURIComponent(HARNESS_FILE)}`);

    const finalText = await page.evaluate(async (timeoutMs) => {
      const start = Date.now();
      let lastProgress = '';
      while (Date.now() - start < timeoutMs) {
        const text = document.body.innerText;
        const summary = text.match(/\d+ passed · \d+ failed/)?.[0] || '0 passed · 0 failed';
        const scenes = [...document.querySelectorAll('#gm-harness-frames iframe')].map((f) => f.title).join(',');
        const progress = `${summary}; frames=${scenes || 'none'}`;
        if (progress !== lastProgress) { console.log('[Harness progress] ' + progress); lastProgress = progress; }
        if (text.includes('complete')) return text;
        await new Promise((r) => setTimeout(r, 500));
      }
      return document.body.innerText; // return whatever we have if it never finishes
    }, TIMEOUT_MS);

    if (!finalText.includes('complete')) {
      console.error('✗ Test Harness never reached "complete" within', TIMEOUT_MS, 'ms');
      console.error(finalText);
      process.exitCode = 1;
      return;
    }

    const summaryMatch = finalText.match(/(\d+) passed · (\d+) failed/);
    const passed = summaryMatch ? Number(summaryMatch[1]) : 0;
    const failed = summaryMatch ? Number(summaryMatch[2]) : -1;

    // pull out each failing row's name + message for a readable report
    const lines = finalText.split('\n');
    const failures = [];
    for (let i = 0; i < lines.length; i++) {
      if (lines[i] === '✗') {
        const name = lines[i + 1] || '(unknown test)';
        const msg = lines[i + 2] && !['✓', '✗'].includes(lines[i + 2]) ? lines[i + 2] : '';
        failures.push(msg ? `${name} — ${msg}` : name);
      }
    }

    console.log(`\nThe Games Master · Test Harness (headless)\n${'-'.repeat(44)}`);
    console.log(`${passed} passed, ${failed} failed`);
    if (failures.length) {
      console.log('\nFailures:');
      failures.forEach((f) => console.log(`  ✗ ${f}`));
    }
    if (consoleErrors.length) {
      console.log(`\n${consoleErrors.length} browser console error(s):`);
      consoleErrors.forEach((e) => console.log(`  ! ${e}`));
    }
    if (foreignNoise.length) {
      console.log(`\n${foreignNoise.length} third-party request problem(s) (reported, not gating):`);
      foreignNoise.forEach((e) => console.log(`  · ${e}`));
    }
    console.log('');

    process.exitCode = failed === 0 && consoleErrors.length === 0 ? 0 : 1;
  } finally {
    if (browser) await browser.close();
    server.close();
  }
}

main().catch((err) => {
  console.error('Test runner crashed:', err);
  process.exitCode = 1;
});
