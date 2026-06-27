using System.Collections.Generic;

namespace Arcanum.Formats.Dialog
{
    /// <summary>
    /// Runtime state of one conversation: the current NPC line plus the player options reachable from it.
    /// The UI shows <see cref="NpcText"/> and <see cref="Options"/>, then calls <see cref="Pick"/> with the
    /// chosen option; the conversation ends when an option has no valid target.
    /// </summary>
    public sealed class DialogConversation
    {
        private readonly DialogScript _script;
        private readonly IDialogContext _ctx;

        public int CurrentLine { get; private set; }
        public int PlayerIq { get; }

        public DialogConversation(DialogScript script, int startLine = 1, int playerIq = 20, IDialogContext ctx = null)
        {
            _script = script;
            CurrentLine = startLine;
            PlayerIq = playerIq;
            _ctx = ctx;
        }

        /// <summary>The current NPC line's spoken text (gender variant + <c>@name@</c> codes expanded), or null.</summary>
        public string NpcText => _script != null && _script.TryGet(CurrentLine, out DialogLine l)
            ? DialogText.Expand(l.NpcSpeech(_ctx?.PcIsMale ?? true), _ctx) : null;

        /// <summary>A player option's display text with <c>@name@</c> codes expanded.</summary>
        public string OptionText(DialogLine option) => DialogText.Expand(option.Text, _ctx);

        /// <summary>The player options available at the current NPC line (script tests applied).</summary>
        public List<DialogLine> Options()
            => _script != null ? _script.OptionsFor(CurrentLine, PlayerIq, _ctx) : new List<DialogLine>();

        /// <summary>
        /// Run a chosen option's effect, then advance to its target (an <c>fl</c> effect overrides the
        /// target). Returns false when the conversation ends (no valid next line).
        /// </summary>
        public bool Pick(DialogLine option)
        {
            if (_script == null) return false;

            DialogScriptEvaluator.RunEffect(option.Effect, _ctx, out int gotoOverride);
            int next = gotoOverride >= 0 ? gotoOverride : option.Target;

            if (next == 0 || !_script.TryGet(next, out _)) return false;
            CurrentLine = next;
            return true;
        }
    }
}
