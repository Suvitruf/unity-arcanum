# World, maps & quests

How *Arcanum: Of Steamworks and Magick Obscura* lays out its game world, moves the player between
maps, draws the overland world map, and tracks quests in the journal. Reconstructed from the
[`arcanum-ce`](https://github.com/alexbatalov/arcanum-ce) decompilation of the retail engine and
cross-checked against the shipped game data. Source references point at files/lines so claims can be
verified.

In short: the world is a set of maps, each map is a grid of 64×64-tile **sectors**, and you move
between maps by stepping on invisible **jump points** or using scripted **teleporter** objects. The
**world map** is a separate overland screen for long-distance travel between known **areas**; town
maps overlay a fog-of-war minimap that unfogs as you explore. **Quests** advance through a fixed
seven-value state ladder; the journal shows every quest you've heard of, sorted by when its state
last changed.

## Contents

- [World & map structure](#world--map-structure)
- [The map index (`MapList.mes`)](#the-map-index-maplistmes)
- [Map transitions](#map-transitions)
  - [Jump points (`map.jmp`)](#jump-points-mapjmp)
  - [Scripted teleporters & doors](#scripted-teleporters--doors)
- [The world map](#the-world-map)
- [Town maps & fog-of-war](#town-maps--fog-of-war)
- [Quests & the journal](#quests--the-journal)

## World & map structure

A **map** is a single playable space — a town, a dungeon level, a wilderness encounter, or the
overland continent. On disk a map is a folder under `maps/<name>/` holding the sector files, the
map's jump points, and its properties.

Every map is a rectangular grid of **sectors**. A sector is a fixed **64×64 tile** block stored in
its own `.sec` file. The map's `map.prp` records the map's full size in tiles, which is always a
whole number of sectors:

```
/* 0x00 */ int32  base_terrain_type   // default terrain fill
/* 0x08 */ int64  width               // map width  in TILES  = sectorsWide × 64
/* 0x10 */ int64  height              // map height in TILES   = sectorsHigh × 64
```

Source: `MapProperties` in `arcanum-ce src/game/map.c:82`, read in `map_open` (`map.c:621`).

A **location** is a single tile, packed into a 64-bit integer as global X/Y tile coordinates
(`location.h:38`):

```
x = loc & 0xFFFFFFFF
y = (loc >> 32) & 0xFFFFFFFF
```

A tile's sector is found by dividing both coordinates by 64 (a right-shift of 6):

```
sector_x = x >> 6
sector_y = y >> 6
```

Source: `sector_id_from_loc` (`sector.c:592`, `LOCATION_GET_X(loc) >> 6`). The low 6 bits of each
coordinate (`& 0x3F`) are the tile's position *within* its 64×64 sector. The engine streams sectors
in and out around the player as they move, so only nearby sectors are resident at once.

A map's default entry tile is read from an optional `startloc.txt` (two text lines, x then y) when
the map is opened (`map_open`, `map.c:631`); it is not part of `map.prp`.

## The map index (`MapList.mes`)

Maps refer to each other by a small integer **map id**, not by folder name. The translation from id
to folder lives in `rules/MapList.mes` (`map_list_info_load`, `map.c:2003`). In the shipped data this
holds 81 maps.

The engine reads `.mes` keys starting at **5000**, incrementing by one and stopping at the first gap.
The **1-based position** in that consecutive run is the map id — so map id 1 is key 5000, map id 2 is
key 5001, and so on (`map_id = key − 4999`). Each value is comma-separated:

```
Name, x, y [, Type: <T>] [, WorldMap: <N>] [, Area: <N>]
```

For example:

```
{5000}{Arcanum1-024-fixed, 92958,82592, Type: START_MAP, WorldMap: 0}
```

- `Name` is the map folder under `maps/` (matched case-insensitively; folder names contain spaces and
  irregular casing, e.g. "Bates Mansion Lev 1").
- `x, y` is the map's location on the overland.
- `Type: START_MAP` marks the overland continent map (map id 1, `Arcanum1-024-fixed`).
- `WorldMap: <N>` keys the map to a world-map definition.
- `Area: <N>` links the map to an overland **area** (a town or landmark). Many sub-maps share one
  area — e.g. Tarant's town map, its sewers, and Bates Mansion all carry the same `Area:` id.

The folder is resolved from an id by `map_get_name` (`map.c:880`).

## Map transitions

There are two ways the player crosses from one map to another: passive **jump points** baked into the
map, and active **teleporter** objects triggered by use or script.

### Jump points (`map.jmp`)

Each map folder carries a `map.jmp` file listing **jump points**. A jump point is a single trigger
tile plus a destination (a map id and a tile). When the player's object lands on a jump-point tile,
the engine teleports them to the destination map and tile.

Source: `map_process_jumppoint` (`map.c:1002`) looks up the player's tile via `jumppoint_get`, fills
a teleport request with `{ loc = dst_loc, map = dst_map }`, and calls `teleport_do`. The destination
folder is resolved through `MapList` and loaded from `maps/<name>/`.

The file format (`jumppoint.c`, struct in `jumppoint.h`, 32 bytes per entry):

```
int32   count
count × JumpPoint:                 // 0x20 = 32 bytes each
  /* 0x00 */ uint32  flags         // always 0 in shipped data
  /* 0x08 */ int64   loc           // trigger tile (packed location)
  /* 0x10 */ int32   dst_map       // 1-based MapList id; 0 ⇒ same map
  /* 0x18 */ int64   dst_loc       // destination tile (packed location)
```

Jump points are **invisible** — the engine only draws them in the editor; in-game they are silent
tile triggers. A real-world transition is usually a small cluster of adjacent trigger tiles all
pointing at the same destination, so the doorway is a 2–3 tile band rather than a single pixel.
For instance, Bates Mansion Lev 1 has six jump-point tiles (across columns 109–110, rows 92–94) all
warping to map 1 at tile (61976, 65664) — i.e. stepping out of the mansion drops you onto the
overland.

Most maps' `map.jmp` files are empty (`count == 0`); that is normal, not an error. The overland
START map itself has no jump points — you leave it by clicking the world map or entering an area's
edge.

### Scripted teleporters & doors

Some transitions are not baked into the map grid but driven by **scripts** attached to objects —
typically doors, stairs, ladders, and trapdoors. When the player uses such an object, its `SAP_USE`
script fires and runs a teleport action that moves the player (and the script carries the destination
map and tile). The engine flags these triggers with `SF_TELEPORT_TRIGGER` (`script.h:93`) and the
teleport action is `SAT_TELEPORT` (`script.h:196`). Critters also carry built-in teleport-destination
fields (`OBJ_F_CRITTER_TELEPORT_DEST` / `OBJ_F_CRITTER_TELEPORT_MAP`, `obj.h:253`) used by scripted
follow-the-player transitions.

The underlying move is the same `teleport_do` path used by jump points, so scripted doors and
passive tiles end up at the same place: tear down the current map, load the destination, and warp the
player to the target tile.

## The world map

The **world map** is a pan/zoom overland view of the continent with a marker for each known location.
Clicking a marker fast-travels there. It is a separate screen from the in-map view (`wmap_ui.c`,
`area.c`).

**Data files:**

| File | Purpose |
|---|---|
| `WorldMap/WorldMap.mes` | layout: `numHor, numVer, chunkBase, ZoomedName, MapKeyedTo`. Shipped overland: `8, 8, SmallMapChunks, MapKeyedTo: 1`. |
| `WorldMap/SmallMapChunks001…064.bmp` | overland image tiles, 8-bpp palettized BMP, 250×250 px each. |
| `WorldMap/Map_Zoomed.bmp` | a single 365×365 continent-overview image. |
| `mes/gamearea.mes` | the overland **areas** (locations). |
| `rules/MapList.mes` | maps; map 1 is the `START_MAP` overland. |

**The overland image** is assembled from `numHor × numVer` (8×8) chunks of 250 px each into a single
2000×2000 image. Chunks are 1-based and row-major, with chunk 001 at the top-left
(`index = row × numHor + col + 1`).

**Coordinate conversion** between overland tiles and image pixels (`wmap_ui.c:1643,1756`, with map
pixel width 2000):

```
px_x = 2000 − tileX / 64      (x is flipped)
px_y =        tileY / 64      (y is not)
```

**Areas** are the clickable locations. `mes/gamearea.mes` (`area_mod_load`, `area.c`) holds 82
entries:

```
{id}{tileX, tileY, labelXoff, labelYoff /Name/Description[/Radius:n]}
```

The leading four fields are comma-separated; the name/description are slash-separated. The optional
trailing `Radius:n` is the area's detection radius **in sectors** (default 5; converted to tiles as
`radius × 64` in `area.c:242`). A radius of `−1` means the area is never auto-discovered by travel.
Area 0 is the "unknown" placeholder used for open wilderness (`AREA_UNKNOWN`). Areas are points on the
overland, not separate maps; one area maps to many sub-maps via each map's `Area:` field.

**Travel.** The engine moves the player as overland travel rather than an instant jump: the party
walks a route of waypoints across the continent, game time advances roughly an hour per sector
crossed (`wmap_ui.c:3996`), and each step rolls for **random encounters** (frequency, terrain gating,
and level-scaled monster groups from `Rules/WMap_Rnd.mes`). An encounter interrupts travel, loads an
encounter map, and resumes afterward. Areas passed within their detection radius en route become
**known** (`area_set_known`).

**Where the world map is available.** You can only open the world map (and thus travel) when the
player is standing on the overland in open wilderness, not inside a town or dungeon
(`wmap_ui_open_internal`, `wmap_ui.c:1137`): the current map must be the `START_MAP`, the player's
area must be `AREA_UNKNOWN`, the player must be alive and conscious, not in an encounter, and the
current sector must not be blocked (`sector_is_blocked`, `wmap_ui.c:1171`). To travel out of a town
you walk out of it first. The map shows only areas the player already **knows** (`area_is_known`).

## Town maps & fog-of-war

Inside a town the player has a separate **town map** — a top-down minimap of the local area that
starts fogged and unfogs as the player explores. A sector can be tagged with a town map id
(`townmap_set`, `townmap.c:217`), and the known/unknown state of each town-map tile is stored as a
**bit array** (`townmap.c:55`).

As the player moves, the tiles they have seen are marked known (`townmap_tile_known_set`,
`townmap.c:625`); a tile's known state is read back with `townmap_tile_known_get` (`townmap.c:639`).
When drawing the map, an unknown tile is shown only if it has a known neighbour, so the explored
region has a soft fogged border rather than a hard edge — the engine checks the four adjacent tiles
and adds edge-blit flags accordingly (`townmap.c:504–545`). A tile with no known neighbour is not
drawn at all.

The whole map can be revealed or re-hidden at once via `townmap_set_known` (`townmap.c:459`), which
fills the bit array with `0xFF` (all known) or `0` (all unknown) (`townmap_set_known_internal`,
`townmap.c:602`). A tile's coordinates convert to/from its town-map index with
`townmap_loc_to_coords` (`townmap.c:338`). The fog state persists in the save game; if no saved state
exists, the town map starts fully unknown (`townmap.c:687`).

## Quests & the journal

A quest progresses through a fixed ladder of seven states (`QuestState`, `quest.h:8`):

| # | State | Meaning |
|---|---|---|
| 0 | `Unknown` | Never heard of. Not shown in the journal. |
| 1 | `Mentioned` | Heard about it, not yet undertaken. First state shown in the journal. |
| 2 | `Accepted` | Taken on; objective active. |
| 3 | `Achieved` | The objective is done, but not yet turned in / resolved. |
| 4 | `Completed` | Finished successfully (by this character). Awards XP and an alignment shift. |
| 5 | `OtherCompleted` | Finished, but by someone else (another party member / globally). |
| 6 | `Botched` | Can no longer be completed. |

A quest is "known" and appears in the journal once its state is at least `Mentioned` (≥ 1).

**State rules** (`quest_state_set`, `quest.c:366`):

- State only moves **forward** — a request to set a state lower than the current one is ignored.
- Once a quest is `Completed`, `OtherCompleted`, or `Botched`, its state is frozen.
- Reaching `Completed` awards the quest's XP (see below) and applies its alignment adjustment, and
  plays the quest-complete sound.
- Accepting a quest from an NPC nudges that NPC's reaction to at least neutral; completing it nudges
  their reaction up further (`quest_state_set_internal`, `quest.c:398`).

**Global vs per-character state.** Each quest has a single **global** state shared across all
characters (`quest_gstate`), which uses a reduced set of values — `Accepted` (the default, even if
nobody has taken it), `Completed` (somebody finished it), or `Botched` (nobody can finish it now). On
top of that, each player character stores their **own** per-quest state and the timestamp of its last
change (`PcQuestState`: a `DateTime` plus an `int state`, `quest.h:25`), in a per-character array
field (`OBJ_F_PC_QUEST_IDX`). If a character tries to advance a quest that is already globally
completed, their copy is recorded as `OtherCompleted`; if it's globally botched, theirs becomes
`Botched` (`quest.c:398`).

**Botching** is stored as a modifier bit on the per-character state rather than overwriting it, so the
prior progress is preserved underneath. `quest_unbotch` (`quest.c`) clears that bit and restores the
quest to its earlier state — used to revive a quest that became un-botched.

**Data files** (loaded in `quest.c`):

- `rules/gamequest.mes` (keys 1000–1999) — per-quest metadata, parsed by `quest_parse`
  (`quest.c:231`). Each entry is a space-separated list: the quest's **XP level**, an **alignment
  adjustment** (applied on completion), then three banks of seven dialog entry points — one per state
  — for normal dialog, bad-reaction dialog (used when the NPC's reaction is 20 or lower), and
  low-intelligence dialog. These entry points feed the `Q:` generated-dialog operator so NPCs comment
  on quest progress.
- `mes/gamequestlog.mes` (keys 1000–1999) — the **journal description** text, one line per quest
  (`quest.c:154`).
- `mes/gamequestlogdumb.mes` — alternative journal descriptions for low-intelligence characters
  (`quest.c:165`). `quest_copy_description` (`quest.c`) picks the "dumb" version when the character's
  Intelligence is at or below the low-IQ threshold, otherwise the normal one.
- `rules/xp_quest.mes` — quest XP rewards, keyed by **XP level** (not quest id). `quest_get_xp`
  (`quest.c:629`) looks up the level from the quest's metadata and returns the XP amount; that XP is
  granted when the quest reaches `Completed`.

**The journal** is built from the character's per-quest array. `quest_get_logbook_data`
(`quest.c:577`) walks all 2000 quest slots, collects every quest whose state is not `Unknown`,
exposes a botched quest as `Botched`, and **sorts the entries by their change timestamp** (earliest
first) so the journal reads in chronological order. Each row pairs the quest's state with its
description text from `gamequestlog.mes`. When a quest's state changes for the local player, the
logbook button in the interface is lit to signal there's something new to read
(`quest_state_set_internal`, `quest.c`).
