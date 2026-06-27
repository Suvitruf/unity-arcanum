using System;
using System.Collections.Generic;
using Arcanum.Formats.Text;

namespace Arcanum.Formats.Art
{
    /// <summary>
    /// Resolves WALL art IDs to <c>art/wall/*.art</c> paths. Ports
    /// <c>a_name_wall_aid_to_fname</c> + <c>build_wall_file_name</c> from arcanum-ce
    /// <c>a_name.c</c>, using <c>art/wall/wallname.mes</c> (3-char names) and
    /// <c>art/wall/structure.mes</c> (interior/exterior name indices). Validated:
    /// all 747 walls of the Dernholm castle sector resolve to existing files.
    /// </summary>
    public sealed class WallArtResolver
    {
        // build_wall_file_name piece-suffix table (a_name.c off_5BB6B0).
        private static readonly string[] Piece =
        {
            "bse","lfc","bse","bcl","bcr","tcl","tcr","uec","lec","w3l","w3a","w3r","w4l","w4a","w4b","w4r",
            "w5l","w5a","w5b","w5c","w5r","d3l","d3a","d3r","d4l","d4a","d4b","d4r","d6l","d6a","d6b","d6c",
            "d6d","d6r","p3l","p3a","p3r","p4l","p4a","p4b","p4r","p5l","p5a","p5b","p5c","p5r",
        };
        private static readonly char[] DamageChar = { 'U', 'L', 'R' };

        private readonly string[] _names;            // 3-char wall names, by file order
        private readonly (int Interior, int Exterior)[] _structures;

        public WallArtResolver(MesFile wallName, MesFile structure)
        {
            var names = new List<string>();
            foreach (var e in wallName.Entries) names.Add(Trim3(e.Value));
            _names = names.ToArray();

            int IndexOf(string token)
            {
                string n = Trim3(token);
                for (int i = 0; i < _names.Length; i++)
                    if (string.Equals(_names[i], n, StringComparison.OrdinalIgnoreCase)) return i;
                return -1;
            }

            var structs = new List<(int, int)>();
            foreach (var e in structure.Entries)
            {
                string[] toks = e.Value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                int interior = toks.Length > 0 ? IndexOf(toks[0]) : 4;
                int exterior = toks.Length > 1 ? IndexOf(toks[1]) : 0;
                structs.Add((interior, exterior));
            }
            _structures = structs.ToArray();
        }

        public static WallArtResolver FromMes(MesFile wallName, MesFile structure)
            => new WallArtResolver(wallName, structure);

        public string Resolve(uint artId)
        {
            if (ArtId.Type(artId) != ArtId.TypeWall) return null;

            int num = (int)((artId >> 20) & 255);
            int rotation = (int)((artId >> 11) & 7);
            int piece = (int)((artId >> 14) & 0x3F);
            int damage = (int)(artId & 0x480);
            int variation = (int)((artId >> 8) & 3);

            // Rotation 2/3/6/7 swaps the two damage bits.
            if (rotation == 2 || rotation == 3 || rotation == 6 || rotation == 7)
            {
                int swapped = 0;
                if ((damage & 0x400) != 0) swapped |= 0x80;
                if ((damage & 0x80) != 0) swapped |= 0x400;
                damage = swapped;
            }

            int newDamage;
            if ((damage & 0x400) != 0) { newDamage = 0x400; if (piece == 7) piece = 0; }
            else if ((damage & 0x80) != 0) { newDamage = 0x80; if (piece == 8) piece = 0; }
            else newDamage = 0;

            if (num < 0 || num >= _structures.Length) return null;
            int nameIdx = rotation / 2 == 0 || rotation / 2 == 3
                ? _structures[num].Interior
                : _structures[num].Exterior;
            if (nameIdx < 0 || nameIdx >= _names.Length) return null;
            if (piece < 0 || piece >= Piece.Length) return null;

            int dmgIdx;
            if ((newDamage & 0x400) != 0) dmgIdx = 1;
            else if ((newDamage & 0x80) != 0) dmgIdx = (piece >= 2 && piece <= 6) ? 1 : 2;
            else dmgIdx = 0;

            return $"art/wall/{_names[nameIdx]}{Piece[piece]}{DamageChar[dmgIdx]}{(char)('0' + variation)}.art".ToLowerInvariant();
        }

        private static string Trim3(string s)
        {
            int sp = s.IndexOf(' ');
            if (sp >= 0) s = s.Substring(0, sp);
            return s.Length > 3 ? s.Substring(0, 3) : s;
        }
    }

    /// <summary>
    /// Resolves PORTAL (door/window) art IDs to <c>art/portal/*.art</c> via
    /// <c>art/portal/portal.mes</c> (number → "&lt;file&gt; &lt;n&gt;"). Ports
    /// <c>a_name_portal_aid_to_fname</c> / <c>sub_4EC8F0</c>.
    /// </summary>
    public sealed class PortalArtResolver
    {
        private const int WindowType = 1;
        // Wall p_piece slots that hold a portal (engine a_name_portal_aid_from_wall_aid).
        private static readonly HashSet<int> DoorSlots = new HashSet<int> { 22, 25, 26, 29, 30, 31, 32 };
        private static readonly HashSet<int> WindowSlots = new HashSet<int> { 10, 13, 14, 17, 18, 19 };

        private readonly MesFile _names;
        private readonly Dictionary<string, int> _numByFile; // portal file name (lower, with .art) → mes key

        public PortalArtResolver(MesFile portalMes)
        {
            _names = portalMes;
            _numByFile = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in portalMes.Entries)
            {
                int sp = e.Value.IndexOf(' ');
                string file = (sp >= 0 ? e.Value.Substring(0, sp) : e.Value).Trim().ToLowerInvariant();
                if (file.Length > 0) _numByFile[file] = e.Key;
            }
        }

        public static PortalArtResolver FromMes(MesFile portalMes) => new PortalArtResolver(portalMes);

        /// <summary>
        /// Derives the portal art that belongs in a wall slot, from the wall's art id and file name — ports
        /// arcanum-ce <c>a_name_portal_aid_from_wall_aid</c>. A door-slot wall yields the DOOR art (the wood
        /// swinging door), a window-slot wall the WINDOW art. The wall file name (<c>{name}{piece}{dmg}{var}.art</c>)
        /// has its piece-type char (index 3) set to 'E' (door) / 'F' (window) and its damage char (index 6) to 'U',
        /// then that portal file is looked up for its number. Returns null if the wall isn't a portal slot.
        /// <para>This is why a "Light Wooden Door" stored with 2-frame window art still renders as a 7-frame swing
        /// door: the engine takes the art from the door-slot wall, not the stored art.</para>
        /// </summary>
        public uint? DeriveFromWall(uint wallArtId, string wallPath)
        {
            if (string.IsNullOrEmpty(wallPath) || ArtId.Type(wallArtId) != ArtId.TypeWall) return null;

            int pPiece = (int)((wallArtId >> 14) & 0x3F);
            bool isWindow;
            if (DoorSlots.Contains(pPiece)) isWindow = false;
            else if (WindowSlots.Contains(pPiece)) isWindow = true;
            else return null;

            int slash = wallPath.LastIndexOf('/');
            string file = slash >= 0 ? wallPath.Substring(slash + 1) : wallPath; // "{name3}{piece3}{dmg}{var}.art"
            if (file.Length < 12) return null;

            char[] f = file.ToCharArray();
            f[3] = isWindow ? 'F' : 'E'; // piece-type char → door/window
            f[6] = 'U';                  // force the undamaged variant
            string portalFile = new string(f).ToLowerInvariant();

            if (!_numByFile.TryGetValue(portalFile, out int key)) return null;
            int num = isWindow ? key - 1001 : key;
            if (num < 0 || num >= 512) return null;

            int rotation = (int)((wallArtId >> 11) & 7);
            // tig_art_portal_id_create: type(28)=PORTAL, num(19), frame(14)=0, rotation(11), windowBit(10), palette 0.
            return ((uint)ArtId.TypePortal << 28)
                 | ((uint)num << 19)
                 | ((uint)rotation << 11)
                 | ((isWindow ? 1u : 0u) << 10);
        }

        public string Resolve(uint artId)
        {
            if (ArtId.Type(artId) != ArtId.TypePortal) return null;

            int num = (int)((artId >> 19) & 0x1FF);
            int type = (int)((artId >> 10) & 1);
            if (type == WindowType) num += 1001;

            string entry = _names.Get(num);
            if (string.IsNullOrEmpty(entry)) return null;

            int sp = entry.IndexOf(' ');
            string file = sp >= 0 ? entry.Substring(0, sp) : entry;
            string path = ("art/portal/" + file).ToLowerInvariant();

            // Damaged portals swap a letter near the end of the file name.
            if ((artId & 0x200) != 0 && path.Length >= 6)
            {
                char[] c = path.ToCharArray();
                c[c.Length - 6] = 'd';
                path = new string(c);
            }
            return path;
        }
    }
}
