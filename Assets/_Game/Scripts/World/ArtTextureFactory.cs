using System;
using Arcanum.Formats.Art;
using UnityEngine;

namespace Arcanum.Runtime.Art
{
    /// <summary>
    /// Converts decoded <see cref="ArtFrame"/> data into Unity <see cref="Texture2D"/>
    /// and <see cref="Sprite"/> objects. ART stores rows top-down with palette index 0
    /// as the transparent colour key; Unity textures are bottom-up, so rows are flipped
    /// here and index-0 pixels are written fully transparent.
    /// The original world tiles are drawn on isometric pixel grid (78×40).
    /// </summary>
    public static class ArtTextureFactory
    {
        private const int TransparentIndex = 0;

        /// <summary>When set, <see cref="CreateSprite"/> packs frames into this shared atlas instead of giving
        /// each sprite its own texture — so sprites batch (sorting unchanged). Null = one texture per sprite.</summary>
        public static RuntimeSpriteAtlas ActiveAtlas;

        // Decodes a frame to bottom-up RGBA32 with index 0 = transparent. mirrorX mirrors the pixels in place
        // (engine TIG_ART_BLT_FLIP_X) keeping the hotspot/rect — unlike SpriteRenderer.flipX which shifts an
        // off-centre pivot.
        private static Color32[] BuildPixels(ArtFrame frame, ArtPalette palette, bool mirrorX, out int w, out int h)
        {
            int width = frame.Width, height = frame.Height;
            w = Mathf.Max(width, 1);
            h = Mathf.Max(height, 1);

            var pixels = new Color32[w * h];
            ArtColor[] colors = palette.Colors;
            for (int y = 0; y < height; y++)
            {
                int srcRow = y * width;
                int dstRow = (height - 1 - y) * width; // vertical flip: top-down → bottom-up
                for (int x = 0; x < width; x++)
                {
                    byte index = frame.Indices[srcRow + x];
                    int dstX = mirrorX ? (width - 1 - x) : x;
                    pixels[dstRow + dstX] = index == TransparentIndex
                        ? new Color32(0, 0, 0, 0)
                        : new Color32(colors[index].R, colors[index].G, colors[index].B, 255);
                }
            }

            return pixels;
        }

        public static Texture2D CreateTexture(ArtFrame frame, ArtPalette palette, bool mirrorX = false)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (palette == null) throw new ArgumentNullException(nameof(palette));

            Color32[] pixels = BuildPixels(frame, palette, mirrorX, out int w, out int h);
            // Point filtering, NO mipmaps: each sprite renders full-resolution so its shape stays true at every
            // zoom (mipmaps averaged pixel art into a blurry blob between zoom levels — worse).
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "ArtFrame", };
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false);
            return texture;
        }

        /// <summary>
        /// Builds a sprite for one frame. Pixels map 1:1 to Unity units at the given
        /// <paramref name="pixelsPerUnit"/>, and the pivot is placed on the frame's
        /// hotspot so animations and world placement stay registered.
        /// </summary>
        public static Sprite CreateSprite(ArtFrame frame, ArtPalette palette, float pixelsPerUnit = 100f, Vector2? pivotOverride = null, bool mirrorX = false)
        {
            if (frame == null)
                throw new ArgumentNullException(nameof(frame));

            if (palette == null)
                throw new ArgumentNullException(nameof(palette));

            int width = Mathf.Max(frame.Width, 1);
            int height = Mathf.Max(frame.Height, 1);

            // Hotspot is top-left based; convert to Unity's bottom-left normalized pivot. Not clamped: roof art
            // uses negative hotspots that anchor outside the sprite. For a mirrored (flipped) piece the engine
            // zeroes hot_x (tig_art_frame_data), so the piece's LEFT edge sits at the anchor.
            Vector2 pivot = pivotOverride ?? new Vector2(
                mirrorX ? 0f : (width > 0 ? frame.HotX / (float)width : 0.5f),
                height > 0 ? (height - frame.HotY) / (float)height : 0.5f);

            // Shared atlas (batching): pack into a page and return a sub-rect sprite. Falls back to a standalone
            // texture if no atlas is active or the frame is too big to pack. Sorting is unaffected either way.
            if (ActiveAtlas != null)
            {
                Color32[] px = BuildPixels(frame, palette, mirrorX, out int aw, out int ah);
                Sprite atlased = ActiveAtlas.Add(px, aw, ah, pivot, pixelsPerUnit);
                if (atlased != null) return atlased;
            }

            Texture2D texture = CreateTexture(frame, palette, mirrorX);
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), pivot, pixelsPerUnit,
                extrude: 0, meshType: SpriteMeshType.FullRect);
            sprite.name = "ArtSprite";
            return sprite;
        }
    }
}
