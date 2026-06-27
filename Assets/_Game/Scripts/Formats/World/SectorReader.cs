using System.IO;
using System.Text;

namespace Arcanum.Formats.World
{
    /// <summary>The terrain layer of a sector: a 64×64 grid of tile <c>art_id</c>s.</summary>
    public sealed class SectorTerrain
    {
        public const int Size = 64;
        public const int TileCount = Size * Size;

        /// <summary>Row-major grid (index = <c>y * 64 + x</c>) of packed tile art IDs.</summary>
        public uint[] TileArtIds { get; }

        public SectorTerrain(uint[] tileArtIds) => TileArtIds = tileArtIds;

        public uint At(int x, int y) => TileArtIds[y * Size + x];
    }

    /// <summary>
    /// Reads Arcanum <c>.sec</c> sector files. Full sectors serialize lights, tiles,
    /// roofs, scripts, sounds, blocks and objects in that order (arcanum-ce
    /// <c>sector.c</c>); for terrain rendering we only need the first two: skip the
    /// light list, then read the 4096-entry tile grid.
    /// </summary>
    public static class SectorReader
    {
        // sizeof(LightSerializedData) — alexbatalov/arcanum-ce light.c static_assert.
        private const int LightRecordSize = 0x30;

        /// <summary>Reads just the terrain grid from a (decompressed) sector file.</summary>
        public static SectorTerrain ReadTerrain(byte[] sectorBytes)
        {
            using var stream = new MemoryStream(sectorBytes, writable: false);
            using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

            int lightCount = reader.ReadInt32();
            if (lightCount < 0 || (long)lightCount * LightRecordSize > stream.Length)
                throw new DatFormatException($"Sector has an implausible light count ({lightCount}).");
            stream.Seek((long)lightCount * LightRecordSize, SeekOrigin.Current);

            long need = stream.Position + (long)SectorTerrain.TileCount * 4;
            if (need > stream.Length)
                throw new DatFormatException(
                    $"Sector is truncated: need {need} bytes for the tile grid but only {stream.Length} present.");

            var ids = new uint[SectorTerrain.TileCount];
            for (int i = 0; i < ids.Length; i++) ids[i] = reader.ReadUInt32();
            return new SectorTerrain(ids);
        }

        /// <summary>
        /// Reads the placed objects (scenery, walls, doors, etc.) from a sector. The object
        /// count lives in the last 4 bytes; the list itself is the final section. Rather than
        /// parse every intermediate section exactly, we skip past the lights+tiles (which are
        /// sized precisely) and then locate the list self-validatingly: the correct start is
        /// the version marker from which walking exactly <c>count</c> objects lands on the
        /// trailing count. This is robust to roof/script/sound/block section quirks.
        /// </summary>
        public static System.Collections.Generic.List<Objects.ObjectInstance> ReadObjects(byte[] sectorBytes)
        {
            int len = sectorBytes.Length;
            int count = ReadI32(sectorBytes, len - 4);
            if (count < 0 || count > 200000)
                throw new DatFormatException($"Sector object count is implausible ({count}).");
            // Empty sector (no objects) — common for the void sectors around a small interior map. There's
            // no object list to locate, so return early rather than scanning and failing.
            if (count == 0) return new System.Collections.Generic.List<Objects.ObjectInstance>();

            int lightCount = ReadI32(sectorBytes, 0);
            int afterTiles = 4 + lightCount * LightRecordSize + SectorTerrain.TileCount * 4;

            for (int start = afterTiles; start <= len - 4; start++)
            {
                if (ReadI32(sectorBytes, start) != Objects.ObjectInstanceReader.ObjectFileVersion) continue;
                if (TryWalk(sectorBytes, start, count, len - 4, out var objects)) return objects;
            }

            throw new DatFormatException("Could not locate the sector object list.");
        }

        /// <summary>Locates the object-list start via the self-validating walk; −1 if not found.</summary>
        public static int FindObjectListStart(byte[] sectorBytes)
        {
            int len = sectorBytes.Length;
            int count = ReadI32(sectorBytes, len - 4);
            if (count < 0 || count > 200000) return -1;
            if (count == 0) return -1; // empty sector: no object list (so no trailing block mask to locate)

            int lightCount = ReadI32(sectorBytes, 0);
            int afterTiles = 4 + lightCount * LightRecordSize + SectorTerrain.TileCount * 4;
            for (int start = afterTiles; start <= len - 4; start++)
            {
                if (ReadI32(sectorBytes, start) != Objects.ObjectInstanceReader.ObjectFileVersion) continue;
                if (TryWalk(sectorBytes, start, count, len - 4, out _)) return start;
            }

            return -1;
        }

        private const int TileScriptNodeSize = 0x18; // tile_script_list.c node (flags/id/Script/next)
        private const int ScriptStructSize = 0xC;    // Script = ScriptHeader(8) + num(4)
        private const int BlockMaskSize = 512;       // 128 × uint32 = 4096 bits

        /// <summary>The optional sections between the roofs and the object list, gated by the <c>0xAA00NN</c>
        /// version placeholder (engine <c>sector_load_editor</c>, sector.c). Higher versions add more sections:
        /// <c>0xAA0001</c> tile-scripts, <c>0xAA0002</c> sector-script, <c>0xAA0003</c> townmap/aptitude/light-scheme
        /// + sounds, <c>0xAA0004</c> the block mask. Shipped retail sectors are <c>0xAA0004</c>.</summary>
        public sealed class SectorSections
        {
            public int Version;                                // the 0xAA00NN placeholder (-1 if not present)
            public int TileScriptCount;                        // per-tile triggered scripts
            public int SectorScriptNum;                        // sector-wide script number (0 = none)
            public int TownMapInfo;                            // sector → town-map link
            public int AptitudeAdjustment;                     // magick/tech aptitude bias of the sector
            public int LightScheme;                            // day/night lighting scheme id
            public int SoundFlags, MusicScheme, AmbientScheme; // per-sector audio
            public int BlockMaskOffset = -1;                   // byte offset of the 512-byte block mask, or -1 if this version has none
            public int ObjectListOffset;                       // where the object list begins
        }

        /// <summary>Deterministically walks the placeholder + optional sections (lights → tiles → roofs →
        /// placeholder → [tile-scripts] → [sector-script] → [townmap/aptitude/light-scheme + sounds] → [blocks] →
        /// objects), surfacing the section data + offsets that the heuristic object-list scan skips.</summary>
        public static SectorSections ReadSections(byte[] b)
        {
            var s = new SectorSections { Version = -1 };
            int o = PostRoofsOffset(b);
            if (o < 0 || o + 4 > b.Length)
            {
                s.ObjectListOffset = o;
                return s;
            }

            int v = s.Version = ReadI32(b, o);
            o += 4;
            if (v != 0xAA0000) // tile-scripts
            {
                s.TileScriptCount = ReadI32(b, o);
                o += 4;
                o += s.TileScriptCount * TileScriptNodeSize;
            }

            if (v >= 0xAA0002) // sector-script (Script: hdr 8 + num 4)
            {
                s.SectorScriptNum = ReadI32(b, o + 8);
                o += ScriptStructSize;
            }

            if (v >= 0xAA0003) // townmap_info / aptitude_adj / light_scheme + sounds
            {
                s.TownMapInfo = ReadI32(b, o);
                o += 4;
                s.AptitudeAdjustment = ReadI32(b, o);
                o += 4;
                s.LightScheme = ReadI32(b, o);
                o += 4;
                s.SoundFlags = ReadI32(b, o);
                s.MusicScheme = ReadI32(b, o + 4);
                s.AmbientScheme = ReadI32(b, o + 8);
                o += 0xC;
            }

            if (v >= 0xAA0004)
            {
                s.BlockMaskOffset = o;
                o += BlockMaskSize;
            } // block mask

            s.ObjectListOffset = o;
            return s;
        }

        /// <summary>
        /// The sector's per-tile blocking bitmask — 128 × uint32 = 4096 bits, bit set ⇒ blocked
        /// (engine <c>SectorBlockList.mask</c>), indexed <c>y*64 + x</c>. The block section only exists in
        /// <c>0xAA0004</c> sectors; for older versions there is none (all-walkable), so we gate on the
        /// placeholder version rather than blindly reading the 512 bytes before the object list.
        /// </summary>
        public static bool[] ReadBlockMask(byte[] sectorBytes)
        {
            var blocked = new bool[SectorTerrain.TileCount];
            SectorSections s = ReadSections(sectorBytes);
            if (s.Version < 0xAA0004) return blocked; // no block section in this version (the bug fix)

            // Prefer the validated object-list locator (objStart − 512); fall back to the deterministic offset
            // for empty sectors (no object list to anchor on).
            int objStart = FindObjectListStart(sectorBytes);
            int maskStart = objStart >= 0 ? objStart - BlockMaskSize : s.BlockMaskOffset;
            if (maskStart < 0 || maskStart + BlockMaskSize > sectorBytes.Length) return blocked;

            for (int i = 0; i < blocked.Length; i++)
            {
                uint word = (uint)ReadI32(sectorBytes, maskStart + (i >> 5) * 4);
                blocked[i] = ((word >> (i & 31)) & 1u) != 0;
            }

            return blocked;
        }

        // Byte offset immediately after the lights + tile grid + roof section (where the placeholder begins).
        private static int PostRoofsOffset(byte[] b)
        {
            int lightCount = ReadI32(b, 0);
            int o = 4 + lightCount * LightRecordSize + SectorTerrain.TileCount * 4;
            if (o + 4 > b.Length) return o;
            int empty = ReadI32(b, o);
            o += 4;                                                              // roof: empty flag
            if (empty == 0 && o + RoofCount * 4 <= b.Length) o += RoofCount * 4; // 256 roof art ids if non-empty
            return o;
        }

        private static bool TryWalk(byte[] b, int start, int count, int expectedEnd,
                                    out System.Collections.Generic.List<Objects.ObjectInstance> objects)
        {
            objects = new System.Collections.Generic.List<Objects.ObjectInstance>(count);
            int o = start;
            try
            {
                for (int i = 0; i < count; i++)
                    objects.Add(Objects.ObjectInstanceReader.Read(b, ref o));
            }
            catch
            {
                objects = null;
                return false;
            }

            return o == expectedEnd;
        }

        /// <summary>A non-empty roof grid cell: a 4×4 tile block at (CellX*4, CellY*4) covered by a roof sprite.</summary>
        public readonly struct RoofCell
        {
            public readonly int CellX;
            public readonly int CellY;
            public readonly uint ArtId;

            public RoofCell(int cellX, int cellY, uint artId)
            {
                CellX = cellX;
                CellY = cellY;
                ArtId = artId;
            }
        }

        private const int RoofGrid = 16; // 16×16 roof cells over the 64×64 tile sector
        private const int RoofCount = RoofGrid * RoofGrid;

        /// <summary>
        /// Reads the sector's roof grid (the section right after the tile grid): a flag, then —
        /// if non-empty — 256 roof art IDs on a 16×16 grid. Outdoor sectors use these to cover
        /// buildings; interiors have none.
        /// </summary>
        public static System.Collections.Generic.List<RoofCell> ReadRoofs(byte[] sectorBytes)
        {
            var roofs = new System.Collections.Generic.List<RoofCell>();
            int lightCount = ReadI32(sectorBytes, 0);
            int o = 4 + lightCount * LightRecordSize + SectorTerrain.TileCount * 4;
            if (o + 4 > sectorBytes.Length) return roofs;

            int empty = ReadI32(sectorBytes, o);
            o += 4;
            if (empty != 0 || o + RoofCount * 4 > sectorBytes.Length) return roofs;

            for (int i = 0; i < RoofCount; i++)
            {
                uint artId = (uint)ReadI32(sectorBytes, o + i * 4);
                if (artId == 0 || artId == 0xFFFFFFFF) continue;
                roofs.Add(new RoofCell(i % RoofGrid, i / RoofGrid, artId));
            }

            return roofs;
        }

        private static int ReadI32(byte[] b, int o) => b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24);

        /// <summary>A placed light from the sector's light list (engine <c>LightSerializedData</c>, 48 bytes).</summary>
        public readonly struct SectorLight
        {
            public readonly long Loc; // tile location (x = Loc & 0x3F, y = (Loc >> 32) & 0x3F)
            public readonly int OffsetX, OffsetY;
            public readonly uint Flags;
            public readonly uint ArtId;
            public readonly byte R, G, B;
            public readonly uint TintColor; // tig_color tint (offset 0x24)
            public readonly int Palette;    // palette index (offset 0x28)

            public SectorLight(long loc, int ox, int oy, uint flags, uint artId, byte r, byte g, byte b,
                               uint tintColor, int palette)
            {
                Loc = loc;
                OffsetX = ox;
                OffsetY = oy;
                Flags = flags;
                ArtId = artId;
                R = r;
                G = g;
                B = b;
                TintColor = tintColor;
                Palette = palette;
            }

            // loc is a full LOCATION (x = low dword, y = high dword); intra-sector tile is the low 6 bits of each.
            public int TileX => (int)(Loc & 0x3F);
            public int TileY => (int)((Loc >> 32) & 0x3F);
        }

        /// <summary>
        /// Reads the sector's light list — the very first section (count at offset 0, then
        /// <c>count</c> × 48-byte <c>LightSerializedData</c> records). NOTE the on-disk colour byte order
        /// is r, b, g (light.c struct), not r, g, b.
        /// </summary>
        public static System.Collections.Generic.List<SectorLight> ReadLights(byte[] b)
        {
            int count = ReadI32(b, 0);
            var lights = new System.Collections.Generic.List<SectorLight>(count < 0 ? 0 : count);
            if (count < 0 || 4 + (long)count * LightRecordSize > b.Length) return lights;

            int o = 4;
            for (int i = 0; i < count; i++)
            {
                long loc = (uint)ReadI32(b, o + 0x08) | ((long)ReadI32(b, o + 0x0C) << 32);
                int ox = ReadI32(b, o + 0x10), oy = ReadI32(b, o + 0x14);
                uint flags = (uint)ReadI32(b, o + 0x18);
                uint artId = (uint)ReadI32(b, o + 0x1C);
                byte r = b[o + 0x20], blue = b[o + 0x21], g = b[o + 0x22]; // disk order: r, b, g
                uint tint = (uint)ReadI32(b, o + 0x24);
                int palette = ReadI32(b, o + 0x28);
                lights.Add(new SectorLight(loc, ox, oy, flags, artId, r, g, blue, tint, palette));
                o += LightRecordSize;
            }

            return lights;
        }
    }
}
