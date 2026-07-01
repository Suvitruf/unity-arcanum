# Test scenes & game-data setup

This project runs on the **original Arcanum's own data files** — it bundles none of them, so the first thing any
scene needs is to know where your installed game lives. This page covers that setup and the **test scenes** that
exercise one subsystem at a time.

## Pointing at your Arcanum data

The engine reads the game's `.dat` archives at runtime (`arcanum1.dat … arcanum4.dat`, plus the module data).
`GameDataLocator` resolves a file by name (e.g. `arcanum2.dat`) so nothing in the code hard-codes a path. You tell
it where to look in one of two ways:

1. **A `GameDataConfig` asset (recommended).** `Assets → Create → Arcanum → Game Data Config`, place the asset in
   a **`Resources/`** folder (any one — that's how it's loaded, by name), and set **Data Roots** to your install:
   the folder that contains `arcanum1.dat … arcanum4.dat`. Add more than one root if your data is split.
2. **A project-local drop folder.** Put the `.dat` files in a `GameData/` folder at the project root. This is the
   relative fallback, searched unless the config turns it off.

`GameDataConfig` also has a **Search project GameData folder** toggle (on by default). With neither a config nor a
`GameData/` folder, the locator finds nothing and logs *"No root config found"* — that's expected; just point it at
your data. **No install paths are baked into the source**, which is what keeps the repo machine-independent.

> You must own a legitimate copy of *Arcanum* (GOG or Steam). The project only reads the data you already have.

## Test scenes

Test scenes exercise **one subsystem in isolation** — handy for eyeballing a feature, catching regressions, and
shipping a small self-contained slice. Each is a single **driver `MonoBehaviour`** on an otherwise empty scene:
press Play and it mounts the data (via `GameDataLocator`), builds its content, and frames the camera itself. No
manual scene wiring.

### Character art gallery — `Scenes/TestCharactersArt`

Driver: **`CharacterArtGallery`**. Renders every **player race × gender** as a grid of critter sprites, each a
small **turntable**: it turns through all eight isometric facings and plays the idle animation, so you can check a
character from every side. Labels under each cell name the race and gender.

It's built straight from the game's own art pipeline — the critter art-id is assembled from race/gender/equipment
bits, resolved to a `.art` file, and decoded to sprites — so the grid doubles as a check on the **paper-doll
composition** and its documented fallbacks: female critter art exists only for the **human** body (a non-human
female falls back to the race's male body), and the small races share the **halfling** body.

Inspector knobs (all `[SerializeField] private`, so they're editor-only): the archive to read, starting facing and
turntable speed, the **armour**/**weapon** codes (e.g. armour `0` = the base "UW" body, which shows race/gender
most clearly), pixels-per-unit, and the grid spacing.

Open the scene, make sure your data is configured (above), and press Play.

### Tile gallery — `Scenes/TestTiles`

Driver: **`TileGallery`**. Reads the tile-name table (`art/tile/tilename.mes`), takes the unique terrain codes
across its four buckets (outdoor/indoor × flippable/non-flippable), and shows each terrain's **base tile** as a
labelled grid. It's a quick way to confirm that every terrain type **resolves and decodes**, and to spot any that
are missing a base tile — the Console logs `TileGallery: N terrain types (M without a base tile)`, and a missing
one appears as an empty labelled cell.

A "base tile" is the non-blended, full tile of a terrain (engine `<name>bse0a` — edge 0, variant a); the blended
edge/corner variants that stitch two terrains together aren't shown here. Inspector knobs (all `[SerializeField]
private`): the archive to read, the column count, cell size, and pixels-per-unit.

As with the character gallery, configure your data and press Play.

> The gallery shows each terrain's base tile in isolation. To see **real, blended terrain** assembled the way the
> game draws it, use the terrain demo below — it runs the actual in-game generator over a real sector.

### Terrain / sector demo — `Scenes/TestTerrain`

Driver: **`TileMapDemo`**. Loads one real Arcanum **sector** (a 64×64 patch of the world, a `.sec` file from the
module archive) and renders its ground layer through **`TileMapRenderer`** — the *exact* terrain generator the full
game uses. So unlike the gallery, this exercises the whole path end-to-end: tile art-ids resolved through the
blend/variant routing, the mirror-edge tiles (the flip edges that reuse a canonical tile drawn horizontally
swapped), facade tiles (large buildings stored in the tile layer), and the **batched mesh** — all 4096 tiles packed
into one atlas + one draw call, depth-ordered so the diamond edges overlap correctly. Drag to pan, scroll to zoom;
the Console logs the tile count and any blend misses.

![Switching sectors live with the Sector Browser](https://cdn.arcanum.aapanasik.com/github/test-scene-terrain.gif)

This is the test scene's real purpose: it proves the extracted generator works in isolation, and it's the
self-contained slice you can ship to the public repo (it needs only `Arcanum.Formats` + `Arcanum.World`, no
gameplay code — see [Terrain rendering](terrain-rendering.md)). Inspector knobs (all `[SerializeField] private`):
the two source archives, the sector path, the **Batch Terrain** toggle (off = one `SpriteRenderer` per tile, the
heavy fallback), pixels-per-unit, and the background colour.

Configure your data and press Play.

#### Switching sectors — the Sector Browser

You rarely want to hand-type sector paths. The `TileMapDemo` inspector has a **Browse sectors…** button that opens
a **Sector Browser** window (as shown above):

- It enumerates every `maps/<name>/<id>.sec` in the demo's module archive and lists them with a **search** box —
  type part of a map name to filter (e.g. `bates`, `shrouded`). **Refresh** re-scans.
- Each row has a **Load** button; the currently rendered sector is highlighted and marked `◀ current`.
- In **Play mode**, Load renders the picked sector immediately and re-frames the camera — so you can walk the whole
  world's terrain a sector at a time without leaving Play. In **edit mode**, Load just writes the choice to the
  **Sector Path** field (it renders on the next Play).

Under the hood the archives are mounted once and the resolvers/sprite caches are reused, so switching sectors is
fast. The browser is editor-only (`TileMapDemoEditor` + `SectorBrowserWindow` in the `Arcanum.Editor` assembly); the
runtime `LoadSector(path)` method it calls is public, so a build could drive it too.
