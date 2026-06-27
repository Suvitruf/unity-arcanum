using System.Collections.Generic;
using System.Text;

namespace Arcanum.Formats.Dialog
{
    /// <summary>
    /// Parses an Arcanum <c>.dlg</c> (Latin-1 text, one record per line) into a <see cref="DialogScript"/>.
    /// Each record is <c>{num}{female}{male}{iq}{test}{goto}{effect}</c>; non-record lines are ignored.
    /// </summary>
    public static class DlgReader
    {
        public static DialogScript Read(byte[] bytes)
        {
            string text = Latin1(bytes);
            var lines = new SortedDictionary<int, DialogLine>();

            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] != '{') continue;

                string[] f = SplitBraceFields(line);
                if (f.Length < 7) continue;
                if (!int.TryParse(f[0].Trim(), out int num)) continue;

                int.TryParse(f[3].Trim(), out int iq);
                int.TryParse(f[5].Trim(), out int target);
                lines[num] = new DialogLine(num, f[1], f[2], iq, f[4], target, f[6]);
            }

            return new DialogScript(lines);
        }

        // Byte-for-byte Latin-1: every byte maps to the same code point. Avoids encoding-name lookups.
        private static string Latin1(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length);
            for (int i = 0; i < bytes.Length; i++) sb.Append((char)bytes[i]);
            return sb.ToString();
        }

        // Split "{a}{b}{c}…" into [a, b, c, …]. Dialog text never contains braces, so a plain scan is safe.
        private static string[] SplitBraceFields(string line)
        {
            var fields = new List<string>();
            int i = 0;
            while (i < line.Length && line[i] == '{')
            {
                int end = line.IndexOf('}', i + 1);
                if (end < 0) break;
                fields.Add(line.Substring(i + 1, end - i - 1));
                i = end + 1;
            }
            return fields.ToArray();
        }
    }
}
