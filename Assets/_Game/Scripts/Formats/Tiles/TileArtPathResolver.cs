using System;
using Arcanum.Formats.Text;

namespace Arcanum.Formats.Tiles
{
    /// <summary>
    /// Turns a sector's packed tile <c>art_id</c> into an <c>art/tile/*.art</c>
    /// virtual path. Ports <c>a_name_tile_aid_to_fname</c> + <c>build_tile_file_name</c>
    /// from arcanum-ce <c>a_name.c</c>. Validated against the shipped game. The "flipped" blend edges
    /// (2/9/12/13) ship no file of their own — they reuse their canonical-edge partner (8/3/6/7) drawn
    /// horizontally mirrored (see <see cref="TileArtId.FileEdge"/> / <see cref="TileArtId.IsMirrored"/>); this
    /// resolves to the canonical file and the renderer flips it. <see cref="BaseFallback"/> covers anything still
    /// absent.
    /// </summary>
    public sealed class TileArtPathResolver
    {
        // Maps a blend-edge index (0..15) to its file-name character (a_name.c off_5BB4E4).
        private const string EdgeChars = "06b489237ea5dc10";

        private readonly TileNameTable _table;

        public TileArtPathResolver(TileNameTable table) => _table = table;

        public static TileArtPathResolver FromMes(MesFile tilenameMes)
            => new TileArtPathResolver(TileNameTable.FromMes(tilenameMes));

        /// <summary>The primary <c>art/tile/*.art</c> path, or null if the id isn't a resolvable tile.</summary>
        public string Resolve(uint artId)
        {
            if (!TileArtId.IsTile(artId)) return null;

            int type = TileArtId.TileType(artId);
            string name1 = _table.Lookup(TileArtId.Num1(artId), type, TileArtId.Flippable1(artId));
            string name2 = _table.Lookup(TileArtId.Num2(artId), type, TileArtId.Flippable2(artId));
            if (name1 == null || name2 == null) return null;

            return Build(name1, name2, TileArtId.FileEdge(artId), TileArtId.Variant(artId));
        }

        /// <summary>
        /// Like <see cref="Resolve"/> but verifies the file is present (via <paramref name="exists"/>), and when the
        /// stored variant suffix is missing it tries the blend's other variants — mirroring how the engine's
        /// <c>tilevariant.dat</c> cache picks an existing variant rather than dropping to a plain base tile. Returns
        /// null if no variant of the blend exists (the caller can then fall to <see cref="BaseFallback"/>).
        /// <paramref name="onMissing"/>, if set, is called with <c>(name1, name2, edge)</c> for a blend that had no
        /// existing variant — for measuring the remaining gap (the cases that may need intermediate-terrain routing).
        /// </summary>
        public string ResolveExisting(uint artId, Func<string, bool> exists, Action<string, string, int> onMissing = null)
        {
            if (exists == null || !TileArtId.IsTile(artId)) return Resolve(artId);

            int type = TileArtId.TileType(artId);
            string name1 = _table.Lookup(TileArtId.Num1(artId), type, TileArtId.Flippable1(artId));
            string name2 = _table.Lookup(TileArtId.Num2(artId), type, TileArtId.Flippable2(artId));
            if (name1 == null || name2 == null) return null;

            int edge = TileArtId.FileEdge(artId);

            // Stored variant first, then every other suffix (a..h) for the same blend.
            string canonical = Build(name1, name2, edge, TileArtId.Variant(artId));
            if (exists(canonical)) return canonical;
            for (int v = 0; v < 8; v++)
            {
                string p = Build(name1, name2, edge, v);
                if (exists(p)) return p;
            }

            onMissing?.Invoke(name1, name2, edge); // a blend with no existing variant — the remaining gap
            return null;
        }

        /// <summary>
        /// A base-tile substitute (<c>&lt;name1&gt;bse0&lt;variant&gt;</c>) for the rare
        /// blend tiles whose exact file isn't present. Keeps terrain hole-free.
        /// </summary>
        public string BaseFallback(uint artId)
        {
            if (!TileArtId.IsTile(artId)) return null;
            string name1 = _table.Lookup(TileArtId.Num1(artId), TileArtId.TileType(artId), TileArtId.Flippable1(artId));
            if (name1 == null) return null;

            int variant = TileArtId.Variant(artId);
            if (variant >= 8) variant -= 8;
            return Path($"{name1}bse0{(char)('a' + variant)}");
        }

        private string Build(string name1, string name2, int edge, int variant)
        {
            if (variant >= 8) variant -= 8;
            char v = (char)('a' + variant);

            if (edge == 15 || string.Equals(name1, name2, StringComparison.OrdinalIgnoreCase))
                return Path($"{name1}bse{EdgeChars[edge]}{v}");

            if (edge == 0)
                return Path($"{name2}bse{EdgeChars[0]}{v}");

            int order1 = _table.Order(name1);
            if (order1 < 0) return Path($"{name1}bse{EdgeChars[edge]}{v}");

            int order2 = _table.Order(name2);
            if (order2 < 0) return Path($"{name2}bse{EdgeChars[15 - edge]}{v}");

            return order1 < order2
                ? Path($"{name1}{name2}{EdgeChars[edge]}{v}")
                : Path($"{name2}{name1}{EdgeChars[15 - edge]}{v}");
        }

        private static string Path(string core) => "art/tile/" + core + ".art";
    }
}
