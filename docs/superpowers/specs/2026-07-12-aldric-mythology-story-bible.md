# Aldric Voss — Mythology & Story Bible (Draft 3 — consolidated with Art Direction canon + plot-hole pass)

## Status

Draft 3. Supersedes Drafts 1 and 2 in full. Adversarially reviewed on all four lenses now: tone/genre-fit, consistency, game-design coherence, and plot-hole hunting. The plot-hole review ran late (dispatched with the other three, but its result arrived after Draft 2 was already written to consolidate with Art Direction) — three of its eleven findings turned out to already be fixed by the Draft 2 consolidation pass for unrelated reasons (Percival-as-host, the double-booked ledger clue, the Cheat ending's two-readings claim); the other eight are addressed directly in this revision, worst first, inline where each one lives. This document is the foundational lore that every per-game cheat design, clue placement, and secret-room puzzle in the rest of this design pass must stay consistent with.

## Why this draft exists

Draft 1 was written before I'd read `The Games Master - Art Direction.dc.html` in full. That file already contains a complete, better-integrated mythology — §1 (The Reframe), §11 (The Story Underneath), §13 (The Six Endings, Mapped), and §18 (The Story — the treatment) answer nearly everything Draft 1 tried to invent from scratch, in several places using near-identical language to what I drafted independently. Three review agents each independently flagged that Draft 1 was competing with existing canon instead of building on it.

**This document's job now is narrower and more honest: consolidate Art Direction as primary canon, resolve the one real internal contradiction it contains, fix one factual error, retire the parts of Draft 1 that duplicate or conflict with it, and keep only the genuinely new ground — which is mostly about Shut the Box, since Art Direction never designed that room (it didn't exist yet).**

Everything in Art Direction §1/§3/§5/§10/§11/§13/§17/§18/§19 is treated as authoritative and is cited, not restated in full, below. Read that file alongside this one.

**Precision note (plot-hole review, minor):** the six-ending table cited throughout this document (§13's exact wording, and the underlying condition table in the original README) is design intent, not yet-shipped mechanics. The Parlor prototype currently implements Corruption, Sanity, and `cheatsCaught` — the substrate the endings will read off of — but Escape/Replacement/Pact/Collection/Hollow/Cheat as coded outcomes don't exist in the runnable build yet. Treat this document's ending text as the target a future implementation task must build toward, not as something already locked in code.

## 1. The Bargain (Art Direction §1, §11 — no changes)

Aldric Voss was a guest once. He won. The house made him the host. **"He is not trying to win. He is trying to lose — and he can't"** (§1) is the thesis this entire document exists to support, not replace. Winning transfers the role — this is *already* the Replacement ending, happening one loop earlier, to him.

Draft 1's version of this (§1) said the same thing in different words. No change needed; deferring to Art Direction's phrasing as canonical.

**Small addition, closing a gap the plot-hole review flagged:** why this protagonist, why tonight? Not fate, not a targeted selection — the house's invitation logic is already established as indiscriminate ("a house that said yes to everything"). It doesn't choose the desperate because they're special; it finds them because desperation is what reliably produces someone willing to say yes to an offer this strange. The coincidence that the protagonist is broke and playing for someone they love, same as Aldric once was, isn't a hidden design — it's what every guest who ever said yes has in common, which is the actual, unglamorous premise underneath the haunting.

## 2. Who the previous host was — RETIRING Draft 1's Percival theory

Draft 1 proposed that the portrait named **Percival** (Entry Hall, right wall, z=-19 — worn nameplate, "someone came back to this one more than the rest") is secretly the *previous* Games Master, and that Aldric himself wears the nameplate smooth returning to it.

**Retiring this.** Two independent reviewers flagged it, and rereading Art Direction confirms why it doesn't hold up: §11 and §10 both treat "the friend in the walls" as **deliberately non-corporeal** — "a residue, a memory worn into the walls... the house has already forgotten it's there" (§11), explicitly "keep it ambiguous" (§10) whether it's Aldric's earlier self or a previous near-escapee. A portrait is the opposite of that — fixed, named, framed, hanging in a specific spot next to eight others exactly like it. Pinning "the friend" to a discrete nameplate resolves an ambiguity Art Direction *chose* to keep open, and does it with a device (a portrait) that structurally can't carry the ambient quality the design calls for.

**What replaces it:** the previous host — "the friend in the walls" — stays exactly as Art Direction already decided: unseen, unnamed, no portrait, no fixed identity. Percival's portrait keeps its shipped text and its mystery untouched (worn nameplate, unexplained repeat visits) with no added claim about who he secretly is. The game is free to leave "who kept touching this nameplate, and why" as an open question a player can wonder about without the mythology forcing an answer.

**Twist retired: Draft 1 twist #2 ("Percival is the previous Games Master") is cut entirely.**

## 3. The Rule Itself (Draft 1 §3 — kept, minor trim)

The house's rule, not a devil's, not an enforcer's — it's a quasi-sentient presence elsewhere in the existing text ("a house that said yes to everything"). Aldric knows the shape of the rule (win ordinarily → inherit it; get caught past a full reckoning → the game voids, releases everyone) but not its source. No change; this was consistent with Art Direction and didn't duplicate anything, so it stays as the one piece of genuinely original connective tissue in this document.

## 4. Why He Can't Just Let You Win (Draft 1 §4 — kept, extended to cover the recruitment pitch)

He can't consciously choose to lose any more than he could consciously choose an ordinary win — both feed the house the same way. The only outcome that breaks the cycle for both parties is getting caught, decisively, past the threshold he fell short of himself. This is what makes him sympathetic rather than just tragic-adjacent, and it isn't stated this explicitly anywhere in Art Direction (which gestures at it via "the mask failing" but doesn't spell out the mechanism) — this is this document's one real added clarification, kept.

**Extension, addressing a real gap the plot-hole review found:** his opening pitch (`introSeq`, Parlor) is an unqualified, appealing sales job for exactly the trap — *"You may have every note of it. You need only win it from me... And I will play perfectly fair."* No coded hint that winning has a downside. The review is right that a man secretly hoping to be stopped doesn't usually spend his opening lines making the trap sound this good — unless the same rule from §4's first paragraph covers the pitch too, which it does: **steering a guest away from an ordinary win is its own form of intentionally losing.** If he hedged the invitation, warned that winning outright was a trap, he'd be choosing the outcome rather than letting the game run straight — the exact thing the rule forbids him from doing. The pitch has to be wholehearted for the same reason he can't throw a hand: any deliberate softening is itself a disallowed move. What leaks through isn't in the content of the pitch, it's in the involuntary edges around it — see the corrected §6 below, where the "old habit" slip and the pitch's own polish aren't two different things in tension, they're the same failing mechanism observed at two different moments.

## 5. The Escalation — CORRECTED to match shipped code behavior

Draft 1 claimed that centuries of near-misses pushed Aldric's cheating trigger earlier and earlier, until **"now, this deep in, he cheats reflexively — sometimes before there's any real threat at all."**

**This is wrong and needs correcting.** The game-design-coherence review checked this against `gmFollow()` in `The Parlor - Playable Prototype.dc.html`: Aldric's actual cheat trigger is **reactive only** — the probability array is indexed by corruption tier, but every tier's cheat still fires only when he is about to legitimately lose. There is no code path where he cheats while safely ahead. Claiming otherwise in the mythology creates a document that describes behavior the game doesn't have.

**Corrected version:** what climbs with corruption tier is not *whether* the cheat is threat-gated but three other things, all already true of the tier-indexed array and now given a narrative reading instead of a false one:
- **Frequency** — at higher tiers, more of his hands are "about to lose" hands that get rescued, simply because he's playing looser/more recklessly the more unraveled he gets (a real, defensible read of "a mask failing" — he's a worse player under pressure, not a braver cheat).
- **Margin** — at low tiers he cheats to survive a genuine near-loss; at high tiers he starts rescuing hands he was merely *behind* in, not yet doomed in. Still reactive to a losing position, just a less severe one triggers it.
- **Visibility** — the tell gets sloppier and more legible at higher tiers (per §3's IRRITATED state: "the state that carries the secret," gold flickering green "relief he can't afford to show"), because the compulsion outpaces his control of his own face.

This gives Draft 1's core idea (the tier system as a compressed echo of the centuries-long arc) a version that's actually true of the code, instead of one that reads well but contradicts it.

**Second correction, addressing a real gap the plot-hole review found:** Draft 1 also claimed "centuries" of guests with only "a handful" of near-misses, and that framing doesn't survive contact with two things the review checked directly. First, the actual Parlor cheat mechanic is close to tutorialized — a flat, generous cheat-when-losing chance, and a scripted "test" sequence that walks an ordinary player into catching him after just two suspicious moments. Second, the Entry Hall ledger physically shows only about ten total names. Centuries of near-total failure doesn't square with either fact, and the document shouldn't leave the two claims sitting next to each other unreconciled.

**Resolution, using something the review itself flagged as already true of the code and simply unclaimed:** the Parlor's corruption meter *doesn't start at Tier 0 (Honest)* — it defaults to Tier 1 on a fresh session. The player never meets an average night. They arrive at a point already past the baseline, on the leading edge of a curve that's been steepening, not partway through a flat centuries-long average. Two things follow from taking that seriously instead of ignoring it:

- **The escalation isn't gradual and centuries-flat — it's recent and accelerating.** Most of "the centuries" were something closer to Tier 0: rare cheating, hard to catch, a very long stretch of near-total failure that's consistent with "a handful" and with a thin ledger. The curve only started climbing sharply in living memory, and this guest is arriving right at the point it's cresting — which is *why* the mechanic feels tutorialized rather than rare. It isn't representative of the historical norm; it's the tail end of one, which is the whole reason this particular night is the one where the rule finally breaks.
- **The ledger was never a full census.** The house's memory is already established as selective and lossy elsewhere in this mythology (§2's "friend in the walls" is a residue the house has *forgotten*) — extending that same lossiness to guests is free, not new invention. Most guests over centuries left no durable trace at all; a portrait and a ledger line is something the house grants rarely, not a guarantee. Ten names is the short list of the ones it bothered to remember, not the total count of everyone who ever sat down.

## 6. Present-Day Motivation — CORRECTED, tells reframed as leakage rather than a separate stable channel

He wants it to stop. The already-shipped line *"…forgive me. Old habit, that last word. Pick up your hand. We begin."* reads, under this mythology, as a real crack in the compulsion.

**Correction, addressing a real gap the plot-hole review found:** Draft 1 described this as a second, separate thing from the escalating cheat compulsion — a stable "residual humanity channel" that stayed constant while the cheating itself calcified. The review correctly asked why centuries of a hardening compulsion would leave one specific outlet untouched. It shouldn't, and doesn't need to: **the tells aren't a deliberately maintained channel, they're leakage from the exact same failing mechanism, not a second one.** §5 already establishes that visibility gets sloppier and more legible at higher corruption tiers, because the compulsion is outpacing his control of his own face. Read that way, "planting a tell" isn't Aldric choosing to help you — it's the same erosion that makes him cheat more often and more visibly also making him worse at hiding what he wants. There is no protected, unchanging residue of his old self standing apart from the compulsion; there's one mask, failing on every seam at once, and the tells are what that failure looks like from the player's side of the table. This is a stronger, more unified model than Draft 1's two-track version, and it directly answers the review's question instead of leaving it open.

## 7. How the Six Endings Re-read — CORRECTED against actual ending text

Art Direction §13 gives the exact canonical text for all six endings. Cross-checking against it:

- **Escape, Pact, Collection** — Draft 1's readings of these don't contradict §13's text and add a layer of meaning on top without inventing new plot. Kept.

- **Replacement — reconciled, this is the most serious fix in this revision.** The shipped ending text is: *"GM removes his mask, nothing behind it, player sits in his chair."* Taken at face value, that's not "a sad, complicit person underneath" — it's an empty vessel, and the plot-hole review correctly flagged that this directly contradicts nine sections spent building Aldric as a specific, continuous, guilty person. It doesn't have to contradict that, if "nothing behind it" is read as being about what the *role* does rather than a claim about who Aldric always was. **The mask comes off because the transfer is already happening, and the transfer is what hollows it.** Replacement is the ending where the player wins by force — Defiance, an Inversion, no exposure, no understanding — which per Art Direction's own thesis is one of "the two 'victories' a player instinctively chases" that actually traps them. The player hasn't earned Aldric's interior life by beating him this way; they've earned the chair. What they see when the mask comes off is a preview of their own future in it, not a retroactive claim that Aldric was always hollow — the same way Aldric, centuries ago, presumably saw a real person in the losing host's face and it didn't stop the role from eventually grinding that person down too. The bargain doesn't erase who you were the instant you win it; it erases you slowly, the way it's been erasing Aldric this whole document. "Nothing behind it" is where that erosion ends up, not where it starts — which is exactly consistent with the corruption-tier system in §5 already describing a mask failing gradually rather than a mask that was never real.

- **Cheat / true ending — Draft 1's "two readings at once" claim is wrong and is cut.** The actual shipped ending text (§13) is unambiguous: the mask dissolves, resolves into the player's own face, **"Finally."** — freed, curse breaks, credits roll. There is no warning embedded in it, no dual meaning. Reading a second, darker meaning into it that the text doesn't support is exactly the kind of over-explaining the tone/genre-fit review flagged, and the plot-hole review separately caught that it doesn't even hold together on its own terms — if the game truly "voids, releasing everyone," there's no chair left for the "warning" half of the reading to be about. Art Direction's own thesis already does the necessary work without either problem: **"the two 'victories' a player instinctively chases — beat him hard (Replacement) or play nice (Collection) — are the two that trap you. Only understanding gets everyone out."** That's the twist. It doesn't need a second reading bolted onto the one ending that's actually clean.

- **Hollow — given a real answer instead of a one-line dismissal.** Draft 1 disposed of this in a sentence ("no mythology needed here"), and the plot-hole review correctly called that a shrug: Hollow is a scripted sequence (colors drain, one final game with blank cards and numberless dice, no credits), not a bare game-over screen, and it deserves the same "what actually happens" treatment as the other five. Answer: **a Hollow guest gets no portrait and no ledger line.** Collection at least earns a place on the wall — a name, a record, a kind of grim permanence. Hollow earns nothing, because there's no one left in the room to make the choice a portrait or a ledger entry requires; the house collects outcomes, and a mind that's broken past deciding anything hasn't produced one. The blank cards and numberless dice aren't a punishment being inflicted on the player so much as what's left to occupy a guest who can no longer follow a real game — the house isn't cruel here, just indifferent, still going through motions with someone no longer capable of playing them. This is the one ending that leaves no trace anywhere in the house, which is worse than the other five specifically because it's forgotten completely, without even a worn nameplate to show for it.

**Twist retired: Draft 1 twist #5 ("the true ending's resolution is a warning as much as a resolution") is cut entirely.**

## 8. The Hidden Room, the Shards, and the Puzzle — CORRECTED, ledger error fixed, Gallery dependency flagged for reassignment

**Factual error fixed:** Draft 1 claimed the ledger's illegible name is the *first* entry, "discoverable mid-to-late game." The actual shipped ledger text (`The Games Master - Entry Hall.dc.html`, written in Task 1 of the mansion-intro-polish pass) reads:

> Marr. Dufresne. Pike. Hale. Gall. Quill. Thale. Aubrey-Locke. A ninth, the ink gone soft where someone kept touching it, worn past reading. Every one crossed neatly through. At the foot, a blank line left open. My width, exactly.

The illegible entry is the **ninth and last** name before the player's own blank line — already deliberately written to parallel Percival's own worn nameplate (per that spec's own note). Draft 1's twist #3 ("the ledger's illegible name is Aldric's own") is also cut on the same logic as §2 above: Aldric was a guest at a different table, one loop earlier, under a different host — he has no reason to appear on *his own* ledger of *his own* guests. The illegible ninth entry is Percival's, full stop, matching the portrait. No further reveal is needed or earned here; the parallel between the worn ledger line and the worn nameplate is the payoff, and it's already shipped.

**The hidden room** (Art Direction §11, §18): Aldric's own original chamber, sealed since before he became host — kept as Draft 1 described it (original invitation letter, journal in his own hand). This doesn't conflict with anything in Art Direction; it's a reasonable, unstated elaboration on "the house remembers" (§17).

**The shards**: kept as Draft 1 proposed — broken mirror pieces, tying to the Mirror secret game and the "face resolves into the player's own face" ending image. Three or more, found via examine points.

**The unlock mechanism — flagged, not solved here.** Art Direction ties Gallery's hidden-room gate specifically to **"match his portrait — one portrait is Aldric as a guest, young, unmasked — the answer to the whole game... match it and it swings open"** (§5). Gallery is being replaced by Shut the Box. This is a real dependency, not a mythology problem — it belongs to Task 19 (designing Shut the Box), where the new room needs its own version of a rare, attention-and-skill-gated unlock that does the same job. Resolved in the Shut the Box design doc, not here — see that document for the replacement mechanism (a clean "perfect box" on the tile mapped to the worn ninth ledger name/Percival).

## 9. The "friend" collision — the one real contradiction, resolved

The consistency review's most important finding: Art Direction contains two different answers to "who is the friend," in two different sections, likely from separate drafting passes that were never reconciled against each other:

- **§11 "The Story Underneath"**: "the friend in the walls" = the previous host, an ambient, non-corporeal presence, deliberately kept unidentified.
- **§18 "The Story — the treatment"**: the invitation letter's three-stage reveal ends with **"a friend who never left. The hand is Aldric's own. He wrote the warning. He is the friend."** — a friend who is explicitly, definitively Aldric.

These cannot both be describing the same entity. **Resolution: they aren't the same entity, and the shared word "friend" is a coincidence worth making deliberate rather than leaving as an accidental collision.**

- **The letter's "friend"** (§18) is Aldric, writing to himself — or rather, to the next guest, the way he wishes someone had written to him. This is a concrete, physical clue: an object, a specific handwriting, a three-stage reveal with a clean payoff. Fully resolved, no ambiguity kept.
- **"The friend in the walls"** (§11, §10) is the previous host — ambient, unseen, cross-run-seeded, never confirmed, never given a face. Fully unresolved, ambiguity deliberately kept, per §10's own instruction.

**Going forward, these two devices should never be conflated in dialogue or clue text.** If the game ever needs to refer to both in the same scene, the letter's signer should be called "a friend" (matching its own text) and the ambient presence should be called "the friend in the walls" or "something in the walls" — never just "the friend" alone once both are live in the same sequence, to avoid recreating the exact collision found here. This is a house style note for future writing, not a new plot point.

## 10. Twists — Revised List

Draft 1 listed five; two are cut per the corrections above. What survives:

1. **Aldric was once a guest; winning transferred the host role to him, exactly like the Replacement ending.** *(§1, primary — this is Art Direction's own thesis, not new.)*
2. **The illegible ninth ledger entry parallels Percival's worn nameplate** — a quiet, unexplained repetition the player can notice without it being spelled out. *(§8, corrected.)*
3. **Aldric's small dialogue slips (already shipped) are real cracks in the compulsion, not writing flourish.** *(§6.)*
4. **The letter's "friend" and "the friend in the walls" are two different things that happen to share a name — and the game should keep them distinct on purpose.** *(§9 — this is a craft note as much as a twist, but worth flagging since a future draft could easily blur it back together.)*

Retired: "Percival is the previous Games Master" (unsupported, contradicts §11's non-corporeal design; independently confirmed by the plot-hole review noting Percival's portrait text uses the identical guest-arrival template as the other eight); "the true ending has a second, darker reading" (contradicts §13's actual clean text, and independently confirmed by the plot-hole review as a mechanical contradiction about whether the house voids or persists after the true ending).

The tone/genre-fit review's harshest note — that the premise itself is stock (Inscryption/Leshy-shaped) and only the Replacement-ending mechanism genuinely surprises — is accepted rather than argued with. This document isn't claiming novelty it doesn't have; the twist that lands is the one Art Direction already owns (§1/§11), and this document's job was to stop competing with it.

## 11. Explicitly Deferred / Open Questions

- **Who Aldric loved / played for, originally** — still deliberately unnamed. A forced parallel to the protagonist's own Mara/Nora would need to be earned, not asserted; not doing that here.
- **Gallery→Shut the Box's hidden-room mechanism** — resolved at the design level in the Shut the Box document (Task 19), not duplicated here.
- **Ending table is design intent, not shipped code** (plot-hole review, minor) — see the precision note under "Why this draft exists." Whoever implements the six endings should treat §7 and §13 as the spec, not assume the mechanical hookup already exists.

All four review lenses (tone/genre-fit, consistency, game-design coherence, plot-hole hunting) have now returned and been addressed directly in the sections above. Nothing from any of the four is left open except the two items in this list, both of which are genuinely deferred by design rather than unresolved by oversight.
