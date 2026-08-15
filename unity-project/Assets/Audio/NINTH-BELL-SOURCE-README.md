# Ninth Bell audio provenance

These are final curated gameplay cues, not placeholders. The reproducible curation pipeline is
`scripts/gen-bell-audio.py` in the repository at `/Users/damato/Projects/the-games-master`.

| Shipped resource | Source and treatment | License |
|---|---|---|
| `clock_chime.ogg` | craigsmith, Freesound 438313. One archival grandfather-clock strike isolated before the next strike and extended with a restrained room tail. | CC0 1.0 |
| `heartbeat.ogg` | JonasTisell, Freesound 670465. Actual resting heartbeat, seven complete cycles, boundary-trimmed for looping. | CC0 1.0 |
| `whisper_bed.ogg` | PlumForestPodcast, Freesound 517868. Human whisper with no meaning, curated as one non-looping crossing cue. | CC0 1.0 |
| `ear_whine.ogg` | Project-authored deterministic periodic tinnitus synthesis. | Project original, CC0 |
| `chapel_bell.ogg` | Anthon, Horror Elements, `Amb_bell`, previously curated as a single exterior toll. | Unity Asset Store EULA |

Source pages:

- https://freesound.org/people/craigsmith/sounds/438313/
- https://freesound.org/people/JonasTisell/sounds/670465/
- https://freesound.org/people/PlumForestPodcast/sounds/517868/

The remote high-quality previews used by the pipeline are SHA-256 pinned. A source change causes a
hard failure instead of silently changing the shipped sound. Raw Horror Elements pack files remain
excluded from redistribution under the Asset Store license.
