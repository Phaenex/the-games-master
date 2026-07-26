# Codex Review Prompt — Wend Hill Prologue (independent grading)

> Paste everything below the line into Codex. It is written so you can verify or refute every claim
> yourself, not take anyone's word. Be adversarial. The point is to find what's wrong.

---

You are doing an independent, skeptical review of a Unity horror-game opening scene that another AI
(Claude) built and claims is now good. Your job: walk it, look at it with your own eyes, verify the
claims below against reality, and grade honestly. Assume the builder is biased toward its own work.
Find the flaws.

## The project

- Unity 6000.5.3f1, HDRP. Project at `/Users/damato/GamesMaster-Unity`.
- Web repo (scripts, docs) at `/Users/damato/Projects/the-games-master`.
- The scene is `Assets/Scenes/WendHill.unity` — the Prologue: a night approach up a drive to a lit
  Victorian mansion, past a gate that locks behind you, through grounds (cemetery, garden, coach
  yard), to a porch where a nine-toll bell takes you.

## How to drive it (do this yourself — don't trust prior screenshots)

All commands from the web repo. **Unity is single-instance**: never run two at once, and make sure no
Unity Editor is open before you start (`ps -Ao args | grep 'MacOS/Unity' | grep GamesMaster`).
The `~/GamesMaster-Unity` path is outside the default sandbox, so these need the sandbox disabled.

```bash
cd /Users/damato/Projects/the-games-master
node scripts/unity-cli.mjs rebuild   # rebuild the scene from GmEstateBuilderV2 (batchmode)
node scripts/unity-cli.mjs tour      # capture 12 screenshots -> ~/GamesMaster-Unity/Screens/WendHill/tour-*.png
node scripts/unity-cli.mjs test      # EditMode tests
npm run test:all                     # C# + JS logic + browser harness
```

The tour is the review set: `tour-01-spawn` (arrival), `02-car`, `03-gate`, `04-lookback`,
`05-middrive` (the money approach shot), `06-cem-path`, `07-cem-inside`, `08-chapel`, `09-gdn-inside`,
`10-well-shed`, `11-coach-yard`, `12-porch` (at the mansion). **Open all 12 PNGs and look at each.**
A luminance number is not a visual verdict.

Note: `tour` opens a real Unity window for ~40s to render (HDRP can't render headless here). The CLI
warms the Library headless first specifically so a "rebuild Library?" dialog can't block it — if a
Unity modal dialog still pops up during your run, that's a finding, report it.

## The builder's claims — verify or refute each

Grade each as CONFIRMED / PARTIAL / FALSE with what you actually saw:

1. **The gate is dark wrought iron**, not white plastic. (Look at 01, 03.)
2. **The ground is clean tiling mud**, no hard-edged grass-sprite-atlas patches and no giant flat
   leaf planes z-fighting the terrain. (Look at 05, 06, 07, 09 — the earlier bug covered most shots.)
3. **The drive is a tiling muddy road**, not a single texture smeared/streaked over 142 units. (05, 12.)
4. **The cemetery reads as a graveyard** — ~29 leaning grave crosses plus the open grave, not a chapel
   in an empty field. (06, 07.)
5. **The tree avenue is bare/dead trees**, not blocky low-poly green-leaf trees that read as broken
   shard geometry. (01, 05, 06.)
6. **The chapel, coach house, and shed are not crushed to near-black** — they have dim fill light so
   texture reads. (08 chapel, 11 coach-yard, 10 well-shed.)
7. **The night still reads dark and moody** in open areas (the ground was tinted dark after the mud
   swap made it too bright). Not washed-out gray. (all shots.)
8. **The mansion stands textured and lit** at the end of the drive, right-side-up, doors sealed (they
   never open — that's canon). (05, 12.)
9. **The car sits clear of the roadside fence**, not clipping through it. (02, 04.)
10. **Ground mist** rolls low across the scene for atmosphere. (01, 05.)
11. **Tests pass**: `node scripts/unity-cli.mjs test` should report 34/34 EditMode; `npm run test:all`
    should show C# 23, JS 23, browser 47. Run them. Report the real numbers.
12. **No magenta anywhere.** Rebuild and `grep -c 'EXPECT MAGENTA' ~/GamesMaster-Unity/Logs/cli-rebuild.log`
    should be 0, and no pink surfaces in any shot.

## Also check for regressions the builder might have missed

- `grep 'placements:' ~/GamesMaster-Unity/Logs/cli-rebuild.log` should read `17 placed, 0 missing`.
- The WitchVillage pack must NOT have been mass-converted:
  `grep -rl 'guid: 0000000000000000f000000000000000' --include='*.mat' ~/GamesMaster-Unity/Assets/LeartesStudios/WitchVillage | wc -l` should still be ~141.
- The exposure must still be fixed, not automatic: `GmEstateBuilderV2.cs` should have
  `NightExposureEV = -3f` and `MoonLux = 1.7f` unchanged (automatic exposure would wash the night).
- No leftover temp editor scripts (`Assets/Editor/GmTemp*.cs`).

## What the builder itself admits is still weak (confirm, and find more)

- The `08-chapel` waypoint camera sits nose-against the chapel wall — a tour-camera framing quirk, not
  a scene defect. Confirm it's just the camera.
- A couple of trees still carry sparse leaves (not all bare). Minor.
- `EV -3` is the builder's own number, never art-directed by a human. Judge whether the night is too
  dark, too bright, or right.
- The grounds are dressed but the world beyond the fence is still fairly sparse/flat. Judge whether it
  reads as intentional-abandoned or just empty.

## Deliverable

For each of the 12 shots: a one-line honest verdict (does it look like a real horror-game opening?).
Then: the 12 claims graded CONFIRMED/PARTIAL/FALSE with evidence. Then a ranked list, worst first, of
what is still wrong or cheap-looking. Then an overall letter grade (A–F) for "would a player believe
this is a real horror game's opening," with one sentence justifying it. Do not be kind — the builder
asked for the pressure. Confirm you committed nothing.
