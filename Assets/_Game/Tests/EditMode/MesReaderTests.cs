using Arcanum.Formats.Text;
using NUnit.Framework;

namespace Arcanum.Formats.Tests
{
    /// <summary>
    /// Parsing of the <c>.mes</c> brace grammar: <c>{key}{value}</c> pairs, text/comments outside braces ignored,
    /// values taken literally (incl. embedded newlines), and the Latin-1 byte decode.
    /// </summary>
    public sealed class MesReaderTests
    {
        [Test]
        public void ParsesKeyValuePairs()
        {
            var mes = MesReader.Read("{1}{hello}{2}{world}");
            Assert.That(mes.Count, Is.EqualTo(2));
            Assert.That(mes.Get(1), Is.EqualTo("hello"));
            Assert.That(mes.Get(2), Is.EqualTo("world"));
        }

        [Test]
        public void IgnoresTextAndCommentsOutsideBraces()
        {
            var mes = MesReader.Read("// a comment\n{1}{a}  garbage between  {2}{b}\n// trailing");
            Assert.That(mes.Count, Is.EqualTo(2));
            Assert.That(mes.Get(1), Is.EqualTo("a"));
            Assert.That(mes.Get(2), Is.EqualTo("b"));
        }

        [Test]
        public void PreservesWhitespaceAndNewlinesInsideValue()
        {
            var mes = MesReader.Read("{10}{ line one\nline two }");
            Assert.That(mes.Get(10), Is.EqualTo(" line one\nline two "));
        }

        [Test]
        public void HandlesEmptyValue()
        {
            var mes = MesReader.Read("{5}{}");
            Assert.That(mes.TryGet(5, out var v), Is.True);
            Assert.That(v, Is.EqualTo(string.Empty));
        }

        [Test]
        public void DiscardsUnterminatedFinalField()
        {
            var mes = MesReader.Read("{1}{ok}{2}{oops");
            Assert.That(mes.Count, Is.EqualTo(1));
            Assert.That(mes.Get(1), Is.EqualTo("ok"));
        }

        [Test]
        public void SkipsNonIntegerKey()
        {
            var mes = MesReader.Read("{abc}{value}{7}{kept}");
            Assert.That(mes.Get(7), Is.EqualTo("kept"));
            Assert.That(mes.TryGet(0, out _), Is.False, "non-numeric key must not inject a key-0 entry");
        }

        [Test]
        public void TrimsKeyWhitespace()
        {
            var mes = MesReader.Read("{  42  }{answer}");
            Assert.That(mes.Get(42), Is.EqualTo("answer"));
        }

        [Test]
        public void DecodesBytesAsLatin1()
        {
            // 0xE9 = 'é' in Latin-1/Windows-1252 — single-byte text must round-trip to U+00E9.
            byte[] bytes = { (byte)'{', (byte)'1', (byte)'}', (byte)'{', 0xE9, (byte)'}' };
            var mes = MesReader.Read(bytes);
            Assert.That(mes.Get(1), Is.EqualTo("é"));
        }
    }
}
