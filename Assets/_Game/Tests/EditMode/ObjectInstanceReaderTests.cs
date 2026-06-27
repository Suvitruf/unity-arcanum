using System.IO;
using Arcanum.Formats.Objects;
using NUnit.Framework;

namespace Arcanum.Formats.Tests
{
    /// <summary>
    /// Surfacing of the in-scope object fields (HP_PTS, ITEM_FLAGS, GOLD_QUANTITY, CRITTER_FLAGS, NPC_FACTION,
    /// portal/container lock-and-key, written text). Builds minimal synthetic <i>instance</i> records — only the
    /// chosen fields' change-bitmap bits are set, so the reader reads exactly those — and asserts the values land
    /// AND the cursor finishes exactly at the record end (no field-walk desync).
    /// </summary>
    public sealed class ObjectInstanceReaderTests
    {
        private const int OdTypeInt32 = 3; // ObjectFieldData.OdType code for OD_TYPE_INT32

        // Every field we newly surface must be INT32 — a wrong ordinal (non-INT32) would desync the walk.
        [Test]
        public void SurfacedFieldsAreInt32()
        {
            int[] fields = { 27, 46, 47, 48, 56, 57, 58, 87, 165, 186, 202, 203, 204, 218, 219, 231, 292 };
            foreach (int f in fields)
                Assert.That(ObjectFieldData.OdType[f], Is.EqualTo(OdTypeInt32), $"field ordinal {f} must be INT32");
        }

        [Test]
        public void ReadsGoldItemAndCommonFields()
        {
            // Gold (type 8) enumerates common + ITEM parent group + gold group.
            byte[] rec = BuildInstance(ObjectType.Gold, (27, 999), (87, 0x40), (165, 4200)); // HP_PTS, ITEM_FLAGS, GOLD_QUANTITY
            int off = 0;
            ObjectInstance inst = ObjectInstanceReader.Read(rec, ref off);
            Assert.That(inst.HpPoints, Is.EqualTo(999));
            Assert.That(inst.ItemFlags, Is.EqualTo(0x40));
            Assert.That(inst.GoldQuantity, Is.EqualTo(4200));
            Assert.That(off, Is.EqualTo(rec.Length), "cursor landed exactly at the record end");
        }

        [Test]
        public void ReadsNpcCritterAndFactionFields()
        {
            byte[] rec = BuildInstance(ObjectType.Npc, (27, 30), (218, 0x01), (231, 17), (292, 5)); // HP_PTS, CRITTER_FLAGS, PORTRAIT, FACTION
            int off = 0;
            ObjectInstance inst = ObjectInstanceReader.Read(rec, ref off);
            Assert.That(inst.HpPoints, Is.EqualTo(30));
            Assert.That(inst.CritterFlags, Is.EqualTo(0x01));
            Assert.That(inst.Portrait, Is.EqualTo(17));
            Assert.That(inst.Faction, Is.EqualTo(5));
            Assert.That(off, Is.EqualTo(rec.Length));
        }

        [Test]
        public void ReadsPortalLockAndKey()
        {
            byte[] rec = BuildInstance(ObjectType.Portal, (46, 0x02), (47, 25), (48, 1234)); // FLAGS, LOCK_DIFFICULTY, KEY_ID
            int off = 0;
            ObjectInstance inst = ObjectInstanceReader.Read(rec, ref off);
            Assert.That(inst.PortalFlags, Is.EqualTo(0x02));
            Assert.That(inst.LockDifficulty, Is.EqualTo(25));
            Assert.That(inst.KeyId, Is.EqualTo(1234));
            Assert.That(off, Is.EqualTo(rec.Length));
        }

        [Test]
        public void AbsentFieldsAreNull()
        {
            byte[] rec = BuildInstance(ObjectType.Gold); // no overridden fields
            int off = 0;
            ObjectInstance inst = ObjectInstanceReader.Read(rec, ref off);
            Assert.That(inst.HpPoints, Is.Null);
            Assert.That(inst.GoldQuantity, Is.Null);
            Assert.That(inst.ItemFlags, Is.Null);
            Assert.That(off, Is.EqualTo(rec.Length));
        }

        // Builds a minimal instance: header + the type's change bitmap (bits set for the given fields) + each
        // field's INT32 value. Fields MUST be passed in field-enum order (ascending ordinal works here, since each
        // is either a common field or in one of the type's groups). Uses the real engine tables via InternalsVisibleTo.
        private static byte[] BuildInstance(ObjectType type, params (int fld, int val)[] int32Fields)
        {
            int typeRaw = (int)type;
            int ndw = ObjectFieldEngine.DwordCount[typeRaw];
            var field48 = new uint[ndw];
            foreach (var (fld, _) in int32Fields)
                field48[ObjectFieldEngine.ChangeIdx[fld]] |= ObjectFieldEngine.Mask[fld];

            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(119);          // version
            w.Write(new byte[24]); // prototype_oid
            w.Write(new byte[24]); // object_oid
            w.Write(typeRaw);      // type
            w.Write((short)0);     // num_fields (instance only; value unused by the reader)
            foreach (uint d in field48) w.Write(d);
            foreach (var (_, val) in int32Fields) w.Write(val); // only the set fields, in enum order
            return ms.ToArray();
        }
    }
}
