using Arcanum.Formats.Tiles;
using NUnit.Framework;

namespace Arcanum.Formats.Tests
{
    /// <summary>Decoding of the packed 32-bit tile <c>art_id</c> — fields + the flip edge/variant remap tables.</summary>
    public sealed class TileArtIdTests
    {
        // Packs a tile art-id from its components. The art-type nibble (bits 28–31) is 0 for TILE, so it
        // contributes nothing to the packed value and is omitted.
        private static uint Pack(int num1, int num2, int tileType, int flip1, int flip2, int edge, int variant, int flags)
            => (uint)(((num1 & 63) << 22)
                      | ((num2 & 63) << 16)
                      | ((edge & 0xF) << 12)
                      | ((variant & 7) << 9)
                      | ((tileType & 1) << 8)
                      | ((flip1 & 1) << 7)
                      | ((flip2 & 1) << 6)
                      | (flags & 0xF));

        [Test]
        public void DecodesFields()
        {
            uint id = Pack(num1: 5, num2: 10, tileType: 1, flip1: 1, flip2: 0, edge: 2, variant: 3, flags: 0);
            Assert.That(TileArtId.IsTile(id), Is.True);
            Assert.That(TileArtId.Num1(id), Is.EqualTo(5));
            Assert.That(TileArtId.Num2(id), Is.EqualTo(10));
            Assert.That(TileArtId.TileType(id), Is.EqualTo(1));
            Assert.That(TileArtId.Flippable1(id), Is.EqualTo(1));
            Assert.That(TileArtId.Flippable2(id), Is.EqualTo(0));
            Assert.That(TileArtId.Edge(id), Is.EqualTo(2), "no flip → edge unchanged");
            Assert.That(TileArtId.Variant(id), Is.EqualTo(3));
        }

        [Test]
        public void FlipRemapsEdge()
        {
            // flags bit 0 set → edge 3 remaps to EdgeFlipped[3] == 9.
            uint id = Pack(0, 0, 1, 0, 0, edge: 3, variant: 0, flags: 1);
            Assert.That(TileArtId.Edge(id), Is.EqualTo(9));
        }

        [Test]
        public void FlipAddsEightToSymmetricVariant()
        {
            // edge 4 is symmetric (EdgeFlipped[4] == EdgeNormal[4] == 4); flipping bumps the variant by 8.
            uint id = Pack(0, 0, 1, 0, 0, edge: 4, variant: 2, flags: 1);
            Assert.That(TileArtId.Edge(id), Is.EqualTo(4));
            Assert.That(TileArtId.Variant(id), Is.EqualTo(10));
        }

        [Test]
        public void FileEdgeMapsFlipEdgesToCanonicalPartner()
        {
            // The four "flipped" edges ship no file of their own — they map to their mirror partner (which does),
            // and are flagged as mirrored so the renderer draws that partner horizontally flipped.
            (int flip, int canonical)[] pairs = { (2, 8), (9, 3), (12, 6), (13, 7) };
            foreach ((int flip, int canonical) in pairs)
            {
                uint id = Pack(0, 0, 1, 0, 0, edge: flip, variant: 0, flags: 0);
                Assert.That(TileArtId.Edge(id), Is.EqualTo(flip));
                Assert.That(TileArtId.FileEdge(id), Is.EqualTo(canonical), $"edge {flip} resolves to file edge {canonical}");
                Assert.That(TileArtId.IsMirrored(id), Is.True, $"edge {flip} is mirrored");
            }
        }

        [Test]
        public void FileEdgeIsIdentityForCanonicalEdges()
        {
            // Every edge that ships its own file is left unchanged and not mirrored — so the tiles that already
            // resolved keep resolving exactly as before.
            foreach (int e in new[] { 0, 1, 3, 4, 5, 6, 7, 8, 10, 11, 14, 15 })
            {
                uint id = Pack(0, 0, 1, 0, 0, edge: e, variant: 0, flags: 0);
                Assert.That(TileArtId.FileEdge(id), Is.EqualTo(e), $"edge {e} unchanged");
                Assert.That(TileArtId.IsMirrored(id), Is.False, $"edge {e} not mirrored");
            }
        }

        [Test]
        public void FlippedAsymmetricEdgeResolvesToMirroredCanonicalFile()
        {
            // The realistic path a sector stores: raw edge 8 with the flip flag → display edge 2 (EdgeFlipped[8]),
            // which maps back to file edge 8 drawn mirrored.
            uint id = Pack(0, 0, 1, 0, 0, edge: 8, variant: 0, flags: 1);
            Assert.That(TileArtId.Edge(id), Is.EqualTo(2), "raw 8 flipped → display edge 2");
            Assert.That(TileArtId.FileEdge(id), Is.EqualTo(8), "file edge is the canonical partner");
            Assert.That(TileArtId.IsMirrored(id), Is.True);
        }

        [Test]
        public void NonTileIsRejected()
        {
            uint wall = 2u << 28; // art type 2 != TILE
            Assert.That(TileArtId.IsTile(wall), Is.False);
        }
    }
}
