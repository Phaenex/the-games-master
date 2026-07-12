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
const PORT = 8765;
const HARNESS_FILE = 'The Games Master - Test Harness.dc.html';
const TIMEOUT_MS = 60_000;

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
  const browser = await chromium.launch();
  const page = await browser.newPage();
  const consoleErrors = [];
  page.on('console', (msg) => { if (msg.type() === 'error') consoleErrors.push(msg.text()); });

  try {
    await page.goto(`http://localhost:${PORT}/${encodeURIComponent(HARNESS_FILE)}`);

    const finalText = await page.evaluate(async (timeoutMs) => {
      const start = Date.now();
      while (Date.now() - start < timeoutMs) {
        if (document.body.innerText.includes('complete')) return document.body.innerText;
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
    console.log('');

    process.exitCode = failed === 0 ? 0 : 1;
  } finally {
    await browser.close();
    server.close();
  }
}

main().catch((err) => {
  console.error('Test runner crashed:', err);
  process.exitCode = 1;
});
