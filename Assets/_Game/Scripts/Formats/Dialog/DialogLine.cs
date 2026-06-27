namespace Arcanum.Formats.Dialog
{
    /// <summary>
    /// One record of an Arcanum <c>.dlg</c>: <c>{num}{text1}{text2}{iq}{test}{goto}{effect}</c>.
    /// The line's role is decided by <c>iq</c> (engine dialog.c: blank/0 = an NPC speech line, non-zero = a
    /// player option). For an NPC line, <c>text1</c> is what's said to a male PC and <c>text2</c> the variant
    /// for a female PC (the NPC addresses you by your gender; blank ⇒ reuse text1). For a player option,
    /// <c>text1</c> is the option text and <c>text2</c> is a gender gate (a number; blank = any).
    /// </summary>
    public readonly struct DialogLine
    {
        public readonly int Num;
        public readonly string Text;   // text1: NPC speech (male-PC variant) OR the player-option text
        public readonly string Text2;  // text2: NPC speech female-PC variant, OR a player-option gender gate
        public readonly int Iq;        // intelligence gate: >0 = minimum, <0 = maximum |iq|, 0 = an NPC line
        public readonly string Test;   // script condition (empty = always available)
        public readonly int Target;    // line to go to when chosen (0 = end of conversation)
        public readonly string Effect; // script action run when chosen (empty = none)

        public DialogLine(int num, string text, string text2, int iq, string test, int target, string effect)
        {
            Num = num;
            Text = text;
            Text2 = text2;
            Iq = iq;
            Test = test;
            Target = target;
            Effect = effect;
        }

        /// <summary>An NPC speech line (vs a player option) — engine rule: the IQ field is blank/zero.</summary>
        public bool IsNpcSpeech => Iq == 0;

        /// <summary>NPC speech addressed to the PC's gender: text1 for a male PC, text2 for a female PC
        /// (falling back to text1 when the female variant is blank). Ports dialog.c's <c>female_str</c> pick.</summary>
        public string NpcSpeech(bool pcMale)
            => pcMale || string.IsNullOrWhiteSpace(Text2) ? Text : Text2;

        /// <summary>A player option's gender gate — the raw <c>STAT_GENDER</c> value: 0 = male-only, 1 = female-only,
        /// -1 = any (blank). Only meaningful for option lines.</summary>
        public int OptionGender
            => Iq != 0 && !string.IsNullOrWhiteSpace(Text2) && int.TryParse(Text2.Trim(), out int g) ? g : -1;
    }
}
