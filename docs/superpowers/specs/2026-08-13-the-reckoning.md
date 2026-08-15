# The Reckoning — Contextual Grounds Pressure

> **Partially supersedes `2026-07-17-the-ninth-bell.md`.** The bell survives whole: nine tolls,
> diegetic, inevitable, monotonic symptoms, the same crossing. What changes is **the spacing**
> between tolls — it now reads what the player did on the grounds instead of ignoring it.
>
> Owner decision, 2026-08-13. Companion to `2026-07-17-the-ninth-bell.md` (the bell itself) and
> `2026-07-14-house-history.md` (the layer model this doc borrows its grounding principle from).

---

## The decision

The ninth-bell spec's whole thesis was that the count ignores the player: *"no matter what you
do, you are counted in at nine."* That stands. What's added is a second thing that was never
true before: **the estate's outbuildings become genuinely enterable**, not shut-door facades,
starting with the coach house (Phase B of the implementation plan this doc accompanies). Letting
the player actually go somewhere and do something, while the count stays fixed and blind to it,
wastes the one lever that would make exploring feel like it mattered — for better or worse. So
the count now listens. It still can't be stopped, bargained with, or outrun past a hard window —
only *nudged inside it*.

## The name

**The Reckoning** — the coaching-inn word for the bill a guest settles before leaving
(`house-history.md`'s L4 layer), and it lands on the narrator's own arithmetic:

> *"Forty-one thousand, to men who don't take excuses. Nine thousand more to the bank."*

A man who's been doing this kind of sum to himself all night is a fitting source for a system
that's silently doing sums about him. The word is internal and code-only — it never appears in
UI, HUD, or player-facing text.

## What the house counts: privacy, not curiosity

The grounding rule, borrowed directly from `house-history.md`'s layer stack: **the house minds
its private rooms far more than its public ones.** L4 (the coaching inn — taproom, stable yard,
lodgers' rooms, spaces every paying guest always had a right to) costs little to enter. Layers
closer to L2/L3/L5 (the failed private manor, the members' club, the underside) cost more. This
gives a principle to weigh new outbuildings against instead of a pile of disconnected numbers —
the coach house is the L4 layer "made physical" per `full-grounds-plan.md`, so it's deliberately
the cheapest room on the estate to enter.

## The guarantee

*The house always counts to nine, and it always finishes between a configured minimum and
maximum number of seconds after the gate turns. What the player did decides where in that window
it lands. Nothing the player does moves it outside that window.*

Mechanically: every toll's interval is drawn from

```
Iᵢ = clamp( baseᵢ × (1 − authority×pressure) × (1 + jitterᵢ),  floorᵢ,  baseᵢ )
```

clamped on both sides, so the running total is bounded above (nothing outruns it) and below
(nothing collapses it to a stopwatch), for any player behavior. The interval for toll *N* is
fixed the instant toll *N−1* rings — pressure accrued afterward can only touch the *next* toll,
never one already scheduled. Toll count itself never varies: it is asserted as exactly nine
elsewhere in the codebase (`GmPerceptualAudit`, `GmSymptoms`' fixed-size arrays), and the shipped
narrative — nine names, nine tiles, nine portraits, "COUNTED OUT" — depends on that number being
fixed. Only the spacing moves.

## What raises pressure

Monotonic by construction — the accumulator rejects any negative or NaN delta. Nothing the
player does lowers it. Leaving a building doesn't give time back. There's no cure. Free signals
that cost nothing new: first entry into each of the 7 authored branch rects, first examine of
each of the 13 grounds POIs. The coach house (Phase B) adds: first entry, capped dwell time, and
its multi-line clue (each line costs more than the last, so restraint is a real, legible choice).

## No meter, ever

The only feedback is the bell arriving sooner, and one narration line the first time pressure
crosses a threshold. **No bar, gauge, or numeric readout is ever shown to the player.** This is
a design lock, not an oversight — a visible meter turns dread into a resource to manage, which
is the opposite of what a house that "needs no thug" (per the ninth-bell spec's own framing) is
supposed to feel like.

## Seeding

A single per-run seed drives every random draw in the system (toll jitter, and anything added
later), each as its own independent stream derived by hashing `(seed, streamName)` — never a
shared generator. No draw ever happens inside a per-frame `Update()`; every draw is bound to a
discrete event (a toll boundary, a first entry), so `(seed, ordered events) → exact schedule` is
a pure, replayable function. Same seed, same run — useful for playtest reports ("seed 8837 felt
too fast") and for deterministic gates/tours, which force a fixed seed the same way they already
force a fixed review profile.

## Corruption stays separate

The Reckoning is its own Prologue-scoped stat, not `GmRunStore`'s corruption tier. Corruption
gates Ending D, has an open, unresolved ceiling conflict between `GmHouseProgress` (4) and
`GmRunStore` (5) (`docs/TASKBOARD.md` lane F6, still awaiting Nick), and is meant to reflect
whole-run failures across all seven games — not whether someone poked around a shed. A single
config-gated, default-off bridge exists for later use, routed through `GmHouseProgress`'s
4-ceiling path specifically so it can never hand a player Ending D on its own while F6 stays
open.

## What's still open (not decided by this doc)

1. Does exploring make the house hurry, or buy the player more time? A single signed dial
   (`reckoningPressureAuthority`) controls this. Ships at `0` — no change from today — until
   answered on a walk.
2. How much randomness. Ships at zero jitter until tuned.
3. Whether the chapel, shed, or a new icehouse open in a later phase, and in what mode — see
   the implementation plan's "Open decisions" for the specific chapel argument (diegetic bell
   source, worth more shut, vs. lets the player pull the rope and answer a toll early). Not
   decided here; decide after Phase B ships and gets walked.
4. Whether the 13 existing grounds POI examines should start banking persistent discoveries the
   same way outbuilding clues will. Not in this pass.

## What this needs built

See the implementation plan for the full breakdown. In dependency order: the pure schedule
function and seeding (zero behavior change by default) → the pressure accumulator wired to
existing free signals → the coach house interior, its clue persistence, and the probe/registry/
gate wiring that proves it.
