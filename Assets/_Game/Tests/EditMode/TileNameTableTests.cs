using Arcanum.Formats.Text;
using Arcanum.Formats.Tiles;
using NUnit.Framework;

namespace Arcanum.Formats.Tests
{
    /// <summary>
    /// <see cref="TileNameTable"/>: bucketing by key range, the <c>/flags</c> + footstep-sound suffix parse, and
    /// the cross-bucket <see cref="TileNameTable.Order"/> used by blended-tile naming.
    /// </summary>
    public sealed class TileNameTableTests
    {
        // num/type/flippable per Lookup: type 1 = outdoor, 0 = indoor; flippable 1/0.
        private static TileNameTable Build()
        {
            string mes =
                "{0}{grs/sbn 3}" + // outdoor flippable[0]: sinkable|block|natural, sound 3
                "{1}{wtr/s 7}" +   // outdoor flippable[1]: sinkable, sound 7
                "{100}{roc/b}" +   // outdoor non-flippable[0]: block, no sound
                "{200}{flr5}" +    // indoor flippable[0]: no slash → name flr, sound 5
                "{300}{wal/f 2}";  // indoor non-flippable[0]: block|flyable, sound 2
            return TileNameTable.FromMes(MesReader.Read(mes));
        }

        [Test]
        public void BucketsByKeyRange()
        {
            var t = Build();
            Assert.That(t.Lookup(0, type: 1, flippable: 1), Is.EqualTo("grs"));
            Assert.That(t.Lookup(1, 1, 1), Is.EqualTo("wtr"));
            Assert.That(t.Lookup(0, 1, 0), Is.EqualTo("roc")); // outdoor non-flippable
            Assert.That(t.Lookup(0, 0, 1), Is.EqualTo("flr")); // indoor flippable
            Assert.That(t.Lookup(0, 0, 0), Is.EqualTo("wal")); // indoor non-flippable
            Assert.That(t.Lookup(99, 1, 1), Is.Null);          // out of range
        }

        [Test]
        public void ParsesFlags()
        {
            var t = Build();
            Assert.That(t.FlagsOf(0, 1, 1), Is.EqualTo(TileFlags.Sinkable | TileFlags.Block | TileFlags.Natural));
            Assert.That(t.FlagsOf(1, 1, 1), Is.EqualTo(TileFlags.Sinkable));
            Assert.That(t.FlagsOf(0, 1, 0), Is.EqualTo(TileFlags.Block));
            Assert.That(t.FlagsOf(0, 0, 1), Is.EqualTo(TileFlags.None));
            Assert.That(t.FlagsOf(0, 0, 0), Is.EqualTo(TileFlags.Block | TileFlags.Flyable)); // 'f'
        }

        [Test]
        public void ParsesFootstepSound()
        {
            var t = Build();
            Assert.That(t.SoundOf(0, 1, 1), Is.EqualTo(3)); // after flags
            Assert.That(t.SoundOf(1, 1, 1), Is.EqualTo(7));
            Assert.That(t.SoundOf(0, 1, 0), Is.EqualTo(0)); // flags but no sound
            Assert.That(t.SoundOf(0, 0, 1), Is.EqualTo(5)); // no-slash form
        }

        [Test]
        public void OrderIsOutdoorFlippableThenNonFlippable()
        {
            var t = Build();
            Assert.That(t.Order("grs"), Is.EqualTo(0));
            Assert.That(t.Order("wtr"), Is.EqualTo(1));
            Assert.That(t.Order("roc"), Is.EqualTo(2)); // 2 outdoor-flippable names precede it
            Assert.That(t.Order("zzz"), Is.EqualTo(-1));
        }
    }
}
