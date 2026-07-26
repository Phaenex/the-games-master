# Court Evidence Deck — Draft Copy

Placeholder prose for Phase 1. Tone: dry exhibit labels, not game tutorials. True card must be discoverable; decoys must sound equally official.

When the Court scene lands, these become the `EVIDENCE` array. `isTrue` / `isShard` drive mechanics; player never sees those flags.

---

## Deck A — first-visit (straight trial, loseable)

Use on first Court visit / low corruption. No planted ordering. No gavel tarnish.

1. **Ledger extract, night of arrival**
   - Body: "Nine names. One blank line where a tenth would fit. The ink on the blank line has taken the paper's grain the way old ink does — as if something was written, then scraped."
   - isTrue: false

2. **Host's mark, wax seal 'V'**
   - Body: "The same seal that closed the invitation. Present on three exhibit slips from three different years. The wax has never cracked unevenly — always the same fault line."
   - isTrue: false

3. **Guest register fragment — Percival**
   - Body: "Last sat: before dawn. Notation in a second hand: *kept his own counsel*. The second hand matches no clerk entry elsewhere in the book."
   - isTrue: **true** (straight trial: this is the clean procedural tell if the player connects it to Entry Hall Percival)

4. **Chandelier inventory note**
   - Body: "One fixture enumerated twice. Once as brass. Once as 'not brass — do not polish.'"
   - isTrue: false

5. **Door-latch schedule**
   - Body: "Outer gate: locked from inside after third guest clears the drive. No key assigned to staff. No key listed at all."
   - isTrue: false

---

## Deck B — rigged visit (corruption high / revisit)

Same labels, different order pressure: true card surfaces earlier more often. Presenting the true card triggers **gavel tarnish**.

True card (rigged tell version):

**3R. Guest register fragment — Percival (annotated)**
- Body: "Last sat: before dawn. Notation in a second hand: *kept his own counsel*. Beneath, in the host's own press: *leave this one*. The ink of the second note is wetter than the first by years it cannot explain."
- isTrue: **true**
- On present while rigged: gavel gold → dull; beat line optional: "The brass went quiet in his hand, as if the metal had remembered something before he did."

Decoy that enables mid-trial misfile stumble:

**6. Bailiff's summons (undated)**
- Body: "To be read aloud. Charge omitted. The blank where the charge would sit has been folded so often the paper shines."
- isTrue: false
- When misfiled: voice stumble — prosecutor cadence breaks into judge cadence mid-sentence.

---

## Shard exhibit (Phase 3 wire; can live in deck early as inert)

**S. Broken looking-glass, incorrectly labeled 'hand mirror — guest luggage'**
- Body: "Silvered glass. Edge that cuts both ways. Catalogued under luggage though no guest ever claimed a bag."
- isTrue: false
- isShard: true — examine/collect increments shard count; not the card that wins the case.

---

## Wax-seal HUD copy

- Sealed: "Argument unmade"
- Cracked: "Argument stands"
- Three cracked → case closed for the player (win)
- Zero cracked + time/pressure out → loss on straight trial

---

## Notes for implementation

- Never put `isTrue` in on-screen text.
- Deck A must be honestly loseable if the player presents decoys.
- Deck B only after reactive-risk gate fires (see cheats design §2.1).
- Keep cards short — fan UI at lower edge has little room.
