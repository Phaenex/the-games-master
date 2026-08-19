# House Memory recovery design intent

- **ID:** `boot-house-memory-recovery`
- **Human owner:** Nick D'Amato, approving the House Remembers design and recovery scope
- **Primary object:** the title menu as the single safe boundary for inspecting, restoring or
  resetting persistent House memory
- **Information priority:** preserve player agency first, explain the consequence before mutation,
  disclose exactly what diagnostics omit, then expose technical detail only in the preview
- **Rejected defaults:** silent reset, automatic restore, overwriting an existing diagnostics file,
  presenting inferred play patterns as facts, and dumping storage terminology into player copy
- **Responsive and accessibility behavior:** keyboard/controller focus uses both a square marker and
  color; high contrast restyles dynamic panel content; Ledger and diagnostics panels have bounded,
  scrollable height and wrap at large text scales
- **Success measure:** a player can identify the recommended valid restore, inspect consequences,
  cancel without a write, and see success or failure feedback without reading logs
- **Failure conditions:** a corrupt root is treated as restorable without a validated predecessor;
  Continue is mutated before durable recovery intent; a destination is overwritten; support output
  contains paths, saves, seeds, inputs, evidence or identity; a modal clips beyond the 16:9 frame

Rendered proof is stored as `docs/playtest/screenshots/boot-tour-*.png`. The native macOS save panel
still requires one release-candidate human exercise because the automated tour injects deterministic
cancel and success destinations instead of controlling an operating-system dialog.
