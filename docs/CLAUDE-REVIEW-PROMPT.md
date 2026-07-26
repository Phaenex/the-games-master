# Claude Review Prompt — Codex Phase 0 Unity Work

> Paste everything below the line into Claude. This is a review-only first pass. Claude must produce
> evidence and a fix queue before touching game code. The author being reviewed is Codex.

---

You are the independent, adversarial reviewer for **The Games Master** Unity opening. Codex performed
a final asset, placement, lighting, audio, rare-event, and verification pass and graded its own result
**B+ / ready for Nick review**. Do not inherit that grade. Assume the author is biased, rerun every
important command yourself, inspect the images yourself, and try to disprove its claims.

This is not a courtesy review. Nick wants an A+ candidate. Find every thing that still reads cheap,
procedural, repeated, empty, overlit, underlit, misplaced, tonally wrong, or test-inflated.

## Projects and immutable rules

- Unity 6000.5.3f1 HDRP project: `/Users/damato/GamesMaster-Unity`
- Web/docs/test repository: `/Users/damato/Projects/the-games-master`
- Unity scene: `Assets/Scenes/WendHill.unity`
- Builder: `Assets/Editor/GmEstateBuilderV2.cs`
- Current self-review, which is evidence to challenge rather than truth:
  `docs/playtest/codex-phase0-final-pass-2026-07-17.md`

Hard locks:

- Review first. **Do not edit game code until the written audit and ranked fix queue exist.**
- No push.
- No commit unless Nick explicitly authorizes it.
- No purchases without asking Nick.
- Exactly one mansion. No hut/house/demo-scene substitute as a second mansion.
- Threshold Refusal remains closed-door. The outer front doors never open.
- The retired three-toll chapel knock-back must not return; the ninth bell owns the crossing.
- Do not mass-convert or dump whole purchased scenes into Wend Hill.
- Do not award A+ from green tests. This is an art/feel review with tests as a floor.

## Read before running anything

Read completely:

1. `docs/CODEX-HANDOFF.md`
2. `docs/STEAM-TRACKER.md`
3. `docs/NICK-NEEDED.md`
4. `docs/playtest/codex-phase0-final-audit-2026-07-17.md`
5. `docs/playtest/codex-phase0-final-pass-2026-07-17.md`
6. `docs/playtest/codex-to-claude-review-handoff-2026-07-18.md`
7. `docs/playtest/unity-opening-asset-palette.md`
8. `docs/superpowers/specs/2026-07-14-opening-threshold.md`
9. `docs/superpowers/plans/2026-07-17-the-ninth-bell.md`

## Drive Unity yourself

Unity is single-instance. Do not run two Unity commands together. Before starting, confirm the project
is not already open:

```bash
ps -Ao pid,args | grep 'Unity.app/Contents/MacOS/Unity' | grep GamesMaster-Unity | grep -v grep
```

From `/Users/damato/Projects/the-games-master`, run in this order:

```bash
node scripts/unity-cli.mjs rebuild
node scripts/unity-cli.mjs audit
node scripts/unity-cli.mjs test
node scripts/unity-cli.mjs playtest
node scripts/unity-cli.mjs tour
```

`tour` opens a real Unity window because HDRP renders white in batch mode on this machine. It should
capture 16 PNG files and exit cleanly. A modal dialog, partial capture, stalled editor, or watchdog
termination is a finding even if a retry works.

Run `audit` a second time after the tour and compare the two count/fingerprint lines. Codex claims the
final scene reproduces as:

```text
objects=3968 renderers=2949 colliders=496 lights=9 fingerprint=046d838dfbce58dd
```

If any value differs, investigate before accepting “deterministic.” Do not excuse the difference as
randomness; the builder is supposed to seed its placement.

## Open every visual artifact

The files are under `/Users/damato/GamesMaster-Unity/Screens/WendHill/`:

1. `tour-01-spawn.png`
2. `tour-02-car.png`
3. `tour-03-gate.png`
4. `tour-04-lookback.png`
5. `tour-05-middrive.png`
6. `tour-06-cem-path.png`
7. `tour-07-cem-inside.png`
8. `tour-08-chapel.png`
9. `tour-09-gdn-inside.png`
10. `tour-10-well-shed.png`
11. `tour-11-coach-yard.png`
12. `tour-12-porch.png`
13. `tour-13-cem-detail.png`
14. `tour-14-gdn-detail.png`
15. `tour-15-figure-far.png`
16. `tour-16-figure-gone.png`

Open all sixteen at full resolution. Also create and inspect a 4×4 contact sheet. A mean-luminance
number is only a blank/white-frame guard, never a visual grade.

For every shot write one blunt line covering:

- focal hierarchy;
- believable scale and grounding;
- asset repetition or kit seams;
- lighting/material coherence;
- whether it looks like a commercial horror-game opening rather than an assembled prototype.

## Inspect the actual implementation

Read, do not skim:

- `Assets/Editor/GmEstateBuilderV2.cs`
- `Assets/Editor/GmEstateQualityAudit.cs`
- `Assets/Editor/GmMansion.cs`
- `Assets/Scripts/GmRareEvents.cs`
- `Assets/Scripts/GmAmbience.cs`
- `Assets/Scripts/GmShotTour.cs`
- `Assets/Tests/EditMode/Editor/GmEstateBuildTests.cs`
- `Assets/Tests/PlayMode/GmPrologueRouteTests.cs`
- `scripts/unity-cli.mjs`
- `scripts/play-full.mjs`
- `scripts/verify-breath.mjs`

Look specifically for tests that merely assert names/counts while missing visual failure, source
comments that claim safety without enforcing it, in-memory materials that leak or duplicate, route
tests that teleport around rather than exercise collision, and review-only code that can affect normal
play.

## Claims to grade CONFIRMED / PARTIAL / FALSE

Give evidence for every call.

1. **Single-mansion canon:** exactly one mansion shell is present and no purchased hut/house/demo
   scene is being used as disguised additional architecture.
2. **Closed-door canon:** both outer front doors remain sealed in gameplay and under adversarial
   pressure; no visual-tour or test hook opens them.
3. **Controlled night:** Gradient Sky, ACES, fixed EV -3, and 1.7-lux moon persist; no automatic
   exposure, cobalt daylight, white clipping, or crushed black navigation frame.
4. **Ground/drive integrity:** tiling mud and road read naturally; no grass-atlas rectangles, giant
   leaf planes, obvious z-fighting, or stretched single texture.
5. **Woodland integrity:** avenue and ridge vegetation read dead/bare, with believable scale and no
   decorative collision or green/glowing clumps in the playable zones.
6. **Cemetery composition:** clear entry/cross-path/spine and focal beats exist, but determine whether
   repeated wall/cross modules still expose the kit badly enough to keep it below A.
7. **Kitchen-garden composition:** fence, work paths, scarecrow, well/shed, dead beds, and two small
   produce clusters make semantic and visual sense. Codex admits this is the weakest B-grade zone.
   Decide whether it actually reads as a neglected garden or as an empty field with four red props.
8. **Coach-yard composition:** loading/feed/repair clusters are grounded, readable, motivated, and do
   not block the turning area. Determine whether the foreground cart dominates or clips.
9. **Mansion windows:** all eleven facade glass slots resolve deterministically as 2 dark, 4 dim, and
   5 lit without material leaks, flat “birthday lighting,” or white panes.
10. **Window figure:** the one-in-three upper-window figure is subtle but legible in shot 15, gone in
    16, permanently disappears below z=18, and cannot reappear during the same walk.
11. **Lighting discipline:** local fills reveal texture without exposing invisible rigs, producing
    hard arbitrary shadows, or making outbuildings look occupied when they should feel abandoned.
12. **Audio graph:** exterior runtime uses one wind bed at no more than 0.08 with a high-pass at or
    above 120 Hz plus sparse wildlife; no `amb_dark`, deep-space, underwater, rumble, or synthetic
    drone is active. Distinguish runtime references from comments and retired web assets.
13. **Audio A/B honesty:** F8 switches filtered wind versus sparse wildlife/silence without stacking,
    volume drift, or a hidden second continuous loop. State clearly if you cannot judge the sound by
    ear; do not convert a structural pass into an audio A+.
14. **Audit legitimacy:** `GmEstateQualityAudit` would catch missing roots, a second mansion, wrong
    shaders, floating props, path intrusion, decorative colliders, and forbidden audio rather than
    merely printing a reassuring line.
15. **Route legitimacy:** the two PlayMode tests genuinely pressure exploration, side bounds, and the
    sealed threshold. Identify any important route they skip.
16. **Tour legitimacy:** all sixteen captures use the intended current scene state, figure force/gone
    hooks do not contaminate normal play, and synchronous rendering did not trade reliability for
    stale temporal effects.
17. **Web regression fixes:** the `play-full`, `verify-breath`, `verify-handoff`, and `run-gates`
    changes fixed stale choreography, headless timing, and cleanup only; they did not weaken gameplay
    assertions, move the player past a real defect, hide a failed child process, kill unrelated browser
    jobs, or make a test pass by hard-coding the answer.
18. **No cleanup fraud:** no temporary C# scripts, no mass Witch Village conversion, no ignored
    magenta assets used in the scene, no deleted user work, and no commit/push.

## Independent command checks

Run and report exact outputs:

```bash
node scripts/unity-cli.mjs test
node scripts/unity-cli.mjs playtest
npm test
node scripts/test-shutbox-csharp.mjs
node scripts/run-gates.mjs

grep -rl 'guid: 0000000000000000f000000000000000' --include='*.mat' \
  /Users/damato/GamesMaster-Unity/Assets/LeartesStudios/WitchVillage | wc -l

find /Users/damato/GamesMaster-Unity/Assets -type f \
  \( -iname '*temp*.cs' -o -iname '*tmp*.cs' -o -iname '*backup*.cs' \) -print

rg -n -i 'amb_dark|deep[_ -]?space|underwater|rumble|spaceship' \
  /Users/damato/GamesMaster-Unity/Assets/Scripts \
  /Users/damato/GamesMaster-Unity/Assets/Editor
```

`run-gates.mjs` is intentionally slow. Let it finish. Codex reports several red intermediate runs,
then a later uninterrupted 2026-07-18 invocation passed 9/9 in 67s, 306s, 72s, 402s, 49s, 63s, 72s,
55s, and 27s. Run the whole command fresh; do not accept Codex's final green output, earlier stitched
7+2 result, or targeted passes as your evidence. Pressure the longer bounds and descendant-only
cleanup for false confidence.

## Known weaknesses you must pressure

Codex admits all of these. Confirm them and find more:

- The garden may still be too sparse to deserve even B.
- Cemetery kit repetition remains visible at detail distance.
- The modern vehicle is canonically defensible but may still clash with the estate's art language.
- Exterior audio has structural evidence but no Nick ear approval.
- EV -3 has never been art-directed on Nick's display.
- The 4m45 grounds sequence may feel tense or merely delay the actual game.
- Placeholder OnGUI presentation is not ship UI.
- Static screenshots do not establish frame pacing, stutter, or Steam-target performance.

## A+ rubric

Do not give a single overall A+ unless all of the following are true:

- No visual zone grades below A.
- The money approach shot and porch close-up both look commercially credible at full resolution.
- Garden and cemetery communicate their roles without explanation from this prompt.
- Repetition, grounding, scale, material, and lighting defects are polish-level only.
- Normal play, adversarial play, and two cold rebuilds are green.
- The complete nine-gate browser runner is green in one uninterrupted invocation.
- No canon/hard-lock violation exists.
- Audio and pacing are explicitly marked **human-unverified** unless Nick has actually cleared them;
  at most call the build an “A+ visual/technical candidate” without that human evidence.

Weight the grade: visual/art direction 60%, gameplay feel and audio 20%, robustness/testing 20%.
Excellent automation cannot drag B art to A+.

## Deliverable

Write `docs/playtest/claude-phase0-independent-review-2026-07-18.md` containing:

1. One-line verdict for every one of the 16 screenshots.
2. All 18 claims marked CONFIRMED / PARTIAL / FALSE with direct evidence.
3. Exact test/audit/fingerprint outputs.
4. A critique of the tests themselves, including false confidence or missing coverage.
5. Per-zone grades: arrival, drive/woodland, mansion/porch, cemetery/chapel, garden, coach yard,
   audio, route/feel, and tooling.
6. A ranked defect list, worst first, separating BLOCKING / POLISH / NICK.
7. Concrete acceptance criteria for moving each non-A zone to A and then A+.
8. One final letter grade and a separate “ready for Nick?” yes/no.
9. Explicit confirmation that you edited no game code, committed nothing, and pushed nothing during
   this review-only pass.

Be harsh. If the scene is still B work, say B. If Codex's tests are self-congratulatory, explain
exactly how. If a claimed A-range zone does not survive the images, fail it. The purpose of this
review is to produce the next truthful fix list, not to validate Codex.
