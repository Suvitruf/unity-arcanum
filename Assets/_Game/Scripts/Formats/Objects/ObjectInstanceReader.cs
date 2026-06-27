using System.Collections.Generic;

namespace Arcanum.Formats.Objects
{
    /// <summary>A weapon's combat stats, parsed from the <c>OBJ_F_WEAPON_*</c> fields (per damage type for
    /// the damage ranges). Tech complexity + range + ammo decide which skill governs the attack.</summary>
    public sealed class WeaponFields
    {
        public const int DamageTypeCount = 5;
        public int Flags;
        public int BonusToHit;
        public int SpeedFactor;
        public int Range = 1;
        public int MinStrength;
        public int AmmoType = 10000;
        public int MagicTechComplexity; // from the item group; −value = tech
        public readonly int[] DamageMin = new int[DamageTypeCount];
        public readonly int[] DamageMax = new int[DamageTypeCount];
    }

    /// <summary>One placed object instance, decoded from a sector object list or a <c>.mob</c>.</summary>
    public sealed class ObjectInstance
    {
        public ObjectType Type { get; }

        /// <summary>Prototype number this instance derives from (<c>prototype_oid.d.a</c>).</summary>
        public int PrototypeNumber { get; }

        /// <summary>Raw packed <c>OBJ_F_LOCATION</c>, or null if not overridden on the instance.</summary>
        public long? Location { get; }

        /// <summary>Tile X within the sector (0..63), if a location was present.</summary>
        public int TileX => Location.HasValue ? (int)(Location.Value & 0x3F) : -1;

        public int TileY => Location.HasValue ? (int)((Location.Value >> 32) & 0x3F) : -1;

        /// <summary>Instance's own art id (<c>OBJ_F_CURRENT_AID</c>), or null if inherited from the prototype.</summary>
        public uint? CurrentArtId { get; }

        /// <summary>Sub-tile draw offset in screen pixels (<c>OBJ_F_OFFSET_X/Y</c>).</summary>
        public int OffsetX { get; }

        public int OffsetY { get; }

        /// <summary>Display-name id (<c>OBJ_F_DESCRIPTION</c>) into description.mes, or null if inherited from the prototype.</summary>
        public int? Description { get; }

        /// <summary>Object flags (<c>OBJ_F_FLAGS</c>, the <c>OF_*</c> bitset), or null if inherited from the prototype.</summary>
        public int? Flags { get; }

        /// <summary>Dialog number from the NPC's scripts (<c>SAP_DIALOG</c>, else <c>SAP_DIALOG_OVERRIDE</c>); 0 = none.</summary>
        public int DialogNum { get; }

        /// <summary>The <c>SAP_USE</c> script number from <c>OBJ_F_SCRIPTS</c> — the script that runs when this
        /// object is used/clicked (door teleporters, levers); 0 = none.</summary>
        public int UseScriptNum { get; internal set; }

        /// <summary>The <c>SAP_EXAMINE</c> script number — runs when the object is looked at (signs, examinable
        /// scenery; usually floats a line of text); 0 = none.</summary>
        public int ExamineScriptNum { get; internal set; }

        /// <summary>The <c>SAP_HEARTBEAT</c> / <c>SAP_FIRST_HEARTBEAT</c> script numbers — periodic ticks (AI,
        /// and self-gating NPCs that toggle off/kill themselves unless a quest flag is set); 0 = none.</summary>
        public int HeartbeatScriptNum { get; internal set; }

        public int FirstHeartbeatScriptNum { get; internal set; }

        /// <summary><c>OBJ_F_HP_DAMAGE</c> — damage taken. A critter is dead (engine <c>critter_is_dead</c>:
        /// <c>hp_current ≤ 0</c>) when this reaches max HP; crash-site bodies are placed with the 32000 kill
        /// sentinel. 0 = none.</summary>
        public int HpDamage { get; internal set; }

        /// <summary>For NPCs, <c>OBJ_F_NPC_REACTION_BASE</c>: the authored starting reaction toward the PC
        /// (engine default 50 = neutral); null if inherited from the prototype.</summary>
        public int? ReactionBase { get; internal set; }

        /// <summary>For items, <c>OBJ_F_ITEM_INV_LOCATION</c>: a worn slot (1000–1008) or a loose grid cell; -1 = unset.</summary>
        public int InvLocation { get; }

        /// <summary>For items, <c>OBJ_F_ITEM_INV_AID</c>: the compact inventory-icon art id, or null if inherited from the prototype.</summary>
        public uint? InvAid { get; }

        /// <summary>For items, <c>OBJ_F_ITEM_WEIGHT</c> / <c>OBJ_F_ITEM_WORTH</c>; null if inherited from the prototype.</summary>
        public int? Weight { get; }

        public int? Worth { get; }

        /// <summary>Parsed <c>OBJ_F_WEAPON_*</c> combat stats; null unless this is a weapon record.</summary>
        public WeaponFields Weapon { get; internal set; }

        /// <summary>For critters, <c>OBJ_F_CRITTER_STAT_BASE_IDX</c> — base stats indexed by <c>STAT_*</c>
        /// (STR..CHA = 0..7, LEVEL = 17); null if inherited from the prototype. Absent keys are 0.</summary>
        public int[] StatBase { get; internal set; }

        /// <summary>For armour items, <c>OBJ_F_ARMOR_AC_ADJ</c> — the AC bonus the armour grants; null otherwise.</summary>
        public int? ArmorAc { get; internal set; }

        /// <summary>For critters, <c>OBJ_F_CRITTER_BASIC_SKILL_IDX</c> / <c>OBJ_F_CRITTER_TECH_SKILL_IDX</c> —
        /// skill ranks indexed by <c>BASIC_SKILL_*</c> (12) / <c>TECH_SKILL_*</c> (4); null if inherited.</summary>
        public int[] BasicSkills { get; internal set; }

        public int[] TechSkills { get; internal set; }

        /// <summary><c>OBJ_F_RESISTANCE_IDX</c> — base damage resistances (%), indexed by <c>RESISTANCE_TYPE_*</c>
        /// (NORMAL,FIRE,ELECTRICAL,POISON,MAGIC); null if unset. For armour, <c>OBJ_F_ARMOR_RESISTANCE_ADJ_IDX</c>.</summary>
        public int[] Resistances { get; internal set; }

        public int[] ArmorResist { get; internal set; }

        /// <summary>Base max HP (<c>OBJ_F_HP_PTS</c>): a critter is dead when <c>HP_PTS − <see cref="HpDamage"/> ≤ 0</c>.
        /// Usually authored on the prototype; null if not overridden here.</summary>
        public int? HpPoints { get; internal set; }

        /// <summary>For items, <c>OBJ_F_ITEM_FLAGS</c> (<c>OIF_*</c>: identified, no_pickup, no_display, …); null if unset.</summary>
        public int? ItemFlags { get; internal set; }

        /// <summary>For gold piles, <c>OBJ_F_GOLD_QUANTITY</c> — the number of coins; null otherwise.</summary>
        public int? GoldQuantity { get; internal set; }

        /// <summary>For critters, <c>OBJ_F_CRITTER_FLAGS</c> / <c>FLAGS2</c> (<c>OCF_*</c>: dead, sleeping, concealed,
        /// animal, mechanical, …); null if unset.</summary>
        public int? CritterFlags { get; internal set; }

        public int? CritterFlags2 { get; internal set; }

        /// <summary>For critters, <c>OBJ_F_CRITTER_PORTRAIT</c> — the portrait id; null if unset.</summary>
        public int? Portrait { get; internal set; }

        /// <summary>For NPCs, <c>OBJ_F_NPC_FACTION</c> — the faction number (drives who's hostile to whom); null if unset.</summary>
        public int? Faction { get; internal set; }

        /// <summary>Lock state for a portal (<c>OPF_*</c>) or container (<c>OCOF_*</c>); null if unset.</summary>
        public int? PortalFlags { get; internal set; }

        public int? ContainerFlags { get; internal set; }

        /// <summary>Lock difficulty + key id for a locked portal/container, and a key item's own id
        /// (<c>OBJ_F_*_LOCK_DIFFICULTY</c> / <c>_KEY_ID</c>, <c>OBJ_F_KEY_KEY_ID</c>); null if unset.</summary>
        public int? LockDifficulty { get; internal set; }

        public int? KeyId { get; internal set; }

        /// <summary>For written items (books/signs), <c>OBJ_F_WRITTEN_SUBTYPE</c> and the text line range
        /// (<c>_TEXT_START_LINE</c> / <c>_TEXT_END_LINE</c>) into a <c>.mes</c>; null if unset.</summary>
        public int? WrittenSubtype { get; internal set; }

        public int? TextStartLine { get; internal set; }
        public int? TextEndLine { get; internal set; }

        /// <summary>Light this object emits (<c>OBJ_F_LIGHT_AID</c>); null/0 if it isn't a light source.</summary>
        public uint? LightAid { get; internal set; }

        /// <summary>The light's packed colour (<c>OBJ_F_LIGHT_COLOR</c>, 0x00RRGGBB); null if unset.</summary>
        public int? LightColor { get; internal set; }

        /// <summary><c>OBJ_F_SCENERY_FLAGS</c> (<c>OSCF_*</c>: nocturnal, is-fire, …); null if unset.</summary>
        public int? SceneryFlags { get; internal set; }

        /// <summary>This instance's own object id (24-byte <c>ObjectID</c>) — the key inventory/parent refs point at.</summary>
        public byte[] Oid { get; }

        /// <summary>For items, the holder's object id (<c>OBJ_F_ITEM_PARENT</c>); null if on the ground / not an item.</summary>
        public byte[] ParentOid { get; }

        /// <summary>True if this object lives inside a container/critter inventory (so it shouldn't render on the map).</summary>
        public bool IsInInventory => ParentOid != null;

        public ObjectInstance(ObjectType type, int prototypeNumber, long? location, uint? currentArtId,
                              int offsetX, int offsetY, int? description = null, byte[] oid = null, byte[] parentOid = null,
                              int? flags = null, int dialogNum = 0, int invLocation = -1, uint? invAid = null,
                              int? weight = null, int? worth = null)
        {
            Type = type;
            PrototypeNumber = prototypeNumber;
            Location = location;
            CurrentArtId = currentArtId;
            OffsetX = offsetX;
            OffsetY = offsetY;
            Description = description;
            Oid = oid;
            ParentOid = parentOid;
            Flags = flags;
            DialogNum = dialogNum;
            InvLocation = invLocation;
            InvAid = invAid;
            Weight = weight;
            Worth = worth;
        }

        /// <summary>Stable dictionary key for an <c>ObjectID</c> (hex of its 24 bytes), or null.</summary>
        public static string OidKey(byte[] oid) => oid == null ? null : System.BitConverter.ToString(oid);
    }

    /// <summary>
    /// Decodes Arcanum object instances using the validated field engine. Instance layout
    /// (arcanum-ce obj.c <c>obj_read</c>/<c>obj_inst_read_file</c>):
    /// <code>
    /// version(4)=119  prototype_oid(24)  object_oid(24)  type(4)  num_fields(2)  field_48(4*dwords)  [overridden fields]
    /// </code>
    /// Only fields whose <c>field_48</c> bit is set are stored, in field-enum order, each via the
    /// "fast" reader (INT32 raw; INT64/HANDLE/STRING/ARRAY = presence byte + payload).
    /// </summary>
    public static class ObjectInstanceReader
    {
        public const int ObjectFileVersion = 119;
        private const int OidSize = 24;                      // sizeof(ObjectID)
        private const int OidNumberOffset = 8;               // ObjectID.d.a
        private const int F_LIGHT_AID = 14;                  // OBJ_F_LIGHT_AID (INT32 art id) → object emits a light
        private const int F_LIGHT_COLOR = 15;                // OBJ_F_LIGHT_COLOR (INT32 tig_color 0x00RRGGBB)
        private const int F_SCENERY_FLAGS = 69;              // OBJ_F_SCENERY_FLAGS (INT32) → OSCF_* (nocturnal, is_fire, …)
        private const int F_OFFSET_X = 3;                    // OBJ_F_OFFSET_X (common field)
        private const int F_OFFSET_Y = 4;                    // OBJ_F_OFFSET_Y
        private const int F_FLAGS = 19;                      // OBJ_F_FLAGS (common INT32) → OF_* bitset
        private const int F_HP_DAMAGE = 29;                  // OBJ_F_HP_DAMAGE — critter is dead when this ≥ max HP (crash bodies = 32000)
        private const int F_DESCRIPTION = 23;                // OBJ_F_DESCRIPTION (common INT32) → description.mes id
        private const int F_SCRIPTS = 32;                    // OBJ_F_SCRIPTS_IDX (SCRIPT array, indexed by SAP_*)
        private const int F_ITEM_PARENT = 88;                // OBJ_F_ITEM_PARENT (HANDLE) → holder's ObjectID
        private const int F_ITEM_WEIGHT = 89;                // OBJ_F_ITEM_WEIGHT (INT32)
        private const int F_ITEM_WORTH = 91;                 // OBJ_F_ITEM_WORTH (INT32) → base coin value
        private const int F_ITEM_INV_AID = 93;               // OBJ_F_ITEM_INV_AID (INT32 art id) → compact inventory icon
        private const int F_ITEM_INV_LOCATION = 94;          // OBJ_F_ITEM_INV_LOCATION (INT32) → worn slot 1000–1008 or loose cell
        private const int F_NPC_REACTION_BASE = 295;         // OBJ_F_NPC_REACTION_BASE (INT32) → authored starting reaction
        private const int F_ITEM_MAGIC_TECH_COMPLEXITY = 96; // OBJ_F_ITEM_MAGIC_TECH_COMPLEXITY (INT32) → −tech / +magic

        private const int F_ARMOR_AC_ADJ = 152; // OBJ_F_ARMOR_AC_ADJ (INT32) → AC bonus this armor grants

        // OBJ_F_CRITTER_STAT_BASE_IDX (INT32_ARRAY) — base stats keyed by STAT_*: STR..CHA = 0..7, LEVEL = 17,
        // ALIGNMENT = 19, GENDER = 26, RACE = 27. We read the whole STAT_COUNT-sized array (absent keys read 0).
        private const int F_CRITTER_STAT_BASE = 220;
        private const int StatCount = 28;               // STAT_COUNT
        private const int F_RESISTANCE = 31;            // OBJ_F_RESISTANCE_IDX (common INT32_ARRAY) — base damage resistances
        private const int F_ARMOR_RESISTANCE_ADJ = 154; // OBJ_F_ARMOR_RESISTANCE_ADJ_IDX (INT32_ARRAY) — armour resist bonus
        private const int ResistanceCount = 5;          // RESISTANCE_TYPE_COUNT (NORMAL,FIRE,ELECTRICAL,POISON,MAGIC)
        private const int F_CRITTER_BASIC_SKILL = 221;  // OBJ_F_CRITTER_BASIC_SKILL_IDX (INT32_ARRAY) by BASIC_SKILL_*
        private const int F_CRITTER_TECH_SKILL = 222;   // OBJ_F_CRITTER_TECH_SKILL_IDX (INT32_ARRAY) by TECH_SKILL_*
        private const int BasicSkillCount = 12;         // BASIC_SKILL_COUNT

        private const int TechSkillCount = 4; // TECH_SKILL_COUNT

        // Weapon group (OBJ_F_WEAPON_*): begins at ordinal 111.
        private const int F_WEAPON_FLAGS = 112;
        private const int F_WEAPON_BONUS_TO_HIT = 114;
        private const int F_WEAPON_DAMAGE_LOWER = 116; // INT32_ARRAY, indexed by damage type (0..4)
        private const int F_WEAPON_DAMAGE_UPPER = 117; // INT32_ARRAY
        private const int F_WEAPON_SPEED_FACTOR = 119;
        private const int F_WEAPON_RANGE = 121; // 1 = melee
        private const int F_WEAPON_MIN_STRENGTH = 123;
        private const int F_WEAPON_AMMO_TYPE = 125; // 10000 = none

        // Additional in-scope fields surfaced (all INT32). Ordinals from obj.h, verified against ObjectFieldData.OdType.
        private const int F_HP_PTS = 27;       // base max HP — hp_current = HP_PTS − HP_DAMAGE (≤0 ⇒ dead)
        private const int F_PORTAL_FLAGS = 46; // OPF_* (locked / jammed / magically-held)
        private const int F_PORTAL_LOCK_DIFFICULTY = 47;
        private const int F_PORTAL_KEY_ID = 48;
        private const int F_CONTAINER_FLAGS = 56; // OCOF_*
        private const int F_CONTAINER_LOCK_DIFFICULTY = 57;
        private const int F_CONTAINER_KEY_ID = 58;
        private const int F_ITEM_FLAGS = 87;               // OIF_* (identified / no_pickup / no_display / …)
        private const int F_GOLD_QUANTITY = 165;           // coins in a gold pile
        private const int F_KEY_KEY_ID = 186;              // a key item's id (matches a portal/container KEY_ID)
        private const int F_WRITTEN_SUBTYPE = 202;         // book vs sign vs plaque
        private const int F_WRITTEN_TEXT_START_LINE = 203; // book/sign text range into a .mes
        private const int F_WRITTEN_TEXT_END_LINE = 204;
        private const int F_CRITTER_FLAGS = 218;    // OCF_* (is_dead / sleeping / concealed / animal / …)
        private const int F_CRITTER_FLAGS2 = 219;   // OCF2_*
        private const int F_CRITTER_PORTRAIT = 231; // portrait id
        private const int F_NPC_FACTION = 292;      // NPC faction number

        // Script attachment points (indices into the OBJ_F_SCRIPTS array). SAP_USE fires when an object is
        // used (clicked) — doors/teleporters carry their .scr there; SAP_DIALOG* carry the NPC dialog number.
        private const int SAP_EXAMINE = 0; // fires when the object is looked at (signs, examinable scenery)
        private const int SAP_USE = 1;
        private const int SAP_DIALOG = 9;
        private const int SAP_FIRST_HEARTBEAT = 10; // fires once when the object first ticks
        private const int SAP_HEARTBEAT = 19;       // periodic tick (AI, self-gating NPCs that toggle off/kill)
        private const int SAP_DIALOG_OVERRIDE = 31;
        private const int ScriptNumOffset = 8; // num is the 3rd dword of a 12-byte Script entry

        /// <summary>
        /// Reads one object starting at <paramref name="offset"/>, advancing it past the record.
        /// Set <paramref name="hasNumFields"/> false for prototype (<c>.pro</c>) records, which omit
        /// the 2-byte num_fields that instances (<c>.sec</c>/<c>.mob</c>) carry.
        /// </summary>
        /// <summary>Debug hook: when set, <see cref="Read"/> logs every present field's offset/size/type/value
        /// (the Formats assembly has no UnityEngine ref, so wire this to Debug.Log from Runtime/Editor). Used
        /// to find the proto field-walk desync — see Docs/Combat.md TODO.</summary>
        public static System.Action<string> DebugSink;

        public static ObjectInstance Read(byte[] b, ref int offset, bool hasNumFields = true)
        {
            int o = offset;
            int version = I32(b, o);
            o += 4;
            if (version != ObjectFileVersion)
                throw new DatFormatException($"object has version {version}, expected {ObjectFileVersion} at offset {offset}");

            // prototype_oid (24): d.a at +8 is the prototype number for OID_TYPE_A references.
            int protoNumber = I32(b, o + OidNumberOffset);
            o += OidSize;

            byte[] oid = Slice(b, o, OidSize);
            o += OidSize; // object_oid (instance's own id)
            int typeRaw = I32(b, o);
            o += 4;
            if (hasNumFields) o += 2; // num_fields (int16) — instances only, not prototypes

            var type = (ObjectType)typeRaw;
            if (typeRaw < 0 || typeRaw >= ObjectFieldEngine.DwordCount.Length)
                throw new DatFormatException($"object has unknown type {typeRaw} at offset {offset}");

            int ndw = ObjectFieldEngine.DwordCount[typeRaw];
            uint[] field48 = new uint[ndw];
            for (int i = 0; i < ndw; i++)
            {
                field48[i] = U32(b, o);
                o += 4;
            }

            DebugSink?.Invoke($"obj type={typeRaw} ndw={ndw} dataStart={o} len={b.Length}");

            long? location = null;
            uint? currentArtId = null;
            int offsetX = 0, offsetY = 0;
            int? description = null;
            int? flags = null;
            byte[] parentOid = null;
            int dialogNum = 0;
            int useScriptNum = 0;
            int examineScriptNum = 0;
            int heartbeatScriptNum = 0;
            int firstHeartbeatScriptNum = 0;
            int hpDamage = 0;
            int? reactionBase = null;
            int[] statBase = null;
            int? armorAc = null;
            int[] basicSkills = null, techSkills = null;
            int[] resistances = null, armorResist = null;
            int invLocation = -1;
            uint? invAid = null;
            int? weight = null, worth = null;
            uint? lightAid = null;
            int? lightColor = null;
            int? sceneryFlags = null;
            int? hpPoints = null, itemFlags = null, goldQuantity = null, critterFlags = null, critterFlags2 = null;
            int? portrait = null, faction = null, portalFlags = null, containerFlags = null;
            int? lockDifficulty = null, keyId = null;
            int? writtenSubtype = null, textStartLine = null, textEndLine = null;
            WeaponFields wf = typeRaw == (int)ObjectType.Weapon ? new WeaponFields() : null;

            // Prototypes (.pro) store EVERY field in order (engine object_proto_enumerate_fields reads
            // begin+1..end with no bitmap check); instances (.sec/.mob, hasNumFields) store only the
            // overridden fields gated by the field_48 change bitmap. Filtering a proto by the bitmap skips
            // most fields and desyncs the walk — the bug behind the weapon/weight/worth garbage.
            bool readAllFields = !hasNumFields;

            foreach (int fld in EnumerateFields(typeRaw))
            {
                if (!readAllFields)
                {
                    int ci = ObjectFieldEngine.ChangeIdx[fld];
                    if (ci < 0 || ci >= ndw) continue;
                    if ((field48[ci] & ObjectFieldEngine.Mask[fld]) == 0) continue;
                }

                var od = (OdType)ObjectFieldData.OdType[fld];
                int dbgBefore = o; // capture start offset for the field, for the desync trace

                switch (fld)
                {
                    case ObjectFieldData.F_LOCATION:
                    {
                        byte present = b[o++];
                        if (present != 0)
                        {
                            location = I64(b, o);
                            o += 8;
                        }

                        break;
                    }
                    case ObjectFieldData.F_CURRENT_AID:
                        currentArtId = U32(b, o);
                        o += 4;
                        break;
                    case F_LIGHT_AID:
                        lightAid = U32(b, o);
                        o += 4;
                        break;
                    case F_LIGHT_COLOR:
                        lightColor = I32(b, o);
                        o += 4;
                        break;
                    case F_SCENERY_FLAGS:
                        sceneryFlags = I32(b, o);
                        o += 4;
                        break;
                    case F_FLAGS:
                        flags = I32(b, o);
                        o += 4;
                        break;
                    case F_HP_DAMAGE:
                        hpDamage = I32(b, o);
                        o += 4;
                        break;
                    case F_OFFSET_X:
                        offsetX = I32(b, o);
                        o += 4;
                        break;
                    case F_OFFSET_Y:
                        offsetY = I32(b, o);
                        o += 4;
                        break;
                    case F_DESCRIPTION:
                        description = I32(b, o);
                        o += 4;
                        break;
                    case F_ITEM_WEIGHT:
                        weight = I32(b, o);
                        o += 4;
                        break;
                    case F_ITEM_WORTH:
                        worth = I32(b, o);
                        o += 4;
                        break;
                    case F_ITEM_INV_AID:
                        invAid = U32(b, o);
                        o += 4;
                        break;
                    case F_ITEM_INV_LOCATION:
                        invLocation = I32(b, o);
                        o += 4;
                        break;
                    case F_NPC_REACTION_BASE:
                        reactionBase = I32(b, o);
                        o += 4;
                        break;
                    case F_ARMOR_AC_ADJ:
                        armorAc = I32(b, o);
                        o += 4;
                        break;
                    case F_HP_PTS:
                        hpPoints = I32(b, o);
                        o += 4;
                        break;
                    case F_ITEM_FLAGS:
                        itemFlags = I32(b, o);
                        o += 4;
                        break;
                    case F_GOLD_QUANTITY:
                        goldQuantity = I32(b, o);
                        o += 4;
                        break;
                    case F_CRITTER_FLAGS:
                        critterFlags = I32(b, o);
                        o += 4;
                        break;
                    case F_CRITTER_FLAGS2:
                        critterFlags2 = I32(b, o);
                        o += 4;
                        break;
                    case F_CRITTER_PORTRAIT:
                        portrait = I32(b, o);
                        o += 4;
                        break;
                    case F_NPC_FACTION:
                        faction = I32(b, o);
                        o += 4;
                        break;
                    case F_PORTAL_FLAGS:
                        portalFlags = I32(b, o);
                        o += 4;
                        break;
                    case F_CONTAINER_FLAGS:
                        containerFlags = I32(b, o);
                        o += 4;
                        break;
                    case F_PORTAL_LOCK_DIFFICULTY:
                    case F_CONTAINER_LOCK_DIFFICULTY:
                        lockDifficulty = I32(b, o);
                        o += 4;
                        break;
                    case F_PORTAL_KEY_ID:
                    case F_CONTAINER_KEY_ID:
                    case F_KEY_KEY_ID:
                        keyId = I32(b, o);
                        o += 4;
                        break;
                    case F_WRITTEN_SUBTYPE:
                        writtenSubtype = I32(b, o);
                        o += 4;
                        break;
                    case F_WRITTEN_TEXT_START_LINE:
                        textStartLine = I32(b, o);
                        o += 4;
                        break;
                    case F_WRITTEN_TEXT_END_LINE:
                        textEndLine = I32(b, o);
                        o += 4;
                        break;
                    case F_CRITTER_STAT_BASE:
                        statBase = ReadIntArray(b, o, StatCount);
                        o += FieldSize(b, o, od);
                        break;
                    case F_CRITTER_BASIC_SKILL:
                        basicSkills = ReadIntArray(b, o, BasicSkillCount);
                        o += FieldSize(b, o, od);
                        break;
                    case F_CRITTER_TECH_SKILL:
                        techSkills = ReadIntArray(b, o, TechSkillCount);
                        o += FieldSize(b, o, od);
                        break;
                    case F_RESISTANCE:
                        resistances = ReadIntArray(b, o, ResistanceCount);
                        o += FieldSize(b, o, od);
                        break;
                    case F_ARMOR_RESISTANCE_ADJ:
                        armorResist = ReadIntArray(b, o, ResistanceCount);
                        o += FieldSize(b, o, od);
                        break;

                    // Weapon fields only populate on a weapon record (wf != null); otherwise they fall to default.
                    case F_ITEM_MAGIC_TECH_COMPLEXITY when wf != null:
                        wf.MagicTechComplexity = I32(b, o);
                        o += 4;
                        break;
                    case F_WEAPON_FLAGS when wf != null:
                        wf.Flags = I32(b, o);
                        o += 4;
                        break;
                    case F_WEAPON_BONUS_TO_HIT when wf != null:
                        wf.BonusToHit = I32(b, o);
                        o += 4;
                        break;
                    case F_WEAPON_SPEED_FACTOR when wf != null:
                        wf.SpeedFactor = I32(b, o);
                        o += 4;
                        break;
                    case F_WEAPON_RANGE when wf != null:
                        wf.Range = I32(b, o);
                        o += 4;
                        break;
                    case F_WEAPON_MIN_STRENGTH when wf != null:
                        wf.MinStrength = I32(b, o);
                        o += 4;
                        break;
                    case F_WEAPON_AMMO_TYPE when wf != null:
                        wf.AmmoType = I32(b, o);
                        o += 4;
                        break;
                    case F_WEAPON_DAMAGE_LOWER when wf != null:
                        for (int k = 0; k < WeaponFields.DamageTypeCount; k++) wf.DamageMin[k] = ArrayInt32(b, o, k);
                        o += FieldSize(b, o, od);
                        break;
                    case F_WEAPON_DAMAGE_UPPER when wf != null:
                        for (int k = 0; k < WeaponFields.DamageTypeCount; k++) wf.DamageMax[k] = ArrayInt32(b, o, k);
                        o += FieldSize(b, o, od);
                        break;

                    case F_ITEM_PARENT:
                    {
                        byte present = b[o++]; // HANDLE: presence byte + 24-byte OID
                        if (present != 0)
                        {
                            parentOid = Slice(b, o, OidSize);
                            o += OidSize;
                        }

                        break;
                    }
                    case F_SCRIPTS when od == OdType.ScriptArray:
                        if (b[o] != 0) // sparse array: the conversation is at SAP_DIALOG; SAP_DIALOG_OVERRIDE is a
                        {
                            // separate generated/state-line dialog (e.g. *override.dlg, no entry at line 1)
                            dialogNum = ScriptNum(b, o, SAP_DIALOG);
                            if (dialogNum == 0) dialogNum = ScriptNum(b, o, SAP_DIALOG_OVERRIDE);
                            useScriptNum = ScriptNum(b, o, SAP_USE);             // the use/teleport script (doors, levers)
                            examineScriptNum = ScriptNum(b, o, SAP_EXAMINE);     // look-at script (signs, often floats text)
                            heartbeatScriptNum = ScriptNum(b, o, SAP_HEARTBEAT); // periodic tick (AI / self-gating NPCs)
                            firstHeartbeatScriptNum = ScriptNum(b, o, SAP_FIRST_HEARTBEAT);
                        }

                        o += FieldSize(b, o, od);
                        break;

                    default: o += FieldSize(b, o, od); break;
                }

                if (DebugSink != null)
                {
                    string detail = od == OdType.Int32 && dbgBefore + 4 <= b.Length
                        ? $"i32={I32(b, dbgBefore)}"
                        : (dbgBefore < b.Length ? $"present={b[dbgBefore]}" : "OOB");
                    DebugSink($"  fld {fld,3}  off {dbgBefore,4}  size {o - dbgBefore,3}  {od,-12} {detail}");
                }
            }

            offset = o;
            return new ObjectInstance(type, protoNumber, location, currentArtId, offsetX, offsetY, description, oid, parentOid, flags, dialogNum, invLocation, invAid, weight, worth)
            {
                Weapon = wf,
                LightAid = lightAid,
                LightColor = lightColor,
                SceneryFlags = sceneryFlags,
                UseScriptNum = useScriptNum,
                ExamineScriptNum = examineScriptNum,
                HeartbeatScriptNum = heartbeatScriptNum,
                FirstHeartbeatScriptNum = firstHeartbeatScriptNum,
                HpDamage = hpDamage,
                ReactionBase = reactionBase,
                StatBase = statBase,
                ArmorAc = armorAc,
                BasicSkills = basicSkills,
                TechSkills = techSkills,
                Resistances = resistances,
                ArmorResist = armorResist,
                HpPoints = hpPoints,
                ItemFlags = itemFlags,
                GoldQuantity = goldQuantity,
                CritterFlags = critterFlags,
                CritterFlags2 = critterFlags2,
                Portrait = portrait,
                Faction = faction,
                PortalFlags = portalFlags,
                ContainerFlags = containerFlags,
                LockDifficulty = lockDifficulty,
                KeyId = keyId,
                WrittenSubtype = writtenSubtype,
                TextStartLine = textStartLine,
                TextEndLine = textEndLine,
            };
        }

        /// <summary>Reads a full sector object list. The count lives in the last 4 bytes of the sector.</summary>
        public static List<ObjectInstance> ReadSectorObjects(byte[] sectorBytes, int listStart)
        {
            int count = I32(sectorBytes, sectorBytes.Length - 4);
            var result = new List<ObjectInstance>(count);
            int o = listStart;
            for (int i = 0; i < count; i++)
                result.Add(Read(sectorBytes, ref o));
            return result;
        }

        private static IEnumerable<int> EnumerateFields(int type)
        {
            // Common fields first.
            for (int f = ObjectFieldData.F_BEGIN + 1; f < ObjectFieldData.F_END; f++)
                if (!IsMarker(f))
                    yield return f;

            // Then this type's ranges.
            int start = ObjectFieldData.TypeRangeOffset[type];
            int end = ObjectFieldData.TypeRangeOffset[type + 1];
            for (int r = start; r < end; r++)
                for (int f = ObjectFieldData.TypeRangeBegin[r] + 1; f < ObjectFieldData.TypeRangeEnd[r]; f++)
                    if (!IsMarker(f))
                        yield return f;
        }

        private static bool IsMarker(int fld)
        {
            var od = (OdType)ObjectFieldData.OdType[fld];
            return od == OdType.Begin || od == OdType.End;
        }

        private static int FieldSize(byte[] b, int o, OdType od)
        {
            switch (od)
            {
                case OdType.Int32: return 4;
                case OdType.Int64: return 1 + (b[o] != 0 ? 8 : 0);
                case OdType.Handle: return 1 + (b[o] != 0 ? OidSize : 0);
                case OdType.String:
                    return b[o] == 0 ? 1 : 1 + 4 + I32(b, o + 1) + 1;
                case OdType.Int32Array:
                case OdType.Int64Array:
                case OdType.UInt32Array:
                case OdType.UInt64Array:
                case OdType.ScriptArray:
                case OdType.QuestArray:
                case OdType.HandleArray:
                    if (b[o] == 0) return 1;
                    int size = I32(b, o + 1), count = I32(b, o + 5);
                    int dataBytes = size * count;
                    int bitsetCnt = I32(b, o + 1 + 12 + dataBytes);
                    return 1 + 12 + dataBytes + 4 + bitsetCnt * 4;
                default: return 0; // PTR / transient — not serialized
            }
        }

        // The Script.num at attachment index sap (0 if absent). The OBJ_F_SCRIPTS array is a sparse
        // SizeableArray: presence(1) + header{size,count,bitset_id}(12) + compact data(size*count) +
        // bitset{ cnt(4) + cnt dwords }. The engine (sa_get) maps key→slot via bitset_rank, gated by
        // bitset_test, so element `sap` is data[rank(sap)] only if its bit is set.
        private static int ScriptNum(byte[] b, int o, int sap)
        {
            int size = I32(b, o + 1);
            int count = I32(b, o + 5);
            int dataStart = o + 13;
            int bitsetOff = dataStart + size * count;
            int bitsetCnt = I32(b, bitsetOff); // number of 32-bit storage words
            int bitsetData = bitsetOff + 4;

            int word = sap >> 5;             // bitset_index_of
            if (word >= bitsetCnt) return 0; // bitset_test: past the stored words
            uint storage = U32(b, bitsetData + word * 4);
            uint bit = 1u << (sap & 31);        // bitset_mask_of
            if ((storage & bit) == 0) return 0; // bitset_test: not present

            int rank = PopCount(storage & (bit - 1)); // bits before sap in its word
            for (int w = 0; w < word; w++) rank += PopCount(U32(b, bitsetData + w * 4));
            if (rank >= count) return 0;

            return I32(b, dataStart + rank * size + ScriptNumOffset);
        }

        /// <summary>Reads the first <paramref name="count"/> elements of a sparse INT32 SizeableArray into a
        /// dense array (absent keys = 0), or null if the field isn't present. Does NOT advance the cursor —
        /// the caller still skips the field via <see cref="FieldSize"/>.</summary>
        private static int[] ReadIntArray(byte[] b, int o, int count)
        {
            if (b[o] == 0) return null;
            var arr = new int[count];
            for (int k = 0; k < count; k++) arr[k] = ArrayInt32(b, o, k);
            return arr;
        }

        /// <summary>Element <paramref name="key"/> of a sparse INT32 SizeableArray at <paramref name="o"/>
        /// (presence byte + {size,count,bitset}), or 0 if absent. Same bitset rank/test as <see cref="ScriptNum"/>.</summary>
        private static int ArrayInt32(byte[] b, int o, int key)
        {
            if (b[o] == 0) return 0; // not present
            int size = I32(b, o + 1), count = I32(b, o + 5);
            int dataStart = o + 13;
            int bitsetData = dataStart + size * count + 4;
            int bitsetCnt = I32(b, dataStart + size * count);

            int word = key >> 5;
            if (word >= bitsetCnt) return 0;
            uint storage = U32(b, bitsetData + word * 4);
            uint bit = 1u << (key & 31);
            if ((storage & bit) == 0) return 0;

            int rank = PopCount(storage & (bit - 1));
            for (int w = 0; w < word; w++) rank += PopCount(U32(b, bitsetData + w * 4));
            if (rank >= count) return 0;
            return I32(b, dataStart + rank * size);
        }

        private static int PopCount(uint v)
        {
            v -= (v >> 1) & 0x55555555u;
            v = (v & 0x33333333u) + ((v >> 2) & 0x33333333u);
            return (int)((((v + (v >> 4)) & 0x0F0F0F0Fu) * 0x01010101u) >> 24);
        }

        private static byte[] Slice(byte[] b, int o, int len)
        {
            var r = new byte[len];
            System.Array.Copy(b, o, r, 0, len);
            return r;
        }

        private static int I32(byte[] b, int o) => b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24);
        private static uint U32(byte[] b, int o) => (uint)I32(b, o);
        private static long I64(byte[] b, int o) => (uint)I32(b, o) | ((long)I32(b, o + 4) << 32);
    }
}
