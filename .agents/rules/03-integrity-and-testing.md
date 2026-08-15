# Integrity, Testing & Evidence-First Verification

## 1. Evidence Before Claims (Global Rule)
- **Never claim a feature is working without proof**: Code compilation or exit code 0 is necessary but not sufficient.
- **Visual Verification**: Every scene change must be visually reviewed via generated screenshots (milestone walk captures, house proofs, or story tour frames). Check explicitly for lighting blowouts, clipping meshes, floating geometry, or occlusion anomalies.
- **Physical & Collision Verification**: Every narrative barrier (estate gate, locked door, boundary wall) must have corresponding physical colliders tested against lateral sweeps and push attempts.

## 2. Automated Pipeline Workflow
1. **Edit C# Source**: Always modify files in `unity/scenes/` or `unity/project/Assets/`, never raw scene YAML.
2. **Synchronize**: Run `npm run unity:scene:sync` to copy authored C# into `unity-project/`.
3. **Rebuild Scene**: Run `node scripts/unity-cli.mjs rebuild <scene>` in batchmode.
4. **Compile Player**: Run `npm run unity:build:mac` to produce the standalone binary.
5. **Walk Proof**: Run `npm run unity:proof:walk` to verify full 435m route traversal (0 stalls, p95 < 17ms).
6. **House Proof**: Run `node scripts/unity-cli.mjs house-proof` to verify interior rooms, HUD prompts, and card games.
7. **Portable Suite**: Run `npm run test:all` (must pass 100% across all 105 tests).

## 3. Expert Review Panel Milestone Gate
- At the conclusion of each major phase block (e.g. Phases 0–4, Phases 5–6, Phases 7–8), **The Panel** (9 multi-disciplinary specialists: Tech Art, Gameplay Engineering, Foley Audio, Gothic Narrative, Hardcore Horror, Competitive Tabletop, Casual Explorer, Adversarial QA, Editorial Critic) must convene, issue scorecards across the 6 core pillars, and output actionable critiques.

## 4. Human Gates
- Taste verdicts, audio loudness balance, fear pacing, and live gamepad feel are strictly owned by Nick. Automated agents must never impersonate or bypass human gates.
