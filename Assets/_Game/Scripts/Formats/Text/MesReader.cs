using System.Collections.Generic;

namespace Arcanum.Formats.Text
{
    /// <summary>
    /// Parses Arcanum <c>.mes</c> files. The grammar is a flat sequence of
    /// brace-delimited fields; everything outside braces (whitespace, <c>//</c>
    /// comments) is ignored. Fields pair up as <c>{key}{value}</c>, where the key
    /// is an integer. Mirrors <c>parse_field</c>/<c>parse_entry</c> in
    /// alexbatalov/arcanum-ce <c>mes.c</c>. There are <b>no</b> escapes, format tokens or line continuations in
    /// the format — a value is the literal text between its braces (embedded newlines included).
    ///
    /// <para>Key handling: a pair whose key isn't a (trimmed) integer is <b>skipped</b>. The engine instead
    /// <c>atoi</c>s the key (so non-numeric → key 0) and keeps it with a warning (<c>mes.c:526</c>); we don't
    /// inject a spurious key-0 entry. Shipped files always use clean integer keys, so the two agree on real data.</para>
    /// </summary>
    public static class MesReader
    {
        public static MesFile Read(byte[] bytes) => Read(DecodeLatin1(bytes));

        public static MesFile Read(string text)
        {
            var fields = ExtractBraceFields(text);

            var entries = new List<KeyValuePair<int, string>>(fields.Count / 2);
            for (int i = 0; i + 1 < fields.Count; i += 2)
            {
                if (int.TryParse(fields[i].Trim(), out int key))
                    entries.Add(new KeyValuePair<int, string>(key, fields[i + 1]));
            }

            return new MesFile(entries);
        }

        private static List<string> ExtractBraceFields(string text)
        {
            var fields = new List<string>();
            int i = 0, n = text.Length;
            while (true)
            {
                while (i < n && text[i] != '{') i++;
                if (i >= n) break;
                i++; // consume '{'

                int start = i;
                while (i < n && text[i] != '}') i++;
                if (i >= n) break; // unterminated final field — discard

                fields.Add(text.Substring(start, i - start));
                i++; // consume '}'
            }

            return fields;
        }

        /// <summary>Arcanum text is single-byte (Windows-1252); decode bytes as Latin-1.</summary>
        private static string DecodeLatin1(byte[] bytes)
        {
            var chars = new char[bytes.Length];
            for (int i = 0; i < bytes.Length; i++) chars[i] = (char)bytes[i];
            return new string(chars);
        }
    }
}
