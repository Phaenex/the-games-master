# Parlor Physical Vertical Slice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the already deterministic and durable Parlor match into a complete, controller-playable physical table game whose cards, readable evidence, recovery behavior, and presentation remain subordinate to the canonical rules state.

**Architecture:** `GmParlorMatch` and `GmParlorRules` remain the only game authorities. A pure presentation journal converts before/after match snapshots into semantic commands. A scene coordinator executes those commands against bound physical card views, while a controller converts input into the existing rules APIs. Every presentation path can snap to canonical state after skip, interruption, load, or failure. The first slice proves the pattern in Parlor before extracting anything shared for the other six games.

**Tech Stack:** Unity 6 HDRP, C# runtime components, Unity Input System, UI Toolkit, NUnit EditMode and PlayMode tests, governed authored-to-Unity source sync, macOS standalone proof tooling.

---

## Scope and non-negotiable boundaries

- Keep `GmParlorMatch` deterministic and renderer-free.
- Keep `GmParlorRules` as the sole persistence and outcome boundary.
- Never let animation events, physics, frame time, or object transforms decide legality, cheating, tells, scoring, or completion.
- Retire `GmTheReadController` from the Parlor builder. It reads legacy `GmHostAI` state and applies run deltas a second time, so wiring it into the canonical match would corrupt authority.
- Use the existing Gameplay actions: Move for focus, Interact for confirm, CallTell for Read, Cancel for back/fast-forward, Pause for pause. Do not create duplicate actions or IDs.
- Do not force the player camera. The standard view stays diegetic; the focus view is an optional accessible equivalent driven by the same state.
- Do not call the current primitive Aldric AAA. The repository contains no skinned human rig or suitable character clips. This plan builds and proves the animator-ready semantic seam, then holds the final character-art gate open until a licensed rig and bespoke motion set are imported and reviewed.
- Do not commit or push unless Nick explicitly authorizes it. The current branch contains load-bearing uncommitted engine work.

## Required proof at slice close

- EditMode and PlayMode suites fully green.
- Authored and synced mirrors byte-identical.
- Rebuilt Parlor contains 28 individually bound physical cards, legal focus targets, controller wiring, and no `GmTheReadController` or live `GmHostAI` authority component.
- One uninterrupted built-player match can be completed with keyboard and controller.
- Correct Read, false Read, missed cheat, honest play, all four suit effects, round transition, match transition, rematch, pause, fast-forward, and quit are observed.
- Kill and reload during `AwaitingAldricJudgement` reconstructs the same physical table and consumes no outcome twice.
- Reduced-motion and focus-view modes preserve the same legal choices and evidence.
- Steady-state presentation produces zero managed allocations per frame and satisfies the existing 1080p HDRP frame budget.
- Final visual approval requires direct frame inspection. Test green alone is insufficient.

### Task 1: Freeze the semantic presentation contract

**Files:**
- Create: `unity/scenes/parlor/Runtime/GmParlorPresentationCommand.cs`
- Create: `unity/scenes/parlor/Runtime/GmParlorPresentationJournal.cs`
- Create: `unity/scenes/parlor/Tests/GmParlorPresentationJournalTests.cs`
- Modify through sync only: `unity-project/Assets/Scripts/Scenes/parlor/*`
- Modify through sync only: `unity-project/Assets/Tests/EditMode/Editor/Scenes/parlor/*`

- [x] **Step 1: Write the failing journal tests**

Test exact command sequences for these canonical transitions:

```csharp
[Test]
public void PlayerLeadProducesOnlySemanticCardAndHostCommands()
{
    GmParlorMatch before = NewStartedMatch(117);
    GmParlorMatchSnapshot oldState = before.ExportSnapshot();
    Assert.That(before.PlayPlayerCard(0), Is.EqualTo(GmParlorActionError.None));

    GmParlorPresentationCommand[] commands =
        GmParlorPresentationJournal.Build(oldState, before.ExportSnapshot());

    Assert.That(commands, Is.EqualTo(new[]
    {
        GmParlorPresentationCommand.PlayerCardToLead(oldState.playerHand[0]),
        GmParlorPresentationCommand.AldricCardToFollow(before.AldricCard.Value),
        GmParlorPresentationCommand.OpenJudgement(before.TellObservation),
    }));
}
```

Also assert:

- no command contains a transform, duration, clip name, probability, or score mutation;
- stable command IDs are identical after save/restore;
- honest and cheated Aldric plays expose different semantic actions without exposing hidden truth to the player-facing observation command;
- trick, round, match, and rematch boundaries each have an explicit command;
- impossible before/after pairs fail closed with a diagnostic instead of inventing motion.

- [x] **Step 2: Run the test and watch the honest red**

Run: `npm run unity:scene:sync && node scripts/unity-cli.mjs test`

Expected: compile failure naming missing `GmParlorPresentationCommand` and `GmParlorPresentationJournal` types.

- [x] **Step 3: Implement the smallest immutable command vocabulary**

Use a value type with semantic fields only:

```csharp
public enum GmParlorPresentationAction
{
    PlayerCardToLead,
    PlayerCardToFollow,
    AldricCardToLead,
    AldricCardToFollow,
    OpenJudgement,
    ResolveTrick,
    ResolveRound,
    ResolveMatch,
    BeginRound,
    BeginRematch,
    SnapToCanonicalState,
}

public readonly struct GmParlorPresentationCommand : IEquatable<GmParlorPresentationCommand>
{
    public readonly ulong Id;
    public readonly GmParlorPresentationAction Action;
    public readonly GmCard? Card;
    public readonly GmTrickOwner Owner;
    public readonly GmTellObservation Observation;
}
```

Derive `Id` from snapshot version, outcome sequence, round, trick, phase, action, and card identity. Do not use `GetHashCode`, wall-clock time, Unity instance IDs, or random values.

- [x] **Step 4: Prove red to green**

Run: `npm run unity:scene:sync && node scripts/unity-cli.mjs test`

Expected: full EditMode suite green with the new journal tests included.

### Task 2: Bind physical cards to canonical identities

**Files:**
- Create: `unity/scenes/parlor/Runtime/GmParlorCardView.cs`
- Create: `unity/scenes/parlor/Runtime/GmParlorTableLayout.cs`
- Create: `unity/scenes/parlor/Runtime/GmParlorPropBinder.cs`
- Create: `unity/scenes/parlor/Tests/GmParlorPropBinderTests.cs`
- Modify: `unity/scene-system/Editor/GmOwnedPropFactory.cs`
- Modify: `unity/scenes/parlor/Editor/GmParlorBuilder.cs`
- Modify: `unity/scenes/parlor/Tests/GmParlorBuildTests.cs`

- [x] **Step 1: Write failing binding and build contracts**

Assert the rebuilt scene has exactly 28 `GmParlorCardView` components with unique suit/rank identities, one draw slot, seven player slots, seven Aldric slots, two lead/follow table slots, and result/discard storage. Assert every card renderer uses the rounded bespoke card mesh, every card has face and back presentation, and no rule reads card transforms.

```csharp
Assert.That(cards.Select(card => card.Card).Distinct().Count(), Is.EqualTo(28));
Assert.That(Object.FindAnyObjectByType<GmTheReadController>(), Is.Null);
Assert.That(Object.FindAnyObjectByType<GmHostAI>(), Is.Null);
```

Binder tests must prove that the same snapshot always maps the same identities to the same logical slots, including Eyes reveals, Bones return, impossible-eight payment, and reload during judgement.

- [x] **Step 2: Run and observe red**

Run: `npm run unity:scene:sync && node scripts/unity-cli.mjs test`

Expected: compile failure for missing card view/layout/binder types.

- [x] **Step 3: Extend the owned card factory without creating rule authority**

Replace the static decorative `CreateCardDeck` use with a factory method that creates one rounded physical shell at a supplied transform. Each shell receives a `GmParlorCardView`; identity is assigned by the builder and rebound from canonical state at runtime. Generate readable suit and rank face geometry or texture assets, a distinct period card back, edge thickness, contact shadow, and a focus collider sized from the actual mesh bounds.

- [x] **Step 4: Add a pure logical layout**

`GmParlorTableLayout` returns stable logical poses for all slots. It must be testable without scene queries. `GmParlorPropBinder.ApplySnapshot` only maps views to those poses and visible states.

- [x] **Step 5: Rebuild and prove the scene contract**

Run:

```bash
npm run unity:scene:sync
node scripts/unity-cli.mjs test
node scripts/unity-cli.mjs rebuild parlor
node scripts/unity-cli.mjs audit-saved parlor
```

Expected: EditMode green; `[GmParlor] BUILD PASS`; `[GmParlorAudit] PASS`; 28 unique bound cards in the saved scene.

### Task 3: Execute commands with an interruptible presentation coordinator

**Files:**
- Create: `unity/scenes/parlor/Runtime/GmParlorPresentationCoordinator.cs`
- Create: `unity/scenes/parlor/Runtime/GmParlorCardMotion.cs`
- Create: `unity/scenes/parlor/Tests/GmParlorPresentationCoordinatorTests.cs`
- Create: `unity/project/Assets/Tests/PlayMode/GmParlorPresentationPlayModeTests.cs`

- [x] **Step 1: Write failing lifecycle tests**

Every card command must traverse `Approach -> Contact -> Manipulate -> Release -> Settle`. Tests advance a manual clock and assert state and final pose. Add interruption tests at every phase and assert `FastForwardToCanonicalState` leaves every physical card exactly at its binder pose.

Also assert:

- command completion never calls a rules method;
- a missing card view logs one error, snaps all remaining views, and unblocks input;
- reduced motion shortens travel and removes flourish but preserves contact and evidence timing;
- no `Update` path allocates after warmup.

- [x] **Step 2: Capture the compile red**

Run: `npm run unity:scene:sync && node scripts/unity-cli.mjs test`

Expected: missing coordinator and motion types.

- [x] **Step 3: Implement a manual-clock state machine**

Use `Time.unscaledDeltaTime` only at the MonoBehaviour boundary. The state machine accepts a delta explicitly for tests. Use preallocated arrays/queues, deterministic easing, and transform interpolation. Physics may add secondary settling only after the canonical target is fixed; it cannot select a slot or card.

- [x] **Step 4: Prove EditMode and PlayMode green**

Run: `npm run unity:scene:sync && node scripts/unity-cli.mjs test && node scripts/unity-cli.mjs playtest`

Expected: all EditMode and PlayMode tests green, including interruption recovery.

### Task 4: Replace the stale Read path with one canonical table controller

**Files:**
- Create: `unity/scenes/parlor/Runtime/GmParlorController.cs`
- Create: `unity/scenes/parlor/Runtime/GmParlorInput.cs`
- Create: `unity/scenes/parlor/Tests/GmParlorControllerTests.cs`
- Modify: `unity/scenes/parlor/Editor/GmParlorBuilder.cs`
- Delete after tests are migrated: `unity/scenes/parlor/Runtime/GmTheReadController.cs`
- Keep as pure compatibility utility only if still referenced elsewhere: `unity/scenes/parlor/Runtime/GmHostAI.cs`

- [x] **Step 1: Write controller reds around the existing rules adapter**

Test:

- Move changes focus once per threshold crossing and does not repeat while a D-pad direction is held;
- Interact plays the focused card only when `GmParlorMatch.GetPlayerCardError` succeeds through
  the rules adapter;
- Interact accepts honest Aldric play during judgement;
- CallTell invokes only `GmParlorRules.ReadAldricPlay`;
- locked Read produces feedback and does not mutate match or run state;
- Cancel fast-forwards presentation and never abandons a durable match silently. Focus-view back-out
  remains part of Task 6, where that state first exists;
- input stays disabled while a semantic command is blocking;
- restored pending outcomes are activated through the existing explicit start path before input opens;
- errors including `PersistenceFailed` and `OutcomeHandlerFailed` preserve a retryable state.

- [x] **Step 2: Capture red**

Run: `npm run unity:scene:sync && node scripts/unity-cli.mjs test`

Expected: missing controller/input types.

- [x] **Step 3: Implement input translation, not game rules**

`GmParlorInput` reads existing actions by exact name. `GmParlorController` owns focus and presentation gating, then calls only public `GmParlorRules` methods. It must never inspect `AldricCheated` for player-facing feedback before resolution and must never call `GmRunStore` directly.

- [x] **Step 4: Remove stale scene authority**

Update the builder to add `GmParlorController`, `GmParlorInput`, binder, and coordinator. Remove `GmTheReadController` and `GmHostAI` components from shipping Parlor. Delete `GmTheReadController.cs` once repository search proves no remaining callers.

- [x] **Step 5: Prove input IDs and maps remain clean**

Run:

```bash
npm run unity:scene:sync
node scripts/unity-cli.mjs test
node scripts/unity-cli.mjs playtest
npm run unity:scene:check
```

Expected: all suites green, Gameplay map hash unchanged, no duplicate map/action/binding IDs.

Proof captured: EditMode 574/574, PlayMode 31/31, saved-scene audit PASS, scene parity
378/378, Gameplay SHA-256 unchanged at
`70d777ac643257248d0683603509f97dd09e6525a836fcc0af969531b37cd65d`, and 65/65
map/action/binding IDs unique.

### Task 5: Give every decision-relevant tell two evidence channels

**Files:**
- Create: `unity/scenes/parlor/Runtime/GmParlorEvidenceLog.cs`
- Create: `unity/scenes/parlor/Runtime/GmParlorAldricPresenter.cs`
- Create: `unity/scenes/parlor/Tests/GmParlorEvidenceLogTests.cs`
- Create: `unity/scenes/parlor/Tests/GmParlorAldricPresenterTests.cs`
- Modify: `unity/scenes/parlor/Editor/GmParlorBuilder.cs`
- Modify: `unity/scenes/parlor/Runtime/GmParlorPresentationCoordinator.cs`

- [x] **Step 1: Write the evidence truth-table reds**

For Calm/Suspicious observation crossed with honest/cheated hidden truth, assert the player receives observation only before choice. Each Suspicious presentation must combine two channels, chosen from:

- visible hand hesitation or sleeve movement;
- card-contact discontinuity;
- positional audio cue;
- controller pulse;
- optional caption/evidence-log fact.

The evidence log records observed facts such as “His right hand stopped above the deck” and never conclusions such as “Aldric cheated.” False tells must use the same channel vocabulary and quality as true tells.

- [x] **Step 2: Capture red and implement the semantic presenter seam**

Run: `npm run unity:scene:sync && node scripts/unity-cli.mjs test`

Expected red: missing evidence/presenter types.

Implement `GmParlorAldricPresenter` as an animator-ready receiver of semantic cues: calm play, suspicious play, caught, falsely accused, win, loss. It may drive authored mask/glove/table contact objects in this slice, but it must not pretend that substitute is the final full-body character asset.

- [x] **Step 3: Add evidence-equivalent accessibility behavior**

When captions, reduced motion, vibration off, or mono audio are enabled, preserve at least two readable channels using caption facts, card motion, contrast pulse, and focus log. Never make a correct Read depend on color alone, hearing alone, or a sub-200ms animation.

- [x] **Step 4: Prove all four truth/observation combinations**

Run: `npm run unity:scene:sync && node scripts/unity-cli.mjs test && node scripts/unity-cli.mjs playtest`

Expected: all combinations green and hidden truth absent from pre-resolution player APIs.

Current proof: all four combinations pass and the presenter API accepts no hidden-truth or snapshot
input. The disconnected rehearsal glove failed direct visual review and is intentionally hidden;
full hand-motion evidence remains blocked on the licensed Aldric rig. Card-contact/contrast plus the
observed-fact log remain the two truthful shipping channels in this interim slice.

### Task 6: Build the optional focus view and table HUD

**Files:**
- Create: `unity/scenes/parlor/Runtime/GmParlorFocusView.cs`
- Create: `unity/scenes/parlor/Runtime/GmParlorHud.cs`
- Create: `unity/scenes/parlor/Tests/GmParlorFocusViewTests.cs`
- Create: `unity/project/Assets/Tests/PlayMode/GmParlorFocusViewPlayModeTests.cs`
- Modify: `unity/scenes/parlor/Editor/GmParlorBuilder.cs`

- [x] **Step 1: Write view-model tests before UI code**

Assert focus view and diegetic view expose identical playable card indices, card labels, lead/follow state, score, Read availability, and observed evidence. Assert the focus view cannot mutate rules directly and cannot expose Aldric’s hidden hand except during the canonical Eyes reveal.

- [x] **Step 2: Capture red**

Run: `npm run unity:scene:sync && node scripts/unity-cli.mjs test`

Expected: missing focus-view types.

- [x] **Step 3: Implement UI Toolkit presentation**

Use the project typography and pause-menu accessibility settings. Support keyboard, mouse, and controller focus; scalable text; high-contrast focus; reduced motion; captions; and clear legal/illegal feedback. Keep the normal screen sparse: phase, round/trick score, current action, Read prompt, and the last few observed facts.

- [x] **Step 4: Run UI and PlayMode gates**

Run: `npm run unity:scene:sync && node scripts/unity-cli.mjs test && node scripts/unity-cli.mjs playtest`

Expected: full green with equivalent legal choices in both modes.

### Task 7: Prove persistence reconstructs presentation without replaying effects

**Files:**
- Create: `unity/scenes/parlor/Tests/GmParlorPresentationRestoreTests.cs`
- Create: `unity/project/Assets/Tests/PlayMode/GmParlorRestorePlayModeTests.cs`
- Modify: `unity/scenes/parlor/Runtime/GmParlorController.cs`
- Modify: `unity/scenes/parlor/Runtime/GmParlorPropBinder.cs`
- Modify: `unity/scenes/parlor/Runtime/GmParlorPresentationCoordinator.cs`

- [x] **Step 1: Write kill/reload reds at every visible phase**

Cover player lead, Aldric lead, judgement with honest play, judgement with cheat, trick result, round result, match result, and deferred Read teaching. Export, destroy scene objects, restore through `GmRunStore` and `GmSaveSystem`, recreate the controller, then compare canonical logical poses and visible evidence.

Assert no outcome event, room completion, catch, penalty, sound, haptic, or evidence fact is delivered twice.

- [x] **Step 2: Capture red**

Run: `npm run unity:scene:sync && node scripts/unity-cli.mjs test`

Expected: restore tests fail because presentation reconstruction is not yet explicit.

- [x] **Step 3: Add restore-only snap and explicit activation**

On Awake: restore rules, bind physical state, clear stale commands, and remain input-closed. On explicit Start/activation: deliver any durable pending outcome through `GmParlorRules`, build only forward commands, then open input. Never replay an already acknowledged animation or effect.

- [x] **Step 4: Prove EditMode and real PlayMode lifecycle**

Run: `npm run unity:scene:sync && node scripts/unity-cli.mjs test && node scripts/unity-cli.mjs playtest`

Expected: full green, exact physical reconstruction, one-shot outcomes.

Current proof (2026-08-18): EditMode **944/944**, PlayMode **44/44**, tour restore **firstDelta=0 secondDelta=0 cue=0**. Built-player 1080p is Task 9 Step 4.

### Task 8: Add deterministic review automation and adversarial input proof

**Files:**
- Create: `unity/scenes/parlor/Runtime/GmParlorReviewProbe.cs`
- Create: `unity/scenes/parlor/Tests/GmParlorReviewProbeTests.cs`
- Modify: `unity/scenes/parlor/Runtime/GmParlorShotTour.cs`
- Modify: `scripts/unity-cli.mjs`
- Modify: `unity/scene-system/scene-registry.json`

- [x] **Step 1: Write probe contract tests**

The probe must drive public controller intent, not mutate match internals. Freeze a seed matrix that contains honest, cheated, true-tell, false-tell, Eyes, Teeth, Bones, Flames, player win, Aldric win, rematch, and restore cases.

Add held-input and simultaneous-input adversaries: held D-pad, Interact+CallTell same frame, Cancel during each motion phase, Pause during judgement, controller disconnect/reconnect, and quit from focus view.

- [x] **Step 2: Capture red and implement the probe**

Run: `npm run unity:scene:sync && node scripts/unity-cli.mjs test`

Expected red: missing probe type and CLI command.

- [x] **Step 3: Extend the Parlor tour**

Add player view, Aldric hands, player hand, lead/follow cards, true suspicious tell, false suspicious tell, focus view, result state, and restore state shots. Each shot must identify its semantic state in the report.

- [x] **Step 4: Run automation**

Run:

```bash
npm run unity:scene:sync
node scripts/unity-cli.mjs test
node scripts/unity-cli.mjs playtest
node scripts/unity-cli.mjs rebuild parlor
node scripts/unity-cli.mjs audit-saved parlor
node scripts/unity-cli.mjs tour parlor
```

Expected: all suites green, build/audit/tour pass, complete seed matrix reported.

Current proof (2026-08-18): EditMode 944/944, PlayMode 44/44, `[GmParlor] BUILD PASS`, `[GmEstateAudit] SAVED PASS`, tour **24/24 first attempt**. Tour isolation: each case clears the evidence log before staging.

### Task 9: Visual, accessibility, allocation, and frame-budget review

**Files:**
- Create: `unity/scenes/parlor/Editor/GmParlorPresentationAudit.cs`
- Create: `unity/scenes/parlor/Tests/GmParlorPresentationAuditTests.cs`
- Modify: `unity/scenes/parlor/Editor/GmParlorQualityAudit.cs`
- Modify: `unity/scenes/parlor/Runtime/GmParlorReviewProbe.cs`
- Evidence only: `unity-project/Library/GmSceneIntelligence/**/parlor/**`

- [x] **Step 1: Write measurable audit reds**

Audit physical card count/identity, focus collider bounds, legibility distance, card/table penetration, hand/table clearances, command recovery, reduced-motion support, evidence-channel count, and missing presenter bindings.

- [x] **Step 2: Capture red, implement, and rebuild**

Run: `npm run unity:scene:sync && node scripts/unity-cli.mjs test`

Expected red: missing presentation audit.

- [x] **Step 3: Capture and inspect frames directly**

Run: `node scripts/unity-cli.mjs tour parlor`

Open every resulting frame. Reject buried cards, unreadable ranks, clipping sleeves, floating contact, weak focus, UI collisions, over-dark evidence, repeated-looking tells, and camera compositions that hide the decision object.

Current proof (2026-08-18): tour 24/24 first attempt after evidence isolation. Inspected frames under `unity-project/Screens/Parlor/` and copies in `docs/playtest/screenshots/parlor-tour-*.png`. Aldric is still the substitute proxy. True and false suspicious tells use the same caption vocabulary on purpose. Built-player 1080p is the next step, not this one.

- [ ] **Step 4: Profile built-player presentation**

Build the macOS player and run the Parlor probe at 1920x1080 HDRP. Require total p95 below 16.7ms, main thread at or below 10ms, GPU at or below 13.5ms, p99 below 20ms, no non-load frame above 33.3ms, and zero steady-state managed allocation.

The existing standalone probe freezes a 34ms p95 gate. The Aug 14 `Builds/macOS-Game` app is stale and is not evidence for this slice.

- [x] **Step 5: Run accessibility matrix**

Repeat the deterministic proof with reduced motion, captions, vibration off, mono audio, high contrast, and focus view. Legal choices and evidence must remain equivalent.

### Task 10: Hold and close the Aldric character-art gate honestly

**Files:**
- Create when asset is selected: `docs/art/ALDRIC-ASSET-REVIEW.md`
- Modify after import: `unity/scenes/parlor/Editor/GmParlorBuilder.cs`
- Modify after import: `unity/scenes/parlor/Runtime/GmParlorAldricPresenter.cs`
- Add after import: authored Animator Controller, Avatar Mask, clips, and materials under `unity-project/Assets/GamesMaster/Characters/Aldric/`

- [ ] **Step 1: Select a legally usable rig before import**

Acceptance requires a period-appropriate adult male base with documented redistribution/license terms, facial or mask compatibility, separated fingers, clean humanoid avatar mapping, LOD support or a defensible close-room budget, and source files retained. Record vendor, product, version, license, polycount, material count, texture resolution, blend shapes, rig map, and rejected alternatives.

- [ ] **Step 2: Author the required motion set**

Create bespoke clips for seated idle variants, clean lead, clean follow, vulnerable hesitation, false-tell hesitation, palm/sleeve cheat, impossible-eight reveal, caught reaction, false-accusation reaction, round win/loss, match win/loss, stand, and exit. Use upper-body masks plus hand IK for card contact. No stock clip may ship without retarget, contact, timing, and silhouette review.

- [ ] **Step 3: Bind semantic cues, never rules truth**

Map `GmParlorAldricPresenter` cues to Animator parameters. Animation events may emit contact audio or secondary effects but may not call match actions, decide a tell, remove a card, award an outcome, or advance the room.

- [ ] **Step 4: Review at gameplay and diagnostic angles**

Inspect player view plus side and overhead diagnostic captures. Reject hand/card separation, table penetration, foot sliding, shoulder collapse, eye-line drift, repeated identical timing, missing anticipation, and reactions that reveal hidden truth before judgement.

- [ ] **Step 5: Close the gate only with evidence**

The Parlor presentation may be called mechanically complete before this task. It may not be called visually AAA or final until the licensed character, bespoke clips, contacts, LOD/performance, and direct frame review all pass.

## Final regression and closeout

- [ ] Run `npm run unity:scene:sync`.
- [ ] Run `node scripts/unity-cli.mjs test` and require 100% EditMode pass.
- [ ] Run `node scripts/unity-cli.mjs playtest` and require 100% PlayMode pass.
- [ ] Run `npm test` and require every fast and archive suite green.
- [ ] Run `npm run unity:scene:check` and require registry, sync, and package checks green.
- [ ] Run `git diff --check` and compare authored/mirror SHA-256 pairs for every touched governed file.
- [ ] Rebuild, audit, tour, and built-player proof Parlor in separate Unity invocations. Read the log each invocation actually wrote.
- [ ] Inspect every required visual capture directly.
- [ ] Update `docs/TASKBOARD.md`, `docs/MASTER-PLAN.md`, and the current handoff with shipped proof and remaining art gate.
- [ ] Run the prose and UI slop gates on player-facing copy and UI before shipping.
- [ ] Sweep this plan, the approved master spec, and the conversation for anything promised but not shipped. List remaining gaps plainly.

## Execution order decision

Execute inline on `wend-prologue-boundary-navmesh-harnesses`. A separate worktree would omit the uncommitted canonical Parlor engine and persistence changes this slice depends on. Keep a strict file ownership log, avoid unrelated edits, and do not commit or push without explicit authorization.
