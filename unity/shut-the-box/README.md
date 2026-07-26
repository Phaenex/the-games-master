# Shut the Box — Unity rules staging

This directory is the engine-independent C# port of `gm-shutbox-logic.js`. It intentionally lives
outside the active `GamesMaster-Unity` project so work on the rules cannot trigger a Unity domain
reload while the Wend Hill capture pass is running.

- `Runtime/ShutBoxRules.cs` contains the pure box, move, AI, Hold, tile-9 door, and ledger rules.
- `Tests/ShutBoxRulesTests.cs` mirrors the JavaScript suite's 23 assertions as NUnit editor tests.
- `Tests/ShutBoxParityRunner.cs` provides the same 23 checks as a standalone executable.
- The assembly definitions are ready to copy with their directories into a Unity project's
  `Assets/GamesMaster/ShutTheBox/` directory after the editor owner releases the capture session.

Run the standalone compile and parity gate with:

```bash
npm run test:logic:csharp
```

The command uses Unity's bundled Mono/C# compiler, compile-checks both the runtime and NUnit test
assembly, executes the standalone checks, and removes its temporary build directory afterward.
Use `npm run test:all` to run this gate followed by the existing JavaScript and browser harnesses.

Do not maintain a second divergent implementation after import: changes should be applied here
first and verified against both the JavaScript and NUnit parity suites.
