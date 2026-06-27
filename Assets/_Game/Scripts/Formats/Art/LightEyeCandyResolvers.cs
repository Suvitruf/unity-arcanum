using Arcanum.Formats.Text;

namespace Arcanum.Formats.Art
{
    /// <summary>
    /// Resolves a LIGHT art ID to <c>art/light/*.art</c> via <c>art/light/light.mes</c> (number → base name).
    /// Ports <c>a_name_light_aid_to_fname</c> (a_name.c:1851). A light id whose <b>bit 0</b> is set is
    /// "rotational" and appends a facing suffix <c>_s&lt;n&gt;</c> with <c>n = ((id &gt;&gt; 4) &amp; 0x1F) / 8</c>
    /// (engine <c>sub_504700</c>/<c>sub_504790</c>, tig art.c); otherwise the plain <c>&lt;name&gt;.art</c>.
    /// </summary>
    public sealed class LightArtResolver
    {
        private readonly MesFile _names;

        public LightArtResolver(MesFile lightMes) => _names = lightMes;

        public static LightArtResolver FromMes(MesFile lightMes) => new LightArtResolver(lightMes);

        public string Resolve(uint artId)
        {
            if (ArtId.Type(artId) != ArtId.TypeLight) return null;
            string name = _names.Get(ArtId.Num(artId)); // light uses the generic num (id>>19 & 511)
            if (string.IsNullOrEmpty(name)) return null;

            if ((artId & 1) != 0) // rotational light → per-facing variant
            {
                int facing = (int)((artId >> 4) & 0x1F) / 8;
                return ("art/light/" + name + "_s" + facing + ".art").ToLowerInvariant();
            }

            return ("art/light/" + name + ".art").ToLowerInvariant();
        }
    }

    /// <summary>
    /// Resolves an EYE_CANDY art ID (spell / projectile / effect overlay) to <c>art/eye_candy/*.art</c> via
    /// <c>art/eye_candy/eye_candy.mes</c>. Ports the EYE_CANDY case of <c>a_name_aid_to_fname</c> (name.c:1031):
    /// <c>art/eye_candy/&lt;name&gt;_&lt;T&gt;.art</c>, where the overlay <b>type</b> (bits 6–7) selects a code
    /// from <c>FBU</c> — <b>F</b>oreground / <b>B</b>ackground / <b>U</b>nderlay (engine
    /// <c>name_eye_candy_type_codes</c>).
    /// </summary>
    public sealed class EyeCandyArtResolver
    {
        private const string TypeCodes = "FBU"; // TIG_ART_EYE_CANDY_TYPE_{FOREGROUND,BACKGROUND}_OVERLAY / UNDERLAY

        private readonly MesFile _names;

        public EyeCandyArtResolver(MesFile eyeCandyMes) => _names = eyeCandyMes;

        public static EyeCandyArtResolver FromMes(MesFile eyeCandyMes) => new EyeCandyArtResolver(eyeCandyMes);

        public string Resolve(uint artId)
        {
            if (ArtId.Type(artId) != ArtId.TypeEyeCandy) return null;
            int type = (int)((artId >> 6) & 3);                    // EYE_CANDY_ID_TYPE_SHIFT=6, masked by TYPE_COUNT (3), as the engine does
            if (type < 0 || type >= TypeCodes.Length) return null; // engine would read OOB at type 3; we reject it
            string name = _names.Get(ArtId.Num(artId));
            if (string.IsNullOrEmpty(name)) return null;
            return ("art/eye_candy/" + name + "_" + TypeCodes[type] + ".art").ToLowerInvariant();
        }
    }
}
