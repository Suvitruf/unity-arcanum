using System.IO;
using System.Text;
using Arcanum.Formats.Script;
using NUnit.Framework;

namespace Arcanum.Formats.Tests
{
    /// <summary>
    /// Exercises <see cref="ScriptReader"/> against a synthetic <c>.scr</c> built to the documented layout
    /// (header + ScriptFile + 132-byte conditions / 44-byte actions). Models a real door teleporter:
    /// one <c>SCT_TRUE</c> entry whose action is <c>SAT_TELEPORT</c> to map key 5011 @ (104, 92) — the
    /// shape of <c>scr/01267bates mansion to 1st floor_tel.scr</c>. See Docs/Scripting.md.
    /// </summary>
    public sealed class ScriptFormatTests
    {
        [Test]
        public void ParsesTeleportDoorScript()
        {
            byte[] scr = BuildScript(
                cond: (int)Sct.True,
                action: Action((int)Sat.Teleport,
                    opType: new byte[] { 0, (byte)Svt.Number, (byte)Svt.Number, (byte)Svt.Number, 0, 0, 0, 0 },
                    opValue: new[] { 0, 5011, 104, 92, 0, 0, 0, 0 }),
                els: Action((int)Sat.DoNothing, new byte[8], new int[8]));

            ScriptFile file = ScriptReader.Read(scr);

            Assert.That(file.Entries.Count, Is.EqualTo(1));
            ScriptCondition c = file.Entries[0];
            Assert.That((Sct)c.Type, Is.EqualTo(Sct.True));
            Assert.That((Sat)c.Action.Type, Is.EqualTo(Sat.Teleport));
            Assert.That((Svt)c.Action.OpType[1], Is.EqualTo(Svt.Number));
            Assert.That(c.Action.OpValue[1], Is.EqualTo(5011)); // map key → id 12 (5011 − 4999)
            Assert.That(c.Action.OpValue[2], Is.EqualTo(104));  // x
            Assert.That(c.Action.OpValue[3], Is.EqualTo(92));   // y
            Assert.That((Sat)c.Els.Type, Is.EqualTo(Sat.DoNothing));
        }

        [Test]
        public void RejectsImplausibleEntryCount()
        {
            // Valid 64-byte preamble but a wildly large num_entries and no entry bytes.
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms, Encoding.ASCII);
            w.Write(0);
            w.Write(0);            // header
            w.Write(new byte[40]); // description
            w.Write(0);            // flags
            w.Write(1_000_000);    // num_entries (implausible)
            w.Write(1_000_000);    // max_entries
            w.Write(0);            // pad
            w.Flush();
            Assert.That(() => ScriptReader.Read(ms.ToArray()), Throws.TypeOf<DatFormatException>());
        }

        [Test]
        public void SurfacesHeaderFlagsAndCounters()
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms, Encoding.ASCII);
            w.Write(0x5u);                      // ScriptHeader.flags
            w.Write(new byte[] { 1, 2, 3, 4 }); // ScriptHeader.counters (4 packed)
            w.Write(new byte[40]);              // description
            w.Write(0u);                        // ScriptFile.flags
            w.Write(0);                         // num_entries
            w.Write(0);                         // max_entries
            w.Write(0);                         // pad
            w.Flush();

            ScriptFile file = ScriptReader.Read(ms.ToArray());
            Assert.That(file.HeaderFlags, Is.EqualTo(0x5u));
            Assert.That(file.Counters, Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
            Assert.That(file.Entries.Count, Is.EqualTo(0));
        }

        private static (int type, byte[] opType, int[] opValue) Action(int type, byte[] opType, int[] opValue)
            => (type, opType, opValue);

        private static byte[] BuildScript(int cond,
                                          (int type, byte[] opType, int[] opValue) action,
                                          (int type, byte[] opType, int[] opValue) els)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms, Encoding.ASCII);
            w.Write(0u);
            w.Write(0u);           // ScriptHeader: flags, counters
            w.Write(new byte[40]); // description
            w.Write(0u);           // ScriptFile.flags
            w.Write(1);            // num_entries
            w.Write(1);            // max_entries
            w.Write(0);            // pad

            // One ScriptCondition: type, op_type[8], op_value[8], action(44), els(44).
            w.Write(cond);
            w.Write(new byte[8]);
            for (int i = 0; i < 8; i++) w.Write(0);
            WriteAction(w, action);
            WriteAction(w, els);

            w.Flush();
            return ms.ToArray();
        }

        private static void WriteAction(BinaryWriter w, (int type, byte[] opType, int[] opValue) a)
        {
            w.Write(a.type);
            w.Write(a.opType);                                 // 8 bytes
            for (int i = 0; i < 8; i++) w.Write(a.opValue[i]); // 8 int32s
        }
    }
}
