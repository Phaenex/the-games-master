# Per-Game Cheats & Shut the Box Design

## Status

Draft 2. Revised after a full four-lens review (tone, consistency, game-design coherence, plot-hole) of the complete story package. Draft 1's mechanical core survives; this pass fixes a real hole in Shut the Box's loaded-dice cheat (it wasn't actually catchable by its own tracking mechanism), gives Court an explicit fail-state, commits Shut the Box's tracking to one design instead of two, specifies turn structure, corrects Labyrinth's framing so it doesn't reintroduce the threat-gating bug the mythology already fixed once, and states the production-cost tradeoff this room represents openly instead of leaving it unexamined.

Consistent with `2026-07-12-aldric-mythology-story-bible.md` (Draft 4) throughout, especially §4 (the reactive-only cheat trigger — nothing below ever fires without Aldric genuinely being at risk of losing) and §8 (the Mirror/mirrors naming discipline).

---

## 1. Shut the Box — full design (replacing Gallery)

Gallery isn't in the game going forward; Shut the Box takes its slot. Gallery carried real narrative weight — the true-ending gate, the Game II pacing role, a mention in Court's jury tie-in, and the Collection ending's cross-run payoff — and this design either inherits each responsibility on purpose or says explicitly why it doesn't need to.

### 1.1 The room, before the rules

A long hall, dimmer than the rest of the manor's public rooms — the same hall Gallery would have used, its portrait-lights mostly unlit now, dust-sheeted frames along both walls where the room's old purpose still shows through the new one. One table, one box on each side, the wood dark and worn smooth at the corners the way a thing gets touched for a long time by the same hands. The dice are bone, or something made to look like it. Aldric sits across from you, not standing, not performing — this is the quietest he ever is, and that quiet is its own kind of unsettling after the Parlor's noise. He doesn't call the room by name. He just says, rolling first, *"Nine each. Lowest left standing wins. You'll want to count."*

### 1.2 The table

Two boxes, one per player, played head-to-head — a real if less common shut-the-box variant, rather than the more familiar solo-scored version. Nine hinged numbered tiles per box (1–9), two dice. Standard rules: roll, then shut either one open tile matching the total or any combination of open tiles that sums to it; a shut tile stays shut; play ends for a box when no legal move remains. Lowest sum of remaining open tiles, once both boxes are stuck, wins.

**Turn structure:** players alternate rolls, each acting on their own box only. A player whose box gets stuck first doesn't end the game — their box locks at its final sum, and the other player keeps rolling alone. Critically, a player can also choose to **stop voluntarily** before being forced to, locking in their current sum rather than risking a worse one on the next roll — the same press-your-luck tension shut-the-box already has in its solo form, now with a real opponent's locked sum to weigh the decision against. This is what keeps the "someone busted early" case from being dead time: the surviving player is making a live decision (push further or bank the win) rather than just watching dice land.

**Why head-to-head, not solo-scored against a house target:** Aldric needs something at stake for himself, the same way he does in Parlor. A banker who only calls rolls for the player has nothing to cheat *for*.

### 1.3 The reskin — tiles are the ledger, in order

The nine tiles map, in order, to the nine names already on the Entry Hall ledger, verified against the shipped text: 1=Marr, 2=Dufresne, 3=Pike, 4=Hale, 5=Gall, 6=Quill, 7=Thale, 8=Aubrey-Locke, 9=Percival. Never stated in dialogue — discoverable by cross-referencing the ledger's own already-shipped order against the box. An attentive player notices, in retrospect, that shutting a tile reads like closing that guest's case.

### 1.4 The cheat — three catchable types, all reactive, none of them free

Every cheat below still only fires when Aldric's own box is genuinely behind — never from safety, per the mythology's §4. What escalates with corruption tier is how often this happens and how visible it is, not whether a threat is required.

1. **Palming (sleight of hand, Tier 1 baseline).** On a roll that would otherwise leave his box behind, Aldric occasionally swaps in a second, weighted die from his sleeve for the reroll — a real, physical, single-instance act, not a statistical drift. The tell is a half-second of hesitation before he calls the result, and the die itself sitting a shade too still when it lands, the same "watch the hands, not the outcome" instruction Edwin Marr's portrait already gives the player. Calling **Hold** (§1.5) immediately after a suspicious roll, before either box is touched again, catches this.
2. **False calls (Tier 2).** As banker for his own box, Aldric announces his total and which of his own tiles it shuts. At this tier he occasionally shuts two tiles on a roll that only legally justifies one. Fair to catch because the room teaches shut-the-box's real legal-move rules before this happens — a Hold call here is checking his claimed move against the actual rules, not against hidden information.
3. **Board tampering (Tier 3+, the most brazen).** A tile Aldric already shut is open again on a later glance — reopened between the player's turns to keep his own box alive longer. Because this is a real, standing board-state mismatch (a tile marked shut that's now visibly not), it's the most reliably catchable of the three, and it's the one the hidden room's gate depends on (§1.6).

**What this replaces, and why:** an earlier version of this design used a continuous statistical dice-bias against tile 9 specifically, meant to be noticed only in aggregate across many games. That cheat had no board-state signature for Hold to ever check — Hold catches a mismatch between what's true and what's claimed, and a weighted probability distribution never produces one. It's been dropped for palming, which does.

### 1.5 Hold — the catch action, committed to one design

At any point the player can call **Hold**, which lays both boxes open against a running tally the game keeps visibly on screen — a small ledger-style readout of each box's shut/open tiles and the roll history, always present, not something the player has to memorize themselves. A correct Hold, called against one of the three cheat types above, is a caught cheat. An incorrect Hold — calling a discrepancy that isn't real — costs the player, the same asymmetric risk Parlor's Read already has. The skill here is deciding *when* to call it and reading Aldric's behavior, not memory burden; the UI carries the bookkeeping so the tension stays where it belongs.

### 1.6 The hidden-room gate

The gate is a specific, catchable event, not a statistical inference: **calling a correct Hold on a board-tampering cheat (type 3) specifically against tile 9 — Percival's tile.** This gives the door real, perceivable feedback the way Court's gavel-tarnish does: the player sees the exact moment they catch him re-hiding that specific tile, and the door — built into the same wall this room already uses, disguised as paneling — opens in response. Rare by construction (tampering is the highest-corruption cheat, and it has to land on tile 9 specifically, not any tile), which preserves the quality Gallery's old portrait-match gate had: an attentive, patient player finds it; a casual one doesn't.

This also isn't something available on a first visit — it requires the room to have escalated to Tier 3+ corruption at least once, which in practice means it's found on a later playthrough of this room, not turn one. That pacing matters for how the hidden room's own contents land (see the clue/secret-room design).

### 1.7 What this room costs, stated plainly

This is a materially bigger build than Gallery was. Gallery's whole cost advantage was explicit in Art Direction: no opponent AI, 2D textures on 3D frames. Shut the Box reintroduces a real opposing AI (legal-move logic across nine tiles, comparable in scope to `gmFollow()`'s card-following logic in Parlor — and it should be scoped to match that, not exceed it), a live tally UI, and a hidden door trigger. That's a real tradeoff against the project's own "cheapest wins first" build order, and it's an accepted one: the entire reason to replace Gallery was that Gallery structurally couldn't have an active cheating opponent (the design explicitly says "the GM doesn't play, he watches"), which is exactly the thing this room needs to exist. Paying more to get that back is the point of the swap, not an oversight.

### 1.8 Reconciling what Gallery leaves behind

- **Court's jury.** Court's tie-in says "the jury is the Gallery portraits" — the *background* jury dressing can safely use the Entry Hall's nine named portraits, which already exist and are visually available regardless of what game now occupies Gallery's old room. What this doesn't resolve: Gallery's design also names a *specific, separate* secret portrait — Aldric as a young, unmasked guest — that isn't any of the Entry Hall's nine. That portrait doesn't have a home anymore. This is a real, still-open asset gap, not a solved dependency, and it's flagged here rather than papered over.
- **Cross-run memory (Collection ending).** The ledger already is the cross-run memory device. A returning Collection-ending player should find a tenth tile added to their next Shut the Box game — their own name now on the ledger, the box extended by one.

### 1.9 Pacing role

Shut the Box keeps Gallery's old Game II slot — a quiet, arithmetic, turn-based room after Parlor's card tension, where lore lands and sanity pressure begins. Slower and more methodical than Parlor by nature, so the "quiet breath" function transfers without needing anything extra.

---

## 2. The Court — extending the existing design, with an explicit fail-state

Court already has a cheat/clue mechanic (Art Direction §5): Evidence Cards are "the breadcrumbs he planted," and presenting the true one makes his gavel's gold tarnish — the tell that he's rigged the trial *for* the player. This runs in the opposite direction from Parlor's cheat (helping, not surviving), which matches the mythology's §3: he can't consciously choose to help any more than he can consciously choose to lose, so the planted evidence has to be leakage, not a strategy.

### 2.1 The fail-state this room needs, and didn't have

As designed, every cheat type below pushes toward a player win, which — without a stated failure path — risks making the room unloseable. It shouldn't be. **The rigging only exists once Aldric is at genuine risk of losing the trial**, matching the reactive-only rule everywhere else: at low corruption tiers, or on a first visit to this room, the trial runs straight, with no planted evidence and no tarnish, and the player can lose it on a bad case, exactly like a real trial. The unsettling "he's rigged this for me" quality only appears once corruption has climbed enough that Aldric is genuinely at risk of a loss he can't consciously choose — meaning Court gets harder and more honestly adversarial the *earlier* in a playthrough it's visited, and only reveals its rigged nature on a later, higher-corruption visit. This gives the room real stakes on the visit where a player most needs them, and turns the "he's helping me win" realization into something that has to be earned by getting far enough into the game to see it, rather than being true from the first sitting.

### 2.2 The three cheat types, once the rigging is active

1. **The deck.** The evidence deck he deals from is stacked so the true card surfaces sooner than a fair shuffle would. Not visible in a single trial — visible only if a player replays this room and notices the true card's position skewing early across attempts, the same aggregate-pattern shape as Shut the Box's now-retired dice bias, kept here because Court's own clue design already worked this way and doesn't have Shut the Box's Hold-mismatch problem — the tarnish tell (below) is what actually resolves this cheat, not a board-state check.
2. **The staging.** Mid-trial, he occasionally "misfiles" a card — presents a decoy exhibit with a beat of hesitation, a stumble in the prosecutor voice that doesn't match the judge voice a moment later.
3. **The verdict weight.** At the highest corruption, a wax seal (the room's existing three-argument HUD) cracks even on an argument the player hasn't actually landed cleanly — the trial visibly wanting the player to win.

All three resolve through the room's one existing catch beat: presenting the true evidence card, and watching the gavel tarnish. No new verb needed.

### 2.3 A note on "Read"

Art Direction's pacing table calls this "where the player, now armed with the Read verb, first fights back and wins loudly" — that line is almost certainly using "Read" loosely, as a description of the general skill the player has learned by this point, not literally Parlor's own button. Court's actual catch action is presenting evidence, and that's what this design builds on.

---

## 3. The Labyrinth — a different kind of "cheat," matching its own design

Labyrinth is documented twice as the one room that isn't "fair play" — the pacing table calls it explicitly "the one game that's pure fear, not fair play," and its Identity Hook is about ambiguity (is the Huntsman Aldric? a previous guest? you, a beat behind?) rather than a rule to violate. A Parlor-style "catch the rigged card" mechanic doesn't belong here.

### 3.1 What "unfair" means in a chase, and why it's still reactive

Not a violation of stated rules — there are no cards or dice here — but a violation of the implicit fairness of a pursuit, and it still only activates when the player is genuinely at risk, matching the mythology's §4 exactly: **the Huntsman's proximity only closes faster than the turn counter should allow when the player is already close to the 30-turn limit or Sanity is already low** — i.e., when the player is already in real danger, not as a punishment for making good progress early. An earlier version of this design tied the effect to "closing distance too efficiently," which reads as punishing progress rather than responding to threat — the same bug the mythology's §4 already had to correct once for the cheat trigger generally. This version doesn't repeat it: the maze doesn't turn against a player who's doing well: it turns against one who's already in trouble, exactly like every other room's cheat.

### 3.2 Why this room doesn't get a Hold-equivalent catch action

Every other room resolves its cheat through a discrete, teachable catch. Labyrinth's whole premise — "you could be anyone, you could be the player" — depends on the player never being fully sure what they're looking at. A clean "gotcha" here would resolve an ambiguity the room is built to protect.

**What signals this is deliberate, not unfinished:** every other room has a tell-glint HUD affordance that appears when something catchable is present. Labyrinth conspicuously never shows it — not a bug, an absence a genre-savvy player eventually registers as meaningful in its own right, the one room where the game stops offering the "something's catchable here" cue at all.

**What the player gets instead:** noticing the pattern (the fuse burning unevenly, the exit drifting, only when things are already going badly) is an atmosphere-native clue, feeding a shard, not a scored catch. It raises a question — is something making this unfair, or is fear making me miscount? — without answering it, which is the room's actual payoff.

### 3.3 Labyrinth doesn't feed `cheatsCaught`, and that's accounted for

Only Parlor, Court, and Shut the Box feed the true ending's 8+ cheats-caught counter. Parlor is the main, repeatable hub across a full playthrough and is expected to supply most of that total over multiple sessions; Court caps out around three (one per argument/wax seal); Shut the Box's Hold catches are rare by design but uncapped. The arithmetic isn't fully proven without an actual playtest, but it isn't asserted blind either — Parlor being replayable across the run is what makes 8+ realistic, not any single room carrying the whole threshold alone. This needs a real playtest count once these rooms are built, not just this document's word for it.

---

## 4. Engineering note carried over from the mythology review

`cheatsCaught` is currently local Parlor session state with no cross-scene persistence. Before Court's or Shut the Box's catches can count toward the true ending at all, this needs to become shared/save-level state. Flagged here because it blocks two of this document's three rooms, not just one.
