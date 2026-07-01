using System.Collections.Generic;
using UnityEngine;

namespace Arcanum.Runtime.Art
{
    /// <summary>
    /// A growing runtime texture atlas with a simple shelf packer. Frames decoded from <c>.art</c> are blitted
    /// into shared pages, so the sprites referencing them share ONE texture — which lets Unity's 2D batcher
    /// merge contiguous same-page <see cref="SpriteRenderer"/>s into far fewer draw calls.
    ///
    /// IMPORTANT: sorting is untouched. Each sprite is still its own SpriteRenderer with its own
    /// sortingOrder/Z — only the underlying texture is shared, so character-vs-scenery ordering is unchanged.
    /// (Batching only merges sprites that are *contiguous in the sort order* and share a page; a different-page
    /// sprite interleaving by depth simply breaks the run, which is correct.)
    ///
    /// Pages upload lazily via <see cref="ApplyDirty"/> (call once per frame; free when nothing changed). An
    /// oversized frame returns null from <see cref="Add"/> so the caller falls back to a standalone texture.
    /// TODO (perf): each dirty page re-uploads in full on Apply; a GPU-side <c>Graphics.CopyTexture</c> blit per
    /// frame would avoid that. Fine for now since adds happen mostly during (masked) loading/streaming.
    /// </summary>
    public sealed class RuntimeSpriteAtlas
    {
        private const int PageSize = 1024; // 4 MB/page (RGBA32); most critter/scenery frames are well under this
        private const int Pad = 1;         // 1 px gutter between packed frames

        private readonly List<Texture2D> _pages = new List<Texture2D>();
        private readonly List<bool> _dirty = new List<bool>();
        private int _x, _y, _shelfH; // shelf-packing cursor on the current (last) page

        /// <summary>Packs a frame's pixels into a page and returns a sprite over that sub-rect, or null if the
        /// frame is too big to atlas (caller should fall back to a standalone texture).</summary>
        public Sprite Add(Color32[] pixels, int w, int h, Vector2 pivot, float ppu)
        {
            if (pixels == null || w <= 0 || h <= 0 || w > PageSize || h > PageSize) return null;
            if (_pages.Count == 0) NewPage();

            int aw = w + Pad, ah = h + Pad;
            if (_x + aw > PageSize)
            {
                _x = 0;
                _y += _shelfH;
                _shelfH = 0;
            } // wrap to next shelf

            if (_y + ah > PageSize) NewPage(); // shelf overflows the page → new page

            int page = _pages.Count - 1;
            Texture2D tex = _pages[page];
            tex.SetPixels32(_x, _y, w, h, pixels);
            _dirty[page] = true;

            var sprite = Sprite.Create(tex, new Rect(_x, _y, w, h), pivot, ppu, 0, SpriteMeshType.FullRect);
            sprite.name = "AtlasSprite";

            _x += aw;
            if (ah > _shelfH) _shelfH = ah;
            return sprite;
        }

        /// <summary>Uploads any pages written since the last call (cheap — a flag check — when nothing changed).</summary>
        public void ApplyDirty()
        {
            for (int i = 0; i < _pages.Count; i++)
                if (_dirty[i])
                {
                    _pages[i].Apply(false);
                    _dirty[i] = false;
                }
        }

        private void NewPage()
        {
            var tex = new Texture2D(PageSize, PageSize, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Point, // pixel-art: no blur, no cross-sprite bleed under point sampling
                wrapMode = TextureWrapMode.Clamp,
                name = $"SpriteAtlas{_pages.Count}",
            };
            tex.SetPixels32(new Color32[PageSize * PageSize]); // clear to transparent (one-time per page)
            _pages.Add(tex);
            _dirty.Add(true);
            _x = 0;
            _y = 0;
            _shelfH = 0;
        }
    }
}
