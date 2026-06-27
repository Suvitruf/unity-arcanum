using System.Collections.Generic;
using Arcanum.Formats.Dialog;
using NUnit.Framework;

namespace Arcanum.Formats.Tests
{
    /// <summary>
    /// Pins the <c>.dlg</c> parsing + conversation logic: the engine's IQ-decides-role rule (blank/0 = NPC
    /// line, non-zero = player option), gender variants/gates, IQ gates, <c>@name@</c> expansion, and the
    /// regression where a gender-gated option (text2 = a number) was mis-read as NPC speech and truncated the
    /// option list. Pure-format tests over a synthetic dialog; see the DialogLine / DialogScript docs.
    /// </summary>
    public sealed class DialogFormatTests
    {
        // {num}{text1}{text2}{iq}{test}{goto}{effect}. Line 1 = NPC greeting (gender variants); 2–4 = options
        // (any / female-only / IQ≥15); 5 = NPC reply (no female variant → falls back to text1); 6 = end option.
        private const string Dlg =
            "{1}{Hello, sir.}{Hello, madam.}{}{}{0}{}\n" +
            "{2}{Tell me about @npcname@.}{}{1}{}{5}{}\n" +
            "{3}{A woman's question.}{1}{1}{}{5}{}\n" + // gender 1 = female-only (STAT_GENDER)
            "{4}{A clever question.}{}{15}{}{5}{}\n" +
            "{5}{I am @pcname@'s merchant.}{}{}{}{0}{}\n" +
            "{6}{Goodbye.}{}{1}{}{0}{}\n";

        private static DialogScript Parse() => DlgReader.Read(Latin1(Dlg));

        [Test]
        public void IqDecidesNpcVsOption()
        {
            DialogScript s = Parse();
            Assert.That(s.TryGet(1, out DialogLine npc) && npc.IsNpcSpeech, Is.True, "iq blank → NPC line");
            Assert.That(s.TryGet(2, out DialogLine opt) && !opt.IsNpcSpeech, Is.True, "iq non-zero → player option");
        }

        [Test]
        public void GenderGatedOptionDoesNotTruncateList() // the bug: a numeric text2 was read as NPC speech → break
        {
            var male = new Ctx { Male = true };
            List<DialogLine> options = new DialogConversation(Parse(), 1, 20, male).Options();
            CollectionAssert.Contains(Texts(options), "A clever question.",
                "the IQ≥15 option after the female-gated one must still appear");
            CollectionAssert.DoesNotContain(Texts(options), "A woman's question.", "female-only option hidden for a male PC");
        }

        [Test]
        public void OptionsRespectGenderAndIqGates()
        {
            // Female, smart: any + female-only + IQ option all show.
            var femaleSmart = new DialogConversation(Parse(), 1, 20, new Ctx { Male = false }).Options();
            CollectionAssert.AreEquivalent(
                new[] { "Tell me about Merchant.", "A woman's question.", "A clever question." }, Texts(femaleSmart));

            // Male, dull: female-only gated out by gender, clever gated out by IQ.
            var maleDull = new DialogConversation(Parse(), 1, 10, new Ctx { Male = true }).Options();
            CollectionAssert.AreEquivalent(new[] { "Tell me about Merchant." }, Texts(maleDull));
        }

        [Test]
        public void NpcTextPicksGenderVariantWithFallback()
        {
            Assert.That(new DialogConversation(Parse(), 1, 20, new Ctx { Male = true }).NpcText, Is.EqualTo("Hello, sir."));
            Assert.That(new DialogConversation(Parse(), 1, 20, new Ctx { Male = false }).NpcText, Is.EqualTo("Hello, madam."));
            // Line 5 has no female variant → a female PC falls back to text1 (and @pcname@ expands).
            Assert.That(new DialogConversation(Parse(), 5, 20, new Ctx { Male = false, Pc = "Hero" }).NpcText,
                Is.EqualTo("I am Hero's merchant."));
        }

        [Test]
        public void PickAdvancesToTarget()
        {
            var convo = new DialogConversation(Parse(), 1, 20, new Ctx { Male = true });
            DialogLine tell = convo.Options().Find(o => o.Num == 2);
            Assert.That(convo.Pick(tell), Is.True);
            Assert.That(convo.CurrentLine, Is.EqualTo(5));
            Assert.That(convo.NpcText, Does.Contain("merchant"));
        }

        [Test]
        public void ExpandNameCodes()
        {
            var ctx = new Ctx { Pc = "Hero", Npc = "Merchant" };
            Assert.That(DialogText.Expand("@pcname@ meets @npcname@.", ctx), Is.EqualTo("Hero meets Merchant."));
            Assert.That(DialogText.Expand("Hi @unknown@!", ctx), Is.EqualTo("Hi !"));            // unknown code stripped
            Assert.That(DialogText.Expand("price: 5@ each", ctx), Is.EqualTo("price: 5@ each")); // unterminated → literal
            Assert.That(DialogText.Expand("plain", ctx), Is.EqualTo("plain"));
            Assert.That(DialogText.Expand(null, ctx), Is.Null);
        }

        private static List<string> Texts(List<DialogLine> lines)
        {
            // Mirror the UI: option label is the expanded text1.
            var ctx = new Ctx { Pc = "Hero", Npc = "Merchant" };
            var outp = new List<string>();
            foreach (DialogLine l in lines) outp.Add(DialogText.Expand(l.Text, ctx));
            return outp;
        }

        private static byte[] Latin1(string s)
        {
            var b = new byte[s.Length];
            for (int i = 0; i < s.Length; i++) b[i] = (byte)s[i];
            return b;
        }

        [Test]
        public void GenderZeroOptionIsMaleOnly()
        {
            // {0} = male-only (STAT_GENDER 0). Verifies the corrected (previously inverted) convention.
            string dlg = "{1}{Greeting.}{}{}{}{0}{}\n" +
                         "{2}{Man talk.}{0}{1}{}{5}{}\n" + // gender 0 → male-only
                         "{5}{Bye.}{}{}{}{0}{}\n";
            DialogScript s = DlgReader.Read(Latin1(dlg));
            CollectionAssert.Contains(Texts(new DialogConversation(s, 1, 20, new Ctx { Male = true }).Options()),
                "Man talk.", "gender 0 → shown to a male PC");
            CollectionAssert.DoesNotContain(Texts(new DialogConversation(s, 1, 20, new Ctx { Male = false }).Options()),
                "Man talk.", "gender 0 → hidden from a female PC");
        }

        [Test]
        public void EvaluatesSkillFollowAlignmentAreaConditions()
        {
            var ctx = new Ctx { Alignment = -50, Following = true };
            ctx.Basic[2] = 4; // basic skill index 2
            ctx.Tech[1] = 3;  // tech skill index 1 → .dlg value 12+1 = 13
            ctx.Areas.Add(7);

            Assert.That(DialogScriptEvaluator.TestPasses("sk2 3", ctx), Is.True);  // skill 4 ≥ 3
            Assert.That(DialogScriptEvaluator.TestPasses("sk2 5", ctx), Is.False); // skill 4 < 5
            Assert.That(DialogScriptEvaluator.TestPasses("sk13 3", ctx), Is.True); // tech skill 3 ≥ 3
            Assert.That(DialogScriptEvaluator.TestPasses("fo0", ctx), Is.True);    // 0 = must follow
            Assert.That(DialogScriptEvaluator.TestPasses("fo1", ctx), Is.False);   // 1 = must not follow
            Assert.That(DialogScriptEvaluator.TestPasses("na-40", ctx), Is.True);  // align -50 ≤ -40
            Assert.That(DialogScriptEvaluator.TestPasses("na40", ctx), Is.False);  // align -50 < -40 (need ≥)
            Assert.That(DialogScriptEvaluator.TestPasses("ar7", ctx), Is.True);    // area 7 known
            Assert.That(DialogScriptEvaluator.TestPasses("ar9", ctx), Is.False);   // area 9 not known
        }

        [Test]
        public void RunsLeavePartyEffect()
        {
            var ctx = new Ctx();
            DialogScriptEvaluator.RunEffect("lv", ctx, out _);
            Assert.That(ctx.Disbanded, Is.True);
        }

        // Minimal IDialogContext for the pure tests — only gender/names matter here; the rest is permissive.
        private sealed class Ctx : IDialogContext
        {
            public bool Male;
            public string Pc = "Hero";
            public string Npc = "Merchant";

            public bool PcIsMale => Male;
            public string PcName => Pc;
            public string NpcName => Npc;
            public int PcRace => 0;

            public int Intelligence => 20;
            public int Charisma => 10;
            public int Perception => 10;
            public int Level => 1;
            public int Gold { get; set; }
            public int PersuasionSkill => 0;
            public int HaggleSkill => 0;

            public int PcFlag(int index) => 0;
            public void SetPcFlag(int index, int value) { }
            public int PcVar(int index) => 0;
            public void SetPcVar(int index, int value) { }
            public int Quest(int num) => 0;
            public void SetQuest(int num, int state) { }

            public int GlobalFlag(int index) => 0;
            public void SetGlobalFlag(int index, int value) { }
            public int GlobalVar(int index) => 0;
            public void SetGlobalVar(int index, int value) { }

            public int Alignment { get; set; }
            public void AdjustAlignment(int delta) { }
            public void SetAlignment(int value) { }
            public int StoryState => 0;
            public void SetStoryState(int value) { }
            public bool RumorKnown(int id) => false;
            public void SetRumorKnown(int id) { }
            public bool HasReputation(int id) => false;
            public void AddReputation(int id) { }
            public void RemoveReputation(int id) { }
            public void MarkAreaKnown(int id) { }
            public bool HasMetNpc => false;
            public void KillNpc() { }

            public int NpcReaction => 50;
            public void AdjustReaction(int delta) { }
            public void SetReaction(int value) { }
            public int LocalFlag(int index) => 0;
            public void SetLocalFlag(int index, int value) { }
            public int LocalCounter(int index) => 0;
            public void SetLocalCounter(int index, int value) { }

            public bool HasItem(int protoNumber, bool pcSide) => false;
            public void TransferItem(int protoNumber, bool pcToNpc) { }

            public void GiveXp(int questId) { }
            public void GiveFatePoint() { }
            public void StartCombat() { }
            public void RecruitNpc() { }

            // --- newly-implemented gates (testable) ---
            public int[] Basic = new int[12];
            public int[] Tech = new int[4];
            public bool Following;
            public bool Disbanded;
            public readonly System.Collections.Generic.HashSet<int> Areas = new System.Collections.Generic.HashSet<int>();

            public int BasicSkillLevel(int skill) => Basic[skill];
            public int TechSkillLevel(int skill) => Tech[skill];
            public bool IsNpcFollowingPc => Following;
            public bool AreaKnown(int id) => Areas.Contains(id);
            public void DisbandNpc() => Disbanded = true;
        }
    }
}
