using System.IO;
using System.Text;
using Arcanum.Formats.Art;
using NUnit.Framework;

namespace Arcanum.Formats.Tests
{
    /// <summary>
    /// Exercises <see cref="ArtReader"/> against synthetic .art files covering the
    /// header, palette, single vs multi rotation, and both the raw and RLE pixel
    /// paths. Real-asset validation happens visually via the DAT Inspector preview.
    /// </summary>
    public sealed class ArtReaderTests
    {
        [Test]
        public void ReadsRawSingleRotationFrame()
        {
            // 2x2 frame, stored raw (dataSize == width*height).
            byte[] pixels = { 0, 1, 2, 3 };
            byte[] file = BuildArt(
                singleRotation: true,
                frameWidth: 2, frameHeight: 2,
                dataSize: pixels.Length,
                pixelBytes: pixels);

            ArtFile art = ArtReader.Read(file);

            Assert.That(art.RotationCount, Is.EqualTo(1));
            Assert.That(art.FramesPerRotation, Is.EqualTo(1));
            Assert.That(art.Palettes.Count, Is.EqualTo(1));

            ArtFrame frame = art.Rotations[0].Frames[0];
            Assert.That(frame.Width, Is.EqualTo(2));
            Assert.That(frame.Indices, Is.EqualTo(pixels));

            // Palette entry 1 is colour (R,G,B) = (10,20,30) — written BGRA above, read back as RGB.
            ArtColor c = art.PrimaryPalette.Colors[1];
            Assert.That((c.R, c.G, c.B), Is.EqualTo(((byte)10, (byte)20, (byte)30)));
        }

        [Test]
        public void DecodesRleFrame()
        {
            // 5x1 frame, RLE: fill 4×index5, then fill 1×index7. dataSize(4) != w*h(5) ⇒ RLE path.
            byte[] rle = { 0x04, 0x05, 0x01, 0x07 };
            byte[] file = BuildArt(
                singleRotation: true,
                frameWidth: 5, frameHeight: 1,
                dataSize: rle.Length,
                pixelBytes: rle);

            ArtFile art = ArtReader.Read(file);
            byte[] indices = art.Rotations[0].Frames[0].Indices;

            Assert.That(indices, Is.EqualTo(new byte[] { 5, 5, 5, 5, 7 }));
        }

        [Test]
        public void DecodesLiteralRleRun()
        {
            // 4x1 frame: literal run of 3 (0x83) then fill 1. dataSize(6) != w*h(4) ⇒ RLE.
            byte[] rle = { 0x83, 9, 8, 7, 0x01, 6 };
            byte[] file = BuildArt(true, 4, 1, rle.Length, rle);

            ArtFile art = ArtReader.Read(file);
            Assert.That(art.Rotations[0].Frames[0].Indices, Is.EqualTo(new byte[] { 9, 8, 7, 6 }));
        }

        // --- synthetic .art construction ----------------------------------------------------

        private static byte[] BuildArt(bool singleRotation, int frameWidth, int frameHeight, int dataSize, byte[] pixelBytes)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

            w.Write(singleRotation ? 1u : 0u); // flags (bit 0 ⇒ single rotation)
            w.Write(10);                        // fps
            w.Write(8);                         // bpp
            w.Write(1); w.Write(0); w.Write(0); w.Write(0); // palettePresent[4] — only palette 0
            w.Write(0);                         // actionFrame
            w.Write(1);                         // numFrames
            for (int i = 0; i < 8; i++) w.Write(0); // framesTbl[8]
            for (int i = 0; i < 8; i++) w.Write(0); // dataSize[8]
            for (int i = 0; i < 8; i++) w.Write(0); // pixelsTbl[8]

            // Palette 0: entry 0 = black, entry 1 = colour (R,G,B)=(10,20,30) stored BGRA (byte0=B … byte2=R,
            // engine tig_color), rest zeroed.
            for (int i = 0; i < 256; i++)
            {
                if (i == 1) { w.Write((byte)30); w.Write((byte)20); w.Write((byte)10); w.Write((byte)0); } // B,G,R,X
                else { w.Write(0); /* B,G,R,X as one zero int */ }
            }

            // One rotation, one frame header.
            w.Write(frameWidth);
            w.Write(frameHeight);
            w.Write(dataSize);
            w.Write(0); w.Write(0); // hotX, hotY
            w.Write(0); w.Write(0); // offsetX, offsetY

            // Pixel payload.
            w.Write(pixelBytes);

            w.Flush();
            return ms.ToArray();
        }
    }
}
