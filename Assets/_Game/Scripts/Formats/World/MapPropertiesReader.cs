using System.IO;
using System.Text;

namespace Arcanum.Formats.World
{
    /// <summary>
    /// A map's <c>map.prp</c> properties (engine <c>MapProperties</c>, map.c). 24 bytes:
    /// base terrain type and the map's full extent in tiles (stored as sectors × 64,
    /// i.e. <c>sectorCount &lt;&lt; 6</c>).
    /// </summary>
    public readonly struct MapProperties
    {
        /// <summary>On-disk record size — engine <c>static_assert(sizeof(MapProperties) == 0x18)</c>.</summary>
        public const int RecordSize = 0x18;

        public readonly int BaseTerrainType;
        /// <summary>Map width in tiles (a multiple of 64).</summary>
        public readonly long Width;
        /// <summary>Map height in tiles (a multiple of 64).</summary>
        public readonly long Height;

        public MapProperties(int baseTerrainType, long width, long height)
        {
            BaseTerrainType = baseTerrainType;
            Width = width;
            Height = height;
        }

        /// <summary>Map width in 64×64 sectors.</summary>
        public int SectorsWide => (int)(Width >> 6);
        /// <summary>Map height in 64×64 sectors.</summary>
        public int SectorsHigh => (int)(Height >> 6);
    }

    /// <summary>Reads a map's <c>map.prp</c> file. Ports the <c>MapProperties</c> read in arcanum-ce <c>map_open</c>.</summary>
    public static class MapPropertiesReader
    {
        public static MapProperties Read(byte[] prpBytes)
        {
            if (prpBytes.Length < MapProperties.RecordSize)
                throw new DatFormatException(
                    $"map.prp is too small ({prpBytes.Length} bytes); expected {MapProperties.RecordSize}.");

            using var stream = new MemoryStream(prpBytes, writable: false);
            using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

            int baseTerrain = reader.ReadInt32();
            reader.ReadInt32();              // padding_4
            long width = reader.ReadInt64();
            long height = reader.ReadInt64();
            return new MapProperties(baseTerrain, width, height);
        }
    }
}
