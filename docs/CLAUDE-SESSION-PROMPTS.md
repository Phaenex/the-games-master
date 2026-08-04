# The Games Master — session prompts for Claude Code CLI

Two prompts. Use the **kickoff prompt** once (first session after installing the new
config), then the **continue prompt** at the start of every later session. Both assume
you run from `~/Projects/games/the-games-master` with Unity closed.

---

## Prompt 1 — kickoff (run once)

```
This is the first session under the new project configuration. Do not write any code
or fix anything in this session until step 4. Work in this order:

1. Prove the config loaded. Run /context and confirm the root CLAUDE.md is in
   context. Run /agents and confirm all ten agents appear: game-designer,
   unity-architect, gameplay-engineer, feel-tuner, level-designer, test-runner,
   perf-auditor, playtest-runner, review-adversary, story-canon. If any are missing,
   copy them from claude-agents/ into .claude/agents/ and tell me to restart the
   session.

2. Orient. Read, in order: CLAUDE.md, docs/TESTING.md, docs/UNITY-SCENE-WORKFLOW.md,
   docs/STEAM-TRACKER.md, docs/PROGRESS.md, docs/NICK-NEEDED.md, and the most recent
   handoff/audit doc you can find in docs/. Treat every completion claim in those
   docs as unverified — do not inherit any grade, including from prior Claude
   sessions.

3. Baseline the truth. With Unity closed, run:
     npm run unity:scene:check
     npm run test:fast
     npm run gates
   Report results as a table of numbers (pass/fail counts per gate, which assertion
   failed, actual vs expected). Read docs/playtest/agent-report.json and list the
   current HIGH/MED/LOW findings. Open the newest screenshots at full size and give
   a separate visual verdict per area: PASS / BORDERLINE / FAIL. If any run passes
   only on retry, report it as FLAKY.

4. Report the state of the game: overall bar and every phase bar; what is
   objectively broken (agent-fixable) vs what is waiting on me (human gates —
   Phase 0 walk, feel, brightness, audio character, pacing); and the top 5 things
   you would fix next, ranked, with the evidence for each.

5. Propose a work plan for the next session and STOP. Do not start fixing, do not
   commit, do not push, do not buy anything.
```

---

## Prompt 2 — continue (every session after)

Replace the bracketed line, or delete it to let Claude pick from the ranked backlog.

```
Continue work on The Games Master under CLAUDE.md rules.

Focus for this session: [e.g. "fix the HIGH findings in agent-report.json" /
"Gate 2 blockout for entry-hall" / "extract Court feel constants into tunables"]

Session protocol:

1. Re-orient: re-read CLAUDE.md, docs/STEAM-TRACKER.md, docs/PROGRESS.md, and the
   last session's handoff. Do not inherit its grades — spot-check its claims
   against evidence before building on them. Confirm Unity is closed and
   npm run unity:scene:check is green before touching anything.

2. Work the focus through the fix loop: run the relevant gate first to see the
   real starting state, fix HIGH findings, re-run, then MED, re-run. Done means
   two consecutive clean runs. Before debugging ANY failure, check the fix
   playbook and harness rules in docs/TESTING.md — most flakes are the
   instrument, not the game.

3. Use the agents deliberately, not reflexively: playtest-runner after content
   changes, test-runner before any logic lands, unity-architect before anything
   spanning systems, review-adversary before you report a milestone done. One
   task, one agent.

4. New content is not "added" until the rig covers it: gmKind tag, expected-height
   range, reachable POI radius, tour shot named by what it proves, and
   npm run gates green.

5. Learn as you go: any newly diagnosed defect class or tooling trap gets appended
   to docs/TESTING.md (fix playbook / harness rules) in the session it was beaten.

6. Respect the locks: no commit or push without my explicit OK, no purchases, no
   second mansion, coaching-inn never "saloon", Threshold Refusal stays
   closed-door, no hand-edited .unity/.prefab YAML, never overwrite my calibration
   settings, taste calls come to me with evidence instead of being "fixed".

7. End with the handoff format: files changed; commands run; counts and
   fingerprints; screenshot paths + visual verdict (separate from test results);
   remaining objective defects; open Nick-owned questions; anything that passed
   only on retry; confirmation of no commit/push/purchase. Then refresh
   docs/PROGRESS.md with all phase bars and state the next blocker and whether it
   needs me.
```

---

## Optional one-liners for common situations

- **"Just verify, touch nothing":**
  `Run npm run gates and the agent playtest, inspect the screenshots at full size, and give me the numbers table + visual verdicts + ranked findings. Read-only session — no fixes, no commits.`

- **"Adversarial review of the last session":**
  `Use review-adversary to attack the previous session's handoff claims. CONFIRMED / PARTIAL / FALSE per claim with exact evidence, then a letter grade with no kindness. Read-only.`

- **"Before I do my walk":**
  `Prepare for my Phase 0 walk: confirm the build is fresh (npm run unity:build:mac + unity:proof:mac), list exactly what I should be judging (the open human gates from docs/NICK-NEEDED.md), and give me the launch steps. Do not change the scene.`

- **"After my walk":**
  `Here are my walk notes: [notes]. Turn them into ranked findings (my taste verdicts are final — record them, don't re-litigate), update NICK-NEEDED and the trackers, and propose the fix plan. No fixes until I approve the plan.`
