# Boot Unity scene

Phase 0 title and recovery scene. The C# files in this directory are the source of truth; do not
edit the generated `Assets/Scenes/Boot.unity` YAML by hand.

1. Run `npm run unity:scene:sync` to copy sources into `./unity-project`.
2. Build with `node scripts/unity-cli.mjs rebuild boot`, then run `audit boot`.
3. Run Unity EditMode tests and `node scripts/unity-cli.mjs tour boot`.
4. Inspect all seven screenshots at full size. They cover the ordinary title, post-Mirror Ledger
   Review, recovery focus and confirmation, diagnostics preview, cancellation in high contrast and
   successful export feedback.
5. For a release candidate, exercise the real macOS destination panel once in a disposable profile.

The Boot tour uses backbuffer capture because UI Toolkit screen-space content is absent from camera
RenderTextures. Its sparse-UI gate still rejects a fully blank frame, and its perceptual check also
requires visible title and menu regions.
