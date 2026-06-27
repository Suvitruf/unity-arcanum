using System.Collections.Generic;

namespace Arcanum.Formats.Dialog
{
    /// <summary>
    /// A parsed Arcanum dialog (one <c>.dlg</c>): all lines keyed by number, with the logic to gather the
    /// player options that follow an NPC speech line. The conversation walks it: show an NPC line's text,
    /// list <see cref="OptionsFor"/>, and on a pick jump to that option's <see cref="DialogLine.Target"/>.
    /// </summary>
    public sealed class DialogScript
    {
        private readonly SortedDictionary<int, DialogLine> _lines;

        public DialogScript(SortedDictionary<int, DialogLine> lines) => _lines = lines;

        public bool TryGet(int num, out DialogLine line) => _lines.TryGetValue(num, out line);

        public IReadOnlyCollection<int> LineNumbers => _lines.Keys;

        /// <summary>
        /// The player options that follow an NPC speech line: the consecutive option lines after
        /// <paramref name="npcLine"/> up to the next NPC speech line, skipping engine markers (B:/E:/R:…)
        /// and lines the player's intelligence doesn't satisfy.
        /// </summary>
        public List<DialogLine> OptionsFor(int npcLine, int playerIq = 20, IDialogContext ctx = null)
        {
            var options = new List<DialogLine>();
            bool past = false;
            foreach (KeyValuePair<int, DialogLine> kv in _lines)
            {
                if (!past)
                {
                    if (kv.Key == npcLine) past = true;
                    continue; // skip everything up to and including the NPC line itself
                }

                DialogLine l = kv.Value;
                if (l.IsNpcSpeech) break;       // reached the next NPC line → this block of options ends
                if (IsMarker(l.Text)) continue; // engine directive, not a player-facing option
                if (!IqAllows(l.Iq, playerIq)) continue;
                // gender gate: the option's gender field is STAT_GENDER (0=male, 1=female); show only if it matches.
                if (ctx != null && l.OptionGender != -1 && l.OptionGender != (ctx.PcIsMale ? 0 : 1)) continue;
                if (!DialogScriptEvaluator.TestPasses(l.Test, ctx)) continue; // script condition gate
                options.Add(l);
            }

            return options;
        }

        // Engine markers are "X:" prefixed (B: barter, E: end, R: reaction-gated, …) — not spoken lines.
        private static bool IsMarker(string text)
            => !string.IsNullOrEmpty(text) && text.Length >= 2 && char.IsLetter(text[0]) && text[1] == ':';

        private static bool IqAllows(int iq, int playerIq)
            => iq == 0 || (iq > 0 ? playerIq >= iq : playerIq <= -iq);
    }
}
