using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Arcanum.Formats.Art
{
    /// <summary>
    /// Decodes Arcanum <c>.art</c> images into <see cref="ArtFile"/> instances.
    ///
    /// On-disk layout (little-endian), reconstructed from alexbatalov/tig
    /// <c>art.c</c> (<c>art_read_header</c> / <c>sub_51B710</c>):
    /// <code>
    /// header (132 bytes):
    ///   flags(u32) fps(i32) bpp(i32)
    ///   palettePresent[4](i32)        // non-zero ⇒ that palette is stored
    ///   actionFrame(i32) numFrames(i32)
    ///   framesTbl[8](i32)  -- ignored  dataSize[8](i32) -- ignored  pixelsTbl[8](i32) -- ignored
    /// palettes:    for each present palette, 256 × (R,G,B,X) bytes
    /// frame heads: for each rotation, numFrames × { width,height,dataSize,hotX,hotY,offsetX,offsetY } (i32 ×7)
    /// pixel data:  for each rotation, for each frame: raw indices if dataSize==w*h, else RLE
    /// </code>
    /// Rotation count is 1 when <c>flags &amp; 0x01</c>, otherwise 8. Only 8-bpp
    /// palette-indexed art exists in the shipped game.
    /// </summary>
    public static class ArtReader
    {
        private const int PaletteSlots = 4;
        private const int RotationSlots = 8;
        private const int FrameHeaderSize = 0x1C; // 28 bytes

        public static ArtFile Read(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            using var stream = new MemoryStream(bytes, writable: false);
            return Read(stream);
        }

        public static ArtFile Read(Stream stream)
        {
            using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

            uint flags = reader.ReadUInt32();
            int fps = reader.ReadInt32();
            int bpp = reader.ReadInt32();
            if (bpp != 8)
                throw new DatFormatException($".art has unsupported bpp={bpp}; only 8-bpp palette-indexed art is supported");

            var palettePresent = new bool[PaletteSlots];
            for (int i = 0; i < PaletteSlots; i++)
                palettePresent[i] = reader.ReadInt32() != 0;

            int actionFrame = reader.ReadInt32();
            int numFrames = reader.ReadInt32();
            if (numFrames < 0 || numFrames > 0xFFFF)
                throw new DatFormatException($".art has an implausible frame count ({numFrames})");

            Skip(reader, 4 * RotationSlots); // framesTbl — runtime pointers, not used on disk
            Skip(reader, 4 * RotationSlots); // dataSize  — recomputed per frame below
            Skip(reader, 4 * RotationSlots); // pixelsTbl — runtime pointers, not used on disk

            var palettes = new List<ArtPalette>();
            for (int i = 0; i < PaletteSlots; i++)
                if (palettePresent[i])
                    palettes.Add(ReadPalette(reader));

            int rotationCount = (flags & 0x01) != 0 ? 1 : RotationSlots;

            // Frame headers for every rotation come first, then all pixel data.
            var frameHeaders = new FrameHeader[rotationCount][];
            for (int rot = 0; rot < rotationCount; rot++)
            {
                frameHeaders[rot] = new FrameHeader[numFrames];
                for (int f = 0; f < numFrames; f++)
                    frameHeaders[rot][f] = ReadFrameHeader(reader);
            }

            var rotations = new ArtRotation[rotationCount];
            for (int rot = 0; rot < rotationCount; rot++)
            {
                var frames = new ArtFrame[numFrames];
                for (int f = 0; f < numFrames; f++)
                {
                    FrameHeader h = frameHeaders[rot][f];
                    byte[] indices = DecodePixels(reader, h.Width, h.Height, h.DataSize);
                    frames[f] = new ArtFrame(h.Width, h.Height, h.HotX, h.HotY, h.OffsetX, h.OffsetY, indices);
                }
                rotations[rot] = new ArtRotation(frames);
            }

            return new ArtFile(flags, fps, actionFrame, numFrames, palettes, rotations);
        }

        private static ArtPalette ReadPalette(BinaryReader reader)
        {
            var colors = new ArtColor[ArtPalette.ColorCount];
            for (int i = 0; i < ArtPalette.ColorCount; i++)
            {
                // Palette entries are little-endian tig_color (BGRA): byte 0 is blue, byte 2 is red.
                byte b = reader.ReadByte();
                byte g = reader.ReadByte();
                byte r = reader.ReadByte();
                reader.ReadByte(); // unused 4th byte
                colors[i] = new ArtColor(r, g, b);
            }
            return new ArtPalette(colors);
        }

        private static FrameHeader ReadFrameHeader(BinaryReader reader)
        {
            return new FrameHeader
            {
                Width = reader.ReadInt32(),
                Height = reader.ReadInt32(),
                DataSize = reader.ReadInt32(),
                HotX = reader.ReadInt32(),
                HotY = reader.ReadInt32(),
                OffsetX = reader.ReadInt32(),
                OffsetY = reader.ReadInt32(),
            };
        }

        /// <summary>
        /// Reconstructs one frame's palette indices. When <paramref name="dataSize"/>
        /// equals the pixel count the data is stored raw; otherwise it is RLE-encoded:
        /// each control byte's low 7 bits are a length; bit 0x80 selects a literal run
        /// (copy that many indices verbatim) versus a fill run (next byte repeated).
        /// </summary>
        private static byte[] DecodePixels(BinaryReader reader, int width, int height, int dataSize)
        {
            if (width < 0 || height < 0)
                throw new DatFormatException($".art frame has negative dimensions ({width}x{height})");

            int pixelCount = width * height;
            var output = new byte[pixelCount];

            if (dataSize == pixelCount)
            {
                ReadExactly(reader, output, 0, pixelCount);
                return output;
            }
            if (dataSize <= 0)
                return output; // fully transparent / empty frame

            int outPos = 0;
            int consumed = 0;
            while (consumed < dataSize)
            {
                byte control = reader.ReadByte();
                int len = control & 0x7F;

                if ((control & 0x80) != 0)
                {
                    GuardRun(outPos, len, pixelCount);
                    ReadExactly(reader, output, outPos, len);
                    consumed += 1 + len;
                }
                else
                {
                    byte color = reader.ReadByte();
                    GuardRun(outPos, len, pixelCount);
                    for (int i = 0; i < len; i++) output[outPos + i] = color;
                    consumed += 2;
                }

                outPos += len;
            }

            return output;
        }

        private static void GuardRun(int outPos, int len, int pixelCount)
        {
            if (outPos + len > pixelCount)
                throw new DatFormatException(
                    $".art RLE run overflows frame ({outPos}+{len} > {pixelCount}); file is corrupt or misparsed");
        }

        private static void Skip(BinaryReader reader, int count)
            => reader.BaseStream.Seek(count, SeekOrigin.Current);

        private static void ReadExactly(BinaryReader reader, byte[] buffer, int offset, int count)
        {
            int read = 0;
            while (read < count)
            {
                int n = reader.Read(buffer, offset + read, count - read);
                if (n <= 0) throw new EndOfStreamException($".art ended early: wanted {count} bytes, got {read}");
                read += n;
            }
        }

        private struct FrameHeader
        {
            public int Width;
            public int Height;
            public int DataSize;
            public int HotX;
            public int HotY;
            public int OffsetX;
            public int OffsetY;
        }
    }
}
