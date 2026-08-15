# Shut the Box — Unity rules staging

This directory is the engine-independent C# port of `gm-shutbox-logic.js`. It intentionally lives
outside the active `GamesMaster-Unity` project so work on the rules cannot trigger a Unity domain
reload while the Wend Hill capture pass is running.

- `Runtime/ShutBoxRules.cs` contains the pure box, move, AI, Hold, tile-9 door, and ledger rules.
- `Tests/ShutBoxRulesTests.cs` mirrors the JavaScript suite's 23 assertions as NUnit editor tests.
- `Tests/ShutBoxParityRunner.cs` provides the same 23 checks as a standalone executable.
- The assembly definitions are meant to be copied with their directories into a Unity project's
  `Assets/GamesMaster/ShutTheBox/` directory.

A copy already sits at `unity-project/Assets/GamesMaster/ShutTheBox/`, complete with `.meta` files,
so Unity treats it as live code. It was placed by hand: `git status` reports it untracked, and no
`sourceDir` in `unity/scene-system/scene-registry.json` covers this package, so `npm run
unity:scene:sync` does not know about it and will not keep the two in step. This directory stays the
source of truth; the copy has to be refreshed by hand until someone registers it.

Run the standalone compile and parity gate with:

```bash
npm run test:logic:csharp
```

The command uses Unity's bundled Mono/C# compiler, compile-checks both the runtime and NUnit test
assembly, executes the standalone checks, and removes its temporary build directory afterward.
Use `npm run test:all` to run this gate followed by the existing JavaScript and browser harnesses.

Do not maintain a second divergent implementation after import: changes should be applied here
first and verified against both the JavaScript and NUnit parity suites.

## Known rule gaps (inherited from `gm-shutbox-logic.js`, not port regressions)

Both need the JavaScript module changed in the same pass, so neither is fixed here yet.

- `ApplyMove` takes no roll total and never checks that the shut tiles sum to the active roll.
  The legality rule lives only in the opt-in `LegalMoves(box, total)` helper, so a caller can shut
  any open combination and the engine reports success. Whether the engine enforces this or the
  turn controller does is still open.
- `ApplyMove` with an empty tile list is vacuously valid: both loops run zero times and it returns
  `Ok` with the sum unchanged. No test in either suite covers an empty selection.
  `GmShutTheBoxController` used to lean on that as a rejection path and no longer does -- it
  returns `MoveResult.Rejected` instead.
