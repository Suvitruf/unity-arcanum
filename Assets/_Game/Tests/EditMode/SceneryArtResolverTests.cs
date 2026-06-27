using Arcanum.Formats.Art;
using Arcanum.Formats.Text;
using NUnit.Framework;

namespace Arcanum.Formats.Tests
{
    /// <summary>
    /// Key formulas for the name-table resolvers in <c>SceneryArtResolver.cs</c>: SCENERY/CONTAINER use
    /// <c>1000*subtype + num</c>; ROOF uses <c>num</c>; ITEM uses its own num (>>17) + <c>num + 20*(subtype + 50*type)</c>.
    /// </summary>
    public sealed class SceneryArtResolverTests
    {
        [Test]
        public void SceneryKeyIsThousandTimesSubtypePlusNum()
        {
            // type 4 = SCENERY; subtype at bits 6–10; num at >>19. key = 1000*subtype + num.
            uint id = (4u << 28) | (2u << 19) | (4u << 6); // subtype 4, num 2 → key 4002
            var r = SceneryArtResolver.FromMes(MesReader.Read("{4002}{oak_tree.art}"));
            Assert.That(r.Resolve(id), Is.EqualTo("art/scenery/oak_tree.art"));
            Assert.That(r.Resolve(2u << 28), Is.Null); // non-scenery
        }

        [Test]
        public void ContainerKeyIsThousandTimesTypePlusNum()
        {
            // type 7 = CONTAINER; container type at bits 6–10; num at >>19. key = 1000*type + num.
            uint id = (7u << 28) | (5u << 19) | (2u << 6); // type 2, num 5 → key 2005
            var r = ContainerArtResolver.FromMes(MesReader.Read("{2005}{chest.art}"));
            Assert.That(r.Resolve(id), Is.EqualTo("art/container/chest.art"));
        }

        [Test]
        public void RoofKeyIsNumAndAppendsArt()
        {
            // type 10 = ROOF; num at >>19. The mes value is a base name; the resolver appends ".art".
            uint id = (10u << 28) | (3u << 19);
            var r = RoofArtResolver.FromMes(MesReader.Read("{3}{tile_roof}"));
            Assert.That(r.Resolve(id), Is.EqualTo("art/roof/tile_roof.art"));
        }

        [Test]
        public void ItemUsesOwnNumAndDispositionMes()
        {
            // type 6 = ITEM; item num at >>17; itemType bits 0–3, subtype bits 6–9, disposition bits 12–13.
            // key = num + 20*(subtype + 50*itemType). itemType 0, subtype 0, num 80 → key 80; disposition 0 = ground.
            uint id = (6u << 28) | (80u << 17);
            var ground = MesReader.Read("{80}{i_passport.art}");
            var r = new ItemArtResolver(ground, null, null, null);
            Assert.That(r.Resolve(id), Is.EqualTo("art/item/i_passport.art"));
        }
    }
}
