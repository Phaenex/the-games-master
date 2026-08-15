# Court Unity scene

Generated scaffold for Phase 3. The C# files in this directory are the source of truth.

1. Run `npm run unity:scene:sync` to copy sources into `./unity-project`.
2. Implement deterministic construction in `Editor/GmCourtBuilder.cs`.
3. Author scene grammar in `Editor/GmCourtCompositionPlan.cs`; the placeholder keeps tests red.
4. Replace all `*-replace-me` review shots in `Runtime/GmCourtShotTour.cs`.
5. Add objective room checks to `Editor/GmCourtQualityAudit.cs`.
6. Run `node scripts/unity-cli.mjs rebuild court`, then `audit court`.
7. Run Unity EditMode tests, PlayMode route tests where relevant, and `tour court`.
8. Inspect every screenshot at full size and complete the human walk before approval.

The scaffold is intentionally incomplete. A successful compile is not a visual or gameplay pass.
