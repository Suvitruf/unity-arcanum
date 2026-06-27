using Arcanum.Formats.Text;
using Arcanum.Formats.Tiles;
using NUnit.Framework;

namespace Arcanum.Formats.Tests
{
    /// <summary>
    /// Covers the terrain text/resolver pipeline: <c>.mes</c> parsing, tile-ID bit
    /// decoding, and tile path resolution. The resolver case reproduces a mapping
    /// verified against the shipped game data (<c>0x00111c0 → art/tile/drtgrs6a.art</c>).
    /// </summary>
    public sealed class TerrainFormatTests
    {
        [Test]
        public void MesParsesKeysValuesAndIgnoresComments()
        {
            var mes = MesReader.Read("// a comment line\n{0}{drt}\n{100}{grs/n 0}\n{200}{abc}");
            Assert.That(mes.Count, Is.EqualTo(3));
            Assert.That(mes.Get(0), Is.EqualTo("drt"));
            Assert.That(mes.Get(100), Is.EqualTo("grs/n 0"));
            Assert.That(mes.CountInRange(0, 99), Is.EqualTo(1));
            Assert.That(mes.CountInRange(100, 199), Is.EqualTo(1));
        }

        [Test]
        public void DecodesTileArtIdBitfields()
        {
            const uint aid = 0x00111c0u;
            Assert.That(TileArtId.IsTile(aid), Is.True);
            Assert.That(TileArtId.Num1(aid), Is.EqualTo(0));
            Assert.That(TileArtId.Num2(aid), Is.EqualTo(1));
            Assert.That(TileArtId.TileType(aid), Is.EqualTo(1)); // outdoor
            Assert.That(TileArtId.Flippable1(aid), Is.EqualTo(1));
            Assert.That(TileArtId.Flippable2(aid), Is.EqualTo(1));
            Assert.That(TileArtId.Edge(aid), Is.EqualTo(1));
            Assert.That(TileArtId.Variant(aid), Is.EqualTo(0));
        }

        [Test]
        public void ResolvesBlendedTilePath()
        {
            // Outdoor-flippable names: index 0 = drt, index 1 = grs.
            var mes = MesReader.Read("{0}{drt}{1}{grs}");
            var resolver = TileArtPathResolver.FromMes(mes);
            Assert.That(resolver.Resolve(0x00111c0u), Is.EqualTo("art/tile/drtgrs6a.art"));
        }

        [Test]
        public void ResolvesBaseTileWhenNamesMatch()
        {
            var mes = MesReader.Read("{0}{drt}");
            var resolver = TileArtPathResolver.FromMes(mes);
            // num1 == num2 == 0 (outdoor, flippable) → both names "drt" → base-tile form.
            Assert.That(resolver.Resolve(0x1c0u), Is.EqualTo("art/tile/drtbse0a.art"));
        }
    }
}
