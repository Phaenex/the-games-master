# Free "Horror Elements" package — review + selective intake

Reviewed 2026-07-16 (was inventory-only until this pass).

## What it is

`~/Library/Unity/Asset Store-5.x/Anthon/AudioSound FX/Horror Elements.unitypackage`
— 193 MB, 49 files, ALL audio (WAV). No models, no textures. Categories:

| Folder | Count | Contents |
|--------|------:|----------|
| Ambient | 16 | dark beds: darkroom, scary, rumble, deep impacts, claustrophobia, bell |
| Hits | 13 | stingers: piano hits, metal, anvil, upstairs booms, suspense |
| Misc | 10 | breath, whisper, shh, cry, laugh, ghost, horrific, noise |
| Save Room + rest | 10 | dream pads etc. |

## License

Unity Asset Store EULA (free-tier acquisition by Nick, 2026-07-15). Usable embedded in
the shipped game. The raw pack and unconverted WAVs must not be redistributed — only
converted in-game assets ship. Logged in `assets/sfx/license.txt`.

## Selected for the opening (converted WAV → Opus OGG)

- `Misc_breath.wav` (8 s) → `assets/sfx/porch_breath.ogg` (45 KB) — wired to the
  Threshold Refusal "Something moved in the porch dark" beat as a low presence layer
  under the latch tick. The beat previously had only the metallic tick; a close breath
  sells "beside them, close enough to smell iron and damp wool".

## Reviewed and deliberately NOT taken (for now)

- `Misc_Whisper.wav` — 24 s, too long for a beat one-shot; would need editing. Revisit
  for the labyrinth/hidden room.
- `Hit_upstair_boom.wav` — would fight the canon `ko_thud.ogg` on the KO beat.
- `Amb_darkroom.wav` (13 MB source) — we already run wind + dark beds; a third bed
  adds mud, not mood. Candidate for Entry Hall/Court later instead.
- Piano/metal stingers — nothing in the opening cues them; Court pressure-clock candidates.

No further extraction until a scene actually needs a listed item.
