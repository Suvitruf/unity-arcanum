using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Arcanum.Formats.IO;
using NUnit.Framework;

namespace Arcanum.Formats.Tests
{
    /// <summary>
    /// Exercises <see cref="ZlibInflate"/>: the engine stores zlib-wrapped payloads (2-byte header + Adler-32),
    /// but the API also accepts header-less raw DEFLATE and an arbitrary offset/length window.
    /// </summary>
    public sealed class ZlibInflateTests
    {
        [Test]
        public void InflatesZlibWrappedStream()
        {
            byte[] data = Encoding.ASCII.GetBytes(new string('Q', 5000) + "-tail");
            byte[] zlib = ZlibWrap(data);
            Assert.That(ZlibInflate.Inflate(zlib, 0, zlib.Length, data.Length), Is.EqualTo(data));
        }

        [Test]
        public void InflatesFromOffsetWithinBuffer()
        {
            byte[] data = Encoding.ASCII.GetBytes("offset payload");
            byte[] zlib = ZlibWrap(data);
            byte[] buffer = new byte[7 + zlib.Length + 5];
            Array.Copy(zlib, 0, buffer, 7, zlib.Length); // embed at offset 7, junk on both sides
            Assert.That(ZlibInflate.Inflate(buffer, 7, zlib.Length, data.Length), Is.EqualTo(data));
        }

        [Test]
        public void InflatesRawDeflateWithoutZlibHeader()
        {
            // A raw stored DEFLATE block leads with 0x01 (BFINAL=1, BTYPE=stored) — not a zlib header — so the
            // header sniff must NOT strip two bytes.
            byte[] data = Encoding.ASCII.GetBytes("raw deflate, no wrapper");
            byte[] raw = StoredDeflate(data);
            Assert.That(ZlibInflate.Inflate(raw, 0, raw.Length, 0), Is.EqualTo(data));
        }

        [Test]
        public void WorksWithoutExpectedSizeHint()
        {
            byte[] data = Encoding.ASCII.GetBytes("no size hint");
            byte[] zlib = ZlibWrap(data);
            Assert.That(ZlibInflate.Inflate(zlib, 0, zlib.Length, 0), Is.EqualTo(data));
        }

        // --- helpers ---

        private static byte[] ZlibWrap(byte[] data)
        {
            using var body = new MemoryStream();
            using (var deflate = new DeflateStream(body, CompressionLevel.Optimal, leaveOpen: true))
                deflate.Write(data, 0, data.Length);

            using var ms = new MemoryStream();
            ms.WriteByte(0x78);
            ms.WriteByte(0x9C); // zlib header (CMF/FLG, %31 == 0)
            body.Position = 0;
            body.CopyTo(ms);
            uint adler = Adler32(data);
            ms.WriteByte((byte)(adler >> 24));
            ms.WriteByte((byte)(adler >> 16));
            ms.WriteByte((byte)(adler >> 8));
            ms.WriteByte((byte)adler);
            return ms.ToArray();
        }

        // A single final stored (uncompressed) DEFLATE block — valid raw DEFLATE with a deterministic 0x01 lead.
        private static byte[] StoredDeflate(byte[] data)
        {
            if (data.Length > 0xFFFF) throw new ArgumentException("stored-block payload must be <= 65535 bytes");
            using var ms = new MemoryStream();
            ms.WriteByte(0x01); // BFINAL=1, BTYPE=00 (stored)
            ushort len = (ushort)data.Length;
            ms.WriteByte((byte)(len & 0xFF));
            ms.WriteByte((byte)(len >> 8));
            ushort nlen = (ushort)~len;
            ms.WriteByte((byte)(nlen & 0xFF));
            ms.WriteByte((byte)(nlen >> 8));
            ms.Write(data, 0, data.Length);
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
