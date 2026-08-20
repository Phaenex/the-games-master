# The Mirror: The House Remembers

**Date:** 2026-08-16  
**Status:** Approved design, staged implementation in progress  
**Human owner:** Nick D'Amato  
**Approval evidence:** Nick approved each design section in the active project task on 2026-08-16; the final presentation and technical standards incorporate the recorded panel consensus from the same task.  
**Scope:** Cross-run memory, adaptive host behavior, seven-game replayability, physical presentation, accessibility, durability, and proof

> **Canon correction, 2026-08-19:** The game names, order, rules sketches, implementation order, and
> related decision record in this document are superseded by
> [`docs/SEVEN-DEBTS-CANON-2026-08-19.md`](../../SEVEN-DEBTS-CANON-2026-08-19.md).
> In particular, Bones now uses three dice and one bank-or-press choice per round; Black Ledger and
> Last Candle replace the earlier Inventory and Key and Bell sketches. The cross-run memory and
> accessibility requirements in this document still apply.

## 1. Purpose

The Games Master needs repeat play to remain interesting after the player learns that Aldric cheats. The answer is not to hide the premise again. Repeat play changes the question from "does he cheat?" to:

- When is he threatened enough to cheat?
- Which rule will he attack?
- Is the tell genuine, suppressed, or planted?
- What evidence survives the performance?
- Is exposing him now better than exploiting the cheat?
- What did the house learn from the last completed night?

The optional post-ending mode is **The Mirror**, with the menu line **The house remembers.** `docs/superpowers/specs/2026-07-12-story-bible-final.md` section 6 and `2026-07-12-aldric-mythology-story-bible.md` section 8 establish it as the secret eighth, menu-level game. It is unrelated to the physical mirrors in the Labyrinth.

Ordinary New Run remains a clean canonical playthrough. The Mirror is opt-in and never contaminates it.

## 2. Current Reality

This design does not claim the seven games are finished.

- Parlor has the strongest foundation: deterministic rules, best-of-three match flow, host logic, cheat and tell behavior, exact snapshots, and durable mid-match continuation.
- Shut the Box has rules and tests, but not a finished physical match, presentation, animation set, or shipping proof.
- Bones has deterministic rules, a durable full-match controller, exact recovery, accessible input
  and HUD, and a direct-review physical table, but no campaign route, production art, or human play
  sign-off. Study has deterministic rules and a match/intervention snapshot core; it has no
  controller, save, input, HUD, or scene yet. Wager, Black Ledger, and Last Candle have approved
  rules only.
- Court, Hidden Room, and Labyrinth are story rooms. They are not part of Aldric's seven table games.
- Existing interior meshes and lighting improve the rooms, but do not make their game presentation complete.

No game may be called finished from rules tests alone. Completion is tracked separately as rules-complete, controller-complete, presentation-complete, and shipping-proven.

### 2.1 Current implementation boundary (2026-08-17)

The Parlor now has the deterministic foundation for adaptation: validated completed-match summaries, a public-action-only behavior accumulator, a pure five-receipt adaptive director, frozen versioned packages, canonical package and replay hashes, and exact snapshot restore. Ordinary play remains baseline. Recollection can replay an explicitly supplied known package and does not teach.

This is not production cross-run House memory yet. The immutable lineage-bound House profile, terminal receipt acknowledgement, generation manifest, compare-and-swap application, recovery flow, Mirror title flow, and Ledger Review are still unimplemented. The mutable run save carries only the active match's frozen package and in-run behavior accumulator. It does not carry a House profile or completed-run receipt history. Abandoned or partial play cannot teach because there is no production receipt application path yet.

The current Read action is also still binary. Automatic focus and evidence-log entries are presentation records, not committed evidence choices, so the profile does not claim an evidence preference from them. Factual claim selection, explicit inspect or pin actions, and durable proof-family claims remain a separate required milestone before evidence-specialist adaptation can ship.

## 3. Experience Principles

### 3.1 The honest contest must work

Every game must be enjoyable when Aldric never cheats. A cheat ruptures a contest the player already cares about. It cannot be the only interesting event.

### 3.2 Aldric cheats only when genuinely threatened

Before every cheat, the authoritative rules model must prove that Aldric is losing or lacks an honest winning line under that game's authored threat rule. He never cheats from safety. Corruption changes frequency and visibility, not this condition.

### 3.3 Adaptation is fair

The house may respond only to completed, meaningful decisions from durable runs. It may not inspect current highlights, cursor motion, uncommitted inputs, pauses, controller choice, accessibility settings, or reaction speed.

The room's complete adaptive package is selected and saved before play begins. It cannot reroll on reload or change because the player starts winning.

### 3.4 Evidence stays physical and auditable

Every discrete cheat has at least two independent evidence channels. False tells may misdirect attention, but they cannot manufacture false physical proof.

The same controller action may invoke a context-sensitive proof verb for accessibility, but the procedure is different in every game. There is no universal glowing Catch prompt.

### 3.5 Knowledge is progression

There are no permanent stat bonuses, random drops, upgrade currency, daily tasks, or grind requirements. The player grows through knowledge, new strategy packages, and new interpretations.

## 4. Mode and Memory Architecture

### 4.1 Two kinds of state

**Run state** contains the active campaign:

- Current scene and checkpoint
- Catches and evidence
- Shards
- Sanity and corruption
- Defiance and compliance
- Completed rooms and table games
- Active game snapshots
- Run RNG and pending outcomes

**House profile state** contains durable cross-run memory:

- Completed-run receipts
- Endings reached
- Cheat families caught or understood
- Broad behavior tendencies
- Host strategies revealed
- Recollection and House Rule unlocks
- Narrative memory tokens
- Room scars and artifact callbacks

New Run clears only run state. It does not modify the House profile.

### 4.2 What the house may learn

The profile may derive only coarse tendencies from completed runs:

- Early or patient accusation
- Preferred evidence channel
- Risk appetite
- Defiance and compliance tendency
- Rooms mastered or repeatedly lost
- Major bargains and betrayals
- Cheats previously caught
- Strategies already revealed

It does not record raw button streams, cursor motion, free-form text, momentary highlights, unrelated filesystem data, or abandoned runs.

### 4.3 Recognition curve

Recognition escalates rather than announcing itself immediately.

1. The first Mirror run changes one object, one line, one strategy, and one room arrangement.
2. A later callback proves the changes were not procedural coincidence.
3. Subsequent runs allow Aldric to become direct when recognition can bait a premature accusation, pressure a bargain, or change the meaning of the player's final commitment.
4. Other hosts remember through dreams, inherited gestures, altered scripts, certainty, or denial.
5. The house remembers through props, inscriptions, arrangements, lighting, and objects that should not have survived.

Aldric never becomes an NG+ tour guide. He reveals memory because it threatens or manipulates the player.

### 4.4 Adaptive room package

At room start, the adaptive director freezes:

1. Honest host strategy
2. Adaptive counter-plan
3. Available cheat grammar
4. Tell presentation
5. Evidence presentation
6. Narrative memory token
7. Room composition variation

Every counter-plan declares:

- The completed-run behavior it responds to
- The tactical change
- The changed tell
- The evidence that remains
- The weakness it creates
- Its foreshadowing
- The information it is forbidden to use

No more than two consecutive rooms may target the same tendency. Before an unseen strategy can produce a loss, the player must be able to observe one primary cue and one independent corroborating channel, retain the observed facts, and submit them to the same proof oracle used by its tests. Required observation and response windows follow the accessibility contract in section 7.

## 5. Seven-Game Portfolio

The portfolio uses different human faculties rather than seven reskinned suspicion meters.

| # | Game | Primary skill | Proof verb |
|---|---|---|---|
| I | Parlor | Tactical inference | Read |
| II | Shut the Box | Arithmetic and risk | Hold |
| III | Bones | Push-your-luck and physical inspection | Weigh |
| IV | Study | Spatial planning and rule control | Appeal |
| V | Wager | Contract reading and resource allocation | Invoke |
| VI | Inventory | Memory and physical forensics | Quarantine |
| VII | Key and Bell | Deduction and behavioral synthesis | Expose, Reverse, Accept, or Permit |

### 5.1 Game I: Parlor

The existing best-of-three Flames engine remains the foundation. Four suits create tactical exceptions. The player reasons about legal winners, leads, follows, Aldric's hand, and tell reliability.

Aldric may renege or produce an impossible rank only when no honest winning line exists. A Read requires evidence and carries risk. Remembered strategies may suppress familiar tells, bait premature Reads, change lead priorities, or sacrifice a small trick to protect a later line. They still cannot violate the legal-win rule.

### 5.2 Game II: Shut the Box

Two nine-tile boxes use public dice, legal tile combinations, voluntary banking, and lowest remaining sum. The player decides whether to keep pushing or secure a score.

When behind, Aldric may palm a die, make a false call, or reopen a shut tile. Hold freezes both boards and exposes the roll and move history. A remembered host may handle a fair die suspiciously to bait a bad Hold, but cannot create false board evidence.

### 5.3 Game III: Bones

Four bone dice create scoring combinations across three throws. Players preserve selected dice, reroll the rest, and bank or risk the unbanked score. Repeated grave faces erase the current unbanked total.

When behind, Aldric may substitute a weighted bone or falsely settle a cocked die. Weigh compares a suspect die against a brass balance and its recorded pips, wear, sound, and throw history. A false proof costs the current unbanked score.

Bones is about physical risk and inspection. Shut the Box remains about arithmetic and board integrity.

### 5.4 Game IV: Study

Study is a compact 5x5 chess contest with a king and limited major pieces. Players score through marked-square control, forced concessions, or mate. Legal moves are visible so full chess knowledge is not a gate.

Aldric receives one visible Arbiter token that permits one declared exception. When behind, he may spend it twice, change its scope, or restore a captured piece. Appeal reconstructs the disputed move from the turn journal and identifies the violated rule.

### 5.5 Game V: Wager

Three rounds present sealed offers with public rewards and concealed clauses. Match-specific sovereigns may be staked, preserved, or placed against a document to reveal portions of hidden ink. The player may accept, refuse, or amend each offer.

When behind in secured value, Aldric may alter a signed clause, misstate a payout, or substitute a seal. Invoke names the exact clause and supplies the physical contradiction. Wager tests greed, restraint, and contract reading without creating permanent economy power.

### 5.6 Game VI: Inventory

Nine prior-guest artifacts are arranged on green baize. Limited inspection actions reveal weight, wear, sound, provenance, orientation, and relationships. After the screen closes, both sides reconstruct the chain of custody.

When behind, Aldric may replace a near-duplicate, rotate the tray, relabel an artifact, or remove one and deny it existed. Each manipulation leaves at least two corroborating traces. Quarantine requires naming the object and reconstructing what changed.

Inventory cannot be a visual spot-the-difference test. Weight, sound, texture description, ledger evidence, and testimony provide equivalent routes. Mirror runs may include an artifact from the player's prior ending.

### 5.7 Game VII: Key and Bell

Seven period keys have discoverable weight, metal, tooth pattern, wear, residue, sound, and lock compatibility. The player asks limited formal questions, examines evidence, and commits keys to a miniature plan of the house. Each bell seals one commitment.

The setup comes from a bounded deterministic grammar. An independent solver must prove each setup is solvable, requires at least three player commitments, preserves at least two non-dominated legal lines after each of the first two commitments, and does not expose a forced final key before the player acquires two independent evidence facts.

When cornered, Aldric may switch a key, alter a tag, answer about the wrong lock, or use the bell to conceal a substitution. Physical state remains the hard proof. Reading Aldric reveals motive and catches the manipulation faster, but the puzzle is mechanically solvable without interpreting his performance.

Final actions are performed through play:

- Expose the manipulation
- Reverse it
- Accept it knowingly
- Permit it

The ending resolver remains authoritative and evaluates the full run after the final action. The finale does not replace ending logic with a single dialogue choice.

### 5.8 Story rhythm

The intended flow is:

```text
Prologue -> Entry Hall
Game I: Parlor
Court
Game II: Shut the Box -> optional Hidden Room
Game III: Bones
Game IV: Study
Labyrinth
Game V: Wager
Game VI: Inventory
Game VII: Key and Bell
Ending
```

Court, Hidden Room, and Labyrinth interrupt competitive rhythm. The Labyrinth selects and freezes remembered route grammar and Huntsman tactics at entry, but receives no forced proof verb or catch score.

## 6. Physical Presentation Standard

### 6.1 Physical-first, not physical-only

Cards, dice, boards, pieces, documents, artifacts, keys, seals, hands, and evidence exist as physical 3D objects in the world. An equivalent focus or board view may show the same authoritative state for readability and accessibility.

The focus view may expose facts that are already perceptible, such as a die face or card value. It may not label guilt, select the correct evidence, or reveal hidden information.

### 6.2 State authority

The deterministic C# rules model is authoritative. It emits typed semantic presentation commands. Animation, physics, Timeline markers, IK, particles, and audio never decide outcomes or advance turns.

Each game implements a narrow presenter interface and reuses shared input, camera, save, overlay, animation, and failure-recovery systems.

### 6.3 Semantic action lifecycle

Every object action has:

1. Approach
2. Contact
3. Manipulation
4. Release
5. Settle

Props define grip sockets, contact surfaces, preferred hand, approach direction, and release targets. Ownership transfers only at contact. Release resolves to a deterministic settled pose.

An action queue prevents two performances from claiming the same hand or prop. Semantic action logs reproduce presentation requests for debugging.

### 6.4 Character animation scope

Full-body Aldric animation is required for:

- Entering, leaving, sitting, and standing
- Center-of-gravity changes
- Reaching across the table or operating furniture
- Major catches and recovery
- Major wins and losses
- Final-game desperation
- Ending transitions
- Tells that depend on posture, shoulders, feet, silhouette, or reflection

Layered upper-body animation plus contact IK is sufficient for routine seated actions and small cheats inside normal reach.

The player has connected shoulders, arms, and a seated body anchor. Disconnected floating hands are not shippable. A full player body is required wherever mirrors, cast shadows, standing transitions, or body position become evidence.

### 6.5 Camera ownership

The game never rotates or translates the player camera to force a tell into view. Aldric performs inside readable sightlines. Optional emphasis must be brief, skippable, and reduced-motion compatible.

If a cheating hand leaves direct view, continuing evidence must remain through silhouette, cloth movement, sound, reflection, or the displaced object.

### 6.6 Asset quality

- Player-facing props use real authored or properly sourced meshes.
- HDRP PBR materials represent wood, metal, paper, wax, bone, cloth, glass, and wear.
- Pivots, scale, bounds, colliders, LODs, grounding, grips, and inspection volumes are validated.
- Primitive geometry is allowed for invisible collision, interaction proxies, and covered architectural shells.
- Visible shipping placeholders are prohibited.
- 2K textures are the default. A 4K texture requires proven close-up value and captured memory cost.

## 7. Accessibility Contract

Accessibility preserves deduction by preserving the information boundary.

- Every decision-relevant cue has two independent channels.
- No cue is color-only, motion-only, audio-only, vibration-only, or tiny-detail-only.
- Tells are pauseable, replayable, and inspectable without twitch timing.
- Observation and response windows may be extended or removed without ending, achievement, or content penalties.
- Evidence logs record observed facts, never conclusions.
- Focus views provide scalable semantic values, captions, contrast backing, and optional read-aloud.
- Full remapping, deterministic target cycling, aim assist, dead-zone control, sensitivity control, and hold-to-toggle conversion are required.
- Irreversible accusations show the selected target and verb, then require confirmation.
- Camera shake, head bob, motion blur, depth of field, FOV changes, forced emphasis, and turn sensitivity have separate controls.
- UI text, captions, card values, die values, prompts, and evidence labels scale independently.
- Essential text targets 4.5:1 contrast. Essential non-text indicators target 3:1.
- Settings persist across saves, scene changes, controller reconnects, and input-device changes.

The game never requires pixel hunting, precise pointer input, held camera aim, rapid presses, timed chords, stereo hearing, vibration, color discrimination, or memory of an unreplayable cue.

Explicit solution hints are separate from accessibility and are labeled as hints.

## 8. Performance Contract

Every performance artifact names exact hardware, OS, GPU and driver, resolution, HDRP preset, build hash, warmup, duration, and sample count.

At 1920x1080, target HDRP quality, 60 Hz standalone:

- Total frame p95: less than 16.7 ms
- Main thread p95: at most 10 ms
- GPU p95: at most 13.5 ms
- Frame p99: less than 20 ms
- No non-loading frame above 33.3 ms
- Zero steady-state managed allocations during table play
- No runtime shader compilation during a match
- No synchronous asset-streaming stall during a match
- At most two realtime shadow-casting punctual lights in a room
- Per-game scene residency guideline: at most 1.5 GB
- Process memory must retain at least 25 percent headroom on the named target machine
- Visible table guideline: at most 1.5 million triangles and 1,200 batches unless a captured GPU profile proves an exception

Performance qualification uses at least three clean repetitions. A single favorable trace is not evidence.

## 9. Replay Progression

### 9.1 Unlocks

Completing any of the six post-play endings unlocks The Mirror. The pre-gate retreat state does not, because the house never acquired or remembered that guest. The first Mirror completion unlocks Recollection mode.

Unlocks come from meaningful firsts:

- Reach a new ending
- Catch a new cheat family
- Beat a host strategy for the first time
- Discover a major room truth
- Win through an alternate strategy
- Understand a cheat without exposing it

Repeating the same ending or catch does not award currency or farm progression.

### 9.2 Ledger Review

After a completed Mirror run, the Ledger Review reports broad learned tendencies without exposing formulas or future strategies. Example facts include early accusation, reliance on written evidence, risk avoidance, or overlooked room spaces.

### 9.3 Recollection

Recollection supports:

- Individual game selection
- Known host strategy selection
- Deterministic seed entry
- Proof-procedure practice
- Unlocked House Rules
- Score and solution-path comparison

It cannot award catches, shards, endings, profile learning, or campaign progress.

### 9.4 Replay-complete content floor

Each finished game needs:

- At least three genuinely different honest host strategies
- At least two viable player strategies
- Two or three reactive cheat families
- Multiple tell performances per cheat family
- Two independent evidence channels per cheat
- At least three remembered counter-plans
- Several deterministic setup families
- An exploit for every counter-plan
- Unique reactions for correct proof, false proof, ignored cheating, wins, and losses
- Recollection support
- Full physical-presentation proof

## 10. Durable Save Protocol

### 10.1 Save domain and lineage

Run state, profile state, root state, receipts, manifests, and backups live under one dedicated save-domain directory.

Every history has a cryptographically random lineage ID. Every root, run, profile generation, manifest, receipt, and backup binds to that lineage. Artifacts from another lineage are rejected and quarantined.

### 10.2 Root generations

The memory epoch and tombstone root use immutable checksummed generations, a checksummed committed-generation manifest, and a monotonic predecessor hash chain.

The epoch is never inferred from a run, profile, or backup. If the newest committed root cannot be established, all House-memory writes and receipts are inhibited. The game never falls back to an older epoch.

### 10.3 Run identity

BeginNewRun acquires the exclusive save lease and atomically allocates a unique run ordinal from the memory root. A crash or abandonment may leave a gap. Reusing an ordinal with another run ID is corruption and fails closed.

If the root is unreadable or storage is unavailable, the recovery menu may start an isolated ordinary run with an explicitly nonpersistent temporary identity. That run cannot unlock The Mirror, update House memory, or claim that progress will persist. It never guesses an epoch or ordinal from damaged files.

### 10.4 Completion receipt

The terminal ending checkpoint and its prepared completion receipt are stored in one atomic run generation.

A run emits exactly one terminal receipt with outcome sequence 1. Receipt identity derives from lineage, epoch, run ID, run ordinal, and outcome sequence.

Receipt identity and payload hashing use a frozen, versioned, domain-separated, length-prefixed binary codec with fixed field order and integer endianness. JSON is never hashed. Historical canonical receipt bytes and hashes are preserved through migrations.

The profile atomically stores receipt ID, payload SHA-256, and effects. The same ID and hash is idempotent. The same ID with different content is corruption. Applied receipts are not pruned in this scope.

The run acknowledges only after the profile generation is durable.

### 10.5 Order-independent materialization

The profile retains immutable receipts and derives its materialized view deterministically by run ordinal and receipt ID.

- Sets use union.
- Tendencies use exact integer sums and counts.
- The last ending uses the greatest valid run ordinal.
- Scars and tokens derive from the sorted receipt list.

All receipt insertion orders must produce the same materialized profile hash.

### 10.6 Validated generations

Profile data uses immutable checksummed generations plus a small checksummed committed-generation manifest. A generation becomes current or last-known-good only after it is reopened, decoded, migrated, checksummed, and semantically validated.

Unreadable or unsupported generations are preserved. They inhibit writes rather than being overwritten.

### 10.7 Writer concurrency

All run, profile, erase, import, and recovery operations use one coordinator, an exclusive file lease, and generation compare-and-swap. A stale writer retries against the latest generation or fails without replacement.

Cloud synchronization is outside this scope and prohibited until a conflict protocol is designed and approved.

### 10.8 Memory erasure and total root loss

Normal erasure commits a new epoch before creating an empty profile. Older receipts remain permanently ineligible.

If root lineage is lost or ambiguous, explicit recovery:

1. Records recovery intent.
2. Acquires the exclusive lease.
3. Atomically renames the entire discoverable save domain to quarantine.
4. Creates a new authorized-recovery genesis with a fresh random lineage ID.
5. Durably commits the new root before enabling writes.

A crash resumes the recovery state machine. Reintroduced old artifacts cannot replace or merge into the new lineage.

## 11. Recovery and Privacy

An unreadable profile is quarantined once under a local incident ID. The menu offers:

- Restore Last Valid Profile, only after full validation
- Play a Normal New Run using isolated state without overwriting recovery files
- Reset House Memory with explicit scope confirmation
- Export Support Diagnostics with preview and destination selection
- Keep Files and Quit

A failed restore leaves every original artifact byte-identical.

Shipping support diagnostics include only build and platform class, error codes, schema versions, validation failures, involved strategy IDs, redacted relative save labels, and bounded performance summaries.

They exclude seeds, event histories, observed evidence, proof outcomes, raw or derived inputs, free text, player names, absolute paths, stable cross-export IDs, full saves, and memory dumps by default.

A gameplay trace is a second explicit export with plain-language disclosure and a random per-export ID. Nothing leaves the machine automatically.

Development traces require an explicit development flag, bounded rotation, a documented local directory, and a delete command. Release-binary inspection must prove the writer, symbols, endpoint strings, upload switches, and remote analytics dependencies are absent. Shipping network monitoring must observe zero outbound attempts.

## 12. Runtime Failure Recovery

Missing or retired strategy IDs first use a versioned migration table. If no mapping exists, the game chooses a named canonical fallback deterministically, persists the fallback and reason, notifies the player once, and prevents a retry loop.

A presentation failure:

1. Marks the semantic command skipped.
2. Releases claimed hands and props.
3. Clears the affected queue entry.
4. Reconstructs canonical object pose and persistent evidence.
5. Continues from authoritative rules state.

It cannot manufacture or erase a catch. Repeated failure in one command family trips a bounded session circuit breaker and uses the equivalent focus presentation for the remainder of the session.

## 13. Verification Strategy

### 13.1 Real oracles

Every seed sweep names an oracle:

- Independent reference model
- Exhaustive bounded solver
- Explicit metamorphic property

"Did not throw" and stable hashes are not correctness oracles.

For every cheat family, tests independently prove:

- Aldric is genuinely threatened.
- No legal winning line exists where required.
- The mutation is exactly the authored cheat.
- Required evidence appears after cheating and is absent in honest play.
- Correct, wrong, ignored, and late proof produce authored consequences.

### 13.2 Replay hash

The canonical replay hash covers rules state, RNG state, event cursor, chosen strategy, evidence state, pending outcomes, and semantic presentation phase. It excludes timestamps, object IDs, animation frames, and unordered serialization.

### 13.3 Coverage matrices

Maintain auditable matrices for:

- Game x host strategy x tier x honest/threatened/cheat state
- Cheat family x evidence channel x correct/wrong/ignored/late response
- Action family x semantic phase x speed/skip/interrupt/reload
- Save schema x corruption point x durability edge x recovery
- Input device x focus mode x UI scale x accessibility channel
- Scene x camera shot x aspect ratio x quality preset
- Game x standalone path x seed x ending contribution
- Asset family x pivot/bounds/collider/LOD/material/shader/build inclusion

Every cell names its oracle and artifact.

### 13.4 Persistence fault injection

Fault tests cover truncation, bit flips, checksum failure, unsupported schema, failed migration, disk full, permission denial, write, payload flush, atomic replace, directory flush, manifest rotation, root corruption, manifest rollback, backup corruption, concurrent writers, erase races, stale lineage, and acknowledgement loss.

The append-only chain detects a stale head when newer commit evidence is still present. It cannot
detect a coherent rollback of the entire House directory to an older valid snapshot. That requires
an external monotonic authority such as a platform service, secure counter, or separately anchored
ledger. Local storage must not claim to prove that case.

Recovery is always exactly the prior committed generation or the new committed generation, never a hybrid.

### 13.5 Presentation invariance

Every action family and semantic phase runs at 0x, 0.25x, 1x, and 4x, plus skip, interrupt, scene unload, and kill/reload.

All paths must preserve final rules hash, RNG cursor, canonical event sequence, evidence, consequences, input ownership, command ownership, and settled object pose.

### 13.6 Artifact provenance

Each proof run emits an immutable manifest containing:

- Source commit and dirty-tree content hash
- Unity version and package-lock hash
- Build configuration and executable SHA-256
- Harness and save-schema versions
- Exact command, seed, UTC time, isolated save directory, and run ID
- Hardware, OS, GPU/driver, display mode, and quality preset
- Raw child exit status, stdout, stderr, Unity log path, hashes, traces, screenshots, and video hashes

Wrappers capture the direct child-process verdict before filtering or tailing logs. No pipeline or chained echo determines success.

Every guard preserves a negative control: mutation identity, expected failure, actual assertion, and artifact manifest, followed by the green run.

### 13.7 Execution tiers

**Presubmit:** pure rules, bounded properties, replay hashes, fast persistence and migrations, changed-guard negative controls, asset validation, EditMode and PlayMode smoke, direct exit-code validation.

**Nightly:** full seed properties, cheat and evidence matrices, all semantic save phases, durability fault injection, animation invariance, standalone path per game, screenshot and accessibility matrices, ten-minute performance traces, and seven-game memory traversal.

**Release:** clean reproducible build, full recordings for all games, hard kill and reload, every supported migration, three-run minimum-spec performance, full memory traversal, packaging proof, and human approvals tied to current artifact hashes.

### 13.8 Human gates

Required play includes:

- Nick's controller playthrough
- First-time horror player
- Strategy-game player
- Repeat-run player
- Low-vision player
- Motor-fatigue player
- Motion-sensitive player

Human approval records reviewer, date, verdict, and exact artifact hashes. Relevant source, asset, scene, shader, or quality-setting changes invalidate the approval.

## 14. Presentation-Complete Gate

One uninterrupted shipping-player capture must show:

1. An honest round
2. Aldric becoming genuinely threatened
3. At least two legal cheat executions
4. A subtle tell and persistent evidence
5. Investigation
6. Correct proof
7. Wrong proof or intentionally ignored cheating
8. Aldric's reaction and recovery
9. Save during choreography
10. Process termination and exact continuation
11. Mouse and controller interaction
12. Accessibility presentation enabled and disabled

The package also includes shipping-camera video, diagnostic side-view video, hashes, screenshot matrix, accessibility proof, asset report, CPU/GPU/memory trace, and frame-by-frame contact inspection.

No visible teleporting, contact gaps, intersections, ownership snaps, foot sliding, camera seizure, buried props, default-sky leakage, missing materials, exceptions, deterministic mismatch, or steady-state GC allocation is accepted.

## 15. Implementation Decomposition

This master design is too large for one implementation plan. Work proceeds through independent vertical slices:

1. **Parlor physical vertical slice:** shared presentation coordinator, semantic actions, controller, UI, physical props, Aldric performance, accessibility, save reconstruction, and complete proof.
2. **House profile and The Mirror:** lineage storage, adaptive director, receipt banking, title flow, Ledger Review, recovery UX, and Recollection shell.
3. **Shut the Box vertical slice:** full board, AI, proof loop, animation family, and hidden-room integration.
4. **Bones vertical slice.**
5. **Study vertical slice.**
6. **Wager vertical slice.**
7. **Black Ledger vertical slice.**
8. **Last Candle vertical slice and ending integration.**
9. **Story-room adaptive pass:** Court, Hidden Room, and Labyrinth.
10. **Full-night qualification:** pacing, catches, all endings, accessibility, performance, and release evidence.

Each slice requires its own focused spec and plan before implementation. Shared infrastructure scales only after Parlor passes the full presentation-complete gate.

## 16. Non-Goals

- Cloud synchronization
- Remote analytics
- Permanent player power upgrades
- Random loot or daily progression
- Adaptive input reading
- Retroactive outcome changes
- Full bespoke motion capture for every prop variant
- A universal catch QTE
- Counting Court, Hidden Room, or Labyrinth among the seven table games
- Calling rules-complete content finished

## 17. Decision Record

📝 DECISION: Use escalating whole-house recognition with Aldric as its clearest voice | WHY: it preserves the first remembered run's dread and later proves the feature is real | ALT: immediate recognition spends the reveal too early; implication-only may feel like random variation.

📝 DECISION: Use distinct-faculty games with Black Ledger and Last Candle as games VI and VII | WHY: each contest owns a different player skill and evidence procedure | ALT: seven period reskins repeat the same catch loop; a genre anthology multiplies production pipelines.

📝 DECISION: Use physical 3D canonical state with equivalent focus views | WHY: physical evidence carries the horror, while equivalent views preserve accessibility and clarity | ALT: flat minigames break immersion; physical-only play excludes players and encourages pixel hunting.

📝 DECISION: Prove the complete interaction architecture on Parlor before scaling | WHY: seven games would otherwise multiply unproven animation, save, camera, and accessibility defects | ALT: parallel game construction creates seven incompatible pipelines.

📝 DECISION: Store profile history as lineage-bound immutable receipts and validated generations | WHY: cross-run memory must survive crashes, erasure, corruption, and retries without resurrection or double application | ALT: two mutable JSON files cannot provide the required durability.
