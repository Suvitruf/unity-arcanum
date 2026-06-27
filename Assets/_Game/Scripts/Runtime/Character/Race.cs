namespace Arcanum.Runtime.Character
{
    /// <summary>
    /// The eight player-character races. The values match the engine <c>Race</c> enum order (stat.h) AND the
    /// critter art-id <b>"body" field (bits 24–26)</b>, so the race index doubles as the paper-doll body — a
    /// dwarf PC literally renders the dwarf body art. The further engine races (Dark Elf, Ogre, Orc) are
    /// NPC-only and have no paper-doll body, so they aren't selectable here.
    /// </summary>
    public enum Race
    {
        Human = 0,
        Dwarf = 1,
        Elf = 2,
        HalfElf = 3,
        Gnome = 4,
        Halfling = 5,
        HalfOrc = 6,
        HalfOgre = 7,
    }

    /// <summary>Maps a race to its critter-art <b>body type</b> — the value that goes in the art-id "body" field
    /// (bits 24–26). The 8 PC races share only 5 bodies (engine <c>name.c</c> <c>name_body_type_strs</c>:
    /// HUMAN=0 "HM", DWARF=1 "DF", HALFLING=2 "GH", HALF_OGRE=3 "HG", ELF=4 "EF"): the human-build races use the
    /// HUMAN body, the small races share the HALFLING body, half-ogre uses HALF_OGRE. Because this is many-to-few,
    /// the race itself can't be read back from the art-id and is stored on the critter instead.</summary>
    public static class RaceArt
    {
        public static int BodyType(Race race) => race switch
        {
            Race.Human => 0,    // HUMAN
            Race.Dwarf => 1,    // DWARF
            Race.Elf => 4,      // ELF
            Race.HalfElf => 0,  // HUMAN build
            Race.Gnome => 2,    // HALFLING (small) body
            Race.Halfling => 2, // HALFLING
            Race.HalfOrc => 0,  // HUMAN build
            Race.HalfOgre => 3, // HALF_OGRE
            _ => 0,
        };
    }
}
