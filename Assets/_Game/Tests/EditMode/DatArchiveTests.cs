using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Arcanum.Formats.Database;
using NUnit.Framework;

namespace Arcanum.Formats.Tests
{
    /// <summary>
    /// Exercises <see cref="DatArchive"/> against synthetic archives built to the
    /// documented on-disk layout. This proves the TOC/footer parsing and both the
    /// plain and zlib payload paths are internally consistent; correctness against
    /// the real game files is confirmed separately via the DAT Inspector window.
    /// </summary>
    public sealed class DatArchiveTests
    {
        [Test]
        public void ReadsPlainEntry()
        {
            byte[] payload = Encoding.ASCII.GetBytes("hello arcanum");
            byte[] archive = BuildArchive(
                new SyntheticEntry("data\\readme.txt", DatEntryFlags.Plain, payload, payload));

            using var dat = DatArchive.Open(new MemoryStream(archive));
            Assert.That(dat.Entries.Count, Is.EqualTo(1));
            Assert.That(dat.TryGetEntry("data/readme.txt", out var entry), Is.True);
            Assert.That(dat.ReadAllBytes(entry), Is.EqualTo(payload));
        }

        [Test]
        public void ReadsCompressedEntry()
        {
            byte[] payload = Encoding.ASCII.GetBytes(new string('A', 4096) + "tail");
            byte[] stored = ZlibCompress(payload);
            byte[] archive = BuildArchive(
                new SyntheticEntry("art\\tile\\grass.art", DatEntryFlags.Compressed, payload, stored));

            using var dat = DatArchive.Open(new MemoryStream(archive));
            Assert.That(dat.ReadAllBytes("art/tile/grass.art"), Is.EqualTo(payload));
        }

        [Test]
        public void RejectsBadMagic()
        {
            byte[] archive = new byte[64];
            Assert.Throws<DatFormatException>(() =>
            {
                using var _ = DatArchive.Open(new MemoryStream(archive));
            });
        }

        [Test]
        public void AcceptsGuidTaggedMagic()
        {
            // The "1TAD" variant carries a GUID before the magic; our reader ignores it and still parses.
            byte[] payload = Encoding.ASCII.GetBytes("guid variant");
            byte[] archive = BuildArchive(DatArchive.MagicDat1,
                new SyntheticEntry("a.txt", DatEntryFlags.Plain, payload, payload));

            using var dat = DatArchive.Open(new MemoryStream(archive));
            Assert.That(dat.Magic, Is.EqualTo(DatArchive.MagicDat1));
            Assert.That(dat.ReadAllBytes("a.txt"), Is.EqualTo(payload));
        }

        [Test]
        public void ReadsMultipleEntriesAndNormalizesPaths()
        {
            byte[] a = Encoding.ASCII.GetBytes("alpha");
            byte[] b = Encoding.ASCII.GetBytes("beta");
            byte[] archive = BuildArchive(
                new SyntheticEntry("Dir\\Mixed Case.TXT", DatEntryFlags.Plain, a, a),
                new SyntheticEntry("rules\\spell.mes", DatEntryFlags.Plain, b, b));

            using var dat = DatArchive.Open(new MemoryStream(archive));
            Assert.That(dat.Entries.Count, Is.EqualTo(2));
            // Lookup is case-insensitive + slash-normalized.
            Assert.That(dat.ReadAllBytes("dir/mixed case.txt"), Is.EqualTo(a));
            Assert.That(dat.ReadAllBytes("RULES/SPELL.MES"), Is.EqualTo(b));
        }

        [Test]
        public void SkipsDirectoryAndIgnoredEntries()
        {
            byte[] real = Encoding.ASCII.GetBytes("real");
            byte[] archive = BuildArchive(
                new SyntheticEntry("art", DatEntryFlags.Directory, Array.Empty<byte>(), Array.Empty<byte>()),
                new SyntheticEntry("art/hidden.art", DatEntryFlags.Plain | DatEntryFlags.Ignored, real, real),
                new SyntheticEntry("art/real.art", DatEntryFlags.Plain, real, real));

            using var dat = DatArchive.Open(new MemoryStream(archive));
            Assert.That(dat.TryGetEntry("art", out _), Is.False, "directory node is not a file");
            Assert.That(dat.TryGetEntry("art/hidden.art", out _), Is.False, "ignored entry is dropped");
            Assert.That(dat.TryGetEntry("art/real.art", out _), Is.True);
        }

        [Test]
        public void ReadsEmptyPayload()
        {
            byte[] archive = BuildArchive(
                new SyntheticEntry("empty.bin", DatEntryFlags.Plain, Array.Empty<byte>(), Array.Empty<byte>()));
            using var dat = DatArchive.Open(new MemoryStream(archive));
            Assert.That(dat.ReadAllBytes("empty.bin"), Is.Empty);
        }

        [Test]
        public void RejectsTruncatedArchive()
        {
            // Lop off the trailer (and part of the TOC); the last 12 bytes no longer carry a valid magic.
            byte[] payload = Encoding.ASCII.GetBytes("x");
            byte[] archive = BuildArchive(
                new SyntheticEntry("ok.txt", DatEntryFlags.Plain, payload, payload));
            Assert.Throws<DatFormatException>(() =>
            {
                using var _ = DatArchive.Open(new MemoryStream(archive, 0, archive.Length - 30));
            });
        }

        // --- synthetic archive construction -------------------------------------------------

        private readonly struct SyntheticEntry
        {
            public readonly string Path;
            public readonly DatEntryFlags Flags;
            public readonly byte[] Payload;
            public readonly byte[] Stored;

            public SyntheticEntry(string path, DatEntryFlags flags, byte[] payload, byte[] stored)
            {
                Path = path;
                Flags = flags;
                Payload = payload;
                Stored = stored;
            }
        }

        private static byte[] BuildArchive(params SyntheticEntry[] entries)
            => BuildArchive(DatArchive.MagicDat, entries);

        private static byte[] BuildArchive(uint magic, params SyntheticEntry[] entries)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

            // 1) Payload blocks at the front; remember each absolute offset.
            var offsets = new long[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                offsets[i] = ms.Position;
                w.Write(entries[i].Stored);
            }

            // 2) entryTableSize sits 4 bytes before the record block. We store absolute
            //    payload offsets, which means baseOffset must be 0, which means
            //    entryTableSize == record-block start == current position + 4.
            long recordBlockStart = ms.Position + 4;
            w.Write((int)recordBlockStart);

            // 3) Record block: count followed by one record per entry.
            w.Write(entries.Length);
            int nameTableSize = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                byte[] name = Encoding.ASCII.GetBytes(entries[i].Path);
                nameTableSize += name.Length;
                w.Write(name.Length);                         // nameSize
                w.Write(name);                                // name bytes
                w.Write(0);                                   // padding / reserved
                w.Write((int)entries[i].Flags);               // flags
                w.Write(entries[i].Payload.Length);           // uncompressedSize
                w.Write(entries[i].Stored.Length);            // compressedSize
                w.Write((uint)offsets[i]);                    // offset (absolute, baseOffset == 0)
            }

            // 4) Trailer (last 12 bytes): magic, nameTableSize, entryTableOffset.
            long fileSize = ms.Position + 12;
            int entryTableOffset = (int)(fileSize - recordBlockStart);
            w.Write(magic);                                   // magic " TAD" / "1TAD"
            w.Write(nameTableSize);
            w.Write(entryTableOffset);
            w.Flush();
            return ms.ToArray();
        }

        /// <summary>Produces a stock zlib stream: 0x78 0x9C header, raw DEFLATE body, Adler-32 trailer.</summary>
        private static byte[] ZlibCompress(byte[] data)
        {
            using var body = new MemoryStream();
            using (var deflate = new DeflateStream(body, CompressionLevel.Optimal, leaveOpen: true))
                deflate.Write(data, 0, data.Length);

            using var ms = new MemoryStream();
            ms.WriteByte(0x78);
            ms.WriteByte(0x9C);
            body.Position = 0;
            body.CopyTo(ms);
            uint adler = Adler32(data);
            ms.WriteByte((byte)(adler >> 24));
            ms.WriteByte((byte)(adler >> 16));
            ms.WriteByte((byte)(adler >> 8));
            ms.WriteByte((byte)adler);
            return ms.ToArray();
        }

        private static uint Adler32(byte[] data)
        {
            const uint mod = 65521;
            uint a = 1, b = 0;
            foreach (byte d in data)
            {
                a = (a + d) % mod;
                b = (b + a) % mod;
            }
            return (b << 16) | a;
        }
    }
}
