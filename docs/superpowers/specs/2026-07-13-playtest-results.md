# Playtest Panel Results — First Full Run

## Overall ratings

| Persona | Rating | Keep playing? |
|---|---|---|
| E. Halloran (Narrative) | 7/10 | Yes — for the Entry Hall writing; endings unverified |
| Vera Koll (Horror devotee) | 6/10 | Yes, conditionally — refuses to grade unbuilt rooms |
| Sam Ng (First-timer) | 6/10 | Yes — hooked by the portraits despite a rough opening |
| Priya Shah (Technical) | 6/10 | Yes, for now — concern is what ships next |
| Ronan Okafor (QA) | 6/10 | **No** |
| Marguerite Ashworth (Lore) | 6/10 | Yes |
| Dex Arroyo (Systems) | 6/10 | Yes, mildly |
| The Skeptic (Commercial) | 4/10 | Yes, on faith not pull |
| Iris Vance (Accessibility) | 3/10 | Yes, but only if Reduce Motion is set before leaving the Prologue |

**Average: 5.6/10.** Nobody hated it; nobody loved it either. The two lowest scores (Iris Vance, The Skeptic) are both about things that are genuinely, verifiably broken or missing right now, not taste calls.

## The two most urgent findings — bugs in what was just shipped and personally verified

These matter most because they're in the car/gate/secret-ending feature that was just built, reviewed, and played through this session — and both slipped past that review.

**The gate lock is narration, not a barrier (Dex Arroyo, high).** The only z-clamp in the walk loop caps retreat at `carZ`, and it never tightens after `triggerGateLock()` fires. A player who keeps holding S after hearing "the lock had already turned" walks straight back through the visibly-shut gate to the car anyway, with nothing stopping them. Five more seconds of the same input that triggered the line disproves it. *Fix: after the lock fires, tighten the clamp to stop retreat near the gate (e.g. `Math.max(this.gateZ+2, ...)`) so it's physically enforced, not just spoken.*

**Retreating while the invitation letter is raised triggers the secret ending invisibly, underneath the letter (Ronan Okafor, high).** The retreat-detection block isn't gated on `state.letterMounted` the way pointer-lock look already is. A player who opens the letter to read it and absent-mindedly holds S gets pulled into the secret ending with zero visual indication — it fires underneath the still-open letter overlay. *Fix: gate the whole movement/retreat block on `!this.state.letterMounted`, matching how look-around already is.*

## Everything else, by severity

### High

- **Mansion exterior is still procedural primitives, not the sourced model** (Vera Koll). The "Haunted Victorian House" model chosen and documented earlier this session in `asset-catalog.html` was never wired into the Prologue — the live scene still builds the mansion from `BoxGeometry`/`ConeGeometry`. *Fix: load `assets/models/exterior/scene.gltf` in place of the primitive mansion group.*
- **Court and the Labyrinth can't be judged for earned dread — they don't exist yet** (Vera Koll). Not a bug, a scope reminder: the cheat-mechanic design reads well on paper but is unplayed.
- **"Nothing behind it" (Replacement ending) doesn't survive without the story bible's own reasoning attached** (E. Halloran). Reads as a cheap "he was always hollow" rug-pull on its own; the bible's "preview of what the chair does to you" reading needs to be *on screen*, not just in the design doc.
- **Every ending payoff being judged is a hypothesis, not a played experience** (E. Halloran) — confirmed: none of the six endings are coded yet, and this makes any verdict on their pacing/delivery provisional.
- **The secret ending triggers by accident within seconds of starting a normal walk** (Sam Ng). The car sits 6 units from spawn; the secret-ending threshold is 3 units of backward movement. Ordinary curiosity ("what's that thing behind me the beat text just mentioned") fires it before a single story beat plays.
- **Reduce Motion doesn't exist past the Prologue** (Iris Vance). Entry Hall never loads `gm-settings.js` at all — the chandelier sway, the wake-up vignette pulse, and the lightning flash all ignore the setting completely, with no Options entry point inside that scene to fix it mid-play.
- **The commercial pitch collapses into "it's Inscryption" before the one real twist reveals itself** (The Skeptic) — hours into a run, long after a trailer or store page would already be judged and dismissed.
- **The first ten minutes has no hook, by design** (The Skeptic) — verified against the actual shipped Parlor text: Read is locked behind `suspicion>=2`, so a first run guarantees losses with no tool to respond, exactly where browser audiences bounce.
- **Nine unbuilt phases against three shipped scenes** (The Skeptic) — confirmed the only `localStorage` use in the whole codebase is a camera-bob toggle; "the house remembers," the design's marquee hook, doesn't survive a page reload today.
- **`old_car.glb` is a single point of failure with no fallback** (Priya Shah) — unlike the gate, which already has a procedural stand-in, a failed car load leaves a bare gap with only a silent `console.warn`.
- **Read's tell is a deterministic, not probabilistic, proxy for the hidden cheat flag** (Dex Arroyo) — once a player learns "border glow = cheat," the entire risk the flavor text promises ("you can't prove it, yet") evaporates; the catch rate becomes ~100% with zero real gamble.
- **Corruption is a one-way odometer, not a feedback loop** (Dex Arroyo) — every code path that increments it fires regardless of whether the player calls Read correctly, incorrectly, Stands, or Continues; no player choice ever steers it.
- **"The house knew that before he did" (Art Direction) contradicts the story bible's own indiscriminate-invitation thesis** (Marguerite Ashworth) — and the new secret ending doubles down on the indiscriminate reading, widening a gap between primary canon and the document that claims to fully align with it.

### Medium

- Thesis ending has no plumbing yet — `cheatsCaught` is Parlor-local state, no cross-scene persistence (Vera Koll, Dex Arroyo, both independently).
- The final story bible still describes its own review process in its *opening* paragraph ("four adversarial lenses... revision cycles") — the exact self-narrating problem an earlier tone review flagged, just relocated (E. Halloran).
- The secret-ending epilogue's closing line echoes the story bible's own thesis-statement prose almost clause-for-clause instead of staying in the character's concrete voice (E. Halloran).
- No way to reach Options once inside the walk phase — only accessible from the main menu (Sam Ng).
- Running outpaces the narrative beats; holding Shift can blow past 3 of 5 beats before their subtext finishes displaying (Sam Ng).
- Reduce Motion is inconsistent even where it exists — the gate-lock's rotation animation isn't gated on it at all (Iris Vance).
- The Text Size control is fully wired through state and UI but changes zero actual font sizes anywhere (Iris Vance).
- A full session's worth of commits went to a branch the roadmap never prioritized (car/gate/secret-ending), while Court — the roadmap's own "cheapest, build first" pick — has zero commits (The Skeptic).
- Every playtest judgment on record, including this entire panel, comes from AI reviewing AI-written design docs — no real human has played this build yet (The Skeptic).
- `three.min.js`'s CDN script tag has no SRI hash, while `GLTFLoader.js`'s does (Priya Shah).
- No loading state or CDN-failure handling anywhere — a slow or blocked CDN leaves a silent black screen (Priya Shah).
- 376MB of sourced exterior/fence textures are full-resolution and unoptimized, not yet wired into any scene (Priya Shah).
- `triggerSecretEnding()` never clears `isWalk`, so the dev-bridge phase readout still reports "walk" during the secret ending (Ronan Okafor).
- `devSnapshot()`'s phase shows "?" during the entire arrival/knockout cinematic and during Options, even in ordinary play (Ronan Okafor).
- `triggerGateLock()` has no internal re-entry guard — calling it twice orphans a `requestAnimationFrame` chain the stored ID can't reach (Ronan Okafor).
- Gate-lock narration/audio can fire before the gate model finishes loading, permanently desyncing the visual from the story for the rest of the session (Ronan Okafor).
- The secret ending is a 7th terminal state with zero footprint in any canon document — the story bible's "Six Endings" section doesn't acknowledge it exists (Marguerite Ashworth).
- The reactive-only compulsion — the game's actual thesis — never surfaces on screen; a player can't distinguish "he had no legal play" from "he just cheated" from play alone (Dex Arroyo).
- 8+ cheats-caught isn't provably reachable — Parlor alone hitting it "isn't fully proven without an actual playtest," and the other two contributing rooms don't exist (Dex Arroyo).

### Low

- The gate-lock beat is well-written but is one of the genre's most reused images, with nothing tying it specifically to Aldric's own mythology (Vera Koll).
- Imogen Thale's portrait is the one of nine that lands no concrete detail, reading as underwritten next to her neighbors (E. Halloran).
- The gate-lock is only audible, not visually confirmed, if the player is facing away when it fires (Sam Ng).
- Menu → cold open → invitation is three passive screens before any interactivity (Sam Ng).
- The Music volume slider is fully wired but controls a system that doesn't exist yet (Iris Vance).
- The WebGL context ceiling found once this session (in the asset catalog page) isn't architecturally documented as a constraint anywhere in the actual game (Priya Shah).
- `devGoTo('door'/'knockout')` silently drops the camera teleport if called before `this.cam` exists (Ronan Okafor).
- Art Direction's own early "The Debt" ending sketch is silently superseded by the canonical six, unaddressed in the final story bible, risking future confusion with the new secret ending (Marguerite Ashworth).
- Shut the Box's hidden-door gate risks becoming a patience-grind rather than a genuine skill check, if its corruption tracking follows Parlor's same one-way-counter precedent (Dex Arroyo).

## What's genuinely working, across multiple independent reviewers

- The reactive-only cheat rule is real, load-bearing code (`gmFollow()`'s `canWinLegal()` check), not narration bolted onto free RNG — confirmed independently by Dex Arroyo and referenced by Vera Koll.
- The portrait/ledger writing in the Entry Hall is the strongest material in the build by a clear margin — praised independently by E. Halloran, Sam Ng, and Vera Koll.
- The Percival retirement, the Mirror/mirrors naming discipline, and the reactive-only escalation rule all survived the whole redesign process without regression — confirmed by Marguerite Ashworth's direct cross-referencing.
- The secret ending's core idea and prose (before the one line E. Halloran flagged) is thematically load-bearing, not a cute gimmick — praised independently by The Skeptic and Vera Koll.
- 27 real, licensed assets are actually on disk, not a wishlist — confirmed by Priya Shah and The Skeptic independently checking the filesystem.
