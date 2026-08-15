# Hidden Room Unity scene

Generated scaffold for Phase 6. The C# files in this directory are the source of truth.

1. Run `npm run unity:scene:sync` to copy sources into `./unity-project`.
2. Implement deterministic construction in `Editor/GmHiddenRoomBuilder.cs`.
3. Author scene grammar in `Editor/GmHiddenRoomCompositionPlan.cs`; the placeholder keeps tests red.
4. Replace all `*-replace-me` review shots in `Runtime/GmHiddenRoomShotTour.cs`.
5. Add objective room checks to `Editor/GmHiddenRoomQualityAudit.cs`.
6. Run `node scripts/unity-cli.mjs rebuild hidden-room`, then `audit hidden-room`.
7. Run Unity EditMode tests, PlayMode route tests where relevant, and `tour hidden-room`.
8. Inspect every screenshot at full size and complete the human walk before approval.

The scaffold is intentionally incomplete. A successful compile is not a visual or gameplay pass.
