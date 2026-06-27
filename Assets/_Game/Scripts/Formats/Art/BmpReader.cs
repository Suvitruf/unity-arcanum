using System;

namespace Arcanum.Formats.Art
{
    /// <summary>A decoded BMP image: 32-bit RGBA pixels, row 0 = bottom (Unity texture order).</summary>
    public readonly struct BmpImage
    {
        public readonly int Width;
        public readonly int Height;
        /// <summary>RGBA32, length <c>Width*Height*4</c>, bottom-up (row 0 is the bottom row).</summary>
        public readonly byte[] Rgba;

        public BmpImage(int width, int height, byte[] rgba)
        {
            Width = width;
            Height = height;
            Rgba = rgba;
        }
    }

    /// <summary>
    /// Minimal reader for the uncompressed Windows BMPs the engine ships (the world-map chunks under
    /// <c>WorldMap/</c> are 8-bit palettized: 54-byte BITMAPFILEHEADER+BITMAPINFOHEADER, a 256-colour BGRA
    /// palette, then bottom-up rows padded to 4 bytes). Also handles 24/32-bit BMPs. No Unity dependency —
    /// returns raw RGBA for a runtime helper to upload into a <c>Texture2D</c>.
    /// </summary>
    public static class BmpReader
    {
        public static BmpImage Read(byte[] b)
        {
            if (b == null || b.Length < 54 || b[0] != (byte)'B' || b[1] != (byte)'M')
                throw new ArgumentException("not a BMP (missing 'BM' signature)");

            int dataOffset = I32(b, 10);
            int headerSize = I32(b, 14);
            int width = I32(b, 18);
            int rawHeight = I32(b, 22);
            int bpp = U16(b, 28);
            int compression = I32(b, 30);
            if (compression != 0)
                throw new NotSupportedException($"compressed BMP (BI_{compression}) not supported");
            if (bpp != 8 && bpp != 24 && bpp != 32)
                throw new NotSupportedException($"{bpp}-bpp BMP not supported");

            bool topDown = rawHeight < 0;          // negative height = rows stored top-to-bottom
            int height = Math.Abs(rawHeight);
            if (width <= 0 || height <= 0) throw new ArgumentException("bad BMP dimensions");

            // Palette (8-bpp): immediately after the DIB header, BGRA per entry. paletteCount can be in the
            // header (0 ⇒ 256 for 8-bpp); we read what fits between the header end and the pixel data.
            byte[] palette = null;
            if (bpp == 8)
            {
                int paletteStart = 14 + headerSize;
                int paletteEntries = (dataOffset - paletteStart) / 4;
                if (paletteEntries < 1) paletteEntries = 256;
                palette = new byte[paletteEntries * 4];
                Buffer.BlockCopy(b, paletteStart, palette, 0, Math.Min(palette.Length, b.Length - paletteStart));
            }

            int bytesPerPixel = bpp / 8;
            int rowSize = ((width * bpp + 31) / 32) * 4; // rows padded up to a 4-byte boundary
            var rgba = new byte[width * height * 4];

            for (int srcRow = 0; srcRow < height; srcRow++)
            {
                int rowStart = dataOffset + srcRow * rowSize;
                // BMP positive-height rows are bottom-up, which already matches Unity's row 0 = bottom; a
                // top-down BMP must be flipped so the bottom row lands in dst row 0.
                int dstRow = topDown ? (height - 1 - srcRow) : srcRow;
                int dst = dstRow * width * 4;
                for (int x = 0; x < width; x++)
                {
                    int sp = rowStart + x * bytesPerPixel;
                    byte r, g, bl;
                    if (bpp == 8)
                    {
                        int idx = b[sp] * 4;
                        bl = palette[idx]; g = palette[idx + 1]; r = palette[idx + 2]; // palette is BGRA
                    }
                    else
                    {
                        bl = b[sp]; g = b[sp + 1]; r = b[sp + 2]; // pixel is BGR(A)
                    }
                    int d = dst + x * 4;
                    rgba[d] = r; rgba[d + 1] = g; rgba[d + 2] = bl; rgba[d + 3] = 255;
                }
            }
            return new BmpImage(width, height, rgba);
        }

        private static int I32(byte[] b, int o) => b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24);
        private static int U16(byte[] b, int o) => b[o] | (b[o + 1] << 8);
    }
}
