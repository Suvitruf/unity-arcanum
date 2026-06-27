# The data layer (`Arcanum.Formats`)

`Arcanum.Formats` decodes the original *Arcanum*'s shipped data files — archives, sprites, maps, text, dialog,
scripts, object prototypes — into plain C# objects. It's the foundation the rest of the engine is built on:
nothing renders or simulates until this layer has turned the game's bytes into something usable.

Two principles shape it:

- **Byte/pixel-exact.** Every format was reverse-engineered against the original engine and checked against the
  shipped data. Where a value's meaning matters, the code cites the original source it came from.
- **No Unity, no game logic.** The whole assembly is pure C# (it doesn't reference `UnityEngine`) and holds only
  *data* — no rendering, no rules, no runtime state. That keeps it portable and lets every format be unit-tested
  with synthetic byte buffers, no Unity play mode required.

It reads the data from **your own legitimate install** of the game; nothing copyrighted is bundled here.

## Sub-namespaces

| Namespace | Decodes | Key types |
|---|---|---|
| `Database` | `.dat` archives (zlib payloads, table-of-contents at the end) + a layered virtual filesystem | `DatArchive`, `DatVirtualFileSystem` |
| `IO` | zlib / DEFLATE inflate | `ZlibInflate` |
| `Text` | `.mes` message tables (`{key}{value}`) — used everywhere for names and strings | `MesReader`, `MesFile` |
| `Art` | `.art` sprites (header, palettes, RLE frames) + art-id → file-path resolvers | `ArtReader`, `*ArtResolver` |
| `Tiles` | terrain tile art-ids + blend / path resolution | `TileArtId`, `TileNameTable`, `TileArtPathResolver` |
| `Objects` | object prototypes (`.pro`) + placed instances via the field engine | `ObjectInstanceReader`, `ObjectProtoInfo` |
| `Dialog` | `.dlg` conversations + the dialog test/effect mini-language | `DlgReader`, `DialogScriptEvaluator` |
| `Script` | compiled `.scr` scripts (opcodes + typed operands) | `ScriptReader`, `ScriptFile` |
| `Quest` | quest metadata, journal text, the XP table | `QuestLog` |
| `World` | sectors (`.sec`), map list / properties, jump points, areas, world map | `SectorReader`, `MapList`, `AreaList` |

## How the pieces connect

Everything starts at the **virtual filesystem**. `DatVirtualFileSystem` mounts the game's `.dat` archives (plus
optional loose-file directories, for mods/patches) and resolves a virtual path like `art/tile/grass.art` to its
raw bytes. The readers take those bytes and produce data objects.

A recurring idea is the **art-id**: a packed 32-bit integer that identifies a sprite by *type* plus parameters
(a critter's race/gender/equipment, a tile's terrain + blend edge, a light's facing, …). `ArtId` decodes the
type; a per-type resolver turns the id into an `art/.../*.art` path; `ArtReader` decodes that file.

The densest part is the **object field engine** (`Objects`). Arcanum objects carry hundreds of typed fields,
stored in a sparse, per-object-type change bitmap. `ObjectInstanceReader` walks that layout exactly (verified
byte-for-byte against the original), so a placed door, NPC, or item decodes into a flat `ObjectInstance` (or
`ObjectProtoInfo` for a prototype).

## What's *not* here

The data layer decodes; it doesn't *act*. The game logic that consumes this data lives in the runtime layer
(`Arcanum.Runtime`): turning `.art` into Unity sprites, executing `.scr` scripts, applying a conversation's
effects, running the world simulation. A few original *behaviours* there are still partial (the script VM, some
dialog/quest rules) — but the **format decoding itself is complete and tested**.

## Testing

Because the assembly is pure data with no Unity dependency, each format is covered by **EditMode unit tests**
that build a synthetic byte buffer to the documented layout and assert the reader produces the right values —
and, for the trickier walks (objects, sectors), that the read cursor lands exactly where it should. No game data
or play mode is needed, so they run in CI.
