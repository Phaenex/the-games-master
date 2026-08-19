# Seven Debts: canonical seven-game night

> Status: approved by Nick on 2026-08-19 after the
> [five-participant review](reviews/SEVEN-DEBTS-PANEL-2026-08-19.md) reached 5/5 consensus.

## Canon decision

The seven table games are:

1. Flames
2. Shut the Box
3. Bones
4. Study
5. Wager
6. Black Ledger
7. Last Candle

Court, the Hidden Room, and the Labyrinth remain story chapters. They never increment the table-game
count.

The player-facing sequence is:

```text
Flames
  -> Court
  -> Shut the Box
  -> optional Hidden Room detour, then return
  -> Bones
  -> Study
  -> Wager
  -> Black Ledger
  -> Labyrinth
  -> Last Candle
  -> Ninth Bell and ending resolution
```

## Rules shared by the five new games

- Rules run from deterministic state, legal actions, and a stable action log.
- The game saves before every player decision and immediately after terminal resolution.
- A loss changes the run but never blocks the next room.
- Completing a table increments the table-game count once, regardless of result.
- Win adds 2 Defiance. Loss adds 2 Compliance and removes 0.05 Sanity. Tie adds 1 Defiance and
  1 Compliance.
- Each game permits at most one Aldric intervention.
- An intervention is eligible only when the player's action wins the match or creates a proven forced
  win, and the intervention changes that outcome.
- Challenge restores the logged state from before the intervention. Proceed accepts the changed
  state.
- Opposing an intervention adds a durable clue. These five games never call `RecordCatch`.
- The existing eight-catch true-ending calculation remains owned by Parlor, Court, and Shut the Box.
- Every required fact uses text plus persistent shape, position, motion, or icon evidence. Audio,
  color, flicker, and haptics are supporting channels only.
- Decisions are untimed. Controller focus, text scaling, high contrast, reduced motion, pause, and
  save/resume work at every decision boundary.
- Prior results remain on a shared sideboard: ivory seal for a win, black seal for a loss, upright
  center seal for a tie.

## Game 3: Bones

Bones is a three-round bank-or-press dice game.

- Both sides take one turn per round.
- The player acts first in rounds 1 and 3. Aldric acts first in round 2.
- A turn starts by rolling three standard dice.
- Bank scores their sum.
- Press locks exactly one die and rerolls the other two once.
- If either rerolled die is 1, the turn busts for zero.
- Otherwise the turn scores all three dice plus a 3-point press bonus.
- Highest total after three rounds wins. Equal totals tie.
- Aldric presses when the first sum is 9 or lower. On his last turn, he also presses when banking
  would leave him behind. He locks the highest die, breaking ties by lowest slot ID.

If Aldric's final result loses, and replacing one rolled bust value of 1 with a palmed loaded 6 turns
the result into a tie or win, he may make that substitution. The weighted die sits unnaturally flat,
the physical count disagrees with the action log, and the changed die keeps a persistent marker.
Challenge restores the 1.

The player makes at most three normal choices plus one intervention response. Bones must ship as a
complete vertical slice, including presentation, audio, controller input, accessibility, save/resume,
full-match proof, and human play, before the other four games receive production estimates.

## Game 4: Study

Study is three authored chess positions, not a general chess engine. Each position shows three legal
move cards, board zoom, and a plain-language description of the tactical consequence. Two correct
answers win the match.

The source positions are engine-verified:

| Position | FEN | Correct | Alternatives | Arbiter override |
|---|---|---|---|---|
| The Captured Record | `2Q5/8/8/8/7K/4p3/3R4/7k w - - 0 1` | `Qc1#` | `Qa6`, `Qa8+` | black pawn captures `e3d2`; `Qc1` stays legal but is no longer mate |
| The Closing File | `8/8/1R6/8/1p6/k7/7K/1Q6 w - - 0 1` | `Ra6#` | `Qa1+`, `Qa2+` | black pawn moves `b4b3`; `Ra6` stays legal but is no longer mate |
| The Quiet Rank | `7k/5Q1p/8/8/5R2/8/8/5K2 w - - 0 1` | `Qf8#` | `Qa2`, `Qa7` | black pawn moves `h7h6`; `Qf8` stays legal but is no longer mate |

If a correct choice would give the player their second point, Aldric may apply that position's extra
pawn move before judgment. The original and changed squares remain visible as static markers in
reduced-motion mode. Challenge restores the original snapshot.

## Game 5: Wager

Completing Flames, Shut the Box, Bones, and Study grants one house sovereign each, whether the player
wins or loses. Wager therefore always begins with exactly four usable sovereigns.

- Three sealed contracts contain net values `-2`, `+1`, and `+3`, assigned by the run seed.
- Contracts appear one at a time.
- Reading the first hidden clause costs 1 sovereign, the second costs 2, and the third costs 3.
- The player cannot afford to read all three.
- After an optional Read, the player accepts or passes. Passing the first two forces acceptance of
  the third.
- Final wealth is unspent sovereigns plus the accepted contract's net value.
- Wealth 5 or higher wins, 4 ties, and 3 or lower loses.

If an accepted contract would win, Aldric may swap its clause with the lowest-valued other clause
whose substitution changes the result to tie or loss. Contract ID breaks a tie. Both inserts swap so
the original permutation remains intact. Broken wax, the original insert edge, the signed-value log,
an icon, and plain text expose the change. Challenge restores both inserts. The signed contract also
changes one later dialogue or access detail.

## Game 6: Black Ledger

Black Ledger is one authored case involving Edwin Marr, Halvard Pike, and Constance Aubrey-Locke.
Aubrey-Locke is responsible for altering the debt record.

Six leads are split into three visible categories:

- Record: overwritten ledger row or pressure-mark blotter
- Trace: violet seal wax or inked glove fibers
- Testimony: Marr or Pike

The player inspects exactly one lead from each category, then accuses one guest using two inspected
citations. Every one of the eight possible inquiry sets contains at least two independent facts that
support the true accusation. Evidence records carry authored `supportsGuestIds`; unrelated citations
are rejected without consuming the accusation. Ambiguous evidence can still support a wrong but
logically coherent accusation, which loses the case.

If a submitted accusation would win, Aldric may slide Pike's nameplate over Aubrey-Locke's after
submission. The immutable log still names Aubrey-Locke and the moved plate leaves a clean rectangle
in the dust. Challenge restores the submitted guest. A win turns Aubrey-Locke's portrait to the wall;
a loss shrouds the wrongly accused portrait.

## Game 7: Last Candle

Last Candle uses two contiguous arms of three and four white candles, plus one central black candle.

- On a turn, snuff one or two adjacent candles from the outer end of one arm.
- When both white arms are empty, the next player must snuff the black candle and loses.
- Taking the final white candle therefore wins.
- The player moves first.
- The starting `[3,4]` state has two winning openings and is not a forced loss.
- Exhaustive deterministic play caps the player at four decisions.

Aldric uses the solved state table with action ID as the final tie-break. If a player move leaves him
in a proven forced loss, he may relight one legal boundary candle. Candidate actions are evaluated as
`relight-left`, then `relight-right`; he uses the first that changes the solver result. If neither
does, he cannot intervene. The socket count, action log, persistent outline, and text expose the
relight. Challenge restores the spent candle.

A player win leaves the last hall practical burning through ending resolution. A loss darkens the
hall one fixture at a time. That presentation does not change ending selection.

## Production order

1. Shared deterministic game shell, save boundary, accessibility state, intervention record, result
   reporting, and proof harness.
2. Bones rules tests and complete vertical slice.
3. Freeze the shared shell API after Bones passes its human play gate.
4. Study.
5. Wager.
6. Black Ledger.
7. Last Candle.

The content caps in this document are ceilings. Expanding one requires a new explicit scope decision.
