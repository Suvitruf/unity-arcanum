using Arcanum.Formats.Art;
using Arcanum.Formats.Text;
using NUnit.Framework;

namespace Arcanum.Formats.Tests
{
    /// <summary>
    /// Verifies the FACADE art-id bit layout (tig <c>art.c</c> <c>tig_art_facade_id_*</c>) and name
    /// resolution. Facade num is split: low 8 bits at bit 17, +256 if bit 27 is set; frame at bits 1–10;
    /// walkable at bit 0. Modelled on the real bates-house facades (num 110–113). See Docs/Facades.md.
    /// </summary>
    public sealed class FacadeArtResolverTests
    {
        private const uint TypeFacade = 11u << 28;

        private static uint MakeFacade(int num, int frame, bool walkable)
        {
            uint id = TypeFacade
                | ((num >= 256 ? 1u : 0u) << 27)
                | (((uint)num & 0xFF) << 17)
                | (((uint)frame & 0x3FF) << 1)
                | (walkable ? 1u : 0u);
            return id;
        }

        [Test]
        public void DecodesNumFrameWalkable()
        {
            uint id = MakeFacade(num: 110, frame: 5, walkable: false);
            Assert.That(ArtId.Type(id), Is.EqualTo(ArtId.TypeFacade));
            Assert.That(FacadeArtResolver.Num(id), Is.EqualTo(110));
            Assert.That(FacadeArtResolver.Frame(id), Is.EqualTo(5));
            Assert.That(FacadeArtResolver.Walkable(id), Is.False);

            uint walk = MakeFacade(num: 113, frame: 158, walkable: true);
            Assert.That(FacadeArtResolver.Frame(walk), Is.EqualTo(158));
            Assert.That(FacadeArtResolver.Walkable(walk), Is.True);
        }

        [Test]
        public void DecodesHighNum()
        {
            // num ≥ 256 sets bit 27 and stores num−256 in the low byte.
            uint id = MakeFacade(num: 300, frame: 0, walkable: false);
            Assert.That(FacadeArtResolver.Num(id), Is.EqualTo(300));
        }

        [Test]
        public void ResolvesName()
        {
            var mes = MesReader.Read("{110}{BatesHouse-01}\n{113}{BatesHouse-04}\n");
            var resolver = FacadeArtResolver.FromMes(mes);

            Assert.That(resolver.Resolve(MakeFacade(110, 5, false)), Is.EqualTo("art/facade/bateshouse-01.art"));
            Assert.That(resolver.Resolve(MakeFacade(113, 0, true)), Is.EqualTo("art/facade/bateshouse-04.art"));
            Assert.That(resolver.Resolve(MakeFacade(999, 0, false)), Is.Null); // unknown num → null
        }
    }
}
