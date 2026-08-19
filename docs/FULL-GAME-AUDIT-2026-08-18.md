# The Games Master: Full-game completion audit

> Baseline: 2026-08-18, after commit `1f7970c`.
>
> This is a product-completion score, not a test score. A green scene build proves that authored
> objects compile and can be loaded. It does not prove that a room is frightening, richly dressed,
> audible, fun, or ready to ship.

```
WHOLE GAME             [██████░░░░░░░░░░░░░░]  30%
```

The game has a real technical spine from Boot through the ending trigger, a strong exterior opening,
a traversable house hub, one substantially playable table game, shared run state, saves, recovery,
and automated physical proof. It does not yet have the promised seven-game night. Five table games
are missing, the later rooms are mostly prototype slices, interior sound is not wired, several
builders still ship visible primitive/fallback geometry, and the six endings lack authored
presentation.

## Full-game matrix

| Area | Progress | Evidence-backed state | Remaining work |
|---|---:|---|---|
| Boot, save, recovery | `[██████████████████░░]` 90% | Continue, restore, quarantine, reset, diagnostics, high contrast, 7-state tour | Exercise native macOS Cancel once and record it |
| Grounds and prologue | `[█████████████████░░░]` 85% | 435 m route, collision boundaries, cemetery, estate, Ninth Bell, culling and performance proofs | Nick walk; arrival colour, vehicle, pacing, audio-mix taste calls |
| Environmental detail | `[█████████░░░░░░░░░░░]` 45% | Exterior fog, foliage motion, mist and asynchronous lamps exist; interior exposure/fog is shared; named flame practicals now gain restrained asynchronous runtime motion | Interior dust motes absent; room-specific grime, drafts, reactive props and scary beats need authored passes and visual proof |
| Entry Hall and house hub | `[██████████████░░░░░░]` 70% | Foyer, library, 2F, study, guest room, attic, cellar, vault and game doors are traversable | 22 two-layer POIs, pickup/examine parity, letter reveal, final visual polish and human pacing review |
| Cellar and basement horror | `[█████████░░░░░░░░░░░]` 45% | Lever-gated descent, barrels, braziers, vault grate and climb-out are physically proven | Visual remains partial; blown practical, empty dark volume, sparse clutter, no bespoke soundscape, no dust/air movement, no authored scare cadence |
| Parlor game | `[████████████░░░░░░░░]` 60% | Trick-taking match, legal follow, Read flow, persistence, rematch and 3x built-player proof | Substitute Aldric body, near-black table read, weak lantern pool, missing production card SFX/ambience, final input/feel and art pass |
| Shut the Box | `[██████░░░░░░░░░░░░░░]` 30% | Rules, controller, three Hold outcomes and Tile-9 transition path exist | Finished board assets, hinged animation, real player UI/input, host turn presentation, dice SFX, full-match proof and visual replacement of prototype geometry |
| Court | `[████████████░░░░░░░░]` 60% | Winnable/loseable three-argument hearing, five-card evidence UI, keyboard/controller input, pressure penalty, reactive final-seal rig, physical seal/gavel/role-light states, Court-owned Shard #2 and nine-shot visual tour | Replace remaining prototype geometry, land the actual gavel clip, strengthen verdict/loss presentation, animate the strike and run a human input/feel pass |
| Hidden Room | `[████░░░░░░░░░░░░░░░░]` 20% | Scene shell, journals/invitation/shard objects, re-entry and onward transition code exist | Player-facing journal/invitation/shard flow, assembled mirror, dust atmosphere, scare staging, soundscape, asset replacement and current screenshots |
| Labyrinth and Huntsman | `[████░░░░░░░░░░░░░░░░]` 20% | Fixed-pattern maze and Huntsman state machine exist | Decide fixed vs procedural maze, full chase input/feedback, mirror states, Shard #3 discovery, audio, atmosphere, asset replacement and current proof |
| Seven table games | `[██████░░░░░░░░░░░░░░]` 30% | Parlor is substantial; Shut the Box is a logic slice | Five games absent. Bones, Study and Wager have only signatures; games 6–7 have no canonical identity. Nick must approve rules/order before implementation |
| Story, shards and endings | `[█████████░░░░░░░░░░░]` 45% | Shared state, three shard slots, ending resolver and production trigger exist | Verify 8 catches through real authored play; author six ending sequences with art, copy, audio and reviewed frames |
| Audio | `[████░░░░░░░░░░░░░░░░]` 20% | Exterior wind/crickets/owl and cue assets for the opening exist | `GmAudioManager` has no production instantiation/callers; no Parlor snap, STB dice, Court gavel or interior ambience reaches the player; several named cue clips are absent from the Unity Resources path |
| Accessibility and controls | `[█████████░░░░░░░░░░░]` 45% | Boot/recovery high contrast, Parlor settings surface, shared settings model | Prove every setting has a real consumer, controller parity, rebinding, text scaling and reduce-motion across every room |
| Steam and release | `[█░░░░░░░░░░░░░░░░░░░]` 5% | macOS development build and attribution records | Windows pipeline, Steamworks, achievements, cloud saves, target-hardware matrix, age rating, store assets and final panel review |

## What “dust, flicker, scary basement” means in the build

- **Dust:** the Entry Hall attic and Hidden Room have cobweb meshes and text describing dust. There
  is no authored dust particle system in any scene. Dust in light shafts, disturbed motes, floor
  accumulation and corner dressing are still content work.
- **Flicker:** exterior estate lamps have real asynchronous runtime flicker. The 2026-08-18 fix now
  adds restrained, position-seeded motion to named interior sconces, lamps, chandeliers, hearths,
  braziers and torches at scene load. Navigation, moon, evidence and exit lights stay stable. The
  runtime and builder paths are covered by EditMode tests; final moving-image review remains.
- **Basement:** the cellar is navigable and connected, but the current evidence frame is a sparse,
  warm room with an overexposed practical and a black opening. It is a route, not yet a scary
  basement experience.
- **Games:** only Parlor approaches a full playable game. Shut the Box is a logic/scene slice. Court,
  Hidden Room and Labyrinth are story chapters, not part of the promised seven table games. Five
  table games still have to be designed and built.

## Execution checklist

- [x] Preserve and push the House Memory recovery hardening (`1f7970c`).
- [x] Reframe progress around the whole product instead of the latest slice.
- [x] Inventory current scenes, controllers, tests, captures, placeholders and atmosphere wiring.
- [x] Resolve the old Court sequencing lock through the human gate. Nick selected **Build Court
  Now** on 2026-08-18, so the Phase 0 walk remains a release gate but no longer blocks Court work.
- [ ] Get approved rules and order for Bones, Study, Wager and unnamed games 6–7.
- [ ] Reopen and fix interior audio wiring with regression tests and audible built-player proof.
- [ ] Replace player-visible primitive/fallback art in Court, Shut the Box, Hidden Room and Labyrinth.
- [ ] Run a room-by-room environmental pass: dust, practical motivation, fog, grime, clutter,
  reactive props, shadow movement and scare cadence. Runtime flame flicker is implemented; visual
  review and the rest of the pass remain.
- [ ] Finish Entry Hall POIs, shard/pickup parity and letter progression.
- [ ] Finish Parlor character art, lighting/readability, audio and human input/feel pass.
- [ ] Finish Shut the Box as a complete match with AI, UI, animation, tells and transition.
- [x] Finish Court's complete, loseable mechanical hearing and retire the temporary attic Shard #2
  proxy. Art, audible gavel, strike animation and human feel remain in the Court row above.
- [ ] Finish Hidden Room interaction flow, mirror assembly and authored discovery beats.
- [ ] Finish Labyrinth design decision, chase, Huntsman presentation and shard discovery.
- [ ] Build the five missing table games as rules/runtime/scene/completion/evidence slices.
- [ ] Author and prove all six ending presentations and full-run catch reachability.
- [ ] Complete controller, accessibility, Windows, Steam, cloud-save and target-hardware work.
- [ ] Re-run all tests, builds, scene tours, physical probes, performance checks and visual gates.
- [ ] Run final human walk/audio/taste gates, then commit and push each verified slice.

## Human-owned decisions

1. **Resolved 2026-08-18:** build Court now. The Phase 0 walk remains a release gate, not a Court
   sequencing lock.
2. Approve complete rules and order for Bones, Study and Wager; name and define games 6–7.
3. Judge opening duration, arrival colour, contemporary vehicle, sound mix and figure subtlety on
   Nick's display/headphones.
4. Judge whether the cellar, loft, guest room, Parlor table and Aldric art have crossed from
   functional into convincing.

Until those answers arrive, work can continue on audio wiring, environmental systems, asset
replacement, existing-game presentation, POIs, testing and release infrastructure without inventing
canon or silently overriding a human gate.
