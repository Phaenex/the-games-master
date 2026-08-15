# Labyrinth Unity scene

Generated scaffold for Phase 7. The C# files in this directory are the source of truth.

1. Run `npm run unity:scene:sync` to copy sources into `./unity-project`.
2. Implement deterministic construction in `Editor/GmLabyrinthBuilder.cs`.
3. Author scene grammar in `Editor/GmLabyrinthCompositionPlan.cs`; the placeholder keeps tests red.
4. Replace all `*-replace-me` review shots in `Runtime/GmLabyrinthShotTour.cs`.
5. Add objective room checks to `Editor/GmLabyrinthQualityAudit.cs`.
6. Run `node scripts/unity-cli.mjs rebuild labyrinth`, then `audit labyrinth`.
7. Run Unity EditMode tests, PlayMode route tests where relevant, and `tour labyrinth`.
8. Inspect every screenshot at full size and complete the human walk before approval.

The scaffold is intentionally incomplete. A successful compile is not a visual or gameplay pass.

Composition markers belong on the geometry the builder makes, never on a bare GameObject: the audit
measures renderer bounds, so a marker with nothing under it asserts nothing a reviewer could see.
`GmLabyrinthSceneParts` is how the builder hands that geometry to the plan.

Open, and not answerable from a green audit: the review shots and their claims were paired without
geometric checking, so several shots do not see the element they claim (01 and 06 in particular).
Those are framing calls for Nick, not numbers to tune until the audit stops complaining.
