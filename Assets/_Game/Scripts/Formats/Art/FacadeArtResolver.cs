using Arcanum.Formats.Text;

namespace Arcanum.Formats.Art
{
    /// <summary>
    /// Resolves a FACADE art ID (TIG type 11) to its <c>art/Facade/*.art</c> path via
    /// <c>art/facade/facadename.mes</c>. Facades are large buildings stored in the sector <b>tile layer</b>
    /// (not the object list): each building tile holds a facade art id whose <c>num</c> selects the building
    /// art and whose <c>frame</c> selects the 78×40 piece drawn at that tile (engine <c>tile_draw_iso</c>).
    /// Bit layout from tig <c>art.c</c> (<c>tig_art_facade_id_*</c>): num is split — low 8 bits at bit 17,
    /// +256 if bit 27 is set; frame is bits 1–10; walkable is bit 0.
    /// </summary>
    public sealed class FacadeArtResolver
    {
        private readonly MesFile _names; // key = facade num, value = file name (no extension)

        public FacadeArtResolver(MesFile facadeNameMes) => _names = facadeNameMes;

        public static FacadeArtResolver FromMes(MesFile facadeNameMes) => new FacadeArtResolver(facadeNameMes);

        /// <summary>The facade building number (engine <c>tig_art_facade_id_num_get</c>).</summary>
        public static int Num(uint artId)
        {
            int num = (int)((artId >> 17) & 0xFF);
            if ((artId & (1u << 27)) != 0) num += 256;
            return num;
        }

        /// <summary>The frame (building piece) drawn at this tile (engine <c>tig_art_facade_id_frame_get</c>).</summary>
        public static int Frame(uint artId) => (int)((artId >> 1) & 0x3FF);

        /// <summary>True if this facade tile is walkable (engine <c>tig_art_facade_id_walkable_get</c> — bit 0).</summary>
        public static bool Walkable(uint artId) => (artId & 1u) != 0;

        /// <summary>The virtual path (e.g. <c>art/facade/bateshouse-01.art</c>), or null if unresolvable.</summary>
        public string Resolve(uint artId)
        {
            if (ArtId.Type(artId) != ArtId.TypeFacade) return null;
            string name = _names.Get(Num(artId));
            if (string.IsNullOrEmpty(name)) return null;
            return ("art/facade/" + name + ".art").ToLowerInvariant();
        }
    }
}
