# Continuation brief — post-/clear pickup

Last updated 2026-07-17 late (Opus session). Read this + `docs/superpowers/plans/2026-07-17-unity-rebuild.md`
first. Driver can be Opus or Sonnet; panel subagents are Sonnet.

**This file previously claimed HDRP was working and the payloads were "imported natively". Both were
wrong and cost real time. What follows is what was actually observed, with the evidence.**

## Where things stand RIGHT NOW

- **Engine pivot decided and executing:** the game is rebuilding in Unity (`~/GamesMaster-Unity`,
  6000.5.3f1, HDRP) so the Leartes packs run natively. The web build at `~/Projects/the-games-master`
  is the TESTED DESIGN SOURCE (copy/coordinates/mechanics), not dead.
- **Nick's four clicks are gone.** They are scripted. See "Driving Unity from the terminal" below.
- **The Wend Hill scene renders a real night** as of this session. It did not before, in any run.
- **Shut the Box C# port is imported and green in-engine:** 23/23 EditMode tests.

## Driving Unity from the terminal — `scripts/unity-cli.mjs`

No clicking required. Requires the Unity editor to be CLOSED (the script checks the lockfile against
the live process list and clears stale ones).

```bash
node scripts/unity-cli.mjs pipeline   # GmPipelineSetup.Apply      (batchmode)
node scripts/unity-cli.mjs rebuild    # GmEstateBuilderV2.Build    (batchmode)
node scripts/unity-cli.mjs setup      # pipeline then rebuild
node scripts/unity-cli.mjs tour       # 12 shots -> Screens/WendHill/  (opens a window, ~40s)
node scripts/unity-cli.mjs test       # EditMode tests -> Logs/editmode-results.xml (batchmode)
```

Two hard constraints, both verified, do not re-litigate them:
- **HDRP will not render in batchmode here.** `GmProbe` already tried the RenderTexture path and got
  blank frames. The tour therefore launches a *windowed* editor on purpose. Tests and builders stay
  headless because they never render.
- **`~/GamesMaster-Unity` is outside the Bash sandbox's write allowlist.** Every Unity invocation
  needs the sandbox disabled or it dies instantly with exit 127 and no log.

The script fails loudly rather than passing green: it deletes its own stale log and output first
(both have caused false passes), gates the tour on per-shot `meanLum` (all-white = batchmode render,
all-black = exposure vs light mismatch), gates tests on the NUnit XML (0 tests = assembly did not
compile), and times out at 20min instead of waiting on a blocked editor forever.

## Two root causes found this session (both were invisible and both cost days)

### 1. Magenta everything — Leartes ships two pipelines and both got imported

The packs contain built-in AND HDRP variants of the same assets, with the same names.
`AssetDatabase.FindAssets("SM_Tree_")` returned both, and the built-in copies use the Standard
shader (`guid: 0000000000000000f000000000000000`, fileID 46), which HDRP has no pass for and renders
magenta. Materials on built-in shaders, by pack:

| Pack | Materials | On built-in shaders | Has HDRP variant |
|---|---|---|---|
| HauntedVillage | 76 | 20 | **no** |
| Abandoned Village | 168 | 7 | yes |
| WitchVillage | 252 | **141** | yes |

Fixed in `GmEstateBuilderV2` via `PreferHdrp()` / `RendersUnderHdrp()`: prefer assets whose materials
resolve to a shader under `Assets/` or `Packages/`, fall back to built-in *with a loud warning*
rather than silently dropping the prop. `FindMaterial()` already filtered for HDRP, which is why the
ground and drive looked correct while everything else was pink.

**Still open:** `SM_House_02` has no HDRP variant in the pack, so retargeting structurally cannot fix
it. It logs `EXPECT MAGENTA`. It needs Unity's built-in→HDRP converter on that one asset, or a swap.

### 2. The night was never applied — an empty volume profile

`VolumeProfile.Add<T>()` only builds the override in memory. Nothing wrote them into the `.asset` as
sub-objects, so the profile reloaded as `components: []` and overrode nothing. HDRP silently fell
back to its **default sky and default automatic exposure**.

That fallback is why the estate rendered as blue daylight, and why cutting the moon from 2200 lux to
1.7 changed the measured luminance by *two points* (84 → 82): auto-exposure compensated for whatever
light level was set. Any theory built on lighting values was chasing a ghost.

Fixed with `AddOverride<T>()` (`AssetDatabase.AddObjectToAsset` per component) plus an assert that
fails the build if zero overrides persist. **This bug is invisible in the editor** — the in-memory
profile looks correct and only play mode reveals it. If the estate ever looks like day again, check
`Assets/Scenes/WendHillNight.asset` for `components: []` before touching a single light value.

### Lighting constants (top of `GmEstateBuilderV2`)

- `MoonLux = 1.7f` — matches Leartes' own directional light in Showcase (`m_Intensity: 1.7`,
  `m_LightUnit: 2` == Lux). The old 2200 was ~1300x that.
- `NightExposureEV = -3f` — **an agent's number, not art-directed.** Yields meanLum 29–58 across the
  12 tour shots: dark, navigable, real shadows. Nick has not signed off on it.
- Leartes pair 1.7 lux with AutomaticHistogram exposure. We deliberately use **Fixed**: auto-exposure
  re-brightens authored darkness, which is the exact failure the web build spent three days rooting
  out. Do not "fix" this by switching to automatic.

## Shut the Box — Codex's lane, now closed

Codex is dead. It did **not** leave the lane empty, and an earlier check in this session wrongly
reported that it had, because it only looked inside `~/GamesMaster-Unity/Assets/`.

Codex staged a complete, tested C# port in the **web repo** at `unity/shut-the-box/`, deliberately
outside the Unity project so rules work could not trigger a domain reload during the capture pass
(its README says so explicitly). That work is now imported to
`~/GamesMaster-Unity/Assets/GamesMaster/ShutTheBox/` and verified in-engine.

- `Runtime/ShutBoxRules.cs` — pure rules: box, moves, AI, Hold cheats, tile-9 door, ledger.
  `noEngineReferences: true`, so it compiles standalone under Mono with no Unity.
- `Tests/ShutBoxRulesTests.cs` — 23 NUnit assertions mirroring the JS suite.
- `Tests/ShutBoxParityRunner.cs` — the same 23 as a standalone exe.

Green as of this session: C# parity 23/23 (Mono), JS logic 23/23, browser harness 47/47,
Unity EditMode 23/23 (`GamesMaster.ShutTheBox.Tests.dll`).

**The web repo copy stays the source of truth.** Change rules there, verify against both suites, then
re-import. Do not let the two implementations diverge.

## Standing rules that survive the clear

- No commit/push/purchases without Nick's explicit word (the large uncommitted tree is INTENTIONAL).
- Threshold Refusal canon: front doors never open in the Prologue.
- Every change verified with evidence; screenshots read by eye; honest verdicts. Web-side:
  `npm run gates` / `npm run test:all`. Unity-side: `unity-cli.mjs test` + tour + read the shots.
- Story canon: story bible + house-history spec. Lore proposal AWAITING Nick's five calls:
  `docs/superpowers/specs/2026-07-17-control-and-history-design.md`.

## Next actions queue

1. **Nick walks it.** Now worth doing — there is a real night to judge. `EV -3` is the open taste
   call, and the mansion is still a placeholder.
2. `SM_House_02` magenta: converter on that one asset, or swap it.
3. Spawn is bare — no mansion, no car, no trees at z=72 (tree lines start at z=58).
4. The agent visual panel is unblocked: `Screens/WendHill/*.png` are real. 3 Sonnet subagents
   (aesthetics, horror-mood, technical) scoring against a Leartes baseline. Note the baseline still
   needs its own waypoints — `GmShotTour`'s 12 are Wend Hill coordinates and are meaningless in
   Showcase.
5. Mansion pack decision (Nick) → import → place at z=-58 per design data → arm figure event.
6. Letter/menu/cold-open UI port (UGUI), from design-data copy.
7. Court build per `2026-07-17-next-after-walk.md` once Nick calls the opening good.
