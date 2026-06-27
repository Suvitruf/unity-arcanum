using System.Globalization;
using Arcanum.Formats.Text;

namespace Arcanum.Formats.World
{
    /// <summary>
    /// World-map image layout from <c>WorldMap/WorldMap.mes</c> (engine <c>wmap_load_worldmap_info</c>).
    /// Entry <c>50 + worldmapId</c> is <c>numHor, numVer, chunkBaseName, ZoomedName: &lt;name&gt;,
    /// MapKeyedTo: &lt;mapId&gt;</c> — e.g. <c>8, 8, SmallMapChunks, ZoomedName: Map_Zoomed, MapKeyedTo: 1</c>.
    /// The overland image is the <c>numHor×numVer</c> grid of <c>&lt;chunkBaseName&gt;NNN.bmp</c> tiles
    /// (1-based, 3-digit), each <see cref="TileSize"/> px; <c>&lt;zoomedName&gt;.bmp</c> is a single overview.
    /// </summary>
    public readonly struct WorldMapInfo
    {
        /// <summary>Engine default chunk tile size (px); WorldMap.mes entry 50 can override but the shipped one doesn't.</summary>
        public const int TileSize = 250;

        public readonly int NumHorTiles;
        public readonly int NumVerTiles;
        public readonly string ChunkBaseName; // e.g. "SmallMapChunks" → SmallMapChunks001.bmp …
        public readonly string ZoomedName;    // single overview image base, e.g. "Map_Zoomed"
        public readonly int MapKeyedTo;       // MapList map id whose overland this image represents

        public WorldMapInfo(int numHor, int numVer, string chunkBaseName, string zoomedName, int mapKeyedTo)
        {
            NumHorTiles = numHor;
            NumVerTiles = numVer;
            ChunkBaseName = chunkBaseName;
            ZoomedName = zoomedName;
            MapKeyedTo = mapKeyedTo;
        }

        public int PixelWidth => NumHorTiles * TileSize;
        public int PixelHeight => NumVerTiles * TileSize;

        /// <summary>Path of the 1-based chunk at <paramref name="index"/> (1..NumHor*NumVer): <c>WorldMap/&lt;base&gt;NNN.bmp</c>.</summary>
        public string ChunkPath(int index) => $"WorldMap/{ChunkBaseName}{index:D3}.bmp";
        public string ZoomedPath => $"WorldMap/{ZoomedName}.bmp";

        /// <summary>Parses entry <c>50 + worldmapId</c> of WorldMap.mes; returns false if absent/malformed.</summary>
        public static bool TryParse(MesFile mes, int worldmapId, out WorldMapInfo info)
        {
            info = default;
            if (mes == null || !mes.TryGet(50 + worldmapId, out string value) || string.IsNullOrWhiteSpace(value))
                return false;

            string[] p = value.Split(',');
            if (p.Length < 3) return false;

            if (!int.TryParse(p[0].Trim(), out int numHor)) return false;
            if (!int.TryParse(p[1].Trim(), out int numVer)) return false;
            string chunkBase = p[2].Trim();
            string zoomed = null;
            int mapKeyedTo = 0;
            for (int i = 3; i < p.Length; i++)
            {
                int colon = p[i].IndexOf(':');
                if (colon < 0) continue;
                string key = p[i].Substring(0, colon).Trim();
                string val = p[i].Substring(colon + 1).Trim();
                if (key.Equals("ZoomedName", System.StringComparison.OrdinalIgnoreCase)) zoomed = val;
                else if (key.Equals("MapKeyedTo", System.StringComparison.OrdinalIgnoreCase))
                    int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out mapKeyedTo);
            }

            info = new WorldMapInfo(numHor, numVer, chunkBase, zoomed, mapKeyedTo);
            return true;
        }
    }
}
