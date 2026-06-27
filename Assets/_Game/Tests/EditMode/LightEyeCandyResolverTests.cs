using Arcanum.Formats.Art;
using Arcanum.Formats.Text;
using NUnit.Framework;

namespace Arcanum.Formats.Tests
{
    /// <summary>LIGHT art-id → <c>art/light/*.art</c>, incl. the rotational <c>_s&lt;n&gt;</c> facing suffix.</summary>
    public sealed class LightArtResolverTests
    {
        // type 9 = LIGHT; num at >>19; bit 0 = rotational; bits 4–8 = the facing value (/8 → suffix).
        private static uint LightId(int num, int rotational, int facingRaw)
            => (9u << 28) | (((uint)num & 511) << 19) | ((uint)rotational & 1) | (((uint)facingRaw & 0x1F) << 4);

        private static LightArtResolver Build() => LightArtResolver.FromMes(MesReader.Read("{5}{torch}"));

        [Test]
        public void NonRotationalLight()
            => Assert.That(Build().Resolve(LightId(5, 0, 0)), Is.EqualTo("art/light/torch.art"));

        [Test]
        public void RotationalLightAppendsFacing()
            // facingRaw 16 → (16 & 0x1F) / 8 = 2 → "_s2"
            => Assert.That(Build().Resolve(LightId(5, 1, 16)), Is.EqualTo("art/light/torch_s2.art"));

        [Test]
        public void UnknownNumIsNull()
            => Assert.That(Build().Resolve(LightId(99, 0, 0)), Is.Null);

        [Test]
        public void NonLightIsNull()
            => Assert.That(Build().Resolve(2u << 28), Is.Null);
    }

    /// <summary>EYE_CANDY art-id → <c>art/eye_candy/&lt;name&gt;_&lt;F|B|U&gt;.art</c> by overlay type.</summary>
    public sealed class EyeCandyArtResolverTests
    {
        // type 14 = EYE_CANDY; num at >>19; overlay type at bits 6–7.
        private static uint EyeCandyId(int num, int type)
            => (14u << 28) | (((uint)num & 511) << 19) | (((uint)type & 3) << 6);

        private static EyeCandyArtResolver Build() => EyeCandyArtResolver.FromMes(MesReader.Read("{7}{fireball}"));

        [Test]
        public void ForegroundOverlay()
            => Assert.That(Build().Resolve(EyeCandyId(7, 0)), Is.EqualTo("art/eye_candy/fireball_f.art"));

        [Test]
        public void BackgroundOverlay()
            => Assert.That(Build().Resolve(EyeCandyId(7, 1)), Is.EqualTo("art/eye_candy/fireball_b.art"));

        [Test]
        public void Underlay()
            => Assert.That(Build().Resolve(EyeCandyId(7, 2)), Is.EqualTo("art/eye_candy/fireball_u.art"));

        [Test]
        public void NonEyeCandyIsNull()
            => Assert.That(Build().Resolve(2u << 28), Is.Null);

        [Test]
        public void UnknownNumIsNull()
            => Assert.That(Build().Resolve(EyeCandyId(99, 0)), Is.Null);
    }
}
