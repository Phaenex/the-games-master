# Codex Phase 0 Final-Pass Baseline — 2026-07-17

## Verdict

**B- / BORDERLINE PASS FOR NICK REVIEW.** The Unity estate is functional, navigable, textured, and
recognizably authored, but it is not yet an A-level opening. The remaining weaknesses are concentrated
in composition, variation, close-range readability, the missing window-figure event, and the exterior
audio ear gate. This audit is the frozen baseline for the final Phase 0 pass.

## Evidence frozen before edits

- `node scripts/unity-cli.mjs test`: **37/37 EditMode tests passed**.
- `npm test`: Shut the Box logic reached **23/23**; the browser harness entered its named scene run
  without a startup/page error. The full harness is re-run as a hard gate after implementation.
- Current HDRP tour: `~/GamesMaster-Unity/Screens/WendHill/tour-01-spawn.png` through
  `tour-12-porch.png`, all 12 present at 1920x1080.
- Previous measured tour luminance: mean values approximately **14–27**, with ACES and fixed EV -3.
- Current Steam tracker: overall Unity/Steam **15%**; Phase 0 **70%**.

## Audit table

| ID | Finding | Severity | Evidence | Fix owner |
|---|---|---|---|---|
| F0-01 | Cemetery is a readable room, but one wall module repeats continuously and grave families still resolve as kit rows from several views. | POLISH | `tour-06-cem-path.png`, `tour-07-cem-inside.png` | Agent |
| F0-02 | Kitchen-garden beds and work grammar disappear into the night from the canonical views. | POLISH | `tour-09-gdn-inside.png`, `tour-10-well-shed.png` | Agent |
| F0-03 | Coach yard has recognizable buildings but too little work-yard staging and negative-space control. | POLISH | `tour-11-coach-yard.png` | Agent |
| F0-04 | Mansion is grounded, textured, sealed, and amber, but almost every pane has the same state and the facade reads too evenly occupied. | POLISH | `tour-01-spawn.png`, `tour-05-middrive.png`, `tour-12-porch.png` | Agent |
| F0-05 | Canonical one-in-three upper-window figure is not implemented in Unity. | BLOCKING | `GmRareEvents.Update()` is empty | Agent |
| F0-06 | Existing wind graph is structurally sparse, but only a human ear can decide whether its source recording still reads as an engine/spaceship. | NICK | `GmAmbience`; repair report | Agent prepares A/B; Nick taste gate |
| F0-07 | Modern SUV conflicts visually with the old estate, but it is canonically the present-day guest's arrival car and is correctly parked/interactive. | NICK | `tour-02-car.png`; `NICK-NEEDED.md` | Nick taste call |
| F0-08 | Bright local pools and long hard shadows occasionally call attention to lighting rigs rather than architecture. | POLISH | cemetery, chapel, garden, porch tour frames | Agent |
| F0-09 | Placement correctness is covered indirectly, but there is no single audit for forbidden assets, material/shader failures, decorative colliders, grounded props, or route intrusion. | BLOCKING | current editor/test surface | Agent |
| F0-10 | Threshold Refusal remains sealed and the retired three-toll chapel knock-back must not return. | WONTFIX (canon) | `GmMansion.SealTheDoors`; ninth-bell spec/tests | Preserve |
| F0-11 | No additional environment or sound purchase is justified until owned assets and the no-wind mix have been exhausted. | BUY | bundle inventory + Horror Elements inspection | Nick only if later evidence fails |

## Locked implementation order

1. Add deterministic audit/test coverage.
2. Mine only the owned, theme-safe prop palette.
3. Finish cemetery, garden, coach yard, and mansion variation without disturbing passing zones.
4. Implement and force-test the window figure.
5. Produce wind/no-wind audio candidates and run the structural tests.
6. Rebuild twice, capture canonical and diagnostic views, inspect every image, then run route/pacing walks.

No purchase, commit, push, second mansion, or opened Threshold Refusal is authorized.
