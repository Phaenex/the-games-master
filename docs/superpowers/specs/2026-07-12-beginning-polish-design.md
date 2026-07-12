# Beginning Polish: Options, the Letter's Doubt Reveal, and the Post-Wake Beat

## Context

An audit of the actual shipped Prologue/Entry Hall files (not the design docs — the real code) found three genuine gaps in "the beginning" of the game, confirmed and scoped with the user directly:

1. **No real Options screen.** A single "Head-bob" toggle sits inline on the main menu. No audio, no controls reference, no accessibility settings.
2. **The invitation letter has no doubt-based reveal.** It's a flip-once prop in Prologue only; its back face literally says *"the back is bare — though the paper holds a faint impression, as if something waits to be written"* — a comment in the code even says *"blank on the walk — the clue only surfaces inside the house,"* meaning this was clearly intended to do more and never finished. There's no way to re-examine the letter in Entry Hall or Parlor at all — no inventory system exists past Prologue.
3. **Nothing scripted happens right after standing up** in the Entry Hall wake sequence — `walkEnabled` flips true and the player is released into free-roam with zero directed first moment.

This spec covers building all three. Everything else audited (the cold-open lines, the 5 walk-to-mansion beats, the 3 wake-up beats, the letter's front-face text) was checked and found genuinely well-written — none of it is being touched.

## 1. A minimal shared "doubt" store

The letter's stage 3 needs to know the player is near the true ending, which lives in Parlor — a different page entirely. This requires the first real piece of cross-scene persistence in the codebase (the only existing precedent is a `gm_bob` camera toggle in `localStorage`).

**Scope, deliberately narrow:** this is not the full persistence system the build roadmap's Phase 5 describes (ledger growth, shard tracking, `cheatsCaught` unification). It's a single new module, `gm-doubt.js`, exposing:

```js
window.GMDoubt = {
  get(){ try { return parseInt(localStorage.getItem('gm_doubt')||'0', 10); } catch(e){ return 0; } },
  set(stage){ try { localStorage.setItem('gm_doubt', String(Math.max(this.get(), stage))); } catch(e){} }
};
```

Stage is an integer: `0` (blank, default), `1` (stage 2 text — "watch his hands"), `2` (stage 3 text — "he is the friend"). `set()` only ever ratchets forward (`Math.max`), so a stage can't regress if a scene re-triggers it. Every `.dc.html` file that needs it adds one `<script src="./gm-doubt.js"></script>` tag, same pattern as `support.js`.

**When each stage is set:**
- Stage 1→2 (`set(1)`): fires in Entry Hall once the player has read at least 5 of the 9 portraits plus the ledger — reusing counters the POI system already tracks, no new tracking needed.
- Stage 2→3 (`set(2)`): fires in Parlor when `cheatsCaught >= 4` (the same threshold the game already uses internally for its `VULNERABLE` gm-state transition — reusing an existing signal rather than inventing a new one).

## 2. The letter's three-stage back face

**Where it's viewable:**
- **Prologue** (already built) — the existing raise/flip interaction, back face now reads `GMDoubt.get()` on flip instead of showing static text.
- **Entry Hall** (new) — a new `examine`-type POI, "The invitation, folded in your coat" — positioned near the entry doors (where the player first stands after waking), using the same POI pattern as the portraits. Opens the same flip-card visual Prologue uses (the component gets copied/adapted, not reinvented).
- **Parlor** — no interactive re-examine here; Parlor's UI is a fixed card-table layout with no inventory concept, and forcing one in is a bigger, more awkward lift than the payoff justifies. Instead, stage 3 is delivered as a one-time scripted insert: when `cheatsCaught` crosses 4 (the same signal that sets `GMDoubt` to stage 2... wait — stage 3 is set at this threshold per above, so the insert fires at the moment of the *transition* to stage 3, not on every subsequent hand) — a single full-screen beat showing the letter's back face with the stage-3 text, dismissible, appearing once.

**Text per stage** (from the story bible, §7 and Art Direction §18 — not new writing, already-established canon):
- Stage 0 (default): *"the back is bare — though the paper holds a faint impression, as if something waits to be written"* (unchanged, existing text).
- Stage 1: *"He cannot lose… watch his hands. — a friend who got out."*
- Stage 2: *"a friend who never left. The hand is Aldric's own. He wrote the warning. He is the friend."*

## 3. The post-wake beat

**Where:** `The Games Master - Entry Hall.dc.html`, `beginWake()` — after the existing timeline finishes (`T(11400, ...)`, where `walkEnabled` currently flips true), insert one new beat before free-roam actually opens up.

**What:** a single `T(11400, ...)` beat (pushing the existing final timer to `T(15000, ...)`) showing one directed image — reusing the chandelier already modeled in this scene (`addChandelier` or equivalent, confirmed present in `buildPOIs`/scene construction):

> *"The chandelier hung dead still overhead. Then — not a draft, nothing in this house moved on its own — it swayed, once, the way a thing does when someone has just let go of it."*

This matches the Prologue's own foreshadowing (the walk-up glimpse beat already describes "something enormous hanging dead center of it all, swaying very slightly, though no one had touched it") — paying off a detail already planted rather than introducing a new one. After this beat clears (a few seconds, same pacing as the existing wake beats), `walkEnabled` flips true and free-roam begins exactly as it does today.

## 4. The Options screen

**New file or new state?** New `sc-if` block in the Prologue file (`isOptions`), reachable from the main menu (a new "Options" button, replacing the inline Head-bob toggle) and from a small gear icon during gameplay pause — but pausing mid-walk isn't a currently-supported concept (no pause state exists in Prologue's walk mode), so **Options is menu-only for this pass** — reachable from the title screen, not mid-game. Extending it to a pause menu is future scope, not part of this spec.

**Contents:**
- **Audio** — three sliders: Master, Music, SFX (0-100). No actual audio system exists in the codebase yet (confirmed: no `<audio>` tags, no Web Audio usage anywhere) — these sliders are real UI, backed by real `localStorage`-persisted values (`gm_vol_master` etc.), wired to nothing yet since there's no sound to control. This is intentionally honest: the sliders work and persist, but audibly do nothing until a sound system exists. Flagging this rather than faking a sound system that isn't there.
- **Controls reference** — a static read-only list matching what's already shown in the walk HUD's legend (`Click · look · move mouse`, `Arrows turn · WASD walk`, `Space jump · C crouch · F light · E interact`) — same information, just also reachable from the menu instead of only visible in the corner during the walk.
- **Accessibility** — two real, functional toggles: **Reduce motion** (disables the head-bob — this subsumes and replaces the existing inline Head-bob toggle rather than duplicating it — and also disables the Entry Hall wake sequence's pulsing vignette effect, setting `headachePulse` to a static value instead of oscillating) and **Text size** (small/default/large, applied via a CSS custom property multiplier on the existing beat/subtitle text sizes).

**What this explicitly does not include:** a graphics-quality setting (nothing in this codebase has adjustable render quality — all geometry/textures are fixed procedural content, so there's no dial to build) and a controls *remap* (only a controls *reference* — actual key rebinding would mean touching every scene's hardcoded key-check logic, out of scope for this pass).

## Testing

1. `GMDoubt` unit-level check: set/get ratchets forward only, survives a page reload (real `localStorage`, not component state).
2. Prologue: examine the letter at each of the three doubt stages (forcing `GMDoubt.set()` via the dev bridge), confirm the correct back text renders.
3. Entry Hall: confirm the new invitation POI appears, is positioned sensibly near the entry doors, and shows the same three stages correctly.
4. Parlor: force `cheatsCaught` to cross 4 via the dev bridge, confirm the one-time stage-3 insert fires exactly once, not on every subsequent hand.
5. Entry Hall: confirm the new chandelier beat fires after the existing wake sequence and before `walkEnabled` flips true, and that it doesn't clip visually with the "How long had I been under?" beat before it.
6. Options: sliders and toggles persist across a reload; Reduce Motion actually disables head-bob and the wake vignette pulse; Text Size actually changes rendered text size.
7. Full `npm test` run — none of the existing Test Harness assertions should regress; the Test Harness's own POI-count assertions will need updating once more (Entry Hall gains one more examine POI, the same kind of update made during the mansion-intro-polish pass).
