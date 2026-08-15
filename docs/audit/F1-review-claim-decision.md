# F1: ReviewClaim / Claim mismatch, and what was done about it

Status: **interim adapter shipped, three decisions open for Nick.**
Touched: `unity/scene-system/Runtime/GmSceneComposition.cs` only. No composition plan was edited.

## The mismatch

Six scaffold composition plans call a method that did not exist:

```
unity/scenes/court/Editor/GmCourtCompositionPlan.cs          8 calls
unity/scenes/entry-hall/Editor/GmEntryHallCompositionPlan.cs 8
unity/scenes/hidden-room/Editor/GmHiddenRoomCompositionPlan.cs 6
unity/scenes/labyrinth/Editor/GmLabyrinthCompositionPlan.cs  8
unity/scenes/parlor/Editor/GmParlorCompositionPlan.cs       10
unity/scenes/shut-the-box/Editor/GmShutTheBoxCompositionPlan.cs 8
                                                     total  48
```

Every one of the 48 has the identical shape (verified by parsing the calls, not by eye):

```csharp
GmCompositionAuthoring.ReviewClaim(owner, shotName, primaryId, secondaryId,
    scopeA, scopeB, new Vector2(tx, ty), tolerance, claimText);
```

What actually existed was `GmCompositionAuthoring.Claim`, with a different arity, a different
parameter order, and three element arrays instead of one secondary id:

```csharp
Claim(GameObject owner, string shotName, string claim, string primaryElementId,
      string[] foregroundIds, string[] supportingIds, string[] backgroundIds,
      Vector2 target, Vector2 tolerance,
      float minimumViewportHeight = 0.03f, float maximumViewportHeight = 0.9f)
```

`Claim` had **zero callers anywhere in the repo** before this change. Neither side of the mismatch
was load-bearing or proven. The 48 sites are the first and only consumers of this API.

## Options considered

| Option | What it does | Verdict |
|---|---|---|
| (a) Thin `ReviewClaim` adapter onto `Claim`, scope args dropped | 1 small method, 48 sites compile unchanged | **Implemented**, marked interim |
| (b) Map the scope args into `backgroundElementIds` | Reject | Manufactures up to 96 false findings, see below |
| (c) Rewrite all 48 call sites to call `Claim` directly | Right long-term destination | Deferred until the open decisions land |

**(b) is measurably wrong, not a judgment call.** I resolved all 96 scope arguments against the
element/cluster/zone ids each plan declares: **0 of 96 are element ids** (36 are cluster ids, 60 are
zone ids). `GmSceneCompositionAudit.ValidateClaimElements` resolves background ids through the
element map only, so (b) would emit `review shot 'X' background references missing element
'table-zone'` on every claim in all six scenes. A gate that always fails for a reason that is not
true gets bypassed, and that is how a hard gate turns into no gate.

**(c) was deferred, not rejected.** It is a reorder rather than an insert (claim text moves from
arg 9 to arg 3, element id from arg 3 to arg 4), so 48 hand-edits are exactly the shape of change
that silently swaps a pair. More to the point, (c) does not answer the tolerance question below. It
just makes the same unconfirmed guess 48 separate times, in 48 places where it cannot be reviewed as
one policy. Do (c) after the decisions land, in one deliberate pass.

**`[Obsolete]` on the adapter was considered and skipped.** It would broadcast the debt on every
build, which is genuinely better than silence, but it puts 48 warnings into the Editor assembly and
`RULES.md` bans TODO-shaped placeholders. The comment block on the method carries the same
information without risking a warnings-as-errors build. This document is the durable record.

## What was implemented

Two changes, both in `unity/scene-system/Runtime/GmSceneComposition.cs`.

### 1. `Claim` is now idempotent, keyed by shot name

`Claim` was the one factory in `GmCompositionAuthoring` that always did `owner.AddComponent<T>()`
while every sibling (`Zone`, `Cluster`, `Element`, `Route`, `Reserve`, `Motivate`, `Begin`) resolved
through `AddOrGet<T>`. `GmReviewCompositionClaim` also carries no `[DisallowMultipleComponent]`,
unlike every other marker type, so Unity would not block the duplicate either. Re-authoring against a
non-fresh owner stacked a second component instead of reconfiguring the first, which is exactly the
drift `UniqueMap(claims, claim => claim.ShotName, ...)` and the fingerprint check in
`docs/UNITY-SCENE-WORKFLOW.md` Gate 7 exist to catch.

**It could not simply be switched to `AddOrGet`.** All 10 parlor claims pass the same `owner`
GameObject, and the same holds in the other five plans. `AddOrGet` would collapse all 10 shots onto a
single component, leaving one claim where ten were authored. That would have been a much worse defect
than the one being fixed.

The identity used instead is the shot name, which is what the audit already keys on. `ExistingClaim`
looks for a claim on the owner with a matching `ShotName` and reconfigures it; only a genuinely new
shot adds a component. Many claims per owner still works, and a rebuild in place is now stable.

### 2. `ReviewClaim` interim adapter

An overload matching the call sites exactly, adapting onto `Claim`. It adds no serialized state and
no new marker type. Verified: all 48 call sites bind to
`(GameObject, string, string, string, string, string, Vector2, float, string)` with zero mismatches.

## What the adapter assumes

Three assumptions. All three are visible in the method's comment block, and all three are Nick's to
overrule.

1. **`secondaryElementId` becomes `supportingIds`.** `ValidateClaimElements` runs the support layer
   with `depthRule: null`, foreground with `depth < primaryDepth - 0.15`, background with
   `depth > primaryDepth + 0.15`. Support is the only bucket that asserts "declared, visible, framed"
   without also asserting a depth ordering nobody authored. That ordering genuinely flips between
   adjacent shots. Parlor `01-table-perspective` shoots from `(0, 1.25, -1.35)` at primary
   `card-table` (world z 0, depth about 1.35 m) with secondary `aldric-chair` (world z 1.4, depth
   about 2.75 m) **behind** it. The very next shot, `02-aldric-portrait-framing`, moves the camera to
   `(0, 1.2, -0.6)` and swaps the same pair, putting the secondary **in front** at about 0.6 m against
   a primary at about 2.0 m. No blanket foreground/background mapping could be right for both.
   `foregroundIds` and `backgroundIds` are left empty for the same reason.

2. **The two scope arguments are accepted and not validated.** 36 cluster ids, 60 zone ids, 0 element
   ids. `GmReviewCompositionClaim` has no zone or cluster field and the audit's claim validation never
   reads either concept, so nothing is lost that was ever expressible. They stay in the call sites as
   authored annotation. Anyone reading a plan file should know those two strings are documentation,
   not a checked constraint.

3. **The trailing float is read as a symmetric viewport tolerance.** This is the positional reading,
   and it is the assumption I have the least confidence in. See below.

## What Nick must confirm or overrule

### D1. What is the trailing float? (blocks any real framing gate)

Read as a tolerance, a large fraction of these assertions cannot fail. `Visible()` already rejects any
primary whose viewport center falls outside `[-0.1, 1.1]` on either axis, so from a target near 0.5 the
largest reachable delta is 0.6. I measured every shot against its own target:

- **11 of 48 are vacuous on both axes.** The framing check cannot fire at all.
- **14 of 48 are vacuous on at least one axis.**
- Tolerance range across the 48: min 0.35, median 0.55, max 0.85.

The eleven dead ones:

| scene / shot | tolerance | target | max reachable delta |
|---|---|---|---|
| court / 03-witness-spotlight | 0.70 | (0.50, 0.45) | (0.60, 0.65) |
| court / 04-evidence-table | 0.65 | (0.50, 0.50) | (0.60, 0.60) |
| court / 05-gavel-tarnish-tell | 0.75 | (0.50, 0.60) | (0.60, 0.70) |
| entry-hall / 04-ledger-closeup | 0.80 | (0.50, 0.50) | (0.60, 0.60) |
| hidden-room / 02-desk-invitation | 0.70 | (0.50, 0.50) | (0.60, 0.60) |
| hidden-room / 03-mirror-shard3 | 0.65 | (0.50, 0.50) | (0.60, 0.60) |
| hidden-room / 05-mirror-reconstructed | 0.80 | (0.50, 0.60) | (0.60, 0.70) |
| labyrinth / 03-mirror-shrine | 0.70 | (0.50, 0.55) | (0.60, 0.65) |
| labyrinth / 07-exit-gate | 0.65 | (0.50, 0.50) | (0.60, 0.60) |
| parlor / 09-the-read-focus | 0.85 | (0.50, 0.50) | (0.60, 0.60) |
| shut-the-box / 06-hold-verb-framing | 0.80 | (0.50, 0.50) | (0.60, 0.60) |

On those eleven the audit will report PASS having proven only "the primary is somewhere on screen",
which `Visible()` established one line earlier.

There is a second reading. The values run backwards for a tolerance: the loosest numbers sit on the
close-ups (`09-the-read-focus` 0.85, `04-ledger-closeup` 0.80, `05-gavel-tarnish-tell` 0.75,
`06-hold-verb-framing` 0.80) and the tightest on the wides. A close-up is the easiest shot to frame
precisely and the one where framing matters most, so a tolerance would go the other way. The numbers
track how much of the frame the subject fills, which is a meaning the claim type already has a field
for: `minimumViewportHeight`. Under that reading the current mapping inverts a strengthening
parameter into a disabling one across all 48 shots.

Both readings are defensible from the data and they are near inverses. This is a framing call, framing
is a feel gate, so it is Nick's. **Do not let anyone retune these 48 numbers to make a gate go green.**

### D2. Should shot scope (cluster / zone) be a checked constraint or stay a comment?

Right now it is a comment that lives in a parameter. If "this shot belongs to this cluster / spans
these two zones" should actually be enforced, that is a new field on a serialized MonoBehaviour that
six scenes will bake into `.unity` output, plus a new audit rule. Worth doing on purpose if Nick wants
it; not worth inheriting by accident from an adapter written to unblock a compile.

### D3. Keep the adapter, or do the 48-site rewrite?

Once D1 is settled, option (c) becomes cheap and correct: inline the adapter, write the arrays out at
each site, and delete `ReviewClaim`. Until then the adapter is the only place the unconfirmed
assumption lives, which is where a correction is cheapest to apply.

## Two things this change does NOT do

Stating these plainly so a green compile is not mistaken for a green gate.

1. **These six scenes will still fail the composition audit.** No composition plan creates any
   geometry: 0 occurrences of `CreatePrimitive`, `MeshRenderer`, `MeshFilter`, or `InstantiatePrefab`
   across all six files. Every composition element is a bare `GameObject`, while the real geometry is
   built separately under Environment/Gameplay/Lighting and never linked. `ValidateElements` will
   report `composition element 'X' has no renderer` for essentially every element in every scene, and
   `CombinedBounds` on an empty renderer array returns a degenerate `new Bounds()` at the world origin,
   which is its own hazard. Linking markers to geometry is the larger defect. The missing method was
   the top of the stack, not the stack. All six scenes are registry status `scaffold`, not `review`,
   which is consistent with that.

2. **The audit cannot currently detect a vacuous claim.** `ValidateShot` never sanity-checks the
   tolerance band against the window `Visible()` already enforces, and `Configure` only `Clamp01`s each
   axis, so 0.85 is accepted as legitimate. A claim can be authored that asserts nothing and the gate
   has no way to say so. That blind spot is what let D1 become dangerous in the first place. A vacuity
   check belongs in `ValidateReviewClaims`, and it is out of scope for this change
   (`GmSceneCompositionAudit.cs` was not touched).

   Related, worth watching for in review: `ValidateReviewClaims` opens with
   `if (!manifest.RequireReviewClaimForEveryShot && claims.Count == 0) return;`. Deleting the claims
   alone fails loudly, but setting `requireEveryShot: false` in `Begin` **and** dropping the claims
   makes the entire review-claim gate vanish with no output at all.

## Evidence and its limits

Verified this session, by parsing the six plan files and reading the audit source:

- 48 call sites, all with identical argument shape, all binding the new overload.
- 96 scope arguments: 0 element ids, 36 cluster ids, 60 zone ids.
- All 48 primary ids and all 48 secondary ids resolve to a declared element in the same plan.
- 11 of 48 tolerances vacuous on both axes, 14 on at least one, against the `[-0.1, 1.1]` window in
  `GmSceneCompositionAudit.Visible`.
- 0 geometry-creating calls in any composition plan.
- The parlor 01/02 depth flip, computed from the plan's element transforms and the shot positions in
  `unity/scenes/parlor/Runtime/GmParlorShotTour.cs`.
- `Claim` had no callers before this change.

**Not verified:** nothing was compiled and no Unity run happened. Unity must be closed for a batchmode
run and this session did not own the editor. "The Editor assembly compiles after this change" is
untested. There may be further compile errors in these six untracked scaffolds behind this one.
