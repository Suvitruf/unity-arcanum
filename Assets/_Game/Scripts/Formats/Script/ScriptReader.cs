using System.IO;
using System.Text;

namespace Arcanum.Formats.Script
{
    /// <summary>
    /// Reads a compiled Arcanum <c>.scr</c> script file. Ports arcanum-ce
    /// <c>script_file_load_hdr</c> + <c>script_file_load_code</c> (script.c). Layout (little-endian):
    /// <code>
    /// ScriptHeader:  flags(4)  counters(4)
    /// ScriptFile:    description(40)  flags(4)  num_entries(4)  max_entries(4)  pad(4)
    ///                num_entries × ScriptCondition(0x84)
    /// ScriptCondition: type(4)  op_type(8)  op_value(8×4)  action(0x2C)  els(0x2C)
    /// ScriptAction:    type(4)  op_type(8)  op_value(8×4)
    /// </code>
    /// Validated byte-exact against the shipped <c>arcanum2.dat</c> scripts (e.g. the <c>*_tel</c> doors).
    /// </summary>
    public static class ScriptReader
    {
        private const int ConditionSize = 0x84; // 132
        private const int ActionSize = 0x2C;    // 44

        public static ScriptFile Read(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var r = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

            var file = new ScriptFile();
            file.HeaderFlags = r.ReadUInt32();                           // ScriptHeader.flags (template; live state in OBJ_F_SCRIPTS)
            for (int i = 0; i < 4; i++) file.Counters[i] = r.ReadByte(); // ScriptHeader.counters — 4 packed counters

            byte[] desc = r.ReadBytes(40);
            file.Description = Encoding.ASCII.GetString(desc).TrimEnd('\0');
            file.Flags = r.ReadUInt32();
            int numEntries = r.ReadInt32();
            r.ReadInt32(); // max_entries
            r.ReadInt32(); // pad (the x86 ScriptFile::entries pointer slot)

            if (numEntries < 0 || (long)stream.Position + (long)numEntries * ConditionSize > bytes.Length)
                throw new DatFormatException($".scr has an implausible entry count ({numEntries}).");

            for (int i = 0; i < numEntries; i++)
            {
                var c = new ScriptCondition { Type = r.ReadInt32() };
                ReadOperands(r, c.OpType, c.OpValue);
                c.Action = ReadAction(r);
                c.Els = ReadAction(r);
                file.Entries.Add(c);
            }

            return file;
        }

        private static ScriptAction ReadAction(BinaryReader r)
        {
            var a = new ScriptAction { Type = r.ReadInt32() };
            ReadOperands(r, a.OpType, a.OpValue);
            return a;
        }

        // op_type is 8 bytes (one Svt each); op_value is 8 int32s.
        private static void ReadOperands(BinaryReader r, byte[] opType, int[] opValue)
        {
            for (int i = 0; i < 8; i++) opType[i] = r.ReadByte();
            for (int i = 0; i < 8; i++) opValue[i] = r.ReadInt32();
        }
    }
}
