# Codex Takeover — Review, Fix, Full Project Plan

> **For agentic workers:** REQUIRED: Read `docs/CODEX-HANDOFF.md` first. Execute Part 1 before Part 2. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Codex cold-reviews the repo against canon and screenshots, fixes agent-owned opening defects, then owns the phased build through endings without violating Nick/buy/git locks.

**Architecture:** Three.js single-file `.dc.html` scenes + small shared `gm-*.js` modules; Playwright harnesses for live walk/visual proof; `docs/PROGRESS.md` as source of truth for %. No bundler.

**Tech Stack:** Three.js (browser globals), Playwright, Node test scripts, local static serve, Unity→GLB assets under `assets/models/unity/`.

**Related canon:**  
`docs/superpowers/specs/2026-07-14-opening-threshold.md` · house-history · `docs/superpowers/plans/2026-07-12-build-roadmap.md` · court/STB plan · `docs/NICK-NEEDED.md`

---

## Part 0 — File ownership (don’t sprawl)

| Responsibility | Primary files |
|----------------|---------------|
| Opening exterior + Threshold Refusal | `The Games Master - Prologue.dc.html` |
| Wake / hall | `The Games Master - Entry Hall.dc.html` |
| Parlor loop | `The Games Master - Parlor.dc.html` |
| Court shell → full | `The Games Master - Court.dc.html`, `gm-court-props.js` |
| STB shell → full | `The Games Master - Shut the Box.dc.html`, `gm-shutbox-logic.js`, `gm-shutbox-board.js` |
| Shared meters | `gm-doubt.js`, `gm-settings.js`, `gm-progress.js` |
| Assets / credit | `assets/sfx/license.txt`, `asset-catalog.html` |
| Proof | `scripts/play-*.mjs`, `docs/playtest/screenshots/`, `docs/PROGRESS.md` |

---

## Part 1 — Review + fix opening (before Nick buy / Court)

### Task 1: Cold review audit (no code)

**Files:**
- Read: `docs/CODEX-HANDOFF.md`, `docs/PROGRESS.md`, `docs/NICK-NEEDED.md`
- Create: `docs/playtest/codex-review-YYYY-MM-DD.md` (today’s date)

- [ ] **Step 1:** Read hard rules + Threshold Refusal + last 3 PROGRESS CHECKINs
- [ ] **Step 2:** Run `npm test` — expect 23 + 47 pass
- [ ] **Step 3:** Run `node scripts/play-door.mjs` — expect `errors:0`, phase aftermath
- [ ] **Step 4:** Run `node scripts/play-full.mjs` or `play-gate.mjs` — expect gate lock + no pageerrors
- [ ] **Step 5:** Open and **visually inspect** door + outgate PNGs; note mismatches vs canon
- [ ] **Step 6:** Write audit table:

| ID | Finding | Severity (BLOCKING/POLISH/BUY/NICK/WONTFIX) | Evidence (path or cmd) | Fix owner |
|----|---------|-----------------------------------------------|------------------------|-----------|
| … | … | … | … | … |

- [ ] **Step 7:** Stop and report the table to Nick (or proceed if he said “fix without asking”)

### Task 2: Fix harness / runtime blockers

**Files:**
- Modify: whatever `pageerror` / failed wait points at (usually Prologue)
- Test: re-run the failing harness

- [ ] **Step 1:** Repro each BLOCKING item from Task 1
- [ ] **Step 2:** Minimal fix (no prologue rewrite)
- [ ] **Step 3:** Re-run failing script + `npm test`
- [ ] **Step 4:** Save new shots if UI changed

### Task 3: Opening visual residuals (agent-owned)

**Files:**
- Modify: `The Games Master - Prologue.dc.html` (search keys below)
- Test: `play-door.mjs`, outgate capture script or `play-full.mjs`

Known search terms:

| Issue class | Search |
|-------------|--------|
| Glow orbs | `SphereGeometry`, `glow`, pier lamps |
| Porch dress | `_mountEnvPorchLamps`, `_buildPorchSideDark`, `palePorch` |
| Doors | `_mountOwnedFrontDoors`, `_dressDoorPanelOverlays`, `doorFaceTex` |
| Trees | `_mountOwnedDeadTrees`, `buildRuinsGrounds`, `DeadTree_` |
| Bushes green | `Bush_2x2`, foliage `map` |
| Env fence | `_dressEnvOuterWalls`, `web/SM_Fence` |

- [ ] **Step 1:** Kill any returned floating MeshBasic light orbs (pier/drive/porch)
- [ ] **Step 2:** Confirm doors shut through KO; panel overlays readable; no door swing
- [ ] **Step 3:** Confirm owned trees still mount (`_ownedDeadTreesReady`); darkness holds (fog:false MeshBasic)
- [ ] **Step 4:** Outgate + porch re-shot; read PNGs yourself
- [ ] **Step 5:** Log verdict in PROGRESS CHECKIN (PASS / BORDERLINE / FAIL)

**Do not:** buy packs in this task. Flag BUY items for Nick.

### Task 4: Docs truth sync

**Files:**
- Modify: `docs/PROGRESS.md`, `docs/NICK-NEEDED.md` if stale
- Modify: `assets/sfx/license.txt` only if you change SFX files

- [ ] **Step 1:** Align “what’s done” with harness evidence
- [ ] **Step 2:** Keep buy guidance: walk first → trees maybe → no mansion
- [ ] **Step 3:** Leave Nick Phase 0 walk checkbox open until he reports

### Task 5: Hand to Nick for Phase 0 walk

- [ ] **Step 1:** Tell Nick exactly what to play (spawn look-back → drive → gate → porch KO → Hall)
- [ ] **Step 2:** Ask for: mood yes/no · trees buy yes/no · Threshold Refusal length · car vibe
- [ ] **Step 3:** After notes, file as CHECKIN; only then buy/implement tree pack if he says yes

**Gate out of Part 1:** Nick walk notes logged OR Nick explicit waiver to continue Court.

---

## Part 2 — Full project takeover (after Part 1 gate)

### Task 6: Court to first playable pressure

**Files:**
- Modify: `The Games Master - Court.dc.html`, `gm-court-props.js`
- Plan detail: `docs/superpowers/plans/2026-07-14-court-and-shut-the-box.md`
- Evidence copy: `docs/superpowers/specs/2026-07-14-court-evidence-draft.md`
- Test: extend harness / `npm test`; screenshots under `docs/playtest/screenshots/`

- [ ] **Step 1:** Inventory shell vs missing (role lights, pressure clock, loseable first visit)
- [ ] **Step 2:** Wire evidence 1–5 resolve path with real lose chance
- [ ] **Step 3:** Gavel tarnish RAF already exists — verify live, don’t rewrite
- [ ] **Step 4:** Modular furniture dress only if GLBs already local (Table_Large / Chair_1)
- [ ] **Step 5:** Visual gate on Court walk + one full lose + one win path
- [ ] **Step 6:** PROGRESS update

### Task 7: Shut the Box to full match

**Files:**
- Modify: `The Games Master - Shut the Box.dc.html`, `gm-shutbox-board.js`
- Keep pure rules in `gm-shutbox-logic.js` + `scripts/test-shutbox.mjs` (TDD)
- Test: add logic tests first for new rules; then scene harness

- [ ] **Step 1:** List missing vs plan (AI opponent, Hold cheats, tile-9 door)
- [ ] **Step 2:** TDD any new `gm-shutbox-logic` behavior
- [ ] **Step 3:** Wire Hold/cheat tallies on screen
- [ ] **Step 4:** Tile-9 Hold → door flag only under correct condition
- [ ] **Step 5:** Visual + logic gate; Nick play + AI panel after Phase 2 (cadence rule)

### Task 8: Shards (Phase 3)

**Files:** Entry Hall + Court + (later) Labyrinth; shared counter stub → Phase 5 store

- [ ] **Step 1:** Percival portrait mirror shard examine in Entry Hall
- [ ] **Step 2:** Court evidence-card shard once Court exists
- [ ] **Step 3:** Stub shard count that survives scene hop (local ok until Phase 5)

### Task 9: Hidden room (Phase 4)

**Depends:** STB tile-9 gate

- [ ] **Step 1:** Small chamber scene + door in STB hall wall
- [ ] **Step 2:** Static invite + short journal fragments (story bible §5 — incomplete on purpose)
- [ ] **Step 3:** Set `hiddenRoomFound` flag for true ending

### Task 10: Persistence (Phase 5)

**Files:** `gm-progress.js` (likely), scene consumers

- [ ] **Step 1:** Move `cheatsCaught` / shards / hidden-room to shared save
- [ ] **Step 2:** Harness: reload mid-run, assert survival
- [ ] **Step 3:** Collection-ending stub for ledger name / tenth tile (can be partial)

### Task 11: Labyrinth (Phase 6)

**Depends:** Art Direction “last among rooms”; grey-box first

- [ ] **Step 1:** 7×7 sliding kit grey-box + fuse AI stub
- [ ] **Step 2:** Rubber-band rules from roadmap (proximity/fuse only when already strained)
- [ ] **Step 3:** Palette break + no tell-glint HUD
- [ ] **Step 4:** Mirror room shard completes Phase 3

### Task 12: Endings (Phase 7)

**Depends:** Corruption, Sanity, cheatsCaught, shards, hidden room

- [ ] **Step 1:** Wire six endings per story bible
- [ ] **Step 2:** Nick play + AI panel
- [ ] **Step 3:** Screenshot matrix per ending entry

### Task 13: Manor puzzles (Phase 8) — deferrable

- [ ] **Step 1:** Only after endings scaffold or Nick prioritizes
- [ ] **Step 2:** Keep scope small; no new mansion buy

### Task 14: Final polish (Phase 9)

- [ ] **Step 1:** Full opening + Court + STB + one ending path visual pass
- [ ] **Step 2:** Nick play + AI panel + screenshot pass
- [ ] **Step 3:** Catalog/credits honesty pass (`asset-catalog.html`, `license.txt`, CREDITS)

---

## Part 3 — Standing operating loop (every Codex session)

```
1. Read PROGRESS + NICK-NEEDED (2 min)
2. State checkpoint to Nick
3. Do the smallest next checkbox that is AGENT-owned
4. npm test + relevant play-*.mjs + read new screenshots
5. Update PROGRESS CHECKIN
6. Ask before: buy · commit · push · reopen Threshold Refusal · declare Court “done”
```

### Buy decision matrix (do not invent new cart)

| Ask Nick | Only if |
|----------|---------|
| Dead Tree Pack ~$15–25 | Opening grounds still kit-cheap **after** his walk + owned BotD/Polytope |
| Cemetery props ~$10–20 | Tombs thin after trees decision |
| Ground/weed | Prefer Poly Haven free first |
| Anything mansion-shaped | **No** |

### Done definitions

| Claim | Requires |
|-------|----------|
| “Opening fixed” | Harness green + screenshots you read + Nick walk or waiver |
| “Court playable” | Loseable first visit + visual gate + Nick not blocked on opening |
| “Phase 2 done” | STB match + tile-9 gate + Nick play + AI panel |
| “Ready to push” | Nick said push |

---

## Appendix A — Pasteable Codex kickoff

```
Repo: ~/Projects/the-games-master
Read docs/CODEX-HANDOFF.md then docs/superpowers/plans/2026-07-15-codex-takeover.md.
Start at Part 1 Task 1 (cold review, no code until audit exists).
Rules: no push; no second mansion; Threshold Refusal closed doors; visual gate;
ask Nick before buys/commits. Prefer surgical Prologue edits.
```

## Appendix B — What prior agent already shipped (do not redo blindly)

- Threshold Refusal lengthened; `ko_thud` + authored gate SFX; porch side-dark
- Door_Double + panel overlays; Env porch lamps without glow spheres
- Env fence LOD0 outer walls; SM_Wall_Door rejected
- Owned trees: BotD hero + Polytope fill (`_mountOwnedDeadTrees`)
- Car HD 03; Court/STB shells; STB logic 23 tests
- Catalog `#buy-next` no-mansion lock

Re-verify with eyes + harness; don’t assume bit-rot free.
