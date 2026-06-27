namespace Arcanum.Formats.Tiles
{
    /// <summary>
    /// Decodes the packed 32-bit tile <c>art_id</c> stored in sector terrain grids.
    /// Bit layout and helper tables are ported from alexbatalov/tig <c>art.c</c>
    /// (tile-id getters) and the game-side resolver in arcanum-ce <c>a_name.c</c>.
    /// </summary>
    public static class TileArtId
    {
        public const int ArtTypeTile = 0; // tig_art_type(aid) == TIG_ART_TYPE_TILE

        // 16-entry remap tables (tig art.c dword_5BE880 / dword_5BE8C0) used when a
        // tile is horizontally flipped (flags bit 0 set).
        private static readonly int[] EdgeNormal = { 0, 1, 8, 3, 4, 5, 6, 7, 8, 3, 10, 11, 6, 7, 14, 15 };
        private static readonly int[] EdgeFlipped = { 0, 1, 2, 9, 4, 5, 12, 13, 2, 9, 10, 11, 12, 13, 14, 15 };

        public static int Type(uint aid) => (int)(aid >> 28);
        public static bool IsTile(uint aid) => Type(aid) == ArtTypeTile;

        public static int Num1(uint aid) => (int)((aid >> 22) & 63);
        public static int Num2(uint aid) => (int)((aid >> 16) & 63);

        /// <summary>0 = indoor, 1 = outdoor.</summary>
        public static int TileType(uint aid) => (int)((aid >> 8) & 1);

        public static int Flippable1(uint aid) => (int)((aid >> 7) & 1);
        public static int Flippable2(uint aid) => (int)((aid >> 6) & 1);

        private static int Flags(uint aid) => (int)(aid & 0xF);

        /// <summary>The blend-edge index (0..15). Remapped when the tile is flipped.</summary>
        public static int Edge(uint aid)
        {
            int v = (int)((aid >> 12) & 0xF);
            if ((Flags(aid) & 1) != 0) v = EdgeFlipped[v];
            return v;
        }

        /// <summary>The variant index (0..15). Offset by 8 for flipped symmetric edges.</summary>
        public static int Variant(uint aid)
        {
            int v = (int)((aid >> 9) & 7);
            int edge = Edge(aid);
            if (EdgeFlipped[edge] == EdgeNormal[edge] && (Flags(aid) & 1) != 0) v += 8;
            return v;
        }
    }
}
