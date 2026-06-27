using System.Collections.Generic;

namespace Arcanum.Formats.Dialog
{
    /// <summary>
    /// Evaluates a <c>.dlg</c> line's <c>test</c> (a condition guarding an option) and runs its <c>effect</c>
    /// (consequences when chosen). Ports the engine's runtime mini-language (dialog.c): the string is a list
    /// of 2-letter codes each followed by 1–2 integers; for a test ALL conditions must pass (AND). Comparative
    /// codes use the sign convention: positive arg = "actual ≥ arg", negative = "actual ≤ |arg|".
    ///
    /// Codes whose game state we don't model yet degrade gracefully: an unknown TEST passes (so options are
    /// never wrongly hidden and dialog can't dead-end) and an unknown EFFECT is a no-op. Both are reported
    /// through <see cref="OnUnsupported"/> for visibility.
    /// </summary>
    public static class DialogScriptEvaluator
    {
        /// <summary>Optional sink for unsupported codes (code, arg) — e.g. a debug log. Null = silent.</summary>
        public static System.Action<string, int> OnUnsupported;

        private const int BasicSkillCount = 12; // BASIC_SKILL_COUNT — a `sk` value ≥ this is a tech skill

        public static bool TestPasses(string test, IDialogContext ctx)
        {
            if (string.IsNullOrEmpty(test) || ctx == null) return true;
            foreach (Token t in Tokenize(test))
                if (!EvalTest(t, ctx))
                    return false;
            return true;
        }

        /// <summary>Runs the effect. If it contains an <c>fl</c> (goto), returns that line via
        /// <paramref name="gotoOverride"/> (else -1) so the conversation can jump there.</summary>
        public static void RunEffect(string effect, IDialogContext ctx, out int gotoOverride)
        {
            gotoOverride = -1;
            if (string.IsNullOrEmpty(effect) || ctx == null) return;
            foreach (Token t in Tokenize(effect))
                RunEffect(t, ctx, ref gotoOverride);
        }

        private static bool EvalTest(Token t, IDialogContext ctx)
        {
            switch (t.Code)
            {
                case "ps": return Cmp(ctx.PersuasionSkill, t.A);
                case "ha": return Cmp(ctx.HaggleSkill, t.A);
                case "ch": return Cmp(ctx.Charisma, t.A);
                case "pe": return Cmp(ctx.Perception, t.A);
                case "le": return Cmp(ctx.Level, t.A);
                case "re": return Cmp(ctx.NpcReaction, t.A);
                case "$$": return Cmp(ctx.Gold, t.A);

                // Race gate (engine DIALOG_COND_RA, STAT_RACE): +N → must be race N−1, −N → must not be.
                case "ra": return t.A > 0 ? ctx.PcRace + 1 == t.A : ctx.PcRace + 1 != -t.A;
                case "al": return Cmp(ctx.Alignment, t.A);                                     // alignment ≥/≤
                case "ss": return Cmp(ctx.StoryState, t.A);                                    // story state ≥/≤
                case "ru": return t.A > 0 ? ctx.RumorKnown(t.A) : !ctx.RumorKnown(-t.A);       // know / not-know rumor
                case "rp": return t.A > 0 ? ctx.HasReputation(t.A) : !ctx.HasReputation(-t.A); // have / not-have rep
                case "me": return t.A == 0 ? !ctx.HasMetNpc : (t.A != 1 || ctx.HasMetNpc);     // met-before gate

                case "gf": return ctx.GlobalFlag(t.A) == t.B;
                case "gv": return ctx.GlobalVar(t.A) == t.B;
                case "pf": return ctx.PcFlag(t.A) == t.B;
                case "pv": return ctx.PcVar(t.A) == t.B;
                case "lf": return ctx.LocalFlag(t.A) == t.B;
                case "lc": return ctx.LocalCounter(t.A) == t.B;
                case "qu": return ctx.Quest(t.A) == t.B;
                case "qb": return ctx.Quest(t.A) <= t.B;
                case "qa": return ctx.Quest(t.A) >= t.B;

                case "in": return t.A >= 0 ? ctx.HasItem(t.A, true) : ctx.HasItem(-t.A, false);
                case "ni": return t.A >= 0 ? !ctx.HasItem(t.A, true) : !ctx.HasItem(-t.A, false);

                // Skill level (engine DIALOG_COND_SK): A = skill value (basic if < BASIC_SKILL_COUNT, else tech),
                // B = required level (sign convention via Cmp).
                case "sk":
                    return t.A < BasicSkillCount
                        ? Cmp(ctx.BasicSkillLevel(t.A), t.B)
                        : Cmp(ctx.TechSkillLevel(t.A - BasicSkillCount), t.B);
                case "fo": return t.A == 0 ? ctx.IsNpcFollowingPc : !ctx.IsNpcFollowingPc; // 0 = follows PC, 1 = doesn't
                case "na": return t.A < 0 ? ctx.Alignment <= t.A : ctx.Alignment >= -t.A;  // alignment, sign-reversed vs `al`
                case "ar": return t.A > 0 ? ctx.AreaKnown(t.A) : !ctx.AreaKnown(-t.A);     // area is / isn't known

                default:
                    OnUnsupported?.Invoke(t.Code, t.A);
                    return true; // permissive: don't hide an option we can't evaluate
            }
        }

        private static void RunEffect(Token t, IDialogContext ctx, ref int gotoOverride)
        {
            switch (t.Code)
            {
                case "$$": ctx.Gold += t.A; break;                                                    // +give / −take
                case "re": ApplyMode(t, ctx.NpcReaction, ctx.AdjustReaction, ctx.SetReaction); break; // reaction
                case "al": ApplyMode(t, ctx.Alignment, ctx.AdjustAlignment, ctx.SetAlignment); break; // alignment
                case "fl": gotoOverride = t.A; break;                                                 // jump to a line
                case "co": ctx.StartCombat(); break;
                case "fp": ctx.GiveFatePoint(); break;
                case "xp": ctx.GiveXp(t.A); break;        // quest XP (host: quest_get_xp → critter_give_xp)
                case "jo": ctx.RecruitNpc(); break;       // NPC joins the party
                case "nk": ctx.KillNpc(); break;          // kill the speaking NPC
                case "ss": ctx.SetStoryState(t.A); break; // global story milestone
                case "ru": ctx.SetRumorKnown(t.A); break; // learn a rumor
                case "mm": ctx.MarkAreaKnown(t.A); break; // reveal a world-map area
                case "rp":
                    if (t.A > 0) ctx.AddReputation(t.A);
                    else ctx.RemoveReputation(-t.A);
                    break;



                case "gf": ctx.SetGlobalFlag(t.A, t.B); break;
                case "gv": ctx.SetGlobalVar(t.A, t.B); break;
                case "pf": ctx.SetPcFlag(t.A, t.B); break;
                case "pv": ctx.SetPcVar(t.A, t.B); break;
                case "lf": ctx.SetLocalFlag(t.A, t.B); break;
                case "lc": ctx.SetLocalCounter(t.A, t.B); break;
                case "qu": ctx.SetQuest(t.A, t.B); break;

                case "in": ctx.TransferItem(t.A >= 0 ? t.A : -t.A, t.A >= 0); break; // +PC→NPC / −NPC→PC
                case "lv": ctx.DisbandNpc(); break;                                  // NPC leaves the party (critter_disband)

                default: OnUnsupported?.Invoke(t.Code, t.A); break; // no-op
            }
        }

        // positive arg → actual ≥ arg; negative arg → actual ≤ |arg| (engine sign convention).
        private static bool Cmp(int actual, int arg) => arg >= 0 ? actual >= arg : actual <= -arg;

        // `al`/`re` operator semantics: +/− adjust by (signed) value, >/< clamp-set toward value, else set.
        private static void ApplyMode(Token t, int current, System.Action<int> adjust, System.Action<int> set)
        {
            switch (t.Op)
            {
                case '+':
                case '-': adjust(t.A); break;
                case '>':
                    if (current < t.A) set(t.A);
                    break;
                case '<':
                    if (current > t.A) set(t.A);
                    break;
                default: set(t.A); break;
            }
        }

        private readonly struct Token
        {
            public readonly string Code;
            public readonly int A;
            public readonly int B;
            public readonly char Op;

            public Token(string code, int a, int b, char op)
            {
                Code = code;
                A = a;
                B = b;
                Op = op;
            }
        }

        // Scan code (2 chars, alpha or '$'), an optional operator (+ > <, or '-' as a sign), then 1–2 ints.
        private static IEnumerable<Token> Tokenize(string s)
        {
            int i = 0, n = s.Length;
            while (i < n)
            {
                while (i < n && !IsCodeChar(s[i])) i++; // skip to a code start
                if (i + 1 >= n) yield break;
                string code = s.Substring(i, 2);
                i += 2;
                char op = '='; // '=' set, '+'/'-' adjust, '>'/'<' clamp-set (used by `al`/`re`)
                int j = i;
                while (j < n && s[j] == ' ') j++;
                if (j < n && (s[j] == '+' || s[j] == '>' || s[j] == '<' || s[j] == '-')) op = s[j];
                int a = ReadInt(s, ref i, out bool gotA); // ReadInt skips +/>/< and consumes a leading '-'
                int b = ReadInt(s, ref i, out _);
                yield return new Token(code, gotA ? a : 0, b, op);
            }
        }

        private static bool IsCodeChar(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '$';

        // Reads an optional signed integer at i (skipping leading spaces/punctuation but NOT letters, which
        // start the next code). Advances i past it; sets got=false if no number was found before a code/end.
        private static int ReadInt(string s, ref int i, out bool got)
        {
            int n = s.Length;
            while (i < n && !char.IsDigit(s[i]) && s[i] != '-')
            {
                if (IsCodeChar(s[i]))
                {
                    got = false;
                    return 0;
                } // next code reached → no number here

                i++;
            }

            if (i >= n)
            {
                got = false;
                return 0;
            }

            int sign = 1;
            if (s[i] == '-')
            {
                sign = -1;
                i++;
            }

            int v = 0;
            bool any = false;
            while (i < n && char.IsDigit(s[i]))
            {
                v = v * 10 + (s[i] - '0');
                i++;
                any = true;
            }

            got = any;
            return any ? sign * v : 0;
        }
    }
}
