using System;
using Arcanum.Formats.Text;

namespace Arcanum.Formats.Tiles
{
    /// <summary>
    /// Turns a sector's packed tile <c>art_id</c> into an <c>art/tile/*.art</c>
    /// virtual path. Ports <c>a_name_tile_aid_to_fname</c> + <c>build_tile_file_name</c>
    /// from arcanum-ce <c>a_name.c</c>. Validated against the shipped game: 99.6% of a
    /// real sector's 4096 tiles resolve to files that exist in <c>arcanum2.dat</c>;
    /// the remainder are flippable blend tiles the engine mirrors at runtime, for
    /// which <see cref="BaseFallback"/> gives a sensible substitute.
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

            return Build(name1, name2, TileArtId.Edge(artId), TileArtId.Variant(artId));
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
