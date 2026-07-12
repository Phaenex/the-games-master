# Mansion & Intro Polish — Design

## Context

The Games Master currently has three playable scenes (Prologue, Entry Hall, The Parlor), all verified working via the project's Test Harness. Two things were flagged as feeling thin against the intended experience:

1. The transition from the Prologue's front door into blackout never actually shows the mansion's interior — it cuts from "the doors swing open, warm light spills out" straight to "a hand grabs your collar, floor rises, black."
2. Of the 10 portraits already modeled in the Entry Hall (see `addPortrait` calls), only 1 (the deliberately faceless one) has any text attached. The other 9 are pure decoration. The existing ledger POI ("Names, down the page... every one crossed neatly through") gestures at previous guests but names none of them.

This spec covers three additive changes, none of which touch existing passing behavior: named portrait content tied to the ledger, one new narrative beat in the Prologue's arrival sequence, and a small sensory addition to the existing wake-up.

## 1. Portraits & Ledger

**Mechanism**: Reuses the existing POI pattern exactly (`buildPOIs()` in `The Games Master - Entry Hall.dc.html`) — no new systems. Each of the 9 painted portraits gets its own `type:'examine'` entry positioned at its real x/z (from the existing `addPortrait` calls: left wall x=-7.68 at z=[10,3,-4,-13,-21]; right wall x=7.68 at z=[12,5,-11,-19] — z=-2 stays the existing faceless portrait, untouched). Verb: `'Read the nameplate'`. `em` is a one-line framing action; `es` is the content below.

Tone is deliberately mixed per confirmed direction — some echo the protagonist's own desperation, some are foolish or cold, one is darkly wry. 5 of 9 carry a clue (mechanical tell or house lore), woven into the prose rather than flagged as a "hint." The 9:

1. **Left z=10 — Edwin Marr** (sympathetic · mechanical clue)
   > EDWIN MARR. The paint has him mid-turn, same as all the rest, but his eyes haven't quite caught up to his shoulders — he's still watching the table over his own back. A tell, I'd learn later, cheats give right before they play the impossible card: they look anywhere but at their hands. Edwin must have noticed too. Too late for it to save him.

2. **Left z=3 — Caspian Dufresne** (foolish/prideful · no clue)
   > CASPIAN DUFRESNE, self-declared the finest card hand in three counties, according to the plate — though the plate is the only place that claim survives. He came, I'd guess, the way men like that always come to a game they can't lose: certain, cheerful, already composing the story he'd tell after. There's no second portrait of him. There didn't need to be.

3. **Left z=-4 — Halvard Pike** (cold/calculating · lore clue)
   > HALVARD PIKE. An actuary, or so I'd guess from the ledger's second column, where someone had noted his trade in a hand too neat to be his own. He'd have come with odds worked out to the decimal, certain a fair game has no cheat that arithmetic can't survive. What he hadn't worked out, I'd learn: the game was never fair, and losing was never really the point. Winning was going to cost him something the numbers didn't have a column for.

4. **Left z=-13 — Solveig Hale** (sympathetic · no clue, deliberately withheld)
   > SOLVEIG HALE. A debt of her own on the ledger's third column, in the same neat foreign hand — a sister's name beside it, and a hospital I didn't recognize. I understood her before I read a word further. I did not need a clue from her portrait. I only needed to know I wasn't the first person this house had made a bargain with, and wouldn't be the last.

5. **Left z=-21 — Theo Gall** (cold/predatory · no clue)
   > THEO GALL. No trade beside his name, no debt, nothing the ledger thought worth recording — which told me plenty on its own. A man who comes to a game like this owing nothing usually comes to take something instead. I don't know what Theo tried. I know only that his portrait, like all the rest, hangs facing away from the table, and that his shoulders, even in paint, look like a man still walking backward out of a room.

6. **Right z=12 — Barnaby Quill** (foolish/comic · lore clue)
   > BARNABY QUILL. Arrived, by the look of him, three drinks into a party he hadn't been invited to and was delighted to have found anyway — the only face on this wall with anything like a smile still on it. I almost envied him that, until I noticed what the smile was aimed at: not the viewer. The stairs. Whatever he found up there, going up laughing, I don't think he came down the same way. There are more doors in this house than the one I walked through.

7. **Right z=5 — Imogen Thale** (sympathetic · no clue, deliberately withheld)
   > IMOGEN THALE. No trade, no debt column, no note at all beside her name — whoever kept this ledger either didn't know her reasons or thought better of writing them down. I found I didn't want to guess. Some of the losing, I think, isn't mine to read.

8. **Right z=-11 — Constance Aubrey-Locke** (cold/aristocratic thrill-seeker · mechanical clue)
   > CONSTANCE AUBREY-LOCKE. Old money, by the double-barreled name and the fur still painted at her throat — the kind of guest, I'd guess, who came the way other people go hunting: for the thrill of a thing that could, in theory, kill you, with none of the actual risk expected to land. In the portrait her chin is lifted a fraction too high. Aldric lifts his the same fraction, I'd notice later, in the half-second before he plays a card he has no right to be holding.

9. **Right z=-19 — Percival [surname worn away]** (darkly wry · lore clue)
   > The plate's been rubbed half through by hands other than mine — only PERCIVAL survives it. Whoever he was, someone came back to this one more than the rest. I couldn't blame them. There's something almost comic in his portrait, chin low, shoulders slack, a man who'd clearly lost everything worth losing before he ever sat down at this table — for whom the stakes must have felt, absurdly, like a relief. I hoped, looking at him, that losing wasn't the same as the ledger made it look. I was less and less sure, the longer I looked.

**Ledger update** — the existing entry's `es` text gets expanded from generic ("Names, down the page...") to actually list these names, so the two systems visibly correspond:

> Names, down the page, in a hand too neat to be any one guest's own. Marr. Dufresne. Pike. Hale. Gall. Quill. Thale. Aubrey-Locke. A ninth, the ink gone soft where someone kept touching it, worn past reading. Every one crossed neatly through. At the foot, a blank line left open. My width, exactly.

(The "worn past reading" ninth line deliberately parallels Percival's own worn nameplate.)

## 2. Door → glimpse → dark

**Current behavior** (`The Games Master - Prologue.dc.html`, the `arriving`/`knockStarted` block): once `arrDoor` reaches 1 (doors fully open), a hold timer (`arrHold`) counts up; at `arrHold > 0.9` seconds, `knockStarted` flips true and the grab/blackout sequence begins immediately. There's no beat fired in that window — it's currently just a bare pause.

**Change**: extend the hold window from 0.9s to ~2.0s, and the first time `arrDoor` reaches 1, fire one `speak()` beat (the same mechanism the walk-up narrative beats already use) describing the glimpse:

> Beyond the widening gap: checkered marble running back into gold light, a staircase climbing into dark, and something enormous and bright hanging dead center of it all, swaying very slightly, though no one had touched it.

This is deliberately text-only, not new 3D geometry — the walk sequence already carries its emotional weight through prose over a comparatively simple visual, and actually rendering a peek would mean duplicating a chunk of Entry Hall's scene-construction into Prologue for a few frames of screen time. The line also quietly foreshadows the real Entry Hall (checkered floor, staircase, chandelier) without naming any of them outright.

## 3. Wake-up / headache

**Current behavior** (`The Games Master - Entry Hall.dc.html`, `beginWake()`): camera tilts as if lying down (`wakeRoll:1.15, wakePitch:0.52`), holds, then rises smoothly over ~11.4 seconds while "How long had I been under?" displays.

**Change**: add a subtle pulsing vignette synced to the same timeline — a fixed-position radial-gradient overlay (darkened edges, center clear) whose opacity oscillates on a slow ~1.1s period, strongest at the start of `waking` and fading out as `rising` completes. This is a small additive CSS/inline-style element following the same pattern as the existing letterbox/vignette overlays already in the scene — not a new system, just one more state-driven div.

## Testing

None of these changes touch the `window.__GM` bridge contracts the Test Harness already asserts on (scene builds, phase transitions, POI count, no runtime errors) — the portrait additions raise the walkable-hall's total examinable POI count from 2 to 11 explicitly-examinable entries (9 new + the existing faceless portrait + ledger), but the harness's assertion is `pois===11` for total POI count already, which counts these new entries. **Note**: this needs verification against the actual current `pois` count once implemented — if adding 9 portrait POIs pushes the total above 11 (the harness's hardcoded expectation), the Test Harness's own assertion (`scene builds with 11 points of interest`) will need updating to match the new true count, since 11 was the count *before* these portraits existed as POIs.

Verification plan:
1. Run `npm test` — confirm the harness still passes (updating its hardcoded POI count if needed, per the note above).
2. Manually walk the Entry Hall in a real browser, examine all 9 new portraits + the ledger, confirm text displays correctly and no portrait POI overlaps/conflicts with another (positions are far enough apart per existing spacing, but worth a visual check).
3. Manually play through the Prologue's arrival sequence, confirm the glimpse beat displays and doesn't visually clip with the grab/blackout that follows immediately after.
4. Manually trigger Entry Hall's wake-up (`goTo('wake')`), confirm the vignette pulse renders and doesn't obscure the existing "How long had I been under?" text.
