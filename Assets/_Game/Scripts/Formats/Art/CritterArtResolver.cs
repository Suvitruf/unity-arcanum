using Arcanum.Formats.Text;

namespace Arcanum.Formats.Art
{
    /// <summary>
    /// Resolves a CRITTER art ID to its composite art path. Critters are a paper-doll:
    /// the art id encodes gender, body type, armor, shield, weapon, anim, rotation and frame,
    /// which assemble into a filename. Ports the CRITTER branch of arcanum-ce
    /// <c>name.c name_resolve_path</c>:
    /// <code>
    /// art/critter/&lt;BODY&gt;&lt;G&gt;/&lt;BODY&gt;&lt;G&gt;&lt;ARMOR&gt;&lt;S&gt;&lt;W&gt;&lt;animchar&gt;.art
    /// </code>
    /// e.g. a human male villager, no shield, unarmed, standing → <c>art/critter/HMM/HMMV1XAa.art</c>.
    /// The resolved <c>.art</c> file is multi-rotation (8 facings) with the animation's frames.
    /// </summary>
    public sealed class CritterArtResolver
    {
        // Field → filename codes (name.c). Index by the art-id field value. Internal so the
        // monster/unique-NPC resolvers (which share the armour/shield/weapon naming) can reuse them.
        private static readonly string[] BodyStrs = { "HM", "DF", "GH", "HG", "EF" };
        internal static readonly string[] ArmorStrs = { "UW", "V1", "LA", "CM", "PM", "RB", "PC", "BN", "CD" };
        internal static readonly char[] WeaponCodes = { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'X', 'Y', 'N', 'Z' };
        private static readonly char[] GenderCodes = { 'F', 'M', 'X' };
        internal static readonly char[] ShieldCodes = { 'X', 'S' };

        private const int ArmorPlate = 4, ArmorPlateClassic = 6;
        private const int BodyHuman = 0, BodyDwarf = 1, BodyHalfling = 2, BodyElf = 4; // BodyStrs indices
        private const int GenderFemale = 0, GenderMale = 1; // GenderCodes indices ('F','M')
        internal const int WeaponSword = 3, WeaponTwoHandedSword = 7;
        private const int AnimStand = 0, AnimExplode = 25;

        /// <summary>Weapon filename code, with the two-handed-sword-with-shield → sword special case (name.c).</summary>
        internal static char WeaponCode(int weapon, int shield)
            => (weapon == WeaponTwoHandedSword && shield == 1) ? WeaponCodes[WeaponSword] : WeaponCodes[weapon];

        public static CritterArtResolver Create() => new CritterArtResolver();

        /// <summary>Path for a critter art id. <paramref name="forceStand"/> overrides the anim to the idle/STAND pose.</summary>
        public string Resolve(uint artId, bool forceStand = false)
        {
            if (ArtId.Type(artId) != ArtId.TypeCritter) return null;

            int armor = (int)((artId >> 20) & 0xF);
            int bodyType = (int)((artId >> 24) & 7);
            int gender = (int)((artId >> 27) & 1);
            int shield = (int)((artId >> 19) & 1);
            int weapon = (int)(artId & 0xF);
            int anim = forceStand ? AnimStand : (int)((artId >> 6) & 0x1F);

            if (bodyType >= BodyStrs.Length || armor >= ArmorStrs.Length || weapon >= WeaponCodes.Length)
                return null;

            // The shipped art has female paper-doll bodies only for the HUMAN body. The engine maps a female ELF
            // to the human female body (name.c:671). For the other non-human races (no female art either) we keep
            // the race's body and use its MALE art, so race/size still read — a female dwarf stays a dwarf rather
            // than becoming a human (gender shows in the portrait). Then: PLATE is genderless and elf/halfling
            // lack plate art, so they fall back to human/dwarf.
            if (gender == GenderFemale)
            {
                if (bodyType == BodyElf) bodyType = BodyHuman;       // elf female → human female (engine)
                else if (bodyType != BodyHuman) gender = GenderMale; // dwarf/halfling/half-ogre female → race's male body
            }
            if (armor == ArmorPlate || armor == ArmorPlateClassic)
            {
                if (bodyType == BodyElf) bodyType = BodyHuman;
                else if (bodyType == BodyHalfling) bodyType = BodyDwarf;
            }

            // Plate armour uses a genderless body; EXPLODE uses the "XX" genderless gibs set.
            char genderCode;
            if (armor == ArmorPlate || armor == ArmorPlateClassic) genderCode = GenderCodes[2];
            else genderCode = GenderCodes[gender];

            string bodyStr = BodyStrs[bodyType];
            string armorStr;
            if (anim == AnimExplode) { armorStr = "XX"; genderCode = GenderCodes[2]; }
            else armorStr = ArmorStrs[armor];

            char shieldCode = ShieldCodes[shield];
            char weaponCode = (weapon == WeaponTwoHandedSword && shield == 1)
                ? WeaponCodes[WeaponSword]
                : WeaponCodes[weapon];

            char animChar = (char)('a' + anim);
            return $"art/critter/{bodyStr}{genderCode}/{bodyStr}{genderCode}{armorStr}{shieldCode}{weaponCode}{animChar}.art"
                .ToLowerInvariant();
        }

        /// <summary>The facing rotation (0–7) baked into a critter/monster/unique art id.</summary>
        public static int RotationOf(uint artId) => (int)((artId >> 11) & 7);

        /// <summary>The palette/recolour index (bits 4–5, engine <c>ART_ID_PALETTE_SHIFT</c>) — selects which of
        /// the art's palettes to use (e.g. a brown vs the default robe). 0 = the art's primary palette.</summary>
        public static int PaletteOf(uint artId) => (int)((artId >> 4) & 3);

        /// <summary>Returns <paramref name="artId"/> with the animation (bits 6–10) and rotation (bits 11–13) replaced.</summary>
        public static uint WithAnimRotation(uint artId, int anim, int rotation)
        {
            artId = (artId & ~(0x1Fu << 6)) | ((uint)(anim & 0x1F) << 6);
            artId = (artId & ~(0x7u << 11)) | ((uint)(rotation & 0x7) << 11);
            return artId;
        }
    }

    /// <summary>
    /// Resolves a MONSTER art id (single-piece animated, not paper-doll) to
    /// <c>art/monster/&lt;specie&gt;/&lt;specie&gt;&lt;armor&gt;&lt;shield&gt;&lt;weapon&gt;&lt;anim&gt;.art</c>
    /// via <c>art/monster/monster.mes</c>. Ports the MONSTER branch of <c>name.c</c>; reuses the critter
    /// armour/shield/weapon codes (monster armour is 3 bits, shift 20, to avoid the specie field at 23).
    /// </summary>
    public sealed class MonsterArtResolver
    {
        private readonly MesFile _names;

        public MonsterArtResolver(MesFile monsterMes) => _names = monsterMes;
        public static MonsterArtResolver FromMes(MesFile monsterMes) => new MonsterArtResolver(monsterMes);

        public string Resolve(uint artId, bool forceStand = true)
        {
            if (ArtId.Type(artId) != ArtId.TypeMonster) return null;
            string name = _names?.Get((int)((artId >> 23) & 0x1F)); // MONSTER_ID_SPECIE_SHIFT 23
            if (string.IsNullOrEmpty(name)) return null;
            int armor = (int)((artId >> 20) & 0x7);                  // MONSTER_ID_ARMOR_SHIFT 20 (3 bits)
            int shield = (int)((artId >> 19) & 1);
            int weapon = (int)(artId & 0xF);
            if (armor >= CritterArtResolver.ArmorStrs.Length || weapon >= CritterArtResolver.WeaponCodes.Length) return null;
            // Idle 'a' by default; for combat anims honour the anim bits (6–10) so walk/attack animate.
            char animChar = forceStand ? 'a' : (char)('a' + ((artId >> 6) & 0x1F));
            return $"art/monster/{name}/{name}{CritterArtResolver.ArmorStrs[armor]}{CritterArtResolver.ShieldCodes[shield]}{CritterArtResolver.WeaponCode(weapon, shield)}{animChar}.art"
                .ToLowerInvariant();
        }
    }

    /// <summary>
    /// Resolves a UNIQUE_NPC art id to <c>art/unique_npc/&lt;name&gt;/&lt;name&gt;&lt;shield&gt;&lt;weapon&gt;&lt;anim&gt;.art</c>
    /// via <c>art/unique_npc/unique_npc.mes</c> (num → name). Ports the UNIQUE_NPC branch of <c>name.c</c>
    /// (no armour code). NPCs whose num isn't in the mes fall through unresolved.
    /// </summary>
    public sealed class UniqueNpcArtResolver
    {
        private readonly MesFile _names;

        public UniqueNpcArtResolver(MesFile uniqueMes) => _names = uniqueMes;
        public static UniqueNpcArtResolver FromMes(MesFile uniqueMes) => new UniqueNpcArtResolver(uniqueMes);

        public string Resolve(uint artId, bool forceStand = true)
        {
            if (ArtId.Type(artId) != ArtId.TypeUniqueNpc) return null;
            // Unique-NPC num has its OWN layout (tig art.c: UNIQUE_NPC_ID_NUM_SHIFT=20, MAX_NUM=256), NOT the
            // generic ArtId.Num (shift 19) — using that resolved e.g. Victoria/Clarisse to the 'PH1uw' placeholder.
            string name = _names?.Get((int)((artId >> 20) & 0xFF));
            if (string.IsNullOrEmpty(name)) return null;
            int shield = (int)((artId >> 19) & 1);
            int weapon = (int)(artId & 0xF);
            if (weapon >= CritterArtResolver.WeaponCodes.Length) return null;
            char animChar = forceStand ? 'a' : (char)('a' + ((artId >> 6) & 0x1F));
            return $"art/unique_npc/{name}/{name}{CritterArtResolver.ShieldCodes[shield]}{CritterArtResolver.WeaponCode(weapon, shield)}{animChar}.art"
                .ToLowerInvariant();
        }
    }
}
