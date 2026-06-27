using NUnit.Framework;
using Arcanum.Formats.Art;

namespace Arcanum.Formats.Tests
{
    /// <summary>Critter art-path resolution, focused on the engine body remaps (name.c): a female elf has no
    /// elf-female art and falls back to the human female body; males/other races keep their own body.</summary>
    public class CritterArtResolverTests
    {
        // type=critter, gender bit 27, body (race) bits 24-26, armor bits 20-23 (2 = LA), weapon 0.
        private static uint Critter(int gender, int body, int armor = 2)
            => ((uint)ArtId.TypeCritter << 28) | ((uint)gender << 27) | ((uint)body << 24) | ((uint)armor << 20);

        [Test]
        public void FemaleElf_FallsBackToHumanFemaleBody()
        {
            string path = CritterArtResolver.Create().Resolve(Critter(gender: 0, body: 4)); // female (0), elf body (4)
            StringAssert.Contains("art/critter/hmf/", path); // remapped to human female
            StringAssert.DoesNotContain("/ef", path);
        }

        [Test]
        public void MaleElf_KeepsElfBody()
        {
            string path = CritterArtResolver.Create().Resolve(Critter(gender: 1, body: 4)); // male (1), elf body (4)
            StringAssert.Contains("art/critter/efm/", path);
        }

        [Test]
        public void FemaleDwarf_UsesDwarfMaleBody()
        {
            // No female art for non-human bodies, so a female dwarf keeps the dwarf body but uses its male art.
            string path = CritterArtResolver.Create().Resolve(Critter(gender: 0, body: 1)); // female (0), dwarf body (1)
            StringAssert.Contains("art/critter/dfm/", path);
        }

        [Test]
        public void FemaleHalfOgre_UsesHalfOgreMaleBody()
        {
            string path = CritterArtResolver.Create().Resolve(Critter(gender: 0, body: 3)); // female (0), half-ogre body (3)
            StringAssert.Contains("art/critter/hgm/", path);
        }
    }
}
