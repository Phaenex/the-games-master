# The Games Master — Final Story Bible

## What this document is

The complete, consolidated story: Aldric Voss's mythology, why he cheats and can't stop, how each game's hidden cheat works, where the clues live, what the hidden room contains, and how the six endings resolve.

Primary canon underneath all of it is `The Games Master - Art Direction.dc.html` — its thesis, its six endings, its room designs for Court and Labyrinth, and its persistence system are treated as authoritative throughout. Three working documents — `2026-07-12-aldric-mythology-story-bible.md`, `2026-07-12-per-game-cheats-design.md`, `2026-07-12-clues-and-secret-rooms-design.md` — hold the detailed reasoning behind individual decisions and the exact shipped-code references each claim was checked against, for anyone who needs to trace a specific line back to its source.

---

## 1. The Premise

Aldric Voss is not trying to win. He is trying to lose — and he can't.

He was a guest once, exactly like the protagonist: desperate, in debt, playing for someone he loved. He won. The house made him the host. That's the trap nobody is told about: an ordinary win doesn't free a guest. It transfers the role. Winning is not how you leave — it's how you become the next Aldric.

He knows this, and he can't consciously choose either side of it. He can't throw the game (that's still a choice the house recognizes and punishes the same way), and he can't let a guest win ordinarily either (same trap, just handed forward). The only outcome that breaks the cycle for both parties is getting caught — decisively, past a full reckoning — which is exactly what he fell short of doing to the host before him, centuries ago.

Why this particular guest, tonight — not fate. The house's invitation is indiscriminate; it finds whoever is desperate enough to say yes to an offer this strange, and that's what the protagonist and Aldric-as-a-guest actually have in common. Not destiny. Just what every guest who ever answered the letter already shared.

Art Direction's own line — "He is not here to gamble. He is here because it is the last door left, and the house knew that before he did" — reads like foreknowledge about this specific man, and it isn't. The house recognizes the shape of desperation on sight, the same recognition it extends to every guest who ever said yes; "knew that before he did" describes how well it reads a man out of options, not that it singled him out in advance. Nobody is chosen. Everybody who reaches the gate already qualified.

## 2. What's Actually Escalating

A session's Corruption meter never starts at Tier 0 — it defaults to Tier 1, so every guest meets him already past the baseline of a very long curve. Most of his history was closer to true Tier 0: rare cheating, hard to catch, centuries of near-total failure to be caught at all, which is why the Entry Hall ledger only lists nine names. The house's memory is selective — most guests over the centuries left no durable trace, and a portrait and a ledger line are something it grants rarely, not something every guest earns.

What climbs with the tier, verified against the actual cheat logic: **frequency** (more of his hands need rescuing as he plays looser under pressure) and **visibility** (the tell gets sloppier — gold flickering green for a frame in the IRRITATED state, "relief he can't afford to show"). The trigger itself never stops being reactive. At every tier, across every room in this document, he cheats only when he's genuinely about to lose. There is no point in the story where he cheats from safety, and no room in this design ever violates that rule.

His tells and his cheating aren't two separate systems — one hardening, one holding steady. They're the same failing mechanism, observed from two angles. The line already in the game — *"…forgive me. Old habit, that last word. Pick up your hand. We begin."* — is that failure, caught mid-sentence.

## 3. Where He Cheats, and How Each One Is Caught

Four rooms carry this story. Each cheat is reactive-only (see §2) and escalates in frequency/visibility with corruption, never in kind.

### The Parlor (shipped)

The trick-taking card game. Aldric cheats only when he can't legally win the hand. The player's **Read** verb catches a hand an honest opponent couldn't have won. This is the model every other room's catch mechanic is built to match in shape, even where the specifics differ.

### The Court — "The Assize of One"

A real trial where every station — prosecutor, judge, bailiff, jury foreman — is him. The jury is the Entry Hall's nine named portraits, past guests watching the player stand where they stood.

This room only starts rigging itself once Aldric is genuinely at risk of losing the trial — which in practice means a **first visit, at low corruption, plays straight**: no planted evidence, no tarnish, a real chance to lose on a weak case. Only once corruption has climbed enough that he's actually threatened does the room's real nature show itself: Evidence Cards become breadcrumbs he's planted, unconsciously, and presenting the true one makes his gavel's gold tarnish — the tell that the trial has been rigged *for* the player. Three layers to this, once it's active: the evidence deck is stacked so the true card surfaces sooner across repeat visits; mid-trial he sometimes "misfiles" a card, a stumble in the prosecutor's voice not matching the judge's a moment later; and at the highest corruption, a wax seal (the room's three-argument HUD) cracks even on an argument the player hasn't actually landed. All three resolve through presenting the true evidence card — no new verb needed.

This gives the room real stakes early (he might actually beat you, honestly, on your first visit) and its unsettling reveal later (he's rigging this to lose to you, and it's costing him control of his own trial to do it).

### Shut the Box — replacing Gallery

Gallery doesn't exist as a room going forward. It carried real weight — the true-ending gate, a quiet pacing beat after Parlor, a nod in Court's jury text, a cross-run Collection-ending payoff — and Shut the Box inherits each responsibility deliberately, described below.

**The game:** head-to-head shut-the-box, two boxes of nine hinged tiles each, played with two dice. Roll, then shut one tile matching the total or any open combination summing to it; lowest remaining sum wins once both boxes are stuck. Players alternate turns; a player whose box sticks first doesn't end the game — their sum locks, and the other keeps rolling, with the live option to **stop voluntarily** and bank their current sum rather than risk a worse one, the same press-your-luck tension shut-the-box already has.

**The reskin:** the nine tiles map, in ledger order, to the nine named guests — Marr, Dufresne, Pike, Hale, Gall, Quill, Thale, Aubrey-Locke, Percival — never stated outright, discoverable by an attentive player cross-referencing the ledger's own text.

**The cheat, three types, all reactive:** palming a weighted die into a critical reroll (a tell in his hesitation and how the die sits when it lands — watch the hands, not the outcome, exactly as Edwin Marr's portrait already warns); false calls, shutting two of his own tiles on a roll that only legally justifies one; and, at the highest corruption, board tampering — a tile he already shut reopening between the player's turns.

**The catch:** calling **Hold**, which lays both boxes open against a running tally the game keeps visibly on screen. A correct Hold is a caught cheat; an incorrect one costs the player, the same asymmetric risk as Parlor's Read.

**The hidden room gate:** a correct Hold that catches board-tampering specifically on tile 9 — Percival's tile. A real, perceivable moment, not a statistical inference. Because tampering only appears at high corruption, this isn't available on a casual first visit — it's earned on a later playthrough of the room, once the escalation has climbed that far, which matters for what's found there (§5).

**What this costs:** more than Gallery did. Gallery's whole advantage was no opponent AI and flat 2D portraits; this room needs a real opposing AI (scoped to match `gmFollow()`'s existing complexity in Parlor, not exceed it) and a live tracking UI. That's an accepted tradeoff — the entire point of the swap was giving this room an active cheating opponent, which Gallery structurally couldn't have.

**What's inherited, what's still open:** Court's jury dressing can safely still use the Entry Hall's nine portraits, since they live there regardless of what fills Gallery's old room. Still genuinely unresolved: Gallery's design also named a *separate* secret portrait — Aldric as a young, unmasked guest — that isn't any of the Entry Hall's nine and doesn't have a home in this design. Flagged, not solved. On the Collection ending's cross-run side: a returning player should find a tenth tile added to their next game, their own name now on the ledger.

Pacing role carries over cleanly — slower and more arithmetic than Parlor by nature, the same quiet-breath beat Gallery used to hold.

### The Labyrinth — "The House Between"

First-person, always, a 7×7 maze built from the manor's hidden underside. The Huntsman stalks it, never fully seen, thirty turns before he catches you. The only physical mirrors in the game live here, showing nothing, or the GM, or a figure a beat behind — this room's whole point is not knowing what you're looking at, which is also why it doesn't get a discrete catch mechanic the way the other three rooms do. Forcing one here would resolve an ambiguity the room exists to protect.

What passes for "cheating" here is still reactive, exactly like every other room: the Huntsman's proximity only closes faster than the turn counter should allow, or the exit beacon only drifts farther, **when the player is already close to the turn limit or Sanity is already low** — never as a punishment for good progress. Noticing the pattern feeds a shard, not a scored catch, and every other room's tell-glint HUD cue conspicuously never appears here — an absence, not an oversight, the one room where the game stops offering "something's catchable" at all.

**On the true ending's cheat count:** only Parlor, Court, and Shut the Box feed the 8+ cheats-caught threshold. Parlor, played repeatedly across a full run, is expected to supply most of that total; Court caps around three; Shut the Box's catches are rare but uncapped. This needs a real playtest to confirm, not just this document's arithmetic.

## 4. The Clue Economy

Clue legibility climbs the same curve as cheat visibility (§2): early clues (Entry Hall portraits, first Parlor sessions) are deniable and easy to miss; middle clues (Court's gavel tarnish, mid-corruption Parlor tells) get a visible signal attached; late clues (Shut the Box's reopened tiles, high-corruption Parlor, Labyrinth's irregularities) are close to overt. One curve, not a separate rule per room.

**The ledger and the nameplate are one clue, told twice.** The Entry Hall ledger's illegible ninth entry — worn past reading from repeated touching — sits directly above the blank line left for the player. It parallels Percival's own worn nameplate, the portrait someone kept returning to. Both point at the same unstated fact: someone came back to this name, over and over, for reasons the game never has to spell out.

**Three shards of a broken mirror** are required for the true ending, tied to the Labyrinth's own physical mirrors specifically — not to be confused with "the Mirror," Art Direction's unrelated name for the secret eighth game/NG+ mode. Locations: behind Percival's portrait in the Entry Hall (intended to be found after the ledger, though not strictly enforced); among Court's Evidence Cards, mounted and mislabeled; and in one of the Labyrinth's own mirror rooms, found only by stopping to look rather than fleeing past it.

## 5. The Hidden Room

Aldric's own original chamber, sealed since before he became host, forgotten by the house the way old wounds get walled over. The door is built into the same hall Shut the Box now occupies, disguised as paneling.

**The gate:** the correct Hold call on tile-9 board-tampering, described in §3. Because that cheat only appears at high corruption, this room isn't reachable on a casual first visit — a player who finds it has already been through most of the game's real cheat-catching, which shapes what's inside.

**What's there, deliberately incomplete:**
- **Aldric's own, older invitation** — a different, aged object from the player's own letter, in a younger and less controlled hand, proof he was a guest before he was a host. It never uses the word "friend," keeping it clear of both the player's letter and "the friend in the walls" (§6).
- **A handful of journal fragments**, not a full confession — a line about a near-miss, a line about the first small cheat, one entry that just trails off. They confirm the bargain was real and personal without narrating the whole arc, which is the true ending's job, not this room's — a player who finds this room can do so well before that ending, and a fully spelled-out confession here would flatten it.

This is not a second site for the player's own letter's three-stage reveal, which stays entirely with that object.

## 6. Two Things That Share a Word, and Aren't the Same Thing

**"A friend."** The player's invitation letter's three-stage reveal ends with the hand identified as Aldric's own — he wrote it, to the next guest, the way he wishes someone had written to him. Fully resolved. Separately, "the friend in the walls" is the host before Aldric — ambient, unseen, cross-run-seeded, never confirmed, never given a face, never resolved on purpose. Not Percival, who is just a portrait with a worn nameplate and no more mystery attached than that. If a scene needs both, the letter's signer stays "a friend"; the ambient presence stays "the friend in the walls."

**"The Mirror."** Art Direction's name for the secret eighth game — a menu-level NG+ system, not an object in any room. Unrelated to the Labyrinth's literal glass mirrors, which are where the shards live. Any future text referencing "a mirror" should say which one.

## 7. The Six Endings

Conditions are unchanged from Art Direction; only the meaning underneath them is new here.

- **Escape** (Defiance ≥15, Sanity ≥40) — the player leaves without triggering either trap. Aldric watches from the window — now legible as him watching someone get the clean escape he never had.
- **Replacement** (Defiance ≥18, an Inversion used) — *"He removes the mask — nothing behind it — and you sit in his chair."* Won by force, no exposure, no understanding. The mask comes off because the transfer is already happening, and the transfer is what hollows it — not proof Aldric was always empty, a preview of what the chair does to whoever sits in it next.
- **Pact** (balanced Defiance/Compliance) — a negotiated third option, neither escape nor damnation.
- **Collection** (Compliance ≥15) — the player's portrait joins the wall. One more name for the next guest's ledger.
- **Cheat / true ending** (8+ cheats caught, 3+ shards, hidden room found) — the player succeeds exactly where Aldric fell short, centuries ago. **"Finally."** Freed, the curse breaks, credits roll — no second reading needed. The two "victories" a player instinctively chases, beating him hard or playing nice, are the two that trap you. Only understanding gets everyone out.
- **Hollow** (Sanity hits 0) — the one ending where he tries hardest to help: a last numberless game, blank cards, no credits, he goes quiet, then human, and almost helps, and can't. He tried hardest here, and it wasn't enough — the starkest version of the rule that's driven this whole story: even mercy isn't a choice the house lets him make on purpose. No portrait, no ledger line, no trace anywhere in the house — worse than the other five specifically because it's forgotten completely.

**A seventh state, outside the six: leaving before the game starts.** Retreating to the car before ever passing the gate at the start of the walk ends the game immediately — a real, shipped state (`triggerSecretEnding()` in the Prologue), not a design placeholder. It isn't one of the six above and shouldn't be counted as one: the six are all reached by sitting at the table and playing the game out to one of its real conclusions; this one is reached by never sitting down. It leaves no trace — no portrait, no ledger line, nothing written to shared state — the same way Hollow leaves nothing, but for the opposite reason: Hollow is what's left of someone the house used up, this is someone the house never got. Mechanically and thematically it's the cleanest possible proof of §1's thesis: the invitation was never fate, so declining it costs nothing and changes nothing for the house, which will simply find someone else. That's the point of the ending, not a flaw in it.

Art Direction's §11 sketches an earlier, different fourth ending called "The Debt" — losing the night with the house keeping the guest "a while longer," remembered by the ledger for next time. That sketch is superseded by the canonical six in §13 above and shares no mechanical identity with the new secret ending: Debt was a *post-play loss with memory*; the secret ending is a *pre-play exit with none*. Treat Debt as retired, not as an alternate name for leaving before the gate.

## 8. Twists, Honestly Ranked

Only the first of these is a twist a player actually experiences. The rest are continuity notes worth stating plainly.

1. **Aldric was once a guest who won, and winning is what trapped him.** The premise itself is genre-standard (Inscryption/Leshy-shaped); this document doesn't claim otherwise. The mechanism — winning transfers the role, exactly like the Replacement ending happening to him one loop earlier — is what's specific to this game.
2. The ledger's worn ninth entry and Percival's worn nameplate are the same clue, told twice.
3. Aldric's dialogue slips are real cracks in the compulsion, not writing flourish.
4. The letter's "friend" and "the friend in the walls" are two different things sharing a word, kept distinct on purpose.

## 9. What's Still Genuinely Open

- **Who Aldric loved or played for, originally** — deliberately unnamed. A forced parallel to the protagonist's own losses would need to be earned, not asserted.
- **Gallery's secret young-Aldric portrait** has no home in the new design and needs one, or an explicit decision to drop it.
- **Exact journal fragment prose** — not written here; this document establishes function, not final text.
- **The `cheatsCaught` persistence gap** — currently local Parlor session state. Needs to become shared/save-level state before Court's or Shut the Box's catches can count toward the true ending at all.
- **Whether 8+ cheats-caught is actually reachable across a real playthrough** — reasoned as plausible (§3, Labyrinth section) but unverified without an actual playtest.
- **Six endings as coded outcomes** — currently design intent. Corruption, Sanity, and a cheat counter exist in the Parlor prototype; none of the six endings are wired up yet.
- **Whether a fourth, optional shard belongs inside the hidden room** — a nice-to-have, not committed to.
