using System;
using System.Text;

namespace Arcanum.Formats.Dialog
{
    /// <summary>
    /// Expands the inline name codes in dialog text — a port of engine dialog.c <c>sub_416B00</c>. Codes are
    /// delimited by <c>@</c>: <c>@pcname@</c> → the player's name, <c>@npcname@</c> → the speaking NPC's name.
    /// An unknown <c>@code@</c> is dropped (matching the engine); an unterminated <c>@</c> is left literal.
    /// </summary>
    public static class DialogText
    {
        public static string Expand(string raw, IDialogContext ctx)
        {
            if (string.IsNullOrEmpty(raw) || raw.IndexOf('@') < 0) return raw;

            var sb = new StringBuilder(raw.Length);
            int i = 0;
            while (i < raw.Length)
            {
                int at = raw.IndexOf('@', i);
                if (at < 0) { sb.Append(raw, i, raw.Length - i); break; }
                sb.Append(raw, i, at - i);                       // text before the '@'

                int end = raw.IndexOf('@', at + 1);
                if (end < 0) { sb.Append(raw, at, raw.Length - at); break; } // unterminated → leave literal

                string code = raw.Substring(at + 1, end - at - 1);
                if (string.Equals(code, "pcname", StringComparison.OrdinalIgnoreCase)) sb.Append(ctx?.PcName);
                else if (string.Equals(code, "npcname", StringComparison.OrdinalIgnoreCase)) sb.Append(ctx?.NpcName);
                // unknown code → append nothing (the engine strips it)
                i = end + 1;
            }
            return sb.ToString();
        }
    }
}
