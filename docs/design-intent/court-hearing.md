# Court hearing design intent

- `id`: `court-hearing`
- `humanOwner`: Nick D'Amato
- `userQuestion`: Which exhibit answers the argument in front of me, and did Aldric alter the hearing?
- `primaryTask`: Read five exhibits, select the one that answers each of three arguments, and notice
  the final-seal rig when corruption makes Aldric desperate.
- `primaryObject`: The evidence card. The current argument, clock and wax seals support that choice.
- `informationPriority`: Current argument, selected exhibit, time, seals, feedback, optional shard.
- `designThesis`: A severe black-and-wax evidence docket sits over the physical courtroom while the
  room itself changes to confirm each ruling.
- `signatureMove`: On a corrupted final argument, the gavel visibly tarnishes but the correct seal
  still breaks. The tell reveals the rig without stealing a valid win.
- `rejectedDefaults`: No dialogue wheel because the player is matching evidence, not choosing tone.
  No generic card-grid dashboard because the exhibits belong to one active argument. No color-only
  correctness flash because physical seals, written feedback and the gavel carry the state.
- `typography`: Large pale text on near-black panels for a dark HDRP room. Exhibit bodies wrap and
  the row shrinks to the available width instead of clipping.
- `color`: Tarnished brass, wax red and paper-white text come from courtroom objects. Gold marks the
  selected exhibit and frame hierarchy. It is not the only state signal.
- `sound`: One gavel strike is requested after accepted evidence. The production clip is still
  missing, so this remains a tracked audio gap rather than a claimed feature.
- `motion`: The role light turns between argument positions and cracked seals rotate. Motion is
  brief and state-bearing; the shared reduce-motion consumer still needs a Court-specific proof.
- `accessibility`: Keyboard digits select exhibits, shared Interact presents, controller horizontal
  input moves selection, buttons remain clickable, and text stays at 15 px or larger at the 1920 by
  1080 reference size. Controller focus, text scaling and reduced motion remain release checks.
- `performance`: One UI Toolkit document, five buttons and three physical seal renderers. No
  per-frame object search or allocation in the hearing update loop.
- `canonicalSource`: `unity/scenes/court/Runtime/`, `unity/scenes/court/Editor/`, and
  `unity/scenes/court/Tests/`. `unity-project/Assets/` is the synchronized Unity mirror.
- `renderedEvidence`: `unity-project/Screens/Court/tour-01-court-overview.png` through
  `tour-09-verdict-passage-open.png`, captured 2026-08-19.
- `successMeasure`: A fresh player can finish or time out without a soft-lock, can recover after
  testing a future answer too early, and can identify the tarnished-gavel tell without losing the win.
- `failureConditions`: Clipped exhibits, evidence becoming permanently unusable on the wrong
  argument, a timer dead end, invisible state changes, duplicated Shard #2, or claiming audible
  feedback while the clip is absent.
- `independentReview`: UNVERIFIED. Automated structure, state and visual capture gates passed, but
  the implementer is not an independent reviewer and Nick has not completed the input/feel pass.
