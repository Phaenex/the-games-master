# Boot Unity scene

Generated scaffold for Phase 0. The C# files in this directory are the source of truth.

1. Run `npm run unity:scene:sync` to copy sources into `./unity-project`.
2. Implement deterministic construction in `Editor/GmBootBuilder.cs`.
3. Author scene grammar in `Editor/GmBootCompositionPlan.cs`; the placeholder keeps tests red.
4. Replace all `*-replace-me` review shots in `Runtime/GmBootShotTour.cs`.
5. Add objective room checks to `Editor/GmBootQualityAudit.cs`.
6. Run `node scripts/unity-cli.mjs rebuild boot`, then `audit boot`.
7. Run Unity EditMode tests, PlayMode route tests where relevant, and `tour boot`.
8. Inspect every screenshot at full size and complete the human walk before approval.

The scaffold is intentionally incomplete. A successful compile is not a visual or gameplay pass.
