# Unity Rebuild — native assets, no conversion pipeline

Decision (Nick, 2026-07-17): rebuild in Unity so the purchased packs run NATIVELY. The web build
stays as the playable design source of truth (copy, coordinates, mechanics, all tested), and the
FBX→GLB conversion pipeline is retired for art (it strips Unity's GUID-linked materials — the
root cause of "where are the Unity assets").

## Architecture

- **Engine:** Unity 6000.5.3f1 (installed). **Pipeline: HDRP** — the Leartes packs ship HDRP
  materials + their own HDRP quality presets ("HDRP Balanced / High Fidelity / Performant").
  Native import = the store-page look with zero conversion.
- **Project:** fresh dedicated project `~/GamesMaster-Unity` (HDRP). `~/Games Master` (Built-in,
  Nick's sandbox) is NOT touched — importing HDRP there would pink his existing scenes.
- **Source of truth:** the web build's design exports to `unity/design-data/*.json` — estate
  placements, walk rects, POIs (both text layers), beats, cold open, letter, endings copy, SFX
  cues. Unity consumes these; the story lives in ONE place.
- **Automation split:** I drive everything scriptable — project creation, manifest, batchmode
  package imports, C# systems, editor scene-builder scripts (`-executeMethod`), play-mode tests,
  batchmode screenshots. Nick drives what needs eyes/GUI: HDRP wizard prompts if any, lighting
  bakes review, in-editor look tuning, and the actual playing.

## Phases

- **U0 — Boot (now):** create project · HDRP via manifest · batchmode-import Haunted Village
  (native .unitypackage) · assign Leartes HDRP settings asset · verify a batchmode screenshot of
  their demo content renders non-pink. Gate: a real HDRP frame with Leartes materials.
- **U1 — Estate greybox from design data:** editor script builds the Wend Hill layout from
  placements JSON using the native prefabs (church, houses, fences, well…) + terrain/ground +
  drive spline. FPS controller (InputSystem, already familiar in Nick's sandbox) + walk colliders
  from the rect data. Gate: walk the estate in-editor at HDRP quality.
- **U2 — Systems port:** beats (z-triggers + queue), examine (two-layer, E prompt), gate-lock,
  Threshold Refusal sequence, letter UI, menu/options, SFX cues (the OGGs import natively),
  window figure + bell + dying lamp as behaviors. Gate: prologue playable start→aftermath.
- **U3 — Testing rig, Unity edition:** Unity Test Framework play-mode suite + batchmode runner
  reproducing the agent-playtest contract (geometry audit via scene scan, POI reachability via
  NavMesh, walkthrough with input simulation, screenshot metering). Same findings-JSON shape.
- **U4+ —** interior scenes (Mansion Interior payload NATIVE — the $50 bundle's biggest win),
  Court/STB per the existing next-after-walk plan, lore per the control-and-history doc once
  Nick blesses it.

## What transfers 1:1 (nothing is lost)

Story bible, house history, control/history proposal, all copy (verbatim via JSON), estate
coordinates (same world units), walk-rect topology (→ colliders), POI system design (both
layers), beat timings (the READING-time math holds), Shut-the-Box rules (JS → C# port, its 23
tests come along), six-endings math, and every hard-won testing rule (they were mostly about
instruments, not the web).

## Honesty

This is a real rebuild: U0–U2 before the prologue is walkable again at the new quality. The web
prototype stays runnable throughout — nothing is deleted. Unity batchmode steps can be slow
(HDRP compile + 1.3GB import); they run in background like everything else today.
