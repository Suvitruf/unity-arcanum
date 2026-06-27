using Arcanum.Formats.Text;
using Arcanum.Formats.Tiles;
using NUnit.Framework;

namespace Arcanum.Formats.Tests
{
    /// <summary>
    /// <see cref="TileArtPathResolver"/> path-building branches (self-blend, edge-0, ordered two-name blend) and
    /// the base-tile fallback, ported from <c>build_tile_file_name</c> (a_name.c). Edge→char map "06b489237ea5dc10".
    /// </summary>
    public sealed class TileArtPathResolverTests
    {
        // Art-type nibble (bits 28–31) is 0 for TILE, so it's omitted from the packed value.
        private static uint Pack(int num1, int num2, int tileType, int flip1, int flip2, int edge, int variant, int flags)
            => (uint)(((num1 & 63) << 22) | ((num2 & 63) << 16)
                      | ((edge & 0xF) << 12) | ((variant & 7) << 9)
                      | ((tileType & 1) << 8) | ((flip1 & 1) << 7) | ((flip2 & 1) << 6) | (flags & 0xF));

        // outdoor-flippable names: [0]=aaa (order 0), [1]=bbb (order 1)
        private static TileArtPathResolver Build() =>
            TileArtPathResolver.FromMes(MesReader.Read("{0}{aaa}{1}{bbb}"));

        [Test]
        public void Edge15UsesName1Base()
        {
            uint id = Pack(0, 1, 1, 1, 1, edge: 15, variant: 0, flags: 0);
            Assert.That(Build().Resolve(id), Is.EqualTo("art/tile/aaabse0a.art")); // EdgeChars[15]='0'
        }

        [Test]
        public void SameNameUsesName1Base()
        {
            uint id = Pack(0, 0, 1, 1, 1, edge: 7, variant: 0, flags: 0);
            Assert.That(Build().Resolve(id), Is.EqualTo("art/tile/aaabse3a.art")); // EdgeChars[7]='3'
        }

        [Test]
        public void EdgeZeroUsesName2Base()
        {
            uint id = Pack(0, 1, 1, 1, 1, edge: 0, variant: 1, flags: 0);
            Assert.That(Build().Resolve(id), Is.EqualTo("art/tile/bbbbse0b.art")); // name2, EdgeChars[0]='0', variant 1
        }

        [Test]
        public void OrderedTwoNameBlend()
        {
            uint id = Pack(0, 1, 1, 1, 1, edge: 3, variant: 0, flags: 0);
            // order(aaa)=0 < order(bbb)=1 → "aaa"+"bbb"+EdgeChars[3]('4')+'a'
            Assert.That(Build().Resolve(id), Is.EqualTo("art/tile/aaabbb4a.art"));
        }

        [Test]
        public void BaseFallbackUsesName1()
        {
            uint id = Pack(1, 0, 1, 1, 1, edge: 3, variant: 2, flags: 0);               // num1=1 → bbb
            Assert.That(Build().BaseFallback(id), Is.EqualTo("art/tile/bbbbse0c.art")); // variant 2 → 'c'
        }

        [Test]
        public void NonTileReturnsNull()
        {
            Assert.That(Build().Resolve(2u << 28), Is.Null);
        }
    }
}
