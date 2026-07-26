# Claude adversarial review: Phase 0 B+ to A-candidate delta

Review only. Do not change the scene, select a guarded candidate, buy anything, open the mansion
doors, add a second mansion, commit or push. Threshold Refusal must remain closed-door. Court stays
locked until Nick approves Phase 0.

## Why this review exists

Claude correctly cut the previous Codex A- claim to B+ after finding four Reed Terrain prefabs that
the built player rejected. Codex then fixed the immediate defect. This pass goes further: it is meant
to prove the defect class became reusable project policy, settle the objective darkness failure in
the chapel frame, make the arrival car readable, add player display calibration, and remove proof UI
contamination. Do not inherit Codex's new A-candidate grade.

Current estate/material/Terrain fingerprint: `2c9c97b0db8dc80b`. The shared composition and guarded-
variant layer intentionally uses its own fingerprint, `460221080faabbff`; do not treat two named
fingerprint algorithms as drift.

## Commands

Run from `/Users/damato/Projects/the-games-master` with Unity closed:

```bash
npm run unity:scene:check
npm run test:all
node scripts/unity-cli.mjs audit-saved
node scripts/unity-cli.mjs test
node scripts/unity-cli.mjs playtest
node scripts/unity-cli.mjs report
node scripts/unity-cli.mjs pacing
node scripts/unity-cli.mjs variants
node scripts/unity-cli.mjs tour
npm run unity:build:mac
npm run unity:proof:mac
npm run gates
```

Inspect all 18 files in `/Users/damato/GamesMaster-Unity/Screens/WendHill/`, all nine PNGs and
`player.log` under `Library/GmSceneIntelligence/standalone-proof/`, and the files named below.

## Claims to confirm, partially confirm or refute

1. `GmTerrainPrototypeAudit` is shared scene-system code, not another Wend Hill-only check. It
   rejects child-only meshes, accepts a valid root LODGroup, checks real mesh data and verifies a
   caller-selected minimum population.
2. Wend Hill's audit and test now call that shared module; all six populated Terrain variants pass.
3. `GmRuntimeIntegrityPolicy.cs` and `scripts/unity-runtime-integrity.mjs` catch the original Reed
   warning plus unsupported/missing/internal-error shaders, while the tests prove shutdown chatter is
   not mislabeled.
4. The final player log contains no render-integrity defect and no UnityEditor assembly ships.
5. The chapel did not get a global exposure lift. Fixed EV remains -2.55 and authored post exposure
   remains 0. The retained cold fill is bounded and classified as `CompositionFill`.
6. The chapel tour gate is meaningful. Two candidates really failed at mean 8.9/9.0 and less than
   24% readable pixels; the retained shot passes the >=9.5 mean and >=24% pixels-at-10 floor.
7. The final chapel reads as modest three-quarter architecture, not a black wedge and not an occupied
   second mansion.
8. The SUV's body is now readable oxblood with restrained reflectance; lights remain off. Decide
   whether it looks deliberately ordinary or merely purple/modern and out of place.
9. `GmDisplayCalibration` persists five levels from -0.5 to +0.5 stops, clamps correctly, changes
   through Left/Right Arrow and D-pad, and leaves level 0 as the asset-authored grade.
10. Automated review forces level 0 without overwriting the user's preference. Check for profile
    mutation, editor-asset dirtiness and PlayerPrefs leakage.
11. The 720p pause frame is legible and exposes calibration plus resume/quit controls without
    crowding. Virtual-controller proof actually changes brightness and returns to level 0.
12. Native composition frames 02-07 contain no controller-test interaction text, triggered story
    card, controls legend or wind-review toast. Dedicated UI frames still prove cold-open and pause.
13. Current results are 103/103 EditMode, 5/5 PlayMode, 18/18 tour, 7/7 clean native composition,
    2/2 controller UI, 5/5 audio decode and zero runtime/render-integrity errors.
14. The new objective review session is automation evidence only. It does not impersonate Nick,
    resolve the five open defects, approve a rule or create a visual baseline.
15. No purchased source asset was modified; no temporary script/material, second mansion or opened
    threshold survived.

## Attack these weak points

- Can a malformed root LODGroup still false-negative the Terrain audit?
- Can the Node and C# warning policies drift or flag harmless finalization noise?
- Does calibration instantiate a runtime profile safely, or accidentally persist into the authored
  asset? Does automated proof alter a real player's saved setting?
- Is the chapel still unusably dark on the native player, or did the fill make it implausibly lit?
- Does the SUV improvement read as intentional bodywork or as a muddy purple blob?
- Are any native scene frames still contaminated by proof-only UI?
- Did the chapel fill move anything beyond its own light transform in the fingerprint?
- Did the new tests merely assert hierarchy, or do native frames and logs prove the behavior?

Give a harsh letter grade. A is allowed only if the former must-fix class is prevented, the player
evidence is clean, and the chapel/car/display changes hold visually. A+ still requires Nick's actual
display, ears, physical controller and uninterrupted 4m45 walk. Report every false claim with an
exact file, line, frame, object or log message. Do not implement fixes during review.
