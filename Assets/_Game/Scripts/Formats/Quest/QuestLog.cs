using System;
using System.Collections.Generic;
using Arcanum.Formats.Text;

namespace Arcanum.Formats.Quest
{
    /// <summary>The engine quest states (quest.h <c>QuestState</c>), in order — the per-PC progress of a quest.</summary>
    public enum QuestState
    {
        Unknown = 0,
        Mentioned = 1,
        Accepted = 2,
        Achieved = 3,
        Completed = 4,
        OtherCompleted = 5,
        Botched = 6,
    }

    /// <summary>The three dialog-entry banks the <c>Q:</c> generated-dialog operator picks from (engine
    /// <c>quest_parse</c>): normal, bad-reaction (PC reaction ≤ 20), and dumb (PC intelligence ≤ 4).</summary>
    public enum DialogBank
    {
        Normal = 0,
        BadReaction = 1,
        Dumb = 2,
    }

    /// <summary>Per-quest metadata from <c>rules/gamequest.mes</c> (engine <c>Quest</c> struct, quest.c:19).</summary>
    public sealed class QuestInfo
    {
        /// <summary>Index into <c>xp_quest.mes</c> for the XP awarded on completion (engine <c>quest_get_xp</c>).</summary>
        public int ExperienceLevel;

        /// <summary>Alignment shift applied to the PC when the quest completes (engine <c>quest_state_set</c>).</summary>
        public int AlignmentAdjustment;

        /// <summary>Per-state <c>Q:</c> dialog entry-point lines (−1 = none), one bank each. Index by
        /// <see cref="QuestState"/>.</summary>
        public readonly int[] NormalDialog = new int[QuestLog.QuestStateCount];

        public readonly int[] BadReactionDialog = new int[QuestLog.QuestStateCount];
        public readonly int[] DumbDialog = new int[QuestLog.QuestStateCount];
    }

    /// <summary>
    /// Quest data, parsed from the shipped files (engine quest.c): per-quest <b>metadata</b> from
    /// <c>rules/gamequest.mes</c> (XP level, alignment shift, the <c>Q:</c> dialog entry banks), the journal
    /// <b>descriptions</b> from <c>mes/gamequestlog.mes</c> (+ low-INT variants from <c>gamequestlogdumb.mes</c>),
    /// and the XP table from <c>rules/xp_quest.mes</c>.
    ///
    /// <para>The per-PC quest <b>state</b> lives on the character/save (Runtime). The <b>botched modifier</b>
    /// (<see cref="BotchedModifier"/>) is OR'd onto a state to mark it un-completable while preserving the
    /// underlying state — use <see cref="StripBotched"/>/<see cref="IsBotched"/>. NOTE: the engine's state
    /// <i>transition rules</i> (global↔PC clamping, completion XP/alignment side-effects, quest.c:398) are applied
    /// by the Runtime quest adapters, not here — this is the data layer.</para>
    /// </summary>
    public sealed class QuestLog
    {
        public const int QuestStateCount = 7;                   // QUEST_STATE_COUNT (Unknown..Botched)
        public const int BotchedModifier = 0x100;               // QUEST_BOTCHED_MODIFIER — OR'd onto a botched state (quest.c:15)
        private const int QuestNumLo = 1000, QuestNumHi = 2000; // quest number range (gamequest/gamequestlog keys)

        private readonly Dictionary<int, string> _desc = new Dictionary<int, string>();
        private readonly Dictionary<int, string> _dumbDesc = new Dictionary<int, string>();
        private readonly Dictionary<int, int> _xp = new Dictionary<int, int>();
        private readonly Dictionary<int, QuestInfo> _meta = new Dictionary<int, QuestInfo>();

        /// <summary>Builds from the quest data files. <paramref name="descriptions"/> = <c>gamequestlog.mes</c>;
        /// <paramref name="xpQuest"/> = <c>rules/xp_quest.mes</c> (XP-level → amount); <paramref name="gameQuest"/>
        /// = <c>rules/gamequest.mes</c> (per-quest metadata); <paramref name="dumbDescriptions"/> =
        /// <c>gamequestlogdumb.mes</c> (low-INT journal text).</summary>
        public static QuestLog FromMes(MesFile descriptions, MesFile xpQuest = null,
                                       MesFile gameQuest = null, MesFile dumbDescriptions = null)
        {
            var log = new QuestLog();
            FillText(log._desc, descriptions);
            FillText(log._dumbDesc, dumbDescriptions);

            if (xpQuest != null)
                for (int lvl = 0; lvl < 100; lvl++) // xp_quest.mes is keyed by small XP-level numbers
                {
                    string s = xpQuest.Get(lvl);
                    if (!string.IsNullOrEmpty(s) && int.TryParse(s.Trim(), out int xp)) log._xp[lvl] = xp;
                }

            if (gameQuest != null)
                for (int num = QuestNumLo; num < QuestNumHi; num++)
                {
                    QuestInfo q = ParseQuest(gameQuest.Get(num));
                    if (q != null) log._meta[num] = q;
                }

            return log;
        }

        private static void FillText(Dictionary<int, string> into, MesFile mes)
        {
            if (mes == null) return;
            for (int num = QuestNumLo; num < QuestNumHi; num++)
            {
                string s = mes.Get(num);
                if (!string.IsNullOrEmpty(s)) into[num] = s;
            }
        }

        // gamequest.mes entry = "expLevel alignAdj  <7 normal>  <7 badReaction>  <7 dumb>" (engine quest_parse).
        private static QuestInfo ParseQuest(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string[] tok = value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (tok.Length < 2) return null;

            var q = new QuestInfo { ExperienceLevel = Tok(tok, 0), AlignmentAdjustment = Tok(tok, 1) };
            for (int i = 0; i < QuestStateCount; i++)
            {
                q.NormalDialog[i] = Tok(tok, 2 + i);
                q.BadReactionDialog[i] = Tok(tok, 2 + QuestStateCount + i);
                q.DumbDialog[i] = Tok(tok, 2 + 2 * QuestStateCount + i);
            }

            return q;
        }

        private static int Tok(string[] tok, int i) => i < tok.Length && int.TryParse(tok[i], out int v) ? v : 0;

        /// <summary>The journal description for a quest number, or null. Pass <paramref name="dumb"/> for the
        /// low-INT (≤ 4) variant from <c>gamequestlogdumb.mes</c> (falls back to the normal text if absent).</summary>
        public string Description(int num, bool dumb = false)
        {
            if (dumb && _dumbDesc.TryGetValue(num, out string d)) return d;
            return _desc.GetValueOrDefault(num);
        }

        /// <summary>XP awarded for an <c>xp</c> effect's level argument (engine <c>quest_get_xp</c>), or 0.</summary>
        public int Xp(int level) => _xp.GetValueOrDefault(level, 0);

        /// <summary>Per-quest metadata (XP level, alignment shift, dialog banks), or null if undefined.</summary>
        public QuestInfo Meta(int num) => _meta.GetValueOrDefault(num);

        /// <summary>XP a quest grants on completion — <c>xp_quest.mes[experience_level]</c> (engine
        /// <c>quest_get_xp(quest.experience_level)</c>); 0 if the quest/level is unknown.</summary>
        public int QuestXp(int num) => _meta.TryGetValue(num, out QuestInfo q) ? Xp(q.ExperienceLevel) : 0;

        /// <summary>Alignment shift applied to the PC when the quest completes; 0 if unknown.</summary>
        public int AlignmentAdjustment(int num) => _meta.TryGetValue(num, out QuestInfo q) ? q.AlignmentAdjustment : 0;

        /// <summary>The <c>Q:</c> generated-dialog entry-point line for a quest in a given state + reaction bank,
        /// or −1 (no line / unknown quest).</summary>
        public int DialogEntry(int num, QuestState state, DialogBank bank = DialogBank.Normal)
        {
            if (!_meta.TryGetValue(num, out QuestInfo q)) return -1;
            int s = (int)state;
            if (s < 0 || s >= QuestStateCount) return -1;
            return bank switch
            {
                DialogBank.BadReaction => q.BadReactionDialog[s],
                DialogBank.Dumb => q.DumbDialog[s],
                _ => q.NormalDialog[s],
            };
        }

        public int Count => _desc.Count;

        /// <summary>All quest numbers that have a description, ascending (for seeding/inspecting real quests).</summary>
        public IEnumerable<int> Numbers
        {
            get
            {
                var nums = new List<int>(_desc.Keys);
                nums.Sort();
                return nums;
            }
        }

        // --- Botched modifier (engine quest_state_get/set) ---

        /// <summary>Strips the botched bit, returning the underlying state (engine <c>quest_state_get</c>).</summary>
        public static QuestState StripBotched(int rawState) => (QuestState)(rawState & ~BotchedModifier);

        /// <summary>True if a raw state carries the botched modifier (the quest can no longer be completed).</summary>
        public static bool IsBotched(int rawState) => (rawState & BotchedModifier) != 0;

        /// <summary>OR the botched modifier onto a state, preserving the underlying state (engine
        /// <c>quest_state_set</c> on botch).</summary>
        public static int WithBotched(int rawState) => rawState | BotchedModifier;

        /// <summary>A quest appears in the journal once its (de-botched) state is at least
        /// <see cref="QuestState.Mentioned"/>.</summary>
        public static bool IsKnown(int state) => StripBotched(state) >= QuestState.Mentioned;

        public static string Label(QuestState state) => state switch
        {
            QuestState.Mentioned => "Mentioned",
            QuestState.Accepted => "Accepted",
            QuestState.Achieved => "Achieved",
            QuestState.Completed => "Completed",
            QuestState.OtherCompleted => "Completed (by another)",
            QuestState.Botched => "Botched",
            _ => "",
        };
    }
}
