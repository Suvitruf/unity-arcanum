using System.Collections.Generic;

namespace Arcanum.Formats.Art
{
    /// <summary>
    /// A single 24-bit palette colour. ART palettes store four bytes per entry
    /// in <c>R, G, B, X</c> order (the fourth byte is unused). Palette index 0
    /// is the engine's colour key and is treated as fully transparent.
    /// </summary>
    public readonly struct ArtColor
    {
        public readonly byte R;
        public readonly byte G;
        public readonly byte B;

        public ArtColor(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }
    }

    /// <summary>A 256-entry colour table. An .art file may carry up to four (alternate colour schemes).</summary>
    public sealed class ArtPalette
    {
        public const int ColorCount = 256;
        public readonly ArtColor[] Colors;

        public ArtPalette(ArtColor[] colors) => Colors = colors;
    }

    /// <summary>
    /// One decoded image frame: its dimensions, draw offsets, hotspot, and the
    /// raw 8-bit palette indices (length <c>Width * Height</c>, row-major,
    /// top-down). Index 0 is transparent.
    /// </summary>
    public sealed class ArtFrame
    {
        public int Width { get; }
        public int Height { get; }

        /// <summary>Anchor point within the frame, measured from the top-left in pixels.</summary>
        public int HotX { get; }
        public int HotY { get; }

        /// <summary>Per-frame draw offset (used to keep animations registered).</summary>
        public int OffsetX { get; }
        public int OffsetY { get; }

        /// <summary>Palette indices, <c>Width * Height</c> bytes, row-major, top row first.</summary>
        public byte[] Indices { get; }

        public ArtFrame(int width, int height, int hotX, int hotY, int offsetX, int offsetY, byte[] indices)
        {
            Width = width;
            Height = height;
            HotX = hotX;
            HotY = hotY;
            OffsetX = offsetX;
            OffsetY = offsetY;
            Indices = indices;
        }
    }

    /// <summary>One facing/rotation of an animation, holding <see cref="ArtFile.FramesPerRotation"/> frames.</summary>
    public sealed class ArtRotation
    {
        public readonly ArtFrame[] Frames;

        public ArtRotation(ArtFrame[] frames) => Frames = frames;
    }

    /// <summary>
    /// A fully decoded Arcanum <c>.art</c> asset — tiles, items, scenery, UI
    /// graphics and directional creature animations all use this format.
    /// </summary>
    public sealed class ArtFile
    {
        public uint Flags { get; }
        public int Fps { get; }

        /// <summary>Frame index at which an action (e.g. a weapon hit) fires during the animation.</summary>
        public int ActionFrame { get; }

        /// <summary>Number of frames in each rotation.</summary>
        public int FramesPerRotation { get; }

        /// <summary>Either 1 (tiles, items, single-facing) or 8 (directional creatures/scenery).</summary>
        public int RotationCount => Rotations.Count;

        /// <summary>True when the asset has a single facing (<c>flags &amp; 0x01</c>).</summary>
        public bool IsSingleRotation => (Flags & 0x01) != 0;

        public IReadOnlyList<ArtPalette> Palettes { get; }
        public IReadOnlyList<ArtRotation> Rotations { get; }

        /// <summary>The default colour table (palette 0), or null if the file carried none.</summary>
        public ArtPalette PrimaryPalette => Palettes.Count > 0 ? Palettes[0] : null;

        public ArtFile(uint flags, int fps, int actionFrame, int framesPerRotation,
            IReadOnlyList<ArtPalette> palettes, IReadOnlyList<ArtRotation> rotations)
        {
            Flags = flags;
            Fps = fps;
            ActionFrame = actionFrame;
            FramesPerRotation = framesPerRotation;
            Palettes = palettes;
            Rotations = rotations;
        }
    }
}
