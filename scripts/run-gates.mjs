// ONE COMMAND, FULL CONFIDENCE: runs every gate in sequence and prints a pass/fail table.
// This is the entry point for "did I break anything?" after ANY change. npm run gates.
import { spawn } from 'child_process';

const GATES = [
  { name: 'unit+harness tests', cmd: ['npm', ['test']] },
  { name: 'agent playtest',     cmd: ['node', ['scripts/agent-playtest.mjs']] },
  { name: 'door sequence',      cmd: ['node', ['scripts/play-door.mjs']] },
  { name: 'full walk',          cmd: ['node', ['scripts/play-full.mjs']] },
  { name: 'env entrance',       cmd: ['node', ['scripts/verify-env-entrance.mjs']] },
  { name: 'porch breath',       cmd: ['node', ['scripts/verify-breath.mjs']] },
  { name: 'g2 bell+lamp',       cmd: ['node', ['scripts/verify-g2.mjs']] },
  { name: 'hall handoff',       cmd: ['node', ['scripts/verify-handoff.mjs']] },
  { name: 'polish (leaves/owl)',cmd: ['node', ['scripts/verify-polish.mjs']] },
];

const run = (cmd, args) => new Promise((resolve) => {
  const t0 = Date.now();
  // A gate that throws before browser.close() must not leave a software-rendering Chromium tree to
  // starve every later visual test. A detached process group lets the runner clean up only descendants
  // of this gate; unrelated Playwright jobs on the machine are outside that group and remain untouched.
  const child = spawn(cmd, args, { stdio: ['ignore', 'pipe', 'pipe'], detached: true });
  let tail = [];
  const keep = (buf) => { tail = tail.concat(buf.toString().split('\n')).slice(-30); };
  child.stdout.on('data', keep); child.stderr.on('data', keep);
  child.on('close', (code) => {
    try { process.kill(-child.pid, 'SIGTERM'); } catch (err) {
      if (err.code !== 'ESRCH') tail.push(`runner cleanup warning: ${err.message}`);
    }
    resolve({ code, secs: ((Date.now()-t0)/1000).toFixed(0), tail });
  });
});

const results = [];
for (const g of GATES) {
  process.stdout.write(`▶ ${g.name} … `);
  const r = await run(g.cmd[0], g.cmd[1]);
  results.push({ name: g.name, ...r });
  console.log(r.code === 0 ? `PASS (${r.secs}s)` : `FAIL (${r.secs}s)`);
  if (r.code !== 0) { console.log('  ── last output ──'); r.tail.forEach(l=>console.log('  ' + l)); }
}

console.log('\n════════ GATES SUMMARY ════════');
let failed = 0;
for (const r of results) {
  console.log(`${r.code === 0 ? '✓' : '✗'} ${r.name.padEnd(22)} ${r.code === 0 ? 'PASS' : 'FAIL'} ${r.secs}s`);
  if (r.code !== 0) failed++;
}
console.log(failed === 0 ? '\nALL GATES GREEN' : `\n${failed} GATE(S) FAILED`);
process.exit(failed === 0 ? 0 : 1);
