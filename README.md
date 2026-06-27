# Arcanum — Unity Reimplementation

A fan-made reimplementation of the engine behind **Arcanum: Of Steamworks and Magick Obscura** (Troika Games,
2001), built from scratch in **Unity 6** (C#, URP/2D).

It is a *clean-room* reimplementation in the spirit of projects like **OpenMW** and **OpenRA**: rather than
modifying or redistributing the original game, it **reads the original game's own data files at runtime** —
sprites, maps, dialog, prototypes, scripts — and runs them on a new, modern engine. Every file format was
reverse-engineered and checked against the shipped data and the community decompilation.

To play this project you have to buy original game. You can do it on different platforms:
- [Steam](https://store.steampowered.com/app/500810/Arcanum_Of_Steamworks_and_Magick_Obscura/)
- [GGG](https://www.gog.com/en/game/arcanum_of_steamworks_and_magick_obscura)

> ⚠️ **Work in progress.** This is an engine and systems project under active development — not a finished,
> playable game. Expect rough edges, placeholders, and missing features.

---

## 😺 Fan project

![Lighting](https://cdn.arcanum.aapanasik.com/github/point-light.gif)

- This is an **unofficial, non-commercial fan project**, made out of love for the original game.
- It is **not affiliated with, endorsed by, or associated with** Troika Games, Activision, Microsoft, or any current
  rights holder of the *Arcanum* intellectual property. All trademarks and copyrights belong to their
  respective owners.
- **No original game assets are included** in this repository — no art, audio, text, maps, or data from
  *Arcanum*. The project only contains C# source code and Unity assets. To run it you must **own a legitimate copy** of
  *Arcanum* (e.g. from GOG or Steam); the engine loads the data from your own installation.
- If you are a rights holder and have any concerns, please get in touch and they will be addressed promptly. I've tried to reach Activision and Microsoft several times, was ignored.

---

## ✅ What works so far (partially)

A high level snapshot be subsystems:

- **World** — streaming isometric maps, terrain/walls/doors/roofs, day–night lighting and shadows.
- **Characters** — composite paper-doll sprites (race, gender, armour, shield, weapon), NPCs/monsters, animations.
- **Movement & interaction** — click-to-move A\* pathfinding, doors, containers/loot, ground items, inventory
  and equipment.
- **Combat** — real-time *and* turn-based, action points, criticals/status, ranged + line-of-sight, death, XP,
  level-up — driven by the original's real stats.
- **Dialog, quests, party** — conversation system, journal, recruitable followers that fight alongside you.

---

## 📚 Documentation

Will be in 2 folders:

- [`documentation/`](documentation/) — **about the original Arcanum**: its data formats, engine systems, and
  behaviours, reverse-engineered and written up for contributors (no engine-specific code).
- [`documentation_unity/`](documentation_unity/) — **about this Unity reimplementation**: architecture, systems, and
  how it's built.

---

## 🗺️ Status & how this repo is updated

I work on this in my spare time, so updates come **from time to time** rather than on a schedule. The plan:

1. **Documentation first** — the reverse-engineering notes and system write-ups are being published and polished
   first, so the knowledge is useful on its own.
2. **Then code, module by module** — individual systems will be opened up as they reach a presentable state,
   rather than all at once. I'll try cover it (when I can) with tests.

---

## 🎯 Goals

- The project is and will remain **free**.
- If it ever reaches a state worth releasing — and if it's possible to do so properly — I'd love to put it on
  **Steam**. In that case the aim would be to support the **Workshop** (mods/custom content) and **local
  co-op**. These are aspirations, not promises — nothing here is guaranteed.

---

## 🛠️ Requirements

- **Unity 6000.0.0f1** (or newer) with URP.
- A legitimate installation of the original **Arcanum** (the engine reads its data files; nothing copyrighted is
  bundled here).

---

## 🙏 Credits & references

- **Troika Games** — for *Arcanum: Of Steamworks and Magick Obscura*, the game this project exists out of love for.
- [`arcanum-ce`](https://github.com/alexbatalov/arcanum-ce) and the `tig` library — the community decompilation
  that made reverse-engineering the formats and behaviours possible.

---

## 🤖 A note on AI

Parts of this project's code (and documentation) are written with the help of AI coding tools. Everything is
reviewed, integrated, and tested by me before it lands — AI speeds up the work, but it isn't a substitute for
understanding the engine or the original game's formats. It will not be possible to make this project in a reasonable time by 1 person.

---

## 📄 License

The **source code** in this repository is released under its accompanying license (see `LICENSE`). This license
covers *only this project's original code* — it does **not** grant any rights to *Arcanum* or its assets, which
remain the property of their respective owners.

## 💬 Support and community

- **Join on Discord** — chat about the project, follow progress, ask questions, share ideas, or help
  test: **[discord.gg/gvj4MdgASx](https://discord.gg/gvj4MdgASx)**. Everyone curious about Arcanum or the
  reimplementation is welcome.
- **Support development** — this is a free, non-commercial labour of love built in my spare time. If you'd
  like to help it move along faster, you can support me on
  **[Patreon](https://www.patreon.com/apanasik)**. Entirely optional — it
  funds the *time* spent on the engine, not the game itself (you still need your own legitimate copy of
  *Arcanum*).
