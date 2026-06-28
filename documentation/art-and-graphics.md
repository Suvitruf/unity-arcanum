# Art & graphics — how Arcanum stores and draws sprites

*Arcanum: Of Steamworks and Magick Obscura* draws everything — terrain, walls, roofs, buildings,
critters, items, lights — from a single sprite format (`.art`) addressed by a single 32-bit handle (the
**art-id**). The art-id is not an index into a list; it is a **packed bitfield** whose layout changes per
art *type*, and the engine turns it into a file path on disk through a chain of per-type lookup tables
(`.mes` files). This page describes that format, the bit layouts, the path resolution, and how the world
layers (tiles, walls, roofs, facades, lighting, day/night) are composited.

Source references point at the `arcanum-ce` decompilation of the retail engine and its low-level library
**tig** so claims can be checked — chiefly `first_party/tig/src/art.c` (the art-id and `.art` reader) and
`src/game/name.c` + `src/game/a_name.c` (path resolution), plus `tile.c`, `wall.c`, `roof.c`, `light.c`
for drawing.

---

## The `.art` sprite format

An `.art` file is an **8-bits-per-pixel, palette-indexed** sprite. It holds a header, **up to four
palettes** (each 256 colours), and the pixel data for one or more **rotations**, each with one or more
**frames**.

### Header

The file opens with a fixed header (`art_read_header`, `art.c:5958`):

| Field | Size | Meaning |
|---|---|---|
| `flags` | 4 | bit `0x01` marks a *single-rotation* sprite (only one direction stored — UI, items, tiles, roofs…) |
| `fps` | 4 | playback rate for animated frames |
| `bpp` | 4 | bits per pixel — **always 8**; the loader rejects anything else (`art.c:5760`) |
| palette presence ×4 | 4×4 | one dword per palette slot; a **non-zero** value means "this palette is present in the file" |
| `action_frame` | 4 | the frame at which an animation's effect fires (e.g. the swing connects) |
| `num_frames` | 4 | frame count, the same for every rotation |
| (frames table, pixel table) | 2×32 | on-disk pointer placeholders, **skipped** when reading |

### Palettes — 8-bit indexed, index 0 is the colour key

After the header come the palettes that were flagged present, **256 × 4-byte colour** each
(`art.c:5780`). A pixel byte is an **index** into the palette; the engine looks the byte up to get the
real colour. **Palette index 0 is the transparent colour-key** — `tig_art_anim_data` reports the
sprite's transparent colour as `palette->colors[0]` (`art.c:1191`), so any pixel that indexes entry 0 is
drawn as nothing.

Holding **up to four palettes in one file** is how Arcanum recolours a sprite without duplicating its
pixels: the same indexed image is paired with a different palette to produce, e.g., a brown robe versus a
default one. *Which* of the four palettes is used is chosen by two bits in the art-id (see "palette index"
below).

### Frames, rotations and direction mirroring

After the palettes, for each stored rotation, the file has a **frame table** (`num_frames` ×
`TigArtFileFrameData`) followed by the pixel data (`art.c:5812`). Each frame record is
(`art.c:119`, `tig/art.h:301`):

| Field | Meaning |
|---|---|
| `width`, `height` | frame pixel dimensions |
| `data_size` | size of this frame's pixel stream (used to detect RLE — see below) |
| `hot_x`, `hot_y` | **hotspot**: the anchor pixel that lands on the object's screen position |
| `offset_x`, `offset_y` | per-frame draw offset |

A sprite can carry up to **8 rotations** (`MAX_ROTATIONS = 8`) — the eight isometric facings. Two
economy measures apply:

- **Single-rotation art** (`flags & 0x01`) stores just one direction and reuses it for all eight; this is
  the norm for tiles, roofs, items, interface and other non-directional art (`art.c:5585`).
- **Mirroring**: for critters, monsters and unique NPCs the engine stores only **5 of the 8 rotations**
  and produces the other three by **flipping horizontally** at draw time (`art.c:5588`,
  `start = 4, num_rotations = 5`). So east-facing and west-facing frames share one set of pixels.

### Pixel rows are top-down

Pixel rows are stored **top row first** (top-down). The reader walks frames in order and lays their pixels
out contiguously, row by row from the top (`art.c:5841`).

### Compression — per-frame RLE

Each frame is stored either raw or run-length-encoded, decided **per frame** by comparing its `data_size`
to `width × height` (`art.c:5842`):

- If `data_size == width × height`, the frame is **uncompressed** — read it straight through.
- Otherwise it is **RLE-encoded**. The stream is a series of control bytes: the low 7 bits are a length
  `len`; if the high bit (`0x80`) is set, the next `len` bytes are **literal** pixel indices; if it is
  clear, the **next single byte** is a colour index repeated `len` times (`art.c:5862`).

Because pixels are indices, RLE runs of the colour-key (index 0) collapse the large transparent borders
of an isometric sprite very cheaply.

---

## The art-id — one 32-bit handle for every sprite

Everything the engine draws is named by a 32-bit **art-id**. The **top nibble** (bits 28–31) is the
**art type** (`tig_art_type` = `art_id >> 28`, `art.c:668`). The remaining 28 bits are a packed record
whose meaning depends entirely on that type.

### Art types (bits 28–31)

| Value | Type | Value | Type |
|---|---|---|---|
| 0 | TILE | 8 | MISC |
| 1 | WALL | 9 | LIGHT |
| 2 | CRITTER | 10 | ROOF |
| 3 | PORTAL | 11 | FACADE |
| 4 | SCENERY | 12 | MONSTER |
| 5 | INTERFACE | 13 | UNIQUE_NPC |
| 6 | ITEM | 14 | EYE_CANDY |
| 7 | CONTAINER | | |

(`tig/art.h:25`.)

### Fields shared by most types

Several fields sit at the **same bit position across most types** (the generic layout, `art.c:16`):

| Field | Bits | Notes |
|---|---|---|
| **palette index** | **4–5** | selects which of the file's up-to-4 palettes to use (`ART_ID_PALETTE_SHIFT = 4`) |
| rotation | 11–13 | the facing (0–7); LIGHT and EYE_CANDY put rotation at bit 9 instead |
| frame | 14+ | which frame of the animation |
| num (generic) | **19** | the object/art number, masked to 512 (`ART_ID_NUM_SHIFT = 19`, max 512) |

The **palette index at bits 4–5** is the recolour selector for the whole engine: it is read identically
for every type (`tig_art_id_palette_get`, `art.c:1029`) and picks one of the four in-file palettes.

### The per-type "num" shift — a subtle trap

The "num" of a sprite (its number within its type's lookup table) is **not** at the same shift for every
type. `tig_art_num_get` (`art.c:674`) switches on type:

| Type | num location | Max |
|---|---|---|
| ITEM | `(art_id >> 17) & 0x7FF` | 2000 |
| UNIQUE_NPC | `(art_id >> 20) & 0xFF` | 256 |
| INTERFACE | `(art_id >> 16) & 0xFFF` | 4096 |
| everything else (generic) | `(art_id >> 19) & 0x1FF` | 512 |

Reading an item or unique-NPC id with the generic `>> 19` shift silently returns the wrong number and
resolves the wrong art — the shift **must** be chosen by type.

### Critter / monster / unique-NPC paper-doll layout

Critters are the richest layout because their on-screen look is assembled from many independent pieces —
the "paper doll". `tig_art_critter_id_create` (`art.c:1739`) packs:

| Field | Bits | Range / meaning |
|---|---|---|
| type (=2) | 28–31 | CRITTER |
| gender | 27 | 0 female, 1 male |
| body type | 24–26 | human / dwarf / halfling / half-ogre / elf |
| armour | 20–23 | armour appearance class (underwear, leather, chain, plate, robe…) |
| shield | 19 | 0 none, 1 carrying a shield |
| frame | 14–18 | animation frame |
| rotation | 11–13 | facing 0–7 |
| anim | 6–10 | animation id (stand, walk, run, attack, death variants…) |
| **palette** | **4–5** | recolour |
| weapon | 0–3 | drawn weapon class (dagger, sword, axe, mace, pistol, bow, staff…) |

**Monsters** (`art.c:1768`) replace gender+body-type with a single **specie** field at bit 23 (wolf,
spider, orc, skeleton, …; 32 species) plus a 3-bit armour at bit 20, keeping the same shield/frame/
rotation/anim/palette/weapon positions. **Unique NPCs** (`art.c:1795`) replace those identity bits with a
**num** at bit 20 (the named-character index) and keep shield/frame/rotation/anim/palette/weapon. The
anim field for all three is read the same way (`(art_id >> 6) & 0x1F`, `art.c:1820`).

So a single critter art-id encodes "male human, in plate, holding a sword and shield, walk animation,
frame 3, facing NE, palette 0" — and the engine resolves that to a specific `.art` file *and* a frame
within it.

### Item layout

Items pack a different set of descriptors (`tig_art_item_id_create`, `art.c:2127`):

| Field | Bits | Meaning |
|---|---|---|
| type (=6) | 28–31 | ITEM |
| num | 17–27 | item art number (`>> 17`) |
| armour coverage | 14–16 | torso / shield / helmet / gauntlets / boots / ring / medallion |
| disposition | 12–13 | **ground / inventory / paperdoll / schematic** — which "view" of the item |
| destroyed | bit 10 (`0x400`) | destroyed-state art |
| subtype | 6–11 | weapon/ammo/armour sub-category |
| palette | 4–5 | recolour |
| type field | 0–3 | weapon, ammo, armour, gold, food, scroll, key, …, generic |
| damaged | bit 11 (`0x800`) | damaged-state art |

The **disposition** is important: the same logical item has different art depending on whether it is lying
on the ground, sitting in the inventory grid, worn on the paper doll, or shown as a crafting schematic —
and disposition selects which lookup table (and therefore which `.art`) is used (see resolution below).

The item "num" range even depends on the item: weapons, ammo and torso armour cap at 20 variants, while
other items allow up to 1000 (`sub_502830`, `art.c:737`).

### Facade, roof, light layouts (briefly)

- **Facades** (large buildings living in the tile layer) split their num oddly: low 8 bits at bit 17 plus
  +256 if bit 27 is set; a **frame** at bits 1–10 (which 78×40 building piece), a **walkable** flag at bit
  0, and type/flippable bits (`tig_art_facade_id_create`, `art.c:516`/`88`).
- **Roofs** carry a 13-piece **piece** field (corners, edges, centre), a **fill** bit (13) and a **fade**
  bit (12) used to make a roof translucent when the player walks under it (`tig/art.h:264`, `art.c:82`).
- **Lights** put rotation at bit 9 and a frame at bit 12 (`art.c:78`); light sprites are the animated
  glow/flame art.

---

## From art-id to file path

The engine never stores file paths in data — it **derives** the path from the art-id at load time, via
`name_resolve_path` (`name.c:856`, registered as tig's art file-path resolver). Resolution depends on the
type; most types funnel a number through a per-type **`.mes`** lookup table (a `{number}{filename}` map),
all under `art\`:

| Type | `.mes` table | Path pattern |
|---|---|---|
| SCENERY | `art\scenery\scenery.mes` | `art\scenery\<name>` |
| INTERFACE | `art\interface\interface.mes` | `art\interface\<name>` |
| CONTAINER | `art\container\container.mes` | `art\container\<name>` |
| MONSTER | `art\monster\monster.mes` | `art\monster\<name>\<name><armor><shield><weapon><anim>.art` |
| UNIQUE_NPC | `art\unique_npc\unique_npc.mes` | `art\unique_npc\<name>\<name><shield><weapon><anim>.art` |
| EYE_CANDY | `art\eye_candy\eye_candy.mes` | `art\eye_candy\<name>_<F\|B\|U>.art` |
| ITEM | item `.mes` (per disposition) | `art\item\<name>` |
| TILE / WALL / PORTAL / ROOF / LIGHT / FACADE | dedicated tables (see below) | various |

These tables are loaded once at startup (`name.c:244`).

### Scenery and interface — number into a table

The simplest cases: take the art-id's num and look it up. Scenery uses a **composite key**:
`1000 × scenery_type + num` (`name.c:938`), because scenery is grouped by category (trees, plants, metal,
stone, light-sources, beds…) and the number restarts within each category. Interface uses the plain num
(`name.c:945`). Both just prepend the directory to the looked-up filename.

### Critters — pure string composition (no `.mes`)

Critter art is the one type whose path is **built from the bitfields directly**, with no lookup table
(`name.c:887`). The body type, gender, armour, shield, weapon and animation are mapped to short codes and
concatenated:

```
art\critter\<body><gender>\<body><gender><armor><shield><weapon><anim>.art
```

The code tables (`name.c:118`):

- **body** — `HM` human, `DF` dwarf, `GH` halfling, `HG` half-ogre, `EF` elf
- **gender** — `F`, `M`, `X` (plate armour forces `X`, a shared unisex set)
- **armour** — `UW` underwear, `V1` villager, `LA` leather, `CM` chain, `PM` plate, `RB` robe, `PC`
  plate-classic, `BN` barbarian, `CD` city-dweller
- **shield** — `X` none, `S` shield
- **weapon** — `A` none … `C` dagger, `D` sword, `E` axe, `F` mace, `G` pistol, `H` two-handed sword, `I`
  bow, `K` rifle, `N` staff …
- **anim** — the animation index expressed as a letter (`'a' + anim`)

So art-id "male human, plate, shield, sword, walk" composes to (roughly)
`art\critter\HMX\HMXPMSDb.art` — directory from body+gender, filename encoding the whole paper doll.
Monsters and unique NPCs are the same idea but pull the base name from their `.mes` table first
(`name.c:978`/`1006`). One special case: a two-handed sword carried *with* a shield is drawn as a one-
handed sword (`name.c:914`).

### Items — disposition picks the table

Item resolution (`a_name_item_aid_to_fname`, `a_name.c:1061`) computes a key from `num`, `subtype`,
`type` and armour coverage, then looks it up in **one of four** `.mes` tables chosen by **disposition** —
ground, inventory, paperdoll, or schematic. That is what lets one item show a dropped-on-the-floor sprite,
a grid icon, a worn-on-the-doll sprite, and a schematic drawing, all from one logical item.

### Tiles, walls, roofs, lights, facades, portals

These have their own name tables and composition rules (`a_name.c`):

- **Tiles** — `art\tile\…`; tile names come from `art\tile\tilename.mes`, split into indoor/outdoor and
  flippable/non-flippable sets, and the filename encodes the tile pair and a couple of variation letters.
- **Walls** — `art\wall\<name><piece><damage><variation>.art` (`a_name.c:1635`); the **piece** code (one
  of ~46 — corners, edges, doorways `d…`, posts `p…`) and a **damage** code (`0x400` → fully destroyed,
  `0x80` → damaged) select the right segment.
- **Roofs** — `art\roof\<name>.art` (`a_name.c:1912`).
- **Lights** — `art\light\<name>.art`, or `art\light\<name>_s<n>.art` when the light has a directional
  variant (`a_name.c:1865`).
- **Facades** — `art\Facade\<name>.art` (`a_name.c:1146`); names from `facadename.mes`.
- **Portals** (doors and windows) — `art\portal\…` (`a_name.c:1216`).

### Terrain flags & footstep sounds

A `tilename.mes` entry is more than a name. Its full value is `name[/flags] [sound]`: the first three
characters are the terrain code that builds the `.art` filename, an optional `/`-suffix carries **terrain
flags**, and a trailing integer is the tile's **footstep sound id** (`load_tile_names`, `a_name.c:460–503`).
The table is split by key range — `0–99` outdoor-flippable, `100–199` outdoor-non-flippable, `200–299`
indoor-flippable, `300–399` indoor-non-flippable (keys ≥ 400 hold a separate edge/blend adjacency graph).

The flag suffix is a run of single letters (engine `TF_*`, `a_name.c:8`):

| Letter | Flag | Meaning |
|---|---|---|
| `b` | BLOCK | impassable terrain |
| `f` | BLOCK + FLYABLE | blocked on foot, but flagged flyable (levitation/flight crosses it) |
| `s` | SINKABLE | deep water — a critter sinks |
| `i` | SLIPPERY | ice |
| `n` | NATURAL | natural ground rather than built/paved |
| `p` | SOUNDPROOF | blocks sound propagation |

So a plain grass tile is just `name + sound`; a deep-water tile carries `/s`, an impassable mountain `/b`,
and an icy patch `/i`. Note that `f` sets **both** BLOCK and FLYABLE, not FLYABLE alone.

### Blend tile edges & mirroring

Where two terrains meet, a tile is a **blend** — part terrain A, part terrain B — and a **4-bit edge code**
(0–15) says which of the diamond's four corners belong to each terrain. The filename concatenates the two
terrain codes, an edge character and a variant letter: `art\tile\<A><B><edge><variant>.art`
(`build_tile_file_name`, `a_name.c` `0x4EAF70`). A fixed ordering keeps one canonical file per pair (the
lower-ordered terrain goes first; otherwise the names swap and the edge becomes `15 − edge`), and the edge
index maps to its filename character through the constant string `06b489237ea5dc10`. Edges `0` and `15` are
the degenerate "all one terrain" cases and resolve to that terrain's base tile (`<name>bse…`).

**The game ships only half the edge art.** Four of the sixteen edge patterns — **2, 9, 12, 13** — have *no
file of their own*. Each is the exact **horizontal mirror** of one of **8, 3, 6, 7**, so the game stores only
the latter and draws it flipped left-to-right to produce the former — halving the blend art for those edges.
The pairing comes from two 16-entry remap tables in the art library (`tig art.c` `dword_5BE880` /
`dword_5BE8C0`):

| "Flipped" edge (no file) | Canonical partner (has file) |
|---|---|
| 2 | 8 |
| 9 | 3 |
| 12 | 6 |
| 13 | 7 |

A mirrored tile is marked with a flip flag in its art-id; the engine resolves the **canonical** partner's
filename and mirrors it at draw time. Every other edge ships its own file and draws as-is.

> **Gotcha.** A sector routinely stores tiles whose displayed edge is one of `2/9/12/13`. Build the filename
> straight from that edge and you ask for a file that doesn't exist — you get a hole (a flat base tile where a
> blend belongs, and a hard seam at the terrain boundary). You must map the edge to its canonical partner,
> resolve *that* file, and draw it **horizontally mirrored**, which reproduces the original edge exactly.
> These mirrored edges are common — on the order of 6–12% of an outdoor sector's tiles.

---

## Drawing the world

Arcanum's world is **isometric** and composited in layers, drawn back-to-front so that nearer things
overwrite farther ones (painter's algorithm). The grid is a diamond of 78×40-pixel tiles.

### Isometric projection

A map cell `(x, y)` projects to screen position (`location_xy`, `location.c:135`):

```
screen_x = origin_x + 40 * (y - x - 1)
screen_y = origin_y + 20 * (y + x)
```

Each tile spans **78 px wide × 40 px tall**, with the cell centre at the tile's mid-point (`+40, +20`).
The inverse maps a screen click back to a cell by dividing the rotated coordinates by 40.

### Tiles — the ground layer

Terrain tiles are drawn first, in the iso pass (`tile_draw_iso`, `tile.c:628`). Each tile is an
**78×40** frame with hotspot (39,39) blitted at the cell's screen position. Tiles are single-rotation art
(no facing). A sector's ground is a grid of tile art-ids; **facade** tiles (large buildings) live in this
same grid but carry a FACADE art-id, so a building cell renders a 78×40 *facade* frame instead of a ground
tile — which is why a building has no terrain beneath it and sits behind every object as a flat backdrop.

### Walls, scenery, objects

Above the ground, walls, scenery, items and critters are drawn as ordinary sprites at their cell's screen
position offset by the sprite's hotspot. Their depth in the back-to-front order follows the isometric
diagonal (`x + y`), so a critter correctly interleaves between the wall behind it and the wall in front.
Walls are segmented art (the **piece** field picks corner/edge/doorway pieces) so a long wall is built
from many small `.art` segments, and damaged/destroyed walls swap to alternate art via the damage bits.

### Roofs — fade when you walk under

Roofs are drawn last, over everything (`roof_draw`, `roof.c:216`). A roof tile is skipped where the player
is standing so interiors are visible (`roof_is_covered_xy`). When a roof should **fade** (the player has
moved under it), the engine sets the roof art-id's **fade bit** and blits the roof piece with a
**per-corner alpha ramp** — the alpha values assigned to the four corners depend on the roof **piece**
(north edge, corner, centre…) so the building dissolves smoothly toward the player rather than popping off
(`roof.c:272`). This is the classic Arcanum effect of a roof melting away as you step inside.

### Lighting and day / night

Outdoor lighting is a single **global tint** applied to sprite palettes, driven by the time of day. The
engine keeps a `light_outdoor_color` (`light.c:137`) and considers it **daytime when the hour is in
[06:00, 18:00)** (`light.c:1903`) — outside that range the world is tinted toward night. The tint is fed
into palette modification (`light.c:2032`) so it recolours every drawn sprite uniformly: the same indexed
pixels, a darker/cooler palette at night.

On top of the ambient tint, **placed point lights** illuminate their surroundings. These come from two
places:

- the **sector's light list** — static lights baked into the map (torches, lamps, indoor sources), each a
  48-byte record with a tile location, an art-id, and an **r, b, g** colour (note the on-disk channel
  order);
- **object-attached lights** — objects (lamp posts, braziers) that carry a light art-id and a packed
  colour, contributing a light at the object's position.

### Shadows

Shadows are drawn from a **dedicated shadow sprite**, not by projecting the body sprite
(`shadow_apply`, `light.c`). By default a single flat under-foot shadow is blitted at a fixed rotation,
darkened by the ambient colour. With "real shadows" enabled (off by default), the engine *additionally*
casts **one directional shadow sprite per nearby light**: it looks the light's relative screen position up
in a prerendered map to choose the shadow sprite's **rotation and frame** (so the shadow points away from
that light) and darkens it by the light's brightness versus ambient. The shadow imagery is always
**prebaked sprite frames** *selected* by the light geometry — never sheared or ray-cast per pixel. There
is no sun-direction shadow outdoors; overland shadows come from placed lights plus the ambient blob.

### Animated scenery and the nocturnal cycle

A scenery object whose current art has **more than one frame auto-animates in place** (campfires,
fountains, flames) at the art's `fps` — the flame *is* the object, perfectly positioned by definition,
with no separate overlay. Lamps that light up at night are a **separate "nocturnal" object**: the engine
switches nocturnal scenery **off during the day and on at night** (`light.c`, the 06:00–18:00 test), so by
day you see a static unlit lamp and after dusk its lit, animated variant switches on at the same spot.
Many lamps and **all candles** are single-frame art, so they render static — their lit look is painted
into the sprite rather than animated.

### Additive blends

Glows, flames and lamp halos are blitted **additively** (the art's translucency flag maps to tig's
`TIG_ART_BLT_BLEND_ADD`, `tig/art.h:326`), so their transparent/dark pixels add nothing and only the
bright pixels glow over the scene. Eye-candy art (visual effects) also uses subtract, multiply and
several alpha-lerp blend modes for spell and weather effects.

---

## Quick reference

| Thing | Value | Source |
|---|---|---|
| Art type field | `art_id >> 28` | `art.c:668` |
| Palette index | bits 4–5 | `art.c:1029` |
| Generic num shift | `>> 19`, max 512 | `art.c:689` |
| Item num shift | `>> 17`, max 2000 | `art.c:685` |
| Unique-NPC num shift | `>> 20`, max 256 | `art.c:683` |
| Interface num shift | `>> 16`, max 4096 | `art.c:687` |
| Palettes per file | up to 4 | `art.c:110` |
| Rotations | up to 8 (critters mirror 5→8) | `art.c:5588` |
| Pixel depth | 8 bpp, palette-indexed | `art.c:5760` |
| Transparent colour | palette index 0 | `art.c:1191` |
| Pixel rows | top-down | `art.c:5841` |
| Frame compression | per-frame RLE (`0x80` literal / repeat) | `art.c:5862` |
| Tile size | 78 × 40 px | `tile.c:667` |
| Iso projection | `40·(y−x−1)`, `20·(y+x)` | `location.c:138` |
| Daytime | hour ∈ [6, 18) | `light.c:1903` |
