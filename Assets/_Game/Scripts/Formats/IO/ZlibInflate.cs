using System.IO;
using System.IO.Compression;

namespace Arcanum.Formats.IO
{
    /// <summary>
    /// Inflates zlib/DEFLATE payloads as produced by the original Arcanum tools.
    /// The engine compresses with stock zlib, so streams carry the 2-byte zlib
    /// header (CMF/FLG) and an Adler-32 trailer. <see cref="DeflateStream"/>
    /// only understands raw DEFLATE, so we detect and strip the zlib wrapper and
    /// let the trailing checksum fall off the end of the stream.
    /// </summary>
    public static class ZlibInflate
    {
        public static byte[] Inflate(byte[] data, int offset, int count, int expectedSize)
        {
            int start = offset;
            int length = count;

            if (count >= 2 && LooksLikeZlibHeader(data[offset], data[offset + 1]))
            {
                start += 2;   // skip CMF + FLG
                length -= 2;  // the 4-byte Adler-32 trailer is simply ignored by DeflateStream
            }

            using var input = new MemoryStream(data, start, length, writable: false);
            using var inflater = new DeflateStream(input, CompressionMode.Decompress);
            using var output = expectedSize > 0 ? new MemoryStream(expectedSize) : new MemoryStream();
            inflater.CopyTo(output);
            return output.ToArray();
        }

        /// <summary>
        /// A zlib header has DEFLATE as its compression method (low nibble 8) and
        /// the 16-bit big-endian CMF/FLG word is a multiple of 31.
        /// </summary>
        private static bool LooksLikeZlibHeader(byte cmf, byte flg)
        {
            if ((cmf & 0x0F) != 0x08) return false;
            int word = (cmf << 8) | flg;
            return word % 31 == 0;
        }
    }
}
