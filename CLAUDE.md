# The Games Master — project rules for Claude

## Pinned environment (verify after every Unity upgrade)

- Unity **6000.5.3f1**, **HDRP**, Linear color space
- Input: **new Input System** (keyboard/mouse + full gamepad surface)
- Review platform: **macOS** standalone build (unsigned local app)
- Layout: authored source lives in this repo (`unity/` for scene system + C#);
  it syncs into the local Unity project at `~/GamesMaster-Unity`
  (`npm run unity:scene:sync`; `npm run unity:scene:check` detects drift).
  **Edit the repo copy, never the synced copy.**
- Unity must be **closed** for every `unity-cli.mjs` / batchmode run.
  Never launch a second editor against the project.
- Scripting backend / API level: verify in `ProjectSettings/` before relying on it.
- HDRP rule: built-in Standard materials render magenta — never acceptable,
  even as temporary blockout. HDRP/Lit from the first cube.

## What this game is

A first-person psychological-horror card game (Inscryption-adjacent). Seven games,
one host — Aldric Voss, the Games Master, a cheat who wants to lose. Catching
cheats, not winning tricks, is the real game. Current production target:
`WendHill_Prologue` (Unity). The Three.js `.dc.html` pages are archived prototypes,
not the authoritative opening.

## Ownership model: B — deterministic scene builders

The C# builder is the authored source; the generated `.unity` file is build output.
**Never hand-edit `.unity` or `.prefab` YAML.** The scene factory contract lives in
`docs/UNITY-SCENE-WORKFLOW.md`: registry (`unity/scene-system/scene-registry.json`),
scene identity, double-rebuild fingerprint, composition plans, per-scene tours.
Automated changes to serialized assets go through Editor scripts using the Unity API.

## Commands

```bash
npm run gates            # THE command: all opening gates, pass/fail table
npm test                 # unit + in-page harness
npm run test:fast        # editor-free fast suite (hook / quick check)
npm run playtest:agent   # walkthrough rig; findings → docs/playtest/agent-report.json
node scripts/unity-cli.mjs test|playtest|tour <scene>|audit <scene>|rebuild <scene>
npm run unity:build:mac && npm run unity:proof:mac   # native build + player proof
```

## Verification and evidence

- After any change: `npm run test:fast`. Before reporting content work done:
  `npm run gates`. New content must be covered by the walkthrough rig (tagged
  `userData.gmKind` / expected-height table / reachable POI radius) before it is
  reported as added — see `docs/TESTING.md`.
- Automated green ≠ done. Test results and visual verdict are reported separately;
  screenshots are inspected at full size and classified PASS / BORDERLINE / FAIL.
  Reject black/white/tiny frames by percentile pixel stats — measure, don't eyeball.
- A run that passed only on retry is FLAKY, not green. Preserve the first failing
  log. Never convert an unexpected Unity exit or zero-test XML into a pass.
- Never inherit a grade — not Codex's, not a prior Claude session's. Re-verify
  claims against evidence (CONFIRMED / PARTIAL / FALSE, then a letter grade).
- Virtual-gamepad proof is proof of bindings and sequence, never of feel. Say which
  you have.

## The fix loop

Findings rank HIGH / MED / LOW. Fix HIGH, re-run, fix MED, re-run; LOW is judgment.
Done = two consecutive clean runs. Objective defects only: part-buried scatter and
intentional darkness are art, not bugs. Purchases, canon changes, and taste calls
leave the loop and go to Nick's queue with evidence attached.

## Learning

- Fix playbook + harness lessons: `docs/TESTING.md` (defect class → root cause →
  proven fix; the six hard-won harness rules). Consult BEFORE debugging — most
  flakes are the instrument, not the game. Append every newly beaten defect class.
- Scene Intelligence may record observations, but rule promotion, defect
  resolution, and baseline approval require Nick's explicit confirmation.
  Never fabricate a human verdict. Automated runs never overwrite player-set
  calibration (display level, sensitivity).

## Hard locks

- **No push. No commit unless Nick explicitly authorizes it.**
- No asset or tool purchases without asking first.
- No second mansion. The coaching-inn history is canon — never call it a saloon.
- Threshold Refusal stays closed-door: drive → gate lock → porch → doors remain
  shut → porch dark/KO → Entry Hall.
- Never edit or delete unrelated dirty worktree files.
- Court work is gated behind Nick's Phase 0 walk.

## Human gates (Nick-owned; list them open in every report)

Physical-controller feel, display brightness, audio/wind character and loudness
balance, figure subtlety, pacing, fear. The Phase 0 live walk is the deciding gate.
Never claim "perfect," "done," or "ready for Nick" from automated tests alone.

## Feel boundary

Every constant affecting game feel lives in a ScriptableObject config with [Range]
and [Tooltip], never inline in a .cs file. Never claim something "feels better."

## Read first

`README.md` → `docs/TESTING.md` → `docs/UNITY-SCENE-WORKFLOW.md` →
`docs/STEAM-TRACKER.md` (current delivery truth) → `docs/PROGRESS.md` →
`docs/NICK-NEEDED.md`. Check-in format: refresh PROGRESS.md, show all phase bars,
state completed work, next blocker, whether Nick action is required, and report
test results and visual verdict separately.
