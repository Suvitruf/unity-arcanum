using System.Collections.Generic;
using System.Globalization;
using Arcanum.Formats.Text;

namespace Arcanum.Formats.World
{
    /// <summary>Map classification from the <c>Type:</c> field (engine <c>MAP_TYPE</c>, map.c:107).</summary>
    public enum MapType { None = 0, StartMap = 1, ShoppingMap = 2 }

    /// <summary>One map's entry in <c>rules/MapList.mes</c> (engine <c>MapListInfo</c>, map.c).</summary>
    public readonly struct MapListEntry
    {
        /// <summary>1-based map id, used by <see cref="JumpPoint.DstMap"/> and the engine's teleport.</summary>
        public readonly int MapId;

        /// <summary>Map folder name under <c>maps/</c> (case-insensitive on disk).</summary>
        public readonly string Name;

        /// <summary>Default arrival tile (world map coordinates); the comma-separated x, y in the entry.</summary>
        public readonly long X;

        public readonly long Y;

        /// <summary>World-map id this map belongs to, or −1 if unspecified.</summary>
        public readonly int WorldMap;

        /// <summary>Area id, or 0 if unspecified.</summary>
        public readonly int Area;

        /// <summary>Map classification (overland start map / shopping map), from the <c>Type:</c> field.</summary>
        public readonly MapType Type;

        public MapListEntry(int mapId, string name, long x, long y, int worldMap, int area, MapType type)
        {
            MapId = mapId;
            Name = name;
            X = x;
            Y = y;
            WorldMap = worldMap;
            Area = area;
            Type = type;
        }
    }

    /// <summary>
    /// Resolves the map id stored in a <see cref="JumpPoint"/> to a map folder name (and back),
    /// from <c>rules/MapList.mes</c>. Ports arcanum-ce <c>map_list_info_load</c> / <c>map_get_name</c>:
    /// entries are numbered consecutively from 5000; map id is the 1-based position in that run.
    /// Each value is comma-separated: <c>Name, x, y[, Type: ...][, WorldMap: N][, Area: N]</c>.
    /// </summary>
    public sealed class MapList
    {
        /// <summary>First <c>.mes</c> key the engine scans from (map id 1).</summary>
        public const int FirstKey = 5000;

        private readonly List<MapListEntry> _entries = new List<MapListEntry>();

        private readonly Dictionary<string, MapListEntry> _byName =
            new Dictionary<string, MapListEntry>(System.StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<MapListEntry> Entries => _entries;

        public static MapList Read(byte[] mesBytes) => Read(MesReader.Read(mesBytes));

        public static MapList Read(MesFile mes)
        {
            var list = new MapList();
            // The engine starts at key 5000 and increments, stopping at the first gap.
            for (int key = FirstKey; mes.TryGet(key, out string value); key++)
            {
                int mapId = key - FirstKey + 1;
                var entry = Parse(mapId, value);
                list._entries.Add(entry);
                if (!list._byName.ContainsKey(entry.Name))
                    list._byName[entry.Name] = entry;
            }

            return list;
        }

        /// <summary>Looks up a map by its 1-based id; false if out of range.</summary>
        public bool TryGet(int mapId, out MapListEntry entry)
        {
            if (mapId >= 1 && mapId <= _entries.Count)
            {
                entry = _entries[mapId - 1];
                return true;
            }

            entry = default;
            return false;
        }

        /// <summary>The map folder name for a 1-based id, or null if out of range (engine <c>map_get_name</c>).</summary>
        public string GetName(int mapId) => TryGet(mapId, out var e) ? e.Name : null;

        /// <summary>Finds a map by folder name (case-insensitive); false if absent (engine <c>map_list_info_find</c>).</summary>
        public bool TryGetByName(string name, out MapListEntry entry) => _byName.TryGetValue(name, out entry);

        private static MapListEntry Parse(int mapId, string value)
        {
            string[] parts = value.Split(',');
            string name = parts.Length > 0 ? parts[0].Trim() : string.Empty;
            long x = parts.Length > 1 ? ParseLong(parts[1]) : 0;
            long y = parts.Length > 2 ? ParseLong(parts[2]) : 0;

            int worldMap = -1;
            int area = 0;
            MapType type = MapType.None;
            for (int i = 3; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (TryNamed(p, "WorldMap:", out int wm)) worldMap = wm;
                else if (TryNamed(p, "Area:", out int a)) area = a;
                else if (TryMapType(p, out MapType t)) type = t;
            }

            return new MapListEntry(mapId, name, x, y, worldMap, area, type);
        }

        // Type: NONE / START_MAP / SHOPPING_MAP (engine off_59F058). Returns false if not a Type: token.
        private static bool TryMapType(string token, out MapType type)
        {
            type = MapType.None;
            const string prefix = "Type:";
            if (!token.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)) return false;
            string v = token.Substring(prefix.Length).Trim();
            if (string.Equals(v, "START_MAP", System.StringComparison.OrdinalIgnoreCase)) type = MapType.StartMap;
            else if (string.Equals(v, "SHOPPING_MAP", System.StringComparison.OrdinalIgnoreCase)) type = MapType.ShoppingMap;
            return true;
        }

        private static bool TryNamed(string token, string prefix, out int result)
        {
            result = 0;
            if (!token.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)) return false;
            return int.TryParse(token.Substring(prefix.Length).Trim(),
                NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }

        private static long ParseLong(string s) =>
            long.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long v) ? v : 0;
    }
}
