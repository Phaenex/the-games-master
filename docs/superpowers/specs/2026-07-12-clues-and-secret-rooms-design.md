# Clue Placement & Secret Rooms Design

## Status

Draft 2. Revised after the full four-lens review of the complete story package. This pass: removes a fabricated ending quote that had propagated in from the mythology document, fixes a "Mirror" naming collision (the secret eighth game versus the Labyrinth's literal glass mirrors), aligns the hidden-room gate with the per-game-cheats document's revised mechanism (a specific caught cheat, not a statistical inference), and rewrites the hidden room's contents as fragments rather than a full narrated confession, so it doesn't pre-empt the true ending's own payoff for a player who finds it early.

Builds on `2026-07-12-aldric-mythology-story-bible.md` (Draft 4) and `2026-07-12-per-game-cheats-design.md` (Draft 2, §1.6 specifically — the hidden-room gate).

---

## 1. The clue economy

| Room | Discovery type | Feeds into | Status |
|---|---|---|---|
| Entry Hall | 9 portrait POIs + ledger, 5 of 9 carry a mechanical or lore clue | Court's jury dressing; the tile-order pattern in Shut the Box; atmosphere | Shipped |
| Parlor | Read (catch a hand an honest opponent couldn't win) | `cheatsCaught` | Shipped |
| Court | Present the true Evidence Card → gavel tarnishes, once the trial is genuinely rigged (per-game-cheats §2.1) | `cheatsCaught` (once persistence is fixed) | Designed, not built |
| Shut the Box | Hold (check a claimed board state against the live tally) | `cheatsCaught`; a correct Hold catching board-tampering on tile 9 specifically → hidden room | Designed, not built |
| Labyrinth | Noticing fuse/distance irregularities when already in danger — atmosphere-native, not scored | A shard; does not feed `cheatsCaught`, by design | Designed, not built |
| Hidden room | Aldric's original invitation + journal fragments | Confirms the bargain in his own hand, without duplicating the true ending's own reveal | New, this document |

## 2. The escalation curve

Clue legibility climbs across a playthrough the same way cheat visibility does (mythology §4): early clues (Entry Hall, first Parlor sessions) are ambiguous and deniable; middle clues (Court, mid-corruption Parlor) get a visible tell attached; late clues (Shut the Box, high-corruption Parlor, Labyrinth) are close to overt. One curve, applied consistently, rather than a separate rule per room.

## 3. The three shards

Per the mythology (§9, and Art Direction's Mirror-game tie-in), 3+ shards of a broken mirror are required for the true ending. **Naming note, so this doesn't repeat the collision the mythology's §8 already flagged once:** these are literal glass shards, tied to the Labyrinth's own physical mirrors and the "face resolves" imagery associated with them — not the secret eighth game Art Direction calls "the Mirror," which is an unrelated menu-level NG+ system. Nothing below refers to that game mode.

1. **Entry Hall — behind Percival's portrait.** A second examine pass on the portrait, after the ledger has already been read, turns up a mirror sliver wedged into the frame's backing. The "after the ledger" sequencing is intent, not an enforced gate — a player who examines the portrait twice before reading the ledger can find it early, and that's fine; it softens the intended discovery order without breaking anything, since the shard itself doesn't carry information, only atmosphere.
2. **Court — among the Evidence Cards.** One exhibit, examined closely, turns out to be a mirror shard mounted and mislabeled as something else — evidence for a case that was never really about guilt.
3. **Labyrinth — in one of the maze's literal mirror rooms.** The room's own Identity Hook already is a set of physical mirrors; a shard here uses what the room already has rather than inventing new lore.

A fourth, optional shard can live in the hidden room itself for a second playthrough — not counted toward the 3+ threshold, since the room is already the reward by the time a player is inside it.

## 4. The hidden room

**What it is:** Aldric's own original chamber, sealed since before he became host. The door is built into the same hall Shut the Box now occupies — disguised as wall paneling.

**The gate**, aligned with per-game-cheats §1.6: **a correct Hold call that catches board-tampering on tile 9 specifically — Percival's tile.** This is a real, player-perceivable event (the exact moment of the catch), not a statistical inference the player has to trust blindly. Because tampering only appears at Tier 3+ corruption, this isn't available on a first, low-corruption visit to the room — it's found on a later playthrough of Shut the Box, once the escalation has actually climbed that far. That matters for what's inside (below): the player who finds this room has already been through enough of the game's actual cheat-catching to have earned most of the understanding the room might otherwise have to spell out.

**Why reading the portraits and ledger still matters, even though they're not a second hard gate:** a player who's read Percival's worn nameplate and the ledger's matching worn ninth line, and has noticed tile 9 is the one Aldric fights hardest to protect, experiences this discovery differently than a player who backed into it cold. The mechanical gate doesn't check for that reading — the game doesn't check homework — but the moment lands harder for a player who's done it.

**What's inside — kept fragmentary, on purpose:**
- **Aldric's original invitation** — a different, older object from the player's own letter: aged, in a hand that's younger and less controlled than his current one. It doesn't use the word "friend" anywhere on it, keeping it clear of both the letter's three-stage reveal and "the friend in the walls" (mythology §7).
- **A handful of journal fragments, not a full account.** A few short, incomplete entries in his own hand — a line about a near-miss, a line about the first small cheat, one entry that just trails off — gesturing at the arc the mythology's §4 describes rather than narrating it start to finish. The true ending's own reveal is what completes this picture; the journal's job is to confirm the bargain was real and personal, not to pre-explain the ending a player hasn't reached yet. An earlier version of this design had the journal spell out "the near-miss, the guilt, the first small cheat, and the slow calcification" as a connected narrative — cut, because a player who finds this room can do so well before the true ending, and a fully narrated confession here would flatten that ending's own payoff into something already told.

**What it is not:** a second location for the letter's three-stage "he is the friend" reveal — that device lives entirely with the player's own invitation letter. The hidden room's letter and the player's own letter should never appear in the same examine text without being clearly distinguished by name.

## 5. What this leaves genuinely open

- **Exact journal fragment text** — not written here; this document establishes what the fragments are for and what they must not do (over-explain), not their final prose.
- **A fourth, optional shard inside the hidden room** — flagged as a nice-to-have, not committed to.
- **The `cheatsCaught` persistence fix** — restated from per-game-cheats §4, since it blocks the Shut the Box gate from ever firing for real until it's done.
- **Whether the 8+ cheats-caught threshold is actually reachable across a real playthrough** — per-game-cheats §3.3 gives a reasoned estimate (Parlor supplying most of it across a full run) but flags this needs an actual playtest count, not just document arithmetic.
