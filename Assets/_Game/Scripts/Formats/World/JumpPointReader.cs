using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Arcanum.Formats.World
{
    /// <summary>
    /// A single map transition (engine <c>JumpPoint</c>, jumppoint.c). When the player
    /// steps onto <see cref="SrcLoc"/> the engine teleports them to <see cref="DstLoc"/>
    /// on map <see cref="DstMap"/> (a 1-based index into <c>rules/MapList.mes</c> —
    /// resolve with <see cref="MapList"/>).
    /// </summary>
    public readonly struct JumpPoint
    {
        /// <summary>On-disk record size — engine <c>static_assert(sizeof(JumpPoint) == 0x20)</c>.</summary>
        public const int RecordSize = 0x20;

        public readonly uint Flags;
        /// <summary>Trigger tile (packed engine location: x = low 32 bits, y = high 32 bits).</summary>
        public readonly long SrcLoc;
        /// <summary>Destination map — 1-based index into the MapList; 0 means "same map".</summary>
        public readonly int DstMap;
        /// <summary>Destination tile (packed engine location).</summary>
        public readonly long DstLoc;

        public JumpPoint(uint flags, long srcLoc, int dstMap, long dstLoc)
        {
            Flags = flags;
            SrcLoc = srcLoc;
            DstMap = dstMap;
            DstLoc = dstLoc;
        }

        public int SrcX => (int)(SrcLoc & 0xFFFFFFFF);
        public int SrcY => (int)((SrcLoc >> 32) & 0xFFFFFFFF);
        public int DstX => (int)(DstLoc & 0xFFFFFFFF);
        public int DstY => (int)((DstLoc >> 32) & 0xFFFFFFFF);
    }

    /// <summary>
    /// Reads a map's <c>map.jmp</c> file — the table of jump points (map↔map transitions)
    /// for that map. Ports arcanum-ce <c>jumppoint_read_all</c>: an <c>int32</c> count
    /// followed by that many 32-byte <see cref="JumpPoint"/> records.
    /// </summary>
    public static class JumpPointReader
    {
        public static List<JumpPoint> Read(byte[] jmpBytes)
        {
            using var stream = new MemoryStream(jmpBytes, writable: false);
            using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

            int count = reader.ReadInt32();
            if (count < 0 || (long)4 + (long)count * JumpPoint.RecordSize > jmpBytes.Length)
                throw new DatFormatException($"map.jmp has an implausible jump-point count ({count}).");

            var points = new List<JumpPoint>(count);
            for (int i = 0; i < count; i++)
            {
                uint flags = reader.ReadUInt32();
                reader.ReadInt32();              // padding_4
                long srcLoc = reader.ReadInt64();
                int dstMap = reader.ReadInt32();
                reader.ReadInt32();              // padding_14
                long dstLoc = reader.ReadInt64();
                points.Add(new JumpPoint(flags, srcLoc, dstMap, dstLoc));
            }
            return points;
        }
    }
}
