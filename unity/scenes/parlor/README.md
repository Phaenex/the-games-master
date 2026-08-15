# Parlor Unity scene

Generated scaffold for Phase 2. The C# files in this directory are the source of truth.

1. Run `npm run unity:scene:sync` to copy sources into `./unity-project`.
2. Implement deterministic construction in `Editor/GmParlorBuilder.cs`.
3. Author scene grammar in `Editor/GmParlorCompositionPlan.cs`; the placeholder keeps tests red.
4. Replace all `*-replace-me` review shots in `Runtime/GmParlorShotTour.cs`.
5. Add objective room checks to `Editor/GmParlorQualityAudit.cs`.
6. Run `node scripts/unity-cli.mjs rebuild parlor`, then `audit parlor`.
7. Run Unity EditMode tests, PlayMode route tests where relevant, and `tour parlor`.
8. Inspect every screenshot at full size and complete the human walk before approval.

The scaffold is intentionally incomplete. A successful compile is not a visual or gameplay pass.

## Builder / plan contract

`GmParlorBuilder` owns every renderer in the room and registers each one it intends to author into a
`Dictionary<string, GameObject>` keyed by the composition element id. `GmParlorCompositionPlan.Author`
takes that map and attaches its element markers to those objects; it creates zone and cluster
transforms and nothing else. A missing key throws rather than falling back to an empty GameObject,
because a composition element with no renderer is an intent the audit cannot measure — that failure
mode is what made every element in this scene unmeasurable before.
