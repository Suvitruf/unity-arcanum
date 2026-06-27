using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Arcanum.Formats.Objects
{
    /// <summary>Arcanum object types (the <c>OBJ_TYPE_*</c> enum). Validated against real .pro files.</summary>
    public enum ObjectType
    {
        Wall = 0,
        Portal = 1,
        Container = 2,
        Scenery = 3,
        Projectile = 4,
        Weapon = 5,
        Ammo = 6,
        Armor = 7,
        Gold = 8,
        Food = 9,
        Scroll = 10,
        Key = 11,
        KeyRing = 12,
        Written = 13,
        Generic = 14,
        Pc = 15,
        Npc = 16,
        Trap = 17,
    }

    /// <summary>What we extract from a prototype for rendering: its number, type, and base art ID.</summary>
    public sealed class ObjectProtoInfo
    {
        public int ProtoNumber { get; }
        public ObjectType Type { get; }

        /// <summary>The <c>OBJ_F_CURRENT_AID</c> value — the sprite art ID for this prototype.</summary>
        public uint CurrentArtId { get; }

        /// <summary>The <c>OBJ_F_DESCRIPTION</c> id (into description.mes), or null if unset.</summary>
        public int? Description { get; }

        /// <summary>The prototype's <c>OBJ_F_FLAGS</c> (<c>OF_*</c> bitset), or null if unset.</summary>
        public int? Flags { get; }

        /// <summary>The prototype's dialog number (<c>OBJ_F_SCRIPTS</c>); 0 = none. NPC dialog usually lives here.</summary>
        public int DialogNum { get; }

        /// <summary>The prototype's <c>SAP_USE</c> script number (<c>OBJ_F_SCRIPTS</c>); 0 = none. Door
        /// teleporters often carry their script on the prototype.</summary>
        public int UseScriptNum { get; }

        /// <summary>The prototype's <c>SAP_EXAMINE</c> script number (<c>OBJ_F_SCRIPTS</c>); 0 = none. Signs and
        /// examinable scenery usually carry their look-at script on the prototype.</summary>
        public int ExamineScriptNum { get; }

        /// <summary>The prototype's heartbeat / first-heartbeat script numbers (<c>OBJ_F_SCRIPTS</c>); 0 = none.</summary>
        public int HeartbeatScriptNum { get; }

        public int FirstHeartbeatScriptNum { get; }

        /// <summary>The prototype's <c>OBJ_F_ITEM_INV_AID</c> — the compact inventory-icon art id, or null if unset.</summary>
        public uint? InvAid { get; }

        /// <summary>The prototype's item weight / worth (<c>OBJ_F_ITEM_WEIGHT</c> / <c>OBJ_F_ITEM_WORTH</c>); 0 if unset.</summary>
        public int Weight { get; }

        public int Worth { get; }

        /// <summary>Parsed <c>OBJ_F_WEAPON_*</c> combat stats; null unless this is a weapon prototype.</summary>
        public WeaponFields Weapon { get; }

        /// <summary>The light this prototype emits (<c>OBJ_F_LIGHT_AID</c> / <c>OBJ_F_LIGHT_COLOR</c>); null
        /// unless it's a light source (lamps, torches, braziers).</summary>
        public uint? LightAid { get; }

        public int? LightColor { get; }

        /// <summary><c>OBJ_F_SCENERY_FLAGS</c> (<c>OSCF_*</c>); null unless a scenery prototype that sets it.</summary>
        public int? SceneryFlags { get; }

        /// <summary><c>OBJ_F_NPC_REACTION_BASE</c> — the prototype's authored starting reaction (default 50);
        /// null if unset on the prototype.</summary>
        public int? ReactionBase { get; internal set; }

        /// <summary><c>OBJ_F_CRITTER_STAT_BASE_IDX</c> — the prototype's base stats keyed by <c>STAT_*</c>
        /// (STR..CHA = 0..7, LEVEL = 17); null for non-critters. This is where most NPCs get their real stats.</summary>
        public int[] StatBase { get; internal set; }

        /// <summary><c>OBJ_F_ARMOR_AC_ADJ</c> — the AC bonus an armour prototype grants; null otherwise.</summary>
        public int? ArmorAc { get; internal set; }

        /// <summary><c>OBJ_F_CRITTER_BASIC_SKILL_IDX</c> / <c>_TECH_SKILL_IDX</c> — the prototype's skill ranks.</summary>
        public int[] BasicSkills { get; internal set; }

        public int[] TechSkills { get; internal set; }

        /// <summary><c>OBJ_F_RESISTANCE_IDX</c> base resistances; <c>OBJ_F_ARMOR_RESISTANCE_ADJ_IDX</c> armour bonus.</summary>
        public int[] Resistances { get; internal set; }

        public int[] ArmorResist { get; internal set; }

        /// <summary>Base max HP (<c>OBJ_F_HP_PTS</c>) — combined with an instance's <c>HpDamage</c> to tell if a
        /// critter is dead; null if unset on the prototype.</summary>
        public int? HpPoints { get; internal set; }

        /// <summary><c>OBJ_F_ITEM_FLAGS</c> (<c>OIF_*</c>) / <c>OBJ_F_GOLD_QUANTITY</c>; null if not an item / gold.</summary>
        public int? ItemFlags { get; internal set; }

        public int? GoldQuantity { get; internal set; }

        /// <summary><c>OBJ_F_CRITTER_FLAGS</c> / <c>FLAGS2</c> (dead, sleeping, animal, …) and the portrait id; null
        /// for non-critters.</summary>
        public int? CritterFlags { get; internal set; }

        public int? CritterFlags2 { get; internal set; }
        public int? Portrait { get; internal set; }

        /// <summary><c>OBJ_F_NPC_FACTION</c> — the prototype's faction; null for non-NPCs.</summary>
        public int? Faction { get; internal set; }

        /// <summary>Lock/key for a portal or container prototype, and a key prototype's id; null if unset.</summary>
        public int? PortalFlags { get; internal set; }

        public int? ContainerFlags { get; internal set; }
        public int? LockDifficulty { get; internal set; }
        public int? KeyId { get; internal set; }

        /// <summary>Written-item (book/sign) subtype + text line range (<c>OBJ_F_WRITTEN_*</c>); null otherwise.</summary>
        public int? WrittenSubtype { get; internal set; }

        public int? TextStartLine { get; internal set; }
        public int? TextEndLine { get; internal set; }

        public ObjectProtoInfo(int protoNumber, ObjectType type, uint currentArtId, int? description = null,
                               int? flags = null, int dialogNum = 0, uint? invAid = null, int weight = 0, int worth = 0,
                               WeaponFields weapon = null, uint? lightAid = null, int? lightColor = null, int? sceneryFlags = null,
                               int useScriptNum = 0, int examineScriptNum = 0, int heartbeatScriptNum = 0, int firstHeartbeatScriptNum = 0)
        {
            HeartbeatScriptNum = heartbeatScriptNum;
            FirstHeartbeatScriptNum = firstHeartbeatScriptNum;
            SceneryFlags = sceneryFlags;
            ProtoNumber = protoNumber;
            Type = type;
            CurrentArtId = currentArtId;
            Description = description;
            Flags = flags;
            DialogNum = dialogNum;
            UseScriptNum = useScriptNum;
            ExamineScriptNum = examineScriptNum;
            InvAid = invAid;
            Weight = weight;
            Worth = worth;
            Weapon = weapon;
            LightAid = lightAid;
            LightColor = lightColor;
        }
    }

    /// <summary>
    /// Reads the leading fields of an Arcanum <c>.pro</c> prototype file. The full
    /// object field engine is large; for rendering we only need the prototype's
    /// number, type, and base art ID (<c>OBJ_F_CURRENT_AID</c>, the very first
    /// serialized field). Layout validated against the shipped <c>data/proto/*.pro</c>:
    /// <code>
    /// version(4)=119  prototype_oid(24)  object_oid(24)  type(4)  fieldBitmap(4*N)  CURRENT_AID(4) ...
    /// </code>
    /// where the proto number is <c>object_oid.d.a</c> (offset 0x24) and N is the
    /// per-type change-bitmap dword count.
    /// </summary>
    public static class ObjectProtoReader
    {
        private const int ObjectFileVersion = 119;
        private const int ObjectOidNumberOffset = 0x24; // object_oid.d.a (OID_TYPE_A)

        public static bool TryReadInfo(byte[] proto, out ObjectProtoInfo info)
        {
            info = null;
            if (proto == null || proto.Length < 0x40) return false;
            if (ReadInt(proto, 0) != ObjectFileVersion) return false;

            // A .pro is an object record without the instance-only num_fields; the field engine
            // walks it for every type (gives art id + description id), where the old fixed-offset
            // reader only handled a few static types.
            try
            {
                int off = 0;
                var p = ObjectInstanceReader.Read(proto, ref off, hasNumFields: false);
                int protoNumber = ReadInt(proto, ObjectOidNumberOffset);
                info =
                    new ObjectProtoInfo(protoNumber, p.Type, p.CurrentArtId ?? 0u, p.Description, p.Flags, p.DialogNum, p.InvAid, p.Weight ?? 0, p.Worth ?? 0, p.Weapon, p.LightAid, p.LightColor,
                        p.SceneryFlags, p.UseScriptNum, p.ExamineScriptNum, p.HeartbeatScriptNum, p.FirstHeartbeatScriptNum)
                    {
                        ReactionBase = p.ReactionBase,
                        StatBase = p.StatBase,
                        ArmorAc = p.ArmorAc,
                        BasicSkills = p.BasicSkills,
                        TechSkills = p.TechSkills,
                        Resistances = p.Resistances,
                        ArmorResist = p.ArmorResist,
                        HpPoints = p.HpPoints,
                        ItemFlags = p.ItemFlags,
                        GoldQuantity = p.GoldQuantity,
                        CritterFlags = p.CritterFlags,
                        CritterFlags2 = p.CritterFlags2,
                        Portrait = p.Portrait,
                        Faction = p.Faction,
                        PortalFlags = p.PortalFlags,
                        ContainerFlags = p.ContainerFlags,
                        LockDifficulty = p.LockDifficulty,
                        KeyId = p.KeyId,
                        WrittenSubtype = p.WrittenSubtype,
                        TextStartLine = p.TextStartLine,
                        TextEndLine = p.TextEndLine,
                    };
                return true;
            }
            catch { return false; }
        }

        private static int ReadInt(byte[] b, int o) =>
            b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24);
    }

    /// <summary>
    /// Loads prototypes by number from the loose <c>data/proto/NNNNNN - Name.pro</c>
    /// files in an Arcanum install, caching parsed results. Used to resolve the art
    /// of placed scenery/walls/portals whose instances inherit it from their proto.
    /// </summary>
    public sealed class ProtoLibrary
    {
        private readonly string _protoDir;
        private readonly Dictionary<int, string> _pathByNumber = new Dictionary<int, string>();
        private readonly Dictionary<int, ObjectProtoInfo> _cache = new Dictionary<int, ObjectProtoInfo>();

        public ProtoLibrary(string protoDir)
        {
            _protoDir = protoDir;
            if (Directory.Exists(protoDir))
            {
                foreach (string path in Directory.EnumerateFiles(protoDir, "*.pro"))
                {
                    string name = Path.GetFileNameWithoutExtension(path);
                    int dash = name.IndexOf(" - ", System.StringComparison.Ordinal);
                    string numPart = dash >= 0 ? name.Substring(0, dash) : name;
                    if (int.TryParse(numPart, out int number)) _pathByNumber[number] = path;
                }
            }
        }

        public int Count => _pathByNumber.Count;

        /// <summary>All known prototype numbers (unparsed). Use with <see cref="Get"/> to scan for protos of a
        /// given type — e.g. to pick starting items.</summary>
        public IEnumerable<int> Numbers => _pathByNumber.Keys;

        public ObjectProtoInfo Get(int protoNumber)
        {
            if (_cache.TryGetValue(protoNumber, out var cached)) return cached;
            ObjectProtoInfo info = null;
            if (_pathByNumber.TryGetValue(protoNumber, out string path))
                ObjectProtoReader.TryReadInfo(File.ReadAllBytes(path), out info);
            _cache[protoNumber] = info;
            return info;
        }
    }
}
