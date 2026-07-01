# Terrain rendering — the `Arcanum.World` assembly

How this project turns a sector's ground-tile grid into rendered geometry. The terrain generator is isolated in its
own assembly so it can be reused by test scenes and shipped as a self-contained slice, independent of the rest of
the game.

For the underlying data — the `.sec` tile list, the tile art-id encoding, and how blend edges and mirroring work in
the original game — see [Data formats](../documentation/data-formats.md) and
[Art & graphics](../documentation/art-and-graphics.md). This page is about the **Unity side**.

## Assembly layout

The Unity code is split so that the lowest, reusable layer doesn't depend on gameplay:

| Assembly | Contents | References |
|---|---|---|
| `Arcanum.Formats` | Pure C# file/format readers — no Unity. | — |
| **`Arcanum.World`** | Unity-side art primitives + the terrain/tile generator. | `Arcanum.Formats` |
| `Arcanum.Runtime` | The full game (world controller, combat, UI, …). | `Arcanum.World`, `Arcanum.Formats`, … |

`Arcanum.World` holds the engine-art primitives every sprite needs — `ArtTextureFactory` (decoded ART frame →
`Texture2D`/`Sprite`), `RuntimeSpriteAtlas`, `IsoProjection` (tile ↔ world position), and `GameDataLocator` (finds
your install's `.dat` files) — plus `TileMapRenderer`, the terrain generator. Pulling these *down* out of the
gameplay assembly is what lets a test scene render real terrain while referencing only `Arcanum.Formats` +
`Arcanum.World`. (A few of these primitives keep their original `Arcanum.Runtime.*` namespaces for now even though
they live in `Arcanum.World`; the assembly boundary, not the namespace, is what matters for the slice.)

## `TileMapRenderer`

The generator is a plain class (not a `MonoBehaviour`) — you construct it with the data it needs and call it per
sector:

```csharp
var tileMap = new TileMapRenderer(vfs, tileResolver, facades, pixelsPerUnit);
tileMap.RenderSector(terrain, offX, offY, root, baseMaterial, sortingOrder, batch: true);
```

- **`vfs`** — a mounted `DatVirtualFileSystem` (the archives holding the tile + facade art).
- **`tileResolver`** — `TileArtPathResolver`, built from `art/tile/tilename.mes`; maps a tile art-id to its `.art`
  path, including the blend/variant search.
- **`facades`** — optional `FacadeArtResolver` (`art/facade/facadename.mes`); large buildings are stored in the
  tile layer as TIG type 11 and decode to a specific frame rather than a terrain tile.
- **`offX/offY`** — the sector's global tile origin. Only matters when stitching adjacent sectors; a standalone
  sector renders at `(0, 0)`.

`RenderSector` parents its output under `root` and, when `batch` is true, produces a single mesh; otherwise it falls
back to one sprite per tile via a caller-supplied `PlaceTileDelegate` (so the host keeps control of depth/Z order).
`BlendMisses` / `BlendMissSamples` expose the tiles whose every blend variant was missing (they fell back to the
base tile) — a diagnostic for terrain coverage.

### The batched mesh

The in-game path (`batch: true`) builds **one mesh per sector** instead of 4096 GameObjects:

1. Collect the sector's **distinct** tile textures and pack them into one `Texture2D` atlas (`PackTextures`).
2. Emit one quad per tile, UV'd into the atlas. **Mirror-edge tiles** (the flip edges that reuse a canonical
   neighbour drawn horizontally swapped) are handled by swapping the quad's U coordinates — no separate texture.
3. Order the quads **back-to-front by isometric depth** (`x + y`) so the diamond tile edges overlap correctly
   within the single transparent mesh.
4. Give every vertex white vertex colours — the URP 2D lit sprite shader multiplies by vertex colour, so a bare
   mesh would otherwise render black under lighting.

The result is one draw call per sector. Terrain deliberately bypasses the shared runtime sprite atlas while
building (each tile is packed into the *mesh* atlas instead), and restores it afterwards.

## Where it's used

- **The game** — `ArcanumSectorDemo` constructs one `TileMapRenderer` and calls `RenderSector` for each streamed
  sector, passing its lit material and a placement delegate for the fallback path.
- **The test scene** — `TileMapDemo` (`Scenes/TestTerrain`) loads a single real sector and renders it through the
  same `TileMapRenderer`. See [Test scenes](test-scenes.md).
