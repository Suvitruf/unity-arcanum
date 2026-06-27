# Data formats — how Arcanum stores its game data

*Arcanum: Of Steamworks and Magick Obscura* keeps almost all of its content in a handful of binary
formats packed into a few `.dat` archives. Everything an installation ships — sprites, text, prototypes,
maps, dialog, scripts — lives inside those archives and is read through one virtual file system. This
page describes the **container** (`.dat`), the **text tables** (`.mes`), the **object model** that
underpins prototypes and map objects (`.pro`, `.sec`, `.mob`), and how an object's typed fields are laid
out on disk. The sprite, dialog, and script formats are mentioned where they fit but documented in detail
elsewhere — see the [Art & graphics](art-and-graphics.md), [Dialog](dialog.md), and
[Scripting](scripting.md) pages.

The big idea to hold onto: Arcanum has a single, general **object record** format. A *prototype* and a
*map object* are the same kind of record — the only difference is whether the record stores **every**
field of its type or **only the fields that were changed** away from a prototype.

Source references point at the [`arcanum-ce`](https://github.com/alexbatalov/arcanum-ce) decompilation of
the retail engine and its low-level library **tig**, so claims can be checked — chiefly
`first_party/tig/src/database.c` (`.dat`), `src/game/mes.c` (`.mes`), `src/game/obj.c` /
`obj_private.c` (the object/field engine), `src/game/sector.c` + `sector_object_list.c` (`.sec`), and
`obj_file.c` / `obj_id.c` (`.mob` and object IDs).

---

## `.dat` archives — zlib entries with a table of contents in the footer

A `.dat` is a flat archive of files. Each entry may be stored **plain** or **zlib-compressed**, and the
**table of contents lives at the end of the file**, not the start — so a reader seeks to the footer first,
learns where every entry is, then seeks back to pull bytes out.

### Reading the footer

The very last bytes of the file form a fixed footer. A reader seeks to **12 bytes before EOF** and reads
(`tig_database_open`, `database.c:53`):

| Field | Size | Meaning |
|---|---|---|
| magic (`id`) | 4 | FourCC. `' TAD'` = a plain archive; `'1TAD'` = an archive carrying a 16-byte GUID |
| name-table size | 4 | total bytes of all entry name strings in the TOC |
| entry-table offset | 4 | distance, measured back from EOF, to the start of the entry table |

If the magic is `'1TAD'`, a **16-byte GUID** and a repeat of the magic sit just before those 12 bytes
(read from 24 bytes before EOF, `database.c:90`); a `' TAD'` archive simply has a zero GUID. The reader
then seeks to `EOF − 4 − entry-table-offset` and reads a 4-byte **entry-table size**, followed
immediately by the **entry count** (4 bytes). The base offset at which the *actual file data* begins is
computed as `file-size − entry-table-size − entry-table-offset` (`database.c:142`), and every entry's
stored offset is relative to that base.

### The entry table

Then come `entry-count` records, each (`database.c:160`):

| Field | Size | Meaning |
|---|---|---|
| name length | 4 | length of this entry's name string (taken from the name table) |
| name | *name length* | the path, e.g. `art\interface\interface.art` |
| *(reserved)* | 4 | skipped |
| flags | 4 | `0x01` = stored plain, `0x02` = zlib-compressed; `0x400` = directory |
| size | 4 | uncompressed size in bytes |
| compressed size | 4 | bytes actually stored (equals `size` for plain entries) |
| offset | 4 | start of this entry's data, relative to the data base computed above |

Names are stored Windows-style (backslashes) and the engine lowercases them and converts separators on
load, so lookups are case-insensitive and slash-agnostic. A compressed entry is a raw zlib stream that the
engine inflates on the fly through a small streaming buffer (`database.c:646`); a plain entry is copied
verbatim. Directory entries (flag `0x400`) exist only so the archive can answer "list this folder".

Multiple archives can be mounted at once; lookups walk the open archives, so a patch archive can shadow
files in a base archive. An archive can also carry an "ignore list" that hides specific entries.

---

## `.mes` message tables — number → string

A `.mes` file is a plain-text lookup table: a list of **`{number}{string}` pairs**, used everywhere a
number needs to map to a human string or a filename. The art-id resolver, descriptions, UI labels, name
lists — all read `.mes` files.

The grammar is brace-delimited and forgiving (`parse_field` / `parse_entry`, `mes.c:516`):

- A **field** is whatever sits between a `{` and the next `}`. Everything outside braces — whitespace,
  newlines, comment-ish text between records — is skipped.
- A **record** is two consecutive fields: the first is the **number** (parsed with `atoi`; the first
  character is expected to be a digit), the second is the **string value**.

So a line looks like:

```
{169}{char_but}
{5000}{The lever won't budge.}
```

Strings are read literally, including any internal punctuation, up to the closing brace; a `{` seen while
reading a value is flagged as a probably-missing `}`. Text is 8-bit (Latin-1). After loading, entries are
sorted by number and looked up by binary search (`mes.c`), and duplicate numbers are reported. A value can
span lines, since newlines inside a field are just part of the string.

Some `.mes` files are pure data tables where the *value* itself is structured (tab- or slash-separated
columns the caller splits further) — the `.mes` layer only ever sees "number → one string"; any internal
structure is the caller's convention.

---

## The object model — one record format for prototypes and map objects

Everything placeable in the world — a wall, a door, a chest, a sword, a townsperson — is an **object**.
An object is a typed bag of **fields**. The set of fields an object has is fixed by its **type**, and the
on-disk record can be written in two modes: a **prototype** writes *every* field of its type, while an
**instance** (a map object) writes *only the fields it overrode*, leaning on its prototype for the rest.

### Object types

The object's type (`ObjectType`, `obj.h:351`) picks which fields it has. The values are:

| # | Type | # | Type | # | Type |
|---|---|---|---|---|---|
| 0 | WALL | 7 | ARMOR | 14 | GENERIC (item) |
| 1 | PORTAL | 8 | GOLD (money) | 15 | PC |
| 2 | CONTAINER | 9 | FOOD | 16 | NPC |
| 3 | SCENERY | 10 | SCROLL | 17 | TRAP |
| 4 | PROJECTILE | 11 | KEY | 18 | MONSTER |
| 5 | WEAPON | 12 | KEY_RING | 19 | UNIQUE_NPC |
| 6 | AMMO | 13 | WRITTEN | | |

Types 5–14 are the **item** family; PC and NPC are the **critter** family.

### Fields are organised into per-type groups

The field enum (`ObjectField`, `obj.h:10`) is divided into **groups** bracketed by `*_BEGIN` / `*_END`
sentinels. Every object, whatever its type, starts with the **common** group (`OBJ_F_BEGIN … OBJ_F_END`):
location, the current art-id, render offsets, flags, name and description ids, armour class, hit-point
fields, material, the scripts array, the sound-effect id, and so on. After the common group, an object
carries the group(s) for its type:

- An item subtype carries the shared **ITEM** group *plus* its own group. A weapon, for example, gets
  `OBJ_F_ITEM_BEGIN … ITEM_END` followed by `OBJ_F_WEAPON_BEGIN … WEAPON_END`
  (`object_proto_enumerate_fields`, `obj.c:4676`). Armour gets ITEM + ARMOR, ammo gets ITEM + AMMO, and
  so on.
- A critter carries the shared **CRITTER** group plus PC or NPC (`obj.c:4757`).
- Simple world objects (WALL, PORTAL, CONTAINER, SCENERY, PROJECTILE, TRAP) carry the common group plus
  their single own group.

Because the layout is "common group, then type group(s), in enum order", an object's fields always occupy
the **same positions** for a given type — which is what lets the change bitmap below address them by bit.
The `*_BEGIN`/`*_END` sentinels are layout markers only and are never written as data; the engine
iterates `begin+1 … end` (`obj.c:4614`). Each group also reserves a few padding slots so the layout has
stable, future-proof positions.

### Field data types

Each field has a data type (`ObjDataType`, `obj_private.h:11`) that decides how it is encoded on disk:

| Type | On-disk encoding |
|---|---|
| `INT32` | 4 raw bytes, **no presence flag** |
| `INT64` | 1 presence byte; if set, 8 bytes follow |
| `STRING` | 1 presence byte; if set, a 4-byte length then *length + 1* bytes (text + NUL) |
| `HANDLE` (object reference) | 1 presence byte; if set, a 24-byte **ObjectID** (see below) |
| array types (`INT32_ARRAY`, `INT64_ARRAY`, `UINT32_ARRAY`, `UINT64_ARRAY`, `SCRIPT_ARRAY`, `QUEST_ARRAY`, `HANDLE_ARRAY`) | 1 presence byte; if set, a sparse-array blob (below) |

The per-field type table is `object_fields[]`, populated field-by-field in `obj.c` (e.g. `LOCATION` is
`INT64`, `OVERLAY_*` are `UINT32_ARRAY`). Only `INT32` is written bare; every other type carries a leading
presence byte so an absent value costs a single zero byte.

### Array fields are sparse

Array-typed fields (overlays, the per-stat tables, the scripts list, the quest table…) are stored as a
**sparse keyed array**: a small header `{ size, count, bitset_id }` followed by `size × count` bytes of
**compactly packed** data, followed by a **bitset** marking which logical keys are actually present
(`sa_write_file`, `sa.c:110`; bitset = a 4-byte count then that many 32-bit words, `obj_private.c:1403`).

The crucial consequence: the data is dense, so a logical index is **not** `data + key × size`. To find a
key's slot you test its bit in the bitset, and the physical slot is the **rank** of that bit — the
popcount of set bits *before* it. This matters for anything that reads an array field by index (the
scripts array indexed by attachment point, the per-stat arrays, etc.): walk the bitset, don't multiply.

### Prototype vs instance — the field-48 change bitmap

This is the heart of the format. Both kinds of record begin the same way (`obj_read`, `obj.c:1233`):

```
[ int32 version = 119 ]
[ ObjectID oid (24 bytes) ]
```

What follows is decided by the object's **prototype id**. A **prototype** has a "blocked" prototype id
(type tag −1) and writes the *full* field set; an **instance** references a real prototype and writes
*only its overrides*. The reader branches on that tag, not on any explicit "is-proto" flag (`obj.c:1246`).

**Prototype record** (`obj_proto_write_file`, `obj.c:2892`), after the version + oid:

1. `int32 type`
2. the **available mask** — a bitmap (one bit per field, 32 fields per 32-bit word) saying which fields
   this prototype declares
3. **every field** of the type, in enum order, with no per-field gate

**Instance record** (`obj_inst_write_file`, `obj.c:2988`), after the version + oid:

1. `int32 type`
2. `int16 num_fields` — how many overridden fields follow
3. the **change bitmap** (the field the engine calls `field_48`) — one bit per field, set for each field
   this instance overrode
4. **only the overridden fields**, in field order, each gated by its bit in the change bitmap

So an instance stores the common preamble, its change bitmap, and then a **densely packed** run of just
the values it changed; every unset bit means "use the prototype's value". The packed value at logical
field *N* lives at the rank (popcount) of bit *N* within the change bitmap — the same sparse-rank trick as
array fields, applied to the whole record. When the engine reads a field whose change bit is clear, it
resolves the read against the object's prototype.

This is the single most important thing to get right when reading map data: an instance does **not** store
its fields at fixed offsets and does **not** store placeholders for unchanged fields — it stores a bitmap
and a compact list, and everything else is inherited.

> A subtle point for the **loose `.pro` files** specifically: prototypes store *every* field, so a field
> being present does not mean it holds a meaningful value. Several item fields (notably weight and worth)
> are written as **sentinels** (e.g. −1) in the shipped protos and are filled in by the engine at load
> time from internal default tables (`set_item_defaults`, `obj.c`), not read from the proto. Reading the
> raw field gives you the sentinel, not the real number.

### Object prototypes — `.pro`

A prototype is one object record in the prototype mode above, stored as a **loose file** under the
archive's `proto\` folder. The filename encodes the prototype's numeric id and its name:
`proto\%06d - %s.pro` (`proto.c:300`) — a zero-padded 6-digit number, a space-dash-space, then the name,
e.g. `proto\000146 - Plate Gauntlets.pro`. The number is the prototype's **"A" id** (the 32-bit id form
described below). Map objects reference their prototype by id, and at load the engine resolves that to the
matching `.pro`.

---

## Object IDs — the 24-byte OID

Every object record carries an **ObjectID** ("OID"), a fixed **24-byte** structure
(`static_assert(sizeof == 0x18)`, `obj_id.h:34`):

```
int16  type                  // which kind of id this is
int16  (padding)
int32  (padding)
union (16 bytes) {           // interpreted per type
    int64   handle;          // a live in-memory handle
    int32   a;               // an "A" id (the proto-file number)
    Guid    guid;            // a 16-byte GUID
    struct  { int64 location; int32 temp_id; int32 map; } p;   // positional id
}
```

The `type` tag tells you how to read the 16-byte payload (`obj_id.h:13`):

| Tag | Name | Meaning |
|---|---|---|
| −2 | HANDLE | a live runtime handle; never a persistent identity |
| −1 | BLOCKED | marks a **prototype** record (drives the proto-vs-instance branch) |
| 0 | NULL | no id |
| 1 | A | a single 32-bit value — used for prototype file numbers |
| 2 | GUID | a 16-byte GUID — the durable global identity of an instance |
| 3 | P | a *positional* id `{ location, temp_id, map }` — identifies a static sector object by its map, tile, and load-order index |

The OID is written as a plain 24-byte struct (no per-field encoding) right after the 4-byte version at the
start of every object record (`obj.c:1242`). The positional (type 3) id's `temp_id` is the object's index
within its sector's load order, assigned while the sector's object list is read (`sector_object_list.c:166`).

---

## `.sec` sectors — terrain plus a list of objects

A sector is a **64 × 64 = 4096-tile** square of the world. Its file is named after a packed 64-bit
coordinate id rendered as a decimal string — `<id>.sec` (`sector.c:1465`) — where the low 26 bits are the
sector's X and the next 26 bits its Y (`sector.h:84`). The world tile origin of a sector is its
coordinates shifted left by 6 (64 tiles per side).

A `.sec` is read as a sequence of sections in a fixed order (`sector_load_game`, `sector.c:1453`):

1. **Light list** — placed lights
2. **Tile list** — the 4096 ground-tile art-ids
3. **Roof list** — roof art
4. a 4-byte **version/placeholder** word in the range `0xAA0000 … 0xAA0004`, which gates the optional
   sections that follow (older sectors stop earlier)
5. **Tile-script list** (if the version is past the base)
6. **Sector-script list** (version ≥ `0xAA0002`)
7. **townmap-info**, **aptitude adjustment**, **light scheme** (three 4-byte ints), then the **sound
   list** (version ≥ `0xAA0003`)
8. **Block list** — per-tile blocking (version ≥ `0xAA0004`)
9. **Object list** — always last

### The light list

The first section is a 4-byte **count**, then that many fixed **48-byte** light records
(`LightSerializedData`, `static_assert(sizeof == 0x30)`, `light.c:46`):

| Offset | Size | Field | Notes |
|---|---|---|---|
| 0x00 | 8 | `obj` | owning object handle (runtime; not a persistent identity) |
| 0x08 | 8 | `loc` | a full **LOCATION** — x = low dword, y = high dword; the sector-local tile is the low 6 bits of each (`LOCATION_GET_X/Y`, `light.c:1156`) |
| 0x10 | 4 | `offset_x` | sub-tile pixel offset of the light |
| 0x14 | 4 | `offset_y` | |
| 0x18 | 4 | `flags` | light flags (rotation/type bits) |
| 0x1C | 4 | `art_id` | the light sprite's art-id |
| 0x20 | 1 | `r` | colour — **stored on disk as r, b, g**, not r, g, b |
| 0x21 | 1 | `b` | |
| 0x22 | 1 | `g` | |
| 0x24 | 4 | `tint_color` | packed tint (`tig_color`) |
| 0x28 | 4 | `palette` | palette index |
| 0x2C | 4 | *(padding)* | |

Two gotchas: `loc` is a **full LOCATION**, not a packed tile id — the sector-local Y comes from the high
dword (masked to 6 bits), not from shifting the low word; and the three colour bytes are in **r, b, g**
order on disk.

### The object list and its trailing count

The object list is a run of full object records — **same format as every other object record** — but the
**count is written at the very end of the file**, after all the records. To read it (`objlist_load`,
`sector_object_list.c:115`): remember the current position, seek to **4 bytes before EOF**, read the
32-bit **object count**, seek back, then read exactly that many objects with `obj_read`. As a sanity check
the count is read a second time (now reached sequentially at EOF) and compared. On save, the writer
streams out each *static* object, counting as it goes, and writes the single 4-byte count last
(`objlist_save`, `sector_object_list.c:306`).

Only **static** objects (walls, scenery, fixed containers) are persisted in the sector. Movable and
dynamic objects are handled separately, as `.mob` files.

---

## `.mob` objects — one mobile object per file

A `.mob` file is a **solitary object record**: exactly one object, written and read with the same
`obj_write` / `obj_read` used everywhere else (`objf_solitary_write` / `objf_solitary_read`,
`obj_file.c:16`). Each mobile object — a critter, a droppable item, anything dynamic — lives in its own
file named after its OID string (`obj_file.c:149`), and a companion `.del` marker file records deletions.

This split mirrors the static/dynamic divide: the `.sec` holds the fixed scenery embedded in its object
list, while the surrounding mobile objects are individual `.mob` files keyed by OID. (For a finished,
shipped map these per-object `.mob` files are consolidated into a single file that begins with a 16-byte
GUID and then runs object records back-to-back until EOF.)

---

## See also

- `.art` sprites and the art-id bitfields — **[Art & graphics](art-and-graphics.md)**.
- `.dlg` conversations — **[Dialog](dialog.md)**.
- `.scr` scripts and the object heartbeat — **[Scripting](scripting.md)**.
