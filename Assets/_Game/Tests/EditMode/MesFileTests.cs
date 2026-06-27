using System.Collections.Generic;
using Arcanum.Formats.Text;
using NUnit.Framework;

namespace Arcanum.Formats.Tests
{
    /// <summary>
    /// <see cref="MesFile"/> lookups, range count, duplicate-key policy (last wins) and the <c>mes_merge</c>
    /// overlay (<see cref="MesFile.MergedWith"/>).
    /// </summary>
    public sealed class MesFileTests
    {
        private static MesFile Mes(params (int k, string v)[] entries)
        {
            var list = new List<KeyValuePair<int, string>>();
            foreach (var (k, v) in entries) list.Add(new KeyValuePair<int, string>(k, v));
            return new MesFile(list);
        }

        [Test]
        public void GetAndTryGet()
        {
            var mes = Mes((1, "a"), (5, "b"));
            Assert.That(mes.Get(1), Is.EqualTo("a"));
            Assert.That(mes.Get(99), Is.Null);
            Assert.That(mes.TryGet(5, out var v), Is.True);
            Assert.That(v, Is.EqualTo("b"));
            Assert.That(mes.TryGet(99, out _), Is.False);
        }

        [Test]
        public void EntriesPreserveFileOrder()
        {
            var mes = Mes((3, "c"), (1, "a"), (2, "b"));
            Assert.That(mes.Entries.Count, Is.EqualTo(3));
            Assert.That(mes.Entries[0].Key, Is.EqualTo(3));
            Assert.That(mes.Entries[2].Key, Is.EqualTo(2));
        }

        [Test]
        public void CountInRangeIsInclusive()
        {
            var mes = Mes((10, "a"), (15, "b"), (20, "c"), (25, "d"));
            Assert.That(mes.CountInRange(15, 20), Is.EqualTo(2));
            Assert.That(mes.CountInRange(0, 9), Is.EqualTo(0));
            Assert.That(mes.CountInRange(10, 25), Is.EqualTo(4));
        }

        [Test]
        public void DuplicateKeyLastWins()
        {
            var mes = Mes((1, "first"), (1, "second"));
            Assert.That(mes.Get(1), Is.EqualTo("second"));
        }

        [Test]
        public void MergedWithOverridesAndAppends()
        {
            var baseMes = Mes((1, "base-1"), (2, "base-2"));
            var overlay = Mes((2, "over-2"), (3, "over-3"));
            var merged = baseMes.MergedWith(overlay);

            Assert.That(merged.Get(1), Is.EqualTo("base-1")); // untouched
            Assert.That(merged.Get(2), Is.EqualTo("over-2")); // overridden
            Assert.That(merged.Get(3), Is.EqualTo("over-3")); // appended
            Assert.That(merged.Count, Is.EqualTo(3));
            // Override is in place — the base key keeps its position.
            Assert.That(merged.Entries[1].Key, Is.EqualTo(2));
            Assert.That(merged.Entries[1].Value, Is.EqualTo("over-2"));
        }

        [Test]
        public void MergedWithNullOverlayIsCopy()
        {
            var baseMes = Mes((1, "a"));
            var merged = baseMes.MergedWith(null);
            Assert.That(merged.Get(1), Is.EqualTo("a"));
            Assert.That(merged.Count, Is.EqualTo(1));
        }
    }
}
