using Arcanum.Formats.Text;

namespace Arcanum.Formats.Art
{
    /// <summary>Shared helpers for decoding packed art IDs (tig art.c bit layout).</summary>
    public static class ArtId
    {
        private const int TypeShift = 28;
        private const int NumShift = 19;
        private const int MaxNum = 512;

        public const int TypeTile = 0, TypeWall = 1, TypeCritter = 2, TypePortal = 3,
            TypeScenery = 4, TypeInterface = 5, TypeItem = 6, TypeContainer = 7,
            TypeMisc = 8, TypeLight = 9, TypeRoof = 10, TypeFacade = 11,
            TypeMonster = 12, TypeUniqueNpc = 13, TypeEyeCandy = 14;

        public static int Type(uint artId) => (int)(artId >> TypeShift);

        /// <summary>Generic art "number" for types that index a name table (scenery, etc.).</summary>
        public static int Num(uint artId) => (int)((artId >> NumShift) & (MaxNum - 1));

        // Item art-id disposition (ITEM_ID_DISPOSITION_SHIFT=12, 2 bits): which representation of the item.
        public const int ItemDispositionGround = 0, ItemDispositionInventory = 1, ItemDispositionPaperdoll = 2;
        private const int ItemDispositionShift = 12;

        /// <summary>For an ITEM art id, returns the same id with its disposition replaced (the engine derives
        /// the inventory icon from the ground art this way — same item, the compact cell-shaped frame).</summary>
        public static uint WithItemDisposition(uint artId, int disposition)
            => (artId & ~(0x3u << ItemDispositionShift)) | (((uint)disposition & 0x3u) << ItemDispositionShift);
    }

    /// <summary>
    /// Resolves a SCENERY art ID to its <c>art/scenery/*.art</c> path via
    /// <c>art/scenery/scenery.mes</c>. The mes key is <c>1000 * scenery_sub_type + num</c>
    /// (engine <c>name.c:938</c>) — scenery is sub-categorised (PROJECTILE=0, TREES, PLANTS,
    /// FURNITURE/BEDS, MACHINERY, …) and only sub-type 0 keys on <c>num</c> alone, which is why
    /// projectiles resolved but trees/furniture didn't until this was fixed.
    /// </summary>
    public sealed class SceneryArtResolver
    {
        private const int SceneryTypeShift = 6;   // SCENERY_ID_TYPE_SHIFT
        private const int SceneryMaxType = 32;    // SCENERY_ID_MAX_TYPE (mask 0x1F)

        private readonly MesFile _names; // key = 1000*sub_type + num, value = "<file>.art"

        public SceneryArtResolver(MesFile sceneryMes) => _names = sceneryMes;

        public static SceneryArtResolver FromMes(MesFile sceneryMes) => new SceneryArtResolver(sceneryMes);

        /// <summary>The scenery sub-type (trees / furniture / machinery / …) packed at bits 6–10.</summary>
        public static int SubType(uint artId) => (int)((artId >> SceneryTypeShift) & (SceneryMaxType - 1));

        /// <summary>Returns the virtual path (e.g. <c>art/scenery/cabinet_flip.art</c>), or null if unresolvable.</summary>
        public string Resolve(uint artId)
        {
            if (ArtId.Type(artId) != ArtId.TypeScenery) return null;
            int key = 1000 * SubType(artId) + ArtId.Num(artId); // name.c: 1000*type + num
            string name = _names.Get(key);
            if (string.IsNullOrEmpty(name)) return null;
            return ("art/scenery/" + name).ToLowerInvariant();
        }
    }

    /// <summary>
    /// Resolves an ITEM art ID to <c>art/item/*.art</c> — ports <c>a_name_item_aid_to_fname</c>.
    /// The item id packs type(0-3 bits), subtype(6-9), disposition(12-13), armor_coverage(14-16);
    /// the mes key is <c>num + 20*(subtype + 50*type)</c> (+armor-coverage adjust), and the disposition
    /// chooses the mes file: 0 ground, 1 inventory, 2 paperdoll, 3 schematic.
    /// </summary>
    public sealed class ItemArtResolver
    {
        private const int TypeArmor = 2;        // TIG_ART_ITEM_TYPE_ARMOR
        private const int CoverageTorso = 0;    // TIG_ART_ARMOR_COVERAGE_TORSO
        private readonly MesFile[] _byDisposition; // [ground, inventory, paperdoll, schematic]

        public ItemArtResolver(MesFile ground, MesFile inventory, MesFile paperdoll, MesFile schematic)
            => _byDisposition = new[] { ground, inventory, paperdoll, schematic };

        public string Resolve(uint artId)
        {
            if (ArtId.Type(artId) != ArtId.TypeItem) return null;
            int type = (int)(artId & 0xF);                 // ITEM_ID_TYPE_SHIFT 0
            int subtype = (int)((artId >> 6) & 0xF);        // ITEM_ID_SUBTYPE_SHIFT 6
            int disposition = (int)((artId >> 12) & 0x3);   // ITEM_ID_DISPOSITION_SHIFT 12

            // Items have their OWN num layout (engine tig_art_num_get, art.c:685: (id>>17)&0x7FF) — NOT the
            // generic ArtId.Num (>>19 & 511). Using that gave e.g. the Passport a 'watch_part' icon (num 20)
            // instead of 'i_passport' (num 80).
            int num = (int)((artId >> 17) & 0x7FF);
            int key = num + 20 * (subtype + 50 * type);
            if (type == TypeArmor)
            {
                int coverage = (int)((artId >> 14) & 0x7);  // ITEM_ID_ARMOR_COVERAGE_SHIFT 14
                if (coverage != CoverageTorso) key += 20 * (5 * coverage + 10);
            }

            string name = _byDisposition[disposition]?.Get(key);
            if (string.IsNullOrEmpty(name)) return null;
            return ("art/item/" + name).ToLowerInvariant();
        }
    }

    /// <summary>
    /// Resolves a CONTAINER art ID to <c>art/container/*.art</c> via <c>art/container/container.mes</c>.
    /// The container id packs a 5-bit type (tig <c>CONTAINER_ID_TYPE_SHIFT</c>=6) alongside the 9-bit
    /// num; the mes is keyed by <c>1000*type + num</c> (verified: woodbarrel/Chest_Fancy/Chest_old).
    /// </summary>
    public sealed class ContainerArtResolver
    {
        private readonly MesFile _names;

        public ContainerArtResolver(MesFile containerMes) => _names = containerMes;

        public static ContainerArtResolver FromMes(MesFile containerMes) => new ContainerArtResolver(containerMes);

        public string Resolve(uint artId)
        {
            if (ArtId.Type(artId) != ArtId.TypeContainer) return null;
            int type = (int)((artId >> 6) & 0x1F);          // CONTAINER_ID_TYPE_SHIFT=6, MAX_TYPE=32
            string name = _names.Get(1000 * type + ArtId.Num(artId));
            if (string.IsNullOrEmpty(name)) return null;
            return ("art/container/" + name).ToLowerInvariant();
        }
    }

    /// <summary>
    /// Resolves a ROOF art ID to <c>art/roof/*.art</c> via <c>art/roof/roofname.mes</c>
    /// (number → name). Ports <c>a_name_roof_aid_to_fname</c> / <c>build_roof_file_name</c>.
    /// </summary>
    public sealed class RoofArtResolver
    {
        private readonly MesFile _names;

        public RoofArtResolver(MesFile roofMes) => _names = roofMes;

        public static RoofArtResolver FromMes(MesFile roofMes) => new RoofArtResolver(roofMes);

        public string Resolve(uint artId)
        {
            if (ArtId.Type(artId) != ArtId.TypeRoof) return null;
            string name = _names.Get(ArtId.Num(artId));
            if (string.IsNullOrEmpty(name)) return null;
            return ("art/roof/" + name + ".art").ToLowerInvariant();
        }
    }
}
