using System.Collections.Generic;
using System.Globalization;
using Arcanum.Formats.Text;

namespace Arcanum.Formats.World
{
    /// <summary>
    /// One named location on the world map — engine <c>Area</c> (game/area.c), loaded from
    /// <c>mes/gamearea.mes</c>. Area 0 is the special "unknown" placeholder.
    /// </summary>
    public readonly struct Area
    {
        public readonly int Id;

        /// <summary>World-map tile coordinates (engine <c>location_make(x, y)</c>, in tiles on the START_MAP).</summary>
        public readonly long TileX;

        public readonly long TileY;

        /// <summary>Pixel offset for the name label relative to the area point on the world-map UI.</summary>
        public readonly int LabelXOff;

        public readonly int LabelYOff;
        public readonly string Name;
        public readonly string Description;

        /// <summary>Detection radius in <b>tiles</b> — the engine stores <c>radius × 64</c> (default 5 sectors → 320,
        /// area 0 → 0); −1 marks a place not auto-discovered while travelling.</summary>
        public readonly int RadiusTiles;

        public Area(int id, long tileX, long tileY, int labelXOff, int labelYOff, string name, string description, int radiusTiles)
        {
            Id = id;
            TileX = tileX;
            TileY = tileY;
            LabelXOff = labelXOff;
            LabelYOff = labelYOff;
            Name = name;
            Description = description;
            RadiusTiles = radiusTiles;
        }
    }

    /// <summary>
    /// Parses <c>mes/gamearea.mes</c> (engine <c>area_mod_load</c>). Each entry is
    /// <c>{id}{x, y, labelXoff, labelYoff /Name/Description[/Radius:n]}</c> — the leading four are
    /// comma-separated, the rest are slash-separated. Default radius is 5 sectors.
    /// </summary>
    public sealed class AreaList
    {
        private const int DefaultRadiusSectors = 5;

        private readonly List<Area> _areas;
        private readonly Dictionary<int, Area> _byId;

        public AreaList(List<Area> areas)
        {
            _areas = areas;
            _byId = new Dictionary<int, Area>();
            foreach (Area a in areas) _byId[a.Id] = a;
        }

        public IReadOnlyList<Area> Areas => _areas;
        public int Count => _areas.Count;
        public bool TryGet(int id, out Area area) => _byId.TryGetValue(id, out area);

        public static AreaList FromMes(MesFile mes)
        {
            var areas = new List<Area>();
            foreach (KeyValuePair<int, string> e in mes.Entries)
            {
                if (TryParse(e.Key, e.Value, out Area area)) areas.Add(area);
            }

            return new AreaList(areas);
        }

        private static bool TryParse(int id, string value, out Area area)
        {
            area = default;
            if (string.IsNullOrWhiteSpace(value)) return false;

            string[] parts = value.Split('/');
            string[] nums = parts[0].Split(',');
            if (nums.Length < 2) return false;

            long x = ParseLong(nums[0]);
            long y = ParseLong(nums[1]);
            int labelX = nums.Length > 2 ? (int)ParseLong(nums[2]) : 0;
            int labelY = nums.Length > 3 ? (int)ParseLong(nums[3]) : 0;

            string name = parts.Length > 1 ? parts[1].Trim() : string.Empty;
            string desc = parts.Length > 2 ? parts[2].Trim() : string.Empty;

            // Optional trailing "Radius:n" (locale-translated label, e.g. Russian "Радиус:") — the number
            // after the colon is the detection radius in sectors; −1 = not auto-discovered while travelling.
            int radius = DefaultRadiusSectors;
            for (int i = 3; i < parts.Length; i++)
            {
                int colon = parts[i].IndexOf(':');
                if (colon >= 0 && int.TryParse(parts[i].Substring(colon + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int r))
                    radius = r;
            }

            // Engine area_mod_load stores the radius in TILES: radius × 64 (default 5 → 320); area 0 = 0;
            // a −1 radius stays the "not auto-discovered" sentinel.
            int radiusTiles = id == 0 ? 0 : radius == -1 ? -1 : radius * 64;
            area = new Area(id, x, y, labelX, labelY, name, desc, radiusTiles);
            return true;
        }

        private static long ParseLong(string s) =>
            long.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long v) ? v : 0;
    }
}
