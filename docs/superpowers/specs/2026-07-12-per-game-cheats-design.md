# Per-Game Cheats & Shut the Box Design

## Status

Draft 1. Covers three tasks together because they're one design problem: how each room's hidden cheat works, mixing loaded-dice/sleight-of-hand/board-tampering per the user's brief, staying consistent with `2026-07-12-aldric-mythology-story-bible.md` (Draft 3) and with what Art Direction already built for Court and Labyrinth. Not yet adversarially reviewed — folded into the Task 24 final review alongside the mythology and the clue/secret-room passes.

## Ground rule this document follows

Check what's already designed before inventing. That was the mistake in the mythology's first draft. Court turns out to already have a complete, specific cheat-and-clue mechanic (Art Direction §5) that's different in kind from Parlor's, and Labyrinth is explicitly documented as **"pure fear, not fair play"** (Art Direction, pacing table, beat IV) — not a game with rules to rig at all. Forcing both into Parlor's mold would mean overwriting existing, better design work for the sake of surface consistency. Each room below gets the treatment that fits what's already true of it.

---

## 1. Shut the Box — full design (replacing Gallery)

Gallery doesn't exist as a room going forward; Shut the Box takes its slot. That's a bigger job than designing a new minigame, because Gallery was carrying real narrative weight: the true-ending gate (§5's "match his hidden young-guest portrait"), the pacing role (Game II — the quiet, dread-soaked breath after Parlor), a mention as Court's jury (though that dependency turns out not to actually need Gallery specifically — see 1.6), and the Collection ending's cross-run payoff (your portrait joins the wall on a replay).

Everything below either inherits one of those responsibilities on purpose, or explicitly says why it doesn't need to.

### 1.1 The table

Two boxes, one per player — a genuine if less common shut-the-box variant, played head-to-head rather than solo-scored. Nine hinged numbered tiles per box (1–9), two dice. Standard rules: roll, then shut either one open tile matching the total or any combination of open tiles that sums to it; a shut tile stays shut; play ends for a box when no legal move remains; lowest sum of remaining open tiles wins. Whoever's box totals lower when both are stuck wins the game.

**Why head-to-head, not solo-scored:** Aldric needs skin in the game, the same way he has one in Parlor. A banker who just calls rolls for the player has nothing to cheat *for*. Racing his own box against the player's gives him a motive that matches every other room.

### 1.2 The reskin — tiles are the ledger, in order

The nine tiles map, in order, to the nine names already on the Entry Hall ledger and the nine portraits already built: 1=Marr, 2=Dufresne, 3=Pike, 4=Hale, 5=Gall, 6=Quill, 7=Thale, 8=Aubrey-Locke, 9=Percival. This is never stated in dialogue — it's discoverable by cross-referencing the ledger's own already-shipped text ("Marr. Dufresne. Pike. Hale. Gall. Quill. Thale. Aubrey-Locke. A ninth... worn past reading.") against the box, which lists the same nine in the same order. An attentive player notices that shutting a tile reads, in retrospect, like closing out that guest's case.

This costs nothing new to build (the names and their order already exist) and gives Shut the Box the same "everything in this house connects" quality Gallery had via the Court tie-in, without needing portraits as an asset.

### 1.3 The cheat — mixing all three types, tied to corruption tier per the corrected mythology §5

Per the mythology's corrected escalation model, none of this is threat-free or reflexive — every cheat below still only fires when Aldric's box is actually behind. What changes with corruption tier is frequency, margin, and visibility, not whether a threat is required.

1. **Loaded dice (statistical, low corruption, Tier 1 baseline).** Across a long sample, his rolls under-produce the totals that would let the player legally shut tile 9 (Percival's tile) early. Not visible in any single roll — only in aggregate, over many games or a single long one. A player who's read Edwin Marr's portrait ("cheats give right before they play the impossible card: they look anywhere but at their hands") already has the in-fiction instruction to start tallying results instead of trusting single rolls.

2. **False calls (sleight of hand, Tier 2).** As banker for his own box, Aldric announces his roll total and which of his own tiles it shuts. At this tier, he occasionally shuts two tiles on a roll that only legally justifies one — a rules violation disguised as confident, fast play. This is fair to catch specifically because the game has to teach shut-the-box's real legal-move rules clearly before this room, making the false call a genuine catch rather than an arbitrary gotcha.

3. **Board tampering (Tier 3+, most visible).** A tile Aldric already shut is open again on a later glance — quietly reopened between the player's turns to keep his own box alive longer. This is the most brazen version, matching the mythology's corrected §5 (visibility gets sloppier at higher tiers, not just frequency).

### 1.4 The catch mechanic — "Hold," Shut the Box's version of Parlor's Read

At any point, the player can call **Hold** — forcing both boxes to be laid open and checked against what the player has tracked (mentally, or via a simple running tally the UI exposes, matching Parlor's existing Read affordance rather than inventing a new UI language). A correct Hold is a caught cheat. An incorrect Hold — calling out a discrepancy that isn't there — costs the player, the same asymmetric-risk shape Parlor's Read already has, so this isn't a free action to spam.

**Persistence note, flagging a real gap the game-design-coherence review already found in Parlor:** `cheatsCaught` is currently local Parlor session state with no cross-scene persistence. If the true ending's "8+ cheats caught" threshold is meant to count catches from Shut the Box, Court, and anywhere else too, that counter needs to move to shared/save-level state before this room can wire in for real. This is an engineering task, not a story problem, but it needs to happen before Shut the Box's Hold can contribute to the true ending — noting it here so it isn't lost.

### 1.5 The hidden-room gate — Shut the Box's version of "match his portrait"

Gallery's true-ending gate was matching Aldric's hidden young-guest portrait. Shut the Box has no portraits, so it needs its own rare, attention-and-skill-gated equivalent that does the same job rather than borrowing Gallery's mechanism wholesale.

**The gate: shutting tile 9 — Percival's tile — cleanly, in a game where it wasn't one of Aldric's engineered near-misses.** Because cheat type 1 (loaded dice) specifically biases against the totals that let the player close tile 9 early, a clean, legitimate shut of that tile is statistically rare *by design* — it either means the dice weren't rigged that round, or the player fought through the bias with enough patience and Holds to force it anyway. That rarity is exactly the quality Gallery's gate had (an attentive, patient player finds it; a casual one doesn't). When it happens, a door that was always there — behind or adjacent to wherever this game is staged — is what's found, same beat Gallery's swinging-portrait-frame used to deliver.

This preserves Gallery's exact *function* (rare, meaningful, skill-gated, not a random drop) while being native to Shut the Box's own mechanics instead of a portrait-match bolted onto a dice game.

### 1.6 Reconciling the two dependencies Gallery leaves behind

- **Court's jury.** Court's tie-in text says "the jury is the Gallery portraits" — but the portraits themselves already live in the Entry Hall (built in the mansion-intro-polish pass), not in a separate Gallery room. Court's callback is to the portraits as objects, not to the room. This dependency was never actually on Gallery specifically, so it needs no change: Court's jury is still "the nine portraits, past guests, watching you stand where they stood," regardless of what game now occupies the room Gallery used to.
- **Cross-run memory (Collection ending).** Gallery's "your portrait joins the wall on a replay" doesn't map to a dice game, but the ledger already is the cross-run memory device (§17, the persistence system). Stated explicitly for a future implementer: a returning Collection-ending player should find a **tenth tile** added to their next Shut the Box game — their own name now on the ledger, the box extended by one, in keeping with §17's existing cross-run ledger growth. This is a small addition to an already-planned system, not a new one.

### 1.7 Pacing role

Gallery's slot in the seven-beat pacing curve (Game II — a quiet, dread-soaked breath after Parlor's card tension; where lore lands; where sanity pressure begins) transfers naturally. Shut the Box is inherently slower and more arithmetic than Parlor — turn-based, methodical, no bluffing under time pressure — so it keeps the same "quiet breath" function without needing anything extra.

---

## 2. The Court — clarifying and extending the existing design, not replacing it

Court already has a complete cheat/clue mechanic (Art Direction §5, "The Assize of One"): Evidence Cards are described as "the breadcrumbs he planted," and presenting the correct one makes "his gavel's gold tarnish — the tell that he's rigged this *for* you." This is the one room in the existing design where the cheat runs in the *opposite direction* from Parlor's — he's not cheating to survive, he's cheating to help the player win the trial, then getting caught in the act of having done it.

### 2.1 Why this direction is correct, not a design gap

This finding matters because it's a near-perfect match for the corrected mythology's §4 and §6: Aldric can't consciously choose to help the player, any more than he can consciously choose to lose outright or soften his own recruitment pitch — all three are the same rule. So the planted evidence has to be read as **compulsive, not strategic** — leakage from the same failing mask that produces the Parlor tells and the "old habit" dialogue slip, not a deliberate plan to lose this specific trial. The gavel's tarnish is Court's version of the IRRITATED-state gold flicker: the visible cost of a compulsion he can't fully suppress.

### 2.2 Mixing the three cheat types within Court's existing frame

Art Direction's Court design doesn't specify *how* the evidence gets planted mechanically — only that it is and that presenting the true card exposes it. To give the room the same "mix of all three" texture as Parlor and Shut the Box, without touching the parts that already work:

1. **Loaded dice equivalent — the deck itself.** The evidence deck he deals from is subtly stacked so the true card surfaces sooner than random shuffling would produce, across repeat playthroughs. Statistical, invisible in a single trial.
2. **Sleight of hand — the staging.** Mid-trial, he occasionally "misfiles" a card — presents a decoy exhibit with a beat of hesitation, a stumble in the prosecutor voice that doesn't match the judge voice a moment later. This is the room's version of a false call: catchable by an attentive player who's noticed his voice shifting station to station.
3. **Board tampering — the verdict weight.** At higher corruption, the wax seals (the room's existing HUD — "three arguments = three wax seals that break as you win them") crack even on an argument the player hasn't actually landed cleanly yet, nudging the trial toward a win regardless of the player's actual case. This is the most brazen version — the trial visibly wants the player to win, whether they've earned it or not, which is unsettling in exactly the way Court's "he's rigged this for you" premise wants to be.

All three route through the same existing catch beat: presenting the true evidence card and watching the gavel tarnish. No new UI verb needed — Court already has one, and it already does this job.

### 2.3 What this room contributes to the mythology, not just mechanics

Court is the room where a player can first suspect the truth without yet understanding it — a trial that seems to want you to win is stranger and more revealing than one that's simply hard. This is consistent with the corruption-tier pacing note already on this room (Corruption 2, "the first hostile spike... where the player, now armed with Read, first fights back and wins loudly") — Court's cheat isn't hidden the way Parlor's is; it's *found*, and the finding is the point.

---

## 3. The Labyrinth — a deliberately different kind of "cheat," matching its own design

Labyrinth is documented, twice, as the one room that breaks from "fair play": the pacing table calls it explicitly **"the one game that's pure fear, not fair play"** (beat IV), and its Identity Hook section is about ambiguity (is the Huntsman Aldric? a previous guest? you, a beat behind?) rather than about a discrete rule to violate. Giving it a Parlor-style "catch the rigged card" mechanic would work against a room that's explicitly designed not to be that kind of game.

### 3.1 What "cheating" means here instead

Not a violation of stated rules (there are no cards, no dice, no dealt hands) — a violation of the *implicit* fairness of a chase: the idea that a 30-turn fuse and a stalking figure should behave consistently. The "cheat" is the pursuit quietly not playing fair with its own stated terms:

- The Huntsman's proximity occasionally closes faster than the turn counter should allow — a rubber-band that punishes the player for getting close to the exit rather than for making a mistake.
- The 30-turn fuse (already the room's own HUD device) burns down slightly faster on turns where the player has made genuine progress toward the exit — the maze getting harder exactly when it should be getting easier, discoverable only by a player tracking their own turn count against visible progress.
- The exit beacon (already established as "the only gold left" in this room's deliberately desaturated palette) drifts a few units farther on a fresh glance, on rare occasions, if the player has been closing distance too efficiently.

### 3.2 Why this room doesn't get a "Read"/"Hold"-equivalent catch action

Every other room's cheat resolves through a discrete, teachable catch: Read in Parlor, presenting evidence in Court, Hold in Shut the Box. Labyrinth's identity-ambiguity premise is the opposite of a clean resolution — the room's whole thesis (per its own Identity Hook: "you could be anyone. You could be the player") depends on the player never being fully sure what they're looking at. Giving this room a definitive "gotcha, caught you" beat would resolve an ambiguity Art Direction is explicitly protecting elsewhere (compare to the mythology's §9 treatment of "the friend in the walls" — deliberately left open, not a puzzle with a solved state).

**What the player gets instead:** noticing the pattern (fuse burning unevenly, distance drifting) is an atmosphere-native clue, not a scored catch — it doesn't add to `cheatsCaught`, but it can be one of the shard/hidden-room clues (see the secret-rooms design, Task 23) precisely because it raises the right question — *is something making this unfair, or is fear making me miscount?* — without answering it. That question is Labyrinth's actual payoff, and it's a different kind of contribution to the true ending's evidence than a tallied catch is.

### 3.3 Consistency check against the mythology

This doesn't create a hole in the "8+ cheats caught" true-ending threshold — Labyrinth was never going to be one of the rooms that contributes discrete catches, the same way Hollow was never going to need its own cheat mechanic. Not every room has to carry every system. Forcing one here would cost the room its actual identity for the sake of a uniformity the brief didn't actually require once Labyrinth's own design was checked first.
