using System.Collections.Generic;
using Arcanum.Formats.Art;
using Arcanum.Formats.Database;
using Arcanum.Formats.Tiles;
using Arcanum.Formats.World;
using Arcanum.Runtime.Art;
using Arcanum.Runtime.World;
using UnityEngine;

namespace Arcanum.World
{
    /// <summary>
    /// Encapsulated terrain/tile generator: turns a sector's terrain grid (<see cref="SectorTerrain"/>) into
    /// rendered Unity geometry, either as a single batched mesh (one draw call per sector) or, as a fallback,
    /// one <see cref="SpriteRenderer"/> per tile.
    /// <para>
    /// Extracted from the game's main world controller so the exact in-game terrain generation can be reused by
    /// standalone test scenes and shipped as a self-contained slice. Depends only on <c>Arcanum.Formats</c>
    /// (pure readers) plus the engine-art primitives in this assembly — no gameplay code.
    /// </para>
    /// </summary>
    public sealed class TileMapRenderer
    {
        private const int TerrainAtlasSize = 2048;

        private readonly DatVirtualFileSystem _vfs;
        private readonly TileArtPathResolver _tileResolver;
        private readonly FacadeArtResolver _facades; // large buildings stored in the tile layer (TIG type 11)
        private readonly float _pixelsPerUnit;

        private readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();
        private readonly Dictionary<string, ArtFile> _artCache = new Dictionary<string, ArtFile>();
        private readonly Dictionary<(string, int), Sprite> _facadeSpriteCache = new Dictionary<(string, int), Sprite>();
        private readonly HashSet<string> _blendMissSamples = new HashSet<string>();
        private int _blendMisses;

        /// <param name="vfs">Mounted archive(s) holding the tile + facade art.</param>
        /// <param name="tileResolver">Tile art-id → path resolver (from <c>art/tile/tilename.mes</c>).</param>
        /// <param name="facades">Optional facade resolver (from <c>art/facade/facadename.mes</c>); may be null.</param>
        /// <param name="pixelsPerUnit">Sprite PPU (the engine art is 100 px/unit).</param>
        public TileMapRenderer(DatVirtualFileSystem vfs, TileArtPathResolver tileResolver, FacadeArtResolver facades, float pixelsPerUnit)
        {
            _vfs = vfs;
            _tileResolver = tileResolver;
            _facades = facades;
            _pixelsPerUnit = pixelsPerUnit;
        }

        /// <summary>Places one terrain tile in the per-tile fallback path. Lets the host own depth/Z ordering.</summary>
        public delegate SpriteRenderer PlaceTileDelegate(string name, Transform parent, Sprite sprite, Vector3 localPos, int sortingOrder);

        /// <summary>Blend tiles whose every variant was missing (fell back to the base tile). Diagnostic only.</summary>
        public int BlendMisses => _blendMisses;

        /// <summary>A capped sample of the missing blends (<c>"name1+name2 e&lt;edge&gt;"</c>), for logging.</summary>
        public IReadOnlyCollection<string> BlendMissSamples => _blendMissSamples;

        /// <summary>
        /// Renders one sector's terrain under <paramref name="root"/>. When <paramref name="batch"/> is true (the
        /// default in-game path) this emits a single packed-atlas mesh; otherwise it places one sprite per tile via
        /// <paramref name="placeTile"/> (required for the fallback). <paramref name="offX"/>/<paramref name="offY"/>
        /// are the sector's global tile origin.
        /// </summary>
        public void RenderSector(SectorTerrain terrain, int offX, int offY, Transform root, Material baseMaterial,
                                 int sortingOrder, bool batch, PlaceTileDelegate placeTile = null)
        {
            // Terrain tiles must NOT use the shared sprite atlas: the batched path packs each tile's own texture
            // into its mesh atlas (Texture2D.PackTextures), so a tile sprite whose .texture is a shared atlas page
            // would pack garbage and break the UVs. Build tiles with standalone textures; restore the atlas after.
            RuntimeSpriteAtlas prevAtlas = ArtTextureFactory.ActiveAtlas;
            ArtTextureFactory.ActiveAtlas = null;
            try
            {
                if (batch) BuildTerrainMesh(terrain, offX, offY, root, baseMaterial, sortingOrder);
                else BuildTerrainTiles(terrain, offX, offY, root, placeTile);
            }
            finally { ArtTextureFactory.ActiveAtlas = prevAtlas; }
        }

        // Fallback: one GameObject+SpriteRenderer per tile (4096 per sector). Simple but heavy.
        private void BuildTerrainTiles(SectorTerrain terrain, int offX, int offY, Transform root, PlaceTileDelegate placeTile)
        {
            if (placeTile == null)
            {
                Debug.LogWarning("TileMapRenderer: non-batched terrain needs a placeTile delegate; nothing drawn.");
                return;
            }

            Sprite filler = GetTileSprite(MostCommonArtId(terrain));
            var parent = NewChild("Tiles", root);
            for (int y = 0; y < SectorTerrain.Size; y++)
            {
                for (int x = 0; x < SectorTerrain.Size; x++)
                {
                    Sprite sprite = GetTileSprite(terrain.At(x, y)) ?? filler;
                    if (sprite == null) continue;
                    int gx = offX + x, gy = offY + y;
                    placeTile($"Tile_{x}_{y}", parent, sprite, IsoProjection.TileToWorld(gx, gy, _pixelsPerUnit), (gx + gy) * 2);
                }
            }
        }

        // Batched terrain: pack the sector's distinct tile textures into one atlas, then emit a single mesh of
        // 4096 quads (one draw call) instead of 4096 GameObjects. Quads are ordered back-to-front by iso depth so
        // the diamond edges overlap correctly within the one transparent mesh.
        private void BuildTerrainMesh(SectorTerrain terrain, int offX, int offY, Transform root, Material baseMaterial, int sortingOrder)
        {
            const int size = SectorTerrain.Size;
            Sprite filler = GetTileSprite(MostCommonArtId(terrain));

            var distinct = new List<Texture2D>();
            var indexByTex = new Dictionary<Texture2D, int>();
            int[] tileTex = new int[size * size];
            bool[] tileFlip = new bool[size * size]; // mirrored blend tiles (flip edges) reuse canonical art flipped
            Sprite sample = null;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    uint aid = terrain.At(x, y);
                    Sprite own = GetTileSprite(aid);
                    Sprite sp = own ?? filler;

                    Texture2D tex = sp ? sp.texture : null;
                    if (!tex)
                    {
                        tileTex[y * size + x] = -1;
                        continue;
                    }

                    sample ??= sp;
                    if (!indexByTex.TryGetValue(tex, out int idx))
                    {
                        idx = distinct.Count;
                        indexByTex[tex] = idx;
                        distinct.Add(tex);
                    }

                    tileTex[y * size + x] = idx;
                    tileFlip[y * size + x] = own && TileArtId.IsMirrored(aid);
                }
            }

            if (!sample) return;

            var atlas = new Texture2D(TerrainAtlasSize, TerrainAtlasSize, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "TerrainAtlas"
            };
            Rect[] rects = atlas.PackTextures(distinct.ToArray(), 2, TerrainAtlasSize, makeNoLongerReadable: true);

            // Quad footprint + pivot from a sample tile (all tiles share 78×40 with the same hotspot).
            float w = sample.rect.width / _pixelsPerUnit, h = sample.rect.height / _pixelsPerUnit;
            float px = sample.pivot.x / sample.rect.width, py = sample.pivot.y / sample.rect.height;

            var order = new List<int>(size * size);
            for (int i = 0; i < size * size; i++)
                if (tileTex[i] >= 0)
                    order.Add(i);
            order.Sort((a, b) => ((a % size) + (a / size)).CompareTo((b % size) + (b / size)));

            var verts = new List<Vector3>(order.Count * 4);
            var uvs = new List<Vector2>(order.Count * 4);
            var tris = new List<int>(order.Count * 6);
            foreach (int i in order)
            {
                int x = i % size, y = i / size;
                Vector3 pos = IsoProjection.TileToWorld(offX + x, offY + y, _pixelsPerUnit);
                Vector3 bl = pos + new Vector3(-px * w, -py * h, 0f);
                int b = verts.Count;
                verts.Add(bl);
                verts.Add(bl + new Vector3(w, 0f, 0f));
                verts.Add(bl + new Vector3(0f, h, 0f));
                verts.Add(bl + new Vector3(w, h, 0f));
                Rect r = rects[tileTex[i]];
                // Mirrored tiles (flip edges 2/9/12/13) reuse the canonical art with horizontally swapped U.
                float u0 = tileFlip[i] ? r.xMax : r.xMin;
                float u1 = tileFlip[i] ? r.xMin : r.xMax;
                uvs.Add(new Vector2(u0, r.yMin));
                uvs.Add(new Vector2(u1, r.yMin));
                uvs.Add(new Vector2(u0, r.yMax));
                uvs.Add(new Vector2(u1, r.yMax));
                tris.Add(b);
                tris.Add(b + 2);
                tris.Add(b + 3);
                tris.Add(b);
                tris.Add(b + 3);
                tris.Add(b + 1);
            }

            var mesh = new Mesh { name = "TerrainMesh" };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            // White vertex colours: the 2D lit sprite shader multiplies by vertex colour, and a bare mesh
            // otherwise defaults to black under it. Harmless for the unlit material too.
            var cols = new Color32[verts.Count];
            for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 255, 255, 255);
            mesh.SetColors(cols);
            mesh.RecalculateBounds();

            var go = new GameObject("TerrainMesh");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            Material terrainBase = baseMaterial ?? new Material(Shader.Find("Sprites/Default"));
            mr.sharedMaterial = new Material(terrainBase) { mainTexture = atlas };
            mr.sortingOrder = sortingOrder;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        /// <summary>
        /// Resolves a terrain tile art-id to its sprite: facade tiles (large buildings, TIG type 11) decode to the
        /// facade art's 78×40 frame; everything else routes through the blend/variant resolver with a base fallback.
        /// Returns null when no art file exists. Cached.
        /// </summary>
        private Sprite GetTileSprite(uint artId)
        {
            // Facade tiles (large buildings) live in the tile layer as TIG type 11; resolve them to the facade
            // art's specific 78×40 frame instead of falling back to filler terrain.
            if (ArtId.Type(artId) == ArtId.TypeFacade) return GetFacadeSprite(artId);
            string path = _tileResolver.ResolveExisting(artId, _vfs.Exists, OnTileBlendMissing);
            if (path == null || !_vfs.Exists(path)) path = _tileResolver.BaseFallback(artId);
            if (path == null || !_vfs.Exists(path)) return null;
            return GetSprite(path); // hotspot pivot, like objects/roofs and the engine
        }

        // A blend whose every variant was absent (after the variant search) — the cases that may still need
        // intermediate-terrain routing. Collected so we can see whether the remaining gap is worth that work.
        private void OnTileBlendMissing(string name1, string name2, int edge)
        {
            _blendMisses++;
            if (_blendMissSamples.Count < 24) _blendMissSamples.Add($"{name1}+{name2} e{edge}");
        }

        // The 78×40 facade frame for a facade tile id (its own hotspot, like a tile). Cached by (path, frame).
        private Sprite GetFacadeSprite(uint artId)
        {
            string path = _facades?.Resolve(artId);
            if (path == null || !_vfs.Exists(path)) return null;
            int frame = FacadeArtResolver.Frame(artId);
            if (_facadeSpriteCache.TryGetValue((path, frame), out var cached)) return cached;

            Sprite sprite = null;
            try
            {
                if (!_artCache.TryGetValue(path, out var art))
                {
                    art = ArtReader.Read(_vfs.ReadAllBytes(path));
                    _artCache[path] = art;
                }

                ArtFrame[] frames = art.Rotations[0].Frames;
                ArtFrame f = frames[Mathf.Clamp(frame, 0, frames.Length - 1)];
                sprite = ArtTextureFactory.CreateSprite(f, art.PrimaryPalette, _pixelsPerUnit);
            }
            catch (System.Exception ex) { Debug.LogWarning($"facade decode failed '{path}' frame {frame}: {ex.Message}"); }

            _facadeSpriteCache[(path, frame)] = sprite;
            return sprite;
        }

        private Sprite GetSprite(string path)
        {
            if (_spriteCache.TryGetValue(path, out var cached)) return cached;
            Sprite sprite = null;
            try
            {
                ArtFile art = ArtReader.Read(_vfs.ReadAllBytes(path));
                ArtFrame frame = art.Rotations[0].Frames[0];
                sprite = ArtTextureFactory.CreateSprite(frame, art.PrimaryPalette, _pixelsPerUnit);
            }
            catch (System.Exception ex) { Debug.LogWarning($"decode failed '{path}': {ex.Message}"); }

            _spriteCache[path] = sprite;
            return sprite;
        }

        private static Transform NewChild(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        private static uint MostCommonArtId(SectorTerrain terrain)
        {
            var counts = new Dictionary<uint, int>();
            foreach (uint id in terrain.TileArtIds)
                counts[id] = counts.TryGetValue(id, out int c) ? c + 1 : 1;
            uint best = 0;
            int bestCount = -1;
            foreach (var kvp in counts)
                if (kvp.Value > bestCount)
                {
                    bestCount = kvp.Value;
                    best = kvp.Key;
                }

            return best;
        }
    }
}
