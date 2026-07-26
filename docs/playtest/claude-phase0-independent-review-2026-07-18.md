# Claude Independent Adversarial Review — Codex Phase 0 (Wend Hill Unity Opening) — 2026-07-18

**Reviewer:** Claude (Opus 4.8), adversarial second pass. I did not inherit Codex's B+.
**Method:** independent cold rebuild + headless audit/test/playtest, full-resolution inspection of all
16 tour shots (plus a 4× magnified/brightened figure crop and a 4×4 contact sheet I generated), a
line-by-line read of the builder/audit/mansion/rare-events/tour/ambience code and both Unity test
files and the three web gate scripts, the prompt's grep checks, an independent windowed tour
re-capture, a second audit for the fingerprint comparison, and the full nine-gate browser runner.

## 0. One-line verdict

**B+ product / A- tooling. NOT an A+ candidate, and not "ready for Nick to sign off Phase 0" — it is
ready for Nick's *judgment walk*, which is a different thing.** The automation is genuinely
A-grade and, unusually, honest about its own limits. But it is a *structural floor* that by
construction cannot see the three defects that actually cap the grade: a garden that reads as an
empty field, a cemetery whose 3-cross/1-wall kit repeats visibly, and a "window figure" so subtle it
is invisible even at 4× magnification with the exposure lifted. Codex's own B+ is defensible; my job
was to find whether the A-range zones survive the images, and two of the six do not.

---

## 1. Screenshot verdicts (one blunt line each)

Shots graded against the current deterministic scene (my cold rebuild reproduced fingerprint
`046d838dfbce58dd` exactly, so these describe the shipped layout, not a stale capture). A 4×4 contact
sheet is preserved at `docs/playtest/claude-review-contact-sheet-2026-07-18.png`; the 16 full-res
frames are in `~/GamesMaster-Unity/Screens/WendHill/`.

| # | Shot | Blunt verdict |
|---|---|---|
| 01 | spawn | Strong amber focal house through the open gate, but the scattered picket segments left/right and the pale toy-ish chapel box read as dropped kit; **B**. |
| 02 | car | Modern sedan intentionally tinted near-black so it recedes — combined with EV-3 it's *too* dark to read as the interactive POI, and the frosted/speckled tree bark reads as texture noise; **B-**. |
| 03 | gate | The money approach: gate-leaf framing + amber house + fog at the base is genuinely commercial-credible; foreground crushes toward black; **A-**. |
| 04 | lookback | Weakest of the arrival set — no focal subject, and it exposes the repeated pale bare-tree instances the most; **B-**. |
| 05 | middrive | Clean symmetric avenue to a readable facade (mansard, cupola, varied panes); a couple of mirrored tree instances; **A-/B+**. |
| 06 | cem-path | Navigable cemetery with a real chapel focal beat, but the 3 cross models + 1 repeated wall module are obvious; **B**. |
| 07 | cem-inside | The worst frame: repeated pale rectangular rubble grave-plots read as a procedural grid, not graves; **C+/B-**. |
| 08 | chapel | A flat plank wall fills ~90% of frame — proves the material is textured, says nothing about composition; low-value **C+**. |
| 09 | gdn-inside | Reads as an empty field with a shed, a well, an unreadable pale orb (the "scarecrow"), and a few red dots, not a kitchen garden; the amber shed interior is the one good element; **B-/C+**. |
| 10 | well-shed | Same sparse read; the mossy well is the strongest asset; green moss is borderline-saturated at EV-3; **B-**. |
| 11 | coach-yard | The best single frame: warm lantern-lit bay + grounded cart + receding barn has real focal hierarchy and warm/cool contrast; commercially credible; **A-/A**. |
| 12 | porch | The hero shot lands: real Second-Empire facade, varied panes, inviting-yet-ominous porch glow, dead-tree framing; facade texture is slightly flat/grey; **A-**. |
| 13 | cem-detail | Confirms the kit repetition up close; the bright white rubble plots pull focus oddly; lower third crushed; **B-**. |
| 14 | gdn-detail | The "scarecrow" is a featureless pale sphere on a pole — a readability failure; huge hard tree-shadow; **C+/B-**. |
| 15 | figure-far | Strong avenue composition; **the forced window figure is NOT legible** at this framing — see §2 claim 10; **A- as a shot, figure fails**. |
| 16 | figure-gone | Same strong avenue closer in; figure correctly absent (can't distinguish it from 15 by eye either way); **A-**. |

**Cross-shot tells:** (a) the picket-fence lines (drive-edge boundary at x=±6.8 with intentional
missing panels) read as repetitive kit across the wide frames; (b) several frames (02, 13, 14) crush
their lower halves to near-black — atmospheric but a real navigation-darkness concern for the EV-3
gate; (c) the pale bare-tree bark is bright and repeats across the flanks.

---

## 2. The 18 claims — CONFIRMED / PARTIAL / FALSE (with evidence)

1. **Single-mansion canon — CONFIRMED.** `GmEstateBuilderV2.Build` calls `GmMansion.Build` exactly
   once; `BuildPlacements` instantiates individual whitelisted design-data props (no scene dump);
   no second shell in any of the 16 shots. Caveat carried to §4: the *audit's* single-mansion guard
   (`ValidateSingleMansion`) only counts objects named `"Mansion ("`, so a differently-named second
   building would slip past the automated check even though the canon holds in fact.
2. **Closed-door canon — CONFIRMED.** `GmMansion.SealTheDoors` hides the `Cube.043` door slab
   (dot-normalized match, and the test asserts `matched>0` so it can't pass vacuously);
   `play-full.mjs` asserts door rotations `|left|,|right| ≤ 0.001`; the PlayMode adversarial test
   blocks crossing past z=-37.4 into the mansion. No tour/test hook opens the doors. Matches the
   locked Threshold-Refusal spec.
3. **Controlled night — CONFIRMED.** Builder sets GradientSky + ACES tonemapping + fixed EV -3 +
   1.7-lux moon, persisted via `AddObjectToAsset` (the memory-only-profile trap is fixed), and four
   EditMode tests verify each override *persists* and exposure is `Fixed` not automatic. No white
   clipping in any shot. **Open question (Nick):** several foregrounds crush to near-black — whether
   EV-3 is too dark to navigate is a display call only Nick can make.
4. **Ground/drive integrity — CONFIRMED.** Real tiling mud/road HDRP materials (the old grass-atlas
   billboard is explicitly replaced), tiling 40×40 / 2×16; `SM_DriedLeaves` deliberately excluded to
   kill the giant-leaf-plane bug. No atlas rectangles or stretched single texture in the shots.
5. **Woodland integrity — CONFIRMED.** `_Foliage`/green-leaf trees filtered out; only bare
   `SM_Tree_*` + dead willow used; decorative colliders stripped (190 removed on TreeLines, logged).
   No green/glowing clumps. Minor: the pale bark is brighter than ideal and instances repeat.
6. **Cemetery composition — PARTIAL (stays below A).** Real entry/cross-path/spine and a chapel focal
   beat exist, but the zone is built from only 3 cross models + 1 repeated `SM_StoneWall_01` module,
   and shots 07/13 expose that kit badly. Correctly below A.
7. **Kitchen-garden composition — PARTIAL, leaning FALSE on "reads as a garden."** By construction it
   is 4 dead-bush rows + 4 desaturated pumpkins + paths + fence + debris. It reads as a **neglected
   empty field with a few red props**, not a kitchen garden, and the scarecrow is an unreadable pale
   orb. This is a real B-/C+ zone, weaker than Codex's B.
8. **Coach-yard composition — CONFIRMED.** Three motivated clusters (loading/feed/repair), wheel
   ruts, a 7-lumen motivated lantern, clear turning area; shot 11 is the best frame in the set and the
   foreground cart is grounded, not clipping. Genuinely A-.
9. **Mansion windows — CONFIRMED.** `VaryTheWindows` classifies the `Glass` material slots by stable
   hash into dark/dim/lit; with 11 slots the math is exactly `max(1,round(1.65))=2` dark,
   `round(3.85)=4` dim, 5 lit — matching the claimed 2/4/5. Materials are real external `.mat` assets
   (the embedded-material-doesn't-persist bug is fixed); no white panes, not "birthday lighting."
10. **Window figure — PARTIAL.** Mechanism is solid: `GmRareEvents` arms one-in-three (or forced for
    the tour), latches gone permanently below z=18, cannot reappear; the figure rig is a
    quad-glow+capsule+sphere with colliders destroyed (test asserts 0). **But "subtle but legible in
    shot 15" is generous** — the silhouette material is near-black (0.001) with a very dim warm glow,
    and I could not resolve the figure in shot 15 even at 4× magnification with +3× brightness, nor
    distinguish it from shot 16. As a horror beat it is currently below the threshold of perception.
    Also: the figure's presence in the 14 *non-forced* shots rides an unseeded `Random.value < 1/3`,
    so those frames are not bit-reproducible in that one respect.
11. **Lighting discipline — CONFIRMED (mostly).** Local fills are low, cold, shadow-free point lights
    placed from rendered bounds; the coach lantern is 7 lumen, garden fill 8, sconces 50, all
    lux-scaled to the 1.7 moon. No exposed rigs. Minor: the big bare-tree shadows in 09/14 are hard
    and long enough to read as slightly arbitrary — a polish note, not a rig leak.
12. **Audio graph — CONFIRMED (structural).** One looping bed only: `amb_wind` at 0.08 with a 145 Hz
    high-pass; crickets/owl are intermittent one-shots, not loops; Perlin drift on gain/pitch. Grep
    confirms `amb_dark`/`deep-space`/`underwater`/`rumble`/`spaceship` appear **only in comments**
    (plus one intentional `underwater` low-pass in `GmCrossing` for the bell-crossing beat, not the
    exterior bed). No synthetic drone active.
13. **Audio A/B honesty — CONFIRMED (structural), sound HUMAN-UNVERIFIED.** F8 (`#if UNITY_EDITOR`
    only) mutes/unmutes the single wind bed; it never adds a second continuous loop; wildlife one-
    shots are unaffected. No stacking. **I cannot judge the sound by ear** — and several bell-sequence
    clips are synthesized placeholders per the ninth-bell plan. This stays a Nick ear gate.
14. **Audit legitimacy — CONFIRMED with one real gap.** `GmEstateQualityAudit` genuinely enforces:
    required roots, built-in-shader/magenta materials, decorative-collider bans, grounding bounds,
    path-intrusion sampling along 3 authored segments, a garden-bed renderer floor, and a structural
    audio check. It is not a reassuring print. **Gaps:** (a) the single-mansion check is name-scoped
    (see claim 1); (b) `ValidateGardenBeds` enforces `≥18 renderers` — a count that passes while the
    garden still reads sparse (the exact "asserts counts, misses visual failure" trap).
15. **Route legitimacy — PARTIAL.** The two PlayMode tests use real `CharacterController.Move` (not
    teleport) with a stall detector; the exploration route reaches every zone and the adversarial test
    pressures the east/west drive bounds + the sealed threshold. **Skipped routes:** lateral escape at
    the cemetery/garden/coach-yard *outer* perimeters (bounds are generated there by `BuildWalkBounds`,
    but no test pressures them), and the gate-lock backward re-crossing. Coverage is narrower than the
    "genuinely pressure exploration and side bounds" framing implies.
16. **Tour legitimacy — CONFIRMED (captures are honest) but tour is FLAKY.** Captures via an owned
    RenderTexture (not the absent backbuffer); opens WendHill.unity fresh (never a restored scene);
    `runOnPlay` defaults off and arming is `#if UNITY_EDITOR`-only, so the figure force-hooks and the
    tour cannot run in a player build — no play contamination. When it runs, the output is legitimate
    and reproducible (my retry matched Codex's luminance 15/16 and pixel-matched the baseline).
    **But my first independent run STALLED and captured nothing** (watchdog @300s; §3). It passed on
    retry, but per the prompt a stalled/watchdog-terminated tour is a finding regardless. Additional
    caveats: the tour **teleports** between waypoints so it proves nothing about collision (that's the
    PlayMode tests' job), the 10 synchronous `cam.Render()` calls are a heuristic temporal settle
    rather than true frame pacing, and the unseeded figure roll (claim 10) means shots 1–14 aren't
    bit-reproducible. Net: the capture path is sound; the windowed-open reliability is not, and it
    needs an operator to notice-and-retry.
17. **Web regression fixes — CONFIRMED.** `play-full.mjs` drives real controller key state (fixing the
    teleport/clamp-through-locked-gate) and keeps the strict, unweakened canon assertions
    (`phase==='aftermath'`, `errors===0`, doors `≤0.001`). `run-gates.mjs` cleanup is **strictly
    descendant-scoped** — each gate is `spawn(..., {detached:true})` (own process-group leader) and
    `process.kill(-child.pid)` signals only that group, so a separate Playwright job (different pgid)
    is untouched; the manual-TERM incident was operator error, not the runner. **Honest caveat:**
    `play-full`/`verify-breath` fast-forward the internal `arrHold` timer to bound an otherwise-
    unbounded software-render wait — they prove the *sequence/latch*, not the real-time *duration*,
    which is explicitly delegated to `play-door.mjs`. Disclosed in comments; not outcome-faking.
18. **No cleanup fraud — CONFIRMED.** Zero temp/tmp/backup `.cs` under Assets; 141 WitchVillage
    built-in materials remain (pack not mass-converted) and the shader test proves none are used in
    the scene; no ignored magenta assets in-scene; no deleted user work; no commit/push (see §9).

**Tally: 12 CONFIRMED · 6 PARTIAL · 0 FALSE.** The PARTIALs (6 cemetery, 7 garden, 10 figure, 14
audit-gap, 15 route-gap) are where the grade is actually decided, and claim 7 is close to FALSE on
"reads as a garden."

---

## 3. Exact test / audit / fingerprint outputs

**Cold headless rebuild → audit → test → playtest (my run, 2026-07-18 01:44–01:46):**

```
[GmEstateAudit] PASS: objects=3968 renderers=2949 colliders=496 lights=9 fingerprint=046d838dfbce58dd
Unity tests (EditMode): 41/41 passed, 0 failed
Unity tests (PlayMode):  2/2 passed, 0 failed
[GmV2] TreeLines: removed 190 decorative collider(s)
[GmV2] FlankGroundcover: removed 59 decorative collider(s)   (= 249 total, matches Codex)
```

The fingerprint **matches Codex's claimed `046d838dfbce58dd` exactly** on an independent cold
rebuild — determinism is real (`rng = new System.Random(7)`), not asserted.

**Grep checks:**
```
WitchVillage built-in (magenta) materials on disk:  141   (pack not mass-converted)
temp/tmp/backup .cs under Assets:                     0
forbidden audio names in Scripts/Editor:  comments only (+ 1 intentional GmCrossing underwater LPF)
```

**Second audit (post-tour, fingerprint comparison):**
```
[GmEstateAudit] PASS: objects=3968 renderers=2949 colliders=496 lights=9 fingerprint=046d838dfbce58dd
```
Identical to the pre-tour audit and to Codex's claim — **three deterministic runs, two of them after
the tour ran**. Determinism is real, and the tour did not corrupt the scene.

**Windowed tour re-capture (16 shots, modal/stall watch) — STALLED ONCE, PASSED ON RETRY:**
- **Attempt 1 FAILED:** the windowed editor booted through licensing, the log then froze, and the CLI
  idle watchdog killed it at 300s ("parked on a modal dialog") having captured **0 shots**. The
  headless warm pass had exited clean, no Unity process lingered, and the log showed no shader-compile
  or play-mode entry — so the editor stalled during project load / windowed open, either on a modal or
  on a step exceeding the 300s guard. **Per the prompt this is a finding even though the retry worked.**
- **Attempt 2 PASSED:** 16/16 PNGs, `TOUR COMPLETE`, clean editor exit. Per-shot meanLum
  `17,22,14,25,19,25,18,24,22,22,26,24,21,17,18,16` matches Codex's reported sequence on 15 of 16
  (shot 04: 25 vs 24 — a 1-unit temporal-render delta, consistent with the ±1 nondeterminism noted in
  claim 16).
- **Cross-check:** my fresh captures pixel-diff against the baseline I graded at mean-abs-diff
  0.07–0.32 per channel (noise-level) — so my visual verdicts apply to this independent capture.
  Shot 15's 0.07 diff confirms the forced figure renders identically in both and is imperceptible in
  both.

**Browser suite (my independent runs):**
- `npm test` — **47/47 harness passed, 0 failed.**
- `node scripts/test-shutbox-csharp.mjs` — **23/23 C# parity passed, 0 failed.**
- `node scripts/run-gates.mjs` — **9/9 GREEN in one uninterrupted invocation** (my times, ~18 min):
  ```
  unit+harness 157s · agent playtest 230s · door 63s · full walk 385s · env entrance 43s ·
  porch breath 52s · g2 bell+lamp 66s · hall handoff 62s · polish 21s   → ALL GATES GREEN
  ```
  Times differ from Codex's reported run (different machine load) but every gate passes. The
  descendant-scoped cleanup did not kill any unrelated job during my run. This is a genuine clean
  nine-gate baseline, not a stitched 7+2.

---

## 4. Critique of the tests themselves

The EditMode suite is, unusually, **not self-congratulatory** — nearly every test cites the real
shipped bug it catches, and several explicitly guard against vacuous passes (the door-slab test
asserts `matched>0`; a comment records fixing an exposure test that compared int-to-Enum and *never
matched even when Fixed was set*). The audit enforces rather than prints. This is A-grade test
engineering.

**But the whole suite shares one structural blind spot, and it is the crux of this review:** every
assertion is a **count or a name** — `≥18 bed renderers`, `cemetery childCount>10`,
`garden transforms>15`, `≥6 glass slots classified`. Those cannot see composition. So:

- The **garden passes** `KitchenGardenHasVisibleBrokenDeadBeds…` with ≥18 renderers while still
  reading as an empty field — the test proves the props *exist*, not that they *read*.
- The **cemetery passes** `childCount>10` while the 3-cross/1-wall kit repeats obviously.
- The **figure passes** `WindowFigureCanBeForced…` (it toggles and latches) while being invisible to
  a human at the tour framing.

This is not a flaw *in* the tests — they are honest about being a structural floor. It is the reason
**green tests must not be read as A art**, which is exactly why this review exists. Add to that the
two coverage gaps already noted: the audit's name-scoped single-mansion check (claim 1/14) and the
PlayMode adversarial test's untested lateral perimeters + gate-lock backtrack (claim 15).

---

## 5. Per-zone grades

| Zone | My grade | Codex | Delta | Hard read |
|---|---|---|---|---|
| Arrival / gate / drive | **B+** | A- | ↓ | Hero shots 03/05 are A-, but 01/04/02 drag it with kit clutter, dark car, repeated pale trees. |
| Drive / woodland | **B+** | — | — | Bare/dead and mood-correct; pale bark brightness + instance repetition keep it off A. |
| Mansion / porch | **A-** | A- | = | Facade + varied panes + porch glow genuinely land; texture slightly flat. |
| Cemetery / chapel | **B** | B+ | ↓ | Navigable and composed, but 3-cross/1-wall kit repeats visibly; shot 07 is C+. |
| Kitchen garden | **B-** | B | ↓ | Reads as an empty field with red props; scarecrow is an unreadable orb. |
| Coach yard | **A-** | A- | = | Best zone; shot 11 is commercially credible. |
| Exterior audio | **B+ (structural), unverified by ear** | B+ pending | = | Clean single-bed graph; sound quality is Nick's call. |
| Route / feel | **B+** | — | — | Real collision-tested happy path + threshold; narrow adversarial coverage; 4m45 pacing unjudged. |
| Test / review tooling | **A-** | A | ↓ | Reproducible fingerprint (3×), honest tests, descendant-scoped cleanup, a watchdog that honestly caught its own stall. Docked from A only for the flaky windowed tour (§3/claim 16). Still cannot lift B art to A+. |

**Weighted (visual 60 / feel+audio 20 / robustness 20):** visual averages mid-B+ (two zones below B,
two at A-, rest B/B+); feel+audio B+ but human-unverified; robustness A- (9/9 gates green + 41/41 +
2/2 + 47/47 + 23/23 + fingerprint ×3, docked only for the flaky windowed tour). **Overall B+.**

---

## 6. Ranked defect queue (worst first)

### BLOCKING for A+ (not blocking for Nick's walk)
1. **Kitchen garden reads as an empty field, not a garden** (shots 09/10/14). The dead-bush rows +
   4 pumpkins do not communicate "kitchen garden," and the scarecrow renders as a featureless pale
   orb on a pole. This is the single lowest zone and the audit's `≥18 renderers` floor actively
   masks it.
2. **Cemetery kit repetition** (shots 06/07/13). 3 cross models + 1 wall module read as a procedural
   grid at detail distance; the bright pale rubble grave-plots pull focus.
3. **Window figure is imperceptible** (shot 15). The horror beat does not land — invisible even at 4×
   magnification with lifted exposure. A beat nobody can perceive is a wasted beat.

### POLISH
4. Scattered picket-fence lines read as repetitive kit across wide frames (01/03/04).
5. Crushed-black foregrounds in 02/13/14 — atmospheric but hurt navigation legibility at EV-3.
6. Arrival car (02) tinted so dark + EV-3 it barely reads as the interactive POI.
7. Chapel "detail" shot (08) is a flat wall — a non-shot; re-frame to show the chapel as a place.
8. Pale bare-tree bark is bright and instances repeat visibly on the flanks.
9. Hard/long single-tree shadows in 09/14 read as slightly arbitrary.
10. Unseeded one-in-three figure roll makes tour shots 1–14 non-reproducible in that one respect.

### AUTOMATION GAPS (fix so the floor actually floors)
11. Audit's single-mansion check is name-scoped (`"Mansion ("`) — add a geometry/count guard so a
    differently-named second building can't slip past.
12. No adversarial-escape test at the cemetery/garden/coach-yard outer perimeters, and none for the
    gate-lock backward re-crossing.
13. **Windowed tour is flaky** — stalled on my first independent run (watchdog @300s, 0 shots),
    passed only on retry. The capture path is sound but the windowed-open reliability needs an
    operator to notice-and-retry; a CI/unattended run would report a false failure or need a retry
    loop. Consider a longer/adaptive idle guard or a headless-render path so the visual proof isn't
    one flaky window away from "no evidence."

### NICK (taste / human-only, cannot be automated)
14. Modern HD vehicle on a gothic estate — canon-defensible, art-language clash still visible.
15. EV-3 navigation darkness on Nick's actual display.
16. 4m45 nine-bell grounds duration — tense or merely slow?
17. Filtered wind vs sparse wildlife+silence (F8) — which reads less synthetic by ear.

### KNOWN / DISCLOSED (not defects to fix now)
- `OnGUI` crossing/card presentation is placeholder, not ship UI (ninth-bell plan says so).
- Bell-sequence audio is synthesized placeholder pending a sourcing pass.
- Static shots don't establish frame pacing / stutter / Steam-target performance.

---

## 7. Acceptance criteria: moving each non-A zone to A, then A+

**Kitchen garden (B- → A):**
- Replace the pale-orb scarecrow with a legible silhouette (crossbar + stuffed form + hat) that reads
  as a scarecrow in motion at EV-3, or cut it.
- Give the beds actual *garden grammar*: raised/edged bed borders, a visible row structure with
  bare-earth furrows between dead plantings, one collapsed cold-frame or trellis — so the space
  reads "someone grew food here and stopped," not "field with props."
- **→ A+:** a hero close-up (a 15th/17th tour shot) that a stranger reads as a neglected kitchen
  garden with no caption.

**Cemetery (B → A):**
- Break the kit: at least 2–3 more grave-marker variants (leaning slab, broken obelisk, chest tomb)
  and vary wall-module scale/rotation/damage so the boundary stops reading as one repeated piece.
- Tame the bright rubble grave-plots (they out-value the crosses); darken/vary them.
- **→ A+:** the detail shots (07/13) survive at full res with no visible repeat within one frame.

**Window figure (PARTIAL → lands):**
- Raise the silhouette contrast/size just enough to be perceptible-but-deniable at the shot-15
  framing; verify by a *blind* read of the shot, not by knowing where to look.
- Seed the figure roll so the deterministic tour is fully reproducible.

**Arrival (B+ → A):**
- Thin/vary the picket lines so they read as a decayed boundary, not editor breadcrumbs; lift the
  arrival car read a touch; re-frame or drop shot 08.

**Audio / pacing / EV (→ cleared):** Nick's walk + ears only. Automation has taken these as far as it
structurally can.

---

## 8. Final grade + "ready for Nick?"

- **Product: B+.** Two of six visual zones are below B (garden) or below B+ (cemetery), one hero
  beat (figure) is imperceptible, and audio/pacing/EV are human-unverified. That is not A+ under the
  prompt's own rubric (no zone below A; garden/cemetery self-explanatory; repetition polish-level
  only — none of which hold).
- **Tooling/verification: A-.** Reproducible fingerprint (×3), honest count-based tests, descendant-
  scoped cleanup, unweakened canon assertions, a watchdog that honestly caught its own stall — docked
  from A only because the windowed tour flaked on me (stalled once, needed a retry).
- **Ready for Nick? YES — for his judgment walk, which is the correct next step; NO — as a Phase 0
  sign-off.** Nick should walk it precisely to rule on the garden, cemetery, EV-3, the car, the 4m45
  pacing, and the wind A/B. Do not close Phase 0 on green automation.

**Excellent automation cannot drag B art to A+, and it hasn't tried to — Codex was honest. The gap to
A+ is art (garden + cemetery) and one imperceptible beat (figure), not tests.**

---

## 9. Review-only pass confirmation

I edited **no game code and no tooling source**, committed nothing, and pushed nothing. Verified:

- **games-master repo:** `git log` HEAD is unchanged (`eb9a01d`, the pre-session commit) — no new
  commits. My only additions are this review file (under the already-untracked `docs/playtest/`) and
  two working directories (`scratchpad-codex-baseline-shots/`, `scratchpad-review-crops/`), which I
  removed after finalizing. The 12 tracked files `git diff` reports as modified were **already
  modified before my session** (they appear in the session-start `git status`); none were touched by
  me. No `push`.
- **GamesMaster-Unity:** not a git repo, so no commit/push is even possible there. I edited zero
  `.cs`/source files. I *ran* the builder (`unity-cli.mjs rebuild`/`audit`×2) and the tour×2, which
  regenerate the deterministic scene, its window/stone/coach materials, and `Screens/WendHill/*.png`
  — that is the builder doing its intended job (fingerprint `046d838dfbce58dd` reproduced every
  time), not a source edit. No canon was weakened, no second mansion built, no door opened, no
  purchase made.
- I did **not** implement any fix from §6 — this was a review-only pass, as required.

---

## 10. Addendum — post-review fixes (authorized by Nick after the review was delivered)

The review above was written and delivered first; Nick then authorized fixing the items that do **not**
depend on his taste calls. Still no commit and no push.

### ⚠️ New baseline fingerprint

```
before:  objects=3968 renderers=2949 colliders=496 lights=9 fingerprint=046d838dfbce58dd
after:   objects=3968 renderers=2949 colliders=496 lights=9 fingerprint=4707410a08b94635
```

The figure rig's silhouette geometry changed, and the fingerprint hashes every transform's
position/rotation/scale — so a shift is expected and correct. Object/renderer/collider/light counts are
**identical**, which is the right signature for "geometry tuned, nothing added or removed." The new
value **reproduced exactly on a second cold rebuild**, so determinism is preserved. Future determinism
checks must compare against `4707410a08b94635`, not the old value.

### What was fixed

1. **Window figure now perceptible** (was defect #3, BLOCKING). Root cause was measured, not guessed:
   projecting the rig into shot 15's camera puts it dead-centre at 96 units, where the whole pane
   renders ~18×9 px and the old silhouette body was **~2 px wide** — and its glow `(0.19,0.055,0.008)`
   was visually identical to an ordinary lit pane `(0.16,0.061,0.011)`, so nothing drew the eye.
   Widened the silhouette to ~50% of the pane and made that pane clearly the brightest on the facade.
   **Verified by capture:** in the wide frame it reads as one subtly brighter window (deniable); at
   magnification there is an unmistakable standing figure. Shot 16 returns to a normal amber pane with
   no artifact, and shot 12 is figure-free.
2. **Tour is now deterministic** (was defect #10). The tour explicitly hides the figure for shots
   01–14, so those frames no longer inherit the one-in-three roll. **The gameplay roll is deliberately
   NOT seeded** — a fixed seed would make every playthrough identical and destroy the "about one walk
   in three" canon. Only the review capture needed determinism.
3. **Audit single-mansion guard hardened** (was defect #11). Added a name-independent check that counts
   prefab instances whose source asset is `haunted_victorian_house.fbx`, so a renamed second shell
   cannot slip past. Documented honest residual limit in code: it catches a second copy of *this* shell,
   not an arbitrary house prop used as disguised architecture — that stays a visual-review judgement
   rather than a fragile size heuristic that would false-positive on the chapel and coach house.
4. **PlayMode perimeter coverage added** (was defect #12). New `SideZonePerimetersContainThePlayer`
   pressures seven previously untested outer bounds (cemetery south/north/east-through-forecourt,
   garden west/south, coach yard west/north). Containment assertions only — props may legitimately stop
   a push short, and that must not fail the test. PlayMode is now **3/3**.
5. **Chapel shot 08 reframed** (was defect #7). The old waypoint stood 3.1 units off the chapel door,
   so the frame was one flat plank wall. Moved to a 15-unit three-quarter vantage sighted through the
   cemetery boundary's authored opening. The chapel now reads as a building — spire, cross, dead trees
   behind, stone wall as foreground. **C+ → B+/A-.**
6. **Flaky tour mitigated** (was defect #13). `unity-cli.mjs` now retries a GUI task once, logging the
   retry loudly so flakiness stays visible rather than silently absorbed. Batchmode tasks are
   deliberately not retried — repeating one would only hide a real compile error.

### Verification after the fixes

```
rebuild            ✓          audit          PASS (fingerprint 4707410a08b94635, reproduced 2×)
EditMode tests     41/41      PlayMode tests 3/3   (was 2/2)
tour               16/16, clean exit, no stall
```

Browser gates were **not** re-run and did not need to be: nothing in the web game or its harness was
touched. The only JS edit was `scripts/unity-cli.mjs`, which `run-gates.mjs` does not invoke.

### Deliberately NOT done

- **Kitchen garden and cemetery** (defects #1, #2 — the two real blocking art items). Left alone on
  purpose: garden readability is explicitly Nick's taste call, Codex already burned seven rejected
  iterations guessing at it, and rebuilding them before his walk risks building the wrong thing.
- **Gate-lock backtrack test** (listed in §6 #12). Dropped after reading `GmThreshold`: the gate lock is
  a narrative/audio beat that adds **no physical barrier**, and retreating to the car before the lock is
  deliberate canon (the secret ending). A containment test there would assert behaviour nobody
  specified. Reporting the gap is more honest than inventing a passing test.
- All remaining §6 POLISH and NICK items, which depend on the walk.

### Grade after fixes

Unchanged: **B+ product.** One blocking beat (figure) now lands and one non-shot became a real shot,
but the two zones that actually cap the grade — garden and cemetery — are untouched by design, and
audio/pacing/EV remain human-unverified. Tooling moves back to **A** (the flaky tour now self-heals and
route coverage is broader).
