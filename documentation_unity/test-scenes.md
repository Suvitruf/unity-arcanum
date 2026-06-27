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
