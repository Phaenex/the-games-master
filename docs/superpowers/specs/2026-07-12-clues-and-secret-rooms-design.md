# Clue Placement & Secret Rooms Design

## Status

Draft 1. Builds directly on `2026-07-12-aldric-mythology-story-bible.md` (Draft 3, §8 — the hidden room, the shards, the puzzle) and `2026-07-12-per-game-cheats-design.md` (Shut the Box §1.5 — the tile-9 unlock). Not yet adversarially reviewed — folded into the Task 24 final review.

## What this document has to reconcile

Two documents each proposed a piece of the hidden-room puzzle without fully wiring them together: the mythology said finding the room comes from reading all nine portraits and the ledger and noticing a pattern; the per-game cheats doc said the mechanical gate is cleanly shutting tile 9 in Shut the Box. These aren't competing answers — one is the mechanical gate, the other is the reason a player would know to aim for it. This document makes that explicit, places the three shards, and separates two objects the mythology already flagged as easy to conflate: the player's own invitation letter (already fully designed, three-stage reveal) and Aldric's original invitation, a different, older physical object that only exists in the hidden room.

---

## 1. The clue economy — what's already in place, mapped in one table

| Room | Discovery type | Feeds into | Status |
|---|---|---|---|
| Entry Hall | 9 portrait POIs + ledger, 5 of 9 carry a mechanical or lore clue | Court's jury tie-in; the tile-order pattern in Shut the Box; general atmosphere/dread | Shipped (mansion-intro-polish pass) |
| Parlor | Read (catch a hand an honest opponent couldn't win) | `cheatsCaught` | Shipped |
| Court | Present the true Evidence Card → gavel tarnishes | `cheatsCaught` (once persistence is fixed, see per-game-cheats §1.4) | Designed, not built |
| Shut the Box | Hold (force a recount against tracked state) | `cheatsCaught`; clean tile-9 shut → hidden room | Designed, not built |
| Labyrinth | Noticing fuse/distance irregularities — atmosphere-native, not scored | A shard (see §3); does *not* feed `cheatsCaught`, by design (per-game-cheats §3.2–3.3) | Designed, not built |
| Hidden room | Aldric's original invitation + journal | Confirms the bargain in his own hand; not a puzzle to solve, a reward for having solved one | New, this document |

The reason Labyrinth doesn't carry a `cheatsCaught` clue while every other playable room does is deliberate, not a gap — restated from per-game-cheats §3.3 so this table doesn't read as inconsistent on its own.

## 2. The escalation curve — clue density should climb, not stay flat

Matching the mythology's corrected §5 (visibility gets sloppier at higher corruption, not just frequency), clue *legibility* should climb the same way across the game's run, independent of which room it's in:

- **Early (Entry Hall, first Parlor sessions):** clues are ambiguous, deniable, the kind a first-time player might not register as clues at all — Edwin Marr's portrait tell, the ledger's understated "every one crossed neatly through." Nothing here insists on being noticed.
- **Middle (Court, mid-corruption Parlor):** clues get a visible tell attached (gavel tarnish, IRRITATED-state gold flicker) — still requires the player to be looking, but the game meets them partway.
- **Late (Shut the Box, high-corruption Parlor, Labyrinth):** clues are close to overt — a reopened tile, a fuse burning visibly wrong, a compulsion barely held. By this point the game isn't hiding it so much as the player is deciding whether to act on what's now obvious.

This isn't a new system — it's the corrected §5 escalation applied consistently to clue design instead of only to cheat behavior, so the two escalate together instead of clue difficulty being flat while cheat visibility climbs.

## 3. The three shards — locations, and why each one

Per the mythology (§8), 3+ shards of a broken mirror are required for the true ending, tying to the secret Mirror game and the "face resolves into the player's own face" ending image. Locations, chosen so each shard sits somewhere its own room's thesis already points to it, rather than being generic collectibles dropped anywhere:

1. **Entry Hall — behind Percival's portrait.** A new examine point, easy to miss: a sliver of mirror wedged into the frame's backing, findable only by examining the portrait a second time after the ledger has already been read (so the "worn nameplate, someone kept touching it" detail is fresh when the player finds a reason someone might have been reaching behind the frame, not just at it). Does not reopen or change the retired Percival theory — this is a physical object placed near his portrait, not a claim about who he is.
2. **Court — among the Evidence Cards.** One of the exhibits, examined closely, turns out to be a broken mirror shard mounted and mislabeled as something else — evidence of the trial's own staged, planted nature (Court's existing "breadcrumbs he planted" premise), hiding in plain sight as a piece of "evidence" for a case that was never really about guilt.
3. **Labyrinth — in one of the maze's mirror rooms.** The room already has "the only mirrors in the game" as its Identity Hook; a shard here is the least-invented placement in this whole document — it's simply what a broken version of a room's own central object already implies. Finding it requires stopping to examine a mirror rather than fleeing the Huntsman past it, which is its own small test of nerve.

A fourth, optional shard can live in the hidden room itself once found (see §4) — not counted toward the 3+ threshold (the room is already the reward, not a place still gating access to itself), but a nice completionist beat for a second playthrough.

## 4. The hidden room

**What it is:** Aldric's own original chamber — sealed since before he became host, per the mythology's §8, "forgotten by the house itself in the way old wounds get walled over." Physically, the door is built into the same room Shut the Box is staged in (the old Gallery hall) — disguised as wall paneling, unremarkable until it isn't.

**The gate:** cleanly shutting tile 9 (Percival's tile) in a game of Shut the Box where the loaded-dice bias against it didn't determine the outcome — per per-game-cheats §1.5, this is rare by construction, which is exactly the quality Gallery's old portrait-match gate had.

**Why reading the portraits and ledger still matters, even though they're not a second hard gate:** the mythology's original instinct (§8, Draft 1) was that reading all nine portraits and the ledger hands the player the raw material for a pattern. That's true, but it's not a separate lock — it's what turns the mechanical gate from a fluke into something the player understands they're aiming for. A player who's never read a single portrait can still get lucky and shut tile 9 clean; the door still opens for them, because the house doesn't check homework. But a player who's read Percival's worn nameplate, then the ledger's matching worn ninth line, then notices tile 9 is the one the dice keep steering them away from, is the player for whom this lands as a discovery rather than a coincidence. The design intentionally doesn't force this sequencing — it rewards attention without punishing luck.

**What's inside:**
- **Aldric's original invitation** — a different, older object from the player's own invitation letter. This one is his: aged, in a hand that isn't his current one (younger, less controlled), proof he was a guest before he was a host. This does not use the word "friend" anywhere on it, precisely so it can't be confused with the player's own letter or with "the friend in the walls" — a deliberate choice per the mythology's §9 house style note about never letting the two "friend" devices blur together. Calling this object anything other than "his invitation" or "his own letter" in future writing would risk recreating exactly the collision that note exists to prevent.
- **A journal in his own hand** — chronicling the escalation described in the mythology's §5 firsthand: the near-miss, the guilt, the first small cheat, and the slow calcification into what the player has been fighting all game. This is where the corrected §5 (frequency/margin/visibility all climbing together, not a threat-free reflex) gets to be shown in his own words rather than only inferred from mechanics — a firsthand account is a better delivery method for this history than any dialogue dump would be.

**What it is not:** a second location for the letter's three-stage "he is the friend" reveal (§18) — that device lives entirely with the player's own invitation letter, tracked by doubt/Sanity state, and never needs to be duplicated or explained here. The hidden room's letter and the player's own letter should never appear in the same POI or examine text without being clearly distinguished by name ("his own, older letter" vs. "your own invitation") — same rule as the "friend" collision, applied to a second object that could suffer the same fate if a future pass isn't careful.

## 5. What this leaves genuinely open

- **Exact journal prose** — not written here. This document establishes what the journal is for and what it must not contradict; the actual in-fiction text is a writing task downstream of this design, not a design decision itself.
- **Whether a fourth, optional shard should exist inside the hidden room** — flagged as a nice-to-have in §3, not committed to.
- **The `cheatsCaught` persistence fix** (Parlor session-local → shared/save state) — restated from per-game-cheats §1.4 because it blocks Shut the Box and Court's catches from counting toward the true ending, not just Shut the Box's. This is an engineering task for whenever these rooms move from design to implementation, not something this document can resolve on paper.
