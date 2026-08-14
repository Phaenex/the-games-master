# Panel review — the opening (WendHill_Prologue), 2026-08-15

Six independent reviewers with deliberately conflicting stakes judged the opening 5-10 minutes as a
designed experience against a AAA psychological-horror bar: a horror creative director, a
systems-breaker/speedrunner, a mainstream newcomer, a narrative designer, an onboarding &
accessibility consultant, and a genre-literate target-audience fan. Two rounds plus a chair
synthesis, 14 agents.

## Verdict

**6 of 6: FAILS_AAA_BAR. 0 of 6 would sign it off even if their own top three were fixed.**

Consensus was **not** reached on ranking (3/6 on the top blocker), and that is reported honestly
rather than smoothed. The chair's read: blockers 1 and 2 are one defect, and under that merge all
six rank it first or second — real convergence on rank one only.

> "Zero of six reviewers would sign this off with their own top three fixed. That number is the
> verdict, not the ranking above it."

## The five claims I verified myself before accepting any of this

Every one of these was checked against the source, not taken on the panel's word.

1. **The bell never arms for an obedient player — the prologue does not play.** `bell?.Arm()` has
   exactly two production call sites: `GmThreshold.cs:63` and `GmVillageSave.cs:93` (restoring an
   already-armed save). The first sits inside `lockNow`, which requires
   `maxProgress >= gateAnchor.RouteMetres + 1f && progress < maxProgress - 0.5f` — the player must
   pass the gate **and then move backward**. `GmBellSummons.Update()` returns immediately unless
   armed. So a player who does exactly what the invitation says — walk to the house — reaches the
   porch, reads one line, and stands there forever. No tolls, no crossing, no Entry Hall. The entire
   nine-count is gated behind an optional glance over the shoulder.
2. **The first line inside the house contradicts the hard canon lock.** `GmHouseBeginning.cs`
   `IntroLines[0]`: *"The doors give without a sound—the way a house opens when it has been
   expecting you."* Two minutes after the porch refusal, in the same voice and the same image. The
   canon rule is that the house's own front doors never open.
3. **Beats run at 640-730 wpm.** Dwell is hardcoded `Time.time + 4.2f` at
   `GmDesignRuntime.cs:167` and `:195`, against authored beats of 45-51 words. Comfortable adult
   silent reading is ~238 wpm; subtitle guidance 160-180. No pause, no hold-to-read, no journal, no
   replay.
4. **The payoff of the nine-count never reaches the screen.** `GmBellSummons` calls
   `rt?.ShowBeat("The card said nine." ...)` and `threshold?.BeginCrossing()` on consecutive lines
   in the same frame. The crossing takes the screen before the card can be read.
5. **The game muffles the bell it asks you to count.** `GmSymptoms.cs:62-64` adds an
   `AudioLowPassFilter` to the camera's **AudioListener**, and `:111` walks its cutoff down as tolls
   advance (to 500Hz by toll 9). The bell is a plain 3D `AudioSource` (`GmBellSummons.cs:71-72`)
   with no separate mixer routing, so the sanity system progressively eats the count's only output
   channel — and the ending is arithmetic over that count.

## Ranked blockers (chair's final order)

| # | Blocker | Needs Nick? |
|---|---|---|
| 1 | The refusal and the gate lock are the same defect: both are scalar comparisons against `route.ProjectDistance`, thirty lines apart. One root cause, three shipped failures. Nobody ever put a hand on that door, which is why a caption about a door prints while the player walks through the drawing room. | No |
| 2 | The narration performs the game's verb and the player is handed the button last. ~12 authored grounds discrepancies are exactly the shape of a caught cheat; in every case the narrator catches it and the player pressed E to hear about it. The Read verb is then gated behind `suspicion >= 2`, raised only by *letting two cheats past*. | **Yes** |
| 3 | Aldric is absent from his own opening. Scored zero in the tally but appeared in four of six "what would tip it over" lists — reviewers ranked what is broken above what is missing. | **Yes** |
| 4 | The nine-count's payoff line never renders (claim 4 above). | No |
| 5 | The first line inside hands back the civil doorway the opening exists to refuse (claim 2 above). | **Yes** |
| 6 | Nobody has played this end to end, and the verification apparatus is pointed at the opposite property: 328 passing tests measure whether the player can *reach* things, in a game whose thesis is being *stopped*. | No |
| 7 | The grounds fail all three jobs the design doc assigns them — the drive is not composed, it is inferred at runtime by regex-matching road meshes out of a purchased Abandoned Village scene and walking the densest cluster from its sparse end. No human placed the turn the game is named after. | **Yes** |
| 8 | Beat dwell vs word count (claim 3 above). | **Yes** |
| 9 | The count has one channel and the game filters it out; Reduce Motion and Text Scale have no consumers (claim 5 above). | No |

## What the panel said to preserve — the only unanimous item

**The prose.** All six, independently, and several quoted the same lines without coordinating:
*"It's a common name. It's a common name."* · the coins fused heads-down · small boots to the shed
door and none coming back · the scarecrow in better tailoring than his · *"Fresh. No name yet."*
The newcomer — not a writer, self-described as jaded — said it beat anything in the last three
horror games he bought.

The narrative designer's framing is the one to hold: **the objects are about the narrator's
situation, not the house's lore**, and the writing would collapse if moved to a different narrator.
Every reviewer proposing a systems or accessibility pass attached the same warning: do not sand this
into examine-flavour or safer explanatory prose. Blockers 2 and 8 are the two most likely to do
exactly that.

Also preserve: **nine as a structural spine** (nine names, nine tiles, nine portraits, nine tolls —
never let contextual spacing produce eight or ten); **the porch anticlimax as an idea** (it needs the
door to be physical and *wanted* first); **the bell over the hand** and the reasoning that rejected
the porch KO, which is what makes the optional grounds possible at all; **the secret ending**, which
the genre fan calls the best decision in the documents because it is the only moment where player
judgment defeats the house rather than decorating it; **the crossing's construction** exactly as
authored — cut rather than fade, ~2s of nothing, hearing returning before sight; **the shut chapel**;
and the codebase's own honesty in its comments.

## The bottom line, verbatim

> "The design is not the problem in most places, and it is the problem in two. Where the panel could
> point at prose, at the nine-count as a structure, at the bell beating the hand, at the porch
> anticlimax and at the secret ending, this would hold at AAA, and the writing would be better than
> most of what it shipped against. Where it would not hold is that the design itself never puts the
> host in his own opening and never hands the player the verb the entire seven-game structure is
> built on, and no amount of competent building fixes an omission at the design layer. Everything
> else on the list is an implementation failure of a good design, repeating one specific mistake:
> the beats were authored as sentences about physical facts, so the physical facts were never built,
> which is why a caption about a door prints while the player walks through the drawing room. That
> part is weeks of known work. The part that is not weeks of work is that nobody on this project has
> played the thing end to end, no ending has ever resolved, and the 328 passing tests measure
> whether the player can reach things in a game whose thesis is being stopped, so every taste
> judgment in the design docs is currently a guess with a citation, including the ones the panel
> praised."
