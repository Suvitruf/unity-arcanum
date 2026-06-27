# The original Arcanum — data & engine reference

Notes on how *Arcanum: Of Steamworks and Magick Obscura* stores its data and runs its systems, reconstructed by
reverse-engineering the [`arcanum-ce`](https://github.com/alexbatalov/arcanum-ce) decompilation of the retail
engine and cross-checking the shipped game files.

This is a description of **the original game** — its binary formats, the rules its engine applies, and the
quirks that bite anyone trying to read its data. It is not about any particular reimplementation. Source
references point at files/lines in `arcanum-ce` so claims can be verified.

## Contents

- **[Data formats](data-formats.md)** — the binary file formats: `.dat` archives, `.art` sprites, `.mes`
  message tables, `.pro` prototypes (and the object-field engine), `.sec`/`.mob` sectors & objects, `.dlg`
  dialog, `.scr` scripts.
- **[Art & graphics](art-and-graphics.md)** — art-id bit layouts, the 8-bit palette system, and how
  tiles / walls / roofs / facades / lighting & day-night are drawn.
- **[Dialog](dialog.md)** — how conversations are stored and gated (the IQ rule, gender variants, condition &
  effect codes, reaction).
- **[Scripting](scripting.md)** — the `.scr` script VM (conditions, actions, control flow) and the object
  heartbeat / `ai_timeevent` cadence.
- **[Combat & characters](combat-and-characters.md)** — to-hit / damage / armour-class / resistance formulas,
  the "damage-taken" HP model, and the stat / skill / effect / level-up systems.
- **[Magick](magick.md)** — the spell system: colleges, fatigue-cost casting, and the eye-candy (visual-effect)
  data chain.
- **[World, maps & quests](world-maps-quests.md)** — sector layout, map transitions (jump points & teleporters),
  the world map, and the quest/journal system.

## Reproducing these findings

- **Engine source:** `arcanum-ce` (`src/game`, `src/tig`) is a clean, hand-named decompilation of the retail
  binary; its `// 0x…` comments are the real `Arcanum.exe` addresses.
- **Game data:** the shipped `.dat` archives (zlib-compressed with a trailing table of contents) hold the
  `.art`, `.mes`, `.dlg`, `.pro`, `.sec`/`.mob` files described here.
