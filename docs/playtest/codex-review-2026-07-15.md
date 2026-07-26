# Codex Cold Review — 2026-07-15

Automated opening behavior is healthy. The visual pass remains uneven: the threshold sequence is mechanically sound, but the porch/facade and some grounds silhouettes do not yet meet the intended shipped-horror bar.

| ID | Finding | Severity | Evidence | Fix owner |
|----|---------|----------|----------|-----------|
| A1 | Shut the Box logic and shared harness suites pass: 23 + 47. | WONTFIX | `npm test` | — |
| A2 | Door and full-walk harnesses reach aftermath with `errors:0`; the gate locks and the front doors remain closed. | WONTFIX | `node scripts/play-door.mjs`; `node scripts/play-full.mjs`; `door-01..06`; `full-01..18` | — |
| A3 | Opening harnesses swallow required wait failures, do not all fail on captured page errors, and contain stale open-door commentary. | POLISH | `scripts/play-door.mjs`; `scripts/play-full.mjs`; `scripts/play-gate.mjs` | Agent |
| A4 | Front-door handles read as oversized bright paddles at porch distance. | POLISH | `door-01-threshold.png`; `door-03-closed-hold.png`; `full-12-door-close.png` | Agent |
| A5 | The porch/facade reads flat and pale rather than convincing horror architecture. | POLISH | `door-01-threshold.png`; `full-12-door-close.png`; `full-13-facade-up.png` | Agent |
| A6 | Outer grounds and tree silhouettes still read visibly low-poly/kit-like. | POLISH | `full-07-left-flank.png`; `full-08-right-flank.png`; `full-14-look-back-drive.png` | Agent, owned assets first |
| A7 | A higher-fidelity Dead Tree Pack may still help after the owned-assets pass, but buying it is not an automatic fix. | BUY | `begin-outgate-prebuy.png`; `begin-outgate-trees.png`; current full-walk shots | Nick after Phase 0 walk |
| A8 | Nick's Phase 0 walk is the remaining human gate before Court. | NICK | `docs/PROGRESS.md`; `docs/NICK-NEEDED.md` | Nick |
| A9 | A second mansion, an open-door entrance, and `SM_Wall_Door` as a surround conflict with locked canon. | WONTFIX | opening-threshold spec; handoff hard rules | — |

## Gate results

- Mechanics: **PASS**
- Playable opening mood: **BORDERLINE**
- Shipped horror-art bar: **FAIL**
- Canon: **PASS** — Threshold Refusal remains closed-door through KO.

No source files were changed before this audit was written.

## Fresh takeover re-audit — purchased horror bundle

Re-run after Nick purchased the Leartes Studios Horror Environments Bundle. This table supersedes the earlier buy recommendation while preserving the original evidence trail.

| ID | Finding | Severity | Evidence | Fix owner |
|----|---------|----------|----------|-----------|
| R1 | Logic and browser harness baseline is healthy: 23 Shut the Box tests and 47 shared harness checks pass. | WONTFIX | `npm test` | — |
| R2 | Both live opening walks reach `aftermath` with `errors:0`; gate lock fires and both front-door rotations remain `0`. | WONTFIX | `node scripts/play-door.mjs`; `node scripts/play-full.mjs` | — |
| R3 | Threshold Refusal reads correctly as a closed-door knockout and the aftermath copy preserves “No doorway.” | WONTFIX | `door-03-closed-hold.png`; `door-03b-porch-dark.png`; `door-04-knock-start.png`; `door-06-aftermath.png` | — |
| R4 | Current dead trees, rocks, ruins, cemetery and garden silhouettes remain visibly low-poly and tonally flat in the live walk. | POLISH | `full-04-cemetery-pass.png`; `full-05-garden-pass.png`; `full-07-left-flank.png`; `full-08-right-flank.png`; `full-14-look-back-drive.png` | Agent, using purchased bundle candidates first |
| R5 | The gate works mechanically, but the near-field bars/piers and surrounding vegetation still read as a prototype when the player turns back. | POLISH | `full-01-spawn.png`; `full-03-gate-locked.png`; `begin-outgate-trees.png` | Agent |
| R6 | The existing mansion shell remains canon-locked, but the porch columns and oversized bright door framing do not match its material detail. Improve dressing/materials without replacing the mansion or opening the leaves. | POLISH | `door-01-threshold.png`; `door-03-closed-hold.png`; `full-09-at-porch.png`; `full-12-door-close.png` | Agent |
| R7 | The $50 bundle purchase supersedes the old Dead Tree Pack buy call. Do not buy another visual pack until its nine included environments have been mined and tested. | WONTFIX | Nick purchase; bundle package `ReadMe.txt` | — |
| R8 | **Resolved:** the nine purchased payload packages are now present in the Asset Store cache; selective extraction/conversion is the remaining agent work. | WONTFIX | `npm run assets:horror:check` → `9/9 payload packages ready` | — |
| R9 | Nick’s in-motion Phase 0 walk remains the human taste gate; automated evidence cannot close it. | NICK | `docs/PROGRESS.md`; `docs/NICK-NEEDED.md` | Nick |
| R10 | Steam packaging is a later delivery decision and does not justify changing the current opening canon or replacing the mansion during Part 1. | WONTFIX | takeover plan; opening-threshold spec | — |

### Re-audit verdict

- Runtime/mechanics: **PASS**
- Threshold Refusal canon: **PASS**
- Current opening mood: **BORDERLINE**
- Current shipped-art bar: **FAIL**
- Asset value at $50: **PASS / excellent**, pending selective integration and performance testing

## Payload-ready continuation audit

Re-run after Nick confirmed the purchased assets were downloaded. This is the no-code audit gate immediately before selective bundle extraction and integration.

| ID | Finding | Severity | Evidence | Fix owner |
|----|---------|----------|----------|-----------|
| P1 | All nine Leartes environment payloads are now present in Unity's Asset Store cache (roughly 30 GB total). The entitlement-only blocker is closed. | WONTFIX | `npm run assets:horror:check` → `9/9 payload packages ready` | — |
| P2 | Baseline remains healthy: 23 Shut the Box tests and 47 shared harness checks pass. | WONTFIX | `npm test` | — |
| P3 | Door and full live walks reach `aftermath` with `errors:0`; gate lock fires, 40 owned trees load, and both front-door rotations remain exactly `0`. | WONTFIX | `node scripts/play-door.mjs`; `node scripts/play-full.mjs` | — |
| P4 | The approach's near and mid-ground vegetation still reads as oversized flat cutouts/low-poly blocks; cemetery and garden stone also reads as pale primitive geometry. This fails the shipped-art bar and is now fixable from already-purchased assets. | BLOCKING | `full-04-cemetery-pass.png`; `full-06-mid-drive.png`; `full-07-left-flank.png`; `full-08-right-flank.png`; `full-14-look-back-drive.png` | Agent — mine bundle selectively |
| P5 | The locked gate works, but the car becomes a pale opaque silhouette and the bars/piers/vegetation flatten together during the required look-back. | POLISH | `full-03-gate-locked.png`; `begin-outgate-prebuy.png`; `env-outgate-01-lookback.png` | Agent |
| P6 | At porch distance, bright rectangular door trim, pale column caps/rails, and oversized lamp silhouettes look procedural against the weathered facade. Preserve the closed leaves and mansion shell; redress/material-match only. | POLISH | `door-01-threshold.png`; `door-03-closed-hold.png`; `door-03b-porch-dark.png`; `env-door-01-closed.png` | Agent |
| P7 | The invitation prompt and large narrative copy persist through much of the visual walk and obscure scene evaluation, but they do not break play or canon. | POLISH | `full-03-gate-locked.png`; `full-06-mid-drive.png`; `full-09-at-porch.png` | Agent, after environment cutover |
| P8 | No additional asset purchase is justified. Mine Haunted Village, Abandoned Village, Witch Village, Sorcerer's Hut and Mansion Interior first; use only web-sized selected models and textures. | WONTFIX | `npm run assets:horror:check`; handoff no-buy/no-second-mansion locks | — |
| P9 | Nick's in-motion Phase 0 walk remains the human taste gate. The asset integration pass may proceed, but Court cannot be declared complete without Nick's walk or explicit waiver. | NICK | `docs/PROGRESS.md`; `docs/NICK-NEEDED.md`; takeover Part 1 gate | Nick |
| P10 | A second mansion, open front doors, and full Unity demo-scene imports remain canon/performance rejects. | WONTFIX | opening-threshold spec; handoff hard rules | — |

### Payload-ready verdict

- Asset intake: **PASS — 9/9 ready**
- Runtime/mechanics: **PASS**
- Threshold Refusal canon: **PASS**
- Playable opening mood: **BORDERLINE**
- Shipped horror-art bar: **FAIL**
- Next agent-owned work: inventory the purchased packs, select web-suitable vegetation/stone/porch candidates, then replace the most visible opening leftovers in small visually gated batches.
